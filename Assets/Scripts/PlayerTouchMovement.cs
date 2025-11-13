using System;
using System.Collections;
using _2._Scripts.UI.MainMenu;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch;

public class PlayerTouchMovement : MonoBehaviour
{
    // FSM States
    public enum PlayerInputState
    {
        Idle,                   // No input active
        Moving,                 // Left joystick only
        Aiming,                 // Right finger pressed, waiting to determine action
        ShootingBurst,          // Dragging right joystick (burst mode)
        ShootingContinuous,     // Holding right finger without drag (auto-fire)
        MovingAndAiming,        // Moving + right finger pressed
        MovingAndShootingBurst, // Moving + burst shooting
        MovingAndShootingContinuous // Moving + continuous shooting
    }

    [Header("FSM State (Read-Only)")]
    [SerializeField] private PlayerInputState currentState = PlayerInputState.Idle;
    [SerializeField] private PlayerInputState previousState = PlayerInputState.Idle;

    [Header("Joystick Settings")]
    [SerializeField]
    private Vector2 JoystickSize = new Vector2(300, 300);
    [SerializeField]
    private FloatingJoystick Joystick;
    [SerializeField]
    private NavMeshAgent Player;
    
    [SerializeField] private string objectivesPanelName = "Objectives";
    [SerializeField] private GameObject dragTutorial;
    [SerializeField] private GameObject shootTutorial;
    [SerializeField] private PanelsController panelsController;
    
    private Finger MovementFinger;
    private Vector2 MovementAmount;
    
    [SerializeField]
    private MainCharacter mainCharacter;

    private Finger TapFinger;
    
    bool dragClosed, shootClosed, objectivesShown;
    
    private Finger ShootingFinger;
    private Vector2 ShootingStartPosition;
    private Vector2 ShootingAmount;
    private bool isContinuousShooting = false;
    private bool hasBurstFired = false;
    private Coroutine continuousShootingCoroutine;

    [Header("Shooting Settings")]
    [SerializeField] private FloatingJoystick ShootingJoystick;
    [SerializeField] private float continuousShootingInterval = 0.3f;
    [SerializeField] private int burstShotCount = 5;
    [SerializeField] private float burstShotInterval = 0.25f;
    [SerializeField]
    private float continuousShootingDelay = 0.3f;
    private Coroutine delayedContinuousCoroutine;

    private void Awake()
    {
        dragClosed = shootClosed = objectivesShown = false;
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        ETouch.Touch.onFingerDown += HandleFingerDown;
        ETouch.Touch.onFingerUp += HandleLoseFinger;
        ETouch.Touch.onFingerMove += HandleFingerMove;
    }

    private void OnDisable()
    {
        ETouch.Touch.onFingerDown -= HandleFingerDown;
        ETouch.Touch.onFingerUp -= HandleLoseFinger;
        ETouch.Touch.onFingerMove -= HandleFingerMove;
        EnhancedTouchSupport.Disable();
    }

    private void HandleFingerMove(Finger MovedFinger)
    {
        if (MovedFinger == MovementFinger)
        {
            Vector2 knobPosition;
            float maxMovement = JoystickSize.x / 2f;
            ETouch.Touch currentTouch = MovedFinger.currentTouch;
            if (!IsValidVector2(currentTouch.screenPosition))
            {
                return;
            }

            if (Vector2.Distance(
                    currentTouch.screenPosition,
                    Joystick.RectTransform.anchoredPosition
                ) > maxMovement)
            {
                knobPosition = (
                    currentTouch.screenPosition - Joystick.RectTransform.anchoredPosition
                    ).normalized
                    * maxMovement;
            }
            else
            {
                knobPosition = currentTouch.screenPosition - Joystick.RectTransform.anchoredPosition;
            }

            Joystick.Knob.anchoredPosition = knobPosition;
            MovementAmount = knobPosition / maxMovement;

            if (!dragClosed && dragTutorial && dragTutorial.activeSelf)
                StartCoroutine(CloseAfter(dragTutorial, 2f, () => { dragClosed = true; TryShowObjectives(); }));
        }
        else if (MovedFinger == ShootingFinger)
        {
            // Handle shooting joystick movement
            Vector2 knobPosition;
            float maxMovement = JoystickSize.x / 2f;
            ETouch.Touch currentTouch = MovedFinger.currentTouch;

            if (!IsValidVector2(currentTouch.screenPosition))
            {
                return;
            }

            // Calculate knob position relative to start position
            Vector2 dragVector = currentTouch.screenPosition - ShootingStartPosition;

            if (dragVector.magnitude > maxMovement)
            {
                knobPosition = dragVector.normalized * maxMovement;
            }
            else
            {
                knobPosition = dragVector;
            }

            // Clamp knob position to stay within joystick bounds
            knobPosition = Vector2.ClampMagnitude(knobPosition, maxMovement);

            ShootingJoystick.Knob.anchoredPosition = knobPosition;
            ShootingAmount = knobPosition / maxMovement;

            // If we have significant drag, transition to burst shooting
            if (dragVector.magnitude > 30f && mainCharacter != null)
            {
                // Transition to burst shooting state
                if (currentState == PlayerInputState.Aiming)
                {
                    TransitionToState(PlayerInputState.ShootingBurst);
                }
                else if (currentState == PlayerInputState.MovingAndAiming)
                {
                    TransitionToState(PlayerInputState.MovingAndShootingBurst);
                }
                else if (currentState == PlayerInputState.ShootingContinuous)
                {
                    TransitionToState(PlayerInputState.ShootingBurst);
                }
                else if (currentState == PlayerInputState.MovingAndShootingContinuous)
                {
                    TransitionToState(PlayerInputState.MovingAndShootingBurst);
                }

                // Fire burst if we haven't already
                if (!hasBurstFired)
                {
                    Vector3 shootDirection = new Vector3(dragVector.normalized.x, 0, dragVector.normalized.y);
                    StartCoroutine(ShootBurst(shootDirection));
                    hasBurstFired = true;
                }
            }
        }
    }

