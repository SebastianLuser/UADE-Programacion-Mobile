using UnityEngine;

public class NPCModel
{
    private NPCDataSO data;
    private NPCRuntimeState runtimeState;
    
    public NPCDataSO Data => data;
    public NPCRuntimeState RuntimeState => runtimeState;
    
    public NPCModel(NPCDataSO npcData)
    {
        data = npcData;
        runtimeState = new NPCRuntimeState();
        runtimeState.Initialize(data.maxHealth);
    }
    
    public void UpdateDetection(bool isVisible, Transform target)
    {
        bool wasVisible = runtimeState.isPlayerVisible;
        runtimeState.isPlayerVisible = isVisible;
        
        if (isVisible && target != null)
        {
            runtimeState.currentTarget = target;
            runtimeState.lastKnownPlayerPosition = target.position;
        }
    }
    
    public void UpdateTimer(float deltaTime)
    {
        runtimeState.stateTimer += deltaTime;
    }
}