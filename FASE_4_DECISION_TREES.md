# 🌳 FASE 4: DECISION TREES (Día 6)

## 🎯 **OBJETIVO DE LA FASE**
Implementar **árboles de decisión** para crear comportamiento inteligente y reactivo en los NPCs, especialmente para el **segundo grupo de enemigos (Civilians)**, cumpliendo con el requisito obligatorio de Decision Trees del TP.

---

## 📋 **¿QUÉ BUSCAMOS LOGRAR?**

### **Problema Actual:**
- Solo tienes Guards con FSM (falta segundo grupo de enemigos)
- No hay toma de decisiones basada en múltiples factores
- Comportamiento predecible y estático
- Decision Trees es requisito obligatorio del TP

### **Solución con Decision Trees:**
- **Civilians** como segundo grupo de enemigos
- **Toma de decisiones inteligente** basada en contexto
- **Comportamiento emergente** e impredecible
- **Integración completa** con Blackboard y Steering

---

## 🏗️ **ARQUITECTURA DE DECISION TREES**

### **DecisionNode.cs - Clase Base**
```csharp
public abstract class DecisionNode : ScriptableObject, IDecisionNode
{
    [Header("Node Information")]
    [SerializeField] protected string nodeName;
    [SerializeField, TextArea(2, 4)] protected string description;
    [SerializeField] protected bool enableDebugLogs = false;
    
    [Header("Performance")]
    [SerializeField] protected float executionCost = 1f; // Para optimización futura
    
    public abstract IDecisionNode Evaluate(IAIContext context);
    public virtual string GetNodeName() => string.IsNullOrEmpty(nodeName) ? GetType().Name : nodeName;
    public virtual string GetDescription() => description;
    public abstract NodeType GetNodeType();
    
    protected void LogDecision(IAIContext context, string decision)
    {
        if (enableDebugLogs)
        {
            Logger.LogDebug($"{context.GetTransform().name} - {GetNodeName()}: {decision}");
        }
    }
    
    protected void LogError(IAIContext context, string error)
    {
        Logger.LogError($"{context.GetTransform().name} - {GetNodeName()}: {error}");
    }
    
    // Para estadísticas y debugging
    [System.NonSerialized]
    public int executionCount = 0;
    [System.NonSerialized]
    public float totalExecutionTime = 0f;
}

public enum NodeType
{
    Condition,
    Action,
    Selector,
    Sequence,
    Parallel
}
```

### **ConditionNode.cs - Nodos de Decisión**
```csharp
public abstract class ConditionNode : DecisionNode
{
    [Header("Condition Branches")]
    [SerializeField] protected DecisionNode trueNode;
    [SerializeField] protected DecisionNode falseNode;
    
    [Header("Condition Settings")]
    [SerializeField] protected bool invertResult = false;
    [SerializeField] protected float evaluationCooldown = 0f;
    
    private float lastEvaluationTime = 0f;
    private bool lastResult = false;
    
    public override NodeType GetNodeType() => NodeType.Condition;
    
    public override IDecisionNode Evaluate(IAIContext context)
    {
        executionCount++;
        float startTime = Time.realtimeSinceStartup;
        
        // Cooldown para optimización
        bool useCache = false;
        if (evaluationCooldown > 0f && Time.time - lastEvaluationTime < evaluationCooldown)
        {
            useCache = true;
        }
        
        bool result;
        if (useCache)
        {
            result = lastResult;
            LogDecision(context, $"Using cached result: {result}");
        }
        else
        {
            result = EvaluateCondition(context);
            lastResult = result;
            lastEvaluationTime = Time.time;
        }
        
        if (invertResult)
        {
            result = !result;
        }
        
        LogDecision(context, $"Condition result: {result} (inverted: {invertResult})");
        
        totalExecutionTime += Time.realtimeSinceStartup - startTime;
        
        return result ? trueNode : falseNode;
    }
    
    protected abstract bool EvaluateCondition(IAIContext context);
    
    // Para debugging en inspector
    public string GetLastEvaluationInfo()
    {
        return $"Last result: {lastResult}, Executions: {executionCount}, Avg time: {(totalExecutionTime / Mathf.Max(1, executionCount) * 1000):F2}ms";
    }
}
```

