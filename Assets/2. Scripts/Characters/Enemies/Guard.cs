using UnityEngine;
using Game.AI.Steering;

//todo revisar pasar a MVC
public class Guard : BaseCharacter, IUpdatable, IAIMovementController
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
    
    [Header("AI Configuration")]
    [SerializeField] private AIPersonalityType personalityType = AIPersonalityType.Aggressive;
    [SerializeField] private bool enableNewAISystem = true;

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
    
    // AI System components
    private AIContext aiContext;
    private IBlackboard blackboard;
    private IPlayerDetector playerDetector;

    // Steering components
    private Vector3 _vel;
    private ObstacleAvoidance obstacleAvoidance;
    
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
    
    public bool IsActive => isAlive && gameObject.activeInHierarchy;
    
    // AI System access
    public AIContext AIContext => aiContext;
    public IBlackboard Blackboard => blackboard;
    public AIPersonalityType PersonalityType => personalityType;

    // Steering Physics access
    public float Mass => mass;
    public float MaxForce => maxForce;
    public float MaxSpeed => maxSpeed;
    public float SlowingDistance => slowingDistance;
    public Vector3 CurrentVelocity => _vel;
    
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
        if (enableNewAISystem && playerDetector is PlayerDetector detector)
        {
            return detector.GetCurrentDetectionResult();
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
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.RegisterUpdatable(this);
        
        // Start patrolling after a frame to ensure everything is initialized
        StartCoroutine(StartPatrolAfterFrame());
    }
    
    private System.Collections.IEnumerator StartPatrolAfterFrame()
    {
        yield return null; // Wait one frame
        Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Starting patrol after frame delay");
        
        // Try to register with UpdateManager again (in case it wasn't available during Awake)
        var updateManager = ServiceLocator.Get<UpdateManager>();
        if (updateManager != null)
        {
            updateManager.RegisterUpdatable(this);
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Successfully registered with UpdateManager");
        }
        else
        {
            Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: UpdateManager still not available, starting coroutine to wait for it");
            StartCoroutine(WaitForUpdateManager());
        }
        
        StartPatrol();
    }
    
    private System.Collections.IEnumerator WaitForUpdateManager()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // Check every half second
            
            var updateManager = ServiceLocator.Get<UpdateManager>();
            if (updateManager != null)
            {
                updateManager.RegisterUpdatable(this);
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Successfully registered with UpdateManager after waiting");
                break;
            }
            else
            {
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Still waiting for UpdateManager...");
            }
        }
    }
    
    private void InitializeAISystem()
    {
        if (enableNewAISystem)
        {
            // Initialize AI Context
            aiContext = gameObject.GetComponent<AIContext>();
            if (aiContext == null)
            {
                aiContext = gameObject.AddComponent<AIContext>();
                Logger.LogInfo($"Guard {gameObject.name}: Added AIContext component");
            }

            // Get blackboard service
            blackboard = ServiceLocator.Get<IBlackboard>();
            if (blackboard == null)
            {
                Logger.LogWarning($"Guard {gameObject.name}: Blackboard service not available yet");
            }

            // Get player detector
            playerDetector = gameObject.GetComponent<IPlayerDetector>();
            if (playerDetector == null)
            {
                var detectorComponent = gameObject.AddComponent<PlayerDetector>();
                playerDetector = detectorComponent;
                Logger.LogInfo($"Guard {gameObject.name}: Added PlayerDetector component");
            }

            // Configure personality
            if (aiContext != null)
            {
                aiContext.SetPersonalityType(personalityType);
            }
        }

        // Initialize steering physics
        _vel = Vector3.zero;
        obstacleAvoidance = new ObstacleAvoidance(transform, avoidRadius, avoidAngle, personalArea, obstaclesMask);

        // Ensure maxSpeed is at least as fast as chaseSpeed for proper movement
        if (maxSpeed < chaseSpeed)
        {
            maxSpeed = chaseSpeed * 1.2f; // Give some headroom
            Logger.LogInfo($"Guard {gameObject.name}: Adjusted maxSpeed to {maxSpeed} to match chaseSpeed");
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
        if (enableNewAISystem && blackboard == null)
        {
            blackboard = ServiceLocator.Get<IBlackboard>();
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
        if (!isAlive) return;
        
        // Add debug log with reduced frequency to avoid spam
        if (Time.frameCount % 60 == 0) // Log every 60 frames (about once per second at 60fps)
        {
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: OnUpdate is being called - Frame {Time.frameCount}");
        }
        
        UpdateAISystem(deltaTime);
        UpdateMovementSystem(deltaTime);
        //UpdateFsm();
        stateTimer += deltaTime; // Keep timer for conditions that need it
    }
    
    private void UpdateMovementSystem(float deltaTime)
    {
        // Only log movement system updates when there are issues or state changes
        if (isMovementPaused || !CanMove()) 
        {
            // Only log once per second when blocked to avoid spam
            if (Time.frameCount % 60 == 0)
            {
                Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Movement blocked - isPaused: {isMovementPaused}, CanMove: {CanMove()}");
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
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Following steering target to {currentDestination}");
            }
        }
        
        // Debug patrol status only when reaching points or significant changes
        if (isActivelyPatrolling)
        {
            // Only log detailed patrol info every 2 seconds to reduce spam
            if (Time.frameCount % 120 == 0)
            {
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Currently patrolling - currentIndex: {currentPatrolIndex}, patrolPoints.Length: {(patrolPoints?.Length ?? 0)}");
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Current position: {transform.position}, Current destination: {currentDestination}");
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: isActivelyPatrolling: {isActivelyPatrolling}, MovementStatus: {currentMovementStatus}");
                
                if (patrolPoints != null && patrolPoints.Length > currentPatrolIndex)
                {
                    Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Target patrol point: {patrolPoints[currentPatrolIndex].position}");
                    Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Distance to target: {Vector3.Distance(transform.position, patrolPoints[currentPatrolIndex].position):F2}");
                }
            }
        }
        
        // Check if destination reached for patrol logic
        if (isActivelyPatrolling && patrolPoints != null && patrolPoints.Length > 0 && HasReachedDestination())
        {
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Reached patrol point {currentPatrolIndex}, moving to next");
            // Move to next patrol point
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            if (patrolPoints.Length > 0)
            {
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Moving to patrol point {currentPatrolIndex} at {patrolPoints[currentPatrolIndex].position}");
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
                Logger.LogInfo($"[STEERING CALC] Pos: {transform.position}, Target: {currentDestination}, Vel: {_vel}, TargetSpeed: {targetSpeed}");
                Logger.LogInfo($"[STEERING CALC] Calculated steering: {steering}");
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
            Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Movement constrained at position {transform.position}");
            currentMovementStatus = MovementStatus.Constrained;
            OnMovementBlocked?.Invoke();
            return;
        }
    }
    
    private void UpdateAISystem(float deltaTime)
    {
        if (enableNewAISystem && blackboard != null && player != null)
        {
            // Update blackboard with current player information
            blackboard.SetValue(BlackboardKeys.PLAYER_TRANSFORM, player);
            blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, player.position);
            
            // Update last known position if we can see the player
            if (CanSeePlayer())
            {
                lastKnownPlayerPosition = player.position;
                blackboard.SetValue(BlackboardKeys.LAST_KNOWN_PLAYER_POSITION, lastKnownPlayerPosition);
            }
            
            // Update detection information
            var detectionResult = GetDetectionResult();
            blackboard.SetValue($"Guard_{gameObject.GetInstanceID()}_DetectionLevel", detectionResult.level);
            blackboard.SetValue($"Guard_{gameObject.GetInstanceID()}_CanSeePlayer", detectionResult.level > PlayerDetectionLevel.None);
        }
    }
    
    protected override void OnDeath()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
        base.OnDeath();
    }
    
    private void SetupPatrolPoints()
    {
        Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: SetupPatrolPoints called");
        
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: No patrol points assigned, creating default ones");
            patrolPoints = new Transform[2];
            
            GameObject point1 = new GameObject("PatrolPoint1");
            point1.transform.position = transform.position + Vector3.forward * 5f;
            patrolPoints[0] = point1.transform;
            
            GameObject point2 = new GameObject("PatrolPoint2");
            point2.transform.position = transform.position + Vector3.back * 5f;
            patrolPoints[1] = point2.transform;
            
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Created patrol points at {point1.transform.position} and {point2.transform.position}");
        }
        else
        {
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Using {patrolPoints.Length} existing patrol points");
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Patrol point {i}: {patrolPoints[i].position}");
                }
                else
                {
                    Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Patrol point {i} is null!");
                }
            }
        }
    }
    
    public void StartPatrol()
    {
        Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: StartPatrol called");
        
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Cannot start patrol - no patrol points assigned");
            return;
        }
        
        // Initialize patrol state
        currentPatrolIndex = 0;
        currentMovementStatus = MovementStatus.Patrolling;
        currentMovementMode = MovementMode.Walk;
        isActivelyPatrolling = true;  // Set patrol flag
        
        Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Starting patrol with {patrolPoints.Length} points, moving to point 0");
        
        // Move to first patrol point
        MoveTo(patrolPoints[currentPatrolIndex].position, patrolSpeed);
        
        Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Patrol started successfully");
    }
    
    public void StopPatrol()
    {
        if (isActivelyPatrolling)
        {
            isActivelyPatrolling = false;
            currentMovementStatus = MovementStatus.Idle;
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Patrol stopped");
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
    /// Apply steering force with obstacle avoidance and movement
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
            Logger.LogInfo($"[STEERING DEBUG] {gameObject.name}: Vel: {_vel.magnitude:F2}, AvoidedVel: {avoidedVel.magnitude:F2}, MaxSpeed: {maxSpeed}");
            Logger.LogInfo($"[STEERING DEBUG] Raw steering: {steering.magnitude:F2}, DeltaTime: {Time.deltaTime:F4}, Effective: {effectiveDeltaTime:F4}, Movement: {(avoidedVel * effectiveDeltaTime).magnitude:F4}");
        }
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
                Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: Move called but not alive");
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
        if (enableNewAISystem && blackboard != null)
        {
            blackboard.SetValue($"Guard_{gameObject.GetInstanceID()}_LastShootTime", lastShootTime);
            blackboard.SetValue($"Guard_{gameObject.GetInstanceID()}_ShootDirection", direction);
        }
    }
    
    private void CreateBullet(Vector3 direction)
    {
        var poolService = ServiceLocator.Get<ObjectPoolService>();
        if (poolService != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolService.GetBullet(spawnPosition, direction, 15f, true);
        }
        else
        {
            // Fallback to creating bullet manually if service not available
            GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObj.name = "EnemyBullet";
            bulletObj.transform.position = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            bulletObj.transform.localScale = Vector3.one * 0.2f;
            
            var renderer = bulletObj.GetComponent<Renderer>();
            renderer.material.color = Color.red;
            
            var bulletRb = bulletObj.AddComponent<Rigidbody>();
            bulletRb.useGravity = false;
            
            var bulletCollider = bulletObj.GetComponent<Collider>();
            bulletCollider.isTrigger = true;
            
            var bulletObject = bulletObj.AddComponent<BulletObject>();
            bulletObject.InitializeBullet(direction, 15f, null, true);
        }
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
        if (enableNewAISystem && blackboard != null && player != null)
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
        Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: MoveTo called - Target: {target}, Speed: {speed}, CanMove: {CanMove()}");

        if (!CanMove())
        {
            Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: MoveTo blocked - CanMove returned false");
            return;
        }

        currentDestination = target;
        currentMovementSpeed = speed;
        currentMovementStatus = MovementStatus.Moving;

        // Check constraints
        if (hasMovementConstraints && !movementConstraints.Contains(target))
        {
            Logger.LogWarning($"[PATROL DEBUG] {gameObject.name}: MoveTo constrained - target outside bounds");
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

        Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: MoveTo using steering - Destination: {currentDestination}, Status: {currentMovementStatus}");
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
                Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: HasReachedDestination - Status is Idle, returning true");
            }
            return true;
        }
        
        float distanceToDestination = Vector3.Distance(transform.position, currentDestination);
        bool reached = distanceToDestination < 0.5f;
        
        // Only log distance checks when close to destination or occasionally
        if (reached || Time.frameCount % 120 == 0)
        {
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: HasReachedDestination - Distance: {distanceToDestination:F2}, Reached: {reached}, Status: {currentMovementStatus}");
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Current pos: {transform.position}, Destination: {currentDestination}");
        }
        
        if (reached && currentMovementStatus == MovementStatus.Moving)
        {
            Logger.LogInfo($"[PATROL DEBUG] {gameObject.name}: Destination reached, changing status to Idle");
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
            Vector3 avoidedVel = obstacleAvoidance.GetDir2(integratedVel, false);
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
    
    #endregion
}