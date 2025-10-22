# 🚗 FASE 3: STEERING BEHAVIORS (Día 4–5)

## 🎯 **OBJETIVO DE LA FASE**
Implementar **steering behaviors complejos** que reemplacen el movimiento lineal básico por comportamientos naturales y realistas, cumpliendo con los requisitos obligatorios del TP: "steering behaviours complejos" y "obstacle avoidance en todo movimiento".

---

## 📋 **PASO 4: STEERING BASE SYSTEM**

### **¿QUÉ BUSCAMOS?**
Crear un sistema modular de steering behaviors que permita:
1. **Movimientos complejos** (no directos al objetivo)
2. **Combinación de behaviors** con prioridades
3. **Obstacle avoidance obligatorio** en todo movimiento
4. **Behaviors requeridos**: Seek, Flee, Pursuit, Evade

### **Problema Actual en Guard.cs:**
```csharp
// Método Move actual - PROHIBIDO por el TP:
public override void Move(Vector3 direction)
{
    Vector3 movement = direction * (characterData.moveSpeed * Time.deltaTime); // ❌ Movimiento lineal
    transform.position += movement; // ❌ Directo, sin steering
    
    // No hay obstacle avoidance
    // No hay steering complejo
    // Movimiento robótico
}
```

---

## 🏗️ **IMPLEMENTACIÓN DEL SISTEMA BASE**

### **SteeringBehavior.cs - Clase Base Abstracta**
```csharp
public abstract class SteeringBehavior : MonoBehaviour
{
    [Header("Behavior Settings")]
    [SerializeField] protected float maxForce = 5f;
    [SerializeField] protected float maxSpeed = 3f;
    [SerializeField] protected float weight = 1f;
    [SerializeField] protected int priority = 1; // 0 = máxima prioridad
    [SerializeField] protected bool isActive = true;
    
    [Header("Debug")]
    [SerializeField] protected bool showDebugRays = false;
    [SerializeField] protected Color debugColor = Color.white;
    
    // Método principal que cada behavior debe implementar
    public abstract Vector3 Calculate(Transform agent, Transform target = null);
    
    // Propiedades para el SteeringController
    public float Weight => weight;
    public int Priority => priority;
    public virtual bool IsActive => isActive && enabled && gameObject.activeInHierarchy;
    
    // Utilidades compartidas
    protected Vector3 ClampForce(Vector3 force)
    {
        return Vector3.ClampMagnitude(force, maxForce);
    }
    
    protected float GetMaxSpeed() => maxSpeed;
    
    protected Vector3 GetDesiredVelocity(Vector3 targetPosition, Vector3 currentPosition)
    {
        Vector3 desired = (targetPosition - currentPosition).normalized * maxSpeed;
        return desired;
    }
    
    // Para debugging
    protected void DrawDebugRay(Vector3 origin, Vector3 direction, Color color, float duration = 0.1f)
    {
        if (showDebugRays)
        {
            Debug.DrawRay(origin, direction, color, duration);
        }
    }
    
    protected virtual void OnDrawGizmos()
    {
        if (!showDebugRays || !Application.isPlaying) return;
        DrawBehaviorGizmos();
    }
    
    protected virtual void DrawBehaviorGizmos()
    {
        // Override en behaviors específicos
    }
}
```

