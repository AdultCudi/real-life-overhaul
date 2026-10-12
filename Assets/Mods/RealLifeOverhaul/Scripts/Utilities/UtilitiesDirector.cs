using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Buildings;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The city's power and water for every place the player rents or owns (Scripts/Utilities):
    ///
    ///   accounts   a rented place has the landlord's, supplied at once: the player just pays. A bought place is
    ///              supplied for three days at half as much again (the temporary supply) and then cut, unless an
    ///              account is applied for in person at the utilities office, which connects it the next morning
    ///              (a deposit on a poor credit rating). A place the player gives up has its last bill.
    ///   the books  each morning yesterday's use is added to the month's (UtilitiesRules: by floor, kind, people and
    ///              season, at that day's prices); at the turn of the month each account is billed, due in half a
    ///              month. Auto debit (the Utilities app) pays on the due day. A bill a day overdue has the late fee
    ///              added; three days after its due day the supply is cut, and reconnecting costs a fee.
    ///   a cut      a business without power closes, as one without water does where it needs water (food and
    ///              drink, salons, gyms); a home without power has no screens or cooking, without water no shower
    ///              or sink (Home/HomeHealth, through Wiring/UtilityLinks), and either weighs on the player's mood.
    ///
    /// The office stands a sign at its door; the desk is the game's own list (DeskList, UtilitiesDesk). The Utilities
    /// app (UtilitiesApp) shows every account, its bills and auto debit. Nothing runs per frame but a few checks: the
    /// day's books once a morning, the effects once an hour, the office and its panel once a second.
    /// </summary>
    public sealed class UtilitiesDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeOverhaulUtilities";
        private static readonly string[] AppGlyphs = { "Icon-Electricity", "Icon-Energy", "Icon-Lightbulb", "Icon-Building" };
        private const int CatchUpDays = 7;          // a long jump (a stay in hospital, a trip) is added up day by day for at most a week
        private const int ClosedHours = 2;          // a business without what it needs is kept closed this long, hour by hour

        public static IModLogger Logger;
        public static UtilitiesDirector Instance { get; private set; }

        /// <summary>True while the office's panel is up, or its button was pressed this frame.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door != null && Instance._door.TakesKey;

        private UtilitiesState _state;
        private GameInstance _save;
        private bool _loaded, _failed, _gaveUp;
        private float _nextSecond, _nextHook;
        private int _hour = -1;
        private Action _onSave;
        private Action _onDesk;
        private readonly List<PlaceInfo> _places = new List<PlaceInfo>(16);
        private readonly StringBuilder _text = new StringBuilder(256);
        private UtilitiesOffice _office;
        private DoorPanel _door;
        private DeskList _window;
        private UtilitiesDesk _desk;
        private PhoneApp _app;
        private UtilitiesApp _view;

        /// <summary>The accounts while a save is running; null otherwise.</summary>
        public UtilitiesState State => _loaded ? _state : null;

        /// <summary>The player's places as last read (each morning, when the app opens, at the desk).</summary>
        public IReadOnlyList<PlaceInfo> Places => _places;

        public UtilitiesOffice Office => _office;
        public DeskList Window => _window;

        /// <summary>True if the player holds a power account of their own that is supplied (Life: the electricity company's events).</summary>
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

        /// <summary>True if a place has a service now; true for a place the utilities do not know (nothing to cut).</summary>
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

        /// <summary>What a place's power and water come to a day now (money): today's use as supplied, at today's prices; nothing for a place not supplied.</summary>
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
            _onSave = OnSaveGame;
            _door = new DoorPanel(Logger, "Utilities check (office panel)");
            _window = new DeskList("RealLifeOverhaul Utilities Desk", 1100f, 120f);
            _onDesk = _door.Press(DeskClicked);
        }

        private void OnDestroy()
        {
            Stop("the mod is unloading");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- running

        private void Update()
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
            if (fresh)
            {
                // The feature has just appeared in this save: today's use is the first added up, from a month that starts today.
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
            Logger?.Info("Utilities: " + (fresh ? "started in this save" : "read from the save (line v" + version + ")") + ": " + _state.Accounts.Count + " account(s) for "
                         + _places.Count + " place(s); the month began on day " + _state.MonthStart + ".");
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
            _places.Clear();
            Logger?.Info("Utilities: stopped (" + why + ").");
        }

        private void Fault(string where, Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Utilities: switched off for this session after an error in " + where + ". Nothing more is billed or cut; every place keeps its supply.");
            try
            {
                Stop("an error");
            }
            catch (Exception)
            {
                // (stopping is best effort after a fault)
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

        // ---------------------------------------------------------------- the places and their accounts

        /// <summary>
        /// The accounts brought up to the player's places today: a new place gets one (a rented place the landlord's, a
        /// bought one the temporary supply), a place bought after being rented needs an account of its own, and a place
        /// no longer the player's has its last bill (its deposit set against it).
        /// </summary>
        public void Sync(int today, bool fresh)
        {
            UtilitiesGame.Places(_places);
            int newOwned = 0;
            for (int p = 0; p < _places.Count; p++)
            {
                PlaceInfo place = _places[p];
                for (int s = 0; s < UtilitiesCatalogue.Services; s++)
                {
                    var service = (Service)s;
                    Account a = _state.Find(place.Place, service);
                    if (a == null || a.State == SupplyState.Closed)
                    {
                        if (a != null)
                        {
                            _state.Accounts.Remove(a);   // (a place given up and taken again starts afresh; what was owed on it was settled when it closed)
                        }
                        a = place.Owned ? UtilitiesRules.Temporary(place.Place, service, place.Kind, today) : UtilitiesRules.Rented(place.Place, service, place.Kind, today);
                        _state.Accounts.Add(a);
                        if (place.Owned && s == 0)
                        {
                            newOwned++;
                        }
                        continue;
                    }
                    a.Kind = place.Kind;
                    if (place.Owned && !a.Owned && a.State == SupplyState.Active)
                    {
                        // Bought after being rented: the landlord's account goes with the landlord.
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
            for (int i = _state.Accounts.Count - 1; i >= 0; i--)
            {
                Account a = _state.Accounts[i];
                if (a.State != SupplyState.Closed && !Holds(a.Place))
                {
                    Close(a, today);
                }
                if (a.State == SupplyState.Closed && a.Owed <= 0 && a.Accrued <= 0)
                {
                    _state.Accounts.RemoveAt(i);
                }
            }
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

        // A place no longer the player's: its last bill now, its deposit set against it, and the rest given back or owed.
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

        // ---------------------------------------------------------------- the day

        private void Day(int today)
        {
            float started = Time.realtimeSinceStartup;
            int monthDays = GameCalendar.MonthDays;
            int level = Prices.Level;
            Sync(today, false);
            int from = Math.Max(_state.LastDay + 1, today - CatchUpDays);
            int bills = 0, billed = 0;
            for (int d = from; d < today; d++)
            {
                int season = CalendarRules.Season(d, GameCalendar.DaysPerYear);
                for (int p = 0; p < _places.Count; p++)
                {
                    PlaceInfo place = _places[p];
                    bool open = place.Kind == PlaceKind.Home || UtilitiesGame.OpenOn(place.Reg, d);
                    int people = UtilitiesGame.People(place);
                    for (int s = 0; s < UtilitiesCatalogue.Services; s++)
                    {
                        var service = (Service)s;
                        Account a = _state.Find(place.Place, service);
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
                        InternetRules.AddDay(a, monthDays, a.OutageUntil > a.OutageFrom && a.OutageFrom / 24 == d);   // (a day the provider failed is not charged)
                    }
                }
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
            Unconnected();
            Effects();
            _view?.Changed();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Utilities check (day " + today + "): " + _state.Accounts.Count + " account(s), " + bills + " bill(s) of " + billed + "; the month began on day "
                             + _state.MonthStart + "; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: PASS.");
            }
        }

        // The providers' month: each price moves (InternetRules.NextPromo), and a plan out of contract is priced again.
        private void NewMonth(int day, int level)
        {
            for (int p = 0; p < InternetRules.Count && p < _state.Promo.Length; p++)
            {
                _state.Promo[p] = InternetRules.NextPromo(_state.Seed, _state.Month, p, _state.Promo[p]);
            }
            _state.Month++;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Service == Service.Internet && a.State != SupplyState.Closed && a.ContractUntil <= day && a.Provider >= 0)
                {
                    a.PriceCents = InternetRules.MonthCents(a.Provider, a.Plan, a.BusinessPlan, false, level, _state.Promo[a.Provider]);
                }
            }
        }

        // Today's outages, drawn from the save's seed: each connection of a provider that fails is down for some hours.
        private void Outages(int today)
        {
            for (int p = 0; p < InternetRules.Count; p++)
            {
                if (!InternetRules.Outage(_state.Seed, today, p, false, out int from, out int hours))
                {
                    continue;
                }
                InternetRules.Outage(_state.Seed, today, p, true, out _, out int repaired);
                int affected = 0;
                for (int i = 0; i < _state.Accounts.Count; i++)
                {
                    Account a = _state.Accounts[i];
                    if (a.Service == Service.Internet && a.Provider == p && a.State == SupplyState.Active)
                    {
                        a.OutageFrom = today * 24 + from;
                        a.OutageUntil = a.OutageFrom + (a.BusinessPlan ? repaired : hours);
                        affected++;
                    }
                }
                if (affected > 0)
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_outage", "provider", UtilitiesText.T(InternetRules.Get(p).NameKey),
                        "hour", UtilitiesText.Hour(from), "hours", hours.ToString()), 12f);
                }
            }
        }

        // A business that cannot open without a connection, and has none: said once a morning.
        private void Unconnected()
        {
            for (int p = 0; p < _places.Count; p++)
            {
                PlaceInfo place = _places[p];
                if (NeedsNet(place) && !Online(place, UtilitiesGame.Today * 24 + 12))
                {
                    Account a = _state.Find(place.Place, Service.Internet);
                    if (a == null || a.State == SupplyState.Closed || a.State == SupplyState.Cut)
                    {
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_net_toast_needed", "place", place.Name), 10f);
                    }
                }
            }
        }

        /// <summary>True for a business that cannot open without a connection (web design, software, the headquarters).</summary>
        public static bool NeedsNet(PlaceInfo place)
        {
            return place.Kind != PlaceKind.Home && place.Reg != null && Array.IndexOf(InternetCatalogue.Needs, place.Reg.businessTypeName) >= 0;
        }

        /// <summary>True if a place has a connection that works at an hour (hours from the save's first day).</summary>
        public bool Online(PlaceInfo place, int hour)
        {
            Account a = _state.Find(place.Place, Service.Internet);
            return a != null && a.State == SupplyState.Active && !InternetRules.InOutage(a, hour);
        }

        // Accounts applied for connect; a temporary supply that has run out is cut.
        private void Turns(int today)
        {
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                SupplyState before = a.State;
                if (!UtilitiesRules.Turn(a, today))
                {
                    continue;
                }
                string where = NameOf(a);
                if (before == SupplyState.Pending && a.State == SupplyState.Active)
                {
                    UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_toast_connected", "service", UtilitiesText.Service(a.Service), "place", where), 8f);
                }
                else if (a.State == SupplyState.Cut)
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_toast_temporary_over", "service", UtilitiesText.Service(a.Service), "place", where,
                        "office", OfficeName()), 12f);
                }
            }
        }

        // The bills that fall due: auto debit pays them; one a day overdue has the late fee added, and is cut three days after.
        private void Dues(int today)
        {
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Owed > 0 && a.AutoDebit && a.DueDay > 0 && today >= a.DueDay)
                {
                    if (!Pay(a, today, true))
                    {
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_toast_debit_failed", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                            "amount", UtilitiesText.Money(a.Owed)), 10f);
                    }
                }
                switch (UtilitiesRules.Due(a, today))
                {
                    case DueStep.ChargeLate:
                        int fee = UtilitiesCatalogue.LateFee;
                        a.Owed = (int)Math.Min((long)a.Owed + fee, int.MaxValue / 4);
                        a.LateCharged = true;
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_toast_late", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                            "fee", UtilitiesText.Money(fee), "day", UtilitiesText.Day(a.DueDay + UtilitiesCatalogue.CutAfterDays)), 12f);
                        break;
                    case DueStep.Cut:
                        UtilitiesRules.Cut(a, today, true);
                        UtilitiesText.Toast(NotificationType.Error, UtilitiesText.T("reallife_util_toast_cut", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                            "office", OfficeName()), 14f);
                        break;
                }
            }
        }

        /// <summary>What is owed on an account paid now. False if there was not the cash (the game says so when not quiet).</summary>
        public bool Pay(Account a, int today, bool quiet = false)
        {
            if (a == null || a.Owed <= 0)
            {
                return true;
            }
            PlaceInfo? place = PlaceOf(a);
            string key = a.Service == Service.Power ? "reallife_transaction_util_power" : a.Service == Service.Water ? "reallife_transaction_util_water" : "reallife_transaction_util_net_bill";
            if (!UtilitiesGame.Pay(a.Owed, key, place?.Reg))
            {
                return false;
            }
            int paid = a.Owed;
            int back = UtilitiesRules.Paid(a, today);
            if (back > 0)
            {
                UtilitiesGame.Give(back, "reallife_transaction_util_deposit_back", place?.Reg);
            }
            if (!quiet)
            {
                UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_toast_paid", "service", UtilitiesText.Service(a.Service), "place", NameOf(a),
                    "amount", UtilitiesText.Money(paid)), 8f);
            }
            _view?.Changed();
            return true;
        }

        /// <summary>What an application costs today: the deposit by the credit grade, and the reconnection fee after a cut for debt.</summary>
        public int ApplicationCost(Account a, out int deposit, out int fee)
        {
            deposit = fee = 0;
            if (a == null)
            {
                return 0;
            }
            PlaceInfo? place = PlaceOf(a);
            if (a.Owned && place.HasValue)
            {
                int season = CalendarRules.Season(UtilitiesGame.Today, GameCalendar.DaysPerYear);
                long use = UtilitiesRules.Use(a.Service, place.Value.Kind, place.Value.Sqm, UtilitiesGame.People(place.Value), true, season);
                int month = UtilitiesRules.Month(UtilitiesRules.DayCents(a.Service, use, Prices.Level, false), GameCalendar.MonthDays);
                deposit = UtilitiesRules.Deposit(UtilitiesBridges.GradeNow, month);
            }
            fee = a.CutForDebt ? UtilitiesCatalogue.ReconnectFee : 0;
            return deposit + fee;
        }

        /// <summary>An account applied for in person: what it costs is paid, and it connects the next morning. False with a reason key if it cannot be.</summary>
        public bool Apply(Account a, out string why)
        {
            why = null;
            int today = UtilitiesGame.Today;
            if (a == null || a.Owed > 0)
            {
                why = "reallife_util_desk_owed_first";
                return false;
            }
            int cost = ApplicationCost(a, out int deposit, out int fee);
            PlaceInfo? place = PlaceOf(a);
            if (cost > 0 && !UtilitiesGame.Pay(cost, fee > 0 && deposit == 0 ? "reallife_transaction_util_reconnect" : "reallife_transaction_util_deposit", place?.Reg))
            {
                why = "reallife_util_desk_no_cash";
                return false;
            }
            bool reconnect = a.CutForDebt;
            if (!UtilitiesRules.Apply(a, today))
            {
                why = "reallife_util_desk_not_now";
                return false;
            }
            a.Deposit += deposit;
            Logger?.Info("Utilities: " + a.Service + " applied for at " + a.Place + (reconnect ? " (reconnection)" : string.Empty) + ": deposit " + deposit + ", fee " + fee
                         + "; connects on day " + a.ConnectDay + ".");
            _view?.Changed();
            return true;
        }

        // ---------------------------------------------------------------- the internet, bought online in the app

        /// <summary>What a plan costs a place a month now, cents (a business plan at a business).</summary>
        public int NetPrice(PlaceInfo place, int provider, int plan, bool contract)
        {
            return InternetRules.MonthCents(provider, plan, place.Kind != PlaceKind.Home, contract, Prices.Level, provider >= 0 && provider < _state.Promo.Length ? _state.Promo[provider] : 1000);
        }

        /// <summary>The fee for leaving the place's connection now (a contract left early); nothing without one.</summary>
        public int NetExitFee(PlaceInfo place)
        {
            Account a = _state.Find(place.Place, Service.Internet);
            return a != null && a.State != SupplyState.Closed ? InternetRules.ExitFee(a, UtilitiesGame.Today, GameCalendar.MonthDays) : 0;
        }

        /// <summary>
        /// A plan bought for a place, or the place's plan changed: another provider means leaving the old one (the exit
        /// fee of a contract left early) and an engineer's visit; the same provider changes the plan at once. False with a
        /// reason key if it cannot be.
        /// </summary>
        public bool BuyNet(PlaceInfo place, int provider, int plan, bool contract, out string why)
        {
            why = null;
            int today = UtilitiesGame.Today, monthDays = GameCalendar.MonthDays;
            if (!InternetRules.Reaches(provider, place.Reg != null ? place.Reg.Neighborhood : null))
            {
                why = "reallife_net_no_reach";
                return false;
            }
            Account a = _state.Find(place.Place, Service.Internet);
            if (a != null && a.Owed > 0)
            {
                why = "reallife_net_owed_first";
                return false;
            }
            bool live = a != null && a.State != SupplyState.Closed && a.State != SupplyState.Cut;
            bool same = live && a.Provider == provider;
            int exit = live && !same ? InternetRules.ExitFee(a, today, monthDays) : 0;
            int install = same || contract ? 0 : InternetCatalogue.InstallFee;
            int price = NetPrice(place, provider, plan, contract);
            if (exit + install > UtilitiesGame.Cash)
            {
                why = "reallife_net_no_cash";
                return false;
            }
            if (exit > 0)
            {
                UtilitiesGame.Pay(exit, "reallife_transaction_util_net_exit", place.Reg);
            }
            if (install > 0)
            {
                UtilitiesGame.Pay(install, "reallife_transaction_util_net_install", place.Reg);
            }
            if (a == null)
            {
                a = new Account { Place = place.Place, Kind = place.Kind, AutoDebit = true };
                _state.Accounts.Add(a);
            }
            if (same)
            {
                // The same provider: the plan and its price change today; a contract runs on (or begins, if one is taken now).
                a.Plan = plan;
                a.PriceCents = price;
                if (contract && a.ContractUntil <= today)
                {
                    a.ContractUntil = today + InternetCatalogue.ContractMonths * Math.Max(1, monthDays);
                }
                UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_net_toast_changed", "place", place.Name, "provider", UtilitiesText.T(InternetRules.Get(provider).NameKey),
                    "mbps", InternetRules.Get(provider).Mbps[plan].ToString(), "price", UtilitiesText.Money(price / 100f)), 10f);
            }
            else
            {
                if (a.Accrued > 0)
                {
                    UtilitiesRules.Bill(a, today, monthDays);   // what the old connection came to this month, billed now
                }
                InternetRules.SignUp(a, provider, plan, place.Kind != PlaceKind.Home, contract, today, monthDays, price);
                a.Kind = place.Kind;
                UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_net_toast_bought", "place", place.Name, "provider", UtilitiesText.T(InternetRules.Get(provider).NameKey),
                    "mbps", InternetRules.Get(provider).Mbps[plan].ToString(), "day", UtilitiesText.Day(a.ConnectDay)), 10f);
            }
            Logger?.Info("Utilities: internet at " + place.Place + ": " + InternetRules.Get(provider).Id + " plan " + plan + (contract ? " on contract" : " month by month") + ", " + price
                         + " cents a month" + (exit > 0 ? ", exit fee " + exit : string.Empty) + (install > 0 ? ", installation " + install : string.Empty) + ".");
            _view?.Changed();
            return true;
        }

        /// <summary>A place's connection cancelled: the exit fee of a contract left early, the month so far billed, and it is closed.</summary>
        public bool CancelNet(PlaceInfo place, out string why)
        {
            why = null;
            Account a = _state.Find(place.Place, Service.Internet);
            if (a == null || a.State == SupplyState.Closed)
            {
                return true;
            }
            int today = UtilitiesGame.Today, monthDays = GameCalendar.MonthDays;
            int exit = InternetRules.ExitFee(a, today, monthDays);
            if (exit > 0 && !UtilitiesGame.Pay(exit, "reallife_transaction_util_net_exit", place.Reg))
            {
                why = "reallife_net_no_cash";
                return false;
            }
            UtilitiesRules.Bill(a, today, monthDays);
            a.State = SupplyState.Closed;
            a.ContractUntil = 0;
            a.OutageFrom = a.OutageUntil = 0;
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_net_toast_cancelled", "place", place.Name), 8f);
            _view?.Changed();
            return true;
        }

        /// <summary>A connection cut for an unpaid bill, with nothing owed now, reconnected the next morning.</summary>
        public bool ReconnectNet(PlaceInfo place, out string why)
        {
            why = null;
            Account a = _state.Find(place.Place, Service.Internet);
            if (a == null || a.State != SupplyState.Cut)
            {
                return false;
            }
            if (a.Owed > 0)
            {
                why = "reallife_net_owed_first";
                return false;
            }
            a.State = SupplyState.Pending;
            a.CutForDebt = false;
            a.ConnectDay = UtilitiesGame.Today + 1;
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_net_toast_reconnect", "place", place.Name), 8f);
            _view?.Changed();
            return true;
        }

        /// <summary>Test: every connection in an outage from now for two hours.</summary>
        public void TestOutage()
        {
            if (!Ready("reallife_util_test_outage"))
            {
                return;
            }
            int now = UtilitiesGame.Today * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0), count = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Service == Service.Internet && a.State == SupplyState.Active)
                {
                    a.OutageFrom = now;
                    a.OutageUntil = now + 2;
                    count++;
                }
            }
            Effects();
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_test_outage_done", "count", count.ToString()), 8f);
            _view?.Changed();
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

        // ---------------------------------------------------------------- the hour: what a cut does

        private void Hour()
        {
            if (!_loaded)
            {
                return;
            }
            Effects();
        }

        private void Effects()
        {
            int today = UtilitiesGame.Today;
            int hour = today * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0);
            bool homeDark = false, homeDry = false;
            BuildingRegistration inHome = HomeIn();
            for (int p = 0; p < _places.Count; p++)
            {
                PlaceInfo place = _places[p];
                Account power = _state.Find(place.Place, Service.Power), water = _state.Find(place.Place, Service.Water);
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
                bool offline = NeedsNet(place) && !Online(place, hour);
                if (dark || (dry && needsWater) || offline)
                {
                    Func<BuildingRegistration, int, bool> close = UtilitiesBridges.CloseFor;
                    if (close != null && close(place.Reg, ClosedHours) && Meta.Dev.Verbose)
                    {
                        Logger?.Info("Utilities: " + place.Name + " closed for want of " + (dark ? "power" : dry && needsWater ? "water" : "an internet connection") + ".");
                    }
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

        // The player's home they are in now, or null.
        private static BuildingRegistration HomeIn()
        {
            if (!BuildingManager.IsInsideBuilding)
            {
                return null;
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.IsPlayerOwnedBusiness ? manager.buildingRegistration : null;
        }

        // ---------------------------------------------------------------- the second: the office, its panel, the app

        private void Second()
        {
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            _office?.Tick();
            OfficePanel();
        }

        // The office's panel while the player stands at its desk on foot.
        private void OfficePanel()
        {
            PlayerController player = PlayerHelper.PlayerController;
            bool can = player != null && _office != null && _office.Usable && !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle
                       && !ThirdPersonCamera.IsUiOpen() && !JobDirector.TakesInteractKey && !_window.IsOpen;
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
            UtilitiesGame.Places(_places);
            if (_desk == null || !_desk.Open())
            {
                UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_desk_fallback"), 8f);
            }
        }

        private void Install()
        {
            var view = new UtilitiesApp(this);
            GameSprites.Want(AppGlyphs);
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
                    Logger?.Warn("Utilities: the game's phone is not built as this version expects, so the Utilities app is left off. Bills are still billed and paid by auto debit; the office's desk takes payments.");
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

        /// <summary>The app is opening: the places are read again so that it shows today's.</summary>
        public bool AppOpened()
        {
            if (!_loaded)
            {
                return false;
            }
            UtilitiesGame.Places(_places);
            return true;
        }

        // ---------------------------------------------------------------- tests (the options' buttons)

        /// <summary>Test: the month billed now, as at its turn.</summary>
        public void TestBill()
        {
            if (!Ready("reallife_util_test_bill"))
            {
                return;
            }
            int today = UtilitiesGame.Today, count = 0, total = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                int bill = UtilitiesRules.Bill(_state.Accounts[i], today, GameCalendar.MonthDays);
                if (bill > 0)
                {
                    count++;
                    total += bill;
                }
            }
            _state.MonthStart = today;
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_toast_bills", "count", count.ToString(), "amount", UtilitiesText.Money(total),
                "day", UtilitiesText.Day(today + UtilitiesRules.DueIn(GameCalendar.MonthDays))), 10f);
            _view?.Changed();
        }

        /// <summary>Test: the bills owed fall due today, so that tomorrow is a day late (and the cut follows).</summary>
        public void TestDue()
        {
            if (!Ready("reallife_util_test_due"))
            {
                return;
            }
            int today = UtilitiesGame.Today, count = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Owed > 0)
                {
                    a.DueDay = today;
                    count++;
                }
            }
            UtilitiesText.Toast(NotificationType.Info, UtilitiesText.T("reallife_util_test_due_done", "count", count.ToString()), 8f);
            _view?.Changed();
        }

        /// <summary>Test: the supply at the player's home cut now, as for an unpaid bill.</summary>
        public void TestCutHome()
        {
            if (!Ready("reallife_util_test_cut"))
            {
                return;
            }
            int today = UtilitiesGame.Today, count = 0;
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.Kind == PlaceKind.Home && a.State != SupplyState.Closed)
                {
                    UtilitiesRules.Cut(a, today, true);
                    count++;
                }
            }
            Effects();
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_test_cut_done", "count", count.ToString()), 8f);
            _view?.Changed();
        }

        /// <summary>Test: every account supplied again with nothing owed, as if all had been paid and reconnected.</summary>
        public void TestRestore()
        {
            if (!Ready("reallife_util_test_restore"))
            {
                return;
            }
            for (int i = 0; i < _state.Accounts.Count; i++)
            {
                Account a = _state.Accounts[i];
                if (a.State == SupplyState.Closed)
                {
                    continue;
                }
                a.Owed = 0;
                a.DueDay = 0;
                a.LateCharged = false;
                a.CutForDebt = false;
                a.State = SupplyState.Active;
                a.TemporaryUntil = 0;
            }
            UtilitiesText.Toast(NotificationType.Success, UtilitiesText.T("reallife_util_test_restore_done"), 8f);
            _view?.Changed();
        }

        private bool Ready(string what)
        {
            if (_loaded)
            {
                return true;
            }
            UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_test_not_running"), 6f);
            return false;
        }
    }
}
