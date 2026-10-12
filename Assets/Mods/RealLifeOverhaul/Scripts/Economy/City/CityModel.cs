using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One district: the game's own name for the neighbourhood, where its income started and where it stands.</summary>
    public sealed class CityDistrict
    {
        public string Name;
        public int Start;       // tenths of a point, from the game's own class shares; never changed
        public int Income;      // tenths of a point, 600 to 1400
        public int Before;      // what it was a month ago
    }

    /// <summary>The supply of one kind of goods.</summary>
    public struct SupplyLine
    {
        public int Factor;          // today's price factor in thousandths
        public SupplyShock Shock;   // a shortage or a glut, running or announced for tomorrow
        public int From;            // the model's day it starts on
        public int Days;
        public int Size;            // thousandths, before the strength

        /// <summary>True for a shock that is announced and has not started.</summary>
        public bool Announced(int age) => Shock != SupplyShock.None && From > age;

        /// <summary>True for a shock that is running on this day of the model.</summary>
        public bool Running(int age) => Shock != SupplyShock.None && From <= age && age < From + Days;
    }

    /// <summary>What a day is like outside the model: the season, the weather, what is on in the city.</summary>
    public struct CityDay
    {
        public int Season;          // 0 winter, 1 spring, 2 summer, 3 autumn
        public CityWeather Weather;
        public CityEvent Event;
    }

    /// <summary>What a day's tick did, for whoever applies it.</summary>
    public struct CityTick
    {
        public bool MonthTurned;
        public int RateMoved;       // quarter points, at the turn of a month; 0 if it held
        public bool CycleChanged;
        public int ShockAnnounced;  // the kind of goods a shortage or a glut was drawn for, or -1
    }

    /// <summary>
    /// The city's economy as it stands: everything the save keeps of it (<see cref="CityStore"/>). Game-free.
    /// Every figure is a whole number (see <see cref="MacroCatalogue"/>).
    /// </summary>
    public sealed class CityState
    {
        public int Seed;
        public int Age;             // days the model has counted; a month turns when it is a multiple of the month's days
        public int LastDay;         // the game's day last counted
        public CycleState Cycle = CycleState.Steady;
        public int MonthsLeft = MacroCatalogue.OpeningSteadyMonths;
        public int MonthsIn = 1;    // the month of this state it is in, from 1
        public int Base;            // quarter points on the banks' own rates
        public int BaseBefore;      // what it was the month before: what inflation feels (the lag)
        public bool Pending;        // a decision is announced for tomorrow
        public int PendingMove;
        public RateReason PendingReason;
        public int LastMove;
        public RateReason LastReason;
        public int Index = MacroCatalogue.IndexStart;
        public int Inflation = MacroCatalogue.InflationTarget;
        public int Pressure;
        public int MonthChange;     // the last month's rise in prices, hundredths of a per cent
        public int CarryRent, CarryWages, CarryTakings;   // adjustments that could not be taken, carried to the next day
        public int CarryLiving;                           // ...and of the game's own shops and services
        public int LastCopycatDay;
        public int Buys;            // the kinds of goods the player's businesses stock, a bit each (the last look)
        public readonly List<CityDistrict> Districts = new List<CityDistrict>(12);
        public readonly SupplyLine[] Supply = new SupplyLine[MacroCatalogue.SupplyKinds];
        public readonly List<CityNews> News = new List<CityNews>(8);

        public CityState()
        {
            for (int k = 0; k < Supply.Length; k++)
            {
                Supply[k].Factor = 1000;
            }
        }

        public CityDistrict District(string name)
        {
            for (int i = 0; i < Districts.Count; i++)
            {
                if (string.Equals(Districts[i].Name, name, StringComparison.Ordinal))
                {
                    return Districts[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// How the city's economy moves. Game-free and exact: nothing is random but numbers drawn from the save's seed, the
    /// purpose and the day or month they are for, so the same seed and the same days give the same history
    /// (RealLifeOverhaul.Build/EconomyCheck runs twenty years of it from many seeds).
    ///
    /// ONE DAY (<see cref="Day"/>): a month turns if it is time (<see cref="Month"/>); a shortage or a glut that has run
    /// its days ends; today's price factors are worked out from the season, the weather, the city's events and what is
    /// running; a shortage or a glut may be drawn for tomorrow; and on the last day of a month the central bank
    /// decides, for tomorrow (<see cref="Decide"/>). So a decision and a shock are both known, and in the paper, a day
    /// before they take effect.
    ///
    /// ONE MONTH: the decision announced yesterday takes effect; the cycle counts a month and may change state;
    /// inflation moves towards where the cycle, a slow pressure, the month's surprise and the rate of the month before
    /// push it, and the index rises by a twelfth of it; each district drifts.
    /// </summary>
    public static class CityModel
    {
        private const int PurposeCycle = 11, PurposeMonths = 12, PurposeJitter = 13, PurposePressure = 21, PurposeShock = 22,
            PurposeDistrict = 31, PurposeSupply = 41, PurposeSupplyKind = 42, PurposeSupplyWhich = 43, PurposeSupplySize = 44, PurposeSupplyDays = 45;

        /// <summary>A whole number from 0 to just under <paramref name="range"/> that is always the same for a seed, a purpose and a count.</summary>
        public static int Draw(int seed, int purpose, int n, int range)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)purpose * 2246822519u ^ (uint)n * 3266489917u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return range <= 1 ? 0 : (int)(h % (uint)range);
            }
        }

        // A whole number between two bounds, both included.
        private static int Between(int seed, int purpose, int n, int least, int most)
        {
            return least + Draw(seed, purpose, n, most - least + 1);
        }

        /// <summary>A figure's swing at a strength: a per cent of it, rounded towards nothing.</summary>
        public static int Swung(int value, int level)
        {
            return value * MacroCatalogue.Swing(level) / 100;
        }

        // ---------------------------------------------------------------- opening

        /// <summary>A new economy for a save: steady for its first three months, the rate the banks' own, prices at 100.</summary>
        public static CityState Open(int seed, int gameDay)
        {
            return new CityState { Seed = seed, LastDay = gameDay };
        }

        /// <summary>A district's starting income from the game's class shares; 100 where the game gives none.</summary>
        public static int StartIncome(int working, int middle, int upper)
        {
            int all = Math.Max(0, working) + Math.Max(0, middle) + Math.Max(0, upper);
            if (all <= 0)
            {
                return MacroCatalogue.IncomeMiddle;
            }
            int tenths = (Math.Max(0, working) * MacroCatalogue.ClassWorking + Math.Max(0, middle) * MacroCatalogue.ClassMiddle + Math.Max(0, upper) * MacroCatalogue.ClassUpper) * 10 / all;
            return MacroCatalogue.Clamp(tenths, MacroCatalogue.IncomeLeast, MacroCatalogue.IncomeMost);
        }

        /// <summary>Makes sure the state has this district; one the save already knows keeps its income and takes the start given.</summary>
        public static void Know(CityState state, string name, int start)
        {
            CityDistrict d = state.District(name);
            if (d == null)
            {
                state.Districts.Add(new CityDistrict { Name = name, Start = start, Income = start, Before = start });
                return;
            }
            d.Start = start;
            d.Income = BandIncome(d.Income, start);
            d.Before = BandIncome(d.Before, start);
        }

        private static int BandIncome(int income, int start)
        {
            int least = Math.Max(MacroCatalogue.IncomeLeast, start - MacroCatalogue.IncomeBand);
            int most = Math.Min(MacroCatalogue.IncomeMost, start + MacroCatalogue.IncomeBand);
            return MacroCatalogue.Clamp(income, least, most);
        }

        // ---------------------------------------------------------------- a day

        /// <summary>The month of the model a state is in, from 0.</summary>
        public static int MonthNumber(CityState state, int monthDays)
        {
            return state.Age / Math.Max(1, monthDays);
        }

        /// <summary>Days until the central bank's next decision takes effect (the next turn of a month), 1 at least.</summary>
        public static int DaysToTurn(CityState state, int monthDays)
        {
            monthDays = Math.Max(1, monthDays);
            return monthDays - state.Age % monthDays;
        }

        /// <summary>One game day. <paramref name="gameDay"/> is the day that has begun; the pieces for its paper are added to the state.</summary>
        public static CityTick Day(CityState state, int gameDay, CityDay env, int level, int monthDays)
        {
            var tick = new CityTick { ShockAnnounced = -1 };
            monthDays = Math.Max(MacroCatalogue.MonthDaysLeast, monthDays);
            state.Age++;
            state.LastDay = gameDay;
            for (int i = state.News.Count - 1; i >= 0; i--)
            {
                if (state.News[i].Day < gameDay)
                {
                    state.News.RemoveAt(i);
                }
            }
            if (state.Cycle == CycleState.Recession && !MacroCatalogue.Recessions(level))
            {
                // The strength was set to Relaxed in a recession: it is over now.
                CycleState was = state.Cycle;
                state.Cycle = CycleState.Slowdown;
                state.MonthsIn = 1;
                state.MonthsLeft = Math.Max(1, state.MonthsLeft);
                tick.CycleChanged = true;
                state.News.Add(new CityNews { Day = gameDay, Kind = CityNewsKind.Cycle, A = (int)state.Cycle, B = (int)was });
            }
            if (state.Age % monthDays == 0)
            {
                Month(state, gameDay, level, monthDays, ref tick);
            }
            Supply(state, gameDay, env, level, ref tick);
            if ((state.Age + 1) % monthDays == 0)
            {
                Decide(state);
                state.News.Add(new CityNews { Day = gameDay, Kind = CityNewsKind.RateDecision, A = state.PendingMove, B = (int)state.PendingReason });
            }
            return tick;
        }

        // ---------------------------------------------------------------- the central bank

        /// <summary>
        /// The decision for next month, from this month's inflation and the state of the cycle. It raises when
        /// inflation is over the target's band and cuts when it is under; a slowdown or a recession takes a quarter or
        /// a half point off whatever that gives; with nothing pressing, the rate goes a quarter point back towards the
        /// banks' own. One point at most, and never outside -2 to +6.
        /// </summary>
        public static void Decide(CityState state)
        {
            RateReason reason;
            int move = Rule(state.Inflation, state.Cycle, state.Base, out reason);
            state.Pending = true;
            state.PendingMove = move;
            state.PendingReason = move == 0 ? RateReason.Hold : reason;
        }

        /// <summary>The rule itself: the move in quarter points for an inflation, a state and a rate.</summary>
        public static int Rule(int inflation, CycleState cycle, int baseNow, out RateReason reason)
        {
            int gap = inflation - MacroCatalogue.InflationTarget;
            int move = 0;
            reason = RateReason.Hold;
            if (gap > MacroCatalogue.InflationBand)
            {
                move = Math.Max(1, gap / 100 * MacroCatalogue.MovePerGapPoint);
                reason = RateReason.PricesHigh;
            }
            else if (gap < -MacroCatalogue.InflationBand)
            {
                move = -Math.Max(1, -gap / 100 * MacroCatalogue.MovePerGapPoint);
                reason = RateReason.PricesLow;
            }
            if (cycle == CycleState.Slowdown || cycle == CycleState.Recession)
            {
                move += cycle == CycleState.Recession ? MacroCatalogue.MoveRecession : MacroCatalogue.MoveSlowdown;
                if (move < 0 && reason != RateReason.PricesLow)
                {
                    reason = cycle == CycleState.Recession ? RateReason.Recession : RateReason.Slowdown;
                }
            }
            else if (move == 0 && baseNow != MacroCatalogue.BaseStart)
            {
                move = baseNow > MacroCatalogue.BaseStart ? -MacroCatalogue.MoveNormal : MacroCatalogue.MoveNormal;
                reason = RateReason.Normal;
            }
            move = MacroCatalogue.Clamp(move, -MacroCatalogue.MoveMost, MacroCatalogue.MoveMost);
            move = MacroCatalogue.Clamp(baseNow + move, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost) - baseNow;
            if (move == 0)
            {
                reason = RateReason.Hold;
            }
            return move;
        }

        // ---------------------------------------------------------------- a month

        private static void Month(CityState state, int gameDay, int level, int monthDays, ref CityTick tick)
        {
            int month = state.Age / monthDays;
            tick.MonthTurned = true;

            // Yesterday's decision takes effect. What inflation feels this month is the rate of the month before last.
            int lag = state.BaseBefore;
            state.BaseBefore = state.Base;
            state.LastMove = 0;
            state.LastReason = RateReason.Hold;
            if (state.Pending)
            {
                int now = MacroCatalogue.Clamp(state.Base + state.PendingMove, MacroCatalogue.BaseLeast, MacroCatalogue.BaseMost);
                state.LastMove = now - state.Base;
                state.LastReason = state.PendingReason;
                state.Base = now;
                state.Pending = false;
                state.PendingMove = 0;
                state.PendingReason = RateReason.Hold;
                tick.RateMoved = state.LastMove;
            }

            // The cycle.
            state.MonthsLeft--;
            state.MonthsIn++;
            if (state.MonthsLeft <= 0)
            {
                CycleState was = state.Cycle;
                state.Cycle = Next(state.Seed, was, month, level);
                state.MonthsLeft = Between(state.Seed, PurposeMonths, month, MacroCatalogue.StateMonthsLeast, MacroCatalogue.StateMonthsMost);
                if (state.Cycle != was)
                {
                    state.MonthsIn = 1;
                    tick.CycleChanged = true;
                    state.News.Add(new CityNews { Day = gameDay, Kind = CityNewsKind.Cycle, A = (int)state.Cycle, B = (int)was });
                }
            }

            // Prices.
            state.Pressure = MacroCatalogue.Clamp(state.Pressure - state.Pressure * MacroCatalogue.PressureFade / 100
                                                 + Between(state.Seed, PurposePressure, month, -MacroCatalogue.PressureStep, MacroCatalogue.PressureStep),
                MacroCatalogue.PressureLeast, MacroCatalogue.PressureMost);
            int surprise = Between(state.Seed, PurposeShock, month, MacroCatalogue.ShockLeast, MacroCatalogue.ShockMost);
            int pushed = MacroCatalogue.InflationTarget + Swung(MacroCatalogue.CyclePush[(int)state.Cycle] + state.Pressure + surprise, level) - MacroCatalogue.RateEffectPerQuarter * lag;
            state.Inflation = MacroCatalogue.Clamp(state.Inflation + (pushed - state.Inflation) * MacroCatalogue.InflationFollow / 100, MacroCatalogue.InflationLeast, MacroCatalogue.InflationMost);
            state.MonthChange = Rounded(state.Inflation, MacroCatalogue.MonthsPerYear);
            state.Index = MacroCatalogue.Clamp((int)(state.Index + (long)state.Index * state.MonthChange / 10000L), MacroCatalogue.IndexLeast, MacroCatalogue.IndexMost);
            state.News.Add(new CityNews { Day = gameDay, Kind = CityNewsKind.Prices, A = state.Index, B = state.MonthChange });

            // The districts: the one that moved most makes the paper, if it moved enough.
            CityDistrict most = null;
            for (int i = 0; i < state.Districts.Count; i++)
            {
                CityDistrict d = state.Districts[i];
                d.Before = d.Income;
                int noise = Between(state.Seed, PurposeDistrict + i * 97, month, -MacroCatalogue.IncomeNoise, MacroCatalogue.IncomeNoise);
                int pull = (d.Start - d.Income) * MacroCatalogue.IncomePull / 100;
                d.Income = BandIncome(d.Income + Swung(MacroCatalogue.IncomeDrift[(int)state.Cycle] + noise, level) + pull, d.Start);
                if (most == null || Math.Abs(d.Income - d.Before) > Math.Abs(most.Income - most.Before))
                {
                    most = d;
                }
            }
            if (most != null && Math.Abs(most.Income - most.Before) >= MacroCatalogue.IncomeNews)
            {
                state.News.Add(new CityNews { Day = gameDay, Kind = CityNewsKind.District, A = most.Income - most.Before, B = most.Income, Name = most.Name });
            }
        }

        // a / b to the nearest whole number, halves away from nothing
        private static int Rounded(int a, int b)
        {
            return a >= 0 ? (a + b / 2) / b : -((-a + b / 2) / b);
        }

        /// <summary>The state that follows another, by the save's own table. Relaxed never enters a recession: where the table says one, the slowdown goes on.</summary>
        public static CycleState Next(int seed, CycleState from, int month, int level)
        {
            int row = (int)from;
            int[] chances = MacroCatalogue.Transition[row];
            int[] pair = MacroCatalogue.JitterPair[row];
            int jitter = Between(seed, PurposeJitter, row, -MacroCatalogue.TransitionJitter, MacroCatalogue.TransitionJitter);
            int roll = Draw(seed, PurposeCycle, month, 100);
            int sum = 0;
            CycleState next = from;
            for (int to = 0; to < chances.Length; to++)
            {
                int chance = chances[to] + (to == pair[0] ? jitter : to == pair[1] ? -jitter : 0);
                if (chance <= 0)
                {
                    continue;
                }
                sum += chance;
                next = (CycleState)to;
                if (roll < sum)
                {
                    break;
                }
            }
            return next == CycleState.Recession && !MacroCatalogue.Recessions(level) ? CycleState.Slowdown : next;
        }

        // ---------------------------------------------------------------- supply

        private static void Supply(CityState state, int gameDay, CityDay env, int level, ref CityTick tick)
        {
            int season = MacroCatalogue.Clamp(env.Season, 0, 3);
            int weather = MacroCatalogue.Clamp((int)env.Weather, 0, MacroCatalogue.WeatherPush[0].Length - 1);
            int what = MacroCatalogue.Clamp((int)env.Event, 0, MacroCatalogue.EventPush[0].Length - 1);
            int busy = 0;
            bool announced = false;
            for (int k = 0; k < state.Supply.Length; k++)
            {
                ref SupplyLine line = ref state.Supply[k];
                if (line.Shock != SupplyShock.None && state.Age >= line.From + line.Days)
                {
                    line.Shock = SupplyShock.None;
                    line.From = line.Days = line.Size = 0;
                }
                int push = MacroCatalogue.SeasonPush[k][season] + MacroCatalogue.WeatherPush[k][weather] + MacroCatalogue.EventPush[k][what];
                if (line.Running(state.Age))
                {
                    push += line.Shock == SupplyShock.Shortage ? line.Size : -line.Size;
                }
                line.Factor = MacroCatalogue.Clamp(1000 + Swung(push, level), MacroCatalogue.FactorLeast, MacroCatalogue.FactorMost);
                if (line.Shock != SupplyShock.None)
                {
                    busy++;
                    announced |= line.Announced(state.Age);
                }
            }
            // At most one is drawn a day, for tomorrow, and never while two are already running or announced.
            if (announced || busy >= MacroCatalogue.ShocksAtOnce || Draw(state.Seed, PurposeSupply, state.Age, 1000) >= MacroCatalogue.ShockChance)
            {
                return;
            }
            int kind = Draw(state.Seed, PurposeSupplyWhich, state.Age, MacroCatalogue.SupplyKinds);
            if (state.Supply[kind].Shock != SupplyShock.None)
            {
                return;
            }
            bool shortage = Draw(state.Seed, PurposeSupplyKind, state.Age, 100) < MacroCatalogue.ShockShortageShare;
            Announce(state, gameDay, kind, shortage ? SupplyShock.Shortage : SupplyShock.Glut,
                shortage ? Between(state.Seed, PurposeSupplySize, state.Age, MacroCatalogue.ShortageLeast, MacroCatalogue.ShortageMost)
                    : Between(state.Seed, PurposeSupplySize, state.Age, MacroCatalogue.GlutLeast, MacroCatalogue.GlutMost),
                Between(state.Seed, PurposeSupplyDays, state.Age, MacroCatalogue.ShockDaysLeast, MacroCatalogue.ShockDaysMost), level);
            tick.ShockAnnounced = kind;
        }

        /// <summary>A shortage or a glut of a kind of goods from tomorrow, with its piece for today's paper. (Also what the test tools call.)</summary>
        public static void Announce(CityState state, int gameDay, int kind, SupplyShock shock, int size, int days, int level)
        {
            ref SupplyLine line = ref state.Supply[kind];
            line.Shock = shock;
            line.From = state.Age + 1;
            line.Days = MacroCatalogue.Clamp(days, MacroCatalogue.ShockDaysLeast, MacroCatalogue.ShockDaysMost);
            line.Size = shock == SupplyShock.Shortage ? MacroCatalogue.Clamp(size, MacroCatalogue.ShortageLeast, MacroCatalogue.ShortageMost)
                : MacroCatalogue.Clamp(size, MacroCatalogue.GlutLeast, MacroCatalogue.GlutMost);
            for (int i = state.News.Count - 1; i >= 0; i--)
            {
                if (state.News[i].Kind == CityNewsKind.Supply && state.News[i].A == kind)
                {
                    state.News.RemoveAt(i);
                }
            }
            state.News.Add(new CityNews { Day = gameDay, Kind = CityNewsKind.Supply, A = kind, B = (int)shock, C = line.Days, D = ShockPercent(line, level) });
        }

        /// <summary>What a shock does to its kind's prices at a strength, in whole per cent (a glut's is under nothing).</summary>
        public static int ShockPercent(in SupplyLine line, int level)
        {
            int size = Swung(line.Size, level);
            return line.Shock == SupplyShock.None ? 0 : Rounded(line.Shock == SupplyShock.Shortage ? size : -size, 10);
        }

        // ---------------------------------------------------------------- what the state means

        /// <summary>The rate in points on top of the banks' own.</summary>
        public static float BasePoints(CityState state)
        {
            return state.Base / (float)MacroCatalogue.QuartersPerPoint;
        }

        /// <summary>What a customer spends against the ordinary, in thousandths, at a strength.</summary>
        public static int Spend(CycleState cycle, int level)
        {
            return 1000 + Swung(MacroCatalogue.StateSpend[(int)cycle] - 1000, level);
        }

        /// <summary>The chance of the mod's own business troubles against the ordinary, in thousandths, at a strength.</summary>
        public static int Trouble(CycleState cycle, int level)
        {
            return 1000 + Swung(MacroCatalogue.StateTrouble[(int)cycle] - 1000, level);
        }

        /// <summary>What a property is worth against the game's own figure, in thousandths: the cycle, the district against its start, and the price index.</summary>
        public static int Property(CityState state, CityDistrict district, int level)
        {
            long value = 1000 + Swung(MacroCatalogue.StateProperty[(int)state.Cycle] - 1000, level);
            if (district != null)
            {
                value = value * (1000 + (district.Income - district.Start) * MacroCatalogue.PropertyPerPoint / 10) / 1000;
            }
            return (int)(value * state.Index / MacroCatalogue.IndexStart);
        }

        /// <summary>What a district bears against the game's own price for it, in thousandths: its income against its start, and what customers spend.</summary>
        public static int Tolerance(CityState state, CityDistrict district, int level)
        {
            int income = district != null ? 1000 + (district.Income - district.Start) * MacroCatalogue.TolerancePerPoint / 10 : 1000;
            return income * Spend(state.Cycle, level) / 1000;
        }

        /// <summary>A business's prices against what its district bears, in thousandths (1000: exactly what it bears), from its prices against the game's own.</summary>
        public static int Against(int priceRatio, int tolerance)
        {
            return tolerance <= 0 ? priceRatio : (int)((long)priceRatio * 1000 / tolerance);
        }

        /// <summary>True for prices the district does not bear.</summary>
        public static bool Over(int against)
        {
            return against > 1000 + MacroCatalogue.OverBand;
        }

        /// <summary>Customers lost or gained for prices, in per cent: nothing lost up to 2% over, 0.8% for each per cent over after that, 10% at most; 2% gained at or under.</summary>
        public static int PriceFootfall(int against)
        {
            if (against <= 0)
            {
                return 0;   // no prices to judge by
            }
            if (against <= 1000)
            {
                return MacroCatalogue.UnderGain;
            }
            if (!Over(against))
            {
                return 0;
            }
            return -Math.Min(MacroCatalogue.OverLossMost, (against - 1000) * MacroCatalogue.OverLossPerPercent / 10000);
        }

        /// <summary>The cycle's part of a business's footfall, in per cent of customers.</summary>
        public static int CycleFootfall(CycleState cycle, int level)
        {
            return Swung(MacroCatalogue.StateFootfall[(int)cycle], level);
        }

        /// <summary>The district's part: a tenth of a per cent for each point its income is away from 100.</summary>
        public static int DistrictFootfall(CityDistrict district)
        {
            return district == null ? 0 : Rounded((district.Income - MacroCatalogue.IncomeMiddle) * MacroCatalogue.FootfallPerPoint, 10000);
        }

        /// <summary>The three parts together, kept inside what the city's economy may do; a change too small to matter is none.</summary>
        public static int Footfall(int cycle, int district, int price)
        {
            int all = MacroCatalogue.Clamp(cycle + district + price, MacroCatalogue.FootfallLeast, MacroCatalogue.FootfallMost);
            return Math.Abs(all) < MacroCatalogue.FootfallQuiet ? 0 : all;
        }

        /// <summary>What the game's wholesale price of a kind of goods is multiplied by, in thousandths: supply and the price index.</summary>
        public static int Wholesale(CityState state, int kind)
        {
            return (int)((long)state.Supply[kind].Factor * state.Index / MacroCatalogue.IndexStart);
        }

        /// <summary>What rents and wages cost against the game's own figures, in thousandths: the price index.</summary>
        public static int Costs(CityState state)
        {
            return state.Index / (MacroCatalogue.IndexStart / 1000);
        }

        /// <summary>What takings come to against the game's own figure, in thousandths: the price index and what customers spend.</summary>
        public static int Takings(CityState state, int level)
        {
            return (int)((long)Costs(state) * Spend(state.Cycle, level) / 1000);
        }
    }

    /// <summary>
    /// The arithmetic of an adjustment (rents, wages, takings): the game's own figure for the day times how far the
    /// mod's factor is from one. Nothing within 1% of one; whole money, halves away from nothing; a charge is never
    /// taken beyond the cash in hand, and what could not be taken is carried to the next day as it is. Game-free.
    /// </summary>
    public static class CityAdjust
    {
        /// <summary>The adjustment of a figure by a factor in thousandths; more than nothing means the figure grew.</summary>
        public static int Of(float figure, int factor)
        {
            if (figure <= 0f || Math.Abs(factor - 1000) < MacroCatalogue.AdjustQuiet)
            {
                return 0;
            }
            double exact = (double)figure * (factor - 1000) / 1000.0;
            double whole = Math.Round(exact, MidpointRounding.AwayFromZero);
            return whole > int.MaxValue / 2 ? int.MaxValue / 2 : whole < int.MinValue / 2 ? int.MinValue / 2 : (int)whole;
        }

        /// <summary>
        /// What is taken now of a charge and of what was carried, given the cash in hand; the rest is carried.
        /// A charge of less than nothing (a refund) first pays off what was carried, and is returned as less than nothing.
        /// </summary>
        public static int Take(int charge, int cash, ref int carried)
        {
            long want = (long)Math.Max(0, carried) + charge;
            if (want <= 0)
            {
                carried = 0;
                return (int)Math.Max(want, int.MinValue / 2);
            }
            int take = (int)Math.Min(want, Math.Max(0, cash));
            carried = (int)Math.Min(want - take, int.MaxValue / 2);
            return take;
        }
    }
}
