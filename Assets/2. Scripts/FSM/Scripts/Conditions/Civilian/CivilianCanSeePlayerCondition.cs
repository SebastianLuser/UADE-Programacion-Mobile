using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "CanSeePlayerCondition", menuName = "Main/FSM/Civilian Conditions/Can See Player")]
public class CivilianCanSeePlayerCondition : StateCondition
{
    public override bool CompleteCondition(IUseFsm p_model)
    {
        Debug.Log($"CanSeePlayerCondition: Evaluating for {p_model?.GetType()?.Name}");
    
        if (p_model is Civilian civilian)
        {
            bool canSee = civilian.HasLoS();
            Debug.Log($"CanSeePlayerCondition: HasLoS() = {canSee}");
            return canSee;
        }
    
        Debug.Log("CanSeePlayerCondition: Model is not Civilian!");
        return false;
    }
}