using UnityEngine;

public class PlayerController : MainCharacter
{
    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayerMask = 1;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private Transform groundCheckPoint;
    
    [Header("Movement Settings")]
    [SerializeField] private float groundMoveSpeed = 5f;
    [SerializeField] private bool onlyMoveOnGround = false;
    
    private bool isGrounded;
    private CapsuleCollider capsuleCollider;
    
    protected override void Awake()
    {
        base.Awake();
        capsuleCollider = GetComponent<CapsuleCollider>();
        
        // Create ground check point if not assigned
        if (groundCheckPoint == null)
        {
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(transform);
            groundCheck.transform.localPosition = Vector3.zero;
            groundCheckPoint = groundCheck.transform;
        }
    }
    
    private void Update()
    {
        CheckGroundStatus();
    }
    
    private void CheckGroundStatus()
    {
        // Perform ground check using raycast
        Vector3 checkPosition = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        
        // Adjust check position to bottom of capsule
        if (capsuleCollider != null)
        {
            checkPosition.y = transform.position.y - (capsuleCollider.height * 0.5f) + capsuleCollider.center.y;
        }
        
        isGrounded = Physics.CheckSphere(checkPosition, groundCheckDistance, groundLayerMask);
        
        // Visual debug in scene view
        Debug.DrawRay(checkPosition, Vector3.down * groundCheckDistance, isGrounded ? Color.green : Color.red);
    }
    
    public override void Move(Vector3 direction)
    {
        if (!isAlive) return;
        
        // Simple movement without ground restrictions
        Vector3 movement = direction * groundMoveSpeed * Time.deltaTime;
        
        if (rb != null)
        {
            Vector3 newPosition = transform.position + movement;
            rb.MovePosition(newPosition);
        }
        else
        {
            transform.position += movement;
        }
        
        if (direction.magnitude > 0.1f)
        {
            lastMoveDirection = direction;
        }
    }
    
    public bool IsGrounded()
    {
        return isGrounded;
    }
    
    public void SetGroundLayerMask(LayerMask layerMask)
    {
        groundLayerMask = layerMask;
    }
    
    public void SetGroundCheckDistance(float distance)
    {
        groundCheckDistance = Mathf.Max(0.01f, distance);
    }
    
    // Override HandleInput to add ground check feedback
    public new void HandleInput(Vector2 movementInput, Vector3 shootDirection)
    {
        Vector3 movement = new Vector3(movementInput.x, 0, movementInput.y);
        
        
        // Always allow movement - no ground restrictions
        Move(movement);
        
        // Shooting
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
    
    private void OnDrawGizmosSelected()
    {
        // Draw ground check visualization in editor
        if (groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 checkPosition = groundCheckPoint.position;
            
            if (capsuleCollider != null)
            {
                checkPosition.y = transform.position.y - (capsuleCollider.height * 0.5f) + capsuleCollider.center.y;
            }
            
            Gizmos.DrawWireSphere(checkPosition, groundCheckDistance);
        }
    }
}