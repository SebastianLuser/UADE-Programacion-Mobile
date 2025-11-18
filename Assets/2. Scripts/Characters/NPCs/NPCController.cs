using Scripts.FSM.Models;
using Scripts.FSM.Base.StateMachine;
using Services;
using Services.MicroServices.AudioService;
using UnityEngine;
using System.Collections.Generic;

public abstract class  NPCController : Character, IUseFsm
{
    [SerializeField] protected NPCDataSO npcData;
    [SerializeField] protected List<StateData> stateDataList;

    protected NPCModel model;
    protected NPCView view;
    protected StateMachine stateMachine;
    protected Transform player;
    protected IAudioService m_audioService;
    protected AudioConfig m_audioConfig;

    public NPCModel Model => model;
    public NPCView View => view;
    public StateMachine StateMachine => stateMachine;

    protected override void Awake()
    {
        base.Awake();
        m_audioService = ServiceLocator.Get<IAudioService>();
        m_audioConfig = (m_audioService as AudioService)?.Config;
        InitializeComponents();
        InitializeAI();
        InitializeStateMachine();
    }
    
    protected virtual void InitializeComponents()
    {
        model = new NPCModel(npcData);
        view = GetComponent<NPCView>();

        if (view == null)
        {
            MyLogger.LogError($"{gameObject.name}: NPCView component required for NPCController!");
        }
        else
        {
            view.SetController(this);
        }
    }

    protected virtual void InitializeAI()
    {
        if (npcData != null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player != null)
            {
                SetTargetTransform(player);
            }
        }
    }

    protected virtual void InitializeStateMachine()
    {
        if (stateDataList != null && stateDataList.Count > 0)
        {
            stateMachine = new StateMachine(stateDataList, this);
        }
        else
        {
            MyLogger.LogWarning($"{gameObject.name}: No state data assigned to NPCController!");
        }
    }

    protected virtual void Update()
    {
        UpdateFsm();
    }
    
    public virtual void MoveTo(Vector3 destination)
    {
        if (view != null)
        {
            view.MoveTo(destination);
        }
    }

    public virtual void StopMovement()
    {
        if (view != null)
        {
            view.StopMovement();
        }
    }

    public virtual void FaceDirection(Vector3 direction)
    {
        if (view != null)
        {
            view.FaceDirection(direction);
        }
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        model?.RuntimeState.TakeDamage(damage);

        // Play hurt sound based on gender
        if (m_audioService != null && m_audioConfig != null && npcData != null)
        {
            AudioClip hurtSFX = npcData.gender == NPCGender.Male ? m_audioConfig.maleHurtSFX : m_audioConfig.femaleHurtSFX;
            m_audioService.PlaySFX(hurtSFX);
        }

        if (model?.RuntimeState.isAlive == false)
        {
            OnDeath();
        }
    }

    protected override void OnDeath()
    {
        // Play death sound based on gender
        if (m_audioService != null && m_audioConfig != null && npcData != null)
        {
            AudioClip deathSFX = npcData.gender == NPCGender.Male ? m_audioConfig.maleDeathSFX : m_audioConfig.femaleDeathSFX;
            m_audioService.PlaySFX(deathSFX);
        }

        base.OnDeath();
    }

    public override void Move(Vector3 direction)
    {
        if (view != null)
        {
            view.Move(direction);
        }
    }

    public Transform GetModelTransform()
    {
        return transform;
    }

    public void UpdateFsm()
    {
        if (model != null && model.RuntimeState.isAlive)
        {
            model.UpdateTimer(Time.deltaTime);
            UpdateDetection();
            stateMachine?.RunStateMachine();
        }
    }

    public void SetTargetTransform(Transform target)
    {
        player = target;
    }

    public Transform GetTargetTransform()
    {
        return player;
    }

    protected virtual void UpdateDetection()
    {
        if (model != null && player != null && npcData != null)
        {
            bool isVisible = CanSeePlayer();
            model.UpdateDetection(isVisible, player);
        }
    }

    public virtual bool CanSeePlayer()
    {
        if (player == null || npcData == null) return false;

        Vector3 directionToPlayer = player.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer > npcData.detectionRange) return false;

        Vector3 forward = transform.forward;
        float angle = Vector3.Angle(forward, directionToPlayer.normalized);
        if (angle > npcData.fieldOfView * 0.5f) return false;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, directionToPlayer.normalized,
            out RaycastHit hit, distanceToPlayer))
        {
            return hit.transform == player;
        }

        return true;
    }
}