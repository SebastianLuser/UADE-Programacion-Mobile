using UnityEngine;

public class GuardModel : NPCModel
{
    public GuardDataSO GuardData { get; private set; }

    // Advanced AI state tracking
    private DetectionResult currentDetectionResult;
    private float threatLevel;
    private float informationConfidence;
    private AIPersonalityType personality;

    // FSM patrol tracking
    private int currentPatrolLoops = 0;
    private bool patrolDirection = true; // true = 0->N, false = N->0
    private bool hasReachedCurrentPatrolPoint = false;

    // Movement tracking
    private Vector3 currentVelocity;
    private MovementStatus movementStatus = MovementStatus.Idle;
    private MovementMode movementMode = MovementMode.Walk;

    public GuardModel(GuardDataSO guardData) : base(guardData)
    {
        GuardData = guardData;
        personality = guardData.personalityType;
        currentDetectionResult = DetectionResult.None;
        threatLevel = 0f;
        informationConfidence = 0.2f;
    }

    public bool IsPlayerInAttackRange(Transform playerTransform)
    {
        if (playerTransform == null || GuardData == null) return false;

        float distance = Vector3.Distance(
            RuntimeState.lastKnownPlayerPosition,
            playerTransform.position
        );

        return distance <= GuardData.attackRange;
    }

    public bool CanAttack()
    {
        return RuntimeState.isAlive && RuntimeState.isPlayerVisible;
    }

    public void StartAttack()
    {
        RuntimeState.ResetStateTimer();
    }

    public void StartChase()
    {
        RuntimeState.ResetStateTimer();
    }

    public void StartSearch()
    {
        RuntimeState.ResetStateTimer();
    }

    public bool HasSearchTimeElapsed()
    {
        return RuntimeState.stateTimer >= Data.searchTime;
    }

    #region Advanced AI State Management

    /// <summary>
    /// Update detection information
    /// </summary>
    public void UpdateDetectionResult(DetectionResult detectionResult)
    {
        currentDetectionResult = detectionResult;

        // Update information confidence based on detection level
        informationConfidence = detectionResult.level switch
        {
            PlayerDetectionLevel.None => 0.1f,
            PlayerDetectionLevel.Peripheral => 0.3f,
            PlayerDetectionLevel.Partial => 0.6f,
            PlayerDetectionLevel.Clear => 0.9f,
            PlayerDetectionLevel.Immediate => 1.0f,
            _ => 0.2f
        };
    }

    /// <summary>
    /// Update threat assessment
    /// </summary>
    public void UpdateThreatLevel(float newThreatLevel)
    {
        threatLevel = Mathf.Clamp01(newThreatLevel);
    }

    /// <summary>
    /// Get current detection result
    /// </summary>
    public DetectionResult GetCurrentDetectionResult()
    {
        return currentDetectionResult;
    }

    /// <summary>
    /// Get current threat level
    /// </summary>
    public float GetThreatLevel()
    {
        return threatLevel;
    }

    /// <summary>
    /// Get information confidence
    /// </summary>
    public float GetInformationConfidence()
    {
        return informationConfidence;
    }

    /// <summary>
    /// Should investigate based on personality and detection
    /// </summary>
    public bool ShouldInvestigate()
    {
        return personality switch
        {
            AIPersonalityType.Aggressive => currentDetectionResult.level >= PlayerDetectionLevel.Peripheral,
            AIPersonalityType.Cautious => currentDetectionResult.level >= PlayerDetectionLevel.Partial,
            AIPersonalityType.Conservative => currentDetectionResult.level >= PlayerDetectionLevel.Clear,
            _ => currentDetectionResult.level >= PlayerDetectionLevel.Partial
        };
    }

    /// <summary>
    /// Should attack based on personality and situation
    /// </summary>
    public bool ShouldAttack(float distanceToPlayer)
    {
        bool inAttackRange = distanceToPlayer <= GuardData.attackRange;
        bool canSee = currentDetectionResult.level >= PlayerDetectionLevel.Clear;

        return personality switch
        {
            AIPersonalityType.Aggressive => canSee && distanceToPlayer <= GuardData.detectionRange,
            AIPersonalityType.Cautious => canSee && inAttackRange,
            AIPersonalityType.Conservative => canSee && inAttackRange && threatLevel > 0.7f,
            _ => canSee && inAttackRange
        };
    }

    #endregion

    #region FSM Patrol State Management

    /// <summary>
    /// Get current patrol loops
    /// </summary>
    public int GetCurrentPatrolLoops()
    {
        return currentPatrolLoops;
    }

    /// <summary>
    /// Set current patrol loops
    /// </summary>
    public void SetCurrentPatrolLoops(int loops)
    {
        currentPatrolLoops = loops;
    }

    /// <summary>
    /// Get patrol direction
    /// </summary>
    public bool GetPatrolDirection()
    {
        return patrolDirection;
    }

    /// <summary>
    /// Set patrol direction
    /// </summary>
    public void SetPatrolDirection(bool direction)
    {
        patrolDirection = direction;
    }

