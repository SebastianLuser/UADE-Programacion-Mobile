using UnityEngine;
using DevelopmentUtilities;
using AI.Interfaces;
using AI.Core;
using AI.Steering;
using AI.Controllers;
using AI.DecisionTree;

namespace AI.NPCs
{
    public enum CivilianBehaviorType
    {
        Flee,
        Attack,
        Hide,
        Panic
    }

    [System.Serializable]
    public class CivilianBehaviorWeights
    {
        [Header("Behavior Weights (Probability)")]
        [Range(0f, 100f)] public float fleeWeight = 70f;    // 70% chance to flee
        [Range(0f, 100f)] public float attackWeight = 10f;  // 10% chance to attack
        [Range(0f, 100f)] public float hideWeight = 15f;    // 15% chance to hide
        [Range(0f, 100f)] public float panicWeight = 5f;    // 5% chance to panic
    }

    public class CivilianAI : MonoBehaviour, IAIContext
    {
        [Header("Civilian Configuration")]
        public CivilianBehaviorWeights behaviorWeights = new CivilianBehaviorWeights();
        public float detectionRange = 8f;
        public float panicDuration = 5f;
        
        [Header("Escape Configuration")]
        public Transform[] escapePoints;
        public float escapeReachDistance = 2f;

        [Header("Components")]
        public AIBlackboard blackboard = new AIBlackboard();

        // Dependencies
        private IDecisionNode decisionTree;
        private SteeringController steeringController;
        private RouletteWheel<CivilianBehaviorType> rouletteWheel;
        private IPlayerDetector playerDetector;
        
        // State
        private float panicTimer;
        private CivilianBehaviorType currentBehaviorChoice = CivilianBehaviorType.Flee;
        private AIState currentState = AIState.Patrol;
        private Transform targetEscapePoint;
        private bool isEscaping = false;
        private bool hasSeenPlayer = false; // To track if player was detected this cycle
        private float playerLostTimer = 0f; // Cooldown before detecting player again
        private const float PLAYER_LOST_COOLDOWN = 2f; // 2 seconds cooldown

        // Public properties for debugging
        public CivilianBehaviorType CurrentBehaviorChoice => currentBehaviorChoice;
        public AIState CurrentState => currentState;
        public bool IsEscaping => isEscaping;
        public Transform TargetEscapePoint => targetEscapePoint;

        // IAIContext Implementation
        public IAIBlackboard Blackboard => blackboard;
        public IAIMovementController Movement => null; // Using SteeringController instead
        public IAIAnimationController Animation => new NullAnimationController(); // No animations for civilians in test
        public IPlayerDetector PlayerDetector => playerDetector;
        public Transform Transform => transform;

        private void Awake()
        {
            InitializeComponents();
            SetupRouletteWheel();
            SetupSteeringBehaviours();
            InitializeDecisionTree();
        }

        private void InitializeComponents()
        {
            steeringController = GetComponent<SteeringController>() ?? gameObject.AddComponent<SteeringController>();
            playerDetector = new PlayerDetector();
            
            // Configure steering controller
            steeringController.maxSpeed = 3f; // Slower than player
            steeringController.maxForce = 6f;
            // NO asignamos target inicialmente - solo cuando detectemos al player
            steeringController.target = null;
        }

        private void SetupRouletteWheel()
        {
            rouletteWheel = new RouletteWheel<CivilianBehaviorType>();
            
            // Create dictionary with weights
            var behaviorDict = new System.Collections.Generic.Dictionary<CivilianBehaviorType, float>
            {
                { CivilianBehaviorType.Flee, behaviorWeights.fleeWeight },
                { CivilianBehaviorType.Attack, behaviorWeights.attackWeight },
                { CivilianBehaviorType.Hide, behaviorWeights.hideWeight },
                { CivilianBehaviorType.Panic, behaviorWeights.panicWeight }
            };

            rouletteWheel.SetCachedDictionary(behaviorDict);
        }

