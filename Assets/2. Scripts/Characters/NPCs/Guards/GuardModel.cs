using UnityEngine;

public class GuardModel : NPCModel
{
    public GuardDataSO GuardData { get; private set; }

    public GuardModel(GuardDataSO guardData) : base(guardData)
    {
        GuardData = guardData;
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
}