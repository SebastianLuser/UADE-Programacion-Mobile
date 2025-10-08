using UnityEngine;

namespace ScriptableObjects.Bullets
{
    [CreateAssetMenu(menuName = "Main/Bullets/Bullet Data")]
    public class BulletData : ScriptableObject
    {
        [field: SerializeField] public BulletObject Prefab { get; private set; }
        [field: SerializeField] public float Speed { get; private set; } = 10f;
        [field: SerializeField] public float Damage { get; private set; } = 25f;
        [field: SerializeField] public float Lifetime { get; private set; } = 5f;
        [field: SerializeField] public LayerMask Layer { get; private set; }
        
        [field: Header("Visual")]
        [field: SerializeField] public Color Color { get; private set; } = Color.yellow;
    
        [field: Header("Physics")]
        [field: SerializeField] public bool UseGravity { get; private set; } = false;
    }
}