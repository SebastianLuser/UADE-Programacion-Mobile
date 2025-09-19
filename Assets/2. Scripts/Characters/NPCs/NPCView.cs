using UnityEngine;

public abstract class NPCView : MonoBehaviour, ICharacter2
{
    protected NPCController controller;
    protected Rigidbody rb;

    public GameObject GameObject => gameObject;
    public Transform Transform => transform;
    public bool IsAlive => controller?.Model.RuntimeState.isAlive ?? false;
    
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Logger.LogWarning($"{gameObject.name}: No Rigidbody found, NPC won't move properly");
        }
    }

    public void SetController(NPCController npcController)
    {
        controller = npcController;
    }
    
    public virtual void Initialize()
    {
        // Implementation from ICharacter2
    }

    public virtual void Move(Vector3 direction)
    {
        if (!IsAlive || rb == null) return;

        Vector3 movement = direction * (GetCurrentMoveSpeed() * Time.deltaTime);
        rb.MovePosition(transform.position + movement);
    }

    public virtual void TakeDamage(float damage)
    {
        controller?.TakeDamage(damage);
    }

    public virtual void MoveTo(Vector3 destination)
    {
        if (!IsAlive) return;

        Vector3 direction = (destination - transform.position).normalized;
        Move(direction);
    }

    public virtual void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    public virtual void FaceDirection(Vector3 direction)
    {
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                GetRotationSpeed() * Time.deltaTime);
        }
    }

    protected abstract float GetCurrentMoveSpeed();
    protected abstract float GetRotationSpeed();
}