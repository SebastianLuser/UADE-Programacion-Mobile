using System.Collections.Generic;
using UnityEngine;

public class CharacterManager : BaseManager
{
    [Header("Character Settings")]
    [SerializeField] private Vector3 playerSpawnPosition = Vector3.zero;
    [SerializeField] private Vector3[] enemySpawnPositions = { new Vector3(10f, 0f, 0f) };
    
    [Header("Character Prefabs")]
    [SerializeField] private GameObject mainCharacterPrefab;
    [SerializeField] private GameObject enemyPrefab;
    
    private ICharacter mainCharacter;
    private List<ICharacter> enemies = new List<ICharacter>();
    private List<ICharacter> allCharacters = new List<ICharacter>();
    
    public ICharacter MainCharacter => mainCharacter;
    public IReadOnlyList<ICharacter> Enemies => enemies.AsReadOnly();
    public IReadOnlyList<ICharacter> AllCharacters => allCharacters.AsReadOnly();
    
    protected override void OnInitialize()
    {
        ServiceLocator.Register<CharacterManager>(this);
    }
    
    public void SpawnMainCharacter()
    {
        if (mainCharacter != null)
        {
            Logger.LogWarning("Main character already spawned!");
            return;
        }
        
        if (mainCharacterPrefab == null)
        {
            Logger.LogError("Main character prefab is not assigned!");
            return;
        }
        
        GameObject characterObject = Instantiate(mainCharacterPrefab, playerSpawnPosition, Quaternion.identity);
        mainCharacter = characterObject.GetComponent<ICharacter>();
        
        if (mainCharacter == null)
        {
            Logger.LogError("Main character prefab does not have ICharacter component!");
            Destroy(characterObject);
            return;
        }
        
        allCharacters.Add(mainCharacter);
        Logger.LogInfo("Main character spawned successfully");
    }
    
    public ICharacter SpawnEnemy(Vector3 position, Quaternion rotation)
    {
        if (enemyPrefab == null)
        {
            Logger.LogError("Enemy prefab is not assigned!");
            return null;
        }
        
        GameObject enemyObject = Instantiate(enemyPrefab, position, rotation);
        var enemy = enemyObject.GetComponent<ICharacter>();
        
        if (enemy == null)
        {
            Logger.LogError("Enemy prefab does not have ICharacter component!");
            Destroy(enemyObject);
            return null;
        }
        
        enemies.Add(enemy);
        allCharacters.Add(enemy);
        
        Logger.LogInfo($"Enemy spawned at position {position}");
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