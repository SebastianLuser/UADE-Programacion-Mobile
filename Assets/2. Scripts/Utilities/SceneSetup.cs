using UnityEngine;

public class SceneSetup : MonoBehaviour
{
    [Header("Manual Scene Setup")]
    [SerializeField] private bool setupOnStart = true;
    [SerializeField] private bool createPlayer = true;
    [SerializeField] private bool createEnemies = true;
    
    [Header("Player Configuration")]
    [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
    
    [Header("Enemy Configuration")]
    [SerializeField] private Vector3[] enemySpawnPositions = new Vector3[] 
    {
        new Vector3(5f, 0f, 5f),
        new Vector3(-5f, 0f, -5f)
    };
    
    [Header("Enemy Patrol Points")]
    [SerializeField] private Vector3[] patrolPointsEnemy1 = new Vector3[]
    {
        new Vector3(3f, 0f, 3f),
        new Vector3(7f, 0f, 7f)
    };
    
    [SerializeField] private Vector3[] patrolPointsEnemy2 = new Vector3[]
    {
        new Vector3(-3f, 0f, -3f),
        new Vector3(-7f, 0f, -7f)
    };
    
    private GameManager gameManager;
    private CharacterManager characterManager;
    
    private void Start()
    {
        if (setupOnStart)
        {
            SetupScene();
        }
    }
    
    [ContextMenu("Setup Scene")]
    public void SetupScene()
    {
        // Get GameManager and disable auto-initialization
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("No GameManager found in scene!");
            return;
        }
        
        // Initialize managers manually
        gameManager.Initialize();
        
        // Get CharacterManager
        characterManager = ServiceLocator.Get<CharacterManager>();
        
        if (characterManager == null)
        {
            Debug.LogError("CharacterManager not found!");
            return;
        }
        
        // Clear any existing characters
        characterManager.RemoveAllCharacters();
        
        // Setup player
        if (createPlayer)
        {
            SetupPlayer();
        }
        
        // Setup enemies with custom configurations
        if (createEnemies)
        {
            SetupEnemies();
        }
        
        Debug.Log("Custom scene setup completed!");
    }
    
    private void SetupPlayer()
    {
        // Set the spawn position in CharacterManager before spawning
        SetCharacterManagerSpawnPosition(playerSpawnPosition);
        
        characterManager.SpawnMainCharacter();
        Debug.Log($"Player spawned at {playerSpawnPosition}");
    }
    
    private void SetCharacterManagerSpawnPosition(Vector3 position)
    {
        // Use reflection to set the private field in CharacterManager
        var spawnPositionField = typeof(CharacterManager).GetField("playerSpawnPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        if (spawnPositionField != null)
        {
            spawnPositionField.SetValue(characterManager, position);
        }
        else
        {
            Debug.LogWarning("Could not set player spawn position - using CharacterManager default");
        }
    }
    
    private void SetupEnemies()
    {
        for (int i = 0; i < enemySpawnPositions.Length; i++)
        {
            var enemy = characterManager.SpawnEnemy(enemySpawnPositions[i], Quaternion.identity);
            
            // Configure patrol points based on enemy index
            ConfigureEnemyPatrol(enemy, i);
            
            Debug.Log($"Enemy {i + 1} spawned at {enemySpawnPositions[i]}");
        }
    }
    
    private void ConfigureEnemyPatrol(ICharacter enemy, int enemyIndex)
    {
        // Get the Guard component to configure patrol
        var guard = enemy.GameObject.GetComponent<Guard>();
        if (guard == null) return;
        
        Vector3[] patrolPoints = null;
        
        switch (enemyIndex)
        {
            case 0:
                patrolPoints = patrolPointsEnemy1;
                break;
            case 1:
                patrolPoints = patrolPointsEnemy2;
                break;
            default:
                // Create default patrol points around spawn position
                Vector3 spawnPos = enemySpawnPositions[enemyIndex];
                patrolPoints = new Vector3[]
                {
                    spawnPos + Vector3.forward * 2f,
                    spawnPos + Vector3.back * 2f
                };
                break;
        }
        
        // Set patrol points using reflection or public method if available
        SetGuardPatrolPoints(guard, patrolPoints);
    }
    
    private void SetGuardPatrolPoints(Guard guard, Vector3[] points)
    {
        // This depends on how the Guard class is implemented
        // You might need to adjust this based on the actual Guard implementation
        
        // Option 1: If Guard has a public method
        // guard.SetPatrolPoints(points);
        
        // Option 2: Using reflection to set private field
        var patrolField = typeof(Guard).GetField("patrolPoints", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (patrolField != null)
        {
            patrolField.SetValue(guard, points);
            Debug.Log($"Patrol points set for guard: {string.Join(", ", points)}");
        }
        else
        {
            Debug.LogWarning("Could not set patrol points - Guard class structure might be different");
        }
    }
    
    [ContextMenu("Clear Scene")]
    public void ClearScene()
    {
        if (characterManager != null)
        {
            characterManager.RemoveAllCharacters();
            Debug.Log("Scene cleared!");
        }
    }
}