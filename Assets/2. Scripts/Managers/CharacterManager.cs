using System.Collections.Generic;
using UnityEngine;

public class CharacterManager : BaseManager
{
    [Header("Character Settings")]
    [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
    [SerializeField] private Vector3[] enemySpawnPositions = { new Vector3(10f, 0f, 0f) };
    
    private MainCharacterFactory mainCharacterFactory;
    private EnemyFactory enemyFactory;
    
    private ICharacter mainCharacter;
    private List<ICharacter> enemies = new List<ICharacter>();
    private List<ICharacter> allCharacters = new List<ICharacter>();
    
    public ICharacter MainCharacter => mainCharacter;
    public IReadOnlyList<ICharacter> Enemies => enemies.AsReadOnly();
    public IReadOnlyList<ICharacter> AllCharacters => allCharacters.AsReadOnly();
    
    protected override void OnInitialize()
    {
        InitializeFactories();
        ServiceLocator.Register<CharacterManager>(this);
    }
    
    private void InitializeFactories()
    {
        mainCharacterFactory = new MainCharacterFactory();
        enemyFactory = new EnemyFactory();
    }
    
    public void SpawnMainCharacter()
    {
        if (mainCharacter != null)
        {
            Debug.LogWarning("Main character already spawned!");
            return;
        }
        
        mainCharacter = mainCharacterFactory.CreateCharacter(playerSpawnPosition, Quaternion.identity);
        allCharacters.Add(mainCharacter);
        
        Debug.Log("Main character spawned successfully");
    }
    
    public ICharacter SpawnEnemy(Vector3 position, Quaternion rotation)
    {
        var enemy = enemyFactory.CreateCharacter(position, rotation);
        enemies.Add(enemy);
        allCharacters.Add(enemy);
        
        Debug.Log($"Enemy spawned at position {position}");
        return enemy;
    }
    
    public void SpawnEnemiesAtDefaultPositions()
    {
        foreach (var position in enemySpawnPositions)
        {
            SpawnEnemy(position, Quaternion.identity);
        }
    }
    
    public void RemoveCharacter(ICharacter character)
    {
        if (character == null) return;
        
        if (character == mainCharacter)
        {
            mainCharacter = null;
        }
        
        enemies.Remove(character);
        allCharacters.Remove(character);
        
        if (character.GameObject != null)
        {
            Destroy(character.GameObject);
        }
    }
    
    public void RemoveAllEnemies()
    {
        var enemiesToRemove = new List<ICharacter>(enemies);
        foreach (var enemy in enemiesToRemove)
        {
            RemoveCharacter(enemy);
        }
    }
    
    public void RemoveAllCharacters()
    {
        var charactersToRemove = new List<ICharacter>(allCharacters);
        foreach (var character in charactersToRemove)
        {
            RemoveCharacter(character);
        }
    }
    
    public int GetAliveEnemiesCount()
    {
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy.IsAlive)
                count++;
        }
        return count;
    }
    
    public bool IsMainCharacterAlive()
    {
        return mainCharacter != null && mainCharacter.IsAlive;
    }
    
    protected override void OnShutdown()
    {
        RemoveAllCharacters();
        ServiceLocator.Unregister<CharacterManager>();
    }
}