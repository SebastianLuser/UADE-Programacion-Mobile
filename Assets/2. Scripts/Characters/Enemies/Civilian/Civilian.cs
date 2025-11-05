using UnityEngine;
using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using System.Collections.Generic;
using System.Linq;
using Services.MicroServices.BlackboardService;
using Services;
using Services.MicroServices.UpdateService;
using Unity.Assertions;
using UnityEditor;

public class Civilian : BaseCharacter, IUseFsm, IUpdateListener
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 1.5f;        // Idle drift speed
    [SerializeField] private float fleeSpeed = 7f;          // Must be > playerSpeed (5f)
    [SerializeField] private float evadeSpeed = 8f;         // Slightly higher than flee
    [SerializeField] private float pursueSpeed = 3f;        // <= guard chase (4f)

    [Header("Detection Settings")]
    [SerializeField] private float sightFOV = 120f;         // Field of view angle
    [SerializeField] private float sightRange = 6f;         // Detection range
    [SerializeField] private float meleeRange = 1.5f;       // Close contact range

    [Header("Behavior Distances & Timers")]
    [SerializeField] private float safeDistance = 10f;      // Distance to stop fleeing
    [SerializeField] private float idleSecondsAfterSafe = 3f; // Idle time after reaching safety
    [SerializeField] private float loseSightGrace = 2f;     // Grace period after losing sight

    [Header("Roulette Decision System")]
    [SerializeField] private float escapeWeight = 0.8f;     // Weight for escape path
    [SerializeField] private float attackWeight = 0.2f;     // Weight for attack path

    [Header("Attack Configuration")]
    [SerializeField] private float attackWindup = 0.35f;    // Seconds before hit
    [SerializeField] private float attackHitWin = 0.10f;    // Hit window duration
    [SerializeField] private float attackRecover = 0.35f;   // Recovery after hit
    [SerializeField] private float attackLoseSightGrace = 0.3f; // Time before aborting attack
    [SerializeField] private int meleeDamage = 1;           // Damage per melee hit
    [SerializeField] private Color attackColor = Color.red; // Visual feedback while attacking

    [Header("Steering Physics")]
    [SerializeField] private float mass = 1f;
    [SerializeField] private float maxForce = 15f;
    [SerializeField] private float slowingDistance = 2f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstaclesMask = -1;
    [SerializeField] private float avoidRadius = 1.5f;
    [SerializeField] private float avoidAngle = 90f;
    [SerializeField] private float personalArea = 0.3f;

    [Header("FSM Configuration")]
    [SerializeField] private float evadeTime = 1f;          // Duration of evade state (0.75-1.25s)
    [SerializeField] private float safeTime = 2f;           // Time to maintain safety before idle
    [SerializeField] private List<StateData> stateDataList = new List<StateData>();
    [SerializeField] private bool useFSM = true;

    [Header("Decision Tree")]
    [SerializeField] private bool useDecisionTree = true;   // Enable/disable decision tree system
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool canAttack = false;        // Civilians typically don't attack

    // --- Pathfinding (mobile-friendly) ---
    [Header("Pathfinding")]
    [SerializeField] private GraphAsset fleeGraph;
    [SerializeField] private int fleeTargetNodeIndex = -1;
    [SerializeField] private float fleeWaypointReach = 0.8f;//0.5f;
    [SerializeField] private float fleeRecomputeInterval = 1.0f;

    // Reusable Buffers for Pathfinding (init in Start)
    private float[] _astarG, _astarF;
    private int[] _astarFrom, _heapIdx, _pathIdx;
    private float[] _heapF;
    private bool[] _astarClosed;
    private Vector3[] _worldPath;
    private PathFollowerAgent _pathFollower;
    private float _lastAStarTime = -999f;
    private int _pathLen = 0;
    private int _graphCachedNodeCount = -1;


    // Component references
    private Transform player;
    private IPlayerDetector playerDetector;
    private IBlackboardService m_blackboardService;
    private Renderer meshRenderer;
    private Material originalMaterial;
    private Color originalColor;
    private CivilianDecisionTreeRunner decisionTreeRunner;

    // Steering components (identical to Guard)
    private Vector3 _vel;
    private ObstacleAvoidance obstacleAvoidance;
    private float currentMaxSpeed;

    // FSM components
    private StateMachine stateMachine;
    private float stateTimer;
    private float safeTimer; // Timer for tracking safety duration
    private float pursuitLoseSightTimer; // Timer for tracking lose sight during pursuit

    // Legacy state tracking (for compatibility)
    private CivilianState currentState = CivilianState.Idle;
    private Vector3 lastKnownPlayerPosition;
    private bool hasEverSeenPlayer = false;

    // Movement state
    private Vector3 currentMovementDirection;
    private float currentMovementSpeed;

    public enum CivilianState
    {
        Idle,
        Fleeing,
        Evading,
        Safe
    }

    #region Properties

    public float WalkSpeed => walkSpeed;
    public float FleeSpeed => fleeSpeed;
    public float EvadeSpeed => evadeSpeed;
    public float PursueSpeed => pursueSpeed;
    public float SightFOV => sightFOV;
    public float SightRange => sightRange;
    public float MeleeRange => meleeRange;
    public float SafeDistance => safeDistance;
    public float IdleSecondsAfterSafe => idleSecondsAfterSafe;
    public float EvadeTime => evadeTime;
    public float SafeTime => safeTime;
    public float EscapeWeight => escapeWeight;
    public float AttackWeight => attackWeight;
    public float AttackWindup => attackWindup;
    public float AttackHitWin => attackHitWin;
    public float AttackRecover => attackRecover;
    public float AttackLoseSightGrace => attackLoseSightGrace;
    public int MeleeDamage => meleeDamage;
    public Color AttackColor => attackColor;
    public bool CanAttack => canAttack;
    public bool EnableDebugLogs => enableDebugLogs;
    public bool UseDecisionTree => useDecisionTree;
    public Transform Player => player;
    public CivilianState CurrentState => currentState;
    public Vector3 CurrentVelocity => _vel;
    public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
    
    // FSM Properties
    public float StateTimer 
    { 
        get => stateTimer; 
        set => stateTimer = value; 
    }
    
    public float SafeTimer 
    { 
        get => safeTimer; 
        set => safeTimer = value; 
    }

    public float PursuitLoseSightTimer 
    { 
        get => pursuitLoseSightTimer; 
        set => pursuitLoseSightTimer = value; 
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        base.Awake();
        InitializeComponents();
        SubscribeUpdateService();
        TryInitFleePathfinding();
    }

    private void Start()
    {
        InitializeSteering();
        FindPlayer();
        InitializeScriptableObjectFSM();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        
        // Get or add PlayerDetector
        playerDetector = GetComponent<IPlayerDetector>();
        Assert.IsNotNull(playerDetector);

        // Get renderer for color changes during attacks
        meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null && meshRenderer.material != null)
        {
            originalMaterial = meshRenderer.material;
            originalColor = meshRenderer.material.color;
        }

        // Get blackboard service (read-only access)
        m_blackboardService = ServiceLocator.Get<IBlackboardService>();
        if (m_blackboardService == null && enableDebugLogs)
        {
            MyLogger.LogWarning($"Civilian {gameObject.name}: Blackboard service not available");
        }

        // Initialize decision tree runner if enabled
        if (useDecisionTree)
        {
            decisionTreeRunner = GetComponent<CivilianDecisionTreeRunner>();
            Assert.IsNotNull(decisionTreeRunner);
        }
    }

    private void InitializeSteering()
    {
        // Initialize steering physics (identical to Guard)
        _vel = Vector3.zero;
        obstacleAvoidance = new ObstacleAvoidance(transform, avoidRadius, avoidAngle, personalArea, obstaclesMask);
        currentMaxSpeed = walkSpeed;

        if (enableDebugLogs)
            MyLogger.LogInfo($"Civilian {gameObject.name}: Steering initialized");
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                player = playerGO.transform;
                if (enableDebugLogs)
                    MyLogger.LogInfo($"Civilian {gameObject.name}: Found player at {player.name}");
            }
        }
    }

    private void InitializeFSM()
    {
        // Legacy FSM fallback - Start in Idle state
        currentState = CivilianState.Idle;
        
        if (enableDebugLogs)
            MyLogger.LogInfo($"Civilian {gameObject.name}: Legacy FSM initialized");
    }

    private void InitializeScriptableObjectFSM()
    {
        if (useFSM && stateDataList != null && stateDataList.Count > 0)
        {
            stateMachine = new StateMachine(stateDataList, this);
            
            if (enableDebugLogs)
                MyLogger.LogInfo($"Civilian {gameObject.name}: ScriptableObject FSM initialized with {stateDataList.Count} states");
        }
        else
        {
            // Fallback to legacy FSM
            InitializeFSM();
            
            if (enableDebugLogs)
                MyLogger.LogInfo($"Civilian {gameObject.name}: Using legacy FSM (ScriptableObject FSM disabled or no states configured)");
        }
    }

    //Pathfinding
    private void TryInitFleePathfinding()
    {
        if (fleeGraph == null || fleeGraph.NodeCount <= 0) return;
        if (_astarG != null && _graphCachedNodeCount == fleeGraph.NodeCount) return; // ya listo

        AllocateFleeBuffers(fleeGraph.NodeCount);
    }

    private void AllocateFleeBuffers(int n)
    {
        _astarG = new float[n];
        _astarF = new float[n];
        _astarFrom = new int[n];
        _heapIdx = new int[n];
        _heapF = new float[n];
        _astarClosed = new bool[n];
        _pathIdx = new int[n];
        _worldPath = new Vector3[n];

        if (_pathFollower == null)
            _pathFollower = new PathFollowerAgent();

        _pathFollower.waypointReachDist = fleeWaypointReach;
        _pathFollower.slowingDistance = 1.0f;

        _graphCachedNodeCount = n;
        _pathLen = 0; // limpiar ruta previa si cambió el grafo
    }

    private void ClearFleeBuffers()
    {
        _astarG = _astarF = null;
        _astarFrom = _heapIdx = _pathIdx = null;
        _heapF = null;
        _astarClosed = null;
        _worldPath = null;
        _graphCachedNodeCount = -1;
        _pathLen = 0;
    }

    // este es el que invoca el estado: idempotente, rápido
    public void EnsureFleePathfindingInitialized()
    {
        if (fleeGraph == null || fleeGraph.NodeCount <= 0)
        {
            ClearFleeBuffers();
            return;
        }
        if (_astarG == null || _graphCachedNodeCount != fleeGraph.NodeCount)
            AllocateFleeBuffers(fleeGraph.NodeCount);
    }

    #endregion

    #region Pathfinding

    // 1) Closest node
    private int ClosestNodeIndex(Vector3 pos)
    {
        if (fleeGraph == null) return -1;
        int best = -1; float bestSq = float.PositiveInfinity;
        for (int i = 0; i < fleeGraph.NodeCount; i++)
        {
            float d = (fleeGraph.nodePositions[i] - pos).sqrMagnitude;
            if (d < bestSq) { bestSq = d; best = i; }
        }
        return best;
    }


    // 2) Syntactic sugar
    public bool HasFleeGraph =>
        fleeGraph != null && fleeGraph.NodeCount > 0 && fleeTargetNodeIndex >= 0;

    public bool HasFleePath => _pathLen > 0;

    // 3) Recompute si hace falta (intervalo o path vacío)
    /*public void RecomputeFleePathIfNeeded(float now)
    {
        if (!HasFleeGraph) { _pathLen = 0; return; }

        if (now - _lastAStarTime < fleeRecomputeInterval && _pathLen > 0)
            return; // todavía fresco

        int startIdx = ClosestNodeIndex(transform.position);
        if (startIdx < 0) { _pathLen = 0; return; }

        _pathLen = AStarNoAlloc.FindPath(
            fleeGraph, startIdx, fleeTargetNodeIndex,
            _astarG, _astarF, _astarFrom, _astarClosed,
            _heapIdx, _heapF, _pathIdx
        );

        if (_pathLen > 0)
        {
            _pathFollower.BuildWorldPath(fleeGraph, _pathIdx, _pathLen, _worldPath);
            _lastAStarTime = now;
        }
    }*/
    public void RecomputeFleePathIfNeeded(float now)
    {
        if (!HasFleeGraph) { _pathLen = 0; return; }

        // 1) Si ya tengo ruta, y estoy cerca del waypoint actual, NO recomputar
        if (_pathLen > 0 && _pathFollower != null)
        {
            int ci = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            Vector3 curWp = _worldPath[ci];
            float r = fleeWaypointReach * 1.5f; // holgura
            if ((transform.position - curWp).sqrMagnitude <= r * r)
                return; // voy bien hacia el target actual
        }

        // 2) Respeta el intervalo de recompute
        if (now - _lastAStarTime < fleeRecomputeInterval && _pathLen > 0)
            return;

        // 3) Correr A*
        int startIdx = ClosestNodeIndex(transform.position);
        if (startIdx < 0) { _pathLen = 0; return; }

        // Guardar target previo (si había ruta)
        Vector3 prevTarget = Vector3.zero;
        bool hadPath = _pathLen > 0 && _pathFollower != null;
        int prevIdx = 0;
        if (hadPath)
        {
            prevIdx = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            prevTarget = _worldPath[prevIdx];
        }

        int newLen = AStarNoAlloc.FindPath(
            fleeGraph, startIdx, fleeTargetNodeIndex,
            _astarG, _astarF, _astarFrom, _astarClosed,
            _heapIdx, _heapF, _pathIdx
        );

        if (newLen > 0)
        {
            _pathLen = _pathFollower.BuildWorldPath(fleeGraph, _pathIdx, newLen, _worldPath);
            _lastAStarTime = now;

            // 4) Preservar progreso: buscar en la ruta nueva el waypoint más cercano al target anterior
            if (hadPath)
            {
                int best = 0;
                float bestSq = float.PositiveInfinity;
                for (int i = 0; i < _pathLen; i++)
                {
                    float sq = (_worldPath[i] - prevTarget).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = i; }
                }
                _pathFollower.ReseedCursor(best);
            }
        }
    }

    // 4) Un tick de follow-path → steering deseado
    public Vector3 TickFleePathSteering()
    {
        return _pathFollower != null && _pathLen > 0
            ? _pathFollower.Tick(transform.position, CurrentVelocity, FleeSpeed, _worldPath)
            : Vector3.zero;
    }

    // 5) ¿Llegué al último waypoint?
    /*public bool FleePathReachedEnd()
    {
        if (_pathFollower == null || _pathLen <= 0) return false;
        if (!_pathFollower.ReachedEnd) return false;

        // chequeo de distancia final
        return Vector3.Distance(transform.position, _worldPath[_pathLen - 1]) <= fleeWaypointReach;
    }*/
    public bool FleePathReachedEnd()
    {
        if (_pathFollower == null || _pathLen <= 0) return false;
        if (!_pathFollower.ReachedEnd) return false; // ya estamos en el último waypoint

        // Chequeo de distancia final (sin sqrt)
        var goal = _worldPath[_pathLen - 1];
        float r2 = fleeWaypointReach * fleeWaypointReach;
        return (transform.position - goal).sqrMagnitude <= r2;
    }


    // 6) Limpiar ruta (opcional)
    public void ClearFleePath()
    {
        _pathLen = 0;
    }

    #endregion

    #region FSM Management

    /// <summary>
    /// Set the current max speed for movement
    /// </summary>
    /// <param name="speed">The new max speed</param>
    public void SetCurrentMaxSpeed(float speed)
    {
        currentMaxSpeed = speed;
    }

    /// <summary>
    /// Change material color to attack color (red)
    /// </summary>
    public void SetAttackColor()
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = attackColor;
        }
    }

    /// <summary>
    /// Restore original material color
    /// </summary>
    public void RestoreOriginalColor()
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = originalColor;
        }
    }

    /// <summary>
    /// Apply damage to player if available
    /// </summary>
    public void DealMeleeAttack()
    {
        if (player == null) return;

        // Try to get player health component
        var playerHealth = player.GetComponent<IDamageable>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(meleeDamage);
            
            if (enableDebugLogs)
                MyLogger.LogInfo($"Civilian {gameObject.name}: Dealt {meleeDamage} melee damage to player");
        }
        else
        {
            /* Todo: Que es esto?
            // Fallback: try GameStateManager
            var gameStateManager = ServiceLocator.Get<GameStateService>();
            if (gameStateManager != null)
            {
                // gameStateManager.ApplyMeleeHit(meleeDamage);
                if (enableDebugLogs)
                    MyLogger.LogInfo($"Civilian {gameObject.name}: Applied melee hit via GameStateManager");
            }*/
        }

        // Notify decision tree that damage was dealt
        if (useDecisionTree && decisionTreeRunner != null)
        {
            decisionTreeRunner.OnMeleeDamageDealt();
        }
    }

    /// <summary>
    /// Called by FSM when an attack cycle completes (for decision tree integration)
    /// </summary>
    public void OnAttackCycleComplete()
    {
        if (useDecisionTree && decisionTreeRunner != null)
        {
            decisionTreeRunner.OnAttackCycleComplete();
            
            if (enableDebugLogs)
                MyLogger.LogInfo($"Civilian {gameObject.name}: Notified DecisionTree of attack cycle completion");
        }
    }

    /// <summary>
    /// Check if pursuit should abort due to extended lose sight period
    /// This method implements the Single Responsibility pattern - 
    /// the Civilian owns the decision logic for pursuit abort
    /// </summary>
    public bool ShouldAbortPursuit()
    {
        // If we can see the player, pursuit should continue
        if (HasLoS())
        {
            return false;
        }

        // If we can't see the player, check if grace period has elapsed
        return pursuitLoseSightTimer >= attackLoseSightGrace;
    }

    /// <summary>
    /// Request a specific FSM state change (called by Decision Tree)
    /// </summary>
    public void RequestStateChange(string stateName)
    {
        if (!isAlive) return;

        if (enableDebugLogs)
            MyLogger.LogInfo($"Civilian {gameObject.name}: Decision Tree requesting state change to {stateName}");

        // Map DT suggestion to actual FSM state name
        string mappedStateName = MapDecisionTreeSuggestionToFSMState(stateName);

        // If using ScriptableObject FSM, try to change state by name
        if (useFSM && stateMachine != null)
        {
            bool stateChangeSuccess = stateMachine.ChangeStateByName(mappedStateName);
            
            if (stateChangeSuccess)
            {
                if (enableDebugLogs)
                    MyLogger.LogInfo($"Civilian {gameObject.name}: Successfully changed FSM state to {mappedStateName}");
            }
            else
            {
                if (enableDebugLogs)
                    MyLogger.LogWarning($"Civilian {gameObject.name}: Failed to find FSM state with name '{mappedStateName}'. Available states: {GetAvailableStateNames()}");
                
                // Fallback to legacy system if FSM state change fails
                RequestLegacyStateChange(stateName);
            }
        }
        else
        {
            // Use legacy state system as fallback
            RequestLegacyStateChange(stateName);
        }
    }

    /// <summary>
    /// Map Decision Tree suggestions to actual FSM state names
    /// </summary>
    private string MapDecisionTreeSuggestionToFSMState(string dtSuggestion)
    {
        switch (dtSuggestion.ToLower())
        {
            case "fleeing":
            case "flee":
                return "S_CivFlee";
                
            case "pursuing":
            case "pursue":
                return "S_CivPersuit";
                
            case "idle":
                return "S_CivIdle";
                
            case "evading":
            case "evade":
                return "S_CivEvade";
                
            case "attack":
            case "attacking":
                return "S_CivAttack";
                
            default:
                // Return original suggestion if no mapping found
                return dtSuggestion;
        }
    }

    /// <summary>
    /// Request a state change using the legacy state system
    /// </summary>
    private void RequestLegacyStateChange(string stateName)
    {
        CivilianState newState = currentState;

        switch (stateName.ToLower())
        {
            case "fleeing":
            case "flee":
                newState = CivilianState.Fleeing;
                break;
            case "evading":
            case "evade":
                newState = CivilianState.Evading;
                break;
            case "idle":
                newState = CivilianState.Idle;
                break;
            case "safe":
                newState = CivilianState.Safe;
                break;
            case "pursuing":
            case "pursue":
                // Civilians don't normally pursue, but if canAttack is true, treat as fleeing for now
                newState = canAttack ? CivilianState.Fleeing : CivilianState.Fleeing;
                break;
        }

        if (newState != currentState)
        {
            ChangeState(newState);
        }
    }

    /// <summary>
    /// Check if the decision tree system is actively influencing behavior
    /// </summary>
    public bool IsDecisionTreeActive()
    {
        return useDecisionTree && decisionTreeRunner != null && decisionTreeRunner.enabled;
    }

    /// <summary>
    /// Get available state names for debugging
    /// </summary>
    private string GetAvailableStateNames()
    {
        if (stateMachine?.GetAllStates() == null)
            return "None";

        var stateNames = stateMachine.GetAllStates()
            .Where(state => state?.State?.StateName != null)
            .Select(state => state.State.StateName)
            .ToArray();

        return stateNames.Length > 0 ? string.Join(", ", stateNames) : "None";
    }

    #endregion

    #region Legacy State System Support

    /// <summary>
    /// Change legacy state (for compatibility and fallback)
    /// </summary>
    private void ChangeState(CivilianState newState)
    {
        if (currentState != newState)
        {
            if (enableDebugLogs)
                MyLogger.LogInfo($"Civilian {gameObject.name}: {currentState} → {newState}");

            currentState = newState;
            stateTimer = 0f;

            // State-specific initialization
            switch (newState)
            {
                case CivilianState.Safe:
                    // Write to blackboard if civilian reaches safety
                    if (m_blackboardService != null)
                    {
                        // This could be used for global alert state
                        m_blackboardService.SetValue(BlackboardKeys.GLOBAL_ALERT, true);
                    }
                    break;
            }
        }
    }

    #endregion

    #region Steering Physics

    /// <summary>
    /// Integrate steering force to update velocity with mass and force limits
    /// </summary>
    private Vector3 Integrate(Vector3 steering, float dt)
    {
        // Clamp steering force to maximum
        Vector3 clampedForce = steering;
        if (clampedForce.sqrMagnitude > maxForce * maxForce)
        {
            clampedForce = clampedForce.normalized * maxForce;
        }

        // Apply force to velocity (F = ma, so a = F/m)
        Vector3 acceleration = clampedForce / mass;
        Vector3 newVel = _vel + acceleration * dt;

        // Clamp velocity to current maximum speed
        if (newVel.sqrMagnitude > currentMaxSpeed * currentMaxSpeed)
        {
            newVel = newVel.normalized * currentMaxSpeed;
        }

        return newVel;
    }

    /// <summary>
    /// Apply steering force with obstacle avoidance and movement (identical to Guard)
    /// </summary>
    /*public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Update velocity using physics integration
        _vel = Integrate(steering, Time.deltaTime);

        // 2) Pass velocity through obstacle avoidance
        Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(_vel, false);

        // 3) Move and face movement direction
        if (avoidedVel.sqrMagnitude > 0.001f)
        {
            // Update position
            Vector3 movement = avoidedVel * Time.deltaTime;
            transform.position += movement;

            // Update movement state
            currentMovementDirection = avoidedVel.normalized;
            currentMovementSpeed = avoidedVel.magnitude;

            // Face movement direction
            if (avoidedVel.magnitude > 0.1f)
            {
                Vector3 lookDirection = avoidedVel.normalized;
                lookDirection.y = 0f; // Keep rotation in XZ plane
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }*/
    /*************************************************************************public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Integrar la FUERZA de steering del PATH (Seek/Arrive) → velocidad deseada
        Vector3 desiredVel = Integrate(steering, Time.deltaTime); // tu integración ya respeta maxSpeed/force
        desiredVel.y = 0f;

        float desiredSpeed = desiredVel.magnitude;
        if (desiredSpeed <= 0.0001f)
            return;

        Vector3 desiredDir = desiredVel / Mathf.Max(desiredSpeed, 1e-5f); // dir del path

        // 2) Evitación → sacar SOLO componente lateral (sin permitir empuje hacia atrás)
        // Nota: GetDirImproved devuelve una "velocidad" corregida; usamos su DIRECCIÓN como insumo
        Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(desiredVel, false);
        Vector3 avoidDir = avoidedVel.sqrMagnitude > 1e-6f ? avoidedVel.normalized : Vector3.zero;

        // Proyectar la evitación al plano ortogonal a -desiredDir (o sea, quitar componente "hacia atrás")
        // Resultado: la evitación solo empuja lateralmente (ni frena ni invierte el rumbo)
        if (avoidDir != Vector3.zero)
        {
            // Quitar componente opuesta al camino
            avoidDir = Vector3.ProjectOnPlane(avoidDir, -desiredDir);
            if (avoidDir.sqrMagnitude > 1e-6f) avoidDir.Normalize();
        }

        // 3) Blend PATH vs AVOIDANCE
        float pathW = 1.0f;
        float avoidW = 0.0f;//0.40f; // probá 0.30–0.50 según tu nivel de obstáculos

        // Mezcla manteniendo escala de velocidades
        Vector3 blended = pathW * desiredVel + avoidW * avoidDir * currentMaxSpeed;

        // Clamp y plano XZ
        float maxV = currentMaxSpeed;
        if (blended.sqrMagnitude > maxV * maxV)
            blended = blended.normalized * maxV;
        blended.y = 0f;

        // 4) Aplicar movimiento y facing
        _vel = blended;
        if (_vel.sqrMagnitude > 0.001f)
        {
            transform.position += _vel * Time.deltaTime;
            currentMovementDirection = _vel.normalized;
            currentMovementSpeed = _vel.magnitude;

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 look = _vel.normalized; look.y = 0f;
                transform.rotation = Quaternion.LookRotation(look);
            }
        }
    }*/
    /*public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Integrar fuerzas → velocidad provisional de "path/steering"
        Vector3 desiredVel = Integrate(steering, Time.deltaTime);

        // 2) Evitar obstáculos (devuelve dirección “segura” a mezclar)
        Vector3 avoidDir = obstacleAvoidance.GetDirImproved(desiredVel, false);

        // 3) Pesos (probá estos valores)
        float pathW = 1.0f;
        float avoidW = 0.3f;//0.4f; // 0.35–0.55

        // Si avoidance empuja en contra (> ~100°), lo capamos fuerte
        if (avoidDir.sqrMagnitude > 1e-4f && desiredVel.sqrMagnitude > 1e-4f)
        {
            float cos = Vector3.Dot(desiredVel.normalized, avoidDir.normalized);
            if (cos < -0.2f) avoidW *= 0.15f;
        }

        // 4) Mezcla
        Vector3 blended = pathW * desiredVel + avoidW * avoidDir;

        // 5) Clamp a tu velocidad máxima actual
        if (blended.sqrMagnitude > currentMaxSpeed * currentMaxSpeed)
            blended = blended.normalized * currentMaxSpeed;

        // 6) Mover y orientar
        if (blended.sqrMagnitude > 0.001f)
        {
            transform.position += blended * Time.deltaTime;

            currentMovementDirection = blended.normalized;
            currentMovementSpeed = blended.magnitude;

            if (blended.magnitude > 0.1f)
            {
                var lookDir = blended.normalized; lookDir.y = 0;
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // Guardá la _vel para otros sistemas si la usás
        _vel = blended;
    }*/
    public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Integrar la FUERZA del PATH → velocidad deseada
        Vector3 desiredVel = Integrate(steering, Time.deltaTime);
        desiredVel.y = 0f;

        float desiredSpeed = desiredVel.magnitude;
        if (desiredSpeed <= 0.0001f)
            return;

        Vector3 desiredDir = desiredVel / Mathf.Max(desiredSpeed, 1e-5f);

        // 2) Evitación → usar SOLO la dirección y quitar componente "hacia atrás"
        Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(desiredVel, false);
        Vector3 avoidDir = avoidedVel.sqrMagnitude > 1e-6f ? avoidedVel.normalized : Vector3.zero;

        if (avoidDir != Vector3.zero)
        {
            // remover la componente que empuja en contra del camino
            avoidDir = Vector3.ProjectOnPlane(avoidDir, -desiredDir);
            if (avoidDir.sqrMagnitude > 1e-6f) avoidDir.Normalize();
        }

        // 3) Blend PATH vs AVOIDANCE (tuning suave)
        float pathW = 1.0f;
        float avoidW = 0.35f;     // probá 0.25–0.45 según densidad de obstáculos

        // si por algún motivo el ángulo es muy contrario, capamos aún más
        if (avoidDir.sqrMagnitude > 1e-6f)
        {
            float cos = Vector3.Dot(desiredDir, avoidDir);
            if (cos < -0.2f) avoidW *= 0.15f;
        }

        Vector3 blended = pathW * desiredVel + avoidW * avoidDir * currentMaxSpeed;

        // 4) Clamp y plano XZ
        float maxV = currentMaxSpeed;
        if (blended.sqrMagnitude > maxV * maxV)
            blended = blended.normalized * maxV;
        blended.y = 0f;

        // 5) Aplicar movimiento y facing
        _vel = blended;

        if (_vel.sqrMagnitude > 0.001f)
        {
            transform.position += _vel * Time.deltaTime;
            currentMovementDirection = _vel.normalized;
            currentMovementSpeed = _vel.magnitude;

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 look = _vel.normalized; look.y = 0f;
                transform.rotation = Quaternion.LookRotation(look);
            }
        }
    }


    #endregion

    #region Detection

    /// <summary>
    /// Check if civilian has line of sight to player
    /// </summary>
    public bool HasLoS()
    {
        if (player == null || playerDetector == null) return false;

        // Force visibility if player is in melee range to avoid LoS flickering during attacks
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer <= meleeRange)
        {
            if (enableDebugLogs)
                MyLogger.LogInfo($"Civilian {gameObject.name}: Forcing LoS=true (in melee range: {distanceToPlayer:F2} <= {meleeRange})");
            return true;
        }

        // Get debug info which includes hasLOS (line of sight) information
        var debugInfo = playerDetector.GetDebugInfo();

        // Additional distance check
        bool inRange = distanceToPlayer <= sightRange;

        // Additional FOV check
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        Vector3 forward = transform.forward;
        float angle = Vector3.Angle(forward, directionToPlayer);
        bool inFOV = angle <= sightFOV * 0.5f;

        // Use hasLOS specifically - true line of sight with no obstacles
        return debugInfo.hasLOS && inRange && inFOV;
    }

    /// <summary>
    /// Get distance to player
    /// </summary>
    public float GetDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, player.position);
    }

    /// <summary>
    /// Check if player is in melee range
    /// </summary>
    public bool IsPlayerInMeleeRange()
    {
        return GetDistanceToPlayer() <= meleeRange;
    }

    #endregion

    /*
    #region Behavior System (LEGACY - Replaced by FSM)

    private void UpdateBehavior()
    {
        bool canSeePlayer = HasLoS();
        float distanceToPlayer = GetDistanceToPlayer();

        // Update last known position if we can see player
        if (canSeePlayer)
        {
            lastKnownPlayerPosition = player.position;
            hasEverSeenPlayer = true;
        }

        // State machine logic
        switch (currentState)
        {
            case CivilianState.Idle:
                HandleIdleState(canSeePlayer, distanceToPlayer);
                break;

            case CivilianState.Fleeing:
                HandleFleeingState(canSeePlayer, distanceToPlayer);
                break;

            case CivilianState.Evading:
                HandleEvadingState(canSeePlayer, distanceToPlayer);
                break;

            case CivilianState.Safe:
                HandleSafeState(canSeePlayer, distanceToPlayer);
                break;
        }
    }

    private void HandleIdleState(bool canSeePlayer, float distanceToPlayer)
    {
        if (canSeePlayer)
        {
            // Player spotted - start fleeing
            ChangeState(CivilianState.Fleeing);
            return;
        }

        // Idle movement - gentle drift
        Vector3 driftDirection = new Vector3(
            Mathf.Sin(Time.time * 0.3f) * 0.5f,
            0f,
            Mathf.Cos(Time.time * 0.2f) * 0.5f
        );

        currentMaxSpeed = walkSpeed;
        Vector3 steering = Steering.Seek(transform.position, transform.position + driftDirection, _vel, walkSpeed);
        ApplySteering(steering * 0.3f); // Gentle movement
    }

    private void HandleFleeingState(bool canSeePlayer, float distanceToPlayer)
    {
        currentMaxSpeed = fleeSpeed;

        if (canSeePlayer)
        {
            // Direct flee from player
            Vector3 steering = Steering.Flee(transform.position, player.position, _vel, fleeSpeed);
            ApplySteering(steering);
        }
        else if (hasEverSeenPlayer && lastKnownPlayerPosition != Vector3.zero)
        {
            // Switch to evading mode
            ChangeState(CivilianState.Evading);
        }
        else
        {
            // No player info - return to idle
            ChangeState(CivilianState.Idle);
        }

        // Check if reached safe distance
        if (distanceToPlayer >= safeDistance)
        {
            ChangeState(CivilianState.Safe);
        }
    }

    private void HandleEvadingState(bool canSeePlayer, float distanceToPlayer)
    {
        currentMaxSpeed = evadeSpeed;

        if (canSeePlayer)
        {
            // Player reacquired - back to fleeing
            ChangeState(CivilianState.Fleeing);
            return;
        }

        // Evade from last known position with prediction
        if (hasEverSeenPlayer && lastKnownPlayerPosition != Vector3.zero)
        {
            // Get player velocity if available
            Vector3 playerVel = Vector3.zero;
            if (player != null)
            {
                var playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    playerVel = playerRb.linearVelocity;
                }
            }

            Vector3 steering = Steering.Evade(transform.position, _vel, lastKnownPlayerPosition, playerVel, evadeSpeed);
            ApplySteering(steering);
        }

        // Timeout after grace period
        if (stateTimer >= loseSightGrace)
        {
            if (distanceToPlayer >= safeDistance)
            {
                ChangeState(CivilianState.Safe);
            }
            else
            {
                ChangeState(CivilianState.Idle);
            }
        }
    }

    private void HandleSafeState(bool canSeePlayer, float distanceToPlayer)
    {
        if (canSeePlayer)
        {
            // Player spotted again - back to fleeing
            ChangeState(CivilianState.Fleeing);
            return;
        }

        // Stay still and wait
        currentMaxSpeed = walkSpeed;
        Vector3 brakeForce = -_vel * 3f;
        ApplySteering(brakeForce);

        // Return to idle after timer
        if (stateTimer >= idleSecondsAfterSafe)
        {
            ChangeState(CivilianState.Idle);
        }
    }

    private void ChangeState(CivilianState newState)
    {
        if (currentState != newState)
        {
            if (enableDebugLogs)
                Logger.LogInfo($"Civilian {gameObject.name}: {currentState} → {newState}");

            currentState = newState;
            stateTimer = 0f;

            // State-specific initialization
            switch (newState)
            {
                case CivilianState.Safe:
                    // Write to blackboard if civilian reaches safety
                    if (blackboard != null)
                    {
                        // This could be used for global alert state
                        blackboard.SetValue(BlackboardKeys.GLOBAL_ALERT, true);
                    }
                    break;
            }
        }
    }

    #endregion
    */

    #region BaseCharacter Implementation

    public override void Move(Vector3 direction)
    {
        if (!isAlive) return;

        // Direct movement using steering
        Vector3 targetVel = direction.normalized * currentMaxSpeed;
        Vector3 steering = targetVel - _vel;
        ApplySteering(steering);
    }

    public override void Shoot(Vector3 direction)
    {
        if (!isAlive || !canAttack || !CanShoot()) return;

        // Basic shooting implementation
        lastShootTime = Time.time;
        if (enableDebugLogs)
            MyLogger.LogInfo($"{gameObject.name}: Civilian shooting at {direction}");
        // TODO: Implement actual shooting logic when canAttack is enabled
    }

    #endregion

    #region IUpdatable Implementation

    public bool IsActive => isAlive && gameObject.activeInHierarchy;

    #endregion

    #region IUseFsm Implementation

    public Transform GetModelTransform()
    {
        return transform;
    }

    public void UpdateFsm()
    {
        if (stateMachine != null)
        {
            stateMachine.RunStateMachine();
        }
    }

    public void SetTargetTransform(Transform p_target)
    {
        player = p_target;
        
        // Update AI system when target changes
        if (m_blackboardService != null && player != null)
        {
            m_blackboardService.SetValue(BlackboardKeys.PLAYER_TRANSFORM, player);
            m_blackboardService.SetValue(BlackboardKeys.PLAYER_POSITION, player.position);
        }
    }

    public Transform GetTargetTransform()
    {
        return player;
    }

    #endregion

    #region Debug and Gizmos

    private void OnDrawGizmos()
    {
        if (!enabled) return;

        // Sight range and FOV
        Gizmos.color = currentState == CivilianState.Fleeing ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // FOV cone
        if (sightFOV < 360f)
        {
            Vector3 leftBoundary = Quaternion.Euler(0, -sightFOV * 0.5f, 0) * transform.forward * sightRange;
            Vector3 rightBoundary = Quaternion.Euler(0, sightFOV * 0.5f, 0) * transform.forward * sightRange;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        }

        // Safe distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, safeDistance);

        // Melee range
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        // Current velocity
        if (_vel.magnitude > 0.1f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, _vel);
        }

        // Last known player position
        if (hasEverSeenPlayer && lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.orange;
            Gizmos.DrawWireCube(lastKnownPlayerPosition, Vector3.one * 0.5f);
        }
    }

    [ContextMenu("Debug Civilian Status")]
    private void DebugCivilianStatus()
    {
        Debug.Log("=== CIVILIAN STATUS ===");
        Debug.Log($"Using ScriptableObject FSM: {useFSM}");
        Debug.Log($"Using Decision Tree: {useDecisionTree}");
        
        if (useFSM && stateMachine != null)
        {
            var currentState = stateMachine.GetCurrentState();
            Debug.Log($"Current State: {(currentState?.State?.StateName ?? "None")}");
            Debug.Log($"ScriptableObject FSM Active: True");
            Debug.Log($"Available FSM States: {GetAvailableStateNames()}");
        }
        else
        {
            Debug.Log($"Legacy State: {currentState}");
            Debug.Log($"ScriptableObject FSM Active: False");
        }
        
        Debug.Log($"State Timer: {stateTimer:F2}s");
        Debug.Log($"Safe Timer: {safeTimer:F2}s");
        Debug.Log($"Can See Player: {HasLoS()}");
        Debug.Log($"Distance to Player: {GetDistanceToPlayer():F2}");
        Debug.Log($"Current Velocity: {_vel.magnitude:F2}");
        Debug.Log($"Current Max Speed: {currentMaxSpeed:F2}");
        Debug.Log($"Has Ever Seen Player: {hasEverSeenPlayer}");
        Debug.Log($"Last Known Player Pos: {lastKnownPlayerPosition}");
        Debug.Log($"Can Attack: {canAttack}");
        
        // Decision Tree status
        if (useDecisionTree && decisionTreeRunner != null)
        {
            Debug.Log($"Decision Tree Active: {decisionTreeRunner.enabled}");
            Debug.Log($"DT Status: {decisionTreeRunner.GetStatus()}");
            Debug.Log($"DT Last Suggestion: {decisionTreeRunner.LastSuggestion}");
        }
        else
        {
            Debug.Log($"Decision Tree Active: False");
        }
        
        Debug.Log("=======================");
    }

    // Getter for pathfinding
    public int PathLenForDebug => _pathLen;
    public PathFollowerAgent PathFollower => _pathFollower;
    public Vector3[] WorldPathForDebug => _worldPath;

