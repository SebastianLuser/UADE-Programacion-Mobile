using Scripts.FSM.Base.StateMachine;
using Scripts.FSM.Models;
using UnityEngine;

[CreateAssetMenu(fileName = "AllyNeedsCoverCondition", menuName = "Main/FSM/Ally Conditions/Needs Cover")]
public class AllyNeedsCoverCondition : StateCondition
{
    [SerializeField, Range(0f, 1f)] private float healthThreshold = 0.35f;

    public override bool CompleteCondition(IUseFsm p_model)
    {
        if (p_model is Ally ally)
        {
            float maxHealth = Mathf.Max(ally.MaxHealth, 0.01f);
            float healthPercent = ally.CurrentHealth / maxHealth;

            return healthPercent <= healthThreshold && ally.GetCurrentTarget() != null;
        }

        return false;
    }
}
