# 🎭 FASE 7: PERSONALIDADES DE GUARDS (Día 9)

## 🎯 **OBJETIVO DE LA FASE**
Implementar completamente las **personalidades diferenciadas** para Guards (Aggressive/Conservative) usando **Roulette Wheel Selection** para crear comportamiento distintivo y cumplir con el requisito obligatorio del TP.

---

## 📋 **¿QUÉ BUSCAMOS LOGRAR?**

### **Problema Actual:**
- Guards tienen personalidades pero comportamiento similar
- Roulette Wheel Selection no está implementado en AI
- Falta diferenciación real en toma de decisiones
- Comportamiento predecible sin variación

### **Solución con Personalidades Completas:**
- **Aggressive Guards**: Riesgo alto, ataque directo, movimiento agresivo
- **Conservative Guards**: Cautela, cobertura, coordinación, defensiva
- **Roulette Wheel Selection** para decisiones probabilísticas
- **Comportamiento emergente** único por personalidad

---

## 🧠 **SISTEMA DE PERSONALIDADES AVANZADO**

### **GuardPersonalitySystem.cs - Manager Central**
```csharp
public class GuardPersonalitySystem : MonoBehaviour
{
    [Header("Personality Configuration")]
    [SerializeField] private List<GuardPersonalityProfile> availablePersonalities;
    [SerializeField] private bool randomizeOnStart = true;
    [SerializeField] private float personalityIntensity = 1f;
    
    [Header("Roulette Wheel Settings")]
    [SerializeField] private bool useAdaptiveWeights = true;
    [SerializeField] private float adaptationRate = 0.1f;
    [SerializeField] private int maxRouletteHistory = 10;
    
    [Header("Global Personality Modifiers")]
    [SerializeField] private GlobalPersonalityModifiers globalModifiers;
    
    private Dictionary<Guard, PersonalityController> guardPersonalities = new Dictionary<Guard, PersonalityController>();
    private IBlackboard blackboard;
    
    void Start()
    {
        blackboard = ServiceLocator.Get<IBlackboard>();
        InitializeGuardPersonalities();
    }
    
    private void InitializeGuardPersonalities()
    {
        var guards = FindObjectsOfType<Guard>();
        
        foreach (var guard in guards)
        {
            AssignPersonalityToGuard(guard);
        }
        
        Logger.LogDebug($"PersonalitySystem: Initialized {guardPersonalities.Count} guard personalities");
    }
    
    public void AssignPersonalityToGuard(Guard guard)
    {
        GuardPersonalityProfile personality;
        
        if (randomizeOnStart)
        {
            personality = SelectRandomPersonality();
        }
        else
        {
            // Use assigned personality from Guard component
            personality = guard.GetAssignedPersonality() ?? GetDefaultPersonality();
        }
        
        var controller = new PersonalityController(guard, personality, globalModifiers);
        guardPersonalities[guard] = controller;
        
        // Apply personality to guard
        controller.ApplyPersonalityToGuard();
        
        Logger.LogDebug($"Assigned {personality.personalityName} to guard {guard.name}");
    }
    
    private GuardPersonalityProfile SelectRandomPersonality()
    {
        if (availablePersonalities == null || availablePersonalities.Count == 0)
        {
            return GetDefaultPersonality();
        }
        
        // Use Roulette Wheel to select personality based on weights
        var weights = availablePersonalities.Select(p => p.selectionWeight).ToArray();
        var roulette = new RouletteWheel<GuardPersonalityProfile>(availablePersonalities.ToArray(), weights);
        
        return roulette.GetRandomItem();
    }
    
    private GuardPersonalityProfile GetDefaultPersonality()
    {
        return availablePersonalities?.FirstOrDefault() ?? 
               ScriptableObject.CreateInstance<StandardGuardPersonality>();
    }
    
    public PersonalityController GetGuardPersonality(Guard guard)
    {
        return guardPersonalities.TryGetValue(guard, out var controller) ? controller : null;
    }
    
    public void UpdateGlobalModifiers(GlobalPersonalityModifiers newModifiers)
    {
        globalModifiers = newModifiers;
        
        // Update all existing personalities
        foreach (var controller in guardPersonalities.Values)
        {
            controller.UpdateGlobalModifiers(globalModifiers);
        }
    }
}

[System.Serializable]
public class GlobalPersonalityModifiers
{
    [Header("Combat Intensity")]
    [Range(0.5f, 2f)] public float globalAggressionMultiplier = 1f;
    [Range(0.5f, 2f)] public float globalCautiousnesMultiplier = 1f;
    
    [Header("Situation Modifiers")]
    [Range(0f, 2f)] public float allyLossStressMultiplier = 1.2f;
    [Range(0f, 2f)] public float playerNearbyTensionMultiplier = 1.1f;
    [Range(0f, 2f)] public float lowHealthDesperationMultiplier = 1.5f;
    
    [Header("Time of Day Effects")]
    [Range(0.5f, 1.5f)] public float nightVisionPenalty = 0.8f;
    [Range(0.5f, 1.5f)] public float dayAlertnessBonsu = 1.1f;
}
```