### **ActionNode.cs - Nodos de Acción**
```csharp
public abstract class ActionNode : DecisionNode
{
    [Header("Action Settings")]
    [SerializeField] protected float actionDuration = 1f;
    [SerializeField] protected bool isRepeatable = true;
    [SerializeField] protected float cooldownTime = 0f;
    
    [Header("Integration")]
    [SerializeField] protected bool updateBlackboard = true;
    [SerializeField] protected bool triggerEvents = true;
    
    private float lastExecutionTime = 0f;
    
    public override NodeType GetNodeType() => NodeType.Action;
    
    public override IDecisionNode Evaluate(IAIContext context)
    {
        executionCount++;
        
        // Verificar cooldown
        if (cooldownTime > 0f && Time.time - lastExecutionTime < cooldownTime)
        {
            LogDecision(context, $"Action in cooldown ({Time.time - lastExecutionTime:F1}s/{cooldownTime:F1}s)");
            return null; // No ejecutar
        }
        
        float startTime = Time.realtimeSinceStartup;
        
        LogDecision(context, "Executing action");
        
        try
        {
            ExecuteAction(context);
            lastExecutionTime = Time.time;
        }
        catch (System.Exception e)
        {
            LogError(context, $"Action execution failed: {e.Message}");
        }
        
        totalExecutionTime += Time.realtimeSinceStartup - startTime;
        
        // Los nodos de acción son terminales por defecto
        return null;
    }
    
    protected abstract void ExecuteAction(IAIContext context);
    
    protected void SetCivilianState(IAIContext context, CivilianState state)
    {
        var civilian = context.GetTransform().GetComponent<CivilianAI>();
        if (civilian != null)
        {
            civilian.SetState(state);
        }
    }
    
    protected void UpdateBlackboardEntry(IAIContext context, string key, object value)
    {
        if (updateBlackboard)
        {
            context.GetBlackboard()?.SetValue(key, value);
        }
    }
}
```

---

## 🔍 **NODOS DE CONDICIÓN ESPECÍFICOS**

### **IsPlayerVisibleNode.cs**
```csharp
[CreateAssetMenu(fileName = "IsPlayerVisible", menuName = "AI/Decision Trees/Conditions/IsPlayerVisible")]
public class IsPlayerVisibleNode : ConditionNode
{
    [Header("Vision Settings")]
    [SerializeField] private bool requireDirectLineOfSight = true;
    [SerializeField] private float maxDetectionRange = 10f;
    
    protected override bool EvaluateCondition(IAIContext context)
    {
        bool canSee = context.IsPlayerVisible();
        
        if (canSee && requireDirectLineOfSight)
        {
            // Verificación adicional de distancia
            float distance = context.GetDistanceToPlayer();
            if (distance > maxDetectionRange)
            {
                canSee = false;
            }
        }
        
        return canSee;
    }
}
```

### **IsPlayerCloseNode.cs**
```csharp
[CreateAssetMenu(fileName = "IsPlayerClose", menuName = "AI/Decision Trees/Conditions/IsPlayerClose")]
public class IsPlayerCloseNode : ConditionNode
{
    [Header("Distance Settings")]
    [SerializeField] private float closeDistance = 3f;
    [SerializeField] private float veryCloseDistance = 1.5f;
    [SerializeField] private bool useVeryCloseCheck = false;
    
    protected override bool EvaluateCondition(IAIContext context)
    {
        float distance = context.GetDistanceToPlayer();
        
        if (useVeryCloseCheck)
        {
            return distance <= veryCloseDistance;
        }
        
        return distance <= closeDistance && distance > 0f;
    }
}
```

### **IsAlertActiveNode.cs**
```csharp
[CreateAssetMenu(fileName = "IsAlertActive", menuName = "AI/Decision Trees/Conditions/IsAlertActive")]
public class IsAlertActiveNode : ConditionNode
{
    [Header("Alert Settings")]
    [SerializeField] private int minimumAlertLevel = 1;
    [SerializeField] private float alertTimeout = 30f; // Segundos
    
    protected override bool EvaluateCondition(IAIContext context)
    {
        var blackboard = context.GetBlackboard();
        if (blackboard == null) return false;
        
        int alertLevel = blackboard.GetValue<int>(BlackboardKeys.ALERT_LEVEL);
        
        if (alertLevel < minimumAlertLevel)
            return false;
        
        // Verificar timeout de alerta
        if (alertTimeout > 0f)
        {
            float alertTime = blackboard.GetValue<float>(BlackboardKeys.ALERT_TIME);
            if (Time.time - alertTime > alertTimeout)
            {
                // La alerta ha expirado
                blackboard.SetValue(BlackboardKeys.ALERT_LEVEL, 0);
                return false;
            }
        }
        
        return true;
    }
}
```

