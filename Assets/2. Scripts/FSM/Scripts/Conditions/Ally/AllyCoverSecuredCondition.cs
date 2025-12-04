using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "AllyCoverSecuredCondition", menuName = "Main/FSM/Ally Conditions/Cover Secured")]
public class AllyCoverSecuredCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is Ally ally)
        {
            if (!ally.HasCoverPoint) return false;

            float distance = Vector3.Distance(ally.transform.position, ally.CoverPoint);
            return distance <= ally.CoverArrivalTolerance;
        }

        return false;
    }
}
