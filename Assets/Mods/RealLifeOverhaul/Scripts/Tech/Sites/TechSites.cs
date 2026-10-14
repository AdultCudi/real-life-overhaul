using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Streets;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The tech sector's buildings, all of them only to be seen (Scripts/Tech/Sites):
    ///
    ///   server rooms   the player's data centres furnished while the player is inside (ServerRoomView), by their tier and
    ///                  racks: option "Server rooms in data centres", with the tech sector
    ///   headquarters   the city's tech companies sharing a few of the city's large empty office buildings, a floor each
    ///                  (Headquarters, the towers), named, on the map, the floor chosen at the door or the lift (TowerFloors),
    ///                  each floor with its company's furnished lobby (LobbyView) and the rest of the floor fitted out round
    ///                  it, the game's own office design for the unit and rooms of a tech company's (FitOut/FitView, built a
    ///                  little a frame on FitOut/FitOutDirector); where their jobs are, the desk of one on its company's
    ///                  floor: option "Tech company headquarters"
    ///   frontages      the data centres' signs, booth, barrier and fence, and "Your cloud" on the map (Frontages):
    ///                  option "Data centre signs and security", with the tech sector
    ///   tech offices   a few fittings in an office a tech firm of the player's runs in (OfficeView): option "Tech fittings
    ///                  in your offices", with the tech sector
    ///
    /// None of it is saved, none of it is an item of the player's or counted in what their furniture is worth, and nothing
    /// is sent over Going Public's connection: every machine builds its own from the same city (the headquarters are the
    /// same for everyone; another player's building is never touched).
    ///
    /// The cost: no Update. A one-second timer on the real clock (which room the player is in, the headquarters, the
    /// frontage near the player) and a half-second one that changes two materials' colour while the player is in a
    /// server room, and does one comparison otherwise. A room is measured and built once on the way in, under the game's
    /// fade. A fault switches the part it was in off for the session with one line in the log.
    /// </summary>
    public sealed class TechSites : MonoBehaviour
    {
        private const string Warehouse = "ba:buildingtype_warehouse";
        private const string Office = "ba:buildingtype_office";
        private const int TriesMost = 5;

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
                    StartCoroutine(Lift(floor));
                }
            };
            _fronts = new Frontages(Logger);
            _room = new ServerRoomView(Logger);
            _lobby = new LobbyView(Logger);
            _hqFloor = new FitOut.FitView(Logger);
            _hqFloor.Built += HqFloorBuilt;
            _office = new OfficeView(Logger);
            _onEnter = _ => Soon();
            _onExit = _ => Soon();
            StartCoroutine(Tick());
            StartCoroutine(Lights());
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
        // The player has just gone in or out: the room is looked at as soon as the game has put it together (under its
        // fade), not at the next second. A few frames' look at one flag, then once.
        private void Soon()
        {
            using var hotSpot = HotSpots.Time(HotSoon);
            if (!_soon && isActiveAndEnabled)
            {
                _soon = true;
                StartCoroutine(SoonAfter());
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

        // The status lights of a server room, twice a second; one comparison otherwise.
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

        private void Slow()
        {
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

        // ---------------------------------------------------------------- the room the player is in

        private void Inside(int day)
        {
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
                _hqFloor.Tick(now);   // (the test's floor, until the player leaves: nothing else of this is built meanwhile)
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
                            _room.End();   // (built again at the next look, round the furniture as it is now)
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
                    bool office = Settings.TechSiteOffices && infra != null && GoingPublic.Mine(reg) && TechTypes.Is(reg.businessTypeName) && TypeOf(reg) == Office;
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

        // The company whose floor of a tower the player is in: the floor they arrive on the first time it is asked, and again
        // if the towers have changed under them (the views laid again for it). Null outside a tower that may be walked into.
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
                // The towers were chosen again while the player was in this one: the floor they are on stays, and is laid again
                // only if another company is on it now (or, the floors fewer, they are taken to the floor the door would give).
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

        /// <summary>The company whose floor of a tower the player is on now, or null (the careers' desk is on its company's floor).</summary>
        public string FloorCompany()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (!Settings.TechSiteHq || !BuildingManager.IsInsideBuilding || manager == null || manager.enteringBuilding || manager.exitingBuilding
                || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking)
            {
                return null;
            }
            Headquarters.Hq hq = OnFloor(manager.buildingRegistration);
            return hq != null ? hq.Name : null;
        }

        /// <summary>True while a panel of the towers is up, and in the frame its button ran (World/NearbyInteraction).</summary>
        public static bool TakesInteractKey => Instance != null && Instance._floors != null && Instance._floors.TakesKey;

        // The lift: the screen dark, this floor's views taken down, the other company's laid on the same room, the player at
        // the entrance; the screen light again once the floor stands (or after a few seconds).
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
                yield return null;   // (what was destroyed is gone, and its cuts out of the walkable floor with it)
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
                    yield return null;   // (behind the dark screen the floor goes up at the hidden pace)
                }
            }
            yield return UI.UiFader.UnFade(0.25f);
        }

        // Once a city load: the city's office buildings counted by who has them, when the mod's buildings have been chosen
        // (the towers last), or after two minutes whatever is chosen. One look at the city's buildings.
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

        // The kinds of business whose designs of the game's furnish a company's offices, in order: a software house's are the
        // web agencies', a games studio's the graphic designers', the rest (hardware, chips, payments, networks) a head office's.
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

        // The rest of a headquarters' floor round its lobby (FitOut/FitView): the game's design for the unit and the plan's own
        // rooms of a tech company, the lobby's pieces and the ways to its desk and seats kept. False if it could not be begun.
        private bool HqFloor(BuildingManager manager, Headquarters.Hq hq)
        {
            FitOut.FitView.Request r = _hqRequest;
            r.Reset();
            r.Name = hq.Name + "'s headquarters";
            r.Kind = FitOut.FitKind.TechOffice;
            r.Seed = TechRandom.Of(hq.Name);
            r.Building = manager;
            r.Place = HqTowers.FloorPlace(TalentGame.Place(manager.buildingRegistration), hq.Name);   // (the desk of a job there is on its company's floor)
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
                // The way from the door to the desk, and round it to the receptionist's side.
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
                _lobby.HangLamps();   // (neither the design nor the plan's rooms hung any)
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

        // ---------------------------------------------------------------- for others

        /// <summary>A tech company's headquarters by its name: its tower's address (the careers' jobs there).</summary>
        public bool HeadquartersOf(string company, out string street, out int number)
        {
            street = null;
            number = 0;
            return Settings.TechSiteHq && _hq != null && _hq.Of(company, out street, out number);
        }

        /// <summary>Changes whenever the headquarters do.</summary>
        public long HeadquartersStamp => Settings.TechSiteHq && _hq != null && _hq.All.Count > 0 ? 1 + _hq.All.Count + _hq.Stamp * 16L : 0;

        /// <summary>The towers as places on the city map: one each, its name and address (its companies are on its card and in the search).</summary>
        public void HeadquartersPlaces(List<MapPlace> into)
        {
            for (int i = 0; _hq != null && i < _hq.Towers.Count; i++)
            {
                Headquarters.Tower tower = _hq.Towers[i];
                if (tower.Building == null)
                {
                    continue;
                }
                into.Add(new MapPlace(tower.Front, tower.Name + ", " + tower.Building.building.Address.ToFormattedString(), tower.Building) { CardTypeKey = "reallife_hq_tower_card_type" });
            }
        }

        /// <summary>Changes whenever the player's data centres, their names or the cloud's do.</summary>
        public long CloudStamp
        {
            get
            {
                InfraFile infra = TechDirector.Instance != null ? TechDirector.Instance.Infra : null;
                if (!Settings.TechSiteCloud || infra == null || infra.Centres.Count == 0)
                {
                    return 0;
                }
                unchecked
                {
                    long stamp = 1 + infra.Centres.Count + _fronts.Stamp * 64L + (infra.Cloud.Open ? TechRandom.Of(infra.Cloud.Name) : 0) * 7L + (infra.Cloud.Hyper ? 3 : 0);
                    for (int i = 0; i < infra.Centres.Count; i++)
                    {
                        stamp = stamp * 31 + TechRandom.Of(infra.Centres[i].Name);
                    }
                    return stamp;
                }
            }
        }

        /// <summary>The player's data centres as places on the map: the cloud's regions, a hyperscaler's all of them.</summary>
        public void CloudPlaces(List<MapPlace> into)
        {
            InfraFile infra = TechDirector.Instance != null ? TechDirector.Instance.Infra : null;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            for (int i = 0; infra != null && i < infra.Centres.Count; i++)
            {
                DataCentre d = infra.Centres[i];
                if (!_fronts.Front(d.Place, out Vector3 front, out CityBuildingController building))
                {
                    BuildingRegistration reg = TalentGame.Find(d.Place);
                    building = reg != null && city != null ? city.FindCityBuildingController(reg.Address) : null;
                    if (building == null || !JobSpots.Front(building, out front))
                    {
                        continue;
                    }
                }
                string region = HiringGame.District(d.District);
                string name = infra.Cloud.Open && !string.IsNullOrEmpty(infra.Cloud.Name)
                    ? TechText.Fill("reallife_sites_map_region", "cloud", infra.Cloud.Name, "region", region)
                    : TechText.Fill("reallife_sites_map_centre", "name", d.Name);
                into.Add(new MapPlace(front, name) { Lit = building });
            }
        }

        // ---------------------------------------------------------------- tests

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
                Notifications.Show(built ? NotificationType.Success : NotificationType.Warning, built ? "reallife_sites_test_room_done" : "reallife_sites_test_room_failed", null, 8f, trackOnSaveGame: false);
            }
            catch (Exception e)
            {
                Fault(ref _roomBroken, "the server room", e);
            }
        }

        /// <summary>
        /// The test button: the room of the player's they stand in (an office or a warehouse) fitted out as a tech company's floor
        /// is round their furniture, the plan's own rooms only (never the game's design over their furniture, never a change
        /// to their walls and floors), until they leave. What a headquarters of that size and shape would look like, without
        /// walking to one.
        /// </summary>
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
                Notifications.Show(_testFloor ? NotificationType.Success : NotificationType.Warning, _testFloor ? "reallife_sites_test_floor_done" : "reallife_sites_test_floor_failed", null, 8f, trackOnSaveGame: false);
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
