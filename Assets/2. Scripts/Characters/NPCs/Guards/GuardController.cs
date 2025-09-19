using UnityEngine;
using Scripts.FSM.Base.StateMachine;
using System.Collections.Generic;
using Game.AI.Steering;
using Scripts.FSM.Models;

public class GuardController : NPCController, ICombat, IUpdatable, IAIMovementController
{
    [SerializeField] private GuardDataSO guardData;

    public bool IsActive => IsAlive && gameObject.activeInHierarchy;

    public GuardDataSO GuardData => guardData;

    // Cached GuardModel reference
    private GuardModel guardModel;

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

    protected override void InitializeComponents()
    {
        npcData = guardData;

        // Create GuardModel instead of NPCModel
        model = new GuardModel(guardData);
        guardModel = model as GuardModel;

        // Initialize view
        view = GetComponent<NPCView>();
        if (view == null)
        {
            Logger.LogError($"{gameObject.name}: NPCView component required for NPCController!");
        }
        else
        {
            view.SetController(this);
        }

        InitializeAISystem();
        RegisterWithUpdateManager();
    }

    protected override void InitializeStateMachine()
    {
        if (guardData.useFSM && guardData.stateDataList != null && guardData.stateDataList.Count > 0)
        {
            stateDataList = guardData.stateDataList;
            stateMachine = new StateMachine(stateDataList, this);
            Logger.LogInfo($"Guard {gameObject.name}: FSM initialized with {stateDataList.Count} states");
        }
        else
        {
            Logger.LogWarning($"Guard {gameObject.name}: No FSM states configured!");
        }
    }

    private void InitializeAISystem()
    {
        if (guardData.enableNewAISystem)
        {
            // Initialize AI Context
            aiContext = gameObject.GetComponent<AIContext>();
            if (aiContext == null)
            {
                aiContext = gameObject.AddComponent<AIContext>();
            }

            // Get blackboard service
            blackboard = ServiceLocator.Get<IBlackboard>();

            // Get player detector
            playerDetector = gameObject.GetComponent<IPlayerDetector>();
            if (playerDetector == null)
            {
                var detectorComponent = gameObject.AddComponent<PlayerDetector>();
                playerDetector = detectorComponent;
            }

            // Configure personality
            if (aiContext != null)
            {
                aiContext.SetPersonalityType(guardData.personalityType);
            }
        }

        // Initialize steering physics
        _vel = Vector3.zero;
        obstacleAvoidance = new ObstacleAvoidance(transform, guardData.avoidRadius, guardData.avoidAngle, guardData.personalArea, guardData.obstaclesMask);
    }

