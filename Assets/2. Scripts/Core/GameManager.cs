using UnityEngine;
using DevelopmentUtilities;

public class GameManager : BaseManager
{
    [Header("Manager Configuration")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private GameObject[] managerPrefabs;
    
    [Header("Manager References")]
    [SerializeField] private UpdateManager updateManager;
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private GameStateManager gameStateManager;
    
    private void Start()
    {
        if (autoInitialize)
        {
            Initialize();
        }
    }
    
    protected override void OnInitialize()
    {
        Logger.LogInfo("GameManager: Starting initialization...");
        
        // Use ActionAfterFrame for robust initialization timing
        this.ActionAfterFrame(() =>
        {
            InitializeManagers();
            
            this.ActionAfterFrame(() =>
            {
                InitializeServices();
                RegisterServices();
                
                this.ActionAfterFrame(() =>
                {
                    ServiceLocator.InitializeAllServices();
                    
                    this.ActionAfterFrame(() =>
                    {
                        StartGame();
                        Logger.LogInfo("GameManager: Initialization completed!");
                    });
                });
            });
        });
    }
    
    private void InitializeManagers()
    {
        if (managerPrefabs != null && managerPrefabs.Length > 0)
        {
            InitializeManagersFromPrefabs();
        }
        else
        {
            InitializeManagersFromReferences();
        }
    }
    
    private void InitializeManagersFromPrefabs()
    {
        foreach (var prefab in managerPrefabs)
        {
            if (prefab == null) continue;
            
            var managerInstance = Instantiate(prefab, transform);
            var manager = managerInstance.GetComponent<BaseManager>();
            
            if (manager != null)
            {
                manager.Initialize();
                AssignManagerReference(manager);
                Logger.LogInfo($"Manager {manager.GetType().Name} instantiated and initialized from prefab");
            }
            else
            {
                Logger.LogWarning($"Prefab {prefab.name} does not contain a BaseManager component");
            }
        }
    }
    
    private void InitializeManagersFromReferences()
    {
        InitializeManager(updateManager, "UpdateManager");
        InitializeManager(levelManager, "LevelManager");
        InitializeManager(characterManager, "CharacterManager");
        InitializeManager(inputManager, "InputManager");
        InitializeManager(gameStateManager, "GameStateManager");
    }
    
    private void InitializeServices()
    {
        // Initialize services that don't inherit from BaseManager
        var objectPoolService = new ObjectPoolService();
        ServiceLocator.Register<ObjectPoolService>(objectPoolService);
        
        Logger.LogInfo("Services initialized");
    }
    
    private void InitializeManager(BaseManager manager, string name)
    {
        if (manager != null)
        {
            manager.Initialize();
            Logger.LogInfo($"{name} initialized from reference");
        }
        else
        {
            Logger.LogWarning($"{name} reference is not assigned");
        }
    }
    
    private void AssignManagerReference(BaseManager manager)
    {
        switch (manager)
        {
            case UpdateManager um:
                updateManager = um;
                break;
            case CharacterManager cm:
                characterManager = cm;
                break;
            case InputManager im:
                inputManager = im;
                break;
            case LevelManager lm:
                levelManager = lm;
                break;
            case GameStateManager gsm:
                gameStateManager = gsm;
                break;
        }
    }
    
    private void RegisterServices()
    {
        ServiceLocator.Register<GameManager>(this);
        
        if (updateManager != null) ServiceLocator.Register<UpdateManager>(updateManager);
        if (characterManager != null) ServiceLocator.Register<CharacterManager>(characterManager);
        if (inputManager != null) ServiceLocator.Register<InputManager>(inputManager);
        if (levelManager != null) ServiceLocator.Register<LevelManager>(levelManager);
        if (gameStateManager != null) ServiceLocator.Register<GameStateManager>(gameStateManager);
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
        
        var objectPoolService = ServiceLocator.Get<ObjectPoolService>();
        if (objectPoolService != null)
        {
            objectPoolService.ReturnAllBulletsFromAllPools();
        }
        
        if (gameStateManager != null)
        {
            gameStateManager.StartGame();
        }
        
        Logger.LogInfo("Game restarted");
    }
    
    protected override void OnShutdown()
    {
        if (gameStateManager != null) gameStateManager.Shutdown();
        if (inputManager != null) inputManager.Shutdown();
        if (characterManager != null) characterManager.Shutdown();
        if (levelManager != null) levelManager.Shutdown();
        if (updateManager != null) updateManager.Shutdown();
        
        ServiceLocator.ShutdownAllServices();
        ServiceLocator.Clear();
        
        Logger.LogInfo("GameManager shutdown completed");
    }
}