# Civilian Decision Tree Enhancements - Implementation Summary

## ✅ Implemented Features

### 1. **Nodo Attack Dedicado**
- **Ubicación**: `CivilianDecisionTreeRunner.BuildDecisionTree()`
- **Función**: Nuevo `attackNode` que se ejecuta cuando:
  - Player está en melee range (`IsPlayerInMeleeRange()`)
  - Civilian puede atacar (`civilian.CanAttack`)
  - Stance es ATTACK (`ShouldChooseAttackOverFlee()`)
- **Resultado**: El árbol ahora sugiere "Attack" en lugar de solo "Pursue" cuando está en melee

### 2. **Ventana No Interrumpible de Ataque**
- **Ubicación**: `CivilianDecisionTreeRunner.IsInNonInterruptibleAttackCycle()`
- **Función**: Durante `AttackWindup + AttackHitWin + AttackRecover` el civilian ignora otras sugerencias
- **Implementación**: 
  - `ProcessSuggestion()` rechaza cambios durante el ciclo
  - `StartAttackCycle()` marca el inicio del ciclo
  - `EndAttackCycle()` termina el ciclo y opcionalmente inicia post-hit flee

### 3. **Timers Separados para LoS**
- **Ubicación**: Variables de clase en `CivilianDecisionTreeRunner`
- **Timers**:
  - `pursuitLoseSightTimer_visible`: Para romper stance lock cuando se pierde LoS en rama visible
  - `pursuitLoseSightTimer_invisible`: Para commitment cuando se pierde LoS en rama invisible
- **Beneficio**: Evita acumulación incorrecta de tiempo perdido en diferentes contextos

### 4. **Visibilidad Forzada en Melee**
- **Ubicación**: `Civilian.HasLoS()`
- **Función**: Si `distance <= meleeRange` → retorna `true` automáticamente
- **Beneficio**: Evita parpadeo de LoS durante ataques por colisiones/ángulos

### 5. **Post-Hit Flee (Hit-and-Run)**
- **Ubicación**: `CivilianDecisionTreeRunner.EndAttackCycle()`
- **Función**: Al completar ataque:
  - Rompe stance lock actual
  - Fuerza stance ESCAPE por `postHitFleeTime` segundos (1.2s)
  - Previene re-roll inmediato después del hit
- **Comportamiento**: Attack → PostHitFlee → Puede volver a re-rollear

### 6. **Re-roll con Disparadores Específicos**
- **Ubicación**: `CivilianDecisionTreeRunner.CheckStanceLockBreakers()`
- **Disparadores**:
  - Distancia ≥ SafeDistance
  - LoS perdido > grace period extendido
  - Ciclo de ataque completado
  - Player reference perdida
- **Beneficio**: Re-roll basado en eventos, no solo tiempo

### 7. **Configuración Optimizada**
- **evaluationInterval**: Reducido a 0.15s (desde 0.1s) para menos ruido de logs
- **postHitFleeTime**: 1.2s de flee obligatorio después de hit
- **Stance lock duration**: Mantiene 2.5s como estaba funcionando bien

## 🔄 Flujo de Decisión Mejorado

### Con LoS (Rama Visible):
```
HasLoS() → IsInNonInterruptibleAttackCycle() → 
  SI: Continue Attack
  NO: IsPlayerInMeleeRange() && CanAttack && ShouldChooseAttackOverFlee() →
    SI: Attack
    NO: ShouldChooseAttackOverFlee() →
      SI: Pursue 
      NO: Flee
```

### Sin LoS (Rama Invisible):
```
!HasLoS() → IsInNonInterruptibleAttackCycle() →
  SI: Continue Attack (breve ventana)
  NO: IsCurrentlyPursuing() && IsWithinPursuitCommitment() →
    SI: Continue Pursue
    NO: IsCurrentlyFleeing() && !ShouldReturnToIdle() →
      SI: Continue Flee
      NO: ShouldTriggerAlert() →
        SI: Alert
        NO: Idle
```

## 🎯 Comportamiento Esperado

### Secuencia de Ataque Típica:
1. **LoS + Melee + ATTACK stance** → Sugiere "Attack"
2. **Cycle starts** → Ignora otras sugerencias por ~0.8s
3. **DealMeleeAttack()** → Notifica damage dealt
4. **OnAttackCycleComplete()** → Inicia post-hit flee
5. **Post-hit flee** → Fuerza ESCAPE stance por 1.2s
6. **Post-hit ends** → Puede re-rollear normalmente

### Stance Lock Lifecycle:
1. **Visible + No lock** → Re-roll roulette → Lock stance por 2.5s
2. **Lock active** → Use cached stance, ignore re-rolls
3. **Lock broken by**:
   - Distance ≥ SafeDistance
   - LoS lost > extended grace
   - Attack cycle completed
   - Time expiration (2.5s)

## 🧪 Testing

### Context Menu Commands:
- `[Civilian]` → "Debug Civilian Status": Estado general
- `[DecisionTreeRunner]` → "Debug Decision Tree Status": Estado detallado con timers
- `[CivilianDecisionTreeTest]` → Multiple test functions

### Key Debug Info:
- Stance lock status y tiempo restante
- Attack cycle status y progreso
- Timers separados (visible/invisible)
- Post-hit flee status
- FSM vs DT state matching

## 🚀 Next Steps (Opcional)

Si quieres seguir optimizando:

1. **Alert con cooldown por tipo**: Diferentes cooldowns para diferentes alertas
2. **Stance inheritance**: Que los civiles cercanos "hereden" stance del que alertó
3. **Dynamic weights**: Modificar attack/escape weights basado en salud, contexto, etc.
4. **Group coordination**: Coordinar ataques entre múltiples civiles

## 📋 Checklist de Funcionalidad

- ✅ Stance lock (2.5s) sin ping-pong
- ✅ Attack node dedicado en melee
- ✅ Ventana no interrumpible de ataque
- ✅ Timers LoS separados
- ✅ Visibilidad forzada en melee
- ✅ Post-hit flee (hit-and-run)
- ✅ Re-roll con disparadores específicos
- ✅ Configuración optimizada
- ✅ Debug mejorado
- ✅ Testing utilities

¡El sistema está listo para probar! El civilian ahora debería hacer ataques completos sin interrupciones, mantener stance coherente, y ejecutar hit-and-run efectivo.