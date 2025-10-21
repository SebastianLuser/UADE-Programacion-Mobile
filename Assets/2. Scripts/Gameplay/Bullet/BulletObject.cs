using System;
using ScriptableObjects.Bullets;
using Services;
using Services.MicroServices.UpdateService;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Renderer))]
public class BulletObject : MonoBehaviour, IUpdateListener
{
    private BulletData m_bulletData;
    private Vector3 m_direction;
    private float m_timer;
    private bool m_isActive;

    private Rigidbody m_rb;
    private Renderer m_bulletRenderer;

    public event Action<BulletObject> OnDeactivate;
    
    private static IUpdateService UpdateService => ServiceLocator.Get<IUpdateService>();

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_bulletRenderer = GetComponent<Renderer>();
    }

    public void InitializeBullet(BulletData p_bulletData, Vector3 p_spawnPoint, Vector3 p_shootDirection)
    {
        transform.position = p_spawnPoint;
        
        m_bulletData = p_bulletData;
        m_direction = p_shootDirection.normalized;
        m_timer = 0f;
        m_isActive = true;
        
        m_rb.useGravity = m_bulletData.UseGravity;
        m_rb.linearVelocity = m_direction * m_bulletData.Speed;
        m_bulletRenderer.material.color = m_bulletData.Color;

        SubscribeUpdateService();
        
        gameObject.SetActive(true);
    }

    private void OnTriggerEnter(Collider p_other)
    {
        m_isActive = true;
        if (!m_isActive)
            return;

        if (!p_other.TryGetComponent<BaseCharacter>(out var l_character))
            return;

        if (l_character.GameObject == gameObject)
            return;

//        l_character.TakeDamage(m_bulletData.Damage);
        l_character.TakeDamage(25f);
        Deactivate();
    }

    private void Deactivate()
    {
        m_rb.linearVelocity = Vector3.zero;
        m_rb.angularVelocity = Vector3.zero;
        
        UnsubscribeUpdateService();
        
        gameObject.SetActive(false);
        OnDeactivate?.Invoke(this);
    }

    private void OnDestroy()
    {
        UnsubscribeUpdateService();
    }

    public void MyUpdate()
    {
        if (!m_isActive) return;

        m_timer += Time.deltaTime;
        if (m_timer >= m_bulletData.Lifetime)
        {
            Deactivate();
        }
    }

    public void SubscribeUpdateService()
    {
        UpdateService.AddUpdateListener(this);
    }

    public void UnsubscribeUpdateService()
    {
        UpdateService.RemoveUpdateListener(this);
    }
}