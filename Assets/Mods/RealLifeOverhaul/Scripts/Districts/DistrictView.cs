using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Extensions;
using Localizor;
using UI.Notification;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// The district view: the city's districts tinted on the game's own city map by one of ten layers, as Cities:
    /// Skylines tints its info views, with a key, a card for the district pointed at and the districts in order.
    ///
    ///   the layers   rows of the map's own filter list, under a line of their own just before the game's
    ///                neighbourhoods (Core/MapPlaces.RegisterChoice): ticking one shows it and unticks the others,
    ///                unticking it puts the map back as the game had it. The one ticked is remembered.
    ///   the tint     the game's own district zones (the boxes its neighbourhood filters show), in the band's colour
    ///                at the game's own strength. Put back in the game's colours when the view goes, and the game's
    ///                own filters applied again, so a neighbourhood the player had ticked shows as before.
    ///   the window   a copy of the map's filter window beside it (DistrictPanel): the key, the card and the list.
    ///   the card     every figure the mod keeps for the district under the pointer, or the one last clicked, or the
    ///                one the player is in.
    ///
    /// The figures are read once a day: when the map is opened on a day not yet read, and when a new day comes while it
    /// is open. Crime is the one thing the view keeps itself, because no feature keeps it by district: every crime the
    /// mod reports (a street crime staged near the player, a car broken into, a hit and run, a break-in at one of the
    /// player's shops) is noted against its district with its day, and the layer counts the last four weeks
    /// (DistrictLog, one line in the save).
    ///
    /// The cost: nothing per frame while the map is shut or no layer is ticked. A look a second in real time keeps the
    /// game's events held and notices the save. While a layer shows on the open map one script looks once a frame
    /// whether the map is still the plain map and whether the game has hidden a zone (seven tests), and every tenth of a
    /// second, if the mouse has moved, which district it is over (seven rectangle tests).
    /// </summary>
    public sealed class DistrictView : MonoBehaviour
    {
        public const string ChoiceId = "districts";
        private const string Mod = "reallife";       // the start of every key; a layer's words are its id after one of the stems below
        private const float PointEvery = 0.1f;       // seconds (real time) between looks at what the mouse is over
        private const float ClickPixels = 8f;        // a press and release closer than this on the map is a click, not a drag
        private const float ReportReach = 400f;      // metres from the nearest building a report outside every zone may be

        public static IModLogger Logger;
        public static DistrictView Instance { get; private set; }

        // The rows' icons: one of the game's own pictures where it has one in memory, else one of the map places' glyphs.
        private static readonly string[] Sprites = { "Icon-Coin", "Icon-Building", null, "Icon-PersonManage", null, null, null, null, "Icon-Shop", "Icon-Parking" };
        private static readonly MapIcon[] Glyphs = { MapIcon.Star, MapIcon.Briefcase, MapIcon.Flag, MapIcon.Target, MapIcon.Shield, MapIcon.Star, MapIcon.Wrench, MapIcon.Target, MapIcon.Briefcase, MapIcon.Ballot };

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
            _onMap = OnMap;
            _onDay = OnDay;
            _onSave = OnSave;
            enabled = false;   // (LateUpdate only while a layer shows)
        }

        private void Start()
        {
            try
            {
                var rows = new MapPlaces.ChoiceRow[DistrictRules.Layers];
                GameUi.GameSprites.Want(Sprites);
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
            StartCoroutine(Look());
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

        // ---------------------------------------------------------------- once a second

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

        // ---------------------------------------------------------------- the map's events

        private void OnMap(bool open)
        {
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

        private void OnDay()
        {
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

        // ---------------------------------------------------------------- while a layer shows

        private void LateUpdate()
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
                // The game hides every zone it has no ticked filter for whenever it applies its filters (any tick, the
                // map's opening, the Market Insider's "view on map"): the view's are shown again in the same frame.
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

        // ---------------------------------------------------------------- the figures

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

        private bool _bridgeSaid;

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

        // ---------------------------------------------------------------- the window

        // The district the card is about: the one pointed at in the list or on the map, else the one kept, else the player's.
        private MapDistrict Carded()
        {
            string id = _listHover ?? _hover ?? _kept ?? DistrictGame.PlayerDistrict();
            for (int i = 0; id != null && i < _districts.Count; i++)
            {
                if (_districts[i].Id == id)
                {
                    return _districts[i];
                }
            }
            return null;
        }

        private void Card()
        {
            if (!_showing || _panel == null || !_panel.Alive)
            {
                return;
            }
            int layer = Layer;
            if (layer < 0)
            {
                return;
            }
            MapDistrict card = Carded();
            string key = layer + "|" + (card != null ? card.Id : string.Empty) + "|" + _readDay;
            if (key == _carded)
            {
                return;
            }
            _carded = key;
            DistrictLayer which = (DistrictLayer)layer;
            string id = DistrictRules.Ids[layer];
            _panel.Begin(T(Mod + "_dv_row_" + id), T(Mod + "_dv_about_" + id) + "\n<color=#C9CED3>" + T("reallife_dv_hint") + "</color>");

            // The key.
            _panel.Heading(T("reallife_dv_key"));
            LayerSense sense = DistrictRules.Sense(which);
            int[] edges = _edges[layer];
            bool any = false, none = false;
            for (int i = 0; i < _districts.Count; i++)
            {
                any |= _districts[i].HasFigure[layer];
                none |= !_districts[i].HasFigure[layer];
            }
            if (any && edges == null)
            {
                _panel.Swatch(DistrictRules.Colour(sense, DistrictRules.Bands / 2), T("reallife_dv_all_alike"));
            }
            else if (any)
            {
                for (int band = DistrictRules.Bands - 1; band >= 0; band--)
                {
                    if (!DistrictRules.HasRange(edges, band))
                    {
                        continue;   // (a range too narrow for five steps leaves a band no district can be in)
                    }
                    _panel.Swatch(DistrictRules.Colour(sense, band), T(Mod + "_dv_band_" + id + "_" + band.ToString(CultureInfo.InvariantCulture)) + "  <color=#C9CED3>" + Range(which, edges, band) + "</color>");
                }
            }
            if (none)
            {
                _panel.Swatch(DistrictRules.NoFigure, T(Mod + "_dv_none_" + id));
            }

            // The card.
            if (card != null)
            {
                _panel.Heading(card.Name);
                CardLines(card, which);
            }

            // The districts in order, highest first; those with no figure last.
            _panel.Heading(T("reallife_dv_districts"));
            var order = new List<MapDistrict>(_districts);
            order.Sort((a, b) =>
            {
                if (a.HasFigure[layer] != b.HasFigure[layer])
                {
                    return a.HasFigure[layer] ? -1 : 1;
                }
                int by = b.Figure[layer].CompareTo(a.Figure[layer]);
                return by != 0 ? by : string.CompareOrdinal(a.Name, b.Name);
            });
            for (int i = 0; i < order.Count; i++)
            {
                MapDistrict d = order[i];
                string figure = d.HasFigure[layer] ? Figure(which, d.Figure[layer]) : T("reallife_dv_no_figure");
                _panel.District(DistrictRules.Colour(sense, d.Band[layer]), d.Id, "<b>" + d.Name + "</b>  <color=#C9CED3>" + figure + "</color>", d.Centre);
            }
            _panel.End();
        }

        // Every figure of a district, the layer shown first.
        private void CardLines(MapDistrict d, DistrictLayer first)
        {
            var order = new List<DistrictLayer>(DistrictRules.Layers) { first };
            for (int i = 0; i < DistrictRules.Layers; i++)
            {
                if ((DistrictLayer)i != first)
                {
                    order.Add((DistrictLayer)i);
                }
            }
            string unit = DistrictGame.AreaUnit();
            for (int i = 0; i < order.Count; i++)
            {
                switch (order[i])
                {
                    case DistrictLayer.Wealth:
                        if (d.HasIncome)
                        {
                            Line(d.IncomeLive ? "reallife_dv_card_income" : "reallife_dv_card_income_start", "{level}", d.Income.ToString(CultureInfo.InvariantCulture));
                        }
                        Line("reallife_dv_card_classes", "{working}", Whole(d.Working), "{middle}", Whole(d.Middle), "{upper}", Whole(d.Upper));
                        break;
                    case DistrictLayer.Rent:
                        if (d.CommercialRent > 0)
                        {
                            Line("reallife_dv_card_rent", "{rent}", Money(DistrictGame.PerArea(d.CommercialRent / 100f)), "{unit}", unit);
                        }
                        if (d.HomeRent > 0)
                        {
                            Line("reallife_dv_card_home_rent", "{rent}", Money(DistrictGame.PerArea(d.HomeRent / 100f)), "{unit}", unit);
                        }
                        break;
                    case DistrictLayer.Property:
                        if (d.Value > 0)
                        {
                            Line("reallife_dv_card_value", "{value}", Money(DistrictGame.PerArea(d.Value)), "{unit}", unit);
                        }
                        break;
                    case DistrictLayer.Footfall:
                        if (d.Traffic > 0)
                        {
                            Line("reallife_dv_card_traffic", "{traffic}", Whole((d.Traffic + 5) / 10));
                        }
                        float mean = MeanDemand();
                        if (mean > 0f && d.DemandWeight > 0f)
                        {
                            Line("reallife_dv_card_demand", "{percent}", Whole(Mathf.RoundToInt(d.DemandWeight / mean * 100f)));
                        }
                        if (d.Footfall.HasValue && d.Footfall.Value != 0)
                        {
                            Line(d.Footfall.Value > 0 ? "reallife_dv_card_footfall_up" : "reallife_dv_card_footfall_down", "{percent}", Whole(Math.Abs(d.Footfall.Value)));
                        }
                        if (d.Bears.HasValue)
                        {
                            Line("reallife_dv_card_bears", "{percent}", Whole((d.Bears.Value + 5) / 10));
                        }
                        break;
                    case DistrictLayer.Crime:
                        Line("reallife_dv_card_crime", "{count}", Whole(d.Crimes), "{days}", Whole(DistrictRules.CrimeDays));
                        break;
                    case DistrictLayer.Standing:
                        if (d.HasStanding)
                        {
                            Line("reallife_dv_card_opinion", "{opinion}", Whole(d.Opinion));
                            Line("reallife_dv_card_known", "{known}", Whole(d.Known));
                        }
                        break;
                    case DistrictLayer.Supply:
                        if (d.HasSupply && d.Accounts > 0)
                        {
                            Line(d.Cut > 0 ? "reallife_dv_card_supply_cut" : "reallife_dv_card_supply", "{supplied}", Whole(d.Supplied), "{accounts}", Whole(d.Accounts), "{cut}", Whole(d.Cut));
                        }
                        else if (d.HasSupply)
                        {
                            Line("reallife_dv_card_supply_none");
                        }
                        break;
                    case DistrictLayer.Internet:
                        if (d.HasInternet)
                        {
                            Line(d.Down > 0 ? "reallife_dv_card_internet_down" : "reallife_dv_card_internet", "{providers}", Whole(d.Providers), "{down}", Whole(d.Down));
                            if (d.Fibre == 2)
                            {
                                Line("reallife_dv_card_fibre_open", "{share}", Whole((d.Share + 5) / 10));
                            }
                            else if (d.Fibre == 1)
                            {
                                Line("reallife_dv_card_fibre_dug", "{day}", Whole(d.ReadyDay));
                            }
                        }
                        break;
                    case DistrictLayer.Competition:
                        Line("reallife_dv_card_businesses", "{count}", Whole(d.Businesses), "{mine}", Whole(d.Mine), "{rivals}", Whole(d.Rivals));
                        if (d.Types.Count > 0)
                        {
                            var types = new StringBuilder(64);
                            for (int t = 0; t < d.Types.Count; t++)
                            {
                                if (t > 0)
                                {
                                    types.Append(", ");
                                }
                                types.Append(DistrictGame.TypeName(d.Types[t].Key)).Append(" (").Append(d.Types[t].Value.ToString(CultureInfo.InvariantCulture)).Append(')');
                            }
                            Line("reallife_dv_card_types", "{types}", types.ToString());
                        }
                        break;
                    case DistrictLayer.Council:
                        if (d.Parking > 0f)
                        {
                            Line("reallife_dv_card_parking", "{price}", Money(d.Parking));
                        }
                        if (d.HasCouncil)
                        {
                            if (d.Sites > 0)
                            {
                                Line("reallife_dv_card_sites", "{sites}", Whole(d.Sites));
                            }
                            if (d.Electorate > 0)
                            {
                                Line("reallife_dv_card_electorate", "{voters}", Whole(d.Electorate));
                            }
                            if (!string.IsNullOrEmpty(d.Winner))
                            {
                                Line("reallife_dv_card_result", "{winner}", d.Winner, "{percent}", Whole(d.WinnerPercent));
                            }
                        }
                        break;
                }
            }
        }

        private float MeanDemand()
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < _districts.Count; i++)
            {
                if (_districts[i].DemandWeight > 0f)
                {
                    sum += _districts[i].DemandWeight;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0f;
        }

        private void Line(string key, string a = null, string va = null, string b = null, string vb = null, string c = null, string vc = null)
        {
            string words = T(key);
            if (a != null)
            {
                words = words.Replace(a, Bold(va));
            }
            if (b != null)
            {
                words = words.Replace(b, Bold(vb));
            }
            if (c != null)
            {
                words = words.Replace(c, Bold(vc));
            }
            _panel.Line(words);
        }

        private static string Bold(string value)
        {
            return "<b>" + (value ?? string.Empty) + "</b>";
        }

        // A layer's figure as the key and the list write it.
        private static string Figure(DistrictLayer layer, long value)
        {
            switch (layer)
            {
                case DistrictLayer.Rent:
                    return Money(DistrictGame.PerArea(value / 100f)) + " / " + DistrictGame.AreaUnit();
                case DistrictLayer.Property:
                    return Money(DistrictGame.PerArea(value)) + " / " + DistrictGame.AreaUnit();
                case DistrictLayer.Footfall:
                    return Whole((int)((value + 5) / 10));
                case DistrictLayer.Supply:
                    return Whole((int)value) + "%";
                case DistrictLayer.Council:
                    return Money(value / 100f);
                default:
                    return Whole((int)value);
            }
        }

        // A band's range in the key: "under 88", "88 to 95", "112 or more".
        private static string Range(DistrictLayer layer, int[] edges, int band)
        {
            int? from, to;
            DistrictRules.Range(edges, band, out from, out to);
            if (!from.HasValue && to.HasValue && to.Value <= 0)
            {
                return Figure(layer, 0);   // (nothing at all: "0", not "under 1")
            }
            if (layer == DistrictLayer.Supply && from.HasValue && !to.HasValue)
            {
                return Figure(layer, from.Value);   // (the top of a share: "100%", not "100% or more")
            }
            if (!from.HasValue && to.HasValue)
            {
                return T("reallife_dv_range_under").Replace("{to}", Figure(layer, to.Value + 1L));
            }
            if (from.HasValue && !to.HasValue)
            {
                return T("reallife_dv_range_over").Replace("{from}", Figure(layer, from.Value));
            }
            if (from.HasValue && to.Value == from.Value)
            {
                return Figure(layer, from.Value);
            }
            return from.HasValue ? T("reallife_dv_range_between").Replace("{from}", Figure(layer, from.Value)).Replace("{to}", Figure(layer, to.Value)) : string.Empty;
        }

        private static string Money(float amount)
        {
            return amount >= 100f ? Mathf.Round(amount).ToCurrencyFormat().Replace(".00", string.Empty) : amount.ToCurrencyFormat();
        }

        private static string Whole(int value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        private static string T(string key)
        {
            return key.GetLocalization();
        }

        // ---------------------------------------------------------------- crime reports

        /// <summary>A crime reported at a place in the city: noted against the district it is in (Wiring/DistrictLinks).</summary>
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

        // ---------------------------------------------------------------- tests

        /// <summary>Options-page test: opens the city map with the district view on (the wealth layer if none was ticked).</summary>
        public void TestOpen()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (_broken || _choice == null || city == null || city.cityMap == null || !ModState.Loaded)
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_refused", null, 8f);
                return;
            }
            if (!Settings.DistrictView)
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_off", null, 8f);
                return;
            }
            if (_choice.Picked < 0)
            {
                _choice.Pick((int)DistrictLayer.Wealth);
            }
            if (!CityMap.IsOpen && CityMap.CanOpenMap())
            {
                city.cityMap.Toggle();
            }
            Logger?.Info("District view: test, the map opened with the layer '" + DistrictRules.Ids[Math.Max(0, _choice.Picked)] + "'.");
        }

        /// <summary>Options-page test: a crime report where the player stands, as if one had been reported there.</summary>
        public void TestCrime()
        {
            if (_broken || !_logLoaded || Helpers.PlayerHelper.PlayerController == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_refused", null, 8f);
                return;
            }
            string district = DistrictGame.PlayerDistrict();
            if (string.IsNullOrEmpty(district))
            {
                Notifications.Show(NotificationType.Warning, "reallife_dv_test_refused", null, 8f);
                return;
            }
            Note(district);
            var data = new Dictionary<string, string>(1) { { "district", DistrictGame.Name(district) } };
            Notifications.Show(NotificationType.Info, "reallife_dv_test_crime_done", data, 8f);
            Logger?.Info("District view: test, a crime report noted in " + district + ".");
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
