using System;
using System.Collections;
using BAModAPI;
using RealLifeOverhaul.Tech;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The fit-out of the mod's buildings (Scripts/FitOut): the host of every fitted-out floor's building, a little each
    /// frame (<see cref="FitView"/>: the tech companies' headquarters behind their lobbies, Tech/Sites/TechSites; the bare
    /// back of a police station, Law/StationInterior.Fill.cs; a data centre's floor round its server hall,
    /// Tech/Sites/ServerRoomView), and the fit-out of the mod's other named buildings when the player is inside one that
    /// stands empty: the university (Careers/CampusBuilding: lecture rooms, a library, study desks, a lounge) and, should
    /// one be walked into, the City Utilities office and the courthouse (a city office's desks, filing, waiting seats).
    ///
    /// Only a building the mod names as its own (Core/SpecialBuildings), with its feature on, and standing empty: nobody
    /// rents or owns it, not the player, not another player in a Going Public session, not a rival, and nothing of the
    /// game's is in it. The other features' own scenes in such a building come first: the room is measured a moment
    /// after the player is in (on the walkable floor, which what they stand is cut out of, and round anything of the
    /// game's), and nothing of this is put where they are.
    ///
    /// The cost: no Update. A one-second timer on the real clock (which building the player is in; the people of a
    /// floor that stands); the building itself is FitView's, a few milliseconds a frame while it goes up. Nothing is saved.
    /// </summary>
    public sealed class FitOutDirector : MonoBehaviour
    {
        private const float Settle = 1.5f;          // seconds inside before a named building's floor is measured: its own scenes first
        private const int TriesMost = 3;

        public static IModLogger Logger;

        /// <summary>The host of every fitted-out floor; null while the mod is not loaded.</summary>
        public static FitOutDirector Instance { get; private set; }

        private readonly FitView.Request _request = new FitView.Request();
        private FitView _view;
        private string _inside;
        private float _since;
        private int _tries;
        private bool _broken;

        private void Awake()
        {
            Instance = this;
            _view = new FitView(Logger);
            _view.Built += v => Logger?.Info("Fit-out: " + v.Said + ".");
            StartCoroutine(Tick());
        }

        private void OnDestroy()
        {
            try
            {
                _view?.End();
                GameFurniture.Forget();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return second;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    _broken = true;
                    Logger?.Error(e);
                    Logger?.Warn("Fit-out: the mod's named buildings are left as they are for this session after the error above.");
                    _view.End();
                }
            }
        }

        private void Slow()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            bool inside = SaveGameManager.Current != null && !LoadScene.isLoading && ModState.Loaded && !Meta.SaveTools.Dormant
                          && BuildingManager.IsInsideBuilding && manager != null && !manager.enteringBuilding && !manager.exitingBuilding
                          && !Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking && manager.buildingRegistration != null;
            BuildingRegistration reg = inside ? manager.buildingRegistration : null;
            string key = reg != null ? TalentGame.Place(reg) : null;
            float now = Time.time;
            if (key != _inside)
            {
                _view.End();
                _inside = key;
                _since = now;
                _tries = 0;
            }
            if (key == null)
            {
                return;
            }
            if (_view.Active)
            {
                _view.Tick(now);
                if (!Wanted(reg, out _, out _))
                {
                    _view.End();   // (the option switched off, or the building let)
                }
                return;
            }
            if (_tries >= TriesMost || now - _since < Settle || !Wanted(reg, out FitKind kind, out string name))
            {
                return;
            }
            _tries++;
            Begin(manager, reg, kind, name);
        }

        // The mod's own building the player is in, if its floor is to be fitted out: its kind and its name for the log.
        private static bool Wanted(BuildingRegistration reg, out FitKind kind, out string name)
        {
            kind = FitKind.CivicOffice;
            name = null;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController building = city != null && reg != null ? city.FindCityBuildingController(reg.Address) : null;
            string holder = SpecialBuildings.HolderOf(building);
            if (holder == null || !Empty(reg))
            {
                return false;
            }
            switch (holder)
            {
                case "campus":
                    kind = FitKind.Campus;
                    name = "the university";
                    return Careers.CareersDirector.UniversityOpen;
                case "utilities_office":
                    name = "the City Utilities office";
                    return Settings.Utilities;
                case "courthouse":
                    name = "the courthouse";
                    return JusticeSettings.Enabled && JusticeSettings.Courthouse;
                default:
                    return false;   // (the headquarters and the police stations fit out their own; the hotels are never walked into)
            }
        }

        // Standing empty: nobody rents or owns it (the player, another player in a session, a rival), nothing of the game's is in it.
        private static bool Empty(BuildingRegistration reg)
        {
            return reg != null && reg.AvailableForRent && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && string.IsNullOrEmpty(reg.businessOwnerRivalId)
                   && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty") && reg.Layout == null
                   && (reg.itemInstances == null || reg.itemInstances.Count == 0);
        }

        private void Begin(BuildingManager manager, BuildingRegistration reg, FitKind kind, string name)
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (player == null || !Shapes.Available || !RoomMeasure.Door(manager, player.transform.position, player.transform.forward, out Vector3 door, out Vector3 inward))
            {
                return;
            }
            RoomGrid grid = RoomMeasure.Measure(manager, door, true, out float floor);
            if (grid == null)
            {
                return;
            }
            _request.Reset();
            _request.Name = name + " (" + reg.StreetName + " " + reg.StreetNumber + ")";
            _request.Kind = kind;
            _request.Seed = TechRandom.Of(TalentGame.Place(reg));
            _request.Building = manager;
            _request.Grid = grid;
            _request.Floor = floor;
            _request.Door = door;
            _request.Inward = inward;
            // No design of the game's furnishes a university or a city office: the plan's own rooms do, on the walls and
            // floors of an office design of the game's for the unit.
            _request.LookKinds.Add("LawFirm");
            _request.LookKinds.Add("Headquarters");
            _view.Begin(_request, this);
        }
    }
}
