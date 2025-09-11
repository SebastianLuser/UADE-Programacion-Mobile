using UnityEngine;
using AI.Interfaces;
using AI.DecisionTree;
using AI.Controllers;

namespace AI.Core
{
    public class MobileOptimizedAI : MonoBehaviour, IAIContext
    {
        [Header("AI Configuration")]
        [SerializeField] private AIBlackboard blackboard = new AIBlackboard();
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolRange = 10f;
        [SerializeField] private float updateInterval = 0.1f; // Mobile optimization
        
        [Header("Patrol Configuration")]
        [SerializeField] private int currentPatrolIndex = 0;
        [SerializeField] private bool useRandomPatrol = false;

        // Dependencies
        private IDecisionNode decisionTree;
        private AIMovementController movementController;
        private IAIAnimationController animationController;
        private IPlayerDetector playerDetector;
        
        // Cached components
        private Animator animator;
        private float lastUpdateTime;
        private float idleTimer;
        private float patrolWaitTimer;
        private AIState currentState = AIState.Idle;

        // Interface implementations
        public IAIBlackboard Blackboard => blackboard;
        public IAIMovementController Movement => movementController;
        public IAIAnimationController Animation => animationController;
        public IPlayerDetector PlayerDetector => playerDetector;
        public Transform Transform => transform;

        private void Awake()
        {
            InitializeComponents();
            InitializeAI();
        }

        private void InitializeComponents()
        {
            animator = GetComponent<Animator>();
            
            // Initialize controllers
            movementController = new AIMovementController(transform, moveSpeed);
            
            // Use NullAnimationController if no Animator is present
            if (animator != null)
            {
                animationController = new AIAnimationController(animator);
            }
            else
            {
                animationController = new NullAnimationController();
            }
            
            playerDetector = new PlayerDetector();
        }

        private void InitializeAI()
        {
            // Build decision tree using the builder
            decisionTree = DecisionTreeBuilder.BuildStandardTree();
            
            // Force refresh patrol points if they were set via reflection
            RefreshPatrolConfiguration();
        }

        private void RefreshPatrolConfiguration()
        {
            // Initialize with random patrol starting point para evitar clustering
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                currentPatrolIndex = UnityEngine.Random.Range(0, patrolPoints.Length);
                // Set initial patrol target immediately
                SetRandomPatrolTarget();
                Debug.Log($"AI {name}: Initialized with patrol index {currentPatrolIndex}, mode: {(useRandomPatrol ? "Random" : "Sequential")}");
            }
            // Note: No warning during initial setup - patrol points will be assigned externally
        }

        private void Update()
        {
            // Optimize updates for mobile
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;

            float deltaTime = updateInterval;
            
            UpdateBlackboard();
            UpdateAI(deltaTime);
            UpdateMovement(deltaTime);
        }

        private void UpdateBlackboard()
        {
            if (!playerDetector.HasPlayer) return;

            var playerPos = playerDetector.GetPlayerPosition();
            blackboard.DistanceToPlayer = playerDetector.GetDistanceToPlayer(transform.position);
            blackboard.IsPlayerInSight = playerDetector.IsPlayerVisible(transform.position, blackboard.SightRange);
            
            if (blackboard.IsPlayerInSight)
            {
                blackboard.LastKnownPlayerPosition = playerPos;
            }
            
            blackboard.IsAlive = blackboard.CurrentHealth > 0;
        }

        private void UpdateAI(float deltaTime)
        {
            // Use decision tree to determine next state
            AIState nextState = decisionTree.Execute(blackboard);
            
            if (currentState != nextState)
            {
                OnStateChanged(currentState, nextState);
                currentState = nextState;
            }
            
            ExecuteCurrentState(deltaTime);
        }

        private void OnStateChanged(AIState oldState, AIState newState)
        {
            // Handle state transitions
            switch (newState)
            {
                case AIState.Idle:
                    movementController.StopMovement();
                    animationController.SetBool("IsMoving", false);
                    break;
                case AIState.Patrol:
                    animationController.SetBool("IsMoving", true);
                    patrolWaitTimer = 0f; // Reset patrol wait timer
                    if (blackboard.PatrolTarget == Vector3.zero)
                        SetRandomPatrolTarget();
                    break;
                case AIState.Pursuit:
                    animationController.SetBool("IsMoving", true);
                    animationController.SetBool("IsChasing", true);
                    break;
                case AIState.Attack:
                    movementController.StopMovement();
                    animationController.SetBool("IsMoving", false);
                    break;
                case AIState.Flee:
                    animationController.SetBool("IsMoving", true);
                    animationController.SetBool("IsFleeing", true);
                    break;
                case AIState.Die:
                    movementController.StopMovement();
                    animationController.SetTrigger("Die");
                    break;
            }
        }

