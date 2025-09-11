using UnityEngine;
using AI.Interfaces;

namespace AI.Core
{
    [System.Serializable]
    public class AIBlackboard : IAIBlackboard
    {
        [Header("Health Configuration")]
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float lowHealthThreshold = 30f;

        [Header("Detection Configuration")]
        [SerializeField] private float sightRange = 10f;
        [SerializeField] private float attackRange = 3f;

        // Runtime data
        public bool IsPlayerInSight { get; set; }
        public float DistanceToPlayer { get; set; } = Mathf.Infinity;
        public Vector3 LastKnownPlayerPosition { get; set; }
        public bool IsAlive { get; set; } = true;
        public Vector3 PatrolTarget { get; set; }
        public bool HasReachedPatrolPoint { get; set; }

        // Properties
        public float CurrentHealth 
        { 
            get => currentHealth; 
            set => currentHealth = Mathf.Clamp(value, 0, maxHealth);
        }
        public float MaxHealth => maxHealth;
        public float LowHealthThreshold => lowHealthThreshold;
        public float SightRange => sightRange;
        public float AttackRange => attackRange;

        // Condition methods
        public bool CheckIsAlive() => IsAlive && currentHealth > 0;
        public bool CheckPlayerInSight() => IsPlayerInSight;
        public bool CheckLowHealth() => currentHealth <= lowHealthThreshold;
        public bool CheckPlayerInAttackRange() => IsPlayerInSight && DistanceToPlayer <= attackRange;
        public bool CheckArrivedAtPoint() => HasReachedPatrolPoint;
    }
}