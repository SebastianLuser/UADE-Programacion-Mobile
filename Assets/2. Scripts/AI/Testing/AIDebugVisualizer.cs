using UnityEngine;
using AI.Core;
using AI.NPCs;

namespace AI.Testing
{
    public class AIDebugVisualizer : MonoBehaviour
    {
        [Header("Debug Settings")]
        public bool showSightRanges = true;
        public bool showAttackRanges = true;
        public bool showPatrolTargets = true;
        public bool showHealthBars = true;
        public bool showStateLabels = true;
        public bool showBehaviorWeights = false;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            DrawBasicAIGizmos();
            DrawCivilianAIGizmos();
        }

        private void DrawBasicAIGizmos()
        {
            var basicAIs = FindObjectsOfType<MobileOptimizedAI>();

            foreach (var ai in basicAIs)
            {
                if (ai.Blackboard == null) continue;

                Vector3 pos = ai.transform.position;

                // Sight Range
                if (showSightRanges)
                {
                    Gizmos.color = ai.Blackboard.IsPlayerInSight ? Color.red : Color.yellow;
                    Gizmos.DrawWireSphere(pos, ai.Blackboard.SightRange);
                }

                // Attack Range
                if (showAttackRanges)
                {
                    Gizmos.color = ai.Blackboard.CheckPlayerInAttackRange() ? Color.red : Color.orange;
                    Gizmos.DrawWireSphere(pos, ai.Blackboard.AttackRange);
                }

                // Patrol Target
                if (showPatrolTargets && ai.Blackboard.PatrolTarget != Vector3.zero)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(pos, ai.Blackboard.PatrolTarget);
                    Gizmos.DrawSphere(ai.Blackboard.PatrolTarget, 0.3f);
                }

                // Last Known Player Position
                if (ai.Blackboard.LastKnownPlayerPosition != Vector3.zero)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(pos, ai.Blackboard.LastKnownPlayerPosition);
                    Gizmos.DrawCube(ai.Blackboard.LastKnownPlayerPosition, Vector3.one * 0.2f);
                }

                // Health Bar
                if (showHealthBars)
                {
                    DrawHealthBar(pos + Vector3.up * 2.2f, ai.Blackboard.CurrentHealth, ai.Blackboard.MaxHealth);
                }

