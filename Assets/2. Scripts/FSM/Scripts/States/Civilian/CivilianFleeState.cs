using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianFleeState", menuName = "Main/FSM/Civilian States/Flee State")]
    public class CivilianFleeState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is CivilianController civilian)
            {
                Logger.LogDebug($"Civilian {civilian.name}: Entered Flee State");
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
                Logger.LogDebug($"Civilian {civilian.name}: Exited Flee State");
            }
        }
    }
}