# 👥 FASE 6: CIVILIAN AI COMPLETO (Día 8)

## 🎯 **OBJETIVO DE LA FASE**
Implementar completamente el **Civilian AI** como segundo grupo de enemigos del TP, integrando **Decision Trees**, **Steering Behaviors**, y **Blackboard** para crear NPCs que reaccionen de forma inteligente y realista a la presencia del player.

---

## 📋 **¿QUÉ BUSCAMOS LOGRAR?**

### **Problema Actual:**
- Solo tienes Guards - necesitas segundo grupo de enemigos
- Decision Trees no están implementados en NPCs reales
- Falta variedad de comportamientos de NPCs
- No hay reacciones diferenciadas según contexto

### **Solución con Civilian AI:**
- **Civilians** como segundo grupo de enemigos que **NO atacan directamente**
- **Comportamientos diferenciados**: Flee, Alert Guards, Seek Exits
- **Decision Trees en acción** con NPCs reales
- **Integración completa** con todos los sistemas desarrollados

---

## 🧠 **CIVILIAN AI SYSTEM COMPLETO**

### **CivilianAI.cs - Implementación Final**
```csharp
public class CivilianAI : MonoBehaviour, IAIContext
{
    [Header("Civilian Configuration")]
    [SerializeField] private CivilianPersonality personality;
    [SerializeField] private CivilianType civilianType = CivilianType.Standard;
    [SerializeField] private float baseHealth = 50f;
    [SerializeField] private float maxPanicLevel = 100f;
    
    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float fieldOfView = 110f;
    [SerializeField] private float soundDetectionRange = 5f;
    [SerializeField] private LayerMask detectionLayers = -1;
    
    [Header("Movement Configuration")]
    [SerializeField] private float normalWalkSpeed = 2f;
    [SerializeField] private float panicRunSpeed = 6f;
    [SerializeField] private float investigateSpeed = 1.5f;
    [SerializeField] private float fleeSpeed = 7f;
    
    [Header("Decision Tree")]
    [SerializeField] private DecisionNode rootDecisionNode;
    [SerializeField] private float decisionInterval = 0.8f;
    [SerializeField] private int maxDecisionDepth = 12;
    [SerializeField] private bool allowDecisionOverrides = true;
    
    [Header("Behavior Settings")]
    [SerializeField] private float alertGuardsProbability = 0.6f;
    [SerializeField] private float panicThreshold = 60f;
    [SerializeField] private float calmDownRate = 10f; // panic points per second
    [SerializeField] private float maxAlertingTime = 5f;
    
    [Header("Debug and Visualization")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDecisionPath = false;
    [SerializeField] private bool visualizeStates = true;
    [SerializeField] private GameObject panicEffectPrefab;
    
    // Core Systems
    private IBlackboard blackboard;
    private IPlayerDetector playerDetector;
    private IAIMovementController movementController;
    private CivilianStateManager stateManager;
    private CivilianMemorySystem memorySystem;
    
    // Current State
    private CivilianState currentState = CivilianState.Normal;
    private float currentPanicLevel = 0f;
    private float currentHealth;
    private bool isAlive = true;
    
    // Player Detection
    private Transform playerTransform;
    private Vector3 lastKnownPlayerPosition;
    private float lastPlayerSightTime = -1f;
    private PlayerDetectionLevel detectionLevel = PlayerDetectionLevel.None;
    
    // Decision Making
    private float lastDecisionTime;
    private List<string> currentDecisionPath = new List<string>();
    private Dictionary<string, int> nodeExecutionStats = new Dictionary<string, int>();
    private int totalDecisionsMade = 0;
    
    // Behavior Tracking
    private float stateStartTime;
    private Vector3 targetPosition;
    private Transform currentTarget;
    private List<Vector3> suspiciousPositions = new List<Vector3>();
    private float lastAlertTime = -1f;
    private bool hasAlertedThisDetection = false;
    
    // Coordination
    private List<CivilianAI> nearbyCivilians = new List<CivilianAI>();
    private float lastCoordinationUpdate;
    private bool isBeingHelped = false;
    
    // Visual Effects
    private Renderer civilianRenderer;
    private ParticleSystem panicEffect;
    private Animator civilianAnimator;
    
    #region Initialization
    
    void Start()
    {
        InitializeCivilianSystems();
        SetupPersonality();
        InitializeVisualElements();
        RegisterWithBlackboard();
    }
    
    private void InitializeCivilianSystems()
    {
        // Core services
        blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null)
        {
            Logger.LogError($"CivilianAI {name}: Blackboard service not found!");
            enabled = false;
            return;
        }
        
        // Player reference
        playerTransform = blackboard.GetValue<Transform>(BlackboardKeys.PLAYER_TRANSFORM);
        if (playerTransform == null)
        {
            Logger.LogError($"CivilianAI {name}: Player transform not found in blackboard!");
        }
        
        // Detection system
        playerDetector = GetComponent<IPlayerDetector>();
        if (playerDetector == null)
        {
            var detector = gameObject.AddComponent<PlayerDetector>();
            detector.Initialize(detectionRange, fieldOfView, detectionLayers);
            playerDetector = detector;
        }
        
        // Movement system
        movementController = GetComponent<IAIMovementController>();
        if (movementController == null)
        {
            var controller = gameObject.AddComponent<AIMovementController>();
            movementController = controller;
        }
        
        // Specialized systems
        stateManager = new CivilianStateManager(this);
        memorySystem = new CivilianMemorySystem();
        
        // Initialize health
        currentHealth = baseHealth;
        
        LogDebug("Civilian systems initialized successfully");
    }
    
    private void SetupPersonality()
    {
        if (personality == null)
        {
            Logger.LogWarning($"CivilianAI {name}: No personality assigned! Creating default.");
            personality = ScriptableObject.CreateInstance<StandardCivilianPersonality>();
        }
        
        // Apply personality modifiers to base stats
        var modifiers = personality.GetBehaviorModifiers();
        
        alertGuardsProbability *= modifiers.alertnessMultiplier;
        panicThreshold *= modifiers.panicResistance;
        detectionRange *= modifiers.awarenessMultiplier;
        normalWalkSpeed *= modifiers.speedMultiplier;
        panicRunSpeed *= modifiers.speedMultiplier;
        
        LogDebug($"Personality applied: {personality.GetPersonalityName()}");
    }
    
    private void InitializeVisualElements()
    {
        civilianRenderer = GetComponent<Renderer>();
        civilianAnimator = GetComponent<Animator>();
        
        // Create panic effect if prefab is assigned
        if (panicEffectPrefab != null)
        {
            var effectGO = Instantiate(panicEffectPrefab, transform);
            panicEffect = effectGO.GetComponent<ParticleSystem>();
            if (panicEffect != null)
            {
                panicEffect.Stop();
            }
        }
    }
    
    private void RegisterWithBlackboard()
    {
        // Register this civilian in the global list
        var allCivilians = blackboard.GetValue<List<CivilianAI>>(BlackboardKeys.ALL_CIVILIANS) ?? 
                          new List<CivilianAI>();
        
        if (!allCivilians.Contains(this))
        {
            allCivilians.Add(this);
            blackboard.SetValue(BlackboardKeys.ALL_CIVILIANS, allCivilians);
        }
    }
    
    #endregion
    
    #region Main Update Loop
    
    void Update()
    {
        if (!isAlive) return;
        
        UpdateDetectionSystems();
        UpdatePanicLevel();
        UpdateDecisionMaking();
        UpdateCoordination();
        UpdateVisualState();
        
        // Periodic cleanup and optimization
        if (Time.frameCount % 120 == 0) // Every 2 seconds
        {
            PerformMaintenanceTasks();
        }
    }
    
    private void UpdateDetectionSystems()
    {
        UpdatePlayerDetection();
        UpdateEnvironmentalAwareness();
        UpdateMemorySystem();
    }
    
    private void UpdatePlayerDetection()
    {
        bool previousDetection = detectionLevel != PlayerDetectionLevel.None;
        
        // Primary visual detection
        bool canSeePlayer = playerDetector.CanSeePlayer(playerTransform);
        
        // Sound-based detection
        bool soundDetection = CheckSoundDetection();
        
        // Indirect detection (other civilians alerting)
        bool indirectDetection = CheckIndirectDetection();
        
        // Determine detection level
        if (canSeePlayer)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (distance <= 3f)
            {
                detectionLevel = PlayerDetectionLevel.Direct_Close;
            }
            else if (distance <= 6f)
            {
                detectionLevel = PlayerDetectionLevel.Direct_Medium;
            }
            else
            {
                detectionLevel = PlayerDetectionLevel.Direct_Far;
            }
        }
        else if (soundDetection)
        {
            detectionLevel = PlayerDetectionLevel.Sound;
        }
        else if (indirectDetection)
        {
            detectionLevel = PlayerDetectionLevel.Indirect;
        }
        else
        {
            detectionLevel = PlayerDetectionLevel.None;
        }
        
        // Update tracking variables
        if (detectionLevel != PlayerDetectionLevel.None)
        {
            lastKnownPlayerPosition = playerTransform.position;
            lastPlayerSightTime = Time.time;
            
            // Reset alerting flag if this is a new detection
            if (!previousDetection)
            {
                hasAlertedThisDetection = false;
                OnPlayerDetected();
            }
        }
        else if (previousDetection)
        {
            OnPlayerLost();
        }
    }
    
    private bool CheckSoundDetection()
    {
        // Check for gunshots
        var lastGunshot = blackboard.GetValue<Vector3>(BlackboardKeys.LAST_GUNSHOT_POSITION);
        var gunshotTime = blackboard.GetValue<float>(BlackboardKeys.LAST_GUNSHOT_TIME);
        
        if (Time.time - gunshotTime < 4f) // Recent gunshot
        {
            float distance = Vector3.Distance(transform.position, lastGunshot);
            if (distance <= soundDetectionRange)
            {
                return true;
            }
        }
        
        // Check for other loud events
        var alertLevel = blackboard.GetValue<int>(BlackboardKeys.ALERT_LEVEL);
        if (alertLevel > 1)
        {
            var alertPos = blackboard.GetValue<Vector3>(BlackboardKeys.ALERT_POSITION);
            float distance = Vector3.Distance(transform.position, alertPos);
            if (distance <= soundDetectionRange * 1.5f)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool CheckIndirectDetection()
    {
        // Check if other civilians are panicking nearby
        foreach (var civilian in nearbyCivilians)
        {
            if (civilian != this && civilian.GetCurrentState() == CivilianState.Panicking)
            {
                float distance = Vector3.Distance(transform.position, civilian.transform.position);
                if (distance <= 4f)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private void UpdateEnvironmentalAwareness()
    {
        // Check for suspicious activities (bodies, damaged objects, etc.)
        CheckForBodies();
        CheckForDamage();
        
        // Clean up old suspicious positions
        suspiciousPositions.RemoveAll(pos => 
            Vector3.Distance(transform.position, pos) > 20f ||
            Time.time - GetPositionAge(pos) > 30f);
    }
    
    private void CheckForBodies()
    {
        var bodies = blackboard.GetValue<List<Vector3>>(BlackboardKeys.DEAD_GUARD_POSITIONS) ?? 
                    new List<Vector3>();
        
        foreach (var bodyPos in bodies)
        {
            float distance = Vector3.Distance(transform.position, bodyPos);
            if (distance <= 4f && !suspiciousPositions.Contains(bodyPos))
            {
                suspiciousPositions.Add(bodyPos);
                IncreasePanic(20f);
                LogDebug($"Found body at {bodyPos}, panic increased");
            }
        }
    }
    
    private void CheckForDamage()
    {
        // This would be expanded based on your game's destructible objects
        // For now, check for broken windows, damaged walls, etc. through tags or components
    }
    
    private float GetPositionAge(Vector3 position)
    {
        // This is a simplified version - you'd want to track when each position was added
        return 0f;
    }
    
    #endregion
    
    #region Decision Making System
    
    private void UpdateDecisionMaking()
    {
        if (Time.time - lastDecisionTime >= decisionInterval)
        {
            MakeDecision();
            lastDecisionTime = Time.time;
        }
    }
    
    private void MakeDecision()
    {
        if (rootDecisionNode == null)
        {
            LogDebug("No root decision node - using default behavior");
            DefaultBehavior();
            return;
        }
        
        totalDecisionsMade++;
        currentDecisionPath.Clear();
        
        var currentNode = rootDecisionNode;
        int iterations = 0;
        
        try
        {
            while (currentNode != null && iterations < maxDecisionDepth)
            {
                string nodeName = currentNode.GetNodeName();
                currentDecisionPath.Add(nodeName);
                
                // Track execution statistics
                if (!nodeExecutionStats.ContainsKey(nodeName))
                    nodeExecutionStats[nodeName] = 0;
                nodeExecutionStats[nodeName]++;
                
                // Evaluate the node
                currentNode = currentNode.Evaluate(this);
                iterations++;
            }
        }
        catch (System.Exception e)
        {
            Logger.LogError($"CivilianAI {name}: Decision tree error: {e.Message}");
            DefaultBehavior();
        }
        
        if (iterations >= maxDecisionDepth)
        {
            Logger.LogWarning($"CivilianAI {name}: Decision tree exceeded max depth!");
        }
        
        if (showDecisionPath && currentDecisionPath.Count > 1)
        {
            LogDebug($"Decision path: {string.Join(" -> ", currentDecisionPath)}");
        }
    }
    
    private void DefaultBehavior()
    {
        // Fallback behavior when decision tree fails
        switch (detectionLevel)
        {
            case PlayerDetectionLevel.Direct_Close:
            case PlayerDetectionLevel.Direct_Medium:
                if (!hasAlertedThisDetection && ShouldAlertGuards())
                {
                    AlertNearbyGuards();
                    hasAlertedThisDetection = true;
                }
                FleeFromPlayer();
                break;
                
            case PlayerDetectionLevel.Direct_Far:
            case PlayerDetectionLevel.Sound:
            case PlayerDetectionLevel.Indirect:
                WalkToNearestExit();
                break;
                
            default:
                IdleBehavior();
                break;
        }
    }
    
    #endregion
    
    #region Behavior Actions
    
    public void FleeFromPlayer()
    {
        SetState(CivilianState.Fleeing);
        
        Vector3 fleeDirection = (transform.position - lastKnownPlayerPosition).normalized;
        
        // Add some randomness to avoid predictable movement
        fleeDirection += Random.insideUnitSphere * 0.3f;
        fleeDirection.y = 0;
        fleeDirection = fleeDirection.normalized;
        
        Vector3 fleeTarget = transform.position + fleeDirection * 15f;
        
        // Configure steering behaviors for fleeing
        movementController.ClearAllBehaviors();
        movementController.EnableBehavior(typeof(FleeBehavior), true);
        movementController.EnableBehavior(typeof(EvadeBehavior), true);
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        
        movementController.SetBehaviorWeight(typeof(FleeBehavior), 3f);
        movementController.SetBehaviorWeight(typeof(EvadeBehavior), 2f);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 2f);
        
        movementController.MoveTo(fleeTarget, fleeSpeed);
        
        IncreasePanic(15f);
        
        LogDebug($"Fleeing from player to {fleeTarget}");
    }
    
    public void AlertNearbyGuards()
    {
        SetState(CivilianState.Alerting);
        
        var guards = FindObjectsOfType<Guard>();
        int alertedCount = 0;
        float alertRadius = 12f;
        
        foreach (var guard in guards)
        {
            if (!guard.IsAlive) continue;
            
            float distance = Vector3.Distance(transform.position, guard.transform.position);
            if (distance <= alertRadius)
            {
                // Give the guard information about the player
                guard.SetTargetTransform(playerTransform);
                guard.LastKnownPlayerPosition = lastKnownPlayerPosition;
                
                alertedCount++;
                LogDebug($"Alerted guard: {guard.name} (distance: {distance:F1}m)");
            }
        }
        
        // Update global blackboard
        if (alertedCount > 0)
        {
            blackboard.SetValue(BlackboardKeys.ALERT_LEVEL, 2);
            blackboard.SetValue(BlackboardKeys.ALERT_POSITION, lastKnownPlayerPosition);
            blackboard.SetValue(BlackboardKeys.LAST_ALERT_SOURCE, transform);
            blackboard.SetValue(BlackboardKeys.ALERT_TIME, Time.time);
            
            lastAlertTime = Time.time;
            IncreasePanic(5f); // Small panic increase for alerting
            
            LogDebug($"Successfully alerted {alertedCount} guards");
        }
        
        // After alerting, start fleeing
        StartCoroutine(AlertThenFlee());
    }
    
    private IEnumerator AlertThenFlee()
    {
        yield return new WaitForSeconds(maxAlertingTime);
        
        if (currentState == CivilianState.Alerting)
        {
            FleeFromPlayer();
        }
    }
    
    public void WalkToNearestExit()
    {
        SetState(CivilianState.Escaping);
        
        Transform bestExit = FindBestExit();
        
        if (bestExit != null)
        {
            // Configure steering for calm exit
            movementController.ClearAllBehaviors();
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
            
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 2f);
            movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1.5f);
            
            // Add slight evasion if player is still nearby
            if (detectionLevel != PlayerDetectionLevel.None)
            {
                movementController.EnableBehavior(typeof(EvadeBehavior), true);
                movementController.SetBehaviorWeight(typeof(EvadeBehavior), 1f);
            }
            
            movementController.MoveTo(bestExit.position, normalWalkSpeed * 1.2f);
            targetPosition = bestExit.position;
            
            LogDebug($"Walking to exit: {bestExit.name}");
        }
        else
        {
            // No exit found - random movement away from danger
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection = randomDirection.normalized;
            
            Vector3 randomTarget = transform.position + randomDirection * 10f;
            movementController.MoveTo(randomTarget, normalWalkSpeed);
            
            LogDebug("No exit found - moving randomly");
        }
    }
    
    public void IdleBehavior()
    {
        SetState(CivilianState.Normal);
        
        // Reduce panic slowly
        if (currentPanicLevel > 0)
        {
            DecreasePanic(calmDownRate * Time.deltaTime);
        }
        
        // Occasional random movement
        if (Random.value < 0.01f) // 1% chance per frame
        {
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection = randomDirection.normalized;
            
            Vector3 idleTarget = transform.position + randomDirection * Random.Range(2f, 5f);
            
            movementController.ClearAllBehaviors();
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
            
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 1f);
            movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1f);
            
            movementController.MoveTo(idleTarget, normalWalkSpeed * 0.7f);
        }
    }
    
    public void InvestigateSuspiciousArea()
    {
        if (suspiciousPositions.Count == 0) return;
        
        SetState(CivilianState.Investigating);
        
        Vector3 investigateTarget = suspiciousPositions[0];
        
        movementController.ClearAllBehaviors();
        movementController.EnableBehavior(typeof(SeekBehavior), true);
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        
        movementController.SetBehaviorWeight(typeof(SeekBehavior), 1.5f);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1f);
        
        movementController.MoveTo(investigateTarget, investigateSpeed);
        
        // Remove the position when close enough
        if (Vector3.Distance(transform.position, investigateTarget) < 2f)
        {
            suspiciousPositions.RemoveAt(0);
            IncreasePanic(10f); // Increase panic when investigating
        }
        
        LogDebug($"Investigating suspicious area at {investigateTarget}");
    }
    
    #endregion
    
    #region Utility Methods
    
    private Transform FindBestExit()
    {
        GameObject[] exitPoints = GameObject.FindGameObjectsWithTag("ExitPoint");
        if (exitPoints.Length == 0) return null;
        
        Transform bestExit = null;
        float bestScore = float.MinValue;
        
        foreach (var exitGO in exitPoints)
        {
            float score = EvaluateExitPoint(exitGO.transform);
            if (score > bestScore)
            {
                bestScore = score;
                bestExit = exitGO.transform;
            }
        }
        
        return bestExit;
    }
    
    private float EvaluateExitPoint(Transform exit)
    {
        float score = 0f;
        
        // Distance factor (closer is better)
        float distance = Vector3.Distance(transform.position, exit.position);
        score += 100f / (1f + distance);
        
        // Player avoidance factor
        if (playerTransform != null)
        {
            float playerDistance = Vector3.Distance(playerTransform.position, exit.position);
            if (playerDistance < 5f)
            {
                score -= 50f; // Avoid exits near player
            }
            
            // Avoid exits that require passing near the player
            Vector3 toExit = (exit.position - transform.position).normalized;
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float pathAlignment = Vector3.Dot(toExit, toPlayer);
            
            if (pathAlignment > 0.7f)
            {
                score -= 30f; // Path toward exit goes near player
            }
        }
        
        // Safety factor - prefer exits away from danger
        Vector3 awayFromDanger = Vector3.zero;
        if (lastKnownPlayerPosition != Vector3.zero)
        {
            awayFromDanger = (transform.position - lastKnownPlayerPosition).normalized;
            Vector3 toExitDir = (exit.position - transform.position).normalized;
            float escapeAlignment = Vector3.Dot(awayFromDanger, toExitDir);
            
            if (escapeAlignment > 0f)
            {
                score += escapeAlignment * 25f;
            }
        }
        
        return score;
    }
    
    private bool ShouldAlertGuards()
    {
        // Base probability from configuration
        float probability = alertGuardsProbability;
        
        // Modify based on personality
        var modifiers = personality.GetBehaviorModifiers();
        probability *= modifiers.alertnessMultiplier;
        
        // Modify based on panic level
        if (currentPanicLevel > panicThreshold)
        {
            probability *= 0.6f; // Too panicked to think clearly
        }
        
        // Modify based on distance to player
        float distance = Vector3.Distance(transform.position, lastKnownPlayerPosition);
        if (distance < 3f)
        {
            probability *= 0.4f; // Too close - prioritize fleeing
        }
        else if (distance > 8f)
        {
            probability *= 1.3f; // Safe distance - more likely to help
        }
        
        // Check if guards are nearby
        var guards = FindObjectsOfType<Guard>();
        bool guardsNearby = false;
        foreach (var guard in guards)
        {
            if (guard.IsAlive && Vector3.Distance(transform.position, guard.transform.position) <= 15f)
            {
                guardsNearby = true;
                break;
            }
        }
        
        if (!guardsNearby)
        {
            probability *= 0.3f; // No point alerting if no guards nearby
        }
        
        probability = Mathf.Clamp01(probability);
        
        bool shouldAlert = Random.value <= probability;
        
        LogDebug($"Alert guards decision: {probability:P0} -> {shouldAlert}");
        
        return shouldAlert;
    }
    
    private void IncreasePanic(float amount)
    {
        currentPanicLevel = Mathf.Min(currentPanicLevel + amount, maxPanicLevel);
        
        if (currentPanicLevel > panicThreshold && currentState != CivilianState.Panicking)
        {
            SetState(CivilianState.Panicking);
        }
        
        // Update blackboard with panic areas
        var panicAreas = blackboard.GetValue<List<Vector3>>(BlackboardKeys.CIVILIAN_PANIC_AREAS) ?? 
                        new List<Vector3>();
        panicAreas.Add(transform.position);
        blackboard.SetValue(BlackboardKeys.CIVILIAN_PANIC_AREAS, panicAreas);
    }
    
    private void DecreasePanic(float amount)
    {
        currentPanicLevel = Mathf.Max(currentPanicLevel - amount, 0f);
        
        if (currentPanicLevel <= panicThreshold * 0.5f && currentState == CivilianState.Panicking)
        {
            SetState(CivilianState.Normal);
        }
    }
    
    #endregion
    
    #region State Management
    
    public void SetState(CivilianState newState)
    {
        if (currentState != newState)
        {
            var oldState = currentState;
            currentState = newState;
            stateStartTime = Time.time;
            
            OnStateChanged(oldState, newState);
            LogDebug($"State changed: {oldState} -> {newState}");
        }
    }
    
    private void OnStateChanged(CivilianState oldState, CivilianState newState)
    {
        // State exit logic
        switch (oldState)
        {
            case CivilianState.Alerting:
                StopAlertingEffects();
                break;
                
            case CivilianState.Panicking:
                StopPanicEffects();
                break;
        }
        
        // State entry logic
        switch (newState)
        {
            case CivilianState.Panicking:
                StartPanicEffects();
                break;
                
            case CivilianState.Alerting:
                StartAlertingEffects();
                break;
                
            case CivilianState.Fleeing:
                StartFleeingEffects();
                break;
        }
        
        UpdateAnimatorState();
    }
    
    private void StartPanicEffects()
    {
        if (panicEffect != null)
        {
            panicEffect.Play();
        }
    }
    
    private void StopPanicEffects()
    {
        if (panicEffect != null)
        {
            panicEffect.Stop();
        }
    }
    
    private void StartAlertingEffects()
    {
        // Could add visual/audio effects for alerting
    }
    
    private void StopAlertingEffects()
    {
        // Clean up alerting effects
    }
    
    private void StartFleeingEffects()
    {
        // Could add trailing effects, speed lines, etc.
    }
    
    private void UpdateAnimatorState()
    {
        if (civilianAnimator != null)
        {
            civilianAnimator.SetInteger("CivilianState", (int)currentState);
            civilianAnimator.SetFloat("PanicLevel", currentPanicLevel / maxPanicLevel);
            civilianAnimator.SetBool("CanSeePlayer", detectionLevel != PlayerDetectionLevel.None);
        }
    }
    
    #endregion
    
    #region Visual and Audio Updates
    
    private void UpdateVisualState()
    {
        if (!visualizeStates) return;
        
        UpdateRendererColor();
        UpdateScale();
    }
    
    private void UpdateRendererColor()
    {
        if (civilianRenderer == null || civilianRenderer.material == null) return;
        
        Color stateColor = GetStateColor();
        
        // Blend with panic level
        float panicIntensity = currentPanicLevel / maxPanicLevel;
        Color panicColor = Color.Lerp(stateColor, Color.red, panicIntensity * 0.5f);
        
        civilianRenderer.material.color = panicColor;
    }
    
    private Color GetStateColor()
    {
        switch (currentState)
        {
            case CivilianState.Normal: return Color.white;
            case CivilianState.Suspicious: return Color.yellow;
            case CivilianState.Investigating: return Color.cyan;
            case CivilianState.Alerting: return Color.blue;
            case CivilianState.Panicking: return Color.orange;
            case CivilianState.Fleeing: return Color.red;
            case CivilianState.Escaping: return Color.green;
            default: return Color.gray;
        }
    }
    
    private void UpdateScale()
    {
        // Subtle scale change based on panic level
        float scaleModifier = 1f + (currentPanicLevel / maxPanicLevel) * 0.1f;
        transform.localScale = Vector3.one * scaleModifier;
    }
    
    #endregion
    
    #region Coordination System
    
    private void UpdateCoordination()
    {
        if (Time.time - lastCoordinationUpdate >= 1f) // Update every second
        {
            UpdateNearbyCivilians();
            ProcessCivilianCoordination();
            lastCoordinationUpdate = Time.time;
        }
    }
    
    private void UpdateNearbyCivilians()
    {
        nearbyCivilians.Clear();
        var allCivilians = blackboard.GetValue<List<CivilianAI>>(BlackboardKeys.ALL_CIVILIANS);
        
        if (allCivilians != null)
        {
            foreach (var civilian in allCivilians)
            {
                if (civilian != this && civilian.isAlive)
                {
                    float distance = Vector3.Distance(transform.position, civilian.transform.position);
                    if (distance <= 8f) // Coordination range
                    {
                        nearbyCivilians.Add(civilian);
                    }
                }
            }
        }
    }
    
    private void ProcessCivilianCoordination()
    {
        // Share information with nearby civilians
        ShareInformation();
        
        // Check for help from others
        CheckForMutualSupport();
    }
    
    private void ShareInformation()
    {
        if (detectionLevel == PlayerDetectionLevel.None) return;
        
        foreach (var civilian in nearbyCivilians)
        {
            if (civilian.detectionLevel == PlayerDetectionLevel.None)
            {
                // Share player information
                civilian.ReceivePlayerInformation(lastKnownPlayerPosition, detectionLevel);
            }
        }
    }
    
    public void ReceivePlayerInformation(Vector3 playerPos, PlayerDetectionLevel sourceLevel)
    {
        if (detectionLevel == PlayerDetectionLevel.None || sourceLevel > detectionLevel)
        {
            lastKnownPlayerPosition = playerPos;
            lastPlayerSightTime = Time.time;
            
            // Set to indirect detection if we didn't have any
            if (detectionLevel == PlayerDetectionLevel.None)
            {
                detectionLevel = PlayerDetectionLevel.Indirect;
                IncreasePanic(5f);
                LogDebug("Received player information from nearby civilian");
            }
        }
    }
    
    private void CheckForMutualSupport()
    {
        // Help panicking civilians calm down
        if (currentState == CivilianState.Normal && currentPanicLevel < 20f)
        {
            foreach (var civilian in nearbyCivilians)
            {
                if (civilian.currentState == CivilianState.Panicking)
                {
                    civilian.ReceiveSupport(this);
                }
            }
        }
    }
    
    public void ReceiveSupport(CivilianAI supportingCivilian)
    {
        if (!isBeingHelped)
        {
            isBeingHelped = true;
            DecreasePanic(20f);
            
            StartCoroutine(SupportCooldown());
            
            LogDebug($"Received support from {supportingCivilian.name}");
        }
    }
    
    private IEnumerator SupportCooldown()
    {
        yield return new WaitForSeconds(5f);
        isBeingHelped = false;
    }
    
    #endregion
    
    #region Maintenance and Cleanup
    
    private void PerformMaintenanceTasks()
    {
        // Clean up old memory entries
        memorySystem.CleanupOldEntries(30f);
        
        // Optimize suspicious positions list
        if (suspiciousPositions.Count > 10)
        {
            suspiciousPositions.RemoveRange(0, suspiciousPositions.Count - 10);
        }
        
        // Reset statistics periodically to avoid memory buildup
        if (totalDecisionsMade > 1000)
        {
            var topNodes = nodeExecutionStats.OrderByDescending(kvp => kvp.Value)
                          .Take(20)
                          .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            nodeExecutionStats = topNodes;
            totalDecisionsMade = 500; // Reset but keep some history
        }
    }
    
    private void OnPlayerDetected()
    {
        LogDebug($"Player detected with level: {detectionLevel}");
        IncreasePanic(10f);
        
        // Record in memory system
        memorySystem.RecordEvent(CivilianMemoryEvent.PlayerSighted, lastKnownPlayerPosition, Time.time);
    }
    
    private void OnPlayerLost()
    {
        LogDebug("Player lost from detection");
        
        // Add last known position as suspicious
        if (!suspiciousPositions.Contains(lastKnownPlayerPosition))
        {
            suspiciousPositions.Add(lastKnownPlayerPosition);
        }
    }
    
    #endregion
    
    #region Public Interface (IAIContext)
    
    public Transform GetTransform() => transform;
    public IBlackboard GetBlackboard() => blackboard;
    public bool IsPlayerVisible() => detectionLevel >= PlayerDetectionLevel.Direct_Far;
    public Vector3 GetPlayerPosition() => lastKnownPlayerPosition;
    public float GetDistanceToPlayer() => Vector3.Distance(transform.position, lastKnownPlayerPosition);
    public float GetDetectionRange() => detectionRange;
    public AIPersonalityType GetPersonalityType() => personality?.GetPersonalityType() ?? AIPersonalityType.Civilian;
    
    // Civilian-specific getters
    public CivilianState GetCurrentState() => currentState;
    public float GetPanicLevel() => currentPanicLevel;
    public PlayerDetectionLevel GetDetectionLevel() => detectionLevel;
    public bool IsAlive() => isAlive;
    public bool HasAlertedThisDetection() => hasAlertedThisDetection;
    public float GetTimeSincePlayerSight() => lastPlayerSightTime >= 0 ? Time.time - lastPlayerSightTime : float.MaxValue;
    public int GetSuspiciousPositionsCount() => suspiciousPositions.Count;
    
    #endregion
    
    #region Debug and Statistics
    
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Logger.LogDebug($"CivilianAI {name}: {message}");
        }
    }
    
    void OnDrawGizmos()
    {
        if (!enableDebugLogs) return;
        
        // Current state indicator
        Gizmos.color = GetStateColor();
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.4f);
        
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Panic level indicator
        Gizmos.color = Color.Lerp(Color.green, Color.red, currentPanicLevel / maxPanicLevel);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 0.3f);
        
        // Last known player position
        if (lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(lastKnownPlayerPosition + Vector3.up, Vector3.one);
            Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);
        }
        
        // Suspicious positions
        Gizmos.color = Color.orange;
        foreach (var suspiciousPos in suspiciousPositions)
        {
            Gizmos.DrawWireCube(suspiciousPos, Vector3.one * 0.8f);
        }
        
        // Coordination connections
        Gizmos.color = Color.cyan;
        foreach (var civilian in nearbyCivilians)
        {
            if (civilian != null)
            {
                Gizmos.DrawLine(transform.position + Vector3.up, civilian.transform.position + Vector3.up);
            }
        }
    }
    
    [ContextMenu("Print Civilian Stats")]
    public void PrintCivilianStats()
    {
        Debug.Log($"=== CivilianAI {name} Statistics ===");
        Debug.Log($"State: {currentState}");
        Debug.Log($"Panic Level: {currentPanicLevel:F1}/{maxPanicLevel:F1} ({currentPanicLevel/maxPanicLevel:P0})");
        Debug.Log($"Detection Level: {detectionLevel}");
        Debug.Log($"Time since player sight: {GetTimeSincePlayerSight():F1}s");
        Debug.Log($"Total decisions made: {totalDecisionsMade}");
        Debug.Log($"Suspicious positions: {suspiciousPositions.Count}");
        Debug.Log($"Nearby civilians: {nearbyCivilians.Count}");
        Debug.Log($"Last decision path: {string.Join(" -> ", currentDecisionPath)}");
        
        Debug.Log("Top executed nodes:");
        foreach (var kvp in nodeExecutionStats.OrderByDescending(x => x.Value).Take(5))
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value} times");
        }
    }
    
    [ContextMenu("Force Panic")]
    public void ForcePanic()
    {
        IncreasePanic(panicThreshold + 10f);
    }
    
    [ContextMenu("Reset Civilian")]
    public void ResetCivilian()
    {
        currentPanicLevel = 0f;
        SetState(CivilianState.Normal);
        detectionLevel = PlayerDetectionLevel.None;
        suspiciousPositions.Clear();
        hasAlertedThisDetection = false;
        movementController?.Stop();
    }
    
    #endregion
    
    void OnDestroy()
    {
        // Clean up from blackboard
        var allCivilians = blackboard?.GetValue<List<CivilianAI>>(BlackboardKeys.ALL_CIVILIANS);
        if (allCivilians != null && allCivilians.Contains(this))
        {
            allCivilians.Remove(this);
            blackboard.SetValue(BlackboardKeys.ALL_CIVILIANS, allCivilians);
        }
    }
}

// Supporting enums and classes
public enum CivilianType
{
    Standard,
    Cowardly,
    Brave,
    Elderly,
    Child
}

public enum PlayerDetectionLevel
{
    None = 0,
    Indirect = 1,
    Sound = 2,
    Direct_Far = 3,
    Direct_Medium = 4,
    Direct_Close = 5
}

public enum CivilianState
{
    Normal,
    Suspicious,
    Investigating,
    Alerting,
    Panicking,
    Fleeing,
    Escaping
}

// Memory system for civilians
public class CivilianMemorySystem
{
    private List<CivilianMemoryEntry> memories = new List<CivilianMemoryEntry>();
    
    public void RecordEvent(CivilianMemoryEvent eventType, Vector3 position, float time)
    {
        memories.Add(new CivilianMemoryEntry
        {
            eventType = eventType,
            position = position,
            timestamp = time
        });
    }
    
    public void CleanupOldEntries(float maxAge)
    {
        float cutoffTime = Time.time - maxAge;
        memories.RemoveAll(m => m.timestamp < cutoffTime);
    }
    
    public List<CivilianMemoryEntry> GetRecentEvents(CivilianMemoryEvent eventType, float timeWindow)
    {
        float cutoffTime = Time.time - timeWindow;
        return memories.Where(m => m.eventType == eventType && m.timestamp >= cutoffTime).ToList();
    }
}

[System.Serializable]
public class CivilianMemoryEntry
{
    public CivilianMemoryEvent eventType;
    public Vector3 position;
    public float timestamp;
}

public enum CivilianMemoryEvent
{
    PlayerSighted,
    GuardAlerted,
    SuspiciousSound,
    BodyFound,
    SafeAreaReached
}
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Civilian AI completo** como segundo grupo de enemigos
2. **✅ Decision Trees en acción** con NPCs reales funcionales
3. **✅ Comportamientos diferenciados**: Flee, Alert, Investigate, Escape
4. **✅ Sistema de pánico** dinámico y reactivo
5. **✅ Coordinación entre civilians** para realismo
6. **✅ Integración completa** con Blackboard y Steering
7. **✅ Estados visuales** claros para debugging

### **Testing:**
1. **Detection Response**: Civilians deben reaccionar correctamente al ver player
2. **Decision Variety**: Diferentes decisiones según contexto y personalidad
3. **Panic System**: Nivel de pánico debe afectar comportamiento
4. **Guard Coordination**: Civilians deben alertar guards efectivamente
5. **Escape Behavior**: Deben buscar exits inteligentemente

Esta fase completa el **segundo grupo de enemigos** requerido para el TP y demuestra **Decision Trees** en acción con comportamiento emergente e inteligente.