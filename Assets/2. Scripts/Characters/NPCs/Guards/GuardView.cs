using UnityEngine;

public class GuardView : NPCView
{

    private GuardController guardController;

    protected override void Awake()
    {
        base.Awake();
        guardController = GetComponent<GuardController>();
    }

    protected override float GetCurrentMoveSpeed()
    {
        if (guardController?.GuardData != null)
        {
            if (guardController.Model?.RuntimeState.isPlayerVisible == true)
            {
                return guardController.GuardData.chaseSpeed;
            }
            return guardController.GuardData.patrolSpeed;
        }

        return guardController?.Model?.RuntimeState.isPlayerVisible == true ? 4f : 2f;
    }

    protected override float GetRotationSpeed()
    {
        return guardController?.GuardData?.rotationSpeed ?? 5f;
    }

    public void PlayAttackAnimation()
    {
        // TODO: Implement attack animation
        Logger.LogDebug($"{gameObject.name}: Playing attack animation");
    }

    public void PlayChaseAnimation()
    {
        // TODO: Implement chase animation
        Logger.LogDebug($"{gameObject.name}: Playing chase animation");
    }

    public void PlayPatrolAnimation()
    {
        // TODO: Implement patrol animation
        Logger.LogDebug($"{gameObject.name}: Playing patrol animation");
    }

    public void PlaySearchAnimation()
    {
        // TODO: Implement search animation
        Logger.LogDebug($"{gameObject.name}: Playing search animation");
    }

    public void PlayIdleAnimation()
    {
        // TODO: Implement idle animation
        Logger.LogDebug($"{gameObject.name}: Playing idle animation");
    }
}