using UnityEngine;

public class Guard : BaseCharacter, IUpdatable
{
    [Header("Guard Settings")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float fieldOfView = 90f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float idleTime = 3f;
    [SerializeField] private float searchTime = 5f;
    [SerializeField] private Transform[] patrolPoints;
    
    private StateMachine stateMachine;
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
        InitializeStateMachine();
        FindPlayer();
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
        
        if (player == null)
        {
            FindPlayer();
        }
        
        stateMachine.StartState<IdleState>();
    }
    
    public void OnUpdate(float deltaTime)
    {
        if (!isAlive) return;
        stateMachine.Update();
    }
    
    protected override void OnDeath()
    {
        var updateManager = ServiceLocator.Get<UpdateManager>();
        updateManager?.UnregisterUpdatable(this);
        base.OnDeath();
    }
    
    private void InitializeStateMachine()
    {
        stateMachine = new StateMachine();
        
        stateMachine.AddState(new IdleState(this));
        stateMachine.AddState(new PatrollingState(this));
        stateMachine.AddState(new ChasingState(this));
        stateMachine.AddState(new SearchingState(this));
        stateMachine.AddState(new AttackingState(this));
        
        stateMachine.AddTransition<IdleState, PatrollingState>(() => stateTimer >= idleTime);
        stateMachine.AddTransition<PatrollingState, IdleState>(() => ReachedPatrolPoint());
        stateMachine.AddTransition<IdleState, ChasingState>(() => CanSeePlayer());
        stateMachine.AddTransition<PatrollingState, ChasingState>(() => CanSeePlayer());
        stateMachine.AddTransition<ChasingState, AttackingState>(() => IsPlayerInAttackRange());
        stateMachine.AddTransition<ChasingState, SearchingState>(() => !CanSeePlayer());
        stateMachine.AddTransition<AttackingState, ChasingState>(() => !IsPlayerInAttackRange() && CanSeePlayer());
        stateMachine.AddTransition<AttackingState, SearchingState>(() => !CanSeePlayer());
        stateMachine.AddTransition<SearchingState, ChasingState>(() => CanSeePlayer());
        stateMachine.AddTransition<SearchingState, IdleState>(() => stateTimer >= searchTime);
    }
    
    private void FindPlayer()
    {
        var characterManager = ServiceLocator.Get<CharacterManager>();
        if (characterManager?.MainCharacter != null)
        {
            player = characterManager.MainCharacter.Transform;
        }
        else
        {
            Debug.LogWarning("Guard: CharacterManager not found or MainCharacter not spawned yet!");
        }
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
    
    public bool CanSeePlayer()
    {
        if (player == null) return false;
        
        Vector3 directionToPlayer = (player.position - transform.position);
        float distanceToPlayer = directionToPlayer.magnitude;
        
        if (distanceToPlayer > detectionRange) return false;
        
        directionToPlayer.Normalize();
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        
        if (angle > fieldOfView / 2f) return false;
        
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, directionToPlayer, out RaycastHit hit, distanceToPlayer))
        {
            return hit.collider.GetComponent<MainCharacter>() != null;
        }
        
        return true;
    }
    
    public bool IsPlayerInAttackRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= attackRange;
    }
    
    public bool ReachedPatrolPoint()
    {
        if (patrolPoints == null || currentPatrolIndex >= patrolPoints.Length) return true;
        return Vector3.Distance(transform.position, patrolPoints[currentPatrolIndex].position) < 1f;
    }
    
    public override void Move(Vector3 direction)
    {
        if (!isAlive) return;
        
        Vector3 movement = direction * moveSpeed * Time.deltaTime;
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
        var poolManager = ServiceLocator.Get<ObjectPoolManager>();
        if (poolManager != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolManager.GetBullet(spawnPosition, direction, 15f, true);
        }
        else if (BulletPool.Instance != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            BulletPool.Instance.GetBullet(spawnPosition, direction, 15f, true);
        }
        else
        {
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
}