### **SteeringController.cs - Controlador Principal**
```csharp
public class SteeringController : MonoBehaviour
{
    [Header("Steering Settings")]
    [SerializeField] private float maxForce = 10f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private bool autoFindBehaviors = true;
    
    [Header("Behavior Configuration")]
    [SerializeField] private SteeringBehaviorData[] behaviorDatas;
    
    [Header("Debug")]
    [SerializeField] private bool showCombinedForce = true;
    [SerializeField] private bool enableSteeringLogs = false;
    
    // Runtime variables
    private List<SteeringBehavior> activeBehaviors = new List<SteeringBehavior>();
    private Vector3 lastCalculatedForce;
    private Transform currentTarget;
    
    [System.Serializable]
    public class SteeringBehaviorData
    {
        public SteeringBehavior behavior;
        public float weightMultiplier = 1f;
        public bool overrideWeight = false;
        public bool isEnabled = true;
    }
    
    void Awake()
    {
        if (autoFindBehaviors)
        {
            FindAllBehaviors();
        }
        
        InitializeBehaviors();
    }
    
    private void FindAllBehaviors()
    {
        var behaviors = GetComponents<SteeringBehavior>();
        behaviorDatas = new SteeringBehaviorData[behaviors.Length];
        
        for (int i = 0; i < behaviors.Length; i++)
        {
            behaviorDatas[i] = new SteeringBehaviorData
            {
                behavior = behaviors[i],
                weightMultiplier = 1f,
                isEnabled = true
            };
        }
    }
    
    private void InitializeBehaviors()
    {
        activeBehaviors.Clear();
        
        foreach (var data in behaviorDatas)
        {
            if (data.behavior != null && data.isEnabled)
            {
                activeBehaviors.Add(data.behavior);
            }
        }
        
        // Ordenar por prioridad (0 = máxima prioridad)
        activeBehaviors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        
        if (enableSteeringLogs)
            Logger.LogDebug($"SteeringController on {gameObject.name}: Initialized with {activeBehaviors.Count} behaviors");
    }
    
    public Vector3 CalculateSteering(Transform target = null)
    {
        currentTarget = target;
        Vector3 totalForce = Vector3.zero;
        bool obstacleAvoidanceActive = false;
        
        foreach (var behavior in activeBehaviors)
        {
            if (!behavior.IsActive) continue;
            
            Vector3 force = behavior.Calculate(transform, target);
            
            // Obstacle avoidance tiene prioridad absoluta
            if (behavior is ObstacleAvoidanceBehavior && force.magnitude > 0.1f)
            {
                obstacleAvoidanceActive = true;
                
                if (enableSteeringLogs)
                    Logger.LogDebug($"{gameObject.name}: Obstacle avoidance activated! Force: {force.magnitude:F2}");
                
                // Solo obstacle avoidance cuando hay obstáculos
                lastCalculatedForce = force;
                return Vector3.ClampMagnitude(force, maxForce);
            }
        }
        
        // Si no hay obstacle avoidance activo, combinar todos los behaviors
        if (!obstacleAvoidanceActive)
        {
            foreach (var behavior in activeBehaviors)
            {
                if (!behavior.IsActive) continue;
                
                Vector3 force = behavior.Calculate(transform, target);
                float effectiveWeight = GetEffectiveWeight(behavior);
                
                totalForce += force * effectiveWeight;
                
                if (enableSteeringLogs && force.magnitude > 0.1f)
                    Logger.LogDebug($"{gameObject.name}: {behavior.GetType().Name} force: {force.magnitude:F2} (weight: {effectiveWeight:F2})");
            }
        }
        
        lastCalculatedForce = Vector3.ClampMagnitude(totalForce, maxForce);
        return lastCalculatedForce;
    }
    
    private float GetEffectiveWeight(SteeringBehavior behavior)
    {
        var data = System.Array.Find(behaviorDatas, d => d.behavior == behavior);
        if (data != null)
        {
            float weight = data.overrideWeight ? data.weightMultiplier : behavior.Weight;
            return weight * data.weightMultiplier;
        }
        
        return behavior.Weight;
    }
    
    public void SetTarget(Transform newTarget)
    {
        currentTarget = newTarget;
    }
    
    public void EnableBehavior<T>(bool enable) where T : SteeringBehavior
    {
        var behavior = GetComponent<T>();
        if (behavior != null)
        {
            var data = System.Array.Find(behaviorDatas, d => d.behavior == behavior);
            if (data != null)
            {
                data.isEnabled = enable;
                InitializeBehaviors(); // Rebuild active behaviors list
            }
        }
    }
    
    public void SetBehaviorWeight<T>(float weight) where T : SteeringBehavior
    {
        var behavior = GetComponent<T>();
        if (behavior != null)
        {
            var data = System.Array.Find(behaviorDatas, d => d.behavior == behavior);
            if (data != null)
            {
                data.weightMultiplier = weight;
                data.overrideWeight = true;
            }
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showCombinedForce || !Application.isPlaying) return;
        
        // Mostrar force resultante
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position, lastCalculatedForce);
        
        // Mostrar target actual
        if (currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}
```

---

## 📋 **PASO 5: BEHAVIORS ESPECÍFICOS**

