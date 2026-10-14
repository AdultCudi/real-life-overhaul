using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The cleaning service (cleaners shared between businesses, and contracts with other businesses) and selling a
    /// business. See the Guide's pages of those names.
    ///
    /// This class keeps both going. It has no Update and no place of its own in the game's events: once a second the
    /// credit side's timer calls <see cref="EachSecond"/> (the save is waited for, the options are looked at, a
    /// visiting cleaner is shown or sent away), and the credit side's own handlers of the game's hour and day events
    /// call <see cref="OnHour"/> and <see cref="OnDay"/> when their own work is done (EconomyDirector.AfterHour,
    /// AfterDay). Both come before the game's own hourly cleaning in the same tick (GameManager.RunMainGameTick), which
    /// is what lets a visit's cleaning and the base's be written first.
    ///
    ///   the hour   the crew and the clients are read, the rest of today's round is planned again, and the minutes of
    ///              this hour that a cleaner spends at a client are cleaned there (ServicesDirector.Cleaning.cs)
    ///   the day    yesterday's van is paid, each business's daily dirt is measured, contracts have their morning and
    ///              their week, a new contract offer may come; offers for businesses on the market come, lapse, parts
    ///              of a price are paid, a broken clause is charged (ServicesDirector.Sale.cs). One pass, timed with
    ///              "Developer diagnostics" on
    ///   the save   the visiting cleaner's shift is taken out of the game's schedule; the state is written
    ///
    /// EVERY EFFECT AND HOW IT IS UNDONE
    ///   dirt         written on the game's own tiles by the game's own arithmetic. It is the cleaning itself and is
    ///                not undone, as a shift's cleaning is not
    ///   a shift      one, of the running hour, while a visiting cleaner is shown to the player (CleaningPresence):
    ///                taken out on every path
    ///   money        transactions of the mod's own, dated, each with its own key and label
    ///   a sale       the game's own shut-down and the end of the lease, after the game's own question. Not undone
    ///
    /// A fault in the cleaning service or in selling a business switches that one off for the session with one line
    /// and leaves the other running; a fault outside them switches both off. Either way the mod's shift is out of
    /// the schedule and the game's daily run and the rest of the economy are left alone.
    ///
    /// Cost: nothing per frame. Once a second: a few comparisons, and while the player is indoors with the service
    /// on, a walk over today's visits (a few dozen at most).
    /// </summary>
    public sealed partial class ServicesDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeServices";
        public const int TabCleaning = 0, TabCrew = 1, TabContracts = 2, TabSale = 3;

        private static readonly string[] AppGlyphs = { "Icon-PersonManage", "Icon-Shop", "Icon-Building" };

        public static IModLogger Logger;
        public static ServicesDirector Instance { get; private set; }

        private ServicesState _state;
        private bool _running;
        private bool _broken;          // a fault outside the two parts: off for the session
        private bool _cleaningBroken, _saleBroken;
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private int _parts = -1;       // which of the two were on at the last look, a bit each
        private ServicesApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;

        public bool Running => _running && !_broken;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public ServicesState State => _running ? _state : null;

        /// <summary>The cleaning service runs: the feature is running, its option is on and no fault has stopped it.</summary>
        public bool CleaningOn => _running && !_broken && !_cleaningBroken && Settings.EconomyCleaning;

        /// <summary>
        /// Selling a business runs. Not in a Going Public session: who rents what is the host's record there, which the
        /// game's own ways of giving up a business tell and this sale would not (Core/GoingPublic). What is listed and
        /// offered waits, untouched, until the game is played alone.
        /// </summary>
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
            EconomyDirector.Second += _second;
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Second -= _second;
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;   // another city's director has started since: the state is its
            }
            // The city is unloading (or the mod is being switched off). Nothing is written to the save here: what a
            // save keeps is written when the game saves. The mod's shift, if one is in a schedule, is taken out.
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

        // ---------------------------------------------------------------- once a second (the credit side's timer)

        private void EachSecond()
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
                    CleaningStart(true);   // (switched on in the middle of play: the crew and the floors are read, and nothing is measured across the time it was off)
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

        // ---------------------------------------------------------------- starting and stopping

        private void StartUp()
        {
            GameInstance save = Save;
            int today = save.Day;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version;
            ServicesState state = ServicesStore.Load(save.modData, out version);
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
                         + _state.Crew.Count + " on the crew, " + _state.Clients.Count + " business(es) on the round, " + ContractRules.Running(_state) + " of them under contract, " + _state.Offers.Count
                         + " contract offer(s) open. Selling a business " + (SaleOn ? "on" : "off") + ": " + _state.Listings.Count + " on the market, " + _state.SaleOffers.Count + " offer(s), "
                         + _state.Dues.Count + " price(s) still being paid, " + _state.Clauses.Count + " clause(s).");
        }

        // The feature stops: the mod's shift is out of the game's schedule. With 'keep' the state goes into the save's
        // data first (a stop in the middle of play).
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (keep && _running && Save != null && _state != null)
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

        /// <summary>A fault outside the two parts: one line, the mod's shift out of the schedule, and nothing more of the feature this session.</summary>
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
            Logger?.Warn("Services: switched off for this session after an error in " + where + ". No shift of the mod's is in any schedule; the game's daily run and the rest of the economy are not affected.");
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
            Logger?.Warn("Services: the cleaning service is switched off for this session after an error in " + where + ". No shift of the mod's is in any schedule; nothing more is cleaned or charged by it. Selling a business is not affected.");
        }

        private void SaleFault(string where, Exception e)
        {
            _saleBroken = true;
            _parts = (CleaningOn ? 1 : 0) | (SaleOn ? 2 : 0);
            Logger?.Error(e);
            Logger?.Warn("Services: selling a business is switched off for this session after an error in " + where + ". Nothing more is offered, paid or charged by it. The cleaning service is not affected.");
        }

        private void Install()
        {
            var view = new ServicesApp(this);
            GameSprites.Want(AppGlyphs);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_svc_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(96, 196, 178, 255), new Color32(34, 122, 128, 255), 0.42f, "AppIcon-Services"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Services: the game's phone is not built as this version expects, so the Services app is left off. A round that is set up still runs, contracts still pay, and notifications still say what happens.");
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

        // ---------------------------------------------------------------- the hour, the day, the save

        // The game's hour event (every game hour, and first thing in its NewDay), after the credit side's work on it and before the game's own hourly cleaning.
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
                    return;   // the hour that opens a day: the day event follows, still before the game's hourly cleaning, and does this hour with the new day's round
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
                bool next = day == _state.LastDay + 1;   // (a jump of many days is one pass, for today)
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
                    Logger?.Info("Services check (day " + day + "): " + _state.Crew.Count + " on the crew, " + _state.Clients.Count + " on the round (" + ContractRules.Running(_state) + " under contract), "
                                 + _visits.Count + " visit(s) planned, " + _state.Listings.Count + " business(es) on the market with " + _state.SaleOffers.Count + " offer(s); the cleaning service "
                                 + ((cleaned - started) * 1000f).ToString("0.0") + " ms, selling " + ((done - cleaned) * 1000f).ToString("0.0") + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // The game is about to write a save. A shift of the mod's is taken out of the schedule (the schedule is part of
        // the save) and put back within a second if the visit is still on; the state is written.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null || _state == null)
                {
                    return;
                }
                bool shift = CleaningPresence.Showing;
                CleaningPresence.Clear(false);
                Write(Save);
                if (Verbose)
                {
                    Logger?.Info("Services check (before a save): " + (shift ? "the visiting cleaner's shift taken out of the schedule until the save is written" : "no shift of the mod's in any schedule")
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

        // ---------------------------------------------------------------- money

        /// <summary>A charge of the mod's own. Says so with "Developer diagnostics" on.</summary>
        private bool Charge(int amount, string key, string name, Address address, int? day, bool deductible, bool force, string how)
        {
            bool taken = ServicesGame.Take(amount, key, name, address, day, deductible, force);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Services check (transaction, " + key + ", " + name + "): " + amount + (taken ? " taken" + (deductible ? ", deductible" : string.Empty) : " could not be taken: the cash does not cover it")
                             + "; " + how + ": PASS");
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

        // ---------------------------------------------------------------- what the player is told

        /// <summary>A notification of the game's that opens the Services app on a tab, with something picked.</summary>
        private void Tell(NotificationType type, string key, int tab, string pick, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length >= 2)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // (the game keeps a notification's data for as long as it lists it)
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 11f, null, RealLifeOverhaul.Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        /// <summary>Opens the app on a tab, with something picked.</summary>
        public void Open(int tab, string pick)
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

        /// <summary>For another app's button: a business's page for selling it, by its place (as business pressure writes one).</summary>
        public static void OpenSale(string place)
        {
            Instance?.Open(TabSale, string.IsNullOrEmpty(place) ? null : "sell:" + place);
        }

        /// <summary>True while selling a business runs: another app shows its button for it only then.</summary>
        public static bool CanSell => Instance != null && Instance.SaleOn && Instance._app != null;

        /// <summary>True while the cleaning service runs.</summary>
        public static bool CanClean => Instance != null && Instance.CleaningOn && Instance._app != null;

        /// <summary>The app has been opened: the crew and the clients are read again, so that the page shows the floors as they are now.</summary>
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

        /// <summary>Something has changed: the app's badge is made again, and the app if it is open.</summary>
        internal void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            Badge();
            if (_view != null)
            {
                _view.Changed();
            }
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : Waiting());
            }
        }

        /// <summary>How many things wait for an answer from the player: contract offers and offers for a business.</summary>
        public int Waiting()
        {
            if (!_running || _state == null)
            {
                return 0;
            }
            return (CleaningOn ? _state.Offers.Count : 0) + (SaleOn ? _state.SaleOffers.Count : 0);
        }

        // ---------------------------------------------------------------- for the save tools

        /// <summary>For the save tools: no shift of the mod's is in any schedule; the timer starts the feature again if an option is on.</summary>
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
