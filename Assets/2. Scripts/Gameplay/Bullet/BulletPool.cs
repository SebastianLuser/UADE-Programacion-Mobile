using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 30;
    [SerializeField] private int maxPoolSize = 100;
    [SerializeField] private bool canGrow = true;
    
    private Queue<GameObject> availableBullets = new Queue<GameObject>();
    private HashSet<GameObject> activeBullets = new HashSet<GameObject>();
    private GameObject bulletPrefab;
    
    public static BulletPool Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            CreateBulletPrefab();
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void CreateBulletPrefab()
    {
        bulletPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletPrefab.name = "BulletPrefab";
        bulletPrefab.transform.localScale = Vector3.one * 0.2f;
        bulletPrefab.SetActive(false);
        
        var rigidbody = bulletPrefab.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.linearDamping = 0f;
        rigidbody.angularDamping = 0f;
        
        var collider = bulletPrefab.GetComponent<Collider>();
        collider.isTrigger = true;
        
        bulletPrefab.AddComponent<BulletObject>();
        
        DontDestroyOnLoad(bulletPrefab);
    }
    
    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewBullet();
        }
    }
    
    private GameObject CreateNewBullet()
    {
        GameObject bullet = Instantiate(bulletPrefab, transform);
        bullet.name = $"PooledBullet_{availableBullets.Count}";
        bullet.SetActive(false);
        availableBullets.Enqueue(bullet);
        return bullet;
    }
    
    public GameObject GetBullet(Vector3 position, Vector3 direction, float damage = 25f, bool isEnemyBullet = false)
    {
        GameObject bullet = GetPooledBullet();
        
        bullet.transform.position = position;
        bullet.SetActive(true);
        
        var bulletObject = bullet.GetComponent<BulletObject>();
        if (bulletObject != null)
        {
            bulletObject.InitializeBullet(direction, damage, this, isEnemyBullet);
        }
        
        activeBullets.Add(bullet);
        
        return bullet;
    }
    
    private GameObject GetPooledBullet()
    {
        if (availableBullets.Count > 0)
        {
            return availableBullets.Dequeue();
        }
        
        if (canGrow && activeBullets.Count + availableBullets.Count < maxPoolSize)
        {
            return CreateNewBullet();
        }
        
        Logger.LogWarning("BulletPool: No bullets available and pool at max capacity!");
        return CreateNewBullet();
    }
    
    public void ReturnBullet(GameObject bullet)
    {
        if (bullet == null) return;
        
        activeBullets.Remove(bullet);
        
        var bulletObject = bullet.GetComponent<BulletObject>();
        if (bulletObject != null)
        {
            bulletObject.Reset();
        }
        
        bullet.SetActive(false);
        availableBullets.Enqueue(bullet);
    }
    
    public void ReturnAllBullets()
    {
        var bulletsToReturn = new List<GameObject>(activeBullets);
        foreach (var bullet in bulletsToReturn)
        {
            ReturnBullet(bullet);
        }
    }
    
    public int ActiveBulletsCount => activeBullets.Count;
    public int AvailableBulletsCount => availableBullets.Count;
    public int TotalPoolSize => ActiveBulletsCount + AvailableBulletsCount;
}