### **AreGuardsNearbyNode.cs**
```csharp
[CreateAssetMenu(fileName = "AreGuardsNearby", menuName = "AI/Decision Trees/Conditions/AreGuardsNearby")]
public class AreGuardsNearbyNode : ConditionNode
{
    [Header("Guard Detection")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private int minimumGuards = 1;
    [SerializeField] private bool onlyAliveGuards = true;
    
    protected override bool EvaluateCondition(IAIContext context)
    {
        var guards = FindObjectsOfType<Guard>();
        int nearbyGuards = 0;
        Vector3 position = context.GetTransform().position;
        
        foreach (var guard in guards)
        {
            if (onlyAliveGuards && !guard.IsAlive)
                continue;
            
            float distance = Vector3.Distance(position, guard.transform.position);
            if (distance <= detectionRadius)
            {
                nearbyGuards++;
            }
        }
        
        return nearbyGuards >= minimumGuards;
    }
}
```

### **HasEscapeRouteNode.cs**
```csharp
[CreateAssetMenu(fileName = "HasEscapeRoute", menuName = "AI/Decision Trees/Conditions/HasEscapeRoute")]
public class HasEscapeRouteNode : ConditionNode
{
    [Header("Escape Route Settings")]
    [SerializeField] private float searchRadius = 15f;
    [SerializeField] private string escapePointTag = "ExitPoint";
    [SerializeField] private bool checkPathClear = true;
    [SerializeField] private LayerMask obstacleLayer = 1;
    
    protected override bool EvaluateCondition(IAIContext context)
    {
        GameObject[] exitPoints = GameObject.FindGameObjectsWithTag(escapePointTag);
        Vector3 position = context.GetTransform().position;
        
        foreach (var exitPoint in exitPoints)
        {
            float distance = Vector3.Distance(position, exitPoint.transform.position);
            
            if (distance <= searchRadius)
            {
                // Verificar si el camino está libre
                if (checkPathClear)
                {
                    Vector3 direction = (exitPoint.transform.position - position).normalized;
                    if (!Physics.Raycast(position, direction, distance, obstacleLayer))
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
        }
        
        return false;
    }
}
```

### **RandomChanceNode.cs**
```csharp
[CreateAssetMenu(fileName = "RandomChance", menuName = "AI/Decision Trees/Conditions/RandomChance")]
public class RandomChanceNode : ConditionNode
{
    [Header("Probability Settings")]
    [SerializeField, Range(0f, 1f)] private float probability = 0.5f;
    [SerializeField] private bool usePersonalityModifier = true;
    [SerializeField] private float aggressiveBonus = 0.2f;
    [SerializeField] private float conservativeBonus = -0.1f;
    
    protected override bool EvaluateCondition(IAIContext context)
    {
        float effectiveProbability = probability;
        
        if (usePersonalityModifier)
        {
            var personality = context.GetPersonalityType();
            switch (personality)
            {
                case AIPersonalityType.Aggressive:
                    effectiveProbability += aggressiveBonus;
                    break;
                case AIPersonalityType.Conservative:
                    effectiveProbability += conservativeBonus;
                    break;
            }
        }
        
        effectiveProbability = Mathf.Clamp01(effectiveProbability);
        
        bool result = Random.value <= effectiveProbability;
        
        LogDecision(context, $"Random chance: {effectiveProbability:P0} -> {result}");
        
        return result;
    }
}
```

---

## 🎬 **NODOS DE ACCIÓN ESPECÍFICOS**

