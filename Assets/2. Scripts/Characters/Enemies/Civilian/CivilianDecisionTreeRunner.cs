using UnityEngine;
using System.Collections;

/// <summary>
/// Decision Tree runner for Civilian NPCs that evaluates behavioral suggestions at runtime.
/// The tree suggests actions (Flee, Alert, Resume) to the Civilian FSM without directly moving transforms.
/// All movement continues through ApplySteering → ObstacleAvoidance system.
/// </summary>
public class CivilianDecisionTreeRunner : MonoBehaviour
{
    [Header("Decision Tree Configuration")]
    [SerializeField] private float evaluationInterval = 0.35f;
    [SerializeField] private float alertChanceWhenNoLoS = 0.5f;
    [SerializeField] private string resumeSuggestion = "Idle";
    [SerializeField] private float alertCooldown = 2f;     // Minimum time between alert triggers
    
    [Header("Debug")]
    [SerializeField] private bool debugDT = false;
    
    // Decision Tree components
    private ITreeNode _root;
    
    // Component references
    private Civilian civilian;
    private IBlackboard blackboard;
    
    // State tracking
    private string currentSuggestion = "";
    private string lastSuggestion = "";
    private float lastEvaluationTime = 0f;
    private float lastAlertTime = -1f;      // Last time an alert was triggered
    
    // Coroutine reference
    private Coroutine evaluationCoroutine;

    #region Properties
    
    /// <summary>
    /// Last suggestion made by the decision tree (read-only for diagnostics)
    /// </summary>
    public string LastSuggestion => lastSuggestion;
    
    /// <summary>
    /// Current suggestion from the decision tree
    /// </summary>
    public string CurrentSuggestion => currentSuggestion;
    
    /// <summary>
    /// Whether debug logging is enabled
    /// </summary>
    public bool DebugEnabled => debugDT;
    
    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Get required components
        civilian = GetComponent<Civilian>();
        if (civilian == null)
        {
            Logger.LogError($"CivilianDecisionTreeRunner on {gameObject.name}: No Civilian component found!");
            enabled = false;
            return;
        }

