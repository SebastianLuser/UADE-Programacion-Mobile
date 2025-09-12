# 🎯 PLAN DE IMPLEMENTACIÓN - HEIST GAME AI SYSTEM

## 📋 OBJETIVO FINAL
Implementar un sistema de AI completo que cumpla con todos los requerimientos del TP:
- **1 Guard** con personalidades agresiva/conservadora
- **1 Civilian** que huye al detectar al player o alerta con probabilidad
- **Estructuras requeridas**: FSM, Steering Behaviours, Line of Sight, Decision Trees, Roulette Wheel

---

## 🧠 ¿QUÉ ES UN BLACKBOARD Y POR QUÉ LO NECESITAMOS?

### **Concepto de Blackboard**
Un **Blackboard** es un patrón de diseño en AI que actúa como una "pizarra compartida" donde múltiples agentes (NPCs) pueden:
- **Leer información** común del entorno
- **Escribir datos** que otros agentes necesitan
- **Comunicarse indirectamente** sin conocerse entre sí

### **¿Por qué necesitamos un Blackboard?**

#### **PROBLEMA SIN BLACKBOARD:**
```csharp
// Cada Guard tiene su propia referencia al player
public class Guard : MonoBehaviour
{
    private Transform player; // ❌ Duplicado en cada guard
    private bool playerDetected; // ❌ Solo este guard lo sabe
    private Vector3 lastKnownPosition; // ❌ No se comparte
}

// Resultado: Guards actúan independientemente, sin coordinación
```

#### **SOLUCIÓN CON BLACKBOARD:**
```csharp
// Información centralizada y compartida
public class Blackboard : MonoBehaviour
{
    // Todos los guards acceden a la misma información
    private Dictionary<string, object> sharedData = new();
    
    // Player position: todos los guards saben dónde está
    // Alert level: si un guard detecta al player, todos se alertan
    // Pursuit coordination: evitar que todos persigan al mismo tiempo
}
```

### **Beneficios específicos para tu TP:**

1. **Coordinación de Guards**: Si un guard ve al player, otros guards cercanos se alertan automáticamente
2. **Sistema de Alertas**: Escalación de tensión (Normal → Sospechoso → Alerta máxima)
3. **Información compartida**: Posición del player, última posición conocida, áreas de pánico
4. **Civiles reactivos**: Los civiles pueden reaccionar a alertas de guards sin estar directamente conectados

### **Ejemplo práctico:**
```csharp
// Guard A detecta al player
blackboard.SetValue("player_detected", true);
blackboard.SetValue("alert_level", 2);
blackboard.SetValue("last_known_position", playerPosition);

// Guard B automáticamente accede a esta información
int alertLevel = blackboard.GetValue<int>("alert_level");
if (alertLevel > 1) 
{
    // Cambiar comportamiento sin haber visto al player directamente
    ChangeToInvestigateState();
}
```

---

## 🏗️ FASE 1: FUNDACIONES (Día 1–2)

### **PASO 1: CREAR INTERFACES BASE**

#### **¿Qué buscamos con las interfaces?**

Las interfaces son **contratos** que definen qué puede hacer cada componente, sin especificar cómo lo hace. Esto nos permite:

1. **Desacoplamiento**: Los sistemas no dependen de implementaciones específicas
2. **Flexibilidad**: Podemos cambiar implementaciones sin romper otros sistemas
3. **Testing**: Podemos crear mocks para testing
4. **Escalabilidad**: Agregar nuevos tipos de AI fácilmente

#### **Interfaces a implementar:**

##### **1. IBlackboard - Comunicación Global**
```csharp
public interface IBlackboard
{
    T GetValue<T>(string key);
    void SetValue<T>(string key, T value);
    bool HasKey(string key);
    void Subscribe(string key, System.Action<object> callback);
}
```
**Propósito**: Centralizar toda la información compartida entre AIs.

