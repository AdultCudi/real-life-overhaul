using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BigAmbitions.DayNightCycle;
using CameraControllers;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UI;
using UI.Load;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.TestRun
{
    /// <summary>
    /// The only file of the test run that reads or changes the game: whether a save is loaded and the game runs, the
    /// player's pause, the save that undoes the run, the player taken out of a building or a vehicle and moved the game's
    /// own way (the taxi's: hidden, the black screen, the walking mesh's warp, shown again), the places the run gives its
    /// checks, and the game's time machine to the next hour. Each is a read or a call of the game's, made by the run's
    /// steps; nothing here runs by itself. COST: none a frame; a place is worked out once when a check asks for it.
    /// </summary>
    internal static class RunGame
    {
        private const NavigationBlocker Blocker = (NavigationBlocker)7331;   // a value of the run's own, so nobody else's is ever lifted
        private const float FadeSeconds = 1f;      // s of the game's black screen, as its taxi fades
        private const float SettleSeconds = 1.5f;  // s under the black screen after the warp, for the city round the new place to stream in
        private const float WayWait = 20f;         // s the game is given to take the player out of a building
        private const float StopWait = 6f;         // s a moving vehicle is given to stop before the player gets out
        private const float MeshReach = 6f;        // m round a place the walking mesh is looked for
        private const float Near = 3f;             // m: a player this close to a place is there already
        private const float BuildingReach = 400f;  // m round the player the scaffolding's building is looked for
        private const float TopReach = 6f;         // m up or down from the player's level a door may be

        private static readonly WaitForSecondsRealtime Tick = new WaitForSecondsRealtime(0.25f);
        private static readonly WrapShell Shell = new WrapShell();

        /// <summary>A save is loaded and ready, the player in the city.</summary>
        public static bool Loaded => ModState.Loaded && SaveGameManager.Current != null && !LoadScene.isLoading && PlayerHelper.PlayerController != null;

        public static bool Loading => LoadScene.isLoading;

        /// <summary>The game runs: loaded, its clock going (no menu, no pause, no time machine).</summary>
        public static bool Running => Loaded && Time.timeScale >= 0.01f;

        /// <summary>The phone's full menu (where the options are) is open: the game pauses under it.</summary>
        public static bool MenuOpen => FullMenu.IsOpen;

        /// <summary>The player's own pause (the pause button), as the game keeps it apart from a menu's.</summary>
        public static bool PlayerPaused
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.gameSpeed != null && uis.gameSpeed.playerGameSpeed != null && uis.gameSpeed.playerGameSpeed.paused;
            }
        }

        /// <summary>The player's pause set as the pause button sets it.</summary>
        public static void SetPause(bool paused)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.gameSpeed != null)
            {
                uis.gameSpeed.SetPlayerPause(paused);
            }
        }

        public static bool Session => GoingPublic.InSession;

        /// <summary>True in a city the mod does not know: the expansion's checks stand on New York's map.</summary>
        public static bool OtherCity => CityCheck.Kind == CityKind.Other;

        public static Vector3 Player => PlayerHelper.PlayerController != null ? PlayerHelper.PlayerController.transform.position : Vector3.zero;

        public static void Tell(string key, Dictionary<string, string> data, float seconds)
        {
            Notifications.Show(NotificationType.Info, key, data, seconds);
        }

        public static void Warn(string key, Dictionary<string, string> data, float seconds)
        {
            Notifications.Show(NotificationType.Warning, key, data, seconds);
        }

        // ---------------------------------------------------------------- the save that undoes the run

        /// <summary>
        /// A save of the game's own under the run's name in this character's saves, as the district save's test writes one
        /// (SaveGameManager.Save with a name: the mod's hook runs in its event like at any save). The game's own name for the
        /// save the player plays is kept, and its autosave still sees what the player has not saved. The file, or null with why.
        /// </summary>
        public static string SaveStart(string name, out string why)
        {
            why = null;
            GameInstance save = SaveGameManager.Current;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            if (save == null || game == null)
            {
                why = "no save is loaded";
                return null;
            }
            if (SaveGameManager.SavingGameInProgress)
            {
                why = "a save is being written";
                return null;
            }
            if (!GameSaves())
            {
                why = "the game does not save here now";
                return null;
            }
            string folder = SaveGamePathHelper.GetCharacterFolderPath(save.characterId);
            string file = Path.Combine(folder, name + (game.useSaveGameTypeJson ? ".json" : ".hsg"));
            if (File.Exists(file))
            {
                File.Delete(file);   // the last run's: the file that stands is this run's
            }
            string kept = save.SaveGameName;
            bool unsaved = SaveGameManager.HasChangesSinceLastSave();
            bool saved;
            try
            {
                SaveHook.Keep(null);   // the mod's hook in the game's save event, last
                saved = SaveGameManager.Save(SaveGameManager.SaveType.OldAgeBackUp, name, folder);
            }
            finally
            {
                save.SaveGameName = kept;   // a save under another name names the game after it, as the player's own does
                if (unsaved)
                {
                    SaveGameManager.MarkChange();
                }
            }
            if (!saved)
            {
                why = "the game did not save (it refused or failed)";
                return null;
            }
            return file;
        }

        public static bool Saving => SaveGameManager.SavingGameInProgress;

        // The game's own test of whether saving is allowed here (private: through the contract; "yes" if it cannot be read).
        private static bool GameSaves()
        {
            try
            {
                System.Reflection.MethodInfo method = GameContract.CanSave.Method;
                return method == null || (bool)method.Invoke(null, null);
            }
            catch (Exception)
            {
                return true;
            }
        }

        // ---------------------------------------------------------------- out of doors, and moved

        public static bool Indoors => BuildingManager.IsInsideBuilding;

        public static bool InVehicle => PlayerHelper.IsUsingVehicle;

        /// <summary>True while the game is taking the player in or out of a building, or its black screen is up.</summary>
        public static bool Busy
        {
            get
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                return UiFader.isFading || (manager != null && (manager.enteringBuilding || manager.exitingBuilding)) || TaxiSystem.IsTraveling;
            }
        }

        /// <summary>
        /// The player out of any vehicle (once it has stopped, by the game's own exit) and any building (by the building's
        /// own way out, as its exit zone takes them); <paramref name="why"/> holds what stood in the way, or stays null.
        /// </summary>
        public static IEnumerator Outdoors(string[] why)
        {
            why[0] = null;
            float until = Time.unscaledTime + WayWait;
            while (Busy && Time.unscaledTime < until)
            {
                yield return Tick;
            }
            GameManager game = InstanceBehavior<GameManager>.Instance;
            if (PlayerHelper.IsUsingVehicle && game != null && game.selectedVehicle != null)
            {
                float stop = Time.unscaledTime + StopWait;
                while (game.selectedVehicle != null && Mathf.Abs(game.selectedVehicle.CurrentSpeed) > 1f && Time.unscaledTime < stop)
                {
                    yield return Tick;
                }
                if (game.selectedVehicle != null && PlayerHelper.IsUsingVehicle)
                {
                    game.selectedVehicle.ExitVehicle();
                }
                yield return Tick;
                yield return Tick;
                UIs hud = InstanceBehavior<UIs>.Instance;   // the game lets go of the vehicle only when its panel closes (Law/Custody does the same)
                if (game.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                {
                    hud.playerHUD.itemPanelUI.Toggle(false);
                }
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                why[0] = "the player could not be got out of the vehicle";
                yield break;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                if (manager != null)
                {
                    manager.ExitFromBuilding(0);
                }
                until = Time.unscaledTime + WayWait;
                while ((BuildingManager.IsInsideBuilding || Busy) && Time.unscaledTime < until)
                {
                    yield return Tick;
                }
                if (BuildingManager.IsInsideBuilding)
                {
                    why[0] = "the game did not take the player out of the building within " + WayWait.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " s";
                }
            }
        }

        /// <summary>True when the player stands within a few metres of the place already.</summary>
        public static bool There(Vector3 at)
        {
            Vector3 d = Player - at;
            d.y = 0f;
            return d.sqrMagnitude <= Near * Near;
        }

        /// <summary>
        /// The player moved to a place the way the game's taxi moves them (TaxiSystem.TravelCoroutine), without its fare and
        /// its clock: hidden and held, the black screen, the warp onto the walking mesh, turned to face <paramref name="yaw"/>
        /// with the camera behind them as the game turns it when the player leaves a car, shown again, the black screen off.
        /// </summary>
        public static IEnumerator MoveTo(Vector3 at, float yaw)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || player.Character == null)
            {
                yield break;
            }
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, MeshReach, NavMesh.AllAreas))
            {
                at = hit.position;
            }
            player.Character.ToggleVisibility(show: false);
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
            try
            {
                yield return UiFader.Fade(FadeSeconds);
                player.Character.WarpSafely(at);
                Face(player, yaw);
                float settle = Time.unscaledTime + SettleSeconds;
                while (Time.unscaledTime < settle)
                {
                    yield return null;
                }
            }
            finally
            {
                player.Character.ToggleVisibility(show: true);
                player.UnsetNavigationBlocker(Blocker);
                player.Character.Reset();
            }
            yield return UiFader.UnFade(FadeSeconds);
        }

        // The player and the camera turned the same way, as CarController.ExitVehicle turns the camera.
        private static void Face(PlayerController player, float yaw)
        {
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            GameManager game = InstanceBehavior<GameManager>.Instance;
            PedestrianCam cam = game != null && game.pedestrianCamera != null ? game.pedestrianCamera.GetComponent<PedestrianCam>() : null;
            if (cam != null)
            {
                cam.angle = yaw;
            }
        }

        // ---------------------------------------------------------------- the places

        /// <summary>The lots of the airport's grass: those the test building is tried on (NewBuildingsTestPlot), found free offline.</summary>
        public static int AirportLots => NewBuildingsTestPlot.Lots.Length;

        /// <summary>The spot at the front of an airport lot, facing into it.</summary>
        public static void AirportLot(int index, out Vector3 at, out float yaw)
        {
            MadeLot lot = NewBuildingsTestPlot.Lots[index];
            RunRules.Front(lot.X, lot.Z, lot.Facing, lot.Depth, out float x, out float z, out yaw);
            at = new Vector3(x, Ground(x, z), z);
        }

        /// <summary>The airport lot the test building stands on, or the first (where it stands when asked for from open ground) while it is not made.</summary>
        public static int TestBuildingLot()
        {
            MadeBuilding made = MadeBuildings.Get(NewBuildingsTestPlot.Id);
            if (made == null)
            {
                return 0;
            }
            int best = -1;
            float bestSqr = 40f * 40f;   // m: a door this near a lot's middle is that lot's
            for (int i = 0; i < NewBuildingsTestPlot.Lots.Length; i++)
            {
                MadeLot lot = NewBuildingsTestPlot.Lots[i];
                float dx = made.DoorX - lot.X, dz = made.DoorZ - lot.Z;
                if (dx * dx + dz * dz < bestSqr)
                {
                    bestSqr = dx * dx + dz * dz;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The test building's door, from outside it, facing in; false while it is not made or is hidden.</summary>
        public static bool TestBuildingDoor(out Vector3 at, out float yaw)
        {
            MadeBuilding made = MadeBuildings.Get(NewBuildingsTestPlot.Id);
            at = Vector3.zero;
            yaw = 0f;
            if (made == null || made.Controller == null || !made.Shown)
            {
                return false;
            }
            Vector3 middle = made.Controller.transform.position;
            float outward = RunRules.Toward(middle.x, middle.z, made.DoorX, made.DoorZ);
            double turn = outward * Math.PI / 180.0;
            at = new Vector3(made.DoorX + (float)Math.Sin(turn) * RunRules.FrontStep, made.DoorY, made.DoorZ + (float)Math.Cos(turn) * RunRules.FrontStep);
            yaw = (outward + 180f) % 360f;
            return true;
        }

        /// <summary>The front of the first open lot of Uptown's first block that nothing stands on, facing into it; false while Uptown is not laid.</summary>
        public static bool UptownLot(out Vector3 at, out float yaw, out string lotId)
        {
            at = Vector3.zero;
            yaw = 0f;
            lotId = null;
            IReadOnlyList<Lot> lots = UptownLots.FirstBlock;
            for (int i = 0; i < lots.Count; i++)
            {
                Lot lot = lots[i];
                if (!UptownLots.Open(lot) || DistrictBuildings.Taken(lot.Id))
                {
                    continue;
                }
                RunRules.Front(lot.X, lot.Z, lot.Facing, lot.Depth, out float x, out float z, out yaw);
                at = new Vector3(x, UptownLots.GroundY, z);
                lotId = lot.Id;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The street door of a building of three to six storeys, nearest the player first: one with a door, not fenced,
        /// not the player's, not one of the mod's own places or buildings, its shell read from its own colliders (as the
        /// scaffolding reads it). The spot a step out from its door, facing it. Read once when a check asks for it.
        /// </summary>
        public static bool BesideBuilding(out Vector3 at, out float yaw, out string address)
        {
            at = Vector3.zero;
            yaw = 0f;
            address = null;
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            if (all == null)
            {
                return false;
            }
            Vector3 player = Player;
            var near = new List<KeyValuePair<float, int>>(64);
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                if (!CivicCity.HasDoor(c) || GameMembers.Fenced(c.building) || SpecialBuildings.Holds(c) || MadeBuildings.Of(c) != null)
                {
                    continue;
                }
                Vector3 door = c.entranceDoors[0].doorTransform.position;
                Vector3 off = door - player;
                float sqr = off.x * off.x + off.z * off.z;
                if (sqr < BuildingReach * BuildingReach && Mathf.Abs(off.y) < TopReach)
                {
                    near.Add(new KeyValuePair<float, int>(sqr, i));
                }
            }
            near.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int k = 0; k < near.Count; k++)
            {
                CityBuildingController c = all[near[k].Value];
                BuildingRegistration reg = c.building.GetRegistration();
                if ((reg != null && (reg.RentedByPlayer || reg.BuildingOwnedByPlayer)) || !BuildingShell.Read(c, Shell, out Vector3 origin, out Quaternion _) || !RunRules.Storeys(Shell.Top))
                {
                    continue;
                }
                Vector3 door = c.entranceDoors[0].doorTransform.position;
                float outward = RunRules.Toward(origin.x, origin.z, door.x, door.z);
                double turn = outward * Math.PI / 180.0;
                at = new Vector3(door.x + (float)Math.Sin(turn) * RunRules.FrontStep, door.y, door.z + (float)Math.Cos(turn) * RunRules.FrontStep);
                yaw = (outward + 180f) % 360f;
                address = CivicCity.AddressText(c.building.StreetName, c.building.StreetNumber) + " (" + Mathf.RoundToInt(Shell.Top / RunRules.StoreyMetres) + " storeys)";
                return true;
            }
            return false;
        }

        // The height of the city's ground under a point: the walking mesh's there, else the city's own ground's.
        private static float Ground(float x, float z)
        {
            return NavMesh.SamplePosition(new Vector3(x, UptownLots.GroundY, z), out NavMeshHit hit, MeshReach, NavMesh.AllAreas) ? hit.position.y : UptownLots.GroundY;
        }

        // ---------------------------------------------------------------- time

        /// <summary>The game's own time machine on to the next game hour and a minute past it, as a sleep or a taxi ride runs the clock on.</summary>
        public static bool ToNextHour()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning)
            {
                return false;
            }
            Timestamp now = TimeHelper.Now();
            float minutes = 61f - now.Minute;   // to a minute past the next hour
            Timestamp goal = TimeHelper.Now();
            goal.AddMinutes(minutes);
            uis.timeMachine.StartTimeMachine(goal, disableCancel: true);
            return true;
        }

        public static bool TimeMachine
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.timeMachine != null && uis.timeMachine.isRunning;
            }
        }
    }
}
