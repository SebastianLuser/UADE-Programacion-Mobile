using _2._Scripts.UI.Gameplay.Results;
using Services;
using Services.MicroServices.GameStateService;
using Services.MicroServices.UserDataService.Wallet;
using UnityEngine;
using UnityEngine.Advertisements;

public class RewardedAd : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
    private PlayerCollector m_playerCollector;
    private ResultsView m_resultsPresenter;
    
    [Header("Analytics")]
    [SerializeField] private string analyticsPlacementId = "duplicate_reward";
    [SerializeField] private string analyticsSourcePanel = "Results";
    
#if UNITY_ANDROID
    private const string AD_UNIT_ID = "";
#elif UNITY_IOS
    private const string AD_UNIT_ID = "";
#else
    private const string AD_UNIT_ID = "";
#endif

    private void Start()
    {
        Load();
    }

    private void Load()
    {
        Advertisement.Load(AD_UNIT_ID, this);
    }

    public void Show(PlayerCollector p_playerCollector, ResultsView p_resultsPresenter)
    {
        m_playerCollector = p_playerCollector;
        m_resultsPresenter = p_resultsPresenter;
        
        Advertisement.Show(AD_UNIT_ID, this);
    }

    public void OnUnityAdsAdLoaded(string p_adUnitId)
    {
    }

    public void OnUnityAdsFailedToLoad(string p_adUnitId, UnityAdsLoadError p_error, string
        p_message)
    {
        UGS_Analytics.Instance?.LogRewardAdAborted(analyticsPlacementId, analyticsSourcePanel, $"load_failed: {p_error}");
    }

    public void OnUnityAdsShowStart(string p_adUnitId)
    {
        UGS_Analytics.Instance?.LogRewardAdStarted(analyticsPlacementId, analyticsSourcePanel);
    }

    public void OnUnityAdsShowClick(string p_adUnitId)
    {
    }

    public void OnUnityAdsShowComplete(string p_adUnitId, UnityAdsShowCompletionState
        p_showCompletionState)
    {
        if (p_showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            GrantReward();
            UGS_Analytics.Instance?.LogRewardAdCompleted(analyticsPlacementId, analyticsSourcePanel);
        }
        else
        {
            UGS_Analytics.Instance?.LogRewardAdAborted(analyticsPlacementId, analyticsSourcePanel, "not_ready");
        }

        Load();
    }

    public void OnUnityAdsShowFailure(string p_adUnitId, UnityAdsShowError p_error, string
        p_message)
    {
        UGS_Analytics.Instance?.LogRewardAdAborted(analyticsPlacementId, analyticsSourcePanel, $"display failed: {p_error}");
        Load();
    }

    private void GrantReward()
    {
        if (m_playerCollector == null || m_resultsPresenter == null)
            return;
        
        var l_gameWined = ServiceLocator.Get<IGameStateService>().GetCurrentState() == GameState.Victory;

        m_resultsPresenter.RewardedAdShowed = true;

        var l_walletService = ServiceLocator.Get<IWalletService>();
        var l_bonusCoins = m_playerCollector.SessionCoins;
        var l_bonusDiamonds = m_playerCollector.SessionDiamonds;

        // Grant only the bonus once to avoid duplicating the original session rewards.
        l_walletService?.AddCoins(l_bonusCoins);
        l_walletService?.AddDiamonds(l_bonusDiamonds);

        var l_finalScore = m_playerCollector.TotalPoints * 2;
        var l_finalCoins = m_playerCollector.SessionCoins + l_bonusCoins;
        var l_finalDiamonds = m_playerCollector.SessionDiamonds + l_bonusDiamonds;

        if (l_gameWined)
        {
            m_resultsPresenter.DisplayVictory(l_finalScore, l_finalCoins, l_finalDiamonds);
        }
        else
        {
            m_resultsPresenter.DisplayDefeat(l_finalScore, l_finalCoins, l_finalDiamonds);
        }

        MyLogger.LogDebug($"Ad reward granted successfully");
        UGS_Analytics.Instance?.LogRewardAdCompleted(analyticsPlacementId, analyticsSourcePanel);
    }
}