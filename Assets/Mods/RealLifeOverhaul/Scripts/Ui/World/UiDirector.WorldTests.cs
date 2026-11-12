using System;
using System.Collections;
using System.Collections.Generic;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The world's tests (Options, Tests): a door's box with four actions beside the player, a desk's list of eight with a
    /// note and a greyed entry, a bus brought to the nearest stop, the nearest building's panel, the job offer of the
    /// building the player is in, a locked custody list, and every world piece switched back to the game's own and on
    /// again. Each says in a notice what it did and whether the kit drew it, or exactly why it cannot run. Nothing is
    /// saved: the switch test changes the running values only.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const float WorldSettles = 0.5f;   // seconds of real time for the game to build its box and the kit to read it
        private const int TestChoices = 8;
        private static readonly string[] WorldSwitches = { UiWorld.PromptId, UiWorld.BuildingId, UiWorld.PickerId, UiWorld.OfferId };

        private WorldPanel _testDoor;
        private TravelList _testDesk, _testCell;
        private Action _testDoorDone;
        private Action<int> _testDeskPicked, _testCellPicked;

        /// <summary>Options, Tests: a door's box beside the player with four actions on the game's keys, drawn by the kit; or away again.</summary>
        public void TestWorldDoor()
        {
            try
            {
                if (_testDoor != null)
                {
                    EndTestDoor("reallife_ui_test_world_door_gone");
                    return;
                }
                if (!Ready())
                {
                    return;
                }
                PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
                if (player == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_wait", null, 8f);
                    return;
                }
                _testDoorDone = _testDoorDone ?? (() => EndTestDoor("reallife_ui_test_world_door_pressed"));
                _testDoor = new WorldPanel { Follow = player.transform, FollowLift = 1.8f };
                _testDoor.Lines(UiText.T("reallife_ui_test_world_door_title"), UiText.T("reallife_ui_test_world_door_sub"), UiText.T("common_open"), WorldPanel.Green());
                _testDoor.Button(0, "reallife_ui_test_world_door_first", _testDoorDone, true, true);
                _testDoor.Button(1, "reallife_ui_test_world_door_second", _testDoorDone, true, false);
                _testDoor.Button(2, "reallife_ui_test_world_door_third", _testDoorDone, false, false);
                _testDoor.Button(3, "common_close", _testDoorDone, true, false);
                if (!_testDoor.Show())
                {
                    _testDoor = null;
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_door_busy", null, 8f);
                    return;
                }
                StartCoroutine(Guard.Routine(DoorSoon()));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private IEnumerator DoorSoon()
        {
            yield return new WaitForSecondsRealtime(WorldSettles);
            bool kit = WorldHost.Instance != null && WorldHost.Instance.Prompt.Showing;
            Notifications.Show(kit ? NotificationType.Info : NotificationType.Warning, kit ? "reallife_ui_test_world_door_shown" : "reallife_ui_test_world_door_game", null, 10f);
            Logger?.Info("UI check (world prompt): the test door's box is "
                         + (kit ? "drawn on the kit with " + WorldHost.Instance.Prompt.Model.ActionCount + " actions: PASS." : "the game's own (" + WorldKits.State + "): FAIL."));
        }

        private void EndTestDoor(string key)
        {
            _testDoor?.Hide();
            _testDoor = null;
            Notifications.Show(NotificationType.Info, key, null, 6f);
        }

        /// <summary>Options, Tests: a desk's list of eight choices, a note and a greyed choice, on the kit's picker.</summary>
        public void TestWorldDesk()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (_testDesk == null || !_testDesk.Alive)
                {
                    _testDesk = TravelList.Make("RealLife UiTest Desk");
                }
                if (_testDesk == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_wait", null, 8f);
                    return;
                }
                _testDeskPicked = _testDeskPicked ?? (id =>
                {
                    _testDesk?.Close();
                    Notifications.Show(NotificationType.Success, "reallife_ui_test_world_desk_picked", new Dictionary<string, string>(1) { { "n", (id + 1).ToString() } }, 6f);
                });
                _testDesk.Begin(UiText.T("reallife_ui_test_world_desk_title"), UiText.T("reallife_ui_test_world_desk_info"));
                _testDesk.AddNote(UiText.T("reallife_ui_test_world_note"), UiText.T("reallife_ui_test_world_note_body"), 0f);
                for (int i = 0; i < TestChoices; i++)
                {
                    _testDesk.Add(i, UiText.T("reallife_ui_test_world_choice").Replace("{n}", (i + 1).ToString()), UiText.T("reallife_ui_test_world_choice_line"), "reallife_ui_prompt_select", i != 3);
                }
                _testDesk.Open(_testDeskPicked, null);
                StartCoroutine(Guard.Routine(ListSoon(_testDesk, "desk")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: a locked custody list: no close, Escape the game's menu, one way out that settles it.</summary>
        public void TestWorldCustody()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (_testCell == null || !_testCell.Alive)
                {
                    _testCell = TravelList.Make("RealLife UiTest Custody");
                }
                if (_testCell == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_wait", null, 8f);
                    return;
                }
                _testCellPicked = _testCellPicked ?? (id =>
                {
                    _testCell?.Close();
                    Notifications.Show(NotificationType.Success, "reallife_ui_test_world_custody_done", null, 6f);
                });
                _testCell.Begin(UiText.T("reallife_ui_test_world_custody_title"), UiText.T("reallife_ui_test_world_custody_info"));
                _testCell.Add(0, UiText.T("reallife_ui_test_world_custody_leave"), null, "reallife_ui_prompt_select", true);
                _testCell.Open(_testCellPicked, null);
                _testCell.Locked = true;
                StartCoroutine(Guard.Routine(ListSoon(_testCell, "custody")));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private IEnumerator ListSoon(TravelList list, string which)
        {
            yield return new WaitForSecondsRealtime(WorldSettles);
            bool kit = ReferenceEquals(WorldPicker.Showing, list);
            Notifications.Show(kit ? NotificationType.Info : NotificationType.Warning, kit ? "reallife_ui_test_world_desk_shown" : "reallife_ui_test_world_desk_game", null, 8f);
            Logger?.Info("UI check (world picker, " + which + "): the list is " + (kit ? "on the kit's picker with " + WorldPicker.Screen.Model.Entries.Length + " entries"
                + (list.Locked ? ", locked" : string.Empty) + ": PASS." : "in the game's own window (" + WorldPicker.State + "): FAIL."));
        }

        /// <summary>Options, Tests: a bus to the nearest stop (the transit test), whose box at the stop the kit draws.</summary>
        public void TestWorldBus()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                TransitDirector transit = TransitDirector.Instance;
                if (transit == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_hud_wait", null, 8f);
                    return;
                }
                transit.TestBus();
                Notifications.Show(NotificationType.Info, "reallife_ui_test_world_bus_sent", null, 10f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the nearest building picked, its panel docked at the top left on the kit.</summary>
        public void TestWorldBuilding()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                CityBuildingController building = uis != null && uis.buildingResume != null ? CivicCity.NearestBuilding(PlayerHelper.GetCityPosition(), 0f, 80f) : null;
                if (building == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_building_none", null, 8f);
                    return;
                }
                uis.buildingResume.ShowOptions(building);
                StartCoroutine(Guard.Routine(BuildingSoon()));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private IEnumerator BuildingSoon()
        {
            yield return new WaitForSecondsRealtime(WorldSettles);
            bool kit = WorldHost.Instance != null && WorldHost.Instance.Building.Docked;
            Notifications.Show(kit ? NotificationType.Info : NotificationType.Warning, kit ? "reallife_ui_test_world_building_shown" : "reallife_ui_test_world_door_game", null, 10f);
            Logger?.Info("UI check (building panel): the picked building's panel is " + (kit ? "docked on the kit (" + WorldHost.Instance.Building.State + "): PASS."
                : "the game's own (" + WorldKits.State + "): FAIL."));
        }

        /// <summary>Options, Tests: the job offer of the building the player is in, on the kit's offer card.</summary>
        public void TestWorldOffer()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (manager == null || manager.building == null || uis == null || uis.playerHUD == null || !BuildingManager.IsInsideBuilding)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_offer_inside", null, 8f);
                    return;
                }
                global::Job job = JobHelper.GetJob(manager.building.Address);
                if (job == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_world_offer_none", null, 8f);
                    return;
                }
                uis.playerHUD.ShowJob(job);
                StartCoroutine(Guard.Routine(OfferSoon()));
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private IEnumerator OfferSoon()
        {
            yield return new WaitForSecondsRealtime(WorldSettles);
            bool kit = WorldKits.OfferShowing;
            Notifications.Show(kit ? NotificationType.Info : NotificationType.Warning, kit ? "reallife_ui_test_world_offer_shown" : "reallife_ui_test_world_door_game", null, 10f);
            Logger?.Info("UI check (job offer): the offer is " + (kit ? "on the kit's card: PASS." : "the game's own (" + WorldKits.State + "): FAIL."));
        }

        /// <summary>Options, Tests: every world piece switched to the game's own, or back onto the kit.</summary>
        public void TestWorldSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(UiWorld.PromptId);
                for (int i = 0; i < WorldSwitches.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(WorldSwitches[i]), on);
                }
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_world_switch_kit" : "reallife_ui_test_world_switch_game", null, 8f);
                Logger?.Info("UI check (world switches): every world piece is " + (on ? "on the kit again" : "the game's own") + " (" + WorldKits.State + "): PASS.");
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
