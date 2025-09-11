using UnityEngine;
using AI.Interfaces;
using AI.Core;

namespace AI.Integration
{
    /// <summary>
    /// Adapter to integrate AI system with existing ICharacter interface
    /// </summary>
    public class AICharacterAdapter : MonoBehaviour, ICharacter
    {
        [Header("Character Configuration")]
        [SerializeField] private float health = 100f;
        [SerializeField] private float moveSpeed = 5f;

        private MobileOptimizedAI aiController;
        private bool isAlive = true;

        // ICharacter implementation
        public GameObject GameObject => gameObject;
        public Transform Transform => transform;
        public bool IsAlive => isAlive;

        private void Awake()
        {
            aiController = GetComponent<MobileOptimizedAI>();
            if (aiController == null)
            {
                aiController = gameObject.AddComponent<MobileOptimizedAI>();
            }
        }

        public void Initialize()
        {
            // Initialize AI blackboard with character data
            if (aiController != null && aiController.Blackboard != null)
            {
                aiController.Blackboard.CurrentHealth = health;
                aiController.Blackboard.IsAlive = true;
            }
            isAlive = true;
        }

        public void Move(Vector3 direction)
        {
            if (!isAlive || aiController?.Movement == null) return;
            
            Vector3 targetPosition = transform.position + direction.normalized * moveSpeed;
            aiController.Movement.MoveToTarget(targetPosition);
        }

        public void Shoot(Vector3 direction)
        {
            if (!isAlive) return;
            
            // Delegate to AI attack behavior
            aiController?.PerformAttack();
        }

        public void TakeDamage(float damage)
        {
            if (!isAlive) return;

            health -= damage;
            aiController?.TakeDamage(damage);

            if (health <= 0)
            {
                isAlive = false;
                if (aiController?.Blackboard != null)
                {
                    aiController.Blackboard.IsAlive = false;
                    aiController.Blackboard.CurrentHealth = 0;
                }
            }
        }

        // Additional methods for AI integration
        public void SetPatrolPoints(Transform[] points)
        {
            if (aiController != null)
            {
                var mobileAI = aiController as MobileOptimizedAI;
                if (mobileAI != null)
                {
                    // Use the public method instead of reflection
                    mobileAI.SetPatrolPoints(points);
                }
            }
        }

        public void SetAIPersonality(string personality)
        {
            if (aiController == null) return;

            // Use the public method instead of reflection
            var mobileAI = aiController as MobileOptimizedAI;
            if (mobileAI != null)
            {
                mobileAI.SetAIPersonality(personality);
            }
        }

        // Debugging
        private void OnDrawGizmosSelected()
        {
            if (aiController?.Blackboard != null)
            {
                Gizmos.color = isAlive ? Color.green : Color.red;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
                
                // Show health bar
                Vector3 healthBarPos = transform.position + Vector3.up * 2f;
                float healthPercentage = health / 100f;
                Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercentage);
                Gizmos.DrawCube(healthBarPos, new Vector3(healthPercentage * 2f, 0.1f, 0.1f));
            }
        }
    }
}