# 👁️ FASE 2: DETECCIÓN Y LINE OF SIGHT (Día 3)

## 🎯 **OBJETIVO DE LA FASE**
Implementar un sistema de **detección visual realista** que cumpla con el requisito obligatorio de **Line of Sight** del TP, permitiendo que los NPCs detecten al player de manera inteligente y natural.

---

## 📋 **PASO 3: SISTEMA DE DETECCIÓN COMPLETO**

### **¿QUÉ BUSCAMOS?**
Reemplazar la detección básica actual por un sistema completo que verifique:
1. **Distancia** al player
2. **Campo de visión** (field of view)
3. **Obstáculos** entre el NPC y el player (Line of Sight)
4. **Actualización del Blackboard** para coordinar con otros NPCs

### **Problema Actual en Guard.cs:**
```csharp
// Línea 10: Solo tiene el valor, no la lógica
[SerializeField] private float detectionRange = 8f;
[SerializeField] private float fieldOfView = 90f; // ❌ No se usa realmente

// No hay verificación de obstáculos
// No hay integración con blackboard
// Detección "omnisciente" irreal
```

---

## 🏗️ **IMPLEMENTACIÓN COMPLETA**

### **PlayerDetector.cs - Implementación Principal**
```csharp
public class PlayerDetector : MonoBehaviour, IPlayerDetector
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float fieldOfView = 90f;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float detectionUpdateRate = 0.1f;
    
    [Header("Layer Configuration")]
    [SerializeField] private LayerMask obstacleLayer = 1;
    [SerializeField] private LayerMask playerLayer = 1 << 6;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Advanced Settings")]
    [SerializeField] private bool usePeripheralVision = true;
    [SerializeField] private float peripheralMultiplier = 0.5f;
    [SerializeField] private bool useLightingAffection = false;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private bool enableDetectionLogs = true;
    
    // Runtime variables
    private Vector3 lastKnownPlayerPosition;
    private float lastSeenTime;
    private Transform cachedPlayerTransform;
    private IBlackboard blackboard;
    private bool playerCurrentlyVisible;
    
    // Performance optimization
    private Coroutine detectionCoroutine;
    
    void Start()
    {
        Initialize();
    }
    
    void OnEnable()
    {
        StartDetection();
    }
    
    void OnDisable()
    {
        StopDetection();
    }
    
    private void Initialize()
    {
        blackboard = ServiceLocator.Get<IBlackboard>();
        
        if (blackboard == null)
        {
            Logger.LogError($"PlayerDetector on {gameObject.name}: Blackboard not found!");
            enabled = false;
            return;
        }
        
        FindPlayerReference();
        
        if (enableDetectionLogs)
            Logger.LogDebug($"PlayerDetector on {gameObject.name}: Initialized");
    }
    
    private void FindPlayerReference()
    {
        if (cachedPlayerTransform == null)
        {
            // Primero intentar desde blackboard
            cachedPlayerTransform = blackboard.GetValue<Transform>(BlackboardKeys.PLAYER_TRANSFORM);
            
            // Si no está en blackboard, buscar por tag
            if (cachedPlayerTransform == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj != null)
                {
                    cachedPlayerTransform = playerObj.transform;
                    blackboard.SetValue(BlackboardKeys.PLAYER_TRANSFORM, cachedPlayerTransform);
                }
            }
        }
    }
    
    private void StartDetection()
    {
        if (detectionCoroutine == null)
        {
            detectionCoroutine = StartCoroutine(DetectionCoroutine());
        }
    }
    
    private void StopDetection()
    {
        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
            detectionCoroutine = null;
        }
    }
    
    private IEnumerator DetectionCoroutine()
    {
        while (enabled)
        {
            UpdateDetection();
            yield return new WaitForSeconds(detectionUpdateRate);
        }
    }
    
    private void UpdateDetection()
    {
        if (cachedPlayerTransform == null)
        {
            FindPlayerReference();
            return;
        }
        
        bool wasVisible = playerCurrentlyVisible;
        playerCurrentlyVisible = CanSeePlayer(cachedPlayerTransform);
        
        // Player detectado por primera vez
        if (playerCurrentlyVisible && !wasVisible)
        {
            OnPlayerDetected(cachedPlayerTransform.position);
        }
        // Player perdido de vista
        else if (!playerCurrentlyVisible && wasVisible)
        {
            OnPlayerLost();
        }
        // Player sigue visible - actualizar posición
        else if (playerCurrentlyVisible)
        {
            UpdatePlayerPosition(cachedPlayerTransform.position);
        }
    }
    
    public bool CanSeePlayer(Transform player)
    {
        if (player == null) return false;
        
        // 1. Verificar distancia
        if (!IsPlayerInRange(player, detectionRange))
        {
            return false;
        }
        
        // 2. Verificar campo de visión
        if (!IsPlayerInFieldOfView(player))
        {
            // Verificar visión periférica si está habilitada
            if (usePeripheralVision && IsPlayerInPeripheralVision(player))
            {
                // En visión periférica, reduce la distancia efectiva
                float peripheralRange = detectionRange * peripheralMultiplier;
                if (!IsPlayerInRange(player, peripheralRange))
                    return false;
            }
            else
            {
                return false;
            }
        }
        
        // 3. Verificar obstáculos (Line of Sight principal)
        if (HasObstaclesBetween(player))
        {
            return false;
        }
        
        // 4. Verificar iluminación (opcional)
        if (useLightingAffection && !IsPlayerProperlyLit(player))
        {
            return false;
        }
        
        return true;
    }
    
    public bool IsPlayerInRange(Transform player, float range)
    {
        Vector3 eyePosition = GetEyePosition();
        float distance = Vector3.Distance(eyePosition, player.position);
        return distance <= range;
    }
    
    public bool IsPlayerInFieldOfView(Transform player)
    {
        Vector3 eyePosition = GetEyePosition();
        Vector3 directionToPlayer = (player.position - eyePosition).normalized;
        
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        return angle <= fieldOfView / 2f;
    }
    
    private bool IsPlayerInPeripheralVision(Transform player)
    {
        Vector3 eyePosition = GetEyePosition();
        Vector3 directionToPlayer = (player.position - eyePosition).normalized;
        
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        
        // Visión periférica hasta 120 grados
        return angle <= 120f / 2f;
    }
    
    public bool HasObstaclesBetween(Transform player)
    {
        Vector3 eyePosition = GetEyePosition();
        Vector3 playerPosition = player.position + Vector3.up * 0.1f; // Ligeramente elevado
        
        Vector3 directionToPlayer = (playerPosition - eyePosition).normalized;
        float distanceToPlayer = Vector3.Distance(eyePosition, playerPosition);
        
        // Raycast principal
        if (Physics.Raycast(eyePosition, directionToPlayer, out RaycastHit hit, distanceToPlayer, obstacleLayer))
        {
            // Verificar si lo que golpeamos es el player (puede estar en obstacle layer)
            if (hit.collider.CompareTag(playerTag))
                return false;
            
            if (enableDetectionLogs)
                Logger.LogDebug($"{gameObject.name}: LoS blocked by {hit.collider.name}");
            
            return true;
        }
        
        // Raycast adicional ligeramente hacia arriba (para cubrir players agachados)
        Vector3 upperDirection = (playerPosition + Vector3.up * 0.5f - eyePosition).normalized;
        if (Physics.Raycast(eyePosition, upperDirection, distanceToPlayer, obstacleLayer))
        {
            return true;
        }
        
        return false;
    }
    
    private bool IsPlayerProperlyLit(Transform player)
    {
        // Implementación básica - se puede expandir con sistema de iluminación real
        // Por ahora, asumimos que áreas con luz directa son detectables
        
        RaycastHit hit;
        Vector3 playerPos = player.position + Vector3.up * 1f;
        
        // Verificar si hay luz solar directa
        if (Physics.Raycast(playerPos, Vector3.up, out hit, 50f))
        {
            return false; // Está bajo techo/sombra
        }
        
        return true; // Está bajo luz directa
    }
    
    private Vector3 GetEyePosition()
    {
        return transform.position + Vector3.up * eyeHeight;
    }
    
    public float GetDistanceToPlayer(Transform player)
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(GetEyePosition(), player.position);
    }
    
    public Vector3 GetLastKnownPlayerPosition()
    {
        return lastKnownPlayerPosition;
    }
    
    public float GetTimeSinceLastSeen()
    {
        return Time.time - lastSeenTime;
    }
    
    private void OnPlayerDetected(Vector3 playerPosition)
    {
        lastKnownPlayerPosition = playerPosition;
        lastSeenTime = Time.time;
        
        // Actualizar blackboard para coordinar con otros NPCs
        blackboard.SetValue(BlackboardKeys.PLAYER_DETECTED, true);
        blackboard.SetValue(BlackboardKeys.PLAYER_LAST_SEEN, playerPosition);
        blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, playerPosition);
        
        // Actualizar nivel de alerta
        int currentAlertLevel = blackboard.GetValue<int>(BlackboardKeys.ALERT_LEVEL);
        if (currentAlertLevel < 2)
        {
            blackboard.SetValue(BlackboardKeys.ALERT_LEVEL, 2);
            blackboard.SetValue(BlackboardKeys.ALERT_POSITION, playerPosition);
            blackboard.SetValue(BlackboardKeys.ALERT_TIME, Time.time);
        }
        
        if (enableDetectionLogs)
            Logger.LogDebug($"{gameObject.name}: PLAYER DETECTED at {playerPosition}");
        
        // Notificar al NPC propietario
        var aiContext = GetComponent<IAIContext>();
        if (aiContext != null)
        {
            NotifyOwnerAI("PlayerDetected", playerPosition);
        }
    }
    
    private void OnPlayerLost()
    {
        if (enableDetectionLogs)
            Logger.LogDebug($"{gameObject.name}: Player lost from sight");
        
        // No actualizar blackboard inmediatamente - otros NPCs pueden seguir viendo
        // Solo actualizar la información local
        
        NotifyOwnerAI("PlayerLost", lastKnownPlayerPosition);
    }
    
    private void UpdatePlayerPosition(Vector3 newPosition)
    {
        lastKnownPlayerPosition = newPosition;
        lastSeenTime = Time.time;
        
        // Actualizar blackboard con nueva posición
        blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, newPosition);
        blackboard.SetValue(BlackboardKeys.PLAYER_LAST_SEEN, newPosition);
    }
    
    private void NotifyOwnerAI(string eventType, Vector3 position)
    {
        // Notificar a Guard si es Guard
        var guard = GetComponent<Guard>();
        if (guard != null)
        {
            guard.LastKnownPlayerPosition = position;
        }
        
        // Notificar a Civilian si es Civilian
        var civilian = GetComponent<CivilianAI>();
        if (civilian != null)
        {
            // Los civiles pueden reaccionar inmediatamente
        }
    }
    
    #region Debug Visualization
    void OnDrawGizmos()
    {
        if (!showDebugRays || !Application.isPlaying) return;
        
        DrawDetectionRange();
        DrawFieldOfView();
        DrawLineOfSight();
    }
    
    private void DrawDetectionRange()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetEyePosition(), detectionRange);
        
        if (usePeripheralVision)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetEyePosition(), detectionRange * peripheralMultiplier);
        }
    }
    
    private void DrawFieldOfView()
    {
        Vector3 eyePos = GetEyePosition();
        float halfFOV = fieldOfView / 2f;
        
        // Campo de visión principal
        Gizmos.color = playerCurrentlyVisible ? Color.green : Color.blue;
        
        Vector3 leftBoundary = Quaternion.AngleAxis(-halfFOV, Vector3.up) * transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.AngleAxis(halfFOV, Vector3.up) * transform.forward * detectionRange;
        
        Gizmos.DrawRay(eyePos, leftBoundary);
        Gizmos.DrawRay(eyePos, rightBoundary);
        
        // Conectar los extremos
        Gizmos.DrawLine(eyePos + leftBoundary, eyePos + rightBoundary);
        
        // Visión periférica
        if (usePeripheralVision)
        {
            Gizmos.color = Color.cyan;
            float peripheralHalf = 120f / 2f;
            Vector3 leftPeripheral = Quaternion.AngleAxis(-peripheralHalf, Vector3.up) * transform.forward * (detectionRange * peripheralMultiplier);
            Vector3 rightPeripheral = Quaternion.AngleAxis(peripheralHalf, Vector3.up) * transform.forward * (detectionRange * peripheralMultiplier);
            
            Gizmos.DrawRay(eyePos, leftPeripheral);
            Gizmos.DrawRay(eyePos, rightPeripheral);
        }
    }
    
    private void DrawLineOfSight()
    {
        if (cachedPlayerTransform == null) return;
        
        Vector3 eyePos = GetEyePosition();
        Vector3 playerPos = cachedPlayerTransform.position;
        
        // Raycast hacia el player
        bool hasObstacle = HasObstaclesBetween(cachedPlayerTransform);
        Gizmos.color = hasObstacle ? Color.red : Color.green;
        Gizmos.DrawLine(eyePos, playerPos);
        
        // Punto en la última posición conocida
        if (lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.orange;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 0.5f);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Información adicional cuando está seleccionado
        Vector3 eyePos = GetEyePosition();
        
        // Altura de los ojos
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, eyePos);
        Gizmos.DrawWireSphere(eyePos, 0.1f);
        
        // Dirección forward
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(eyePos, transform.forward * 2f);
    }
    #endregion
    
    #region Context Menu Debug
    [ContextMenu("Test Player Detection")]
    private void TestPlayerDetection()
    {
        if (cachedPlayerTransform != null)
        {
            bool canSee = CanSeePlayer(cachedPlayerTransform);
            Debug.Log($"Can see player: {canSee}");
            Debug.Log($"Distance: {GetDistanceToPlayer(cachedPlayerTransform):F2}");
            Debug.Log($"In FOV: {IsPlayerInFieldOfView(cachedPlayerTransform)}");
            Debug.Log($"Has obstacles: {HasObstaclesBetween(cachedPlayerTransform)}");
        }
        else
        {
            Debug.Log("Player not found!");
        }
    }
    
    [ContextMenu("Force Player Detection")]
    private void ForcePlayerDetection()
    {
        if (cachedPlayerTransform != null)
        {
            OnPlayerDetected(cachedPlayerTransform.position);
        }
    }
    #endregion
}
```

