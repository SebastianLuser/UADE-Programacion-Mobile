using System;
using ScriptableObjects.Bullets;
using Services;
using Services.MicroServices.PoolObjectsService;
using UnityEngine;

public class MainCharacter : BaseCharacter, ICombat
{
    [SerializeField] private MainCharacterDataSO mainCharacterData;
    
    private Rigidbody rb;
    private Vector3 lastMoveDirection;
    private PlayerCollector playerCollector;

    private float RotationSpeed => mainCharacterData?.rotationSpeed ?? characterData.rotationSpeed;
    private BulletData BulletData => mainCharacterData?.bulletData;

    private static IPoolObjectsService PoolObjectsService => ServiceLocator.Get<IPoolObjectsService>();

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        playerCollector = GetComponent<PlayerCollector>();
        
        if (rb == null)
        {
            MyLogger.LogError($"{gameObject.name}: Rigidbody component required for MainCharacter2!");
        }
    }
    
    public override void Move(Vector3 direction)
    {
        if (!isAlive || rb == null) return;
        
        Vector3 movement = direction * (characterData.moveSpeed * Time.deltaTime);
        rb.MovePosition(transform.position + movement);
        
        if (direction.magnitude > 0.1f)
        {
            lastMoveDirection = direction;
        }
    }
    
    public override void Shoot(Vector3 direction)
    {
        if (!isAlive || !CanShoot()) return;
        
        lastShootTime = Time.time;
        CreateBullet(direction);
    }
    
    public bool CanShoot()
    {
        return Time.time >= lastShootTime + characterData.shootCooldown;
    }
    
    private void CreateBullet(Vector3 p_direction)
    {
        if (BulletData == null)
        {
            MyLogger.LogWarning($"{gameObject.name}: BulletData not assigned, cannot shoot!");
            return;
        }
        var l_spawnPosition = transform.position + Vector3.up * 0.5f + p_direction * 0.8f;
        var l_bullet = PoolObjectsService.GetOrCreateObject(BulletData.Prefab);
        l_bullet.OnDeactivate += OnDeactivateBulletHandler;
        l_bullet.InitializeBullet(BulletData, l_spawnPosition, p_direction);
    }

    private static void OnDeactivateBulletHandler(BulletObject p_bullet)
    {
        p_bullet.OnDeactivate -= OnDeactivateBulletHandler;
        PoolObjectsService.ReturnObject(p_bullet);
    }

    public void HandleInput(Vector2 movementInput, Vector3 shootDirection)
    {
        Vector3 movement = new Vector3(movementInput.x, 0, movementInput.y);
        Move(movement);
        
        if (shootDirection.magnitude > 0.1f)
        {
            Shoot(shootDirection);
            RotateTowards(shootDirection);
        }
    }
    
    private void RotateTowards(Vector3 direction)
    {
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);
        }
    }

    public override void TakeDamage(float damage)
    {
        if (!isAlive) return;

        currentHealth -= damage;
        playerCollector?.SyncHealth(currentHealth, MaxHealth);

        if (currentHealth <= 0f)
        {
            isAlive = false;
            OnDeath();
        }
    }

    protected override void OnDeath()
    {
        if (playerCollector != null)
        {
            playerCollector.HandleDeath();
        }
    }
}
