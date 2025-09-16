using UnityEngine;

public class NPCRuntimeState
{
    public float currentHealth;
    public Vector3 lastKnownPlayerPosition;
    public Transform currentTarget;
    public bool isPlayerVisible;
    public float stateTimer;
    public int currentPatrolIndex;
    public bool isAlive = true;
    
    public void Initialize(float maxHealth)
    {
        currentHealth = maxHealth;
        isAlive = true;
        stateTimer = 0f;
        currentPatrolIndex = 0;
        isPlayerVisible = false;
        currentTarget = null;
        lastKnownPlayerPosition = Vector3.zero;
    }
    
    public void ResetStateTimer()
    {
        stateTimer = 0f;
    }
    
    public void TakeDamage(float damage)
    {
        if (!isAlive) return;
        
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            isAlive = false;
            currentHealth = 0f;
        }
    }
}