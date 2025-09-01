using UnityEngine;

public class MainCharacter : BaseCharacter
{
    [SerializeField] private float rotationSpeed = 10f;
    
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