---

## 🔧 **INTEGRACIÓN CON GUARD EXISTENTE**

### **Modificar Guard.cs para usar PlayerDetector:**
```csharp
public class Guard : BaseCharacter, IUpdatable, IUseFsm, IAIContext
{
    // Mantener configuración existente pero marcar como legacy
    [Header("Detection Settings (Legacy - usar PlayerDetector)")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float fieldOfView = 90f;
    
    // Nuevos componentes
    private IPlayerDetector playerDetector;
    private IBlackboard blackboard;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Inicializar nuevos componentes
        playerDetector = GetComponent<IPlayerDetector>();
        blackboard = ServiceLocator.Get<IBlackboard>();
        
        // Asegurar que PlayerDetector existe
        if (playerDetector == null)
        {
            var detectorComponent = gameObject.AddComponent<PlayerDetector>();
            playerDetector = detectorComponent;
            
            Logger.LogWarning($"Guard {gameObject.name}: PlayerDetector was missing, added automatically");
        }
        
        InitializeStateMachine();
        SetupPatrolPoints();
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.RegisterUpdatable(this);
    }
    
    // Modificar Player property para usar blackboard
    public Transform Player => blackboard?.GetValue<Transform>(BlackboardKeys.PLAYER_TRANSFORM);
    
    // Nuevo método para verificar visión usando PlayerDetector
    public bool CanSeePlayer()
    {
        return playerDetector?.CanSeePlayer(Player) ?? false;
    }
    
    // Propiedades actualizadas para usar PlayerDetector
    public float EffectiveDetectionRange => playerDetector?.GetDistanceToPlayer(Player) ?? detectionRange;
    
    #region IAIContext Implementation
    public Transform GetTransform() => transform;
    public IBlackboard GetBlackboard() => blackboard;
    
    public bool IsPlayerVisible()
    {
        return playerDetector?.CanSeePlayer(Player) ?? false;
    }
    
    public Vector3 GetPlayerPosition()
    {
        return Player?.position ?? Vector3.zero;
    }
    
    public float GetDistanceToPlayer()
    {
        if (Player == null) return float.MaxValue;
        return Vector3.Distance(transform.position, Player.position);
    }
    
    public float GetDetectionRange() => detectionRange;
    
    public AIPersonalityType GetPersonalityType() => AIPersonalityType.Aggressive; // Por defecto
    #endregion
}
```

