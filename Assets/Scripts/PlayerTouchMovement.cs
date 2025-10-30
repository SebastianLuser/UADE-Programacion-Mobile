using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch;

public class PlayerTouchMovement : MonoBehaviour
{
    [SerializeField]
    private Vector2 JoystickSize = new Vector2(300, 300);
    [SerializeField]
    private FloatingJoystick Joystick;
    [SerializeField]
    private NavMeshAgent Player;

    private Finger MovementFinger;
    private Vector2 MovementAmount;
    
    [SerializeField]
    private MainCharacter mainCharacter; // Reference to the MainCharacter script

    private Finger TapFinger;

    private void OnEnable()
    {
        ETouch.Touch.onFingerDown += HandleFingerDown;
        ETouch.Touch.onFingerUp += HandleLoseFinger;
        ETouch.Touch.onFingerMove += HandleFingerMove;
    }

    private void OnDisable()
    {
        ETouch.Touch.onFingerDown -= HandleFingerDown;
        ETouch.Touch.onFingerUp -= HandleLoseFinger;
        ETouch.Touch.onFingerMove -= HandleFingerMove;
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
        else if (LostFinger == TapFinger)
        {
            // Call shoot method
            if (mainCharacter != null)
            {
                Vector3 shootDirection = mainCharacter.transform.forward;
                mainCharacter.Shoot(shootDirection);
            }

            TapFinger = null;
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
            TapFinger = TouchedFinger;
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
}