### **PersonalityController.cs - Control Individual**
```csharp
public class PersonalityController
{
    private Guard guard;
    private GuardPersonalityProfile basePersonality;
    private GlobalPersonalityModifiers globalModifiers;
    
    // Adaptive Roulette Wheels for different decision types
    private RouletteWheel<CombatAction> combatActionRoulette;
    private RouletteWheel<MovementAction> movementActionRoulette;
    private RouletteWheel<TacticalAction> tacticalActionRoulette;
    
    // Decision history for adaptive weights
    private List<ActionResult> recentCombatResults = new List<ActionResult>();
    private List<ActionResult> recentMovementResults = new List<ActionResult>();
    private List<ActionResult> recentTacticalResults = new List<ActionResult>();
    
    // Dynamic personality state
    private float currentStressLevel = 0f;
    private float currentConfidenceLevel = 1f;
    private float currentAggressionModifier = 1f;
    private float currentCautiousnessModifier = 1f;
    
    public PersonalityController(Guard guard, GuardPersonalityProfile personality, GlobalPersonalityModifiers globalMods)
    {
        this.guard = guard;
        this.basePersonality = personality;
        this.globalModifiers = globalMods;
        
        InitializeRouletteWheels();
        CalculateInitialPersonalityState();
    }
    
    private void InitializeRouletteWheels()
    {
        // Combat Actions Roulette Wheel
        var combatActions = new CombatAction[]
        {
            CombatAction.DirectAttack,
            CombatAction.SuppressiveFire,
            CombatAction.TakeCover,
            CombatAction.Reposition,
            CombatAction.ChargePlayer,
            CombatAction.Retreat
        };
        
        var combatWeights = CalculateCombatActionWeights();
        combatActionRoulette = new RouletteWheel<CombatAction>(combatActions, combatWeights);
        
        // Movement Actions Roulette Wheel
        var movementActions = new MovementAction[]
        {
            MovementAction.AggressiveAdvance,
            MovementAction.CautiousApproach,
            MovementAction.FlankingManeuver,
            MovementAction.DefensiveRetreat,
            MovementAction.HoldPosition,
            MovementAction.SeekCover
        };
        
        var movementWeights = CalculateMovementActionWeights();
        movementActionRoulette = new RouletteWheel<MovementAction>(movementActions, movementWeights);
        
        // Tactical Actions Roulette Wheel
        var tacticalActions = new TacticalAction[]
        {
            TacticalAction.CallForHelp,
            TacticalAction.CoordinateAttack,
            TacticalAction.AlertOthers,
            TacticalAction.FocusFire,
            TacticalAction.CreateDistraction,
            TacticalAction.SetupAmbush
        };
        
        var tacticalWeights = CalculateTacticalActionWeights();
        tacticalActionRoulette = new RouletteWheel<TacticalAction>(tacticalActions, tacticalWeights);
    }
    
    private float[] CalculateCombatActionWeights()
    {
        var modifiers = basePersonality.GetBehaviorModifiers();
        float aggression = modifiers.aggressionLevel * currentAggressionModifier * globalModifiers.globalAggressionMultiplier;
        float caution = modifiers.cautiousness * currentCautiousnessModifier * globalModifiers.globalCautiousnesMultiplier;
        
        return new float[]
        {
            aggression * 2f,           // DirectAttack
            aggression * 1.2f,         // SuppressiveFire
            caution * 2f,              // TakeCover
            (aggression + caution),    // Reposition
            aggression * 1.8f,         // ChargePlayer
            caution * 1.5f             // Retreat
        };
    }
    
    private float[] CalculateMovementActionWeights()
    {
        var modifiers = basePersonality.GetBehaviorModifiers();
        float aggression = modifiers.aggressionLevel * currentAggressionModifier;
        float caution = modifiers.cautiousness * currentCautiousnessModifier;
        float confidence = modifiers.confidence * currentConfidenceLevel;
        
        return new float[]
        {
            aggression * confidence * 1.5f,    // AggressiveAdvance
            caution * 1.8f,                    // CautiousApproach
            aggression * confidence,           // FlankingManeuver
            caution * (2f - confidence),       // DefensiveRetreat
            caution * confidence,              // HoldPosition
            caution * 2f                       // SeekCover
        };
    }
    
    private float[] CalculateTacticalActionWeights()
    {
        var modifiers = basePersonality.GetBehaviorModifiers();
        float cooperation = modifiers.cooperation;
        float confidence = modifiers.confidence * currentConfidenceLevel;
        float stress = currentStressLevel;
        
        return new float[]
        {
            (1f - confidence + stress) * cooperation,  // CallForHelp
            confidence * cooperation * 1.5f,           // CoordinateAttack
            cooperation * 1.2f,                        // AlertOthers
            cooperation * confidence,                   // FocusFire
            modifiers.aggressionLevel * confidence,     // CreateDistraction
            modifiers.cautiousness * cooperation        // SetupAmbush
        };
    }
    
    public CombatAction SelectCombatAction()
    {
        UpdatePersonalityState();
        UpdateRouletteWeights();
        
        var action = combatActionRoulette.GetRandomItem();
        
        LogPersonalityDecision($"Selected combat action: {action}");
        
        return action;
    }
    
    public MovementAction SelectMovementAction()
    {
        UpdatePersonalityState();
        UpdateRouletteWeights();
        
        var action = movementActionRoulette.GetRandomItem();
        
        LogPersonalityDecision($"Selected movement action: {action}");
        
        return action;
    }
    
    public TacticalAction SelectTacticalAction()
    {
        UpdatePersonalityState();
        UpdateRouletteWeights();
        
        var action = tacticalActionRoulette.GetRandomItem();
        
        LogPersonalityDecision($"Selected tactical action: {action}");
        
        return action;
    }
    
    private void UpdatePersonalityState()
    {
        // Calculate stress based on current situation
        CalculateStressLevel();
        
        // Calculate confidence based on recent success
        CalculateConfidenceLevel();
        
        // Update dynamic modifiers
        UpdateDynamicModifiers();
    }
    
    private void CalculateStressLevel()
    {
        float baseStress = 0f;
        
        // Health-based stress
        float healthPercentage = guard.Health / guard.MaxHealth;
        baseStress += (1f - healthPercentage) * 0.6f;
        
        // Enemy proximity stress
        float distanceToPlayer = guard.GetDistanceToPlayer();
        if (distanceToPlayer < 5f)
        {
            baseStress += (5f - distanceToPlayer) / 5f * 0.4f;
        }
        
        // Ally loss stress
        int deadAllies = GetDeadAlliesCount();
        baseStress += deadAllies * 0.2f;
        
        // Global situation stress
        var alertLevel = ServiceLocator.Get<IBlackboard>()?.GetValue<int>(BlackboardKeys.ALERT_LEVEL) ?? 0;
        baseStress += alertLevel * 0.1f;
        
        // Apply global modifiers
        baseStress *= globalModifiers.allyLossStressMultiplier;
        if (distanceToPlayer < 8f)
        {
            baseStress *= globalModifiers.playerNearbyTensionMultiplier;
        }
        if (healthPercentage < 0.3f)
        {
            baseStress *= globalModifiers.lowHealthDesperationMultiplier;
        }
        
        currentStressLevel = Mathf.Clamp01(baseStress);
    }
    
    private void CalculateConfidenceLevel()
    {
        float baseConfidence = basePersonality.GetBehaviorModifiers().confidence;
        
        // Success/failure history
        float recentSuccessRate = CalculateRecentSuccessRate();
        baseConfidence = Mathf.Lerp(baseConfidence, recentSuccessRate, 0.3f);
        
        // Ally support bonus
        int nearbyAllies = GetNearbyAlliesCount();
        baseConfidence += nearbyAllies * 0.1f;
        
        // Stress penalty
        baseConfidence -= currentStressLevel * 0.3f;
        
        currentConfidenceLevel = Mathf.Clamp01(baseConfidence);
    }
    
    private void UpdateDynamicModifiers()
    {
        var baseModifiers = basePersonality.GetBehaviorModifiers();
        
        // Stress increases aggression for aggressive personalities, decreases it for conservative
        if (baseModifiers.aggressionLevel > 1f)
        {
            currentAggressionModifier = 1f + currentStressLevel * 0.5f; // More aggressive when stressed
        }
        else
        {
            currentAggressionModifier = 1f - currentStressLevel * 0.3f; // Less aggressive when stressed
        }
        
        // Low confidence increases cautiousness
        currentCautiousnessModifier = 1f + (1f - currentConfidenceLevel) * 0.4f;
        
        // Clamp modifiers
        currentAggressionModifier = Mathf.Clamp(currentAggressionModifier, 0.3f, 2f);
        currentCautiousnessModifier = Mathf.Clamp(currentCautiousnessModifier, 0.5f, 2f);
    }
    
    private float CalculateRecentSuccessRate()
    {
        var allResults = new List<ActionResult>();
        allResults.AddRange(recentCombatResults);
        allResults.AddRange(recentMovementResults);
        allResults.AddRange(recentTacticalResults);
        
        if (allResults.Count == 0) return 0.5f; // Neutral if no history
        
        float successCount = allResults.Count(r => r.wasSuccessful);
        return successCount / allResults.Count;
    }
    
    private void UpdateRouletteWeights()
    {
        // Update combat weights
        var newCombatWeights = CalculateCombatActionWeights();
        combatActionRoulette.UpdateWeights(newCombatWeights);
        
        // Update movement weights
        var newMovementWeights = CalculateMovementActionWeights();
        movementActionRoulette.UpdateWeights(newMovementWeights);
        
        // Update tactical weights
        var newTacticalWeights = CalculateTacticalActionWeights();
        tacticalActionRoulette.UpdateWeights(newTacticalWeights);
    }
    
    public void RecordActionResult(ActionType actionType, ActionResult result)
    {
        switch (actionType)
        {
            case ActionType.Combat:
                recentCombatResults.Add(result);
                if (recentCombatResults.Count > 10) recentCombatResults.RemoveAt(0);
                break;
                
            case ActionType.Movement:
                recentMovementResults.Add(result);
                if (recentMovementResults.Count > 10) recentMovementResults.RemoveAt(0);
                break;
                
            case ActionType.Tactical:
                recentTacticalResults.Add(result);
                if (recentTacticalResults.Count > 10) recentTacticalResults.RemoveAt(0);
                break;
        }
        
        LogPersonalityDecision($"Recorded {actionType} result: {(result.wasSuccessful ? "Success" : "Failure")}");
    }
    
    public void ApplyPersonalityToGuard()
    {
        var modifiers = basePersonality.GetBehaviorModifiers();
        
        // Apply base stats modifications
        guard.SetPersonalityModifiers(modifiers);
        
        // Set up roulette wheels
        guard.SetPersonalityController(this);
        
        LogPersonalityDecision($"Applied personality {basePersonality.personalityName} to guard {guard.name}");
    }
    
    public void UpdateGlobalModifiers(GlobalPersonalityModifiers newModifiers)
    {
        globalModifiers = newModifiers;
        UpdateRouletteWeights();
    }
    
    private int GetNearbyAlliesCount()
    {
        var guards = UnityEngine.Object.FindObjectsOfType<Guard>();
        int count = 0;
        
        foreach (var g in guards)
        {
            if (g != guard && g.IsAlive)
            {
                float distance = Vector3.Distance(guard.transform.position, g.transform.position);
                if (distance <= 15f) count++;
            }
        }
        
        return count;
    }
    
    private int GetDeadAlliesCount()
    {
        var guards = UnityEngine.Object.FindObjectsOfType<Guard>();
        return guards.Count(g => g != guard && !g.IsAlive);
    }
    
    private void LogPersonalityDecision(string message)
    {
        Logger.LogDebug($"Personality[{basePersonality.personalityName}] {guard.name}: {message}");
    }
    
    public PersonalityStats GetCurrentStats()
    {
        return new PersonalityStats
        {
            personalityName = basePersonality.personalityName,
            stressLevel = currentStressLevel,
            confidenceLevel = currentConfidenceLevel,
            aggressionModifier = currentAggressionModifier,
            cautiousnessModifier = currentCautiousnessModifier,
            recentSuccessRate = CalculateRecentSuccessRate(),
            combatActionsCount = recentCombatResults.Count,
            movementActionsCount = recentMovementResults.Count,
            tacticalActionsCount = recentTacticalResults.Count
        };
    }
}

// Supporting structs and enums
public enum CombatAction
{
    DirectAttack,
    SuppressiveFire,
    TakeCover,
    Reposition,
    ChargePlayer,
    Retreat
}

public enum MovementAction
{
    AggressiveAdvance,
    CautiousApproach,
    FlankingManeuver,
    DefensiveRetreat,
    HoldPosition,
    SeekCover
}

public enum TacticalAction
{
    CallForHelp,
    CoordinateAttack,
    AlertOthers,
    FocusFire,
    CreateDistraction,
    SetupAmbush
}

public enum ActionType
{
    Combat,
    Movement,
    Tactical
}

[System.Serializable]
public struct ActionResult
{
    public bool wasSuccessful;
    public float timestamp;
    public string actionName;
    public float effectivenessScore; // 0-1 range
}

[System.Serializable]
public struct PersonalityStats
{
    public string personalityName;
    public float stressLevel;
    public float confidenceLevel;
    public float aggressionModifier;
    public float cautiousnessModifier;
    public float recentSuccessRate;
    public int combatActionsCount;
    public int movementActionsCount;
    public int tacticalActionsCount;
}
```

