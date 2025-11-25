using System.Collections.Generic;
using Services;
using Services.MicroServices.BlackboardService;
using UnityEngine;

/// <summary>
/// Leader for Ally faction.
/// Coordinates allied units and issues defensive commands based on player status.
/// </summary>
public class AllyLeader : Ally
{
    [Header("Ally Leadership")]
    [Tooltip("Allies managed by this leader")]
    [SerializeField] private List<Ally> managedAllies = new List<Ally>();

    [Header("Defensive Tactics")]
    [Tooltip("Player health % to trigger protect command")]
    [Range(0f, 1f)]
    [SerializeField] private float playerProtectThreshold = 0.3f;

    [Tooltip("Radius for defensive formation around player")]
    [SerializeField] private float defensiveRadius = 3f;

    [Tooltip("Duration of defensive override command")]
    [SerializeField] private float defensiveCommandDuration = 10f;

    [Header("Coordination")]
    [Tooltip("Interval to check player status and update tactics")]
    [SerializeField] private float tacticsUpdateInterval = 1f;

    [Tooltip("Radius to auto-discover nearby allies")]
    [SerializeField] private float allyDiscoveryRadius = 15f;

    private IBlackboardService blackboard;
    private float nextTacticsUpdateTime;
    private bool isProtectingPlayer;

    public IReadOnlyList<Ally> ManagedAllies => managedAllies;

    protected override void Awake()
    {
        base.Awake();
        blackboard = ServiceLocator.Get<IBlackboardService>();
    }

    private void Start()
    {
        if (blackboard == null)
        {
            blackboard = ServiceLocator.Get<IBlackboardService>();
            if (blackboard == null)
            {
                Debug.LogWarning("[AllyLeader] Blackboard service not found at Start");
            }
        }

        // Auto-discover nearby allies if list is empty
        if (managedAllies.Count == 0)
        {
            DiscoverNearbyAllies();
        }
    }

    private void Update()
    {
        if (blackboard == null)
        {
            blackboard = ServiceLocator.Get<IBlackboardService>();
        }

        UpdateTactics();
    }

    private void UpdateTactics()
    {
        if (Time.time < nextTacticsUpdateTime) return;
        nextTacticsUpdateTime = Time.time + tacticsUpdateInterval;

        MonitorPlayerHealth();
        ShareIntel();
    }

    private void MonitorPlayerHealth()
    {
        if (!blackboard.HasKey("PLAYER_HEALTH")) return;

        float l_playerHealth = blackboard.GetValue<float>("PLAYER_HEALTH");
        float l_playerMaxHealth = blackboard.GetValue<float>("PLAYER_MAX_HEALTH");
        float l_healthPercent = l_playerHealth / l_playerMaxHealth;

        if (l_healthPercent < playerProtectThreshold && !isProtectingPlayer)
        {
            IssueProtectPlayerCommand();
        }
        else if (l_healthPercent > playerProtectThreshold + 0.2f && isProtectingPlayer)
        {
            // Player recovered, resume normal behavior
            ClearAllOverrides();
            isProtectingPlayer = false;
            Debug.Log("[AllyLeader] Player health recovered, resuming normal tactics");
        }
    }

    private void IssueProtectPlayerCommand()
    {
        Transform l_player = GetPlayerToFollow();
        if (l_player == null)
        {
            Debug.LogWarning("[AllyLeader] Cannot issue protect command - player reference is null");
            return;
        }

        Vector3 l_playerPos = l_player.position;
        int l_validAllies = 0;

        for (int i = 0; i < managedAllies.Count; i++)
        {
            if (managedAllies[i] == null || !managedAllies[i].IsAlive) continue;

            // Position allies in circle around player
            float l_angle = (360f / managedAllies.Count) * i * Mathf.Deg2Rad;
            Vector3 l_offset = new Vector3(
                Mathf.Cos(l_angle) * defensiveRadius,
                0f,
                Mathf.Sin(l_angle) * defensiveRadius
            );

            Vector3 l_defensivePos = l_playerPos + l_offset;

            // Use Guard's Leader override system
            managedAllies[i].SetLeaderOverride(l_defensivePos, defensiveCommandDuration, "PROTECT");
            l_validAllies++;
        }

        if (l_validAllies > 0)
        {
            isProtectingPlayer = true;

            // Share status via blackboard
            blackboard.SetValue("ALLIES_PROTECTING_PLAYER", true);
            blackboard.SetValue("ALLY_LEADER_POSITION", transform.position);

            Debug.Log($"[AllyLeader] Issued protect command to {l_validAllies} allies");
        }
    }

    private void ShareIntel()
    {
        // Share detected Guards with other allies via blackboard
        Guard l_currentTarget = GetCurrentTarget();
        if (l_currentTarget != null)
        {
            blackboard.SetValue("ALLY_DETECTED_GUARD_POSITION", l_currentTarget.transform.position);
            blackboard.SetValue("ALLY_DETECTED_GUARD_TIME", Time.time);
        }

        // Update ally count
        int l_aliveAllies = 0;
        foreach (Ally l_ally in managedAllies)
        {
            if (l_ally != null && l_ally.IsAlive)
                l_aliveAllies++;
        }
        blackboard.SetValue("ALLIES_ALIVE_COUNT", l_aliveAllies);
    }

    private void DiscoverNearbyAllies()
    {
        Collider[] l_colliders = Physics.OverlapSphere(transform.position, allyDiscoveryRadius);

        foreach (Collider l_col in l_colliders)
        {
            Ally l_ally = l_col.GetComponent<Ally>();
            if (l_ally != null && l_ally != this && !managedAllies.Contains(l_ally))
            {
                managedAllies.Add(l_ally);
            }
        }

        Debug.Log($"[AllyLeader] Discovered {managedAllies.Count} nearby allies");
    }

    public void ClearAllOverrides()
    {
        if (managedAllies == null) return;

        foreach (Ally l_ally in managedAllies)
        {
            if (l_ally == null) continue;
            l_ally.ClearLeaderOverride();
        }

        isProtectingPlayer = false;
        blackboard.SetValue("ALLIES_PROTECTING_PLAYER", false);
    }

    public void AddManagedAlly(Ally p_ally)
    {
        if (p_ally != null && !managedAllies.Contains(p_ally))
        {
            managedAllies.Add(p_ally);
        }
    }

    public void RemoveManagedAlly(Ally p_ally)
    {
        if (p_ally != null)
        {
            managedAllies.Remove(p_ally);
        }
    }

    /// <summary>
    /// Set the list of allies managed by this leader. Used by spawners.
    /// </summary>
    public void SetManagedAllies(List<Ally> allies)
    {
        managedAllies = allies ?? new List<Ally>();
        Debug.Log($"[AllyLeader] {name} assigned {managedAllies.Count} Allies");
    }
}
