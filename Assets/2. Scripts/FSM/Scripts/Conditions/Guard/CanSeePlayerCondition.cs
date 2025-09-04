using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "CanSeePlayerCondition", menuName = "Main/FSM/Guard Conditions/Can See Player")]
public class CanSeePlayerCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is Guard guard)
        {
            return CanSeePlayer(guard);
        }
        return false;
    }

    private bool CanSeePlayer(Guard guard)
    {
        Transform player = guard.GetTargetTransform();
        if (player == null) return false;

        Vector3 directionToPlayer = player.position - guard.transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        // Check distance
        if (distanceToPlayer > guard.DetectionRange) return false;

        // Check field of view
        Vector3 forward = guard.transform.forward;
        float angle = Vector3.Angle(forward, directionToPlayer.normalized);
        if (angle > guard.FieldOfView * 0.5f) return false;

        // Check line of sight with raycast
        if (Physics.Raycast(guard.transform.position + Vector3.up * 0.5f, directionToPlayer.normalized, 
            out RaycastHit hit, distanceToPlayer))
        {
            return hit.transform == player;
        }

        return true;
    }
}