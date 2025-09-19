# ✨ FASE 9: POLISH Y FINALIZACIÓN (Día 11)

## 🎯 **OBJETIVO DE LA FASE**
Realizar el **polish final** del sistema de AI, optimizar rendimiento, mejorar la experiencia visual, crear documentación de presentación y asegurar que el proyecto esté **listo para entrega del TP**.

---

## 📋 **¿QUÉ BUSCAMOS LOGRAR?**

### **Problema Actual:**
- Sistema funcional pero necesita optimización
- Experiencia visual puede mejorarse
- Falta documentación para presentación
- Necesita ajustes finales para demostración

### **Solución con Polish Completo:**
- **Optimización de rendimiento** para smooth gameplay
- **Mejoras visuales** y efectos de polish
- **Documentación profesional** para presentación
- **Demo scene** perfecta para mostrar todos los features
- **Video/screenshots** para portfolio

---

## 🚀 **OPTIMIZACIÓN DE RENDIMIENTO**

### **AIPerformanceOptimizer.cs - Optimización Central**
```csharp
public class AIPerformanceOptimizer : MonoBehaviour
{
    [Header("Performance Settings")]
    [SerializeField] private AIPerformanceProfile performanceProfile;
    [SerializeField] private bool enableDynamicOptimization = true;
    [SerializeField] private bool enablePerformanceMonitoring = true;
    [SerializeField] private float performanceTargetFPS = 60f;
    
    [Header("LOD Settings")]
    [SerializeField] private float nearDistance = 15f;
    [SerializeField] private float mediumDistance = 30f;
    [SerializeField] private float farDistance = 50f;
    [SerializeField] private bool enableCulling = true;
    
    [Header("Update Frequency Optimization")]
    [SerializeField] private float baseUpdateInterval = 0.1f;
    [SerializeField] private float maxUpdateInterval = 1f;
    [SerializeField] private int maxActiveAI = 20;
    
    // Performance monitoring
    private PerformanceMonitor performanceMonitor;
    private Dictionary<AIEntity, AIPerformanceData> aiPerformanceData = new Dictionary<AIEntity, AIPerformanceData>();
    
    // Dynamic optimization
    private float currentPerformanceScore = 1f;
    private List<OptimizationStrategy> activeOptimizations = new List<OptimizationStrategy>();
    
    // Update management
    private AIUpdateScheduler updateScheduler;
    private Dictionary<AIEntity, float> nextUpdateTimes = new Dictionary<AIEntity, float>();
    
    void Start()
    {
        InitializeOptimization();
    }
    
    private void InitializeOptimization()
    {
        performanceMonitor = new PerformanceMonitor(performanceTargetFPS);
        updateScheduler = new AIUpdateScheduler(maxActiveAI);
        
        // Register all AI entities
        RegisterAllAIEntities();
        
        // Apply initial optimizations
        ApplyPerformanceProfile();
        
        Logger.LogInfo("AIPerformanceOptimizer: Initialization complete");
    }
    
    void Update()
    {
        if (enablePerformanceMonitoring)
        {
            UpdatePerformanceMonitoring();
        }
        
        if (enableDynamicOptimization)
        {
            UpdateDynamicOptimization();
        }
        
        ManageAIUpdates();
    }
    
    private void UpdatePerformanceMonitoring()
    {
        performanceMonitor.Update();
        currentPerformanceScore = performanceMonitor.GetPerformanceScore();
        
        // Update individual AI performance data
        foreach (var kvp in aiPerformanceData.ToList())
        {
            var aiEntity = kvp.Key;
            var data = kvp.Value;
            
            if (aiEntity == null)
            {
                aiPerformanceData.Remove(aiEntity);
                continue;
            }
            
            data.Update(aiEntity);
        }
    }
    
    private void UpdateDynamicOptimization()
    {
        if (currentPerformanceScore < 0.8f) // Performance below 80%
        {
            ApplyPerformanceOptimizations();
        }
        else if (currentPerformanceScore > 0.95f) // Performance very good
        {
            RelaxOptimizations();
        }
    }
    
    private void ApplyPerformanceOptimizations()
    {
        Logger.LogDebug($"Applying performance optimizations (score: {currentPerformanceScore:F2})");
        
        // Reduce update frequencies
        if (!activeOptimizations.Contains(OptimizationStrategy.ReducedUpdateFrequency))
        {
            ReduceUpdateFrequencies();
            activeOptimizations.Add(OptimizationStrategy.ReducedUpdateFrequency);
        }
        
        // Enable aggressive culling
        if (!activeOptimizations.Contains(OptimizationStrategy.AggressiveCulling))
        {
            EnableAggressiveCulling();
            activeOptimizations.Add(OptimizationStrategy.AggressiveCulling);
        }
        
        // Simplify AI behaviors for distant entities
        if (!activeOptimizations.Contains(OptimizationStrategy.BehaviorLOD))
        {
            ApplyBehaviorLOD();
            activeOptimizations.Add(OptimizationStrategy.BehaviorLOD);
        }
        
        // Reduce visual effects
        if (!activeOptimizations.Contains(OptimizationStrategy.ReducedEffects))
        {
            ReduceVisualEffects();
            activeOptimizations.Add(OptimizationStrategy.ReducedEffects);
        }
    }
    
    private void RelaxOptimizations()
    {
        if (activeOptimizations.Count > 0)
        {
            Logger.LogDebug("Relaxing optimizations due to good performance");
            
            // Gradually remove optimizations
            var strategyToRemove = activeOptimizations[0];
            RemoveOptimizationStrategy(strategyToRemove);
            activeOptimizations.RemoveAt(0);
        }
    }
    
    private void ReduceUpdateFrequencies()
    {
        foreach (var kvp in aiPerformanceData)
        {
            var aiEntity = kvp.Key;
            var data = kvp.Value;
            
            // Increase update interval based on distance and importance
            float newInterval = CalculateOptimizedUpdateInterval(aiEntity, data);
            aiEntity.SetUpdateInterval(newInterval);
        }
    }
    
    private float CalculateOptimizedUpdateInterval(AIEntity entity, AIPerformanceData data)
    {
        float baseInterval = baseUpdateInterval;
        
        // Distance-based scaling
        float distance = data.distanceToPlayer;
        if (distance > farDistance)
        {
            baseInterval *= 4f; // Very far entities update 4x slower
        }
        else if (distance > mediumDistance)
        {
            baseInterval *= 2f; // Medium distance entities update 2x slower
        }
        else if (distance > nearDistance)
        {
            baseInterval *= 1.5f; // Near entities update 1.5x slower
        }
        
        // Importance-based scaling
        if (entity.GetImportanceLevel() == AIImportanceLevel.Critical)
        {
            baseInterval *= 0.5f; // Critical entities update more frequently
        }
        else if (entity.GetImportanceLevel() == AIImportanceLevel.Low)
        {
            baseInterval *= 2f; // Low importance entities update less frequently
        }
        
        // State-based scaling
        if (entity.IsInCombat())
        {
            baseInterval *= 0.3f; // Combat entities need frequent updates
        }
        else if (entity.IsIdle())
        {
            baseInterval *= 3f; // Idle entities can update slowly
        }
        
        return Mathf.Clamp(baseInterval, baseUpdateInterval, maxUpdateInterval);
    }
    
    private void EnableAggressiveCulling()
    {
        var aiEntities = FindObjectsOfType<AIEntity>();
        foreach (var entity in aiEntities)
        {
            var data = aiPerformanceData[entity];
            
            // Cull very distant entities
            if (data.distanceToPlayer > farDistance * 1.5f)
            {
                entity.EnableCulling(true);
            }
            
            // Reduce visual components for medium distance
            if (data.distanceToPlayer > mediumDistance)
            {
                entity.ReduceVisualComplexity();
            }
        }
    }
    
    private void ApplyBehaviorLOD()
    {
        var guards = FindObjectsOfType<Guard>();
        var civilians = FindObjectsOfType<CivilianAI>();
        
        foreach (var guard in guards)
        {
            var data = aiPerformanceData[guard as AIEntity];
            ApplyGuardBehaviorLOD(guard, data.distanceToPlayer);
        }
        
        foreach (var civilian in civilians)
        {
            var data = aiPerformanceData[civilian as AIEntity];
            ApplyCivilianBehaviorLOD(civilian, data.distanceToPlayer);
        }
    }
    
    private void ApplyGuardBehaviorLOD(Guard guard, float distance)
    {
        if (distance > farDistance)
        {
            // Far LOD: Only basic patrol, disable complex behaviors
            guard.SetBehaviorLOD(AIBehaviorLOD.Far);
            guard.DisableSteeringBehavior(typeof(ObstacleAvoidanceBehavior));
            guard.DisablePersonalityDecisions();
        }
        else if (distance > mediumDistance)
        {
            // Medium LOD: Reduced steering complexity
            guard.SetBehaviorLOD(AIBehaviorLOD.Medium);
            guard.ReduceSteeringComplexity();
        }
        else
        {
            // Near LOD: Full behavior
            guard.SetBehaviorLOD(AIBehaviorLOD.Near);
            guard.EnableFullBehavior();
        }
    }
    
    private void ApplyCivilianBehaviorLOD(CivilianAI civilian, float distance)
    {
        if (distance > farDistance)
        {
            // Far LOD: Minimal decision making
            civilian.SetDecisionInterval(1f);
            civilian.DisableComplexBehaviors();
        }
        else if (distance > mediumDistance)
        {
            // Medium LOD: Reduced decision frequency
            civilian.SetDecisionInterval(0.8f);
        }
        else
        {
            // Near LOD: Full behavior
            civilian.SetDecisionInterval(0.5f);
            civilian.EnableFullBehavior();
        }
    }
    
    private void ReduceVisualEffects()
    {
        var effectSystems = FindObjectsOfType<ParticleSystem>();
        foreach (var effect in effectSystems)
        {
            var main = effect.main;
            main.maxParticles = Mathf.Max(main.maxParticles / 2, 10);
        }
        
        // Reduce quality of other visual effects
        QualitySettings.shadowDistance *= 0.7f;
        QualitySettings.lodBias *= 0.8f;
    }
    
    private void RemoveOptimizationStrategy(OptimizationStrategy strategy)
    {
        switch (strategy)
        {
            case OptimizationStrategy.ReducedUpdateFrequency:
                RestoreNormalUpdateFrequencies();
                break;
            case OptimizationStrategy.AggressiveCulling:
                DisableAggressiveCulling();
                break;
            case OptimizationStrategy.BehaviorLOD:
                RestoreFullBehavior();
                break;
            case OptimizationStrategy.ReducedEffects:
                RestoreVisualEffects();
                break;
        }
    }
    
    private void ManageAIUpdates()
    {
        updateScheduler.ManageUpdates(Time.deltaTime);
    }
    
    private void RegisterAllAIEntities()
    {
        var allEntities = FindObjectsOfType<AIEntity>();
        foreach (var entity in allEntities)
        {
            RegisterAIEntity(entity);
        }
    }
    
    public void RegisterAIEntity(AIEntity entity)
    {
        if (!aiPerformanceData.ContainsKey(entity))
        {
            aiPerformanceData[entity] = new AIPerformanceData(entity);
            updateScheduler.RegisterEntity(entity);
        }
    }
    
    public void UnregisterAIEntity(AIEntity entity)
    {
        if (aiPerformanceData.ContainsKey(entity))
        {
            aiPerformanceData.Remove(entity);
            updateScheduler.UnregisterEntity(entity);
        }
    }
    
    private void ApplyPerformanceProfile()
    {
        if (performanceProfile == null) return;
        
        performanceTargetFPS = performanceProfile.targetFPS;
        baseUpdateInterval = performanceProfile.baseUpdateInterval;
        maxActiveAI = performanceProfile.maxActiveAI;
        
        // Apply quality settings from profile
        QualitySettings.SetQualityLevel(performanceProfile.qualityLevel, true);
    }
    
    void OnDestroy()
    {
        RestoreOriginalSettings();
    }
    
    private void RestoreOriginalSettings()
    {
        // Restore quality settings
        RestoreVisualEffects();
        RestoreFullBehavior();
        RestoreNormalUpdateFrequencies();
    }
    
    private void RestoreNormalUpdateFrequencies()
    {
        foreach (var kvp in aiPerformanceData)
        {
            kvp.Key?.SetUpdateInterval(baseUpdateInterval);
        }
    }
    
    private void DisableAggressiveCulling()
    {
        var aiEntities = FindObjectsOfType<AIEntity>();
        foreach (var entity in aiEntities)
        {
            entity.EnableCulling(false);
            entity.RestoreVisualComplexity();
        }
    }
    
    private void RestoreFullBehavior()
    {
        var guards = FindObjectsOfType<Guard>();
        var civilians = FindObjectsOfType<CivilianAI>();
        
        foreach (var guard in guards)
        {
            guard.SetBehaviorLOD(AIBehaviorLOD.Near);
            guard.EnableFullBehavior();
        }
        
        foreach (var civilian in civilians)
        {
            civilian.EnableFullBehavior();
            civilian.SetDecisionInterval(0.5f);
        }
    }
    
    private void RestoreVisualEffects()
    {
        // Restore particle systems
        var effectSystems = FindObjectsOfType<ParticleSystem>();
        foreach (var effect in effectSystems)
        {
            // This would need to store original values
        }
        
        // Restore quality settings
        QualitySettings.shadowDistance = 150f; // Default value
        QualitySettings.lodBias = 2f; // Default value
    }
    
    [ContextMenu("Print Performance Report")]
    public void PrintPerformanceReport()
    {
        Debug.Log("=== AI PERFORMANCE REPORT ===");
        Debug.Log($"Current Performance Score: {currentPerformanceScore:F2}");
        Debug.Log($"Target FPS: {performanceTargetFPS}");
        Debug.Log($"Active Optimizations: {activeOptimizations.Count}");
        Debug.Log($"Registered AI Entities: {aiPerformanceData.Count}");
        Debug.Log($"Average Frame Time: {performanceMonitor.GetAverageFrameTime():F2}ms");
        
        foreach (var optimization in activeOptimizations)
        {
            Debug.Log($"  - {optimization}");
        }
        
        Debug.Log("=== END REPORT ===");
    }
}

// Supporting classes
public class PerformanceMonitor
{
    private float targetFPS;
    private float[] frameTimeHistory = new float[60];
    private int frameIndex = 0;
    private float averageFrameTime;
    
    public PerformanceMonitor(float targetFPS)
    {
        this.targetFPS = targetFPS;
    }
    
    public void Update()
    {
        frameTimeHistory[frameIndex] = Time.unscaledDeltaTime;
        frameIndex = (frameIndex + 1) % frameTimeHistory.Length;
        
        averageFrameTime = frameTimeHistory.Average();
    }
    
    public float GetPerformanceScore()
    {
        float targetFrameTime = 1f / targetFPS;
        return Mathf.Clamp01(targetFrameTime / averageFrameTime);
    }
    
    public float GetAverageFrameTime()
    {
        return averageFrameTime * 1000f; // Convert to milliseconds
    }
}

public class AIPerformanceData
{
    public AIEntity entity;
    public float distanceToPlayer;
    public float lastUpdateTime;
    public float cpuUsage;
    public AIImportanceLevel importance;
    
    public AIPerformanceData(AIEntity entity)
    {
        this.entity = entity;
        this.importance = entity.GetImportanceLevel();
    }
    
    public void Update(AIEntity entity)
    {
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            distanceToPlayer = Vector3.Distance(entity.transform.position, player.transform.position);
        }
        
        lastUpdateTime = Time.time;
    }
}

public enum OptimizationStrategy
{
    ReducedUpdateFrequency,
    AggressiveCulling,
    BehaviorLOD,
    ReducedEffects
}

public enum AIImportanceLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum AIBehaviorLOD
{
    Far,
    Medium,
    Near
}
```

