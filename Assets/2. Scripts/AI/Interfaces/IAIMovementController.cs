using UnityEngine;

namespace AI.Interfaces
{
    public interface IAIMovementController
    {
        void MoveToTarget(Vector3 target);
        void StopMovement();
        bool IsMoving { get; }
        Vector3 Position { get; }
        void SetRotation(Quaternion rotation);
        void UpdateMovement(float deltaTime);
    }
}