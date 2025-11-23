using System;
using Newtonsoft.Json;
using Services;
using Services.MicroServices.EventsServices;
using Services.MicroServices.EventsServices.CustomEvents;
using Services.MicroServices.GameStateService;
using Unity.Services.LevelPlay;
using UnityEngine;



public class RewardedAd : MonoBehaviour

{
// App Configuration - LevelPlay Dashboard
    private const string k_AndroidAppKey = "2457c894d";

    // Dependencies
    [SerializeField] private PlayerCollector _playerCollector;

    // Ad Unit/Placement
    [SerializeField]
    private string m_AdUnitId = "2sv49kubzjpb3rfq";

    // Runtime State
    private bool m_IsInitialized;
    private LevelPlayRewardedAd m_RewardedAd;

    // Convenience events for UI updates
    public event Action<bool> AdSuccessfullyCompleted;
    public event Action<bool> AdAvailable;
    
    private void Start()
    {
        Debug.Log("Start rewardedAd");
        RegisterSDKEvents();

        InitializeRewardedAds();
    }

    #region SDK Initialization

    private void RegisterSDKEvents()
    {
        LevelPlay.OnInitSuccess += SdkInitializationCompleted;
        LevelPlay.OnInitFailed += sdkInitializationFailed;
    }
    
    #endregion
    
    private void InitializeRewardedAds()
    {
        
        string appKey = GetPlatformAppKey();

        LevelPlay.SetMetaData("is_test_suite", "enable");
        Debug.Log("Call LevelPlay Init");

        LevelPlay.Init(appKey);

        LevelPlay.SetPauseGame(true);
    }

    /// <summary>
    /// Gets the appropriate app key based on the current platform.
    /// </summary>
    /// <returns>The platform-specific app key</returns>
    private string GetPlatformAppKey()
    {
        return k_AndroidAppKey;
    }
    
    /// <summary>
    /// Callback when the LevelPlay SDK is initialized successfully.
    /// Creates the rewarded ad object, registers to ad events, and loads the first ad.
    /// </summary>
    private void SdkInitializationCompleted(LevelPlayConfiguration configuration)
    {
        if (m_IsInitialized) return;

        m_IsInitialized = true;
        Debug.Log("LevelPlay SDK initialized successfully");

#if DEVELOPMENT_BUILD
    // TODO Remove ValidateIntegration once logs confirm networks are VERIFIED and you have your device's Advertising ID setup as a test device
    LevelPlay.ValidateIntegration();
    
    LaunchTestSuite();
    Debug.Log("Launching test suite");

#endif

        CreateRewardedAd();

        // Set listeners before loading rewarded ad
        RegisterToAdEvents();

        LoadRewardedAd();
    }

    private void sdkInitializationFailed(LevelPlayInitError error)
    {
        Debug.Log("Error al inizializar el SDK: " + error.ErrorMessage);
    }

    /// <summary>
    /// Opens the test suite for debugging ad integration.
    /// </summary>
    private void LaunchTestSuite()
    {
        LevelPlay.LaunchTestSuite();
    }
    
    #region Ad Management

    private void CreateRewardedAd()
    {
        m_RewardedAd = new LevelPlayRewardedAd(m_AdUnitId);
    }

    private void RegisterToAdEvents()
    {
        // Load events
        m_RewardedAd.OnAdLoaded += HandleAdLoadedSuccessfully;
        m_RewardedAd.OnAdLoadFailed += HandleLoadFailed;

        // Display events
        m_RewardedAd.OnAdDisplayed += HandleAdDisplayed;
        m_RewardedAd.OnAdDisplayFailed += HandleAdFailedToDisplay;

        // Reward event
        m_RewardedAd.OnAdRewarded += ProcessAdReward;

        // Completion events
        m_RewardedAd.OnAdClosed += HandleAdClosed;

        // Optional
        m_RewardedAd.OnAdClicked += HandleAdClicked;
        m_RewardedAd.OnAdInfoChanged += HandleAdInfoChanged;
    }

    private void LoadRewardedAd()
    {
        if (m_RewardedAd != null)
        {
            m_RewardedAd.LoadAd();
        }
    }

