using UnityEngine;

[CreateAssetMenu(fileName = "GuardData", menuName = "Game Data/Guard Data")]
public class GuardDataSO : NPCDataSO
{
    [Header("Guard Combat")]
    [field: SerializeField] public float attackRange { get; private set; } = 2f;
    [field: SerializeField] public float chaseSpeed { get; private set; } = 4f;
    [field: SerializeField] public BulletDataSO bulletData { get; private set; }
}