using System;
using System.Collections;
using Items.SpecialItems;
using Player.HUD.ItemInfoOverlays;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The item cards' tests (Options, Tests): the small card over the nearest shelf with stock, the large card over the nearest
    /// factory machine, and every card switched back to the game's own and on again. Each puts the thing's own box up through
    /// the game's OverlayManager, says in a notice whether the kit drew it, and logs the same, or says exactly why it could not run.
    /// Nothing is saved: the switch test changes the running values only.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] OverlaySwitches = { UiOverlays.HoverId, UiOverlays.ClickId, UiOverlays.PersonId };
        private bool _testStockUp, _testMachineUp;

        /// <summary>Options, Tests: the small card over the nearest shelf with stock, drawn by the kit; or away again.</summary>
        public void TestOverlayStock()
        {
            try
            {
                OverlayManager manager = InstanceBehavior<OverlayManager>.Instance;
                if (_testStockUp && manager != null)
                {
                    manager.HideOverlays();
                    _testStockUp = false;
                    Notifications.Show(NotificationType.Info, "reallife_ui_test_overlay_gone", null, 6f);
                    return;
                }
                if (!Ready())
                {
                    return;
                }
                ItemController shelf = Nearest(c => (c.simpleOverlayType & SimpleOverlayType.Stock) != 0 && c.ItemInstance != null);
                if (manager == null || shelf == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_overlay_nothing", null, 8f);
                    return;
                }
                manager.ShowSimpleOverlay(shelf);
                _testStockUp = true;
                StartCoroutine(Guard.Routine(OverlaySoon(false)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the large card over the nearest factory machine, with its two choices; or away again.</summary>
        public void TestOverlayMachine()
        {
            try
            {
                OverlayManager manager = InstanceBehavior<OverlayManager>.Instance;
                if (_testMachineUp && manager != null)
                {
                    manager.HideOverlays();
                    _testMachineUp = false;
                    Notifications.Show(NotificationType.Info, "reallife_ui_test_overlay_gone", null, 6f);
                    return;
                }
                if (!Ready())
                {
                    return;
                }
                ItemController machine = Nearest(c => c is FactoryAssemblyMachineController);
                if (manager == null || machine == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_overlay_no_machine", null, 8f);
                    return;
                }
                manager.HideOverlays();
                if (!manager.ShowDetailedOverlay(machine))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_overlay_busy", null, 8f);
                    return;
                }
                _testMachineUp = true;
                StartCoroutine(Guard.Routine(OverlaySoon(true)));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private IEnumerator OverlaySoon(bool click)
        {
            yield return new WaitForSecondsRealtime(WorldSettles);
            OverlayCardsLive cards = WorldHost.Instance != null ? WorldHost.Instance.Overlays : null;
            bool kit = cards != null && (click ? cards.ClickShowing : cards.HoverShowing);
            Notifications.Show(kit ? NotificationType.Info : NotificationType.Warning, kit ? "reallife_ui_test_overlay_shown" : "reallife_ui_test_overlay_game", null, 10f);
            Logger?.Info("UI check (" + (click ? "large" : "small") + " card over an item): the card is "
                         + (kit ? "drawn on the kit with " + cards.Model.RowCount + " rows, " + cards.Model.ChoiceCount + " choices and " + cards.Model.ActionCount
                             + " actions: PASS." : "the game's own (" + WorldKits.State + "): FAIL."));
        }

        /// <summary>Options, Tests: every card over an item switched to the game's own, or back onto the kit.</summary>
        public void TestOverlaySwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(UiOverlays.HoverId);
                for (int i = 0; i < OverlaySwitches.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(OverlaySwitches[i]), on);
                }
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_overlay_switch_kit" : "reallife_ui_test_overlay_switch_game", null, 8f);
                Logger?.Info("UI check (item cards' switches): every card over an item is " + (on ? "on the kit again" : "the game's own") + " (" + WorldKits.State + "): PASS.");
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The nearest item of the building the player is in that the test wants, or null.
        private static ItemController Nearest(Func<ItemController, bool> wanted)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
            if (manager == null || player == null || manager.allItemControllers == null)
            {
                return null;
            }
            ItemController best = null;
            float far = float.MaxValue;
            for (int i = 0; i < manager.allItemControllers.Count; i++)
            {
                ItemController c = manager.allItemControllers[i];
                if (c == null || !c.isActiveAndEnabled || !wanted(c))
                {
                    continue;
                }
                float d = (c.transform.position - player.transform.position).sqrMagnitude;
                if (d < far)
                {
                    far = d;
                    best = c;
                }
            }
            return best;
        }
    }
}
