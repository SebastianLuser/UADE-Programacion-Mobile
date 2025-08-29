using System.Collections.Generic;
using UnityEngine;

public class UpdateManager : BaseManager
{
    private readonly List<IUpdatable> updatables = new List<IUpdatable>();
    private readonly List<IFixedUpdatable> fixedUpdatables = new List<IFixedUpdatable>();
    private readonly List<ILateUpdatable> lateUpdatables = new List<ILateUpdatable>();
    
    private readonly List<IUpdatable> updatablesToRemove = new List<IUpdatable>();
    private readonly List<IFixedUpdatable> fixedUpdatablesToRemove = new List<IFixedUpdatable>();
    private readonly List<ILateUpdatable> lateUpdatablesToRemove = new List<ILateUpdatable>();
    
    public int UpdateCount => updatables.Count;
    public int FixedUpdateCount => fixedUpdatables.Count;
    public int LateUpdateCount => lateUpdatables.Count;
    
    protected override void OnInitialize()
    {
        ServiceLocator.Register<UpdateManager>(this);
    }
    
    public void RegisterUpdatable(IUpdatable updatable)
    {
        if (updatable != null && !updatables.Contains(updatable))
        {
            updatables.Add(updatable);
        }
    }
    
    public void RegisterFixedUpdatable(IFixedUpdatable fixedUpdatable)
    {
        if (fixedUpdatable != null && !fixedUpdatables.Contains(fixedUpdatable))
        {
            fixedUpdatables.Add(fixedUpdatable);
        }
    }
    
    public void RegisterLateUpdatable(ILateUpdatable lateUpdatable)
    {
        if (lateUpdatable != null && !lateUpdatables.Contains(lateUpdatable))
        {
            lateUpdatables.Add(lateUpdatable);
        }
    }
    
    public void UnregisterUpdatable(IUpdatable updatable)
    {
        if (updatable != null)
        {
            updatablesToRemove.Add(updatable);
        }
    }
    
    public void UnregisterFixedUpdatable(IFixedUpdatable fixedUpdatable)
    {
        if (fixedUpdatable != null)
        {
            fixedUpdatablesToRemove.Add(fixedUpdatable);
        }
    }
    
    public void UnregisterLateUpdatable(ILateUpdatable lateUpdatable)
    {
        if (lateUpdatable != null)
        {
            lateUpdatablesToRemove.Add(lateUpdatable);
        }
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        float deltaTime = Time.deltaTime;
        
        for (int i = updatables.Count - 1; i >= 0; i--)
        {
            var updatable = updatables[i];
            if (updatable != null && updatable.IsActive)
            {
                updatable.OnUpdate(deltaTime);
            }
            else if (updatable == null)
            {
                updatables.RemoveAt(i);
            }
        }
        
        ProcessRemovals();
    }
    
    private void FixedUpdate()
    {
        if (!isInitialized) return;
        
        float fixedDeltaTime = Time.fixedDeltaTime;
        
        for (int i = fixedUpdatables.Count - 1; i >= 0; i--)
        {
            var fixedUpdatable = fixedUpdatables[i];
            if (fixedUpdatable != null && fixedUpdatable.IsActive)
            {
                fixedUpdatable.OnFixedUpdate(fixedDeltaTime);
            }
            else if (fixedUpdatable == null)
            {
                fixedUpdatables.RemoveAt(i);
            }
        }
    }
    
    private void LateUpdate()
    {
        if (!isInitialized) return;
        
        float deltaTime = Time.deltaTime;
        
        for (int i = lateUpdatables.Count - 1; i >= 0; i--)
        {
            var lateUpdatable = lateUpdatables[i];
            if (lateUpdatable != null && lateUpdatable.IsActive)
            {
                lateUpdatable.OnLateUpdate(deltaTime);
            }
            else if (lateUpdatable == null)
            {
                lateUpdatables.RemoveAt(i);
            }
        }
    }
    
    private void ProcessRemovals()
    {
        if (updatablesToRemove.Count > 0)
        {
            foreach (var updatable in updatablesToRemove)
            {
                updatables.Remove(updatable);
            }
            updatablesToRemove.Clear();
        }
        
        if (fixedUpdatablesToRemove.Count > 0)
        {
            foreach (var fixedUpdatable in fixedUpdatablesToRemove)
            {
                fixedUpdatables.Remove(fixedUpdatable);
            }
            fixedUpdatablesToRemove.Clear();
        }
        
        if (lateUpdatablesToRemove.Count > 0)
        {
            foreach (var lateUpdatable in lateUpdatablesToRemove)
            {
                lateUpdatables.Remove(lateUpdatable);
            }
            lateUpdatablesToRemove.Clear();
        }
    }
    
    public void PauseAll()
    {
        enabled = false;
    }
    
    public void ResumeAll()
    {
        enabled = true;
    }
    
    protected override void OnShutdown()
    {
        updatables.Clear();
        fixedUpdatables.Clear();
        lateUpdatables.Clear();
        updatablesToRemove.Clear();
        fixedUpdatablesToRemove.Clear();
        lateUpdatablesToRemove.Clear();
        
        ServiceLocator.Unregister<UpdateManager>();
    }
}