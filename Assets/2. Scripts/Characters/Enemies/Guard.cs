using UnityEngine;
using Scripts.FSM.Models;
using Scripts.FSM.Base.StateMachine;
using System.Collections.Generic;

//todo revisar pasar a MVC
public class Guard : BaseCharacter, IUpdatable, IUseFsm
{
    //todo utilizar scriptable object
    [Header("Guard Settings")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float fieldOfView = 90f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float idleTime = 3f;
    [SerializeField] private float searchTime = 5f;
    [SerializeField] private Transform[] patrolPoints;

    [Header("FSM Configuration")]
    [SerializeField] private List<StateData> statesData = new List<StateData>();
    
    private Scripts.FSM.Base.StateMachine.StateMachine stateMachine;
    private Transform player;
    private Vector3 lastKnownPlayerPosition;
    private int currentPatrolIndex = 0;
    private float stateTimer = 0f;
    
    public float DetectionRange => detectionRange;
    public float AttackRange => attackRange;
    public float FieldOfView => fieldOfView;
    public float PatrolSpeed => patrolSpeed;
    public float ChaseSpeed => chaseSpeed;
    public float IdleTime => idleTime;
    public float SearchTime => searchTime;
    public Transform[] PatrolPoints => patrolPoints;
    public Transform Player => player;
    public Vector3 LastKnownPlayerPosition 
    { 
        get => lastKnownPlayerPosition; 
        set => lastKnownPlayerPosition = value; 
    }
    public int CurrentPatrolIndex 
    { 
        get => currentPatrolIndex; 
        set => currentPatrolIndex = value; 
    }
    public float StateTimer 
    { 
        get => stateTimer; 
        set => stateTimer = value; 
    }
    
    public bool IsActive => isAlive && gameObject.activeInHierarchy;
    
    protected override void Awake()
    {
        base.Awake();
        InitializeStateMachine(); ;
        SetupPatrolPoints();
        
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.RegisterUpdatable(this);
    }
    
    private void Start()
    {
        StartCoroutine(DelayedStart());
    }
    
    private System.Collections.IEnumerator DelayedStart()
    {
        yield return null;
    }
    
    public void OnUpdate(float deltaTime)
    {
        if (!isAlive) return;
        UpdateFsm();
        stateTimer += deltaTime; // Keep timer for conditions that need it
    }
    
    protected override void OnDeath()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
        base.OnDeath();
    }
    
    private void InitializeStateMachine()
    {
        if (statesData == null || statesData.Count == 0)
        {
            Logger.LogError($"Guard {gameObject.name}: No StateData configured! Please assign StateData in the inspector.");
            return;
        }
        
        stateMachine = new StateMachine(statesData, this);
    }
    
    private void SetupPatrolPoints()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            patrolPoints = new Transform[2];
            
            GameObject point1 = new GameObject("PatrolPoint1");
            point1.transform.position = transform.position + Vector3.forward * 5f;
            patrolPoints[0] = point1.transform;
            
            GameObject point2 = new GameObject("PatrolPoint2");
            point2.transform.position = transform.position + Vector3.back * 5f;
            patrolPoints[1] = point2.transform;
        }
    }
    
    
    public override void Move(Vector3 direction)
    {
        if (!isAlive) return;
        
        Vector3 movement = direction * (MoveSpeed * Time.deltaTime);
        transform.position += movement;
        
        if (direction.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    public override void Shoot(Vector3 direction)
    {
        if (!isAlive || !CanShoot()) return;
        
        lastShootTime = Time.time;
        CreateBullet(direction);
    }
    
    private void CreateBullet(Vector3 direction)
    {
        var poolService = ServiceLocator.Get<ObjectPoolService>();
        if (poolService != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolService.GetBullet(spawnPosition, direction, 15f, true);
        }
        else
        {
            // Fallback to creating bullet manually if service not available
            GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObj.name = "EnemyBullet";
            bulletObj.transform.position = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            bulletObj.transform.localScale = Vector3.one * 0.2f;
            
            var renderer = bulletObj.GetComponent<Renderer>();
            renderer.material.color = Color.red;
            
            var bulletRb = bulletObj.AddComponent<Rigidbody>();
            bulletRb.useGravity = false;
            
            var bulletCollider = bulletObj.GetComponent<Collider>();
            bulletCollider.isTrigger = true;
            
            var bulletObject = bulletObj.AddComponent<BulletObject>();
            bulletObject.InitializeBullet(direction, 15f, null, true);
        }
    }
    
    #region IUseFsm Implementation
    
    public Transform GetModelTransform()
    {
        return transform;
    }
    
    public void UpdateFsm()
    {
        stateMachine?.RunStateMachine();
    }
    
    public void SetTargetTransform(Transform p_target)
    {
        player = p_target;
    }
    
    public Transform GetTargetTransform()
    {
        return player;
    }
    
    #endregion
}