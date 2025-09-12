# 🏗️ FASE 1: FUNDACIONES (Día 1–2)

## 🎯 **OBJETIVO DE LA FASE**
Crear la **arquitectura base** del sistema de AI, estableciendo las interfaces y el sistema de comunicación central (Blackboard) que servirá como fundamento para todas las demás fases.

---

## 📋 **PASO 1: INTERFACES BASE**

### **¿QUÉ BUSCAMOS?**
Crear **contratos claros** que definan cómo interactúan los diferentes componentes del sistema, permitiendo flexibilidad y escalabilidad.

### **Interfaces a Implementar:**

#### **1. IBlackboard.cs**
```csharp
public interface IBlackboard
{
    T GetValue<T>(string key);
    void SetValue<T>(string key, T value);
    bool HasKey(string key);
    void Subscribe(string key, System.Action<object> callback);
    void Unsubscribe(string key, System.Action<object> callback);
}
```
**Propósito**: Sistema de memoria compartida entre todos los NPCs.

#### **2. IDecisionNode.cs**
```csharp
public interface IDecisionNode
{
    IDecisionNode Evaluate(IAIContext context);
    string GetNodeName();
}
```
**Propósito**: Base para árboles de decisión modulares.

#### **3. IAIContext.cs**
```csharp
public interface IAIContext
{
    Transform GetTransform();
    IBlackboard GetBlackboard();
    bool IsPlayerVisible();
    Vector3 GetPlayerPosition();
    float GetDistanceToPlayer();
    AIPersonalityType GetPersonalityType();
}
```
**Propósito**: Contexto completo que necesita cada AI para tomar decisiones.

#### **4. IPlayerDetector.cs**
```csharp
public interface IPlayerDetector
{
    bool CanSeePlayer(Transform player);
    float GetDistanceToPlayer(Transform player);
    bool IsPlayerInRange(Transform player, float range);
    Vector3 GetLastKnownPlayerPosition();
    float GetTimeSinceLastSeen();
}
```
**Propósito**: Line of Sight consistente para todos los NPCs.

#### **5. IAIMovementController.cs**
```csharp
public interface IAIMovementController
{
    void MoveTo(Vector3 target, float speed);
    void Flee(Vector3 fromPosition, float speed);
    void Patrol(Transform[] waypoints, float speed);
    void Stop();
    bool HasReachedDestination();
    void SetSteeringTarget(Transform target);
}
```
**Propósito**: Abstracción de todos los steering behaviors.

---

## 📋 **PASO 2: BLACKBOARD SYSTEM**

### **¿QUÉ BUSCAMOS?**
Un sistema de **memoria compartida** que permita coordinación inteligente entre NPCs sin acoplamiento directo.

### **BlackboardKeys.cs**
```csharp
public static class BlackboardKeys
{
    // Player Information
    public const string PLAYER_TRANSFORM = "player_transform";
    public const string PLAYER_POSITION = "player_position";
    public const string PLAYER_LAST_SEEN = "player_last_seen";
    public const string PLAYER_DETECTED = "player_detected";
    
    // Alert System
    public const string ALERT_LEVEL = "alert_level"; // 0=Normal, 1=Suspicious, 2=Alert
    public const string ALERT_POSITION = "alert_position";
    public const string ALERT_TIME = "alert_time";
    public const string LAST_ALERT_SOURCE = "last_alert_source";
    
    // AI Coordination
    public const string GUARDS_CHASING = "guards_chasing";
    public const string GUARDS_INVESTIGATING = "guards_investigating";
    public const string CIVILIAN_PANIC_AREAS = "civilian_panic_areas";
    
    // Game State
    public const string GAME_STATE = "game_state";
    public const string ESCAPE_ROUTES_BLOCKED = "escape_routes_blocked";
}
```

