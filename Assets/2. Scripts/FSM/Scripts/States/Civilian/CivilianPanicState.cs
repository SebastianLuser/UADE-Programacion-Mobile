using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianPanicState", menuName = "Main/FSM/Civilian States/Panic State")]
    public class CivilianPanicState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                civilian.StartPanicking();
                Logger.LogDebug($"Civilian {civilian.name}: Entered Panic State");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                civilian.FleeFromPlayer();
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                Logger.LogDebug($"Civilian {civilian.name}: Exited Panic State");
            }
        }
    }
}