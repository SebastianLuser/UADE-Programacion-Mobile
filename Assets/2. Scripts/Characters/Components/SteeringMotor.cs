using UnityEngine;
using Game.AI.Steering;

/// <summary>
/// Shared steering/locomotion component used by AI characters.
/// Wraps velocity integration, obstacle avoidance and facing so that
/// behavioural scripts can stay focused on decision making.
/// </summary>
[DisallowMultipleComponent]
public class SteeringMotor : MonoBehaviour
{
    [Header("Steering Physics")]
    [SerializeField] private float mass = 1f;
    [SerializeField] private float maxForce = 15f;
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float slowingDistance = 2f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstaclesMask = -1;
    [SerializeField] private float avoidRadius = 1.5f;
    [SerializeField] private float avoidAngle = 90f;
    [SerializeField] private float personalArea = 0.3f;

    private ObstacleAvoidance obstacleAvoidance;
    private Vector3 velocity;

    public Vector3 CurrentVelocity => velocity;
    public float MaxSpeed => maxSpeed;
    public float MaxForce => maxForce;
    public float Mass => mass;
    public float SlowingDistance => slowingDistance;

    private void Awake()
    {
        RebuildAvoidance();
    }

    public void ConfigureSteering(float newMass, float newMaxForce, float newMaxSpeed, float newSlowingDistance)
    {
        mass = Mathf.Max(0.01f, newMass);
        maxForce = Mathf.Max(0.01f, newMaxForce);
        maxSpeed = Mathf.Max(0.01f, newMaxSpeed);
        slowingDistance = Mathf.Max(0.01f, newSlowingDistance);
    }

    public void ConfigureObstacleAvoidance(float radius, float angle, float personal, LayerMask mask)
    {
        avoidRadius = Mathf.Max(0.01f, radius);
        avoidAngle = angle;
        personalArea = Mathf.Max(0.01f, personal);
        obstaclesMask = mask;
        RebuildAvoidance();
    }

    public void SetMaxSpeed(float newMaxSpeed)
    {
        maxSpeed = Mathf.Max(0.01f, newMaxSpeed);
    }

    public void SetSlowingDistance(float newSlowingDistance)
    {
        slowingDistance = Mathf.Max(0.01f, newSlowingDistance);
    }

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }

    public void SetVelocity(Vector3 customVelocity)
    {
        velocity = Vector3.ClampMagnitude(customVelocity, maxSpeed);
    }

    public Vector3 ApplySteering(Vector3 steering, float rotationSpeedMultiplier)
    {
        return ApplySteering(steering, Time.deltaTime, rotationSpeedMultiplier);
    }

    public Vector3 ApplySteering(Vector3 steering, float deltaTime, float rotationSpeedMultiplier)
    {
        velocity = Integrate(steering, deltaTime);
        Vector3 avoidedVel = obstacleAvoidance != null
            ? obstacleAvoidance.GetDirImproved(velocity, false)
            : velocity;

        if (avoidedVel.sqrMagnitude > 0.001f)
        {
            transform.position += avoidedVel * deltaTime;

            if (avoidedVel.magnitude > 0.1f && rotationSpeedMultiplier > 0f)
            {
                Vector3 lookDirection = avoidedVel.normalized;
                lookDirection.y = 0f;
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeedMultiplier * deltaTime);
            }
        }

        return avoidedVel;
    }

    public void Move(Vector3 direction, float rotationSpeedMultiplier)
    {
        Vector3 targetVel = direction.normalized * maxSpeed;
        Vector3 steering = targetVel - velocity;
        ApplySteering(steering, rotationSpeedMultiplier);
    }

    private Vector3 Integrate(Vector3 steering, float dt)
    {
        Vector3 clampedForce = steering;
        if (clampedForce.sqrMagnitude > maxForce * maxForce)
        {
            clampedForce = clampedForce.normalized * maxForce;
        }

        Vector3 acceleration = clampedForce / mass;
        Vector3 newVel = velocity + acceleration * dt;

        if (newVel.sqrMagnitude > maxSpeed * maxSpeed)
        {
            newVel = newVel.normalized * maxSpeed;
        }

        return newVel;
    }

    private void RebuildAvoidance()
    {
        obstacleAvoidance = new ObstacleAvoidance(transform, avoidRadius, avoidAngle, personalArea, obstaclesMask);
    }
}
