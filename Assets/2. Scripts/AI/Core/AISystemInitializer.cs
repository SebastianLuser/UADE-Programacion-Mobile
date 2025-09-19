using UnityEngine;

/// <summary>
/// Initializes and manages the AI system lifecycle.
/// Integrates seamlessly with the existing GameManager and ServiceLocator architecture.
/// 
/// MEJORA: Implementado como BaseManager para consistencia con la arquitectura existente
/// MEJORA: Inicialización automática del player reference desde el tag
/// MEJORA: Manejo robusto de errores durante inicialización
/// MEJORA: Integración con el sistema de logging existente
/// MEJORA: Support para diferentes modos de inicialización (auto vs manual)
/// </summary>
public class AISystemInitializer : BaseManager
{
    [Header("AI System Configuration")]
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool enableAIDebugging = false;
    [SerializeField] private float aiUpdateFrequency = 1.0f;
    
    [Header("Blackboard Configuration")]
    [SerializeField] private Blackboard blackboardPrefab;
    [SerializeField] private bool createBlackboardIfMissing = true;
    
    [Header("Player Detection")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private bool subscribeToPlayerMovement = true;
    
    private Blackboard blackboardInstance;
    private Transform cachedPlayerTransform;
    
    protected override void OnInitialize()
    {
        Logger.LogInfo("AISystemInitializer: Starting AI system initialization...");
        
        try
        {
            InitializeBlackboard();
            InitializePlayerReference();
            ConfigureAISystem();
            
            Logger.LogInfo("AISystemInitializer: AI system initialized successfully");
        }
        catch (System.Exception e)
        {
            Logger.LogError($"AISystemInitializer: Failed to initialize AI system: {e.Message}");
        }
    }
    
    protected override void OnShutdown()
    {
        Logger.LogInfo("AISystemInitializer: Shutting down AI system...");
        
        // Blackboard will shutdown itself as it's a service
        // Just clean up local references
        cachedPlayerTransform = null;
        blackboardInstance = null;
        
        Logger.LogInfo("AISystemInitializer: AI system shutdown completed");
    }
    
    private void InitializeBlackboard()
    {
        // MEJORA: Try to find existing blackboard first
        blackboardInstance = FindObjectOfType<Blackboard>();
        
        if (blackboardInstance == null && createBlackboardIfMissing)
        {
            if (blackboardPrefab != null)
            {
                // Instantiate from prefab
                blackboardInstance = Instantiate(blackboardPrefab);
                blackboardInstance.name = "Blackboard (AI System)";
                Logger.LogInfo("AISystemInitializer: Created Blackboard from prefab");
            }
            else
            {
                // Create new GameObject with Blackboard component
                GameObject blackboardObject = new GameObject("Blackboard (AI System)");
                blackboardInstance = blackboardObject.AddComponent<Blackboard>();
                Logger.LogInfo("AISystemInitializer: Created new Blackboard GameObject");
            }
            
            // MEJORA: Don't destroy on load for persistent AI state
            DontDestroyOnLoad(blackboardInstance.gameObject);
        }
        
        // Ensure blackboard is initialized
        if (blackboardInstance != null && !blackboardInstance.IsInitialized)
        {
            blackboardInstance.Initialize();
        }
        
        // MEJORA: Verify blackboard is properly registered as service
        var blackboardService = ServiceLocator.Get<IBlackboard>();
        if (blackboardService == null)
        {
            Logger.LogError("AISystemInitializer: Blackboard not registered as service!");
        }
        else
        {
            Logger.LogInfo("AISystemInitializer: Blackboard service verified");
        }
    }
    
    private void InitializePlayerReference()
    {
        // Try assigned reference first
        if (playerTransform != null)
        {
            cachedPlayerTransform = playerTransform;
            Logger.LogInfo($"AISystemInitializer: Using assigned player reference: {playerTransform.name}");
        }
        // MEJORA: Auto-find player by tag if enabled
        else if (autoFindPlayer)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                cachedPlayerTransform = playerObject.transform;
                Logger.LogInfo($"AISystemInitializer: Found player by tag '{playerTag}': {playerObject.name}");
            }
            else
            {
                Logger.LogWarning($"AISystemInitializer: Player not found with tag '{playerTag}'");
            }
        }
        
