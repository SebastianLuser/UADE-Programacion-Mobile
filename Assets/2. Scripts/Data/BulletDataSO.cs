using UnityEngine;

[CreateAssetMenu(fileName = "BulletData", menuName = "Game Data/Bullet Data")]
public class BulletDataSO : ScriptableObject
{
    [Header("Movement")]
    public float speed = 25f;
    
    [Header("Combat")]
    public float damage = 10f;
    public float lifetime = 5f;
    
    [Header("Visual")]
    public Vector3 scale = Vector3.one * 0.2f;
    public Color bulletColor = Color.yellow;
    
    [Header("Physics")]
    public bool useGravity = false;
    public bool isTrigger = true;
}