---

## 🎯 **PERSONALIDADES ESPECÍFICAS DETALLADAS**

### **AggressiveGuardPersonality.cs - Versión Completa**
```csharp
[CreateAssetMenu(fileName = "AggressiveGuardPersonality", menuName = "AI/Guard Personalities/Aggressive")]
public class AggressiveGuardPersonality : GuardPersonalityProfile
{
    [Header("Aggressive Specific Settings")]
    [SerializeField] private float berserkerThreshold = 0.2f; // Health percentage to go berserk
    [SerializeField] private float recklessnessBonus = 0.3f;
    [SerializeField] private float chargeDistance = 8f;
    
    protected override void InitializePersonality()
    {
        personalityName = "Aggressive Berserker";
        description = "High-risk, high-reward fighter. Becomes more dangerous when wounded. " +
                     "Prefers direct confrontation and charging tactics.";
        
        personalityType = AIPersonalityType.Aggressive;
        selectionWeight = 1f;
        
        // Base aggressive stats
        aggressionLevel = 1.9f;
        cautiousness = 0.4f;
        confidence = 1.7f;
        cooperation = 0.7f;
        
        // Combat modifiers favor speed and offense
        attackCooldownMultiplier = 0.6f;    // Attacks 40% faster
        moveSpeedMultiplier = 1.4f;         // Moves 40% faster
        detectionRangeMultiplier = 1.1f;    // Slightly better detection
        combatDistanceMultiplier = 0.7f;    // Prefers close combat
        
        // Risk tolerance
        helpCallThreshold = 0.15f;          // Only calls for help when critically wounded
        riskTolerance = 1.9f;               // Very high risk tolerance
        aggressionBonus = 1.6f;             // High aggression bonus
    }
    
    public override GuardBehaviorModifiers GetBehaviorModifiers()
    {
        var modifiers = base.GetBehaviorModifiers();
        
        // Dynamic modifiers based on health (berserker mode)
        if (GetOwnerGuard() != null)
        {
            float healthPercentage = GetOwnerGuard().Health / GetOwnerGuard().MaxHealth;
            
            if (healthPercentage <= berserkerThreshold)
            {
                // Berserker mode - becomes even more aggressive when wounded
                modifiers.aggressionLevel *= 1.5f;
                modifiers.attackCooldownMultiplier *= 0.7f;
                modifiers.moveSpeedMultiplier *= 1.3f;
                modifiers.riskTolerance *= 1.4f;
            }
        }
        
        return modifiers;
    }
    
    public override ActionPreferences GetActionPreferences()
    {
        return new ActionPreferences
        {
            // Strongly prefer direct, aggressive actions
            combatPreferences = new Dictionary<CombatAction, float>
            {
                { CombatAction.DirectAttack, 3f },
                { CombatAction.ChargePlayer, 2.5f },
                { CombatAction.SuppressiveFire, 1.5f },
                { CombatAction.Reposition, 1f },
                { CombatAction.TakeCover, 0.3f },
                { CombatAction.Retreat, 0.1f }
            },
            
            movementPreferences = new Dictionary<MovementAction, float>
            {
                { MovementAction.AggressiveAdvance, 3f },
                { MovementAction.FlankingManeuver, 2f },
                { MovementAction.CautiousApproach, 0.5f },
                { MovementAction.HoldPosition, 0.3f },
                { MovementAction.SeekCover, 0.2f },
                { MovementAction.DefensiveRetreat, 0.1f }
            },
            
            tacticalPreferences = new Dictionary<TacticalAction, float>
            {
                { TacticalAction.CoordinateAttack, 2f },
                { TacticalAction.FocusFire, 1.8f },
                { TacticalAction.CreateDistraction, 1.5f },
                { TacticalAction.AlertOthers, 1f },
                { TacticalAction.CallForHelp, 0.3f },
                { TacticalAction.SetupAmbush, 0.2f }
            }
        };
    }
    
    public override float CalculateActionSuccessProbability(string actionName, float baseSuccessRate)
    {
        // Aggressive personalities have higher success rates for offensive actions
        switch (actionName)
        {
            case "DirectAttack":
            case "ChargePlayer":
            case "AggressiveAdvance":
                return baseSuccessRate * 1.3f; // 30% bonus for aggressive actions
                
            case "TakeCover":
            case "DefensiveRetreat":
            case "CautiousApproach":
                return baseSuccessRate * 0.8f; // 20% penalty for defensive actions
                
            default:
                return baseSuccessRate;
        }
    }
    
    public override SpecialAbility GetSpecialAbility()
    {
        return new SpecialAbility
        {
            name = "Berserker Rage",
            description = "When health drops below 20%, attack speed and movement speed increase dramatically",
            cooldown = 0f, // Passive ability
            isPassive = true,
            triggerCondition = SpecialAbilityTrigger.LowHealth
        };
    }
}
```

