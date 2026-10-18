using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The tech sector's buildings, only to be seen and never saved or sent. Cost: no Update; a one-second timer and a half-second
    /// one for a server room's lights, one comparison when idle. A fault switches its part off for the session.
    /// </summary>
    public sealed partial class TechSites : MonoBehaviour
    {
        private const string Warehouse = "ba:buildingtype_warehouse";
        private const string Office = "ba:buildingtype_office";
        private const int TriesMost = 5;   // tries to build a room in one visit

        public static IModLogger Logger;
        public static TechSites Instance { get; private set; }

        private Headquarters _hq;
        private TowerFloors _floors;
        private int _floorsStamp = -1;   // the headquarters' stamp the floor the player is on was laid for
        private bool _lifting;           // the lift is under way: the screen is dark
        private bool _censusDone;        // the city's office buildings counted in the log, once a city load
        private float _censusAt;         // real time the count is made by at the latest
        private Frontages _fronts;
        private ServerRoomView _room;
        private LobbyView _lobby;
        private FitOut.FitView _hqFloor;                                  // the rest of a headquarters' floor, round its lobby
        private readonly FitOut.FitView.Request _hqRequest = new FitOut.FitView.Request();
        private OfficeView _office;
        private Action<Address> _onEnter, _onExit, _enterSeen, _exitSeen;
        private string _inside;          // the building the views were last asked about ("street~number"), null outdoors
        private int _tries;
        private bool _soon;              // the player has just gone in or out: looked at again next frame
        private bool _roomBroken, _hqBroken, _frontBroken, _officeBroken, _lobbyBroken;
        private DataCentre _preview;     // the test button's data centre, while its room stands
        private bool _testFloor;         // the test button's tech company's floor stands in a place of the player's

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The headquarters (for the map and the careers); never null while this exists.</summary>
        internal Headquarters Hq => _hq;

        private void Awake()
        {
            Instance = this;
            RoomMeasure.Logger = Logger;
            _hq = new Headquarters(Logger);
            _floors = new TowerFloors(Logger);
            _floors.ChangeFloor = floor =>
            {
                if (!_lifting && isActiveAndEnabled)
                {
                    StartCoroutine(Guard.Routine(Lift(floor)));
                }
            };
            _fronts = new Frontages(Logger);
            _room = new ServerRoomView(Logger);
            _lobby = new LobbyView(Logger);
            _hqFloor = new FitOut.FitView(Logger);
            _hqFloor.Built += HqFloorBuilt;
            _office = new OfficeView(Logger);
            _onEnter = Guard.Wrap<Address>(_ => Soon());   // a fault here never reaches the game's own handlers
            _onExit = Guard.Wrap<Address>(_ => Soon());
            StartCoroutine(Guard.Routine(Tick()));
            StartCoroutine(Guard.Routine(Lights()));
        }

        private void OnDestroy()
        {
            GlobalEvents.onEnterBuilding -= _onEnter;
            GlobalEvents.onExitBuilding -= _onExit;
            StopAll();
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
                while (StartLine.Holds(StartSeats.TechSites))
                {
                    yield return null;   // the first load after a save waits for a frame of its own
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private static readonly int HotSoon = HotSpots.Slot("Tech: TechSites.Soon");
        // The player has just gone in or out: the room is looked at as soon as the game has put it together under its fade.
        private void Soon()
        {
            using var hotSpot = HotSpots.Time(HotSoon);
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
                if (manager == null || (!manager.enteringBuilding && !manager.exitingBuilding))
                {
                    break;
                }
                yield return null;
            }
            _soon = false;
            try
            {
                Slow();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private IEnumerator Lights()
        {
            var half = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                yield return half;
                if (_room.Active)
                {
                    try
                    {
                        _room.Blink();
                    }
                    catch (Exception e)
                    {
                        Fault(ref _roomBroken, "the server room", e);
                    }
                }
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Tech: TechSites.Slow");
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);   // the timer's tick, and the look just after a door
            if (Save == null || LoadScene.isLoading || !ModState.Loaded || Meta.SaveTools.Dormant)
            {
                return;
            }
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
            EventHold.Keep(ref GlobalEvents.onExitBuilding, _onExit, ref _exitSeen);
            int day = Save.Day;
            if (!_hqBroken)
            {
                try
                {
                    if (Settings.TechSiteHq)
                    {
                        _hq.Tick(day);
                        _floors.Tick(_hq);
                    }
                    else if (_hq.Picked)
                    {
                        _hq.Off();
                        _floors.Hide("the option is off");
                        _floors.Leave();
                    }
                }
                catch (Exception e)
                {
                    Fault(ref _hqBroken, "the headquarters", e);
                    _hq.Off();
                    _floors.Hide("the headquarters stood down");
                    _floors.Leave();
                }
            }
            Census();
            Inside(day);
            if (!_frontBroken)
            {
                try
                {
                    InfraFile infra = TechDirector.Instance != null ? TechDirector.Instance.Infra : null;
                    PlayerController player = Helpers.PlayerHelper.PlayerController;
                    if (!Settings.TechSiteCloud || infra == null)
                    {
                        _fronts.Clear();
                    }
                    else if (player != null && !BuildingManager.IsInsideBuilding)
                    {
                        _fronts.Tick(player.transform.position, infra);
                    }
                }
                catch (Exception e)
                {
                    Fault(ref _frontBroken, "the data centres' frontages", e);
                    _fronts.Clear();
                }
            }
        }

        private void Inside(int day)
        {
            if (!Settings.TechSiteRooms && !Settings.TechSiteHq && !Settings.TechSiteOffices && _inside == null && _preview == null && !_testFloor
                && !_lifting && !_room.Active && !_lobby.Active && !_hqFloor.Active && !_office.Active && _floors.In == null)
            {
                return;   // every view off and nothing standing
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            bool inside = BuildingManager.IsInsideBuilding && manager != null && !manager.enteringBuilding && !manager.exitingBuilding
                          && !Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking && manager.buildingRegistration != null;
            string key = inside ? TalentGame.Place(manager.buildingRegistration) : null;
            if (key != _inside)
            {
                EndViews();
                _floors.Leave();
                _inside = key;
                _tries = 0;
            }
            if (key == null || _lifting)
            {
                return;
            }
            float now = Time.time;
            if (_testFloor)
            {
                _hqFloor.Hide(BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode);
                _hqFloor.Tick(now);   // the test's floor stands until the player leaves; nothing else is built meanwhile
                return;
            }
            BuildingRegistration reg = manager.buildingRegistration;
            InfraFile infra = TechDirector.Instance != null ? TechDirector.Instance.Infra : null;
            DataCentre centre = _preview ?? (infra != null && GoingPublic.Mine(reg) ? infra.CentreAt(key) : null);
            if (!_roomBroken)
            {
                try
                {
                    if (centre != null && (Settings.TechSiteRooms || _preview != null))
                    {
                        if (!_room.Active && _tries < TriesMost)
                        {
                            _tries++;
                            if (_room.Begin(manager, centre, day))
                            {
                                Logger?.Info("Tech sites: server room: " + _room.Said + ".");
                            }
                        }
                        else if (_room.Active && _room.Tick(manager, centre, day, now))
                        {
                            _room.End();   // built again at the next look, round the furniture as it is now
                            _tries = 0;
                        }
                        return;
                    }
                    if (_room.Active)
                    {
                        _room.End();
                    }
                }
                catch (Exception e)
                {
                    Fault(ref _roomBroken, "the server room", e);
                    _room.End();
                }
            }
            if (!_lobbyBroken)
            {
                try
                {
                    Headquarters.Hq hq = OnFloor(reg);
                    if (hq != null)
                    {
                        if (!_lobby.Active && _tries < TriesMost)
                        {
                            _tries++;
                            bool floor = FitOut.FitOutDirector.Instance != null;
                            if (_lobby.Begin(manager, hq, lamps: !floor))
                            {
                                Logger?.Info("Tech sites: headquarters: " + _lobby.Said + ".");
                                if (!floor || !HqFloor(manager, hq))
                                {
                                    _lobby.HangLamps();
                                }
                            }
                        }
                        _lobby.Tick(now);
                        _hqFloor.Tick(now);
                        return;
                    }
                    if (_lobby.Active)
                    {
                        _lobby.End();
                        _hqFloor.End();
                    }
                }
                catch (Exception e)
                {
                    _floors.Leave();
                    Fault(ref _lobbyBroken, "the lobby", e);
                    _lobby.End();
                    _hqFloor.End();
                }
            }
            if (!_officeBroken)
            {
                try
                {
                    bool office = Settings.TechSiteOffices && infra != null && GoingPublic.Mine(reg) && TechTypes.Is(reg.businessTypeName)
                                  && TypeOf(reg) == Office;
                    if (office)
                    {
                        if (!_office.Active && _tries < TriesMost)
                        {
                            _tries++;
                            if (_office.Begin(manager, TalentGame.NameOf(reg)))
                            {
                                Logger?.Info("Tech sites: office: " + _office.Said + ".");
                            }
                        }
                        else if (_office.Active && _office.Tick(manager))
                        {
                            _office.End();
                            _tries = 0;
                        }
                    }
                    else if (_office.Active)
                    {
                        _office.End();
                    }
                }
                catch (Exception e)
                {
                    Fault(ref _officeBroken, "the tech office", e);
                    _office.End();
                }
            }
        }

        // The company whose floor of a tower the player is on, laid again if the towers changed under them. Null outside a
        // tower that may be walked into.
        private Headquarters.Hq OnFloor(BuildingRegistration reg)
        {
            Headquarters.Tower tower = Settings.TechSiteHq && reg != null ? _hq.At(reg.StreetName, reg.StreetNumber) : null;
            if (tower == null || !Headquarters.Open(tower))
            {
                if (_floors.In != null)
                {
                    _floors.Leave();
                }
                return null;
            }
            if (!ReferenceEquals(_floors.In, tower))
            {
                _floorsStamp = _hq.Stamp;
                _floors.Arrive(tower);
            }
            else if (_floorsStamp != _hq.Stamp)
            {
                // The towers were chosen again while the player was in this one: their floor is laid again only if another
                // company is on it now, or with fewer floors the one the door would give.
                _floorsStamp = _hq.Stamp;
                if (_floors.Here == null)
                {
                    _floors.Arrive(tower);
                }
                if (_lobby.Active && (_floors.Here == null || _lobby.Company != _floors.Here.Company))
                {
                    _lobby.End();
                    _hqFloor.End();
                    _tries = 0;
                }
            }
            return _floors.Here;
        }

        // The lift: behind a dark screen this floor's views come down, the other company's are laid on the same room and the
        // player stands at the entrance; light again once the floor stands, or after a few seconds.
        private IEnumerator Lift(int floor)
        {
            _lifting = true;
            yield return UI.UiFader.Fade(0.25f);
            bool moved = false;
            try
            {
                _lobby.End();
                _hqFloor.End();
                _tries = 0;
                if (_floors.In != null)
                {
                    _floors.Moved(floor);
                    PlayerController player = Helpers.PlayerHelper.PlayerController;
                    if (player != null)
                    {
                        player.ResetNavigation();
                        player.Character.navmeshAgent.Warp(_floors.Spawn);
                        player.transform.rotation = _floors.SpawnTurn;
                    }
                    moved = true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            for (int frames = 0; frames < 10; frames++)
            {
                yield return null;   // what was destroyed is gone, and its cuts out of the walkable floor with it
            }
            _lifting = false;
            if (moved && Save != null)
            {
                try
                {
                    Inside(Save.Day);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                float until = Time.realtimeSinceStartup + 6f;
                while (_hqFloor.Active && !_hqFloor.Ready && Time.realtimeSinceStartup < until)
                {
                    yield return null;   // behind the dark screen the floor goes up at the hidden pace
                }
            }
            yield return UI.UiFader.UnFade(0.25f);
        }

        // Once a city load: the city's office buildings counted in the log by who has them, once the mod's buildings are chosen
        // or after two minutes.
        private void Census()
        {
            if (_censusDone)
            {
                return;
            }
            if (_censusAt <= 0f)
            {
                _censusAt = Time.realtimeSinceStartup + 120f;
            }
            Func<bool> others = TechBridges.OthersPicked;
            bool ready = Settings.TechSiteHq ? _hq.Picked : others == null || others();
            if (!ready && Time.realtimeSinceStartup < _censusAt)
            {
                return;
            }
            _censusDone = true;
            try
            {
                OfficeCensus.Log(Logger);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game's business designs that furnish a company's offices, in order of preference.
        private static readonly string[] SoftwareDesigns = { "WebDevelopmentAgency", "GraphicDesigner", "Headquarters", "MarketingAgency", "LawFirm" };
        private static readonly string[] StudioDesigns = { "GraphicDesigner", "WebDevelopmentAgency", "Headquarters", "MarketingAgency", "LawFirm" };
        private static readonly string[] OfficeDesigns = { "Headquarters", "WebDevelopmentAgency", "GraphicDesigner", "LawFirm", "MarketingAgency" };

        private static string[] DesignsOf(string company)
        {
            if (company.IndexOf("Games", StringComparison.Ordinal) >= 0)
            {
                return StudioDesigns;
            }
            return company.IndexOf("Software", StringComparison.Ordinal) >= 0 || company.IndexOf("Online", StringComparison.Ordinal) >= 0
                   || company.IndexOf("Analytics", StringComparison.Ordinal) >= 0 || company.IndexOf("Search", StringComparison.Ordinal) >= 0
                ? SoftwareDesigns
                : OfficeDesigns;
        }

        // The rest of a headquarters' floor round its lobby (FitOut/FitView), keeping the ways to its desk and seats. False if
        // it could not be begun.
        private bool HqFloor(BuildingManager manager, Headquarters.Hq hq)
        {
            FitOut.FitView.Request r = _hqRequest;
            r.Reset();
            r.Name = hq.Name + "'s headquarters";
            r.Kind = FitOut.FitKind.TechOffice;
            r.Seed = TechRandom.Of(hq.Name);
            r.Building = manager;
            r.Place = HqTowers.FloorPlace(TalentGame.Place(manager.buildingRegistration), hq.Name);   // a job's desk is on its company's floor
            r.Grid = _lobby.Grid;
            r.Floor = _lobby.Floor;
            r.Door = _lobby.Door;
            r.Inward = _lobby.Inward;
            r.DesignKinds.AddRange(DesignsOf(hq.Name));
            Lobby lobby = _lobby.Plan;
            for (int i = 0; i < lobby.Pieces.Count; i++)
            {
                SitePiece p = lobby.Pieces[i];
                r.Site.Fixed.Add(FitOut.FitRect.Around(p.X, p.Z, p.HalfX, p.HalfZ, p.Stands, wall: p.Kind == SitePieceKind.Screen));
            }
            if (lobby.HasDesk)
            {
                r.Site.Keep.Add(new FitOut.FitSpot(lobby.StaffX, lobby.StaffZ, 0.5f));
                r.Site.Keep.Add(new FitOut.FitSpot(lobby.FrontX, lobby.FrontZ, 0.5f));
                // the way from the door to the desk, and round it to the receptionist's side
                Vector3 door = _lobby.Door;
                r.Site.KeepFree.Add(new FitOut.FitRect(Mathf.Min(door.x, lobby.FrontX) - 0.7f, Mathf.Min(door.z, lobby.FrontZ) - 0.7f,
                    Mathf.Max(door.x, lobby.FrontX) + 0.7f, Mathf.Max(door.z, lobby.FrontZ) + 0.7f, false));
                r.Site.KeepFree.Add(FitOut.FitRect.Around(lobby.StaffX, lobby.StaffZ, 0.7f, 0.7f, false));
            }
            IReadOnlyList<Transform> seats = _lobby.SeatsOf;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i] != null)
                {
                    Vector3 front = seats[i].position + seats[i].forward * 0.5f;
                    r.Site.Keep.Add(new FitOut.FitSpot(front.x, front.z, 0.5f));
                }
            }
            return _hqFloor.Begin(r, FitOut.FitOutDirector.Instance);
        }

        private void HqFloorBuilt(FitOut.FitView view)
        {
            Logger?.Info("Tech sites: headquarters' floor: " + view.Said + ".");
            if (!view.HasLamps && _lobby.Active)
            {
                _lobby.HangLamps();   // neither the design nor the plan's rooms hung any
            }
        }

        private static string TypeOf(BuildingRegistration reg)
        {
            try
            {
                return reg.GetBuildingType();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void EndViews()
        {
            _room.End();
            _lobby.End();
            _hqFloor.End();
            _office.End();
            _preview = null;
            _testFloor = false;
        }

        private void Fault(ref bool broken, string what, Exception e)
        {
            if (broken)
            {
                return;
            }
            broken = true;
            Logger?.Error(e);
            Logger?.Warn("Tech sites: " + what + " is switched off for this session after the error above.");
        }

        /// <summary>Everything taken away (the feature's clean-up, the city going).</summary>
        public void StopAll()
        {
            try
            {
                EndViews();
                _fronts?.Clear();
                _hq?.Off();
                _floors?.Hide("the tech sites stopped");
                _floors?.Leave();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _inside = null;
        }
    }
}