### **1. SeekBehavior.cs - Ir Hacia Objetivo (CRÍTICO)**
```csharp
public class SeekBehavior : SteeringBehavior
{
    [Header("Seek Settings")]
    [SerializeField] private float arrivalRadius = 1f;
    [SerializeField] private float slowingRadius = 3f;
    [SerializeField] private bool useArrival = true;
    [SerializeField] private AnimationCurve arrivalCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    public override Vector3 Calculate(Transform agent, Transform target)
    {
        if (target == null) return Vector3.zero;
        
        Vector3 desired = GetDesiredVelocity(target.position, agent.position);
        
        // Arrival behavior - desacelerar cerca del objetivo
        if (useArrival)
        {
            float distance = Vector3.Distance(agent.position, target.position);
            
            if (distance < slowingRadius)
            {
                float slowingFactor = distance / slowingRadius;
                
                // Usar curva para suavizar la desaceleración
                slowingFactor = arrivalCurve.Evaluate(slowingFactor);
                desired *= slowingFactor;
                
                // Detenerse completamente si está muy cerca
                if (distance < arrivalRadius)
                {
                    return Vector3.zero;
                }
            }
        }
        
        DrawDebugRay(agent.position, desired, Color.green);
        
        return ClampForce(desired);
    }
    
    protected override void DrawBehaviorGizmos()
    {
        if (useArrival)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, arrivalRadius);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, slowingRadius);
        }
    }
}
```

### **2. FleeBehavior.cs - Huir del Objetivo (CRÍTICO)**
```csharp
public class FleeBehavior : SteeringBehavior
{
    [Header("Flee Settings")]
    [SerializeField] private float fleeRadius = 5f;
    [SerializeField] private float panicDistance = 2f;
    [SerializeField] private float panicMultiplier = 2f;
    [SerializeField] private bool useDistanceAttenuation = true;
    
    public override Vector3 Calculate(Transform agent, Transform target)
    {
        if (target == null) return Vector3.zero;
        
        Vector3 difference = agent.position - target.position;
        float distance = difference.magnitude;
        
        // Solo huir si está dentro del radio de huida
        if (distance > fleeRadius) return Vector3.zero;
        
        Vector3 desired = difference.normalized * maxSpeed;
        
        // Intensificar si está muy cerca (pánico)
        if (distance < panicDistance)
        {
            desired *= panicMultiplier;
        }
        
        // Atenuar por distancia si está habilitado
        if (useDistanceAttenuation && distance > 0.1f)
        {
            float attenuationFactor = 1f - (distance / fleeRadius);
            desired *= attenuationFactor;
        }
        
        DrawDebugRay(agent.position, desired, Color.red);
        
        return ClampForce(desired);
    }
    
    protected override void DrawBehaviorGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, fleeRadius);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, panicDistance);
    }
}
```

