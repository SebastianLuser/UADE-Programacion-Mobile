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
        fleeGraph != null && fleeGraph.NodeCount > 0 && fleeTargetNodeIndex >= 0 && fleeTargetNodeIndex < fleeGraph.NodeCount;

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
    /*public void RecomputeFleePathIfNeeded(float now)
    {
        //if (!HasFleeGraph) { _pathLen = 0; return; }
        if (!HasFleeGraph)
        {
            _pathLen = 0;
            if (enableDebugLogs)
            {
                if (fleeGraph == null)
                    Debug.LogWarning($"[{name}] No flee graph assigned!");
                else if (fleeGraph.NodeCount == 0)
                    Debug.LogWarning($"[{name}] Flee graph is empty (0 nodes)!");
                else if (fleeTargetNodeIndex < 0 || fleeTargetNodeIndex >= fleeGraph.NodeCount)
                    Debug.LogWarning($"[{name}] Invalid target index: {fleeTargetNodeIndex} (graph has {fleeGraph.NodeCount} nodes)");
            }
            return;
        }
        // 1) Si ya tengo ruta, y estoy cerca del waypoint actual, NO recomputar
        /*if (_pathLen > 0 && _pathFollower != null)
        {
            int ci = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            Vector3 curWp = _worldPath[ci];
            float r = fleeWaypointReach * 1.5f; // holgura
            if ((transform.position - curWp).sqrMagnitude <= r * r)
                return; // voy bien hacia el target actual
        }                                                                                * /
        if (_pathLen > 0 && _pathFollower != null)
        {
            int ci = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            Vector3 curWp = _worldPath[ci];
            float r = fleeWaypointReach * 1.5f;
            if ((transform.position - curWp).sqrMagnitude <= r * r)
            {
                if (enableDebugLogs)
                    Debug.Log($"[{name}] Near current waypoint {ci}, skipping recompute");
                return;
            }
        }

        // 2) Respeta el intervalo de recompute
        /*if (now - _lastAStarTime < fleeRecomputeInterval && _pathLen > 0)
            return;                                                                    * /
        if (now - _lastAStarTime < fleeRecomputeInterval && _pathLen > 0)
        {
            if (enableDebugLogs)
                Debug.Log($"[{name}] Path still fresh ({now - _lastAStarTime:F2}s < {fleeRecomputeInterval}s)");
            return;
        }

        // 3) Correr A*
        /*int startIdx = ClosestNodeIndex(transform.position);
        if (startIdx < 0) { _pathLen = 0; return; }                                     * /
        int startIdx = ClosestNodeIndex(transform.position);
        if (startIdx < 0)
        {
            _pathLen = 0;
            if (enableDebugLogs)
                Debug.LogError($"[{name}] ClosestNodeIndex returned -1! Graph has {fleeGraph.NodeCount} nodes");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[{name}] Computing A* from node {startIdx} (pos: {fleeGraph.nodePositions[startIdx]}) to {fleeTargetNodeIndex} (pos: {fleeGraph.nodePositions[fleeTargetNodeIndex]})");


        // Guardar target previo (si había ruta)
        /*Vector3 prevTarget = Vector3.zero;
        bool hadPath = _pathLen > 0 && _pathFollower != null;
        int prevIdx = 0;
        if (hadPath)
        {
            prevIdx = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            prevTarget = _worldPath[prevIdx];
        }                                                                                 * /
        Vector3 prevTarget = Vector3.zero;
        bool hadPath = _pathLen > 0 && _pathFollower != null;
        int prevIdx = 0;
        if (hadPath)
        {
            prevIdx = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            prevTarget = _worldPath[prevIdx];
        }
        //////

        /*int newLen = AStarNoAlloc.FindPath(
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
        }                                                                                    * /
        // 4) Correr A*
        int newLen = AStarNoAlloc.FindPath(
            fleeGraph, startIdx, fleeTargetNodeIndex,
            _astarG, _astarF, _astarFrom, _astarClosed,
            _heapIdx, _heapF, _pathIdx
        );

        if (newLen > 0)
        {
            _pathLen = _pathFollower.BuildWorldPath(fleeGraph, _pathIdx, newLen, _worldPath);
            _lastAStarTime = now;

            if (enableDebugLogs)
            {
                string pathStr = "";
                for (int i = 0; i < Mathf.Min(5, _pathLen); i++)
                    pathStr += $"{_pathIdx[i]} ";
                if (_pathLen > 5) pathStr += "...";
                Debug.Log($"[{name}] ✓ A* found path with {_pathLen} nodes: [{pathStr}]");
            }

            // 5) Preservar progreso
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

                if (enableDebugLogs)
                    Debug.Log($"[{name}] Preserved progress: cursor reseeded to waypoint {best}");
            }
        }
        else
        {
            _pathLen = 0;
            if (enableDebugLogs)
                Debug.LogError($"[{name}] ✗ A* FAILED to find path from {startIdx} to {fleeTargetNodeIndex}! Graph may be disconnected");
        }

    }*/

    /*public void RecomputeFleePathIfNeeded(float now)
    {
        if (!HasFleeGraph)
        {
            _pathLen = 0;
            if (enableDebugLogs)
            {
                if (fleeGraph == null)
                    Debug.LogWarning($"[{name}] No flee graph assigned!");
                else if (fleeGraph.NodeCount == 0)
                    Debug.LogWarning($"[{name}] Flee graph is empty (0 nodes)!");
                else if (fleeTargetNodeIndex < 0 || fleeTargetNodeIndex >= fleeGraph.NodeCount)
                    Debug.LogWarning($"[{name}] Invalid target index: {fleeTargetNodeIndex} (graph has {fleeGraph.NodeCount} nodes)");
            }
            return;
        }

        // 1) Si ya tengo ruta Y estoy progresando, NO recomputar
        if (_pathLen > 0 && _pathFollower != null)
        {
            int ci = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            Vector3 curWp = _worldPath[ci];
            float distToCurrentWP = Vector3.Distance(transform.position, curWp);

            // Si estamos cerca del waypoint actual, NO recomputar
            if (distToCurrentWP <= fleeWaypointReach * 2.0f)
            {
                if (enableDebugLogs)
                    Debug.Log($"[{name}] Near current WP {ci} (dist: {distToCurrentWP:F2}), keeping path");
                return;
            }

            // Si el path es reciente, NO recomputar
            if (now - _lastAStarTime < fleeRecomputeInterval)
            {
                if (enableDebugLogs)
                    Debug.Log($"[{name}] Path still fresh ({now - _lastAStarTime:F2}s < {fleeRecomputeInterval}s)");
                return;
            }

            // CRÍTICO: Si tenemos path válido, NO recomputar a menos que estemos MUY desviados
            // Esto evita que el START "avance" y se pierdan waypoints
            float distToLastWP = Vector3.Distance(transform.position, _worldPath[_pathLen - 1]);
            if (distToLastWP < fleeWaypointReach * 1.5f)
            {
                // Ya casi llegamos al final, no tocar
                if (enableDebugLogs)
                    Debug.Log($"[{name}] Near final WP, keeping path");
                return;
            }

            // Verificar si nos desviamos MUCHO del path (> 5 metros de todos los waypoints)
            float minDistToPath = float.MaxValue;
            for (int i = 0; i < _pathLen; i++)
            {
                float d = Vector3.Distance(transform.position, _worldPath[i]);
                if (d < minDistToPath) minDistToPath = d;
            }

            if (minDistToPath <= 5.0f) // Si estamos a menos de 5m del path, mantenerlo
            {
                if (enableDebugLogs)
                    Debug.Log($"[{name}] Still close to path ({minDistToPath:F2}m), keeping it");
                return;
            }

            // Si llegamos aquí, estamos MUY desviados → permitir recompute
            Debug.LogWarning($"[{name}] Too far from path ({minDistToPath:F2}m), recomputing");
        }

        // 2) Encontrar nodo más cercano (solo si NO tenemos path válido)
        int startIdx = ClosestNodeIndex(transform.position);
        if (startIdx < 0)
        {
            _pathLen = 0;
            if (enableDebugLogs)
                Debug.LogError($"[{name}] ClosestNodeIndex returned -1!");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[{name}] Computing NEW A* path from node {startIdx} to {fleeTargetNodeIndex}");

        // 3) Guardar contexto previo para preservar progreso
        Vector3 prevTarget = Vector3.zero;
        bool hadPath = _pathLen > 0 && _pathFollower != null;
        int prevIdx = 0;
        if (hadPath)
        {
            prevIdx = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            prevTarget = _worldPath[prevIdx];
        }

        // 4) Correr A*
        int newLen = AStarNoAlloc.FindPath(
            fleeGraph, startIdx, fleeTargetNodeIndex,
            _astarG, _astarF, _astarFrom, _astarClosed,
            _heapIdx, _heapF, _pathIdx
        );

        if (newLen > 0)
        {
            _pathLen = _pathFollower.BuildWorldPath(fleeGraph, _pathIdx, newLen, _worldPath);
            _lastAStarTime = now;

            if (enableDebugLogs)
            {
                string pathStr = "";
                for (int i = 0; i < Mathf.Min(5, _pathLen); i++)
                    pathStr += $"{_pathIdx[i]} ";
                if (_pathLen > 5) pathStr += "...";
                Debug.Log($"[{name}] ✓ A* found path with {_pathLen} nodes: [{pathStr}]");
            }

            // 5) Preservar progreso: buscar waypoint más cercano al target anterior
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

                if (enableDebugLogs)
                    Debug.Log($"[{name}] Preserved progress: cursor at waypoint {best}");
            }
        }
        else
        {
            _pathLen = 0;
            if (enableDebugLogs)
                Debug.LogError($"[{name}] ✗ A* FAILED from {startIdx} to {fleeTargetNodeIndex}!");
        }
    }*/
    public void RecomputeFleePathIfNeeded(float now)
    {
        if (!HasFleeGraph)
        {
            _pathLen = 0;
            return;
        }

        // ==== ESTRATEGIA: Solo recomputar si REALMENTE es necesario ====

        // 1) Si tenemos path válido, verificar si debemos mantenerlo
        if (_pathLen > 0 && _pathFollower != null)
        {
            int currentIdx = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            Vector3 currentWP = _worldPath[currentIdx];
            float distToCurrentWP = Vector3.Distance(transform.position, currentWP);

            // a) Si estamos progresando hacia el waypoint actual, NUNCA recomputar
            if (distToCurrentWP < 10.0f) // Dentro de 10 metros del WP actual
            {
                return; // Mantener path actual
            }

            // b) Si llegamos al último waypoint, no recomputar
            if (_pathFollower.ReachedEnd)
            {
                return;
            }

            // c) Verificar si estamos cerca de CUALQUIER waypoint del path
            bool nearAnyWaypoint = false;
            for (int i = 0; i < _pathLen; i++)
            {
                float dist = Vector3.Distance(transform.position, _worldPath[i]);
                if (dist < 8.0f) // Dentro de 8 metros de algún waypoint
                {
                    nearAnyWaypoint = true;
                    break;
                }
            }

            if (nearAnyWaypoint)
            {
                return; // Mantener path actual
            }

            // d) Respetar intervalo de tiempo
            float timeSinceLastCompute = now - _lastAStarTime;
            if (timeSinceLastCompute < fleeRecomputeInterval)
            {
                return; // Path aún fresco
            }

            // Si llegamos aquí, estamos MUY lejos del path → permitir recompute
            Debug.LogWarning($"[{name}] Far from all waypoints, recomputing path");
        }

        // 2) Computar nuevo path
        int startIdx = ClosestNodeIndex(transform.position);
        if (startIdx < 0)
        {
            _pathLen = 0;
            return;
        }

        // IMPORTANTE: Si ya tenemos un path, verificar que el nuevo START
        // sea diferente del actual, si no, no vale la pena recomputar
        if (_pathLen > 0 && _pathIdx != null && _pathIdx[0] == startIdx)
        {
            if (enableDebugLogs)
                Debug.Log($"[{name}] START node unchanged ({startIdx}), keeping current path");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[{name}] Computing path from node {startIdx} to {fleeTargetNodeIndex}");

        // Guardar progreso anterior
        Vector3 prevTarget = Vector3.zero;
        int prevIdx = 0;
        bool hadPath = _pathLen > 0 && _pathFollower != null;

        if (hadPath)
        {
            prevIdx = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            prevTarget = _worldPath[prevIdx];
        }

        // 3) Ejecutar A*
        int newLen = AStarNoAlloc.FindPath(
            fleeGraph, startIdx, fleeTargetNodeIndex,
            _astarG, _astarF, _astarFrom, _astarClosed,
            _heapIdx, _heapF, _pathIdx
        );

        if (newLen > 0)
        {
            _pathLen = _pathFollower.BuildWorldPath(fleeGraph, _pathIdx, newLen, _worldPath);
            _lastAStarTime = now;

            if (enableDebugLogs)
            {
                string pathStr = "";
                for (int i = 0; i < Mathf.Min(5, _pathLen); i++)
                    pathStr += $"{_pathIdx[i]} ";
                if (_pathLen > 5) pathStr += "...";
                Debug.Log($"[{name}] ✓ New path: {_pathLen} waypoints [{pathStr}]");
            }

            // 4) Preservar progreso: encontrar waypoint más cercano al anterior
            if (hadPath)
            {
                int bestIdx = 0;
                float bestDist = float.MaxValue;

                for (int i = 0; i < _pathLen; i++)
                {
                    float dist = Vector3.Distance(_worldPath[i], prevTarget);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                    }
                }

                // Solo avanzar cursor si el nuevo waypoint está más adelante
                if (bestIdx > 0)
                {
                    _pathFollower.ReseedCursor(bestIdx);
                    if (enableDebugLogs)
                        Debug.Log($"[{name}] Preserved progress: cursor at WP {bestIdx}");
                }
            }
        }
        else
        {
            _pathLen = 0;
            if (enableDebugLogs)
                Debug.LogError($"[{name}] A* failed from {startIdx} to {fleeTargetNodeIndex}");
        }
    }

    // 4) Un tick de follow-path -> steering deseado
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
    /*public void ApplySteering(Vector3 steering)
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
    }*/
    /*public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Integrar
        Vector3 desiredVel = Integrate(steering, Time.deltaTime);
        desiredVel.y = 0f;

        float desiredSpeed = desiredVel.magnitude;
        if (desiredSpeed <= 0.0001f) return;

        Vector3 desiredDir = desiredVel / Mathf.Max(desiredSpeed, 1e-5f);

        // 2) Evitación
        Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(desiredVel, false);
        Vector3 avoidDir = avoidedVel.sqrMagnitude > 1e-6f ? avoidedVel.normalized : Vector3.zero;

        if (avoidDir != Vector3.zero)
        {
            avoidDir = Vector3.ProjectOnPlane(avoidDir, -desiredDir);
            if (avoidDir.sqrMagnitude > 1e-6f) avoidDir.Normalize();
        }

        // 3) Blend
        float pathW = 1.0f;
        float avoidW = 0.35f;

        if (avoidDir.sqrMagnitude > 1e-6f)
        {
            float cos = Vector3.Dot(desiredDir, avoidDir);
            if (cos < -0.2f) avoidW *= 0.15f;
        }

        Vector3 blended = pathW * desiredVel + avoidW * avoidDir * currentMaxSpeed;

        // 4) Clamp
        float maxV = currentMaxSpeed;
        if (blended.sqrMagnitude > maxV * maxV)
            blended = blended.normalized * maxV;
        blended.y = 0f; // <- CRÍTICO

        _vel = blended;

        // 5) Aplicar movimiento MANTENIENDO Y
        if (_vel.sqrMagnitude > 0.001f)
        {
            Vector3 newPos = transform.position + _vel * Time.deltaTime;
            newPos.y = transform.position.y; // <- CRÍTICO: mantener altura
            transform.position = newPos;

            currentMovementDirection = _vel.normalized;
            currentMovementSpeed = _vel.magnitude;

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 look = _vel.normalized;
                look.y = 0f;
                transform.rotation = Quaternion.LookRotation(look);
            }
        }
    }*/
    public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Integrar el steering del path
        Vector3 desiredVel = Integrate(steering, Time.deltaTime);
        desiredVel.y = 0f;

        float desiredSpeed = desiredVel.magnitude;
        if (desiredSpeed <= 0.0001f) return;

        Vector3 desiredDir = desiredVel / Mathf.Max(desiredSpeed, 1e-5f);

        // 2) Obstacle avoidance - SOLO ajuste lateral, NO retroceso
        Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(desiredVel, false);
        Vector3 avoidDir = avoidedVel.sqrMagnitude > 1e-6f ? avoidedVel.normalized : Vector3.zero;

        // CRÍTICO: Proyectar avoidance al plano perpendicular al path
        // Esto elimina cualquier componente que empuje hacia atrás o hacia adelante
        if (avoidDir != Vector3.zero)
        {
            // Quitar componente paralela al path (solo mantener perpendicular)
            avoidDir = Vector3.ProjectOnPlane(avoidDir, desiredDir);
            if (avoidDir.sqrMagnitude > 1e-6f)
                avoidDir.Normalize();
            else
                avoidDir = Vector3.zero; // Si quedó muy chico, ignorar
        }

        // 3) Blend conservador - path tiene MUCHA más prioridad
        float pathW = 1.0f;
        float avoidW = 0.25f; // Reducido de 0.35 para dar más prioridad al path

        // Si el avoidance empuja contra el path, reducir aún más su peso
        if (avoidDir != Vector3.zero)
        {
            float alignment = Vector3.Dot(desiredDir, avoidDir);

            // Si hay conflicto (alignment negativo), reducir drásticamente
            if (alignment < -0.1f)
            {
                avoidW *= 0.05f; // Casi anular el obstacle avoidance
                Debug.DrawRay(transform.position, avoidDir * 2f, Color.red, 0.1f);
            }
            else
            {
                Debug.DrawRay(transform.position, avoidDir * 2f, Color.yellow, 0.1f);
            }
        }

        // Blend: path + ajuste lateral mínimo
        Vector3 blended = desiredVel + avoidDir * (avoidW * currentMaxSpeed);

        // 4) Clamp manteniendo dirección del path
        float maxV = currentMaxSpeed;
        if (blended.sqrMagnitude > maxV * maxV)
        {
            // IMPORTANTE: No normalizar ciegamente, mantener bias hacia el path
            // Si el blended excede la velocidad, recortar la componente de avoidance primero
            Vector3 pathComponent = Vector3.Project(blended, desiredDir);
            Vector3 avoidComponent = blended - pathComponent;

            // Recortar avoidance si es necesario
            float pathMag = pathComponent.magnitude;
            float avoidMag = avoidComponent.magnitude;
            float totalMag = Mathf.Sqrt(pathMag * pathMag + avoidMag * avoidMag);

            if (totalMag > maxV)
            {
                // Priorizar path, recortar avoidance
                float scale = Mathf.Sqrt(Mathf.Max(0, maxV * maxV - pathMag * pathMag)) / Mathf.Max(avoidMag, 1e-5f);
                avoidComponent *= Mathf.Min(scale, 1f);
                blended = pathComponent + avoidComponent;
            }
        }

        blended.y = 0f;
        _vel = blended;

        // 5) Aplicar movimiento manteniendo Y
        if (_vel.sqrMagnitude > 0.001f)
        {
            Vector3 newPos = transform.position + _vel * Time.deltaTime;
            newPos.y = transform.position.y; // Mantener altura
            transform.position = newPos;

            currentMovementDirection = _vel.normalized;
            currentMovementSpeed = _vel.magnitude;

            // Debug: mostrar velocidad final
            Debug.DrawRay(transform.position, _vel, Color.cyan, 0.1f);

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 look = _vel.normalized;
                look.y = 0f;
                transform.rotation = Quaternion.LookRotation(look);
            }
        }
    }
    /*public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Integrar steering del path
        Vector3 desiredVel = Integrate(steering, Time.deltaTime);
        desiredVel.y = 0f;

        float desiredSpeed = desiredVel.magnitude;
        if (desiredSpeed <= 0.0001f) return;

        Vector3 desiredDir = desiredVel / Mathf.Max(desiredSpeed, 1e-5f);

        // 2) Determinar si estamos cerca de un waypoint
        bool nearWaypoint = false;
        float distToCurrentWP = float.MaxValue;

        if (_pathFollower != null && _pathLen > 0)
        {
            int currentWP = Mathf.Clamp(_pathFollower.CurrentIndex, 0, _pathLen - 1);
            distToCurrentWP = Vector3.Distance(transform.position, _worldPath[currentWP]);
            nearWaypoint = distToCurrentWP < fleeWaypointReach * 2.0f; // 2x el reach
        }

        Vector3 finalVel;

        if (nearWaypoint)
        {
            // CERCA DEL WAYPOINT: Priorizar llegada, obstacle avoidance MUY reducido
            Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(desiredVel, false);
            Vector3 avoidDir = avoidedVel.sqrMagnitude > 1e-6f ? avoidedVel.normalized : Vector3.zero;

            if (avoidDir != Vector3.zero)
            {
                // Solo componente perpendicular al path
                avoidDir = Vector3.ProjectOnPlane(avoidDir, desiredDir);
                if (avoidDir.sqrMagnitude > 1e-6f)
                    avoidDir.Normalize();
                else
                    avoidDir = Vector3.zero;
            }

            // Blend muy conservador: 95% path, 5% avoidance
            float pathW = 0.95f;
            float avoidW = 0.05f;

            finalVel = pathW * desiredVel + avoidW * avoidDir * currentMaxSpeed;

            Debug.DrawRay(transform.position, Vector3.up * 0.3f, Color.green, 0.1f); // Indicador: modo cercano

            if (enableDebugLogs && Time.frameCount % 30 == 0) // Log cada 30 frames
                Debug.Log($"[{name}] Near WP (dist: {distToCurrentWP:F2}), reduced avoidance");
        }
        else
        {
            // LEJOS DEL WAYPOINT: Obstacle avoidance normal pero limitado
            Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(desiredVel, false);
            Vector3 avoidDir = avoidedVel.sqrMagnitude > 1e-6f ? avoidedVel.normalized : Vector3.zero;

            if (avoidDir != Vector3.zero)
            {
                // Solo componente perpendicular
                avoidDir = Vector3.ProjectOnPlane(avoidDir, desiredDir);
                if (avoidDir.sqrMagnitude > 1e-6f)
                    avoidDir.Normalize();
                else
                    avoidDir = Vector3.zero;
            }

            // Blend moderado: 85% path, 15% avoidance
            float pathW = 0.85f;
            float avoidW = 0.15f;

            // Reducir avoidance si apunta contra el path
            if (avoidDir.sqrMagnitude > 1e-6f)
            {
                float alignment = Vector3.Dot(desiredDir, avoidDir);
                if (alignment < -0.1f)
                {
                    avoidW *= 0.1f; // Casi anular si hay conflicto
                    Debug.DrawRay(transform.position, avoidDir * 2f, Color.red, 0.1f);
                }
                else
                {
                    Debug.DrawRay(transform.position, avoidDir * 2f, Color.yellow, 0.1f);
                }
            }

            finalVel = pathW * desiredVel + avoidW * avoidDir * currentMaxSpeed;

            Debug.DrawRay(transform.position, Vector3.up * 0.3f, Color.blue, 0.1f); // Indicador: modo normal
        }

        // 3) Clamp a velocidad máxima
        float maxV = currentMaxSpeed;
        if (finalVel.sqrMagnitude > maxV * maxV)
        {
            finalVel = finalVel.normalized * maxV;
        }

        finalVel.y = 0f;
        _vel = finalVel;

        // 4) Aplicar movimiento manteniendo Y
        if (_vel.sqrMagnitude > 0.001f)
        {
            Vector3 newPos = transform.position + _vel * Time.deltaTime;
            newPos.y = transform.position.y; // Mantener altura
            transform.position = newPos;

            currentMovementDirection = _vel.normalized;
            currentMovementSpeed = _vel.magnitude;

            // Debug: velocidad final
            Debug.DrawRay(transform.position, _vel, Color.cyan, 0.1f);

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 look = _vel.normalized;
                look.y = 0f;
                transform.rotation = Quaternion.LookRotation(look);
            }
        }
    }*/
    public void ApplySteeringDebug(Vector3 steering)
    {
        if (!isAlive) return;

        // VERSIÓN SIMPLIFICADA PARA DEBUG - SIN OBSTACLE AVOIDANCE
        Vector3 desiredVel = Integrate(steering, Time.deltaTime);
        desiredVel.y = 0f;

        float maxV = currentMaxSpeed;
        if (desiredVel.sqrMagnitude > maxV * maxV)
            desiredVel = desiredVel.normalized * maxV;

        _vel = desiredVel;

        if (_vel.sqrMagnitude > 0.001f)
        {
            transform.position += _vel * Time.deltaTime;

            Debug.DrawRay(transform.position, _vel, Color.cyan, 0.1f); // Ver velocidad final

            currentMovementDirection = _vel.normalized;
            currentMovementSpeed = _vel.magnitude;

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 look = _vel.normalized;
                look.y = 0f;
                transform.rotation = Quaternion.LookRotation(look);
            }
        }
    }

    /// <summary>
    /// Steering especializado para pathfinding flee - prioriza seguir el path sobre todo
    /// </summary>
    public void ApplySteeringFlee(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Integrar steering del path (igual que Debug)
        Vector3 desiredVel = Integrate(steering, Time.deltaTime);
        desiredVel.y = 0f;

        float maxV = currentMaxSpeed;
        if (desiredVel.sqrMagnitude > maxV * maxV)
            desiredVel = desiredVel.normalized * maxV;

        // 2) Obstacle avoidance MUY SUAVE - solo para evitar colisiones directas
        // No usar GetDirImproved que es muy agresivo, solo detectar colisión inminente
        Vector3 finalVel = desiredVel;

        // Raycast corto hacia adelante para detectar colisión DIRECTA
        Vector3 checkDir = desiredVel.normalized;
        float checkDist = personalArea * 1.5f; // Muy corto, solo colisiones inmediatas

        if (Physics.Raycast(transform.position, checkDir, out RaycastHit hit, checkDist, obstaclesMask))
        {
            // Colisión inminente - ajuste lateral MÍNIMO
            Vector3 normal = hit.normal;
            normal.y = 0f;

            if (normal.sqrMagnitude > 0.01f)
            {
                normal.Normalize();

                // Proyectar velocidad deseada al plano del obstáculo (deslizar)
                Vector3 slideVel = Vector3.ProjectOnPlane(desiredVel, normal);

                // Blend muy suave: 90% original, 10% slide
                finalVel = Vector3.Lerp(desiredVel, slideVel, 0.1f);

                Debug.DrawRay(hit.point, normal, Color.red, 0.1f);
                Debug.DrawRay(transform.position, slideVel.normalized, Color.yellow, 0.1f);
            }
        }

        finalVel.y = 0f;
        _vel = finalVel;

        // 3) Aplicar movimiento (igual que Debug)
        if (_vel.sqrMagnitude > 0.001f)
        {
            Vector3 newPos = transform.position + _vel * Time.deltaTime;
            newPos.y = transform.position.y;
            transform.position = newPos;

            currentMovementDirection = _vel.normalized;
            currentMovementSpeed = _vel.magnitude;

            Debug.DrawRay(transform.position, _vel, Color.cyan, 0.1f);

            if (_vel.sqrMagnitude > 0.01f)
            {
                Vector3 look = _vel.normalized;
                look.y = 0f;
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
    /*private void OnDrawGizmosSelected()
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
    }*/
    /*private void OnDrawGizmosSelected()
    {
        // === DIAGNÓSTICO DE GRAFO ===
        if (fleeGraph != null && fleeGraph.NodeCount > 0)
        {
            // Validar target
            bool validTarget = fleeTargetNodeIndex >= 0 && fleeTargetNodeIndex < fleeGraph.NodeCount;

            // Nodo más cercano (START)
            int startIdx = ClosestNodeIndex(transform.position);
            if (startIdx >= 0)
            {
                Vector3 startPos = fleeGraph.nodePositions[startIdx];

                // Dibujar START
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(startPos, 0.15f);
                UnityEditor.Handles.Label(startPos + Vector3.up * 0.3f, $"START [{startIdx}]");

                // En OnDrawGizmosSelected, después de dibujar START
                if (startIdx >= 0)
                {
                    float dist = Vector3.Distance(transform.position, fleeGraph.nodePositions[startIdx]);
                    UnityEditor.Handles.Label(
                        transform.position + Vector3.up * 1.0f,
                        $"Distance to START: {dist:F1}m",
                        new GUIStyle() { normal = new GUIStyleState() { textColor = dist > 10f ? Color.red : Color.white } }
                    );
                }

                // Línea desde NPC a START
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawLine(transform.position, startPos);

                // Distancia a START
                float distToStart = Vector3.Distance(transform.position, startPos);
                UnityEditor.Handles.Label(
                    Vector3.Lerp(transform.position, startPos, 0.5f),
                    $"{distToStart:F1}m"
                );
            }

            // Nodo objetivo (GOAL)
            if (validTarget)
            {
                Vector3 goalPos = fleeGraph.nodePositions[fleeTargetNodeIndex];

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(goalPos, 0.18f);
                Gizmos.DrawWireSphere(goalPos, fleeWaypointReach);
                UnityEditor.Handles.Label(goalPos + Vector3.up * 0.3f, $"SAFE [{fleeTargetNodeIndex}]");
            }
            else
            {
                // Target inválido - warning grande
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.red;
                style.fontSize = 14;
                style.fontStyle = FontStyle.Bold;
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 0.5f,
                    $"!!!!! INVALID TARGET: {fleeTargetNodeIndex} / {fleeGraph.NodeCount}",
                    style
                );
            }

            // === PATH ACTUAL ===
            if (_pathLen > 0 && _worldPath != null)
            {
                int count = Mathf.Min(_pathLen, _worldPath.Length);
                Vector3[] pathPoints = new Vector3[count];
                for (int i = 0; i < count; i++) pathPoints[i] = _worldPath[i];

                // Línea del path (antialiased)
                UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.9f);
                UnityEditor.Handles.DrawAAPolyLine(5.0f, pathPoints);

                // Waypoints
                for (int i = 0; i < count; i++)
                {
                    Gizmos.color = new Color(0f, 0.6f, 1f, 0.8f);
                    Gizmos.DrawSphere(_worldPath[i], 0.08f);

                    // Números de waypoint
                    UnityEditor.Handles.Label(_worldPath[i] + Vector3.up * 0.1f, i.ToString());
                }

                // Waypoint actual (más grande, verde)
                if (_pathFollower != null)
                {
                    int currentIdx = Mathf.Clamp(_pathFollower.CurrentIndex, 0, count - 1);
                    Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
                    Gizmos.DrawSphere(_worldPath[currentIdx], 0.14f);
                    Gizmos.DrawWireSphere(_worldPath[currentIdx], fleeWaypointReach);

                    UnityEditor.Handles.Label(
                        _worldPath[currentIdx] + Vector3.up * 0.25f,
                        $"CURRENT [{currentIdx}]"
                    );
                }

                // Info del path
                GUIStyle pathStyle = new GUIStyle();
                pathStyle.normal.textColor = Color.cyan;
                pathStyle.fontSize = 11;
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 0.7f,
                    $"Path: {_pathLen} waypoints | Current: {_pathFollower?.CurrentIndex ?? -1} | Reached end: {_pathFollower?.ReachedEnd ?? false}",
                    pathStyle
                );
            }
            else if (validTarget)
            {
                // Tiene grafo válido pero no path
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.yellow;
                style.fontSize = 12;
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 0.7f,
                    "!!!! No path computed",
                    style
                );
            }
        }
        else
        {
            // Sin grafo
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.red;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                "!!!! NO FLEE GRAPH ASSIGNED",
                style
            );
        }
    }*/
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        using (new UnityEditor.Handles.DrawingScope())
        {
            // === DIAGNÓSTICO DE GRAFO ===
            if (fleeGraph != null && fleeGraph.NodeCount > 0)
            {
                bool validTarget = fleeTargetNodeIndex >= 0 && fleeTargetNodeIndex < fleeGraph.NodeCount;

                // === NODO START (más cercano) ===
                int startIdx = ClosestNodeIndex(transform.position);
                if (startIdx >= 0)
                {
                    Vector3 startPos = fleeGraph.nodePositions[startIdx];

                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(startPos, 0.2f);
                    Gizmos.DrawWireSphere(startPos, 0.3f);

                    UnityEditor.Handles.Label(
                        startPos + Vector3.up * 0.5f,
                        $"START\n[{startIdx}]",
                        new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = Color.green },
                            fontSize = 12,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        }
                    );

                    // Línea desde NPC a START
                    UnityEditor.Handles.color = new Color(0, 1, 0, 0.5f);
                    UnityEditor.Handles.DrawDottedLine(transform.position, startPos, 3f);

                    float distToStart = Vector3.Distance(transform.position, startPos);
                    UnityEditor.Handles.Label(
                        Vector3.Lerp(transform.position, startPos, 0.5f),
                        $"{distToStart:F1}m",
                        new GUIStyle() { normal = new GUIStyleState() { textColor = Color.white } }
                    );
                }

                // === NODO GOAL (objetivo final) ===
                if (validTarget)
                {
                    Vector3 goalPos = fleeGraph.nodePositions[fleeTargetNodeIndex];

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(goalPos, 0.25f);
                    Gizmos.DrawWireSphere(goalPos, fleeWaypointReach);

                    UnityEditor.Handles.Label(
                        goalPos + Vector3.up * 0.5f,
                        $"SAFE\n[{fleeTargetNodeIndex}]",
                        new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = Color.yellow },
                            fontSize = 12,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        }
                    );
                }

                // === PATH COMPUTADO (lo importante) ===
                if (_pathLen > 0 && _worldPath != null)
                {
                    int count = Mathf.Min(_pathLen, _worldPath.Length);

                    // ==== DIBUJAR LÍNEA DEL PATH ====
                    Vector3[] pathPoints = new Vector3[count];
                    for (int i = 0; i < count; i++)
                    {
                        pathPoints[i] = _worldPath[i];
                    }

                    UnityEditor.Handles.color = new Color(0.8f, 0.8f, 1f, 1f);
                    UnityEditor.Handles.DrawAAPolyLine(6.0f, pathPoints);

                    // ==== DIBUJAR CADA WAYPOINT ====
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 wp = _worldPath[i];

                        // Color según posición en path
                        bool isFirst = (i == 0);
                        bool isLast = (i == count - 1);
                        bool isCurrent = (_pathFollower != null && _pathFollower.CurrentIndex == i);

                        Color wpColor;
                        float wpSize;
                        string wpLabel;

                        if (isCurrent)
                        {
                            // Waypoint ACTUAL (verde brillante, grande)
                            wpColor = new Color(0.2f, 1f, 0.2f, 1f);
                            wpSize = 0.58f;
                            wpLabel = $"→ WP {i} ←\nCURRENT";

                            Gizmos.color = wpColor;
                            Gizmos.DrawSphere(wp, wpSize);
                            Gizmos.DrawWireSphere(wp, fleeWaypointReach);
                        }
                        else if (isFirst)
                        {
                            // Primer waypoint (verde claro)
                            wpColor = new Color(0.5f, 1f, 0.5f, 0.9f);
                            wpSize = 0.52f;
                            wpLabel = $"WP {i}\nFIRST";
                        }
                        else if (isLast)
                        {
                            // Último waypoint (amarillo)
                            wpColor = new Color(1f, 0.9f, 0f, 0.9f);
                            wpSize = 0.55f;
                            wpLabel = $"WP {i}\nLAST";
                        }
                        else
                        {
                            // Waypoint intermedio (celeste)
                            wpColor = new Color(0f, 0.7f, 1f, 0.8f);
                            wpSize = 0.50f;
                            wpLabel = $"WP {i}";
                        }

                        Gizmos.color = wpColor;
                        Gizmos.DrawSphere(wp, wpSize);

                        // Etiqueta con número e info
                        UnityEditor.Handles.Label(
                            wp + Vector3.up * 0.15f,
                            wpLabel,
                            new GUIStyle()
                            {
                                normal = new GUIStyleState() { textColor = wpColor },
                                fontSize = 10,
                                fontStyle = FontStyle.Bold,
                                alignment = TextAnchor.MiddleCenter
                            }
                        );

                        // Dibujar distancias entre waypoints
                        if (i < count - 1)
                        {
                            float segmentDist = Vector3.Distance(wp, _worldPath[i + 1]);
                            Vector3 midpoint = Vector3.Lerp(wp, _worldPath[i + 1], 0.5f);
                            UnityEditor.Handles.Label(
                                midpoint + Vector3.up * 0.05f,
                                $"{segmentDist:F1}m",
                                new GUIStyle()
                                {
                                    normal = new GUIStyleState() { textColor = new Color(1, 1, 1, 0.7f) },
                                    fontSize = 8
                                }
                            );
                        }

                        // Dibujar flecha direccional
                        if (i < count - 1)
                        {
                            Vector3 dir = (_worldPath[i + 1] - wp).normalized;
                            Vector3 arrowPos = wp + dir * 0.3f;
                            float arrowSize = 0.15f;

                            UnityEditor.Handles.color = new Color(0f, 0.8f, 1f, 0.6f);
                            UnityEditor.Handles.ConeHandleCap(
                                0,
                                arrowPos,
                                Quaternion.LookRotation(dir),
                                arrowSize,
                                EventType.Repaint
                            );
                        }
                    }

                    // ==== INFO GENERAL DEL PATH ====
                    GUIStyle pathInfoStyle = new GUIStyle();
                    pathInfoStyle.normal.textColor = Color.cyan;
                    pathInfoStyle.fontSize = 11;
                    pathInfoStyle.fontStyle = FontStyle.Bold;
                    pathInfoStyle.alignment = TextAnchor.UpperLeft;

                    string pathInfo = $"PATH INFO:\n" +
                                      $"• Total waypoints: {_pathLen}\n" +
                                      $"• Current index: {_pathFollower?.CurrentIndex ?? -1}\n" +
                                      $"• Reached end: {(_pathFollower?.ReachedEnd ?? false ? "YES" : "NO")}\n" +
                                      $"• Reach distance: {fleeWaypointReach:F2}m";

                    if (_pathFollower != null && _pathFollower.CurrentIndex < count)
                    {
                        Vector3 currentWP = _worldPath[_pathFollower.CurrentIndex];
                        float distToCurrent = Vector3.Distance(transform.position, currentWP);
                        pathInfo += $"\n• Dist to current WP: {distToCurrent:F2}m";
                    }

                    UnityEditor.Handles.Label(
                        transform.position + Vector3.up * 1.2f + Vector3.right * 0.5f,
                        pathInfo,
                        pathInfoStyle
                    );

                    // Línea desde NPC al waypoint actual
                    if (_pathFollower != null && _pathFollower.CurrentIndex < count)
                    {
                        Vector3 currentWP = _worldPath[_pathFollower.CurrentIndex];
                        UnityEditor.Handles.color = new Color(0.2f, 1f, 0.2f, 0.5f);
                        UnityEditor.Handles.DrawDottedLine(transform.position, currentWP, 5f);
                    }
                }
                else if (validTarget)
                {
                    // Tiene grafo válido pero no path
                    UnityEditor.Handles.Label(
                        transform.position + Vector3.up * 0.7f,
                        "⚠ NO PATH COMPUTED",
                        new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = Color.yellow },
                            fontSize = 12,
                            fontStyle = FontStyle.Bold
                        }
                    );
                }
            }
            else
            {
                // Sin grafo
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 0.5f,
                    "⚠ NO FLEE GRAPH ASSIGNED",
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.red },
                        fontSize = 14,
                        fontStyle = FontStyle.Bold
                    }
                );
            }

            // === VELOCIDAD ACTUAL ===
            if (_vel.magnitude > 0.1f)
            {
                UnityEditor.Handles.color = Color.magenta;
                UnityEditor.Handles.DrawAAPolyLine(
                    4f,
                    transform.position,
                    transform.position + _vel
                );
                UnityEditor.Handles.Label(
                    transform.position + _vel * 0.5f,
                    $"Velocity: {_vel.magnitude:F1} m/s",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.magenta } }
                );
            }
        }
    }
