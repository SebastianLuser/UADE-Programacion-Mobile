using UnityEngine;
using AI.Interfaces;

namespace AI.Controllers
{
    public class AIMovementController : IAIMovementController
    {
        private readonly Transform transform;
        private readonly float moveSpeed;
        private Vector3 targetPosition;
        private bool isMoving;

        public AIMovementController(Transform transform, float moveSpeed)
        {
            this.transform = transform;
            this.moveSpeed = moveSpeed;
        }

        public bool IsMoving => isMoving;
        public Vector3 Position => transform.position;

        public void MoveToTarget(Vector3 target)
        {
            targetPosition = target;
            isMoving = true;
        }

        public void StopMovement()
        {
            isMoving = false;
        }

        public void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
        }

        public void UpdateMovement(float deltaTime)
        {
            if (!isMoving) return;

            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position += direction * moveSpeed * deltaTime;

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                isMoving = false;
            }
        }
    }
}