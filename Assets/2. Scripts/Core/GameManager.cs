using UnityEngine;

public class GameManager : BaseManager
{
    [Header("Manager Initialization Order")]
    [SerializeField] private bool autoInitialize = true;
    
    private UpdateManager updateManager;
    private CharacterManager characterManager;
    private InputManager inputManager;
    private LevelManager levelManager;
    private ObjectPoolManager objectPoolManager;
    private GameStateManager gameStateManager;
    
    private void Start()
    {
        if (autoInitialize)
        {
            Initialize();
        }
    }
    
    protected override void OnInitialize()
    {
        Debug.Log("GameManager: Starting initialization...");
        
        InitializeServiceLocator();
        InitializeManagers();
        StartGame();
        
        ServiceLocator.Register<GameManager>(this);
        Debug.Log("GameManager: Initialization completed!");
    }
    
    private void InitializeServiceLocator()
    {
        var serviceLocator = ServiceLocator.Instance;
        Debug.Log("ServiceLocator initialized");
    }
    
    private void InitializeManagers()
    {
        CreateManager(ref updateManager, "UpdateManager");
        CreateManager(ref objectPoolManager, "ObjectPoolManager");
        CreateManager(ref levelManager, "LevelManager");
        CreateManager(ref characterManager, "CharacterManager");
        CreateManager(ref inputManager, "InputManager");
        CreateManager(ref gameStateManager, "GameStateManager");
    }
    
    private void CreateManager<T>(ref T manager, string name) where T : BaseManager
    {
        GameObject managerObject = new GameObject($"[{name}]");
        managerObject.transform.SetParent(transform);
        manager = managerObject.AddComponent<T>();
        manager.Initialize();
        Debug.Log($"{name} created and initialized");
    }
    
    private void StartGame()
    {
        if (characterManager != null)
        {
            characterManager.SpawnMainCharacter();
            characterManager.SpawnEnemiesAtDefaultPositions();
        }
        
        if (gameStateManager != null)
        {
            gameStateManager.StartGame();
        }
    }
    
    public void RestartGame()
    {
        if (characterManager != null)
        {
            characterManager.RemoveAllCharacters();
            characterManager.SpawnMainCharacter();
            characterManager.SpawnEnemiesAtDefaultPositions();
        }
        
        if (objectPoolManager != null)
        {
            objectPoolManager.ReturnAllBullets();
        }
        
        if (gameStateManager != null)
        {
            gameStateManager.StartGame();
        }
        
        Debug.Log("Game restarted");
    }
    
    protected override void OnShutdown()
    {
        if (gameStateManager != null) gameStateManager.Shutdown();
        if (inputManager != null) inputManager.Shutdown();
        if (characterManager != null) characterManager.Shutdown();
        if (levelManager != null) levelManager.Shutdown();
        if (objectPoolManager != null) objectPoolManager.Shutdown();
        if (updateManager != null) updateManager.Shutdown();
        
        ServiceLocator.Unregister<GameManager>();
        Debug.Log("GameManager shutdown completed");
    }
}