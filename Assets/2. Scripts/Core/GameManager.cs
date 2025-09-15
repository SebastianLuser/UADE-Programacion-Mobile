using UnityEngine;
using DevelopmentUtilities;

public class GameManager : BaseManager
{
    [Header("Manager Configuration")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private BaseManager[] managerPrefabs;
    
    [Header("Manager References")]
    [SerializeField] private UpdateManager updateManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private GameStateManager gameStateManager;
    
    // MEJORA: Agregado AISystemInitializer para integración completa del sistema de AI
    [Header("AI System")]
    [SerializeField] private AISystemInitializer aiSystemInitializer;
    
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
            if (!prefab) continue;
            
            var managerInstance = Instantiate(prefab, transform);
            
            if (managerInstance)
            {
                managerInstance.Initialize();
                AssignManagerReference(managerInstance);
                Logger.LogInfo($"Manager {managerInstance.GetType().Name} instantiated and initialized from prefab");
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
        InitializeManager(gameStateManager, "GameStateManager");
        
        // MEJORA: Inicializar AISystemInitializer después de los managers básicos
        // pero antes de los servicios para asegurar el orden correcto
        InitializeManager(aiSystemInitializer, "AISystemInitializer");
    }
    
    private void InitializeServices()
    {
        // Initialize services that don't inherit from BaseManager
        var objectPoolService = new ObjectPoolService();
        ServiceLocator.Register(objectPoolService);
        
        Logger.LogInfo("Services initialized");
    }
    
    private void InitializeManager(BaseManager manager, string name)
    {
        if (manager)
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
            case LevelManager lm:
                levelManager = lm;
                break;
            case GameStateManager gsm:
                gameStateManager = gsm;
                break;
            // MEJORA: Agregado case para AISystemInitializer
            case AISystemInitializer asi:
                aiSystemInitializer = asi;
                break;
        }
    }
    
    //todo service is not a manager
    private void RegisterServices()
    {
        ServiceLocator.Register(this);
        
        if (updateManager) ServiceLocator.Register(updateManager);
        if (levelManager) ServiceLocator.Register(levelManager);
        if (gameStateManager) ServiceLocator.Register(gameStateManager);
        
        // MEJORA: Registrar AISystemInitializer como servicio también
        // Esto permite que otros sistemas accedan a él si necesitan configurar AI en runtime
        if (aiSystemInitializer) ServiceLocator.Register(aiSystemInitializer);
    }
    
    private void StartGame()
    {
        
        if (gameStateManager)
        {
            gameStateManager.StartGame();
        }
    }
    
    public void RestartGame()
    {
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
        // MEJORA: Shutdown en orden reverso para dependencies correctas
        // AI System debe cerrarse antes que los managers de los que depende
        if (aiSystemInitializer != null) aiSystemInitializer.Shutdown();
        if (gameStateManager != null) gameStateManager.Shutdown();
        if (levelManager != null) levelManager.Shutdown();
        if (updateManager != null) updateManager.Shutdown();
        
        ServiceLocator.ShutdownAllServices();
        ServiceLocator.Clear();
        
        Logger.LogInfo("GameManager shutdown completed");
    }
}