### **3. ObstacleAvoidanceBehavior.cs - Evitar Obstáculos (OBLIGATORIO)**
```csharp
public class ObstacleAvoidanceBehavior : SteeringBehavior
{
    [Header("Obstacle Avoidance Settings")]
    [SerializeField] private float lookAheadDistance = 3f;
    [SerializeField] private float avoidanceForce = 15f;
    [SerializeField] private LayerMask obstacleLayer = 1;
    [SerializeField] private float sideRayOffset = 0.5f;
    [SerializeField] private float rayHeight = 0.5f;
    
    [Header("Advanced Settings")]
    [SerializeField] private int numberOfRays = 3;
    [SerializeField] private float raySpread = 30f;
    [SerializeField] private bool useSphereCast = true;
    [SerializeField] private float sphereRadius = 0.3f;
    
    // PRIORIDAD MÁXIMA - siempre debe ejecutarse primero
    public override int Priority => 0;
    
    private Vector3 lastAvoidanceDirection;
    
    public override Vector3 Calculate(Transform agent, Transform target = null)
    {
        Vector3 avoidanceDirection = Vector3.zero;
        Vector3 origin = agent.position + Vector3.up * rayHeight;
        
        // Verificar obstáculos con múltiples raycast
        if (HasObstacleAhead(agent, out RaycastHit nearestHit))
        {
            avoidanceDirection = CalculateAvoidanceDirection(agent, nearestHit);
            lastAvoidanceDirection = avoidanceDirection;
            
            DrawDebugRay(origin, avoidanceDirection * avoidanceForce, Color.red, 0.1f);
            
            return avoidanceDirection * avoidanceForce;
        }
        
        // Pequeña inercia para evitar oscilaciones
        if (lastAvoidanceDirection.magnitude > 0.1f)
        {
            lastAvoidanceDirection *= 0.9f; // Decay
            if (lastAvoidanceDirection.magnitude < 0.1f)
                lastAvoidanceDirection = Vector3.zero;
        }
        
        return Vector3.zero;
    }
    
    private bool HasObstacleAhead(Transform agent, out RaycastHit nearestHit)
    {
        nearestHit = new RaycastHit();
        float nearestDistance = float.MaxValue;
        bool hitDetected = false;
        
        Vector3 origin = agent.position + Vector3.up * rayHeight;
        Vector3 forward = agent.forward;
        
        // Raycast central
        if (CastRay(origin, forward, lookAheadDistance, out RaycastHit hit))
        {
            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                hitDetected = true;
            }
        }
        
        // Raycasts laterales
        for (int i = 1; i <= numberOfRays / 2; i++)
        {
            float angle = (raySpread / (numberOfRays / 2)) * i;
            
            // Lado izquierdo
            Vector3 leftDirection = Quaternion.AngleAxis(-angle, Vector3.up) * forward;
            if (CastRay(origin, leftDirection, lookAheadDistance, out hit))
            {
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                    hitDetected = true;
                }
            }
            
            // Lado derecho
            Vector3 rightDirection = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            if (CastRay(origin, rightDirection, lookAheadDistance, out hit))
            {
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                    hitDetected = true;
                }
            }
        }
        
        return hitDetected;
    }
    
    private bool CastRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        if (useSphereCast)
        {
            return Physics.SphereCast(origin, sphereRadius, direction, out hit, distance, obstacleLayer);
        }
        else
        {
            return Physics.Raycast(origin, direction, out hit, distance, obstacleLayer);
        }
    }
    
    private Vector3 CalculateAvoidanceDirection(Transform agent, RaycastHit hitInfo)
    {
        // Método 1: Usar normal de la superficie
        Vector3 avoidDirection = Vector3.Reflect(agent.forward, hitInfo.normal);
        
        // Método 2: Dirección perpendicular (backup)
        if (avoidDirection.magnitude < 0.1f)
        {
            avoidDirection = Vector3.Cross(hitInfo.normal, Vector3.up).normalized;
            
            // Elegir la dirección que más se aleje del obstáculo
            Vector3 toObstacle = (hitInfo.point - agent.position).normalized;
            if (Vector3.Dot(avoidDirection, toObstacle) > 0)
            {
                avoidDirection = -avoidDirection;
            }
        }
        
        // Proyectar al plano horizontal
        avoidDirection.y = 0;
        avoidDirection = avoidDirection.normalized;
        
        // Intensificar basado en proximidad
        float proximityFactor = 1f - (hitInfo.distance / lookAheadDistance);
        proximityFactor = Mathf.Clamp01(proximityFactor);
        
        return avoidDirection * (1f + proximityFactor);
    }
    
    protected override void DrawBehaviorGizmos()
    {
        Vector3 origin = transform.position + Vector3.up * rayHeight;
        Vector3 forward = transform.forward;
        
        // Raycast central
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, forward * lookAheadDistance);
        
        // Raycasts laterales
        Gizmos.color = Color.yellow;
        for (int i = 1; i <= numberOfRays / 2; i++)
        {
            float angle = (raySpread / (numberOfRays / 2)) * i;
            
            Vector3 leftDirection = Quaternion.AngleAxis(-angle, Vector3.up) * forward;
            Vector3 rightDirection = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            
            Gizmos.DrawRay(origin, leftDirection * lookAheadDistance);
            Gizmos.DrawRay(origin, rightDirection * lookAheadDistance);
        }
        
        // SphereCast radius
        if (useSphereCast)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, sphereRadius);
            Gizmos.DrawWireSphere(origin + forward * lookAheadDistance, sphereRadius);
        }
        
        // Última dirección de evasión
        if (lastAvoidanceDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(origin, lastAvoidanceDirection * 2f);
        }
    }
}
```