### **FleeActionNode.cs**
```csharp
[CreateAssetMenu(fileName = "FleeAction", menuName = "AI/Decision Trees/Actions/Flee")]
public class FleeActionNode : ActionNode
{
    [Header("Flee Settings")]
    [SerializeField] private float fleeSpeed = 5f;
    [SerializeField] private float fleeDuration = 3f;
    [SerializeField] private bool usePanicMovement = true;
    
    protected override void ExecuteAction(IAIContext context)
    {
        var movementController = context.GetTransform().GetComponent<IAIMovementController>();
        if (movementController == null)
        {
            LogError(context, "No IAIMovementController found!");
            return;
        }
        
        Vector3 playerPos = context.GetPlayerPosition();
        
        // Activar flee behavior
        movementController.Flee(playerPos, fleeSpeed);
        
        // Configurar steering behaviors específicos
        if (usePanicMovement)
        {
            movementController.EnableBehavior(typeof(FleeBehavior), true);
            movementController.EnableBehavior(typeof(EvadeBehavior), true);
            movementController.SetBehaviorWeight(typeof(FleeBehavior), 2f);
        }
        
        // Actualizar blackboard
        UpdateBlackboardEntry(context, BlackboardKeys.CIVILIAN_PANIC_AREAS, 
            context.GetBlackboard()?.GetValue<List<Vector3>>(BlackboardKeys.CIVILIAN_PANIC_AREAS) ?? new List<Vector3>());
        
        var panicAreas = context.GetBlackboard()?.GetValue<List<Vector3>>(BlackboardKeys.CIVILIAN_PANIC_AREAS);
        panicAreas?.Add(context.GetTransform().position);
        
        // Cambiar estado visual
        SetCivilianState(context, CivilianState.Fleeing);
        
        LogDecision(context, $"FLEEING from player at speed {fleeSpeed}");
    }
}
```

### **AlertGuardsActionNode.cs**
```csharp
[CreateAssetMenu(fileName = "AlertGuards", menuName = "AI/Decision Trees/Actions/AlertGuards")]
public class AlertGuardsActionNode : ActionNode
{
    [Header("Alert Settings")]
    [SerializeField] private float alertRadius = 15f;
    [SerializeField] private float baseAlertProbability = 0.6f;
    [SerializeField] private bool useRouletteWheel = true;
    
    [Header("Personality Modifiers")]
    [SerializeField] private float aggressiveModifier = 0.3f;
    [SerializeField] private float conservativeModifier = -0.2f;
    
    protected override void ExecuteAction(IAIContext context)
    {
        float effectiveProbability = CalculateAlertProbability(context);
        
        bool shouldAlert;
        if (useRouletteWheel)
        {
            // Usar RouletteWheel existente del proyecto
            var roulette = new RouletteWheel<bool>(
                new bool[] { true, false },
                new float[] { effectiveProbability, 1f - effectiveProbability }
            );
            shouldAlert = roulette.GetRandomItem();
        }
        else
        {
            shouldAlert = Random.value <= effectiveProbability;
        }
        
        if (shouldAlert)
        {
            AlertNearbyGuards(context);
            UpdateGlobalBlackboard(context);
            SetCivilianState(context, CivilianState.Alerting);
            
            LogDecision(context, $"ALERTING GUARDS (probability: {effectiveProbability:P0})");
        }
        else
        {
            // Demasiado asustado para alertar - huir en su lugar
            var fleeAction = CreateInstance<FleeActionNode>();
            fleeAction.ExecuteAction(context);
            
            LogDecision(context, $"Too scared to alert guards (probability: {effectiveProbability:P0})");
        }
    }
    
    private float CalculateAlertProbability(IAIContext context)
    {
        float probability = baseAlertProbability;
        
        // Modificador por personalidad
        var personality = context.GetPersonalityType();
        switch (personality)
        {
            case AIPersonalityType.Aggressive:
                probability += aggressiveModifier;
                break;
            case AIPersonalityType.Conservative:
                probability += conservativeModifier;
                break;
        }
        
        // Modificador por distancia al player
        float distance = context.GetDistanceToPlayer();
        if (distance < 3f)
        {
            probability -= 0.3f; // Muy cerca - demasiado asustado
        }
        else if (distance > 8f)
        {
            probability += 0.2f; // Lejos - más valiente
        }
        
        // Modificador por estado de alerta global
        var blackboard = context.GetBlackboard();
        int alertLevel = blackboard?.GetValue<int>(BlackboardKeys.ALERT_LEVEL) ?? 0;
        if (alertLevel > 0)
        {
            probability += 0.2f; // Ya hay alerta - más probable que coopere
        }
        
        return Mathf.Clamp01(probability);
    }
    
    private void AlertNearbyGuards(IAIContext context)
    {
        var guards = FindObjectsOfType<Guard>();
        Vector3 civilianPos = context.GetTransform().position;
        Vector3 playerPos = context.GetPlayerPosition();
        
        foreach (var guard in guards)
        {
            if (!guard.IsAlive) continue;
            
            float distance = Vector3.Distance(civilianPos, guard.transform.position);
            if (distance <= alertRadius)
            {
                // Dar información directa al guard
                guard.SetTargetTransform(context.GetBlackboard()?.GetValue<Transform>(BlackboardKeys.PLAYER_TRANSFORM));
                guard.LastKnownPlayerPosition = playerPos;
                
                LogDecision(context, $"Alerted guard: {guard.name} (distance: {distance:F1}m)");
            }
        }
    }
    
    private void UpdateGlobalBlackboard(IAIContext context)
    {
        var blackboard = context.GetBlackboard();
        if (blackboard == null) return;
        
        blackboard.SetValue(BlackboardKeys.ALERT_LEVEL, 2);
        blackboard.SetValue(BlackboardKeys.ALERT_POSITION, context.GetPlayerPosition());
        blackboard.SetValue(BlackboardKeys.LAST_ALERT_SOURCE, context.GetTransform());
        blackboard.SetValue(BlackboardKeys.ALERT_TIME, Time.time);
        
        // Registrar civilian como fuente de alerta
        var alertingSources = blackboard.GetValue<List<Transform>>("alerting_civilians") ?? new List<Transform>();
        if (!alertingSources.Contains(context.GetTransform()))
        {
            alertingSources.Add(context.GetTransform());
            blackboard.SetValue("alerting_civilians", alertingSources);
        }
    }
}
```

