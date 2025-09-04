using System;
using System.Collections.Generic;
using UnityEngine;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> services = new();
    
    public static void Register<T>(T service)
    
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            Logger.LogWarning($"Service of type {type.Name} is already registered. Overwriting...");
        }
        services[type] = service;
    }
    
    public static T Get<T>()
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
        {
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
            service = (T)foundService;
            return true;
        }
        
        service = default(T);
        return false;
    }
    
    public static void Unregister<T>()
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            services.Remove(type);
        }
    }
    
    public static bool IsRegistered<T>()
    {
        return services.ContainsKey(typeof(T));
    }
    
    public static void Clear()
    {
        services.Clear();
    }
}