### **4. PursuitBehavior.cs - Perseguir con Predicción (COMPLEJO)**
```csharp
public class PursuitBehavior : SteeringBehavior
{
    [Header("Pursuit Settings")]
    [SerializeField] private float maxPredictionTime = 2f;
    [SerializeField] private float predictionAccuracy = 0.8f;
    [SerializeField] private bool useVelocityPrediction = true;
    [SerializeField] private int velocityHistorySize = 5;
    
    [Header("Advanced Settings")]
    [SerializeField] private bool useInterception = true;
    [SerializeField] private float interceptTolerance = 0.5f;
    
    // Tracking variables
    private Vector3 lastTargetPosition;
    private Queue<Vector3> velocityHistory = new Queue<Vector3>();
    private Vector3 predictedPosition;
    
    public override Vector3 Calculate(Transform agent, Transform target)
    {
        if (target == null) return Vector3.zero;
        
        // Actualizar velocidad del target
        Vector3 targetVelocity = UpdateTargetVelocity(target);
        
        // Calcular predicción
        predictedPosition = CalculatePredictedPosition(agent, target, targetVelocity);
        
        // Usar pursuit hacia posición predicha
        Vector3 desired = GetDesiredVelocity(predictedPosition, agent.position);
        
        DrawDebugRay(agent.position, desired, Color.cyan);
        DrawDebugRay(target.position, predictedPosition - target.position, Color.blue);
        
        return ClampForce(desired);
    }
    
    private Vector3 UpdateTargetVelocity(Transform target)
    {
        Vector3 currentVelocity = Vector3.zero;
        
        if (lastTargetPosition != Vector3.zero)
        {
            currentVelocity = (target.position - lastTargetPosition) / Time.deltaTime;
            
            // Mantener historial de velocidades para suavizar
            velocityHistory.Enqueue(currentVelocity);
            if (velocityHistory.Count > velocityHistorySize)
            {
                velocityHistory.Dequeue();
            }
        }
        
        lastTargetPosition = target.position;
        
        // Promedio de velocidades para suavizar predicción
        if (velocityHistory.Count > 0)
        {
            Vector3 averageVelocity = Vector3.zero;
            foreach (var vel in velocityHistory)
            {
                averageVelocity += vel;
            }
            currentVelocity = averageVelocity / velocityHistory.Count;
        }
        
        return currentVelocity;
    }
    
    private Vector3 CalculatePredictedPosition(Transform agent, Transform target, Vector3 targetVelocity)
    {
        float distance = Vector3.Distance(agent.position, target.position);
        
        // Tiempo de predicción basado en distancia y velocidades
        float predictionTime = 0f;
        
        if (useInterception && targetVelocity.magnitude > 0.1f)
        {
            // Cálculo de intercepción más sofisticado
            predictionTime = CalculateInterceptionTime(agent, target, targetVelocity);
        }
        else
        {
            // Predicción simple basada en distancia
            predictionTime = Mathf.Min(distance / maxSpeed, maxPredictionTime);
        }
        
        // Aplicar precisión de predicción
        predictionTime *= predictionAccuracy;
        
        Vector3 predicted = target.position + targetVelocity * predictionTime;
        
        return predicted;
    }
    
    private float CalculateInterceptionTime(Transform agent, Transform target, Vector3 targetVelocity)
    {
        Vector3 toTarget = target.position - agent.position;
        float a = targetVelocity.sqrMagnitude - (maxSpeed * maxSpeed);
        float b = 2f * Vector3.Dot(targetVelocity, toTarget);
        float c = toTarget.sqrMagnitude;
        
        float discriminant = b * b - 4f * a * c;
        
        if (discriminant < 0f) // No hay intercepción posible
        {
            return Vector3.Distance(agent.position, target.position) / maxSpeed;
        }
        
        float t1 = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
        float t2 = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
        
        float interceptTime = Mathf.Min(t1, t2);
        if (interceptTime < 0f) interceptTime = Mathf.Max(t1, t2);
        
        return Mathf.Clamp(interceptTime, 0f, maxPredictionTime);
    }
    
    protected override void DrawBehaviorGizmos()
    {
        // Mostrar posición predicha
        if (predictedPosition != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(predictedPosition, 0.5f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, predictedPosition);
        }
        
        // Mostrar historial de velocidades
        if (velocityHistory.Count > 1)
        {
            Gizmos.color = Color.green;
            Vector3 avgVel = Vector3.zero;
            foreach (var vel in velocityHistory)
                avgVel += vel;
            avgVel /= velocityHistory.Count;
            
            Gizmos.DrawRay(transform.position, avgVel);
        }
    }
}
```

