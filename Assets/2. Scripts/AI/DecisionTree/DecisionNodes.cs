using System;
using UnityEngine;
using AI.Interfaces;

namespace AI.DecisionTree
{
    public class ConditionNode : IDecisionNode
    {
        private readonly Func<IAIBlackboard, bool> condition;
        private readonly IDecisionNode trueNode;
        private readonly IDecisionNode falseNode;
        private readonly Action<IAIContext> onEvaluate;

        public ConditionNode(Func<IAIBlackboard, bool> condition, IDecisionNode trueNode, IDecisionNode falseNode, Action<IAIContext> onEvaluate = null)
        {
            this.condition = condition;
            this.trueNode = trueNode;
            this.falseNode = falseNode;
            this.onEvaluate = onEvaluate;
        }

        public AIState Execute(IAIBlackboard blackboard)
        {
            // Execute side effect if context is available
            if (onEvaluate != null && blackboard is IAIContext context)
            {
                onEvaluate.Invoke(context);
            }

            return condition(blackboard) ? trueNode.Execute(blackboard) : falseNode.Execute(blackboard);
        }
    }

    public class ActionNode : IDecisionNode
    {
        private readonly AIState state;
        private readonly Action<IAIContext> onExecute;

        public ActionNode(AIState state, Action<IAIContext> onExecute = null)
        {
            this.state = state;
            this.onExecute = onExecute;
        }

        public AIState Execute(IAIBlackboard blackboard)
        {
            // Execute side effect action
            if (onExecute != null && blackboard is IAIContext context)
            {
                onExecute.Invoke(context);
            }
            
            return state;
        }
    }

    public class ComplexActionNode : IDecisionNode
    {
        private readonly AIState state;
        private readonly Action<IAIContext> onEnter;
        private readonly Action<IAIContext> onExecute;
        private readonly Func<IAIContext, bool> canExecute;

        public ComplexActionNode(AIState state, 
            Action<IAIContext> onEnter = null,
            Action<IAIContext> onExecute = null, 
            Func<IAIContext, bool> canExecute = null)
        {
            this.state = state;
            this.onEnter = onEnter;
            this.onExecute = onExecute;
            this.canExecute = canExecute;
        }

        public AIState Execute(IAIBlackboard blackboard)
        {
            if (blackboard is IAIContext context)
            {
                // Check if we can execute this action
                if (canExecute != null && !canExecute(context))
                {
                    return AIState.Idle; // Fallback state
                }

                // Execute enter behavior once
                onEnter?.Invoke(context);
                
                // Execute main behavior
                onExecute?.Invoke(context);
            }
            
            return state;
        }
    }
}