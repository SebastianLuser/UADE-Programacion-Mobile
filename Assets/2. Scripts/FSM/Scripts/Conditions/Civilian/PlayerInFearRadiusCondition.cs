using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInFearRadiusCondition", menuName = "Main/FSM/Civilian Conditions/Player In Fear Radius")]
public class PlayerInFearRadiusCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is CivilianController civilian)
        {
            return civilian.PlayerInFearRadius();
        }
        return false;
    }
}