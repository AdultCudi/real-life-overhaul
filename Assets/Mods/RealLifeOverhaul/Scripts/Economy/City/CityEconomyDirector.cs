using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The city's economy: a central bank, prices, a cycle, districts with an income, supply that moves, and what each
    /// does to the player. See the README ("Economy: the city").
    ///
    /// This class keeps it going and applies it. It has no Update, no timer and no place of its own in the game's
    /// events: once a second the credit side's timer calls <see cref="EachSecond"/> (the save is waited for, the
    /// option is looked at), and the credit side's own handlers of the game's hour and day events call
    /// <see cref="OnHour"/> and <see cref="OnDay"/> when their own work is done (EconomyDirector.AfterHour, AfterDay).
    ///
    ///   the day    after the game's daily run and after the credit side has settled its day (the accounts take their
    ///              payments first): the model counts the day (<see cref="CityModel.Day"/>), then the
    ///              rate, the wholesale prices and each business's footfall are put as the model says, the day's
    ///              rent, wages and takings are adjusted, and a rival may answer a business that does well
    ///   the hour   each business's footfall figure is kept (the game writes its own over it when marketing changes)
    ///   the save   the footfall figures are the game's own in the save; the model's state is written
    ///
    /// IN A GOING PUBLIC SESSION the economy is the city's, one for every player (Core/SharedCity, part E): the host
    /// counts its days as alone and its state goes to every guest when a day has been counted; a guest counts no day
    /// of the model (no month, no decision, no shock of its own) but takes the host's (<see cref="CityShare"/>) and
    /// applies it here as its own: the rate, the wholesale prices, the footfall, and its own day's rent, wages and
    /// takings at the city's prices. When the guest leaves, it counts its days again from the host's last one.
    ///
    /// EVERY EFFECT AND HOW IT IS UNDONE
    ///   loan rates        <see cref="LendingTerms.BasePoints"/> and <see cref="AccountRules.BaseBp"/>: two numbers of the
    ///                     mod's own, nothing when this is off; the banks' settings are written and restored by BankTerms
    ///   wholesale prices  the items' own <c>wholesalePrice</c> in memory (<see cref="CityGame"/>): never in a save, put
    ///                     back exactly when this stops for any reason
    ///   footfall          the business feature's promotion figure (Wiring/CityLinks), put back before every save, when
    ///                     this stops, and by the game itself every night
    ///   rents, wages,     one transaction of the mod's own each, dated on the day it is for; nothing of the game's is
    ///   takings           changed
    ///
    /// A fault anywhere switches the city's economy off for the session with one line, puts every figure back and
    /// leaves the game's daily run and the credit side alone (<see cref="Fault"/>).
    /// </summary>
    public sealed class CityEconomyDirector : MonoBehaviour
    {
        private const int PurposeCopycat = 51;

        public static IModLogger Logger;
        public static CityEconomyDirector Instance { get; private set; }

        private readonly List<CityShop> _shops = new List<CityShop>(16);
        private readonly List<CityShop> _pool = new List<CityShop>(16);
        private readonly List<CityNews> _picked = new List<CityNews>(MacroCatalogue.NewsADay);
        private readonly HashSet<BuildingRegistration> _was = new HashSet<BuildingRegistration>(ReferenceComparer<BuildingRegistration>.Instance);   // the businesses whose figure carried something before it was worked out again
        private readonly Dictionary<BuildingRegistration, CityShop> _byReg = new Dictionary<BuildingRegistration, CityShop>(16, ReferenceComparer<BuildingRegistration>.Instance);   // each business's shop of _shops, for FootfallOf
        private readonly ProfitRuns<Address> _runs = new ProfitRuns<Address>();

        private CityState _state;
        private string _beforeTests;   // the state's line as it was before the first test tool changed it; null with no test change in place
        private bool _running;
        private bool _broken;          // a fault: off for the session
        private bool _holding;         // the footfall figures are the game's own for a save: nothing is added until the next game hour
        private int _level;
        private int _monthDays = MacroCatalogue.MonthDaysLeast;
        private Action _second, _onSave, _onHour, _onDay;
        private int _followedDay;      // a guest following the host's city: the last day its own part of the day was done
        private bool _heardHost;       // a guest: the host's state has been applied at least once since it began to follow
        private int _eveTold;          // the day the evening notice of tomorrow's rate decision was last sent (CityStore.EveKey)

        public bool Running => _running;

        /// <summary>The state in use; null while the city's economy is not running.</summary>
        public CityState State => _running ? _state : null;

        /// <summary>The player's businesses as they were read this morning (or when the economy started today).</summary>
        public IReadOnlyList<CityShop> Shops => _shops;

        public int Level => _level;
        public int MonthDays => _monthDays;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            CityGame.Logger = Logger;
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            EconomyDirector.Follow(StartSeats.CityEconomy, _second);   // (its first second after a load has a frame of its own: Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
            SharedCity.Register(new SharedCityPart
            {
                Id = 'E', Name = "economy",
                Ready = () => _running && !_broken && _state != null,
                Write = () => CityShare.Write(_state),
                Apply = ApplyHost,
                Left = () =>
                {
                    _heardHost = false;
                    Logger?.Info("City economy: the host's city is no longer followed; the model counts its days again from day " + (_state != null ? _state.LastDay : 0) + ", the host's last.");
                }
            });
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                return;   // another city's director has started since: the state and the figures are its
            }
            // The city is unloading (or the mod is being switched off): every figure in memory is the game's own again
            // before anything else. Nothing is written to the save here (what a save keeps is written when the game saves).
            try
            {
                StandDown("the city is unloading", false, false, true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Zero();
                CityGame.RestoreWholesale();
            }
            CityGame.Forget();
            Instance = null;
        }

        // ---------------------------------------------------------------- once a second (the credit side's timer)

        private static readonly int HotSecond = HotSpots.Slot("Economy: CityEconomyDirector.Second");
        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);   // the credit side's second, for this feature (Core/HotSpots)
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
            if (!Settings.Economy || !Settings.EconomyCity || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!Settings.EconomyCity ? "the option is off" : "the credit side is not running", true, true, false);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            int level = MacroCatalogue.Level(Settings.EconomyCityLevel);
            if (level != _level)
            {
                _level = level;
                CityEconomy.Level = level;
                ApplyAll(Save.Day, true, false, "the strength was changed");
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
            CityState state = CityStore.Load(save.modData, CityStore.Key, out version);
            bool fresh = state == null;
            if (fresh)
            {
                state = CityModel.Open(new System.Random().Next(1, int.MaxValue), today);
            }
            string before;
            _beforeTests = save.modData.TryGetValue(CityStore.TestKey, out before) && !string.IsNullOrEmpty(before) ? before : null;
            _eveTold = save.modData.TryGetValue(CityStore.EveKey, out string eve) && int.TryParse(eve, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int eveDay) ? eveDay : 0;
            int districts = CityGame.Districts(state);
            int goods = CityGame.FindGoods();

            // The self-check: before anything is applied, nothing of the mod's may be in the game's figures.
            string detail;
            bool clean = CityGame.CheckWholesale(null, out detail);
            bool rateClean = LendingTerms.BasePoints == 0f && AccountRules.BaseBp == 0;
            if (!clean || !rateClean)
            {
                int put = CityGame.RestoreWholesale();
                Zero();
                Logger?.Warn("City economy: figures of an earlier session were still in memory and have been put back (" + put + " wholesale price(s)" + (rateClean ? string.Empty : ", the bank rate") + "). " + detail + ".");
            }
            else if (Verbose)
            {
                Logger?.Info("Economy check (city, self-check at load): " + detail + ", the bank rate at the banks' own: PASS");
            }

            state.LastDay = today;   // days that passed while this was off are not counted
            _state = state;
            _level = MacroCatalogue.Level(Settings.EconomyCityLevel);
            _monthDays = MacroCatalogue.MonthDays(DaysPerYear());
            _holding = false;
            _running = true;
            CityEconomy.Level = _level;
            CityEconomy.Live = state;
            Logger?.Info("City economy: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". " + Summary()
                         + " " + districts + " district(s), " + goods + " good(s) with a wholesale price, a month is " + _monthDays + " day(s).");
            ApplyAll(today, true, false, "the start");
        }

        private static int DaysPerYear()
        {
            GameInstance save = Save;
            int days = GameCalendar.DaysPerYear;
            return days >= 8 ? days : 60;
        }

        // The city's economy stops: every figure it changed is the game's own again. With 'keep' the state goes into
        // the save's data first (a stop in the middle of play). Unless the city itself is going ('unloading'), the banks
        // are given their terms without the rate and the customers still to come today are drawn again from the game's
        // own footfall.
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (keep && _running && Save != null && _state != null)
            {
                Write(Save);
            }
            _running = false;
            _holding = false;
            CityEconomy.Live = null;
            CityEconomy.Level = MacroCatalogue.LevelDefault;
            bool rate = Zero();
            int restored = CityGame.RestoreWholesale();
            int shops = 0;
            if (!unloading)
            {
                if (rate && EconomyDirector.Instance != null)
                {
                    EconomyDirector.Instance.TermsChanged("the bank rate is the banks' own again");
                }
                CustomerDraw.Hold("the city's economy stood down");
                try
                {
                    for (int i = 0; i < _shops.Count; i++)
                    {
                        if (_shops[i].Applied)
                        {
                            WriteFootfall(_shops[i], true, false);   // (nothing is live, so nothing is added: the figure is the game's own, and the business feature's)
                            shops++;
                        }
                    }
                }
                finally
                {
                    CustomerDraw.Release();
                }
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                _pool.Add(_shops[i]);
            }
            _shops.Clear();
            _byReg.Clear();
            if (was && (tell || Verbose))
            {
                string detail;
                bool ok = CityGame.CheckWholesale(null, out detail);
                Logger?.Info("Economy check (city, restored, " + why + "): " + restored + " wholesale price(s) and " + shops + " footfall figure(s) put back, the bank rate "
                             + (rate ? "back at" : "already at") + " the banks' own. " + detail + ": " + (ok && LendingTerms.BasePoints == 0f && AccountRules.BaseBp == 0 ? "PASS" : "FAIL"));
            }
        }

        // The two numbers the credit side reads are nothing again. True if either was something.
        private static bool Zero()
        {
            bool was = LendingTerms.BasePoints != 0f || AccountRules.BaseBp != 0;
            LendingTerms.BasePoints = 0f;
            AccountRules.BaseBp = 0;
            AccountRules.BaseMultiplier = 1f;
            return was;
        }

        /// <summary>A fault: one line, every figure put back, and nothing more of the city's economy this session.</summary>
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
                CityEconomy.Live = null;
                Zero();
                CityGame.RestoreWholesale();
            }
            Logger?.Warn("City economy: switched off for this session after an error in " + where + ". The banks' rates, the wholesale prices and every business's footfall are the game's own again; the game's daily run and the credit rating are not affected.");
        }

        // ---------------------------------------------------------------- the hour, the day, the save

        private static readonly int HotOnHour = HotSpots.Slot("Economy: CityEconomyDirector.OnHour");
        // The game's hour event (every game hour, and first thing in its NewDay, before its daily run), after the credit side's own work on it.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                _holding = false;
                KeepRate(false);
                if (Save.Day > _state.LastDay)
                {
                    return;   // a new day: the game's run writes every figure afresh in a moment, and the day event follows it
                }
                DecisionEve(Save.Day, Save.Hour);
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Wanted != 0 || _shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], false, false);
                    }
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Economy: CityEconomyDirector.OnDay");
        // The game's day event (after its daily run: rent, wages and takings are booked, the day's customers drawn), after the credit side has settled its day.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                bool follow = SharedCity.Follows('E');
                if (follow ? day <= _followedDay : day <= _state.LastDay)
                {
                    return;
                }
                _holding = false;
                _monthDays = MacroCatalogue.MonthDays(DaysPerYear());
                if (follow)
                {
                    // A guest of a Going Public session: the city's day is the host's, which comes with its next update. The
                    // player's own part of the day is done here, at the city's figures as they stand.
                    _followedDay = day;
                    ApplyAll(day, true, true, "the day (the host's city)");
                    Adjust(day - 1);
                    Rivals(day);
                    return;
                }
                if (_state.Districts.Count == 0)
                {
                    CityGame.Districts(_state);   // the game's neighbourhoods were not there at the start: looked for again each day until they are
                }
                var all = new CityTick { ShockAnnounced = -1 };
                int from = Math.Max(_state.LastDay + 1, day - _monthDays + 1);   // (a jump of many days counts a month of them at most)
                for (int d = from; d <= day; d++)
                {
                    int rateBefore = _state.Base, indexBefore = _state.Index, inflationBefore = _state.Inflation;
                    CycleState cycleBefore = _state.Cycle;
                    CityTick tick = CityModel.Day(_state, d, CityBridges.Day != null ? CityBridges.Day(d) : new CityDay { Season = 1 }, _level, _monthDays);
                    all.MonthTurned |= tick.MonthTurned;
                    all.CycleChanged |= tick.CycleChanged;
                    all.RateMoved += tick.RateMoved;
                    all.ShockAnnounced = tick.ShockAnnounced >= 0 ? tick.ShockAnnounced : all.ShockAnnounced;
                    if (Verbose && tick.MonthTurned)
                    {
                        Logger?.Info("Economy check (city, month " + CityModel.MonthNumber(_state, _monthDays) + " on day " + d + "): the rate " + Quarters(rateBefore) + " to " + Quarters(_state.Base)
                                     + " (" + _state.LastReason + "), inflation " + (inflationBefore / 100f).ToString("0.00") + "% to " + (_state.Inflation / 100f).ToString("0.00")
                                     + "% a year (pressure " + (_state.Pressure / 100f).ToString("0.00") + ", the rate it feels " + Quarters(_state.BaseBefore) + "), the index "
                                     + CityText.Index(indexBefore) + " to " + CityText.Index(_state.Index) + ", the cycle " + cycleBefore + " to " + _state.Cycle + " (month " + _state.MonthsIn + ", "
                                     + _state.MonthsLeft + " left): PASS");
                    }
                    if (Verbose && _state.Pending && CityModel.DaysToTurn(_state, _monthDays) == 1)
                    {
                        Logger?.Info("Economy check (city, decision on day " + d + "): inflation " + (_state.Inflation / 100f).ToString("0.00") + "% against a target of "
                                     + (MacroCatalogue.InflationTarget / 100f).ToString("0.00") + "%, the cycle " + _state.Cycle + ", the rate " + Quarters(_state.Base) + ": "
                                     + (_state.PendingMove == 0 ? "held" : "moved by " + Quarters(_state.PendingMove)) + " from tomorrow (" + _state.PendingReason + "): PASS");
                    }
                }
                ApplyAll(day, true, true, "the day");
                Adjust(day - 1);
                Rivals(day);
                Notify(all);
                if (all.MonthTurned)
                {
                    CityEconomy.RaiseMonthTurned(e => Logger?.Error(e));
                }
                SharedCity.Changed('E');   // (a host's guests have the day with the next update)
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // A guest of a Going Public session: the host's state, as its next update brought it. The city's figures are the
        // host's from now on and applied here; the player hears of what touches them, as the host's player does (but not
        // of how the host's city differed from theirs when they joined).
        private bool ApplyHost(string text, bool whole)
        {
            if (!_running || _broken || _state == null)
            {
                return false;
            }
            try
            {
                CityTick tick;
                if (!CityShare.Apply(_state, text, _monthDays, out tick))
                {
                    return false;
                }
                _beforeTests = null;   // (a test's state from before is this player's no more)
                ApplyAll(Save.Day, true, false, "the host's city");
                if (_heardHost)
                {
                    Notify(tick);
                    if (tick.MonthTurned)
                    {
                        CityEconomy.RaiseMonthTurned(e => Logger?.Error(e));
                    }
                }
                _heardHost = true;
                if (Verbose)
                {
                    Logger?.Info("Economy check (city, the host's): " + Summary() + (tick.MonthTurned ? " A month turned." : string.Empty) + ": PASS");
                }
                return true;
            }
            catch (Exception e)
            {
                Fault("the host's city", e);
                return false;
            }
        }

        private static string Quarters(int quarters)
        {
            return (quarters > 0 ? "+" : quarters < 0 ? "-" : string.Empty) + CityText.Points(quarters);
        }

        // The game is about to write a save. The footfall figures are put at what they are without the city's economy
        // (the registration that holds them is part of the save) and stay so until the next game hour; the state is written.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null || _state == null)
                {
                    return;
                }
                int put = 0;
                _holding = true;
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], false, false);
                        put++;
                    }
                }
                Write(Save);
                if (Verbose)
                {
                    Logger?.Info("Economy check (city, before a save): " + put + " footfall figure(s) put at the game's own until the next game hour; the wholesale prices and the banks' settings are not part of a save: PASS");
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
            save.modData[CityStore.Key] = CityStore.Write(_state);
            save.modData[CityStore.EveKey] = _eveTold.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (_beforeTests != null)
            {
                save.modData[CityStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(CityStore.TestKey);
            }
        }

        // ---------------------------------------------------------------- applying the model

        // Everything the model says is put in place: the rate, the wholesale prices, each business's footfall. The writes are
        // one batch: each business asked to be drawn again once, spread over the frames that follow (Core/CustomerDraw).
        private void ApplyAll(int day, bool redraw, bool newDay, string when)
        {
            float started = Time.realtimeSinceStartup;
            bool rate = KeepRate(true);
            int prices = CityGame.ApplyWholesale(_state);
            _was.Clear();
            for (int i = 0; !newDay && i < _shops.Count; i++)
            {
                if (_shops[i].Applied)
                {
                    _was.Add(_shops[i].Reg);
                }
            }
            CityGame.ReadShops(_state, _level, _shops, _pool);
            _byReg.Clear();
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].Reg != null && !_byReg.ContainsKey(_shops[i].Reg))
                {
                    _byReg.Add(_shops[i].Reg, _shops[i]);
                }
            }
            int written = 0, redrawn = 0;
            StringBuilder log = Verbose ? new StringBuilder(160) : null;
            CustomerDraw.Hold("the city's economy: " + when);
            try
            {
                for (int i = 0; i < _shops.Count; i++)
                {
                    CityShop shop = _shops[i];
                    // A figure is written where something is to be added, or where something was and no longer is. (On a
                    // new day the game has just written every figure afresh, so nothing is left from before.)
                    if (shop.Wanted != 0 || _was.Contains(shop.Reg))
                    {
                        WriteFootfall(shop, redraw, newDay);
                        written++;
                        redrawn += redraw && shop.HasFigure ? 1 : 0;
                    }
                    if (log != null && i < 6)
                    {
                        log.Append(i > 0 ? "; " : string.Empty).Append(shop.Reg.BusinessName).Append(' ').Append(CityText.Percent(shop.Wanted)).Append(" (cycle ").Append(shop.Cycle).Append(", district ")
                            .Append(shop.Area).Append(", prices ").Append(shop.Price).Append(shop.Against > 0 ? " at " + (shop.Against / 10f).ToString("0.#") + "% of what the district bears" : ", none to judge by")
                            .Append(shop.HasFigure ? ", promotion " + shop.Own + " to " + shop.Written : ", no promotion figure").Append(')');
                    }
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
            _was.Clear();
            if (log != null)
            {
                string detail;
                bool ok = CityGame.CheckWholesale(_state, out detail);
                var factors = new StringBuilder(80);
                for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
                {
                    factors.Append(k > 0 ? ", " : string.Empty).Append(CityText.KindKeys[k].Substring(CityText.KindKeys[k].LastIndexOf('_') + 1)).Append(' ').Append((CityModel.Wholesale(_state, k) / 1000f).ToString("0.000"));
                }
                Logger?.Info("Economy check (city, applied, " + when + ", day " + day + "): the rate " + Quarters(_state.Base) + (rate ? " (changed)" : string.Empty) + ", " + prices
                             + " wholesale price(s) written (" + factors + "), " + written + " footfall figure(s) written, " + redrawn + " with the day's customers asked to be drawn again (the customers' line says how many were)"
                             + (log.Length > 0 ? ": " + log : string.Empty) + (_shops.Count > 6 ? "; and " + (_shops.Count - 6) + " more" : string.Empty) + ". " + detail + "; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: " + (ok ? "PASS" : "FAIL"));
            }
        }

        // The two numbers the credit side reads are what the model says. True if either changed.
        private bool KeepRate(bool tell)
        {
            float points = CityModel.BasePoints(_state);
            float multiplier = Save != null && Save.gameVariables != null ? Save.gameVariables.bankInterestMultiplier : 1f;
            int bp = ProductTerms.BaseBp(points, multiplier);
            if (LendingTerms.BasePoints == points && AccountRules.BaseBp == bp && AccountRules.BaseMultiplier == multiplier)
            {
                return false;
            }
            LendingTerms.BasePoints = points;
            AccountRules.BaseBp = bp;
            AccountRules.BaseMultiplier = multiplier;
            if (EconomyDirector.Instance != null)
            {
                EconomyDirector.Instance.TermsChanged("the bank rate");   // the banks' settings carry it from now on
            }
            return true;
        }

        private void WriteFootfall(CityShop shop, bool redraw, bool newDay)
        {
            int own = 0, written = 0;
            shop.HasFigure = CityBridges.Footfall != null && shop.Reg != null && CityBridges.Footfall(shop.Reg, redraw, newDay, out own, out written);
            shop.Own = own;
            shop.Written = written;
            shop.Applied = shop.HasFigure && _running && !_holding && shop.Wanted != 0;
        }

        /// <summary>
        /// What the city's economy adds to a business's footfall now, in per cent of customers: what the business
        /// feature asks each time it writes a business's figure (Wiring/CityLinks). Nothing while this is not running,
        /// and nothing while the figures are held at the game's own for a save.
        /// </summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            CityEconomyDirector d = Instance;
            if (d == null || !d._running || d._holding || d._broken || reg == null)
            {
                return 0;
            }
            return d._byReg.TryGetValue(reg, out CityShop shop) ? shop.Wanted : 0;   // (each business's shop as the morning read it: a look-up, asked on every figure written)
        }

        /// <summary>What the morning's chance of a business trouble is multiplied by; one while this is not running.</summary>
        public static float TroubleScale()
        {
            CityEconomyDirector d = Instance;
            return d != null && d._running ? CityModel.Trouble(d._state.Cycle, d._level) / 1000f : 1f;
        }

        /// <summary>How often a rival undercuts against the ordinary, in thousandths; 1000 while this is not running.</summary>
        public static int UndercutScale()
        {
            CityEconomyDirector d = Instance;
            return d != null && d._running ? MacroCatalogue.StateUndercut[(int)d._state.Cycle] : 1000;
        }

        // ---------------------------------------------------------------- rents, wages and takings

        // The day that has ended: the game's own rent, wages and takings of that day, each adjusted by one transaction.
        private void Adjust(int day)
        {
            DayBooks books = CityGame.DayFigures(day);
            int costs = CityModel.Costs(_state), spend = CityModel.Takings(_state, _level);
            Settle(CityAdjust.Of(books.Rent.Total, costs), ref _state.CarryRent, CityGame.RentKey, day, costs, "rent", books.Rent);
            Settle(CityAdjust.Of(books.Wages.Total, costs), ref _state.CarryWages, CityGame.WagesKey, day, costs, "wages", books.Wages);
            Settle(-CityAdjust.Of(books.Takings.Total, spend), ref _state.CarryTakings, CityGame.TakingsKey, day, spend, "takings", books.Takings);
            Settle(CityAdjust.Of(books.Living.Total, costs), ref _state.CarryLiving, CityGame.LivingKey, day, costs, "the game's shops and services", books.Living);
            GameBooks.Refresh(day);   // the day's statements with the lines in them (the game made them before the lines were booked)
        }

        /// <summary>What the game's own takings come to at today's prices, in thousandths (the city's takings line: prices and what customers spend); 1000 while this is not running.</summary>
        public static int TakingsFactor
        {
            get
            {
                CityEconomyDirector d = Instance;
                int factor = d != null && d._running ? CityModel.Takings(d._state, d._level) : 1000;
                return Math.Abs(factor - 1000) < MacroCatalogue.AdjustQuiet ? 1000 : factor;   // (as CityAdjust.Of: nothing within the quiet band)
            }
        }

        // A charge (more than nothing: the player pays) is taken as far as the cash goes and the rest carried; a refund is paid.
        private void Settle(int charge, ref int carried, string key, int day, int factor, string what, DayLine line)
        {
            float figure = line.Total;
            int before = carried;
            int taken = CityAdjust.Take(charge, CityGame.Cash, ref carried);
            int booked = taken != 0 ? -BookShared(-taken, key, day, CityText.Change(factor), line) : 0;
            if (booked != taken)
            {
                carried = (int)Math.Min((long)carried + taken - booked, int.MaxValue / 2);   // the game refused a part: it is still owed
                taken = booked;
            }
            if (Verbose && (charge != 0 || before != 0))
            {
                Logger?.Info("Economy check (city, adjustment for day " + day + "): " + what + " " + figure.ToString("0") + " by the game, factor " + (factor / 1000f).ToString("0.000") + ", so "
                             + (charge >= 0 ? charge + " more to pay" : -charge + " back") + "; carried from before " + before + "; " + (taken >= 0 ? taken + " taken" : -taken + " paid")
                             + ", " + carried + " carried to tomorrow: " + ((long)before + charge == (long)taken + carried ? "PASS" : "FAIL"));
            }
        }

        // The day's line booked where the game's own were (a business, a home, a category of the day's statement),
        // shared out by their figures; where there is nothing to share by, one line of the player's own. What was booked.
        private static int BookShared(int amount, string key, int day, string percent, DayLine line)
        {
            float weight = 0f;
            for (int i = 0; i < line.Figures.Count; i++)
            {
                weight += line.Figures[i];
            }
            if (line.Figures.Count == 0 || Math.Abs(weight) < 0.5f)
            {
                return CityGame.Book(amount, key, day, percent) ? amount : 0;
            }
            int[] parts = CityAdjust.Split(amount, line.Figures.ToArray());
            int booked = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != 0 && CityGame.Book(parts[i], key, day, percent, line.AddressAt(i), line.CategoryAt(i), line.DeductibleAt(i)))
                {
                    booked += parts[i];
                }
            }
            return booked;
        }

        // ---------------------------------------------------------------- rivals

        // A business that has made a profit every day for two weeks may draw a rival: the business feature's own
        // "a competitor undercuts you", with its own answers and its own end. One business a day is looked at: the
        // one that made the most.
        private void Rivals(int day)
        {
            if (CityBridges.Copycat == null || _shops.Count == 0 || (_state.LastCopycatDay > 0 && day - _state.LastCopycatDay < MacroCatalogue.CopycatRestDays))
            {
                return;
            }
            int chance = MacroCatalogue.CopycatChance * CityModel.Trouble(_state.Cycle, _level) / 1000;
            if (CityModel.Draw(_state.Seed, PurposeCopycat, _state.Age, 1000) >= chance)
            {
                return;
            }
            CityShop best = null;
            float most = 0f;
            Runs(day);
            for (int i = 0; i < _shops.Count; i++)
            {
                float total;
                if (ProfitRun(_shops[i].Reg, day, out total) && total > most)
                {
                    most = total;
                    best = _shops[i];
                }
            }
            if (best == null)
            {
                return;
            }
            bool started = CityBridges.Copycat(best.Reg);
            if (started)
            {
                _state.LastCopycatDay = day;
                _state.News.Add(new CityNews { Day = day, Kind = CityNewsKind.Rival, Name = CityStore.Clean(best.Reg.BusinessName) });
            }
            if (Verbose)
            {
                Logger?.Info("Economy check (city, rival): " + best.Reg.BusinessName + " made a profit on each of the last " + MacroCatalogue.CopycatDays + " days (" + most.ToString("0")
                             + " in all), the chance today was " + (chance / 10f).ToString("0.#") + "%: " + (started ? "a rival opens beside it (the business feature's own issue)" : "the business feature did not start one there today") + ": PASS");
            }
        }

        // Every business's run of profit over the last so-many days of the game's statements, in one pass over them (newest
        // first, as a walk for one business goes): a look-up a business afterwards, instead of a walk of every day's
        // statements for each.
        private void Runs(int today)
        {
            _runs.Begin();
            GameInstance save = Save;
            List<FinancialSummary> summaries = save != null ? save.financialSummaries : null;
            int from = today - MacroCatalogue.CopycatDays;
            for (int i = summaries != null ? summaries.Count - 1 : -1; i >= 0; i--)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber >= today)
                {
                    continue;
                }
                if (summary.dayNumber < from)
                {
                    break;   // (the game keeps them oldest first)
                }
                _runs.Day();
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                    if (statement != null && !ReferenceEquals(statement.Address, null))
                    {
                        _runs.Add(statement.Address, statement.TotalProfit);
                    }
                }
            }
        }

        // True if the game's daily statements show a profit at this business on each of the last so-many days: from the
        // morning's runs (Runs). An address that is the game's "no address" is walked for as it always was.
        private bool ProfitRun(BuildingRegistration reg, int today, out float total)
        {
            total = 0f;
            GameInstance save = Save;
            if (save == null || save.financialSummaries == null || reg == null || !reg.HasValidAddress)
            {
                return false;
            }
            Address address = reg.Address;
            if (!ReferenceEquals(address, null) && !(string.IsNullOrEmpty(address.streetName) && address.streetNumber == 0))
            {
                return _runs.Ran(address, MacroCatalogue.CopycatDays, out total);
            }
            return ProfitRunWalk(reg, today, out total);
        }

        // The walk for one business, for an address equal to the game's "no address" (a statement with none matches it).
        private static bool ProfitRunWalk(BuildingRegistration reg, int today, out float total)
        {
            total = 0f;
            GameInstance save = Save;
            int days = 0;
            int from = today - MacroCatalogue.CopycatDays;
            List<FinancialSummary> summaries = save.financialSummaries;
            for (int i = summaries.Count - 1; i >= 0; i--)
            {
                FinancialSummary summary = summaries[i];
                if (summary == null || summary.businessIncomeStatements == null || summary.dayNumber >= today)
                {
                    continue;
                }
                if (summary.dayNumber < from)
                {
                    break;   // (the game keeps them oldest first)
                }
                for (int b = 0; b < summary.businessIncomeStatements.Count; b++)
                {
                    FinancialSummary.BusinessIncomeStatement statement = summary.businessIncomeStatements[b];
                    if (statement != null && statement.Address == reg.Address)
                    {
                        if (statement.TotalProfit <= 0f)
                        {
                            return false;
                        }
                        total += statement.TotalProfit;
                        days++;
                        break;
                    }
                }
            }
            return days >= MacroCatalogue.CopycatDays;
        }

        // ---------------------------------------------------------------- what the player is told

        // Only what touches the player: the rate has moved while they owe on an overdraft or a card, or a shortage or
        // a glut starts tomorrow in goods their businesses stock.
        private void Notify(CityTick tick)
        {
            if (tick.RateMoved != 0 && OwesVariable())
            {
                Notifications.Show(tick.RateMoved > 0 ? NotificationType.Warning : NotificationType.Info, tick.RateMoved > 0 ? "reallife_macro_note_rate_up" : "reallife_macro_note_rate_down",
                    new Dictionary<string, string> { { "points", CityText.Points(tick.RateMoved) }, { "rate", CityText.Rate(_state.Base) } }, 10f, null, Guard.Wrap(OpenCity), true, false);
            }
            int kind = tick.ShockAnnounced;
            if (kind >= 0 && (_state.Buys & (1 << kind)) != 0 && _state.Supply[kind].Announced(_state.Age))
            {
                SupplyLine line = _state.Supply[kind];
                Notifications.Show(line.Shock == SupplyShock.Shortage ? NotificationType.Warning : NotificationType.Info,
                    line.Shock == SupplyShock.Shortage ? "reallife_macro_note_shortage" : "reallife_macro_note_glut",
                    new Dictionary<string, string>
                    {
                        { "kind", CityText.Kind(kind) }, { "percent", Math.Abs(CityModel.ShockPercent(line, _level)).ToString() }, { "days", line.Days.ToString() }
                    }, 12f, null, Guard.Wrap(OpenCity), true, false);
            }
        }

        // The evening before a decision of the central bank takes effect: one notice of what the market expects (the move
        // announced, as the City page shows it). Once a day, the day kept in the player's own line of the save.
        private void DecisionEve(int day, int hour)
        {
            if (!Settings.EconomyRateAhead || !CityModel.DecisionEve(_state, _monthDays, day, hour, _eveTold))
            {
                return;
            }
            _eveTold = day;
            int after = MacroCatalogue.Clamp(_state.Base + _state.PendingMove, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost);
            Notifications.Show(NotificationType.Info, CityModel.DecisionEveKey(_state.PendingMove),
                new Dictionary<string, string> { { "points", CityText.Points(_state.PendingMove) }, { "rate", CityText.Rate(after) } }, 10f, null, Guard.Wrap(OpenCity), true, false);
            if (Verbose)
            {
                Logger?.Info("Economy check (city, the evening before a decision, day " + day + " at " + hour.ToString("00") + ":00): the rate " + Quarters(_state.Base) + " to " + Quarters(after)
                             + " from tomorrow; the notice sent once: PASS");
            }
        }

        private static bool OwesVariable()
        {
            CreditFile file = CreditLedger.File;
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (!a.Closed && !a.Test && a.Owed > 0 && ProductCatalogue.Revolving(a.Kind))
                {
                    return true;
                }
            }
            return false;
        }

        private static void OpenCity()
        {
            Finance.OpenCity();
        }

        /// <summary>
        /// The pieces for a day's paper, the most pressing first, two at most. The same answer however often it is
        /// asked for a day. Nothing while this is not running.
        /// </summary>
        public IReadOnlyList<CityNews> Paper(int day)
        {
            _picked.Clear();
            if (!_running || _state == null)
            {
                return _picked;
            }
            List<CityNews> news = _state.News;
            for (int rank = 0; rank < MacroCatalogue.NewsKinds && _picked.Count < MacroCatalogue.NewsADay; rank++)
            {
                for (int i = 0; i < news.Count && _picked.Count < MacroCatalogue.NewsADay; i++)
                {
                    if (news[i].Day == day && MacroCatalogue.NewsRank(news[i].Kind) == rank)
                    {
                        _picked.Add(news[i]);
                    }
                }
            }
            return _picked;
        }

        private string Summary()
        {
            return "The economy is " + _state.Cycle + " (month " + _state.MonthsIn + ", " + _state.MonthsLeft + " left), the bank rate " + Quarters(_state.Base) + " on the banks' own, inflation "
                   + (_state.Inflation / 100f).ToString("0.00") + "% a year, the price index " + CityText.Index(_state.Index) + ", strength " + _level + ".";
        }

        // ---------------------------------------------------------------- for the save tools

        /// <summary>For the save tools: every figure is the game's own again; the timer starts it again if the option is on.</summary>
        public void StopAll()
        {
            try
            {
                if (_running)
                {
                    _state.CarryRent = _state.CarryWages = _state.CarryTakings = _state.CarryLiving = 0;   // what was carried is written off: nothing of the mod's is owed in a save that is leaving it
                }
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
            _beforeTests = null;
        }

        // ---------------------------------------------------------------- test tools

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyCity || _broken || !_running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (SharedCity.Follows('E'))
            {
                TestRefusals.TellKey(Logger, test, "reallife_gp_city_test_host");
                return false;
            }
            return true;
        }

        // A test tool is about to change the state: the state as it is now is kept, once, to be put back.
        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = CityStore.Write(_state);
            }
        }

        private void TestDone(string what)
        {
            SharedCity.Changed('E');
            ApplyAll(Save.Day, true, false, "a test");
            Logger?.Warn("City economy test: " + what + ". " + Summary());   // asked for by a button: written whether or not diagnostics are on
            Notifications.Show(NotificationType.Info, "reallife_macro_test_done", new Dictionary<string, string>
            {
                { "state", CityText.State(_state.Cycle) }, { "rate", CityText.Rate(_state.Base) }, { "index", CityText.Index(_state.Index) }
            }, 8f, null, Guard.Wrap(OpenCity), true, false);
        }

        /// <summary>"Test: show the city's economy in the log".</summary>
        public void TestShow()
        {
            if (!TestReady("City economy report"))
            {
                return;
            }
            try
            {
                var text = new StringBuilder(900);
                text.Append("City economy: day ").Append(Save.Day).Append(", model day ").Append(_state.Age).Append(", month ").Append(CityModel.MonthNumber(_state, _monthDays)).Append(" (")
                    .Append(CityModel.DaysToTurn(_state, _monthDays)).Append(" day(s) to the next). ").Append(Summary());
                if (_state.Pending)
                {
                    text.Append(" Announced for the turn of the month: ").Append(_state.PendingMove == 0 ? "the rate is held" : "the rate moves by " + Quarters(_state.PendingMove)).Append(" (").Append(_state.PendingReason).Append(").");
                }
                text.Append(" Rents and wages x").Append((CityModel.Costs(_state) / 1000f).ToString("0.000")).Append(", takings x").Append((CityModel.Takings(_state, _level) / 1000f).ToString("0.000"))
                    .Append("; carried: rent ").Append(_state.CarryRent).Append(", wages ").Append(_state.CarryWages).Append(", takings ").Append(_state.CarryTakings).Append(", shops and services ").Append(_state.CarryLiving).Append(". Test changes in place: ")
                    .Append(_beforeTests != null ? "yes" : "no").Append('.');
                Logger?.Warn(text.ToString());
                for (int i = 0; i < _state.Districts.Count; i++)
                {
                    CityDistrict d = _state.Districts[i];
                    Logger?.Warn("City economy:   district " + d.Name + ": income " + CityText.Income(d.Income) + " (started at " + CityText.Income(d.Start) + ", a month ago " + CityText.Income(d.Before)
                                 + "), footfall " + CityText.Percent(CityModel.DistrictFootfall(d)) + ", bears " + (CityModel.Tolerance(_state, d, _level) / 10f).ToString("0.#") + "% of the game's own price.");
                }
                for (int k = 0; k < MacroCatalogue.SupplyKinds; k++)
                {
                    SupplyLine line = _state.Supply[k];
                    Logger?.Warn("City economy:   supply " + k + " (" + CityGame.CountOf(k) + " good(s)): factor " + (line.Factor / 1000f).ToString("0.000") + ", with prices x" + (CityModel.Wholesale(_state, k) / 1000f).ToString("0.000")
                                 + (line.Shock == SupplyShock.None ? string.Empty : ", " + line.Shock + (line.Announced(_state.Age) ? " from tomorrow" : " running") + " for " + line.Days + " day(s), size "
                                                                                   + (line.Size / 10f).ToString("0.#") + "%") + ((_state.Buys & (1 << k)) != 0 ? ", stocked by the player" : string.Empty) + ".");
                }
                float multiplier = Save.gameVariables != null ? Save.gameVariables.baseCustomerPromotionMultiplier : 0.5f;
                for (int i = 0; i < _shops.Count; i++)
                {
                    CityShop s = _shops[i];
                    Logger?.Warn("City economy:   business " + s.Reg.BusinessName + " in " + (s.District != null ? s.District.Name : "?") + ": footfall " + CityText.Percent(s.Wanted) + " asked (cycle " + s.Cycle
                                 + ", district " + s.Area + ", prices " + s.Price + "), " + (s.HasFigure ? "promotion " + s.Own + " to " + s.Written + ", which gives " + CityText.Percent(s.Achieved(multiplier)) : "no promotion figure")
                                 + "; prices " + (s.Against > 0 ? "at " + (s.Against / 10f).ToString("0.#") + "% of what the district bears" + (s.Over ? " (over)" : string.Empty) : "none to judge by") + ".");
                }
                string detail;
                bool ok = CityGame.CheckWholesale(_state, out detail);
                Logger?.Warn("Economy check (city, the test button): " + detail + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(NotificationType.Info, "reallife_macro_test_done", new Dictionary<string, string>
                {
                    { "state", CityText.State(_state.Cycle) }, { "rate", CityText.Rate(_state.Base) }, { "index", CityText.Index(_state.Index) }
                }, 8f, null, Guard.Wrap(OpenCity), true, false);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: step the city's economy a month": the model counts the days to the next turn of a month at once (today's season, weather and events for each).</summary>
        public void TestMonth()
        {
            if (!TestReady("City economy month"))
            {
                return;
            }
            try
            {
                BeforeTest();
                int today = Save.Day;
                CityDay env = CityBridges.Day != null ? CityBridges.Day(today) : new CityDay { Season = 1 };
                int steps = CityModel.DaysToTurn(_state, _monthDays);
                bool turned = false;
                for (int i = 0; i < steps; i++)
                {
                    turned |= CityModel.Day(_state, today, env, _level, _monthDays).MonthTurned;
                }
                TestDone("a month stepped (" + steps + " model day(s))");
                if (turned)
                {
                    CityEconomy.RaiseMonthTurned(e => Logger?.Error(e));
                }
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: put the economy in ...": the cycle is in that state from now, in its first month, for three months at least.</summary>
        public void TestCycle(CycleState state)
        {
            if (!TestReady("City economy cycle"))
            {
                return;
            }
            try
            {
                if (state == CycleState.Recession && !MacroCatalogue.Recessions(_level))
                {
                    TestRefusals.TellKey(Logger, "City economy cycle", "reallife_macro_test_no_recession");
                    return;
                }
                BeforeTest();
                _state.Cycle = state;
                _state.MonthsIn = 1;
                _state.MonthsLeft = MacroCatalogue.OpeningSteadyMonths;
                TestDone("the cycle set to " + state);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: a shortage (or a glut) from tomorrow": the largest of its kind, for five days, in goods the player's businesses stock if there are any.</summary>
        public void TestShock(bool shortage)
        {
            if (!TestReady("City economy supply"))
            {
                return;
            }
            try
            {
                int kind = -1;
                for (int pass = 0; pass < 2 && kind < 0; pass++)
                {
                    for (int k = 0; k < MacroCatalogue.SupplyKinds && kind < 0; k++)
                    {
                        if (_state.Supply[k].Shock == SupplyShock.None && (pass == 1 || (_state.Buys & (1 << k)) != 0))
                        {
                            kind = k;
                        }
                    }
                }
                if (kind < 0)
                {
                    TestRefusals.TellKey(Logger, "City economy supply", "reallife_macro_test_supply_full");
                    return;
                }
                BeforeTest();
                CityModel.Announce(_state, Save.Day, kind, shortage ? SupplyShock.Shortage : SupplyShock.Glut, shortage ? MacroCatalogue.ShortageMost : MacroCatalogue.GlutMost, 5, _level);
                TestDone((shortage ? "a shortage" : "a glut") + " of kind " + kind + " from tomorrow, " + CityModel.ShockPercent(_state.Supply[kind], _level) + "% for 5 days");
                Notify(new CityTick { ShockAnnounced = kind });
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: bank rate up (or down) one point": at once, inside its bounds.</summary>
        public void TestRate(int quarters)
        {
            if (!TestReady("City economy rate"))
            {
                return;
            }
            try
            {
                BeforeTest();
                int before = _state.Base;
                _state.Base = MacroCatalogue.Clamp(_state.Base + quarters, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost);
                TestDone("the bank rate " + Quarters(before) + " to " + Quarters(_state.Base));
                Notify(new CityTick { RateMoved = _state.Base - before, ShockAnnounced = -1 });
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: raise (or lower) this district's income by 10": the district the player is in, inside its bounds.</summary>
        public void TestDistrict(int tenths)
        {
            if (!TestReady("City economy district"))
            {
                return;
            }
            try
            {
                CityDistrict d = _state.District(NeighborhoodHelper.CurrentNeighborhood) ?? (_shops.Count > 0 ? _shops[0].District : null) ?? (_state.Districts.Count > 0 ? _state.Districts[0] : null);
                if (d == null)
                {
                    TestRefusals.Tell(Logger, "City economy district", TestRefusal.NothingFits);
                    return;
                }
                BeforeTest();
                int before = d.Income;
                d.Income = MacroCatalogue.Clamp(d.Income + tenths, Math.Max(MacroCatalogue.IncomeLeast, d.Start - MacroCatalogue.IncomeBand), Math.Min(MacroCatalogue.IncomeMost, d.Start + MacroCatalogue.IncomeBand));
                TestDone("the income of " + d.Name + " " + CityText.Income(before) + " to " + CityText.Income(d.Income));
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>"Test: put the city's economy back": the state is what it was before the first test tool changed it.</summary>
        public void TestBack()
        {
            if (!TestReady("City economy back"))
            {
                return;
            }
            try
            {
                int version;
                CityState back = _beforeTests != null ? CityStore.Read(_beforeTests, out version) : null;
                if (back == null)
                {
                    _beforeTests = null;
                    TestRefusals.TellKey(Logger, "City economy back", "reallife_macro_test_nothing_back");
                    return;
                }
                back.LastDay = Save.Day;
                back.CarryRent = _state.CarryRent;      // what was really carried since is still owed
                back.CarryWages = _state.CarryWages;
                back.CarryTakings = _state.CarryTakings;
                back.CarryLiving = _state.CarryLiving;
                CityGame.Districts(back);
                _state = back;
                _beforeTests = null;
                CityEconomy.Live = back;
                TestDone("the state from before the tests is back");
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
    }
}
