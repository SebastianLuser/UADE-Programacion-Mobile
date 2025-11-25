using System;
using _2._Scripts.UI.Gameplay.Results;
using Services;
using Services.MicroServices.GameStateService;
using Services.MicroServices.UserDataService.Wallet;
using Unity.Services.LevelPlay;
using UnityEngine;


/*
Org Core Id: 4673777054199
Apple Id: 5990864
Android Id: 5990865
Monetization Stats API Key: 9efc50a100668aaeedaf2fd8a6b38cee05e6a1f38ac908d308771b47875e72c7
*/


public class RewardedAd : MonoBehaviour
{
    // App Configuration - LevelPlay Dashboard
    private const string k_AndroidAppKey = "5990865";
    private const string k_AppleApplKey = "5990864";

    // Dependencies
    [SerializeField] private PlayerCollector _playerCollector;
    [SerializeField] private ResultsView _resultsView;

    // Ad Unit/Placement
    [SerializeField]
    private string m_AdUnitId = "2sv49kubzjpb3rfq";
    [Header("Analytics")]
    [SerializeField] private string analyticsPlacementId = "duplicate_reward";
    [SerializeField] private string analyticsSourcePanel = "Results";

    // Runtime State
    private bool m_IsInitialized;
    private bool m_RewardGranted;
    private LevelPlayRewardedAd m_RewardedAd;

    // Convenience events for UI updates
    public event Action<bool> AdSuccessfullyCompleted;
    public event Action<bool> AdAvailable;
    
    private void Start()
    {
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

        LevelPlay.Init(appKey);

        LevelPlay.SetPauseGame(true);
    }

    /// <summary>
    /// Gets the appropriate app key based on the current platform.
    /// </summary>
    /// <returns>The platform-specific app key</returns>
    private string GetPlatformAppKey()
    {
#if UNITY_ANDROID
    return k_AndroidAppKey;
#elif UNITY_IPHONE
    return k_AppleAppKey;
#else
MyLogger.LogWarning("Unexpected platform for ads");
        return "unexpected_platform";
#endif
    }
    
    /// <summary>
    /// Callback when the LevelPlay SDK is initialized successfully.
    /// Creates the rewarded ad object, registers to ad events, and loads the first ad.
    /// </summary>
    private void SdkInitializationCompleted(LevelPlayConfiguration configuration)
    {
        if (m_IsInitialized) return;

        m_IsInitialized = true;
        MyLogger.LogDebug("LevelPlay SDK initialized successfully");

#if DEVELOPMENT_BUILD
    // TODO Remove ValidateIntegration once logs confirm networks are VERIFIED and you have your device's Advertising ID setup as a test device
    LevelPlay.ValidateIntegration();
    
    LaunchTestSuite();
        MyLogger.LogDebug("Launching test suite");

#endif

        CreateRewardedAd();

        // Set listeners before loading rewarded ad
        RegisterToAdEvents();

        LoadRewardedAd();
    }

    private void sdkInitializationFailed(LevelPlayInitError error)
    {
        MyLogger.LogWarning("Error al inizializar el SDK: " + error.ErrorMessage);
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
            UGS_Analytics.Instance?.LogRewardAdStarted(analyticsPlacementId, analyticsSourcePanel);
            ShowRewardedAd();
        }
        else
        {
            UGS_Analytics.Instance?.LogRewardAdAborted(analyticsPlacementId, analyticsSourcePanel, "not_ready");
            MyLogger.LogWarning($"Cannot show ad.");
        }
    }
        
    public bool CanShowAd()
    {
        if (!m_IsInitialized)
        {
            MyLogger.LogWarning("SDK not initialized");
            return false;
        }

        if (m_RewardedAd == null)
        {
            MyLogger.LogWarning("Rewarded ad object not created");
            return false;
        }

        bool isAdReady = m_RewardedAd.IsAdReady();

        if (!isAdReady)
        {
            MyLogger.LogWarning("Ad not ready - still loading or no inventory available");
        }
            
        return isAdReady;
    }
    #endregion

    #region Ad event Callbacks
        
    // Load Events

    private void HandleAdLoadedSuccessfully(LevelPlayAdInfo adInfo)
    {
        AdAvailable?.Invoke(true);
        MyLogger.LogDebug($"Rewarded ad loaded: {adInfo.AdNetwork}");
    }
    
    private void HandleLoadFailed(LevelPlayAdError error)
    {
        MyLogger.LogWarning($"Rewarded ad failed to load: {error.ErrorMessage} (Code: {error.ErrorCode})");
        UGS_Analytics.Instance?.LogRewardAdAborted(analyticsPlacementId, analyticsSourcePanel, $"load_failed_{error.ErrorCode}");
        Invoke(nameof(LoadRewardedAd), 2f);
    }
    
    // Display Events

    private void HandleAdDisplayed(LevelPlayAdInfo adInfo)
    {
        MyLogger.LogDebug("Rewarded ad displayed");
    }

    private void HandleAdFailedToDisplay(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        MyLogger.LogWarning($"$Rewarded ad failed to display: {error}");
        AdSuccessfullyCompleted?.Invoke(false);
        UGS_Analytics.Instance?.LogRewardAdAborted(analyticsPlacementId, analyticsSourcePanel, $"display_failed_{error.ErrorCode}");
    }

    private async void ProcessAdReward(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        if (m_RewardGranted)
        {
            MyLogger.LogWarning("Reward already granted for this ad impression, ignoring duplicate callback.");
            return;
        }

        m_RewardGranted = true;
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

        _resultsView.RewardedAdShowed = true;

        var walletService = ServiceLocator.Get<IWalletService>();
        int bonusCoins = _playerCollector.SessionCoins;
        int bonusDiamonds = _playerCollector.SessionDiamonds;

        // Grant only the bonus once to avoid duplicating the original session rewards.
        walletService?.AddCoins(bonusCoins);
        walletService?.AddDiamonds(bonusDiamonds);

        int finalScore = _playerCollector.TotalPoints * 2;
        int finalCoins = _playerCollector.SessionCoins + bonusCoins;
        int finalDiamonds = _playerCollector.SessionDiamonds + bonusDiamonds;

        if (gameWinned)
        {
            _resultsView.DisplayVictory(finalScore, finalCoins, finalDiamonds);
        }
        else
        {
            _resultsView.DisplayDefeat(finalScore, finalCoins, finalDiamonds);
        }
        

        MyLogger.LogDebug($"Ad reward granted successfully: {reward.Name} x{reward.Amount}");
        UGS_Analytics.Instance?.LogRewardAdCompleted(analyticsPlacementId, analyticsSourcePanel);
    }
    
    // Completion events
    private void HandleAdClosed(LevelPlayAdInfo adInfo)
    {
        MyLogger.LogDebug("Rewarded ad closed");

        if (!m_RewardGranted)
        {
            UGS_Analytics.Instance?.LogRewardAdAborted(analyticsPlacementId, analyticsSourcePanel, "closed_no_reward");
        }

        m_RewardGranted = false;
        // Load another ad for next time
        LoadRewardedAd();
    }

    private void HandleAdClicked(LevelPlayAdInfo adInfo)
    {
        MyLogger.LogDebug("Rewarded ad clicked");
    }

    private void HandleAdInfoChanged(LevelPlayAdInfo adInfo)
    {
        MyLogger.LogDebug($"Rewarded ad info changed: {adInfo.AdNetwork}");

        if (adInfo != null)
        {
            MyLogger.LogDebug($"Updated ad info - Network: {adInfo.AdNetwork}, Instance: {adInfo.InstanceId}");
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
