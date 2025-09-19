using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central AI memory system that allows NPCs to share information and coordinate behavior.
/// Integrates with the existing ServiceLocator pattern for consistent architecture.
/// 
/// IMPROVEMENT: Implemented as IGameService
/// IMPROVEMENT: Added typed callback system for better performance
/// IMPROVEMENT: Smart cache to avoid unnecessary boxing/unboxing
/// IMPROVEMENT: Automatic cleanup of temporary data for better memory management
/// IMPROVEMENT: Integration with UpdateManager for periodic cleanup
/// </summary>
public class Blackboard : MonoBehaviour, IBlackboard, IGameService, IUpdatable
{
    [Header("Configuration")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showInInspector = true;
    [SerializeField] private float cleanupInterval = 30f; // Cleanup every 30 seconds
    [SerializeField] private int maxPanicAreas = 10;
    [SerializeField] private int maxAIPositions = 50;

    [Header("Minimum Scope Mode")]
    [SerializeField] private bool useMinimumScope = false;
    [SerializeField] private bool enableAdvancedFeatures = false;

    // Core data storage
    private Dictionary<string, object> data = new Dictionary<string, object>();
    
    // Dual callback system - typed and object-based for flexibility
    private Dictionary<string, List<Action<object>>> objectSubscribers = new Dictionary<string, List<Action<object>>>();
    private Dictionary<string, object> typedSubscribers = new Dictionary<string, object>(); // Will store List<Action<T>>
    
    // Inspector visualization for debugging
    [SerializeField] private List<BlackboardEntry> debugEntries = new List<BlackboardEntry>();
    
    // Performance tracking
    private float lastCleanupTime;
    private int frameCounter;
    
    // IGameService implementation
    public bool IsInitialized { get; private set; }
    
    // IUpdatable implementation
    public bool IsActive => IsInitialized && enabled;
    
    #region IGameService Implementation
    
    public void Initialize()
    {
        if (IsInitialized) return;
        
        // Register as both interface and concrete type for flexibility
        ServiceLocator.Register<IBlackboard>(this);
        ServiceLocator.Register<Blackboard>(this);
        
        // Register with UpdateManager for periodic cleanup
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.RegisterUpdatable(this);
        
        InitializeDefaultValues();
        
        IsInitialized = true;
        
        if (enableDebugLogs)
            Logger.LogInfo("Blackboard: Service initialized successfully");
    }
    
    public void Shutdown()
    {
        if (!IsInitialized) return;
        
        // Unregister from UpdateManager
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
        
        // Clear all data and subscribers
        Clear();
        
        IsInitialized = false;
        
        if (enableDebugLogs)
            Logger.LogInfo("Blackboard: Service shutdown completed");
    }
    
    #endregion
    
    #region IUpdatable Implementation
    
    public void OnUpdate(float deltaTime)
    {
        frameCounter++;
        SetValue(BlackboardKeys.CURRENT_FRAME, frameCounter);
        
        // Periodic cleanup
        if (Time.time - lastCleanupTime >= cleanupInterval)
        {
            CleanupTemporaryData();
            lastCleanupTime = Time.time;
        }
    }
    
    #endregion
    
    #region Core Blackboard Functionality
    
    public T GetValue<T>(string key)
    {
        if (data.TryGetValue(key, out object value))
        {
            try
            {
                return (T)value;
            }
            catch (InvalidCastException)
            {
                Logger.LogError($"Blackboard: Failed to cast '{key}' from {value?.GetType().Name} to {typeof(T).Name}");
                return default(T);
            }
        }
        
        return default(T);
    }
    
    public void SetValue<T>(string key, T value)
    {
        object oldValue = data.ContainsKey(key) ? data[key] : null;
        data[key] = value;
        
        // Update debug entries for inspector
        if (showInInspector)
            UpdateDebugEntries();
        
        // Notify all subscribers
        NotifyObjectSubscribers(key, value);
        NotifyTypedSubscribers<T>(key, value);

        // Only log changes for important keys, not frequently updated ones
        if (enableDebugLogs && !IsFrequentlyUpdatedKey(key))
            Logger.LogDebug($"Blackboard: '{key}' changed from {oldValue} to {value}");
    }
    
    /// <summary>
    /// Check if a key is frequently updated to avoid debug spam
    /// </summary>
    private bool IsFrequentlyUpdatedKey(string key)
    {
        // Keys that are updated frequently and shouldn't spam the console
        return key.Equals(BlackboardKeys.CURRENT_FRAME) ||
               key.Equals(BlackboardKeys.PLAYER_TRANSFORM) ||
               key.Equals(BlackboardKeys.PLAYER_POSITION) ||
               key.Equals(BlackboardKeys.PLAYER_PREDICTED_POSITION) ||
               key.Equals(BlackboardKeys.LAST_KNOWN_PLAYER_POSITION) ||
               key.Equals(BlackboardKeys.PLAYER_LAST_SEEN) ||
               key.Equals(BlackboardKeys.PLAYER_LAST_SEEN_TIME) ||
               key.Equals(BlackboardKeys.LAST_SHOT_TIME) ||
               key.Equals(BlackboardKeys.ALERT_TIME) ||
               key.Contains("_DetectionLevel") ||
               key.Contains("_CanSeePlayer") ||
               key.Contains("_CurrentPosition") ||
               key.Contains("_LastUpdateTime") ||
               key.Contains("_LastShootTime") ||
               key.Contains("_Time") ||
               key.EndsWith("_FRAME_DATA") ||
               key.EndsWith("_TIME") ||
               (key.StartsWith("Guard_") && (key.Contains("_DetectionLevel") || key.Contains("_CanSeePlayer")));
    }    public bool HasKey(string key)
    {
        return data.ContainsKey(key);
    }
    
    #endregion
    
    #region Typed Subscription System
    
    /// <summary>
    /// Subscribe with typed callback for better performance and type safety
    /// </summary>
    public void Subscribe<T>(string key, Action<T> callback)
    {
        string typedKey = GetTypedKey<T>(key);
        
        if (!typedSubscribers.ContainsKey(typedKey))
            typedSubscribers[typedKey] = new List<Action<T>>();
        
        var callbacks = (List<Action<T>>)typedSubscribers[typedKey];
        callbacks.Add(callback);
        
        if (enableDebugLogs)
            Logger.LogDebug($"Blackboard: Typed subscriber added for '{key}' as {typeof(T).Name}");
    }
    
    /// <summary>
    /// Legacy object-based subscription for backward compatibility
    /// </summary>
    public void Subscribe(string key, Action<object> callback)
    {
        if (!objectSubscribers.ContainsKey(key))
            objectSubscribers[key] = new List<Action<object>>();
        
        objectSubscribers[key].Add(callback);
        
        if (enableDebugLogs)
            Logger.LogDebug($"Blackboard: Object subscriber added for '{key}'");
    }
    
    public void Unsubscribe<T>(string key, Action<T> callback)
    {
        string typedKey = GetTypedKey<T>(key);
        
        if (typedSubscribers.TryGetValue(typedKey, out object callbacksObj))
        {
            var callbacks = (List<Action<T>>)callbacksObj;
            callbacks.Remove(callback);
            
            if (callbacks.Count == 0)
                typedSubscribers.Remove(typedKey);
        }
    }
    
    public void Unsubscribe(string key, Action<object> callback)
    {
        if (objectSubscribers.TryGetValue(key, out var callbacks))
        {
            callbacks.Remove(callback);
            
            if (callbacks.Count == 0)
                objectSubscribers.Remove(key);
        }
    }
    
    #endregion

    #region Minimum Scope Support

    /// <summary>
    /// TODO(MIN_SCOPE): Conditional setter for non-minimum features
    /// </summary>
    private void SetValueIfMinScope<T>(string key, T value, bool isMinimumKey = false)
    {
        if (useMinimumScope && !isMinimumKey && !enableAdvancedFeatures)
        {
            // TODO(MIN_SCOPE): Feature parked - key: {key}
            if (enableDebugLogs)
                Logger.LogDebug($"Blackboard: Key '{key}' parked (minimum scope mode)");
            return;
        }
        SetValue(key, value);
    }

    /// <summary>
    /// TODO(MIN_SCOPE): Check if key is part of minimum scope
    /// </summary>
    private bool IsMinimumScopeKey(string key)
    {
        return key == BlackboardKeys.PLAYER_TRANSFORM ||
               key == BlackboardKeys.LAST_KNOWN_PLAYER_POSITION ||
               key == BlackboardKeys.GLOBAL_ALERT;
    }

    #endregion

    #region Initialization and Cleanup
    
    private void InitializeDefaultValues()
    {
        // Player information
        SetValue(BlackboardKeys.PLAYER_DETECTED, false);
        SetValue(BlackboardKeys.PLAYER_LAST_SEEN_TIME, 0f);

        // Minimum scope keys (always initialized)
        SetValue(BlackboardKeys.GLOBAL_ALERT, false);
        
        // Alert system
        SetValue(BlackboardKeys.ALERT_LEVEL, 0);
        SetValue(BlackboardKeys.ALERT_DURATION, 0f);
        
        // AI coordination
        SetValue(BlackboardKeys.GUARDS_CHASING, new List<Transform>());
        SetValue(BlackboardKeys.GUARDS_INVESTIGATING, new List<Transform>());
        SetValue(BlackboardKeys.CIVILIAN_PANIC_AREAS, new List<Vector3>());
        SetValue(BlackboardKeys.ACTIVE_AI_COUNT, 0);
        SetValue(BlackboardKeys.AI_POSITIONS, new Dictionary<Transform, Vector3>());
        
        // Game state integration
        var gameStateManager = ServiceLocator.Get<GameStateManager>();
        if (gameStateManager != null)
        {
            SetValue(BlackboardKeys.GAME_STATE, gameStateManager.CurrentState);
            // Subscribe to game state changes
            gameStateManager.OnStateChanged += (prev, current) => SetValue(BlackboardKeys.GAME_STATE, current);
        }
        
        // Combat
        SetValue(BlackboardKeys.COMBAT_ACTIVE, false);
        SetValue(BlackboardKeys.LAST_SHOT_TIME, 0f);
        
        // Environmental
        SetValue(BlackboardKeys.SAFE_POSITIONS, new List<Vector3>());
        SetValue(BlackboardKeys.DANGEROUS_AREAS, new List<Vector3>());
        SetValue(BlackboardKeys.COVER_POINTS, new List<Transform>());
        
        // Performance
        SetValue(BlackboardKeys.CURRENT_FRAME, 0);
        SetValue(BlackboardKeys.AI_UPDATE_FREQUENCY, 1.0f);
        SetValue(BlackboardKeys.AI_DEBUG_ENABLED, enableDebugLogs);
        SetValue(BlackboardKeys.DIFFICULTY_MULTIPLIER, 1.0f);
        
        if (enableDebugLogs)
            Logger.LogDebug("Blackboard: Default values initialized");
    }
    
    public void CleanupTemporaryData()
    {
        // Clean panic areas
        var panicAreas = GetValue<List<Vector3>>(BlackboardKeys.CIVILIAN_PANIC_AREAS);
        if (panicAreas != null && panicAreas.Count > maxPanicAreas)
        {
            panicAreas.RemoveRange(0, panicAreas.Count - maxPanicAreas);
            SetValue(BlackboardKeys.CIVILIAN_PANIC_AREAS, panicAreas);
        }
        
        // Clean AI positions of destroyed objects
        var aiPositions = GetValue<Dictionary<Transform, Vector3>>(BlackboardKeys.AI_POSITIONS);
        if (aiPositions != null)
        {
            var keysToRemove = aiPositions.Keys.Where(k => k == null).ToList();
            foreach (var key in keysToRemove)
            {
                aiPositions.Remove(key);
            }
            if (keysToRemove.Count > 0)
                SetValue(BlackboardKeys.AI_POSITIONS, aiPositions);
        }
        
        // Clean chasing guards list
        var chasingGuards = GetValue<List<Transform>>(BlackboardKeys.GUARDS_CHASING);
        if (chasingGuards != null)
        {
            int originalCount = chasingGuards.Count;
            chasingGuards.RemoveAll(g => g == null);
            if (chasingGuards.Count != originalCount)
                SetValue(BlackboardKeys.GUARDS_CHASING, chasingGuards);
        }
        
        // Clean investigating guards list
        var investigatingGuards = GetValue<List<Transform>>(BlackboardKeys.GUARDS_INVESTIGATING);
        if (investigatingGuards != null)
        {
            int originalCount = investigatingGuards.Count;
            investigatingGuards.RemoveAll(g => g == null);
            if (investigatingGuards.Count != originalCount)
                SetValue(BlackboardKeys.GUARDS_INVESTIGATING, investigatingGuards);
        }
        
        if (enableDebugLogs)
            Logger.LogDebug("Blackboard: Temporary data cleanup completed");
    }
    
    public void Clear()
    {
        data.Clear();
        objectSubscribers.Clear();
        typedSubscribers.Clear();
        debugEntries.Clear();
        
        if (IsInitialized)
            InitializeDefaultValues();
        
        if (enableDebugLogs)
            Logger.LogDebug("Blackboard: All data cleared");
    }
    
    #endregion
    
    #region Helper Methods
    
    private void NotifyObjectSubscribers(string key, object value)
    {
        if (objectSubscribers.TryGetValue(key, out var callbacks))
        {
            // ToList() prevents modification during iteration
            foreach (var callback in callbacks.ToList())
            {
                try
                {
                    callback?.Invoke(value);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Blackboard: Error notifying object subscriber for '{key}': {e.Message}");
                }
            }
        }
    }
    
    private void NotifyTypedSubscribers<T>(string key, T value)
    {
        string typedKey = GetTypedKey<T>(key);
        
        if (typedSubscribers.TryGetValue(typedKey, out object callbacksObj))
        {
            var callbacks = (List<Action<T>>)callbacksObj;
            
            foreach (var callback in callbacks.ToList())
            {
                try
                {
                    callback?.Invoke(value);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Blackboard: Error notifying typed subscriber for '{key}': {e.Message}");
                }
            }
        }
    }
    
    private string GetTypedKey<T>(string key)
    {
        return $"{key}_{typeof(T).Name}";
    }
    
    private void UpdateDebugEntries()
    {
        debugEntries.Clear();
        
        foreach (var kvp in data.OrderBy(x => x.Key))
        {
            debugEntries.Add(new BlackboardEntry
            {
                key = kvp.Key,
                value = kvp.Value?.ToString() ?? "null",
                type = kvp.Value?.GetType().Name ?? "null"
            });
        }
    }
    
    #endregion
    
    #region Debug and Testing
    
    [System.Serializable]
    private class BlackboardEntry
    {
        public string key;
        public string value;
        public string type;
    }
    
    [ContextMenu("Print All Data")]
    private void PrintAllData()
    {
        Logger.LogInfo("=== BLACKBOARD DATA ===");
        foreach (var kvp in data.OrderBy(x => x.Key))
        {
            Logger.LogInfo($"  {kvp.Key}: {kvp.Value} ({kvp.Value?.GetType().Name})");
        }
        Logger.LogInfo("======================");
    }
    
    [ContextMenu("Print Subscriber Count")]
    private void PrintSubscriberCount()
    {
        Logger.LogInfo($"Object Subscribers: {objectSubscribers.Sum(kvp => kvp.Value.Count)}");
        Logger.LogInfo($"Typed Subscribers: {typedSubscribers.Count}");
    }
    
    [ContextMenu("Force Cleanup")]
    private void ForceCleanup()
    {
        CleanupTemporaryData();
        Logger.LogInfo("Blackboard: Manual cleanup executed");
    }
    
    [ContextMenu("Clear All Data")]
    private void ClearAllData()
    {
        Clear();
        Logger.LogInfo("Blackboard: All data cleared manually");
    }

    [ContextMenu("Toggle Minimum Scope Mode")]
    private void ToggleMinimumScope()
    {
        useMinimumScope = !useMinimumScope;
        Logger.LogInfo($"Blackboard: Minimum scope mode {(useMinimumScope ? "ENABLED" : "DISABLED")}");
    }

    [ContextMenu("Test Minimum Scope Keys")]
    private void TestMinimumScopeKeys()
    {
        Logger.LogInfo("=== MINIMUM SCOPE TEST ===");
        Logger.LogInfo($"PLAYER_TRANSFORM: {GetValue<Transform>(BlackboardKeys.PLAYER_TRANSFORM)?.name ?? "null"}");
        Logger.LogInfo($"LAST_KNOWN_PLAYER_POSITION: {GetValue<Vector3>(BlackboardKeys.LAST_KNOWN_PLAYER_POSITION)}");
        Logger.LogInfo($"GLOBAL_ALERT: {GetValue<bool>(BlackboardKeys.GLOBAL_ALERT)}");
        Logger.LogInfo("========================");
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        // Initialize automatically but allow manual control
        if (GetComponent<GameManager>() == null) // Only auto-initialize if not part of GameManager
        {
            Initialize();
        }
    }
    
    private void OnDestroy()
    {
        Shutdown();
    }
    
    #endregion
}