        // Register player in blackboard if found
        if (cachedPlayerTransform != null)
        {
            var blackboard = ServiceLocator.Get<IBlackboard>();
            blackboard?.SetValue(BlackboardKeys.PLAYER_TRANSFORM, cachedPlayerTransform);
            blackboard?.SetValue(BlackboardKeys.PLAYER_POSITION, cachedPlayerTransform.position);
            
            // MEJORA: Subscribe to player movement for real-time position updates
            if (subscribeToPlayerMovement)
            {
                StartCoroutine(UpdatePlayerPositionCoroutine());
            }
        }
    }
    
    private void ConfigureAISystem()
    {
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null) return;
        
        // Set initial AI configuration
        blackboard.SetValue(BlackboardKeys.AI_DEBUG_ENABLED, enableAIDebugging);
        blackboard.SetValue(BlackboardKeys.AI_UPDATE_FREQUENCY, aiUpdateFrequency);
        blackboard.SetValue(BlackboardKeys.ACTIVE_AI_COUNT, 0);
        
        // MEJORA: Get game state from existing GameStateManager
        var gameStateManager = ServiceLocator.Get<GameStateManager>();
        if (gameStateManager != null)
        {
            blackboard.SetValue(BlackboardKeys.GAME_STATE, gameStateManager.CurrentState);
            blackboard.SetValue(BlackboardKeys.GAME_PAUSED, gameStateManager.CurrentState == GameState.Paused);
            
            // Subscribe to game state changes
            gameStateManager.OnStateChanged += OnGameStateChanged;
            Logger.LogInfo("AISystemInitializer: Subscribed to GameStateManager events");
        }
        
        // MEJORA: Set up play area bounds from LevelManager if available
        var levelManager = ServiceLocator.Get<LevelManager>();
        if (levelManager != null)
        {
            // You can extend LevelManager to provide bounds information
            // For now, set reasonable defaults
            blackboard.SetValue(BlackboardKeys.PLAY_AREA_BOUNDS, new Bounds(Vector3.zero, Vector3.one * 100f));
        }
        
        Logger.LogInfo("AISystemInitializer: AI system configuration completed");
    }
    
    private void OnGameStateChanged(GameState previousState, GameState newState)
    {
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null) return;
        
        blackboard.SetValue(BlackboardKeys.GAME_STATE, newState);
        blackboard.SetValue(BlackboardKeys.GAME_PAUSED, newState == GameState.Paused);
        
        // MEJORA: Reset AI states when game restarts
        if (newState == GameState.Playing && (previousState == GameState.Menu || previousState == GameState.GameOver))
        {
            ResetAISystem();
        }
        
        Logger.LogDebug($"AISystemInitializer: Game state changed to {newState}");
    }
    
    private void ResetAISystem()
    {
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null) return;
        
        // Reset AI coordination states
        blackboard.SetValue(BlackboardKeys.ALERT_LEVEL, 0);
        blackboard.SetValue(BlackboardKeys.PLAYER_DETECTED, false);
        blackboard.SetValue(BlackboardKeys.COMBAT_ACTIVE, false);
        blackboard.SetValue(BlackboardKeys.GUARDS_CHASING, new System.Collections.Generic.List<Transform>());
        blackboard.SetValue(BlackboardKeys.GUARDS_INVESTIGATING, new System.Collections.Generic.List<Transform>());
        
        // Clean up temporary data
        blackboard.CleanupTemporaryData();
        
        Logger.LogInfo("AISystemInitializer: AI system reset for new game");
    }
    
    private System.Collections.IEnumerator UpdatePlayerPositionCoroutine()
    {
        var blackboard = ServiceLocator.Get<IBlackboard>();
        
        while (cachedPlayerTransform != null && blackboard != null)
        {
            blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, cachedPlayerTransform.position);
            
            // Update at the configured frequency
            yield return new WaitForSeconds(1f / aiUpdateFrequency);
        }
    }
    
    #region Public API for Runtime Configuration
    
    /// <summary>
    /// MEJORA: Allow runtime player assignment for dynamic scenarios
    /// </summary>
    public void SetPlayer(Transform newPlayer)
    {
        cachedPlayerTransform = newPlayer;
        playerTransform = newPlayer;
        
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard != null)
        {
            blackboard.SetValue(BlackboardKeys.PLAYER_TRANSFORM, newPlayer);
            if (newPlayer != null)
            {
                blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, newPlayer.position);
            }
        }
        
        Logger.LogInfo($"AISystemInitializer: Player reference updated to {newPlayer?.name ?? "null"}");
    }
    
    /// <summary>
    /// MEJORA: Runtime configuration of AI parameters
    /// </summary>
    public void SetAIUpdateFrequency(float frequency)
    {
        aiUpdateFrequency = Mathf.Clamp(frequency, 0.1f, 60f);
        
        var blackboard = ServiceLocator.Get<IBlackboard>();
        blackboard?.SetValue(BlackboardKeys.AI_UPDATE_FREQUENCY, aiUpdateFrequency);
        
        Logger.LogInfo($"AISystemInitializer: AI update frequency set to {aiUpdateFrequency}");
    }
    
    /// <summary>
    /// MEJORA: Toggle AI debugging at runtime
    /// </summary>
    public void SetAIDebugging(bool enabled)
    {
        enableAIDebugging = enabled;
        
        var blackboard = ServiceLocator.Get<IBlackboard>();
        blackboard?.SetValue(BlackboardKeys.AI_DEBUG_ENABLED, enabled);
        
        Logger.LogInfo($"AISystemInitializer: AI debugging {(enabled ? "enabled" : "disabled")}");
    }
    
    /// <summary>
    /// MEJORA: Get current AI system status for monitoring
    /// </summary>
    public bool IsAISystemReady()
    {
        var blackboard = ServiceLocator.Get<IBlackboard>();
        return blackboard != null && 
               blackboard.HasKey(BlackboardKeys.PLAYER_TRANSFORM) && 
               IsInitialized;
    }
    
    #endregion
    
    #region Debug Methods
    
    [ContextMenu("Reinitialize AI System")]
    private void ReinitializeAISystem()
    {
        if (IsInitialized)
        {
            Shutdown();
        }
        Initialize();
    }
    
    [ContextMenu("Print AI System Status")]
    private void PrintAISystemStatus()
    {
        Logger.LogInfo("=== AI SYSTEM STATUS ===");
        Logger.LogInfo($"Initialized: {IsInitialized}");
        Logger.LogInfo($"Player Found: {cachedPlayerTransform != null}");
        Logger.LogInfo($"Blackboard Ready: {ServiceLocator.Get<IBlackboard>() != null}");
        Logger.LogInfo($"AI Debugging: {enableAIDebugging}");
        Logger.LogInfo($"Update Frequency: {aiUpdateFrequency}");
        Logger.LogInfo("=======================");
    }
    
    #endregion
}