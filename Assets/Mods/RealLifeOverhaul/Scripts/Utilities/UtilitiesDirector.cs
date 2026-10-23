using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The city's power, water and internet for every place the player rents or owns: accounts, the day's use, monthly bills,
    /// and businesses closed and homes worse off without them. Parts: .Bills, .Internet, .Gone, .Tests. Cost a frame: a few
    /// comparisons; the books once a morning, the places once an hour, the office once a second.
    /// </summary>
    public sealed partial class UtilitiesDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("utilities", "UtilitiesDirector", typeof(UtilitiesDirector));

        public const string AppId = "RealLifeOverhaulUtilities";
        private static readonly string[] AppGlyphs = { "Icon-Electricity", "Icon-Energy", "Icon-Lightbulb", "Icon-Building" };
        private const int CatchUpDays = 7;   // days at most added up one by one after a long jump (hospital, a trip)
        private const int ClosedHours = 2;   // hours a business without what it needs is kept closed, asked again each hour

        // each business closed for want of something, and what it was told it lacked: told once a spell, again when the
        // reason changes or it has been open in between (a headquarters has no switch of the game's to open it by hand)
        private readonly Dictionary<string, string> _toldClosed = new Dictionary<string, string>(8);

        public static IModLogger Logger;
        public static UtilitiesDirector Instance { get; private set; }

        /// <summary>True while the office's panel is up, or its button was pressed this frame.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door != null && Instance._door.TakesKey;

        /// <summary>True once the utilities have stopped for the session after an error (nothing more is billed or cut).</summary>
        public static bool Failed => Instance != null && Instance._failed;

        private UtilitiesState _state;
        private GameInstance _save;
        private bool _loaded, _failed, _gaveUp;
        private float _nextSecond, _nextHook;
        private int _hour = -1;
        private int _graceSeen = -1;   // the day the internet's grace last saw
        private Action _onSave;
        private Action _onDesk;
        private readonly List<PlaceInfo> _places = new List<PlaceInfo>(16);
        private readonly AccountIndex _index = new AccountIndex();   // the accounts by place and service, for the day's and the hour's passes
        private UtilitiesOffice _office;
        private DoorPanel _door;
        private DeskList _window;
        private UtilitiesDesk _desk;
        private PhoneApp _app;
        private UtilitiesApp _view;
        private BroadbandCompany _company;

        /// <summary>The accounts while a save is running; null otherwise.</summary>
        public UtilitiesState State => _loaded ? _state : null;

        /// <summary>The player's places as last read: each morning and hour, when the app opens, at the desk.</summary>
        public IReadOnlyList<PlaceInfo> Places => _places;

        /// <summary>The player's broadband company while a save is running; null otherwise.</summary>
        public BroadbandCompany Company => _loaded ? _company : null;

        public UtilitiesOffice Office => _office;
        public DeskList Window => _window;

        /// <summary>The player holds a supplied power account of their own (Life: the electricity company's events).</summary>
        public static bool HasPowerAccount
        {
            get
            {
                UtilitiesDirector d = Instance;
                if (d == null || !d._loaded)
                {
                    return false;
                }
                int today = UtilitiesGame.Today;
                for (int i = 0; i < d._state.Accounts.Count; i++)
                {
                    Account a = d._state.Accounts[i];
                    if (a.Service == Service.Power && a.State == SupplyState.Active && UtilitiesRules.Supplied(a, today))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>A place has a service now; true for a place the utilities do not know (nothing to cut).</summary>
        public static bool Supplied(BuildingRegistration reg, Service service)
        {
            UtilitiesDirector d = Instance;
            if (d == null || !d._loaded || reg == null || !Settings.Utilities)
            {
                return true;
            }
            Account a = d._state.Find(UtilitiesGame.Place(reg), service);
            return a == null || UtilitiesRules.Supplied(a, UtilitiesGame.Today);
        }

        /// <summary>A place's power, water and internet a day now, money, at today's prices; nothing for what is not supplied.</summary>
        public static float DayCost(BuildingRegistration reg)
        {
            UtilitiesDirector d = Instance;
            if (d == null || !d._loaded || reg == null || !Settings.Utilities)
            {
                return 0f;
            }
            string key = UtilitiesGame.Place(reg);
            int today = UtilitiesGame.Today, season = CalendarRules.Season(today, GameCalendar.DaysPerYear), level = Prices.Level;
            for (int p = 0; p < d._places.Count; p++)
            {
                PlaceInfo place = d._places[p];
                if (!string.Equals(place.Place, key, StringComparison.Ordinal))
                {
                    continue;
                }
                float sum = 0f;
                for (int s = 0; s < UtilitiesCatalogue.Services; s++)
                {
                    var service = (Service)s;
                    Account a = d._state.Find(key, service);
                    if (a != null && UtilitiesRules.Supplied(a, today))
                    {
                        long use = UtilitiesRules.Use(service, place.Kind, place.Sqm, UtilitiesGame.People(place), true, season);
                        sum += UtilitiesRules.DayCents(service, use, level, a.State != SupplyState.Active) / 100f;
                    }
                }
                Account net = d._state.Find(key, Service.Internet);
                if (net != null && net.State == SupplyState.Active)
                {
                    sum += InternetRules.DayCents(net, GameCalendar.MonthDays) / 100f;
                }
                return sum;
            }
            return 0f;
        }

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _onSave = OnSaveGame;
            _door = new DoorPanel(Logger, "Utilities check (office panel)");
            _window = new DeskList("RealLifeOverhaul Utilities Desk", 1100f, 120f);
            _onDesk = _door.Press(DeskClicked);
            _company = new BroadbandCompany(this);
        }

        private void OnDestroy()
        {
            Stop("the mod is unloading");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Utilities: UtilitiesDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            if (!Settings.Utilities)
            {
                if (_loaded)
                {
                    Stop("switched off");
                }
                return;
            }
            GameManager game = InstanceBehavior<GameManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (_loaded && !ReferenceEquals(save, _save))
            {
                Stop("another save");
            }
            if (game == null || game.playerController == null || save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            try
            {
                if (!_loaded)
                {
                    Load(save);
                }
                if (Time.unscaledTime >= _nextHook)
                {
                    _nextHook = Time.unscaledTime + 2f;
                    SaveHook.Keep(_onSave);   // the game clears its event hub on every load: the save hook is kept attached
                }
                int today = save.Day;
                if (today != _graceSeen)
                {
                    GraceClock(today);
                }
                if (today > _state.LastDay + 1)
                {
                    Day(today);
                }
                if (save.Hour != _hour)
                {
                    _hour = save.Hour;
                    Hour();
                }
                if (Time.unscaledTime >= _nextSecond)
                {
                    _nextSecond = Time.unscaledTime + 1f;
                    Second();
                }
            }
            catch (Exception e)
            {
                Fault("its update", e);
            }
        }

        private void Load(GameInstance save)
        {
            _save = save;
            int today = save.Day;
            _state = UtilitiesStore.Load(save.modData, out int version);
            bool fresh = _state == null;
            bool firstNet = InternetRules.FirstMeeting(_state);   // read before the seed below is drawn
            if (fresh)
            {
                // the feature has just appeared in this save: today's use is the first added up, from a month starting today
                _state = new UtilitiesState { LastDay = today - 1, MonthStart = today };
            }
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // the internet's outages and prices are drawn from it, the same on every reload
            }
            _loaded = true;
            _hour = save.Hour;
            _office = new UtilitiesOffice(Logger);
            _desk = new UtilitiesDesk(this);
            Sync(today, fresh);
            _graceSeen = today;
            if (firstNet)
            {
                BeginGrace(today);
                OnSaveGame();   // kept in the save at once: switched off and on before a save, it is read back, not begun again
            }
            BroadbandRules.Apply(_state.Network, today);   // the company as a provider, as this save has it
            Logger?.Info("Utilities: " + (fresh ? "started in this save" : "read from the save (line v" + version + ")") + ": " + _state.Accounts.Count + " account(s) for "
                         + _places.Count + " place(s); the month began on day " + _state.MonthStart
                         + (_state.Network.Exists ? "; a broadband company (" + _state.Network.State + ")" : string.Empty) + ".");
        }

        private void Stop(string why)
        {
            if (!_loaded)
            {
                return;
            }
            _door.Hide(why);
            _window.Destroy(e => Logger?.Error(e));
            _office?.Dispose();
            _office = null;
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
            _view = null;
            _loaded = false;
            _save = null;
            _state = null;
            _graceSeen = -1;
            BroadbandRules.Apply(null, 0);   // no company of this save's is a provider of the next one
            _places.Clear();
            Logger?.Info("Utilities: stopped (" + why + ").");
        }

        private void Fault(string where, Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Utilities: switched off for this session after an error in " + where
                         + ". Nothing more is billed or cut; every place keeps its supply, and a business closed for want of one opens again.");
            try
            {
                Stop("an error");
            }
            catch (Exception)
            {
                // stopping is best effort after a fault
            }
        }

        private void OnSaveGame()
        {
            try
            {
                if (_loaded && _save != null && _save.modData != null)
                {
                    _save.modData[UtilitiesStore.Key] = UtilitiesStore.Write(_state);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The accounts brought up to today's places: new places get one, a place bought after renting needs its own.</summary>
        public void Sync(int today, bool fresh)
        {
            ReadPlaces();
            int newOwned = 0;
            _index.Build(_state);
            for (int p = 0; p < _places.Count; p++)
            {
                PlaceInfo place = _places[p];
                for (int s = 0; s < UtilitiesCatalogue.Services; s++)
                {
                    var service = (Service)s;
                    Account a = _index.Find(_state, place.Place, service);
                    if (a == null || a.State == SupplyState.Closed)
                    {
                        if (a != null)
                        {
                            _state.Accounts.Remove(a);   // a place given up and taken again starts afresh; what was owed was settled when it closed
                        }
                        a = place.Owned ? UtilitiesRules.Temporary(place.Place, service, place.Kind, today) : UtilitiesRules.Rented(place.Place, service, place.Kind, today);
                        _state.Accounts.Add(a);
                        _index.Put(_state, a);
                        if (place.Owned && s == 0)
                        {
                            newOwned++;
                        }
                        continue;
                    }
                    a.Kind = place.Kind;
                    if (place.Owned && !a.Owned && a.State == SupplyState.Active)
                    {
                        // bought after being rented: the landlord's account goes with the landlord
                        a.Owned = true;
                        a.State = SupplyState.Temporary;
                        a.TemporaryUntil = today + UtilitiesCatalogue.TemporaryDays;
                        if (s == 0)
                        {
                            newOwned++;
                        }
                    }
                    else if (!place.Owned && a.Owned)
                    {
                        a.Owned = false;   // now rented: the landlord's account, supplied
                        if (a.State == SupplyState.Temporary || a.State == SupplyState.Cut && a.Owed == 0)
                        {
                            a.State = SupplyState.Active;
                            a.TemporaryUntil = 0;
                        }
                    }
                }
            }
            CloseGivenUp(today);
            if (newOwned > 0)
            {
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(fresh ? "reallife_util_toast_first_owned" : "reallife_util_toast_bought",
                    "count", newOwned.ToString(), "days", UtilitiesCatalogue.TemporaryDays.ToString(), "office", OfficeName()), 12f);
            }
        }

        private bool Holds(string place)
        {
            for (int i = 0; i < _places.Count; i++)
            {
                if (string.Equals(_places[i].Place, place, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // a place no longer the player's: its last bill now, its deposit set against it, the rest given back or owed
        private void Close(Account a, int today)
        {
            UtilitiesRules.Bill(a, today, GameCalendar.MonthDays);
            a.State = SupplyState.Closed;
            a.TemporaryUntil = 0;
            int set = Math.Min(a.Deposit, a.Owed);
            a.Owed -= set;
            int back = a.Deposit - set;
            a.Deposit = 0;
            if (a.Owed <= 0)
            {
                a.DueDay = 0;
            }
            if (back > 0)
            {
                UtilitiesGame.Give(back, "reallife_transaction_util_deposit_back", null);
            }
            Logger?.Info("Utilities: " + a.Service + " at " + a.Place + " closed: " + (a.Owed > 0 ? a.Owed + " still owed." : "settled") + (back > 0 ? ", " + back + " of the deposit back." : "."));
        }

        public PlaceInfo? PlaceOf(Account a)
        {
            for (int i = 0; i < _places.Count; i++)
            {
                if (string.Equals(_places[i].Place, a.Place, StringComparison.Ordinal))
                {
                    return _places[i];
                }
            }
            return null;
        }

        private void Day(int today)
        {
            float started = Time.realtimeSinceStartup;
            int monthDays = GameCalendar.MonthDays;
            int level = Prices.Level;
            Sync(today, false);
            _company.Morning(today);
            int from = Math.Max(_state.LastDay + 1, today - CatchUpDays);
            int bills = 0, billed = 0;
            for (int d = from; d < today; d++)
            {
                int season = CalendarRules.Season(d, GameCalendar.DaysPerYear);
                _index.Build(_state);   // the day's use changes no account's place or service
                for (int p = 0; p < _places.Count; p++)
                {
                    PlaceInfo place = _places[p];
                    bool open = place.Kind == PlaceKind.Home || UtilitiesGame.OpenOn(place.Reg, d);
                    int people = UtilitiesGame.People(place);
                    for (int s = 0; s < UtilitiesCatalogue.Services; s++)
                    {
                        var service = (Service)s;
                        Account a = _index.Find(_state, place.Place, service);
                        if (a != null)
                        {
                            UtilitiesRules.AddDay(a, UtilitiesRules.Use(service, place.Kind, place.Sqm, people, open, season), level, d);
                        }
                    }
                }
                for (int i = 0; i < _state.Accounts.Count; i++)
                {
                    Account a = _state.Accounts[i];
                    if (a.Service == Service.Internet)
                    {
                        InternetRules.AddDay(a, monthDays, a.OutageUntil > a.OutageFrom && a.OutageFrom / 24 == d);   // a day the provider failed is not charged
                    }
                }
                _company.Day(d, level, monthDays);
                if (d + 1 - _state.MonthStart >= monthDays)
                {
                    NewMonth(d + 1, level);
                    for (int i = 0; i < _state.Accounts.Count; i++)
                    {
                        int bill = UtilitiesRules.Bill(_state.Accounts[i], d + 1, monthDays);
                        if (bill > 0)
                        {
                            bills++;
                            billed += bill;
                        }
                    }
                    _state.MonthStart = d + 1;
                }
            }
            _state.LastDay = today - 1;
            if (bills > 0)
            {
                UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_toast_bills", "count", bills.ToString(), "amount", UtilitiesText.Money(billed),
                    "day", UtilitiesText.Day(today + UtilitiesRules.DueIn(monthDays))), 10f);
            }
            Turns(today);
            Dues(today);
            Outages(today);
            if (InternetRules.EndGrace(_state, today))
            {
                UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_grace_over"), 14f);
                UtilitiesBridges.Record?.Invoke("reallife_net_toast_grace_over", new Dictionary<string, string>(0));
                Logger?.Info("Utilities: the internet's grace period ended on day " + today + ".");
            }
            Unconnected();
            Effects();
            _view?.Changed();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Utilities check (day " + today + "): " + _state.Accounts.Count + " account(s), " + bills + " bill(s) of " + billed + "; the month began on day "
                             + _state.MonthStart + "; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS.");
            }
        }

        public string NameOf(Account a)
        {
            PlaceInfo? place = PlaceOf(a);
            return place.HasValue ? place.Value.Name : UtilitiesText.T("reallife_util_place_gone");
        }

        public string OfficeName()
        {
            return _office != null && _office.Searched && _office.Building != null ? _office.Name : UtilitiesText.T("reallife_util_office_unknown");
        }

        private void Hour()
        {
            if (!_loaded)
            {
                return;
            }
            Refresh();   // a place given up since the morning closes now, never closed or billed again
            Effects();
        }

        private void Effects()
        {
            int today = UtilitiesGame.Today;
            int hour = today * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0);
            bool homeDark = false, homeDry = false;
            BuildingRegistration inHome = HomeIn();
            _index.Build(_state);
            for (int p = 0; p < _places.Count; p++)
            {
                PlaceInfo place = _places[p];
                if (!GoingPublic.Mine(place.Reg))
                {
                    continue;   // no longer the player's since the places were read: never closed by the utilities
                }
                Account power = _index.Find(_state, place.Place, Service.Power), water = _index.Find(_state, place.Place, Service.Water);
                bool dark = power != null && !UtilitiesRules.Supplied(power, today);
                bool dry = water != null && !UtilitiesRules.Supplied(water, today);
                if (place.Kind == PlaceKind.Home)
                {
                    if (inHome != null && ReferenceEquals(inHome, place.Reg))
                    {
                        homeDark |= dark;
                        homeDry |= dry;
                    }
                    continue;
                }
                bool needsWater = place.Kind == PlaceKind.FoodAndDrink || place.Kind == PlaceKind.Salon || place.Kind == PlaceKind.Gym;
                bool offline = NeedsNet(place) && !Online(_index.Find(_state, place.Place, Service.Internet), hour) && !InternetRules.InGrace(_state, place.Place, today);
                if (dark || (dry && needsWater) || offline)
                {
                    Func<BuildingRegistration, int, bool> close = UtilitiesBridges.CloseFor;
                    if (close != null)
                    {
                        // closed, or kept closed longer while the want lasts; told only when it is the mod that keeps it
                        // closed (one the player closed by hand is theirs to open)
                        if (close(place.Reg, ClosedHours))
                        {
                            TellClosed(place, dark ? power : dry && needsWater ? water : _state.Find(place.Place, Service.Internet), !dark && !(dry && needsWater), today, hour);
                        }
                    }
                }
                else
                {
                    _toldClosed.Remove(place.Place);   // open again: a later closure is told afresh
                    UtilitiesBridges.Release?.Invoke(place.Reg);   // it has what it lacked: the utilities' closure ends now, not on its hour
                }
            }
            if (homeDark)
            {
                ModState.AddModifier(ModState.NoPower, ClosedHours);
            }
            if (homeDry)
            {
                ModState.AddModifier(ModState.NoWater, ClosedHours);
            }
        }

        // why a business is closed and what opens it, the first time for that reason: a notice, a Business desk message, a log line
        private void TellClosed(PlaceInfo place, Account account, bool internet, int today, int hour)
        {
            string key;
            if (internet)
            {
                key = account == null || account.State == SupplyState.Closed || account.State == SupplyState.Cut ? "reallife_util_closed_net_none"
                    : account.State == SupplyState.Pending ? "reallife_util_closed_net_install"
                    : "reallife_util_closed_net_outage";
            }
            else
            {
                key = account == null ? "reallife_util_closed_apply"
                    : account.State == SupplyState.Cut ? (account.Owed > 0 ? "reallife_util_closed_cut" : "reallife_util_closed_apply")
                    : account.State == SupplyState.Pending ? "reallife_util_closed_connecting"
                    : "reallife_util_closed_apply";
            }
            if (_toldClosed.TryGetValue(place.Place, out string said) && said == key)
            {
                return;
            }
            _toldClosed[place.Place] = key;
            var data = new Dictionary<string, string>(4)
            {
                { "place", place.Name },
                { "service", (account != null ? UtilitiesText.Service(account.Service) : UtilitiesText.Service(Service.Power)).ToLowerInvariant() },   // mid-sentence
                { "office", OfficeName() },
                { "time", account != null && account.OutageUntil > hour ? (account.OutageUntil % 24).ToString("00") + ":00" : string.Empty }
            };
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(key, "place", data["place"], "service", data["service"], "office", data["office"], "time", data["time"]), 14f);
            UtilitiesBridges.Record?.Invoke(key, data);
            Logger?.Warn("Utilities: " + place.Name + " is closed (" + key + ", told), on day " + today + ".");   // a warning: logged without Developer diagnostics
        }

        private static BuildingRegistration HomeIn()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return null;
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && GoingPublic.Mine(manager.buildingRegistration) ? manager.buildingRegistration : null;
        }

        private void Second()
        {
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            _office?.Tick();
            OfficePanel();
        }

        private void OfficePanel()
        {
            PlayerController player = PlayerHelper.PlayerController;
            bool can = player != null && _office != null && _office.Usable && !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle
                       && !ThirdPersonCamera.IsUiOpen() && !JobDirector.TakesInteractKey && !Careers.CareersDirector.TakesInteractKey && !_window.IsOpen;
            Vector3 at = player != null ? player.transform.position : Vector3.zero;
            if (!can || !_office.InReach(at))
            {
                _door.Hide("the player left the desk");
                return;
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(UtilitiesText.T("reallife_util_panel_title"), UtilitiesText.T("reallife_util_panel_hours"), string.Empty, new Color32(255, 255, 255, 255));
            panel.Button(0, "reallife_util_panel_button", _onDesk, true, true);
            _door.Show(_office.Front, at, 1.4f, () => "the desk at " + _office.Name);
        }

        private void DeskClicked()
        {
            if (!_loaded)
            {
                return;
            }
            _door.Hide("the desk was spoken to");
            Refresh();
            if (_desk == null || !_desk.Open())
            {
                UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_desk_fallback"), 8f);
            }
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new UtilitiesApp(this);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_util_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(250, 205, 90, 255), new Color32(40, 120, 150, 255), 0.42f, "AppIcon-Utilities"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Utilities: the game's phone is not built as this version expects, so the Utilities app is left off."
                                 + " Bills are still billed and paid by auto debit; the office's desk takes payments.");
                }
                return;
            }
            _view = view;
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        /// <summary>Opens the Utilities app on its internet page (Newsstand needs a connection); false if the app is not on the phone.</summary>
        public bool OpenInternet()
        {
            if (!_loaded || _app == null || _view == null || !_app.Installed)
            {
                return false;
            }
            _view.Want(UtilitiesApp.InternetTab);
            _app.Open();
            return true;
        }

        /// <summary>The app is opening: the places are read again so it shows today's.</summary>
        public bool AppOpened()
        {
            if (!_loaded)
            {
                return false;
            }
            Refresh();
            return true;
        }
    }
}
