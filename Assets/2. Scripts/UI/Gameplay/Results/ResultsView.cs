using System;
using Services;
using Services.MicroServices.AudioService;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _2._Scripts.UI.Gameplay.Results
{
    public class ResultsView : UIView
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private TMP_Text coinsRewardText;
        [SerializeField] private TMP_Text diamondsRewardText;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Copy")]
        [SerializeField] private string victoryTitle = "Victoria";
        [SerializeField] private string defeatTitle = "Derrota";
        [SerializeField] private string scoreFormat = "Puntaje: {0}";
        [SerializeField] private string coinsFormat = "+{0} Coins";
        [SerializeField] private string diamondsFormat = "+{0} Diamonds";
        [SerializeField, TextArea] private string victoryDetails = "Excelente trabajo.";
        [SerializeField, TextArea] private string defeatDetails = "Intentá nuevamente.";

        public event Action OnRetry;
        public event Action OnMainMenu;

        private IAudioService m_audioService;
        private AudioConfig m_audioConfig;

        private void Awake()
        {
            m_audioService = ServiceLocator.Get<IAudioService>();
            m_audioConfig = (m_audioService as AudioService)?.Config;
        }

        public void DisplayVictory(int score, int coins, int diamonds)
        {
            if (m_audioService != null && m_audioConfig != null)
            {
                m_audioService.PlaySFX(m_audioConfig.winSFX);
            }

            SetTexts(victoryTitle, victoryDetails, score, coins, diamonds);
        }

        public void DisplayDefeat(int score, int coins, int diamonds)
        {
            if (m_audioService != null && m_audioConfig != null)
            {
                m_audioService.PlaySFX(m_audioConfig.lostSFX);
            }

            SetTexts(defeatTitle, defeatDetails, score, coins, diamonds);
        }

        private void SetTexts(string title, string details, int score, int coins, int diamonds)
        {
            if (titleText)
            {
                titleText.text = title;
            }

            if (detailsText)
            {
                detailsText.text = details;
            }

            if (scoreText)
            {
                scoreText.text = string.Format(scoreFormat, score);
            }

            if (coinsRewardText)
            {
                coinsRewardText.text = string.Format(coinsFormat, coins);
            }

            if (diamondsRewardText)
            {
                diamondsRewardText.text = string.Format(diamondsFormat, diamonds);
            }
        }

        public override void Show()
        {
            base.Show();
            if (retryButton)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
            }

            if (mainMenuButton)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
        }

        public override void Hide()
        {
            base.Hide();
            if (retryButton)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (mainMenuButton)
            {
                mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();
            if (retryButton)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (mainMenuButton)
            {
                mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            }
        }

        private void OnRetryClicked()
        {
            if (m_audioService != null && m_audioConfig != null)
            {
                m_audioService.PlaySFX(m_audioConfig.clickButtonSFX);
            }

            OnRetry?.Invoke();
        }

        private void OnMainMenuClicked()
        {
            if (m_audioService != null && m_audioConfig != null)
            {
                m_audioService.PlaySFX(m_audioConfig.clickButtonSFX);
            }

            OnMainMenu?.Invoke();
        }
    }
}
