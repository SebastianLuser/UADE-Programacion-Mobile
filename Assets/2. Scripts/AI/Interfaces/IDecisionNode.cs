namespace AI.Interfaces
{
    public enum AIState
    {
        Die,
        Flee,
        Pursuit,
        Attack,
        Patrol,
        Idle
    }

    public interface IDecisionNode
    {
        AIState Execute(IAIBlackboard blackboard);
    }
}