### **5. EvadeBehavior.cs - Evadir con Predicción (COMPLEJO)**
```csharp
public class EvadeBehavior : SteeringBehavior
{
    [Header("Evade Settings")]
    [SerializeField] private float maxEvadeTime = 1.5f;
    [SerializeField] private float threatRadius = 8f;
    [SerializeField] private float evasionMultiplier = 1.5f;
    [SerializeField] private bool usePanicMode = true;
    [SerializeField] private float panicThreshold = 3f;
    
    // Tracking variables
    private Vector3 lastPursuerPosition;
    private Vector3 pursuerVelocity;
    private Vector3 evasionDirection;
    private bool inPanicMode = false;
    
    public override Vector3 Calculate(Transform agent, Transform target)
    {
        if (target == null) return Vector3.zero;
        
        float distance = Vector3.Distance(agent.position, target.position);
        
        // Solo evadir si el perseguidor está dentro del radio de amenaza
        if (distance > threatRadius) 
        {
            inPanicMode = false;
            return Vector3.zero;
        }
        
        // Calcular velocidad del perseguidor
        Vector3 currentPursuerVelocity = UpdatePursuerVelocity(target);
        
        // Determinar si entrar en modo pánico
        if (usePanicMode && distance < panicThreshold)
        {
            inPanicMode = true;
        }
        else if (distance > panicThreshold * 1.5f)
        {
            inPanicMode = false;
        }
        
        // Calcular tiempo de amenaza y posición predicha
        float threatTime = CalculateThreatTime(agent, target, currentPursuerVelocity);
        Vector3 predictedThreat = target.position + currentPursuerVelocity * threatTime;
        
        // Calcular dirección de evasión
        Vector3 desired = CalculateEvasionDirection(agent, target, predictedThreat);
        
        // Aplicar multiplicadores
        float effectiveMultiplier = evasionMultiplier;
        if (inPanicMode)
        {
            effectiveMultiplier *= 2f;
        }
        
        desired *= effectiveMultiplier;
        
        evasionDirection = desired;
        
        DrawDebugRay(agent.position, desired, inPanicMode ? Color.red : Color.orange);
        DrawDebugRay(target.position, predictedThreat - target.position, Color.yellow);
        
        return ClampForce(desired);
    }
    
    private Vector3 UpdatePursuerVelocity(Transform pursuer)
    {
        if (lastPursuerPosition != Vector3.zero)
        {
            pursuerVelocity = (pursuer.position - lastPursuerPosition) / Time.deltaTime;
        }
        lastPursuerPosition = pursuer.position;
        
        return pursuerVelocity;
    }
    
    private float CalculateThreatTime(Transform agent, Transform pursuer, Vector3 pursuerVel)
    {
        float distance = Vector3.Distance(agent.position, pursuer.position);
        
        // Si el perseguidor se mueve hacia nosotros
        Vector3 toPursuer = (pursuer.position - agent.position).normalized;
        float approachSpeed = Vector3.Dot(pursuerVel, toPursuer);
        
        if (approachSpeed > 0.1f)
        {
            // Tiempo basado en velocidad de aproximación
            float threatTime = distance / (approachSpeed + maxSpeed);
            return Mathf.Min(threatTime, maxEvadeTime);
        }
        else
        {
            // Predicción simple si no se acerca directamente
            return distance / maxSpeed;
        }
    }
    
    private Vector3 CalculateEvasionDirection(Transform agent, Transform pursuer, Vector3 predictedThreat)
    {
        // Dirección base: alejarse de la amenaza predicha
        Vector3 baseDirection = (agent.position - predictedThreat).normalized;
        
        // Añadir componente lateral para evasión más inteligente
        Vector3 lateralDirection = Vector3.Cross(baseDirection, Vector3.up);
        
        // Determinar qué dirección lateral es mejor
        Vector3 pursuerToAgent = (agent.position - pursuer.position).normalized;
        Vector3 pursuerForward = pursuerVelocity.normalized;
        
        // Si el perseguidor se mueve hacia nosotros, evadir lateralmente
        if (Vector3.Dot(pursuerForward, pursuerToAgent) > 0.5f)
        {
            // Combinar evasión directa con movimiento lateral
            float lateralWeight = inPanicMode ? 0.3f : 0.6f;
            baseDirection = Vector3.Lerp(baseDirection, lateralDirection, lateralWeight);
        }
        
        // Evitar ir hacia obstáculos (integración básica con obstacle avoidance)
        var obstacleAvoidance = GetComponent<ObstacleAvoidanceBehavior>();
        if (obstacleAvoidance != null)
        {
            Vector3 obstacleForce = obstacleAvoidance.Calculate(agent);
            if (obstacleForce.magnitude > 0.1f)
            {
                // Ajustar dirección para evitar obstáculos
                baseDirection = Vector3.Lerp(baseDirection, obstacleForce.normalized, 0.4f);
            }
        }
        
        baseDirection.y = 0; // Mantener en plano horizontal
        return baseDirection.normalized * maxSpeed;
    }
    
    protected override void DrawBehaviorGizmos()
    {
        // Radio de amenaza
        Gizmos.color = inPanicMode ? Color.red : Color.orange;
        Gizmos.DrawWireSphere(transform.position, threatRadius);
        
        // Zona de pánico
        if (usePanicMode)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, panicThreshold);
        }
        
        // Dirección de evasión actual
        if (evasionDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, evasionDirection);
        }
        
        // Velocidad del perseguidor
        if (pursuerVelocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(lastPursuerPosition, pursuerVelocity);
        }
    }
}
```

