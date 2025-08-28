using UnityEngine;

public class BulletLogic : IUpdatable
{
    private float speed = 10f;
    private float damage = 25f;
    private float lifetime = 5f;
    
    private Vector3 direction;
    private float timer;
    private bool isActive;
    
    private BulletObject bulletObject;
    private BulletPool pool;
    
    public bool IsActive => isActive;
    public float Damage => damage;
    
    public BulletLogic(BulletObject bulletObj)
    {
        bulletObject = bulletObj;
        isActive = false;
    }
    
    public void Initialize(Vector3 shootDirection, float bulletDamage, BulletPool bulletPool, bool isEnemyBullet = false)
    {
        direction = shootDirection.normalized;
        damage = bulletDamage;
        pool = bulletPool;
        timer = 0f;
        isActive = true;
        
        if (bulletObject != null)
        {
            bulletObject.SetColor(isEnemyBullet ? Color.red : Color.white);
            bulletObject.SetVelocity(direction * speed);
        }
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.RegisterUpdatable(this);
    }
    
    public void OnUpdate(float deltaTime)
    {
        if (!isActive) return;
        
        timer += deltaTime;
        if (timer >= lifetime)
        {
            ReturnToPool();
        }
    }
    
    public void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        
        var character = other.GetComponent<ICharacter>();
        if (character != null && character.GameObject != bulletObject.transform.root.gameObject)
        {
            character.TakeDamage(damage);
            ReturnToPool();
        }
    }
    
    public void ReturnToPool()
    {
        if (!isActive) return;
        
        isActive = false;
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
        
        if (pool != null && bulletObject != null)
        {
            pool.ReturnBullet(bulletObject.gameObject);
        }
    }
    
    public void Reset()
    {
        timer = 0f;
        isActive = false;
        direction = Vector3.zero;
        
        if (bulletObject != null)
        {
            bulletObject.Reset();
        }
    }
}