### **ConservativeGuardPersonality.cs - Versión Completa**
```csharp
[CreateAssetMenu(fileName = "ConservativeGuardPersonality", menuName = "AI/Guard Personalities/Conservative")]
public class ConservativeGuardPersonality : GuardPersonalityProfile
{
    [Header("Conservative Specific Settings")]
    [SerializeField] private float coverBonus = 0.5f;
    [SerializeField] private float coordinationBonus = 0.4f;
    [SerializeField] private float optimalCombatRange = 12f;
    
    protected override void InitializePersonality()
    {
        personalityName = "Conservative Tactician";
        description = "Defensive specialist who uses cover, coordinates with allies, " +
                     "and prefers strategic positioning over direct assault.";
        
        personalityType = AIPersonalityType.Conservative;
        selectionWeight = 1f;
        
        // Base conservative stats
        aggressionLevel = 0.6f;
        cautiousness = 1.9f;
        confidence = 1.1f;
        cooperation = 1.8f;
        
        // Combat modifiers favor safety and precision
        attackCooldownMultiplier = 1.3f;    // Attacks slower but more precisely
        moveSpeedMultiplier = 0.9f;         // Moves more carefully
        detectionRangeMultiplier = 1.4f;    // Better detection range
        combatDistanceMultiplier = 1.5f;    // Prefers longer range combat
        
        // Safety focus
        helpCallThreshold = 0.6f;           // Calls for help earlier
        riskTolerance = 0.5f;               // Low risk tolerance
        aggressionBonus = 0.7f;             // Lower aggression bonus
    }
    
    public override GuardBehaviorModifiers GetBehaviorModifiers()
    {
        var modifiers = base.GetBehaviorModifiers();
        
        // Bonus when near cover
        if (GetOwnerGuard() != null && IsNearCover())
        {
            modifiers.confidence += coverBonus;
            modifiers.aggressionLevel += coverBonus * 0.5f;
        }
        
        // Bonus when near allies
        if (GetOwnerGuard() != null && GetNearbyAlliesCount() > 0)
        {
            modifiers.confidence += coordinationBonus;
            modifiers.cooperation += coordinationBonus;
        }
        
        return modifiers;
    }
    
    public override ActionPreferences GetActionPreferences()
    {
        return new ActionPreferences
        {
            // Prefer defensive and coordinated actions
            combatPreferences = new Dictionary<CombatAction, float>
            {
                { CombatAction.TakeCover, 3f },
                { CombatAction.SuppressiveFire, 2.5f },
                { CombatAction.Reposition, 2f },
                { CombatAction.DirectAttack, 1.2f },
                { CombatAction.Retreat, 1.5f },
                { CombatAction.ChargePlayer, 0.2f }
            },
            
            movementPreferences = new Dictionary<MovementAction, float>
            {
                { MovementAction.SeekCover, 3f },
                { MovementAction.CautiousApproach, 2.5f },
                { MovementAction.HoldPosition, 2f },
                { MovementAction.FlankingManeuver, 1.5f },
                { MovementAction.DefensiveRetreat, 2f },
                { MovementAction.AggressiveAdvance, 0.3f }
            },
            
            tacticalPreferences = new Dictionary<TacticalAction, float>
            {
                { TacticalAction.CallForHelp, 2.5f },
                { TacticalAction.CoordinateAttack, 3f },
                { TacticalAction.SetupAmbush, 2.5f },
                { TacticalAction.AlertOthers, 2f },
                { TacticalAction.FocusFire, 2f },
                { TacticalAction.CreateDistraction, 1f }
            }
        };
    }
    
    public override float CalculateActionSuccessProbability(string actionName, float baseSuccessRate)
    {
        // Conservative personalities excel at defensive and coordinated actions
        switch (actionName)
        {
            case "TakeCover":
            case "SeekCover":
            case "CautiousApproach":
            case "CallForHelp":
            case "CoordinateAttack":
                return baseSuccessRate * 1.4f; // 40% bonus for defensive/coordinated actions
                
            case "ChargePlayer":
            case "AggressiveAdvance":
            case "DirectAttack":
                return baseSuccessRate * 0.7f; // 30% penalty for aggressive actions
                
            case "SetupAmbush":
            case "SuppressiveFire":
                return baseSuccessRate * 1.2f; // 20% bonus for tactical actions
                
            default:
                return baseSuccessRate;
        }
    }
    
    public override SpecialAbility GetSpecialAbility()
    {
        return new SpecialAbility
        {
            name = "Tactical Coordination",
            description = "Can coordinate attacks with nearby allies for increased effectiveness",
            cooldown = 15f,
            isPassive = false,
            triggerCondition = SpecialAbilityTrigger.AlliesNearby
        };
    }
    
    private bool IsNearCover()
    {
        var coverPoints = UnityEngine.Object.FindObjectsOfType<CoverPoint>();
        var guardPosition = GetOwnerGuard().transform.position;
        
        return coverPoints.Any(cover => 
            Vector3.Distance(guardPosition, cover.transform.position) <= 3f);
    }
    
    private int GetNearbyAlliesCount()
    {
        var guards = UnityEngine.Object.FindObjectsOfType<Guard>();
        var guardPosition = GetOwnerGuard().transform.position;
        
        return guards.Count(g => 
            g != GetOwnerGuard() && 
            g.IsAlive && 
            Vector3.Distance(guardPosition, g.transform.position) <= 10f);
    }
}
```

