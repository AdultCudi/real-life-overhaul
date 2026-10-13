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
    ///   headquarters   the city's tech companies each in an empty office building of the city's, named, on the map, with
    ///                  a furnished lobby (Headquarters, LobbyView); where their jobs are: option "Tech company headquarters"
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
        private Frontages _fronts;
        private ServerRoomView _room;
        private LobbyView _lobby;
        private OfficeView _office;
        private Action<Address> _onEnter, _onExit, _enterSeen, _exitSeen;
        private string _inside;          // the building the views were last asked about ("street~number"), null outdoors
        private int _tries;
        private bool _soon;              // the player has just gone in or out: looked at again next frame
        private bool _roomBroken, _hqBroken, _frontBroken, _officeBroken, _lobbyBroken;
        private DataCentre _preview;     // the test button's data centre, while its room stands

        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>The headquarters (for the map and the careers); never null while this exists.</summary>
        internal Headquarters Hq => _hq;

        private void Awake()
        {
            Instance = this;
            RoomMeasure.Logger = Logger;
            _hq = new Headquarters(Logger);
            _fronts = new Frontages(Logger);
            _room = new ServerRoomView(Logger);
            _lobby = new LobbyView(Logger);
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
                    }
                    else if (_hq.Picked)
                    {
                        _hq.Off();
                    }
                }
                catch (Exception e)
                {
                    Fault(ref _hqBroken, "the headquarters", e);
                    _hq.Off();
                }
            }
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
                _inside = key;
                _tries = 0;
            }
            if (key == null)
            {
                return;
            }
            float now = Time.time;
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
                    Headquarters.Hq hq = Settings.TechSiteHq ? _hq.At(reg.StreetName, reg.StreetNumber) : null;
                    if (hq != null && Headquarters.Open(hq))
                    {
                        if (!_lobby.Active && _tries < TriesMost)
                        {
                            _tries++;
                            if (_lobby.Begin(manager, hq))
                            {
                                Logger?.Info("Tech sites: headquarters: " + _lobby.Said + ".");
                            }
                        }
                        _lobby.Tick(now);
                        return;
                    }
                    if (_lobby.Active)
                    {
                        _lobby.End();
                    }
                }
                catch (Exception e)
                {
                    Fault(ref _lobbyBroken, "the lobby", e);
                    _lobby.End();
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
            _office.End();
            _preview = null;
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

        /// <summary>A tech company's headquarters by its name (the careers' jobs there).</summary>
        public bool HeadquartersOf(string company, out string street, out int number)
        {
            street = null;
            number = 0;
            return Settings.TechSiteHq && _hq != null && _hq.Of(company, out street, out number);
        }

        /// <summary>Changes whenever the headquarters do.</summary>
        public long HeadquartersStamp => Settings.TechSiteHq && _hq != null && _hq.All.Count > 0 ? 1 + _hq.All.Count + _hq.Stamp * 16L : 0;

        /// <summary>The headquarters as places on the city map.</summary>
        public void HeadquartersPlaces(List<MapPlace> into)
        {
            for (int i = 0; _hq != null && i < _hq.All.Count; i++)
            {
                Headquarters.Hq hq = _hq.All[i];
                if (hq.Building == null)
                {
                    continue;
                }
                into.Add(new MapPlace(hq.Front, hq.Name + ", " + hq.Building.building.Address.ToFormattedString(), hq.Building) { CardTypeKey = "reallife_hq_card_type" });
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
                Logger?.Info("Tech sites test: " + hq.Name + " at " + hq.Street + " " + hq.Number + (Headquarters.Open(hq) ? " (its lobby is open)" : " (let: it moves at the next look)") + ".");
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
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _inside = null;
        }
    }
}