##### **2. IDecisionNode - Árboles de Decisión**
```csharp
public interface IDecisionNode
{
    IDecisionNode Evaluate(IAIContext context);
    string GetNodeName();
}
```
**Propósito**: Crear árboles de decisión para civiles (¿huir o alertar?).

##### **3. IAIContext - Contexto de AI**
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
**Propósito**: Dar a cada AI acceso a toda la información que necesita para tomar decisiones.

##### **4. IPlayerDetector - Detección de Jugador**
```csharp
public interface IPlayerDetector
{
    bool CanSeePlayer(Transform player);
    float GetDistanceToPlayer(Transform player);
    bool IsPlayerInRange(Transform player, float range);
    Vector3 GetLastKnownPlayerPosition();
}
```
**Propósito**: Implementar Line of Sight de manera consistente en todos los NPCs.

##### **5. IAIMovementController - Control de Movimiento**
```csharp
public interface IAIMovementController
{
    void MoveTo(Vector3 target, float speed);
    void Flee(Vector3 fromPosition, float speed);
    void Patrol(Transform[] waypoints, float speed);
    void Stop();
    bool HasReachedDestination();
}
```
**Propósito**: Abstraer todos los Steering Behaviours bajo una interfaz común.

#### **¿Qué logramos con esto?**

1. **Guard.cs** puede usar `IAIMovementController` sin saber si usa Steering Behaviours o NavMesh
2. **Decision Trees** pueden usar `IAIContext` sin conocer si es Guard o Civilian
3. **Blackboard** puede ser implementado como MonoBehaviour o ScriptableObject según necesidades
4. **Testing** es más fácil porque podemos mockear cada interfaz

---

## 🔄 FASES DETALLADAS

### **FASE 2: DETECCIÓN Y LINE OF SIGHT** (Día 3)

#### **Objetivos:**
- Implementar detección visual realista
- Integrar con blackboard para compartir información
- Cumplir requerimiento de Line of Sight del TP

#### **Implementación:**
```csharp
public class PlayerDetector : MonoBehaviour, IPlayerDetector
{
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float fieldOfView = 90f;
    [SerializeField] private LayerMask obstacleLayer;
    
    public bool CanSeePlayer(Transform player)
    {
        // 1. Check distance
        // 2. Check field of view
        // 3. Raycast for obstacles
        // 4. Update blackboard with detection
    }
}
```

### **FASE 3: STEERING BEHAVIORS** (Día 4–5)

#### **Objetivos:**
- Crear steering behaviours complejos (requisito del TP)
- Implementar obstacle avoidance (obligatorio)
- Sistema modular y reutilizable

#### **Implementación prioritaria:**
1. **SeekBehavior** (ir hacia objetivo)
2. **FleeBehavior** (huir del objetivo)  
3. **ObstacleAvoidanceBehavior** (evitar obstáculos - OBLIGATORIO)
4. **PursuitBehavior** (perseguir con predicción - COMPLEJO)
5. **EvadeBehavior** (evadir con predicción - COMPLEJO)

### **FASE 4: DECISION TREES** (Día 6)

#### **Objetivos:**
- Implementar árboles de decisión para civiles
- Crear comportamiento reactivo inteligente
- Cumplir requerimiento de Decision Trees

#### **Árbol básico para Civilian:**
```
¿Veo al player? 
├─ SÍ → ¿Estoy cerca?
│         ├─ SÍ → HUIR
│         └─ NO → ¿Probabilidad de alertar?
│                   ├─ SÍ → ALERTAR
│                   └─ NO → IGNORAR
└─ NO → IDLE
```

### **FASE 5: INTEGRACIÓN CON GUARD EXISTENTE** (Día 7)

#### **Objetivos:**
- Adaptar Guard.cs existente sin reescribir desde cero
- Mantener FSM funcionando
- Agregar nuevas capacidades

