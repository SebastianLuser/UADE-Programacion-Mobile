using UnityEngine;

public class ObjectPoolManager : BaseManager
{
    private BulletPool bulletPool;
    
    public BulletPool BulletPool => bulletPool;
    
    protected override void OnInitialize()
    {
        SetupBulletPool();
        ServiceLocator.Register(this);
    }
    
    private void SetupBulletPool()
    {
        GameObject bulletPoolObject = new GameObject("BulletPool");
        bulletPoolObject.transform.SetParent(transform);
        bulletPool = bulletPoolObject.AddComponent<BulletPool>();
        
        Logger.LogInfo("Object pools initialized successfully");
    }
    
    public GameObject GetBullet(Vector3 position, Vector3 direction, float damage = 25f, bool isEnemyBullet = false)
    {
        if (bulletPool != null)
        {
            return bulletPool.GetBullet(position, direction, damage, isEnemyBullet);
        }
        
        Logger.LogError("BulletPool is not initialized!");
        return null;
    }
    
    public void ReturnBullet(GameObject bullet)
    {
        if (bulletPool != null)
        {
            bulletPool.ReturnBullet(bullet);
        }
    }
    
    public void ReturnAllBullets()
    {
        if (bulletPool != null)
        {
            bulletPool.ReturnAllBullets();
        }
    }
    
    public int GetActiveBulletsCount()
    {
        return bulletPool?.ActiveBulletsCount ?? 0;
    }
    
    public int GetAvailableBulletsCount()
    {
        return bulletPool?.AvailableBulletsCount ?? 0;
    }
    
    protected override void OnShutdown()
    {
        if (bulletPool != null)
        {
            bulletPool.ReturnAllBullets();
            Destroy(bulletPool.gameObject);
        }
        ServiceLocator.Unregister<ObjectPoolManager>();
    }
}