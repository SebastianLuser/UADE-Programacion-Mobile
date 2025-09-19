using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "HasPanicTimeElapsedCondition", menuName = "Main/FSM/Civilian Conditions/Has Panic Time Elapsed")]
public class HasPanicTimeElapsedCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is CivilianController civilian)
        {
            return civilian.HasPanicTimeElapsed();
        }
        return false;
    }
}