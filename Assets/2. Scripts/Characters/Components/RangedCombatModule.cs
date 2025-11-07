using UnityEngine;
using ScriptableObjects.Bullets;
using Services;
using Services.MicroServices.PoolObjectsService;

[DisallowMultipleComponent]
public class RangedCombatModule : MonoBehaviour
{
    [SerializeField] private BulletData bulletData;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.5f, 0.8f);

    private AlertEmitter alertEmitter;

    private IPoolObjectsService PoolService => ServiceLocator.Get<IPoolObjectsService>();

    private void Awake()
    {
        alertEmitter = GetComponent<AlertEmitter>();
    }

    public void Fire(Vector3 direction, float timestamp, bool notifyAlert)
    {
        if (bulletData == null)
        {
            MyLogger.LogWarning($"{name}: RangedCombatModule missing BulletData.");
            return;
        }

        var poolService = PoolService;
        if (poolService == null)
        {
            MyLogger.LogWarning($"{name}: PoolObjectsService unavailable for RangedCombatModule.");
            return;
        }

        Vector3 normalizedDir = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        Vector3 spawnPos = transform.position
            + transform.right * spawnOffset.x
            + Vector3.up * spawnOffset.y
            + normalizedDir * spawnOffset.z;

        var bullet = poolService.GetOrCreateObject(bulletData.Prefab);
        bullet.OnDeactivate += OnDeactivateBullet;
        bullet.InitializeBullet(bulletData, spawnPos, normalizedDir);

        if (notifyAlert)
        {
            alertEmitter?.ReportShot(timestamp, normalizedDir);
        }
    }

    private void OnDeactivateBullet(BulletObject bullet)
    {
        bullet.OnDeactivate -= OnDeactivateBullet;
        PoolService?.ReturnObject(bullet);
    }
}
