using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class Initializer : MonoBehaviour
{
    [SerializeField] private BaseManager[] managers;

    private void Awake()
    {
        foreach (var manager in managers)
        {
            manager.Initialize();
        }
    }

    private void OnDestroy()
    {
        foreach (var manager in managers)
        {
            Destroy(manager);
        }
    }
}