### **WalkToExitActionNode.cs**
```csharp
[CreateAssetMenu(fileName = "WalkToExit", menuName = "AI/Decision Trees/Actions/WalkToExit")]
public class WalkToExitActionNode : ActionNode
{
    [Header("Exit Settings")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private string exitPointTag = "ExitPoint";
    [SerializeField] private bool useNearestExit = true;
    [SerializeField] private bool avoidPlayerPath = true;
    
    protected override void ExecuteAction(IAIContext context)
    {
        Transform targetExit = FindBestExit(context);
        
        if (targetExit == null)
        {
            LogError(context, "No exit points found!");
            
            // Fallback: caminar aleatoriamente
            var randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection = randomDirection.normalized;
            
            var movementController = context.GetTransform().GetComponent<IAIMovementController>();
            movementController?.MoveTo(context.GetTransform().position + randomDirection * 10f, walkSpeed);
            
            return;
        }
        
        var movementController = context.GetTransform().GetComponent<IAIMovementController>();
        if (movementController != null)
        {
            movementController.MoveTo(targetExit.position, walkSpeed);
            
            // Configurar steering para caminar calmadamente
            movementController.EnableBehavior(typeof(SeekBehavior), true);
            movementController.EnableBehavior(typeof(FleeBehavior), false);
            movementController.SetBehaviorWeight(typeof(SeekBehavior), 1f);
        }
        
        SetCivilianState(context, CivilianState.Normal);
        
        LogDecision(context, $"Walking to exit: {targetExit.name} at speed {walkSpeed}");
    }
    
    private Transform FindBestExit(IAIContext context)
    {
        GameObject[] exitPoints = GameObject.FindGameObjectsWithTag(exitPointTag);
        if (exitPoints.Length == 0) return null;
        
        Vector3 position = context.GetTransform().position;
        Vector3 playerPosition = context.GetPlayerPosition();
        
        Transform bestExit = null;
        float bestScore = float.MinValue;
        
        foreach (var exitPoint in exitPoints)
        {
            float score = CalculateExitScore(position, playerPosition, exitPoint.transform, context);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestExit = exitPoint.transform;
            }
        }
        
        return bestExit;
    }
    
    private float CalculateExitScore(Vector3 civilianPos, Vector3 playerPos, Transform exit, IAIContext context)
    {
        float score = 0f;
        
        // Preferir exits más cercanos
        float distance = Vector3.Distance(civilianPos, exit.position);
        score += 100f / (1f + distance); // Puntuación inversamente proporcional a distancia
        
        if (avoidPlayerPath)
        {
            // Penalizar exits que están cerca del player
            float playerDistance = Vector3.Distance(playerPos, exit.position);
            if (playerDistance < 5f)
            {
                score -= 50f;
            }
            
            // Penalizar exits que requieren pasar cerca del player
            Vector3 toExit = (exit.position - civilianPos).normalized;
            Vector3 toPlayer = (playerPos - civilianPos).normalized;
            float pathAlignment = Vector3.Dot(toExit, toPlayer);
            
            if (pathAlignment > 0.5f) // Camino hacia el exit pasa cerca del player
            {
                score -= 30f;
            }
        }
        
        // Bonus por exits que están "detrás" del civilian relativo al player
        Vector3 awayFromPlayer = (civilianPos - playerPos).normalized;
        Vector3 toExitDir = (exit.position - civilianPos).normalized;
        float escapeAlignment = Vector3.Dot(awayFromPlayer, toExitDir);
        
        if (escapeAlignment > 0f)
        {
            score += escapeAlignment * 20f;
        }
        
        return score;
    }
}
```

