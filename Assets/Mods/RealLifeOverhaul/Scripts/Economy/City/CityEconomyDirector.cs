using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Entities;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The city's economy (docs/features/economy-city.md), applied to the bank rate, wholesale prices, footfall and the day's
    /// rent, wages and takings. No Update, so nothing per frame; a fault switches it off for the session, every figure put back.
    /// </summary>
    public sealed partial class CityEconomyDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static CityEconomyDirector Instance { get; private set; }

        private readonly List<CityShop> _shops = new List<CityShop>(16);
        private readonly List<CityShop> _pool = new List<CityShop>(16);
        // The businesses whose figure carried the city's part before this pass.
        private readonly HashSet<BuildingRegistration> _was = new HashSet<BuildingRegistration>(ReferenceComparer<BuildingRegistration>.Instance);
        private readonly Dictionary<BuildingRegistration, CityShop> _byReg = new Dictionary<BuildingRegistration, CityShop>(16, ReferenceComparer<BuildingRegistration>.Instance);

        private CityState _state;      // never null while running
        private string _beforeTests;   // the state's line before the first test tool changed it; null with no test change in place
        private bool _running;
        private bool _broken;          // a fault: off for the session, never running again
        private bool _holding;         // the footfall figures are the game's own for a save, until the next game hour
        private int _level;
        private int _monthDays = MacroCatalogue.MonthDaysLeast;
        private Action _second, _onSave, _onHour, _onDay;
        private int _followedDay;      // a guest: the last day its own part of the day was done
        private bool _heardHost;       // a guest: the host's state has been applied since it began to follow
        private int _eveTold;          // the day the evening notice of tomorrow's rate decision was last sent

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
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            EconomyDirector.Follow(StartSeats.CityEconomy, _second);   // its first second after a load has a frame of its own (Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
            SharedCity.Register(new SharedCityPart
            {
                Id = 'E', Name = "economy",
                Ready = () => _running,
                Write = () => CityShare.Write(_state),
                Apply = ApplyHost,
                Left = () =>
                {
                    _heardHost = false;
                    Logger?.Info("City economy: the host's city is no longer followed; the model counts its days again from day " + (_state != null ? _state.LastDay : 0)
                                 + ", the host's last.");
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
            // Nothing is written to the save here: that happens when the game saves.
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

        private static readonly int HotSecond = HotSpots.Slot("Economy: CityEconomyDirector.Second");
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
            _eveTold = save.modData.TryGetValue(CityStore.EveKey, out string eve) && int.TryParse(eve, NumberStyles.Integer, CultureInfo.InvariantCulture, out int eveDay)
                ? eveDay
                : 0;
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
                Logger?.Warn("City economy: figures of an earlier session were still in memory and have been put back (" + put + " wholesale price(s)"
                             + (rateClean ? string.Empty : ", the bank rate") + "). " + detail + ".");
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
            int days = GameCalendar.DaysPerYear;
            return days >= 8 ? days : 60;
        }

        // Every figure is the game's own again; with 'keep' the state is written to the save's data first.
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (keep && was && Save != null)
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
                            WriteFootfall(_shops[i], true, false);   // nothing is live now, so the figure is written without the city's part
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
                             + (rate ? "back at" : "already at") + " the banks' own. " + detail + ": "
                             + (ok && LendingTerms.BasePoints == 0f && AccountRules.BaseBp == 0 ? "PASS" : "FAIL"));
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
            Logger?.Warn("City economy: switched off for this session after an error in " + where
                         + ". The banks' rates, the wholesale prices and every business's footfall are the game's own again; the game's daily run and the credit rating are not affected.");
        }

        private static readonly int HotOnHour = HotSpots.Slot("Economy: CityEconomyDirector.OnHour");
        // Every game hour, and first thing in the game's NewDay before its daily run; after the credit side's own work.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || Save == null)
            {
                return;
            }
            try
            {
                _holding = false;
                KeepRate();
                int done = SharedCity.Follows('E') ? Math.Max(_followedDay, _state.LastDay) : _state.LastDay;   // a guest's own part of the day is its own
                if (Save.Day > done)
                {
                    return;   // a new day: the game's run writes every figure afresh in a moment, and the day event follows
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
        // After the game's daily run (rent, wages and takings booked, the day's customers drawn) and the credit side's day.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || Save == null)
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
                    // A guest: the city's day comes with the host's next update; the player's own part is done here.
                    _followedDay = day;
                    ApplyAll(day, true, true, "the day (the host's city)");
                    Adjust(day - 1);
                    Rivals(day);
                    return;
                }
                if (_state.Districts.Count == 0)
                {
                    CityGame.Districts(_state);   // the game's neighbourhoods were not there at the start
                }
                var all = new CityTick { ShockAnnounced = -1 };
                int from = Math.Max(_state.LastDay + 1, day - _monthDays + 1);   // a jump of many days counts a month of them at most
                for (int d = from; d <= day; d++)
                {
                    int rateBefore = _state.Base, felt = _state.BaseBefore, indexBefore = _state.Index, inflationBefore = _state.Inflation;
                    CycleState cycleBefore = _state.Cycle;
                    CityTick tick = CityModel.Day(_state, d, DayOf(d), _level, _monthDays);
                    all.MonthTurned |= tick.MonthTurned;
                    all.CycleChanged |= tick.CycleChanged;
                    all.RateMoved += tick.RateMoved;
                    all.ShockAnnounced = tick.ShockAnnounced >= 0 ? tick.ShockAnnounced : all.ShockAnnounced;
                    if (Verbose && tick.MonthTurned)
                    {
                        Logger?.Info("Economy check (city, month " + CityModel.MonthNumber(_state, _monthDays) + " on day " + d + "): the rate " + Quarters(rateBefore) + " to "
                                     + Quarters(_state.Base) + " (" + _state.LastReason + "), inflation " + PerCent(inflationBefore) + "% to " + PerCent(_state.Inflation)
                                     + "% a year (pressure " + PerCent(_state.Pressure) + ", the rate it felt " + Quarters(felt) + "), the index "
                                     + CityText.Index(indexBefore) + " to " + CityText.Index(_state.Index) + ", the cycle " + cycleBefore + " to " + _state.Cycle
                                     + " (month " + _state.MonthsIn + ", " + _state.MonthsLeft + " left): PASS");
                    }
                    if (Verbose && _state.Pending && CityModel.DaysToTurn(_state, _monthDays) == 1)
                    {
                        Logger?.Info("Economy check (city, decision on day " + d + "): inflation " + PerCent(_state.Inflation) + "% against a target of "
                                     + PerCent(MacroCatalogue.InflationTarget) + "%, the cycle " + _state.Cycle + ", the rate " + Quarters(_state.Base) + ": "
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
                SharedCity.Changed('E');   // the guests have the day with the next update
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private static CityDay DayOf(int day)
        {
            return CityBridges.Day != null ? CityBridges.Day(day) : new CityDay { Season = 1 };
        }

        // A guest takes the host's state; the player hears of what changes, but not of the differences found on joining.
        private bool ApplyHost(string text, bool whole)
        {
            if (!_running)
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
                _beforeTests = null;   // a test's state from before is this player's no more
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

        private static string PerCent(int hundredths)
        {
            return (hundredths / 100f).ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Factor(int thousandths)
        {
            return (thousandths / 1000f).ToString("0.000", CultureInfo.InvariantCulture);
        }

        // The registration holding a footfall figure is saved, so the figures are the game's own until the next game hour.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null)
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
                    Logger?.Info("Economy check (city, before a save): " + put
                                 + " footfall figure(s) put at the game's own until the next game hour; the wholesale prices and the banks' settings are not part of a save: PASS");
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
            save.modData[CityStore.EveKey] = _eveTold.ToString(CultureInfo.InvariantCulture);
            if (_beforeTests != null)
            {
                save.modData[CityStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(CityStore.TestKey);
            }
        }

        // The footfall writes are one batch: each business is drawn again once, spread over the next frames (Core/CustomerDraw).
        private void ApplyAll(int day, bool redraw, bool newDay, string when)
        {
            float started = Time.realtimeSinceStartup;
            bool rate = KeepRate();
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
                    // Also where something was and is no more; on a new day the game has just written every figure afresh.
                    if (shop.Wanted != 0 || _was.Contains(shop.Reg))
                    {
                        WriteFootfall(shop, redraw, newDay);
                        written++;
                        redrawn += redraw && shop.HasFigure ? 1 : 0;
                    }
                    if (log != null && i < 6)
                    {
                        log.Append(i > 0 ? "; " : string.Empty).Append(shop.Reg.BusinessName).Append(' ').Append(CityText.Percent(shop.Wanted)).Append(" (cycle ").Append(shop.Cycle)
                            .Append(", district ").Append(shop.Area).Append(", prices ").Append(shop.Price)
                            .Append(shop.Against > 0 ? " at " + (shop.Against / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "% of what the district bears" : ", none to judge by")
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
                    string kind = CityText.KindKeys[k];
                    factors.Append(k > 0 ? ", " : string.Empty).Append(kind.Substring(kind.LastIndexOf('_') + 1)).Append(' ').Append(Factor(CityModel.Wholesale(_state, k)));
                }
                Logger?.Info("Economy check (city, applied, " + when + ", day " + day + "): the rate " + Quarters(_state.Base) + (rate ? " (changed)" : string.Empty) + ", " + prices
                             + " wholesale price(s) written (" + factors + "), " + written + " footfall figure(s) written, " + redrawn
                             + " with the day's customers asked to be drawn again (the customers' line says how many were)"
                             + (log.Length > 0 ? ": " + log : string.Empty) + (_shops.Count > 6 ? "; and " + (_shops.Count - 6) + " more" : string.Empty) + ". " + detail + "; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: " + (ok ? "PASS" : "FAIL"));
            }
        }

        // The two numbers the credit side reads are what the model says. True if either changed.
        private bool KeepRate()
        {
            float points = CityModel.BasePoints(_state);
            float multiplier = GameLoans.Multiplier;
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

        /// <summary>What the city's economy adds to a business's footfall now, in per cent of customers; asked on every figure the business feature writes.</summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            CityEconomyDirector d = Instance;
            if (d == null || !d._running || d._holding || reg == null)
            {
                return 0;
            }
            return d._byReg.TryGetValue(reg, out CityShop shop) ? shop.Wanted : 0;
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

        private string Summary()
        {
            return "The economy is " + _state.Cycle + " (month " + _state.MonthsIn + ", " + _state.MonthsLeft + " left), the bank rate " + Quarters(_state.Base)
                   + " on the banks' own, inflation " + PerCent(_state.Inflation) + "% a year, the price index " + CityText.Index(_state.Index) + ", strength " + _level + ".";
        }

        /// <summary>For the save tools: every figure is the game's own again and what was carried is written off; the timer starts it again if the option is on.</summary>
        public void StopAll()
        {
            try
            {
                if (_running)
                {
                    _state.CarryRent = _state.CarryWages = _state.CarryTakings = _state.CarryLiving = 0;
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
    }
}
