using UnityEngine;
using DevelopmentUtilities;
using AI.NPCs;
using Attributes;

namespace AI.Testing
{
    public class RouletteWheelTester : MonoBehaviour
    {
        [Header("Test Configuration")]
        public int testIterations = 1000;
        
        [Header("Behavior Weights")]
        [Range(0f, 100f)] public float fleeWeight = 70f;
        [Range(0f, 100f)] public float attackWeight = 10f;
        [Range(0f, 100f)] public float hideWeight = 15f;
        [Range(0f, 100f)] public float panicWeight = 5f;
        
        [Header("Results")]
        [ReadOnlyInspector] public int fleeCount;
        [ReadOnlyInspector] public int attackCount;
        [ReadOnlyInspector] public int hideCount;
        [ReadOnlyInspector] public int panicCount;
        
        [Header("Percentages")]
        [ReadOnlyInspector] public float fleePercentage;
        [ReadOnlyInspector] public float attackPercentage;
        [ReadOnlyInspector] public float hidePercentage;
        [ReadOnlyInspector] public float panicPercentage;

        [ContextMenu("Test Roulette Wheel")]
        public void TestRouletteWheel()
        {
            var roulette = new RouletteWheel<CivilianBehaviorType>();
            
            // Create dictionary with weights
            var behaviorDict = new System.Collections.Generic.Dictionary<CivilianBehaviorType, float>
            {
                { CivilianBehaviorType.Flee, fleeWeight },
                { CivilianBehaviorType.Attack, attackWeight },
                { CivilianBehaviorType.Hide, hideWeight },
                { CivilianBehaviorType.Panic, panicWeight }
            };

            roulette.SetCachedDictionary(behaviorDict);

            // Reset counters
            fleeCount = attackCount = hideCount = panicCount = 0;

            // Run test
            for (int i = 0; i < testIterations; i++)
            {
                var result = roulette.RunWithCached();
                switch (result)
                {
                    case CivilianBehaviorType.Flee: fleeCount++; break;
                    case CivilianBehaviorType.Attack: attackCount++; break;
                    case CivilianBehaviorType.Hide: hideCount++; break;
                    case CivilianBehaviorType.Panic: panicCount++; break;
                }
            }

            // Calculate percentages
            fleePercentage = (float)fleeCount / testIterations * 100f;
            attackPercentage = (float)attackCount / testIterations * 100f;
            hidePercentage = (float)hideCount / testIterations * 100f;
            panicPercentage = (float)panicCount / testIterations * 100f;

            // Log results
            Debug.Log($"=== ROULETTE WHEEL TEST RESULTS ({testIterations} iterations) ===");
            Debug.Log($"Flee: {fleeCount} ({fleePercentage:F1}%) - Expected: {fleeWeight:F1}%");
            Debug.Log($"Attack: {attackCount} ({attackPercentage:F1}%) - Expected: {attackWeight:F1}%");
            Debug.Log($"Hide: {hideCount} ({hidePercentage:F1}%) - Expected: {hideWeight:F1}%");
            Debug.Log($"Panic: {panicCount} ({panicPercentage:F1}%) - Expected: {panicWeight:F1}%");
            Debug.Log("===============================================");
        }

        [ContextMenu("Test with Random Weights")]
        public void TestWithRandomWeights()
        {
            fleeWeight = Random.Range(30f, 80f);
            attackWeight = Random.Range(5f, 25f);
            hideWeight = Random.Range(10f, 30f);
            panicWeight = Random.Range(2f, 15f);
            
            Debug.Log($"Testing with random weights - Flee:{fleeWeight:F1}, Attack:{attackWeight:F1}, Hide:{hideWeight:F1}, Panic:{panicWeight:F1}");
            TestRouletteWheel();
        }

        [ContextMenu("Apply Results to All Civilians")]
        public void ApplyResultsToAllCivilians()
        {
            var civilians = FindObjectsOfType<CivilianAI>();
            
            foreach (var civilian in civilians)
            {
                civilian.behaviorWeights.fleeWeight = fleeWeight;
                civilian.behaviorWeights.attackWeight = attackWeight;
                civilian.behaviorWeights.hideWeight = hideWeight;
                civilian.behaviorWeights.panicWeight = panicWeight;
            }
            
            Debug.Log($"Applied weights to {civilians.Length} civilians");
        }

        private void OnValidate()
        {
            // Auto-normalize weights if they exceed 100
            float total = fleeWeight + attackWeight + hideWeight + panicWeight;
            if (total > 100f)
            {
                float normalizer = 100f / total;
                fleeWeight *= normalizer;
                attackWeight *= normalizer;
                hideWeight *= normalizer;
                panicWeight *= normalizer;
            }
        }
    }
}