    private void HandleLoseFinger(Finger LostFinger)
    {
        if (LostFinger == MovementFinger)
        {
            MovementFinger = null;
            Joystick.Knob.anchoredPosition = Vector2.zero;
            Joystick.gameObject.SetActive(false);
            MovementAmount = Vector2.zero;

            // Transition based on current state
            if (currentState == PlayerInputState.Moving)
            {
                TransitionToState(PlayerInputState.Idle);
            }
            else if (currentState == PlayerInputState.MovingAndAiming)
            {
                TransitionToState(PlayerInputState.Aiming);
            }
            else if (currentState == PlayerInputState.MovingAndShootingBurst)
            {
                TransitionToState(PlayerInputState.ShootingBurst);
            }
            else if (currentState == PlayerInputState.MovingAndShootingContinuous)
            {
                TransitionToState(PlayerInputState.ShootingContinuous);
            }
        }
        else if (LostFinger == ShootingFinger)
        {
            // If in aiming state and no burst/continuous happened, fire single shot
            bool shouldFireSingleShot = (currentState == PlayerInputState.Aiming ||
                                         currentState == PlayerInputState.MovingAndAiming) &&
                                        !hasBurstFired && !isContinuousShooting;

            if (shouldFireSingleShot && mainCharacter != null)
            {
                Vector3 shootDirection = mainCharacter.transform.forward;
                mainCharacter.Shoot(shootDirection);
            }

            // Reset shooting joystick
            ShootingJoystick.Knob.anchoredPosition = Vector2.zero;
            ShootingJoystick.gameObject.SetActive(false);
            ShootingFinger = null;
            ShootingAmount = Vector2.zero;
            hasBurstFired = false;

            // Transition based on current state
            if (currentState == PlayerInputState.Aiming ||
                currentState == PlayerInputState.ShootingBurst ||
                currentState == PlayerInputState.ShootingContinuous)
            {
                TransitionToState(PlayerInputState.Idle);
            }
            else if (currentState == PlayerInputState.MovingAndAiming ||
                     currentState == PlayerInputState.MovingAndShootingBurst ||
                     currentState == PlayerInputState.MovingAndShootingContinuous)
            {
                TransitionToState(PlayerInputState.Moving);
            }

            if (!shootClosed && shootTutorial && shootTutorial.activeSelf)
                StartCoroutine(CloseAfter(shootTutorial, 0f, () => { shootClosed = true; TryShowObjectives(); }));
        }
    }

