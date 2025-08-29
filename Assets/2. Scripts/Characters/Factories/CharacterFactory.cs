using UnityEngine;

public abstract class CharacterFactory : ICharacterFactory
{
    public abstract ICharacter CreateCharacter(Vector3 position, Quaternion rotation);
    
    protected GameObject CreateCharacterGameObject(string name, Vector3 position, Quaternion rotation)
    {
        GameObject characterObject = new GameObject(name);
        characterObject.transform.position = position;
        characterObject.transform.rotation = rotation;
        
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.SetParent(characterObject.transform);
        capsule.transform.localPosition = Vector3.zero;
        capsule.transform.localRotation = Quaternion.identity;
        
        var rigidbody = characterObject.AddComponent<Rigidbody>();
        rigidbody.freezeRotation = true;
        
        return characterObject;
    }
}