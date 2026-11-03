using System.Collections.Generic;
using System.Text;
using BigAmbitions.Items;
using TMPro;
using UI;
using UI.MergeCargo;
using UI.PlayerHUD;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's manage cargo (ManageCargoUi) as the host binds it: what a vehicle or a hand truck carries, with Sell all.
    /// Its own pieces are hidden under the kit; the game's stacks (CargoItemUi, as Going Public sets them up), their
    /// buttons and Sell all are pressed as the player would, so Going Public's own Sell all still runs. The isPanelOpen flag
    /// other mods read stays the game's.
    /// </summary>
    internal sealed class CargoGame : IUiGameScreen, IUiGameWay
    {
        public string Id => BuyKits.CargoId;

        public string Name => "manage cargo";

        public string Title => UiText.T("reallife_ui_buy_manage_cargo");

        public UiIcon Icon => UiIcon.Package;

        public UiGameKind Kind => UiGameKind.Overlay;

        public bool Keys => true;

        public bool Exclusive => true;

        public bool Blocks => true;

        public Component Find()
        {
            GameObject panel = BuyKits.Get<GameObject>(GameContract.CargoPanel, Ui());
            return panel != null ? panel.transform : null;
        }

        public string Check(Component found) => BuyKits.Get<Transform>(GameContract.CargoTemplate, Ui()) == null || BuyKits.Get<Button>(GameContract.CargoSellAll, Ui()) == null
            ? "its stacks or Sell all are not there" : null;

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform panel = found.transform;
            for (int i = 0; i < panel.childCount; i++)
            {
                conceal.Add(panel.GetChild(i));
            }
            conceal.Add(panel.GetComponent<Graphic>());
        }

        public IUiScreen Make(Component found) => new CargoScreen(Ui());

        internal static ManageCargoUi Ui()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null ? uis.playerHUD.manageCargoUI : null;
        }
    }
}
