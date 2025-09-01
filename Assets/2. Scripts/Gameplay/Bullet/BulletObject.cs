using UnityEngine;

public class BulletObject : MonoBehaviour
{
    private BulletLogic bulletLogic;
    private Rigidbody rb;
    private Renderer bulletRenderer;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletRenderer = GetComponent<Renderer>();
        bulletLogic = new BulletLogic(this);
    }
    
    public void InitializeBullet(Vector3 shootDirection, float damage, BulletPool pool, bool isEnemyBullet = false)
    {
        bulletLogic?.Initialize(shootDirection, damage, pool, isEnemyBullet);
    }
    
    public void SetVelocity(Vector3 velocity)
    {
        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }
    
    public void SetColor(Color color)
    {
        if (bulletRenderer != null && bulletRenderer.material != null)
        {
            bulletRenderer.material.color = color;
        }
    }
    
    public void Reset()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        bulletLogic?.OnTriggerEnter(other);
    }
    
    private void OnDisable()
    {
        bulletLogic?.Reset();
    }
    
    public BulletLogic GetLogic()
    {
        return bulletLogic;
    }
}