---

## 🎨 **MEJORAS VISUALES Y POLISH**

### **AIVisualEnhancer.cs - Polish Visual**
```csharp
public class AIVisualEnhancer : MonoBehaviour
{
    [Header("Visual Enhancement Settings")]
    [SerializeField] private bool enableVisualEnhancements = true;
    [SerializeField] private AIVisualQuality visualQuality = AIVisualQuality.High;
    
    [Header("Guard Visual Enhancements")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private GameObject alertIndicatorPrefab;
    [SerializeField] private Material aggressiveGuardMaterial;
    [SerializeField] private Material conservativeGuardMaterial;
    
    [Header("Civilian Visual Enhancements")]
    [SerializeField] private GameObject panicEffectPrefab;
    [SerializeField] private GameObject alertEffectPrefab;
    [SerializeField] private GameObject[] civilianVariantMaterials;
    
    [Header("Environmental Effects")]
    [SerializeField] private GameObject searchlightPrefab;
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private AudioClip[] alertSounds;
    [SerializeField] private AudioClip[] combatSounds;
    
    [Header("UI Polish")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private GameObject stateIndicatorPrefab;
    [SerializeField] private GameObject detectionIndicatorPrefab;
    
    // Visual effect pools
    private ObjectPool<ParticleSystem> muzzleFlashPool;
    private ObjectPool<ParticleSystem> impactEffectPool;
    private ObjectPool<ParticleSystem> panicEffectPool;
    
    // Active visual effects
    private Dictionary<AIEntity, List<GameObject>> activeEffects = new Dictionary<AIEntity, List<GameObject>>();
    private Dictionary<AIEntity, AIVisualState> visualStates = new Dictionary<AIEntity, AIVisualState>();
    
    // Audio management
    private AudioSource audioSource;
    private Dictionary<string, AudioClip> soundLibrary = new Dictionary<string, AudioClip>();
    
    void Start()
    {
        InitializeVisualEnhancements();
    }
    
    private void InitializeVisualEnhancements()
    {
        // Initialize object pools
        InitializeObjectPools();
        
        // Setup audio
        SetupAudioSystem();
        
        // Apply enhancements to existing AI
        ApplyVisualEnhancementsToExistingAI();
        
        Logger.LogInfo("AIVisualEnhancer: Visual enhancements initialized");
    }
    
    private void InitializeObjectPools()
    {
        if (muzzleFlashPrefab != null)
        {
            muzzleFlashPool = new ObjectPool<ParticleSystem>(
                () => Instantiate(muzzleFlashPrefab).GetComponent<ParticleSystem>(),
                effect => effect.gameObject.SetActive(true),
                effect => effect.gameObject.SetActive(false),
                effect => Destroy(effect.gameObject),
                false, 10, 50);
        }
        
        if (impactEffectPrefab != null)
        {
            impactEffectPool = new ObjectPool<ParticleSystem>(
                () => Instantiate(impactEffectPrefab).GetComponent<ParticleSystem>(),
                effect => effect.gameObject.SetActive(true),
                effect => effect.gameObject.SetActive(false),
                effect => Destroy(effect.gameObject),
                false, 10, 30);
        }
        
        if (panicEffectPrefab != null)
        {
            panicEffectPool = new ObjectPool<ParticleSystem>(
                () => Instantiate(panicEffectPrefab).GetComponent<ParticleSystem>(),
                effect => effect.gameObject.SetActive(true),
                effect => effect.gameObject.SetActive(false),
                effect => Destroy(effect.gameObject),
                false, 5, 20);
        }
    }
    
    private void SetupAudioSystem()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D audio
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = 50f;
        
        // Build sound library
        soundLibrary["alert"] = alertSounds?.FirstOrDefault();
        soundLibrary["combat"] = combatSounds?.FirstOrDefault();
    }
    
    private void ApplyVisualEnhancementsToExistingAI()
    {
        // Enhance guards
        var guards = FindObjectsOfType<Guard>();
        foreach (var guard in guards)
        {
            EnhanceGuardVisuals(guard);
        }
        
        // Enhance civilians
        var civilians = FindObjectsOfType<CivilianAI>();
        foreach (var civilian in civilians)
        {
            EnhanceCivilianVisuals(civilian);
        }
    }
    
    public void EnhanceGuardVisuals(Guard guard)
    {
        if (!enableVisualEnhancements) return;
        
        var visualState = new AIVisualState();
        visualStates[guard] = visualState;
        activeEffects[guard] = new List<GameObject>();
        
        // Apply personality-based materials
        ApplyPersonalityMaterial(guard);
        
        // Add health bar
        AddHealthBar(guard);
        
        // Add state indicator
        AddStateIndicator(guard);
        
        // Add detection indicator
        AddDetectionIndicator(guard);
        
        // Setup weapon effects
        SetupWeaponEffects(guard);
        
        // Subscribe to events
        guard.OnAttack += (target) => PlayMuzzleFlash(guard);
        guard.OnHealthChanged += (newHealth) => UpdateHealthBar(guard, newHealth);
        guard.OnStateChanged += (newState) => UpdateStateIndicator(guard, newState);
    }
    
    public void EnhanceCivilianVisuals(CivilianAI civilian)
    {
        if (!enableVisualEnhancements) return;
        
        var visualState = new AIVisualState();
        visualStates[civilian] = visualState;
        activeEffects[civilian] = new List<GameObject>();
        
        // Apply civilian material variation
        ApplyCivilianMaterial(civilian);
        
        // Add panic level indicator
        AddPanicLevelIndicator(civilian);
        
        // Add state indicator
        AddStateIndicator(civilian);
        
        // Setup panic effects
        SetupPanicEffects(civilian);
        
        // Subscribe to events
        civilian.OnStateChanged += (newState) => HandleCivilianStateChange(civilian, newState);
        civilian.OnPanicLevelChanged += (panicLevel) => UpdatePanicEffect(civilian, panicLevel);
    }
    
    private void ApplyPersonalityMaterial(Guard guard)
    {
        var personality = guard.GetPersonalityController();
        if (personality == null) return;
        
        var renderer = guard.GetComponent<Renderer>();
        if (renderer == null) return;
        
        Material materialToApply = null;
        
        if (personality.IsAggressiveType() && aggressiveGuardMaterial != null)
        {
            materialToApply = aggressiveGuardMaterial;
        }
        else if (personality.IsConservativeType() && conservativeGuardMaterial != null)
        {
            materialToApply = conservativeGuardMaterial;
        }
        
        if (materialToApply != null)
        {
            renderer.material = materialToApply;
        }
    }
    
    private void ApplyCivilianMaterial(CivilianAI civilian)
    {
        if (civilianVariantMaterials == null || civilianVariantMaterials.Length == 0) return;
        
        var renderer = civilian.GetComponent<Renderer>();
        if (renderer == null) return;
        
        // Apply random material variant
        var randomMaterial = civilianVariantMaterials[Random.Range(0, civilianVariantMaterials.Length)];
        renderer.material = randomMaterial;
    }
    
    private void AddHealthBar(Guard guard)
    {
        if (healthBarPrefab == null) return;
        
        var healthBarObj = Instantiate(healthBarPrefab, guard.transform);
        healthBarObj.transform.localPosition = Vector3.up * 2.5f;
        
        var healthBar = healthBarObj.GetComponent<AIHealthBar>();
        if (healthBar != null)
        {
            healthBar.Initialize(guard);
        }
        
        activeEffects[guard].Add(healthBarObj);
    }
    
    private void AddStateIndicator(AIEntity entity)
    {
        if (stateIndicatorPrefab == null) return;
        
        var indicatorObj = Instantiate(stateIndicatorPrefab, entity.transform);
        indicatorObj.transform.localPosition = Vector3.up * 3f;
        
        var indicator = indicatorObj.GetComponent<AIStateIndicator>();
        if (indicator != null)
        {
            indicator.Initialize(entity);
        }
        
        activeEffects[entity].Add(indicatorObj);
    }
    
    private void AddDetectionIndicator(Guard guard)
    {
        if (detectionIndicatorPrefab == null) return;
        
        var indicatorObj = Instantiate(detectionIndicatorPrefab, guard.transform);
        indicatorObj.transform.localPosition = Vector3.up * 3.5f;
        
        var indicator = indicatorObj.GetComponent<AIDetectionIndicator>();
        if (indicator != null)
        {
            indicator.Initialize(guard);
        }
        
        activeEffects[guard].Add(indicatorObj);
    }
    
    private void AddPanicLevelIndicator(CivilianAI civilian)
    {
        if (stateIndicatorPrefab == null) return;
        
        var indicatorObj = Instantiate(stateIndicatorPrefab, civilian.transform);
        indicatorObj.transform.localPosition = Vector3.up * 2.5f;
        
        var indicator = indicatorObj.GetComponent<AIPanicIndicator>();
        if (indicator != null)
        {
            indicator.Initialize(civilian);
        }
        
        activeEffects[civilian].Add(indicatorObj);
    }
    
    private void SetupWeaponEffects(Guard guard)
    {
        // This would attach weapon effect points to the guard's weapon
        var weaponTransform = guard.transform.Find("Weapon");
        if (weaponTransform == null) return;
        
        var effectPoint = new GameObject("MuzzlePoint");
        effectPoint.transform.SetParent(weaponTransform);
        effectPoint.transform.localPosition = Vector3.forward;
        
        guard.SetMuzzlePoint(effectPoint.transform);
    }
    
    private void SetupPanicEffects(CivilianAI civilian)
    {
        // Pre-create panic effect point
        var effectPoint = new GameObject("PanicEffectPoint");
        effectPoint.transform.SetParent(civilian.transform);
        effectPoint.transform.localPosition = Vector3.up * 1.5f;
        
        civilian.SetPanicEffectPoint(effectPoint.transform);
    }
    
    private void PlayMuzzleFlash(Guard guard)
    {
        if (muzzleFlashPool == null) return;
        
        var muzzlePoint = guard.GetMuzzlePoint();
        if (muzzlePoint == null) return;
        
        var effect = muzzleFlashPool.Get();
        effect.transform.position = muzzlePoint.position;
        effect.transform.rotation = muzzlePoint.rotation;
        effect.Play();
        
        // Return to pool after effect duration
        StartCoroutine(ReturnEffectToPool(effect, muzzleFlashPool, effect.main.duration));
        
        // Play sound
        PlaySoundAtPosition("combat", muzzlePoint.position);
    }
    
    private void UpdateHealthBar(Guard guard, float newHealth)
    {
        var healthBar = GetActiveEffect<AIHealthBar>(guard);
        if (healthBar != null)
        {
            healthBar.UpdateHealth(newHealth / guard.MaxHealth);
        }
    }
    
    private void UpdateStateIndicator(AIEntity entity, object newState)
    {
        var indicator = GetActiveEffect<AIStateIndicator>(entity);
        if (indicator != null)
        {
            indicator.UpdateState(newState.ToString());
        }
    }
    
    private void HandleCivilianStateChange(CivilianAI civilian, CivilianState newState)
    {
        UpdateStateIndicator(civilian, newState);
        
        // Play state-specific effects
        switch (newState)
        {
            case CivilianState.Panicking:
                PlayPanicEffect(civilian);
                break;
            case CivilianState.Alerting:
                PlayAlertEffect(civilian);
                break;
        }
    }
    
    private void UpdatePanicEffect(CivilianAI civilian, float panicLevel)
    {
        var panicIndicator = GetActiveEffect<AIPanicIndicator>(civilian);
        if (panicIndicator != null)
        {
            panicIndicator.UpdatePanicLevel(panicLevel);
        }
        
        // Scale panic visual effect with panic level
        if (panicLevel > 50f)
        {
            PlayPanicEffect(civilian, panicLevel / 100f);
        }
    }
    
    private void PlayPanicEffect(CivilianAI civilian, float intensity = 1f)
    {
        if (panicEffectPool == null) return;
        
        var effectPoint = civilian.GetPanicEffectPoint();
        if (effectPoint == null) return;
        
        var effect = panicEffectPool.Get();
        effect.transform.position = effectPoint.position;
        
        // Scale effect with intensity
        var main = effect.main;
        main.startLifetime = main.startLifetime.constant * intensity;
        
        effect.Play();
        
        StartCoroutine(ReturnEffectToPool(effect, panicEffectPool, main.duration));
    }
    
    private void PlayAlertEffect(CivilianAI civilian)
    {
        if (alertEffectPrefab == null) return;
        
        var alertEffect = Instantiate(alertEffectPrefab, civilian.transform.position + Vector3.up * 2f, Quaternion.identity);
        
        // Auto-destroy after 3 seconds
        Destroy(alertEffect, 3f);
        
        // Play alert sound
        PlaySoundAtPosition("alert", civilian.transform.position);
    }
    
    private void PlaySoundAtPosition(string soundName, Vector3 position)
    {
        if (!soundLibrary.ContainsKey(soundName) || soundLibrary[soundName] == null) return;
        
        AudioSource.PlayClipAtPoint(soundLibrary[soundName], position, 0.7f);
    }
    
    private T GetActiveEffect<T>(AIEntity entity) where T : Component
    {
        if (!activeEffects.ContainsKey(entity)) return null;
        
        foreach (var effect in activeEffects[entity])
        {
            var component = effect.GetComponent<T>();
            if (component != null) return component;
        }
        
        return null;
    }
    
    private IEnumerator ReturnEffectToPool<T>(T effect, ObjectPool<T> pool, float delay) where T : Component
    {
        yield return new WaitForSeconds(delay);
        pool.Release(effect);
    }
    
    public void SetVisualQuality(AIVisualQuality quality)
    {
        visualQuality = quality;
        ApplyQualitySettings();
    }
    
    private void ApplyQualitySettings()
    {
        switch (visualQuality)
        {
            case AIVisualQuality.Low:
                // Disable most effects
                enableVisualEnhancements = false;
                break;
            case AIVisualQuality.Medium:
                // Enable basic effects only
                enableVisualEnhancements = true;
                // Reduce particle counts
                break;
            case AIVisualQuality.High:
                // Enable all effects
                enableVisualEnhancements = true;
                break;
        }
    }
    
    void OnDestroy()
    {
        // Cleanup all active effects
        foreach (var effectList in activeEffects.Values)
        {
            foreach (var effect in effectList)
            {
                if (effect != null)
                    Destroy(effect);
            }
        }
    }
}

// Supporting classes
public class AIVisualState
{
    public float lastEffectTime;
    public bool isHighlighted;
    public Color currentTint = Color.white;
    public float currentScale = 1f;
}

public enum AIVisualQuality
{
    Low,
    Medium,
    High
}
```

