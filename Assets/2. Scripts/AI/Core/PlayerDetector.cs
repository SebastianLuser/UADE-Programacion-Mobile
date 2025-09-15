using System.Collections;
using UnityEngine;

/// <summary>
/// Advanced player detection system implementing realistic Line of Sight.
/// Integrates with the blackboard system for AI coordination.
/// 
/// MEJORA: Sistema de cache inteligente para performance
/// MEJORA: Detección gradual con niveles de alerta
/// MEJORA: Integración completa con blackboard para coordinación
/// MEJORA: Debug visualization completo con gizmos
/// MEJORA: Support para múltiples tipos de detección (visual, auditiva, etc.)
/// </summary>
public class PlayerDetector : MonoBehaviour, IPlayerDetector
{
    [Header("Detection Configuration")]
    [SerializeField] private DetectionConfig config = DetectionConfig.GetDefault(AIPersonalityType.Conservative);
    
    [Header("Personality Integration")]
    [SerializeField] private AIPersonalityType personalityType = AIPersonalityType.Conservative;
    [SerializeField] private bool autoConfigureFromPersonality = true;
    
    [Header("Performance Optimization")]
    [SerializeField] private bool useFrameCaching = true;
    [SerializeField] private int maxCacheFrames = 3;
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool enableDetectionLogs = false;
    [SerializeField] private bool showDetectionInfo = true;
    
    // Core detection state
    private DetectionResult lastDetectionResult = DetectionResult.None;
    private Transform cachedPlayerTransform;
    private IBlackboard blackboard;
    
    // Performance caching
    private int lastUpdateFrame = -1;
    private DetectionResult cachedResult = DetectionResult.None;
    
    // Runtime tracking
    private Vector3 lastKnownPlayerPosition = Vector3.zero;
    private float lastSeenTime = -1f;
    private PlayerDetectionLevel previousDetectionLevel = PlayerDetectionLevel.None;
    
    // Coroutine management
    private Coroutine detectionCoroutine;
    
