using Scripts.FSM.Models;
using UnityEngine;
using Game.AI.Steering;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianFleeState", menuName = "Main/FSM/Civilian States/Flee State")]
    public class CivilianFleeState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Initialize safe timer (tracks time spent in safe conditions)
                civilian.SafeTimer = 0f;
                civilian.SetCurrentMaxSpeed(civilian.FleeSpeed);

                if (civilian.EnableDebugLogs)
                    Logger.LogInfo($"Civilian {civilian.name}: Entered Flee State - Fleeing at speed {civilian.FleeSpeed:F1}");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Perform flee movement only - Decision Tree handles transitions
                PerformFleeMovement(civilian);
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                if (civilian.EnableDebugLogs)
                    Logger.LogInfo($"Civilian {civilian.name}: Exited Flee State - Reached safety, returning to Idle");
            }
        }

        private void PerformFleeMovement(Civilian civilian)
        {
            if (civilian.Player == null) return;

            Vector3 playerPosition = civilian.Player.position;

            // Use Steering.Flee for direct sustained flee behavior
            Vector3 steering = Steering.Flee(
                civilian.transform.position,
                playerPosition,
                civilian.CurrentVelocity,
                civilian.FleeSpeed
            );

            // Apply through the movement funnel (ApplySteering -> ObstacleAvoidance.GetDir2 -> move)
            civilian.ApplySteering(steering);
        }

    }
}