using UnityEngine;
using Game.AI.Steering;
using Services;
using Services.MicroServices.BlackboardService;
using System.Reflection;
using Services.MicroServices.UpdateService;

public class Ally : Guard, IUpdateListener
{
    [Header("Ally Configuration")]
    [SerializeField] private AllyDataSO allyData;

    private Transform playerToFollow;

    [Header("Guard Detection")]
    [Tooltip("Tag used to identify Guards")]
    [SerializeField] private string guardTag = "Guard";

    [Tooltip("Layer mask for Guard detection")]
    [SerializeField] private LayerMask guardLayerMask = 1 << 7; // Layer 7 = Enemies

    private Guard currentTarget;
    private IBlackboardService blackboard;
    private FlockingSystem.FlockingEntity flockingEntity;
    private Vector3 playerVelocity;

    // Ally specific
    private float followDistance;
    private float followSpeed;
    private float allyAttackRange;
    private float allyChaseSpeed;
    
    protected override void Awake()
    {

        var useFSMField = typeof(Guard).GetField(
            "useFSM",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (useFSMField != null)
            useFSMField.SetValue(this, false);
        base.Awake();
        
        blackboard = ServiceLocator.Get<IBlackboardService>();
        flockingEntity = GetComponent<FlockingSystem.FlockingEntity>();

        InitializeAllySettings();
        ConfigureForAllyBehavior();
    }
    
    private void Start()
    {
        // Assign player if spawner didn't do it
        if (playerToFollow == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerToFollow = p.transform;
                Debug.Log($"[Ally] {name} assigned Player: {playerToFollow.name}");
            }
            else
            {
                Debug.LogError($"[Ally] {name} could not find Player!");
            }
        }
    }
    
    private void InitializeAllySettings()
    {
        if (allyData != null)
        {
            followDistance = allyData.followDistance;
            followSpeed = allyData.followSpeed;
            allyAttackRange = allyData.attackRange;
            allyChaseSpeed = allyData.chaseSpeed;

            SetFlockingSettings(
                allyData.useFlocking,
                allyData.followPlayerWeight,
                allyData.flockingWeight
            );
        }
        else
        {
            Debug.LogWarning("[Ally] Missing AllyDataSO, using defaults");
            followDistance = 3f;
            followSpeed = 4f;
            allyAttackRange = 8f;
            allyChaseSpeed = 5f;
        }
    }

    private void SetFlockingSettings(bool useFlocking, float followW, float flockW)
    {
        var f1 = typeof(Guard).GetField("useFlocking", BindingFlags.NonPublic | BindingFlags.Instance);
        var f2 = typeof(Guard).GetField("baseForceWeight", BindingFlags.NonPublic | BindingFlags.Instance);
        var f3 = typeof(Guard).GetField("flockForceWeight", BindingFlags.NonPublic | BindingFlags.Instance);

        f1?.SetValue(this, useFlocking);
        f2?.SetValue(this, followW);
        f3?.SetValue(this, flockW);
    }

    private void ConfigureForAllyBehavior()
    {
        var detectorField = typeof(Guard).GetField(
            "playerDetector",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        var detector = detectorField?.GetValue(this);
        if (detector != null)
        {
            var t = detector.GetType();
            var tagField = t.GetField("playerTag", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var layerField = t.GetField("playerLayerMask", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            tagField?.SetValue(detector, guardTag);
            layerField?.SetValue(detector, guardLayerMask);
        }
        var personalityField = typeof(Guard).GetField(
            "personalityType",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        personalityField?.SetValue(this, AIPersonalityType.Cautious);
    }
    void IUpdateListener.MyUpdate()
    {
        if (!IsAlive) return;

        UpdatePlayerVelocity();
        UpdateAllyBehavior();
    }

    private void UpdatePlayerVelocity()
    {
        if (playerToFollow == null) return;

        Rigidbody rb = playerToFollow.GetComponent<Rigidbody>();
        if (rb != null)
            playerVelocity = rb.linearVelocity;
    }

    private void UpdateAllyBehavior()
    {
        Guard nearest = FindNearestVisibleGuard();

        if (nearest != null)
        {
            currentTarget = nearest;
            AttackGuard(nearest);
        }
        else
        {
            currentTarget = null;
            FollowPlayer();
        }
    }
    
    private void AttackGuard(Guard target)
    {
        float dist = Vector3.Distance(transform.position, target.transform.position);

        if (dist > allyAttackRange)
        {
            Vector3 gv = target.CurrentVelocity;

            Vector3 steer = Steering.Pursuit(
                transform.position,
                CurrentVelocity,
                target.transform.position,
                gv,
                allyChaseSpeed
            );

            ApplySteering(steer);
        }
        else
        {
            Vector3 brake = -CurrentVelocity * 0.5f;
            ApplySteering(brake);
        }

        if (dist <= allyAttackRange && CanShoot())
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            Shoot(dir);
        }

        FaceDirection(
            (target.transform.position - transform.position).normalized,
            BaseRotationSpeed
        );
    } 
    private void FollowPlayer()
    {
        if (playerToFollow == null) return;

        float dist = Vector3.Distance(transform.position, playerToFollow.position);

        if (dist > followDistance)
        {
            Vector3 steer = Steering.Pursuit(
                transform.position,
                CurrentVelocity,
                playerToFollow.position,
                playerVelocity,
                followSpeed
            );

            ApplySteering(steer);
        }
        else
        {
            Vector3 brake = -CurrentVelocity * 0.3f;
            ApplySteering(brake);
        }
    }
    private Guard FindNearestVisibleGuard()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, DetectionRange, guardLayerMask);

        Guard nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider h in hits)
        {
            Guard g = h.GetComponent<Guard>();
            if (g == null || !g.IsAlive) continue;

            if (g is Ally) continue;

            Vector3 dir = g.transform.position - transform.position;
            float dist = dir.magnitude;

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir.normalized, dist, ObstaclesMask))
                continue;

            if (dist < minDist)
            {
                minDist = dist;
                nearest = g;
            }
        }

        return nearest;
    }
    public Guard GetCurrentTarget() => currentTarget;

    public Transform GetPlayerToFollow() => playerToFollow;

    public void SetPlayerToFollow(Transform p) => playerToFollow = p;
}