    /// <summary>
    /// Get if reached current patrol point
    /// </summary>
    public bool GetHasReachedCurrentPatrolPoint()
    {
        return hasReachedCurrentPatrolPoint;
    }

    /// <summary>
    /// Set if reached current patrol point
    /// </summary>
    public void SetHasReachedCurrentPatrolPoint(bool reached)
    {
        hasReachedCurrentPatrolPoint = reached;
    }

    /// <summary>
    /// Should idle after patrol loops
    /// </summary>
    public bool ShouldIdleAfterPatrol()
    {
        return currentPatrolLoops >= GuardData.loopsToIdle;
    }

    /// <summary>
    /// Should continue patrolling
    /// </summary>
    public bool ShouldContinuePatrol()
    {
        return currentPatrolLoops < GuardData.loopsToIdle;
    }

    /// <summary>
    /// Set current patrol index
    /// </summary>
    public void SetCurrentPatrolIndex(int index)
    {
        RuntimeState.currentPatrolIndex = index;
    }

    /// <summary>
    /// Get current patrol index
    /// </summary>
    public int GetCurrentPatrolIndex()
    {
        return RuntimeState.currentPatrolIndex;
    }

    /// <summary>
    /// Increment patrol loops when completing a cycle
    /// </summary>
    public void IncrementPatrolLoops()
    {
        currentPatrolLoops++;
    }

    /// <summary>
    /// Reset patrol state
    /// </summary>
    public void ResetPatrolState()
    {
        currentPatrolLoops = 0;
        patrolDirection = true;
        hasReachedCurrentPatrolPoint = false;
        RuntimeState.currentPatrolIndex = 0;
    }

    #endregion

    #region Movement State Management

    /// <summary>
    /// Update current velocity
    /// </summary>
    public void UpdateCurrentVelocity(Vector3 velocity)
    {
        currentVelocity = velocity;
    }

    /// <summary>
    /// Get current velocity
    /// </summary>
    public Vector3 GetCurrentVelocity()
    {
        return currentVelocity;
    }

    /// <summary>
    /// Update movement status
    /// </summary>
    public void UpdateMovementStatus(MovementStatus status)
    {
        movementStatus = status;
    }

    /// <summary>
    /// Get movement status
    /// </summary>
    public MovementStatus GetMovementStatus()
    {
        return movementStatus;
    }

    /// <summary>
    /// Update movement mode
    /// </summary>
    public void UpdateMovementMode(MovementMode mode)
    {
        movementMode = mode;
    }

    /// <summary>
    /// Get movement mode
    /// </summary>
    public MovementMode GetMovementMode()
    {
        return movementMode;
    }

    /// <summary>
    /// Get contextual speed based on current state
    /// </summary>
    public float GetContextualSpeed()
    {
        float baseSpeed = currentDetectionResult.level switch
        {
            PlayerDetectionLevel.None => GuardData.patrolSpeed,
            PlayerDetectionLevel.Peripheral => GuardData.patrolSpeed * 1.2f,
            PlayerDetectionLevel.Partial => personality == AIPersonalityType.Aggressive ? GuardData.chaseSpeed * 0.8f : GuardData.patrolSpeed * 1.5f,
            PlayerDetectionLevel.Clear => GuardData.chaseSpeed,
            PlayerDetectionLevel.Immediate => GuardData.chaseSpeed * 1.2f,
            _ => GuardData.patrolSpeed
        };

        // Apply movement mode multiplier
        float modeMultiplier = movementMode switch
        {
            MovementMode.Sneak => 0.5f,
            MovementMode.Walk => 1f,
            MovementMode.Run => 1.5f,
            MovementMode.Sprint => 2f,
            _ => 1f
        };

        return Mathf.Min(baseSpeed * modeMultiplier, GuardData.maxSpeed);
    }

    #endregion

    #region State Queries

    /// <summary>
    /// Is guard in combat state
    /// </summary>
    public bool IsInCombat()
    {
        return currentDetectionResult.level >= PlayerDetectionLevel.Clear && threatLevel > 0.5f;
    }

    /// <summary>
    /// Is guard in alert state
    /// </summary>
    public bool IsAlert()
    {
        return currentDetectionResult.level >= PlayerDetectionLevel.Partial || threatLevel > 0.3f;
    }

    /// <summary>
    /// Is guard in patrol state
    /// </summary>
    public bool IsPatrolling()
    {
        return movementStatus == MovementStatus.Patrolling && currentDetectionResult.level == PlayerDetectionLevel.None;
    }

    /// <summary>
    /// Is guard searching
    /// </summary>
    public bool IsSearching()
    {
        return movementStatus == MovementStatus.Moving && currentDetectionResult.level == PlayerDetectionLevel.Partial;
    }

    /// <summary>
    /// Should return to patrol
    /// </summary>
    public bool ShouldReturnToPatrol()
    {
        return currentDetectionResult.level == PlayerDetectionLevel.None && threatLevel < 0.2f && RuntimeState.stateTimer > GuardData.searchTime;
    }

    #endregion
}