### **IdleActionNode.cs**
```csharp
[CreateAssetMenu(fileName = "IdleAction", menuName = "AI/Decision Trees/Actions/Idle")]
public class IdleActionNode : ActionNode
{
    [Header("Idle Settings")]
    [SerializeField] private float idleDuration = 2f;
    [SerializeField] private bool allowRandomMovement = true;
    [SerializeField] private float randomMovementChance = 0.3f;
    [SerializeField] private float maxRandomDistance = 3f;
    
    protected override void ExecuteAction(IAIContext context)
    {
        var movementController = context.GetTransform().GetComponent<IAIMovementController>();
        
        if (allowRandomMovement && Random.value < randomMovementChance)
        {
            // Movimiento aleatorio ocasional
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection = randomDirection.normalized;
            
            Vector3 randomTarget = context.GetTransform().position + randomDirection * Random.Range(1f, maxRandomDistance);
            
            movementController?.MoveTo(randomTarget, 1f);
            
            LogDecision(context, $"Idle with random movement to {randomTarget}");
        }
        else
        {
            // Completamente idle
            movementController?.Stop();
            
            LogDecision(context, "Continuing normal idle behavior");
        }
        
        SetCivilianState(context, CivilianState.Normal);
    }
}
```

---

## 🧠 **CIVILIAN AI COMPLETO**

