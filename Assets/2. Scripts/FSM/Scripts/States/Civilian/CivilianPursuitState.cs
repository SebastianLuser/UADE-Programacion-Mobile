using Scripts.FSM.Models;
using UnityEngine;
using Game.AI.Steering;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianPursuitState", menuName = "Main/FSM/Civilian States/Pursuit State")]
    public class CivilianPursuitState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Reset lose sight timer when entering pursuit
                civilian.PursuitLoseSightTimer = 0f;
                civilian.SetCurrentMaxSpeed(civilian.PursueSpeed);

                if (civilian.EnableDebugLogs)
                    Logger.LogInfo($"Civilian {civilian.name}: Entered Pursuit State - Chasing at speed {civilian.PursueSpeed:F1}");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Update the lose sight timer based on current LoS status
                UpdateLoseSightTimer(civilian);

                // Perform pursuit movement
                PerformPursuitMovement(civilian);

                // Check if in melee range (transition handled by InMeleeRangeCondition)
                float distanceToPlayer = civilian.GetDistanceToPlayer();
                if (civilian.EnableDebugLogs && distanceToPlayer <= civilian.MeleeRange + 0.1f)
                {
                    Logger.LogInfo($"Civilian {civilian.name}: In melee range ({distanceToPlayer:F2}), ready to attack");
                }

                // The ShouldAbortPursuit() decision will be handled by CivilianLoseSightCondition
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Reset timer when exiting
                civilian.PursuitLoseSightTimer = 0f;
                
                if (civilian.EnableDebugLogs)
                    Logger.LogInfo($"Civilian {civilian.name}: Exited Pursuit State - LoseSight timer: {civilian.PursuitLoseSightTimer:F2}s");
            }
        }

        /// <summary>
        /// Update the lose sight timer - this is the core logic owned by this state
        /// </summary>
        private void UpdateLoseSightTimer(Civilian civilian)
        {
            bool hasLoS = civilian.HasLoS();
            
            if (!hasLoS)
            {
                // Accumulate lose sight time
                civilian.PursuitLoseSightTimer += Time.deltaTime;
                
                if (civilian.EnableDebugLogs && civilian.PursuitLoseSightTimer > 0f)
                {
                    // Log every 0.5 seconds
                    float logInterval = 0.5f;
                    if (Mathf.FloorToInt(civilian.PursuitLoseSightTimer / logInterval) != 
                        Mathf.FloorToInt((civilian.PursuitLoseSightTimer - Time.deltaTime) / logInterval))
                    {
                        Logger.LogInfo($"Civilian {civilian.name}: Lost sight for {civilian.PursuitLoseSightTimer:F1}s/{civilian.AttackLoseSightGrace:F1}s");
                    }
                }
            }
            else
            {
                // Reset timer when we regain sight
                if (civilian.PursuitLoseSightTimer > 0f && civilian.EnableDebugLogs)
                {
                    Logger.LogInfo($"Civilian {civilian.name}: Regained sight, resetting timer from {civilian.PursuitLoseSightTimer:F2}s");
                }
                civilian.PursuitLoseSightTimer = 0f;
            }
        }

        private void PerformPursuitMovement(Civilian civilian)
        {
            if (civilian.Player == null) return;

            Vector3 civilianPos = civilian.transform.position;
            Vector3 playerPos = civilian.Player.position;
            
            // Get player velocity if available
            Vector3 playerVelocity = Vector3.zero;
            var playerRb = civilian.Player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerVelocity = playerRb.linearVelocity;
            }

            // Use Steering.Pursuit for predictive chasing
            Vector3 steering = Steering.Pursuit(
                civilianPos,
                civilian.CurrentVelocity,
                playerPos,
                playerVelocity,
                civilian.PursueSpeed
            );

            // Apply through the movement funnel (ApplySteering -> ObstacleAvoidance.GetDir2 -> move)
            civilian.ApplySteering(steering);
        }
    }
}