        private void SetupSteeringBehaviours()
        {
            // Add steering behaviours
            steeringController.AddBehaviour(new ObstacleAvoidanceBehaviour { Priority = 5f });
            steeringController.AddBehaviour(new FleeBehaviour { Priority = 3f, FleeRadius = detectionRange });
            steeringController.AddBehaviour(new PursuitBehaviour { Priority = 2f });
            steeringController.AddBehaviour(new WanderBehaviour { Priority = 1f });
        }

        private void InitializeDecisionTree()
        {
            decisionTree = BuildCivilianDecisionTree();
            // Inicializar en modo wander
            SetWanderMode();
        }

        private IDecisionNode BuildCivilianDecisionTree()
        {
            return new ConditionNode(
                bb => bb.CheckIsAlive(),
                // If alive
                new ConditionNode(
                    bb => bb.CheckPlayerInSight() && !hasSeenPlayer && playerLostTimer <= 0f,
                    // If player detected - Use Roulette Wheel!
                    new ComplexActionNode(AIState.Attack, // This will be overridden
                        onEnter: ctx => {
                            // Only make decision if we haven't seen player before
                            if (!hasSeenPlayer)
                            {
                                // Use Roulette Wheel to decide behavior
                                currentBehaviorChoice = rouletteWheel.RunWithCached();
                                Debug.Log($"Civilian {name} detected player! Roulette chose: {currentBehaviorChoice}");
                                
                                ConfigureBehaviorBasedOnChoice();
                                hasSeenPlayer = true;
                            }
                        },
                        onExecute: ctx => {
                            UpdateBehaviorExecution();
                        }
                    ),
                    // If no player - wander
                    new ActionNode(AIState.Patrol, ctx => {
                        // Reset the seen player flag when player is lost
                        if (hasSeenPlayer)
                        {
                            Debug.Log($"Civilian {name} lost sight of player, returning to wander");
                            hasSeenPlayer = false;
                            isEscaping = false;
                            targetEscapePoint = null;
                            playerLostTimer = PLAYER_LOST_COOLDOWN; // Set cooldown
                        }
                        SetWanderMode();
                    })
                ),
                // If dead
                new ActionNode(AIState.Die)
            );
        }

