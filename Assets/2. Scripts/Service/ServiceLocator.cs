using System;
using System.Collections.Generic;
using UnityEngine;

public class ServiceLocator : MonoBehaviour
{
    private static ServiceLocator instance;
    private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
    
    public static ServiceLocator Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[ServiceLocator]");
                instance = go.AddComponent<ServiceLocator>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    public static void Register<T>(T service)
    {
        var type = typeof(T);
        if (Instance.services.ContainsKey(type))
        {
            Debug.LogWarning($"Service of type {type.Name} is already registered. Overwriting...");
        }
        Instance.services[type] = service;
    }
    
    public static T Get<T>()
    {
        var type = typeof(T);
        if (Instance.services.TryGetValue(type, out var service))
        {
            return (T)service;
        }
        
        Debug.LogError($"Service of type {type.Name} not found!");
        return default(T);
    }
    
    public static bool TryGet<T>(out T service)
    {
        var type = typeof(T);
        if (Instance.services.TryGetValue(type, out var foundService))
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
        if (Instance.services.ContainsKey(type))
        {
            Instance.services.Remove(type);
        }
    }
    
    public static bool IsRegistered<T>()
    {
        return Instance.services.ContainsKey(typeof(T));
    }
    
    public static void Clear()
    {
        Instance.services.Clear();
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            Clear();
        }
    }
}