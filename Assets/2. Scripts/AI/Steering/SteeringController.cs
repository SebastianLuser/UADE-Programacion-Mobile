using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AI.Interfaces;

namespace AI.Steering
{
    public class SteeringController : MonoBehaviour, ISteeringContext
    {
        [Header("Steering Parameters")]
        public float maxSpeed = 5f;
        public float maxForce = 10f;
        public Transform target;
        
        [Header("Obstacle Detection")]
        public LayerMask obstacleLayer = 1;
        public float obstacleDetectionRadius = 1f;

        // ISteeringContext Implementation
        public Vector3 Position => transform.position;
        public Vector3 Velocity => velocity;
        public Vector3 Forward => transform.forward;
        public float MaxSpeed => maxSpeed;
        public float MaxForce => maxForce;
        public Transform Transform => transform;
        public Transform Target => target;
        public Vector3 TargetPosition => target != null ? target.position : Vector3.zero;
        public Vector3 TargetVelocity => target != null ? GetTargetVelocity() : Vector3.zero;

        // Private fields
        private Vector3 velocity;
        private List<ISteeringBehaviour> behaviours = new List<ISteeringBehaviour>();

        public void AddBehaviour(ISteeringBehaviour behaviour)
        {
            behaviours.Add(behaviour);
        }

        public void RemoveBehaviour(ISteeringBehaviour behaviour)
        {
            behaviours.Remove(behaviour);
        }

        public Vector3 CalculateCombinedSteering()
        {
            Vector3 totalSteering = Vector3.zero;
            float totalWeight = 0f;

            // Sort by priority (highest first)
            var activeBehaviours = behaviours.Where(b => b.IsActive).OrderByDescending(b => b.Priority);

            foreach (var behaviour in activeBehaviours)
            {
                Vector3 steering = behaviour.CalculateSteering(this);
                if (steering != Vector3.zero)
                {
                    totalSteering += steering * behaviour.Priority;
                    totalWeight += behaviour.Priority;
                }
            }

            // Normalize by total weight
            if (totalWeight > 0)
            {
                totalSteering /= totalWeight;
            }

            return Vector3.ClampMagnitude(totalSteering, maxForce);
        }

        public void UpdateSteering(float deltaTime)
        {
            Vector3 steering = CalculateCombinedSteering();
            
            // Apply steering force
            velocity += steering * deltaTime;
            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

            // Move
            if (velocity.magnitude > 0.1f)
            {
                transform.position += velocity * deltaTime;
                transform.rotation = Quaternion.LookRotation(velocity);
            }
        }

        public bool HasObstacleInPath(Vector3 direction, float distance, out Vector3 avoidanceDirection)
        {
            avoidanceDirection = Vector3.zero;

            if (Physics.SphereCast(Position, obstacleDetectionRadius, direction, out RaycastHit hit, distance, obstacleLayer))
            {
                // Calculate avoidance direction (perpendicular to obstacle)
                Vector3 obstacleToAgent = (Position - hit.point).normalized;
                avoidanceDirection = Vector3.Cross(direction, Vector3.up).normalized;
                
                // Choose left or right based on which is closer to away from obstacle
                Vector3 leftAvoid = Vector3.Cross(direction, Vector3.up).normalized;
                Vector3 rightAvoid = -leftAvoid;
                
                if (Vector3.Dot(leftAvoid, obstacleToAgent) > Vector3.Dot(rightAvoid, obstacleToAgent))
                {
                    avoidanceDirection = leftAvoid;
                }
                else
                {
                    avoidanceDirection = rightAvoid;
                }

                return true;
            }

            return false;
        }

        private Vector3 GetTargetVelocity()
        {
            // Try to get velocity from various components
            if (target == null) return Vector3.zero;

            var rigidbody = target.GetComponent<Rigidbody>();
            if (rigidbody != null) return rigidbody.linearVelocity;

            var steeringController = target.GetComponent<SteeringController>();
            if (steeringController != null) return steeringController.velocity;

            // Fallback: estimate velocity from position change
            return Vector3.zero;
        }

        // Get specific behaviour type - useful for configuration
        public T GetBehaviour<T>() where T : class, ISteeringBehaviour
        {
            return behaviours.OfType<T>().FirstOrDefault();
        }
    }
}