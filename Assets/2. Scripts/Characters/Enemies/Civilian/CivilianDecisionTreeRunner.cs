using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Services.MicroServices.BlackboardService;
using DevelopmentUtilities;
using Services;

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
    private ITreeNode m_root;
    
    // Component references
    private Civilian m_civilian;
    
    // State tracking
    private string m_currentSuggestion = "";
    private string m_lastSuggestion = "";
    private float m_lastEvaluationTime = 0f;
    private float m_lastAlertTime = -1f;      // Last time an alert was triggered

    // Decision Tree timing logic (moved from FSM states)
    private float m_evadeStartTime = 0f;      // When evade state was entered
    private float m_fleeStartTime = 0f;       // When flee state was entered
    private float m_safeTimer = 0f;           // Time spent in safe conditions
    private float m_pursuitStartTime = 0f;    // When pursuit was started (for minimum commitment)

    // Separated LoS timers to avoid overlap/conflicts
    private float m_pursuitLoseSightTimerVisible = 0f;   // Timer for breaking stance lock when LoS lost in visible branch
    private float m_pursuitLoseSightTimerInvisible = 0f; // Timer for commitment when LoS lost in invisible branch

    // Attack cycle tracking
    private bool m_isInAttackCycle = false;   // Whether we're in a non-interruptible attack cycle
    private float m_attackCycleStartTime = 0f; // When current attack cycle started
    private bool m_postHitFleeActive = false; // Whether we're in post-hit flee period
    private float m_postHitFleeStartTime = 0f; // When post-hit flee started

    // Stance Lock system - prevents roulette flip-flop
    private bool m_currentStance = false;     // true = ATTACK, false = ESCAPE
    private float m_stanceLockUntil = 0f;     // Time until stance lock expires
    private float m_stanceLockDuration = 2.5f; // How long to maintain stance (2.5 seconds)
    private bool m_hasActiveStanceLock = false;
    
    // Coroutine reference
    private Coroutine m_evaluationCoroutine;
    
    private static IBlackboardService BlackboardService => ServiceLocator.Get<IBlackboardService>();

    #region Properties
    
    /// <summary>
    /// Last suggestion made by the decision tree (read-only for diagnostics)
    /// </summary>
    public string LastSuggestion => m_lastSuggestion;
    
    /// <summary>
    /// Current suggestion from the decision tree
    /// </summary>
    public string CurrentSuggestion => m_currentSuggestion;
    
    /// <summary>
    /// Whether debug logging is enabled
    /// </summary>
    public bool DebugEnabled => debugDT;
    
    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Get required components
        m_civilian = GetComponent<Civilian>();
        if (m_civilian == null)
        {
            MyLogger.LogError($"CivilianDecisionTreeRunner on {gameObject.name}: No Civilian component found!");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        BuildDecisionTree();
        StartEvaluationLoop();
        
        if (debugDT)
            MyLogger.LogInfo($"CivilianDecisionTreeRunner on {gameObject.name}: Decision tree initialized and evaluation started");
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
        var l_fleeNode = new ActionNode(() => SuggestFlee());
        var l_pursueNode = new ActionNode(() => SuggestPursue());
        var l_attackNode = new ActionNode(() => SuggestAttack());
        var l_evadeNode = new ActionNode(() => SuggestEvade());
        var l_idleNode = new ActionNode(() => SuggestIdle());
        var l_alertNode = new ActionNode(() => SuggestAlert());

        // Decision logic when player is visible
        // Priority: Attack cycle (non-interruptible) → Melee range → Roulette choice
        var l_visibleDecisionNode = new QuestionNode(
            () => IsInNonInterruptibleAttackCycle(),
            l_attackNode,  // Continue attack cycle if non-interruptible
            new QuestionNode(
                () => IsPlayerInMeleeRange() && m_civilian.CanAttack && ShouldChooseAttackOverFlee(),
                l_attackNode,  // Enter attack if in melee and stance is ATTACK
                new QuestionNode(
                    () => ShouldChooseAttackOverFlee(),
                    l_pursueNode,  // ATTACK stance but not in melee → pursue
                    l_fleeNode     // ESCAPE stance → flee
                )
            )
        );

        // Decision logic when player is not visible - NEVER suggest flee without LoS
        // Exceptions: Continue current action if within commitment/grace period
        var l_invisibleDecisionNode = new QuestionNode(
            () => IsInNonInterruptibleAttackCycle(),
            l_attackNode,  // Continue attack cycle even without LoS (brief window)
            new QuestionNode(
                () => IsCurrentlyPursuing() && IsWithinPursuitCommitment(),
                l_pursueNode,  // Continue pursuing if committed (hysteresis to prevent jitter)
                new QuestionNode(
                    () => IsCurrentlyFleeing() && !ShouldReturnToIdle(),
                    l_fleeNode,  // Continue fleeing if already fleeing and grace timer hasn't expired
                    new QuestionNode(
                        () => ShouldTriggerAlert(),
                        l_alertNode,
                        l_idleNode  // No LoS = Return to idle (SafeDistance only stops fleeing, doesn't start it)
                    )
                )
            )
        );

        // Create root node - check if player is visible
        m_root = new QuestionNode(
            () => {
                bool l_hasLoS = m_civilian.HasLoS();
                //Debug.Log($"ROOT DECISION: HasLoS = {l_hasLoS}");
                return l_hasLoS;
            },
            l_visibleDecisionNode,
            l_invisibleDecisionNode
        );

        if (debugDT)
            MyLogger.LogInfo($"CivilianDecisionTreeRunner on {gameObject.name}: Enhanced decision tree with timing logic built");
    }

    #endregion

    #region Decision Logic

    /// <summary>
    /// Check if civilian is in a non-interruptible attack cycle
    /// </summary>
    private bool IsInNonInterruptibleAttackCycle()
    {
        if (!m_isInAttackCycle) return false;

        // Calculate total attack cycle duration
        float l_totalAttackDuration = m_civilian.AttackWindup + m_civilian.AttackHitWin + m_civilian.AttackRecover;
        float l_timeSinceAttackStart = Time.time - m_attackCycleStartTime;

        bool l_stillInCycle = l_timeSinceAttackStart < l_totalAttackDuration;

        if (debugDT && l_stillInCycle)
            MyLogger.LogInfo($"Non-interruptible attack cycle: {l_timeSinceAttackStart:F2}s / {l_totalAttackDuration:F2}s");

        return l_stillInCycle;
    }

    /// <summary>
    /// Check if player is in melee range for immediate attack
    /// </summary>
    private bool IsPlayerInMeleeRange()
    {
        if (m_civilian == null) return false;
        return m_civilian.IsPlayerInMeleeRange();
    }

    /// <summary>
    /// Start an attack cycle - marks as non-interruptible
    /// </summary>
    private void StartAttackCycle()
    {
        m_isInAttackCycle = true;
        m_attackCycleStartTime = Time.time;
        m_postHitFleeActive = false; // Reset any previous post-hit flee

        if (debugDT)
            MyLogger.LogInfo($"Started non-interruptible attack cycle at {Time.time:F2}");
    }

    /// <summary>
    /// End attack cycle and optionally start post-hit flee
    /// </summary>
    private void EndAttackCycle(bool p_startPostHitFlee = true)
    {
        m_isInAttackCycle = false;

        if (p_startPostHitFlee)
        {
            m_postHitFleeActive = true;
            m_postHitFleeStartTime = Time.time;
            
            // Break stance lock and force ESCAPE for hit-and-run
            BreakStanceLock("Post-hit flee - forcing hit-and-run");
            LockStance(false, "Post-hit flee - hit-and-run behavior");

            if (debugDT)
                MyLogger.LogInfo($"Ended attack cycle, started post-hit flee for {postHitFleeTime}s");
        }
        else if (debugDT)
        {
            MyLogger.LogInfo($"Ended attack cycle without post-hit flee");
        }
    }

    /// <summary>
    /// Check if we're in post-hit flee period
    /// </summary>
    private bool IsInPostHitFlee()
    {
        if (!m_postHitFleeActive) return false;

        float l_timeSincePostHit = Time.time - m_postHitFleeStartTime;
        bool l_stillFleeing = l_timeSincePostHit < postHitFleeTime;

        if (!l_stillFleeing && m_postHitFleeActive)
        {
            m_postHitFleeActive = false;
            if (debugDT)
                MyLogger.LogInfo($"Post-hit flee period ended after {l_timeSincePostHit:F2}s");
        }

        return l_stillFleeing;
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
                MyLogger.LogInfo($"Civilian {m_civilian.name}: Post-hit flee active - forcing ESCAPE");
            return false;
        }

        // Only consider attack if civilian can attack
        if (!m_civilian.CanAttack)
        {
            if (debugDT)
                MyLogger.LogInfo($"Civilian {m_civilian.name}: Stance Lock - Cannot attack, choosing ESCAPE");

            // Lock into ESCAPE stance
            LockStance(false, "Cannot attack");
            return false;
        }

        // Check if we have an active stance lock
        if (m_hasActiveStanceLock && Time.time < m_stanceLockUntil)
        {
            Debug.Log($"STANCE LOCK ACTIVE: Using cached stance = {(m_currentStance ? "ATTACK" : "ESCAPE")}, expires in {(m_stanceLockUntil - Time.time):F2}s");

            if (debugDT)
                MyLogger.LogInfo($"Civilian {m_civilian.name}: Stance Lock active - Using cached {(m_currentStance ? "ATTACK" : "ESCAPE")} for {(m_stanceLockUntil - Time.time):F2}s");

            return m_currentStance;
        }

        Debug.Log("STANCE LOCK EXPIRED OR NO LOCK - Rolling new roulette");

        var l_decisions = new Dictionary<string, float>
        {
            {"ATTACK", m_civilian.AttackWeight},
            {"ESCAPE", m_civilian.EscapeWeight}
        };
        
        string l_choice = RouletteWheel<string>.Run(l_decisions);
        bool l_chooseAttack = (l_choice == "ATTACK");

        Debug.Log($"NEW ROULETTE: Choice={l_choice}, Attack Weight={m_civilian.AttackWeight}, Escape Weight={m_civilian.EscapeWeight}");

        // Lock into the new stance
        LockStance(l_chooseAttack, $"New roulette: {l_choice}");

        if (debugDT)
            MyLogger.LogInfo($"Civilian {m_civilian.name}: New roulette - Choose: {l_choice}, Locked for {m_stanceLockDuration}s");

        return l_chooseAttack;
    }

    /// <summary>
    /// Lock the civilian into a specific stance (ATTACK or ESCAPE) to prevent flip-flop
    /// </summary>
    private void LockStance(bool p_attackStance, string p_reason)
    {
        m_currentStance = p_attackStance;
        m_stanceLockUntil = Time.time + m_stanceLockDuration;
        m_hasActiveStanceLock = true;

        Debug.Log($"STANCE LOCKED: {(p_attackStance ? "ATTACK" : "ESCAPE")} for {m_stanceLockDuration}s - Reason: {p_reason}");
    }

    /// <summary>
    /// Break stance lock due to specific triggers (distance, LoS loss, etc.)
    /// </summary>
    private void BreakStanceLock(string p_reason)
    {
        if (m_hasActiveStanceLock)
        {
            Debug.Log($"STANCE LOCK BROKEN: {p_reason}");
            m_hasActiveStanceLock = false;
            m_stanceLockUntil = 0f;
        }
    }

    /// <summary>
    /// Check if stance lock should be broken due to context changes
    /// </summary>
    private void CheckStanceLockBreakers()
    {
        if (!m_hasActiveStanceLock) return;

        // Null check for player safety
        if (m_civilian?.Player == null) 
        {
            BreakStanceLock("Player reference lost");
            return;
        }

        // Break lock if player gets too far away (beyond SafeDistance)
        float l_distanceToPlayer = Vector3.Distance(m_civilian.transform.position, m_civilian.Player.position);
        if (l_distanceToPlayer >= m_civilian.SafeDistance)
        {
            BreakStanceLock($"Player too far ({l_distanceToPlayer:F1} >= {m_civilian.SafeDistance})");
            return;
        }

        // Break ATTACK stance lock if lost LoS for too long (using VISIBLE timer)
        if (m_currentStance && !m_civilian.HasLoS())
        {
            m_pursuitLoseSightTimerVisible += evaluationInterval;
            if (m_pursuitLoseSightTimerVisible >= m_civilian.AttackLoseSightGrace * 2f) // Double the normal grace
            {
                BreakStanceLock($"Lost LoS too long in ATTACK stance ({m_pursuitLoseSightTimerVisible:F2}s)");
                return;
            }
        }
        else if (m_civilian.HasLoS())
        {
            m_pursuitLoseSightTimerVisible = 0f; // Reset visible timer if we can see player
        }

        // Break lock if attack cycle completed (post-hit scenarios handled separately)
        if (m_isInAttackCycle)
        {
            float l_totalAttackDuration = m_civilian.AttackWindup + m_civilian.AttackHitWin + m_civilian.AttackRecover;
            float l_timeSinceAttackStart = Time.time - m_attackCycleStartTime;
            
            if (l_timeSinceAttackStart >= l_totalAttackDuration)
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
        if (Time.time - m_lastAlertTime < alertCooldown)
        {
            return false; // Still in cooldown period
        }

        // Check random chance
        bool l_shouldAlert = UnityEngine.Random.value < alertChanceWhenNoLoS;

        if (l_shouldAlert)
        {
            m_lastAlertTime = Time.time; // Update last alert time
        }

        return l_shouldAlert;
    }

    /// <summary>
    /// Determine if civilian should return to idle based on safety conditions and timing
    /// </summary>
    private bool ShouldReturnToIdle()
    {
        Debug.Log("=== ShouldReturnToIdle() START ===");

        // CivilianDecisionTreeRunner.cs  (dentro de ShouldReturnToIdle, al inicio)
        if (IsCurrentlyFleeing() && m_civilian != null)
        {
            // Si usamos grafo y todavía no llegamos al nodo seguro, seguir huyendo
            // (evita cortar por SafeDistance)
            if (m_civilian.HasFleeGraph && m_civilian.HasFleePath && !m_civilian.FleePathReachedEnd())
            {
                // resetear cualquier safe timer local de DT, seguimos huyendo
                m_safeTimer = 0f;
                Debug.Log("FLEE A*: aún no llegué al nodo seguro → continuar huyendo (ignorar SafeDistance)");
                return false;
            }
        }

        if (m_civilian == null || m_civilian.Player == null)
        {
            Debug.Log("CIVILIAN OR PLAYER IS NULL - RETURNING TRUE");
            return true;
        }

        float l_distanceToPlayer = Vector3.Distance(m_civilian.transform.position, m_civilian.Player.position);
        bool l_isSafeDistance = l_distanceToPlayer >= m_civilian.SafeDistance;
        bool l_hasLoS = m_civilian.HasLoS();

        Debug.Log($"Distance: {l_distanceToPlayer:F1}, SafeDistance: {m_civilian.SafeDistance}, HasLoS: {l_hasLoS}");

        // SafeDistance is only for STOPPING fleeing, not for starting it
        // If player is in safe area (beyond SafeDistance), return to idle immediately
        if (l_isSafeDistance)
        {
            Debug.Log($"PLAYER IN SAFE AREA - STOP FLEEING");
            m_safeTimer = 0f;
            return true;
        }

        // If still close but no LoS, use grace timer (for when we lost sight during chase)
        if (!l_hasLoS)
        {
            m_safeTimer += evaluationInterval;
            bool l_shouldReturn = m_safeTimer >= m_civilian.SafeTime;

            Debug.Log($"CLOSE BUT NO LoS - GRACE TIMER: Timer={m_safeTimer:F2}/{m_civilian.SafeTime}, ShouldReturn={l_shouldReturn}");
            return l_shouldReturn;
        }

        // Player is close and visible - keep fleeing
        Debug.Log($"PLAYER CLOSE AND VISIBLE - CONTINUE FLEEING");
        m_safeTimer = 0f;
        return false;
    }

    /// <summary>
    /// Check if civilian is currently in a fleeing state
    /// </summary>
    private bool IsCurrentlyFleeing()
    {
        string l_currentState = GetCurrentFsmStateName();
        return l_currentState == "S_CivFlee" || l_currentState == "S_CivEvade";
    }

    /// <summary>
    /// Check if civilian is currently pursuing/attacking
    /// </summary>
    private bool IsCurrentlyPursuing()
    {
        string l_currentState = GetCurrentFsmStateName();
        return l_currentState == "S_CivPersuit" || l_currentState == "S_CivAttack";
    }

    /// <summary>
    /// Check if civilian should stay committed to pursuit despite losing LoS
    /// </summary>
    private bool IsWithinPursuitCommitment()
    {
        // Give more commitment time than the lose sight grace (attackLoseSightGrace is 0.3s)
        float l_commitmentTime = m_civilian.AttackLoseSightGrace * 3f; // 0.9s commitment
        float l_timeSincePursuitStart = Time.time - m_pursuitStartTime;

        // Always commit for at least 1 second after starting pursuit
        if (l_timeSincePursuitStart < 1f)
        {
            Debug.Log($"PURSUIT COMMITMENT: Within minimum commitment time ({l_timeSincePursuitStart:F2}s < 1.0s)");
            return true;
        }

        // Then use INVISIBLE lose sight timer with extended grace (separate from visible timer)
        if (!m_civilian.HasLoS())
        {
            m_pursuitLoseSightTimerInvisible += evaluationInterval;
        }
        else
        {
            m_pursuitLoseSightTimerInvisible = 0f; // Reset invisible timer when LoS recovered
        }

        bool l_stillCommitted = m_pursuitLoseSightTimerInvisible < l_commitmentTime;

        Debug.Log($"PURSUIT COMMITMENT: Invisible LoS timer={m_pursuitLoseSightTimerInvisible:F2}s < {l_commitmentTime:F2}s = {l_stillCommitted}");
        return l_stillCommitted;
    }

    /// <summary>
    /// Check if evade time has elapsed and should transition to flee
    /// </summary>
    private bool ShouldTransitionFromEvade()
    {
        return (Time.time - m_evadeStartTime) >= m_civilian.EvadeTime;
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
            bool l_hasLoS = m_civilian.HasLoS();
            string l_reason = l_hasLoS ? "Player visible, roulette chose escape" : "No LoS, continuing flee behavior";
            MyLogger.LogInfo($"DT → Flee ({l_reason})");
        }
    }

    /// <summary>
    /// Suggest attacking the player (dedicated attack state)
    /// </summary>
    private void SuggestAttack()
    {
        // Start attack cycle if not already in one
        if (!m_isInAttackCycle)
        {
            StartAttackCycle();
            
            if (debugDT)
                MyLogger.LogInfo($"DT → Attack (Starting attack cycle in melee range)");
        }
        else if (debugDT)
        {
            float l_totalAttackDuration = m_civilian.AttackWindup + m_civilian.AttackHitWin + m_civilian.AttackRecover;
            float l_timeSinceAttackStart = Time.time - m_attackCycleStartTime;
            MyLogger.LogInfo($"DT → Attack (Continuing attack cycle: {l_timeSinceAttackStart:F2}s / {l_totalAttackDuration:F2}s)");
        }

        SetSuggestion("Attack");
    }

    /// <summary>
    /// Suggest pursuing/attacking the player
    /// </summary>
    private void SuggestPursue()
    {
        // Initialize pursuit timing if entering pursuit for first time
        if (m_currentSuggestion != "Pursue")
        {
            m_pursuitStartTime = Time.time;
            m_pursuitLoseSightTimerInvisible = 0f; // Reset invisible lose sight timer when starting pursuit

            if (debugDT)
                MyLogger.LogInfo($"DT → Pursue (Starting pursuit - commitment time initialized)");
        }
        else
        {
            // Reset invisible lose sight timer if we can see player
            if (m_civilian.HasLoS())
            {
                m_pursuitLoseSightTimerInvisible = 0f;
            }
        }

        SetSuggestion("Pursue");

        if (debugDT && m_currentSuggestion == "Pursue")
            MyLogger.LogInfo($"DT → Pursue (Continuing pursuit)");
    }

    /// <summary>
    /// Suggest evading the player (short burst movement)
    /// </summary>
    private void SuggestEvade()
    {
        // If we're already evading, check if time elapsed
        if (m_currentSuggestion == "Evade" && ShouldTransitionFromEvade())
        {
            SuggestFlee();
            return;
        }

        // Set evade start time if entering evade
        if (m_currentSuggestion != "Evade")
        {
            m_evadeStartTime = Time.time;
        }

        SetSuggestion("Evade");

        if (debugDT)
            MyLogger.LogInfo($"DT → Evade (Player visible, evading for {m_civilian.EvadeTime}s)");
    }

    /// <summary>
    /// Suggest idle behavior
    /// </summary>
    private void SuggestIdle()
    {
        SetSuggestion("Idle");

        if (debugDT)
            MyLogger.LogInfo($"DT → Idle (Safe conditions met)");
    }

    /// <summary>
    /// Suggest alerting other NPCs and then resuming
    /// </summary>
    private void SuggestAlert()
    {
        // Trigger global alert only if it's not already set
        var l_currentAlert = BlackboardService.GetValue<bool>(BlackboardKeys.GLOBAL_ALERT);
        if (!l_currentAlert)
        {
            BlackboardService.SetValue(BlackboardKeys.GLOBAL_ALERT, true);
            
            if (debugDT)
                MyLogger.LogInfo($"DT → Alert (No LoS, setting GLOBAL_ALERT to true, cooldown: {alertCooldown}s)");
        }
        else if (debugDT)
        {
            MyLogger.LogInfo($"DT → Alert (No LoS, GLOBAL_ALERT already true, skipping)");
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
            MyLogger.LogInfo($"DT → Resume (No LoS, continuing normal behavior)");
    }

    /// <summary>
    /// Set the current suggestion and track changes
    /// </summary>
    private void SetSuggestion(string p_suggestion)
    {
        if (m_currentSuggestion != p_suggestion)
        {
            m_lastSuggestion = m_currentSuggestion;
            m_currentSuggestion = p_suggestion;
        }
    }

    #endregion

    #region Evaluation Loop

    /// <summary>
    /// Start the decision tree evaluation loop
    /// </summary>
    private void StartEvaluationLoop()
    {
        if (m_evaluationCoroutine == null)
        {
            m_evaluationCoroutine = StartCoroutine(EvaluationLoop());
        }
    }

    /// <summary>
    /// Stop the decision tree evaluation loop
    /// </summary>
    private void StopEvaluationLoop()
    {
        if (m_evaluationCoroutine != null)
        {
            StopCoroutine(m_evaluationCoroutine);
            m_evaluationCoroutine = null;
        }
    }

    /// <summary>
    /// Main evaluation loop that runs the decision tree at intervals
    /// </summary>
    private IEnumerator EvaluationLoop()
    {
        var l_waitTime = new WaitForSeconds(evaluationInterval);

        while (enabled && gameObject.activeInHierarchy)
        {
            yield return l_waitTime;

            // Skip evaluation if civilian is not alive
            if (!m_civilian.IsActive)
                continue;

            // Check if stance lock should be broken due to context changes
            CheckStanceLockBreakers();

            // Run the decision tree
            string l_previousSuggestion = m_currentSuggestion;
            
            if (m_root != null)
            {
                m_root.Execute();
            }

            // Process suggestion if it changed OR if FSM is not in the suggested state
            bool l_suggestionChanged = m_currentSuggestion != l_previousSuggestion;
            bool l_needsStateSync = !string.IsNullOrEmpty(m_currentSuggestion) && !IsCurrentFsmStateMatchingSuggestion();
            
            if ((l_suggestionChanged || l_needsStateSync) && !string.IsNullOrEmpty(m_currentSuggestion))
            {
                if (debugDT && l_needsStateSync && !l_suggestionChanged)
                    MyLogger.LogInfo($"DT re-processing suggestion '{m_currentSuggestion}' (FSM state sync needed)");
                    
                ProcessSuggestion(m_currentSuggestion);
            }

            m_lastEvaluationTime = Time.time;
        }
    }

    #endregion

    #region FSM State Checking

    /// <summary>
    /// Check if the current FSM state matches the current DT suggestion
    /// </summary>
    private bool IsCurrentFsmStateMatchingSuggestion()
    {
        if (m_civilian == null || string.IsNullOrEmpty(m_currentSuggestion))
            return true; // Assume match if we can't determine

        // Map the suggestion to the expected FSM state name
        string l_expectedStateName = MapSuggestionToFsmStateName(m_currentSuggestion);
        
        // Get current FSM state name
        string l_currentStateName = GetCurrentFsmStateName();
        
        if (debugDT && !string.IsNullOrEmpty(l_currentStateName))
        {
            // Only log occasionally to avoid spam, or when there's a mismatch
            bool l_isMatch = string.Equals(l_currentStateName, l_expectedStateName, System.StringComparison.OrdinalIgnoreCase);
            if (!l_isMatch)
            {
                MyLogger.LogInfo($"FSM state mismatch - Current: '{l_currentStateName}', Expected: '{l_expectedStateName}'");
            }
        }

        return string.Equals(l_currentStateName, l_expectedStateName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Map DT suggestion to FSM state name (same logic as in Civilian)
    /// </summary>
    private string MapSuggestionToFsmStateName(string p_suggestion)
    {
        switch (p_suggestion.ToLower())
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
                return p_suggestion;
        }
    }

    /// <summary>
    /// Get the current FSM state name
    /// </summary>
    private string GetCurrentFsmStateName()
    {
        // Use reflection to access the civilian's FSM state
        if (m_civilian != null)
        {
            try
            {
                var l_civilianType = m_civilian.GetType();
                var l_stateMachineField = l_civilianType.GetField("stateMachine", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (l_stateMachineField?.GetValue(m_civilian) is Scripts.FSM.Base.StateMachine.StateMachine l_stateMachine)
                {
                    var l_currentState = l_stateMachine.GetCurrentState();
                    return l_currentState?.State?.StateName ?? "";
                }
            }
            catch (System.Exception l_e)
            {
                if (debugDT)
                    MyLogger.LogWarning($"Failed to get FSM state name: {l_e.Message}");
            }
        }
        
        return "";
    }

    #endregion

    #region FSM Integration

    /// <summary>
    /// Process a suggestion from the decision tree by requesting FSM state changes
    /// </summary>
    private void ProcessSuggestion(string p_suggestion)
    {
        // Skip processing if we're in a non-interruptible attack cycle
        if (IsInNonInterruptibleAttackCycle() && p_suggestion != "Attack")
        {
            if (debugDT)
                MyLogger.LogInfo($"Ignoring suggestion '{p_suggestion}' - in non-interruptible attack cycle");
            return;
        }

        // Note: The Civilian FSM should handle the actual state transitions
        // This is just a bridge to communicate the decision tree's suggestion

        switch (p_suggestion)
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
                RequestStateChange(p_suggestion);
                break;
        }
    }

    /// <summary>
    /// Request a state change from the Civilian's FSM
    /// </summary>
    private void RequestStateChange(string p_stateName)
    {
        // Use the Civilian's RequestStateChange method to bridge to FSM
        if (m_civilian != null)
        {
            m_civilian.RequestStateChange(p_stateName);
        }
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Called by Civilian FSM when an attack cycle completes
    /// </summary>
    public void OnAttackCycleComplete()
    {
        if (m_isInAttackCycle)
        {
            EndAttackCycle(true); // End with post-hit flee
            
            if (debugDT)
                MyLogger.LogInfo($"Attack cycle completed - starting post-hit flee phase");
        }
    }

    /// <summary>
    /// Called by Civilian FSM when melee damage is dealt
    /// </summary>
    public void OnMeleeDamageDealt()
    {
        if (debugDT)
            MyLogger.LogInfo($"Melee damage dealt - attack cycle will complete soon");
        
        // The attack cycle will complete naturally and trigger post-hit flee
    }

    /// <summary>
    /// Manually trigger a decision tree evaluation (useful for testing)
    /// </summary>
    [ContextMenu("Evaluate Decision Tree")]
    public void EvaluateDecisionTree()
    {
        if (m_root != null)
        {
            string l_previousSuggestion = m_currentSuggestion;
            m_root.Execute();
            
            if (debugDT)
                MyLogger.LogInfo($"Manual DT evaluation: {m_currentSuggestion}");
                
            if (m_currentSuggestion != l_previousSuggestion)
            {
                ProcessSuggestion(m_currentSuggestion);
            }
        }
    }

    /// <summary>
    /// Get current decision tree status for debugging
    /// </summary>
    public string GetStatus()
    {
        return $"Current: {m_currentSuggestion}, Last: {m_lastSuggestion}, LastEval: {Time.time - m_lastEvaluationTime:F2}s ago";
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
        Debug.Log($"Time Since Last Alert: {(m_lastAlertTime < 0 ? "Never" : (Time.time - m_lastAlertTime).ToString("F2") + "s")}");
        Debug.Log($"Resume Suggestion: {resumeSuggestion}");
        Debug.Log($"Current Suggestion: {m_currentSuggestion}");
        Debug.Log($"Last Suggestion: {m_lastSuggestion}");
        Debug.Log($"Current FSM State: {GetCurrentFsmStateName()}");
        Debug.Log($"Expected FSM State: {MapSuggestionToFsmStateName(m_currentSuggestion)}");
        Debug.Log($"FSM State Matches: {IsCurrentFsmStateMatchingSuggestion()}");
        Debug.Log($"Last Evaluation: {Time.time - m_lastEvaluationTime:F2}s ago");
        
        // Stance Lock Status
        Debug.Log($"--- STANCE LOCK ---");
        Debug.Log($"Has Active Lock: {m_hasActiveStanceLock}");
        Debug.Log($"Current Stance: {(m_currentStance ? "ATTACK" : "ESCAPE")}");
        Debug.Log($"Lock Expires In: {(m_hasActiveStanceLock ? (m_stanceLockUntil - Time.time).ToString("F2") + "s" : "N/A")}");
        
        // Attack Cycle Status
        Debug.Log($"--- ATTACK CYCLE ---");
        Debug.Log($"Is In Attack Cycle: {m_isInAttackCycle}");
        Debug.Log($"Attack Start Time: {(m_isInAttackCycle ? (Time.time - m_attackCycleStartTime).ToString("F2") + "s ago" : "N/A")}");
        Debug.Log($"Post-Hit Flee Active: {m_postHitFleeActive}");
        Debug.Log($"Post-Hit Flee Time Left: {(m_postHitFleeActive ? (postHitFleeTime - (Time.time - m_postHitFleeStartTime)).ToString("F2") + "s" : "N/A")}");
        
        // Timers Status
        Debug.Log($"--- TIMERS ---");
        Debug.Log($"Pursuit LoS Timer (Visible): {m_pursuitLoseSightTimerVisible:F2}s");
        Debug.Log($"Pursuit LoS Timer (Invisible): {m_pursuitLoseSightTimerInvisible:F2}s");
        Debug.Log($"Safe Timer: {m_safeTimer:F2}s");
        Debug.Log($"Pursuit Start Time: {(m_pursuitStartTime > 0 ? (Time.time - m_pursuitStartTime).ToString("F2") + "s ago" : "Never")}");
        
        // General Status
        Debug.Log($"--- GENERAL ---");
        Debug.Log($"Debug Enabled: {debugDT}");
        Debug.Log($"Can See Player: {(m_civilian != null ? m_civilian.HasLoS() : "N/A")}");
        Debug.Log($"Can Attack: {(m_civilian != null ? m_civilian.CanAttack : "N/A")}");
        Debug.Log($"In Melee Range: {(m_civilian != null ? m_civilian.IsPlayerInMeleeRange() : "N/A")}");
        Debug.Log($"Distance to Player: {(m_civilian != null && m_civilian.Player != null ? Vector3.Distance(m_civilian.transform.position, m_civilian.Player.position).ToString("F2") : "N/A")}");
        
        if (BlackboardService != null)
        {
            Debug.Log($"Global Alert: {BlackboardService.GetValue<bool>(BlackboardKeys.GLOBAL_ALERT)}");
        }
        
        Debug.Log("====================================");
    }

    #endregion
}