        private void ConfigureBehaviorBasedOnChoice()
        {
            // Get all behaviors
            var fleeBehavior = steeringController.GetBehaviour<FleeBehaviour>();
            var pursuitBehavior = steeringController.GetBehaviour<PursuitBehaviour>();
            var wanderBehavior = steeringController.GetBehaviour<WanderBehaviour>();
            var obstacleBehavior = steeringController.GetBehaviour<ObstacleAvoidanceBehaviour>();

            // Disable all behaviors first
            if (fleeBehavior != null) fleeBehavior.IsActive = false;
            if (pursuitBehavior != null) pursuitBehavior.IsActive = false;
            if (wanderBehavior != null) wanderBehavior.IsActive = false;

            // Enable obstacle avoidance always
            if (obstacleBehavior != null) obstacleBehavior.IsActive = true;

            // Configure based on roulette choice
            switch (currentBehaviorChoice)
            {
                case CivilianBehaviorType.Flee:
                    Debug.Log($"Civilian {name} choosing to FLEE to escape point!");
                    
                    // Find nearest escape point
                    targetEscapePoint = GetNearestEscapePoint();
                    
                    if (targetEscapePoint != null)
                    {
                        // Use pursuit behavior to go to escape point
                        if (pursuitBehavior != null)
                        {
                            pursuitBehavior.IsActive = true;
                            pursuitBehavior.Priority = 5f; // Highest priority
                            steeringController.target = targetEscapePoint;
                            Debug.Log($"Civilian {name}: Pursuit target set to {targetEscapePoint.name} at {targetEscapePoint.position}");
                        }
                        isEscaping = true;
                        Debug.Log($"Civilian {name} escaping to {targetEscapePoint.name}");
                    }
                    else
                    {
                        // Fallback to normal flee behavior if no escape points
                        if (fleeBehavior != null) 
                        {
                            fleeBehavior.IsActive = true;
                            fleeBehavior.Priority = 4f;
                            fleeBehavior.FleeRadius = detectionRange * 2f;
                        }
                        Debug.Log($"Civilian {name} fleeing randomly (no escape points)");
                    }
                    break;

                case CivilianBehaviorType.Attack:
                    Debug.Log($"Civilian {name} choosing to ATTACK!");
                    if (pursuitBehavior != null)
                    {
                        pursuitBehavior.IsActive = true;
                        pursuitBehavior.Priority = 4f;
                        // Set target to player for attack
                        if (playerDetector.HasPlayer)
                        {
                            var playerObj = GameObject.FindGameObjectWithTag("Player");
                            if (playerObj != null)
                            {
                                steeringController.target = playerObj.transform;
                                Debug.Log($"Civilian {name}: Attack target set to player at {playerObj.transform.position}");
                            }
                        }
                    }
                    isEscaping = false;
                    break;

                case CivilianBehaviorType.Hide:
                    Debug.Log($"Civilian {name} choosing to HIDE!");
                    if (fleeBehavior != null)
                    {
                        fleeBehavior.IsActive = true;
                        fleeBehavior.Priority = 2f;
                        fleeBehavior.FleeRadius = detectionRange * 1.5f;
                    }
                    isEscaping = false;
                    break;

                case CivilianBehaviorType.Panic:
                    Debug.Log($"Civilian {name} PANICKING!");
                    if (wanderBehavior != null)
                    {
                        wanderBehavior.IsActive = true;
                        wanderBehavior.Priority = 4f;
                        wanderBehavior.WanderJitter = 5f; // Very erratic movement
                    }
                    panicTimer = panicDuration;
                    isEscaping = false;
                    break;
            }
        }

        private void SetWanderMode()
        {
            Debug.Log($"Civilian {name} entering WANDER mode");
            
            // Get all behaviors  
            var fleeBehavior = steeringController.GetBehaviour<FleeBehaviour>();
            var pursuitBehavior = steeringController.GetBehaviour<PursuitBehaviour>();
            var wanderBehavior = steeringController.GetBehaviour<WanderBehaviour>();
            var obstacleBehavior = steeringController.GetBehaviour<ObstacleAvoidanceBehaviour>();

            // Disable reactive behaviors
            if (fleeBehavior != null) fleeBehavior.IsActive = false;
            if (pursuitBehavior != null) pursuitBehavior.IsActive = false;

            // Clear target for wander
            steeringController.target = null;

            // Enable wander
            if (wanderBehavior != null)
            {
                wanderBehavior.IsActive = true;
                wanderBehavior.Priority = 2f;
                wanderBehavior.WanderJitter = 1f; // Normal calm movement
                wanderBehavior.WanderRadius = 2f;
                wanderBehavior.WanderDistance = 3f;
            }

            // Always enable obstacle avoidance
            if (obstacleBehavior != null) 
            {
                obstacleBehavior.IsActive = true;
                obstacleBehavior.Priority = 5f;
            }
        }

