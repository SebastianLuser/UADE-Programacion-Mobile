using UnityEngine;

/// <summary>
/// Simple test utility to validate Civilian Decision Tree functionality
/// </summary>
public class CivilianDecisionTreeTest : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool runContinuousTest = false;
    [SerializeField] private float testInterval = 2f;
    
    private Civilian civilian;
    private CivilianDecisionTreeRunner dtRunner;
    private float lastTestTime;

    private void Start()
    {
        civilian = GetComponent<Civilian>();
        dtRunner = GetComponent<CivilianDecisionTreeRunner>();
        
        if (civilian == null)
        {
            Logger.LogError("CivilianDecisionTreeTest: No Civilian component found!");
            enabled = false;
            return;
        }
        
        Logger.LogInfo("CivilianDecisionTreeTest: Test initialized");
    }

    private void Update()
    {
        if (runContinuousTest && Time.time - lastTestTime > testInterval)
        {
            RunTest();
            lastTestTime = Time.time;
        }
    }

    [ContextMenu("Run Decision Tree Test")]
    public void RunTest()
    {
        if (civilian == null) return;

        Debug.Log("=== CIVILIAN DECISION TREE TEST ===");
        
        // Test civilian state
        Debug.Log($"Civilian Active: {civilian.IsActive}");
        Debug.Log($"Can See Player: {civilian.HasLoS()}");
        Debug.Log($"Distance to Player: {civilian.GetDistanceToPlayer():F2}");
        Debug.Log($"Can Attack: {civilian.CanAttack}");
        Debug.Log($"Using Decision Tree: {civilian.UseDecisionTree}");
        Debug.Log($"Decision Tree Active: {civilian.IsDecisionTreeActive()}");
        
        // Test decision tree runner
        if (dtRunner != null)
        {
            Debug.Log($"DT Debug Enabled: {dtRunner.DebugEnabled}");
            Debug.Log($"DT Current Suggestion: {dtRunner.CurrentSuggestion}");
            Debug.Log($"DT Last Suggestion: {dtRunner.LastSuggestion}");
            Debug.Log($"DT Status: {dtRunner.GetStatus()}");
            
            // Manually trigger evaluation
            dtRunner.EvaluateDecisionTree();
        }
        else
        {
            Debug.Log("Decision Tree Runner: Not found");
        }
        
        // Test blackboard
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard != null)
        {
            Debug.Log($"Global Alert: {blackboard.GetValue<bool>(BlackboardKeys.GLOBAL_ALERT)}");
        }
        else
        {
            Debug.Log("Blackboard: Not available");
        }
        
        Debug.Log("=================================");
    }

    [ContextMenu("Toggle Global Alert")]
    public void ToggleGlobalAlert()
    {
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard != null)
        {
            bool currentAlert = blackboard.GetValue<bool>(BlackboardKeys.GLOBAL_ALERT);
            blackboard.SetValue(BlackboardKeys.GLOBAL_ALERT, !currentAlert);
            Debug.Log($"Global Alert toggled to: {!currentAlert}");
        }
    }

    [ContextMenu("Force DT Evaluation")]
    public void ForceDTEvaluation()
    {
        if (dtRunner != null)
        {
            Debug.Log("Forcing Decision Tree evaluation...");
            dtRunner.EvaluateDecisionTree();
        }
    }

    [ContextMenu("Test State Change - Flee")]
    public void TestStateChangeFlee()
    {
        if (civilian != null)
        {
            Debug.Log("Testing state change to Fleeing...");
            civilian.RequestStateChange("Fleeing");
        }
    }

    [ContextMenu("Test State Change - Idle")]
    public void TestStateChangeIdle()
    {
        if (civilian != null)
        {
            Debug.Log("Testing state change to Idle...");
            civilian.RequestStateChange("Idle");
        }
    }

    [ContextMenu("Test State Change - Pursue")]
    public void TestStateChangePursue()
    {
        if (civilian != null)
        {
            Debug.Log("Testing state change to Pursue...");
            civilian.RequestStateChange("Pursue");
        }
    }

    [ContextMenu("Test State Change - Evade")]
    public void TestStateChangeEvade()
    {
        if (civilian != null)
        {
            Debug.Log("Testing state change to Evade...");
            civilian.RequestStateChange("Evade");
        }
    }

    [ContextMenu("Test State Change - Attack")]
    public void TestStateChangeAttack()
    {
        if (civilian != null)
        {
            Debug.Log("Testing state change to Attack...");
            civilian.RequestStateChange("Attack");
        }
    }

    [ContextMenu("List Available FSM States")]
    public void ListAvailableFSMStates()
    {
        if (civilian != null)
        {
            Debug.Log("Listing available FSM states...");
            civilian.DebugCivilianStatus(); // This will show available states
        }
    }

    [ContextMenu("Check DT vs FSM State Sync")]
    public void CheckDTvsFSMStateSync()
    {
        if (dtRunner != null)
        {
            Debug.Log("Checking DT vs FSM state synchronization...");
            dtRunner.DebugDecisionTreeStatus(); // This will show state matching info
        }
    }
}