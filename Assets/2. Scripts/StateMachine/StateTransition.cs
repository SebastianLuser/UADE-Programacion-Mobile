using System;

public class StateTransition
{
    public Type FromState { get; }
    public Type ToState { get; }
    public Func<bool> Condition { get; }
    
    public StateTransition(Type fromState, Type toState, Func<bool> condition)
    {
        FromState = fromState;
        ToState = toState;
        Condition = condition;
    }
}