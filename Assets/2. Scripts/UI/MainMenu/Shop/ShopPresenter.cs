using System.Collections.Generic;
using Services;
using Services.MicroServices.UserDataService.Wallet;
using UnityEngine;
using UnityEngine.Assertions;

namespace _2._Scripts.UI.MainMenu.Shop
{
    public class ShopPresenter : UIPresenter
    {
        [SerializeField] private string mainUIName = "Main";
        [SerializeField] private ShopCategory defaultCategory = ShopCategory.Weapons;
        [Header("Upgrades")]
        [SerializeField] private MainCharacterDataSO mainCharacterData;
        [SerializeField] private float maxHealthCap = 250f;
        [SerializeField] private float moveSpeedCap = 10f;

        private ShopModel m_model;
        private ShopView m_view;
        private ShopCategory m_currentCategory;
        private IWalletService m_walletService;

        public override void Initialize()
        {
            base.Initialize();

            m_model = uiModel as ShopModel;
            Assert.IsNotNull(m_model, "ShopPresenter requires a ShopModel");

            m_view = uiView as ShopView;
            Assert.IsNotNull(m_view, "ShopPresenter requires a ShopView");

            m_view.OnCategorySelected += OnCategorySelectedHandler;
            m_view.OnBackClicked += OnBackClickedHandler;
            m_view.OnBuyRequested += OnBuyRequestedHandler;

            m_walletService = ServiceLocator.Get<IWalletService>();
            m_walletService.OnWalletChanged += OnWalletChangedHandler;
            OnWalletChangedHandler(m_walletService.Coins, m_walletService.Diamonds);

            ShowCategory(defaultCategory, 0f);

            Hide();
        }

        public override void Shutdown()
        {
            base.Shutdown();

            m_view.OnCategorySelected -= OnCategorySelectedHandler;
            m_view.OnBackClicked -= OnBackClickedHandler;
            m_view.OnBuyRequested -= OnBuyRequestedHandler;

            if (m_walletService != null)
            {
                m_walletService.OnWalletChanged -= OnWalletChangedHandler;
            }
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

        private void OnBuyRequestedHandler(ShopModel.ShopItemDefinition item)
        {
            if (!TrySpendCurrency(item))
            {
                MyLogger.LogWarning($"Not enough {(item.currency == ShopCurrency.Coins ? "coins" : "diamonds")} to buy {item.displayName}");
                return;
            }

            ApplyUpgrade(item);
        }

        private bool TrySpendCurrency(ShopModel.ShopItemDefinition item)
        {
            return item.currency switch
            {
                ShopCurrency.Coins => m_walletService?.SpendCoins(item.price) ?? false,
                ShopCurrency.Diamonds => m_walletService?.SpendDiamonds(item.price) ?? false,
                _ => false
            };
        }

        private void ApplyUpgrade(ShopModel.ShopItemDefinition item)
        {
            if (!mainCharacterData)
            {
                MyLogger.LogWarning("MainCharacterDataSO reference missing. Upgrade cannot be applied.");
                return;
            }

            switch (item.upgradeType)
            {
                case ShopUpgradeType.MaxHealth:
                    mainCharacterData.maxHealth = Mathf.Clamp(mainCharacterData.maxHealth + item.upgradeValue, 0f, maxHealthCap);
                    MyLogger.LogInfo($"Max health upgraded to {mainCharacterData.maxHealth}");
                    break;
                case ShopUpgradeType.MoveSpeed:
                    mainCharacterData.moveSpeed = Mathf.Clamp(mainCharacterData.moveSpeed + item.upgradeValue, 0f, moveSpeedCap);
                    MyLogger.LogInfo($"Move speed upgraded to {mainCharacterData.moveSpeed}");
                    break;
                default:
                    MyLogger.LogInfo($"Purchased {item.displayName} (no stat change configured).");
                    break;
            }
        }

        private void OnWalletChangedHandler(int coins, int diamonds)
        {
            m_view.SetCurrency(coins, diamonds);
        }
    }
}
