using UnityEngine;

public class EnemyFactory : CharacterFactory
{
    public override ICharacter CreateCharacter(Vector3 position, Quaternion rotation)
    {
        GameObject characterObject = CreateCharacterGameObject("Guard", position, rotation);
        var guard = characterObject.AddComponent<Guard>();
        return guard;
    }
}