using System.Collections.Generic;
using System.Text;
using BigAmbitions.Items;
using Extensions;
using TMPro;
using UI;
using UI.Purchase;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's checkout (PurchaseUI) as the host binds it: a store's register, the tax office's payment and the casino
    /// boat's ticket, all one panel the game switches on and off. Its own pieces are hidden under the kit; the panel stays
    /// live and the game's rows, stepper, field and buttons are pressed as the player would press them.
    /// </summary>
    internal sealed class CheckoutGame : IUiGameScreen, IUiGameWay
    {
        public string Id => BuyKits.CheckoutId;

        public string Name => "the checkout";

        public string Title => UiText.T("common_purchase");

        public UiIcon Icon => UiIcon.ShoppingCart;

        public UiGameKind Kind => UiGameKind.Overlay;

        public bool Keys => true;

        public bool Exclusive => true;   // the game orders on Interact and cancels on Cancel too: one press does one thing

        public bool Blocks => true;      // the game's panel holds the store's clicks off while it shows

        public Component Find()
        {
            PurchaseUI ui = Ui();
            return ui != null && ui.panel != null ? ui.panel.transform : null;
        }

        public string Check(Component found)
        {
            PurchaseUI ui = Ui();
            return ui == null || ui.itemsContainer == null || ui.orderButton == null || ui.cancelButton == null ? "its rows or buttons are not there"
                : GameContract.BuyTitle.Field == null ? "its title is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform panel = found.transform;
            for (int i = 0; i < panel.childCount; i++)
            {
                conceal.Add(panel.GetChild(i));
            }
            conceal.Add(panel.GetComponent<Graphic>());
        }

        public IUiScreen Make(Component found) => new CheckoutScreen(Ui());

        internal static PurchaseUI Ui()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null ? uis.playerHUD.purchaseUI : null;
        }
    }
}
