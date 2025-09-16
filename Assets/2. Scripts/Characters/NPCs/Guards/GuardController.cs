using UnityEngine;
using Scripts.FSM.Base.StateMachine;
using System.Collections.Generic;

public class GuardController : NPCController, ICombat
{
    [SerializeField] private GuardDataSO guardData;

    public GuardDataSO GuardData => guardData;

    protected override void InitializeComponents()
    {
        npcData = guardData;
        base.InitializeComponents();
    }

    public void Shoot(Vector3 direction)
    {
        if (!IsAlive || !CanShoot()) return;

        model.RuntimeState.ResetStateTimer();
        CreateBullet(direction);
    }

    public bool CanShoot()
    {
        if (guardData == null) return false;
        return model.RuntimeState.stateTimer >= characterData.shootCooldown;
    }

    private void CreateBullet(Vector3 direction)
    {
        if (guardData?.bulletData == null)
        {
            Logger.LogWarning($"{gameObject.name}: BulletData not assigned, cannot shoot!");
            return;
        }

        var poolService = ServiceLocator.Get<ObjectPoolService>();
        if (poolService != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            poolService.GetBullet(spawnPosition, direction, guardData.bulletData.speed, true);
        }
        else
        {
            GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObj.name = "GuardBullet";
            bulletObj.transform.position = transform.position + Vector3.up * 0.5f + direction * 0.8f;
            bulletObj.transform.localScale = guardData.bulletData.scale;

            var bulletRb = bulletObj.AddComponent<Rigidbody>();
            bulletRb.useGravity = guardData.bulletData.useGravity;

            var bulletCollider = bulletObj.GetComponent<Collider>();
            bulletCollider.isTrigger = guardData.bulletData.isTrigger;

            var bulletObject = bulletObj.AddComponent<BulletObject>();
            bulletObject.InitializeBullet(direction, guardData.bulletData.speed, null);
        }
    }

    public override bool CanSeePlayer()
    {
        return base.CanSeePlayer();
    }

    public bool PlayerInAttackRange()
    {
        if (player == null || guardData == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        return distanceToPlayer <= guardData.attackRange;
    }

    public void AttackPlayer()
    {
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            Shoot(direction);
            FaceDirection(direction);
        }
    }

    public void ChasePlayer()
    {
        if (player != null)
        {
            MoveTo(player.position);
            FaceDirection((player.position - transform.position).normalized);
        }
    }
}