using UnityEngine;

namespace AI.Interfaces
{
    public interface IAIContext
    {
        IAIBlackboard Blackboard { get; }
        IAIMovementController Movement { get; }
        IAIAnimationController Animation { get; }
        IPlayerDetector PlayerDetector { get; }
        Transform Transform { get; }
        
        void TakeDamage(float damage);
        void PerformAttack();
        void SetRandomPatrolTarget();
    }
}