---

## 📊 **DEMO SCENE Y PRESENTACIÓN**

### **AIDemoManager.cs - Manager de Demostración**
```csharp
public class AIDemoManager : MonoBehaviour
{
    [Header("Demo Configuration")]
    [SerializeField] private DemoScenario[] demoScenarios;
    [SerializeField] private float scenarioTransitionTime = 3f;
    [SerializeField] private bool autoplayDemo = false;
    [SerializeField] private KeyCode nextScenarioKey = KeyCode.Space;
    
    [Header("Camera Control")]
    [SerializeField] private Camera demoCamera;
    [SerializeField] private Transform[] cameraPositions;
    [SerializeField] private float cameraTransitionSpeed = 2f;
    
    [Header("UI Elements")]
    [SerializeField] private Canvas demoUI;
    [SerializeField] private Text scenarioTitleText;
    [SerializeField] private Text scenarioDescriptionText;
    [SerializeField] private Text instructionsText;
    [SerializeField] private Button nextScenarioButton;
    [SerializeField] private Slider progressSlider;
    
    [Header("Demo Settings")]
    [SerializeField] private bool showAIDebugInfo = true;
    [SerializeField] private bool enablePerformanceDisplay = true;
    [SerializeField] private bool recordingMode = false;
    
    // Demo state
    private int currentScenarioIndex = 0;
    private bool isDemoRunning = false;
    private float scenarioStartTime;
    private Coroutine currentScenarioCoroutine;
    
    // Components
    private AITestSuite testSuite;
    private AIDebugVisualizer debugVisualizer;
    private AIPerformanceOptimizer performanceOptimizer;
    
    void Start()
    {
        InitializeDemo();
        if (autoplayDemo)
        {
            StartDemo();
        }
    }
    
    void Update()
    {
        if (Input.GetKeyDown(nextScenarioKey) && isDemoRunning)
        {
            NextScenario();
        }
        
        UpdateUI();
    }
    
    private void InitializeDemo()
    {
        // Get AI system components
        testSuite = FindObjectOfType<AITestSuite>();
        debugVisualizer = FindObjectOfType<AIDebugVisualizer>();
        performanceOptimizer = FindObjectOfType<AIPerformanceOptimizer>();
        
        // Setup UI
        if (nextScenarioButton != null)
        {
            nextScenarioButton.onClick.AddListener(NextScenario);
        }
        
        // Configure demo settings
        if (showAIDebugInfo && debugVisualizer != null)
        {
            debugVisualizer.enabled = true;
        }
        
        if (recordingMode)
        {
            SetupRecordingMode();
        }
        
        Logger.LogInfo("AIDemoManager: Demo initialized");
    }
    
    [ContextMenu("Start Demo")]
    public void StartDemo()
    {
        if (isDemoRunning) return;
        
        isDemoRunning = true;
        currentScenarioIndex = 0;
        
        if (demoUI != null)
        {
            demoUI.gameObject.SetActive(true);
        }
        
        PlayScenario(currentScenarioIndex);
        
        Logger.LogInfo("AIDemoManager: Demo started");
    }
    
    [ContextMenu("Stop Demo")]
    public void StopDemo()
    {
        if (!isDemoRunning) return;
        
        isDemoRunning = false;
        
        if (currentScenarioCoroutine != null)
        {
            StopCoroutine(currentScenarioCoroutine);
        }
        
        if (demoUI != null)
        {
            demoUI.gameObject.SetActive(false);
        }
        
        Logger.LogInfo("AIDemoManager: Demo stopped");
    }
    
    public void NextScenario()
    {
        if (!isDemoRunning) return;
        
        currentScenarioIndex++;
        
        if (currentScenarioIndex >= demoScenarios.Length)
        {
            // Demo completed
            CompleteDemoSequence();
            return;
        }
        
        PlayScenario(currentScenarioIndex);
    }
    
    private void PlayScenario(int scenarioIndex)
    {
        if (scenarioIndex < 0 || scenarioIndex >= demoScenarios.Length) return;
        
        var scenario = demoScenarios[scenarioIndex];
        
        Logger.LogInfo($"AIDemoManager: Playing scenario {scenarioIndex + 1}/{demoScenarios.Length}: {scenario.title}");
        
        if (currentScenarioCoroutine != null)
        {
            StopCoroutine(currentScenarioCoroutine);
        }
        
        currentScenarioCoroutine = StartCoroutine(PlayScenarioCoroutine(scenario));
    }
    
    private IEnumerator PlayScenarioCoroutine(DemoScenario scenario)
    {
        scenarioStartTime = Time.time;
        
        // Update UI
        UpdateScenarioUI(scenario);
        
        // Move camera to scenario position
        if (scenario.cameraPositionIndex >= 0 && scenario.cameraPositionIndex < cameraPositions.Length)
        {
            yield return StartCoroutine(MoveCameraToPosition(cameraPositions[scenario.cameraPositionIndex]));
        }
        
        // Setup scenario
        yield return StartCoroutine(SetupScenario(scenario));
        
        // Run scenario
        yield return StartCoroutine(RunScenario(scenario));
        
        // If autoplay is enabled, automatically transition to next scenario
        if (autoplayDemo)
        {
            yield return new WaitForSeconds(scenarioTransitionTime);
            NextScenario();
        }
    }
    
    private IEnumerator SetupScenario(DemoScenario scenario)
    {
        // Reset AI states
        ResetAIEntities();
        
        // Apply scenario settings
        if (scenario.aiSettings != null)
        {
            ApplyAISettings(scenario.aiSettings);
        }
        
        // Position player if specified
        if (scenario.playerStartPosition != Vector3.zero)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                player.transform.position = scenario.playerStartPosition;
                player.transform.rotation = Quaternion.LookRotation(scenario.playerStartDirection);
            }
        }
        
        // Spawn or configure NPCs
        yield return StartCoroutine(ConfigureNPCsForScenario(scenario));
        
        // Apply visual settings
        if (debugVisualizer != null)
        {
            debugVisualizer.SetDebugMode(scenario.debugMode);
        }
        
        yield return new WaitForSeconds(0.5f); // Brief pause for setup
    }
    
    private IEnumerator RunScenario(DemoScenario scenario)
    {
        float startTime = Time.time;
        
        // Execute scenario-specific logic
        switch (scenario.scenarioType)
        {
            case DemoScenarioType.BasicFSM:
                yield return StartCoroutine(RunBasicFSMDemo(scenario));
                break;
            case DemoScenarioType.SteeringBehaviors:
                yield return StartCoroutine(RunSteeringBehaviorsDemo(scenario));
                break;
            case DemoScenarioType.LineOfSight:
                yield return StartCoroutine(RunLineOfSightDemo(scenario));
                break;
            case DemoScenarioType.DecisionTrees:
                yield return StartCoroutine(RunDecisionTreesDemo(scenario));
                break;
            case DemoScenarioType.RouletteWheel:
                yield return StartCoroutine(RunRouletteWheelDemo(scenario));
                break;
            case DemoScenarioType.GuardPersonalities:
                yield return StartCoroutine(RunGuardPersonalitiesDemo(scenario));
                break;
            case DemoScenarioType.CivilianBehaviors:
                yield return StartCoroutine(RunCivilianBehaviorsDemo(scenario));
                break;
            case DemoScenarioType.FullIntegration:
                yield return StartCoroutine(RunFullIntegrationDemo(scenario));
                break;
        }
        
        // Run scenario for specified duration if not manually controlled
        if (scenario.duration > 0 && !autoplayDemo)
        {
            float remainingTime = scenario.duration - (Time.time - startTime);
            if (remainingTime > 0)
            {
                yield return new WaitForSeconds(remainingTime);
            }
        }
    }
    
    private IEnumerator RunBasicFSMDemo(DemoScenario scenario)
    {
        // Demonstrate FSM state transitions
        Logger.LogInfo("Demo: Demonstrating FSM state transitions");
        
        // Focus on guards and show their state changes
        var guards = FindObjectsOfType<Guard>();
        
        foreach (var guard in guards.Take(2)) // Limit to 2 guards for clarity
        {
            // Force state transitions to demonstrate FSM
            guard.ForceState(GuardState.Patrol);
            yield return new WaitForSeconds(2f);
            
            guard.ForceState(GuardState.Alert);
            yield return new WaitForSeconds(2f);
            
            guard.ForceState(GuardState.Chase);
            yield return new WaitForSeconds(2f);
            
            guard.ForceState(GuardState.Attack);
            yield return new WaitForSeconds(2f);
        }
    }
    
    private IEnumerator RunSteeringBehaviorsDemo(DemoScenario scenario)
    {
        Logger.LogInfo("Demo: Demonstrating Steering Behaviors");
        
        // Create demonstration of different steering behaviors
        var aiControllers = FindObjectsOfType<AIMovementController>();
        
        foreach (var controller in aiControllers.Take(3))
        {
            // Demonstrate Seek behavior
            controller.ClearAllBehaviors();
            controller.EnableBehavior(typeof(SeekBehavior), true);
            controller.MoveTo(scenario.playerStartPosition, 3f);
            yield return new WaitForSeconds(3f);
            
            // Demonstrate Flee behavior
            controller.ClearAllBehaviors();
            controller.EnableBehavior(typeof(FleeBehavior), true);
            controller.Flee(scenario.playerStartPosition, 4f);
            yield return new WaitForSeconds(3f);
            
            // Demonstrate Obstacle Avoidance
            controller.EnableBehavior(typeof(ObstacleAvoidanceBehavior), true);
            yield return new WaitForSeconds(3f);
        }
    }
    
    private IEnumerator RunLineOfSightDemo(DemoScenario scenario)
    {
        Logger.LogInfo("Demo: Demonstrating Line of Sight");
        
        // Position player and guards to show line of sight mechanics
        var player = FindObjectOfType<PlayerController>();
        var guards = FindObjectsOfType<Guard>();
        
        if (player != null && guards.Length > 0)
        {
            var guard = guards[0];
            
            // Demo 1: Clear line of sight
            player.transform.position = guard.transform.position + Vector3.forward * 5f;
            yield return new WaitForSeconds(3f);
            
            // Demo 2: Obstructed line of sight
            player.transform.position = guard.transform.position + Vector3.right * 10f;
            yield return new WaitForSeconds(3f);
            
            // Demo 3: Out of range
            player.transform.position = guard.transform.position + Vector3.forward * 20f;
            yield return new WaitForSeconds(3f);
        }
    }
    
    private IEnumerator RunDecisionTreesDemo(DemoScenario scenario)
    {
        Logger.LogInfo("Demo: Demonstrating Decision Trees");
        
        var civilians = FindObjectsOfType<CivilianAI>();
        
        foreach (var civilian in civilians.Take(2))
        {
            // Force different decision scenarios
            civilian.SimulatePlayerDetection(PlayerDetectionLevel.Direct_Close);
            yield return new WaitForSeconds(4f);
            
            civilian.SimulatePlayerDetection(PlayerDetectionLevel.Sound);
            yield return new WaitForSeconds(4f);
            
            civilian.SimulatePlayerDetection(PlayerDetectionLevel.None);
            yield return new WaitForSeconds(2f);
        }
    }
    
    private IEnumerator RunRouletteWheelDemo(DemoScenario scenario)
    {
        Logger.LogInfo("Demo: Demonstrating Roulette Wheel Selection");
        
        var guards = FindObjectsOfType<Guard>();
        
        foreach (var guard in guards.Take(2))
        {
            var personalityController = guard.GetPersonalityController();
            if (personalityController != null)
            {
                // Record multiple decisions to show distribution
                for (int i = 0; i < 10; i++)
                {
                    var action = personalityController.SelectCombatAction();
                    Logger.LogDebug($"Guard {guard.name} selected action: {action}");
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
    }
    
    private IEnumerator RunGuardPersonalitiesDemo(DemoScenario scenario)
    {
        Logger.LogInfo("Demo: Demonstrating Guard Personalities");
        
        var guards = FindObjectsOfType<Guard>();
        var aggressiveGuards = guards.Where(g => g.GetPersonalityController()?.IsAggressiveType() == true).ToArray();
        var conservativeGuards = guards.Where(g => g.GetPersonalityController()?.IsConservativeType() == true).ToArray();
        
        // Show aggressive behavior
        if (aggressiveGuards.Length > 0)
        {
            Logger.LogInfo("Demonstrating Aggressive Guard behavior");
            aggressiveGuards[0].ForceEngageCombat();
            yield return new WaitForSeconds(5f);
        }
        
        // Show conservative behavior
        if (conservativeGuards.Length > 0)
        {
            Logger.LogInfo("Demonstrating Conservative Guard behavior");
            conservativeGuards[0].ForceEngageCombat();
            yield return new WaitForSeconds(5f);
        }
    }
    
    private IEnumerator RunCivilianBehaviorsDemo(DemoScenario scenario)
    {
        Logger.LogInfo("Demo: Demonstrating Civilian Behaviors");
        
        var civilians = FindObjectsOfType<CivilianAI>();
        
        foreach (var civilian in civilians.Take(2))
        {
            // Demo panic behavior
            civilian.IncreasePanic(80f);
            yield return new WaitForSeconds(3f);
            
            // Demo alert behavior
            civilian.ForceAlertGuards();
            yield return new WaitForSeconds(3f);
            
            // Demo flee behavior
            civilian.ForceFleeFromPosition(scenario.playerStartPosition);
            yield return new WaitForSeconds(3f);
            
            // Reset
            civilian.ResetCivilian();
            yield return new WaitForSeconds(1f);
        }
    }
    
    private IEnumerator RunFullIntegrationDemo(DemoScenario scenario)
    {
        Logger.LogInfo("Demo: Demonstrating Full System Integration");
        
        // This scenario shows all systems working together
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            // Move player through the level to trigger all AI systems
            var waypoints = new Vector3[]
            {
                scenario.playerStartPosition,
                scenario.playerStartPosition + Vector3.right * 10f,
                scenario.playerStartPosition + Vector3.forward * 10f,
                scenario.playerStartPosition + Vector3.left * 10f,
                scenario.playerStartPosition
            };
            
            foreach (var waypoint in waypoints)
            {
                player.transform.position = waypoint;
                yield return new WaitForSeconds(4f);
            }
        }
    }
    
    // Helper methods continue in next part...
    
    private void CompleteDemoSequence()
    {
        Logger.LogInfo("AIDemoManager: Demo sequence completed!");
        
        // Show completion UI or restart
        if (autoplayDemo)
        {
            // Restart demo
            currentScenarioIndex = 0;
            PlayScenario(0);
        }
        else
        {
            StopDemo();
        }
    }
    
    // More helper methods and UI management...
}

[System.Serializable]
public class DemoScenario
{
    public string title;
    [TextArea(3, 5)]
    public string description;
    public DemoScenarioType scenarioType;
    public float duration = 10f;
    public int cameraPositionIndex = 0;
    public Vector3 playerStartPosition;
    public Vector3 playerStartDirection = Vector3.forward;
    public AISettings aiSettings;
    public AIDebugMode debugMode = AIDebugMode.Overview;
}

public enum DemoScenarioType
{
    BasicFSM,
    SteeringBehaviors,
    LineOfSight,
    DecisionTrees,
    RouletteWheel,
    GuardPersonalities,
    CivilianBehaviors,
    FullIntegration
}
```