    private void RegisterWithUpdateManager()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        if (updateManager != null)
        {
            updateManager.RegisterUpdatable(this);
        }
    }

    public void OnUpdate(float deltaTime)
    {
        if (!IsAlive) return;

        UpdateAISystem(deltaTime);
        UpdateModelState();
        UpdatePatrolMovement(deltaTime);
    }

    private void UpdateAISystem(float deltaTime)
    {
        if (guardData.enableNewAISystem && blackboard != null && player != null)
        {
            // Update blackboard with current player information
            blackboard.SetValue(BlackboardKeys.PLAYER_TRANSFORM, player);
            blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, player.position);

            // Update detection information
            var detectionResult = GetDetectionResult();
            blackboard.SetValue($"Guard_{gameObject.GetInstanceID()}_CanSeePlayer", detectionResult.level > PlayerDetectionLevel.None);

            // Update model with AI state
            if (guardModel != null)
            {
                guardModel.UpdateDetectionResult(detectionResult);
                guardModel.UpdateThreatLevel(GetThreatLevel());
                guardModel.UpdateCurrentVelocity(_vel);
            }
        }
    }

    private void UpdateModelState()
    {
        if (guardModel != null)
        {
            // Update model state as needed
        }
    }

    protected void OnDestroy()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
    }

    #region ICombat Implementation

    public void Shoot(Vector3 direction)
    {
        if (!IsAlive || !CanShoot()) return;

        model.RuntimeState.ResetStateTimer();
        CreateBullet(direction);
    }

    public bool CanShoot()
    {
        if (guardData == null) return false;
        return model.RuntimeState.stateTimer >= characterData.shootCooldown;
    }

    private void CreateBullet(Vector3 direction)
    {
        if (guardData?.bulletData == null)
        {
            Logger.LogWarning($"{gameObject.name}: BulletData not assigned, cannot shoot!");
            return;
        }

        var poolService = ServiceLocator.Get<ObjectPoolService>();
        if (poolService != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolService.GetBullet(spawnPosition, direction, guardData.bulletData.speed, true);
        }
    }

    public bool PlayerInAttackRange()
    {
        if (player == null || guardData == null) return false;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer <= guardData.attackRange;
    }

    public void AttackPlayer()
    {
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            Shoot(direction);
            FaceDirection(direction);
        }
    }

    public void ChasePlayer()
    {
        if (player != null)
        {
            MoveTo(player.position);
            FaceDirection((player.position - transform.position).normalized);
        }
    }

    #endregion

    #region Advanced Detection and AI

    public override bool CanSeePlayer()
    {
        if (guardData.enableNewAISystem && playerDetector != null)
        {
            return playerDetector.CanSeePlayer(player);
        }

        return CanSeePlayerLegacy();
    }

    private bool CanSeePlayerLegacy()
    {
        if (player == null) return false;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer > guardData.fieldOfView / 2f) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer > guardData.detectionRange) return false;

        return !Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, LayerMask.GetMask("Obstacles"));
    }

    public DetectionResult GetDetectionResult()
    {
        if (guardData.enableNewAISystem && playerDetector is PlayerDetector detector)
        {
            return detector.GetCurrentDetectionResult();
        }

        return CanSeePlayerLegacy() ? DetectionResult.Clear : DetectionResult.None;
    }

    public float GetThreatLevel()
    {
        if (aiContext != null)
        {
            return aiContext.GetThreatLevel();
        }

        if (!CanSeePlayer()) return 0f;

        float distance = Vector3.Distance(transform.position, player.position);
        float distanceFactor = 1f - Mathf.Clamp01(distance / guardData.detectionRange);
        return distanceFactor * 0.7f;
    }

    #endregion

    #region Steering Physics

    private Vector3 Integrate(Vector3 steering, float dt)
    {
        Vector3 clampedForce = steering;
        if (clampedForce.sqrMagnitude > guardData.maxForce * guardData.maxForce)
        {
            clampedForce = clampedForce.normalized * guardData.maxForce;
        }

        Vector3 acceleration = clampedForce / guardData.mass;
        Vector3 newVel = _vel + acceleration * dt;

        if (newVel.sqrMagnitude > guardData.maxSpeed * guardData.maxSpeed)
        {
            newVel = newVel.normalized * guardData.maxSpeed;
        }

        return newVel;
    }
    
    public void ApplySteering(Vector3 steering)
    {
        if (!IsAlive) return;

        _vel = Integrate(steering, Time.deltaTime);
        Vector3 avoidedVel = obstacleAvoidance.GetDir2(_vel, false);

        if (avoidedVel.sqrMagnitude > 0.001f)
        {
            Vector3 movement = avoidedVel * Time.deltaTime;
            transform.position += movement;

            currentMovementDirection = avoidedVel.normalized;
            currentMovementSpeed = avoidedVel.magnitude;

            if (avoidedVel.magnitude > 0.1f)
            {
                Vector3 lookDirection = avoidedVel.normalized;
                lookDirection.y = 0f;
                float rotationSpeed = guardData.baseRotationSpeed * 3f;
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    public void ConfigureSteering(float newMass, float newMaxForce, float newMaxSpeed, float newSlowingDistance)
    {
        guardData.mass = newMass;
        guardData.maxForce = newMaxForce;
        guardData.maxSpeed = newMaxSpeed;
        guardData.slowingDistance = newSlowingDistance;
    }

    public void ConfigureObstacleAvoidance(float radius, float angle, float personalArea, LayerMask obstacleMask)
    {
        guardData.avoidRadius = radius;
        guardData.avoidAngle = angle;
        guardData.personalArea = personalArea;
        guardData.obstaclesMask = obstacleMask;

        obstacleAvoidance = new ObstacleAvoidance(transform, guardData.avoidRadius, guardData.avoidAngle, guardData.personalArea, guardData.obstaclesMask);
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
        _vel = Vector3.zero;
    }

    public float GetMaxSpeed()
    {
        return guardData?.maxSpeed ?? characterData.moveSpeed;
    }

    public bool CanMove()
    {
        return IsAlive && IsActive;
    }

    public void MoveTo(Vector3 target, float speed)
    {
        if (!CanMove()) return;

        currentDestination = target;
        currentMovementSpeed = speed;
        currentMovementStatus = MovementStatus.Moving;

        // Check constraints
        if (hasMovementConstraints && !movementConstraints.Contains(target))
        {
            currentMovementStatus = MovementStatus.Constrained;
            OnMovementBlocked?.Invoke();
            return;
        }

        // Use steering behavior to move to target
        float distanceToTarget = Vector3.Distance(transform.position, target);
        Vector3 steering;

        if (distanceToTarget > guardData.slowingDistance * 2f)
        {
            steering = Steering.Seek(transform.position, target, _vel, speed);
        }
        else
        {
            steering = Steering.Arrive(transform.position, target, _vel, speed, guardData.slowingDistance);
        }

        ApplySteering(steering);
    }

    public void Flee(Vector3 fromPosition, float speed)
    {
        if (!CanMove()) return;

        currentMovementSpeed = speed;
        currentMovementStatus = MovementStatus.Fleeing;

        Vector3 steering = Steering.Flee(transform.position, fromPosition, _vel, speed);
        ApplySteering(steering);

        Vector3 fleeDirection = (transform.position - fromPosition).normalized;
        currentDestination = transform.position + fleeDirection * 10f;
    }

    public void Patrol(Transform[] waypoints, float speed)
    {
        if (!CanMove() || waypoints == null || waypoints.Length == 0) return;

        guardData.patrolPoints = waypoints;
        currentMovementSpeed = speed;
        currentMovementStatus = MovementStatus.Patrolling;

        if (guardModel != null)
        {
            guardModel.SetCurrentPatrolIndex(0);
            MoveTo(waypoints[0].position, speed);
        }
    }

    public void Stop()
    {
        StopMovement();
    }

    public bool HasReachedDestination()
    {
        if (currentMovementStatus == MovementStatus.Idle) return true;

        float distanceToDestination = Vector3.Distance(transform.position, currentDestination);
        bool reached = distanceToDestination < 0.5f;

        if (reached && currentMovementStatus == MovementStatus.Moving)
        {
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
            float speed = rotationSpeed > 0 ? rotationSpeed : guardData.baseRotationSpeed;
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

    public float GetContextualSpeed()
    {
        var detectionResult = GetDetectionResult();

        float baseSpeed = detectionResult.level switch
        {
            PlayerDetectionLevel.None => guardData.patrolSpeed,
            PlayerDetectionLevel.Peripheral => guardData.patrolSpeed * 1.2f,
            PlayerDetectionLevel.Partial => guardData.personalityType == AIPersonalityType.Aggressive ? guardData.chaseSpeed * 0.8f : guardData.patrolSpeed * 1.5f,
            PlayerDetectionLevel.Clear => guardData.chaseSpeed,
            PlayerDetectionLevel.Immediate => guardData.chaseSpeed * 1.2f,
            _ => guardData.patrolSpeed
        };

        return Mathf.Min(baseSpeed, guardData.maxSpeed);
    }

    #endregion

    #region Patrol Management

    public void StartPatrol()
    {
        if (guardData.patrolPoints == null || guardData.patrolPoints.Length == 0)
        {
            SetupDefaultPatrolPoints();
        }

        if (guardModel != null)
        {
            guardModel.ResetPatrolState();
            guardModel.UpdateMovementStatus(MovementStatus.Patrolling);
        }

        currentMovementStatus = MovementStatus.Patrolling;
        currentMovementMode = MovementMode.Walk;

        if (guardData.patrolPoints.Length > 0)
        {
            MoveTo(guardData.patrolPoints[0].position, guardData.patrolSpeed);
        }

        Logger.LogInfo($"Guard {gameObject.name}: Patrol started with {guardData.patrolPoints.Length} points");
    }

    public void StopPatrol()
    {
        if (currentMovementStatus == MovementStatus.Patrolling)
        {
            StopMovement();
            if (guardModel != null)
            {
                guardModel.UpdateMovementStatus(MovementStatus.Idle);
            }
            Logger.LogInfo($"Guard {gameObject.name}: Patrol stopped");
        }
    }

    private void SetupDefaultPatrolPoints()
    {
        if (guardData.patrolPoints == null || guardData.patrolPoints.Length == 0)
        {
            Transform[] defaultPoints = new Transform[2];

            GameObject point1 = new GameObject($"{gameObject.name}_PatrolPoint1");
            point1.transform.position = transform.position + Vector3.forward * 5f;
            defaultPoints[0] = point1.transform;

            GameObject point2 = new GameObject($"{gameObject.name}_PatrolPoint2");
            point2.transform.position = transform.position + Vector3.back * 5f;
            defaultPoints[1] = point2.transform;

            guardData.patrolPoints = defaultPoints;

            Logger.LogInfo($"Guard {gameObject.name}: Created default patrol points");
        }
    }

    public void UpdatePatrolMovement(float deltaTime)
    {
        if (currentMovementStatus != MovementStatus.Patrolling || guardData.patrolPoints == null || guardData.patrolPoints.Length == 0)
            return;

        // Check movement constraints
        if (hasMovementConstraints && !movementConstraints.Contains(transform.position))
        {
            currentMovementStatus = MovementStatus.Constrained;
            OnMovementBlocked?.Invoke();
            return;
        }

        // Update steering target following
        if (steeringTarget != null && currentMovementStatus == MovementStatus.Following)
        {
            currentDestination = steeringTarget.position;
            Vector3 direction = (currentDestination - transform.position).normalized;
            currentMovementDirection = direction;
        }

        if (HasReachedDestination())
        {
            int currentIndex = guardModel?.GetCurrentPatrolIndex() ?? 0;
            int nextIndex = (currentIndex + 1) % guardData.patrolPoints.Length;

            if (guardModel != null)
            {
                guardModel.SetCurrentPatrolIndex(nextIndex);
                guardModel.SetHasReachedCurrentPatrolPoint(true);

                // Check if completed a full cycle
                if (nextIndex == 0)
                {
                    guardModel.IncrementPatrolLoops();

                    // Check if should idle after completing loops
                    if (guardModel.ShouldIdleAfterPatrol())
                    {
                        StopPatrol();
                        // Could implement idle behavior here
                        return;
                    }
                }
            }

            // Move to next patrol point
            if (nextIndex < guardData.patrolPoints.Length)
            {
                MoveTo(guardData.patrolPoints[nextIndex].position, guardData.patrolSpeed);
            }
        }
        else if (currentMovementStatus == MovementStatus.Moving && currentDestination != Vector3.zero)
        {
            // Continue steering-based movement towards current destination
            float targetSpeed = Mathf.Max(currentMovementSpeed, guardData.maxSpeed * 0.5f);
            Vector3 steering = Steering.Seek(transform.position, currentDestination, _vel, targetSpeed);
            ApplySteering(steering);
        }
        else if (currentMovementStatus == MovementStatus.Patrolling && guardData.patrolPoints.Length > 0)
        {
            // Continue moving towards current patrol point
            int currentIndex = guardModel?.GetCurrentPatrolIndex() ?? 0;
            if (currentIndex < guardData.patrolPoints.Length)
            {
                Vector3 steering = Steering.Seek(transform.position, guardData.patrolPoints[currentIndex].position, _vel, guardData.patrolSpeed);
                ApplySteering(steering);
            }
        }
    }

    #endregion

    #region Advanced AI Methods

    public float GetInformationConfidence()
    {
        if (guardModel != null)
        {
            return guardModel.GetInformationConfidence();
        }

        return CanSeePlayer() ? 1.0f : 0.2f;
    }

    public bool ShouldInvestigate()
    {
        if (guardModel != null)
        {
            return guardModel.ShouldInvestigate();
        }

        var detectionResult = GetDetectionResult();
        return guardData.personalityType switch
        {
            AIPersonalityType.Aggressive => detectionResult.level >= PlayerDetectionLevel.Peripheral,
            AIPersonalityType.Cautious => detectionResult.level >= PlayerDetectionLevel.Partial,
            AIPersonalityType.Conservative => detectionResult.level >= PlayerDetectionLevel.Clear,
            _ => detectionResult.level >= PlayerDetectionLevel.Partial
        };
    }

    public bool ShouldAttack()
    {
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.position);

        if (guardModel != null)
        {
            return guardModel.ShouldAttack(distance);
        }

        var detectionResult = GetDetectionResult();
        bool inAttackRange = distance <= guardData.attackRange;
        bool canSee = detectionResult.level >= PlayerDetectionLevel.Clear;

        return guardData.personalityType switch
        {
            AIPersonalityType.Aggressive => canSee && distance <= guardData.detectionRange,
            AIPersonalityType.Cautious => canSee && inAttackRange,
            AIPersonalityType.Conservative => canSee && inAttackRange && GetThreatLevel() > 0.7f,
            _ => canSee && inAttackRange
        };
    }

    public void PursuePlayer()
    {
        if (player == null) return;

        Vector3 playerVel = Vector3.zero;
        var playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerVel = playerRb.linearVelocity;
        }

        Vector3 steering = Steering.Pursuit(transform.position, _vel, player.position, playerVel, guardData.chaseSpeed);
        ApplySteering(steering);

        currentMovementStatus = MovementStatus.Moving;
        currentDestination = player.position;

        if (guardModel != null)
        {
            guardModel.UpdateMovementStatus(MovementStatus.Following);
        }
    }

    public void EvadePlayer()
    {
        if (player == null) return;

        Vector3 playerVel = Vector3.zero;
        var playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerVel = playerRb.linearVelocity;
        }

        Vector3 steering = Steering.Evade(transform.position, _vel, player.position, playerVel, guardData.chaseSpeed);
        ApplySteering(steering);

        currentMovementStatus = MovementStatus.Fleeing;
        Vector3 fleeDirection = (transform.position - player.position).normalized;
        currentDestination = transform.position + fleeDirection * 10f;

        if (guardModel != null)
        {
            guardModel.UpdateMovementStatus(MovementStatus.Fleeing);
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Print Guard Status")]
    public void PrintGuardStatus()
    {
        Debug.Log("=== GUARD STATUS ===");
        Debug.Log($"Guard: {gameObject.name}");
        Debug.Log($"AI System Enabled: {guardData.enableNewAISystem}");
        Debug.Log($"FSM Enabled: {guardData.useFSM}");
        Debug.Log($"Can See Player: {CanSeePlayer()}");
        Debug.Log($"Threat Level: {GetThreatLevel():F2}");
        Debug.Log($"Information Confidence: {GetInformationConfidence():F2}");
        Debug.Log($"Should Investigate: {ShouldInvestigate()}");
        Debug.Log($"Should Attack: {ShouldAttack()}");
        Debug.Log($"Contextual Speed: {GetContextualSpeed():F1}");
        Debug.Log($"Current Velocity: {_vel} (magnitude: {_vel.magnitude:F2})");
        Debug.Log($"Movement Status: {currentMovementStatus}");
        Debug.Log($"Movement Mode: {currentMovementMode}");
        Debug.Log($"Patrol Loops: {guardModel?.GetCurrentPatrolLoops() ?? 0}/{guardData.loopsToIdle}");
        if (player != null)
        {
            Debug.Log($"Distance to Player: {Vector3.Distance(transform.position, player.position):F2}");
        }
        Debug.Log("===================");
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

    [ContextMenu("Start Patrol")]
    private void TestStartPatrol()
    {
        StartPatrol();
    }

    [ContextMenu("Stop Patrol")]
    private void TestStopPatrol()
    {
        StopPatrol();
    }

    [ContextMenu("Reset Velocity")]
    private void ResetVelocity()
    {
        _vel = Vector3.zero;
        Debug.Log("Velocity reset to zero");
    }

    [ContextMenu("Test High Speed Settings")]
    private void TestHighSpeedSettings()
    {
        ConfigureSteering(0.1f, 100f, 20f, 0.5f);
        Debug.Log($"Applied high speed settings: Mass=0.1, MaxForce=100, MaxSpeed=20");
    }

    #endregion
}