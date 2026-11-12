using System.Collections.Generic;
using System.Text;
using TMPro;
using UI;
using UI.PurchaseVehicle;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's vehicle purchase (PurchaseVehicleUI) as the host binds it: the dealer's and the harbour's panel beside the
    /// showcase. Its own pieces are hidden under the kit; the showcase camera, the car's recolouring and the purchase stay
    /// the game's, and the game's colour cells, delivery switch, warehouse list and buttons are pressed as the player would.
    /// EmpireMotors sets the asset and reads IsPanelOpen as before: the panel stays the game's object, open as it was.
    /// </summary>
    internal sealed class VehicleBuyGame : IUiGameScreen, IUiGameWay
    {
        public string Id => BuyKits.VehicleId;

        public string Name => "buying a vehicle";

        public string Title => UiText.T("common_vehicle");

        public UiIcon Icon => UiIcon.Car;

        public UiGameKind Kind => UiGameKind.Overlay;

        public bool Keys => true;

        public bool Exclusive => true;   // Interact buys and Cancel leaves on the game's panel too: one press does one thing

        public bool Blocks => false;     // the showcase turns by the pointer over the world: the kit takes only its own clicks

        public Component Find()
        {
            PurchaseVehicleUI ui = Ui();
            GameObject panel = BuyKits.Get<GameObject>(GameContract.CarPanel, ui);
            return panel != null ? panel.transform : null;
        }

        public string Check(Component found)
        {
            PurchaseVehicleUI ui = Ui();
            return BuyKits.Get<Button>(GameContract.CarBuy, ui) == null || BuyKits.Get<Button>(GameContract.CarCancel, ui) == null ? "its buttons are not there"
                : BuyKits.Get<Transform>(GameContract.CarColourTemplate, ui) == null || BuyKits.Get<Transform>(GameContract.CarSpecTemplate, ui) == null ? "its colours or specs are not there"
                : null;
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

        public IUiScreen Make(Component found) => new VehicleBuyScreen(Ui());

        internal static PurchaseVehicleUI Ui()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null ? uis.playerHUD.purchaseVehicleUI : null;
        }
    }
}
