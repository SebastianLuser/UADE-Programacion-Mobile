using UnityEngine;

namespace AI.Interfaces
{
    public interface IPlayerDetector
    {
        bool IsPlayerVisible(Vector3 fromPosition, float sightRange);
        float GetDistanceToPlayer(Vector3 fromPosition);
        Vector3 GetPlayerPosition();
        bool HasPlayer { get; }
    }
}