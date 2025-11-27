using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "AllyGuardOutOfAttackRangeCondition", menuName = "Main/FSM/Ally Conditions/Guard Out Of Attack Range")]
public class AllyGuardOutOfAttackRangeCondition : StateCondition
{
    [SerializeField] [Range(0f, 1f)] private float extraTolerance = 0.15f;

    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is Ally ally)
        {
            return ally.IsGuardOutOfAttackRange(extraTolerance);
        }

        return false;
    }
}
