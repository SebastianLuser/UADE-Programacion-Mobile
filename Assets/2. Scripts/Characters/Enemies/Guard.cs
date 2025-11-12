using System;
using UnityEngine;
using Game.AI.Steering;
using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using System.Collections.Generic;
using ScriptableObjects.Bullets;
using Services.MicroServices.BlackboardService;
using Services;
using Services.MicroServices.PoolObjectsService;
using Services.MicroServices.UpdateService;
using Unity.Assertions;

//todo revisar pasar a MVC
public class Guard : BaseCharacter, IUseFsm, IUpdateListener
{
    //todo utilizar scriptable object
    [Header("Guard Settings")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float fieldOfView = 90f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float idleTime = 3f;
    [SerializeField] private float searchTime = 5f;
    [SerializeField] private float baseRotationSpeed = 2f;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private BulletData bulletData;

    [Header("FSM Patrol Settings")]
    [SerializeField] private int loopsToIdle = 3;
    [SerializeField] private float idleSeconds = 5f;

    [Header("State Machine Configuration")]
    [SerializeField] private List<StateData> stateDataList = new List<StateData>();
    [SerializeField] private bool useFSM = true;
    
    [Header("AI Configuration")]
    [SerializeField] private AIPersonalityType personalityType = AIPersonalityType.Aggressive;
    [SerializeField] private bool enableNewAISystem = true;

    [Header("Flocking Integration")]
    [SerializeField] private bool useFlocking = false;
    [SerializeField] private float baseForceWeight = 0.8f;
    [SerializeField] private float flockForceWeight = 0.2f;

    [Header("Steering Physics")]
    [SerializeField] private float mass = 1f;
    [SerializeField] private float maxForce = 25f;
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float slowingDistance = 2f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstaclesMask = -1;
    [SerializeField] private float avoidRadius = 2f;
    [SerializeField] private float avoidAngle = 90f;
    [SerializeField] private float personalArea = 0.5f;
    
    private Transform player;
    private Vector3 lastKnownPlayerPosition;
    private int currentPatrolIndex;
    private float stateTimer;
    private bool isActivelyPatrolling = false;  // Track patrol state independently

    // FSM patrol tracking
    private int currentPatrolLoops = 0;
    private bool patrolDirection = true; // true = 0->N, false = N->0
    private bool hasReachedCurrentPatrolPoint = false;
    
    // AI System components
    private AIContext aiContext;
    private IBlackboardService m_blackboardService;
    private IPlayerDetector playerDetector;

    // Steering components
    private Vector3 _vel;
    private ObstacleAvoidance obstacleAvoidance;
    private FlockingSystem.FlockingEntity flockingEntity;

    // FSM components
    private StateMachine stateMachine;
    
    // Movement state for IAIMovementController
    private Vector3 currentMovementDirection;
    private float currentMovementSpeed;
    private Vector3 currentDestination;
    private MovementMode currentMovementMode = MovementMode.Walk;
    private MovementStatus currentMovementStatus = MovementStatus.Idle;
    private bool isMovementPaused = false;
    private Transform steeringTarget;
    private Bounds movementConstraints;
    private bool hasMovementConstraints = false;
    
    // Callbacks
    public System.Action OnMovementComplete { get; set; }
    public System.Action OnMovementBlocked { get; set; }
    
    public float DetectionRange => detectionRange;
    public float AttackRange => attackRange;
    public float FieldOfView => fieldOfView;
    public float PatrolSpeed => patrolSpeed;
    public float ChaseSpeed => chaseSpeed;
    public float IdleTime => idleTime;
    public float SearchTime => searchTime;
    public float BaseRotationSpeed => baseRotationSpeed;
    public Transform[] PatrolPoints => patrolPoints;
    public Transform Player => player;
    public Vector3 LastKnownPlayerPosition
    {
        get => lastKnownPlayerPosition;
        set => lastKnownPlayerPosition = value;
    }
    public int CurrentPatrolIndex
    {
        get => currentPatrolIndex;
        set => currentPatrolIndex = value;
    }
    public float StateTimer
    {
        get => stateTimer;
        set => stateTimer = value;
    }

    // FSM Patrol Properties
    public int LoopsToIdle => loopsToIdle;
    public float IdleSeconds => idleSeconds;
    public int CurrentPatrolLoops
    {
        get => currentPatrolLoops;
        set => currentPatrolLoops = value;
    }
    public bool PatrolDirection
    {
        get => patrolDirection;
        set => patrolDirection = value;
    }
    public bool HasReachedCurrentPatrolPoint
    {
        get => hasReachedCurrentPatrolPoint;
        set => hasReachedCurrentPatrolPoint = value;
    }
    
    public bool IsActive => isAlive && gameObject.activeInHierarchy;
    
    // AI System access
    public AIContext AIContext => aiContext;
    public IBlackboardService BlackboardService => m_blackboardService;
    public AIPersonalityType PersonalityType => personalityType;

    // Steering Physics access
    public float Mass => mass;
    public float MaxForce => maxForce;
    public float MaxSpeed => maxSpeed;
    public float SlowingDistance => slowingDistance;
    public Vector3 CurrentVelocity => _vel;
    
    private static IPoolObjectsService PoolObjectsService => ServiceLocator.Get<IPoolObjectsService>();
    
    // MEJORA: Improved player detection using new AI system
    public bool CanSeePlayer()
    {
        if (enableNewAISystem && playerDetector != null)
        {
            return playerDetector.CanSeePlayer(player);
        }
        
        // Fallback to legacy detection
        return CanSeePlayerLegacy();
    }
    
    // MEJORA: Get advanced detection information
    public DetectionResult GetDetectionResult()
    {
        if (enableNewAISystem && playerDetector != null)
        {
            return playerDetector.GetCurrentDetectionResult();
        }
        
        return CanSeePlayerLegacy() ? DetectionResult.Clear : DetectionResult.None;
    }
    
    private bool CanSeePlayerLegacy()
    {
        if (player == null) return false;
        
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        
        if (angleToPlayer > fieldOfView / 2f) return false;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer > detectionRange) return false;
        
        return !Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, LayerMask.GetMask("Obstacles"));
    }
    
    protected override void Awake()
    {
        base.Awake();
        InitializeAISystem();
        SetupPatrolPoints();
        
        SubscribeUpdateService();
        
        // Start patrolling after a frame to ensure everything is initialized
        StartCoroutine(StartPatrolAfterFrame());
    }
    
    private System.Collections.IEnumerator StartPatrolAfterFrame()
    {
        yield return null; // Wait one frame
        
        StartPatrol();
    }
    
    private void InitializeAISystem()
    {
        if (enableNewAISystem)
        {
            // Initialize AI Context
            aiContext = gameObject.GetComponent<AIContext>();
            Assert.IsNotNull(aiContext);

            // Get blackboard service
            m_blackboardService = ServiceLocator.Get<IBlackboardService>();
            if (m_blackboardService == null)
            {
                MyLogger.LogWarning($"Guard {gameObject.name}: Blackboard service not available yet");
            }

            // Get player detector
            playerDetector = gameObject.GetComponent<IPlayerDetector>();
            Assert.IsNotNull(playerDetector);

            // Configure personality
            if (aiContext != null)
            {
                aiContext.SetPersonalityType(personalityType);
            }
        }

        // Initialize steering physics
        _vel = Vector3.zero;
        obstacleAvoidance = new ObstacleAvoidance(transform, avoidRadius, avoidAngle, personalArea, obstaclesMask);

        // Initialize flocking entity if present
        if (useFlocking)
        {
            flockingEntity = GetComponent<FlockingSystem.FlockingEntity>();
            if (flockingEntity == null)
            {
                MyLogger.LogWarning($"Guard {gameObject.name}: useFlocking enabled but FlockingEntity component not found");
            }
        }

        // Ensure maxSpeed is at least as fast as chaseSpeed for proper movement
        if (maxSpeed < chaseSpeed)
        {
            maxSpeed = chaseSpeed * 1.2f; // Give some headroom
            MyLogger.LogInfo($"Guard {gameObject.name}: Adjusted maxSpeed to {maxSpeed} to match chaseSpeed");
        }

        // Initialize FSM
        InitializeFSM();
    }

    private void InitializeFSM()
    {
        if (useFSM && stateDataList != null && stateDataList.Count > 0)
        {
            stateMachine = new StateMachine(stateDataList, this);
            MyLogger.LogInfo($"Guard {gameObject.name}: FSM initialized with {stateDataList.Count} states");
        }
        else
        {
            MyLogger.LogWarning($"Guard {gameObject.name}: FSM not initialized - useFSM: {useFSM}, stateDataList count: {stateDataList?.Count ?? 0}");
        }
    }
    
    private void Start()
    {
        StartCoroutine(DelayedStart());
    }
    
    private System.Collections.IEnumerator DelayedStart()
    {
        yield return null;
        
        // Ensure blackboard connection is established
        if (enableNewAISystem && m_blackboardService == null)
        {
            m_blackboardService = ServiceLocator.Get<IBlackboardService>();
        }
        
        // Find player if not set
        if (player == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                SetTargetTransform(playerGO.transform);
            }
        }
    }
    
    public void OnUpdate(float deltaTime)
    {
        
    }
    
    private void UpdateMovementSystem(float deltaTime)
    {
        // Only log movement system updates when there are issues or state changes
        if (isMovementPaused || !CanMove()) 
        {
            // Only log once per second when blocked to avoid spam
            if (Time.frameCount % 60 == 0)
            {
                MyLogger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Movement blocked - isPaused: {isMovementPaused}, CanMove: {CanMove()}");
            }
            return;
        }
        
        // Update steering target following
        if (steeringTarget != null && currentMovementStatus == MovementStatus.Following)
        {
            currentDestination = steeringTarget.position;
            Vector3 direction = (currentDestination - transform.position).normalized;
            currentMovementDirection = direction;
            // Only log steering updates occasionally
            if (Time.frameCount % 30 == 0)
            {
                MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Following steering target to {currentDestination}");
            }
        }
        
        // Debug patrol status only when reaching points or significant changes
        if (isActivelyPatrolling)
        {
            // Only log detailed patrol info every 2 seconds to reduce spam
            if (Time.frameCount % 120 == 0)
            {
                MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Currently patrolling - currentIndex: {currentPatrolIndex}, patrolPoints.Length: {(patrolPoints?.Length ?? 0)}");
                MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Current position: {transform.position}, Current destination: {currentDestination}");
                MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: isActivelyPatrolling: {isActivelyPatrolling}, MovementStatus: {currentMovementStatus}");
                
                if (patrolPoints != null && patrolPoints.Length > currentPatrolIndex)
                {
                    MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Target patrol point: {patrolPoints[currentPatrolIndex].position}");
                    MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Distance to target: {Vector3.Distance(transform.position, patrolPoints[currentPatrolIndex].position):F2}");
                }
            }
        }
        
        // Check if destination reached for patrol logic
        if (isActivelyPatrolling && patrolPoints != null && patrolPoints.Length > 0 && HasReachedDestination())
        {
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Reached patrol point {currentPatrolIndex}, moving to next");
            // Move to next patrol point
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            if (patrolPoints.Length > 0)
            {
                MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Moving to patrol point {currentPatrolIndex} at {patrolPoints[currentPatrolIndex].position}");
                MoveTo(patrolPoints[currentPatrolIndex].position, patrolSpeed);
            }
        }
        // Continue steering-based movement towards current destination
        else if (currentMovementStatus == MovementStatus.Moving && currentDestination != Vector3.zero)
        {
            // Use maxSpeed instead of currentMovementSpeed for more aggressive movement
            float targetSpeed = Mathf.Max(currentMovementSpeed, maxSpeed * 0.5f); // At least half max speed
            Vector3 steering = Steering.Seek(transform.position, currentDestination, _vel, targetSpeed);

            // Debug steering calculation
            if (Time.frameCount % 60 == 0)
            {
                MyLogger.LogInfo($"[STEERING CALC] Pos: {transform.position}, Target: {currentDestination}, Vel: {_vel}, TargetSpeed: {targetSpeed}");
                MyLogger.LogInfo($"[STEERING CALC] Calculated steering: {steering}");
            }

            ApplySteering(steering);
        }
        else if (currentMovementStatus == MovementStatus.Patrolling && patrolPoints != null && patrolPoints.Length > 0)
        {
            // Continue moving towards current patrol point
            if (currentPatrolIndex < patrolPoints.Length)
            {
                // Use Seek instead of Arrive for patrol movement to maintain constant speed
                Vector3 steering = Steering.Seek(transform.position, patrolPoints[currentPatrolIndex].position, _vel, patrolSpeed);
                ApplySteering(steering);
            }
        }
        
        // Check movement constraints
        if (hasMovementConstraints && !movementConstraints.Contains(transform.position))
        {
            MyLogger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Movement constrained at position {transform.position}");
            currentMovementStatus = MovementStatus.Constrained;
            OnMovementBlocked?.Invoke();
            return;
        }
    }
    
    private void UpdateAISystem()
    {
        if (enableNewAISystem && m_blackboardService != null && player != null)
        {
            // Update blackboard with current player information
            m_blackboardService.SetValue(BlackboardKeys.PLAYER_TRANSFORM, player);
            m_blackboardService.SetValue(BlackboardKeys.PLAYER_POSITION, player.position);
            
            // Update last known position if we can see the player
            if (CanSeePlayer())
            {
                lastKnownPlayerPosition = player.position;
                m_blackboardService.SetValue(BlackboardKeys.LAST_KNOWN_PLAYER_POSITION, lastKnownPlayerPosition);
            }
            
            // Update detection information
            var detectionResult = GetDetectionResult();
            m_blackboardService.SetValue($"Guard_{gameObject.GetInstanceID()}_DetectionLevel", detectionResult.level);
            m_blackboardService.SetValue($"Guard_{gameObject.GetInstanceID()}_CanSeePlayer", detectionResult.level > PlayerDetectionLevel.None);
        }
    }

    private void OnDisable()
    {
        UnsubscribeUpdateService();
    }

    private void SetupPatrolPoints()
    {
        MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: SetupPatrolPoints called");
        
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: No patrol points assigned, creating default ones");
            patrolPoints = new Transform[2];
            
            GameObject point1 = new GameObject("PatrolPoint1");
            point1.transform.position = transform.position + Vector3.forward * 5f;
            patrolPoints[0] = point1.transform;
            
            GameObject point2 = new GameObject("PatrolPoint2");
            point2.transform.position = transform.position + Vector3.back * 5f;
            patrolPoints[1] = point2.transform;
            
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Created patrol points at {point1.transform.position} and {point2.transform.position}");
        }
        else
        {
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Using {patrolPoints.Length} existing patrol points");
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Patrol point {i}: {patrolPoints[i].position}");
                }
                else
                {
                    MyLogger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Patrol point {i} is null!");
                }
            }
        }
    }
    
    public void StartPatrol()
    {
        MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: StartPatrol called");
        
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            MyLogger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Cannot start patrol - no patrol points assigned");
            return;
        }
        
        // Initialize patrol state
        currentPatrolIndex = 0;
        currentMovementStatus = MovementStatus.Patrolling;
        currentMovementMode = MovementMode.Walk;
        isActivelyPatrolling = true;  // Set patrol flag
        
        MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Starting patrol with {patrolPoints.Length} points, moving to point 0");
        
        // Move to first patrol point
        MoveTo(patrolPoints[currentPatrolIndex].position, patrolSpeed);
        
        MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Patrol started successfully");
    }
    
    public void StopPatrol()
    {
        if (isActivelyPatrolling)
        {
            isActivelyPatrolling = false;
            currentMovementStatus = MovementStatus.Idle;
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Patrol stopped");
        }
    }
    
    
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

        // Clamp velocity to maximum speed
        if (newVel.sqrMagnitude > maxSpeed * maxSpeed)
        {
            newVel = newVel.normalized * maxSpeed;
        }

        return newVel;
    }

    /// <summary>
    /// Apply steering force with optional flocking blend, obstacle avoidance and movement
    /// </summary>
    public void ApplySteering(Vector3 steering)
    {
        if (!isAlive) return;

        Vector3 finalSteering = steering;

        // Blend flocking forces if enabled and active
        if (useFlocking && flockingEntity != null && ShouldFlock())
        {
            Vector3 flockingForce = flockingEntity.GetFlockingForce();
            finalSteering = (steering * baseForceWeight) + (flockingForce * flockForceWeight);

            if (Time.frameCount % 60 == 0)
            {
                MyLogger.LogInfo($"[FLOCK BLEND] {gameObject.name}: base={steering.magnitude:F2}, flock={flockingForce.magnitude:F2}, final={finalSteering.magnitude:F2}");
            }
        }

        // 1) Update velocity using physics integration
        _vel = Integrate(finalSteering, Time.deltaTime);

        // 2) Pass velocity through obstacle avoidance
        Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(_vel, false);

        // 3) Move and face movement direction
        if (avoidedVel.sqrMagnitude > 0.001f)
        {
            // Use actual deltaTime - the steering system works correctly now
            float effectiveDeltaTime = Time.deltaTime;

            // Update position
            Vector3 movement = avoidedVel * effectiveDeltaTime;
            transform.position += movement;

            // Update movement controller state
            currentMovementDirection = avoidedVel.normalized;
            currentMovementSpeed = avoidedVel.magnitude;

            // Face movement direction with faster rotation
            if (avoidedVel.magnitude > 0.1f)
            {
                Vector3 lookDirection = avoidedVel.normalized;
                lookDirection.y = 0f; // Keep rotation in XZ plane

                // Use faster rotation speed for more responsive steering
                float rotationSpeed = baseRotationSpeed * 3f;
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        // Debug velocity more frequently during development
        if (Time.frameCount % 30 == 0)
        {
            float effectiveDeltaTime = Mathf.Max(Time.deltaTime, 0.016f);
            MyLogger.LogInfo($"[STEERING DEBUG] {gameObject.name}: Vel: {_vel.magnitude:F2}, AvoidedVel: {avoidedVel.magnitude:F2}, MaxSpeed: {maxSpeed}");
            MyLogger.LogInfo($"[STEERING DEBUG] Raw steering: {finalSteering.magnitude:F2}, DeltaTime: {Time.deltaTime:F4}, Effective: {effectiveDeltaTime:F4}, Movement: {(avoidedVel * effectiveDeltaTime).magnitude:F4}");
        }
    }

    /// <summary>
    /// Determine if flocking should be active based on current guard state
    /// </summary>
    public bool ShouldFlock()
    {
        if (!useFlocking || !isAlive)
            return false;

        if (useFSM && stateMachine != null)
        {
            var stateName = stateMachine.GetCurrentState()?.State?.StateName;
            // Enable flocking during patrol and chase for group coordination
            return stateName == "S_GuardPatrol" || stateName == "S_GuardChase";
        }

        // Fallback heuristic: flock while patrolling or when seeing player (group chase)
        return isActivelyPatrolling || CanSeePlayer();
    }

    /// <summary>
    /// Configure steering physics parameters at runtime
    /// </summary>
    public void ConfigureSteering(float newMass, float newMaxForce, float newMaxSpeed, float newSlowingDistance)
    {
        mass = newMass;
        maxForce = newMaxForce;
        maxSpeed = newMaxSpeed;
        slowingDistance = newSlowingDistance;
    }

    /// <summary>
    /// Configure obstacle avoidance parameters at runtime
    /// </summary>
    public void ConfigureObstacleAvoidance(float radius, float angle, float personalArea, LayerMask obstacleMask)
    {
        avoidRadius = radius;
        avoidAngle = angle;
        personalArea = personalArea;
        obstaclesMask = obstacleMask;

        // Recreate obstacle avoidance with new parameters
        obstacleAvoidance = new ObstacleAvoidance(transform, avoidRadius, avoidAngle, personalArea, obstaclesMask);
    }

    #endregion

    public override void Move(Vector3 direction)
    {
        if (!isAlive)
        {
            // Only log this occasionally if it's being called repeatedly
            if (Time.frameCount % 120 == 0)
            {
                MyLogger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Move called but not alive");
            }
            return;
        }

        // Legacy direct movement - use maxSpeed instead of characterData.moveSpeed
        Vector3 targetVel = direction.normalized * maxSpeed;
        Vector3 steering = targetVel - _vel;
        ApplySteering(steering);
    }
    
    public override void Shoot(Vector3 direction)
    {
        if (!isAlive || !CanShoot()) return;
        
        lastShootTime = Time.time;
        CreateBullet(direction);
        
        // MEJORA: Update blackboard with combat information
        if (enableNewAISystem && m_blackboardService != null)
        {
            m_blackboardService.SetValue($"Guard_{gameObject.GetInstanceID()}_LastShootTime", lastShootTime);
            m_blackboardService.SetValue($"Guard_{gameObject.GetInstanceID()}_ShootDirection", direction);
        }
    }
    
    private void CreateBullet(Vector3 p_direction)
    {
        var l_spawnPosition = transform.position + Vector3.up * 0.5f + p_direction * 0.8f;
        var l_bullet = PoolObjectsService.GetOrCreateObject(bulletData.Prefab);
        l_bullet.OnDeactivate += OnDeactivateBulletHandler;
        l_bullet.InitializeBullet(bulletData, l_spawnPosition, p_direction, BulletOwner.Guard, gameObject.name);
    }

    private void OnDeactivateBulletHandler(BulletObject p_bullet)
    {
        p_bullet.OnDeactivate -= OnDeactivateBulletHandler;
        PoolObjectsService.ReturnObject(p_bullet);
    }

    #region AI System Integration
    
    public Transform GetModelTransform()
    {
        return transform;
    }
    
    public void SetTargetTransform(Transform p_target)
    {
        player = p_target;
        
        // Update AI system when target changes
        if (enableNewAISystem && m_blackboardService != null && player != null)
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
    
    #region IAIMovementController Implementation
    
    public Vector3 GetCurrentVelocity()
    {
        return _vel;
    }
    
    public Vector3 GetCurrentDirection()
    {
        return currentMovementDirection;
    }
    
    public float GetCurrentSpeed()
    {
        return currentMovementSpeed;
    }
    
    public bool IsMoving()
    {
        return currentMovementSpeed > 0.1f && !isMovementPaused;
    }
    
    public void SetMovementSpeed(float speed)
    {
        currentMovementSpeed = speed;
    }
    
    public void SetMovementDirection(Vector3 direction)
    {
        currentMovementDirection = direction.normalized;
    }
    
    public void StopMovement()
    {
        currentMovementDirection = Vector3.zero;
        currentMovementSpeed = 0f;
        currentMovementStatus = MovementStatus.Idle;
        currentDestination = transform.position;
    }
    
    public float GetMaxSpeed()
    {
        return characterData.moveSpeed;
    }
    
    public bool CanMove()
    {
        return isAlive && IsActive;
    }
    
    // MEJORA: Extended IAIMovementController methods
    public void MoveTo(Vector3 target, float speed)
    {
        MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: MoveTo called - Target: {target}, Speed: {speed}, CanMove: {CanMove()}");

        if (!CanMove())
        {
            MyLogger.LogWarning($"[PATROL DEBUG] {gameObject.name}: MoveTo blocked - CanMove returned false");
            return;
        }

        currentDestination = target;
        currentMovementSpeed = speed;
        currentMovementStatus = MovementStatus.Moving;

        // Check constraints
        if (hasMovementConstraints && !movementConstraints.Contains(target))
        {
            MyLogger.LogWarning($"[PATROL DEBUG] {gameObject.name}: MoveTo constrained - target outside bounds");
            currentMovementStatus = MovementStatus.Constrained;
            OnMovementBlocked?.Invoke();
            return;
        }

        // For patrol movement, use Seek to maintain constant speed
        // For precise positioning (like reaching a specific point), use Arrive
        float distanceToTarget = Vector3.Distance(transform.position, target);
        Vector3 steering;

        if (distanceToTarget > slowingDistance * 2f)
        {
            // Use Seek for constant speed when far from target
            steering = Steering.Seek(transform.position, target, _vel, speed);
        }
        else
        {
            // Use Arrive for smooth stop near target
            steering = Steering.Arrive(transform.position, target, _vel, speed, slowingDistance);
        }

        ApplySteering(steering);

        MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: MoveTo using steering - Destination: {currentDestination}, Status: {currentMovementStatus}");
    }
    
    public void Flee(Vector3 fromPosition, float speed)
    {
        if (!CanMove()) return;

        currentMovementSpeed = speed;
        currentMovementStatus = MovementStatus.Fleeing;

        // Use steering behavior for fleeing
        Vector3 steering = Steering.Flee(transform.position, fromPosition, _vel, speed);
        ApplySteering(steering);

        // Set destination for debugging/tracking purposes
        Vector3 fleeDirection = (transform.position - fromPosition).normalized;
        currentDestination = transform.position + fleeDirection * 10f;
    }
    
    public void Patrol(Transform[] waypoints, float speed)
    {
        if (!CanMove() || waypoints == null || waypoints.Length == 0) return;
        
        patrolPoints = waypoints;
        currentMovementSpeed = speed;
        currentMovementStatus = MovementStatus.Patrolling;
        
        // Move to current patrol point
        if (currentPatrolIndex < patrolPoints.Length)
        {
            MoveTo(patrolPoints[currentPatrolIndex].position, speed);
        }
    }
    
    public void Stop()
    {
        StopMovement();
    }
    
    public bool HasReachedDestination()
    {
        if (currentMovementStatus == MovementStatus.Idle) 
        {
            // Only log this occasionally as it might be checked frequently
            if (Time.frameCount % 120 == 0)
            {
                MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: HasReachedDestination - Status is Idle, returning true");
            }
            return true;
        }
        
        float distanceToDestination = Vector3.Distance(transform.position, currentDestination);
        bool reached = distanceToDestination < 0.5f;
        
        // Only log distance checks when close to destination or occasionally
        if (reached || Time.frameCount % 120 == 0)
        {
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: HasReachedDestination - Distance: {distanceToDestination:F2}, Reached: {reached}, Status: {currentMovementStatus}");
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Current pos: {transform.position}, Destination: {currentDestination}");
        }
        
        if (reached && currentMovementStatus == MovementStatus.Moving)
        {
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Destination reached, changing status to Idle");
            currentMovementStatus = MovementStatus.Idle;
            OnMovementComplete?.Invoke();
        }
        
        return reached;
    }
    
    public void SetSteeringTarget(Transform target)
    {
        steeringTarget = target;
        currentMovementStatus = MovementStatus.Following;
    }
    
    public void SetMovementMode(MovementMode mode)
    {
        currentMovementMode = mode;
        
        // Adjust speed based on mode
        float speedMultiplier = mode switch
        {
            MovementMode.Sneak => 0.5f,
            MovementMode.Walk => 1f,
            MovementMode.Run => 1.5f,
            MovementMode.Sprint => 2f,
            _ => 1f
        };
        
        currentMovementSpeed = GetContextualSpeed() * speedMultiplier;
    }
    
    public void SetMovementConstraints(Bounds allowedArea)
    {
        movementConstraints = allowedArea;
        hasMovementConstraints = true;
    }
    
    public void ClearMovementConstraints()
    {
        hasMovementConstraints = false;
    }
    
    public MovementStatus GetMovementStatus()
    {
        return currentMovementStatus;
    }
    
    public Vector3 GetCurrentDestination()
    {
        return currentDestination;
    }
    
    public void FaceDirection(Vector3 direction, float rotationSpeed = -1f)
    {
        if (!CanMove()) return;
        
        if (direction.magnitude > 0.1f)
        {
            float speed = rotationSpeed > 0 ? rotationSpeed : baseRotationSpeed;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, speed * Time.deltaTime);
        }
    }
    
    public void PauseMovement()
    {
        isMovementPaused = true;
    }
    
    public void ResumeMovement()
    {
        isMovementPaused = false;
    }
    
    public bool IsMovementPaused => isMovementPaused;
    
    #endregion
    
    #region MEJORA: Advanced AI Methods
    
    /// <summary>
    /// Get threat assessment based on current detection and situation
    /// </summary>
    public float GetThreatLevel()
    {
        if (aiContext != null)
        {
            return aiContext.GetThreatLevel();
        }
        
        // Fallback calculation
        if (!CanSeePlayer()) return 0f;
        
        float distance = Vector3.Distance(transform.position, player.position);
        float distanceFactor = 1f - Mathf.Clamp01(distance / detectionRange);
        
        return distanceFactor * 0.7f; // Base threat level
    }
    
    /// <summary>
    /// Get confidence in current player information
    /// </summary>
    public float GetInformationConfidence()
    {
        if (aiContext != null)
        {
            return aiContext.GetInformationConfidence();
        }
        
        // Fallback: if we can see player, confidence is high
        return CanSeePlayer() ? 1.0f : 0.2f;
    }
    
    /// <summary>
    /// Should the guard investigate based on detection level and personality
    /// </summary>
    public bool ShouldInvestigate()
    {
        var detectionResult = GetDetectionResult();
        
        return personalityType switch
        {
            AIPersonalityType.Aggressive => detectionResult.level >= PlayerDetectionLevel.Peripheral,
            AIPersonalityType.Cautious => detectionResult.level >= PlayerDetectionLevel.Partial,
            AIPersonalityType.Conservative => detectionResult.level >= PlayerDetectionLevel.Clear,
            _ => detectionResult.level >= PlayerDetectionLevel.Partial
        };
    }
    
    /// <summary>
    /// Should the guard enter combat based on detection and personality
    /// </summary>
    public bool ShouldAttack()
    {
        var detectionResult = GetDetectionResult();
        float distance = Vector3.Distance(transform.position, player.position);

        bool inAttackRange = distance <= attackRange;
        bool canSee = detectionResult.level >= PlayerDetectionLevel.Clear;

        return personalityType switch
        {
            AIPersonalityType.Aggressive => canSee && distance <= detectionRange,
            AIPersonalityType.Cautious => canSee && inAttackRange,
            AIPersonalityType.Conservative => canSee && inAttackRange && GetThreatLevel() > 0.7f,
            _ => canSee && inAttackRange
        };
    }

    /// <summary>
    /// Pursue the player using steering behaviors
    /// </summary>
    public void PursuePlayer()
    {
        if (player == null) return;

        Vector3 playerVel = Vector3.zero;
        var playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerVel = playerRb.linearVelocity;
        }

        Vector3 steering = Steering.Pursuit(transform.position, _vel, player.position, playerVel, chaseSpeed);
        ApplySteering(steering);

        currentMovementStatus = MovementStatus.Moving;
        currentDestination = player.position;
    }

    /// <summary>
    /// Evade from the player using steering behaviors
    /// </summary>
    public void EvadePlayer()
    {
        if (player == null) return;

        Vector3 playerVel = Vector3.zero;
        var playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerVel = playerRb.linearVelocity;
        }

        Vector3 steering = Steering.Evade(transform.position, _vel, player.position, playerVel, chaseSpeed);
        ApplySteering(steering);

        currentMovementStatus = MovementStatus.Fleeing;
        Vector3 fleeDirection = (transform.position - player.position).normalized;
        currentDestination = transform.position + fleeDirection * 10f;
    }
    
    /// <summary>
    /// Get movement speed based on current state and personality
    /// </summary>
    public float GetContextualSpeed()
    {
        var detectionResult = GetDetectionResult();

        float baseSpeed = detectionResult.level switch
        {
            PlayerDetectionLevel.None => patrolSpeed,
            PlayerDetectionLevel.Peripheral => patrolSpeed * 1.2f,
            PlayerDetectionLevel.Partial => personalityType == AIPersonalityType.Aggressive ? chaseSpeed * 0.8f : patrolSpeed * 1.5f,
            PlayerDetectionLevel.Clear => chaseSpeed,
            PlayerDetectionLevel.Immediate => chaseSpeed * 1.2f,
            _ => patrolSpeed
        };

        // Ensure we don't exceed maxSpeed
        return Mathf.Min(baseSpeed, maxSpeed);
    }
    
    #endregion
    
    #region Debug Methods
    
    [ContextMenu("Print AI Status")]
    public void PrintAIStatus()
    {
        Debug.Log("=== GUARD AI STATUS ===");
        Debug.Log($"AI System Enabled: {enableNewAISystem}");
        Debug.Log($"Personality: {personalityType}");
        Debug.Log($"Can See Player: {CanSeePlayer()}");

        var detectionResult = GetDetectionResult();
        Debug.Log($"Detection Level: {detectionResult.level}");
        Debug.Log($"Threat Level: {GetThreatLevel():F2}");
        Debug.Log($"Information Confidence: {GetInformationConfidence():F2}");
        Debug.Log($"Should Investigate: {ShouldInvestigate()}");
        Debug.Log($"Should Attack: {ShouldAttack()}");
        Debug.Log($"Contextual Speed: {GetContextualSpeed():F1}");

        // Steering physics status
        Debug.Log($"Current Velocity: {_vel} (magnitude: {_vel.magnitude:F2})");
        Debug.Log($"Max Speed: {maxSpeed}, Max Force: {maxForce}, Mass: {mass}");

        if (player != null)
        {
            Debug.Log($"Distance to Player: {Vector3.Distance(transform.position, player.position):F2}");
        }
        Debug.Log("======================");
    }

    [ContextMenu("Test Pursue Player")]
    private void TestPursuePlayer()
    {
        if (player != null)
        {
            PursuePlayer();
            Debug.Log("Started pursuing player using steering behaviors");
        }
        else
        {
            Debug.Log("No player found to pursue");
        }
    }

    [ContextMenu("Test Evade Player")]
    private void TestEvadePlayer()
    {
        if (player != null)
        {
            EvadePlayer();
            Debug.Log("Started evading player using steering behaviors");
        }
        else
        {
            Debug.Log("No player found to evade from");
        }
    }

    [ContextMenu("Test Direct Movement")]
    private void TestDirectMovement()
    {
        Vector3 testDirection = transform.forward;
        Move(testDirection);
        Debug.Log($"Applied direct movement - Direction: {testDirection}, Current Vel: {_vel.magnitude:F2}");
    }

    [ContextMenu("Reset Velocity")]
    private void ResetVelocity()
    {
        _vel = Vector3.zero;
        Debug.Log("Velocity reset to zero");
    }

    [ContextMenu("Force High Speed")]
    private void ForceHighSpeed()
    {
        mass = 0.1f;
        maxForce = 100f;
        maxSpeed = 20f;
        slowingDistance = 0.5f;
        Debug.Log($"Forced high speed settings: Mass={mass}, MaxForce={maxForce}, MaxSpeed={maxSpeed}");
    }

    [ContextMenu("Test Seek Behavior")]
    private void TestSeekBehavior()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Vector3 target = patrolPoints[0].position;
            Debug.Log($"=== SEEK TEST ===");
            Debug.Log($"Position: {transform.position}");
            Debug.Log($"Target: {target}");
            Debug.Log($"Current Vel: {_vel}");
            Debug.Log($"Max Speed: {maxSpeed}");

            Vector3 steering = Steering.Seek(transform.position, target, _vel, maxSpeed);
            Debug.Log($"Calculated steering: {steering}, magnitude: {steering.magnitude:F2}");

            // Calculate expected values manually
            Vector3 desired = target - transform.position;
            desired.y = 0f;
            desired = desired.normalized * maxSpeed;
            Vector3 expectedSteering = desired - _vel;
            Debug.Log($"Expected desired: {desired}");
            Debug.Log($"Expected steering: {expectedSteering}");

            ApplySteering(steering);
        }
    }

    [ContextMenu("Force Manual Movement")]
    private void ForceManualMovement()
    {
        Vector3 forceVel = transform.forward * 5f;
        _vel = forceVel;
        transform.position += _vel * Time.deltaTime;
        Debug.Log($"Forced velocity: {_vel}, moved to: {transform.position}");
    }

    [ContextMenu("Debug Complete Steering Pipeline")]
    private void DebugSteeringPipeline()
    {
        Debug.Log("=== COMPLETE STEERING DEBUG ===");
        Debug.Log($"Current Status: isAlive={isAlive}, currentMovementStatus={currentMovementStatus}");
        Debug.Log($"Current destination: {currentDestination}");
        Debug.Log($"Physics: mass={mass}, maxForce={maxForce}, maxSpeed={maxSpeed}");
        Debug.Log($"Current velocity: {_vel}");
        Debug.Log($"Time.deltaTime: {Time.deltaTime:F6}, FPS: {1f/Time.deltaTime:F1}");

        if (currentDestination != Vector3.zero)
        {
            // Test direct steering calculation
            Vector3 steering = Steering.Seek(transform.position, currentDestination, _vel, maxSpeed);
            Debug.Log($"Direct Seek result: {steering}");

            // Test integration
            Vector3 integratedVel = Integrate(steering, Time.deltaTime);
            Debug.Log($"After integration: {integratedVel}");

            // Test obstacle avoidance
            Vector3 avoidedVel = obstacleAvoidance.GetDirImproved(integratedVel, false);
            Debug.Log($"After obstacle avoidance: {avoidedVel}");

            // Calculate final movement
            float effectiveDeltaTime = Mathf.Max(Time.deltaTime, 0.016f);
            Vector3 finalMovement = avoidedVel * effectiveDeltaTime;
            Debug.Log($"Final movement per frame: {finalMovement.magnitude:F6} units");
            Debug.Log($"Movement per second: {finalMovement.magnitude * (1f/effectiveDeltaTime):F2} units/sec");

            // Apply directly
            ApplySteering(steering);
        }
    }

    [ContextMenu("Test High Speed Movement")]
    private void TestHighSpeedMovement()
    {
        // Bypass all steering and move directly at high speed
        Vector3 direction = (currentDestination - transform.position).normalized;
        Vector3 highSpeedMovement = direction * 2f; // 2 units per frame = 120 units/sec at 60fps
        transform.position += highSpeedMovement;
        Debug.Log($"Direct high speed movement: {highSpeedMovement.magnitude} units per frame");
    }

    [ContextMenu("Debug FSM Status")]
    private void DebugFSMStatus()
    {
        Debug.Log("=== FSM STATUS ===");
        Debug.Log($"Use FSM: {useFSM}");
        Debug.Log($"State Data Count: {stateDataList?.Count ?? 0}");
        Debug.Log($"StateMachine Initialized: {stateMachine != null}");

        if (stateMachine != null)
        {
            var currentState = stateMachine.GetCurrentState();
            Debug.Log($"Current State: {currentState?.State?.StateName ?? "None"}");
        }

        Debug.Log($"Current Patrol Loops: {CurrentPatrolLoops}/{LoopsToIdle}");
        Debug.Log($"Patrol Direction: {(PatrolDirection ? "Forward" : "Backward")}");
        Debug.Log($"Current Patrol Index: {CurrentPatrolIndex}");
        Debug.Log($"Has Reached Current Point: {HasReachedCurrentPatrolPoint}");
        Debug.Log($"State Timer: {StateTimer:F2}");
        Debug.Log("==================");
    }

    #endregion

    protected override void OnDeath()
    {
        base.OnDeath();

        if (UGS_Analytics.Instance != null)
        {
            UGS_Analytics.Instance.LogGuardKilled(gameObject.name, transform.position);
        }
    }

    #region IUseFsm Implementation

    public void UpdateFsm()
    {
        // This method exists for interface compatibility but we call RunStateMachine directly in OnUpdate
        stateMachine?.RunStateMachine();
    }

    // GetModelTransform, SetTargetTransform, and GetTargetTransform are already implemented above

    #endregion

    public void MyUpdate()
    {
        if (!isAlive) return;

        // Add debug log with reduced frequency to avoid spam
        if (Time.frameCount % 60 == 0) // Log every 60 frames (about once per second at 60fps)
        {
            MyLogger.LogInfo($"[PATROL DEBUG] {gameObject.name}: OnUpdate is being called - Frame {Time.frameCount}");
        }

        UpdateAISystem();

        // Run FSM if enabled, otherwise use legacy movement system
        if (useFSM && stateMachine != null)
        {
            stateMachine.RunStateMachine();
            // In FSM mode, states handle their own timers - don't auto-increment
        }
        else
        {
            UpdateMovementSystem(Time.deltaTime);
            stateTimer += Time.deltaTime; // Keep timer for conditions in legacy mode
        }
    }

    public void SubscribeUpdateService()
    {
        ServiceLocator.Get<IUpdateService>().AddUpdateListener(this);
    }

    public void UnsubscribeUpdateService()
    {
        ServiceLocator.Get<IUpdateService>().RemoveUpdateListener(this);
    }
}