### **CivilianAI.cs - Implementación Principal**
```csharp
public enum CivilianState
{
    Normal,
    Suspicious,
    Panicking,
    Fleeing,
    Alerting
}

public class CivilianAI : MonoBehaviour, IAIContext
{
    [Header("Decision Tree")]
    [SerializeField] private DecisionNode rootDecisionNode;
    [SerializeField] private float decisionInterval = 0.5f;
    [SerializeField] private int maxDecisionDepth = 10;
    
    [Header("Civilian Settings")]
    [SerializeField] private CivilianState currentState = CivilianState.Normal;
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float fieldOfView = 120f;
    [SerializeField] private AIPersonalityType personalityType = AIPersonalityType.Civilian;
    
    [Header("Debug")]
    [SerializeField] private bool enableDecisionLogs = true;
    [SerializeField] private bool showDecisionPath = false;
    
    // Componentes del sistema
    private IBlackboard blackboard;
    private IPlayerDetector playerDetector;
    private IAIMovementController movementController;
    
    // Control de decisiones
    private float lastDecisionTime;
    private Transform playerTransform;
    private List<string> decisionPath = new List<string>();
    
    // Estadísticas
    private int totalDecisions = 0;
    private Dictionary<string, int> nodeExecutionCount = new Dictionary<string, int>();
    
    void Start()
    {
        Initialize();
    }
    
    void Update()
    {
        if (Time.time - lastDecisionTime >= decisionInterval)
        {
            MakeDecision();
            lastDecisionTime = Time.time;
        }
        
        // Limpieza periódica
        if (Time.time % 10f < 0.1f) // Cada 10 segundos aproximadamente
        {
            CleanupDecisionPath();
        }
    }
    
    private void Initialize()
    {
        // Obtener componentes requeridos
        blackboard = ServiceLocator.Get<IBlackboard>();
        playerDetector = GetComponent<IPlayerDetector>();
        movementController = GetComponent<IAIMovementController>();
        
        if (blackboard == null)
        {
            Logger.LogError($"CivilianAI on {name}: Blackboard not found!");
            enabled = false;
            return;
        }
        
        // Obtener referencia al player
        playerTransform = blackboard.GetValue<Transform>(BlackboardKeys.PLAYER_TRANSFORM);
        
        // Asegurar que PlayerDetector existe
        if (playerDetector == null)
        {
            var detectorComponent = gameObject.AddComponent<PlayerDetector>();
            playerDetector = detectorComponent;
            Logger.LogWarning($"CivilianAI on {name}: PlayerDetector was missing, added automatically");
        }
        
        // Asegurar que MovementController existe
        if (movementController == null)
        {
            var movementComponent = gameObject.AddComponent<AIMovementController>();
            movementController = movementComponent;
            Logger.LogWarning($"CivilianAI on {name}: AIMovementController was missing, added automatically");
        }
        
        if (enableDecisionLogs)
            Logger.LogDebug($"CivilianAI {name}: Initialized with decision tree: {rootDecisionNode?.name ?? "None"}");
    }
    
    private void MakeDecision()
    {
        if (rootDecisionNode == null)
        {
            if (enableDecisionLogs)
                Logger.LogWarning($"CivilianAI {name}: No root decision node assigned!");
            return;
        }
        
        totalDecisions++;
        decisionPath.Clear();
        
        var currentNode = rootDecisionNode;
        int iterations = 0;
        
        while (currentNode != null && iterations < maxDecisionDepth)
        {
            string nodeName = currentNode.GetNodeName();
            decisionPath.Add(nodeName);
            
            // Contar ejecuciones para estadísticas
            if (!nodeExecutionCount.ContainsKey(nodeName))
                nodeExecutionCount[nodeName] = 0;
            nodeExecutionCount[nodeName]++;
            
            try
            {
                currentNode = currentNode.Evaluate(this);
            }
            catch (System.Exception e)
            {
                Logger.LogError($"CivilianAI {name}: Error evaluating node {nodeName}: {e.Message}");
                break;
            }
            
            iterations++;
        }
        
        if (iterations >= maxDecisionDepth)
        {
            Logger.LogWarning($"CivilianAI {name}: Decision tree exceeded max depth! Possible infinite loop.");
        }
        
        if (showDecisionPath && decisionPath.Count > 1)
        {
            Logger.LogDebug($"CivilianAI {name}: Decision path: {string.Join(" -> ", decisionPath)}");
        }
    }
    
    public void SetState(CivilianState newState)
    {
        if (currentState != newState)
        {
            var oldState = currentState;
            currentState = newState;
            
            if (enableDecisionLogs)
                Logger.LogDebug($"CivilianAI {name}: State changed from {oldState} to {newState}");
            
            UpdateVisualState();
            NotifyStateChange(oldState, newState);
        }
    }
    
    private void UpdateVisualState()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            Color stateColor = GetStateColor();
            renderer.material.color = stateColor;
        }
        
        // Actualizar animaciones si existen
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetInteger("CivilianState", (int)currentState);
        }
    }
    
    private Color GetStateColor()
    {
        switch (currentState)
        {
            case CivilianState.Normal: return Color.white;
            case CivilianState.Suspicious: return Color.yellow;
            case CivilianState.Panicking: return Color.orange;
            case CivilianState.Fleeing: return Color.red;
            case CivilianState.Alerting: return Color.cyan;
            default: return Color.gray;
        }
    }
    
    private void NotifyStateChange(CivilianState oldState, CivilianState newState)
    {
        // Notificar al blackboard si es relevante
        if (newState == CivilianState.Panicking || newState == CivilianState.Fleeing)
        {
            var panicAreas = blackboard.GetValue<List<Vector3>>(BlackboardKeys.CIVILIAN_PANIC_AREAS) ?? new List<Vector3>();
            panicAreas.Add(transform.position);
            blackboard.SetValue(BlackboardKeys.CIVILIAN_PANIC_AREAS, panicAreas);
        }
        
        // Eventos para otros sistemas
        if (newState == CivilianState.Alerting)
        {
            // Incrementar nivel de alerta global
            int currentAlert = blackboard.GetValue<int>(BlackboardKeys.ALERT_LEVEL);
            blackboard.SetValue(BlackboardKeys.ALERT_LEVEL, Mathf.Max(currentAlert, 1));
        }
    }
    
    private void CleanupDecisionPath()
    {
        // Limpiar estadísticas antiguas para evitar memory leaks
        if (nodeExecutionCount.Count > 50)
        {
            var sortedNodes = nodeExecutionCount.OrderByDescending(kvp => kvp.Value).Take(30).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            nodeExecutionCount = sortedNodes;
        }
    }
    
    #region IAIContext Implementation
    public Transform GetTransform() => transform;
    public IBlackboard GetBlackboard() => blackboard;
    
    public bool IsPlayerVisible()
    {
        return playerDetector?.CanSeePlayer(playerTransform) ?? false;
    }
    
    public Vector3 GetPlayerPosition()
    {
        return playerTransform?.position ?? Vector3.zero;
    }
    
    public float GetDistanceToPlayer()
    {
        if (playerTransform == null) return float.MaxValue;
        return Vector3.Distance(transform.position, playerTransform.position);
    }
    
    public float GetDetectionRange() => detectionRange;
    
    public AIPersonalityType GetPersonalityType() => personalityType;
    #endregion
    
    #region Debug and Statistics
    [ContextMenu("Print Decision Statistics")]
    public void PrintDecisionStatistics()
    {
        Debug.Log($"=== CivilianAI {name} Statistics ===");
        Debug.Log($"Total decisions: {totalDecisions}");
        Debug.Log($"Current state: {currentState}");
        Debug.Log($"Last decision path: {string.Join(" -> ", decisionPath)}");
        
        Debug.Log("Node execution counts:");
        foreach (var kvp in nodeExecutionCount.OrderByDescending(x => x.Value))
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value} times");
        }
    }
    
    [ContextMenu("Force Decision")]
    public void ForceDecision()
    {
        MakeDecision();
    }
    
    [ContextMenu("Reset State")]
    public void ResetState()
    {
        SetState(CivilianState.Normal);
        movementController?.Stop();
    }
    
    void OnDrawGizmos()
    {
        // Mostrar estado actual
        Gizmos.color = GetStateColor();
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
        
        // Mostrar rango de detección
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Mostrar último path de decisión en el inspector
        if (showDecisionPath && decisionPath.Count > 0)
        {
            // Esto se podría expandir con una UI más sofisticada
        }
    }
    #endregion
}
```

