using UnityEngine;
using Scripts.FSM.Base.StateMachine;
using System.Collections.Generic;

public class CivilianController : NPCController
{
    [SerializeField] private CivilianDataSO civilianData;

    public CivilianDataSO CivilianData => civilianData;

    protected override void InitializeComponents()
    {
        npcData = civilianData;
        base.InitializeComponents();
    }

    public override bool CanSeePlayer()
    {
        return base.CanSeePlayer();
    }

    public bool PlayerInFearRadius()
    {
        if (player == null || civilianData == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer <= civilianData.fearRadius;
    }

    public bool HasReachedSafeDistance()
    {
        if (player == null || civilianData == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer >= civilianData.fleeDistance;
    }

    public bool HasPanicTimeElapsed()
    {
        if (civilianData == null) return false;
        return model?.RuntimeState.stateTimer >= civilianData.panicDuration;
    }

    public void StartPanicking()
    {
        model?.RuntimeState.ResetStateTimer();
        Logger.LogDebug($"{gameObject.name}: Started panicking!");
    }

    public void FleeFromPlayer()
    {
        if (player != null)
        {
            Vector3 fleeDirection = (transform.position - player.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDirection * civilianData.fleeDistance;

            MoveTo(fleeTarget);
            FaceDirection(fleeDirection);
        }
    }

    public void CalmDown()
    {
        model?.RuntimeState.ResetStateTimer();
        StopMovement();
        Logger.LogDebug($"{gameObject.name}: Calmed down");
    }

    protected override void UpdateDetection()
    {
        base.UpdateDetection();

        // Additional civilian-specific detection (fear radius)
        if (model != null && player != null && civilianData != null)
        {
            bool inFearRadius = PlayerInFearRadius();
            if (inFearRadius && !model.RuntimeState.isPlayerVisible)
            {
                // Player is close but not visible, still causes fear
                model.UpdateDetection(false, player);
            }
        }
    }
}