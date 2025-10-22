using UnityEngine;

public class CivilianView : NPCView
{

    private CivilianController civilianController;

    protected override void Awake()
    {
        base.Awake();
        civilianController = GetComponent<CivilianController>();
    }

    protected override float GetCurrentMoveSpeed()
    {
        if (civilianController?.CivilianData != null)
        {
            if (civilianController.Model?.RuntimeState.isPlayerVisible == true)
            {
                return civilianController.CivilianData.fleeSpeed;
            }
            return civilianController.CivilianData.patrolSpeed;
        }

        return civilianController?.Model?.RuntimeState.isPlayerVisible == true ? 3f : 2f;
    }

    protected override float GetRotationSpeed()
    {
        return civilianController?.CivilianData?.rotationSpeed ?? 5f;
    }

    public void PlayPanicAnimation()
    {
        // TODO: Implement panic animation
        MyLogger.LogDebug($"{gameObject.name}: Playing panic animation");
    }

    public void PlayFleeAnimation()
    {
        // TODO: Implement flee animation
        MyLogger.LogDebug($"{gameObject.name}: Playing flee animation");
    }

    public void PlayNormalAnimation()
    {
        // TODO: Implement normal/calm animation
        MyLogger.LogDebug($"{gameObject.name}: Playing normal animation");
    }

    public void PlayIdleAnimation()
    {
        // TODO: Implement idle animation
        MyLogger.LogDebug($"{gameObject.name}: Playing idle animation");
    }

    public void ShowFearEffect()
    {
        // TODO: Implement fear visual effect (particles, color change, etc.)
        MyLogger.LogDebug($"{gameObject.name}: Showing fear effect");
    }

    public void HideFearEffect()
    {
        // TODO: Hide fear visual effect
        MyLogger.LogDebug($"{gameObject.name}: Hiding fear effect");
    }
}