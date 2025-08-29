using UnityEngine;

public class SearchingState : IState
{
    private readonly Guard guard;
    
    public SearchingState(Guard guard)
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
        
        Vector3 searchDirection = (guard.LastKnownPlayerPosition - guard.Transform.position).normalized;
        
        if (Vector3.Distance(guard.Transform.position, guard.LastKnownPlayerPosition) > 1f)
        {
            guard.Move(searchDirection * guard.PatrolSpeed);
        }
        else
        {
            float angle = guard.StateTimer * 45f;
            Vector3 lookDirection = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            guard.Transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
    
    public void Exit()
    {
    }
}