---

## 🎰 **ROULETTE WHEEL SELECTION AVANZADO**

### **AdaptiveRouletteWheel.cs - Implementación Mejorada**
```csharp
public class AdaptiveRouletteWheel<T> : RouletteWheel<T>
{
    private Dictionary<T, float> baseWeights;
    private Dictionary<T, float> adaptiveWeights;
    private Dictionary<T, ActionHistory> actionHistories;
    private float adaptationRate;
    private int maxHistorySize;
    
    public AdaptiveRouletteWheel(T[] items, float[] weights, float adaptationRate = 0.1f, int maxHistorySize = 20) 
        : base(items, weights)
    {
        this.adaptationRate = adaptationRate;
        this.maxHistorySize = maxHistorySize;
        
        InitializeAdaptiveSystem(items, weights);
    }
    
    private void InitializeAdaptiveSystem(T[] items, float[] weights)
    {
        baseWeights = new Dictionary<T, float>();
        adaptiveWeights = new Dictionary<T, float>();
        actionHistories = new Dictionary<T, ActionHistory>();
        
        for (int i = 0; i < items.Length; i++)
        {
            baseWeights[items[i]] = weights[i];
            adaptiveWeights[items[i]] = weights[i];
            actionHistories[items[i]] = new ActionHistory(maxHistorySize);
        }
    }
    
    public override T GetRandomItem()
    {
        UpdateAdaptiveWeights();
        
        // Use adaptive weights for selection
        var currentWeights = adaptiveWeights.Values.ToArray();
        var weightedItems = adaptiveWeights.Keys.ToArray();
        
        float totalWeight = currentWeights.Sum();
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        
        float currentSum = 0f;
        for (int i = 0; i < weightedItems.Length; i++)
        {
            currentSum += currentWeights[i];
            if (randomValue <= currentSum)
            {
                T selectedItem = weightedItems[i];
                RecordSelection(selectedItem);
                return selectedItem;
            }
        }
        
        // Fallback
        return weightedItems[weightedItems.Length - 1];
    }
    
    public void RecordActionResult(T action, bool wasSuccessful, float effectivenessScore = 1f)
    {
        if (actionHistories.ContainsKey(action))
        {
            actionHistories[action].AddResult(wasSuccessful, effectivenessScore);
        }
    }
    
    private void UpdateAdaptiveWeights()
    {
        foreach (var kvp in baseWeights)
        {
            T action = kvp.Key;
            float baseWeight = kvp.Value;
            
            var history = actionHistories[action];
            float successRate = history.GetSuccessRate();
            float averageEffectiveness = history.GetAverageEffectiveness();
            
            // Calculate adaptive modifier
            float adaptiveModifier = CalculateAdaptiveModifier(successRate, averageEffectiveness);
            
            // Apply adaptation with smoothing
            float currentAdaptiveWeight = adaptiveWeights[action];
            float targetWeight = baseWeight * adaptiveModifier;
            
            adaptiveWeights[action] = Mathf.Lerp(currentAdaptiveWeight, targetWeight, adaptationRate);
            
            // Ensure minimum weight
            adaptiveWeights[action] = Mathf.Max(adaptiveWeights[action], baseWeight * 0.1f);
        }
    }
    
    private float CalculateAdaptiveModifier(float successRate, float effectiveness)
    {
        // Base modifier from success rate (0.5 = neutral, >0.5 = bonus, <0.5 = penalty)
        float successModifier = Mathf.Lerp(0.5f, 1.5f, successRate);
        
        // Effectiveness modifier (0-1 effectiveness maps to 0.8-1.2 modifier)
        float effectivenessModifier = Mathf.Lerp(0.8f, 1.2f, effectiveness);
        
        return successModifier * effectivenessModifier;
    }
    
    private void RecordSelection(T selectedItem)
    {
        if (actionHistories.ContainsKey(selectedItem))
        {
            actionHistories[selectedItem].RecordSelection();
        }
    }
    
    public Dictionary<T, float> GetCurrentWeights()
    {
        return new Dictionary<T, float>(adaptiveWeights);
    }
    
    public Dictionary<T, ActionStats> GetActionStatistics()
    {
        var stats = new Dictionary<T, ActionStats>();
        
        foreach (var kvp in actionHistories)
        {
            var history = kvp.Value;
            stats[kvp.Key] = new ActionStats
            {
                selectionCount = history.GetSelectionCount(),
                successRate = history.GetSuccessRate(),
                averageEffectiveness = history.GetAverageEffectiveness(),
                currentWeight = adaptiveWeights[kvp.Key],
                baseWeight = baseWeights[kvp.Key]
            };
        }
        
        return stats;
    }
    
    public void ResetAdaptation()
    {
        foreach (var action in baseWeights.Keys)
        {
            adaptiveWeights[action] = baseWeights[action];
            actionHistories[action].Clear();
        }
    }
}

public class ActionHistory
{
    private Queue<ActionResult> results;
    private int selectionCount;
    private int maxSize;
    
    public ActionHistory(int maxSize)
    {
        this.maxSize = maxSize;
        results = new Queue<ActionResult>();
        selectionCount = 0;
    }
    
    public void AddResult(bool wasSuccessful, float effectiveness)
    {
        var result = new ActionResult
        {
            wasSuccessful = wasSuccessful,
            effectivenessScore = effectiveness,
            timestamp = Time.time
        };
        
        results.Enqueue(result);
        
        if (results.Count > maxSize)
        {
            results.Dequeue();
        }
    }
    
    public void RecordSelection()
    {
        selectionCount++;
    }
    
    public float GetSuccessRate()
    {
        if (results.Count == 0) return 0.5f; // Neutral if no data
        
        int successCount = results.Count(r => r.wasSuccessful);
        return (float)successCount / results.Count;
    }
    
    public float GetAverageEffectiveness()
    {
        if (results.Count == 0) return 0.5f; // Neutral if no data
        
        return results.Average(r => r.effectivenessScore);
    }
    
    public int GetSelectionCount()
    {
        return selectionCount;
    }
    
    public void Clear()
    {
        results.Clear();
        selectionCount = 0;
    }
}

[System.Serializable]
public struct ActionStats
{
    public int selectionCount;
    public float successRate;
    public float averageEffectiveness;
    public float currentWeight;
    public float baseWeight;
}
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Personalidades diferenciadas** con comportamiento real distintivo
2. **✅ Roulette Wheel Selection** funcionando para toma de decisiones
3. **✅ Sistema adaptativo** que aprende de éxitos/fallos
4. **✅ Aggressive vs Conservative** con diferencias marcadas
5. **✅ Estados dinámicos** (stress, confidence) que afectan decisiones
6. **✅ Coordinación personalizada** según tipo de guard
7. **✅ Debugging completo** y estadísticas detalladas

### **Testing:**
1. **Personality Distinctiveness**: Aggressive guards deben ser notablemente más agresivos
2. **Roulette Adaptation**: Weights deben cambiar basándose en éxito de acciones
3. **Dynamic Behavior**: Stress y confidence deben afectar decisiones
4. **Combat Differences**: Tipos diferentes deben usar tácticas diferentes
5. **Statistical Tracking**: Sistema debe mantener estadísticas precisas

Esta fase cumple completamente el requisito de **Roulette Wheel Selection** del TP y crea personalidades verdaderamente distintivas que evolucionan durante el juego.