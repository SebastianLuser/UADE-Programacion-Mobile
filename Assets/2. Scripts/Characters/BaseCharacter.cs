using UnityEngine;

public abstract class BaseCharacter : MonoBehaviour, ICharacter
{
    [SerializeField] protected float health = 100f;
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected float shootCooldown = 0.5f;
    
    protected float lastShootTime;
    protected bool isAlive = true;
    
    public GameObject GameObject => gameObject;
    public Transform Transform => transform;
    public bool IsAlive => isAlive;
    
    protected virtual void Awake()
    {
        Initialize();
    }
    
    public virtual void Initialize()
    {
        isAlive = true;
        lastShootTime = 0f;
    }
    
    public abstract void Move(Vector3 direction);
    public abstract void Shoot(Vector3 direction);
    
    public virtual void TakeDamage(float damage)
    {
        if (!isAlive) return;
        
        health -= damage;
        if (health <= 0f)
        {
            isAlive = false;
            OnDeath();
        }
    }
    
    protected virtual void OnDeath()
    {
        gameObject.SetActive(false);
    }
    
    protected bool CanShoot()
    {
        return Time.time >= lastShootTime + shootCooldown;
    }
}