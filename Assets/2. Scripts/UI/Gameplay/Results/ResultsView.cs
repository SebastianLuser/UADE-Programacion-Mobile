using System;
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

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Copy")]
        [SerializeField] private string victoryTitle = "Victoria";
        [SerializeField] private string defeatTitle = "Derrota";
        [SerializeField] private string scoreFormat = "Puntaje: {0}";
        [SerializeField, TextArea] private string victoryDetails = "Excelente trabajo.";
        [SerializeField, TextArea] private string defeatDetails = "Intentá nuevamente.";

        public event Action OnRetry;
        public event Action OnMainMenu;

        public void DisplayVictory(int score)
        {
            SetTexts(victoryTitle, victoryDetails, score);
        }

        public void DisplayDefeat(int score)
        {
            SetTexts(defeatTitle, defeatDetails, score);
        }

        private void SetTexts(string title, string details, int score)
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
            OnRetry?.Invoke();
        }

        private void OnMainMenuClicked()
        {
            OnMainMenu?.Invoke();
        }
    }
}
