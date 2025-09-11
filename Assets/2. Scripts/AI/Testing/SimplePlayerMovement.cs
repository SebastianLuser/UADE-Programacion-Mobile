using UnityEngine;

namespace AI.Testing
{
    public class SimplePlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float rotationSpeed = 180f;

        private void Update()
        {
            HandleMovement();
        }

        private void HandleMovement()
        {
            // Get input
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Calculate movement
            Vector3 movement = new Vector3(horizontal, 0, vertical).normalized;

            if (movement.magnitude > 0.1f)
            {
                // Move
                transform.position += movement * moveSpeed * Time.deltaTime;

                // Rotate to face movement direction
                Quaternion targetRotation = Quaternion.LookRotation(movement);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private void OnDrawGizmos()
        {
            // Draw player direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 2f);
        }
    }
}