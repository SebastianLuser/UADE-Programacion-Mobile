using UnityEngine;

/// <summary>
/// Handles melee damage application for AI characters.
/// </summary>
[DisallowMultipleComponent]
public class MeleeCombatModule : MonoBehaviour
{
    [SerializeField] private int meleeDamage = 1;
    [SerializeField] private bool enableDebugLogs = false;

    public int Damage => meleeDamage;

    public bool TryDealDamage(Transform target)
    {
        if (target == null) return false;

        var damageable = target.GetComponent<IDamageable>();
        if (damageable == null) return false;

        damageable.TakeDamage(meleeDamage);

        if (enableDebugLogs)
        {
            MyLogger.LogInfo($"{name}: Dealt {meleeDamage} melee damage to {target.name}");
        }

        return true;
    }
}
