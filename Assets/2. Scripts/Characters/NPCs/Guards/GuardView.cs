using UnityEngine;

public class GuardView : NPCView, IUpdatable
{
    public bool IsActive => gameObject.activeInHierarchy && guardController != null && guardController.IsAlive;

    private GuardController guardController;
    private GuardModel guardModel;

    // Animation state tracking
    private string currentAnimationState = "";
    private float animationBlendSpeed = 5f;

    // Visual feedback components
    private Material originalMaterial;
    private Renderer guardRenderer;

    // Detection visual feedback
    private Color alertColor = Color.yellow;
    private Color combatColor = Color.red;
    private Color normalColor = Color.white;

    protected override void Awake()
    {
        base.Awake();
        guardController = GetComponent<GuardController>();
        guardModel = guardController?.Model as GuardModel;

        // Get renderer for visual feedback
        guardRenderer = GetComponent<Renderer>();
        if (guardRenderer != null)
        {
            originalMaterial = guardRenderer.material;
        }

        RegisterWithUpdateManager();
    }

    private void RegisterWithUpdateManager()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        if (updateManager != null)
        {
            updateManager.RegisterUpdatable(this);
            Logger.LogInfo($"GuardView {gameObject.name}: Registered with UpdateManager");
        }
        else
        {
            Logger.LogWarning($"GuardView {gameObject.name}: UpdateManager not available yet, will retry");
            StartCoroutine(WaitForUpdateManagerRegistration());
        }
    }

    private System.Collections.IEnumerator WaitForUpdateManagerRegistration()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            var updateManager = ServiceLocator.Get<UpdateManager>();
            if (updateManager != null)
            {
                updateManager.RegisterUpdatable(this);
                Logger.LogInfo($"GuardView {gameObject.name}: Successfully registered with UpdateManager after waiting");
                break;
            }
        }
    }

    public void OnUpdate(float deltaTime)
    {
        UpdateAdvancedVisuals();
        UpdateAnimationBasedOnState();
        UpdateDebugVisuals();
    }

    private void OnDestroy()
    {
        // Unregister from UpdateManager
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
    }

    protected override float GetCurrentMoveSpeed()
    {
        if (guardController?.GuardData != null && guardModel != null)
        {
            // Use contextual speed from model
            return guardModel.GetContextualSpeed();
        }

        // Fallback logic
        if (guardController?.GuardData != null)
        {
            if (guardController.Model?.RuntimeState.isPlayerVisible == true)
            {
                return guardController.GuardData.chaseSpeed;
            }
            return guardController.GuardData.patrolSpeed;
        }

        return guardController?.Model?.RuntimeState.isPlayerVisible == true ? 4f : 2f;
    }

    protected override float GetRotationSpeed()
    {
        if (guardController?.GuardData != null)
        {
            // Use base rotation speed from GuardData
            return guardController.GuardData.baseRotationSpeed;
        }
        return 5f;
    }

    /// <summary>
    /// Update advanced visual feedback based on AI state
    /// </summary>
    private void UpdateAdvancedVisuals()
    {
        if (guardModel == null || guardRenderer == null) return;

        // Update material color based on AI state
        Color targetColor = normalColor;

        if (guardModel.IsInCombat())
        {
            targetColor = combatColor;
        }
        else if (guardModel.IsAlert())
        {
            targetColor = alertColor;
        }

        // Smooth color transition
        if (guardRenderer.material.color != targetColor)
        {
            guardRenderer.material.color = Color.Lerp(guardRenderer.material.color, targetColor, Time.deltaTime * animationBlendSpeed);
        }
    }

    /// <summary>
    /// Update animation state based on current AI and movement status
    /// </summary>
    private void UpdateAnimationBasedOnState()
    {
        if (guardModel == null) return;

        string newAnimationState = GetAnimationStateBasedOnAI();

        if (newAnimationState != currentAnimationState)
        {
            TransitionToAnimation(newAnimationState);
            currentAnimationState = newAnimationState;
        }
    }

    /// <summary>
    /// Determine animation state based on AI and movement status
    /// </summary>
    private string GetAnimationStateBasedOnAI()
    {
        // Priority order: Combat > Alert > Movement states
        if (guardModel.IsInCombat())
        {
            return "Combat";
        }

        if (guardModel.IsAlert())
        {
            if (guardModel.IsSearching())
            {
                return "Search";
            }
            return "Alert";
        }

        // Movement-based states
        MovementStatus movementStatus = guardModel.GetMovementStatus();

        return movementStatus switch
        {
            MovementStatus.Patrolling => "Patrol",
            MovementStatus.Moving => "Walk",
            MovementStatus.Fleeing => "Flee",
            MovementStatus.Following => "Chase",
            MovementStatus.Idle => "Idle",
            _ => "Idle"
        };
    }

    /// <summary>
    /// Transition to a new animation state
    /// </summary>
    private void TransitionToAnimation(string animationState)
    {
        switch (animationState)
        {
            case "Combat":
                PlayAttackAnimation();
                break;
            case "Alert":
                PlayAlertAnimation();
                break;
            case "Search":
                PlaySearchAnimation();
                break;
            case "Patrol":
                PlayPatrolAnimation();
                break;
            case "Walk":
                PlayWalkAnimation();
                break;
            case "Chase":
                PlayChaseAnimation();
                break;
            case "Flee":
                PlayFleeAnimation();
                break;
            case "Idle":
                PlayIdleAnimation();
                break;
            default:
                PlayIdleAnimation();
                break;
        }
    }

    #region Animation Methods

    public void PlayAttackAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing attack animation");
        // TODO: Implement actual animation trigger
        // animator?.SetTrigger("Attack");
    }

    public void PlayChaseAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing chase animation");
        // TODO: Implement actual animation trigger
        // animator?.SetBool("IsChasing", true);
    }

    public void PlayPatrolAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing patrol animation");
        // TODO: Implement actual animation trigger
        // animator?.SetBool("IsPatrolling", true);
    }

    public void PlaySearchAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing search animation");
        // TODO: Implement actual animation trigger
        // animator?.SetBool("IsSearching", true);
    }

    public void PlayIdleAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing idle animation");
        // TODO: Implement actual animation trigger
        // animator?.SetBool("IsIdle", true);
    }

    public void PlayAlertAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing alert animation");
        // TODO: Implement actual animation trigger
        // animator?.SetBool("IsAlert", true);
    }

    public void PlayWalkAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing walk animation");
        // TODO: Implement actual animation trigger
        // animator?.SetBool("IsWalking", true);
    }

    public void PlayFleeAnimation()
    {
        Logger.LogDebug($"{gameObject.name}: Playing flee animation");
        // TODO: Implement actual animation trigger
        // animator?.SetBool("IsFleeing", true);
    }

    #endregion

    #region Advanced Visual Feedback

    /// <summary>
    /// Show detection indicator (could be UI element or particle effect)
    /// </summary>
    public void ShowDetectionIndicator(PlayerDetectionLevel level)
    {
        switch (level)
        {
            case PlayerDetectionLevel.Peripheral:
                Logger.LogDebug($"{gameObject.name}: Showing peripheral detection indicator");
                // TODO: Show small indicator
                break;
            case PlayerDetectionLevel.Partial:
                Logger.LogDebug($"{gameObject.name}: Showing partial detection indicator");
                // TODO: Show medium indicator
                break;
            case PlayerDetectionLevel.Clear:
                Logger.LogDebug($"{gameObject.name}: Showing clear detection indicator");
                // TODO: Show strong indicator
                break;
            case PlayerDetectionLevel.Immediate:
                Logger.LogDebug($"{gameObject.name}: Showing immediate detection indicator");
                // TODO: Show critical indicator
                break;
        }
    }

    /// <summary>
    /// Show threat level indicator
    /// </summary>
    public void ShowThreatLevelIndicator(float threatLevel)
    {
        if (threatLevel > 0.7f)
        {
            Logger.LogDebug($"{gameObject.name}: Showing high threat indicator");
            // TODO: Red indicator
        }
        else if (threatLevel > 0.4f)
        {
            Logger.LogDebug($"{gameObject.name}: Showing medium threat indicator");
            // TODO: Yellow indicator
        }
        else if (threatLevel > 0.1f)
        {
            Logger.LogDebug($"{gameObject.name}: Showing low threat indicator");
            // TODO: Green indicator
        }
    }

    /// <summary>
    /// Show movement path visualization (for debugging)
    /// </summary>
    public void ShowMovementPath(Vector3[] waypoints)
    {
        // TODO: Draw debug lines between waypoints
        if (waypoints != null && waypoints.Length > 1)
        {
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                Debug.DrawLine(waypoints[i], waypoints[i + 1], Color.blue, 1f);
            }
        }
    }

    /// <summary>
    /// Show field of view visualization (for debugging)
    /// </summary>
    public void ShowFieldOfView()
    {
        if (guardController?.GuardData == null) return;

        float fov = guardController.GuardData.fieldOfView;
        float range = guardController.GuardData.detectionRange;

        Vector3 leftBoundary = Quaternion.Euler(0, -fov / 2, 0) * transform.forward * range;
        Vector3 rightBoundary = Quaternion.Euler(0, fov / 2, 0) * transform.forward * range;

        Debug.DrawRay(transform.position, leftBoundary, Color.green, 0.1f);
        Debug.DrawRay(transform.position, rightBoundary, Color.green, 0.1f);
        Debug.DrawRay(transform.position, transform.forward * range, Color.yellow, 0.1f);
    }

    #endregion

    #region Movement Visualization

    /// <summary>
    /// Update movement visual effects based on steering
    /// </summary>
    public void UpdateMovementEffects(Vector3 velocity, MovementMode mode)
    {
        // TODO: Add particle effects or trail based on movement
        float speed = velocity.magnitude;

        if (speed > guardController.GuardData.chaseSpeed * 0.8f)
        {
            // Show fast movement effects
            Logger.LogDebug($"{gameObject.name}: Fast movement effects (speed: {speed:F1})");
        }
        else if (speed > guardController.GuardData.patrolSpeed * 0.8f)
        {
            // Show normal movement effects
            Logger.LogDebug($"{gameObject.name}: Normal movement effects (speed: {speed:F1})");
        }

        // Mode-specific effects
        switch (mode)
        {
            case MovementMode.Sneak:
                // Subtle movement effects
                break;
            case MovementMode.Sprint:
                // Intense movement effects
                break;
        }
    }

    #endregion

    #region Debug and Development Methods

    /// <summary>
    /// Update debug visuals (only in development builds)
    /// </summary>
    private void UpdateDebugVisuals()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (guardController?.GuardData != null)
        {
            ShowFieldOfView();

            // Show movement path if patrolling
            if (guardModel?.IsPatrolling() == true && guardController.GuardData.patrolPoints != null)
            {
                Vector3[] patrolPositions = new Vector3[guardController.GuardData.patrolPoints.Length];
                for (int i = 0; i < guardController.GuardData.patrolPoints.Length; i++)
                {
                    if (guardController.GuardData.patrolPoints[i] != null)
                    {
                        patrolPositions[i] = guardController.GuardData.patrolPoints[i].position;
                    }
                }
                ShowMovementPath(patrolPositions);
            }

            // Show detection and threat indicators
            if (guardModel != null)
            {
                var detectionResult = guardModel.GetCurrentDetectionResult();
                if (detectionResult.level > PlayerDetectionLevel.None)
                {
                    ShowDetectionIndicator(detectionResult.level);
                }

                float threatLevel = guardModel.GetThreatLevel();
                if (threatLevel > 0.1f)
                {
                    ShowThreatLevelIndicator(threatLevel);
                }
            }
        }
        #endif
    }

    /// <summary>
    /// Sync animation state with model data
    /// </summary>
    private void SyncWithModel()
    {
        if (guardModel == null) return;

        // Update movement effects based on current velocity and mode
        Vector3 currentVelocity = guardModel.GetCurrentVelocity();
        MovementMode currentMode = guardModel.GetMovementMode();

        if (currentVelocity.magnitude > 0.1f)
        {
            UpdateMovementEffects(currentVelocity, currentMode);
        }
    }

    #endregion
}