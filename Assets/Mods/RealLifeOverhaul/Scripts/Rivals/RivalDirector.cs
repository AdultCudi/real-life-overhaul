using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>
    /// The rival tycoons: up to three named fortunes with holdings on the stock market, who grow with the city and the
    /// player and move against them in the late game (<see cref="RivalRules"/>). A one-second real-time timer starts and
    /// stops them with the option and plays each new game day once; their moves go through the features that own what they
    /// touch (<see cref="RivalBridges"/>). They rest in a Going Public session, as the market is the host's. A fault
    /// switches them off for the session with one line. Per frame: nothing; once a second, a few comparisons.
    /// </summary>
    public sealed partial class RivalDirector : MonoBehaviour
    {
        public static IModLogger Logger;
        public static RivalDirector Instance { get; private set; }

        private readonly RivalWorld _world = new RivalWorld();
        private readonly List<RivalMove> _land = new List<RivalMove>(4), _end = new List<RivalMove>(4);
        private RivalFile _file;
        private bool _running, _broken, _resting;
        private Action _onSave, _openPage;
        private string _beforeTests;   // the line as it was before the first test button changed it; null with no test change in place

        public bool Running => _running && !_broken;

        /// <summary>The save's rivals; null while they do not run.</summary>
        public RivalFile File => Running ? _file : null;

        /// <summary>True while a Going Public session keeps the rivals at rest.</summary>
        public bool Resting => Running && _resting;

        public static int Strength => RivalCatalogue.Clamp(Settings.RivalsStrength, 0, RivalCatalogue.Strengths - 1);

        /// <summary>The worth, at the start's prices, the first rival comes forward from: the option's step.</summary>
        public static long FirstFrom => RivalRules.FirstFrom(Settings.RivalsFirst);

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _openPage = () => RivalBridges.Open?.Invoke();   // (wrapped behind the guard where it is handed to the game: RivalGame.Tell)
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                StandDown(false, false);   // (the city is going: nothing of its businesses is written)
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // The first look is a second after the city's objects are made, never in the frame they are made in.
        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
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
        }

        private static readonly int HotTick = HotSpots.Slot("Rivals: RivalDirector.Tick");
        // Once a second: start when the save is ready, stand down when the option goes off, rest in a session, play a new day.
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);   // the timer's tick (Core/HotSpots)
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.Rivals)
            {
                if (_running)
                {
                    StandDown(true);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            bool resting = GoingPublic.InSession;
            if (resting != _resting)
            {
                _resting = resting;
                Logger?.Info("Rivals: " + (resting
                    ? "a Going Public session: the rivals rest (nothing grows, nothing moves) until it ends."
                    : "no Going Public session: the rivals go on from day " + _file.SettledDay + "."));
                if (!resting)
                {
                    Resumed(Save.Day);   // (they stood still: the days of the session are not played, and what was dated moves on)
                }
                Released(null);   // (the rivals' businesses and their buying up count for nothing while they rest, and again once they are back)
            }
            int day = Save.Day;
            if (!_resting && day > _file.SettledDay)
            {
                PlayDay(day);
            }
        }

        private void StartUp()
        {
            int day = Save.Day;
            RivalFile file = RivalStore.Load(Save.modData);
            bool fresh = file == null;
            if (fresh)
            {
                ulong id = RivalBridges.Playthrough != null ? RivalBridges.Playthrough() : 0UL;
                int seed = RivalRules.SeedFrom(id != 0UL ? id : (ulong)(long)Math.Floor(Save.Money) ^ 0x5bd1e995UL, day);
                file = RivalRules.Open(seed, day, Prices.Level);
            }
            _file = file;
            _running = true;
            _beforeTests = null;
            Save.modData?.Remove(RivalStore.TestKey);   // (the copy from before the tests is this session's alone: a later load starts without one)
            if (!fresh)
            {
                Resumed(day);   // (switched off, or the save loaded on a later day: the days missed are not played)
            }
            Logger?.Info("Rivals: " + (fresh ? "a file is opened for this save on day " + day + " (seed " + file.Seed + "); nobody comes forward before the player is worth "
                                               + RivalRules.At(FirstFrom, Prices.Level) : "the file is read: tier " + file.Tier + ", " + Describe()) + ".");
        }

        // With 'keep' the file is saved first (the option switched off in play); with 'restore' the customers and the
        // buildings' prices are written back now.
        private void StandDown(bool keep, bool restore = true)
        {
            if (keep && _running && Save != null && _file != null)
            {
                PayBreakUps(Save.Day);   // (a fault or the city unloading pays nothing, so a reload cannot pay it twice)
                RivalStore.Save(Save.modData, _file);
            }
            bool had = _file != null;
            _running = false;
            _file = null;
            _resting = false;
            if (had && restore)
            {
                Released(null);   // (the businesses they took customers from have them back, and the buildings their premium, now)
            }
        }

        /// <summary>A fault: one line, and nothing more of the rivals this session. What the save holds of them is left as it is.</summary>
        internal void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown(false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Rivals: switched off for this session after an error in " + where
                + ". The rivals stand still; what the save holds of them is untouched, and the game and the rest of the mod are not affected.");
        }

        /// <summary>For the options page: everything of the rivals' for this save is forgotten, and a new file opens on the next second.</summary>
        public void Forget()
        {
            if (_running && Save != null && _file != null)
            {
                PayBreakUps(int.MaxValue);   // (the save forgets the rivals: what a takeover still owes the player is paid now)
            }
            StandDown(false);   // (its footfall and the buildings' prices are written again: Released)
            _beforeTests = null;
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && _file != null)
                {
                    RivalStore.Save(Save.modData, _file);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void PlayDay(int day)
        {
            RivalGame.Read(_world);
            Holdings();
            RivalDayReport r = RivalRules.Day(_file, _world, day, Strength);
            for (int i = 0; i < _file.Tycoons.Length; i++)
            {
                if ((r.Activated & (1 << i)) != 0)
                {
                    CameForward(_file.Tycoons[i], day);
                }
            }
            Orphans();
            Settle(day);
            Rents(day);
            PayBreakUps(day);
            RivalMove announced = r.Announced > 0 ? _file.Move(r.Announced) : null;
            if (announced != null)
            {
                Announced(announced);
            }
            if (r.Top)
            {
                RivalGame.Tell(NotificationType.Success, "reallife_rival_note_top", null, _openPage);
            }
            Logger?.Info("Rivals: day " + day + ": tier " + _file.Tier + " (worth " + _world.Worth + ", level " + _world.Level + ", cycle " + _world.Cycle + "), " + Describe()
                         + (announced != null ? "; announced " + announced.Kind + " by " + announced.Tycoon + " for day " + announced.Land : string.Empty) + ".");
            if (Verbose)
            {
                int rank = RivalRules.Rank(_file, _world.Worth, _world.Level);
                int gap = _file.NextMoveDay - day;
                Logger?.Info("Rivals check (day " + day + "): " + r.Days + " day(s) played, next move no sooner than in " + gap + " day(s) (the tier's gap "
                    + RivalRules.Gap(Math.Max(1, _file.Tier), Strength)
                             + "), the player " + rank + " on the rich list, " + _file.Moves.Count + " move(s) kept of " + RivalCatalogue.MovesKept + ": "
                             + (gap <= RivalRules.Gap(Math.Max(1, _file.Tier), Strength) + RivalCatalogue.JitterDays
                                 && _file.Moves.Count <= RivalCatalogue.MovesKept * 2 ? "PASS" : "the pace is off"));
            }
        }

        // Each day before the rules: a tycoon's flag is the market's (a fresh market has lost the holding), and a tycoon in
        // play whose holding is not listed has it listed again.
        private void Holdings()
        {
            for (int i = 0; i < _file.Tycoons.Length; i++)
            {
                Tycoon t = _file.Tycoons[i];
                bool listed = RivalBridges.Holding != null && RivalBridges.Holding(t.Def.StockId, out long _, out long _, out long _, out int _);
                t.Listed = t.Active && (listed || (_world.Market && List(t)));
            }
        }

        // A holding with no tycoon in play (the data reset, the tests put back, a takeover) leaves the market at its price.
        private void Orphans()
        {
            for (int i = 0; i < _file.Tycoons.Length; i++)
            {
                Tycoon t = _file.Tycoons[i];
                if (t.Active || !_world.Market || RivalBridges.Holding == null || !RivalBridges.Holding(t.Def.StockId, out long _, out long _, out long _, out int _))
                {
                    continue;
                }
                t.Listed = false;
                if (RivalBridges.DelistHolding != null && RivalBridges.DelistHolding(t.Def.StockId))
                {
                    Logger?.Info("Rivals: " + t.Def.Holding + " had no tycoon in play and left the stock market at its price.");
                }
            }
        }

        // The rivals' effects on the other features written again after a move stopped early, or the rivals stopping,
        // resting or back: the customers of the businesses given (all, with none) and the buildings' prices.
        private void Released(RivalMove m)
        {
            WriteFootfall(m);
            RivalBridges.Revalue?.Invoke();
        }

        // Every takeover's break-up money due by a day, paid with its own notice.
        private void PayBreakUps(int day)
        {
            for (int i = 0; i < _file.Tycoons.Length; i++)
            {
                Tycoon t = _file.Tycoons[i];
                long due = RivalRules.BreakUpDue(t, day);
                if (due <= 0)
                {
                    continue;
                }
                RivalGame.Give(due, RivalGame.BreakUpKey, t.Def.Holding);
                RivalGame.Tell(NotificationType.Success, "reallife_rival_note_breakup", RivalText.Data("holding", t.Def.Holding, "amount", RivalText.Money(due)), _openPage);
                Logger?.Info("Rivals: " + t.Def.Holding + "'s parts sold: " + due + " paid to the player.");
            }
        }

        // Back after standing still: the missed days skipped and the dates moved on (RivalRules.Resume).
        private void Resumed(int day)
        {
            int skipped = RivalRules.Resume(_file, day);
            if (skipped > 0)
            {
                Logger?.Info("Rivals: back after standing still for " + skipped + " day(s): those days are not played, and every move and bar moves on by them.");
            }
        }

        // The moves that come today are done through their features; those that end are taken off.
        private void Settle(int day)
        {
            RivalRules.Due(_file, day, _land, _end);
            for (int i = 0; i < _land.Count; i++)
            {
                RivalMove m = _land[i];
                bool done = Land(m);
                RivalRules.Landed(_file, m, day, done);
                Logger?.Info("Rivals: " + m.Kind + " by " + RivalCatalogue.Tycoons[m.Tycoon].Name + (done ? " came" : " came to nothing (nothing of the player's was there for it)")
                             + (m.Place.Length > 0 ? " at " + m.Place : string.Empty) + ", size " + m.Amount + (m.State == RivalMoveState.Running ? ", to day " + m.End : string.Empty) + ".");
                if (done)
                {
                    NotificationType type = m.Kind == RivalMoveKind.Bid || m.Kind == RivalMoveKind.Suit ? NotificationType.Error : NotificationType.Warning;
                    RivalGame.Tell(type, RivalText.CameKey(m), RivalText.MoveData(m), _openPage);
                }
                else
                {
                    RivalGame.Tell(NotificationType.Info, "reallife_rival_note_nothing", RivalText.MoveData(m), _openPage);
                }
                if (m.Kind == RivalMoveKind.Outlet && m.State == RivalMoveState.Running)
                {
                    WriteFootfall(m);
                }
                else if (m.Kind == RivalMoveKind.Property && m.State == RivalMoveState.Running)
                {
                    RivalBridges.Revalue?.Invoke();   // (the buildings' prices with the premium from today, not the property business's next day)
                }
            }
            for (int i = 0; i < _end.Count; i++)
            {
                RivalMove m = _end[i];
                RivalRules.Ended(m);
                Logger?.Info("Rivals: " + m.Kind + " by " + RivalCatalogue.Tycoons[m.Tycoon].Name + " has run its course (day " + m.End + ").");
                if (m.Kind == RivalMoveKind.Outlet)
                {
                    WriteFootfall(m);
                }
                else if (m.Kind == RivalMoveKind.Property)
                {
                    RivalBridges.Revalue?.Invoke();
                }
            }
            _land.Clear();
            _end.Clear();
        }

        // A move comes: through the feature that owns what it touches. True if it did something.
        private bool Land(RivalMove m)
        {
            Tycoon t = _file.Tycoons[RivalCatalogue.Clamp(m.Tycoon, 0, _file.Tycoons.Length - 1)];
            TycoonDef d = t.Def;
            switch (m.Kind)
            {
                case RivalMoveKind.Property:
                case RivalMoveKind.Outlet:
                    return RivalGame.Business(m.Place) != null;   // (its effect is read by the features as they write: PremiumOf, the rent at the day's round, FootfallOf)
                case RivalMoveKind.Poach:
                {
                    if (RivalBridges.Offer == null || !RivalBridges.Offer(d.Holding, (int)m.Amount, out string name, out string business))
                    {
                        return false;
                    }
                    m.Name = business ?? m.Name;
                    m.Who = name ?? string.Empty;
                    return true;
                }
                case RivalMoveKind.Stake:
                    return t.Listed && RivalBridges.Stake != null && RivalBridges.Stake(d.StockId);
                case RivalMoveKind.Bid:
                    return t.Listed && RivalBridges.Bid != null && RivalBridges.Bid(d.StockId);
                case RivalMoveKind.Lobby:
                {
                    bool called = RivalBridges.Inspect != null && RivalBridges.Inspect(m.Place);
                    m.Second = called ? 1 : 0;
                    return RivalBridges.Ask(RivalBridges.IssuesOn);   // (the inspectors come more often while it runs, whether or not one could be called in today)
                }
                case RivalMoveKind.Suit:
                {
                    int number = RivalBridges.Sue != null ? RivalBridges.Sue(m.Amount, d.Holding) : 0;
                    m.Second = number;
                    return number > 0;
                }
                case RivalMoveKind.Press:
                {
                    bool smear = RivalBridges.Smear != null && RivalBridges.Smear(m.Place);
                    bool paper = RivalBridges.Ask(RivalBridges.PaperOn);
                    RivalBridges.Customers?.Invoke((int)m.Amount);
                    m.Second = (smear ? 1 : 0) | (paper ? 2 : 0);   // (which of its texts is true: a piece in the paper, or word round the city)
                    return true;
                }
                default:
                    return false;
            }
        }

        // Each rented business of the player's once: its running buyings up together, capped (RivalRules.RentRise), charged
        // from the day after the move comes.
        private void Rents(int day)
        {
            for (int s = 0; s < _world.Shops.Count; s++)
            {
                RivalShop shop = _world.Shops[s];
                if (!shop.Rented || shop.Rent <= 0)
                {
                    continue;
                }
                string holding = null;
                for (int i = 0; i < _file.Moves.Count && holding == null; i++)
                {
                    RivalMove m = _file.Moves[i];
                    holding = m.Kind == RivalMoveKind.Property && m.State == RivalMoveState.Running && m.Rented && m.Place == shop.Place
                        && m.Land < day ? RivalCatalogue.Tycoons[m.Tycoon].Holding : null;
                }
                long rise = holding != null ? RivalRules.RentRise(_file, shop.Place, shop.Rent) : 0;
                if (rise <= 0)
                {
                    continue;
                }
                bool paid = RivalGame.ChargeRent(RivalGame.Business(shop.Place), rise, holding);
                if (Verbose)
                {
                    Logger?.Info("Rivals check (rent, " + shop.Place + "): the game's rent " + shop.Rent + " a day, all rises together at most " + RivalCatalogue.RentRiseMost
                        + " per mille, charged " + rise + ": "
                                 + (rise <= (long)shop.Rent * RivalCatalogue.RentRiseMost / 1000L ? (paid ? "PASS" : "not paid (the cash was not there)") : "PAST THE MOST"));
                }
            }
        }

        private void CameForward(Tycoon t, int day)
        {
            t.Listed = _world.Market && List(t);
            TycoonDef d = t.Def;
            RivalGame.Tell(NotificationType.Warning, t.Listed ? "reallife_rival_note_forward" : "reallife_rival_note_forward_unlisted",
                RivalText.Data("rival", d.Name, "holding", d.Holding, "worth", RivalText.Money(t.Worth), "hello", RivalText.Hello(t.Index)), _openPage);
            Logger?.Info("Rivals: " + d.Name + " comes forward on day " + day + " worth " + t.Worth + "; " + d.Holding
                + (t.Listed ? " is on the stock market (id " + d.StockId + ")" : " is not on the stock market here yet") + ".");
        }

        // The holding comes to the market sized by the tycoon's fortune: its shares at their price are what they are worth.
        private static bool List(Tycoon t)
        {
            return RivalBridges.ListHolding != null && RivalBridges.ListHolding(t.Def.StockId, t.Worth);
        }

        private void Announced(RivalMove m)
        {
            RivalGame.Tell(NotificationType.Warning, "reallife_rival_note_announced", RivalText.MoveData(m), _openPage);
        }

        // The customers of the player's businesses a rival's business touches (or all of them, with none given) written again now.
        private void WriteFootfall(RivalMove m)
        {
            if (RivalBridges.Footfall == null)
            {
                return;
            }
            if (m == null)
            {
                RivalGame.Read(_world);
            }
            for (int i = 0; i < _world.Shops.Count; i++)
            {
                RivalShop s = _world.Shops[i];
                if (m == null || (s.District == m.District && s.Type == m.Type))
                {
                    RivalBridges.Footfall(s.Place);
                }
            }
        }

        /// <summary>
        /// The change in customers at one of the player's businesses from rival outlets near it, per cent (0 or less); asked
        /// by business pressure when it writes footfall. Nothing while the rivals are off or at rest.
        /// </summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            RivalDirector d = Instance;
            RivalFile f = d != null && d.Running && !d._resting ? d._file : null;
            string district = f != null ? GameRegistrations.District(reg) : null;
            if (district == null)
            {
                return 0;
            }
            return RivalRules.OutletDip(f, district, reg.businessTypeName);
        }

        /// <summary>The premium on buildings for sale in a district, per mille.</summary>
        public static int PremiumOf(string district)
        {
            RivalDirector d = Instance;
            RivalFile f = d != null && d.Running && !d._resting ? d._file : null;
            return f != null ? RivalRules.Premium(f, district) : 0;
        }

        /// <summary>How much more often inspections, licences and audits come, per mille.</summary>
        public static int InspectionWeight()
        {
            RivalDirector d = Instance;
            RivalFile f = d != null && d.Running && !d._resting ? d._file : null;
            return f != null ? RivalRules.InspectionWeight(f) : 0;
        }

        /// <summary>
        /// The rivals' pieces for a morning's paper, <see cref="RivalCatalogue.PiecesMost"/> at most. Read from the file only,
        /// so the same day gives the same pieces however often it is asked.
        /// </summary>
        public void Paper(int day, List<NewsItem> into)
        {
            RivalFile f = Running && !_resting ? _file : null;
            if (f == null || into == null)
            {
                return;
            }
            int added = 0;
            for (int i = 0; i < f.Tycoons.Length && added < RivalCatalogue.PiecesMost; i++)
            {
                Tycoon t = f.Tycoons[i];
                if (t.ActiveDay == day)
                {
                    into.Add(new NewsItem
                    {
                        Key = "reallife_rival_paper_forward",
                        Data = RivalText.Data("rival", t.Def.Name, "holding", t.Def.Holding, "worth", RivalText.Money(t.Worth), "hello", RivalText.Hello(t.Index))
                    });
                    added++;
                }
            }
            for (int i = 0; i < f.Moves.Count && added < RivalCatalogue.PiecesMost; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Land == day && (m.State == RivalMoveState.Running || m.State == RivalMoveState.Over))
                {
                    into.Add(new NewsItem { Key = RivalText.PaperCameKey(m.Kind), Data = RivalText.MoveData(m) });
                    added++;
                }
            }
            for (int i = 0; i < f.Moves.Count && added < RivalCatalogue.PiecesMost; i++)
            {
                RivalMove m = f.Moves[i];
                if (m.Announced == day && m.State == RivalMoveState.Announced)
                {
                    into.Add(new NewsItem { Key = "reallife_rival_paper_announced", Data = RivalText.MoveData(m) });
                    added++;
                }
            }
            for (int i = 0; i < f.Counters.Count && added < RivalCatalogue.PiecesMost; i++)
            {
                RivalCounterNote c = f.Counters[i];
                if (c.Day == day - 1)
                {
                    TycoonDef d = RivalCatalogue.Tycoons[RivalCatalogue.Clamp(c.Tycoon, 0, RivalCatalogue.TycoonCount - 1)];
                    into.Add(new NewsItem { Key = RivalText.PaperDoneKey(c.Kind), Data = RivalText.Data("rival", d.Name, "holding", d.Holding, "quip", RivalText.Quip(c.Tycoon)) });
                    added++;
                }
            }
            if (added < RivalCatalogue.PiecesMost && day % 7 == RivalCatalogue.RichListDay && RivalRules.AnyActive(f))
            {
                into.Add(new NewsItem { Key = "reallife_rival_paper_list", Data = ListData(f) });
            }
        }

        // The rich list's top three and the player's place, for its piece of the paper.
        private Dictionary<string, string> ListData(RivalFile f)
        {
            var rows = new List<RichRow>(RivalCatalogue.RichListSize);
            RivalRules.RichList(f, f.PlayerWorth, Prices.Level, RivalGame.PlayerName(), rows);
            int rank = RivalRules.Rank(f, f.PlayerWorth, Prices.Level);
            return RivalText.Data("first", rows.Count > 0 ? rows[0].Name : string.Empty, "first_worth", rows.Count > 0 ? RivalText.Money(rows[0].Worth) : string.Empty,
                "second", rows.Count > 1 ? rows[1].Name : string.Empty, "third", rows.Count > 2 ? rows[2].Name : string.Empty, "rank", rank.ToString(), "name", RivalGame.PlayerName());
        }

        /// <summary>One line of where the rivals stand, for the log.</summary>
        internal string Describe()
        {
            if (_file == null)
            {
                return "no file";
            }
            var b = new System.Text.StringBuilder(160);
            for (int i = 0; i < _file.Tycoons.Length; i++)
            {
                Tycoon t = _file.Tycoons[i];
                b.Append(i > 0 ? ", " : string.Empty).Append(t.Def.Name).Append(' ').Append(t.Active ? t.Worth.ToString() : t.DefeatedDay >= 0 ? "out" : "not forward");
            }
            RivalMove pending = _file.Pending();
            b.Append("; ").Append(_file.Moves.Count).Append(" move(s)").Append(pending != null ? ", " + pending.Kind + " coming on day " + pending.Land : string.Empty);
            return b.ToString();
        }
    }
}
