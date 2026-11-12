using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.Items;
using UI;
using UI.ItemPanel;
using UI.Notification;
using UI.Purchase;
using UI.PurchaseVehicle;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of the buying screens (Options, Tests): a checkout with five goods that buys nothing,
    /// the vehicle purchase, the item panel with a vehicle, manage cargo on the vehicle driven, the mod's clothes shop, and
    /// the five screens switched to the game's own and back. Each says in a notice exactly why it cannot run when it
    /// cannot. Nothing is saved: the switch test changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] BuyScreens = { BuyKits.CheckoutId, BuyKits.VehicleId, BuyKits.ItemPanelId, BuyKits.CargoId, BuyKits.WardrobeId };

        /// <summary>Options, Tests: a checkout with five goods; placing the order buys nothing and closes it.</summary>
        public void TestCheckout()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                PurchaseUI ui = CheckoutGame.Ui();
                if (ui == null || PurchaseUI.IsPanelOpen || PurchaseVehicleUI.IsPanelOpen)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_buy_busy", null, 8f);
                    return;
                }
                var goods = new List<CargoInstance>
                {
                    new CargoInstance("ba:itemname_cupcake", 50, 0.9f), new CargoInstance("ba:itemname_croissant", 50, 1.1f),
                    new CargoInstance("ba:itemname_icecream", 40, 1.4f), new CargoInstance("ba:itemname_popcorn", 60, 0.6f),
                    new CargoInstance("ba:itemname_cheeseplatter", 20, 3.2f)
                };
                ui.Open(PurchaseUI.Type.Purchase, Guard.Listener<bool>(_ => { }), Guard.Listener<List<CargoInstance>>(_ => StartCoroutine(Guard.Routine(CloseSoon(ui)))),
                    goods, PurchaseUI.Mode.SingleToggle);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_buy_checkout_note", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the vehicle purchase, on the kit while its switch is on, or how to open it.</summary>
        public void TestVehicleBuy()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                Say(PurchaseVehicleUI.IsPanelOpen, BuyKits.VehicleShowing, "reallife_ui_test_buy_need_dealer");
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the item panel with the vehicle driven.</summary>
        public void TestItemPanel()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                GameManager gm = InstanceBehavior<GameManager>.Instance;
                Say(gm != null && gm.selectedVehicle != null && ItemPanelUI.IsVisible, ItemPanelKit.Showing, "reallife_ui_test_buy_need_vehicle");
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: manage cargo on the vehicle driven, as its cargo button opens it.</summary>
        public void TestCargo()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                GameManager gm = InstanceBehavior<GameManager>.Instance;
                VehicleController car = gm != null ? gm.selectedVehicle : null;
                var ui = CargoGame.Ui();
                if (car == null || car.vehicleInstance == null || car.vehicleType == null || car.vehicleType.maxCargoCapacity <= 0 || ui == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_buy_need_cargo", null, 8f);
                    return;
                }
                if (ui.isPanelOpen || PurchaseUI.IsPanelOpen)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_buy_busy", null, 8f);
                    return;
                }
                ui.Show(car.vehicleInstance);
                StartCoroutine(Guard.Routine(SaySoon(() => BuyKits.CargoShowing)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the mod's clothes shop, as its rack opens it.</summary>
        public void TestWardrobe()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Slow();
                if (!Leisure.ClothesScreen.OpenShop(out string refusal))
                {
                    Notifications.Show(NotificationType.Warning, refusal ?? "reallife_ui_test_buy_busy", null, 8f);
                    return;
                }
                StartCoroutine(Guard.Routine(SaySoon(() => BuyKits.WardrobeShowing)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the five buying screens switched to the game's own for this run, or back onto the kit.</summary>
        public void TestBuySwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(BuyKits.CheckoutId);
                for (int i = 0; i < BuyScreens.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(BuyScreens[i]), on);
                }
                Slow();
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_buy_kit" : "reallife_ui_test_buy_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The checkout's order is in: the test buys nothing, so the register closes a moment later.
        private IEnumerator CloseSoon(PurchaseUI ui)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (ui != null && PurchaseUI.IsPanelOpen)
            {
                ui.Close(false);
            }
        }

        // A screen the test opened shows a frame later: then says whether the kit draws it.
        private IEnumerator SaySoon(Func<bool> kit)
        {
            yield return null;
            yield return null;
            Say(true, kit(), null);
        }

        private static void Say(bool open, bool kit, string how)
        {
            if (!open)
            {
                Notifications.Show(NotificationType.Info, how, null, 8f);
                return;
            }
            Notifications.Show(NotificationType.Info, kit ? "reallife_ui_test_buy_on_kit" : "reallife_ui_test_buy_on_game", null, 8f);
        }
    }
}