    private void HandleFingerDown(Finger TouchedFinger)
    {
        float halfScreenWidth = Screen.width / 2f;

        if (!TryGetScreenPosition(TouchedFinger, out Vector2 screenPosition))
        {
            return;
        }

        // Left side of screen - Movement
        if (MovementFinger == null && screenPosition.x <= halfScreenWidth)
        {
            MovementFinger = TouchedFinger;
            MovementAmount = Vector2.zero;
            Joystick.gameObject.SetActive(true);
            Joystick.RectTransform.sizeDelta = JoystickSize;
            Joystick.RectTransform.anchoredPosition = ClampStartPosition(screenPosition);

            // Determine new state based on current state
            if (currentState == PlayerInputState.Idle)
            {
                TransitionToState(PlayerInputState.Moving);
            }
            else if (currentState == PlayerInputState.Aiming)
            {
                TransitionToState(PlayerInputState.MovingAndAiming);
            }
            else if (currentState == PlayerInputState.ShootingContinuous)
            {
                TransitionToState(PlayerInputState.MovingAndShootingContinuous);
            }
            else if (currentState == PlayerInputState.ShootingBurst)
            {
                TransitionToState(PlayerInputState.MovingAndShootingBurst);
            }
        }
        // Right side of screen - Shooting
        else if (screenPosition.x > halfScreenWidth)
        {
            ShootingFinger = TouchedFinger;
            ShootingStartPosition = ClampShootingPosition(screenPosition);
            ShootingAmount = Vector2.zero;
            hasBurstFired = false;

            ShootingJoystick.gameObject.SetActive(true);
            ShootingJoystick.RectTransform.sizeDelta = JoystickSize;
            ShootingJoystick.RectTransform.anchoredPosition = ShootingStartPosition;
            ShootingJoystick.Knob.anchoredPosition = Vector2.zero;

            // Determine new state based on current state
            if (currentState == PlayerInputState.Idle)
            {
                TransitionToState(PlayerInputState.Aiming);
            }
            else if (currentState == PlayerInputState.Moving)
            {
                TransitionToState(PlayerInputState.MovingAndAiming);
            }
        }
    }

    private Vector2 ClampStartPosition(Vector2 StartPosition)
    {
        if (StartPosition.x < JoystickSize.x / 2)
        {
            StartPosition.x = JoystickSize.x / 2;
        }

        if (StartPosition.y < JoystickSize.y / 2)
        {
            StartPosition.y = JoystickSize.y / 2;
        }
        else if (StartPosition.y > Screen.height - JoystickSize.y / 2)
        {
            StartPosition.y = Screen.height - JoystickSize.y / 2;
        }

        return StartPosition;
    }

    #region FSM Core Methods

    private void TransitionToState(PlayerInputState newState)
    {
        if (currentState == newState) return;

        ExitState(currentState);
        previousState = currentState;
        currentState = newState;
        EnterState(currentState);

        // Uncomment for debugging state transitions
        // Debug.Log($"FSM Transition: {previousState} -> {currentState}");
    }

    private void EnterState(PlayerInputState state)
    {
        switch (state)
        {
            case PlayerInputState.Idle:
                EnterIdleState();
                break;
            case PlayerInputState.Moving:
                EnterMovingState();
                break;
            case PlayerInputState.Aiming:
                EnterAimingState();
                break;
            case PlayerInputState.ShootingBurst:
                EnterShootingBurstState();
                break;
            case PlayerInputState.ShootingContinuous:
                EnterShootingContinuousState();
                break;
            case PlayerInputState.MovingAndAiming:
                EnterMovingAndAimingState();
                break;
            case PlayerInputState.MovingAndShootingBurst:
                EnterMovingAndShootingBurstState();
                break;
            case PlayerInputState.MovingAndShootingContinuous:
                EnterMovingAndShootingContinuousState();
                break;
        }
    }

    private void UpdateState(PlayerInputState state)
    {
        switch (state)
        {
            case PlayerInputState.Moving:
            case PlayerInputState.MovingAndAiming:
            case PlayerInputState.MovingAndShootingBurst:
            case PlayerInputState.MovingAndShootingContinuous:
                UpdateMovement();
                break;
        }
    }

    private void ExitState(PlayerInputState state)
    {
        switch (state)
        {
            case PlayerInputState.Aiming:
            case PlayerInputState.MovingAndAiming:
                ExitAimingState();
                break;
            case PlayerInputState.ShootingContinuous:
            case PlayerInputState.MovingAndShootingContinuous:
                ExitShootingContinuousState();
                break;
        }
    }

    #endregion

    #region State Enter Methods

    private void EnterIdleState()
    {
        // Nothing specific to do
    }

    private void EnterMovingState()
    {
        // Movement joystick already activated in HandleFingerDown
    }

    private void EnterAimingState()
    {
        // Start delayed continuous shooting timer
        if (mainCharacter != null)
        {
            delayedContinuousCoroutine = StartCoroutine(DelayedContinuousShooting());
        }
    }

    private void EnterShootingBurstState()
    {
        // Burst shooting is triggered in HandleFingerMove when drag is detected
    }

    private void EnterShootingContinuousState()
    {
        // Continuous shooting is started by DelayedContinuousShooting coroutine
    }

    private void EnterMovingAndAimingState()
    {
        EnterAimingState();
    }

    private void EnterMovingAndShootingBurstState()
    {
        // Burst shooting handled in HandleFingerMove
    }

