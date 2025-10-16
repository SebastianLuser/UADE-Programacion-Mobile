using ScriptableObjects.Bullets;
using UnityEngine;

[CreateAssetMenu(fileName = "MainCharacterData", menuName = "Game Data/Main Character Data")]
public class MainCharacterDataSO : CharacterDataSO
{
    [Header("Player Specific")]
    public BulletData bulletData;
}