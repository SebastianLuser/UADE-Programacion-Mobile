using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianCalmState", menuName = "Main/FSM/Civilian States/Calm State")]
    public class CivilianCalmState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                civilian.CalmDown();
                MyLogger.LogDebug($"Civilian {civilian.name}: Entered Calm State");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                MyLogger.LogDebug($"Civilian {civilian.name}: Exited Calm State");
            }
        }
    }
}