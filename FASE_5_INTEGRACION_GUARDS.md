# ⚔️ FASE 5: INTEGRACIÓN COMPLETA DE GUARDS (Día 7)

## 🎯 **OBJETIVO DE LA FASE**
Integrar completamente los **Guards** con todos los sistemas desarrollados (Blackboard, Line of Sight, Steering Behaviors), transformando la implementación básica actual en **AI avanzada** con personalidades diferenciadas.

---

## 📋 **¿QUÉ BUSCAMOS LOGRAR?**

### **Problema Actual:**
- Guards tienen implementación básica con FSM simple
- No usan Blackboard para coordinación
- Movimiento lineal sin steering behaviors
- Falta integración con Line of Sight avanzado
- Sin personalidades diferenciadas (Aggressive/Conservative)

### **Solución con Integración Completa:**
- **Guards totalmente integrados** con todos los sistemas
- **Personalidades reales** que afectan comportamiento
- **Coordinación** a través de Blackboard
- **Movimiento inteligente** con steering behaviors
- **Detección avanzada** con Line of Sight completo

---

## 🏗️ **ARQUITECTURA MEJORADA DE GUARDS**

### **Guard.cs - Versión Integrada Completa**
```csharp
public class Guard : BaseCharacter
{
    [Header("Guard AI Configuration")]
    [SerializeField] private GuardPersonality personality;
    [SerializeField] private GuardConfiguration configuration;
    [SerializeField] private bool enableAdvancedLogging = true;
    
    [Header("Advanced Detection")]
    [SerializeField] private float advancedDetectionRange = 12f;
    [SerializeField] private float peripheralVisionRange = 8f;
    [SerializeField] private float soundDetectionRange = 6f;
    [SerializeField] private LayerMask detectionLayers = -1;
    
    [Header("Combat Behavior")]
    [SerializeField] private float optimalCombatDistance = 5f;
    [SerializeField] private float maxCombatDistance = 15f;
    [SerializeField] private float repositionCooldown = 3f;
    [SerializeField] private int burstFireCount = 3;
    [SerializeField] private float burstDelay = 0.3f;
    
    [Header("Coordination")]
    [SerializeField] private bool enableCoordination = true;
    [SerializeField] private float coordinationRange = 20f;
    [SerializeField] private float callForHelpThreshold = 0.3f; // 30% health
    
    // Componentes integrados
    private IBlackboard blackboard;
    private IPlayerDetector advancedPlayerDetector;
    private IAIMovementController movementController;
    private GuardStateManager stateManager;
    private GuardCombatSystem combatSystem;
    private GuardCoordination coordination;
    
    // Estado interno avanzado
    private GuardTacticalState tacticalState;
    private Vector3 lastKnownPlayerPosition;
    private float lastPlayerSightTime;
    private float lastRepositionTime;
    private int consecutiveAttackMisses;
    private List<Vector3> suspiciousPositions = new List<Vector3>();
    
    // Personalidad y comportamiento
    private GuardBehaviorModifiers behaviorModifiers;
    private RouletteWheel<GuardAction> actionSelector;
    private Dictionary<GuardState, float> stateTimers = new Dictionary<GuardState, float>();
    
    #region Initialization and Setup
    
    protected override void Start()
    {
        base.Start();
        InitializeAdvancedSystems();
        SetupPersonality();
        ConfigureStateMachine();
        InitializeCoordination();
    }
    
    private void InitializeAdvancedSystems()
    {
        // Obtener servicios del ServiceLocator
        blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null)
        {
            Logger.LogError($"Guard {name}: Blackboard service not found!");
            enabled = false;
            return;
        }
        
        // Configurar PlayerDetector avanzado
        advancedPlayerDetector = GetComponent<IPlayerDetector>();
        if (advancedPlayerDetector == null)
        {
            var detector = gameObject.AddComponent<PlayerDetector>();
            detector.Initialize(advancedDetectionRange, fieldOfView, detectionLayers);
            advancedPlayerDetector = detector;
        }
        
        // Configurar Movement Controller
        movementController = GetComponent<IAIMovementController>();
        if (movementController == null)
        {
            var controller = gameObject.AddComponent<AIMovementController>();
            movementController = controller;
        }
        
        // Inicializar sistemas especializados
        stateManager = new GuardStateManager(this);
        combatSystem = new GuardCombatSystem(this, configuration.combatSettings);
        coordination = new GuardCoordination(this, blackboard);
        
        LogDebug("Advanced systems initialized successfully");
    }
    
    private void SetupPersonality()
    {
        if (personality == null)
        {
            Logger.LogWarning($"Guard {name}: No personality assigned! Using default Conservative.");
            personality = ScriptableObject.CreateInstance<ConservativeGuardPersonality>();
        }
        
        // Aplicar modificadores de personalidad
        behaviorModifiers = personality.GetBehaviorModifiers();
        
        // Ajustar parámetros base según personalidad
        ApplyPersonalityToStats();
        
        // Configurar Roulette Wheel para toma de decisiones
        SetupActionSelector();
        
        LogDebug($"Personality set: {personality.GetType().Name}");
    }
    
    private void ApplyPersonalityToStats()
    {
        // Modificar velocidades
        moveSpeed *= behaviorModifiers.moveSpeedMultiplier;
        
        // Modificar rangos de detección
        detectionRange *= behaviorModifiers.detectionRangeMultiplier;
        advancedDetectionRange *= behaviorModifiers.detectionRangeMultiplier;
        
        // Modificar configuración de combate
        optimalCombatDistance *= behaviorModifiers.combatDistanceMultiplier;
        attackCooldown *= behaviorModifiers.attackCooldownMultiplier;
        
        // Configurar threshold para llamar ayuda
        callForHelpThreshold *= behaviorModifiers.helpCallThreshold;
        
        LogDebug($"Stats modified by personality: Speed={moveSpeed:F1}, Detection={detectionRange:F1}, Combat Distance={optimalCombatDistance:F1}");
    }
    
    private void SetupActionSelector()
    {
        var actions = new GuardAction[]
        {
            GuardAction.Attack,
            GuardAction.TakeCover,
            GuardAction.Reposition,
            GuardAction.CallForHelp,
            GuardAction.Investigate
        };
        
        var weights = new float[]
        {
            behaviorModifiers.aggressionLevel,
            behaviorModifiers.cautiousness,
            behaviorModifiers.aggressionLevel * 0.5f,
            (1f - behaviorModifiers.confidence) * 0.8f,
            behaviorModifiers.cautiousness * 0.6f
        };
        
        actionSelector = new RouletteWheel<GuardAction>(actions, weights);
    }
    
    #endregion
    
    #region Advanced AI Update Loop
    
    protected override void Update()
    {
        base.Update();
        
        if (!IsAlive) return;
        
        UpdateAdvancedSystems();
        UpdateTacticalState();
        ProcessCoordination();
        UpdatePersonalityBehavior();
        
        // Debug visualization
        if (enableAdvancedLogging && Time.frameCount % 60 == 0) // Every second
        {
            LogCurrentState();
        }
    }
    
    private void UpdateAdvancedSystems()
    {
        // Actualizar detección avanzada
        UpdateAdvancedDetection();
        
        // Actualizar sistema de combate
        combatSystem.Update();
        
        // Actualizar coordinación
        coordination.Update();
        
        // Actualizar steering behaviors
        UpdateSteeringBehaviors();
    }
    
    private void UpdateAdvancedDetection()
    {
        bool previouslyDetected = CanSeePlayer;
        
        // Detección visual principal
        bool visualDetection = advancedPlayerDetector.CanSeePlayer(TargetTransform);
        
        // Detección periférica (reducida)
        bool peripheralDetection = false;
        if (!visualDetection && Vector3.Distance(transform.position, TargetTransform.position) <= peripheralVisionRange)
        {
            peripheralDetection = advancedPlayerDetector.CanSeePlayer(TargetTransform, fieldOfView * 1.5f);
        }
        
        // Detección por sonido (basada en Blackboard)
        bool soundDetection = CheckSoundDetection();
        
        CanSeePlayer = visualDetection || peripheralDetection || soundDetection;
        
        if (CanSeePlayer)
        {
            lastKnownPlayerPosition = TargetTransform.position;
            lastPlayerSightTime = Time.time;
            
            // Notificar al Blackboard
            UpdateBlackboardPlayerPosition();
        }
        
        // Detectar cambio de estado de detección
        if (CanSeePlayer && !previouslyDetected)
        {
            OnPlayerDetected();
        }
        else if (!CanSeePlayer && previouslyDetected)
        {
            OnPlayerLost();
        }
    }
    
    private bool CheckSoundDetection()
    {
        var lastGunshot = blackboard.GetValue<Vector3>(BlackboardKeys.LAST_GUNSHOT_POSITION);
        var gunshotTime = blackboard.GetValue<float>(BlackboardKeys.LAST_GUNSHOT_TIME);
        
        // Sonido reciente y dentro del rango
        if (Time.time - gunshotTime < 3f) // Sonido válido por 3 segundos
        {
            float distance = Vector3.Distance(transform.position, lastGunshot);
            if (distance <= soundDetectionRange)
            {
                LogDebug($"Detected player by sound at distance {distance:F1}m");
                return true;
            }
        }
        
        return false;
    }
    
    private void UpdateTacticalState()
    {
        var previousState = tacticalState;
        
        // Evaluar estado táctico basado en múltiples factores
        tacticalState = EvaluateTacticalSituation();
        
        if (tacticalState != previousState)
        {
            OnTacticalStateChanged(previousState, tacticalState);
        }
        
        // Actualizar temporizadores de estado
        foreach (var key in stateTimers.Keys.ToList())
        {
            stateTimers[key] += Time.deltaTime;
        }
    }
    
    private GuardTacticalState EvaluateTacticalSituation()
    {
        if (!CanSeePlayer && Time.time - lastPlayerSightTime > 10f)
            return GuardTacticalState.Patrolling;
        
        float distanceToPlayer = Vector3.Distance(transform.position, lastKnownPlayerPosition);
        float healthPercentage = Health / MaxHealth;
        int nearbyAllies = GetNearbyAlliesCount();
        
        // Estado crítico - baja vida
        if (healthPercentage < callForHelpThreshold)
        {
            return nearbyAllies > 0 ? GuardTacticalState.Retreating : GuardTacticalState.Desperate;
        }
        
        // Estado agresivo - buena posición
        if (CanSeePlayer && distanceToPlayer <= optimalCombatDistance && healthPercentage > 0.7f)
        {
            return GuardTacticalState.Aggressive;
        }
        
        // Estado defensivo - player muy cerca o aliados caídos
        if (distanceToPlayer < 3f || (nearbyAllies == 0 && GetTotalAlliesCount() > 0))
        {
            return GuardTacticalState.Defensive;
        }
        
        // Estado de búsqueda
        if (!CanSeePlayer && Time.time - lastPlayerSightTime < 10f)
        {
            return GuardTacticalState.Searching;
        }
        
        // Estado de investigación
        if (suspiciousPositions.Count > 0)
        {
            return GuardTacticalState.Investigating;
        }
        
        return GuardTacticalState.Patrolling;
    }
    
    #endregion
    
    #region Steering Behaviors Integration
    
    private void UpdateSteeringBehaviors()
    {
        if (movementController == null) return;
        
        // Configurar behaviors según estado táctico y personalidad
        ConfigureSteeringForCurrentState();
        
        // Aplicar modificadores de personalidad
        ApplyPersonalityToSteering();
    }
    
    private void ConfigureSteeringForCurrentState()
    {
        // Limpiar configuración anterior
        movementController.ClearAllBehaviors();
        
        switch (tacticalState)
        {
            case GuardTacticalState.Aggressive:
                ConfigureAggressiveSteering();
                break;
                
            case GuardTacticalState.Defensive:
                ConfigureDefensiveSteering();
                break;
                
            case GuardTacticalState.Retreating:
                ConfigureRetreatingSteering();
                break;
                
            case GuardTacticalState.Searching:
                ConfigureSearchingSteering();
                break;
                
            case GuardTacticalState.Investigating:
                ConfigureInvestigatingSteering();
                break;
                
            case GuardTacticalState.Patrolling:
                ConfigurePatrollingSteering();
                break;
                
            case GuardTacticalState.Desperate:
                ConfigureDesperateSteering();
                break;
        }
    }
    
    private void ConfigureAggressiveSteering()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, lastKnownPlayerPosition);
        
        if (distanceToPlayer > optimalCombatDistance * 1.2f)
        {
            // Acercarse al player
            movementController.EnableBehavior(typeof(PursuitBehavior), true);
            movementController.SetBehaviorWeight(typeof(PursuitBehavior), 2f);
            
            movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
            movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1.5f);
        }
        else if (distanceToPlayer < optimalCombatDistance * 0.8f)
        {
            // Mantener distancia optimal
            movementController.EnableBehavior(typeof(EvadeBehavior), true);
            movementController.SetBehaviorWeight(typeof(EvadeBehavior), 1f);
        }
        else
        {
            // Posición optimal - movimiento mínimo
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 0.5f);
        }
        
        // Siempre evitar obstáculos
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 2f);
    }
    
    private void ConfigureDefensiveSteering()
    {
        // Buscar cobertura
        Vector3 coverPosition = FindNearestCover();
        if (coverPosition != Vector3.zero)
        {
            movementController.MoveTo(coverPosition, moveSpeed);
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 2f);
        }
        
        // Evitar al player
        movementController.EnableBehavior(typeof(EvadeBehavior), true);
        movementController.SetBehaviorWeight(typeof(EvadeBehavior), 1.5f);
        
        // Evitar obstáculos con alta prioridad
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 3f);
    }
    
    private void ConfigureRetreatingSteering()
    {
        Vector3 safePosition = FindSafeRetreatPosition();
        
        movementController.MoveTo(safePosition, moveSpeed * 1.3f);
        
        // Flee con alta prioridad
        movementController.EnableBehavior(typeof(FleeBehavior), true);
        movementController.SetBehaviorWeight(typeof(FleeBehavior), 3f);
        
        // Evitar obstáculos es crítico durante retreats
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 4f);
        
        LogDebug($"Retreating to position: {safePosition}");
    }
    
    private void ConfigureSearchingSteering()
    {
        if (suspiciousPositions.Count > 0)
        {
            Vector3 searchTarget = suspiciousPositions[0];
            movementController.MoveTo(searchTarget, moveSpeed * 0.7f);
            
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 1.5f);
        }
        else
        {
            // Patrullaje de búsqueda alrededor de la última posición conocida
            Vector3 searchArea = lastKnownPlayerPosition + Random.insideUnitSphere * 5f;
            searchArea.y = transform.position.y;
            
            movementController.MoveTo(searchArea, moveSpeed * 0.5f);
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 1f);
        }
        
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1.5f);
    }
    
    private void ConfigureInvestigatingSteering()
    {
        if (suspiciousPositions.Count > 0)
        {
            Vector3 investigateTarget = suspiciousPositions[0];
            movementController.MoveTo(investigateTarget, moveSpeed * 0.8f);
            
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 1.5f);
            
            // Cuando llegue cerca, eliminar este punto
            if (Vector3.Distance(transform.position, investigateTarget) < 2f)
            {
                suspiciousPositions.RemoveAt(0);
                LogDebug($"Investigated position, {suspiciousPositions.Count} remaining");
            }
        }
        
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1f);
    }
    
    private void ConfigurePatrollingSteering()
    {
        // Patrullaje normal
        movementController.EnableBehavior(typeof(SeekBehavior), true);
        movementController.SetBehaviorWeight(typeof(SeekBehavior), 1f);
        
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1f);
    }
    
    private void ConfigureDesperateSteering()
    {
        // Comportamiento errático cuando está desesperado
        if (Random.value < 0.3f) // 30% chance cada frame de cambiar dirección
        {
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection = randomDirection.normalized;
            
            Vector3 desperateTarget = transform.position + randomDirection * Random.Range(3f, 8f);
            movementController.MoveTo(desperateTarget, moveSpeed * 1.5f);
        }
        
        // Mezcla de flee y movimiento errático
        movementController.EnableBehavior(typeof(FleeBehavior), true);
        movementController.SetBehaviorWeight(typeof(FleeBehavior), 2f);
        
        movementController.EnableBehavior(typeof(EvadeBehavior), true);
        movementController.SetBehaviorWeight(typeof(EvadeBehavior), 1.5f);
        
        movementController.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
        movementController.SetBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1f);
    }
    
    private void ApplyPersonalityToSteering()
    {
        // Los guards agresivos se mueven más rápido hacia el combate
        if (personality.GetPersonalityType() == AIPersonalityType.Aggressive)
        {
            movementController.SetGlobalSpeedMultiplier(behaviorModifiers.moveSpeedMultiplier);
            
            // Aumentar peso de pursuit y seek
            movementController.ModifyBehaviorWeight(typeof(PursuitBehavior), 1.2f);
            movementController.ModifyBehaviorWeight(typeof(SeekBehavior), 1.1f);
        }
        
        // Los guards conservadores priorizan evasión y cobertura
        if (personality.GetPersonalityType() == AIPersonalityType.Conservative)
        {
            movementController.SetGlobalSpeedMultiplier(behaviorModifiers.moveSpeedMultiplier);
            
            // Aumentar peso de evasion y obstacle avoidance
            movementController.ModifyBehaviorWeight(typeof(EvadeBehavior), 1.3f);
            movementController.ModifyBehaviorWeight(typeof(FleeBehavior), 1.2f);
            movementController.ModifyBehaviorWeight(typeof(ObstacleAvoidanceBehavior), 1.4f);
        }
    }
    
    #endregion
    
    #region Advanced Combat System
    
    protected override bool CanAttackPlayer()
    {
        if (!base.CanAttackPlayer()) return false;
        
        // Verificaciones adicionales basadas en estado táctico
        switch (tacticalState)
        {
            case GuardTacticalState.Retreating:
                return false; // No atacar mientras se retira
                
            case GuardTacticalState.Defensive:
                // Solo atacar si está en buena posición
                return IsInGoodCombatPosition();
                
            case GuardTacticalState.Desperate:
                // Atacar más frecuentemente cuando está desesperado
                return Time.time - lastAttackTime >= attackCooldown * 0.7f;
                
            default:
                return true;
        }
    }
    
    private bool IsInGoodCombatPosition()
    {
        float distance = Vector3.Distance(transform.position, lastKnownPlayerPosition);
        
        // Distancia optimal
        if (distance < optimalCombatDistance * 0.5f || distance > maxCombatDistance)
            return false;
        
        // Verificar si tiene cobertura cerca si está siendo conservador
        if (personality.GetPersonalityType() == AIPersonalityType.Conservative)
        {
            Vector3 coverPos = FindNearestCover();
            if (coverPos == Vector3.zero) return false;
        }
        
        return true;
    }
    
    protected override void Attack()
    {
        if (!CanAttackPlayer()) return;
        
        // Usar sistema de combate avanzado
        bool attackSuccess = combatSystem.ExecuteAttack(lastKnownPlayerPosition);
        
        if (attackSuccess)
        {
            lastAttackTime = Time.time;
            consecutiveAttackMisses = 0;
            
            // Notificar al blackboard del disparo
            blackboard.SetValue(BlackboardKeys.LAST_GUNSHOT_POSITION, transform.position);
            blackboard.SetValue(BlackboardKeys.LAST_GUNSHOT_TIME, Time.time);
            
            LogDebug("Attack executed successfully");
        }
        else
        {
            consecutiveAttackMisses++;
            
            // Si falla mucho, cambiar táctica
            if (consecutiveAttackMisses >= 3)
            {
                Vector3 repositionTarget = FindRepositionPoint();
                if (repositionTarget != Vector3.zero)
                {
                    movementController.MoveTo(repositionTarget, moveSpeed);
                    lastRepositionTime = Time.time;
                    consecutiveAttackMisses = 0;
                    
                    LogDebug($"Repositioning after {consecutiveAttackMisses} misses");
                }
            }
        }
    }
    
    #endregion
    
    #region Coordination and Communication
    
    private void ProcessCoordination()
    {
        if (!enableCoordination) return;
        
        coordination.ProcessCoordination();
        
        // Verificar si necesita ayuda
        if (ShouldCallForHelp())
        {
            CallForHelp();
        }
        
        // Verificar alertas de otros guards
        ProcessIncomingAlerts();
    }
    
    private bool ShouldCallForHelp()
    {
        float healthPercentage = Health / MaxHealth;
        
        if (healthPercentage > callForHelpThreshold) return false;
        
        // No llamar ayuda muy frecuentemente
        var lastHelpCall = blackboard.GetValue<float>($"guard_{name}_last_help_call");
        if (Time.time - lastHelpCall < 5f) return false;
        
        // Verificar si hay aliados cerca para ayudar
        return GetNearbyAlliesCount() > 0;
    }
    
    private void CallForHelp()
    {
        blackboard.SetValue($"guard_{name}_last_help_call", Time.time);
        
        // Crear request de ayuda
        var helpRequest = new GuardHelpRequest
        {
            requestingGuard = this,
            position = transform.position,
            urgency = CalculateHelpUrgency(),
            requestTime = Time.time
        };
        
        // Añadir al blackboard
        var helpRequests = blackboard.GetValue<List<GuardHelpRequest>>(BlackboardKeys.GUARD_HELP_REQUESTS) ?? 
                          new List<GuardHelpRequest>();
        helpRequests.Add(helpRequest);
        blackboard.SetValue(BlackboardKeys.GUARD_HELP_REQUESTS, helpRequests);
        
        LogDebug($"Called for help with urgency: {helpRequest.urgency}");
    }
    
    private float CalculateHelpUrgency()
    {
        float healthFactor = 1f - (Health / MaxHealth);
        float enemyDistanceFactor = Mathf.Clamp01(5f / Vector3.Distance(transform.position, lastKnownPlayerPosition));
        float isolationFactor = GetNearbyAlliesCount() == 0 ? 0.5f : 0f;
        
        return Mathf.Clamp01(healthFactor + enemyDistanceFactor + isolationFactor);
    }
    
    private void ProcessIncomingAlerts()
    {
        // Verificar alertas de civilians
        var alertPosition = blackboard.GetValue<Vector3>(BlackboardKeys.ALERT_POSITION);
        var alertTime = blackboard.GetValue<float>(BlackboardKeys.ALERT_TIME);
        
        if (Time.time - alertTime < 30f) // Alerta reciente
        {
            float distance = Vector3.Distance(transform.position, alertPosition);
            if (distance <= coordinationRange && !CanSeePlayer)
            {
                // Investigar la alerta
                AddSuspiciousPosition(alertPosition);
                LogDebug($"Responding to civilian alert at {alertPosition}");
            }
        }
        
        // Verificar requests de ayuda de otros guards
        var helpRequests = blackboard.GetValue<List<GuardHelpRequest>>(BlackboardKeys.GUARD_HELP_REQUESTS);
        if (helpRequests != null)
        {
            foreach (var request in helpRequests.ToArray())
            {
                if (request.requestingGuard != this && ShouldRespondToHelpRequest(request))
                {
                    RespondToHelpRequest(request);
                }
            }
        }
    }
    
    private bool ShouldRespondToHelpRequest(GuardHelpRequest request)
    {
        // No responder a requests muy viejos
        if (Time.time - request.requestTime > 15f) return false;
        
        // Verificar distancia
        float distance = Vector3.Distance(transform.position, request.position);
        if (distance > coordinationRange) return false;
        
        // Verificar estado propio
        if (Health / MaxHealth < 0.5f) return false; // Muy debilitado para ayudar
        
        // Guards agresivos responden más frecuentemente
        float responseChance = behaviorModifiers.confidence * request.urgency;
        
        return Random.value < responseChance;
    }
    
    private void RespondToHelpRequest(GuardHelpRequest request)
    {
        // Moverse hacia la posición de ayuda
        movementController.MoveTo(request.position, moveSpeed * 1.2f);
        
        // Cambiar estado táctico
        tacticalState = GuardTacticalState.Assisting;
        
        LogDebug($"Responding to help request from {request.requestingGuard.name}");
    }
    
    #endregion
    
    #region Utility Methods
    
    private Vector3 FindNearestCover()
    {
        var coverPoints = FindObjectsOfType<CoverPoint>();
        Vector3 bestCover = Vector3.zero;
        float bestScore = float.MinValue;
        
        foreach (var cover in coverPoints)
        {
            float score = EvaluateCoverPoint(cover);
            if (score > bestScore)
            {
                bestScore = score;
                bestCover = cover.transform.position;
            }
        }
        
        return bestCover;
    }
    
    private float EvaluateCoverPoint(CoverPoint cover)
    {
        float distance = Vector3.Distance(transform.position, cover.transform.position);
        
        // Preferir cobertura más cerca
        float score = 100f / (1f + distance);
        
        // Bonus si la cobertura está entre el guard y el player
        Vector3 toPlayer = (lastKnownPlayerPosition - transform.position).normalized;
        Vector3 toCover = (cover.transform.position - transform.position).normalized;
        
        float alignment = Vector3.Dot(toPlayer, toCover);
        if (alignment > 0.5f)
        {
            score += 20f;
        }
        
        // Penalizar si está ocupada
        if (cover.IsOccupied)
        {
            score -= 50f;
        }
        
        return score;
    }
    
    private Vector3 FindSafeRetreatPosition()
    {
        Vector3 awayFromPlayer = (transform.position - lastKnownPlayerPosition).normalized;
        Vector3 retreatDirection = awayFromPlayer + Random.insideUnitSphere * 0.3f;
        retreatDirection.y = 0;
        retreatDirection = retreatDirection.normalized;
        
        return transform.position + retreatDirection * 10f;
    }
    
    private Vector3 FindRepositionPoint()
    {
        Vector3 currentPosition = transform.position;
        Vector3 playerPosition = lastKnownPlayerPosition;
        
        // Intentar encontrar un punto que mantenga distancia optimal
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 candidate = playerPosition + direction * optimalCombatDistance;
            
            // Verificar que sea accesible (sin raycast por simplicidad)
            if (Vector3.Distance(currentPosition, candidate) < 15f)
            {
                return candidate;
            }
        }
        
        return currentPosition;
    }
    
    private int GetNearbyAlliesCount()
    {
        var guards = FindObjectsOfType<Guard>();
        int count = 0;
        
        foreach (var guard in guards)
        {
            if (guard != this && guard.IsAlive)
            {
                float distance = Vector3.Distance(transform.position, guard.transform.position);
                if (distance <= coordinationRange)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    private int GetTotalAlliesCount()
    {
        var guards = FindObjectsOfType<Guard>();
        return guards.Count(g => g != this && g.IsAlive);
    }
    
    private void AddSuspiciousPosition(Vector3 position)
    {
        // Evitar duplicados
        if (suspiciousPositions.Any(p => Vector3.Distance(p, position) < 2f))
            return;
        
        suspiciousPositions.Add(position);
        
        // Limitar tamaño de la lista
        if (suspiciousPositions.Count > 5)
        {
            suspiciousPositions.RemoveAt(0);
        }
    }
    
    private void LogDebug(string message)
    {
        if (enableAdvancedLogging)
        {
            Logger.LogDebug($"Guard {name}: {message}");
        }
    }
    
    private void LogCurrentState()
    {
        LogDebug($"State: {tacticalState}, Health: {Health:F0}/{MaxHealth:F0}, " +
                $"Can See Player: {CanSeePlayer}, Distance: {GetDistanceToPlayer():F1}m, " +
                $"Suspicious Positions: {suspiciousPositions.Count}");
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnPlayerDetected()
    {
        LogDebug("Player detected!");
        
        // Notificar detección inmediata
        coordination.NotifyPlayerDetection(lastKnownPlayerPosition);
        
        // Actualizar estado táctico
        UpdateTacticalState();
    }
    
    private void OnPlayerLost()
    {
        LogDebug("Player lost from sight");
        
        // Añadir última posición conocida como sospechosa
        AddSuspiciousPosition(lastKnownPlayerPosition);
    }
    
    private void OnTacticalStateChanged(GuardTacticalState previousState, GuardTacticalState newState)
    {
        LogDebug($"Tactical state changed: {previousState} -> {newState}");
        
        // Resetear timer para el nuevo estado
        if (!stateTimers.ContainsKey(newState))
            stateTimers[newState] = 0f;
        else
            stateTimers[newState] = 0f;
        
        // Acciones específicas según cambio de estado
        switch (newState)
        {
            case GuardTacticalState.Aggressive:
                OnEnterAggressiveState();
                break;
                
            case GuardTacticalState.Retreating:
                OnEnterRetreatState();
                break;
                
            case GuardTacticalState.Desperate:
                OnEnterDesperateState();
                break;
        }
    }
    
    private void OnEnterAggressiveState()
    {
        // Aumentar velocidad de ataque temporalmente
        attackCooldown *= behaviorModifiers.aggressionBonus;
        
        LogDebug("Entered aggressive mode - increased attack rate");
    }
    
    private void OnEnterRetreatState()
    {
        // Llamar ayuda inmediatamente
        if (enableCoordination)
        {
            CallForHelp();
        }
        
        LogDebug("Entered retreat mode - called for help");
    }
    
    private void OnEnterDesperateState()
    {
        // Reducir cooldown de ataque drásticamente
        attackCooldown *= 0.5f;
        
        // Aumentar velocidad
        moveSpeed *= 1.3f;
        
        LogDebug("Entered desperate mode - combat bonuses activated");
    }
    
    protected override void OnDeath()
    {
        base.OnDeath();
        
        // Notificar muerte al sistema de coordinación
        coordination.NotifyDeath();
        
        // Limpiar del blackboard
        var helpRequests = blackboard.GetValue<List<GuardHelpRequest>>(BlackboardKeys.GUARD_HELP_REQUESTS);
        if (helpRequests != null)
        {
            helpRequests.RemoveAll(r => r.requestingGuard == this);
            blackboard.SetValue(BlackboardKeys.GUARD_HELP_REQUESTS, helpRequests);
        }
        
        LogDebug("Guard died - cleanup completed");
    }
    
    #endregion
    
    #region Debug and Gizmos
    
    void OnDrawGizmos()
    {
        if (!enableAdvancedLogging) return;
        
        // Estado táctico como color
        Gizmos.color = GetTacticalStateColor();
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.5f);
        
        // Rango de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, advancedDetectionRange);
        
        // Rango de coordinación
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, coordinationRange);
        
        // Posiciones sospechosas
        Gizmos.color = Color.orange;
        foreach (var suspiciousPos in suspiciousPositions)
        {
            Gizmos.DrawWireCube(suspiciousPos, Vector3.one);
            Gizmos.DrawLine(transform.position, suspiciousPos);
        }
        
        // Última posición conocida del player
        if (lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(lastKnownPlayerPosition, Vector3.one * 2f);
        }
    }
    
    private Color GetTacticalStateColor()
    {
        switch (tacticalState)
        {
            case GuardTacticalState.Patrolling: return Color.green;
            case GuardTacticalState.Searching: return Color.yellow;
            case GuardTacticalState.Investigating: return Color.orange;
            case GuardTacticalState.Aggressive: return Color.red;
            case GuardTacticalState.Defensive: return Color.blue;
            case GuardTacticalState.Retreating: return Color.cyan;
            case GuardTacticalState.Desperate: return Color.magenta;
            case GuardTacticalState.Assisting: return Color.white;
            default: return Color.gray;
        }
    }
    
    [ContextMenu("Print Advanced Stats")]
    public void PrintAdvancedStats()
    {
        Debug.Log($"=== Guard {name} Advanced Stats ===");
        Debug.Log($"Tactical State: {tacticalState}");
        Debug.Log($"Personality: {personality?.GetType().Name ?? "None"}");
        Debug.Log($"Health: {Health}/{MaxHealth} ({Health/MaxHealth:P0})");
        Debug.Log($"Can See Player: {CanSeePlayer}");
        Debug.Log($"Last Player Sight: {Time.time - lastPlayerSightTime:F1}s ago");
        Debug.Log($"Nearby Allies: {GetNearbyAlliesCount()}");
        Debug.Log($"Suspicious Positions: {suspiciousPositions.Count}");
        Debug.Log($"Consecutive Misses: {consecutiveAttackMisses}");
        
        if (behaviorModifiers != null)
        {
            Debug.Log($"Behavior Modifiers:");
            Debug.Log($"  Aggression: {behaviorModifiers.aggressionLevel:F2}");
            Debug.Log($"  Cautiousness: {behaviorModifiers.cautiousness:F2}");
            Debug.Log($"  Confidence: {behaviorModifiers.confidence:F2}");
        }
    }
    
    #endregion
}

// Enums y clases de apoyo
public enum GuardTacticalState
{
    Patrolling,
    Searching,
    Investigating,
    Aggressive,
    Defensive,
    Retreating,
    Desperate,
    Assisting
}

public enum GuardAction
{
    Attack,
    TakeCover,
    Reposition,
    CallForHelp,
    Investigate
}

[System.Serializable]
public class GuardHelpRequest
{
    public Guard requestingGuard;
    public Vector3 position;
    public float urgency;
    public float requestTime;
}
```

