using UnityEngine;

[CreateAssetMenu(fileName = "MainCharacterData", menuName = "Game Data/Main Character Data")]
public class MainCharacterDataSO : CharacterDataSO
{
    [Header("Player Specific")]
    public BulletDataSO bulletData;
}