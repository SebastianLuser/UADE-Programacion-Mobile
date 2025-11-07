using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch;
using System.Collections;


public class PlayerTouchMovement : MonoBehaviour
{
    [SerializeField] private Vector2 JoystickSize = new Vector2(300, 300);
    [SerializeField] private FloatingJoystick Joystick;
    [SerializeField] private NavMeshAgent Player;

    private Finger MovementFinger;
    private Vector2 MovementAmount;

    [SerializeField] private MainCharacter mainCharacter; // Reference to the MainCharacter script

    private Finger TapFinger;

    private Finger ShootingFinger;
    private Vector2 ShootingStartPosition;
    private Vector2 ShootingAmount;
    private bool isContinuousShooting = false;
    private bool hasBurstFired = false;
    private Coroutine continuousShootingCoroutine;

    [Header("Shooting Settings")]
    [SerializeField] private FloatingJoystick ShootingJoystick; // New joystick for shooting
    [SerializeField] private float continuousShootingInterval = 0.3f; // Time between shots in continuous mode
    [SerializeField] private int burstShotCount = 5; // Number of shots in burst mode
    [SerializeField] private float burstShotInterval = 0.25f; // Time between shots in burst mode
    [SerializeField]
    private float continuousShootingDelay = 0.3f; // Delay before continuous shooting starts
    private Coroutine delayedContinuousCoroutine;

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
            
            // If we have significant drag, trigger burst shooting
            if (dragVector.magnitude > 30f && mainCharacter != null)
            {
                // Cancel delayed continuous shooting if it was scheduled
                if (delayedContinuousCoroutine != null)
                {
                    StopCoroutine(delayedContinuousCoroutine);
                    delayedContinuousCoroutine = null;
                }
                
                // Stop any active continuous shooting
                if (isContinuousShooting && continuousShootingCoroutine != null)
                {
                    StopCoroutine(continuousShootingCoroutine);
                    isContinuousShooting = false;
                }
                
                // Only fire burst if we haven't already or if we're still dragging
                if (!hasBurstFired || dragVector.magnitude > 50f)
                {
                    Vector3 shootDirection = new Vector3(dragVector.normalized.x, 0, dragVector.normalized.y);
                    if (!hasBurstFired)
                    {
                        // First burst
                        StartCoroutine(ShootBurst(shootDirection));
                        hasBurstFired = true;
                    }
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
        }
        else if (LostFinger == ShootingFinger)
        {
            // Cancel delayed continuous shooting
            if (delayedContinuousCoroutine != null)
            {
                StopCoroutine(delayedContinuousCoroutine);
                delayedContinuousCoroutine = null;
            }
            
            // Stop continuous shooting
            if (isContinuousShooting && continuousShootingCoroutine != null)
            {
                StopCoroutine(continuousShootingCoroutine);
                isContinuousShooting = false;
            }
            
            // If no burst was fired and no continuous shooting was active, fire a single shot
            if (!hasBurstFired && !isContinuousShooting && mainCharacter != null)
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
        }
    }

    private void HandleFingerDown(Finger TouchedFinger)
    {
        float halfScreenWidth = Screen.width / 2f;

        if (!TryGetScreenPosition(TouchedFinger, out Vector2 screenPosition))
        {
            return;
        }

        if (MovementFinger == null && screenPosition.x <= halfScreenWidth)
        {
            MovementFinger = TouchedFinger;
            MovementAmount = Vector2.zero;
            Joystick.gameObject.SetActive(true);
            Joystick.RectTransform.sizeDelta = JoystickSize;
            Joystick.RectTransform.anchoredPosition = ClampStartPosition(screenPosition);
        }
        else if (screenPosition.x > halfScreenWidth) // Right side of screen for shooting
        {
            ShootingFinger = TouchedFinger;
            ShootingStartPosition = ClampShootingPosition(screenPosition);
            ShootingAmount = Vector2.zero;
            hasBurstFired = false;
            
            // Show shooting joystick
            ShootingJoystick.gameObject.SetActive(true);
            ShootingJoystick.RectTransform.sizeDelta = JoystickSize;
            ShootingJoystick.RectTransform.anchoredPosition = ShootingStartPosition;
            ShootingJoystick.Knob.anchoredPosition = Vector2.zero;
            
            // Schedule continuous shooting to start after a delay
            // This gives the player time to aim before continuous shooting begins
            if (mainCharacter != null)
            {
                delayedContinuousCoroutine = StartCoroutine(DelayedContinuousShooting());
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

    private void Update()
    {
        Vector3 scaledMovement = Player.speed * Time.deltaTime * new Vector3(
            MovementAmount.x,
            0,
            MovementAmount.y
        );

        Player.transform.LookAt(Player.transform.position + scaledMovement, Vector3.up);
        Player.Move(scaledMovement);
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

    private Vector2 ClampShootingPosition(Vector2 screenPosition)
    {
        // Special clamping for shooting joystick on right side
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
        
        // Make sure direction is normalized
        if (direction.magnitude > 0.1f)
        {
            direction.Normalize();
        }
        else
        {
            // If no direction, use forward
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
        
        // Only start continuous shooting if we're still holding and haven't fired a burst
        if (ShootingFinger != null && !hasBurstFired && mainCharacter != null)
        {
            isContinuousShooting = true;
            continuousShootingCoroutine = StartCoroutine(ShootContinuously());
        }
    }

}