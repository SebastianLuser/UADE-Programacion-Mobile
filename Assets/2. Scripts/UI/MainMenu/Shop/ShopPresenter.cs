using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace _2._Scripts.UI.MainMenu.Shop
{
    public class ShopPresenter : UIPresenter
    {
        [SerializeField] private string mainUIName = "Main";
        [SerializeField] private ShopCategory defaultCategory = ShopCategory.Weapons;
        [SerializeField] private int placeholderCoins = 5200;
        [SerializeField] private int placeholderDiamonds = 100;

        private ShopModel m_model;
        private ShopView m_view;
        private ShopCategory m_currentCategory;

        public override void Initialize()
        {
            base.Initialize();

            m_model = uiModel as ShopModel;
            Assert.IsNotNull(m_model, "ShopPresenter requires a ShopModel");

            m_view = uiView as ShopView;
            Assert.IsNotNull(m_view, "ShopPresenter requires a ShopView");

            m_view.OnCategorySelected += OnCategorySelectedHandler;
            m_view.OnBackClicked += OnBackClickedHandler;

            RefreshCurrency();
            ShowCategory(defaultCategory, 0f);
        }

        public override void Shutdown()
        {
            base.Shutdown();

            m_view.OnCategorySelected -= OnCategorySelectedHandler;
            m_view.OnBackClicked -= OnBackClickedHandler;
        }

        private void RefreshCurrency()
        {
            m_view.SetCurrency(placeholderCoins, placeholderDiamonds);
        }

        private void OnCategorySelectedHandler(ShopCategory category, float scrollPosition)
        {
            ShowCategory(category, scrollPosition);
        }

        private void ShowCategory(ShopCategory category, float scrollPosition)
        {
            m_currentCategory = category;

            IReadOnlyList<ShopModel.ShopItemDefinition> items = m_model.GetItemsForCategory(category);
            m_view.DisplayItems(items);
            m_view.ScrollTo(scrollPosition);
        }

        private void OnBackClickedHandler()
        {
            Hide();
            panelsController.ShowUI(mainUIName);
        }
    }
}
