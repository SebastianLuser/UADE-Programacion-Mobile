using UnityEngine;
using Game.AI.Steering;
using Services;
using Services.MicroServices.BlackboardService;
using Services.MicroServices.UpdateService;
using ScriptableObjects.Bullets;
using Services.MicroServices.PoolObjectsService;

/// <summary>
/// Ally character that follows the player and attacks Guards.
/// Inherits only from BaseCharacter for independence from Guard implementation.
/// Uses IUpdateListener for consistent update system integration.
/// </summary>
public class Ally : BaseCharacter, IUpdateListener
{
    [Header("Ally Configuration")]
    [SerializeField] private AllyDataSO allyData;

    // Auto-detected by 'Player' tag
    private Transform playerToFollow;

    [Header("Guard Detection")]
    [Tooltip("Tag used to identify Guards")]
    [SerializeField] private string guardTag = "Guard";

    [Tooltip("Layer mask for Guard detection")]
    [SerializeField] private LayerMask guardLayerMask = 1 << 7; // Layer 7 = Enemies

    // Configuration from AllyDataSO
    private float followDistance;
    private float followSpeed;
    private float attackRange;
    private float chaseSpeed;
    private float detectionRange;
    private BulletData bulletData;

    // Steering physics from AllyDataSO
    private float mass;
    private float maxForce;
    private float maxSpeed;
    private float slowingDistance;

    // Obstacle avoidance from AllyDataSO
    private LayerMask obstaclesMask;
    private float avoidRadius;
    private float avoidAngle;
    private float personalArea;

    // Runtime state
    private Vector3 velocity;
    private Guard currentTarget;
    private Vector3 playerVelocity;
    private ObstacleAvoidance obstacleAvoidance;
    private IBlackboardService blackboard;
    private float rotationSpeed = 3f;

    // Leader override system
    private bool leaderOverrideActive;
    private Vector3 leaderOverrideTarget;
    private float leaderOverrideExpiresAt;
    private string leaderOverrideRole;

    // Pool service
    private static IPoolObjectsService PoolObjectsService => ServiceLocator.Get<IPoolObjectsService>();

    protected override void Awake()
    {
        base.Awake();

        InitializeFromAllyData();

        // Initialize steering
        velocity = Vector3.zero;
        obstacleAvoidance = new ObstacleAvoidance(transform, avoidRadius, avoidAngle, personalArea, obstaclesMask);

        // Get blackboard service
        blackboard = ServiceLocator.Get<IBlackboardService>();

        // Subscribe to UpdateService
        SubscribeUpdateService();

        Debug.Log($"[Ally] {name} initialized - Independent from Guard, using AllyDataSO");
    }

    private void InitializeFromAllyData()
    {
        if (allyData != null)
        {
            // Player following
            followDistance = allyData.followDistance;
            followSpeed = allyData.followSpeed;

            // Combat
            attackRange = allyData.attackRange;
            chaseSpeed = allyData.chaseSpeed;
            bulletData = allyData.bulletData;

            // Detection (from NPCDataSO base)
            detectionRange = 8f; // Default, AllyDataSO doesn't have this exposed

            // Steering physics
            mass = allyData.mass;
            maxForce = allyData.maxForce;
            maxSpeed = allyData.maxSpeed;
            slowingDistance = allyData.slowingDistance;

            // Obstacle avoidance
            obstaclesMask = allyData.obstaclesMask;
            avoidRadius = allyData.avoidRadius;
            avoidAngle = allyData.avoidAngle;
            personalArea = allyData.personalArea;

            Debug.Log($"[Ally] {name} loaded configuration from AllyDataSO");
        }
        else
        {
            // Fallback defaults
            Debug.LogWarning($"[Ally] {name} - No AllyDataSO assigned! Using default values.");
            followDistance = 3f;
            followSpeed = 4f;
            attackRange = 8f;
            chaseSpeed = 5f;
            detectionRange = 8f;

            mass = 1f;
            maxForce = 20f;
            maxSpeed = 6f;
            slowingDistance = 2f;

            // Obstacle avoidance: Layer 8 (Obstacles) only
            // NEVER use -1 (all layers) or it will avoid player/guards/allies!
            obstaclesMask = 1 << 8;  // Layer 8 = Obstacles
            avoidRadius = 2f;
            avoidAngle = 90f;
            personalArea = 0.5f;
        }
    }