---

## 🎯 **EJEMPLO DE DECISION TREE COMPLETO**

### **CivilianDecisionTree.asset (ScriptableObject)**
```
Root: IsPlayerVisible
├─ TRUE → IsPlayerClose
│  ├─ TRUE → AreGuardsNearby
│  │  ├─ TRUE → RandomChance(0.7) → AlertGuards
│  │  │  └─ FALSE → Flee
│  │  └─ FALSE → Flee
│  └─ FALSE → HasEscapeRoute
│     ├─ TRUE → WalkToExit
│     └─ FALSE → Idle
└─ FALSE → IsAlertActive
   ├─ TRUE → RandomChance(0.5) → Flee
   │  └─ FALSE → WalkToExit
   └─ FALSE → Idle
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Decision Tree System completo** con nodos modulares
2. **✅ Civilian AI funcionando** como segundo grupo de enemigos
3. **✅ Integración con Blackboard** para decisiones basadas en contexto
4. **✅ Múltiples nodos de condición** y acción
5. **✅ Comportamiento emergente** e impredecible
6. **✅ Debug tools** y estadísticas
7. **✅ Estados visuales** para debugging

### **Testing:**
1. **Civilian Detection**: Debe reaccionar cuando ve al player
2. **Decision Complexity**: Diferentes decisiones según contexto
3. **Blackboard Integration**: Debe reaccionar a alertas globales
4. **Steering Integration**: Acciones deben usar steering behaviors
5. **Personality**: Diferentes tipos deben comportarse diferente

Esta fase crea NPCs inteligentes que toman decisiones complejas basadas en múltiples factores, cumpliendo el requisito de Decision Trees y creando el segundo grupo de enemigos necesario para el TP.