#endif
#endif

    #endregion

    #region DebugPathfinding

    [ContextMenu("Debug Graph Info")]
    void DebugGraphInfo()
    {
        if (fleeGraph == null)
        {
            Debug.LogError($"[{name}] fleeGraph is NULL!");
            return;
        }

        Debug.Log($"=== GRAPH INFO [{name}] ===");
        Debug.Log($"Node count: {fleeGraph.NodeCount}");
        Debug.Log($"Target index: {fleeTargetNodeIndex}");
        Debug.Log($"Target valid: {fleeTargetNodeIndex >= 0 && fleeTargetNodeIndex < fleeGraph.NodeCount}");

        // Mostrar primeros 5 nodos
        for (int i = 0; i < Mathf.Min(5, fleeGraph.NodeCount); i++)
        {
            var neighbors = fleeGraph.neighbors[i].data;
            Debug.Log($"  Node {i}: pos={fleeGraph.nodePositions[i]}, neighbors={neighbors.Length} [{string.Join(",", neighbors)}]");
        }

        // Verificar conectividad del target
        if (fleeTargetNodeIndex >= 0 && fleeTargetNodeIndex < fleeGraph.NodeCount)
        {
            var targetNeighbors = fleeGraph.neighbors[fleeTargetNodeIndex].data;
            Debug.Log($"  Target node {fleeTargetNodeIndex}: neighbors={targetNeighbors.Length}");
        }

        Debug.Log("=======================");
    }

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