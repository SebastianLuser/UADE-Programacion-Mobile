using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Analytics;

public class UGS_Analytics : MonoBehaviour
{
    public static UGS_Analytics Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
            GiveConsent(); // StartDataCollection in the new flow
        }
        catch (ServicesInitializationException e) // from Unity.Services.Core
        {
            Debug.LogError(e);
        }
        catch (RequestFailedException e) // from Unity.Services.Core
        {
            Debug.LogError(e);
        }
        catch (System.Exception e) // fallback
        {
            Debug.LogError(e);
        }
    }

    public void GiveConsent()
    {
        AnalyticsService.Instance.StartDataCollection();
        Debug.Log("Consent has been provided. The SDK is now collecting data!");
    }

    // Event 1: Item collected
    public void LogItemCollected(int pointsAdded, int totalPoints)
    {
        var ev = new CustomEvent("itemCollected")
        {
            ["points_added"] = pointsAdded,
            ["total_points"]  = totalPoints
        };
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"Analytics: Item collected - Points added: {pointsAdded}, Total: {totalPoints}");
    }

    // Event 2: Escape unlocked
    public void LogEscapeUnlocked(int totalPoints)
    {
        var ev = new CustomEvent("escapeUnlocked")
        {
            ["total_points"] = totalPoints
        };
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"Analytics: Escape unlocked at {totalPoints} points");
    }

    // Event 3: Player death
    public void LogPlayerDeath(int finalPoints, int finalHealth)
    {
        var ev = new CustomEvent("playerDeath")
        {
            ["final_points"] = finalPoints,
            ["final_health"] = finalHealth
        };
        AnalyticsService.Instance.RecordEvent(ev);
        Debug.Log($"Analytics: Player died - Points: {finalPoints}, Health: {finalHealth}");
    }
    
    void OnApplicationQuit()
    {
        // Fuerza el envío antes de que se cierre el proceso (útil en Editor/PC)
        AnalyticsService.Instance.Flush();
    }
}
