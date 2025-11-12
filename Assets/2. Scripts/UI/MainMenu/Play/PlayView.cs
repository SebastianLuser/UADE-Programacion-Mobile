using System;
using UnityEngine;
using UnityEngine.UI;

namespace _2._Scripts.UI.MainMenu.Play
{
    public class PlayView : UIView
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        public event Action OnStartClicked;
        public event Action OnBackClicked;
        
        public override void Show()
        {
            base.Show();
            startButton.onClick.AddListener(OnStartButtonHandler);
            backButton.onClick.AddListener(OnBackButtonHandler);
        }

        public override void Hide()
        {
            base.Hide();
            startButton.onClick.RemoveListener(OnStartButtonHandler);
            backButton.onClick.RemoveListener(OnBackButtonHandler);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            startButton.onClick.RemoveListener(OnStartButtonHandler);
            backButton.onClick.RemoveListener(OnBackButtonHandler);
        }

        private void OnStartButtonHandler()
        {
            OnStartClicked?.Invoke();
        }
        
        private void OnBackButtonHandler()
        {
            OnBackClicked?.Invoke();
        }
    }
}