---

## 🎯 **CONFIGURACIÓN DE PERSONALIDADES**

### **GuardPersonality.cs - Base de Personalidades**
```csharp
public abstract class GuardPersonality : ScriptableObject
{
    [Header("Base Personality Settings")]
    [SerializeField] protected string personalityName;
    [SerializeField, TextArea(3, 5)] protected string description;
    
    [Header("Behavior Parameters")]
    [SerializeField, Range(0f, 2f)] protected float aggressionLevel = 1f;
    [SerializeField, Range(0f, 2f)] protected float cautiousness = 1f;
    [SerializeField, Range(0f, 2f)] protected float confidence = 1f;
    [SerializeField, Range(0f, 2f)] protected float cooperation = 1f;
    
    [Header("Combat Modifiers")]
    [SerializeField, Range(0.5f, 2f)] protected float attackCooldownMultiplier = 1f;
    [SerializeField, Range(0.5f, 2f)] protected float moveSpeedMultiplier = 1f;
    [SerializeField, Range(0.5f, 2f)] protected float detectionRangeMultiplier = 1f;
    [SerializeField, Range(0.5f, 2f)] protected float combatDistanceMultiplier = 1f;
    
    [Header("Decision Making")]
    [SerializeField, Range(0f, 1f)] protected float helpCallThreshold = 0.3f;
    [SerializeField, Range(0f, 2f)] protected float riskTolerance = 1f;
    [SerializeField, Range(0f, 2f)] protected float aggressionBonus = 1f;
    
    public abstract AIPersonalityType GetPersonalityType();
    
    public virtual GuardBehaviorModifiers GetBehaviorModifiers()
    {
        return new GuardBehaviorModifiers
        {
            aggressionLevel = this.aggressionLevel,
            cautiousness = this.cautiousness,
            confidence = this.confidence,
            cooperation = this.cooperation,
            attackCooldownMultiplier = this.attackCooldownMultiplier,
            moveSpeedMultiplier = this.moveSpeedMultiplier,
            detectionRangeMultiplier = this.detectionRangeMultiplier,
            combatDistanceMultiplier = this.combatDistanceMultiplier,
            helpCallThreshold = this.helpCallThreshold,
            riskTolerance = this.riskTolerance,
            aggressionBonus = this.aggressionBonus
        };
    }
    
    public virtual string GetPersonalityName() => personalityName;
    public virtual string GetDescription() => description;
}

[System.Serializable]
public struct GuardBehaviorModifiers
{
    public float aggressionLevel;
    public float cautiousness;
    public float confidence;
    public float cooperation;
    public float attackCooldownMultiplier;
    public float moveSpeedMultiplier;
    public float detectionRangeMultiplier;
    public float combatDistanceMultiplier;
    public float helpCallThreshold;
    public float riskTolerance;
    public float aggressionBonus;
}
```

