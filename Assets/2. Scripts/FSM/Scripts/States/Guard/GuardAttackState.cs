using Scripts.FSM.Models;
using UnityEngine;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "GuardAttackState", menuName = "Main/FSM/Guard States/Attack State")]
    public class GuardAttackState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                Logger.LogDebug($"Guard {guard.name}: Entered Attack State");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                AttackPlayer(guard);
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Guard guard)
            {
                Logger.LogDebug($"Guard {guard.name}: Exited Attack State");
            }
        }

        private void AttackPlayer(Guard guard)
        {
            Transform target = guard.GetTargetTransform();
            if (target == null) return;

            Vector3 direction = (target.position - guard.transform.position).normalized;
            direction.y = 0;
            
            if (direction.magnitude > 0.1f)
            {
                guard.transform.rotation = Quaternion.LookRotation(direction);
            }

            guard.Shoot(direction);
        }
    }
}