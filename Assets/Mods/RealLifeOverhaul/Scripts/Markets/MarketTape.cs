using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The market's journal, kept OUTSIDE the save, one for each playthrough (a file of the game's own data folder,
    /// named by the playthrough's id; Markets/MarketGame reads and writes it). It is what makes the market proof against
    /// loading a save:
    ///
    ///   - every market day's chance comes from a secret made fresh when the market first reaches that day, and the
    ///     secret is written here at once, never into the save. So nothing in a save says what tomorrow brings, and a
    ///     day played again after a load (from an older save, after a crash) is played with the same secret and the
    ///     same city as it was the first time: the same prices, the same results. A load cannot re-roll a known outcome;
    ///   - the furthest market time ever reached in the playthrough (the frontier) is written here as the market goes.
    ///     After a load, no order can be placed, changed or cancelled until the market is past it: what was seen before
    ///     the load cannot be traded on;
    ///   - what the city was like each day, and what happened during it, is written here too, so a replayed day answers
    ///     to the same news even if the city does something else this time.
    ///
    /// The file is plain lines, written as the market goes and read once at load:
    ///     T1 id                                the header
    ///     D day secret                         a day's secret (hexadecimal)
    ///     V day n,n,n,...                      the city on that day (MarketEnv, in a fixed order)
    ///     E day phase kind a b                 something that happened, applied after that phase
    ///     F day phase                          the frontier (the last line of these counts)
    /// A line that cannot be read is passed over. Game-free.
    /// </summary>
    public sealed class MarketTape
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly Dictionary<int, ulong> _secrets = new Dictionary<int, ulong>(64);
        private readonly Dictionary<int, MarketEnv> _envs = new Dictionary<int, MarketEnv>(64);
        private readonly List<MarketEvent> _events = new List<MarketEvent>(32);
        private readonly HashSet<int> _traded = new HashSet<int>();   // the days on which a player's trade is an event (a Going Public session)
        private readonly StringBuilder _pending = new StringBuilder(256);
        private readonly StringBuilder _whole = new StringBuilder(4096);   // the whole tape as it is written at a save, kept for the next (its room grows once)
        private readonly List<int> _days = new List<int>(64);              // its days in order, as it is written

        public ulong Id { get; private set; }
        public int FrontierDay { get; private set; }
        public int FrontierPhase { get; private set; }
        public long Frontier => MarketState.Time(FrontierDay, FrontierPhase);
        public int Days => _secrets.Count;

        /// <summary>Lines written since the last <see cref="TakePending"/>, to be appended to the file.</summary>
        public bool HasPending => _pending.Length > 0;

        public MarketTape(ulong id)
        {
            Id = id;
        }

        public bool Secret(int day, out ulong secret)
        {
            return _secrets.TryGetValue(day, out secret);
        }

        public void AddSecret(int day, ulong secret)
        {
            if (_secrets.ContainsKey(day))
            {
                return;
            }
            _secrets[day] = secret;
            _pending.Append("D ").Append(day.ToString(Inv)).Append(' ').Append(secret.ToString("X16", Inv)).Append('\n');
        }

        public bool Env(int day, out MarketEnv env)
        {
            return _envs.TryGetValue(day, out env);
        }

        public void AddEnv(int day, in MarketEnv env)
        {
            if (_envs.ContainsKey(day))
            {
                return;
            }
            _envs[day] = env;
            _pending.Append("V ").Append(day.ToString(Inv)).Append(' ');
            WriteEnv(_pending, env);
            _pending.Append('\n');
        }

        /// <summary>The events recorded for a moment of the market (applied after the phase), into a list.</summary>
        public void EventsAt(int day, int phase, List<MarketEvent> into)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Day == day && _events[i].Phase == phase)
                {
                    into.Add(_events[i]);
                }
            }
        }

        /// <summary>True for a day played in a Going Public session with a trade in it: its trades' moves are the tape's.</summary>
        public bool Traded(int day)
        {
            return _traded.Contains(day);
        }

        /// <summary>A day's secret from the host's tape (a guest that follows it): the host's, in place of whatever was here.</summary>
        public void SetSecret(int day, ulong secret)
        {
            if (_secrets.TryGetValue(day, out ulong had) && had == secret)
            {
                return;
            }
            _secrets[day] = secret;
            _pending.Append("D ").Append(day.ToString(Inv)).Append(' ').Append(secret.ToString("X16", Inv)).Append('\n');
        }

        /// <summary>A day's city from the host's tape, in place of whatever was here.</summary>
        public void SetEnv(int day, in MarketEnv env)
        {
            _envs[day] = env;
            _pending.Append("V ").Append(day.ToString(Inv)).Append(' ');
            WriteEnv(_pending, env);
            _pending.Append('\n');
        }

        /// <summary>
        /// Every event from a moment on, forgotten: a guest that takes the host's market again keeps its tape's past, but what
        /// it played alone after it last followed is not the host's (the host's lines take its place; a day's secret and city
        /// are replaced as they come).
        /// </summary>
        public void Forget(long from)
        {
            _events.RemoveAll(e => MarketState.Time(e.Day, e.Phase) >= from);
            _traded.Clear();
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Kind == MarketInputs.EventTrade)
                {
                    _traded.Add(_events[i].Day);
                }
            }
        }

        /// <summary>The tape from a day on, as lines of the file: what a guest needs to play the host's market from that day.</summary>
        public string From(int day)
        {
            var t = new StringBuilder(256);
            var days = new List<int>(_secrets.Keys);
            days.Sort();
            for (int i = 0; i < days.Count; i++)
            {
                if (days[i] >= day)
                {
                    t.Append("D ").Append(days[i].ToString(Inv)).Append(' ').Append(_secrets[days[i]].ToString("X16", Inv)).Append('\n');
                }
            }
            foreach (KeyValuePair<int, MarketEnv> env in _envs)
            {
                if (env.Key >= day)
                {
                    t.Append("V ").Append(env.Key.ToString(Inv)).Append(' ');
                    WriteEnv(t, env.Value);
                    t.Append('\n');
                }
            }
            for (int i = 0; i < _events.Count; i++)
            {
                MarketEvent e = _events[i];
                if (e.Day >= day)
                {
                    t.Append("E ").Append(e.Day.ToString(Inv)).Append(' ').Append(e.Phase.ToString(Inv)).Append(' ').Append(e.Kind.ToString(Inv)).Append(' ')
                        .Append(e.A.ToString(Inv)).Append(' ').Append(e.B.ToString(Inv)).Append('\n');
                }
            }
            t.Append("F ").Append(FrontierDay.ToString(Inv)).Append(' ').Append(FrontierPhase.ToString(Inv)).Append('\n');
            return t.ToString();
        }

        public void AddEvent(in MarketEvent e)
        {
            _events.Add(e);
            if (e.Kind == MarketInputs.EventTrade)
            {
                _traded.Add(e.Day);
            }
            _pending.Append("E ").Append(e.Day.ToString(Inv)).Append(' ').Append(e.Phase.ToString(Inv)).Append(' ').Append(e.Kind.ToString(Inv)).Append(' ')
                .Append(e.A.ToString(Inv)).Append(' ').Append(e.B.ToString(Inv)).Append('\n');
        }

        /// <summary>The frontier moved on (only ever forwards).</summary>
        public void Reach(int day, int phase, bool write)
        {
            if (MarketState.Time(day, phase) <= Frontier)
            {
                return;
            }
            FrontierDay = day;
            FrontierPhase = phase;
            if (write)
            {
                WriteFrontier();
            }
        }

        public void WriteFrontier()
        {
            _pending.Append("F ").Append(FrontierDay.ToString(Inv)).Append(' ').Append(FrontierPhase.ToString(Inv)).Append('\n');
        }

        /// <summary>The lines to append to the file, and none pending after.</summary>
        public string TakePending()
        {
            string text = _pending.ToString();
            _pending.Length = 0;
            return text;
        }

        /// <summary>The whole tape as a file, compacted: header, secrets, cities, events, the frontier.</summary>
        public string Whole()
        {
            StringBuilder t = _whole;
            t.Length = 0;
            t.Append("T1 ").Append(Id.ToString("X16", Inv)).Append('\n');
            List<int> days = _days;
            days.Clear();
            days.AddRange(_secrets.Keys);
            days.Sort();
            for (int i = 0; i < days.Count; i++)
            {
                t.Append("D ").Append(days[i].ToString(Inv)).Append(' ').Append(_secrets[days[i]].ToString("X16", Inv)).Append('\n');
            }
            List<int> envDays = _days;
            envDays.Clear();
            envDays.AddRange(_envs.Keys);
            envDays.Sort();
            for (int i = 0; i < envDays.Count; i++)
            {
                t.Append("V ").Append(envDays[i].ToString(Inv)).Append(' ');
                WriteEnv(t, _envs[envDays[i]]);
                t.Append('\n');
            }
            for (int i = 0; i < _events.Count; i++)
            {
                MarketEvent e = _events[i];
                t.Append("E ").Append(e.Day.ToString(Inv)).Append(' ').Append(e.Phase.ToString(Inv)).Append(' ').Append(e.Kind.ToString(Inv)).Append(' ')
                    .Append(e.A.ToString(Inv)).Append(' ').Append(e.B.ToString(Inv)).Append('\n');
            }
            t.Append("F ").Append(FrontierDay.ToString(Inv)).Append(' ').Append(FrontierPhase.ToString(Inv)).Append('\n');
            _pending.Length = 0;
            return t.ToString();
        }

        /// <summary>A tape read back from its file; a tape of another id, or none, is an empty tape of this id.</summary>
        public static MarketTape Read(ulong id, string text)
        {
            var tape = new MarketTape(id);
            if (string.IsNullOrEmpty(text))
            {
                return tape;
            }
            string[] lines = text.Split('\n');
            bool ours = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length < 3)
                {
                    continue;
                }
                string[] f = line.Split(' ');
                try
                {
                    switch (f[0])
                    {
                        case "T1":
                            ours = f.Length > 1 && ulong.TryParse(f[1], NumberStyles.HexNumber, Inv, out ulong had) && had == id;
                            break;
                        case "D":
                            if (ours && f.Length > 2 && int.TryParse(f[1], NumberStyles.Integer, Inv, out int day) && ulong.TryParse(f[2], NumberStyles.HexNumber, Inv, out ulong secret)
                                && !tape._secrets.ContainsKey(day))
                            {
                                tape._secrets[day] = secret;
                            }
                            break;
                        case "V":
                            if (ours && f.Length > 2 && int.TryParse(f[1], NumberStyles.Integer, Inv, out int envDay) && !tape._envs.ContainsKey(envDay)
                                && ReadEnv(f[2], out MarketEnv env))
                            {
                                tape._envs[envDay] = env;
                            }
                            break;
                        case "E":
                            if (ours && f.Length > 5)
                            {
                                var e = new MarketEvent { Day = Int(f[1]), Phase = Int(f[2]), Kind = Int(f[3]), A = Int(f[4]), B = Int(f[5]) };
                                tape._events.Add(e);
                                if (e.Kind == MarketInputs.EventTrade)
                                {
                                    tape._traded.Add(e.Day);
                                }
                            }
                            break;
                        case "F":
                            if (ours && f.Length > 2)
                            {
                                int fd = Int(f[1]), fp = Int(f[2]);
                                if (MarketState.Time(fd, fp) > tape.Frontier && fp >= 0 && fp <= MarketCatalogue.PhaseClose)
                                {
                                    tape.FrontierDay = fd;
                                    tape.FrontierPhase = fp;
                                }
                            }
                            break;
                    }
                }
                catch (Exception)
                {
                    // a damaged line is passed over
                }
            }
            return tape;
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, Inv, out int value) ? value : 0;
        }

        // ---------------------------------------------------------------- the city of a day, as numbers

        public static void WriteEnv(StringBuilder t, in MarketEnv e)
        {
            t.Append(e.Cycle.ToString(Inv)).Append(',').Append(e.RateBp.ToString(Inv)).Append(',').Append(e.RateNextBp.ToString(Inv)).Append(',').Append(e.InflationBp.ToString(Inv))
                .Append(',').Append(e.PriceLevel.ToString(Inv)).Append(',').Append(e.DistrictIncome.ToString(Inv)).Append(',').Append(e.Levers.ToString(Inv)).Append(',')
                .Append(e.Campaign.ToString(Inv)).Append(',').Append(e.Elections.ToString(Inv)).Append(',').Append(e.Sky.ToString(Inv)).Append(',').Append(e.Spell.ToString(Inv))
                .Append(',').Append(e.Event.ToString(Inv)).Append(',').Append(e.Crimes.ToString(Inv)).Append(',').Append(e.BreakIns.ToString(Inv)).Append(',')
                .Append(e.Fires.ToString(Inv)).Append(',').Append(e.Accidents.ToString(Inv)).Append(',').Append(e.RivalMood.ToString(Inv));
            for (int k = 0; k < MarketEnv.SupplyKinds; k++)
            {
                t.Append(',').Append((e.Supply != null && k < e.Supply.Length ? e.Supply[k] : 1000).ToString(Inv));
            }
            int trades = e.TradeOf != null && e.TradeSales != null ? Math.Min(e.Trades, Math.Min(e.TradeOf.Length, e.TradeSales.Length)) : 0;
            t.Append(',').Append(trades.ToString(Inv));
            for (int i = 0; i < trades; i++)
            {
                t.Append(',').Append(e.TradeOf[i].ToString(Inv)).Append(',').Append(e.TradeSales[i].ToString(Inv));
            }
            // A player's own company's day (Markets/Listing), only while it has one: a day without is written as before.
            if (e.OwnDays > 0 || e.OwnStanding >= 0 || e.OwnDebt != 0 || e.OwnPress != 0 || e.OwnScandal != 0)
            {
                t.Append(',').Append(e.OwnFrom.ToString(Inv)).Append(',').Append(e.OwnDays.ToString(Inv)).Append(',').Append(e.OwnRev.ToString(Inv)).Append(',')
                    .Append(e.OwnProfit.ToString(Inv)).Append(',').Append(e.OwnStanding.ToString(Inv)).Append(',').Append(e.OwnPress.ToString(Inv)).Append(',')
                    .Append(e.OwnScandal.ToString(Inv)).Append(',').Append(e.OwnDebt.ToString(Inv));
            }
        }

        public static bool ReadEnv(string text, out MarketEnv e)
        {
            e = MarketEnv.Quiet();
            string[] f = text.Split(',');
            if (f.Length < 17 + MarketEnv.SupplyKinds + 1)
            {
                return false;
            }
            e.Cycle = Clamp(Int(f[0]), 0, 3);
            e.RateBp = Clamp(Int(f[1]), -500, 3000);
            e.RateNextBp = Clamp(Int(f[2]), -300, 300);
            e.InflationBp = Clamp(Int(f[3]), -500, 3000);
            e.PriceLevel = Clamp(Int(f[4]), 100, 10000);
            e.DistrictIncome = Clamp(Int(f[5]), 300, 3000);
            e.Levers = Math.Max(0, Int(f[6]));
            e.Campaign = Int(f[7]) != 0 ? 1 : 0;
            e.Elections = Math.Max(0, Int(f[8]));
            e.Sky = Clamp(Int(f[9]), 0, 2);
            e.Spell = Clamp(Int(f[10]), 0, 3);
            e.Event = Clamp(Int(f[11]), 0, 7);
            e.Crimes = Clamp(Int(f[12]), 0, 1000);
            e.BreakIns = Clamp(Int(f[13]), 0, 1000);
            e.Fires = Clamp(Int(f[14]), 0, 1000);
            e.Accidents = Clamp(Int(f[15]), 0, 1000);
            e.RivalMood = Clamp(Int(f[16]), 0, 1000);
            for (int k = 0; k < MarketEnv.SupplyKinds; k++)
            {
                e.Supply[k] = Clamp(Int(f[17 + k]), 300, 3000);
            }
            int at = 17 + MarketEnv.SupplyKinds;
            int trades = Clamp(Int(f[at]), 0, MarketEnv.TradesMost);
            e.Trades = 0;
            for (int i = 0; i < trades && at + 2 + i * 2 < f.Length; i++)
            {
                e.TradeOf[i] = Int(f[at + 1 + i * 2]);
                e.TradeSales[i] = Math.Max(0, Int(f[at + 2 + i * 2]));
                e.Trades++;
            }
            int own = at + 1 + trades * 2;
            if (own + 7 < f.Length)
            {
                e.OwnFrom = Int(f[own]);
                e.OwnDays = Clamp(Int(f[own + 1]), 0, 400);
                e.OwnRev = Math.Max(0L, Long(f[own + 2]));
                e.OwnProfit = Long(f[own + 3]);
                e.OwnStanding = Clamp(Int(f[own + 4]), -1, 100);
                e.OwnPress = Clamp(Int(f[own + 5]), -10000, 10000);
                e.OwnScandal = Clamp(Int(f[own + 6]), 0, 10);
                e.OwnDebt = Math.Max(0L, Long(f[own + 7]));
            }
            return true;
        }

        private static long Long(string text)
        {
            return long.TryParse(text, NumberStyles.Integer, Inv, out long value) ? value : 0L;
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
