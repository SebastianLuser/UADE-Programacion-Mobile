using UnityEngine;

public class CivilianModel : NPCModel
{
    public CivilianDataSO CivilianData { get; private set; }

    public CivilianModel(CivilianDataSO civilianData) : base(civilianData)
    {
        CivilianData = civilianData;
    }

    public bool IsPlayerInFearRadius(Transform playerTransform)
    {
        if (playerTransform == null || CivilianData == null) return false;

        float distance = Vector3.Distance(
            RuntimeState.lastKnownPlayerPosition,
            playerTransform.position
        );

        return distance <= CivilianData.fearRadius;
    }

    public bool HasReachedSafeDistance(Transform playerTransform)
    {
        if (playerTransform == null || CivilianData == null) return false;

        Vector3 currentPosition = RuntimeState.currentTarget?.position ?? Vector3.zero;
        float distance = Vector3.Distance(currentPosition, playerTransform.position);

        return distance >= CivilianData.fleeDistance;
    }

    public bool CanFlee()
    {
        return RuntimeState.isAlive;
    }

    public void StartPanic()
    {
        RuntimeState.ResetStateTimer();
    }

    public void StartFleeing()
    {
        RuntimeState.ResetStateTimer();
    }

    public bool HasPanicTimeElapsed()
    {
        return RuntimeState.stateTimer >= CivilianData.panicDuration;
    }

    public Vector3 GetFleeDirection(Transform playerTransform, Vector3 currentPosition)
    {
        if (playerTransform == null) return Vector3.forward;

        Vector3 direction = (currentPosition - playerTransform.position).normalized;
        return direction;
    }
}