    private void Start()
    {
        // Auto-detect player by tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerToFollow = player.transform;
            Debug.Log($"[Ally] {name} found Player: {playerToFollow.name}");
        }
        else
        {
            Debug.LogError($"[Ally] {name} could not find Player with tag 'Player'!");
        }
    }

    private void OnDisable()
    {
        UnsubscribeUpdateService();
    }

    #region IUpdateListener Implementation

    public void MyUpdate()
    {
        if (!isAlive) return;

        UpdatePlayerVelocity();
        UpdateBehavior();
    }

    public void SubscribeUpdateService()
    {
        var updateService = ServiceLocator.Get<IUpdateService>();
        if (updateService != null)
        {
            updateService.AddUpdateListener(this);
        }
        else
        {
            Debug.LogWarning($"[Ally] {name} - UpdateService not found!");
        }
    }

    public void UnsubscribeUpdateService()
    {
        var updateService = ServiceLocator.Get<IUpdateService>();
        if (updateService != null)
        {
            updateService.RemoveUpdateListener(this);
        }
    }

    #endregion

    #region Core Behavior

    private void UpdatePlayerVelocity()
    {
        if (playerToFollow == null) return;

        Rigidbody rb = playerToFollow.GetComponent<Rigidbody>();
        if (rb != null)
            playerVelocity = rb.linearVelocity;
    }

    private void UpdateBehavior()
    {
        // Priority 0: Leader Override (highest priority)
        if (HandleLeaderOverride())
        {
            currentTarget = null;
            return;
        }

        // Priority 1: Attack Guards if detected
        Guard nearestGuard = FindNearestVisibleGuard();
        if (nearestGuard != null)
        {
            currentTarget = nearestGuard;
            AttackGuard(nearestGuard);
            return;
        }

        // Priority 2: Follow Player
        currentTarget = null;
        FollowPlayer();
    }

    #endregion

    #region Combat

    private void AttackGuard(Guard target)
    {
        if (target == null || !target.IsAlive) return;

        float distance = Vector3.Distance(transform.position, target.transform.position);

        // Movement: Pursue if far, brake if close
        if (distance > attackRange)
        {
            Vector3 targetVelocity = target.CurrentVelocity;
            Vector3 steeringForce = Steering.Pursuit(
                transform.position,
                velocity,
                target.transform.position,
                targetVelocity,
                chaseSpeed
            );
            ApplySteering(steeringForce);
        }
        else
        {
            // Brake
            Vector3 brakeForce = -velocity * 0.5f;
            ApplySteering(brakeForce);
        }

        // Shooting: Fire if in range
        if (distance <= attackRange && CanShoot())
        {
            Vector3 direction = (target.transform.position - transform.position).normalized;
            Shoot(direction);
        }

        // Rotation: Face target
        Vector3 toTarget = (target.transform.position - transform.position).normalized;
        FaceDirection(toTarget);

        // Debug
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Ally] {name} ATTACKING {target.name} at distance {distance:F2}");
        }
    }

    private Guard FindNearestVisibleGuard()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, guardLayerMask);

        Guard nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            Guard guard = hit.GetComponent<Guard>();

            // Filter out invalid targets
            if (guard == null || !guard.IsAlive) continue;
            if (guard is Ally) continue; // Don't target other Allies

            Vector3 direction = guard.transform.position - transform.position;
            float distance = direction.magnitude;

            // Line of sight check
            bool blocked = Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                direction.normalized,
                distance,
                obstaclesMask
            );
            if (blocked) continue;

            // Track nearest
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = guard;
            }
        }

        return nearest;
    }

    #endregion

    #region Player Following

    private void FollowPlayer()
    {
        if (playerToFollow == null)
        {
            if (Time.frameCount % 60 == 0)
                Debug.LogWarning($"[Ally] {name} has no Player to follow!");
            return;
        }

        float distance = Vector3.Distance(transform.position, playerToFollow.position);

        // Movement: Pursue if far, brake if close
        if (distance > followDistance)
        {
            Vector3 steeringForce = Steering.Pursuit(
                transform.position,
                velocity,
                playerToFollow.position,
                playerVelocity,
                followSpeed
            );
            ApplySteering(steeringForce);
        }
        else
        {
            // Brake gently
            Vector3 brakeForce = -velocity * 0.3f;
            ApplySteering(brakeForce);
        }

        // Debug
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Ally] {name} FOLLOWING Player at distance {distance:F2}");
        }
    }

    #endregion

    #region Steering Physics (Copied from Guard)

    /// <summary>
    /// Apply steering force with physics integration and obstacle avoidance.
    /// </summary>
    private void ApplySteering(Vector3 steeringForce)
    {
        if (!isAlive) return;

        // 1. Integrate steering force into velocity
        Vector3 desiredVelocity = Integrate(steeringForce, Time.deltaTime);
        desiredVelocity.y = 0f; // Keep on ground

        float desiredSpeed = desiredVelocity.magnitude;
        if (desiredSpeed <= 0.0001f) return;

        Vector3 desiredDirection = desiredVelocity / Mathf.Max(desiredSpeed, 1e-5f);

        // 2. Apply obstacle avoidance
        Vector3 avoidedVelocity = obstacleAvoidance.GetDirImproved(desiredVelocity, false);
        Vector3 avoidanceDelta = avoidedVelocity - desiredVelocity;
        Vector3 avoidDirection = avoidanceDelta.sqrMagnitude > 1e-6f ? avoidanceDelta.normalized : Vector3.zero;

        // Blend desired movement with avoidance
        float pathWeight = 1.0f;
        float avoidWeight = 0.35f;

        if (avoidDirection != Vector3.zero)
        {
            float oppositeFactor = Mathf.Clamp01(-Vector3.Dot(avoidDirection, desiredDirection));
            float weightBoost = Mathf.Lerp(0f, 0.75f, oppositeFactor);
            avoidWeight += weightBoost;
        }
        avoidWeight = Mathf.Clamp(avoidWeight, 0f, 1f);

        float avoidScale = Mathf.Max(desiredSpeed, 0.1f);
        Vector3 blendedVelocity = (desiredVelocity * pathWeight) + (avoidDirection * (avoidWeight * avoidScale));

        // Clamp to max speed while prioritizing path component
        if (blendedVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            Vector3 pathComponent = Vector3.Project(blendedVelocity, desiredDirection);
            Vector3 avoidComponent = blendedVelocity - pathComponent;

            float pathMag = pathComponent.magnitude;
            float avoidMag = avoidComponent.magnitude;
            float totalMag = Mathf.Sqrt(pathMag * pathMag + avoidMag * avoidMag);

            if (totalMag > maxSpeed)
            {
                float scale = Mathf.Sqrt(Mathf.Max(0, maxSpeed * maxSpeed - pathMag * pathMag)) / Mathf.Max(avoidMag, 1e-5f);
                avoidComponent *= Mathf.Min(scale, 1f);
                blendedVelocity = pathComponent + avoidComponent;
            }
        }

        blendedVelocity.y = 0f;
        velocity = blendedVelocity;

        // 3. Apply movement
        if (velocity.sqrMagnitude > 0.001f)
        {
            Vector3 movement = velocity * Time.deltaTime;
            transform.position += movement;
        }
    }

    /// <summary>
    /// Integrate steering force with mass and force limits.
    /// </summary>
    private Vector3 Integrate(Vector3 steeringForce, float deltaTime)
    {
        // Clamp steering force to maximum
        Vector3 clampedForce = steeringForce;
        if (clampedForce.sqrMagnitude > maxForce * maxForce)
        {
            clampedForce = clampedForce.normalized * maxForce;
        }

        // Apply force to velocity (F = ma, so a = F/m)
        Vector3 acceleration = clampedForce / mass;
        Vector3 newVelocity = velocity + acceleration * deltaTime;

        // Clamp velocity to maximum speed
        if (newVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            newVelocity = newVelocity.normalized * maxSpeed;
        }

        return newVelocity;
    }

    /// <summary>
    /// Smoothly rotate to face a direction.
    /// </summary>
    private void FaceDirection(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) return;

        direction.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    #endregion

    #region Abstract Method Implementations

    public override void Move(Vector3 direction)
    {
        // Direct movement (used for legacy compatibility)
        Vector3 targetVelocity = direction.normalized * maxSpeed;
        Vector3 steeringForce = targetVelocity - velocity;
        ApplySteering(steeringForce);
    }

    public override void Shoot(Vector3 direction)
    {
        if (!isAlive || !CanShoot()) return;

        lastShootTime = Time.time;
        CreateBullet(direction);
    }

    private void CreateBullet(Vector3 direction)
    {
        if (bulletData == null)
        {
            Debug.LogWarning($"[Ally] {name} has no BulletData assigned!");
            return;
        }

        Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
        var bullet = PoolObjectsService.GetOrCreateObject(bulletData.Prefab);
        bullet.OnDeactivate += OnBulletDeactivate;
        bullet.InitializeBullet(bulletData, spawnPosition, direction);
    }

    private void OnBulletDeactivate(BulletObject bullet)
    {
        bullet.OnDeactivate -= OnBulletDeactivate;
        PoolObjectsService.ReturnObject(bullet);
    }

    #endregion

    #region Leader Override System

    /// <summary>
    /// Set a leader override command that takes priority over normal behavior.
    /// Used by AllyLeader to coordinate defensive formations and tactics.
    /// </summary>
    public void SetLeaderOverride(Vector3 target, float duration, string role = "")
    {
        leaderOverrideActive = true;
        leaderOverrideTarget = target;
        leaderOverrideExpiresAt = Time.time + duration;
        leaderOverrideRole = role;

        Debug.Log($"[Ally] {name} received leader override: {role} at {target} for {duration}s");
    }

    /// <summary>
    /// Clear the current leader override, returning to normal behavior.
    /// </summary>
    public void ClearLeaderOverride()
    {
        leaderOverrideActive = false;
        leaderOverrideTarget = Vector3.zero;
        leaderOverrideRole = string.Empty;
        leaderOverrideExpiresAt = 0f;

        Debug.Log($"[Ally] {name} cleared leader override");
    }

    /// <summary>
    /// Handle leader override behavior. Returns true if override is active and handled.
    /// </summary>
    private bool HandleLeaderOverride()
    {
        if (!leaderOverrideActive)
            return false;

        // Check if override expired
        if (Time.time >= leaderOverrideExpiresAt)
        {
            ClearLeaderOverride();
            return false;
        }

        // Move toward override target
        float distance = Vector3.Distance(transform.position, leaderOverrideTarget);

        if (distance > 1.5f) // Arrival tolerance
        {
            // Use Seek behavior to move to target
            Vector3 steeringForce = Steering.Seek(
                transform.position,
                velocity,
                leaderOverrideTarget,
                followSpeed
            );
            ApplySteering(steeringForce);
        }
        else
        {
            // Reached target, brake gently
            Vector3 brakeForce = -velocity * 0.5f;
            ApplySteering(brakeForce);
        }

        // Face the override target
        Vector3 toTarget = (leaderOverrideTarget - transform.position).normalized;
        if (toTarget.magnitude > 0.1f)
        {
            FaceDirection(toTarget);
        }

        // Debug
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Ally] {name} executing override '{leaderOverrideRole}' - distance: {distance:F2}");
        }

        return true;
    }

    #endregion

    #region Public Accessors

    public Guard GetCurrentTarget() => currentTarget;
    public Transform GetPlayerToFollow() => playerToFollow;
    public void SetPlayerToFollow(Transform player) => playerToFollow = player;
    public Vector3 CurrentVelocity => velocity;
    public bool LeaderOverrideActive => leaderOverrideActive;

    #endregion

    #region Debug Gizmos

    private void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Follow distance
        if (playerToFollow != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerToFollow.position, followDistance);
            Gizmos.DrawLine(transform.position, playerToFollow.position);
        }

        // Current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }

        // Leader override target
        if (leaderOverrideActive)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leaderOverrideTarget, 1.5f);
            Gizmos.DrawLine(transform.position, leaderOverrideTarget);

            #if UNITY_EDITOR
            // Draw role text in editor
            UnityEditor.Handles.Label(
                leaderOverrideTarget + Vector3.up * 2f,
                $"Override: {leaderOverrideRole}",
                new GUIStyle { normal = new GUIStyleState { textColor = Color.cyan } }
            );
            #endif
        }
    }

    #endregion
}