---

## 🔧 **INTEGRACIÓN CON IAIMovementController**

### **AIMovementController.cs - Implementación Completa**
```csharp
public class AIMovementController : MonoBehaviour, IAIMovementController
{
    [Header("Movement Settings")]
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private bool smoothRotation = true;
    
    [Header("Physics")]
    [SerializeField] private bool useRigidbody = false;
    [SerializeField] private float mass = 1f;
    
    private SteeringController steeringController;
    private Transform currentTarget;
    private Vector3 currentVelocity;
    private Rigidbody rb;
    private bool isMoving = false;
    
    void Awake()
    {
        steeringController = GetComponent<SteeringController>();
        rb = GetComponent<Rigidbody>();
        
        if (steeringController == null)
        {
            Logger.LogError($"AIMovementController on {gameObject.name}: SteeringController not found!");
            enabled = false;
        }
    }
    
    void FixedUpdate()
    {
        if (isMoving && currentTarget != null)
        {
            UpdateMovement();
        }
    }
    
    private void UpdateMovement()
    {
        // Calcular steering force
        Vector3 steeringForce = steeringController.CalculateSteering(currentTarget);
        
        if (useRigidbody && rb != null)
        {
            // Aplicar force usando Rigidbody
            rb.AddForce(steeringForce / mass, ForceMode.Force);
            currentVelocity = rb.velocity;
            
            // Limitar velocidad máxima
            if (currentVelocity.magnitude > maxSpeed)
            {
                rb.velocity = currentVelocity.normalized * maxSpeed;
                currentVelocity = rb.velocity;
            }
        }
        else
        {
            // Aplicar force usando Transform
            currentVelocity += steeringForce * Time.fixedDeltaTime;
            currentVelocity = Vector3.ClampMagnitude(currentVelocity, maxSpeed);
            
            // Aplicar movimiento
            if (currentVelocity.magnitude > 0.1f)
            {
                transform.position += currentVelocity * Time.fixedDeltaTime;
            }
        }
        
        // Rotar hacia la dirección de movimiento
        if (currentVelocity.magnitude > 0.1f)
        {
            RotateTowards(currentVelocity.normalized);
        }
        
        // Verificar si llegó al destino
        if (HasReachedDestination())
        {
            Stop();
        }
    }
    
    private void RotateTowards(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) return;
        
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        
        if (smoothRotation)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.fixedDeltaTime
            );
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }
    
    public void MoveTo(Vector3 target, float speed)
    {
        // Crear target temporal
        GameObject tempTarget = new GameObject($"TempTarget_{gameObject.name}");
        tempTarget.transform.position = target;
        currentTarget = tempTarget.transform;
        
        maxSpeed = speed;
        isMoving = true;
        
        // Configurar behaviors para MoveTo
        steeringController.EnableBehavior<SeekBehavior>(true);
        steeringController.EnableBehavior<FleeBehavior>(false);
        steeringController.EnableBehavior<PursuitBehavior>(false);
        steeringController.EnableBehavior<EvadeBehavior>(false);
        // ObstacleAvoidance siempre activo
        
        Logger.LogDebug($"{gameObject.name}: MoveTo {target} at speed {speed}");
    }
    
    public void Flee(Vector3 fromPosition, float speed)
    {
        GameObject fleeTarget = new GameObject($"FleeTarget_{gameObject.name}");
        fleeTarget.transform.position = fromPosition;
        currentTarget = fleeTarget.transform;
        
        maxSpeed = speed;
        isMoving = true;
        
        // Configurar behaviors para Flee
        steeringController.EnableBehavior<FleeBehavior>(true);
        steeringController.EnableBehavior<SeekBehavior>(false);
        steeringController.EnableBehavior<PursuitBehavior>(false);
        steeringController.EnableBehavior<EvadeBehavior>(true); // Complementa flee
        
        Logger.LogDebug($"{gameObject.name}: Flee from {fromPosition} at speed {speed}");
    }
    
    public void Patrol(Transform[] waypoints, float speed)
    {
        if (waypoints == null || waypoints.Length == 0) return;
        
        // Encontrar waypoint más cercano
        Transform nearestWaypoint = waypoints[0];
        float nearestDistance = Vector3.Distance(transform.position, nearestWaypoint.position);
        
        foreach (var waypoint in waypoints)
        {
            float distance = Vector3.Distance(transform.position, waypoint.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestWaypoint = waypoint;
            }
        }
        
        MoveTo(nearestWaypoint.position, speed);
    }
    
    public void Stop()
    {
        isMoving = false;
        currentVelocity = Vector3.zero;
        
        if (useRigidbody && rb != null)
        {
            rb.velocity = Vector3.zero;
        }
        
        // Limpiar target temporal
        if (currentTarget != null && currentTarget.name.Contains("TempTarget"))
        {
            DestroyImmediate(currentTarget.gameObject);
        }
        currentTarget = null;
        
        Logger.LogDebug($"{gameObject.name}: Stopped movement");
    }
    
    public bool HasReachedDestination()
    {
        if (currentTarget == null) return true;
        
        float distance = Vector3.Distance(transform.position, currentTarget.position);
        return distance <= stoppingDistance;
    }
    
    public void SetSteeringTarget(Transform target)
    {
        currentTarget = target;
        steeringController.SetTarget(target);
    }
    
    public void EnableBehavior(System.Type behaviorType, bool enable)
    {
        if (behaviorType == typeof(SeekBehavior))
            steeringController.EnableBehavior<SeekBehavior>(enable);
        else if (behaviorType == typeof(FleeBehavior))
            steeringController.EnableBehavior<FleeBehavior>(enable);
        else if (behaviorType == typeof(PursuitBehavior))
            steeringController.EnableBehavior<PursuitBehavior>(enable);
        else if (behaviorType == typeof(EvadeBehavior))
            steeringController.EnableBehavior<EvadeBehavior>(enable);
    }
    
    public void SetBehaviorWeight(System.Type behaviorType, float weight)
    {
        if (behaviorType == typeof(SeekBehavior))
            steeringController.SetBehaviorWeight<SeekBehavior>(weight);
        else if (behaviorType == typeof(FleeBehavior))
            steeringController.SetBehaviorWeight<FleeBehavior>(weight);
        else if (behaviorType == typeof(PursuitBehavior))
            steeringController.SetBehaviorWeight<PursuitBehavior>(weight);
        else if (behaviorType == typeof(EvadeBehavior))
            steeringController.SetBehaviorWeight<EvadeBehavior>(weight);
    }
}
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Sistema de Steering modular** con SteeringController
2. **✅ ObstacleAvoidance obligatorio** con máxima prioridad
3. **✅ Steering behaviors complejos**: Pursuit y Evade con predicción
4. **✅ Behaviors básicos**: Seek y Flee funcionando
5. **✅ Integración con IAIMovementController**
6. **✅ Debug visualization completa**
7. **✅ Reemplazo del movimiento lineal** en Guard.cs

### **Testing:**
1. **Obstacle Avoidance**: NPCs deben evitar paredes automáticamente
2. **Pursuit**: Guards deben perseguir prediciendo movimiento del player
3. **Flee**: Civilians deben huir de manera natural, no lineal
4. **Combinación**: Múltiples behaviors deben funcionar juntos

Esta fase transforma NPCs robóticos en entidades con movimiento natural y realista, cumpliendo todos los requisitos de steering del TP.