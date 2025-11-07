using UnityEngine;
using Services;
using Services.MicroServices.BlackboardService;

/// <summary>
/// Centralizes blackboard writes for AI events (player info, detection, combat, global alerts).
/// Characters call into this module instead of duplicating ServiceLocator plumbing.
/// </summary>
[DisallowMultipleComponent]
public class AlertEmitter : MonoBehaviour
{
    [SerializeField] private string agentPrefix = "Agent";
    [SerializeField] private bool logWarnings = true;

    private IBlackboardService blackboardService;
    private Transform trackedPlayer;

    public IBlackboardService Blackboard => blackboardService;

    private void Awake()
    {
        blackboardService = ServiceLocator.Get<IBlackboardService>();
        if (blackboardService == null && logWarnings)
        {
            MyLogger.LogWarning($"{name}: AlertEmitter could not locate IBlackboardService.");
        }
    }

    public void Configure(string prefix)
    {
        if (!string.IsNullOrEmpty(prefix))
        {
            agentPrefix = prefix;
        }
    }

    public void SetPlayer(Transform player, bool syncImmediately = true)
    {
        trackedPlayer = player;
        if (syncImmediately)
        {
            SyncPlayerTransform();
        }
    }

    public void SyncPlayerTransform()
    {
        if (blackboardService == null || trackedPlayer == null) return;

        blackboardService.SetValue(BlackboardKeys.PLAYER_TRANSFORM, trackedPlayer);
        blackboardService.SetValue(BlackboardKeys.PLAYER_POSITION, trackedPlayer.position);
    }

    public void ReportDetection(DetectionResult detection, Vector3 lastKnownPosition)
    {
        if (blackboardService == null) return;

        string agentId = $"{agentPrefix}_{gameObject.GetInstanceID()}";
        blackboardService.SetValue($"{agentId}_DetectionLevel", detection.level);
        blackboardService.SetValue($"{agentId}_CanSeePlayer", detection.canSeePlayer);

        if (detection.canSeePlayer || lastKnownPosition != Vector3.zero)
        {
            blackboardService.SetValue(BlackboardKeys.LAST_KNOWN_PLAYER_POSITION, lastKnownPosition);
        }
    }

    public void ReportShot(float lastShootTime, Vector3 direction)
    {
        if (blackboardService == null) return;

        string agentId = $"{agentPrefix}_{gameObject.GetInstanceID()}";
        blackboardService.SetValue($"{agentId}_LastShootTime", lastShootTime);
        blackboardService.SetValue($"{agentId}_ShootDirection", direction);
    }

    public void EmitGlobalAlert(bool state = true)
    {
        if (blackboardService == null) return;
        blackboardService.SetValue(BlackboardKeys.GLOBAL_ALERT, state);
    }
}
