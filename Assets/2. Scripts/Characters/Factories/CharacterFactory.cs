using UnityEngine;

public abstract class CharacterFactory : ICharacterFactory
{
    [SerializeField] protected GameObject characterPrefab;
    
    public abstract ICharacter CreateCharacter(Vector3 position, Quaternion rotation);
    
    protected virtual GameObject InstantiatePrefab(Vector3 position, Quaternion rotation)
    {
        if (characterPrefab == null)
        {
            Logger.LogError($"{GetType().Name}: Character prefab is not assigned!");
            return null;
        }
        
        GameObject instance = Object.Instantiate(characterPrefab, position, rotation);
        return instance;
    }
}