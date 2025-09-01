using UnityEngine;

public class IdleState : IState
{
    private readonly Guard guard;
    
    public IdleState(Guard guard)
    {
        this.guard = guard;
    }
    
    public void Enter()
    {
        guard.StateTimer = 0f;
    }
    
    public void Update()
    {
        guard.StateTimer += Time.deltaTime;
    }
    
    public void Exit()
    {
    }
}