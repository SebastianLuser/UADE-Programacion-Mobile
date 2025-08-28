using UnityEngine;

public class PatrollingState : IState
{
    private readonly Guard guard;
    
    public PatrollingState(Guard guard)
    {
        this.guard = guard;
    }
    
    public void Enter()
    {
        guard.StateTimer = 0f;
    }
    
    public void Update()
    {
        if (guard.PatrolPoints == null || guard.PatrolPoints.Length == 0) return;
        
        Vector3 targetPosition = guard.PatrolPoints[guard.CurrentPatrolIndex].position;
        Vector3 direction = (targetPosition - guard.Transform.position).normalized;
        
        guard.Move(direction * guard.PatrolSpeed);
        
        if (guard.ReachedPatrolPoint())
        {
            guard.CurrentPatrolIndex = (guard.CurrentPatrolIndex + 1) % guard.PatrolPoints.Length;
        }
    }
    
    public void Exit()
    {
    }
}