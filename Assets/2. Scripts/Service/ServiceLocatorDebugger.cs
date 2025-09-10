using System;
using System.Collections.Generic;
using UnityEngine;
using DevelopmentUtilities.DictionaryUtilities;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class ServiceInfo
{
    public string typeName;
    public string status;
    public bool isInitialized;
    
    public ServiceInfo(string name, string serviceStatus, bool initialized)
    {
        typeName = name;
        status = serviceStatus;
        isInitialized = initialized;
    }
}

[Serializable]
public class ServiceDictionary : SerializableDictionary<string, ServiceInfo> { }

[CreateAssetMenu(fileName = "ServiceLocatorDebugger", menuName = "Debug/Service Locator Debugger")]
public class ServiceLocatorDebugger : ScriptableObject
{
    [Header("Service Registry Debug")]
    [SerializeField] private ServiceDictionary registeredServices = new ServiceDictionary();
    [SerializeField] private bool autoRefresh = true;
    [SerializeField] private float refreshInterval = 1f;
    
    private float lastRefreshTime;
    
    public void RefreshServices()
    {
        registeredServices.Clear();
        
        var serviceTypes = ServiceLocator.GetRegisteredServiceTypes();
        
        foreach (var serviceType in serviceTypes)
        {
            var isInitialized = false;
            var status = "Not Initialized";
            
            if (ServiceLocator.TryGetServiceObject(serviceType, out var service))
            {
                if (service is IGameService gameService)
                {
                    isInitialized = gameService.IsInitialized;
                    status = isInitialized ? "Initialized" : "Registered but not initialized";
                }
                else
                {
                    status = "Legacy service (no IGameService)";
                }
            }
            
            registeredServices[serviceType.Name] = new ServiceInfo(serviceType.Name, status, isInitialized);
        }
        
        lastRefreshTime = Time.time;
        
        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        #endif
    }
    
    private void OnEnable()
    {
        RefreshServices();
    }
    
    private void OnValidate()
    {
        if (autoRefresh && Time.time - lastRefreshTime > refreshInterval)
        {
            RefreshServices();
        }
    }
    
    public void LogAllServices()
    {
        Logger.LogInfo("=== Service Locator Debug Report ===");
        foreach (var kvp in registeredServices)
        {
            Logger.LogInfo($"Service: {kvp.Key} | Status: {kvp.Value.status} | Initialized: {kvp.Value.isInitialized}");
        }
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(ServiceLocatorDebugger))]
    public class ServiceLocatorDebuggerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var debugger = (ServiceLocatorDebugger)target;
            
            EditorGUILayout.LabelField("Service Locator Debugger", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Refresh Services"))
            {
                debugger.RefreshServices();
            }
            
            if (GUILayout.Button("Log All Services"))
            {
                debugger.LogAllServices();
            }
            
            if (GUILayout.Button("Initialize All Services"))
            {
                ServiceLocator.InitializeAllServices();
                debugger.RefreshServices();
            }
            
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
    #endif
}