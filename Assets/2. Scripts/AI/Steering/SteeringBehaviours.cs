using UnityEngine;
using AI.Interfaces;

namespace AI.Steering
{
    // Individual Steering Behaviours
    public class SeekBehaviour : ISteeringBehaviour
    {
        public float Priority { get; set; } = 1f;
        public bool IsActive { get; set; } = true;

        public Vector3 CalculateSteering(ISteeringContext context)
        {
            if (!IsActive || context.Target == null) return Vector3.zero;

            Vector3 desiredVelocity = (context.TargetPosition - context.Position).normalized * context.MaxSpeed;
            return desiredVelocity - context.Velocity;
        }
    }

    public class FleeBehaviour : ISteeringBehaviour
    {
        public float Priority { get; set; } = 2f;
        public bool IsActive { get; set; } = true;
        public float FleeRadius { get; set; } = 10f;

        public Vector3 CalculateSteering(ISteeringContext context)
        {
            if (!IsActive || context.Target == null) return Vector3.zero;

            float distance = Vector3.Distance(context.Position, context.TargetPosition);
            if (distance > FleeRadius) return Vector3.zero;

            Vector3 desiredVelocity = (context.Position - context.TargetPosition).normalized * context.MaxSpeed;
            return desiredVelocity - context.Velocity;
        }
    }

    public class PursuitBehaviour : ISteeringBehaviour
    {
        public float Priority { get; set; } = 1f;
        public bool IsActive { get; set; } = true;
        public float PredictionTime { get; set; } = 1f;

        public Vector3 CalculateSteering(ISteeringContext context)
        {
            if (!IsActive || context.Target == null) return Vector3.zero;

            // Predict future position of target
            float distance = Vector3.Distance(context.Position, context.TargetPosition);
            float time = distance / context.MaxSpeed;
            time = Mathf.Min(time, PredictionTime); // Limit prediction time

            Vector3 futurePosition = context.TargetPosition + (context.TargetVelocity * time);
            
            // Seek to predicted position
            Vector3 desiredVelocity = (futurePosition - context.Position).normalized * context.MaxSpeed;
            return desiredVelocity - context.Velocity;
        }
    }

    public class ObstacleAvoidanceBehaviour : ISteeringBehaviour
    {
        public float Priority { get; set; } = 5f; // Highest priority
        public bool IsActive { get; set; } = true;
        public float LookAheadDistance { get; set; } = 5f;

        public Vector3 CalculateSteering(ISteeringContext context)
        {
            if (!IsActive) return Vector3.zero;

            Vector3 avoidanceDirection;
            if (context.HasObstacleInPath(context.Forward, LookAheadDistance, out avoidanceDirection))
            {
                return avoidanceDirection * context.MaxForce;
            }

            return Vector3.zero;
        }
    }

    public class WanderBehaviour : ISteeringBehaviour
    {
        public float Priority { get; set; } = 0.5f;
        public bool IsActive { get; set; } = true;
        public float WanderRadius { get; set; } = 3f;
        public float WanderDistance { get; set; } = 5f;
        public float WanderJitter { get; set; } = 1f;

        private Vector3 wanderTarget = Vector3.forward;

        public Vector3 CalculateSteering(ISteeringContext context)
        {
            if (!IsActive) return Vector3.zero;

            // Add small random displacement to wander target
            wanderTarget += new Vector3(
                UnityEngine.Random.Range(-WanderJitter, WanderJitter),
                0,
                UnityEngine.Random.Range(-WanderJitter, WanderJitter)
            );

            // Project back to unit circle
            wanderTarget = wanderTarget.normalized;

            // Scale by wander radius
            wanderTarget *= WanderRadius;

            // Move wander target ahead of character
            Vector3 targetLocal = wanderTarget + Vector3.forward * WanderDistance;
            
            // Transform to world space
            Vector3 targetWorld = context.Transform.TransformPoint(targetLocal);

            // Seek to wander target
            Vector3 desiredVelocity = (targetWorld - context.Position).normalized * context.MaxSpeed;
            return desiredVelocity - context.Velocity;
        }
    }
}