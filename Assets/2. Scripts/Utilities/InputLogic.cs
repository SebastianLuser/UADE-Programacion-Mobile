using UnityEngine;

public class InputLogic : IUpdatable
{
    private Camera mainCamera;
    private CharacterManager characterManager;
    private bool isActive;
    
    public bool IsActive => isActive;
    
    public InputLogic(Camera camera)
    {
        mainCamera = camera;
        isActive = true;
    }
    
    public void Initialize()
    {
        characterManager = ServiceLocator.Get<CharacterManager>();
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.RegisterUpdatable(this);
    }
    
    public void OnUpdate(float deltaTime)
    {
        if (!isActive || characterManager?.MainCharacter == null) return;
        
        Vector2 movementInput = GetMovementInput();
        Vector3 shootDirection = GetShootDirection();
        
        // Call HandleInput on the MainCharacter (works for both MainCharacter and PlayerController)
        var mainCharacter = characterManager.MainCharacter as MainCharacter;
        if (mainCharacter != null)
        {
            mainCharacter.HandleInput(movementInput, shootDirection);
        }
    }
    
    private Vector2 GetMovementInput()
    {
        Vector2 input = Vector2.zero;
        
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            input.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            input.y -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            input.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            input.x += 1f;
        
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.position.x < Screen.width * 0.5f)
            {
                Vector2 deltaPosition = touch.deltaPosition * 0.01f;
                input = deltaPosition;
            }
        }
        
        return input.normalized;
    }
    
    private Vector3 GetShootDirection()
    {
        Vector3 shootDirection = Vector3.zero;
        
        if (characterManager?.MainCharacter == null) return shootDirection;
        
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 targetPosition = hit.point;
                targetPosition.y = characterManager.MainCharacter.Transform.position.y;
                shootDirection = (targetPosition - characterManager.MainCharacter.Transform.position).normalized;
            }
        }
        
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.position.x >= Screen.width * 0.5f)
            {
                Vector3 touchPosition = touch.position;
                Ray ray = mainCamera.ScreenPointToRay(touchPosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Vector3 targetPosition = hit.point;
                    targetPosition.y = characterManager.MainCharacter.Transform.position.y;
                    shootDirection = (targetPosition - characterManager.MainCharacter.Transform.position).normalized;
                }
            }
        }
        
        return shootDirection;
    }
    
    public void Shutdown()
    {
        isActive = false;
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
    }
}