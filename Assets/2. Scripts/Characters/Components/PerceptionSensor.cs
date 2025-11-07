using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Lightweight perception wrapper that centralizes access to IPlayerDetector data,
/// maintains last-known information, and exposes convenience helpers for states/blackboard.
/// </summary>
[DisallowMultipleComponent]
public class PerceptionSensor : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Vision Settings")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float fieldOfView = 90f;
    [SerializeField] private float loseSightGrace = 2f;
    [SerializeField] private float forcedVisionRange = 1.5f;
    [SerializeField] private LayerMask detectorObstacleMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = false;
    [SerializeField] private bool logDetections = false;

    private IPlayerDetector playerDetector;
    private DetectionResult cachedResult = DetectionResult.None;
    private float lastSeenTime = float.NegativeInfinity;
    private Vector3 lastKnownPosition = Vector3.zero;
    private int lastUpdateFrame = -1;

    public Transform Target => target;
    public float DetectionRange => detectionRange;
    public float FieldOfView => fieldOfView;
    public float LoseSightGrace => loseSightGrace;
    public float ForcedVisionRange => forcedVisionRange;
    public Vector3 LastKnownPosition => lastKnownPosition;
    public float TimeSinceLastSeen => lastSeenTime < 0f ? float.MaxValue : Time.time - lastSeenTime;
    public bool CanSeeTarget => cachedResult.canSeePlayer;
    public bool HasLineOfSight => cachedResult.hasLineOfSight;
    public bool HasRecentContact => TimeSinceLastSeen <= loseSightGrace;
    public float DistanceToTarget => (target == null || playerDetector == null) ? float.MaxValue : playerDetector.GetDistanceToPlayer(target);

    private void Awake()
    {
        playerDetector = GetComponent<IPlayerDetector>();
        Assert.IsNotNull(playerDetector, $"{nameof(PerceptionSensor)} on {name} requires an IPlayerDetector implementation.");
        ApplyDetectorConfig();
    }

    private void OnValidate()
    {
        detectionRange = Mathf.Max(0.1f, detectionRange);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 360f);
        loseSightGrace = Mathf.Max(0f, loseSightGrace);
        forcedVisionRange = Mathf.Max(0f, forcedVisionRange);
    }

    public void SetTarget(Transform newTarget, bool refreshImmediately = true)
    {
        target = newTarget;
        if (refreshImmediately)
        {
            ForceRefresh();
        }
    }

    public void ClearTarget()
    {
        target = null;
        cachedResult = DetectionResult.None;
        lastKnownPosition = Vector3.zero;
        lastSeenTime = float.NegativeInfinity;
    }

    public void Configure(float range, float fov, float loseSight, float forcedRange, LayerMask obstacleMask)
    {
        detectionRange = Mathf.Max(0.1f, range);
        fieldOfView = Mathf.Clamp(fov, 1f, 360f);
        loseSightGrace = Mathf.Max(0f, loseSight);
        forcedVisionRange = Mathf.Max(0f, forcedRange);
        detectorObstacleMask = obstacleMask;
        ApplyDetectorConfig();
        ForceRefresh();
    }

    private void ApplyDetectorConfig()
    {
        playerDetector?.SetDetectionParameters(detectionRange, fieldOfView, detectorObstacleMask);
    }

    public DetectionResult GetDetectionResult(bool forceUpdate = false)
    {
        if (!forceUpdate && lastUpdateFrame == Time.frameCount)
        {
            return cachedResult;
        }

        if (target == null || playerDetector == null)
        {
            cachedResult = DetectionResult.None;
            cachedResult.lastKnownPosition = lastKnownPosition;
            cachedResult.timeSinceLastSeen = TimeSinceLastSeen;
            lastUpdateFrame = Time.frameCount;
            return cachedResult;
        }

        playerDetector.CanSeePlayer(target);
        DetectionResult detectorResult = playerDetector.GetCurrentDetectionResult();
        float distance = playerDetector.GetDistanceToPlayer(target);
        if (distance <= 0f)
        {
            distance = detectorResult.distance > 0f ? detectorResult.distance : Vector3.Distance(transform.position, target.position);
        }

        DetectionResult result = detectorResult;
        result.distance = distance;

        if (result.lastKnownPosition == Vector3.zero && result.canSeePlayer)
        {
            result.lastKnownPosition = target.position;
        }

        if (!result.canSeePlayer && forcedVisionRange > 0f && distance <= forcedVisionRange)
        {
            result = new DetectionResult(
                PlayerDetectionLevel.Immediate,
                true,
                true,
                true,
                distance,
                0f,
                target.position,
                0f
            );
        }

        CacheResult(result);
        lastUpdateFrame = Time.frameCount;
        return cachedResult;
    }

    public bool IsTargetWithinRange(float range)
    {
        return DistanceToTarget <= range;
    }

    public void ForceRefresh()
    {
        lastUpdateFrame = -1;
        GetDetectionResult(true);
    }

    private void CacheResult(DetectionResult result)
    {
        cachedResult = result;

        if (result.canSeePlayer && target != null)
        {
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;
            cachedResult.lastKnownPosition = lastKnownPosition;
            cachedResult.timeSinceLastSeen = 0f;

            if (logDetections)
            {
                Debug.Log($"[PerceptionSensor:{name}] Saw target at {lastKnownPosition} (level {result.level})");
            }
        }
        else
        {
            cachedResult.lastKnownPosition = lastKnownPosition;
            cachedResult.timeSinceLastSeen = TimeSinceLastSeen;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos) return;

        Vector3 origin = transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, forcedVisionRange);

        Vector3 forward = transform.forward * detectionRange;
        Quaternion leftRot = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f);
        Quaternion rightRot = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + leftRot * forward);
        Gizmos.DrawLine(origin, origin + rightRot * forward);
    }
}
