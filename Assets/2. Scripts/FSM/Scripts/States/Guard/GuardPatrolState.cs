using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "GuardPatrolState", menuName = "Main/FSM/Guard States/Patrol State")]
    public class GuardPatrolState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                Logger.LogDebug($"Guard {guard.name}: Entered Patrol State");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                MoveTowardsPatrolPoint(guard);
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                Logger.LogDebug($"Guard {guard.name}: Exited Patrol State");
            }
        }

        private void MoveTowardsPatrolPoint(Guard guard)
        {
            if (guard.PatrolPoints == null || guard.PatrolPoints.Length == 0) return;

            var currentPatrolPoint = guard.PatrolPoints[guard.CurrentPatrolIndex];
            if (currentPatrolPoint == null) return;

            Vector3 direction = (currentPatrolPoint.position - guard.transform.position).normalized;
            direction.y = 0;

            if (direction.magnitude > 0.1f)
            {
                guard.Move(direction.normalized * guard.PatrolSpeed);
            }
        }
    }
}