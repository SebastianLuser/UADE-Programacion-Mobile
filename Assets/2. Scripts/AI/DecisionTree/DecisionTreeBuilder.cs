using UnityEngine;
using AI.Interfaces;

namespace AI.DecisionTree
{
    public static class DecisionTreeBuilder
    {
        public static IDecisionNode BuildStandardTree()
        {
            return new ConditionNode(
                bb => bb.CheckIsAlive(),
                // If alive
                new ConditionNode(
                    bb => bb.CheckPlayerInSight(),
                    // If player in sight
                    trueNode: new ConditionNode(
                        bb => bb.CheckLowHealth(),
                        // If low health -> Flee
                        trueNode: new ActionNode(AIState.Flee, ctx => {
                            Debug.Log("AI is fleeing due to low health!");
                            ctx.Animation.SetTrigger("Panic");
                        }),
                        // If not low health
                        falseNode: new ConditionNode(
                            bb => bb.CheckPlayerInAttackRange(),
                            trueNode: new ComplexActionNode(AIState.Attack,
                                onEnter: ctx => {
                                    ctx.Animation.SetBool("InCombat", true);
                                    Debug.Log("Entering combat mode!");
                                },
                                canExecute: ctx => ctx.Blackboard.IsPlayerInSight
                            ),
                            falseNode: new ActionNode(AIState.Pursuit, ctx => {
                                ctx.Animation.SetBool("IsChasing", true);
                                ctx.Blackboard.LastKnownPlayerPosition = ctx.PlayerDetector.GetPlayerPosition();
                            })
                        )
                    ),
                    // If player not in sight
                    falseNode: new ConditionNode(
                        bb => bb.CheckArrivedAtPoint(),
                        trueNode: new ActionNode(AIState.Idle, ctx => {
                            Debug.Log("AI resting at patrol point");
                        }),
                        falseNode: new ActionNode(AIState.Patrol, ctx => {
                            if (ctx.Blackboard.PatrolTarget == Vector3.zero)
                            {
                                ctx.SetRandomPatrolTarget();
                            }
                        })
                    )
                ),
                // If not alive
                falseNode: new ComplexActionNode(AIState.Die,
                    onEnter: ctx => {
                        Debug.Log("AI has died");
                        ctx.Animation.SetTrigger("Die");
                        // Disable components
                        if (ctx.Transform.GetComponent<Collider>() != null)
                        {
                            ctx.Transform.GetComponent<Collider>().enabled = false;
                        }
                    },
                    canExecute: ctx => ctx.Blackboard.CurrentHealth <= 0
                )
            );
        }

        // Alternative aggressive tree
        public static IDecisionNode BuildAggressiveTree()
        {
            return new ConditionNode(
                bb => bb.CheckIsAlive(),
                // Aggressive behavior - always pursue if player in sight
                new ConditionNode(
                    bb => bb.CheckPlayerInSight(),
                    trueNode: new ConditionNode(
                        bb => bb.CheckPlayerInAttackRange(),
                        trueNode: new ActionNode(AIState.Attack, ctx => {
                            ctx.Animation.SetFloat("AttackSpeed", 1.5f); // Faster attacks
                        }),
                        falseNode: new ActionNode(AIState.Pursuit)
                    ),
                    falseNode: new ActionNode(AIState.Patrol)
                ),
                falseNode: new ActionNode(AIState.Die)
            );
        }

        // Defensive/cowardly tree
        public static IDecisionNode BuildDefensiveTree()
        {
            return new ConditionNode(
                bb => bb.CheckIsAlive(),
                new ConditionNode(
                    bb => bb.CheckPlayerInSight(),
                    // Always flee when player is seen (cowardly AI)
                    trueNode: new ActionNode(AIState.Flee, ctx => {
                        ctx.Animation.SetBool("IsScared", true);
                    }),
                    falseNode: new ActionNode(AIState.Patrol)
                ),
                falseNode: new ActionNode(AIState.Die)
            );
        }
    }
}