    // Debug info
    private string lastBlockingObject = "";
    private Color currentGizmoColor = Color.yellow;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        if (autoConfigureFromPersonality)
        {
            config = DetectionConfig.GetDefault(personalityType);
        }
    }
    
    private void Start()
    {
        Initialize();
    }
    
    private void OnEnable()
    {
        StartDetectionCoroutine();
    }
    
    private void OnDisable()
    {
        StopDetectionCoroutine();
    }
    
    private void OnDestroy()
    {
        StopDetectionCoroutine();
    }
    
    #endregion
    
    #region Initialization
    
    private void Initialize()
    {
        // Get blackboard service
        blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null)
        {
            Logger.LogError($"PlayerDetector on {gameObject.name}: Blackboard service not found! Detection will not work properly.");
            enabled = false;
            return;
        }
        
        // Find player reference
        FindPlayerReference();
        
        // Apply personality-based config if needed
        if (autoConfigureFromPersonality)
        {
            ApplyPersonalityConfig();
        }
        
        if (enableDetectionLogs)
            Logger.LogInfo($"PlayerDetector on {gameObject.name}: Initialized with personality {personalityType}");
    }
    
    private void FindPlayerReference()
    {
        // First try to get from blackboard
        cachedPlayerTransform = blackboard?.GetValue<Transform>(BlackboardKeys.PLAYER_TRANSFORM);
        
        // Fallback to finding by tag
        if (cachedPlayerTransform == null)
        {
            var playerObject = GameObject.FindGameObjectWithTag(config.playerTag);
            if (playerObject != null)
            {
                cachedPlayerTransform = playerObject.transform;
                
                // Update blackboard with found player
                blackboard?.SetValue(BlackboardKeys.PLAYER_TRANSFORM, cachedPlayerTransform);
                
                if (enableDetectionLogs)
                    Logger.LogInfo($"PlayerDetector: Found player by tag '{config.playerTag}'");
            }
            else
            {
                Logger.LogWarning($"PlayerDetector on {gameObject.name}: Player not found with tag '{config.playerTag}'");
            }
        }
    }
    
    private void ApplyPersonalityConfig()
    {
        // MEJORA: Configuración automática basada en personalidad
        // Esto permite balancing centralizado sin modificar cada prefab
        var newConfig = DetectionConfig.GetDefault(personalityType);
        
        // Preserve any manual overrides that make sense
        newConfig.obstacleLayerMask = config.obstacleLayerMask;
        newConfig.playerLayerMask = config.playerLayerMask;
        newConfig.playerTag = config.playerTag;
        
        config = newConfig;
        
        if (enableDetectionLogs)
            Logger.LogInfo($"PlayerDetector: Applied {personalityType} personality config");
    }
    
    #endregion
    
    #region Detection Coroutine Management
    
    private void StartDetectionCoroutine()
    {
        if (detectionCoroutine == null)
        {
            detectionCoroutine = StartCoroutine(DetectionUpdateCoroutine());
        }
    }
    
    private void StopDetectionCoroutine()
    {
        if (detectionCoroutine != null)
        {
            StopCoroutine(detectionCoroutine);
            detectionCoroutine = null;
        }
    }
    
    private IEnumerator DetectionUpdateCoroutine()
    {
        while (enabled && gameObject.activeInHierarchy)
        {
            UpdateDetection();
            yield return new WaitForSeconds(config.updateRate);
        }
    }
    
    #endregion
    
    #region Core Detection Logic
    
    private void UpdateDetection()
    {
        if (cachedPlayerTransform == null)
        {
            FindPlayerReference();
            return;
        }
        
        // Perform detection
        var currentResult = PerformDetection(cachedPlayerTransform);
        
        // Check for level changes
        if (currentResult.level != previousDetectionLevel)
        {
            OnDetectionLevelChanged(previousDetectionLevel, currentResult.level);
            previousDetectionLevel = currentResult.level;
        }
        
        // Update blackboard if significant detection
        if (currentResult.IsSignificant)
        {
            UpdateBlackboardWithDetection(currentResult);
        }
        
        // Store result
        lastDetectionResult = currentResult;
        
        // Update gizmo color for visualization
        UpdateGizmoColor(currentResult.level);
    }
    
    private DetectionResult PerformDetection(Transform player)
    {
        // MEJORA: Frame caching para performance
        if (useFrameCaching && lastUpdateFrame == Time.frameCount)
        {
            return cachedResult;
        }
        
        if (player == null)
        {
            return DetectionResult.None;
        }
        
        // Get positions
        Vector3 eyePosition = GetEyePosition();
        Vector3 playerPosition = player.position;
        
        // Calculate basic metrics
        float distance = Vector3.Distance(eyePosition, playerPosition);
        Vector3 directionToPlayer = (playerPosition - eyePosition).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        
        // Check distance first (early exit)
        if (distance > config.detectionRange)
        {
            var result = new DetectionResult(
                PlayerDetectionLevel.None, false, false, false,
                distance, angle, lastKnownPlayerPosition, GetTimeSinceLastSeen(), ""
            );
            
            CacheResult(result);
            return result;
        }
        
        // Check field of view
        bool inMainFOV = IsInFieldOfView(angle, config.fieldOfView);
        bool inPeripheralFOV = config.usePeripheralVision && IsInFieldOfView(angle, 120f);
        
        if (!inMainFOV && !inPeripheralFOV)
        {
            var result = new DetectionResult(
                PlayerDetectionLevel.None, false, false, false,
                distance, angle, lastKnownPlayerPosition, GetTimeSinceLastSeen(), ""
            );
            
            CacheResult(result);
            return result;
        }
        
        // Check line of sight
        bool hasLineOfSight = CheckLineOfSight(eyePosition, playerPosition, out string blocker);
        
        // Determine detection level
        PlayerDetectionLevel level = CalculateDetectionLevel(distance, angle, inMainFOV, inPeripheralFOV, hasLineOfSight);
        
        // Update tracking info if visible
        if (hasLineOfSight)
        {
            lastKnownPlayerPosition = playerPosition;
            lastSeenTime = Time.time;
        }
        
        var finalResult = new DetectionResult(
            level, hasLineOfSight, inMainFOV || inPeripheralFOV, hasLineOfSight,
            distance, angle, lastKnownPlayerPosition, GetTimeSinceLastSeen(), blocker
        );
        
        CacheResult(finalResult);
        return finalResult;
    }
    
    private PlayerDetectionLevel CalculateDetectionLevel(float distance, float angle, bool inMainFOV, bool inPeripheralFOV, bool hasLineOfSight)
    {
        if (!hasLineOfSight)
        {
            return PlayerDetectionLevel.None;
        }
        
        // MEJORA: Sistema gradual de detección para comportamientos más naturales
        float maxRange = config.detectionRange;
        float immediateRange = maxRange * 0.25f;
        float clearRange = maxRange * 0.5f;
        float partialRange = maxRange * 0.75f;
        
        if (distance <= immediateRange && inMainFOV)
        {
            return PlayerDetectionLevel.Immediate;
        }
        else if (distance <= clearRange && inMainFOV)
        {
            return PlayerDetectionLevel.Clear;
        }
        else if (distance <= partialRange && inMainFOV)
        {
            return PlayerDetectionLevel.Partial;
        }
        else if (inPeripheralFOV && distance <= maxRange * config.peripheralMultiplier)
        {
            return PlayerDetectionLevel.Peripheral;
        }
        
        return PlayerDetectionLevel.None;
    }
    
    #endregion
    
    #region IPlayerDetector Implementation
    
    public bool CanSeePlayer(Transform player)
    {
        if (player == null) return false;
        
        var result = PerformDetection(player);
        return result.canSeePlayer;
    }
    
    public float GetDistanceToPlayer(Transform player)
    {
        if (player == null) return float.MaxValue;
        
        return Vector3.Distance(GetEyePosition(), player.position);
    }
    
    public bool IsPlayerInRange(Transform player, float range)
    {
        return GetDistanceToPlayer(player) <= range;
    }
    
    public Vector3 GetLastKnownPlayerPosition()
    {
        return lastKnownPlayerPosition;
    }
    
    public float GetTimeSinceLastSeen()
    {
        return lastSeenTime > 0 ? Time.time - lastSeenTime : float.MaxValue;
    }
    
    public void SetDetectionParameters(float detectionRange, float fieldOfView, LayerMask obstacleLayerMask)
    {
        config.detectionRange = detectionRange;
        config.fieldOfView = fieldOfView;
        config.obstacleLayerMask = obstacleLayerMask;
        
        if (enableDetectionLogs)
            Logger.LogInfo($"PlayerDetector: Updated parameters - Range: {detectionRange}, FOV: {fieldOfView}");
    }
    
    public bool CanHearPlayer(Transform player, float noiseLevel = 1f)
    {
        if (!config.useNoiseDection || player == null) return false;
        
        // MEJORA: Implementación básica de detección auditiva
        float hearingRange = config.detectionRange * 0.5f * noiseLevel;
        return GetDistanceToPlayer(player) <= hearingRange;
    }
    
    public Vector3 GetPredictedPlayerPosition(float predictionTime = 1f)
    {
        if (cachedPlayerTransform == null) return Vector3.zero;
        
        // MEJORA: Predicción simple basada en velocidad del player
        var playerRigidbody = cachedPlayerTransform.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            return cachedPlayerTransform.position + playerRigidbody.linearVelocity * predictionTime;
        }
        
        return cachedPlayerTransform.position;
    }
    
    public float GetAngleToPlayer(Transform player)
    {
        if (player == null) return 0f;
        
        Vector3 directionToPlayer = (player.position - GetEyePosition()).normalized;
        return Vector3.Angle(transform.forward, directionToPlayer);
    }
    
    public bool IsPlayerInFieldOfView(Transform player)
    {
        if (player == null) return false;
        
        float angle = GetAngleToPlayer(player);
        return IsInFieldOfView(angle, config.fieldOfView);
    }
    
    public void InvalidateCache()
    {
        lastUpdateFrame = -1;
        cachedResult = DetectionResult.None;
    }
    
    public (Vector3 position, float range, float fov, bool hasLOS) GetDebugInfo()
    {
        return (GetEyePosition(), config.detectionRange, config.fieldOfView, lastDetectionResult.hasLineOfSight);
    }
    
    #endregion
    
    #region Helper Methods
    
    private Vector3 GetEyePosition()
    {
        return transform.position + Vector3.up * config.eyeHeight;
    }
    
    private bool IsInFieldOfView(float angle, float fieldOfView)
    {
        return angle <= fieldOfView * 0.5f;
    }
    
    private bool CheckLineOfSight(Vector3 fromPosition, Vector3 toPosition, out string blocker)
    {
        blocker = "";
        
        Vector3 direction = (toPosition - fromPosition).normalized;
        float distance = Vector3.Distance(fromPosition, toPosition);
        
        // Main raycast
        if (Physics.Raycast(fromPosition, direction, out RaycastHit hit, distance, config.obstacleLayerMask))
        {
            // Check if we hit the player (player might be on obstacle layer)
            if (hit.collider.CompareTag(config.playerTag))
            {
                return true;
            }
            
            blocker = hit.collider.name;
            lastBlockingObject = blocker;
            return false;
        }
        
        // Additional raycast slightly upward for crouching players
        Vector3 upperDirection = (toPosition + Vector3.up * 0.5f - fromPosition).normalized;
        if (Physics.Raycast(fromPosition, upperDirection, distance, config.obstacleLayerMask))
        {
            return false;
        }
        
        lastBlockingObject = "";
        return true;
    }
    
    private void CacheResult(DetectionResult result)
    {
        if (config.useCache)
        {
            cachedResult = result;
            lastUpdateFrame = Time.frameCount;
        }
    }
    
    #endregion
    
    #region Event Handling
    
    private void OnDetectionLevelChanged(PlayerDetectionLevel previousLevel, PlayerDetectionLevel newLevel)
    {
        if (enableDetectionLogs)
            Logger.LogInfo($"PlayerDetector ({gameObject.name}): Detection level changed from {previousLevel} to {newLevel}");
        
        // Notify blackboard of significant changes
        if (newLevel >= PlayerDetectionLevel.Partial && previousLevel < PlayerDetectionLevel.Partial)
        {
            // Player detected for first time
            NotifyBlackboardPlayerDetected();
        }
        else if (newLevel < PlayerDetectionLevel.Partial && previousLevel >= PlayerDetectionLevel.Partial)
        {
            // Player lost
            NotifyBlackboardPlayerLost();
        }
        
        // Notify owner AI component
        NotifyOwnerAI(previousLevel, newLevel);
    }
    
    private void UpdateBlackboardWithDetection(DetectionResult result)
    {
        if (blackboard == null) return;
        
        blackboard.SetValue(BlackboardKeys.PLAYER_POSITION, result.lastKnownPosition);
        blackboard.SetValue(BlackboardKeys.PLAYER_LAST_SEEN, result.lastKnownPosition);
        blackboard.SetValue(BlackboardKeys.PLAYER_LAST_SEEN_TIME, Time.time);
        
        // Update alert level based on detection
        int currentAlertLevel = blackboard.GetValue<int>(BlackboardKeys.ALERT_LEVEL);
        int suggestedAlertLevel = GetAlertLevelForDetection(result.level);
        
        if (suggestedAlertLevel > currentAlertLevel)
        {
            blackboard.SetValue(BlackboardKeys.ALERT_LEVEL, suggestedAlertLevel);
            blackboard.SetValue(BlackboardKeys.ALERT_POSITION, result.lastKnownPosition);
            blackboard.SetValue(BlackboardKeys.ALERT_TIME, Time.time);
            blackboard.SetValue(BlackboardKeys.LAST_ALERT_SOURCE, transform);
        }
    }
    
    private int GetAlertLevelForDetection(PlayerDetectionLevel detectionLevel)
    {
        return detectionLevel switch
        {
            PlayerDetectionLevel.None => 0,
            PlayerDetectionLevel.Peripheral => 1,
            PlayerDetectionLevel.Partial => 2,
            PlayerDetectionLevel.Clear => 3,
            PlayerDetectionLevel.Immediate => 3,
            _ => 0
        };
    }
    
    private void NotifyBlackboardPlayerDetected()
    {
        if (blackboard == null) return;
        
        blackboard.SetValue(BlackboardKeys.PLAYER_DETECTED, true);
        
        // Add this detector to investigating list
        var investigating = blackboard.GetValue<System.Collections.Generic.List<Transform>>(BlackboardKeys.GUARDS_INVESTIGATING) 
                           ?? new System.Collections.Generic.List<Transform>();
        
        if (!investigating.Contains(transform))
        {
            investigating.Add(transform);
            blackboard.SetValue(BlackboardKeys.GUARDS_INVESTIGATING, investigating);
        }
    }
    
    private void NotifyBlackboardPlayerLost()
    {
        // Don't immediately update PLAYER_DETECTED to false - other AIs might still see player
        // Just remove this detector from investigating list
        if (blackboard == null) return;
        
        var investigating = blackboard.GetValue<System.Collections.Generic.List<Transform>>(BlackboardKeys.GUARDS_INVESTIGATING);
        if (investigating != null && investigating.Contains(transform))
        {
            investigating.Remove(transform);
            blackboard.SetValue(BlackboardKeys.GUARDS_INVESTIGATING, investigating);
        }
    }
    
    private void NotifyOwnerAI(PlayerDetectionLevel previousLevel, PlayerDetectionLevel newLevel)
    {
        // Notify Guard component if present
        var guard = GetComponent<Guard>();
        if (guard != null)
        {
            guard.LastKnownPlayerPosition = lastKnownPlayerPosition;
        }
        
        // Could notify other AI components here as needed
    }
    
    private void UpdateGizmoColor(PlayerDetectionLevel level)
    {
        currentGizmoColor = level switch
        {
            PlayerDetectionLevel.None => Color.gray,
            PlayerDetectionLevel.Peripheral => Color.yellow,
            PlayerDetectionLevel.Partial => Color.orange,
            PlayerDetectionLevel.Clear => Color.red,
            PlayerDetectionLevel.Immediate => Color.magenta,
            _ => Color.gray
        };
    }
    
    #endregion
    
    #region Debug and Visualization
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        DrawDetectionRange();
        
        if (Application.isPlaying)
        {
            DrawFieldOfView();
            DrawLineOfSight();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        DrawDetailedDebugInfo();
    }
    
    private void DrawDetectionRange()
    {
        Vector3 eyePos = GetEyePosition();
        
        // Main detection range
        Gizmos.color = new Color(currentGizmoColor.r, currentGizmoColor.g, currentGizmoColor.b, 0.3f);
        Gizmos.DrawWireSphere(eyePos, config.detectionRange);
        
        // Peripheral range if enabled
        if (config.usePeripheralVision)
        {
            Gizmos.color = new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b, 0.2f);
            Gizmos.DrawWireSphere(eyePos, config.detectionRange * config.peripheralMultiplier);
        }
    }
    
    private void DrawFieldOfView()
    {
        Vector3 eyePos = GetEyePosition();
        
        // Main field of view
        Gizmos.color = currentGizmoColor;
        float halfFOV = config.fieldOfView * 0.5f;
        
        Vector3 leftBoundary = Quaternion.AngleAxis(-halfFOV, Vector3.up) * transform.forward * config.detectionRange;
        Vector3 rightBoundary = Quaternion.AngleAxis(halfFOV, Vector3.up) * transform.forward * config.detectionRange;
        
        Gizmos.DrawRay(eyePos, leftBoundary);
        Gizmos.DrawRay(eyePos, rightBoundary);
        
        // Peripheral vision if enabled
        if (config.usePeripheralVision)
        {
            Gizmos.color = Color.cyan;
            float peripheralHalf = 60f; // 120° total
            Vector3 leftPeripheral = Quaternion.AngleAxis(-peripheralHalf, Vector3.up) * transform.forward * (config.detectionRange * config.peripheralMultiplier);
            Vector3 rightPeripheral = Quaternion.AngleAxis(peripheralHalf, Vector3.up) * transform.forward * (config.detectionRange * config.peripheralMultiplier);
            
            Gizmos.DrawRay(eyePos, leftPeripheral);
            Gizmos.DrawRay(eyePos, rightPeripheral);
        }
    }
    
    private void DrawLineOfSight()
    {
        if (cachedPlayerTransform == null) return;
        
        Vector3 eyePos = GetEyePosition();
        Vector3 playerPos = cachedPlayerTransform.position;
        
        // Line to player
        Gizmos.color = lastDetectionResult.hasLineOfSight ? Color.green : Color.red;
        Gizmos.DrawLine(eyePos, playerPos);
        
        // Last known position
        if (lastKnownPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.orange;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 0.5f);
            
            // Line to last known position
            Gizmos.color = new Color(Color.orange.r, Color.orange.g, Color.orange.b, 0.5f);
            Gizmos.DrawLine(eyePos, lastKnownPlayerPosition);
        }
    }
    
    private void DrawDetailedDebugInfo()
    {
        Vector3 eyePos = GetEyePosition();
        
        // Eye position
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(eyePos, 0.1f);
        Gizmos.DrawLine(transform.position, eyePos);
        
        // Forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(eyePos, transform.forward * 2f);
        
        // Detection level indicators
        if (Application.isPlaying)
        {
            Gizmos.color = currentGizmoColor;
            Gizmos.DrawWireCube(eyePos + Vector3.up * 0.5f, Vector3.one * 0.2f);
        }
    }
    
    #endregion
    
    #region Context Menu Debug
    
    [ContextMenu("Test Detection")]
    private void TestDetection()
    {
        if (cachedPlayerTransform != null)
        {
            var result = PerformDetection(cachedPlayerTransform);
            Debug.Log($"=== DETECTION TEST RESULTS ===");
            Debug.Log($"Detection Level: {result.level}");
            Debug.Log($"Can See Player: {result.canSeePlayer}");
            Debug.Log($"In Field of View: {result.inFieldOfView}");
            Debug.Log($"Has Line of Sight: {result.hasLineOfSight}");
            Debug.Log($"Distance: {result.distance:F2}");
            Debug.Log($"Angle: {result.angle:F1}°");
            Debug.Log($"Blocked By: {result.blockedBy}");
            Debug.Log($"Time Since Last Seen: {result.timeSinceLastSeen:F1}s");
        }
        else
        {
            Debug.Log("Player not found for testing!");
        }
    }
    
    [ContextMenu("Force Player Detection")]
    private void ForcePlayerDetection()
    {
        if (cachedPlayerTransform != null)
        {
            lastKnownPlayerPosition = cachedPlayerTransform.position;
            lastSeenTime = Time.time;
            OnDetectionLevelChanged(PlayerDetectionLevel.None, PlayerDetectionLevel.Clear);
            Debug.Log("Forced player detection!");
        }
    }
    
    [ContextMenu("Reset Detection State")]
    private void ResetDetectionState()
    {
        lastDetectionResult = DetectionResult.None;
        lastKnownPlayerPosition = Vector3.zero;
        lastSeenTime = -1f;
        previousDetectionLevel = PlayerDetectionLevel.None;
        InvalidateCache();
        Debug.Log("Detection state reset!");
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// MEJORA: Get current detection result for AI decision making
    /// </summary>
    public DetectionResult GetCurrentDetectionResult()
    {
        return lastDetectionResult;
    }
    
    /// <summary>
    /// MEJORA: Get detection level for quick checks
    /// </summary>
    public PlayerDetectionLevel GetDetectionLevel()
    {
        return lastDetectionResult.level;
    }
    
    /// <summary>
    /// MEJORA: Check if detection is significant enough for action
    /// </summary>
    public bool HasSignificantDetection()
    {
        return lastDetectionResult.IsSignificant;
    }
    
    /// <summary>
    /// MEJORA: Runtime configuration of personality
    /// </summary>
    public void SetPersonalityType(AIPersonalityType newPersonality)
    {
        personalityType = newPersonality;
        if (autoConfigureFromPersonality)
        {
            ApplyPersonalityConfig();
        }
    }
    
    /// <summary>
    /// MEJORA: Get current configuration for external use
    /// </summary>
    public DetectionConfig GetDetectionConfig()
    {
        return config;
    }
    
    #endregion
}