using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _2._Scripts.UI.Gameplay.Objectives
{
    public class ObjectivesView : UIView
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text objectivesText;
        [SerializeField] private Button dismissButton;
        [SerializeField] private string defaultTitle = "Objetivos";

        public event Action OnDismissRequested;

        public void SetTitle(string title)
        {
            if (!titleText)
            {
                return;
            }

            titleText.text = string.IsNullOrEmpty(title) ? defaultTitle : title;
        }

        public void SetObjectives(IEnumerable<string> objectives)
        {
            if (!objectivesText)
            {
                return;
            }

            objectivesText.text = objectives == null ? string.Empty : string.Join("\n", objectives);
        }

        public override void Show()
        {
            base.Show();
            if (dismissButton)
            {
                dismissButton.onClick.AddListener(OnDismiss);
            }
        }

        public override void Hide()
        {
            base.Hide();
            if (dismissButton)
            {
                dismissButton.onClick.RemoveListener(OnDismiss);
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();
            if (dismissButton)
            {
                dismissButton.onClick.RemoveListener(OnDismiss);
            }
        }

        private void OnDismiss()
        {
            OnDismissRequested?.Invoke();
        }
    }
}
