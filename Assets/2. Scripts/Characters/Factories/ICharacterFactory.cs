using UnityEngine;

public interface ICharacterFactory
{
    ICharacter CreateCharacter(Vector3 position, Quaternion rotation);
}