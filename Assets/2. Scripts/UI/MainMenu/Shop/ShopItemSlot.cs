using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _2._Scripts.UI.MainMenu.Shop
{
    public class ShopItemSlot : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Image currencyIcon;
        [SerializeField] private Image itemIcon;
        [SerializeField] private GameObject saleBadge;
        [SerializeField] private Sprite coinsSprite;
        [SerializeField] private Sprite diamondsSprite;

        public void SetData(ShopModel.ShopItemDefinition item)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (nameText)
            {
                nameText.text = item.displayName;
            }

            if (priceText)
            {
                priceText.text = item.price.ToString();
            }

            if (itemIcon)
            {
                itemIcon.sprite = item.icon;
                itemIcon.enabled = item.icon;
            }

            if (currencyIcon)
            {
                currencyIcon.sprite = item.currency == ShopCurrency.Coins ? coinsSprite : diamondsSprite;
                currencyIcon.enabled = currencyIcon.sprite != null;
            }

            if (saleBadge)
            {
                saleBadge.SetActive(item.showSaleBadge);
            }
        }

        public void Clear()
        {
            if (nameText)
            {
                nameText.text = string.Empty;
            }

            if (priceText)
            {
                priceText.text = string.Empty;
            }

            if (itemIcon)
            {
                itemIcon.enabled = false;
            }

            if (currencyIcon)
            {
                currencyIcon.enabled = false;
            }

            if (saleBadge)
            {
                saleBadge.SetActive(false);
            }

            gameObject.SetActive(false);
        }
    }
}
