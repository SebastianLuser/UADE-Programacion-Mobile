using Scripts.FSM.Models;
using UnityEngine;
using Game.AI.Steering;

namespace Scripts.FSM.Base.StateMachine
{
    [CreateAssetMenu(fileName = "CivilianEvadeState", menuName = "Main/FSM/Civilian States/Evade State")]
    public class CivilianEvadeState : State
    {
        public override void EnterState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Initialize evade timer using EvadeTime property
                civilian.StateTimer = civilian.EvadeTime;
                civilian.SetCurrentMaxSpeed(civilian.EvadeSpeed);

                if (civilian.EnableDebugLogs)
                    Logger.LogInfo($"Civilian {civilian.name}: Entered Evade State - Evading for {civilian.EvadeTime:F2}s at speed {civilian.EvadeSpeed:F1}");
            }
        }

        public override void ExecuteState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                // Perform evasive movement only - Decision Tree handles transitions
                PerformEvadeMovement(civilian);
            }
        }

        public override void ExitState(IUseFsm p_model)
        {
            if (p_model is Civilian civilian)
            {
                if (civilian.EnableDebugLogs)
                    Logger.LogInfo($"Civilian {civilian.name}: Exited Evade State - Timer expired, transitioning to Flee");
            }
        }

        private void PerformEvadeMovement(Civilian civilian)
        {
            if (civilian.Player == null) return;

            Vector3 playerPosition = civilian.Player.position;
            Vector3 playerVelocity = GetPlayerVelocity(civilian);

            // Use Steering.Evade for predictive evasion (burst lateral/away movement)
            Vector3 steering = Steering.Evade(
                civilian.transform.position,
                civilian.CurrentVelocity,
                playerPosition,
                playerVelocity,
                civilian.EvadeSpeed
            );

            // Add some lateral force for unpredictable burst movement
            Vector3 lateralForce = GetLateralEvasionForce(civilian);
            steering += lateralForce;

            // Apply through the movement funnel (ApplySteering -> ObstacleAvoidance.GetDir2 -> move)
            civilian.ApplySteering(steering);
        }

        private Vector3 GetPlayerVelocity(Civilian civilian)
        {
            if (civilian.Player == null) return Vector3.zero;

            // Try to get player velocity from Rigidbody
            var playerRb = civilian.Player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                return playerRb.linearVelocity;
            }

            // Fallback: estimate velocity from position change
            return Vector3.zero;
        }

        private Vector3 GetLateralEvasionForce(Civilian civilian)
        {
            if (civilian.Player == null) return Vector3.zero;

            // Calculate perpendicular direction to player for lateral burst movement
            Vector3 dirToPlayer = (civilian.Player.position - civilian.transform.position).normalized;
            Vector3 lateralDirection = Vector3.Cross(Vector3.up, dirToPlayer);

            // Alternate between left and right based on time for unpredictable movement
            float lateralSign = Mathf.Sign(Mathf.Sin(Time.time * 3f));
            lateralDirection *= lateralSign;

            // Scale by evade speed for strong lateral burst
            return lateralDirection * civilian.EvadeSpeed * 0.5f;
        }
    }
}