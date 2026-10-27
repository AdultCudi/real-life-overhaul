using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// The district view: the city's districts tinted on the game's own city map by one of ten layers, picked from rows
    /// of the map's filter list, with a key, a card for the district pointed at and the districts in order
    /// (<see cref="DistrictPanel"/>). The game's zones and filters are put back when it goes. Figures are read once a
    /// day; crime is the one figure it keeps itself (<see cref="DistrictLog"/>), as no feature keeps it by district.
    /// Cost: nothing per frame while the map is shut or no layer is ticked; while one shows, a few tests a frame and a
    /// look at the mouse every tenth of a second.
    /// </summary>
    public sealed partial class DistrictView : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("districtview", "DistrictView", typeof(DistrictView));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const string ChoiceId = "districts";
        private const string Mod = "reallife";       // the start of every key; a layer's words are its id after one of the stems below
        private const float PointEvery = 0.1f;       // seconds (real time) between looks at what the mouse is over
        private const float ClickPixels = 8f;        // a press and release closer than this on the map is a click, not a drag
        private const float ReportReach = 400f;      // metres from the nearest building a report outside every zone may be

        public static IModLogger Logger;
        public static DistrictView Instance { get; private set; }

        // The rows' icons: one of the game's own pictures where it has one in memory, else one of the map places' glyphs.
        private static readonly string[] Sprites = { "Icon-Coin", "Icon-Building", null, "Icon-PersonManage", null, null, null, null, "Icon-Shop", "Icon-Parking" };
        private static readonly MapIcon[] Glyphs =
        {
            MapIcon.Star, MapIcon.Briefcase, MapIcon.Flag, MapIcon.Target, MapIcon.Shield, MapIcon.Star, MapIcon.Wrench, MapIcon.Target, MapIcon.Briefcase, MapIcon.Ballot
        };

        private MapPlaces.Choice _choice;
        private List<MapDistrict> _districts = new List<MapDistrict>(0);
        private readonly int[][] _edges = new int[DistrictRules.Layers][];
        private DistrictPanel _panel;
        private DistrictLog _log = new DistrictLog();
        private GameInstance _logSave;
        private bool _logLoaded;
        private int _readDay = int.MinValue;
        private bool _mapOpen, _showing, _tinted, _broken, _panelFailed;
        private int _paintedLayer = -1;
        private string _hover, _listHover, _kept, _carded;
        private float _nextPoint;
        private Vector3 _mouseAt, _downAt;
        private bool _down;
        private bool _bridgeSaid;

        private Action<bool> _onMap;
        private Action<bool> _mapSeen;
        private Action _onDay;
        private Action _daySeen;
        private Action _onSave;

        /// <summary>The layer ticked, or -1.</summary>
        public int Layer => _choice != null && Settings.DistrictView ? _choice.Picked : -1;

        private void Awake()
        {
            Instance = this;
            GameUi.GameSprites.Want(Sprites);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _onMap = Guard.Wrap<bool>(OnMap);
            _onDay = Guard.Wrap(OnDay);
            _onSave = OnSave;
            enabled = false;   // (LateUpdate only while a layer shows)
            MapParts.Add("district view", MapLook);   // asked after the map shut, with Developer diagnostics on (Diagnostics/SessionWatch.Map)
        }

        // The map check after the map shut (Diagnostics/SessionWatch.Map): no layer shown or tinted, its frame work off.
        private int MapLook(System.Text.StringBuilder line)
        {
            bool holding = _mapOpen || enabled || _showing || _tinted;
            line?.Append("district view: ").Append(holding ? "still as for the open map" : "off with the map");
            return holding ? 1 : 0;
        }

        private void Start()
        {
            try
            {
                var rows = new MapPlaces.ChoiceRow[DistrictRules.Layers];
                for (int i = 0; i < rows.Length; i++)
                {
                    Sprite icon = Sprites[i] != null ? GameUi.GameSprites.Get(Sprites[i]) : null;
                    int layer = i;
                    rows[i] = new MapPlaces.ChoiceRow
                    {
                        Id = DistrictRules.Ids[i],
                        TitleKey = Mod + "_dv_row_" + DistrictRules.Ids[i],
                        Icon = icon != null ? icon : MapPlaces.Glyph(Glyphs[i]),
                        Focus = () => Best(layer)
                    };
                }
                _choice = MapPlaces.RegisterChoice(ChoiceId, "reallife_dv_category", rows);
                _choice.Available = () => Settings.DistrictView && !_broken;
                _choice.Changed = Picked;
            }
            catch (Exception e)
            {
                Fail(e);
                return;
            }
            StartCoroutine(Guard.Routine(Look()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onCityMapToggle -= _onMap;
            GlobalEvents.onNewDay -= _onDay;
            if (_panel != null)
            {
                _panel.Destroy();
                _panel = null;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Look()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                try
                {
                    if (!_broken)
                    {
                        Tick();
                    }
                }
                catch (Exception e)
                {
                    Fail(e);
                }
                yield return second;
            }
        }

        private void Tick()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !ModState.Loaded)
            {
                return;
            }
            EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            SaveHook.Keep(_onSave);
            if (!_logLoaded || !ReferenceEquals(save, _logSave))
            {
                string line = null;
                if (save.modData != null)
                {
                    save.modData.TryGetValue(DistrictLog.Key, out line);
                }
                _log = DistrictLog.Read(line);
                if (!DistrictLog.Readable(line))
                {
                    SaveLines.Unreadable(save.modData, DistrictLog.Key, "its version is not one this version of the mod reads");   // (kept as it was read: Core/SaveLines)
                }
                _log.Prune(save.Day);
                _logSave = save;
                _logLoaded = true;
                _readDay = int.MinValue;
                Logger?.Info("District view: ready; " + _log.Reports + " crime report(s) of the last " + DistrictRules.CrimeDays + " days in the save.");
            }
            if (CityMap.IsOpen != _mapOpen)
            {
                OnMap(CityMap.IsOpen);   // (the event was missed: it had not been held again yet)
            }
            else if (_mapOpen)
            {
                Decide();   // (the option may have been switched on the options page)
            }
        }

        private static readonly int HotOnMap = HotSpots.Slot("Districts: DistrictView.OnMap");
        private void OnMap(bool open)
        {
            using var hotSpot = HotSpots.Time(HotOnMap);
            try
            {
                _mapOpen = open;
                _hover = _listHover = null;
                _down = false;
                if (!open)
                {
                    _kept = null;
                }
                Decide();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Districts: DistrictView.OnDay");
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            try
            {
                if (SaveGameManager.Current != null)
                {
                    _log.Prune(SaveGameManager.Current.Day);
                }
                _readDay = int.MinValue;
                if (_showing)
                {
                    Show();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // A row was ticked or unticked.
        private void Picked(int layer)
        {
            try
            {
                Decide();
                if (_showing)
                {
                    Show();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // Whether a layer belongs on the screen now, and putting it there or taking it off.
        private void Decide()
        {
            bool want = !_broken && _mapOpen && Layer >= 0;
            enabled = want;   // (LateUpdate: whether the map stays the plain map, the zones, the mouse)
            if (want && DistrictGame.MapIsPlain())
            {
                if (!_showing || _paintedLayer != Layer)
                {
                    Show();
                }
            }
            else if (_showing || _tinted)
            {
                Hide(_mapOpen && DistrictGame.MapIsPlain());
            }
        }

        // The districts and today's figures, read if they are not yet. False if the map has no districts to show.
        private bool Prepare()
        {
            if (_districts.Count == 0 || _districts[0].Zone == null)
            {
                _districts = DistrictGame.Read();
                _readDay = int.MinValue;
                if (_districts.Count == 0)
                {
                    Logger?.Warn("District view: the city map has no district zones to tint; the view is left off.");
                    _broken = true;
                    MapPlaces.Refresh();
                    return false;
                }
            }
            int today = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
            if (_readDay != today)
            {
                Read(today);
                _readDay = today;
            }
            return true;
        }

        private void Show()
        {
            if (!Prepare())
            {
                return;
            }
            int layer = Layer;
            Paint(layer);
            if (_panel == null || !_panel.Alive)
            {
                _panel = _panelFailed ? null : DistrictPanel.Make();
                if (_panel == null)
                {
                    _panelFailed = true;   // (the tint still shows; the window's absence is in the log once)
                }
                else
                {
                    _panel.Pointed = id => { _listHover = id; Card(); };
                    _panel.Clicked = id => { _kept = id; Card(); };
                    _panel.Closed = () => _choice?.Pick(-1);
                }
            }
            _showing = true;
            _carded = null;
            Card();
            _panel?.Show(true);
        }

        private void Hide(bool giveBack)
        {
            _showing = false;
            _panel?.Show(false);
            if (_tinted)
            {
                for (int i = 0; i < _districts.Count; i++)
                {
                    DistrictGame.Untint(_districts[i]);
                }
                _tinted = false;
                _paintedLayer = -1;
                if (giveBack)
                {
                    DistrictGame.GameFilters();   // the game's own neighbourhood filters, as the player had them
                }
            }
        }

        private void Paint(int layer)
        {
            if (layer < 0)
            {
                return;
            }
            LayerSense sense = DistrictRules.Sense((DistrictLayer)layer);
            for (int i = 0; i < _districts.Count; i++)
            {
                DistrictGame.Tint(_districts[i], DistrictRules.Colour(sense, _districts[i].Band[layer]));
            }
            _tinted = true;
            _paintedLayer = layer;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Districts: DistrictView.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (_broken || !_mapOpen)
            {
                enabled = false;
                return;
            }
            try
            {
                bool plain = DistrictGame.MapIsPlain();
                if (plain != _showing)
                {
                    if (plain)
                    {
                        Show();
                    }
                    else
                    {
                        Hide(false);   // (the subway's choice, the full menu or a choice of the mod's has the map: the game puts its own zones away)
                    }
                    return;
                }
                if (!_showing)
                {
                    return;
                }
                // The game hides every zone with no ticked filter whenever it applies its filters: shown again this frame.
                for (int i = 0; i < _districts.Count; i++)
                {
                    MeshRenderer zone = _districts[i].Zone;
                    if (zone != null && !zone.gameObject.activeSelf)
                    {
                        zone.gameObject.SetActive(true);
                    }
                }
                Pointer();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The district under the mouse (a tenth of a second at most, and only when it has moved), and a click that keeps one.
        private void Pointer()
        {
            bool onWindow = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0))
            {
                _down = !onWindow;
                _downAt = Input.mousePosition;
            }
            if (_down && Input.GetMouseButtonUp(0))
            {
                _down = false;
                Vector3 point;
                if (!onWindow && (Input.mousePosition - _downAt).sqrMagnitude <= ClickPixels * ClickPixels && DistrictGame.Pointed(out point))
                {
                    MapDistrict clicked = DistrictGame.At(_districts, point, 0f);
                    _kept = clicked != null ? clicked.Id : _kept;
                    Card();
                }
            }
            float now = Time.unscaledTime;
            if (now < _nextPoint || Input.mousePosition == _mouseAt)
            {
                return;
            }
            _nextPoint = now + PointEvery;
            _mouseAt = Input.mousePosition;
            string over = null;
            Vector3 ground;
            if (!onWindow && DistrictGame.Pointed(out ground))
            {
                MapDistrict under = DistrictGame.At(_districts, ground, 0f);
                over = under != null ? under.Id : null;
            }
            if (over != _hover)
            {
                _hover = over;
                Card();
            }
        }

        private void Read(int today)
        {
            DistrictGame.Figures(_districts);
            for (int i = 0; i < _districts.Count; i++)
            {
                MapDistrict d = _districts[i];
                Bridges(d, today);
                d.Crimes = _log.Count(d.Id, today, DistrictRules.CrimeDays);
                Set(d, DistrictLayer.Wealth, d.HasIncome, d.Income);
                Set(d, DistrictLayer.Rent, d.CommercialRent > 0, d.CommercialRent);
                Set(d, DistrictLayer.Property, d.Value > 0, d.Value);
                Set(d, DistrictLayer.Footfall, d.Traffic > 0, d.Traffic);
                Set(d, DistrictLayer.Crime, true, d.Crimes);
                Set(d, DistrictLayer.Standing, d.HasStanding, d.Opinion);
                Set(d, DistrictLayer.Supply, d.HasSupply && d.Accounts > 0, d.Accounts > 0 ? d.Supplied * 100L / d.Accounts : 0);
                Set(d, DistrictLayer.Internet, d.HasInternet, d.Providers);
                Set(d, DistrictLayer.Competition, d.Buildings > 0, d.Businesses);
                Set(d, DistrictLayer.Council, d.Parking > 0f, (long)Math.Round(d.Parking * 100.0));
            }
            var values = new long[_districts.Count];
            var known = new bool[_districts.Count];
            for (int layer = 0; layer < DistrictRules.Layers; layer++)
            {
                for (int i = 0; i < _districts.Count; i++)
                {
                    values[i] = _districts[i].Figure[layer];
                    known[i] = _districts[i].HasFigure[layer];
                }
                int[] edges;
                int[] bands = DistrictRules.BandAll((DistrictLayer)layer, values, known, out edges);
                _edges[layer] = edges;
                for (int i = 0; i < _districts.Count; i++)
                {
                    _districts[i].Band[layer] = bands[i];
                }
            }
        }

        private static void Set(MapDistrict d, DistrictLayer layer, bool has, long figure)
        {
            d.HasFigure[(int)layer] = has;
            d.Figure[(int)layer] = has ? figure : 0;
        }

        // What the other features say of a district. A reader that throws is said once and gives no figure.
        private void Bridges(MapDistrict d, int today)
        {
            d.HasIncome = d.HasStanding = d.HasSupply = d.HasInternet = d.HasCouncil = false;
            d.Footfall = d.Bears = null;
            d.Winner = null;
            try
            {
                if (DistrictBridges.Income != null)
                {
                    d.HasIncome = DistrictBridges.Income(d.Id, out d.Income, out d.IncomeLive);
                }
                d.Footfall = DistrictBridges.Footfall?.Invoke(d.Id);
                d.Bears = DistrictBridges.Bears?.Invoke(d.Id);
                if (DistrictBridges.Standing != null)
                {
                    d.HasStanding = DistrictBridges.Standing(d.Id, out d.Opinion, out d.Known);
                }
                if (DistrictBridges.Supply != null)
                {
                    d.HasSupply = DistrictBridges.Supply(d.Id, out d.Accounts, out d.Supplied, out d.Cut);
                }
                if (DistrictBridges.Internet != null)
                {
                    d.HasInternet = DistrictBridges.Internet(d.Id, today, out d.Providers, out d.Down, out d.Fibre, out d.ReadyDay, out d.Share);
                }
                if (DistrictBridges.Council != null)
                {
                    d.HasCouncil = DistrictBridges.Council(d.Id, out d.Sites, out d.Electorate, out d.Winner, out d.WinnerPercent);
                }
            }
            catch (Exception e)
            {
                if (!_bridgeSaid)
                {
                    _bridgeSaid = true;
                    Logger?.Error(e);
                    Logger?.Warn("District view: a feature's figures for the districts could not be read (above); those are left off the view.");
                }
            }
        }

        // Where a layer's row's arrow flies the map: the district highest on it (lowest on crime), once figures are read.
        private Vector3? Best(int layer)
        {
            try
            {
                if (_broken || !Prepare())
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                Fail(e);
                return null;
            }
            MapDistrict best = null;
            bool low = DistrictRules.Sense((DistrictLayer)layer) == LayerSense.LowGood;
            for (int i = 0; i < _districts.Count; i++)
            {
                MapDistrict d = _districts[i];
                if (d.HasFigure[layer] && (best == null || (low ? d.Figure[layer] < best.Figure[layer] : d.Figure[layer] > best.Figure[layer])))
                {
                    best = d;
                }
            }
            return best != null ? best.Centre : (Vector3?)null;
        }

        /// <summary>A crime reported at a place in the city, noted against its district.</summary>
        public void Report(Vector3 where)
        {
            if (_broken || SaveGameManager.Current == null || !_logLoaded || (where.x == 0f && where.z == 0f))
            {
                return;
            }
            if (_districts.Count == 0)
            {
                _districts = DistrictGame.Read();
            }
            MapDistrict d = DistrictGame.At(_districts, where, ReportReach);
            Note(d != null ? d.Id : DistrictGame.NearestDistrict(where, ReportReach));
        }

        /// <summary>A crime reported in a district by its id.</summary>
        public void ReportIn(string district)
        {
            if (!_broken && SaveGameManager.Current != null && _logLoaded)
            {
                Note(district);
            }
        }

        private void Note(string district)
        {
            if (string.IsNullOrEmpty(district))
            {
                return;
            }
            _log.Add(SaveGameManager.Current.Day, district);
            _readDay = int.MinValue;   // (read again the next time it is shown)
            if (_showing)
            {
                Show();
            }
        }

        private void OnSave()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || !_logLoaded || !ReferenceEquals(save, _logSave))
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                if (_log.Reports > 0)
                {
                    save.modData[DistrictLog.Key] = _log.Write();
                }
                else
                {
                    save.modData.Remove(DistrictLog.Key);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The save's crime reports are forgotten (Save tools: reset the mod's data).</summary>
        public void Forget()
        {
            _log.Clear();
            _readDay = int.MinValue;
        }

        private void Fail(Exception e)
        {
            if (_broken)
            {
                return;
            }
            _broken = true;
            Logger?.Error(e);
            Logger?.Warn("District view: switched off for this session after an error.");
            try
            {
                Hide(CityMap.IsOpen);
                MapPlaces.Refresh();
            }
            catch (Exception)
            {
                // (the map is going with the city)
            }
            enabled = false;
        }
    }
}
