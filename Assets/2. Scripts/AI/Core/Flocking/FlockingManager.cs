using System.Collections.Generic;
using Game.AI.Flocking;
using UnityEngine;

namespace FlockingSystem
{
    /// <summary>
    /// Central manager for all flocking entities.
    /// Uses spatial hashing for efficient neighbor queries O(n) instead of O(n²).
    /// </summary>
    public class FlockingManager : MonoBehaviour
    {
        [Header("Optimization")]
        [Tooltip("Cell size for spatial hashing. Should be ~2x the average detection radius.")]
        [Min(0.1f)]
        [SerializeField] private float spatialHashCellSize = 10f;

        [Tooltip("Rebuild spatial hash every N frames (1 = every frame, higher = better performance but less accuracy)")]
        [Range(1, 10)]
        [SerializeField] private int hashRebuildInterval = 1;

        private List<FlockingEntity> entities = new List<FlockingEntity>();
        private SpatialHash spatialHash;
        private int frameCounter = 0;

        private void Awake()
        {
            spatialHash = new SpatialHash(spatialHashCellSize);
        }

        private void LateUpdate()
        {
            frameCounter++;

            if (frameCounter >= hashRebuildInterval)
            {
                RebuildSpatialHash();
                frameCounter = 0;
            }
        }

        /// <summary>
        /// Register a new flocking entity.
        /// </summary>
        public void RegisterEntity(FlockingEntity entity)
        {
            if (!entities.Contains(entity))
            {
                entities.Add(entity);
            }
        }

        /// <summary>
        /// Unregister a flocking entity (called on destroy).
        /// </summary>
        public void UnregisterEntity(FlockingEntity entity)
        {
            entities.Remove(entity);
        }

        /// <summary>
        /// Get all neighbors within a radius of an entity.
        /// Uses spatial hashing for O(n) performance.
        /// </summary>
        public List<FlockingEntity> GetNeighbors(FlockingEntity entity, float radius)
        {
            return spatialHash.Query(entity.transform.position, radius, entity);
        }

        /// <summary>
        /// Rebuild the spatial hash grid with current entity positions.
        /// Called automatically based on hashRebuildInterval.
        /// </summary>
        private void RebuildSpatialHash()
        {
            spatialHash.Clear();

            foreach (var entity in entities)
            {
                if (entity != null)
                {
                    spatialHash.Insert(entity);
                }
            }
        }

        /// <summary>
        /// Get total number of registered entities.
        /// </summary>
        public int GetEntityCount()
        {
            return entities.Count;
        }

        /// <summary>
        /// Update spatial hash cell size at runtime.
        /// Recommended: 2x the average detection radius for optimal performance.
        /// </summary>
        public void SetSpatialHashCellSize(float cellSize)
        {
            spatialHashCellSize = Mathf.Max(0.1f, cellSize);
            spatialHash.SetCellSize(spatialHashCellSize);
            RebuildSpatialHash();
        }

        private void OnValidate()
        {
            if (spatialHash != null)
            {
                spatialHash.SetCellSize(spatialHashCellSize);
            }
        }
    }
}