                // State Label  
                if (showStateLabels)
                {
                    var aiState = GetCurrentAIState(ai);
                    var patrolMode = GetPatrolMode(ai);
                    DrawLabel(pos + Vector3.up * 3.2f, $"State: {aiState} | {patrolMode}");
                }
            }
        }

        private void DrawCivilianAIGizmos()
        {
            var civilians = FindObjectsOfType<CivilianAI>();

            foreach (var civilian in civilians)
            {
                if (civilian.Blackboard == null) continue;

                Vector3 pos = civilian.transform.position;

                // Detection Range
                if (showSightRanges)
                {
                    Gizmos.color = civilian.Blackboard.IsPlayerInSight ? Color.red : Color.green;
                    var civilianScript = civilian.GetComponent<CivilianAI>();
                    if (civilianScript != null)
                    {
                        Gizmos.DrawWireSphere(pos, civilianScript.detectionRange);
                    }
                }

                // Health Bar
                if (showHealthBars)
                {
                    DrawHealthBar(pos + Vector3.up * 2.2f, civilian.Blackboard.CurrentHealth, civilian.Blackboard.MaxHealth);
                }

                // State Label for Civilians
                if (showStateLabels)
                {
                    var civilianScript = civilian.GetComponent<CivilianAI>();
                    if (civilianScript != null)
                    {
                        string stateText = GetCivilianStateText(civilianScript);
                        DrawLabel(pos + Vector3.up * 3.2f, stateText);
                    }
                }

                // Escape Route Visualization
                var civScript = civilian.GetComponent<CivilianAI>();
                if (civScript != null && civScript.IsEscaping && civScript.TargetEscapePoint != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(pos, civScript.TargetEscapePoint.position);
                    Gizmos.DrawWireSphere(civScript.TargetEscapePoint.position, 1f);
                }

                // Behavior Weights (if enabled)
                if (showBehaviorWeights)
                {
                    var civilianScript = civilian.GetComponent<CivilianAI>();
                    if (civilianScript != null)
                    {
                        string weightsText = $"F:{civilianScript.behaviorWeights.fleeWeight:F0} " +
                                           $"A:{civilianScript.behaviorWeights.attackWeight:F0} " +
                                           $"H:{civilianScript.behaviorWeights.hideWeight:F0} " +
                                           $"P:{civilianScript.behaviorWeights.panicWeight:F0}";
                        DrawLabel(pos + Vector3.up * 4.2f, weightsText);
                    }
                }
            }
        }

        private void DrawHealthBar(Vector3 position, float currentHealth, float maxHealth)
        {
            float healthPercentage = Mathf.Clamp01(currentHealth / maxHealth);
            float barWidth = 2f;
            float barHeight = 0.2f;

            // Background
            Gizmos.color = Color.red;
            Gizmos.DrawCube(position, new Vector3(barWidth, barHeight, 0.1f));

            // Health
            Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercentage);
            Vector3 healthBarSize = new Vector3(barWidth * healthPercentage, barHeight, 0.1f);
            Vector3 healthBarPos = position + Vector3.left * (barWidth * (1f - healthPercentage) * 0.5f);
            Gizmos.DrawCube(healthBarPos, healthBarSize);

            // Health text
            DrawLabel(position + Vector3.up * 0.3f, $"{currentHealth:F0}/{maxHealth:F0}");
        }

        private void DrawLabel(Vector3 position, string text)
        {
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
            #endif
        }

        private string GetCurrentAIState(MobileOptimizedAI ai)
        {
            // Try to get current state through reflection or other means
            // This is a simplified version - in a real implementation you might expose this publicly
            if (ai.Blackboard.CheckIsAlive())
            {
                if (ai.Blackboard.CheckPlayerInSight())
                {
                    if (ai.Blackboard.CheckLowHealth())
                        return "Flee";
                    else if (ai.Blackboard.CheckPlayerInAttackRange())
                        return "Attack";
                    else
                        return "Pursuit";
                }
                else
                {
                    if (ai.Blackboard.CheckArrivedAtPoint())
                        return "Idle";
                    else
                        return "Patrol";
                }
            }
            else
            {
                return "Die";
            }
        }

        private string GetPatrolMode(MobileOptimizedAI ai)
        {
            try
            {
                var useRandomField = ai.GetType().GetField("useRandomPatrol", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bool useRandom = (bool)(useRandomField?.GetValue(ai) ?? false);
                
                var indexField = ai.GetType().GetField("currentPatrolIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int currentIndex = (int)(indexField?.GetValue(ai) ?? 0);
                
                return useRandom ? $"Random" : $"Seq-{currentIndex}";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetCivilianStateText(CivilianAI civilian)
        {
            string behaviorText = civilian.CurrentBehaviorChoice.ToString();
            string stateText = civilian.CurrentState.ToString();
            
            if (civilian.IsEscaping)
            {
                string targetName = civilian.TargetEscapePoint?.name ?? "Unknown";
                return $"ESCAPING to {targetName}";
            }
            else if (civilian.Blackboard.IsPlayerInSight)
            {
                return $"{behaviorText} | {stateText}";
            }
            else
            {
                return "WANDERING";
            }
        }

        [ContextMenu("Toggle All Debug")]
        public void ToggleAllDebug()
        {
            bool newState = !showSightRanges;
            showSightRanges = newState;
            showAttackRanges = newState;
            showPatrolTargets = newState;
            showHealthBars = newState;
            showStateLabels = newState;
        }
    }
}