### **Blackboard.cs**
```csharp
public class Blackboard : MonoBehaviour, IBlackboard, IGameService
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showInInspector = true;
    
    // Core data storage
    private Dictionary<string, object> data = new Dictionary<string, object>();
    
    // Event system for notifications
    private Dictionary<string, List<System.Action<object>>> subscribers = new Dictionary<string, List<System.Action<object>>>();
    
    // Inspector visualization (para debugging)
    [SerializeField] private List<BlackboardEntry> debugEntries = new List<BlackboardEntry>();
    
    void Awake()
    {
        // Registrar como servicio
        ServiceLocator.Register<IBlackboard>(this);
        
        // Inicializar valores por defecto
        InitializeDefaultValues();
    }
    
    private void InitializeDefaultValues()
    {
        SetValue(BlackboardKeys.ALERT_LEVEL, 0);
        SetValue(BlackboardKeys.PLAYER_DETECTED, false);
        SetValue(BlackboardKeys.GUARDS_CHASING, new List<Transform>());
        SetValue(BlackboardKeys.CIVILIAN_PANIC_AREAS, new List<Vector3>());
        
        if (enableDebugLogs)
            Logger.LogDebug("Blackboard: Initialized with default values");
    }
    
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
                Logger.LogError($"Blackboard: Failed to cast {key} to {typeof(T)}");
                return default(T);
            }
        }
        
        return default(T);
    }
    
    public void SetValue<T>(string key, T value)
    {
        object oldValue = data.ContainsKey(key) ? data[key] : null;
        data[key] = value;
        
        // Update debug entries para inspector
        if (showInInspector)
            UpdateDebugEntries();
        
        // Notify subscribers
        NotifySubscribers(key, value);
        
        if (enableDebugLogs)
            Logger.LogDebug($"Blackboard: {key} changed from {oldValue} to {value}");
    }
    
    public bool HasKey(string key)
    {
        return data.ContainsKey(key);
    }
    
    public void Subscribe(string key, System.Action<object> callback)
    {
        if (!subscribers.ContainsKey(key))
            subscribers[key] = new List<System.Action<object>>();
        
        subscribers[key].Add(callback);
        
        if (enableDebugLogs)
            Logger.LogDebug($"Blackboard: Subscriber added for {key}");
    }
    
    public void Unsubscribe(string key, System.Action<object> callback)
    {
        if (subscribers.ContainsKey(key))
        {
            subscribers[key].Remove(callback);
            
            if (subscribers[key].Count == 0)
                subscribers.Remove(key);
        }
    }
    
    private void NotifySubscribers(string key, object value)
    {
        if (subscribers.TryGetValue(key, out var callbacks))
        {
            foreach (var callback in callbacks.ToList()) // ToList previene modificación durante iteración
            {
                try
                {
                    callback?.Invoke(value);
                }
                catch (System.Exception e)
                {
                    Logger.LogError($"Blackboard: Error notifying subscriber for {key}: {e.Message}");
                }
            }
        }
    }
    
    private void UpdateDebugEntries()
    {
        debugEntries.Clear();
        
        foreach (var kvp in data)
        {
            debugEntries.Add(new BlackboardEntry
            {
                key = kvp.Key,
                value = kvp.Value?.ToString() ?? "null",
                type = kvp.Value?.GetType().Name ?? "null"
            });
        }
    }
    
    // Método para limpiar datos temporales
    public void CleanupTemporaryData()
    {
        var panicAreas = GetValue<List<Vector3>>(BlackboardKeys.CIVILIAN_PANIC_AREAS);
        if (panicAreas != null && panicAreas.Count > 10) // Límite para performance
        {
            panicAreas.RemoveRange(0, panicAreas.Count - 5);
            SetValue(BlackboardKeys.CIVILIAN_PANIC_AREAS, panicAreas);
        }
        
        var chasingGuards = GetValue<List<Transform>>(BlackboardKeys.GUARDS_CHASING);
        if (chasingGuards != null)
        {
            chasingGuards.RemoveAll(g => g == null); // Remover referencias nulas
            SetValue(BlackboardKeys.GUARDS_CHASING, chasingGuards);
        }
    }
    
    // Para debugging en inspector
    [System.Serializable]
    private class BlackboardEntry
    {
        public string key;
        public string value;
        public string type;
    }
    
    #region Debug Methods
    [ContextMenu("Print All Data")]
    private void PrintAllData()
    {
        foreach (var kvp in data)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value} ({kvp.Value?.GetType().Name})");
        }
    }
    
    [ContextMenu("Clear All Data")]
    private void ClearAllData()
    {
        data.Clear();
        debugEntries.Clear();
        InitializeDefaultValues();
    }
    #endregion
}
```

### **AIPersonalityType.cs**
```csharp
public enum AIPersonalityType
{
    Aggressive,     // Guards que persiguen inmediatamente
    Conservative,   // Guards más cautelosos
    Civilian,       // NPCs no hostiles
    Player         // Para el player controller
}
```

---

## 🔧 **INTEGRACIÓN CON SISTEMA EXISTENTE**

### **Modificar GameManager o similar:**
```csharp
public class GameManager : MonoBehaviour
{
    [Header("AI System")]
    [SerializeField] private Blackboard blackboard;
    
    void Awake()
    {
        // Asegurar que Blackboard se registre primero
        if (blackboard == null)
            blackboard = FindObjectOfType<Blackboard>();
        
        if (blackboard == null)
        {
            GameObject blackboardObj = new GameObject("Blackboard");
            blackboard = blackboardObj.AddComponent<Blackboard>();
        }
    }
    
    void Start()
    {
        // Inicializar player reference en blackboard
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            blackboard.SetValue(BlackboardKeys.PLAYER_TRANSFORM, player.transform);
        }
    }
}
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Todas las interfaces creadas** y compilando
2. **✅ Blackboard funcionando** como servicio
3. **✅ BlackboardKeys definidas** para todo el proyecto
4. **✅ Player registrado** en el blackboard
5. **✅ Debug tools** funcionando en inspector
6. **✅ Integración con ServiceLocator** completa

### **Testing básico:**
```csharp
// En cualquier script de prueba:
void Start()
{
    var blackboard = ServiceLocator.Get<IBlackboard>();
    
    // Test escribir y leer
    blackboard.SetValue("test_key", "test_value");
    string value = blackboard.GetValue<string>("test_key");
    
    Debug.Log($"Blackboard test: {value}"); // Debe mostrar "test_value"
}
```

---

## 🚨 **PUNTOS CRÍTICOS**

1. **ServiceLocator**: Asegúrate de que el Blackboard se registre ANTES de que cualquier AI trate de usarlo
2. **Player Reference**: El player debe estar taggeado como "Player" para ser encontrado automáticamente
3. **Memory Management**: El Blackboard puede acumular datos, usa CleanupTemporaryData() periódicamente
4. **Thread Safety**: Todos los accesos deben ser desde el main thread de Unity

---

## 📁 **ESTRUCTURA DE ARCHIVOS RESULTANTE**

```
Assets/2. Scripts/
├── AI/
│   ├── Interfaces/
│   │   ├── IBlackboard.cs
│   │   ├── IDecisionNode.cs
│   │   ├── IAIContext.cs
│   │   ├── IPlayerDetector.cs
│   │   └── IAIMovementController.cs
│   ├── Blackboard/
│   │   ├── Blackboard.cs
│   │   ├── BlackboardKeys.cs
│   │   └── AIPersonalityType.cs
│   └── Core/
│       └── AISystemInitializer.cs
```

Esta fase es la **fundación** de todo el sistema. Sin ella, las demás fases no pueden funcionar correctamente.