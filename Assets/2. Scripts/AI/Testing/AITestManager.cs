using UnityEngine;
using AI.Core;
using AI.NPCs;
using AI.Integration;

namespace AI.Testing
{
    public class AITestManager : MonoBehaviour
    {
        [Header("Test Configuration")]
        public GameObject basicAIPrefab;
        public GameObject civilianAIPrefab;
        public GameObject playerPrefab;
        public Transform[] patrolPoints = new Transform[4];
        public Transform[] escapePoints = new Transform[3]; // Escape points for civilians
        
        [Header("Spawn Settings")]
        public int numberOfBasicAIs = 3;
        public int numberOfCivilians = 5;
        public float spawnRadius = 10f;
        
        [Header("Test Controls")]
        public KeyCode spawnBasicAIKey = KeyCode.Alpha1;
        public KeyCode spawnCivilianKey = KeyCode.Alpha2;
        public KeyCode damageAllKey = KeyCode.Alpha3;
        public KeyCode resetSceneKey = KeyCode.R;
        public KeyCode toggleDebugKey = KeyCode.Tab;
        
        private GameObject currentPlayer;
        private AIDebugVisualizer debugVisualizer;

        private void Start()
        {
            SetupTestScene();
            SetupDebugVisualizer();
        }

        private void Update()
        {
            HandleTestInputs();
        }

        [ContextMenu("Setup Test Scene")]
        public void SetupTestScene()
        {
            Debug.Log("Setting up AI Test Scene...");
            
            // Create player if doesn't exist
            if (currentPlayer == null)
            {
                CreateTestPlayer();
            }
            
            // Create patrol points if not assigned
            if (patrolPoints[0] == null)
            {
                CreatePatrolPoints();
            }
            
            // Create escape points if not assigned
            if (escapePoints.Length == 0 || escapePoints[0] == null)
            {
                CreateEscapePoints();
            }
            
            // Spawn initial AIs
            SpawnTestAIs();
            
            Debug.Log("Test scene setup complete!");
        }

        private void SetupDebugVisualizer()
        {
            // Check if debug visualizer already exists
            debugVisualizer = FindObjectOfType<AIDebugVisualizer>();
            
            if (debugVisualizer == null)
            {
                // Create debug visualizer
                GameObject debugObj = new GameObject("AI Debug Visualizer");
                debugVisualizer = debugObj.AddComponent<AIDebugVisualizer>();
                
                // Enable all debug features by default
                debugVisualizer.showSightRanges = true;
                debugVisualizer.showAttackRanges = false; // Less clutter
                debugVisualizer.showPatrolTargets = false; // Less clutter
                debugVisualizer.showHealthBars = true;
                debugVisualizer.showStateLabels = true;
                debugVisualizer.showBehaviorWeights = false; // Can enable if needed
                
                Debug.Log("AI Debug Visualizer created and configured");
            }
            
            LogInstructions();
        }

        private void CreateTestPlayer()
        {
            if (GameObject.FindWithTag("Player") != null)
            {
                currentPlayer = GameObject.FindWithTag("Player");
                return;
            }

            // Create simple player
            currentPlayer = new GameObject("TestPlayer");
            currentPlayer.transform.position = Vector3.zero;
            currentPlayer.tag = "Player";
            
            // Add basic movement
            var testMovement = currentPlayer.AddComponent<SimplePlayerMovement>();
            
            // Visual representation
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(currentPlayer.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.GetComponent<Renderer>().material.color = Color.blue;
            
            Debug.Log("Test player created at origin");
        }

        private void CreatePatrolPoints()
        {
            GameObject patrolParent = new GameObject("PatrolPoints");
            
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                GameObject point = new GameObject($"PatrolPoint_{i}");
                point.transform.SetParent(patrolParent.transform);
                
                // Arrange in a square pattern
                float angle = (i * 90f) * Mathf.Deg2Rad;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * 8f,
                    0,
                    Mathf.Sin(angle) * 8f
                );
                point.transform.position = position;
                patrolPoints[i] = point.transform;
                
                // Visual marker
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(point.transform);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one * 0.5f;
                sphere.GetComponent<Renderer>().material.color = Color.yellow;
                DestroyImmediate(sphere.GetComponent<Collider>());
            }
            
            Debug.Log("Created patrol points");
        }

