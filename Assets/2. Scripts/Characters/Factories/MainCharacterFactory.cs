using UnityEngine;

public class MainCharacterFactory : CharacterFactory
{
    public override ICharacter CreateCharacter(Vector3 position, Quaternion rotation)
    {
        GameObject characterObject = CreateCharacterGameObject("MainCharacter", position, rotation);
        var playerController = characterObject.AddComponent<PlayerController>();
        return playerController;
    }
}