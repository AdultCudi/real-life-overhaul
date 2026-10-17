using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The cleaning service and selling a business, run by the credit side's second, hour and day (the last two before the
    /// game's hourly cleaning). The one thing it puts into the game that must come out again is a visiting cleaner's shift.
    /// Cost: nothing per frame; once a second a few comparisons, and a walk over today's visits while the player is indoors.
    /// </summary>
    public sealed partial class ServicesDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeServices";
        public const int TabCleaning = 0, TabCrew = 1, TabContracts = 2, TabSale = 3;

        private static readonly string[] AppGlyphs = { "Icon-PersonManage", "Icon-Shop", "Icon-Building" };
        private static readonly int HotSecond = HotSpots.Slot("Economy: ServicesDirector.Second");

        public static IModLogger Logger;
        public static ServicesDirector Instance { get; private set; }

        private ServicesState _state;
        private bool _running;
        private bool _broken;          // a fault outside the two parts: off for the session
        private bool _cleaningBroken, _saleBroken;
        private bool _gaveUp;          // the game's phone is not built as expected: no app, the rest runs
        private int _parts = -1;       // which of the two parts were on at the last look, a bit each
        private ServicesApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;

        public bool Running => _running && !_broken;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public ServicesState State => _running ? _state : null;

        public bool CleaningOn => _running && !_broken && !_cleaningBroken && Settings.EconomyCleaning;

        /// <summary>Never in a Going Public session: the host keeps who rents what, which this sale would not tell. Listings wait until then.</summary>
        public bool SaleOn => _running && !_broken && !_saleBroken && Settings.EconomySale && !GoingPublic.InSession;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            ServicesGame.Logger = Logger;
            CleaningPresence.Logger = Logger;
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            EconomyDirector.Follow(StartSeats.Services, _second);   // its first second after a load gets a frame of its own (Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;   // another city's director has started since: the state is its
            }
            // The city is unloading: nothing is written to the save here, only the mod's shift taken out.
            try
            {
                StandDown("the city is unloading", false, false, true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _running = false;
            }
            Instance = null;
        }

        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);
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
                Fault("the timer", e);
            }
        }

        private void Slow()
        {
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            EconomyDirector credit = EconomyDirector.Instance;
            bool wanted = Settings.EconomyCleaning || Settings.EconomySale;
            if (!Settings.Economy || !wanted || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!wanted ? "both options are off" : "the credit side is not running", true, true, false);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            int parts = (CleaningOn ? 1 : 0) | (SaleOn ? 2 : 0);
            if (parts != _parts)
            {
                // An option was switched: what no longer applies is taken back at once.
                bool cleaningWent = (_parts & 1) != 0 && (parts & 1) == 0, cleaningCame = (_parts & 1) == 0 && (parts & 1) != 0;
                _parts = parts;
                if (cleaningWent)
                {
                    CleaningStop();
                }
                else if (cleaningCame)
                {
                    CleaningStart(true);   // switched on mid-play: nothing is measured across the time it was off
                }
                Changed();
                if (Verbose)
                {
                    Logger?.Info("Services check (options): the cleaning service " + (CleaningOn ? "on" : "off") + ", selling a business " + (SaleOn ? "on" : "off")
                                 + (cleaningWent ? "; the round is dropped and no shift of the mod's is in any schedule" : string.Empty) + ": PASS");
                }
            }
            if (CleaningOn)
            {
                PresenceLook();
            }
        }

        private void StartUp()
        {
            GameInstance save = Save;
            int today = save.Day;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            ServicesState state = ServicesStore.Load(save.modData, out int version);
            bool fresh = state == null;
            if (fresh)
            {
                state = new ServicesState { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = today, LastOfferDay = today };
            }
            bool skipped = !fresh && state.LastDay != today;
            state.LastDay = today;   // days that passed while this was off are not counted
            _state = state;
            _running = true;
            _parts = (CleaningOn ? 1 : 0) | (SaleOn ? 2 : 0);
            if (CleaningOn)
            {
                CleaningStart(skipped);
            }
            Logger?.Info("Services: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". Cleaning service " + (CleaningOn ? "on" : "off") + ": "
                         + _state.Crew.Count + " on the crew, " + _state.Clients.Count + " business(es) on the round, " + ContractRules.Running(_state) + " of them under contract, "
                         + _state.Offers.Count + " contract offer(s) open. Selling a business " + (SaleOn ? "on" : "off") + ": " + _state.Listings.Count + " on the market, "
                         + _state.SaleOffers.Count + " offer(s), " + _state.Dues.Count + " price(s) still being paid, " + _state.Clauses.Count + " clause(s).");
        }

        // The feature stops and the mod's shift leaves the schedule; with 'keep' the state is first written to the save's data.
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (keep && _running && Save != null)
            {
                Write(Save);
            }
            bool shift = CleaningPresence.Showing;
            if (unloading)
            {
                CleaningPresence.Forget();
            }
            else
            {
                CleaningPresence.Clear(true);
            }
            _running = false;
            _parts = -1;
            RoundDrop();
            if (!unloading)
            {
                RemoveApp();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Services check (restored, " + why + "): " + (shift ? "the visiting cleaner's shift was taken out of the game's schedule" : "no shift of the mod's was in any schedule")
                             + "; no wage, no employee and no schedule of the player's was ever written: PASS");
            }
        }

        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
        }

        // A fault outside the two parts: the mod's shift out of the schedule, and nothing more of the feature this session.
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown("a fault", false, false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
            }
            Logger?.Warn("Services: switched off for this session after an error in " + where
                         + ". No shift of the mod's is in any schedule; the game's daily run and the rest of the economy are not affected.");
        }

        private void CleaningFault(string where, Exception e)
        {
            _cleaningBroken = true;
            Logger?.Error(e);
            try
            {
                CleaningStop();
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            _parts = (CleaningOn ? 1 : 0) | (SaleOn ? 2 : 0);
            Logger?.Warn("Services: the cleaning service is switched off for this session after an error in " + where
                         + ". No shift of the mod's is in any schedule; nothing more is cleaned or charged by it. Selling a business is not affected.");
        }

        private void SaleFault(string where, Exception e)
        {
            _saleBroken = true;
            _parts = (CleaningOn ? 1 : 0) | (SaleOn ? 2 : 0);
            Logger?.Error(e);
            Logger?.Warn("Services: selling a business is switched off for this session after an error in " + where
                         + ". Nothing more is offered, paid or charged by it. The cleaning service is not affected.");
        }

        private void Install()
        {
            var view = new ServicesApp(this);
            GameSprites.Want(AppGlyphs);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_svc_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(96, 196, 178, 255), new Color32(34, 122, 128, 255), 0.42f, "AppIcon-Services"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Services: the game's phone is not built as this version expects, so the Services app is left off. "
                                 + "A round that is set up still runs, contracts still pay, and notifications still say what happens.");
                }
                return;
            }
            _view = view;
            Badge();
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

        // The game's hour event (every game hour, and first thing in its NewDay), after the credit side's work on it.
        private void OnHour()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                if (Save.Day > _state.LastDay)
                {
                    CleaningPresence.Clear(false);
                    return;   // the day event follows, still before the hourly cleaning, and does this hour with the new round
                }
                if (CleaningOn)
                {
                    try
                    {
                        CleaningHour(Save.Hour, false);
                    }
                    catch (Exception e)
                    {
                        CleaningFault("the hour", e);
                    }
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        // The game's day event (after its daily run), after the credit side has settled its day.
        private void OnDay()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day <= _state.LastDay)
                {
                    return;
                }
                float started = Time.realtimeSinceStartup;
                bool next = day == _state.LastDay + 1;   // a jump of many days is one pass, for today
                _state.LastDay = day;
                if (CleaningOn)
                {
                    try
                    {
                        CleaningDay(day, next);
                        CleaningHour(Save.Hour, true);
                    }
                    catch (Exception e)
                    {
                        CleaningFault("the day", e);
                    }
                }
                float cleaned = Time.realtimeSinceStartup;
                if (SaleOn)
                {
                    try
                    {
                        SaleDay(day);
                    }
                    catch (Exception e)
                    {
                        SaleFault("the day", e);
                    }
                }
                Changed();
                if (Verbose)
                {
                    float done = Time.realtimeSinceStartup;
                    Logger?.Info("Services check (day " + day + "): " + _state.Crew.Count + " on the crew, " + _state.Clients.Count + " on the round (" + ContractRules.Running(_state)
                                 + " under contract), " + _visits.Count + " visit(s) planned, " + _state.Listings.Count + " business(es) on the market with " + _state.SaleOffers.Count
                                 + " offer(s); the cleaning service " + ((cleaned - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, selling "
                                 + ((done - cleaned) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // Before a save: the mod's shift leaves the schedule, which is saved (the next second puts it back), and the state is written.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null)
                {
                    return;
                }
                bool shift = CleaningPresence.Showing;
                CleaningPresence.Clear(false);
                Write(Save);
                if (Verbose)
                {
                    Logger?.Info("Services check (before a save): "
                                 + (shift ? "the visiting cleaner's shift taken out of the schedule until the save is written" : "no shift of the mod's in any schedule")
                                 + "; the state written under its own key: PASS");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[ServicesStore.Key] = ServicesStore.Write(_state);
        }

        private bool Charge(int amount, string key, string name, Address address, int? day, bool deductible, bool force, string how)
        {
            bool taken = ServicesGame.Take(amount, key, name, address, day, deductible, force);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Services check (transaction, " + key + ", " + name + "): " + amount
                             + (taken ? " taken" + (deductible ? ", deductible" : string.Empty) : " could not be taken: the cash does not cover it") + "; " + how + ": PASS");
            }
            return taken;
        }

        private void Pay(int amount, string key, string name, Address address, int? day, string how)
        {
            ServicesGame.Give(amount, key, name, address, day);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Services check (transaction, " + key + ", " + name + "): " + amount + " paid to the player; " + how + ": PASS");
            }
        }

        // A notification of the game's that opens the app on a tab, with something picked.
        private void Tell(NotificationType type, string key, int tab, string pick, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs.Length >= 2)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // a dictionary each: the game keeps a notification's data while it lists it
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 11f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        private void Open(int tab, string pick)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>For another app's button: the cleaning service's page. Does nothing while it is not running.</summary>
        public static void OpenCleaning()
        {
            Instance?.Open(TabCleaning, null);
        }

        /// <summary>For another app's button: a business's page for selling it, by its place.</summary>
        public static void OpenSale(string place)
        {
            Instance?.Open(TabSale, string.IsNullOrEmpty(place) ? null : "sell:" + place);
        }

        /// <summary>True while selling a business runs: another app shows its button for it only then.</summary>
        public static bool CanSell => Instance != null && Instance.SaleOn && Instance._app != null;

        public static bool CanClean => Instance != null && Instance.CleaningOn && Instance._app != null;

        /// <summary>The app has been opened: the crew and the clients are read again, so the page shows the floors as they are.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                if (CleaningOn)
                {
                    ReadAll();
                }
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        private void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            Badge();
            _view?.Changed();
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : Waiting());
            }
        }

        /// <summary>How many things wait for the player's answer: contract offers and offers for a business.</summary>
        public int Waiting()
        {
            if (!_running)
            {
                return 0;
            }
            return (CleaningOn ? _state.Offers.Count : 0) + (SaleOn ? _state.SaleOffers.Count : 0);
        }

        /// <summary>For the save tools: no shift of the mod's stays in any schedule; the timer starts the feature again if an option is on.</summary>
        public void StopAll()
        {
            try
            {
                StandDown("the save tools asked", true, true, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            try
            {
                StandDown("the save's line was removed", false, false, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
            _state = null;
        }
    }
}
