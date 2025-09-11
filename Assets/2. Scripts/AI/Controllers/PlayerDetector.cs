using UnityEngine;
using AI.Interfaces;

namespace AI.Controllers
{
    public class PlayerDetector : IPlayerDetector
    {
        private readonly Transform playerTransform;
        private readonly string playerTag;

        public PlayerDetector(string playerTag = "Player")
        {
            this.playerTag = playerTag;
            var playerGO = GameObject.FindWithTag(playerTag);
            playerTransform = playerGO?.transform;
        }

        public bool HasPlayer => playerTransform != null;

        public bool IsPlayerVisible(Vector3 fromPosition, float sightRange)
        {
            if (!HasPlayer) return false;
            
            float distance = Vector3.Distance(fromPosition, playerTransform.position);
            return distance <= sightRange;
        }

        public float GetDistanceToPlayer(Vector3 fromPosition)
        {
            return HasPlayer ? Vector3.Distance(fromPosition, playerTransform.position) : Mathf.Infinity;
        }

        public Vector3 GetPlayerPosition()
        {
            return HasPlayer ? playerTransform.position : Vector3.zero;
        }
    }
}