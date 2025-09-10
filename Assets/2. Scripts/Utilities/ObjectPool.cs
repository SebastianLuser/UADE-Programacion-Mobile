using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic Object Pool for any Component type - THE definitive pool implementation
/// </summary>
[System.Serializable]
public class ObjectPool<T> where T : Component
{
    [SerializeField] private bool isDynamic = true;
    [SerializeField] private int initialSize = 10;
    [SerializeField] private int maxSize = 100;
    
    [Header("Pool Status (Debug)")]
    [SerializeField] private int activeCount;
    [SerializeField] private int availableCount;
    
    private Queue<T> availableObjects = new Queue<T>();
    private HashSet<T> activeObjects = new HashSet<T>();
    private T prefab;
    private Transform parent;
    
    public int ActiveCount => activeObjects.Count;
    public int AvailableCount => availableObjects.Count;
    public int TotalCount => ActiveCount + AvailableCount;
    
    /// <summary>
    /// Default constructor for serialization
    /// </summary>
    public ObjectPool()
    {
        this.activeObjects = new HashSet<T>();
        this.availableObjects = new Queue<T>();
    }
    
    /// <summary>
    /// Create an object pool
    /// </summary>
    public ObjectPool(T prefab, Transform parent = null, int initialPoolSize = 10, 
                     bool isDynamicPool = true, int maxPoolSize = 100)
    {
        this.prefab = prefab;
        this.parent = parent;
        this.initialSize = initialPoolSize;
        this.isDynamic = isDynamicPool;
        this.maxSize = maxPoolSize;
        
        // Initialize collections
        this.availableObjects = new Queue<T>();
        this.activeObjects = new HashSet<T>();
        
        InitializePool();
    }
    
    
    private void InitializePool()
    {
        for (int i = 0; i < initialSize; i++)
        {
            CreateNewObject();
        }
        
        UpdateDebugInfo();
    }
    
    private T CreateNewObject()
    {
        T newObject = UnityEngine.Object.Instantiate(prefab, parent);
        newObject.name = $"{prefab.name}_Pooled_{TotalCount}";
        newObject.gameObject.SetActive(false);
        
        availableObjects.Enqueue(newObject);
        return newObject;
    }
    
    /// <summary>
    /// Get object from pool
    /// </summary>
    public T Get()
    {
        T obj = null;
        
        if (availableObjects.Count > 0)
        {
            obj = availableObjects.Dequeue();
        }
        else if (isDynamic)
        {
            if (TotalCount >= maxSize)
            {
                Logger.LogWarning($"ObjectPool<{typeof(T).Name}>: Creating object beyond max size!");
            }
            obj = UnityEngine.Object.Instantiate(prefab, parent);
            obj.name = $"{prefab.name}_Pooled_{TotalCount}";
            obj.gameObject.SetActive(false);
        }
        else
        {
            Logger.LogWarning($"ObjectPool<{typeof(T).Name}>: No objects available and pool is not dynamic!");
            return null;
        }
        
        activeObjects.Add(obj);
        obj.gameObject.SetActive(true);
        
        if (obj is IPoolable poolable)
        {
            poolable.OnPoolGet();
        }
        
        UpdateDebugInfo();
        return obj;
    }
    
    /// <summary>
    /// Return object to pool
    /// </summary>
    public void Return(T obj)
    {
        if (obj == null) return;
        
        if (!activeObjects.Contains(obj))
        {
            Logger.LogWarning($"ObjectPool<{typeof(T).Name}>: Trying to return object that wasn't from this pool!");
            return;
        }
        
        activeObjects.Remove(obj);
        
        if (obj is IPoolable returnPoolable)
        {
            returnPoolable.OnPoolReturn();
        }
        
        obj.gameObject.SetActive(false);
        availableObjects.Enqueue(obj);
        
        UpdateDebugInfo();
    }
    
    /// <summary>
    /// Return all active objects to pool
    /// </summary>
    public void ReturnAll()
    {
        var objectsToReturn = new List<T>(activeObjects);
        foreach (var obj in objectsToReturn)
        {
            Return(obj);
        }
    }
    
    /// <summary>
    /// Clear the entire pool
    /// </summary>
    public void Clear()
    {
        ReturnAll();
        
        while (availableObjects.Count > 0)
        {
            T obj = availableObjects.Dequeue();
            if (obj != null)
            {
                if (obj is IPoolable destroyPoolable)
                {
                    destroyPoolable.OnPoolDestroy();
                }
                
                UnityEngine.Object.Destroy(obj.gameObject);
            }
        }
        
        activeObjects.Clear();
        UpdateDebugInfo();
    }
    
    private void UpdateDebugInfo()
    {
        activeCount = activeObjects.Count;
        availableCount = availableObjects.Count;
    }
    
}