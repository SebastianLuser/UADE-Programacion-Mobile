using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianIdleState", menuName = "Main/FSM/Civilian States/Idle State")]
    public class CivilianIdleState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                civilian.StopMovement();
                Logger.LogDebug($"Civilian {civilian.name}: Entered Idle State");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                Logger.LogDebug($"Civilian {civilian.name}: Exited Idle State");
            }
        }
    }
}