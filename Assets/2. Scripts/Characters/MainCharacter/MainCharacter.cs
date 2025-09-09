using UnityEngine;

public class MainCharacter : BaseCharacter
{
    [SerializeField] protected float rotationSpeed = 10f;
    
    protected Rigidbody rb;
    private Camera mainCamera;
    protected Vector3 lastMoveDirection;
    
    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        
        // Try to get InputManager, but don't error if not available yet
        if (ServiceLocator.TryGet<InputManager>(out var inputManager))
        {
            mainCamera = inputManager.MainCamera;
        }
        else
        {
            // Fallback to Camera.main if InputManager not ready yet
            mainCamera = Camera.main;
            
            // If no main camera exists, we'll set it up later when InputManager initializes
            if (mainCamera == null)
            {
                Debug.LogWarning("No main camera found. Will be set up when InputManager initializes.");
            }
        }
    }
    
    public override void Move(Vector3 direction)
    {
        if (!isAlive) return;
        
        Vector3 movement = direction * moveSpeed * Time.deltaTime;
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
        var poolManager = ServiceLocator.Get<ObjectPoolManager>();
        if (poolManager != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolManager.GetBullet(spawnPosition, direction, 25f, false);
        }
        else if (BulletPool.Instance != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            BulletPool.Instance.GetBullet(spawnPosition, direction, 25f, false);
        }
        else
        {
            GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObj.name = "Bullet";
            bulletObj.transform.position = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            bulletObj.transform.localScale = Vector3.one * 0.2f;
            
            var bulletRb = bulletObj.AddComponent<Rigidbody>();
            bulletRb.useGravity = false;
            
            var bulletCollider = bulletObj.GetComponent<Collider>();
            bulletCollider.isTrigger = true;
            
            var bulletObject = bulletObj.AddComponent<BulletObject>();
            bulletObject.InitializeBullet(direction, 25f, null);
        }
    }
    
    public void HandleInput(Vector2 movementInput, Vector3 shootDirection)
    {
        // Ensure we have a camera reference
        if (mainCamera == null && ServiceLocator.TryGet<InputManager>(out var inputManager))
        {
            mainCamera = inputManager.MainCamera;
        }
        
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
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}