        private void UpdateBehaviorExecution()
        {
            switch (currentBehaviorChoice)
            {
                case CivilianBehaviorType.Panic:
                    panicTimer -= Time.deltaTime;
                    if (panicTimer <= 0)
                    {
                        // Switch to flee after panic
                        currentBehaviorChoice = CivilianBehaviorType.Flee;
                        ConfigureBehaviorBasedOnChoice();
                        Debug.Log($"Civilian {name}: Panic ended, switching to flee");
                    }
                    break;

                case CivilianBehaviorType.Flee:
                    // Ensure we're escaping to the right target
                    if (isEscaping && targetEscapePoint != null)
                    {
                        // Make sure pursuit behavior is targeting the escape point
                        var pursuitBehavior = steeringController.GetBehaviour<PursuitBehaviour>();
                        if (pursuitBehavior != null && pursuitBehavior.IsActive)
                        {
                            steeringController.target = targetEscapePoint;
                        }
                    }
                    break;

                case CivilianBehaviorType.Attack:
                    // Ensure we're pursuing the player
                    if (playerDetector.HasPlayer)
                    {
                        var playerObj = GameObject.FindGameObjectWithTag("Player");
                        if (playerObj != null)
                        {
                            steeringController.target = playerObj.transform;
                        }
                    }
                    break;

                case CivilianBehaviorType.Hide:
                    // Hide behavior is handled by flee behavior configuration
                    break;
            }
        }

        private void Update()
        {
            UpdateBlackboard();
            
            // Update player lost timer
            if (playerLostTimer > 0f)
            {
                playerLostTimer -= Time.deltaTime;
            }
            
            UpdateDecisionTree();
            CheckEscapeProgress(); // Check if civilian reached escape point
            steeringController.UpdateSteering(Time.deltaTime);
        }

        private void UpdateBlackboard()
        {
            if (playerDetector.HasPlayer)
            {
                blackboard.DistanceToPlayer = playerDetector.GetDistanceToPlayer(transform.position);
                blackboard.IsPlayerInSight = blackboard.DistanceToPlayer <= detectionRange;
                
                if (blackboard.IsPlayerInSight)
                {
                    blackboard.LastKnownPlayerPosition = playerDetector.GetPlayerPosition();
                }
            }
            else
            {
                blackboard.IsPlayerInSight = false;
                blackboard.DistanceToPlayer = Mathf.Infinity;
            }
            
            blackboard.IsAlive = blackboard.CurrentHealth > 0;
        }

        private void UpdateDecisionTree()
        {
            AIState nextState = decisionTree.Execute(blackboard);
            
            if (currentState != nextState)
            {
                Debug.Log($"Civilian {name}: State change from {currentState} to {nextState}");
                currentState = nextState;
            }
        }

        // IAIContext methods
        public void TakeDamage(float damage)
        {
            blackboard.CurrentHealth -= damage;
        }

        public void PerformAttack()
        {
            Debug.Log("Civilian attacking! (This is rare!)");
        }

        public void SetRandomPatrolTarget()
        {
            // Not used for civilians - they wander instead
        }

        // Public configuration methods
        public void SetEscapePoints(Transform[] points)
        {
            escapePoints = points;
            Debug.Log($"Civilian {name}: Assigned {points?.Length ?? 0} escape points");
        }

        private Transform GetNearestEscapePoint()
        {
            if (escapePoints == null || escapePoints.Length == 0) return null;
            
            Transform nearest = null;
            float nearestDistance = Mathf.Infinity;
            
            foreach (var point in escapePoints)
            {
                if (point == null) continue;
                
                float distance = Vector3.Distance(transform.position, point.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = point;
                }
            }
            
            return nearest;
        }

        private void CheckEscapeProgress()
        {
            if (!isEscaping || targetEscapePoint == null) return;
            
            float distanceToEscape = Vector3.Distance(transform.position, targetEscapePoint.position);
            if (distanceToEscape <= escapeReachDistance)
            {
                Debug.Log($"Civilian {name} reached escape point! Despawning...");
                // Despawn civilian
                if (Application.isPlaying)
                    Destroy(gameObject);
            }
        }

        // Debug
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            Gizmos.color = Color.blue;
            if (blackboard.LastKnownPlayerPosition != Vector3.zero)
            {
                Gizmos.DrawLine(transform.position, blackboard.LastKnownPlayerPosition);
            }
            
            // Draw escape route if escaping
            if (isEscaping && targetEscapePoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, targetEscapePoint.position);
                Gizmos.DrawWireSphere(targetEscapePoint.position, escapeReachDistance);
            }
        }
    }
}