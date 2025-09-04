using UnityEngine;

public class EnemyFactory : CharacterFactory
{
    public override ICharacter CreateCharacter(Vector3 position, Quaternion rotation)
    {
        GameObject characterObject = InstantiatePrefab(position, rotation);
        if (characterObject == null) return null;
        
        var enemy = characterObject.GetComponent<ICharacter>();
        if (enemy == null)
        {
            Logger.LogError("EnemyFactory: Prefab does not contain ICharacter component!");
            Object.Destroy(characterObject);
            return null;
        }
        
        enemy.Initialize();
        return enemy;
    }
}