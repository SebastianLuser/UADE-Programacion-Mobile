using UnityEngine;

namespace AI.Interfaces
{
    public interface IAIBlackboard
    {
        // Health
        float CurrentHealth { get; set; }
        float MaxHealth { get; }
        float LowHealthThreshold { get; }
        
        // Player Detection
        bool IsPlayerInSight { get; set; }
        float DistanceToPlayer { get; set; }
        float SightRange { get; }
        float AttackRange { get; }
        Vector3 LastKnownPlayerPosition { get; set; }
        
        // AI Status
        bool IsAlive { get; set; }
        Vector3 PatrolTarget { get; set; }
        bool HasReachedPatrolPoint { get; set; }
        
        // Condition methods
        bool CheckIsAlive();
        bool CheckPlayerInSight();
        bool CheckLowHealth();
        bool CheckPlayerInAttackRange();
        bool CheckArrivedAtPoint();
    }
}