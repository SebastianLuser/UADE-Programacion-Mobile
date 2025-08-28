using UnityEngine;

public class ChasingState : IState
{
    private readonly Guard guard;
    
    public ChasingState(Guard guard)
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
        
        guard.LastKnownPlayerPosition = guard.Player.position;
        
        Vector3 direction = (guard.Player.position - guard.Transform.position).normalized;
        guard.Move(direction * guard.ChaseSpeed);
    }
    
    public void Exit()
    {
    }
}