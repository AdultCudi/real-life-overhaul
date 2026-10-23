using System;
using System.Collections.Generic;
using Helpers;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// What the test entries and the director's checks ask of the game: the player, the door, the game's own way into a
    /// building and out of it, the hourly event. Each is a read or one call of the game's; nothing runs by itself.
    /// </summary>
    internal static partial class MadeGame
    {
        private const float NoticeSeconds = 10f;   // s a test's notice stays

        /// <summary>A test's notice in the game's own notifications, its one {name} filled in.</summary>
        public static void Tell(string key, string name, string value)
        {
            Dictionary<string, string> data = name != null ? new Dictionary<string, string>(1) { { name, value ?? string.Empty } } : null;   // the game keeps the notice's data
            Notifications.Show(NotificationType.Info, key, data, NoticeSeconds);
        }

        /// <summary>True while a save is not ready for the mod: none loaded, or loading.</summary>
        public static bool Loading => LoadScene.isLoading || !ModState.Loaded || SaveGameManager.Current == null;

        /// <summary>The local player's position, or zero outside a city.</summary>
        public static Vector3 PlayerAt()
        {
            PlayerController player = PlayerHelper.PlayerController;
            return player != null ? player.transform.position : Vector3.zero;
        }

        /// <summary>The way the player faces, in degrees round the vertical.</summary>
        public static float PlayerFacing()
        {
            PlayerController player = PlayerHelper.PlayerController;
            return player != null ? player.transform.eulerAngles.y : 0f;
        }

        /// <summary>The camera the game draws with now, and the way it looks (degrees); false with none.</summary>
        public static bool Camera(out Vector3 at, out float facing)
        {
            Camera main = GameManager.GetMainCamera();
            at = main != null ? main.transform.position : Vector3.zero;
            Vector3 flat = main != null ? Vector3.ProjectOnPlane(main.transform.forward, Vector3.up) : Vector3.zero;
            facing = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat).eulerAngles.y : 0f;
            return main != null;
        }

        public static bool Indoors => BuildingManager.IsInsideBuilding;

        public static bool InVehicle => PlayerHelper.IsUsingVehicle;

        /// <summary>True while the game is walking the player in or out of a building, behind its black screen.</summary>
        public static bool Moving
        {
            get
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                return manager != null && (manager.enteringBuilding || manager.exitingBuilding);
            }
        }

        /// <summary>True while the player is inside that made building, the game done taking them in.</summary>
        public static bool InsideOf(MadeBuilding made)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return BuildingManager.IsInsideBuilding && manager != null && !manager.enteringBuilding && manager.building == made.Building;
        }

        /// <summary>The structure loaded for the building the player is in, and the exit zones the game found in it.</summary>
        public static bool Structure(out string loaded, out int exits)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            loaded = manager != null && manager.currentBuildingVersion != null ? manager.currentBuildingVersion.name : null;
            exits = manager != null && manager.exitZones != null ? manager.exitZones.Count : 0;
            return loaded != null;
        }

        /// <summary>
        /// Into a made building by the game's own way, as its door's click does when it lets the player in
        /// (CityBuildingController.Interact): the game fades, loads its structure for the size and version and puts the
        /// player at its exit zone. False if the game would not start it.
        /// </summary>
        public static bool Enter(MadeBuilding made)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || made.Building == null || made.Controller == null || made.Controller.entranceDoors.Length == 0)
            {
                return false;
            }
            return manager.EnterBuilding(made.Building, useSaveGamePlayerPosition: false, inverseVehicleRotation: false, 0, made.Controller.entranceDoors[0].doorId);
        }

        /// <summary>Out of the building by the game's own way, as its exit zone does: the player is put at the door outside.</summary>
        public static bool Exit()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || !BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            manager.ExitFromBuilding(0);
            return true;
        }

        /// <summary>The game's own hourly light of a building (its shop glass by the hour), run now; false with the fault's text if it threw.</summary>
        public static bool HourlyLight(CityBuildingController c, out string fault)
        {
            fault = null;
            try
            {
                c.UpdateIndoorOutdoorLight();
                return true;
            }
            catch (Exception e)
            {
                fault = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        /// <summary>Keeps a handler on the game's hourly event, which the game empties at every load (EventHold).</summary>
        public static void KeepHour(Action handler, ref Action seen)
        {
            EventHold.Keep(ref GlobalEvents.onNewHour, handler, ref seen);
        }

        public static void DropHour(Action handler)
        {
            GlobalEvents.onNewHour -= handler;
        }

        /// <summary>
        /// True when <paramref name="handler"/> is called after every hourly handler of the made buildings' controllers (the
        /// game raises the event with a plain call, so one that threw would stop it before this one): its place against
        /// theirs in the event's list. One copy of the list, at a test's press.
        /// </summary>
        public static bool HourAfterMade(Action handler, out int made)
        {
            made = 0;
            Action all = GlobalEvents.onNewHour;
            if (all == null || handler == null)
            {
                return false;
            }
            Delegate[] list = all.GetInvocationList();
            int mine = -1, last = -1;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(handler))
                {
                    mine = i;
                }
                else if (list[i].Target is CityBuildingController c && MadeBuildings.Of(c) != null)
                {
                    made++;
                    last = i;
                }
            }
            return mine >= 0 && made > 0 && mine > last;
        }
    }
}