        // Get blackboard service
        blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null)
        {
            Logger.LogWarning($"CivilianDecisionTreeRunner on {gameObject.name}: Blackboard service not available");
        }
    }

    private void Start()
    {
        BuildDecisionTree();
        StartEvaluationLoop();
        
        if (debugDT)
            Logger.LogInfo($"CivilianDecisionTreeRunner on {gameObject.name}: Decision tree initialized and evaluation started");
    }

    private void OnDisable()
    {
        StopEvaluationLoop();
    }

    private void OnDestroy()
    {
        StopEvaluationLoop();
    }

    #endregion

    #region Decision Tree Construction

    private void BuildDecisionTree()
    {
        // Build the decision tree structure:
        // Root: IsPlayerVisible?
        //   YES → Check if should attack (roulette) or flee
        //     Attack → Action_PursueSuggest
        //     Flee → Action_FleeSuggest
        //   NO → Question_Random(alertChanceWhenNoLoS)
        //     YES → Action_Alert
        //     NO → Action_Resume

        // Create leaf nodes
        var fleeNode = new ActionNode(() => SuggestFlee());
        var pursueNode = new ActionNode(() => SuggestPursue());
        var alertNode = new ActionNode(() => SuggestAlert());
        var resumeNode = new ActionNode(() => SuggestResume());

        // Create roulette node for attack vs flee decision when player is visible
        var attackDecisionNode = new QuestionNode(
            () => ShouldChooseAttackOverFlee(),
            pursueNode,
            fleeNode
        );

        // Create random alert node for when player is not visible
        var alertDecisionNode = new QuestionNode(
            () => ShouldTriggerAlert(),
            alertNode,
            resumeNode
        );

        // Create root node - check if player is visible
        _root = new QuestionNode(
            () => civilian.HasLoS(),
            attackDecisionNode,
            alertDecisionNode
        );

        if (debugDT)
            Logger.LogInfo($"CivilianDecisionTreeRunner on {gameObject.name}: Decision tree built successfully");
    }

    #endregion

    #region Decision Logic

    /// <summary>
    /// Determine if civilian should choose attack over flee when player is visible.
    /// Uses the existing roulette system weights.
    /// </summary>
    private bool ShouldChooseAttackOverFlee()
    {
        // Only consider attack if civilian can attack
        if (!civilian.CanAttack)
            return false;

        // Use the existing roulette weights
        float totalWeight = civilian.EscapeWeight + civilian.AttackWeight;
        float attackThreshold = civilian.AttackWeight / totalWeight;
        
        return UnityEngine.Random.value < attackThreshold;
    }

    /// <summary>
    /// Determine if an alert should be triggered with cooldown protection
    /// </summary>
    private bool ShouldTriggerAlert()
    {
        // Check if enough time has passed since last alert
        if (Time.time - lastAlertTime < alertCooldown)
        {
            return false; // Still in cooldown period
        }

        // Check random chance
        bool shouldAlert = UnityEngine.Random.value < alertChanceWhenNoLoS;
        
        if (shouldAlert)
        {
            lastAlertTime = Time.time; // Update last alert time
        }
        
        return shouldAlert;
    }

    #endregion

    #region Action Suggestions

    /// <summary>
    /// Suggest fleeing from the player
    /// </summary>
    private void SuggestFlee()
    {
        SetSuggestion("Flee");
        
        if (debugDT)
            Logger.LogInfo($"DT → Flee (Player visible, choosing escape)");
    }

    /// <summary>
    /// Suggest pursuing/attacking the player
    /// </summary>
    private void SuggestPursue()
    {
        SetSuggestion("Pursue");
        
        if (debugDT)
            Logger.LogInfo($"DT → Pursue (Player visible, choosing attack)");
    }

    /// <summary>
    /// Suggest alerting other NPCs and then resuming
    /// </summary>
    private void SuggestAlert()
    {
        // Trigger global alert only if it's not already set
        if (blackboard != null)
        {
            bool currentAlert = blackboard.GetValue<bool>(BlackboardKeys.GLOBAL_ALERT);
            if (!currentAlert)
            {
                blackboard.SetValue(BlackboardKeys.GLOBAL_ALERT, true);
                
                if (debugDT)
                    Logger.LogInfo($"DT → Alert (No LoS, setting GLOBAL_ALERT to true, cooldown: {alertCooldown}s)");
            }
            else if (debugDT)
            {
                Logger.LogInfo($"DT → Alert (No LoS, GLOBAL_ALERT already true, skipping)");
            }
        }
        else if (debugDT)
        {
            Logger.LogInfo($"DT → Alert (No LoS, blackboard unavailable)");
        }
        
        SetSuggestion(resumeSuggestion);
    }

    /// <summary>
    /// Suggest resuming normal behavior
    /// </summary>
    private void SuggestResume()
    {
        SetSuggestion(resumeSuggestion);
        
        if (debugDT)
            Logger.LogInfo($"DT → Resume (No LoS, continuing normal behavior)");
    }

    /// <summary>
    /// Set the current suggestion and track changes
    /// </summary>
    private void SetSuggestion(string suggestion)
    {
        if (currentSuggestion != suggestion)
        {
            lastSuggestion = currentSuggestion;
            currentSuggestion = suggestion;
        }
    }

    #endregion

    #region Evaluation Loop

    /// <summary>
    /// Start the decision tree evaluation loop
    /// </summary>
    private void StartEvaluationLoop()
    {
        if (evaluationCoroutine == null)
        {
            evaluationCoroutine = StartCoroutine(EvaluationLoop());
        }
    }

    /// <summary>
    /// Stop the decision tree evaluation loop
    /// </summary>
    private void StopEvaluationLoop()
    {
        if (evaluationCoroutine != null)
        {
            StopCoroutine(evaluationCoroutine);
            evaluationCoroutine = null;
        }
    }

    /// <summary>
    /// Main evaluation loop that runs the decision tree at intervals
    /// </summary>
    private IEnumerator EvaluationLoop()
    {
        var waitTime = new WaitForSeconds(evaluationInterval);

        while (enabled && gameObject.activeInHierarchy)
        {
            yield return waitTime;

            // Skip evaluation if civilian is not alive
            if (!civilian.IsActive)
                continue;

            // Run the decision tree
            string previousSuggestion = currentSuggestion;
            
            if (_root != null)
            {
                _root.Execute();
            }

            // Process suggestion if it changed OR if FSM is not in the suggested state
            bool suggestionChanged = currentSuggestion != previousSuggestion;
            bool needsStateSync = !string.IsNullOrEmpty(currentSuggestion) && !IsCurrentFSMStateMatchingSuggestion();
            
            if ((suggestionChanged || needsStateSync) && !string.IsNullOrEmpty(currentSuggestion))
            {
                if (debugDT && needsStateSync && !suggestionChanged)
                    Logger.LogInfo($"DT re-processing suggestion '{currentSuggestion}' (FSM state sync needed)");
                    
                ProcessSuggestion(currentSuggestion);
            }

            lastEvaluationTime = Time.time;
        }
    }

    #endregion

    #region FSM State Checking

    /// <summary>
    /// Check if the current FSM state matches the current DT suggestion
    /// </summary>
    private bool IsCurrentFSMStateMatchingSuggestion()
    {
        if (civilian == null || string.IsNullOrEmpty(currentSuggestion))
            return true; // Assume match if we can't determine

        // Map the suggestion to the expected FSM state name
        string expectedStateName = MapSuggestionToFSMStateName(currentSuggestion);
        
        // Get current FSM state name
        string currentStateName = GetCurrentFSMStateName();
        
        if (debugDT && !string.IsNullOrEmpty(currentStateName))
        {
            // Only log occasionally to avoid spam, or when there's a mismatch
            bool isMatch = string.Equals(currentStateName, expectedStateName, System.StringComparison.OrdinalIgnoreCase);
            if (!isMatch)
            {
                Logger.LogInfo($"FSM state mismatch - Current: '{currentStateName}', Expected: '{expectedStateName}'");
            }
        }

        return string.Equals(currentStateName, expectedStateName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Map DT suggestion to FSM state name (same logic as in Civilian)
    /// </summary>
    private string MapSuggestionToFSMStateName(string suggestion)
    {
        switch (suggestion.ToLower())
        {
            case "fleeing":
            case "flee":
                return "S_CivFlee";
                
            case "pursuing":
            case "pursue":
                return "S_CivPersuit";
                
            case "idle":
                return "S_CivIdle";
                
            case "evading":
            case "evade":
                return "S_CivEvade";
                
            case "attack":
            case "attacking":
                return "S_CivAttack";
                
            default:
                return suggestion;
        }
    }

    /// <summary>
    /// Get the current FSM state name
    /// </summary>
    private string GetCurrentFSMStateName()
    {
        // Use reflection to access the civilian's FSM state
        if (civilian != null)
        {
            try
            {
                var civilianType = civilian.GetType();
                var stateMachineField = civilianType.GetField("stateMachine", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (stateMachineField?.GetValue(civilian) is Scripts.FSM.Base.StateMachine.StateMachine stateMachine)
                {
                    var currentState = stateMachine.GetCurrentState();
                    return currentState?.State?.StateName ?? "";
                }
            }
            catch (System.Exception e)
            {
                if (debugDT)
                    Logger.LogWarning($"Failed to get FSM state name: {e.Message}");
            }
        }
        
        return "";
    }

    #endregion

    #region FSM Integration

    /// <summary>
    /// Process a suggestion from the decision tree by requesting FSM state changes
    /// </summary>
    private void ProcessSuggestion(string suggestion)
    {
        // Note: The Civilian FSM should handle the actual state transitions
        // This is just a bridge to communicate the decision tree's suggestion
        
        switch (suggestion)
        {
            case "Flee":
                RequestStateChange("Fleeing");
                break;
                
            case "Pursue":
                RequestStateChange("Pursuing");
                break;
                
            case "Idle":
                RequestStateChange("Idle");
                break;
                
            default:
                RequestStateChange(suggestion);
                break;
        }
    }

    /// <summary>
    /// Request a state change from the Civilian's FSM
    /// </summary>
    private void RequestStateChange(string stateName)
    {
        // Use the Civilian's RequestStateChange method to bridge to FSM
        if (civilian != null)
        {
            civilian.RequestStateChange(stateName);
        }
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Manually trigger a decision tree evaluation (useful for testing)
    /// </summary>
    [ContextMenu("Evaluate Decision Tree")]
    public void EvaluateDecisionTree()
    {
        if (_root != null)
        {
            string previousSuggestion = currentSuggestion;
            _root.Execute();
            
            if (debugDT)
                Logger.LogInfo($"Manual DT evaluation: {currentSuggestion}");
                
            if (currentSuggestion != previousSuggestion)
            {
                ProcessSuggestion(currentSuggestion);
            }
        }
    }

    /// <summary>
    /// Get current decision tree status for debugging
    /// </summary>
    public string GetStatus()
    {
        return $"Current: {currentSuggestion}, Last: {lastSuggestion}, LastEval: {Time.time - lastEvaluationTime:F2}s ago";
    }

    #endregion

    #region Debug

    [ContextMenu("Debug Decision Tree Status")]
    private void DebugDecisionTreeStatus()
    {
        Debug.Log("=== CIVILIAN DECISION TREE STATUS ===");
        Debug.Log($"Evaluation Interval: {evaluationInterval}s");
        Debug.Log($"Alert Chance (No LoS): {alertChanceWhenNoLoS:P0}");
        Debug.Log($"Alert Cooldown: {alertCooldown}s");
        Debug.Log($"Time Since Last Alert: {(lastAlertTime < 0 ? "Never" : (Time.time - lastAlertTime).ToString("F2") + "s")}");
        Debug.Log($"Resume Suggestion: {resumeSuggestion}");
        Debug.Log($"Current Suggestion: {currentSuggestion}");
        Debug.Log($"Last Suggestion: {lastSuggestion}");
        Debug.Log($"Current FSM State: {GetCurrentFSMStateName()}");
        Debug.Log($"Expected FSM State: {MapSuggestionToFSMStateName(currentSuggestion)}");
        Debug.Log($"FSM State Matches: {IsCurrentFSMStateMatchingSuggestion()}");
        Debug.Log($"Last Evaluation: {Time.time - lastEvaluationTime:F2}s ago");
        Debug.Log($"Debug Enabled: {debugDT}");
        Debug.Log($"Can See Player: {(civilian != null ? civilian.HasLoS() : "N/A")}");
        Debug.Log($"Can Attack: {(civilian != null ? civilian.CanAttack : "N/A")}");
        
        if (blackboard != null)
        {
            Debug.Log($"Global Alert: {blackboard.GetValue<bool>(BlackboardKeys.GLOBAL_ALERT)}");
        }
        
        Debug.Log("====================================");
    }

    #endregion
}