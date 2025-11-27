using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "AllyGuardLostCondition", menuName = "Main/FSM/Ally Conditions/Guard Lost")]
public class AllyGuardLostCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is Ally ally)
        {
            var target = ally.GetCurrentTarget();
            if (target == null) return true;
            if (!target.IsAlive) return true;

            return !ally.CanSeeGuard(target);
        }

        return false;
    }
}