---

## 🎮 **CONFIGURACIÓN EN UNITY**

### **Setup en Inspector:**
1. **Agregar PlayerDetector** a todos los Guards
2. **Configurar layers**:
   - Layer 0: Default (obstáculos)
   - Layer 6: Player
   - Layer 7: NPCs
3. **Tags requeridos**:
   - "Player" para el player
   - "Guard" para guards
   - "Civilian" para civilians

### **Configuración recomendada:**
```csharp
// Para Guards agresivos:
detectionRange = 10f;
fieldOfView = 90f;
eyeHeight = 1.6f;
detectionUpdateRate = 0.1f;

// Para Guards conservadores:
detectionRange = 8f;
fieldOfView = 70f;
eyeHeight = 1.6f;
detectionUpdateRate = 0.15f;

// Para Civilians:
detectionRange = 6f;
fieldOfView = 120f; // Mejor visión periférica
eyeHeight = 1.5f;
detectionUpdateRate = 0.2f; // Menos frecuente
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Line of Sight funcional** con raycast de obstáculos
2. **✅ Field of View real** (no solo decorativo)
3. **✅ Integración con Blackboard** para compartir detecciones
4. **✅ Debug visualization** completa con Gizmos
5. **✅ Performance optimizada** con corrutinas
6. **✅ Múltiples tipos de detección** (principal, periférica, iluminación)

### **Testing:**
1. **Colocar obstáculos** entre Guard y Player - no debe detectar
2. **Player fuera de FOV** - no debe detectar
3. **Player muy lejos** - no debe detectar
4. **Detección exitosa** - debe actualizar blackboard y mostrar gizmos verdes

---

## 🚨 **PUNTOS CRÍTICOS**

1. **Layers correctos**: Player y obstáculos deben estar en layers diferentes
2. **Performance**: No usar Update(), usar corrutinas con intervalos
3. **Null checks**: Siempre verificar que Player existe
4. **Blackboard dependency**: PlayerDetector requiere Blackboard funcionando

Esta fase transforma NPCs "omniscientes" en entidades con visión realista, cumpliendo el requisito crítico de Line of Sight del TP.