#### **Modificaciones clave:**
```csharp
public class Guard : BaseCharacter, IUpdatable, IUseFsm, IAIContext
{
    // Mantener configuración existente
    [SerializeField] private float detectionRange = 8f;
    
    // Agregar personalidad
    [SerializeField] private GuardPersonalitySO personality;
    
    // Integrar nuevo sistema
    private IBlackboard blackboard;
    private IPlayerDetector playerDetector;
    
    // Modificar player reference
    public Transform Player => blackboard?.GetValue<Transform>("player_transform");
}
```

### **FASE 6: CIVILIAN AI** (Día 8)

#### **Objetivos:**
- Crear segundo grupo de enemigos (requisito)
- Implementar Roulette Wheel Selection
- Behavior reactivo con Decision Trees

### **FASE 7: GUARD PERSONALITIES** (Día 9)

#### **Objetivos:**
- Crear comportamientos diferentes (requisito)
- Implementar tipos Agresivo/Conservador
- Usar ScriptableObjects para configuración

```csharp
[CreateAssetMenu(fileName = "GuardPersonality", menuName = "AI/Guard Personality")]
public class GuardPersonalitySO : ScriptableObject
{
    public AIPersonalityType personalityType;
    public float aggressivenessMultiplier = 1f;
    public float detectionRangeMultiplier = 1f;
    public float chaseSpeedMultiplier = 1f;
}
```

### **FASE 8-9: TESTING Y POLISH** (Día 10-12)

#### **Objetivos:**
- Implementar herramientas de debug
- Player controller con estados Idle/Walk
- Optimización y bug fixing

---

## ✅ CHECKLIST DE REQUERIMIENTOS DEL TP

- [ ] **Line of Sight** ✅ PlayerDetector con raycast
- [ ] **Finite State Machine** ✅ Mantener FSM existente de Guard
- [ ] **Steering Behaviours** ✅ Seek, Flee, Pursuit, Evade, ObstacleAvoidance
- [ ] **Decision Trees** ✅ Para comportamiento de Civilians
- [ ] **Roulette Wheel Selection** ✅ Para selección de acciones
- [ ] **Patrol State** ✅ Waypoints con recorrido inverso
- [ ] **Idle State** ✅ Pausa después de X iteraciones
- [ ] **RunAway State** ✅ Flee/Evade complejo
- [ ] **Attack State** ✅ Seek/Pursuit + attack
- [ ] **Player States** ✅ Idle/Walk con flechas direccionales
- [ ] **2 Grupos enemigos** ✅ Guards + Civilians
- [ ] **5 unidades mínimo** ✅ 2-3 Guards + 2-3 Civilians
- [ ] **Comportamientos diferentes** ✅ Agresivo/Conservador

---

## 📊 TIMELINE DETALLADO

| Día | Fase | Entregables Específicos |
|-----|------|------------------------|
| 1-2 | Fundaciones | Interfaces + Blackboard + BlackboardKeys |
| 3 | Line of Sight | PlayerDetector completo con raycast |
| 4-5 | Steering | 5 behaviors + SteeringController |
| 6 | Decision Trees | Civilian decision tree completo |
| 7 | Guard Integration | Guard.cs adaptado + IAIContext |
| 8 | Civilian AI | CivilianAI + RouletteWheel integration |
| 9 | Personalities | GuardPersonalitySO + tipos |
| 10-11 | Testing | Debug tools + Player controller |
| 12 | Polish | Bug fixes + optimization |

---

## 🔧 CONSIDERACIONES TÉCNICAS

### **Integración con sistemas existentes:**
- ✅ Mantener `ServiceLocator` pattern
- ✅ Usar `UpdateManager` existente
- ✅ Integrar con `ObjectPoolService`
- ✅ Mantener estructura de carpetas actual

### **Performance:**
- Update intervals para móviles (0.1f-0.2f)
- Usar ServiceLocator para referencias
- Evitar FindObjectOfType en runtime

### **Debug:**
- Gizmos para visualizar ranges
- Inspector personalizado para Blackboard
- Console logs con categorías

Esta planificación te permitirá cumplir todos los requerimientos del TP de manera estructurada y escalable.