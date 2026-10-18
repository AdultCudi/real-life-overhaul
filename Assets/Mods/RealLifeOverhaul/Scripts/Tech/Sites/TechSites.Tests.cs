using System;
using System.Collections.Generic;
using Streets;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The test buttons of the tech sector's buildings.</summary>
    public sealed partial class TechSites
    {
        /// <summary>The test button: a server room in the warehouse of the player's they stand in, as a tier III data centre with 60 racks would have.</summary>
        public void TestRoom()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration reg = BuildingManager.IsInsideBuilding && manager != null ? manager.buildingRegistration : null;
            if (reg == null || !GoingPublic.Mine(reg) || TypeOf(reg) != Warehouse)
            {
                Notifications.Show(NotificationType.Warning, "reallife_sites_test_room_where", null, 8f, trackOnSaveGame: false);
                return;
            }
            try
            {
                EndViews();
                _inside = TalentGame.Place(reg);
                _preview = new DataCentre { Place = _inside, Name = TalentGame.NameOf(reg), Tier = 2, Racks = 60, GpuRacks = 12, Technicians = 2, District = InfraGame.District(reg) };
                bool built = _room.Begin(manager, _preview, Save != null ? Save.Day : 0);
                Logger?.Info("Tech sites test: server room: " + (built ? _room.Said : "the floor could not be measured") + ".");
                Notifications.Show(built ? NotificationType.Success : NotificationType.Warning,
                    built ? "reallife_sites_test_room_done" : "reallife_sites_test_room_failed", null, 8f, trackOnSaveGame: false);
            }
            catch (Exception e)
            {
                Fault(ref _roomBroken, "the server room", e);
            }
        }

        /// <summary>The test button: the player's room fitted out round their furniture as a tech company's floor would be, until they leave.</summary>
        public void TestFloor()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration reg = BuildingManager.IsInsideBuilding && manager != null ? manager.buildingRegistration : null;
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (reg == null || player == null || !GoingPublic.Mine(reg) || FitOut.FitOutDirector.Instance == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_sites_test_floor_where", null, 8f, trackOnSaveGame: false);
                return;
            }
            try
            {
                EndViews();
                _inside = TalentGame.Place(reg);
                RoomGrid grid = null;
                float floor = 0f;
                if (RoomMeasure.Door(manager, player.transform.position, player.transform.forward, out Vector3 door, out Vector3 inward))
                {
                    grid = RoomMeasure.Measure(manager, door, true, out floor);
                }
                FitOut.FitView.Request r = _hqRequest;
                r.Reset();
                r.Name = "the test floor in " + TalentGame.NameOf(reg);
                r.Kind = FitOut.FitKind.TechOffice;
                r.Seed = TechRandom.Of(_inside);
                r.Building = manager;
                r.Grid = grid;
                r.Floor = floor;
                r.Door = door;
                r.Inward = inward;
                r.Look = false;
                _testFloor = grid != null && _hqFloor.Begin(r, FitOut.FitOutDirector.Instance);
                Notifications.Show(_testFloor ? NotificationType.Success : NotificationType.Warning,
                    _testFloor ? "reallife_sites_test_floor_done" : "reallife_sites_test_floor_failed", null, 8f, trackOnSaveGame: false);
            }
            catch (Exception e)
            {
                Fault(ref _lobbyBroken, "the lobby", e);
            }
        }

        /// <summary>The test button: every headquarters in the log, and the nearest in a notification.</summary>
        public void TestHeadquarters()
        {
            if (!Settings.TechSiteHq || _hq.All.Count == 0)
            {
                Notifications.Show(NotificationType.Warning, "reallife_sites_test_hq_none", null, 8f, trackOnSaveGame: false);
                return;
            }
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            Headquarters.Hq nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < _hq.All.Count; i++)
            {
                Headquarters.Hq hq = _hq.All[i];
                float sqr = player != null ? (hq.Front - player.transform.position).sqrMagnitude : i;
                Logger?.Info("Tech sites test: " + hq.Name + " at " + hq.Street + " " + hq.Number + ", floor " + hq.Floor + " of " + hq.Tower.Name
                             + (Headquarters.Open(hq) ? " (its lobby is open)" : " (let: it moves at the next look)") + ".");
                if (sqr < best)
                {
                    best = sqr;
                    nearest = hq;
                }
            }
            var data = new Dictionary<string, string>(3)
            {
                { "count", _hq.All.Count.ToString() }, { "company", nearest.Name }, { "address", nearest.Building != null ? nearest.Building.building.Address.ToFormattedString() : nearest.Street }
            };
            Notifications.Show(NotificationType.Info, "reallife_sites_test_hq_done", data, 12f, trackOnSaveGame: false);
        }
    }
}