        private void ExecuteCurrentState(float deltaTime)
        {
            switch (currentState)
            {
                case AIState.Idle:
                    idleTimer += deltaTime;
                    // Random idle time para evitar sincronización
                    float idleTimeThreshold = UnityEngine.Random.Range(2f, 4f);
                    if (idleTimer >= idleTimeThreshold)
                    {
                        SetRandomPatrolTarget();
                        idleTimer = 0f;
                        // Reset el flag para asegurar que este AI específico puede continuar
                        blackboard.HasReachedPatrolPoint = false;
                    }
                    break;
                    
                case AIState.Patrol:
                    if (blackboard.PatrolTarget != Vector3.zero)
                    {
                        float distanceToTarget = Vector3.Distance(transform.position, blackboard.PatrolTarget);
                        
                        if (distanceToTarget > 1.5f)
                        {
                            movementController.MoveToTarget(blackboard.PatrolTarget);
                            patrolWaitTimer = 0f; // Reset wait timer while moving
                        }
                        else
                        {
                            // Reached patrol point - wait a bit before getting next target
                            movementController.StopMovement();
                            blackboard.HasReachedPatrolPoint = true;
                            
                            patrolWaitTimer += deltaTime;
                            if (patrolWaitTimer >= UnityEngine.Random.Range(1f, 2f)) // Random wait time
                            {
                                blackboard.PatrolTarget = Vector3.zero; // Clear current target
                                SetRandomPatrolTarget();
                                patrolWaitTimer = 0f;
                            }
                        }
                    }
                    else
                    {
                        // No target set, get a new one immediately
                        SetRandomPatrolTarget();
                        blackboard.HasReachedPatrolPoint = false; // Ensure flag is reset
                    }
                    break;
                    
                case AIState.Pursuit:
                    Vector3 targetPosition = blackboard.IsPlayerInSight 
                        ? playerDetector.GetPlayerPosition() 
                        : blackboard.LastKnownPlayerPosition;
                    movementController.MoveToTarget(targetPosition);
                    break;
                    
                case AIState.Attack:
                    if (playerDetector.HasPlayer)
                    {
                        Vector3 direction = (playerDetector.GetPlayerPosition() - transform.position).normalized;
                        if (direction != Vector3.zero)
                        {
                            Quaternion lookRotation = Quaternion.LookRotation(direction);
                            transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, 5f * deltaTime);
                        }
                    }
                    break;
                    
                case AIState.Flee:
                    if (playerDetector.HasPlayer)
                    {
                        Vector3 fleeDirection = (transform.position - playerDetector.GetPlayerPosition()).normalized;
                        Vector3 fleeTarget = transform.position + fleeDirection * 10f;
                        movementController.MoveToTarget(fleeTarget);
                    }
                    break;
            }
        }

        private void UpdateMovement(float deltaTime)
        {
            movementController.UpdateMovement(deltaTime);
        }

        #region IAIContext Implementation
        public void TakeDamage(float damage)
        {
            blackboard.CurrentHealth -= damage;
        }

        public void PerformAttack()
        {
            animationController.SetTrigger("Attack");
            Debug.Log("AI performing attack!");
        }

        public void SetRandomPatrolTarget()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                if (useRandomPatrol)
                {
                    // Random patrol - puede repetir puntos
                    int randomIndex = UnityEngine.Random.Range(0, patrolPoints.Length);
                    blackboard.PatrolTarget = patrolPoints[randomIndex].position;
                }
                else
                {
                    // Sequential patrol - cada AI tiene su propio índice
                    currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                    blackboard.PatrolTarget = patrolPoints[currentPatrolIndex].position;
                    
                    // Añadir pequeña variación para evitar que múltiples AIs se superpongan
                    Vector3 offset = new Vector3(
                        UnityEngine.Random.Range(-1f, 1f),
                        0,
                        UnityEngine.Random.Range(-1f, 1f)
                    );
                    blackboard.PatrolTarget += offset;
                }
            }
            else
            {
                // Fallback: Random patrol en área if no patrol points available
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * patrolRange;
                blackboard.PatrolTarget = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
                
                // Only log this as info, not warning, since it's expected during initialization
                if (Application.isPlaying) // Only log during runtime, not during setup
                {
                    Debug.Log($"AI {name}: Using fallback random patrol area (no patrol points assigned yet)");
                }
            }
            
            blackboard.HasReachedPatrolPoint = false;
        }
        #endregion

        // Public methods for external configuration
        public void SetPatrolPoints(Transform[] points, bool randomMode = false)
        {
            patrolPoints = points;
            useRandomPatrol = randomMode;
            RefreshPatrolConfiguration();
            
            // Validate patrol points assignment
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                Debug.LogWarning($"AI {name}: SetPatrolPoints called but no valid patrol points provided!");
            }
            else
            {
                Debug.Log($"AI {name}: Successfully assigned {patrolPoints.Length} patrol points");
            }
        }

        public void ForceRefreshPatrol()
        {
            RefreshPatrolConfiguration();
        }

        public bool HasValidPatrolPoints()
        {
            return patrolPoints != null && patrolPoints.Length > 0;
        }

        public void SetAIPersonality(string personality)
        {
            decisionTree = personality.ToLower() switch
            {
                "aggressive" => DecisionTreeBuilder.BuildAggressiveTree(),
                "defensive" => DecisionTreeBuilder.BuildDefensiveTree(),
                _ => DecisionTreeBuilder.BuildStandardTree()
            };
            
            Debug.Log($"AI {name}: Personality set to {personality}");
        }

        // Debug visualization
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, blackboard.SightRange);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, blackboard.AttackRange);
            
            if (blackboard.PatrolTarget != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(blackboard.PatrolTarget, 0.5f);
                Gizmos.DrawLine(transform.position, blackboard.PatrolTarget);
            }
        }
    }
}