using UnityEngine;

public class InputManager : BaseManager
{
    private InputLogic inputLogic;
    private Camera mainCamera;
    
    public Camera MainCamera => mainCamera;
    
    protected override void OnInitialize()
    {
        SetupCamera();
        SetupInputLogic();
        ServiceLocator.Register<InputManager>(this);
    }
    
    private void SetupCamera()
    {
        mainCamera = Camera.main;
        
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 10f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            
            Debug.Log("Main camera created successfully");
        }
    }
    
    private void SetupInputLogic()
    {
        inputLogic = new InputLogic(mainCamera);
        inputLogic.Initialize();
        
        Debug.Log("Input logic setup complete");
    }
    
    public InputLogic GetInputLogic()
    {
        return inputLogic;
    }
    
    protected override void OnShutdown()
    {
        if (inputLogic != null)
        {
            inputLogic.Shutdown();
        }
        ServiceLocator.Unregister<InputManager>();
    }
}