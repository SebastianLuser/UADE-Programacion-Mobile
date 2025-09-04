using UnityEngine;

public class MainCharacterFactory : CharacterFactory
{
    public override ICharacter CreateCharacter(Vector3 position, Quaternion rotation)
    {
        GameObject characterObject = InstantiatePrefab(position, rotation);
        if (characterObject == null) return null;
        
        var mainCharacter = characterObject.GetComponent<MainCharacter>();
        if (mainCharacter == null)
        {
            Logger.LogError("MainCharacterFactory: Prefab does not contain MainCharacter component!");
            Object.Destroy(characterObject);
            return null;
        }
        
        mainCharacter.Initialize();
        return mainCharacter;
    }
}