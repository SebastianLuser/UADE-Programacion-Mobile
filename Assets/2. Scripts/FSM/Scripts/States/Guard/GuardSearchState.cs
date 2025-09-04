using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "GuardSearchState", menuName = "Main/FSM/Guard States/Search State")]
    public class GuardSearchState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                guard.StateTimer = 0f;
                Logger.LogDebug($"Guard {guard.name}: Entered Search State - Looking for player at {guard.LastKnownPlayerPosition}");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                SearchForPlayer(guard);
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                Logger.LogDebug($"Guard {guard.name}: Exited Search State");
            }
        }

        private void SearchForPlayer(Guard guard)
        {
            Vector3 direction = (guard.LastKnownPlayerPosition - guard.transform.position).normalized;
            direction.y = 0;

            float distanceToLastKnown = Vector3.Distance(guard.transform.position, guard.LastKnownPlayerPosition);
            
            if (distanceToLastKnown > 1f && direction.magnitude > 0.1f)
            {
                guard.Move(direction.normalized * guard.PatrolSpeed);
            }
        }
    }
}