#if UNITY_EDITOR
    /*private void OnDrawGizmosSelected()
    {
        // dibujar solo si hay path
        if (_pathLen <= 0 || _worldPath == null) return;

        // colores
        Color cLine = new Color(0f, 0.8f, 1f, 0.9f);   // celeste
        Color cWp = new Color(0f, 0.6f, 1f, 0.8f);   // waypoints
        Color cCurrent = new Color(0.2f, 1f, 0.2f, 1f);   // waypoint actual (verde)
        Color cGoal = new Color(1f, 0.5f, 0.1f, 1f);   // objetivo final (naranja)
        float rWp = 0.08f;
        float rCur = 0.12f;

        // línea del path
        Gizmos.color = cLine;
        for (int i = 0; i < _pathLen - 1; i++)
        {
            Gizmos.DrawLine(_worldPath[i], _worldPath[i + 1]);
        }

        // waypoints
        Gizmos.color = cWp;
        for (int i = 0; i < _pathLen; i++)
            Gizmos.DrawSphere(_worldPath[i], rWp);

        // waypoint actual
        int ci = _pathFollower != null ? Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1) : 0;
        Gizmos.color = cCurrent;
        Gizmos.DrawSphere(_worldPath[ci], rCur);

        // objetivo final + radio de llegada
        Gizmos.color = cGoal;
        Gizmos.DrawSphere(_worldPath[_pathLen - 1], rCur * 1.2f);
        Gizmos.DrawWireSphere(_worldPath[_pathLen - 1], fleeWaypointReach);

        // etiqueta útil
#if UNITY_EDITOR
        Handles.Label(_worldPath[_pathLen - 1] + Vector3.up * 0.2f, $"Safe ({_pathLen - 1})");
#endif
        // Dibuja path actual en escena cuando seleccionás el Civilian
        if (_pathLen > 0 && _worldPath != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < _pathLen; i++)
            {
                Gizmos.DrawSphere(_worldPath[i], 0.12f);
                if (i < _pathLen - 1)
                    Gizmos.DrawLine(_worldPath[i], _worldPath[i + 1]);
            }

            // destino final
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_worldPath[_pathLen - 1], fleeWaypointReach);
        }
    }*/
    private void OnDrawGizmosSelected()
    {
        if (_pathLen <= 0 || _worldPath == null) return;

        // convertir _worldPath a array de points del tamaño exacto
        var count = Mathf.Min(_pathLen, _worldPath.Length);
        Vector3[] pts = new Vector3[count];
        for (int i = 0; i < count; i++) pts[i] = _worldPath[i];

        // línea AA con grosor
        Handles.color = new Color(0f, 0.8f, 1f, 0.9f);
        Handles.DrawAAPolyLine(4.0f, pts); // grosor = 4 px

        // waypoints
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.8f);
        for (int i = 0; i < count; i++)
            Gizmos.DrawSphere(_worldPath[i], 0.08f);

        // waypoint actual
        int ci = _pathFollower != null ? Mathf.Clamp(_pathFollower.CurrentIndex, 0, count - 1) : 0;
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
        Gizmos.DrawSphere(_worldPath[ci], 0.12f);

        // destino + radio
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
        Gizmos.DrawSphere(_worldPath[count - 1], 0.14f);
        Gizmos.DrawWireSphere(_worldPath[count - 1], fleeWaypointReach);

        // etiqueta SAFE
        Handles.Label(_worldPath[count - 1] + Vector3.up * 0.2f, $"Safe ({count - 1})");
    }
#endif

    #endregion

    #region Cleanup

    private void OnDisable()
    {
        UnsubscribeUpdateService();
    }

    public void MyUpdate()
    {
        if (!isAlive) return;

        // Update timers
        stateTimer += Time.deltaTime;

        // Use ScriptableObject FSM if enabled, otherwise fallback to legacy system
        if (useFSM && stateMachine != null)
        {
            UpdateFsm();
        }
        else if (!useDecisionTree)
        {
            // Use legacy behavior system only if decision tree is disabled
            // UpdateBehavior(); // Commented out - legacy system replaced by FSM/DT
        }

        // Note: Decision Tree runs independently via its own coroutine
        // and influences behavior through RequestStateChange calls
    }

    public void SubscribeUpdateService()
    {
        ServiceLocator.Get<IUpdateService>().AddUpdateListener(this);
    }

    public void UnsubscribeUpdateService()
    {
        ServiceLocator.Get<IUpdateService>().RemoveUpdateListener(this);
    }

    #endregion
}