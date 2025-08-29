using UnityEngine;

public class AttackingState : IState
{
    private readonly Guard guard;
    
    public AttackingState(Guard guard)
    {
        this.guard = guard;
    }
    
    public void Enter()
    {
        guard.StateTimer = 0f;
    }
    
    public void Update()
    {
        if (guard.Player == null) return;
        
        Vector3 directionToPlayer = (guard.Player.position - guard.Transform.position).normalized;
        guard.Transform.rotation = Quaternion.LookRotation(directionToPlayer);
        
        guard.Shoot(directionToPlayer);
    }
    
    public void Exit()
    {
    }
}