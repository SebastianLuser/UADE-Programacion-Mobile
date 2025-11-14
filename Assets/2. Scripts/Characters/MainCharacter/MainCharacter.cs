using System;
using System.Collections;
using ScriptableObjects.Bullets;
using Services;
using Services.MicroServices.AudioService;
using Services.MicroServices.PoolObjectsService;
using Services.MicroServices.UserDataService.PlayerUpgrades;
using UnityEngine;

public class MainCharacter : BaseCharacter, ICombat
{
    [SerializeField] private MainCharacterDataSO mainCharacterData;

    private float lastShootTime;
    private Rigidbody rb;
    private Vector3 lastMoveDirection;
    private PlayerCollector playerCollector;
    private float currentMagSize;
    private IPlayerUpgradeService m_upgradeService;
    private IAudioService m_audioService;
    private AudioConfig m_audioConfig;

    private float RotationSpeed => mainCharacterData?.rotationSpeed ?? characterData.rotationSpeed;
    private BulletData BulletData => mainCharacterData?.bulletData;

    private static IPoolObjectsService PoolObjectsService => ServiceLocator.Get<IPoolObjectsService>();

    protected override void Awake()
    {
        PrepareRuntimeData();
        base.Awake();
        rb = GetComponent<Rigidbody>();
        playerCollector = GetComponent<PlayerCollector>();
        currentMagSize = mainCharacterData != null ? mainCharacterData.magSize : currentMagSize;

        m_audioService = ServiceLocator.Get<IAudioService>();
        m_audioConfig = (m_audioService as AudioService)?.Config;

        if (rb == null)
        {
            MyLogger.LogError($"{gameObject.name}: Rigidbody component required for MainCharacter2!");
        }
    }

    private void PrepareRuntimeData()
    {
        if (mainCharacterData != null)
        {
            var cloned = ScriptableObject.Instantiate(mainCharacterData);
            if (cloned.bulletData != null)
            {
                cloned.bulletData = ScriptableObject.Instantiate(cloned.bulletData);
            }
            mainCharacterData = cloned;
            characterData = cloned;
        }
        else if (characterData != null)
        {
            characterData = ScriptableObject.Instantiate(characterData);
        }

        m_upgradeService = ServiceLocator.Get<IPlayerUpgradeService>();
        if (m_upgradeService != null && mainCharacterData != null)
        {
            m_upgradeService.ApplyUpgrades(mainCharacterData);
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
        currentMagSize--;

        if (m_audioService != null && m_audioConfig != null)
        {
            m_audioService.PlaySFX(m_audioConfig.playerPistolSingleShotSFX);
        }
    }
    
    public bool CanShoot()
    {
        if (mainCharacterData.magSize != 0)
        {
            return Time.time >= lastShootTime + characterData.shootCooldown;   
        }
        else
        {
            return Time.time >= lastShootTime + mainCharacterData.reloadTime;
            StartCoroutine(ReloadGun());
        }
    }
    
    private IEnumerator ReloadGun()
    {
        yield return new WaitForSeconds(mainCharacterData.reloadTime);

        currentMagSize = mainCharacterData.magSize;
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
