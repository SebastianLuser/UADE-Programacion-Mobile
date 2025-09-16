using UnityEngine;

public class MainCharacter2 : Character, ICombat
{
    [SerializeField] private MainCharacterDataSO mainCharacterData;
    
    private float lastShootTime;
    private Rigidbody rb;
    private Vector3 lastMoveDirection;
    
    private float RotationSpeed => mainCharacterData?.rotationSpeed ?? characterData.rotationSpeed;
    private BulletDataSO BulletData => mainCharacterData?.bulletData;
    
    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        
        if (rb == null)
        {
            Logger.LogError($"{gameObject.name}: Rigidbody component required for MainCharacter2!");
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
    
    public void Shoot(Vector3 direction)
    {
        if (!isAlive || !CanShoot()) return;
        
        lastShootTime = Time.time;
        CreateBullet(direction);
    }
    
    public bool CanShoot()
    {
        return Time.time >= lastShootTime + characterData.shootCooldown;
    }
    
    private void CreateBullet(Vector3 direction)
    {
        if (BulletData == null)
        {
            Logger.LogWarning($"{gameObject.name}: BulletData not assigned, cannot shoot!");
            return;
        }
        
        var poolService = ServiceLocator.Get<ObjectPoolService>();
        if (poolService != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolService.GetBullet(spawnPosition, direction, BulletData.speed, false);
        }
        else
        {
            // Fallback creation
            GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObj.name = "PlayerBullet";
            bulletObj.transform.position = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            bulletObj.transform.localScale = BulletData.scale;
            
            var bulletRb = bulletObj.AddComponent<Rigidbody>();
            bulletRb.useGravity = BulletData.useGravity;
            
            var bulletCollider = bulletObj.GetComponent<Collider>();
            bulletCollider.isTrigger = BulletData.isTrigger;
            
            var bulletObject = bulletObj.AddComponent<BulletObject>();
            bulletObject.InitializeBullet(direction, BulletData.speed, null);
        }
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
}