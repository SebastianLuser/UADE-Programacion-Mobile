using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolService : IGameService
{
    private readonly Dictionary<string, ObjectPool<BulletObject>> bulletPools = new();
    private readonly Dictionary<string, BulletObject> bulletPrefabs = new();
    private Transform poolParent;
    
    public bool IsInitialized { get; private set; }
    
    public void Initialize()
    {
        if (IsInitialized) return;
        
        CreatePoolParent();
        SetupDefaultBulletPool();
        
        IsInitialized = true;
        Logger.LogInfo("ObjectPoolService initialized successfully");
    }
    
    public void Shutdown()
    {
        if (!IsInitialized) return;
        
        foreach (var pool in bulletPools.Values)
        {
            pool.Clear();
        }
        
        bulletPools.Clear();
        bulletPrefabs.Clear();
        
        if (poolParent != null)
        {
            Object.Destroy(poolParent.gameObject);
            poolParent = null;
        }
        
        IsInitialized = false;
        Logger.LogInfo("ObjectPoolService shutdown successfully");
    }
    
    private void CreatePoolParent()
    {
        GameObject poolParentObject = new GameObject("ObjectPools");
        Object.DontDestroyOnLoad(poolParentObject);
        poolParent = poolParentObject.transform;
    }
    
    private void SetupDefaultBulletPool()
    {
        // Create default bullet prefab
        BulletObject defaultBulletPrefab = CreateDefaultBulletPrefab();
        RegisterBulletPool("default", defaultBulletPrefab, 30);
    }
    
    private BulletObject CreateDefaultBulletPrefab()
    {
        GameObject bulletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletObject.name = "DefaultBulletPrefab";
        bulletObject.transform.localScale = Vector3.one * 0.2f;
        bulletObject.SetActive(false);
        
        // Setup physics
        var rigidbody = bulletObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.linearDamping = 0f;
        rigidbody.angularDamping = 0f;
        
        // Setup collider
        var collider = bulletObject.GetComponent<Collider>();
        collider.isTrigger = true;
        
        // Add bullet component
        var bulletComponent = bulletObject.AddComponent<BulletObject>();
        
        Object.DontDestroyOnLoad(bulletObject);
        return bulletComponent;
    }
    
    public void RegisterBulletPool(string poolName, BulletObject prefab, int initialSize = 10)
    {
        if (bulletPools.ContainsKey(poolName))
        {
            Logger.LogWarning($"ObjectPoolService: Pool '{poolName}' already exists. Overwriting...");
            bulletPools[poolName].Clear();
        }
        
        GameObject poolContainer = new GameObject($"Pool_{poolName}");
        poolContainer.transform.SetParent(poolParent);
        
        var pool = new ObjectPool<BulletObject>(
            prefab,
            poolContainer.transform,
            initialSize
        );
        
        bulletPools[poolName] = pool;
        bulletPrefabs[poolName] = prefab;
        
        Logger.LogInfo($"ObjectPoolService: Registered pool '{poolName}' with {initialSize} initial objects");
    }
    
    public GameObject GetBullet(Vector3 position, Vector3 direction, float damage = 25f, bool isEnemyBullet = false, string poolName = "default")
    {
        if (!IsInitialized)
        {
            Logger.LogError("ObjectPoolService: Service not initialized!");
            return null;
        }
        
        if (!bulletPools.TryGetValue(poolName, out var pool))
        {
            Logger.LogError($"ObjectPoolService: Pool '{poolName}' not found!");
            return null;
        }
        
        BulletObject bullet = pool.Get();
        if (bullet == null) return null;
        
        bullet.transform.position = position;
        bullet.InitializeBullet(direction, damage, null, isEnemyBullet);
        
        // Set color based on bullet type
        if (isEnemyBullet)
        {
            bullet.SetColor(Color.red);
        }
        else
        {
            bullet.SetColor(Color.yellow);
        }
        
        return bullet.gameObject;
    }
    
    public void ReturnBullet(GameObject bulletGameObject, string poolName = "default")
    {
        if (!IsInitialized || bulletGameObject == null) return;
        
        if (!bulletPools.TryGetValue(poolName, out var pool))
        {
            Logger.LogError($"ObjectPoolService: Pool '{poolName}' not found!");
            return;
        }
        
        var bulletObject = bulletGameObject.GetComponent<BulletObject>();
        if (bulletObject != null)
        {
            pool.Return(bulletObject);
        }
    }
    
    public void ReturnAllBullets(string poolName = "default")
    {
        if (!IsInitialized) return;
        
        if (!bulletPools.TryGetValue(poolName, out var pool))
        {
            Logger.LogError($"ObjectPoolService: Pool '{poolName}' not found!");
            return;
        }
        
        pool.ReturnAll();
    }
    
    public void ReturnAllBulletsFromAllPools()
    {
        foreach (var pool in bulletPools.Values)
        {
            pool.ReturnAll();
        }
    }
    
    public int GetActiveBulletsCount(string poolName = "default")
    {
        if (!bulletPools.TryGetValue(poolName, out var pool))
            return 0;
            
        return pool.ActiveCount;
    }
    
    public int GetAvailableBulletsCount(string poolName = "default")
    {
        if (!bulletPools.TryGetValue(poolName, out var pool))
            return 0;
            
        return pool.AvailableCount;
    }
    
    public Dictionary<string, (int active, int available)> GetAllPoolsStatus()
    {
        var status = new Dictionary<string, (int active, int available)>();
        
        foreach (var kvp in bulletPools)
        {
            status[kvp.Key] = (kvp.Value.ActiveCount, kvp.Value.AvailableCount);
        }
        
        return status;
    }
    
}