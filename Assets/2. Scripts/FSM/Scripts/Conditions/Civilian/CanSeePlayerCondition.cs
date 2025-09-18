using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "CivilianCanSeePlayerCondition", menuName = "Main/FSM/Civilian Conditions/Can See Player")]
public class CanSeePlayerCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is CivilianController civilian)
        {
            return civilian.CanSeePlayer();
        }
        return false;
    }
}