---

## ✅ **CRITERIOS DE COMPLETITUD FINAL**

Al finalizar esta fase deberás tener:

1. **✅ Sistema optimizado** para rendimiento suave
2. **✅ Polish visual profesional** con efectos y animaciones
3. **✅ Demo scene completa** mostrando todos los features
4. **✅ Documentación de presentación** lista
5. **✅ Video/screenshots** para portfolio
6. **✅ Código comentado y organizado** para entrega
7. **✅ Validación final** de todos los requisitos del TP

### **Checklist Final del TP:**
- **✅ FSM (State Machine)**: Implementado en Guards con estados complejos
- **✅ Steering Behaviors**: Sistema completo con Seek, Flee, Pursuit, Evade, Obstacle Avoidance
- **✅ Line of Sight**: Detección avanzada con campo de visión y obstáculos
- **✅ Decision Trees**: Implementado en Civilians con nodos complejos
- **✅ Roulette Wheel Selection**: Sistema adaptativo en personalidades de Guards
- **✅ Dos grupos de enemigos**: Guards (agresivos) y Civilians (reactivos)
- **✅ Personalidades diferenciadas**: Aggressive vs Conservative con comportamiento distintivo

### **Deliverables finales:**
1. **Proyecto Unity completo** con todos los sistemas implementados
2. **Demo scene** que demuestra cada requisito del TP
3. **Documentación técnica** explicando la implementación
4. **Video demo** de 3-5 minutos mostrando el funcionamiento
5. **Código fuente** limpio y comentado
6. **Reporte final** de testing y validación

Tu sistema de AI está ahora **completo y listo para entregar**, cumpliendo todos los requisitos del TP con **calidad profesional** y **documentación exhaustiva**. 🎉