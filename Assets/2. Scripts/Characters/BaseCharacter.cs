using UnityEngine;
using UnityEngine.Assertions;

public abstract class BaseCharacter : MonoBehaviour, ICharacter
{
    [SerializeField] protected CharacterDataSO characterData;
    
    protected float currentHealth;
    protected float lastShootTime;
    protected bool isAlive = true;
    
    public float Health => currentHealth;
    protected float MoveSpeed => characterData?.moveSpeed ?? 5f;
    private float ShootCooldown => characterData?.shootCooldown ?? 0.5f;
    
    public GameObject GameObject => gameObject;
    public Transform Transform => transform;
    public bool IsAlive => isAlive;
    
    protected virtual void Awake()
    {
        Assert.IsNotNull(characterData);
        Initialize();
    }
    
    public virtual void Initialize()
    {
        if (characterData != null)
        {
            currentHealth = characterData.maxHealth;
        }
        else
        {
            currentHealth = 100f;
            Logger.LogWarning($"{gameObject.name}: No CharacterDataSO assigned, using default values");
        }
        
        isAlive = true;
        lastShootTime = 0f;
    }
    
    public abstract void Move(Vector3 direction);
    public abstract void Shoot(Vector3 direction);
    
    public virtual void TakeDamage(float damage)
    {
        if (!isAlive) return;
        
        currentHealth -= damage;
        if (currentHealth <= 0f)
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
        return Time.time >= lastShootTime + ShootCooldown;
    }
}