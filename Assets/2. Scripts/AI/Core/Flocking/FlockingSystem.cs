using System.Collections.Generic;
using Game.AI.Flocking;
using UnityEngine;

namespace FlockingSystem
{
    /// <summary>
    /// Entity that exhibits flocking behaviour based on a configurable profile.
    /// Registers with FlockingManager and calculates forces from nearby neighbors.
    /// Uses static Steering class for all movement calculations.
    /// </summary>
    public class FlockingEntity : MonoBehaviour
    {
        [Header("Flocking Configuration")]
        [Tooltip("Profile defining behaviours and parameters")]
        [SerializeField] private FlockingProfile profile;

        [Header("References")]
        [Tooltip("FlockingManager reference - assign manually")]
        [SerializeField] private FlockingManager flockingManager;

        [Tooltip("Rigidbody reference - assign manually")]
        [SerializeField] private Rigidbody rb;

        [Header("Movement Settings")]
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float maxForce = 3f;

        private Vector3 velocity;
        private List<NeighborData> cachedNeighbors = new List<NeighborData>();

        public FlockingProfile Profile => profile;
        public Vector3 Velocity => velocity;

        private void Awake()
        {
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }

            if (profile != null)
            {
                maxSpeed = profile.maxSpeed;
                maxForce = profile.maxForce;
            }
        }

        private void Start()
        {
            if (flockingManager != null)
            {
                flockingManager.RegisterEntity(this);
            }
            else
            {
                Debug.LogError($"FlockingManager reference not assigned on {gameObject.name}", this);
            }

            ApplyInitialVelocity();
        }

        private void Update()
        {
            if (profile == null)
                return;

            UpdateNeighbors();
            ApplyFlockingForces();
            Move();
        }

        private void OnDestroy()
        {
            if (flockingManager != null)
            {
                flockingManager.UnregisterEntity(this);
            }
        }

        private void ApplyInitialVelocity()
        {
            if (profile == null) return;

            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ).normalized;

            AddForce(randomDirection * maxSpeed);
        }

        private void UpdateNeighbors()
        {
            cachedNeighbors.Clear();

            if (flockingManager == null)
                return;

            List<FlockingEntity> nearbyEntities = flockingManager.GetNeighbors(this, profile.detectionRadius);

            int maxNeighbors = profile.maxNeighbors > 0 ? profile.maxNeighbors : nearbyEntities.Count;
            int count = Mathf.Min(nearbyEntities.Count, maxNeighbors);

            for (int i = 0; i < count; i++)
            {
                cachedNeighbors.Add(new NeighborData(this, nearbyEntities[i]));
            }
        }

        private void ApplyFlockingForces()
        {
            if (cachedNeighbors.Count == 0)
                return;

            Vector3 totalForce = Vector3.zero;

            // Separation
            if (profile.separationConfig != null)
            {
                Vector3 separationForce = SeparationBehaviour.Calculate(this, cachedNeighbors, profile.separationConfig, profile);
                totalForce += separationForce * profile.separationConfig.weight;
            }

            // Cohesion
            if (profile.cohesionConfig != null)
            {
                Vector3 cohesionForce = CohesionBehaviour.Calculate(this, cachedNeighbors, profile.cohesionConfig, profile);
                totalForce += cohesionForce * profile.cohesionConfig.weight;
            }

            // Alignment
            if (profile.alignmentConfig != null)
            {
                Vector3 alignmentForce = AlignmentBehaviour.Calculate(this, cachedNeighbors, profile.alignmentConfig, profile);
                totalForce += alignmentForce * profile.alignmentConfig.weight;
            }

            AddForce(totalForce);
        }

        private void AddForce(Vector3 force)
        {
            velocity = Vector3.ClampMagnitude(velocity + force, maxSpeed);
            velocity.y = 0f; // Keep in XZ plane
        }

        private void Move()
        {
            if (velocity.sqrMagnitude < 0.001f)
                return;

            transform.forward = velocity.normalized;
            transform.position += velocity * Time.deltaTime;
        }

        public void SetProfile(FlockingProfile newProfile)
        {
            profile = newProfile;

            if (profile != null)
            {
                maxSpeed = profile.maxSpeed;
                maxForce = profile.maxForce;
            }
        }
    }
}
