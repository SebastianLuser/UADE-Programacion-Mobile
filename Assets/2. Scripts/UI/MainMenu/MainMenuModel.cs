using System;

namespace _2._Scripts.UI.MainMenu
{
    public class MainMenuModel : UIModel
    {
        public event Action<int> OnChangedCoins;
        public event Action<int> OnChangedDiamon;
    }
}