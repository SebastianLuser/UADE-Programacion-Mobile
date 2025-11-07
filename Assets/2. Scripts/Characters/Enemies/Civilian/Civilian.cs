using UnityEngine;
using Game.AI.Steering;
using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using System.Collections.Generic;
using System.Linq;
using Services.MicroServices.BlackboardService;
using Services;
using Services.MicroServices.UpdateService;
using Unity.Assertions;

[RequireComponent(typeof(SteeringMotor), typeof(PerceptionSensor))]
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
    [SerializeField] private LayerMask perceptionObstacleMask = 0;

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

    [Header("Movement Components")]
    [SerializeField] private SteeringMotor steeringMotor;

    [Header("Perception Components")]
    [SerializeField] private PerceptionSensor perceptionSensor;

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

    // Component references
    private Transform player;
    private IBlackboardService m_blackboardService;
    private Renderer meshRenderer;
    private Material originalMaterial;
    private Color originalColor;
    private CivilianDecisionTreeRunner decisionTreeRunner;

    // Steering/perception components
    private SteeringMotor Motor => steeringMotor != null ? steeringMotor : (steeringMotor = GetComponent<SteeringMotor>());
    private PerceptionSensor Sensor => perceptionSensor != null ? perceptionSensor : (perceptionSensor = GetComponent<PerceptionSensor>());
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
    public Vector3 CurrentVelocity => steeringMotor != null ? steeringMotor.CurrentVelocity : Vector3.zero;
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
        steeringMotor = Motor;
        perceptionSensor = Sensor;
        ConfigurePerceptionSensor();

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
        steeringMotor = Motor;
        steeringMotor.ResetVelocity();
        SetCurrentMaxSpeed(walkSpeed);

        if (enableDebugLogs)
            MyLogger.LogInfo($"Civilian {gameObject.name}: Steering initialized");
    }

    private void ConfigurePerceptionSensor()
    {
        if (perceptionSensor == null) return;

        LayerMask mask = perceptionObstacleMask.value == 0 ? LayerMask.GetMask("Obstacles") : perceptionObstacleMask;
        perceptionSensor.Configure(sightRange, sightFOV, loseSightGrace, meleeRange, mask);

        if (player != null)
        {
            perceptionSensor.SetTarget(player, false);
        }
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                player = playerGO.transform;
                perceptionSensor?.SetTarget(player);
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

    #endregion

    #region FSM Management

    /// <summary>
    /// Set the current max speed for movement
    /// </summary>
    /// <param name="speed">The new max speed</param>
    public void SetCurrentMaxSpeed(float speed)
    {
        currentMaxSpeed = speed;
        steeringMotor?.SetMaxSpeed(currentMaxSpeed);
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

    public void ApplySteering(Vector3 steering)
    {
        if (!isAlive || steeringMotor == null) return;

        Vector3 avoidedVel = steeringMotor.ApplySteering(steering, 3f);

        if (avoidedVel.sqrMagnitude > 0.001f)
        {
            currentMovementDirection = avoidedVel.normalized;
            currentMovementSpeed = avoidedVel.magnitude;
        }
    }

    #endregion

    #region Detection

    /// <summary>
    /// Check if civilian has line of sight to player
    /// </summary>
    public bool HasLoS()
    {
        if (perceptionSensor == null) return false;

        var detection = perceptionSensor.GetDetectionResult();

        if (detection.canSeePlayer)
        {
            lastKnownPlayerPosition = perceptionSensor.LastKnownPosition;
            hasEverSeenPlayer = true;
        }
        else if (perceptionSensor.HasRecentContact)
        {
            lastKnownPlayerPosition = perceptionSensor.LastKnownPosition;
        }

        return detection.canSeePlayer;
    }

    /// <summary>
    /// Get distance to player
    /// </summary>
    public float GetDistanceToPlayer()
    {
        return perceptionSensor != null ? perceptionSensor.DistanceToTarget : float.MaxValue;
    }

    /// <summary>
    /// Check if player is in melee range
    /// </summary>
    public bool IsPlayerInMeleeRange()
    {
        return perceptionSensor != null && perceptionSensor.IsTargetWithinRange(meleeRange);
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

        SetCurrentMaxSpeed(walkSpeed);
        Vector3 steering = Steering.Seek(transform.position, transform.position + driftDirection, CurrentVelocity, walkSpeed);
        ApplySteering(steering * 0.3f); // Gentle movement
    }

    private void HandleFleeingState(bool canSeePlayer, float distanceToPlayer)
    {
        SetCurrentMaxSpeed(fleeSpeed);

        if (canSeePlayer)
        {
            // Direct flee from player
            Vector3 steering = Steering.Flee(transform.position, player.position, CurrentVelocity, fleeSpeed);
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
        SetCurrentMaxSpeed(evadeSpeed);

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

            Vector3 steering = Steering.Evade(transform.position, CurrentVelocity, lastKnownPlayerPosition, playerVel, evadeSpeed);
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
        SetCurrentMaxSpeed(walkSpeed);
        Vector3 brakeForce = -CurrentVelocity * 3f;
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
        Vector3 steering = targetVel - CurrentVelocity;
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
        perceptionSensor?.SetTarget(player, true);
        
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
        if (CurrentVelocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, CurrentVelocity);
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
        Debug.Log($"Current Velocity: {CurrentVelocity.magnitude:F2}");
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

        if (perceptionSensor != null)
        {
            var detection = perceptionSensor.GetDetectionResult();
            if (detection.canSeePlayer)
            {
                lastKnownPlayerPosition = perceptionSensor.LastKnownPosition;
                hasEverSeenPlayer = true;
            }
            else if (perceptionSensor.HasRecentContact)
            {
                lastKnownPlayerPosition = perceptionSensor.LastKnownPosition;
            }
        }

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