        private void ShowRewardedAd()
        {
            m_RewardedAd.ShowAd();
        }

        #endregion

        #region Public Interface
        /// <summary>
        /// User-facing method to show a rewarded ad when a button is clicked.
        /// Checks availability before showing the ad.
        /// </summary>
        public void ClickShowAdReward()
        {
            if (CanShowAd())
            {
                ShowRewardedAd();
            }
            else
            {
                Debug.LogWarning($"Cannot show ad.");
            }
        }
        
        public bool CanShowAd()
        {
            if (!m_IsInitialized)
            {
                Debug.LogWarning("SDK not initialized");
                return false;
            }

            if (m_RewardedAd == null)
            {
                Debug.LogWarning("Rewarded ad object not created");
                return false;
            }

            bool isAdReady = m_RewardedAd.IsAdReady();

            if (!isAdReady)
            {
                Debug.LogWarning("Ad not ready - still loading or no inventory available");
            }
            
            return isAdReady;
        }
        #endregion

        #region Ad event Callbacks
        
    // Load Events

    private void HandleAdLoadedSuccessfully(LevelPlayAdInfo adInfo)
    {
        AdAvailable?.Invoke(true);
        Debug.Log($"Rewarded ad loaded: {adInfo.AdNetwork}");
    }
    
    private void HandleLoadFailed(LevelPlayAdError error)
    {
        Debug.LogError($"Rewarded ad failed to load: {error.ErrorMessage} (Code: {error.ErrorCode})");
        Invoke(nameof(LoadRewardedAd), 2f);
    }
    
    // Display Events

    private void HandleAdDisplayed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Rewardede ad displayed");
    }

    private void HandleAdFailedToDisplay(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogError($"$Rewarded ad failed to display: {error}");
        AdSuccessfullyCompleted?.Invoke(false);
    }

    private async void ProcessAdReward(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
            AdSuccessfullyCompleted?.Invoke(true);

            bool gameWinned;

            if (ServiceLocator.Get<IGameStateService>().GetCurrentState() == GameState.Victory)
            {
                gameWinned = true;
            }
            else
            {
                gameWinned = false;
            }
            
            
            ServiceLocator.Get<IEventService>().DispatchEvent(new GameResultEvent(gameWinned, 
                _playerCollector.TotalPoints * 2, _playerCollector.SessionCoins  * 2, 
                _playerCollector.SessionDiamonds  * 2));
        

            Debug.Log($"Ad reward granted successfully: {reward.Name} x{reward.Amount}");
    }
    
    // Completion events
    private void HandleAdClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Rewarded ad closed");

        // Load another ad for next time
        LoadRewardedAd();
    }

    private void HandleAdClicked(LevelPlayAdInfo adInfo)
    {
        Debug.Log("Rewarded ad clicked");
    }

    private void HandleAdInfoChanged(LevelPlayAdInfo adInfo)
    {
        Debug.Log($"Rewarded ad info changed: {adInfo.AdNetwork}");

        if (adInfo != null)
        {
            Debug.Log($"Updated ad info - Network: {adInfo.AdNetwork}, Instance: {adInfo.InstanceId}");
        }
    }
    
    // Cleanup
    private void OnDestroy()
    {
        RemoveEventHandlers();
    }

    private void RemoveEventHandlers()
    {
        // Remove SDK level events
        LevelPlay.OnInitSuccess -= SdkInitializationCompleted;

        // Remove ad-specific events
        if (m_RewardedAd != null)
        {
            m_RewardedAd.OnAdLoaded -= HandleAdLoadedSuccessfully;
            m_RewardedAd.OnAdLoadFailed -= HandleLoadFailed;
            m_RewardedAd.OnAdDisplayed -= HandleAdDisplayed;
            m_RewardedAd.OnAdDisplayFailed -= HandleAdFailedToDisplay;
            m_RewardedAd.OnAdRewarded -= ProcessAdReward;
            m_RewardedAd.OnAdClosed -= HandleAdClosed;
            m_RewardedAd.OnAdClicked -= HandleAdClicked;
            m_RewardedAd.OnAdInfoChanged -= HandleAdInfoChanged;
        }
    }

    #endregion
}