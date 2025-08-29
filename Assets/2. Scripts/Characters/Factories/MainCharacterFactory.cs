using UnityEngine;

public class MainCharacterFactory : CharacterFactory
{
    public override ICharacter CreateCharacter(Vector3 position, Quaternion rotation)
    {
        GameObject characterObject = CreateCharacterGameObject("MainCharacter", position, rotation);
        var mainCharacter = characterObject.AddComponent<MainCharacter>();
        return mainCharacter;
    }
}