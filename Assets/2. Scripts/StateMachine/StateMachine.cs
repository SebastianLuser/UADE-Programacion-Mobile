using System;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    private readonly Dictionary<Type, IState> states = new Dictionary<Type, IState>();
    private readonly List<StateTransition> transitions = new List<StateTransition>();
    
    private IState currentState;
    private Type currentStateType;
    
    public Type CurrentStateType => currentStateType;
    public IState CurrentState => currentState;
    
    public void AddState<T>(T state) where T : IState
    {
        states[typeof(T)] = state;
    }
    
    public void AddTransition<TFrom, TTo>(Func<bool> condition) 
        where TFrom : IState 
        where TTo : IState
    {
        transitions.Add(new StateTransition(typeof(TFrom), typeof(TTo), condition));
    }
    
    public void StartState<T>() where T : IState
    {
        var stateType = typeof(T);
        if (states.ContainsKey(stateType))
        {
            ChangeState(stateType);
        }
        else
        {
            Debug.LogError($"State {stateType.Name} not found in state machine");
        }
    }
    
    public void Update()
    {
        if (currentState == null) return;
        
        currentState.Update();
        CheckTransitions();
    }
    
    private void CheckTransitions()
    {
        foreach (var transition in transitions)
        {
            if (transition.FromState == currentStateType && transition.Condition())
            {
                ChangeState(transition.ToState);
                break;
            }
        }
    }
    
    private void ChangeState(Type newStateType)
    {
        if (currentState != null)
        {
            currentState.Exit();
        }
        
        currentStateType = newStateType;
        currentState = states[newStateType];
        currentState.Enter();
    }
    
    public void ForceTransition<T>() where T : IState
    {
        ChangeState(typeof(T));
    }
}