    private void EnterMovingAndShootingContinuousState()
    {
        // Continuous shooting already started
    }

    #endregion

    #region State Exit Methods

    private void ExitAimingState()
    {
        // Cancel delayed continuous shooting
        if (delayedContinuousCoroutine != null)
        {
            StopCoroutine(delayedContinuousCoroutine);
            delayedContinuousCoroutine = null;
        }
    }

    private void ExitShootingContinuousState()
    {
        // Stop continuous shooting
        if (isContinuousShooting && continuousShootingCoroutine != null)
        {
            StopCoroutine(continuousShootingCoroutine);
            isContinuousShooting = false;
        }
    }

    #endregion

    #region Update Methods

    private void UpdateMovement()
    {
        Vector3 scaledMovement = Player.speed * Time.deltaTime * new Vector3(
            MovementAmount.x,
            0,
            MovementAmount.y
        );

        if (scaledMovement.magnitude > 0.01f)
        {
            Player.transform.LookAt(Player.transform.position + scaledMovement, Vector3.up);
            Player.Move(scaledMovement);
        }
    }

    #endregion

    private void Update()
    {
        UpdateState(currentState);
    }

    private bool TryGetScreenPosition(Finger finger, out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (finger == null)
        {
            return false;
        }

        if (IsValidVector2(finger.screenPosition))
        {
            screenPosition = finger.screenPosition;
            return true;
        }

        var touch = finger.currentTouch;
        bool touchValid = touch.touchId >= 0;
        if (touchValid)
        {
            if (IsValidVector2(touch.screenPosition))
            {
                screenPosition = touch.screenPosition;
                return true;
            }

            if (IsValidVector2(touch.startScreenPosition))
            {
                screenPosition = touch.startScreenPosition;
                return true;
            }
        }

        return false;
    }

    private bool IsValidVector2(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    
    IEnumerator CloseAfter(GameObject go, float seconds, System.Action onClosed)
    {
        if (!go) yield break;
        if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        go.SetActive(false);
        onClosed?.Invoke();
    }
    
    void TryShowObjectives()
    {
        if (objectivesShown) return;

        bool dragDone  = (dragTutorial  == null) || !dragTutorial.activeInHierarchy || dragClosed;
        bool shootDone = (shootTutorial == null) || !shootTutorial.activeInHierarchy || shootClosed;
        
        Debug.Log(dragDone);
        Debug.Log(shootDone);

        if (dragDone && shootDone)
        {
            objectivesShown = true;
            if (panelsController)
                panelsController.ShowUI(objectivesPanelName);
        }
    }
    
    private Vector2 ClampShootingPosition(Vector2 screenPosition)
    {
        float minX = Screen.width / 2 + JoystickSize.x / 2;
        float maxX = Screen.width - JoystickSize.x / 2;
        float minY = JoystickSize.y / 2;
        float maxY = Screen.height - JoystickSize.y / 2;

        screenPosition.x = Mathf.Clamp(screenPosition.x, minX, maxX);
        screenPosition.y = Mathf.Clamp(screenPosition.y, minY, maxY);

        return screenPosition;
    }

    private IEnumerator ShootContinuously()
    {
        while (isContinuousShooting && mainCharacter != null)
        {
            Vector3 shootDirection = mainCharacter.transform.forward;
            mainCharacter.Shoot(shootDirection);
            yield return new WaitForSeconds(continuousShootingInterval);
        }
    }

    private IEnumerator ShootBurst(Vector3 direction)
    {
        if (mainCharacter == null) yield break;
        
        if (direction.magnitude > 0.1f)
        {
            direction.Normalize();
        }
        else
        {
            direction = mainCharacter.transform.forward;
        }
        
        for (int i = 0; i < burstShotCount; i++)
        {
            if (mainCharacter != null)
            {
                mainCharacter.Shoot(direction);
            }
            yield return new WaitForSeconds(burstShotInterval);
        }
    }
    
    private IEnumerator DelayedContinuousShooting()
    {
        yield return new WaitForSeconds(continuousShootingDelay);

        if (ShootingFinger != null && !hasBurstFired && mainCharacter != null)
        {
            isContinuousShooting = true;
            continuousShootingCoroutine = StartCoroutine(ShootContinuously());

            // Transition to continuous shooting state
            if (currentState == PlayerInputState.Aiming)
            {
                TransitionToState(PlayerInputState.ShootingContinuous);
            }
            else if (currentState == PlayerInputState.MovingAndAiming)
            {
                TransitionToState(PlayerInputState.MovingAndShootingContinuous);
            }
        }

        delayedContinuousCoroutine = null;
    }
}
