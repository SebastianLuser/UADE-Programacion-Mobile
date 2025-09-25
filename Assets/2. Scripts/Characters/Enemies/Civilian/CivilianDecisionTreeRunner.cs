using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DevelopmentUtilities;

/// <summary>
/// Decision Tree runner for Civilian NPCs that evaluates behavioral suggestions at runtime.
/// The tree suggests actions (Flee, Alert, Resume) to the Civilian FSM without directly moving transforms.
/// All movement continues through ApplySteering → ObstacleAvoidance system.
/// </summary>
public class CivilianDecisionTreeRunner : MonoBehaviour
{
    [Header("Decision Tree Configuration")]
    [SerializeField] private float evaluationInterval = 0.15f;  // Reduced frequency now that we have locks/hysteresis
    [SerializeField] private float alertChanceWhenNoLoS = 0.5f;
    [SerializeField] private string resumeSuggestion = "Idle";
    [SerializeField] private float alertCooldown = 2f;     // Minimum time between alert triggers
    
    [Header("Attack Configuration")]
    [SerializeField] private float postHitFleeTime = 1.2f;  // Time to flee after landing a hit (hit-and-run)
    
    [Header("Debug")]
    [SerializeField] private bool debugDT = true;  // Enable debug to see roulette working
    
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

    // Decision Tree timing logic (moved from FSM states)
    private float evadeStartTime = 0f;      // When evade state was entered
    private float fleeStartTime = 0f;       // When flee state was entered
    private float safeTimer = 0f;           // Time spent in safe conditions
    private float pursuitStartTime = 0f;    // When pursuit was started (for minimum commitment)

    // Separated LoS timers to avoid overlap/conflicts
    private float pursuitLoseSightTimer_visible = 0f;   // Timer for breaking stance lock when LoS lost in visible branch
    private float pursuitLoseSightTimer_invisible = 0f; // Timer for commitment when LoS lost in invisible branch

    // Attack cycle tracking
    private bool isInAttackCycle = false;   // Whether we're in a non-interruptible attack cycle
    private float attackCycleStartTime = 0f; // When current attack cycle started
    private bool postHitFleeActive = false; // Whether we're in post-hit flee period
    private float postHitFleeStartTime = 0f; // When post-hit flee started

    // Stance Lock system - prevents roulette flip-flop
    private bool currentStance = false;     // true = ATTACK, false = ESCAPE
    private float stanceLockUntil = 0f;     // Time until stance lock expires
    private float stanceLockDuration = 2.5f; // How long to maintain stance (2.5 seconds)
    private bool hasActiveStanceLock = false;
    
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
        // Enhanced decision tree with timing logic and attack cycle management:
        // Root: IsPlayerVisible?
        //   YES → Check melee range → Attack/Pursue/Flee based on stance and range
        //   NO → Check for timeout conditions and alert triggers

        // Create leaf nodes
        var fleeNode = new ActionNode(() => SuggestFlee());
        var pursueNode = new ActionNode(() => SuggestPursue());
        var attackNode = new ActionNode(() => SuggestAttack());
        var evadeNode = new ActionNode(() => SuggestEvade());
        var idleNode = new ActionNode(() => SuggestIdle());
        var alertNode = new ActionNode(() => SuggestAlert());

        // Decision logic when player is visible
        // Priority: Attack cycle (non-interruptible) → Melee range → Roulette choice
        var visibleDecisionNode = new QuestionNode(
            () => IsInNonInterruptibleAttackCycle(),
            attackNode,  // Continue attack cycle if non-interruptible
            new QuestionNode(
                () => IsPlayerInMeleeRange() && civilian.CanAttack && ShouldChooseAttackOverFlee(),
                attackNode,  // Enter attack if in melee and stance is ATTACK
                new QuestionNode(
                    () => ShouldChooseAttackOverFlee(),
                    pursueNode,  // ATTACK stance but not in melee → pursue
                    fleeNode     // ESCAPE stance → flee
                )
            )
        );

