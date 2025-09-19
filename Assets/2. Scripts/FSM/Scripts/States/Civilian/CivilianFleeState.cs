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
                // Perform flee movement
                PerformFleeMovement(civilian);

                // Update safety timer (accumulates only while safe conditions are met)
                UpdateSafetyTimer(civilian);

                // FSM conditions will handle transition when safe conditions are sustained
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

        private void UpdateSafetyTimer(Civilian civilian)
        {
            if (civilian.Player == null) return;

            float distanceToPlayer = Vector3.Distance(civilian.transform.position, civilian.Player.position);
            bool hasLineOfSight = civilian.HasLoS();
            bool isSafeDistance = distanceToPlayer >= civilian.SafeDistance;

            // Safety conditions: far enough AND no line of sight
            bool isSafe = isSafeDistance && !hasLineOfSight;

            if (isSafe)
            {
                // Accumulate safe time (grace timer)
                civilian.SafeTimer += Time.deltaTime;

                if (civilian.EnableDebugLogs && civilian.SafeTimer > 0f && 
                    Mathf.FloorToInt(civilian.SafeTimer) != Mathf.FloorToInt(civilian.SafeTimer - Time.deltaTime))
                {
                    Logger.LogInfo($"Civilian {civilian.name}: Safe for {civilian.SafeTimer:F1}s/{civilian.SafeTime:F1}s (distance: {distanceToPlayer:F1}, no LoS)");
                }
            }
            else
            {
                // Reset safe timer if conditions not met
                if (civilian.SafeTimer > 0f)
                {
                    if (civilian.EnableDebugLogs)
                    {
                        string reason = !isSafeDistance ? $"too close ({distanceToPlayer:F1} < {civilian.SafeDistance:F1})" : "still visible";
                        Logger.LogInfo($"Civilian {civilian.name}: Safety lost - {reason}, resetting timer");
                    }
                    civilian.SafeTimer = 0f;
                }
            }
        }
    }
}