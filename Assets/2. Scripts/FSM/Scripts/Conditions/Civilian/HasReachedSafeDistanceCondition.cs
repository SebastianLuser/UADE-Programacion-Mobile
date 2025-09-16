using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "HasReachedSafeDistanceCondition", menuName = "Main/FSM/Civilian Conditions/Has Reached Safe Distance")]
public class HasReachedSafeDistanceCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is CivilianController civilian)
        {
            return civilian.HasReachedSafeDistance();
        }
        return false;
    }
}