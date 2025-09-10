using UnityEngine;

public class MainCharacter : BaseCharacter
{
    [SerializeField] private MainCharacterDataSO mainCharacterData;
    
    private float RotationSpeed => mainCharacterData?.rotationSpeed ?? characterData?.rotationSpeed ?? 10f;
    private BulletDataSO BulletData => mainCharacterData?.bulletData;
    
    private Rigidbody rb;
    private Camera mainCamera;
    private Vector3 lastMoveDirection;
    
    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        
        var inputManager = ServiceLocator.Get<InputManager>();
        if (inputManager != null)
        {
            mainCamera = inputManager.MainCamera;
        }
        else
        {
            mainCamera = Camera.main;
        }
    }
    
    public override void Move(Vector3 direction)
    {
        if (!isAlive) return;
        
        Vector3 movement = direction * (MoveSpeed * Time.deltaTime);
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
    
    private void CreateBullet(Vector3 direction)
    {
        float bulletSpeed = BulletData?.speed ?? 25f;
        
        var poolService = ServiceLocator.Get<ObjectPoolService>();
        if (poolService != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolService.GetBullet(spawnPosition, direction, bulletSpeed, false);
        }
        else
        {
            // Fallback to creating bullet manually if service not available
            GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObj.name = "Bullet";
            bulletObj.transform.position = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            bulletObj.transform.localScale = BulletData?.scale ?? Vector3.one * 0.2f;
            
            var bulletRb = bulletObj.AddComponent<Rigidbody>();
            bulletRb.useGravity = BulletData?.useGravity ?? false;
            
            var bulletCollider = bulletObj.GetComponent<Collider>();
            bulletCollider.isTrigger = BulletData?.isTrigger ?? true;
            
            var bulletObject = bulletObj.AddComponent<BulletObject>();
            bulletObject.InitializeBullet(direction, bulletSpeed, null);
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