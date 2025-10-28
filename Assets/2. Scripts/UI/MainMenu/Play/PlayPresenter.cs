using Unity.Assertions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _2._Scripts.UI.MainMenu.Play
{
    public class PlayPresenter : UIPresenter
    {
        [SerializeField] private string mainUIName = "Main";
        private PlayView m_playView;

        public override void Initialize()
        {
            base.Initialize();
            m_playView = uiView as PlayView;
            Assert.IsNotNull(m_playView);
        }

        public override void Show()
        {
            base.Show();
            m_playView.OnStartClicked += OnStartClickedHandler;
            m_playView.OnBackClicked += OnBackClickedHandler;
        }

        public override void Hide()
        {
            base.Hide();
            m_playView.OnStartClicked -= OnStartClickedHandler;
            m_playView.OnBackClicked -= OnBackClickedHandler;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            m_playView.OnStartClicked -= OnStartClickedHandler;
            m_playView.OnBackClicked -= OnBackClickedHandler;
        }

        private static void OnStartClickedHandler()
        {
            SceneManager.LoadScene("DemoProto");
        }
        
        private void OnBackClickedHandler()
        {
            panelsController.ShowUI(mainUIName);
        }
    }
}