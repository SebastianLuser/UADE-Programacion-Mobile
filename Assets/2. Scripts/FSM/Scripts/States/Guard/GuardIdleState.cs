using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "GuardIdleState", menuName = "Main/FSM/Guard States/Idle State")]
    public class GuardIdleState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                guard.StateTimer = 0f;
                Logger.LogDebug($"Guard {guard.name}: Entered Idle State");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                Logger.LogDebug($"Guard {guard.name}: Exited Idle State");
            }
        }
    }
}