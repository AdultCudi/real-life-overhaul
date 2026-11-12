using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.Items;
using Buildings;
using Localizor;
using UI;
using UI.CustomUI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The tests of the world's smaller windows (Options, Tests): each opens the game's own window the game's way (the
    /// phone's private driver, the items list filled as BizMan fills a store's, the sign of a shop the player rents, the
    /// preview terminal at the firm the player is in, a preview of a building the player rents, the colour picker), then
    /// says in a notice whether the kit drew it, or exactly why it cannot run, and writes a PASS or FAIL line to Player.log.
    /// Nothing is saved by a test; the colour picker keeps a colour only if Add is pressed, as it does without the kit.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const int TestGoods = 14;

        /// <summary>Options, Tests: the phone's private driver opened beside the phone.</summary>
        public void TestWorldDriver()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.smartphoneUI == null || uis.smartphoneUI.privateDriverUI == null || !uis.smartphoneUI.gameObject.activeInHierarchy)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_driver_phone", null, 8f);
                    return;
                }
                if (!uis.smartphoneUI.privateDriverUI.gameObject.activeSelf)
                {
                    uis.smartphoneUI.privateDriverUI.OnClickPrivateDriverButton();   // the phone's own button's way
                }
                StartCoroutine(Guard.Routine(ToolSoon(UiWorld.DriverId, "private driver", "reallife_ui_test_world_driver_shown")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the items list filled with fourteen of the game's goods as BizMan fills a store's, and opened.</summary>
        public void TestWorldItems()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                BizManPresentation bizman = uis != null ? uis.GetComponentInChildren<BizManPresentation>(true) : null;
                ItemsListEntry row = bizman != null && GameContract.ItemsStoreRow.Field != null
                    ? GameContract.ItemsStoreRow.Field.GetValue(bizman) as ItemsListEntry : null;
                List<string> goods = ItemHelper.GetRetailProductItemNames();
                if (uis == null || uis.itemsList == null || row == null || goods == null || goods.Count == 0)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_items_none", null, 8f);
                    return;
                }
                uis.itemsList.Clear();
                for (int i = 0; i < goods.Count && i < TestGoods; i++)
                {
                    ItemsListEntry entry = UnityEngine.Object.Instantiate(row);
                    Item item = ItemsGetter.GetByName(goods[i], true);
                    entry.Init(goods[i].Localize(), ItemHelper.GetIconWithFallback(goods[i]), item != null ? item.DefaultMarketPrice : 0f);
                    uis.itemsList.AddEntry(entry);
                    entry.gameObject.SetActive(true);
                }
                uis.itemsList.SetTitle(UiText.T("reallife_ui_test_world_items_title"));
                uis.itemsList.Toggle(true);
                StartCoroutine(Guard.Routine(ToolSoon(UiWorld.ItemsId, "items list", "reallife_ui_test_world_items_shown")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the sign's look of the nearest shop the player rents, as its building's panel opens it.</summary>
        public void TestWorldSign()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                CityBuildingController shop = NearestRented(false);
                if (uis == null || uis.draggableWindows == null || uis.draggableWindows.signAppearance == null || shop == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_sign_none", null, 8f);
                    return;
                }
                uis.draggableWindows.signAppearance.SetBuilding(shop.buildingRegistration);
                StartCoroutine(Guard.Routine(ToolSoon(UiWorld.SignId, "sign appearance", "reallife_ui_test_world_sign_shown")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the preview terminal of the interior firm the player stands in.</summary>
        public void TestWorldTerminal()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                bool firm = BuildingManager.IsInsideBuilding && manager != null && manager.building != null && manager.building.SpecialService != null
                            && manager.building.SpecialService.settings is InteriorInstallationFirmSettings;
                if (!firm)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_terminal_none", null, 8f);
                    return;
                }
                PreviewTerminalUI.Show();
                StartCoroutine(Guard.Routine(ToolSoon(UiWorld.TerminalId, "preview terminal", "reallife_ui_test_world_terminal_shown")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: a preview of the nearest building the player rents, as BizMan's Preview starts one.</summary>
        public void TestWorldPreview()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                CityBuildingController shop = NearestRented(true);
                Building building = shop != null && shop.buildingRegistration != null ? GameRegistrations.BuildingOf(shop.buildingRegistration) : null;
                if (uis == null || uis.buildingPreview == null || building == null || BuildingPreview.isPreviewing)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_preview_none", null, 8f);
                    return;
                }
                uis.buildingPreview.PreviewBuilding(building);
                StartCoroutine(Guard.Routine(ToolSoon(UiWorld.PreviewId, "building preview", "reallife_ui_test_world_preview_shown")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the game's custom colour picker opened on white; Cancel leaves it keeping nothing.</summary>
        public void TestWorldColour()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.customColorPicker == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_wait", null, 8f);
                    return;
                }
                uis.customColorPicker.onColorPick = null;
                uis.customColorPicker.Open(_ => { }, Color.white);
                StartCoroutine(Guard.Routine(ToolSoon(UiWorld.ColourId, "colour picker", "reallife_ui_test_world_colour_shown")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private IEnumerator ToolSoon(string id, string name, string shownKey)
        {
            yield return new WaitForSecondsRealtime(WorldSettles);
            UiGameHost host = WorldKits.Tool(id);
            bool kit = host != null && host.Showing;
            Notifications.Show(kit ? NotificationType.Info : NotificationType.Warning, kit ? shownKey : "reallife_ui_test_world_door_game", null, 10f);
            Logger?.Info("UI check (" + name + "): the window is " + (kit ? "on the kit: PASS." : "the game's own (" + (host != null ? host.State : "no host") + "): FAIL."));
        }

        // The nearest building the player rents with a business in it (and a sign's settings); for a preview, never the one
        // the player stands in, which the game does not preview.
        private static CityBuildingController NearestRented(bool preview)
        {
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            if (all == null || InstanceBehavior<GameManager>.Instance == null || InstanceBehavior<GameManager>.Instance.playerController == null)
            {
                return null;
            }
            Vector3 at = Helpers.PlayerHelper.GetPosition();
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            CityBuildingController best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController cbc = all[i];
                BuildingRegistration reg = cbc != null ? cbc.buildingRegistration : null;
                if (reg == null || !reg.RentedByPlayer || reg.signAppearanceSettings == null)
                {
                    continue;
                }
                if (preview && BuildingManager.IsInsideBuilding && manager != null && manager.building != null && ReferenceEquals(manager.building, GameRegistrations.BuildingOf(reg)))
                {
                    continue;
                }
                float sq = (cbc.transform.position - at).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = cbc;
                }
            }
            return best;
        }
    }
}
