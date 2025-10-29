using UnityEngine;

public interface ICharacter2
{
    GameObject GameObject { get; }
    Transform Transform { get; }
    void Initialize();
    void Move(Vector3 direction);
}