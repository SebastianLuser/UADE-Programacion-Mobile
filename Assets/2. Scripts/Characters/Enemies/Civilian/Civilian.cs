using UnityEngine;
using Game.AI.Steering;
using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using System.Collections.Generic;

public class Civilian : BaseCharacter, IUpdatable, IUseFsm
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

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool canAttack = false;        // Civilians typically don't attack

    // Component references
    private Transform player;
    private IPlayerDetector playerDetector;
    private IBlackboard blackboard;
    private Renderer meshRenderer;
    private Material originalMaterial;
    private Color originalColor;

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
        InitializeComponents();
    }

    private void Start()
    {
        InitializeSteering();
        FindPlayer();
        InitializeScriptableObjectFSM();
        RegisterWithUpdateManager();
    }

    public void OnUpdate(float deltaTime)
    {
        if (!isAlive) return;

        // Use ScriptableObject FSM if enabled, otherwise fallback to legacy system
        if (useFSM && stateMachine != null)
        {
            UpdateFsm();
        }
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // Get or add PlayerDetector
        playerDetector = GetComponent<IPlayerDetector>();
        if (playerDetector == null)
        {
            var detectorComponent = gameObject.AddComponent<PlayerDetector>();
            playerDetector = detectorComponent;

            if (enableDebugLogs)
                Logger.LogInfo($"Civilian {gameObject.name}: Added PlayerDetector component");
        }

        // Get renderer for color changes during attacks
        meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer != null && meshRenderer.material != null)
        {
            originalMaterial = meshRenderer.material;
            originalColor = meshRenderer.material.color;
        }

        // Get blackboard service (read-only access)
        blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null && enableDebugLogs)
        {
            Logger.LogWarning($"Civilian {gameObject.name}: Blackboard service not available");
        }
    }

    private void InitializeSteering()
    {
        // Initialize steering physics (identical to Guard)
        _vel = Vector3.zero;
        obstacleAvoidance = new ObstacleAvoidance(transform, avoidRadius, avoidAngle, personalArea, obstaclesMask);
        currentMaxSpeed = walkSpeed;

        if (enableDebugLogs)
            Logger.LogInfo($"Civilian {gameObject.name}: Steering initialized");
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
                    Logger.LogInfo($"Civilian {gameObject.name}: Found player at {player.name}");
            }
        }
    }

    private void RegisterWithUpdateManager()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        if (updateManager != null)
        {
            updateManager.RegisterUpdatable(this);
            if (enableDebugLogs)
                Logger.LogInfo($"Civilian {gameObject.name}: Registered with UpdateManager");
        }
    }

    private void InitializeFSM()
    {
        // Legacy FSM fallback - Start in Idle state
        currentState = CivilianState.Idle;
        
        if (enableDebugLogs)
            Logger.LogInfo($"Civilian {gameObject.name}: Legacy FSM initialized");
    }

    private void InitializeScriptableObjectFSM()
    {
        if (useFSM && stateDataList != null && stateDataList.Count > 0)
        {
            stateMachine = new StateMachine(stateDataList, this);
            
            if (enableDebugLogs)
                Logger.LogInfo($"Civilian {gameObject.name}: ScriptableObject FSM initialized with {stateDataList.Count} states");
        }
        else
        {
            // Fallback to legacy FSM
            InitializeFSM();
            
            if (enableDebugLogs)
                Logger.LogInfo($"Civilian {gameObject.name}: Using legacy FSM (ScriptableObject FSM disabled or no states configured)");
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
        var playerHealth = player.GetComponent<ICharacter>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(meleeDamage);
            
            if (enableDebugLogs)
                Logger.LogInfo($"Civilian {gameObject.name}: Dealt {meleeDamage} melee damage to player");
        }
        else
        {
            // Fallback: try GameStateManager
            var gameStateManager = ServiceLocator.Get<GameStateManager>();
            if (gameStateManager != null)
            {
                // gameStateManager.ApplyMeleeHit(meleeDamage);
                if (enableDebugLogs)
                    Logger.LogInfo($"Civilian {gameObject.name}: Applied melee hit via GameStateManager");
            }
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

    #endregion

    #region Steering Physics (Identical to Guard)

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
    public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        // 1) Update velocity using physics integration
        _vel = Integrate(steering, Time.deltaTime);

        // 2) Pass velocity through obstacle avoidance
        Vector3 avoidedVel = obstacleAvoidance.GetDir2(_vel, false);

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
    }

    #endregion

    #region Detection

    /// <summary>
    /// Check if civilian has line of sight to player
    /// </summary>
    public bool HasLoS()
    {
        if (player == null || playerDetector == null) return false;

        // Delegate to PlayerDetector with distance and FOV checks
        var detectionResult = playerDetector.CanSeePlayer(player);

        // Additional distance check
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        bool inRange = distanceToPlayer <= sightRange;

        // Additional FOV check
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        Vector3 forward = transform.forward;
        float angle = Vector3.Angle(forward, directionToPlayer);
        bool inFOV = angle <= sightFOV * 0.5f;

        return detectionResult && inRange && inFOV;
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
            Logger.LogInfo($"{gameObject.name}: Civilian shooting at {direction}");
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
        if (blackboard != null && player != null)
        {
            blackboard.SetValue(BlackboardKeys.PLAYER_TRANSFORM, player);
            blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, player.position);
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
        
        if (useFSM && stateMachine != null)
        {
            var currentState = stateMachine.GetCurrentState();
            Debug.Log($"Current State: {(currentState?.State?.StateName ?? "None")}");
            Debug.Log($"ScriptableObject FSM Active: True");
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
        Debug.Log("=======================");
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        if (updateManager != null)
        {
            updateManager.UnregisterUpdatable(this);
        }
    }

    #endregion
}