### **AggressiveGuardPersonality.cs**
```csharp
[CreateAssetMenu(fileName = "AggressiveGuardPersonality", menuName = "AI/Guard Personalities/Aggressive")]
public class AggressiveGuardPersonality : GuardPersonality
{
    void OnEnable()
    {
        // Configuración para guard agresivo
        personalityName = "Aggressive Guard";
        description = "Attacks quickly, takes risks, prioritizes offense over defense. " +
                     "Charges toward enemies and calls for help less frequently.";
        
        aggressionLevel = 1.8f;
        cautiousness = 0.6f;
        confidence = 1.6f;
        cooperation = 0.8f;
        
        attackCooldownMultiplier = 0.7f; // Ataca más rápido
        moveSpeedMultiplier = 1.3f; // Se mueve más rápido
        detectionRangeMultiplier = 1.1f; // Detección ligeramente mayor
        combatDistanceMultiplier = 0.8f; // Prefiere combate cerca
        
        helpCallThreshold = 0.2f; // Llama ayuda solo cuando está muy mal
        riskTolerance = 1.7f; // Toma más riesgos
        aggressionBonus = 1.5f; // Bonus de agresión
    }
    
    public override AIPersonalityType GetPersonalityType()
    {
        return AIPersonalityType.Aggressive;
    }
    
    public override GuardBehaviorModifiers GetBehaviorModifiers()
    {
        var modifiers = base.GetBehaviorModifiers();
        
        // Modificadores específicos adicionales para agresivos
        modifiers.aggressionLevel += 0.3f; // Boost adicional
        modifiers.confidence += 0.2f;
        
        return modifiers;
    }
}
```

