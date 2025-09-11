using UnityEngine;

namespace AI.Interfaces
{
    public interface ISteeringBehaviour
    {
        Vector3 CalculateSteering(ISteeringContext context);
        float Priority { get; set; }
        bool IsActive { get; set; }
    }

    public interface ISteeringContext
    {
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        Vector3 Forward { get; }
        float MaxSpeed { get; }
        float MaxForce { get; }
        Transform Transform { get; }
        
        // For obstacle detection
        bool HasObstacleInPath(Vector3 direction, float distance, out Vector3 avoidanceDirection);
        
        // For target detection
        Transform Target { get; }
        Vector3 TargetPosition { get; }
        Vector3 TargetVelocity { get; }
    }
}