        private void CreateEscapePoints()
        {
            GameObject escapeParent = new GameObject("EscapePoints");
            
            // Create escape points at the edges of the map (further than patrol points)
            Vector3[] escapePositions = {
                new Vector3(-15f, 0, 0),   // Left edge
                new Vector3(15f, 0, 0),    // Right edge  
                new Vector3(0, 0, 15f)     // Far edge
            };
            
            for (int i = 0; i < escapePoints.Length && i < escapePositions.Length; i++)
            {
                GameObject point = new GameObject($"EscapePoint_{i}");
                point.transform.SetParent(escapeParent.transform);
                point.transform.position = escapePositions[i];
                escapePoints[i] = point.transform;
                
                // Visual marker (red spheres for escape points)
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(point.transform);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one * 0.7f;
                sphere.GetComponent<Renderer>().material.color = Color.red;
                DestroyImmediate(sphere.GetComponent<Collider>());
            }
            
            Debug.Log("Created escape points");
        }

        private void SpawnTestAIs()
        {
            // Clear existing AIs
            var existingAIs = FindObjectsOfType<MobileOptimizedAI>();
            for (int i = existingAIs.Length - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(existingAIs[i].gameObject);
                else
                    DestroyImmediate(existingAIs[i].gameObject);
            }

            var existingCivilians = FindObjectsOfType<CivilianAI>();
            for (int i = existingCivilians.Length - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(existingCivilians[i].gameObject);
                else
                    DestroyImmediate(existingCivilians[i].gameObject);
            }

            // Use coroutine to spawn with proper timing
            StartCoroutine(SpawnAIsWithDelay());
        }

        private System.Collections.IEnumerator SpawnAIsWithDelay()
        {
            // Wait a frame to ensure everything is clean
            yield return null;

            // Spawn Basic AIs
            for (int i = 0; i < numberOfBasicAIs; i++)
            {
                SpawnBasicAI(i);
                yield return new WaitForSeconds(0.1f); // Small delay between spawns
            }

            // Spawn Civilians
            for (int i = 0; i < numberOfCivilians; i++)
            {
                SpawnCivilian(i);
                yield return new WaitForSeconds(0.1f); // Small delay between spawns
            }

            Debug.Log("All AIs spawned successfully with proper initialization!");
        }

        private void SpawnBasicAI(int index)
        {
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = new Vector3(randomPos.x, 0, randomPos.y);

            GameObject aiObject;
            if (basicAIPrefab != null)
            {
                aiObject = Instantiate(basicAIPrefab, spawnPosition, Quaternion.identity);
            }
            else
            {
                // Create basic AI from scratch
                aiObject = CreateBasicAIObject($"BasicAI_{index}", spawnPosition);
            }

            // Configure patrol points
            var aiAdapter = aiObject.GetComponent<AICharacterAdapter>();
            if (aiAdapter != null)
            {
                aiAdapter.SetPatrolPoints(patrolPoints);
                
                // Set guard personality - only aggressive or standard for guards (no defensive/fleeing)
                string[] guardPersonalities = { "standard", "aggressive" };
                aiAdapter.SetAIPersonality(guardPersonalities[Random.Range(0, guardPersonalities.Length)]);
            }

            // Configure MobileOptimizedAI directly using new public API
            var mobileAI = aiObject.GetComponent<MobileOptimizedAI>();
            if (mobileAI != null)
            {
                // Set guard personality directly - only aggressive or standard for guards
                string[] guardPersonalities = { "standard", "aggressive" };
                string selectedPersonality = guardPersonalities[Random.Range(0, guardPersonalities.Length)];
                mobileAI.SetAIPersonality(selectedPersonality);
                
                // Use new public method instead of reflection
                bool useRandomMode = Random.Range(0f, 1f) > 0.5f; // 50% chance
                
                // Wait for next frame to ensure all components are initialized
                StartCoroutine(DelayedPatrolSetup(mobileAI, useRandomMode));
            }

            Debug.Log($"Spawned Basic AI {index} at {spawnPosition}");
        }

        private void SpawnCivilian(int index)
        {
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = new Vector3(randomPos.x, 0, randomPos.y);

            GameObject civilianObject;
            if (civilianAIPrefab != null)
            {
                civilianObject = Instantiate(civilianAIPrefab, spawnPosition, Quaternion.identity);
            }
            else
            {
                // Create civilian from scratch
                civilianObject = CreateCivilianAIObject($"Civilian_{index}", spawnPosition);
            }

            // Randomize behavior weights
            var civilianAI = civilianObject.GetComponent<CivilianAI>();
            if (civilianAI != null)
            {
                civilianAI.behaviorWeights.fleeWeight = Random.Range(50f, 90f);
                civilianAI.behaviorWeights.attackWeight = Random.Range(5f, 20f);
                civilianAI.behaviorWeights.hideWeight = Random.Range(10f, 30f);
                civilianAI.behaviorWeights.panicWeight = Random.Range(2f, 15f);
                
                // Set escape points for flee behavior
                civilianAI.SetEscapePoints(escapePoints);
            }

            Debug.Log($"Spawned Civilian {index} at {spawnPosition}");
        }