        // Decision logic when player is not visible - NEVER suggest flee without LoS
        // Exceptions: Continue current action if within commitment/grace period
        var invisibleDecisionNode = new QuestionNode(
            () => IsInNonInterruptibleAttackCycle(),
            attackNode,  // Continue attack cycle even without LoS (brief window)
            new QuestionNode(
                () => IsCurrentlyPursuing() && IsWithinPursuitCommitment(),
                pursueNode,  // Continue pursuing if committed (hysteresis to prevent jitter)
                new QuestionNode(
                    () => IsCurrentlyFleeing() && !ShouldReturnToIdle(),
                    fleeNode,  // Continue fleeing if already fleeing and grace timer hasn't expired
                    new QuestionNode(
                        () => ShouldTriggerAlert(),
                        alertNode,
                        idleNode  // No LoS = Return to idle (SafeDistance only stops fleeing, doesn't start it)
                    )
                )
            )
        );

        // Create root node - check if player is visible
        _root = new QuestionNode(
            () => {
                bool hasLoS = civilian.HasLoS();
                Debug.Log($"ROOT DECISION: HasLoS = {hasLoS}");
                return hasLoS;
            },
            visibleDecisionNode,
            invisibleDecisionNode
        );

        if (debugDT)
            Logger.LogInfo($"CivilianDecisionTreeRunner on {gameObject.name}: Enhanced decision tree with timing logic built");
    }

    #endregion

    #region Decision Logic

    /// <summary>
    /// Check if civilian is in a non-interruptible attack cycle
    /// </summary>
    private bool IsInNonInterruptibleAttackCycle()
    {
        if (!isInAttackCycle) return false;

        // Calculate total attack cycle duration
        float totalAttackDuration = civilian.AttackWindup + civilian.AttackHitWin + civilian.AttackRecover;
        float timeSinceAttackStart = Time.time - attackCycleStartTime;

        bool stillInCycle = timeSinceAttackStart < totalAttackDuration;

        if (debugDT && stillInCycle)
            Logger.LogInfo($"Non-interruptible attack cycle: {timeSinceAttackStart:F2}s / {totalAttackDuration:F2}s");

        return stillInCycle;
    }

    /// <summary>
    /// Check if player is in melee range for immediate attack
    /// </summary>
    private bool IsPlayerInMeleeRange()
    {
        if (civilian == null) return false;
        return civilian.IsPlayerInMeleeRange();
    }

    /// <summary>
    /// Start an attack cycle - marks as non-interruptible
    /// </summary>
    private void StartAttackCycle()
    {
        isInAttackCycle = true;
        attackCycleStartTime = Time.time;
        postHitFleeActive = false; // Reset any previous post-hit flee

        if (debugDT)
            Logger.LogInfo($"Started non-interruptible attack cycle at {Time.time:F2}");
    }

    /// <summary>
    /// End attack cycle and optionally start post-hit flee
    /// </summary>
    private void EndAttackCycle(bool startPostHitFlee = true)
    {
        isInAttackCycle = false;

        if (startPostHitFlee)
        {
            postHitFleeActive = true;
            postHitFleeStartTime = Time.time;
            
            // Break stance lock and force ESCAPE for hit-and-run
            BreakStanceLock("Post-hit flee - forcing hit-and-run");
            LockStance(false, "Post-hit flee - hit-and-run behavior");

            if (debugDT)
                Logger.LogInfo($"Ended attack cycle, started post-hit flee for {postHitFleeTime}s");
        }
        else if (debugDT)
        {
            Logger.LogInfo($"Ended attack cycle without post-hit flee");
        }
    }

    /// <summary>
    /// Check if we're in post-hit flee period
    /// </summary>
    private bool IsInPostHitFlee()
    {
        if (!postHitFleeActive) return false;

        float timeSincePostHit = Time.time - postHitFleeStartTime;
        bool stillFleeing = timeSincePostHit < postHitFleeTime;

        if (!stillFleeing && postHitFleeActive)
        {
            postHitFleeActive = false;
            if (debugDT)
                Logger.LogInfo($"Post-hit flee period ended after {timeSincePostHit:F2}s");
        }

        return stillFleeing;
    }

    /// <summary>
    /// Determine if civilian should choose attack over flee when player is visible.
    /// Uses the existing roulette system weights.
    /// </summary>
    private bool ShouldChooseAttackOverFlee()
    {
        // Force ESCAPE stance during post-hit flee period
        if (IsInPostHitFlee())
        {
            if (debugDT)
                Logger.LogInfo($"Civilian {civilian.name}: Post-hit flee active - forcing ESCAPE");
            return false;
        }

        // Only consider attack if civilian can attack
        if (!civilian.CanAttack)
        {
            if (debugDT)
                Logger.LogInfo($"Civilian {civilian.name}: Stance Lock - Cannot attack, choosing ESCAPE");

            // Lock into ESCAPE stance
            LockStance(false, "Cannot attack");
            return false;
        }

        // Check if we have an active stance lock
        if (hasActiveStanceLock && Time.time < stanceLockUntil)
        {
            Debug.Log($"STANCE LOCK ACTIVE: Using cached stance = {(currentStance ? "ATTACK" : "ESCAPE")}, expires in {(stanceLockUntil - Time.time):F2}s");

            if (debugDT)
                Logger.LogInfo($"Civilian {civilian.name}: Stance Lock active - Using cached {(currentStance ? "ATTACK" : "ESCAPE")} for {(stanceLockUntil - Time.time):F2}s");

            return currentStance;
        }

        Debug.Log("STANCE LOCK EXPIRED OR NO LOCK - Rolling new roulette");

        var decisions = new Dictionary<string, float>
        {
            {"ATTACK", civilian.AttackWeight},
            {"ESCAPE", civilian.EscapeWeight}
        };
        
        string choice = RouletteWheel<string>.Run(decisions);
        bool chooseAttack = (choice == "ATTACK");

        Debug.Log($"NEW ROULETTE: Choice={choice}, Attack Weight={civilian.AttackWeight}, Escape Weight={civilian.EscapeWeight}");

        // Lock into the new stance
        LockStance(chooseAttack, $"New roulette: {choice}");

        if (debugDT)
            Logger.LogInfo($"Civilian {civilian.name}: New roulette - Choose: {choice}, Locked for {stanceLockDuration}s");

        return chooseAttack;
    }

    /// <summary>
    /// Lock the civilian into a specific stance (ATTACK or ESCAPE) to prevent flip-flop
    /// </summary>
    private void LockStance(bool attackStance, string reason)
    {
        currentStance = attackStance;
        stanceLockUntil = Time.time + stanceLockDuration;
        hasActiveStanceLock = true;

        Debug.Log($"STANCE LOCKED: {(attackStance ? "ATTACK" : "ESCAPE")} for {stanceLockDuration}s - Reason: {reason}");
    }

    /// <summary>
    /// Break stance lock due to specific triggers (distance, LoS loss, etc.)
    /// </summary>
    private void BreakStanceLock(string reason)
    {
        if (hasActiveStanceLock)
        {
            Debug.Log($"STANCE LOCK BROKEN: {reason}");
            hasActiveStanceLock = false;
            stanceLockUntil = 0f;
        }
    }

    /// <summary>
    /// Check if stance lock should be broken due to context changes
    /// </summary>
    private void CheckStanceLockBreakers()
    {
        if (!hasActiveStanceLock) return;

        // Null check for player safety
        if (civilian?.Player == null) 
        {
            BreakStanceLock("Player reference lost");
            return;
        }

        // Break lock if player gets too far away (beyond SafeDistance)
        float distanceToPlayer = Vector3.Distance(civilian.transform.position, civilian.Player.position);
        if (distanceToPlayer >= civilian.SafeDistance)
        {
            BreakStanceLock($"Player too far ({distanceToPlayer:F1} >= {civilian.SafeDistance})");
            return;
        }

        // Break ATTACK stance lock if lost LoS for too long (using VISIBLE timer)
        if (currentStance && !civilian.HasLoS())
        {
            pursuitLoseSightTimer_visible += evaluationInterval;
            if (pursuitLoseSightTimer_visible >= civilian.AttackLoseSightGrace * 2f) // Double the normal grace
            {
                BreakStanceLock($"Lost LoS too long in ATTACK stance ({pursuitLoseSightTimer_visible:F2}s)");
                return;
            }
        }
        else if (civilian.HasLoS())
        {
            pursuitLoseSightTimer_visible = 0f; // Reset visible timer if we can see player
        }

        // Break lock if attack cycle completed (post-hit scenarios handled separately)
        if (isInAttackCycle)
        {
            float totalAttackDuration = civilian.AttackWindup + civilian.AttackHitWin + civilian.AttackRecover;
            float timeSinceAttackStart = Time.time - attackCycleStartTime;
            
            if (timeSinceAttackStart >= totalAttackDuration)
            {
                EndAttackCycle(true); // End with post-hit flee
                return; // EndAttackCycle will break the stance lock
            }
        }
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

    /// <summary>
    /// Determine if civilian should return to idle based on safety conditions and timing
    /// </summary>
    private bool ShouldReturnToIdle()
    {
        Debug.Log("=== ShouldReturnToIdle() START ===");

        if (civilian == null || civilian.Player == null)
        {
            Debug.Log("CIVILIAN OR PLAYER IS NULL - RETURNING TRUE");
            return true;
        }

        float distanceToPlayer = Vector3.Distance(civilian.transform.position, civilian.Player.position);
        bool isSafeDistance = distanceToPlayer >= civilian.SafeDistance;
        bool hasLoS = civilian.HasLoS();

        Debug.Log($"Distance: {distanceToPlayer:F1}, SafeDistance: {civilian.SafeDistance}, HasLoS: {hasLoS}");

        // SafeDistance is only for STOPPING fleeing, not for starting it
        // If player is in safe area (beyond SafeDistance), return to idle immediately
        if (isSafeDistance)
        {
            Debug.Log($"PLAYER IN SAFE AREA - STOP FLEEING");
            safeTimer = 0f;
            return true;
        }

        // If still close but no LoS, use grace timer (for when we lost sight during chase)
        if (!hasLoS)
        {
            safeTimer += evaluationInterval;
            bool shouldReturn = safeTimer >= civilian.SafeTime;

            Debug.Log($"CLOSE BUT NO LoS - GRACE TIMER: Timer={safeTimer:F2}/{civilian.SafeTime}, ShouldReturn={shouldReturn}");
            return shouldReturn;
        }

        // Player is close and visible - keep fleeing
        Debug.Log($"PLAYER CLOSE AND VISIBLE - CONTINUE FLEEING");
        safeTimer = 0f;
        return false;
    }

    /// <summary>
    /// Check if civilian is currently in a fleeing state
    /// </summary>
    private bool IsCurrentlyFleeing()
    {
        string currentState = GetCurrentFSMStateName();
        return currentState == "S_CivFlee" || currentState == "S_CivEvade";
    }

    /// <summary>
    /// Check if civilian is currently pursuing/attacking
    /// </summary>
    private bool IsCurrentlyPursuing()
    {
        string currentState = GetCurrentFSMStateName();
        return currentState == "S_CivPersuit" || currentState == "S_CivAttack";
    }

    /// <summary>
    /// Check if civilian should stay committed to pursuit despite losing LoS
    /// </summary>
    private bool IsWithinPursuitCommitment()
    {
        // Give more commitment time than the lose sight grace (attackLoseSightGrace is 0.3s)
        float commitmentTime = civilian.AttackLoseSightGrace * 3f; // 0.9s commitment
        float timeSincePursuitStart = Time.time - pursuitStartTime;

        // Always commit for at least 1 second after starting pursuit
        if (timeSincePursuitStart < 1f)
        {
            Debug.Log($"PURSUIT COMMITMENT: Within minimum commitment time ({timeSincePursuitStart:F2}s < 1.0s)");
            return true;
        }

        // Then use INVISIBLE lose sight timer with extended grace (separate from visible timer)
        if (!civilian.HasLoS())
        {
            pursuitLoseSightTimer_invisible += evaluationInterval;
        }
        else
        {
            pursuitLoseSightTimer_invisible = 0f; // Reset invisible timer when LoS recovered
        }

        bool stillCommitted = pursuitLoseSightTimer_invisible < commitmentTime;

        Debug.Log($"PURSUIT COMMITMENT: Invisible LoS timer={pursuitLoseSightTimer_invisible:F2}s < {commitmentTime:F2}s = {stillCommitted}");
        return stillCommitted;
    }

    /// <summary>
    /// Check if evade time has elapsed and should transition to flee
    /// </summary>
    private bool ShouldTransitionFromEvade()
    {
        return (Time.time - evadeStartTime) >= civilian.EvadeTime;
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
        {
            // Determine the reason for fleeing based on current context
            bool hasLoS = civilian.HasLoS();
            string reason = hasLoS ? "Player visible, roulette chose escape" : "No LoS, continuing flee behavior";
            Logger.LogInfo($"DT → Flee ({reason})");
        }
    }

    /// <summary>
    /// Suggest attacking the player (dedicated attack state)
    /// </summary>
    private void SuggestAttack()
    {
        // Start attack cycle if not already in one
        if (!isInAttackCycle)
        {
            StartAttackCycle();
            
            if (debugDT)
                Logger.LogInfo($"DT → Attack (Starting attack cycle in melee range)");
        }
        else if (debugDT)
        {
            float totalAttackDuration = civilian.AttackWindup + civilian.AttackHitWin + civilian.AttackRecover;
            float timeSinceAttackStart = Time.time - attackCycleStartTime;
            Logger.LogInfo($"DT → Attack (Continuing attack cycle: {timeSinceAttackStart:F2}s / {totalAttackDuration:F2}s)");
        }

        SetSuggestion("Attack");
    }

    /// <summary>
    /// Suggest pursuing/attacking the player
    /// </summary>
    private void SuggestPursue()
    {
        // Initialize pursuit timing if entering pursuit for first time
        if (currentSuggestion != "Pursue")
        {
            pursuitStartTime = Time.time;
            pursuitLoseSightTimer_invisible = 0f; // Reset invisible lose sight timer when starting pursuit

            if (debugDT)
                Logger.LogInfo($"DT → Pursue (Starting pursuit - commitment time initialized)");
        }
        else
        {
            // Reset invisible lose sight timer if we can see player
            if (civilian.HasLoS())
            {
                pursuitLoseSightTimer_invisible = 0f;
            }
        }

        SetSuggestion("Pursue");

        if (debugDT && currentSuggestion == "Pursue")
            Logger.LogInfo($"DT → Pursue (Continuing pursuit)");
    }

    /// <summary>
    /// Suggest evading the player (short burst movement)
    /// </summary>
    private void SuggestEvade()
    {
        // If we're already evading, check if time elapsed
        if (currentSuggestion == "Evade" && ShouldTransitionFromEvade())
        {
            SuggestFlee();
            return;
        }

        // Set evade start time if entering evade
        if (currentSuggestion != "Evade")
        {
            evadeStartTime = Time.time;
        }

        SetSuggestion("Evade");

        if (debugDT)
            Logger.LogInfo($"DT → Evade (Player visible, evading for {civilian.EvadeTime}s)");
    }

    /// <summary>
    /// Suggest idle behavior
    /// </summary>
    private void SuggestIdle()
    {
        SetSuggestion("Idle");

        if (debugDT)
            Logger.LogInfo($"DT → Idle (Safe conditions met)");
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

            // Check if stance lock should be broken due to context changes
            CheckStanceLockBreakers();

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
        // Skip processing if we're in a non-interruptible attack cycle
        if (IsInNonInterruptibleAttackCycle() && suggestion != "Attack")
        {
            if (debugDT)
                Logger.LogInfo($"Ignoring suggestion '{suggestion}' - in non-interruptible attack cycle");
            return;
        }

        // Note: The Civilian FSM should handle the actual state transitions
        // This is just a bridge to communicate the decision tree's suggestion

        switch (suggestion)
        {
            case "Attack":
                RequestStateChange("Attack");
                break;

            case "Flee":
                RequestStateChange("Fleeing");
                break;

            case "Pursue":
                RequestStateChange("Pursuing");
                break;

            case "Evade":
                RequestStateChange("Evading");
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
    /// Called by Civilian FSM when an attack cycle completes
    /// </summary>
    public void OnAttackCycleComplete()
    {
        if (isInAttackCycle)
        {
            EndAttackCycle(true); // End with post-hit flee
            
            if (debugDT)
                Logger.LogInfo($"Attack cycle completed - starting post-hit flee phase");
        }
    }

    /// <summary>
    /// Called by Civilian FSM when melee damage is dealt
    /// </summary>
    public void OnMeleeDamageDealt()
    {
        if (debugDT)
            Logger.LogInfo($"Melee damage dealt - attack cycle will complete soon");
        
        // The attack cycle will complete naturally and trigger post-hit flee
    }

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
        Debug.Log($"Post-Hit Flee Time: {postHitFleeTime}s");
        Debug.Log($"Time Since Last Alert: {(lastAlertTime < 0 ? "Never" : (Time.time - lastAlertTime).ToString("F2") + "s")}");
        Debug.Log($"Resume Suggestion: {resumeSuggestion}");
        Debug.Log($"Current Suggestion: {currentSuggestion}");
        Debug.Log($"Last Suggestion: {lastSuggestion}");
        Debug.Log($"Current FSM State: {GetCurrentFSMStateName()}");
        Debug.Log($"Expected FSM State: {MapSuggestionToFSMStateName(currentSuggestion)}");
        Debug.Log($"FSM State Matches: {IsCurrentFSMStateMatchingSuggestion()}");
        Debug.Log($"Last Evaluation: {Time.time - lastEvaluationTime:F2}s ago");
        
        // Stance Lock Status
        Debug.Log($"--- STANCE LOCK ---");
        Debug.Log($"Has Active Lock: {hasActiveStanceLock}");
        Debug.Log($"Current Stance: {(currentStance ? "ATTACK" : "ESCAPE")}");
        Debug.Log($"Lock Expires In: {(hasActiveStanceLock ? (stanceLockUntil - Time.time).ToString("F2") + "s" : "N/A")}");
        
        // Attack Cycle Status
        Debug.Log($"--- ATTACK CYCLE ---");
        Debug.Log($"Is In Attack Cycle: {isInAttackCycle}");
        Debug.Log($"Attack Start Time: {(isInAttackCycle ? (Time.time - attackCycleStartTime).ToString("F2") + "s ago" : "N/A")}");
        Debug.Log($"Post-Hit Flee Active: {postHitFleeActive}");
        Debug.Log($"Post-Hit Flee Time Left: {(postHitFleeActive ? (postHitFleeTime - (Time.time - postHitFleeStartTime)).ToString("F2") + "s" : "N/A")}");
        
        // Timers Status
        Debug.Log($"--- TIMERS ---");
        Debug.Log($"Pursuit LoS Timer (Visible): {pursuitLoseSightTimer_visible:F2}s");
        Debug.Log($"Pursuit LoS Timer (Invisible): {pursuitLoseSightTimer_invisible:F2}s");
        Debug.Log($"Safe Timer: {safeTimer:F2}s");
        Debug.Log($"Pursuit Start Time: {(pursuitStartTime > 0 ? (Time.time - pursuitStartTime).ToString("F2") + "s ago" : "Never")}");
        
        // General Status
        Debug.Log($"--- GENERAL ---");
        Debug.Log($"Debug Enabled: {debugDT}");
        Debug.Log($"Can See Player: {(civilian != null ? civilian.HasLoS() : "N/A")}");
        Debug.Log($"Can Attack: {(civilian != null ? civilian.CanAttack : "N/A")}");
        Debug.Log($"In Melee Range: {(civilian != null ? civilian.IsPlayerInMeleeRange() : "N/A")}");
        Debug.Log($"Distance to Player: {(civilian != null && civilian.Player != null ? Vector3.Distance(civilian.transform.position, civilian.Player.position).ToString("F2") : "N/A")}");
        
        if (blackboard != null)
        {
            Debug.Log($"Global Alert: {blackboard.GetValue<bool>(BlackboardKeys.GLOBAL_ALERT)}");
        }
        
        Debug.Log("====================================");
    }

    #endregion
}