### **ConservativeGuardPersonality.cs**
```csharp
[CreateAssetMenu(fileName = "ConservativeGuardPersonality", menuName = "AI/Guard Personalities/Conservative")]
public class ConservativeGuardPersonality : GuardPersonality
{
    void OnEnable()
    {
        // Configuración para guard conservador
        personalityName = "Conservative Guard";
        description = "Prioritizes safety and defense, uses cover effectively, " +
                     "calls for help early and coordinates well with allies.";
        
        aggressionLevel = 0.7f;
        cautiousness = 1.8f;
        confidence = 0.9f;
        cooperation = 1.6f;
        
        attackCooldownMultiplier = 1.4f; // Ataca más lento pero más preciso
        moveSpeedMultiplier = 0.9f; // Se mueve más cauteloso
        detectionRangeMultiplier = 1.3f; // Mayor rango de detección
        combatDistanceMultiplier = 1.4f; // Prefiere distancia
        
        helpCallThreshold = 0.5f; // Llama ayuda más temprano
        riskTolerance = 0.6f; // Evita riesgos
        aggressionBonus = 0.8f; // Menos agresivo
    }
    
    public override AIPersonalityType GetPersonalityType()
    {
        return AIPersonalityType.Conservative;
    }
    
    public override GuardBehaviorModifiers GetBehaviorModifiers()
    {
        var modifiers = base.GetBehaviorModifiers();
        
        // Modificadores específicos adicionales para conservadores
        modifiers.cautiousness += 0.4f; // Boost adicional de cautela
        modifiers.cooperation += 0.3f; // Mejor cooperación
        
        return modifiers;
    }
}
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Guards completamente integrados** con todos los sistemas
2. **✅ Personalidades funcionales** que cambian comportamiento real
3. **✅ Steering behaviors** configurados según contexto
4. **✅ Sistema de coordinación** entre guards
5. **✅ Estados tácticos** dinámicos y reactivos
6. **✅ Combate avanzado** con repositioning y táctica
7. **✅ Integración con Blackboard** para comunicación global

### **Testing:**
1. **Personality Differences**: Guards agresivos vs conservadores deben comportarse diferente
2. **Tactical States**: Deben cambiar estados según salud, distancia, aliados
3. **Coordination**: Guards deben ayudarse y comunicarse
4. **Steering Integration**: Movimiento debe ser fluido y realista
5. **Combat Intelligence**: Deben reposicionarse y usar táctica

Esta fase transforma tus Guards básicos en **AI compleja e inteligente** que usa todos los sistemas desarrollados trabajando en conjunto.