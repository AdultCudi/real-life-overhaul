using System;
using System.Collections;
using BAModAPI;
using RealLifeOverhaul.Tech;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The host of every fitted-out floor (<see cref="FitView"/>: tech headquarters, a police station's back rooms, a data
    /// centre's floor), and the fit-out of the mod's other named buildings that stand empty when the player is inside: the
    /// courthouse. Only a building the mod names as its own, with its feature on, that nobody rents or owns (no player of a
    /// Going Public session, no rival) and with nothing of the game's in it; what other features stand there is kept.
    /// The cost: no Update; a one-second timer on the real clock, a few frames' look at one flag after a door, the room
    /// measured only behind the black screen, and FitView's few milliseconds a frame while a floor goes up.
    /// Nothing is saved.
    /// </summary>
    public sealed class FitOutDirector : MonoBehaviour
    {
        private const int TriesMost = 3;   // measures of one building a visit
        private static readonly int HotEntered = HotSpots.Slot("FitOut: FitOutDirector.Entered");
        private static readonly int HotSlow = HotSpots.Slot("FitOut: FitOutDirector.Slow");

        public static IModLogger Logger;

        /// <summary>The host of every fitted-out floor; null while the mod is not loaded.</summary>
        public static FitOutDirector Instance { get; private set; }

        private readonly FitView.Request _request = new FitView.Request();
        private FitView _view;
        private Action<Address> _onEnter, _enterSeen;
        private string _inside;
        private int _tries;
        private bool _soon;
        private bool _broken;

        private void Awake()
        {
            Instance = this;
            _view = new FitView(Logger);
            _view.Built += v => Logger?.Info("Fit-out: " + v.Said + ".");
            _onEnter = Guard.Wrap<Address>(_ => Entered());
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            GlobalEvents.onEnterBuilding -= _onEnter;
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
                Safely();
            }
        }

        // Just gone in: the room is looked at as soon as the game has put it together, under its black screen.
        private void Entered()
        {
            using var hotSpot = HotSpots.Time(HotEntered);
            if (!_soon && isActiveAndEnabled)
            {
                _soon = true;
                StartCoroutine(Guard.Routine(SoonAfter()));
            }
        }

        private IEnumerator SoonAfter()
        {
            yield return null;
            for (int frames = 0; frames < 180; frames++)
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                if (manager == null || !manager.enteringBuilding)
                {
                    break;
                }
                yield return null;
            }
            _soon = false;
            Safely();
        }

        private void Safely()
        {
            if (_broken)
            {
                return;
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

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotSlow);
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
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
                    _view.End();   // the option switched off, or the building let
                }
                return;
            }
            // only behind the black screen: a few thousand samples of the walkable floor are a long frame
            if (_tries >= TriesMost || !SceneFinds.Hidden || !Wanted(reg, out FitKind kind, out string name))
            {
                return;
            }
            _tries++;
            Begin(manager, reg, kind, name);
        }

        // The mod's building the player is in, if its floor is to be fitted out: its kind, and its name for the log.
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
                case "courthouse":
                    name = "the courthouse";
                    return JusticeSettings.Enabled && JusticeSettings.Courthouse;
                default:
                    return false;   // the headquarters and police stations fit out their own; the others are the game's or never entered
            }
        }

        // Nobody rents or owns it (the player, another player in a session, a rival), and nothing of the game's is in it.
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
            // the plan's own rooms, on the walls and floors of one of the game's office designs for the unit
            _request.LookKinds.Add("LawFirm");
            _request.LookKinds.Add("Headquarters");
            _view.Begin(_request, this);
        }
    }
}
