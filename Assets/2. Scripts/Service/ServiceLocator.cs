using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> services = new();
    private static readonly Dictionary<Type, bool> serviceInitializationStatus = new();
    private static readonly List<Type> initializationOrder = new();
    private static bool isInitializing = false;
    
    public static void Register<T>(T service)
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            Logger.LogWarning($"Service of type {type.Name} is already registered. Overwriting...");
            
            // Shutdown old service if it implements IGameService
            if (services[type] is IGameService oldGameService && oldGameService.IsInitialized)
            {
                oldGameService.Shutdown();
            }
        }
        
        services[type] = service;
        serviceInitializationStatus[type] = false;
        
        if (!initializationOrder.Contains(type))
        {
            initializationOrder.Add(type);
        }
        
        Logger.LogInfo($"Service {type.Name} registered successfully");
    }
    
    public static T Get<T>()
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
        {
            // Ensure service is initialized if it implements IGameService
            if (service is IGameService gameService && !gameService.IsInitialized && !isInitializing)
            {
                InitializeService(gameService, type);
            }
            
            return (T)service;
        }
        
        Logger.LogError($"Service of type {type.Name} not found!");
        return default(T);
    }
    
    public static bool TryGet<T>(out T service)
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var foundService))
        {
            // Ensure service is initialized if it implements IGameService
            if (foundService is IGameService gameService && !gameService.IsInitialized && !isInitializing)
            {
                InitializeService(gameService, type);
            }
            
            service = (T)foundService;
            return true;
        }
        
        service = default(T);
        return false;
    }
    
    public static void InitializeAllServices()
    {
        isInitializing = true;
        Logger.LogInfo("Initializing all registered services...");
        
        foreach (var serviceType in initializationOrder)
        {
            if (services.TryGetValue(serviceType, out var service) && service is IGameService gameService)
            {
                if (!gameService.IsInitialized)
                {
                    InitializeService(gameService, serviceType);
                }
            }
        }
        
        isInitializing = false;
        Logger.LogInfo("All services initialized successfully");
    }
    
    private static void InitializeService(IGameService service, Type serviceType)
    {
        try
        {
            Logger.LogInfo($"Initializing service: {serviceType.Name}");
            service.Initialize();
            serviceInitializationStatus[serviceType] = true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize service {serviceType.Name}: {ex.Message}");
        }
    }
    
    public static void Unregister<T>()
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
        {
            // Shutdown service if it implements IGameService
            if (service is IGameService gameService && gameService.IsInitialized)
            {
                gameService.Shutdown();
            }
            
            services.Remove(type);
            serviceInitializationStatus.Remove(type);
            initializationOrder.Remove(type);
            Logger.LogInfo($"Service {type.Name} unregistered successfully");
        }
    }
    
    public static bool IsRegistered<T>()
    {
        return services.ContainsKey(typeof(T));
    }
    
    public static bool IsInitialized<T>() where T : IGameService
    {
        var type = typeof(T);
        return serviceInitializationStatus.TryGetValue(type, out bool isInitialized) && isInitialized;
    }
    
    public static void ShutdownAllServices()
    {
        Logger.LogInfo("Shutting down all services...");
        
        // Shutdown in reverse order
        var reverseOrder = initializationOrder.AsEnumerable().Reverse();
        
        foreach (var serviceType in reverseOrder)
        {
            if (services.TryGetValue(serviceType, out var service) && service is IGameService gameService)
            {
                if (gameService.IsInitialized)
                {
                    try
                    {
                        Logger.LogInfo($"Shutting down service: {serviceType.Name}");
                        gameService.Shutdown();
                        serviceInitializationStatus[serviceType] = false;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to shutdown service {serviceType.Name}: {ex.Message}");
                    }
                }
            }
        }
    }
    
    public static void Clear()
    {
        ShutdownAllServices();
        services.Clear();
        serviceInitializationStatus.Clear();
        initializationOrder.Clear();
        Logger.LogInfo("ServiceLocator cleared");
    }
    
    public static void LogRegisteredServices()
    {
        Logger.LogInfo("=== Registered Services ===");
        foreach (var kvp in services)
        {
            var isInitialized = serviceInitializationStatus.TryGetValue(kvp.Key, out bool initialized) ? initialized : false;
            var status = kvp.Value is IGameService ? (isInitialized ? "Initialized" : "Not Initialized") : "N/A";
            Logger.LogInfo($"- {kvp.Key.Name}: {status}");
        }
    }
    
    // Debug helper methods for ServiceLocatorDebugger
    public static IEnumerable<Type> GetRegisteredServiceTypes()
    {
        return services.Keys;
    }
    
    public static bool TryGetServiceObject(Type serviceType, out object service)
    {
        return services.TryGetValue(serviceType, out service);
    }
    
    public static Dictionary<Type, bool> GetInitializationStatus()
    {
        return new Dictionary<Type, bool>(serviceInitializationStatus);
    }
}