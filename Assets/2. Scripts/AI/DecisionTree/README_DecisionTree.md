# Civilian Decision Tree System

## Overview

The Civilian Decision Tree system provides runtime AI decision-making for Civilian NPCs without directly controlling movement. The Decision Tree suggests behaviors to the Civilian's FSM, which handles the actual execution through the existing `ApplySteering` → `ObstacleAvoidance` system.

## Components

### CivilianDecisionTreeRunner
- **Purpose**: Runs the decision tree at configurable intervals and suggests actions
- **Auto-attachment**: Automatically added to Civilian GameObjects when `useDecisionTree` is enabled
- **Evaluation**: Independent coroutine-based evaluation loop

### Decision Tree Structure
```
Root: IsPlayerVisible?
├── YES → Check Attack vs Escape Roulette
│   ├── Attack → Suggest "Pursue" 
│   └── Escape → Suggest "Flee"
└── NO → Random Alert Check
    ├── Alert → Trigger GLOBAL_ALERT & Suggest "Idle"
    └── Resume → Suggest "Idle"
```

## Configuration

### Civilian Inspector Settings
- **Use Decision Tree**: Enable/disable the decision tree system
- **Evaluation Interval**: How often the tree evaluates (default: 0.35s)
- **Alert Chance When No LoS**: Probability of triggering alert when player not visible (default: 0.5)
- **Resume Suggestion**: State to suggest when resuming normal behavior (default: "Idle")

### Debug Settings
- **Debug DT**: Enable decision tree logging
- **Can Attack**: Allow civilians to choose attack over escape (uses roulette weights)

## Usage

### Setting Up a Civilian with Decision Tree

1. **Automatic Setup**: Set `useDecisionTree = true` in the Civilian inspector
   - The `CivilianDecisionTreeRunner` will be automatically added
   - Decision tree will be built and start evaluating on Start()

2. **Manual Setup**: Add `CivilianDecisionTreeRunner` component manually
   - Ensure the Civilian has `useDecisionTree = true`
   - Configure evaluation parameters in the runner inspector

### Monitoring and Testing

#### Debug Commands (Context Menu)
- **Civilian**: "Debug Civilian Status" - Shows complete civilian state including DT status
- **DT Runner**: "Debug Decision Tree Status" - Shows DT-specific information
- **DT Runner**: "Evaluate Decision Tree" - Force immediate evaluation

#### Test Component
Add `CivilianDecisionTreeTest` component for runtime testing:
- **Run Decision Tree Test**: Complete system status check
- **Toggle Global Alert**: Test blackboard integration
- **Force DT Evaluation**: Manual tree evaluation trigger

### Expected Behavior

#### With Debug Enabled
You should see logs like:
```
DT → Flee (Player visible, choosing escape)
DT → Alert (No LoS, random alert triggered)
DT → Resume (No LoS, continuing normal behavior)
```

#### State Transitions
- **Player Enters LoS**: DT suggests "Flee" → FSM executes S_CivFlee
- **Player Exits LoS (Safe Distance)**: DT suggests "Resume" (Idle) → FSM returns to S_CivIdle
- **Random Alert**: DT triggers `GLOBAL_ALERT = true` on blackboard
- **Attack Decision**: DT suggests "Pursue" → FSM executes S_CivPersuit

## Integration Points

### Blackboard Integration
- **Read**: Checks `GLOBAL_ALERT` state (future enhancement)
- **Write**: Sets `GLOBAL_ALERT = true` when alert action triggers

### FSM Integration
- **Suggestion-Based**: DT suggests state changes via `Civilian.RequestStateChange()`
- **State Mapping**: DT suggestions mapped to actual FSM state names:
  - "Flee" → "S_CivFlee"
  - "Pursue" → "S_CivPersuit" 
  - "Idle" → "S_CivIdle"
  - "Evade" → "S_CivEvade"
  - "Attack" → "S_CivAttack"
- **Non-Invasive**: FSM retains control over actual state execution
- **Fallback Support**: Works with both ScriptableObject FSM and legacy state system

### Roulette System
When `civilian.CanAttack = true` and player is visible:
- Uses existing `escapeWeight` vs `attackWeight` values
- **Attack Chosen**: Suggests "Pursue" for melee behavior
- **Escape Chosen**: Suggests "Flee" for avoidance behavior

## Performance

- **Evaluation Frequency**: Configurable interval (default 0.35s)
- **Coroutine-Based**: Non-blocking evaluation loop
- **Minimal Overhead**: Simple tree structure with fast boolean checks
- **No Transform Manipulation**: Movement remains through existing steering system

## Troubleshooting

### No DT Logs Appearing
1. Check `debugDT` is enabled in `CivilianDecisionTreeRunner`
2. Verify `useDecisionTree` is true in `Civilian`
3. Ensure civilian is alive and active

### DT Not Affecting Behavior
1. Verify FSM is configured and running
2. Check that state names match between DT suggestions and FSM states
3. Use "Debug Civilian Status" to check integration

### Performance Issues
1. Increase `evaluationInterval` for less frequent evaluation
2. Disable `debugDT` in production builds
3. Monitor civilian count and consider pooling for large numbers

## Future Enhancements

- **Conditional Alerts**: Read blackboard state to influence alert behavior
- **Group Coordination**: DT responses to other civilians' alerts
- **Dynamic Weights**: Runtime adjustment of roulette probabilities
- **State Name Mapping**: Configurable mapping between DT suggestions and FSM states