        private GameObject CreateBasicAIObject(string name, Vector3 position)
        {
            GameObject aiObject = new GameObject(name);
            aiObject.transform.position = position;
            aiObject.tag = "AI";

            // Visual representation
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(aiObject.transform);
            capsule.transform.localPosition = Vector3.zero;
            capsule.GetComponent<Renderer>().material.color = Color.red;

            // AI Components
            aiObject.AddComponent<MobileOptimizedAI>();
            aiObject.AddComponent<AICharacterAdapter>();

            return aiObject;
        }

        private GameObject CreateCivilianAIObject(string name, Vector3 position)
        {
            GameObject civilianObject = new GameObject(name);
            civilianObject.transform.position = position;
            civilianObject.tag = "Civilian";

            // Visual representation
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(civilianObject.transform);
            cylinder.transform.localPosition = Vector3.zero;
            cylinder.GetComponent<Renderer>().material.color = Color.green;

            // AI Components
            civilianObject.AddComponent<CivilianAI>();

            return civilianObject;
        }

        private void HandleTestInputs()
        {
            if (Input.GetKeyDown(spawnBasicAIKey))
            {
                SpawnBasicAI(Random.Range(100, 999));
            }

            if (Input.GetKeyDown(spawnCivilianKey))
            {
                SpawnCivilian(Random.Range(100, 999));
            }

            if (Input.GetKeyDown(damageAllKey))
            {
                DamageAllAIs();
            }

            if (Input.GetKeyDown(resetSceneKey))
            {
                SetupTestScene();
            }

            if (Input.GetKeyDown(toggleDebugKey))
            {
                ToggleDebugVisualization();
            }
        }

        private void DamageAllAIs()
        {
            var allAdapters = FindObjectsOfType<AICharacterAdapter>();
            foreach (var adapter in allAdapters)
            {
                adapter.TakeDamage(25f);
            }

            var allCivilians = FindObjectsOfType<CivilianAI>();
            foreach (var civilian in allCivilians)
            {
                civilian.TakeDamage(25f);
            }

            Debug.Log("Damaged all AIs by 25 points");
        }

        private void ToggleDebugVisualization()
        {
            if (debugVisualizer != null)
            {
                bool newState = !debugVisualizer.showStateLabels;
                debugVisualizer.showStateLabels = newState;
                debugVisualizer.showHealthBars = newState;
                debugVisualizer.showSightRanges = newState;
                
                Debug.Log($"Debug visualization {(newState ? "enabled" : "disabled")}");
            }
        }

        private System.Collections.IEnumerator DelayedPatrolSetup(MobileOptimizedAI mobileAI, bool useRandomMode)
        {
            // Wait for next frame to ensure all components are ready
            yield return null;
            
            // Now set patrol points properly
            mobileAI.SetPatrolPoints(patrolPoints, useRandomMode);
            
            // Validate the setup
            if (!mobileAI.HasValidPatrolPoints())
            {
                Debug.LogError($"AI {mobileAI.name}: Failed to assign patrol points during delayed setup!");
            }
            
            Debug.Log($"AI {mobileAI.name}: Delayed setup complete - Mode: {(useRandomMode ? "Random" : "Sequential")}");
        }

        private void LogInstructions()
        {
            Debug.Log("=== AI TEST INSTRUCTIONS ===");
            Debug.Log("WASD - Move player");
            Debug.Log($"{spawnBasicAIKey} - Spawn Basic AI");
            Debug.Log($"{spawnCivilianKey} - Spawn Civilian AI");
            Debug.Log($"{damageAllKey} - Damage all AIs");
            Debug.Log($"{resetSceneKey} - Reset scene");
            Debug.Log($"{toggleDebugKey} - Toggle debug visualization (states, health, sight ranges)");
            Debug.Log("Watch AI behaviors:");
            Debug.Log("- Red Capsules = Basic AI (patrol, chase, attack)");
            Debug.Log("- Green Cylinders = Civilians (wander, flee to escape points, or attack)");
            Debug.Log("- Yellow Spheres = Patrol Points");
            Debug.Log("- Red Spheres = Escape Points (civilians despawn here)");
            Debug.Log("============================");
        }

        private void OnDrawGizmos()
        {
            // Draw spawn radius
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            // Draw connections between patrol points
            if (patrolPoints != null && patrolPoints[0] != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    if (patrolPoints[i] != null)
                    {
                        int nextIndex = (i + 1) % patrolPoints.Length;
                        if (patrolPoints[nextIndex] != null)
                        {
                            Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[nextIndex].position);
                        }
                    }
                }
            }
        }
    }
}