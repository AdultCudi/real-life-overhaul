using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// The world as suppliers and contracts need it: the clock, the customer's cash and standing, the market, the goods and the places. The game's side is SupplierGame;
    /// the checks hand in a world of their own, so the very engine that ships is what they drive. Every call is made on an hour's or a day's pass or a press.
    /// </summary>
    public interface ISupplierWorld
    {
        int Day { get; }
        int Hour { get; }
        /// <summary>The month the city counts, for the rivals' monthly sums.</summary>
        int Month { get; }
        /// <summary>The engine plays a day that has passed (a time skip's days one by one): the world answers with that day and the delivery hour. -1 puts the real clock back.</summary>
        void Play(int day);
        /// <summary>The cash the customer can pay suppliers with, cents: the cash less what the investors of their firms are owed (Tech's firm account), which a payment may never take.</summary>
        long Cash { get; }
        /// <summary>The city's price level, tenths of a point (1000 at the start).</summary>
        int Level { get; }
        /// <summary>The customer's credit rating (300 to 850); 0 where there is none.</summary>
        int Credit { get; }
        /// <summary>The city's cycle: 0 boom, 1 steady, 2 slowdown, 3 recession.</summary>
        int Cycle { get; }
        /// <summary>Today's price factor of a kind of goods, thousandths (1000 calm).</summary>
        int Factor(int kind);
        /// <summary>The size of a market shortage of a kind running on a day, as known today, thousandths; 0 for none.</summary>
        int ShortageOn(int kind, int day);
        /// <summary>The shortage of a kind that is running or announced: the days until it starts (0 running), the days it then lasts and its size; all 0 for none.</summary>
        void ShortAhead(int kind, out int from, out int days, out int size);
        /// <summary>The season, weather and city event of a day.</summary>
        CityDay Conditions(int day);
        /// <summary>A product's market price of the day, mills a unit (the game's wholesale price with its whole-market event); 0 for none.</summary>
        long Market(string item);
        /// <summary>A product's wholesale price of the day without the game's own market event on it, mills: what a fixed price is written from.</summary>
        long Base(string item);
        /// <summary>What a product fetches on export today, mills.</summary>
        long Export(string item);
        /// <summary>True while the game has a product on backorder across the whole market: nobody brings it.</summary>
        bool Backorder(string item);
        /// <summary>The most units of a product a supplier brings in a week (the game's own wholesale limit); 0 for no limit.</summary>
        int Most(string item);
        /// <summary>Units in a box of a product.</summary>
        int Box(string item);
        /// <summary>True while a place is the customer's own and trading.</summary>
        bool PlaceOpen(string place);
        /// <summary>What a place sells: each product with its kind and the units it sold in the last week.</summary>
        void Wants(string place, List<ContractLine> into);
        /// <summary>Goods put at a place at a unit cost: how many found room.</summary>
        int Deliver(string place, string item, int units, long mills);
        /// <summary>Money out of the customer's cash through the mod's booking call; false, nothing taken, when the cash does not cover it.</summary>
        bool Pay(long cents, string key, string place, int supplier);
        /// <summary>Money into the customer's cash.</summary>
        void Receive(long cents, string key, string place, int supplier);
        /// <summary>
        /// True where somebody who orders stock decides at a place today: a manager of the mod's with stock orders on who is at work (or stood in for), or the game's
        /// purchasing agent for a warehouse.
        /// </summary>
        bool Buyer(string place);
        /// <summary>The buyer's budget: true, with so much of this month's budget spent, when the sum (cents) is inside what is left of it; a purchasing agent has none.</summary>
        bool Spend(string place, long cents);
        /// <summary>Days more a first delivery takes to a place far from a supplier's depot.</summary>
        int FarDays(int supplier, string place);
        /// <summary>A share of what a supplier took reaches the tycoon whose holding owns it, cents.</summary>
        void OwnerGain(int tycoon, long cents);
        /// <summary>A tycoon's outlets paid so much more (or less) for their supply today, cents.</summary>
        void RivalMoved(int tycoon, long cents);
    }

    /// <summary>What a supplier offers a place: the goods, the prices and the first day, or why it offers nothing.</summary>
    public sealed class Quote
    {
        public Refusal Why;
        public int Supplier, Weeks, Share, Discount, Level, FirstDay, LeftOut;   // LeftOut: products the supplier trades that the place sells and a contract's most left out
        public long Value, MarketValue, Min;   // cents a delivery at the offered prices and at the day's market prices; the supplier's minimum order
        public readonly List<ContractLine> Lines = new List<ContractLine>(SupplierCatalogue.LinesMost);

        /// <summary>The offer against the day's market price, thousandths: under 1000 is cheaper.</summary>
        public int AgainstMarket => MarketValue > 0 ? (int)(Value * 1000 / MarketValue) : 1000;
    }

    /// <summary>One outlet of a rival tycoon, for the rivals' day: whose it is, its key and the kind of goods it sells.</summary>
    public struct RivalOutletRef
    {
        public int Tycoon, Kind;
        public string Key;
    }

    /// <summary>
    /// Suppliers and contracts at work, game-free: an offer, a signing, a delivery with its payment (take or pay), what is owed and how it is collected, a contract ended
    /// by its term, by the customer, by the supplier or by the place closing, the day of every supplier's own books, tomorrow's deliveries judged, the buyer's rules, and
    /// the rivals. The director calls this with the game's world and EconomyCheck with its own (SupplierChecks.cs); nothing here is random but the hash of the save's seed.
    /// </summary>
    public static class SupplierEngine
    {
        // The goods' and the owed payment's keys hold the game's own word for a delivery contract: the Finance app's cash flow reads a type's words, and these are stock.
        public const string GoodsKey = "reallife_transaction_supply_deliverycontract";
        public const string SettleKey = "reallife_transaction_supply_settlement";
        public const string OwedKey = "reallife_transaction_supply_deliverycontract_owed";
        public const string CreditKey = "reallife_transaction_supply_credit";

        private static readonly List<ContractLine> Wanted = new List<ContractLine>(32);
        private static readonly int[] Factors = new int[SupplierCatalogue.Kinds], Shortages = new int[SupplierCatalogue.Kinds];
        private static readonly Quote Scratch = new Quote(), BestQuote = new Quote();
        private static readonly List<int> Ids = new List<int>(SupplierCatalogue.ContractsMost);
        private static readonly List<ContractLine> Missing = new List<ContractLine>(SupplierCatalogue.LinesMost);

        // ---- an offer

        /// <summary>The step of the catalogue's shares nearest a share.</summary>
        public static int ShareStep(int share)
        {
            int best = 0;
            for (int i = 1; i < SupplierCatalogue.Shares.Length; i++)
            {
                best = Math.Abs(SupplierCatalogue.Shares[i] - share) < Math.Abs(SupplierCatalogue.Shares[best] - share) ? i : best;
            }
            return best;
        }

        /// <summary>
        /// What a supplier offers a place for a term (0: one delivery at the day's price) at a share of its week's sales. The goods are what the place sells of the kinds
        /// the supplier trades, in whole boxes, inside the game's own weekly limit.
        /// </summary>
        /// <param name="kinds">the kinds of goods the offer is held to, a bit each; -1 for every kind the supplier trades</param>
        public static void Offer(SupplierState s, ISupplierWorld w, int supplier, string place, int weeks, int shareStep, Quote q, int kinds = -1)
        {
            q.Lines.Clear();
            q.Supplier = supplier;
            q.Weeks = SupplierCatalogue.TermStep(weeks) >= 0 ? weeks : 0;
            q.Share = SupplierCatalogue.Shares[SupplierCatalogue.Clamp(shareStep, 0, SupplierCatalogue.Shares.Length - 1)];
            q.Value = q.MarketValue = q.Min = 0;
            q.Discount = 0;
            q.FirstDay = 0;
            q.LeftOut = 0;
            SupplierDef def = SupplierCatalogue.Firm(supplier);
            FirmState f = s.Firm(supplier);
            if (def == null || f == null || string.IsNullOrEmpty(place) || !w.PlaceOpen(place))
            {
                q.Why = Refusal.Off;
                return;
            }
            q.Level = SupplierRules.Level(def, f);
            q.Min = SupplierRules.AtLevel(def.MinOrder, w.Level);
            q.FirstDay = w.Day + def.LeadDays + Math.Max(0, w.FarDays(supplier, place));
            Wanted.Clear();
            w.Wants(place, Wanted);
            bool any = Wanted.Count > 0;
            // The best sellers first: a contract holds so many products, and the ones the place sells most (units at the day's price) are the ones it takes.
            for (int i = 0; i < Wanted.Count; i++)
            {
                Wanted[i].Mills = w.Market(Wanted[i].Item);
            }
            Wanted.Sort(BestSellers);
            for (int i = 0; i < Wanted.Count; i++)
            {
                if ((kinds & (1 << Wanted[i].Kind)) != 0)
                {
                    Add(w, def, q, Wanted[i], ((long)Wanted[i].Units * q.Share + 999) / 1000);
                }
            }
            Wanted.Clear();
            Price(s, w, def, f, place, q, any);
        }

        private static readonly Comparison<ContractLine> BestSellers = (a, b) =>
        {
            int order = ((long)b.Units * b.Mills).CompareTo((long)a.Units * a.Mills);
            return order != 0 ? order : string.CompareOrdinal(a.Item, b.Item);
        };

        /// <summary>
        /// What a supplier offers a place for one delivery at the day's price of goods named one by one (the units a late or short delivery leaves missing), in whole boxes.
        /// </summary>
        public static void OfferFor(SupplierState s, ISupplierWorld w, int supplier, string place, IList<ContractLine> goods, Quote q)
        {
            q.Lines.Clear();
            q.Supplier = supplier;
            q.Weeks = 0;
            q.Share = 1000;
            q.Value = q.MarketValue = q.Min = 0;
            q.Discount = 0;
            q.FirstDay = 0;
            q.LeftOut = 0;
            SupplierDef def = SupplierCatalogue.Firm(supplier);
            FirmState f = s.Firm(supplier);
            if (def == null || f == null || string.IsNullOrEmpty(place) || !w.PlaceOpen(place))
            {
                q.Why = Refusal.Off;
                return;
            }
            q.Level = SupplierRules.Level(def, f);
            q.Min = SupplierRules.AtLevel(def.MinOrder, w.Level);
            q.FirstDay = w.Day + def.LeadDays + Math.Max(0, w.FarDays(supplier, place));
            for (int i = 0; goods != null && i < goods.Count; i++)
            {
                Add(w, def, q, goods[i], goods[i].Units);
            }
            Price(s, w, def, f, place, q, goods != null && goods.Count > 0);
        }

        // A product joins an offer: only of a kind the supplier trades, in whole boxes, inside the game's own weekly limit and the most a contract holds.
        private static void Add(ISupplierWorld w, SupplierDef def, Quote q, ContractLine want, long units)
        {
            if (!def.Trades(want.Kind) || units <= 0 || w.Market(want.Item) <= 0 || w.Base(want.Item) <= 0)
            {
                return;
            }
            int box = Math.Max(1, w.Box(want.Item)), most = w.Most(want.Item);
            units = (units + box - 1) / box * box;
            if (most > 0 && units > most)
            {
                units = most / box * box;
            }
            if (units <= 0)
            {
                return;
            }
            if (q.Lines.Count >= SupplierCatalogue.LinesMost)
            {
                q.LeftOut++;   // a product the supplier would bring and a contract has no room for: the page says how many
                return;
            }
            q.Lines.Add(new ContractLine { Item = want.Item, Kind = want.Kind, Units = (int)Math.Min(int.MaxValue / 2, units) });
            q.MarketValue += SupplierRules.Cents(units, w.Market(want.Item));
        }

        // The offer's prices and whether the supplier makes it at all.
        private static void Price(SupplierState s, ISupplierWorld w, SupplierDef def, FirmState f, string place, Quote q, bool any)
        {
            if (q.Lines.Count == 0)
            {
                q.Why = any ? Refusal.Kind : Refusal.NoGoods;
                return;
            }
            q.Discount = SupplierRules.Discount(q.Weeks, q.MarketValue, q.Min, w.Credit, s.Record.LateSince(w.Day - SupplierCatalogue.LateWindow));
            for (int i = 0; i < q.Lines.Count; i++)
            {
                ContractLine line = q.Lines[i];
                if (q.Weeks > 0)
                {
                    w.ShortAhead(line.Kind, out int from, out int days, out int size);
                    int term = SupplierRules.TermFactor(q.Weeks * SupplierCatalogue.WeekDays, from, days, size);
                    line.Mills = SupplierRules.FixedMills(w.Base(line.Item), w.Factor(line.Kind), term, q.Level, q.Discount);
                }
                else
                {
                    line.Mills = SupplierRules.SpotMills(w.Market(line.Item), q.Level, q.Discount);
                }
                q.Value += SupplierRules.Cents(line.Units, line.Mills);
            }
            q.Why = SupplierRules.Refuse(def, f, w.Day, s.OwedAll(), q.MarketValue, q.Min, w.Credit);
            if (q.Why == Refusal.None && s.RunningCount() >= SupplierCatalogue.ContractsMost)
            {
                q.Why = Refusal.Full;
            }
            if (q.Why == Refusal.None && q.Weeks > 0 && Holds(s, q.Supplier, place))
            {
                q.Why = Refusal.Held;
            }
        }

        // True while a place has a fixed price contract running with a supplier: one a place and supplier, so a price cannot be stacked.
        private static bool Holds(SupplierState s, int supplier, string place)
        {
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                Contract c = s.Contracts[i];
                if (c.Running && c.Fixed && c.Supplier == supplier && c.Place == place)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Signs what a supplier offers; null, with the reason, when it offers nothing. Nothing is paid at signing: every delivery is paid as it comes.</summary>
        public static Contract Sign(SupplierState s, ISupplierWorld w, int supplier, string place, string placeName, int weeks, int shareStep, out Refusal why)
        {
            Offer(s, w, supplier, place, weeks, shareStep, Scratch);
            why = Scratch.Why;
            return why == Refusal.None ? Make(s, w, Scratch, place, placeName) : null;
        }

        // A contract made of an offer. The buyer's rules come in with it (from the contract it follows, if any), and so does the knowledge that the supplier was in trouble
        // when it was signed, before anything is judged: its first delivery, if it is tomorrow's, is judged at once by the rules it will run by.
        private static Contract Make(SupplierState s, ISupplierWorld w, Quote q, string place, string placeName, Contract rules = null, bool covering = false)
        {
            FirmState signedWith = s.Firm(q.Supplier);
            var c = new Contract
            {
                Id = s.NextId++, Supplier = q.Supplier, Place = place, PlaceName = placeName ?? string.Empty, Weeks = q.Weeks, Share = q.Share, Start = w.Day,
                Due = Math.Max(w.Day + 1, q.FirstDay), Covering = covering,
                Knew = signedWith != null && (signedWith.InTrouble(w.Day) || signedWith.Health < SupplierCatalogue.HealthWeak)
            };
            if (rules != null)
            {
                c.Renew = rules.Renew;
                c.RenewLimit = rules.RenewLimit;
                c.Cover = rules.Cover;
                c.LeaveRule = rules.LeaveRule;
            }
            for (int i = 0; i < q.Lines.Count; i++)
            {
                ContractLine line = q.Lines[i];
                c.Lines.Add(new ContractLine { Item = line.Item, Kind = line.Kind, Units = line.Units, Mills = line.Mills });
            }
            s.Contracts.Add(c);
            Trim(s);
            s.Say(SupplyEventKind.Signed, c, c.Supplier, Value(c), c.Due, c.Weeks);
            if (c.Due == w.Day + 1)
            {
                JudgeNext(s, w, c);   // too late for the day's pass: tomorrow's delivery is judged now, by the same draw
            }
            return c;
        }

        // The ended contracts past what the page keeps are forgotten, the oldest first; one that still has money owed on it stays.
        private static void Trim(SupplierState s)
        {
            int ended = 0;
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                ended += s.Contracts[i].Running ? 0 : 1;
            }
            for (int i = 0; i < s.Contracts.Count && ended > SupplierCatalogue.ContractsKept; i++)
            {
                if (!s.Contracts[i].Running && s.OwedOn(s.Contracts[i].Id) == 0)
                {
                    s.Contracts.RemoveAt(i--);
                    ended--;
                }
            }
        }

        /// <summary>What one delivery of a contract is worth at its agreed prices, cents.</summary>
        public static long Value(Contract c)
        {
            long sum = 0;
            for (int i = 0; i < c.Lines.Count; i++)
            {
                sum += SupplierRules.Cents(c.Lines[i].Units, c.Lines[i].Mills);
            }
            return sum;
        }

        /// <summary>What the same goods cost at the day's market prices, cents.</summary>
        public static long MarketValue(Contract c, ISupplierWorld w)
        {
            long sum = 0;
            for (int i = 0; i < c.Lines.Count; i++)
            {
                sum += SupplierRules.Cents(c.Lines[i].Units, w.Market(c.Lines[i].Item));
            }
            return sum;
        }

        /// <summary>What ending a contract now would cost, cents: the settlement less the credit the supplier owes on it.</summary>
        public static long BreakCost(Contract c)
        {
            return c == null || !c.Running ? 0L : Math.Max(0L, SupplierRules.Settlement(Value(c), c.Left) - c.Credit);
        }

        /// <summary>True when the supplier's own failures give the customer the right to leave with nothing to pay.</summary>
        public static bool MayLeave(Contract c) => c != null && c.Running && c.Failures >= SupplierCatalogue.FailuresToLeave;

        // ---- a contract's end

        /// <summary>The customer ends a contract early: the settlement is due at once, paid from the cash or, where that does not cover it, owed and said.</summary>
        public static bool Break(SupplierState s, ISupplierWorld w, int id)
        {
            Contract c = s.ContractOf(id);
            if (c == null || !c.Running)
            {
                return false;
            }
            long settle = Settle(s, w, c, false);
            End(s, w, c, ContractState.Broken);
            s.Say(SupplyEventKind.Broken, c, c.Supplier, settle);
            return true;
        }

        /// <summary>The customer leaves by right after the supplier's own failures: nothing to pay, and the credit it owes is paid out.</summary>
        public static bool Leave(SupplierState s, ISupplierWorld w, int id)
        {
            Contract c = s.ContractOf(id);
            if (!MayLeave(c))
            {
                return false;
            }
            End(s, w, c, ContractState.Left);
            s.Say(SupplyEventKind.Left, c, c.Supplier);
            return true;
        }

        // The settlement of a contract ended early: taken from the cash, or put on the list of what is owed with its supplier's late fee, to be paid in its days. Returns it.
        // A settlement left unpaid past its days is acted on by the day's pass like any sum owed (Collect); one put on the list because the contract was ended for want of
        // payment (defaulted) was counted already.
        private static long Settle(SupplierState s, ISupplierWorld w, Contract c, bool defaulted)
        {
            long settle = BreakCost(c);
            c.Credit = 0;   // the credit went against the settlement
            if (settle <= 0)
            {
                return 0;
            }
            if (!defaulted && w.Cash >= settle && w.Pay(settle, SettleKey, c.Place, c.Supplier))
            {
                c.Paid += settle;
                return settle;
            }
            SupplierDef def = SupplierCatalogue.Firm(c.Supplier);
            LatePolicy late = def != null ? def.Late : LatePolicy.Lenient;
            long owed = SupplierRules.WithFee(settle, late);
            Owe(s, new OwedLine
            {
                Supplier = c.Supplier, Contract = c.Id, Cents = owed, Since = w.Day, Settlement = true, Acted = defaulted,
                Due = w.Day + SupplierCatalogue.GraceDays[(int)late]
            });
            return owed;
        }

        // A sum put on the list of what is owed. Past the lines the save keeps it joins the oldest line of its contract, or its supplier, so nothing owed is dropped or hidden.
        private static void Owe(SupplierState s, OwedLine line)
        {
            if (s.Owed.Count >= SupplierCatalogue.OwedLinesMost)
            {
                int same = -1;
                for (int i = 0; i < s.Owed.Count; i++)
                {
                    if (s.Owed[i].Supplier == line.Supplier && (same < 0 || (s.Owed[i].Contract == line.Contract && s.Owed[same].Contract != line.Contract)))
                    {
                        same = i;
                    }
                }
                if (same >= 0)
                {
                    s.Owed[same].Cents += line.Cents;
                    return;
                }
            }
            s.Owed.Add(line);
        }

        private static void End(SupplierState s, ISupplierWorld w, Contract c, ContractState state)
        {
            c.State = state;
            c.Ended = w.Day;
            c.Out = Arrival.None;
            c.Slip = 0;
            PayOutCredit(s, w, c);
            Trim(s);
        }

        // The credit a supplier still owes on an ended contract is paid out, or said to be lost with the supplier; once.
        private static void PayOutCredit(SupplierState s, ISupplierWorld w, Contract c)
        {
            if (c.Credit <= 0)
            {
                return;
            }
            long credit = c.Credit;
            c.Credit = 0;   // gone from the contract before the money moves: a fault in between loses a credit, never pays it twice
            FirmState f = s.Firm(c.Supplier);
            if (c.State == ContractState.Failed || f == null || !f.Open)
            {
                s.Say(SupplyEventKind.CreditLost, c, c.Supplier, credit);   // the supplier is gone: said, not hidden
            }
            else
            {
                w.Receive(credit, CreditKey, c.Place, c.Supplier);
                s.Say(SupplyEventKind.CreditBack, c, c.Supplier, credit);
            }
        }

        private static void EndUnpaid(SupplierState s, ISupplierWorld w, Contract c)
        {
            long settle = Settle(s, w, c, true);
            s.Record.Defaults++;
            FirmState f = s.Firm(c.Supplier);
            if (f != null)
            {
                f.SourUntil = w.Day + SupplierCatalogue.SourDays;
            }
            End(s, w, c, ContractState.Unpaid);
            s.Say(SupplyEventKind.EndedUnpaid, c, c.Supplier, settle, s.OwedOn(c.Id));
        }

        private static void EndPlace(SupplierState s, ISupplierWorld w, Contract c)
        {
            long settle = Settle(s, w, c, false);
            End(s, w, c, ContractState.Closed);
            s.Say(SupplyEventKind.EndedPlace, c, c.Supplier, settle, s.OwedOn(c.Id));
        }

        // ---- the hour: deliveries

        /// <summary>The deliveries that have come due: each is brought, paid for and counted. Asked once a game hour.</summary>
        public static void Hour(SupplierState s, ISupplierWorld w)
        {
            int day = w.Day, hour = w.Hour;
            Snapshot(s);
            for (int i = 0; i < Ids.Count; i++)
            {
                Contract c = s.ContractOf(Ids[i]);
                if (c == null || !c.Running)
                {
                    continue;
                }
                if (c.JudgedFor != c.Due && day >= c.Due)
                {
                    JudgeNext(s, w, c);   // its day came with no day's pass before it (a long sleep): judged by the same draw before anything is brought
                }
                int arrives = c.Due + c.Slip;
                if (day > arrives || (day == arrives && hour >= SupplierCatalogue.DeliveryHour))
                {
                    Deliver(s, w, c);
                }
            }
            s.LastHour = hour;
        }

        // The running contracts as they stand now, by id: a pass goes through these, so one that ends, is trimmed away or is signed on the way moves nothing under it.
        private static void Snapshot(SupplierState s)
        {
            Ids.Clear();
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                if (s.Contracts[i].Running)
                {
                    Ids.Add(s.Contracts[i].Id);
                }
            }
        }

        /// <summary>
        /// A time skip of several days (a long sleep, a journey): every day that went by since the last one counted is played one by one, the suppliers' books, the day's
        /// judgement and the delivery hour of each, so how the player slept changes nothing of what happened. At most <see cref="SupplierCatalogue.CatchUpDays"/> are played;
        /// the days before them are a pause, as after the option was off (<see cref="Resume"/>). Returns the days played. Today's own pass follows, asked by the caller.
        /// </summary>
        public static int CatchUp(SupplierState s, ISupplierWorld w, int today)
        {
            int from = SupplierRules.PlayFrom(s.LastDay, today, out bool paused);
            if (from >= today)
            {
                return 0;
            }
            if (paused)
            {
                Resume(s, from);
            }
            int played = 0;
            try
            {
                for (int d = from; d < today; d++)
                {
                    w.Play(d);
                    Day(s, w);
                    Hour(s, w);
                    played++;
                }
            }
            finally
            {
                w.Play(-1);
            }
            return played;
        }

        /// <summary>
        /// The feature was away (the option off, a save from long ago): a delivery whose day passed meanwhile is neither brought nor charged, and each contract goes on
        /// at its next day of the week. The term is counted in deliveries, so nothing of it is lost and nothing gained by the pause.
        /// </summary>
        public static int Resume(SupplierState s, int day)
        {
            int moved = 0;
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                Contract c = s.Contracts[i];
                if (!c.Running || c.Due + c.Slip >= day)
                {
                    continue;
                }
                while (c.Due < day)
                {
                    c.Due += SupplierCatalogue.WeekDays;
                }
                c.Slip = 0;
                c.Out = Arrival.None;
                c.Why = Cause.None;
                c.OutShare = 1000;
                c.OutKind = -1;
                moved++;
            }
            return moved;
        }

        private static void Deliver(SupplierState s, ISupplierWorld w, Contract c)
        {
            SupplierDef def = SupplierCatalogue.Firm(c.Supplier);
            FirmState f = s.Firm(c.Supplier);
            if (def == null || f == null)
            {
                End(s, w, c, ContractState.Failed);
                return;
            }
            if (!w.PlaceOpen(c.Place))
            {
                EndPlace(s, w, c);
                return;
            }
            Arrival outcome = c.Out;
            int outShare = c.OutShare, outKind = c.OutKind;   // kept for the goods, which are put down after the step has cleared them from the contract
            // What the supplier brings to the door and what it is charged at: the agreed price, never under what the goods fetch on export that day.
            long due = 0, whole = Value(c), missing = 0;
            int units = 0;
            for (int i = 0; i < c.Lines.Count; i++)
            {
                ContractLine line = c.Lines[i];
                int brought = w.Backorder(line.Item) ? 0 : SupplierRules.Brought(line.Units, line.Kind, c.Out, c.OutShare, c.OutKind);
                due += SupplierRules.Cents(brought, SupplierRules.ChargeMills(line.Mills, w.Export(line.Item), w.Market(line.Item)));
                missing += SupplierRules.Cents(line.Units - brought, line.Mills);
                units += brought;
            }
            // The remedy: only for a failure that is the supplier's own and that the customer did not sign knowing of.
            bool theirs = SupplierRules.Theirs(c.Why) && !c.Knew && (c.Out == Arrival.Late || c.Out == Arrival.Short);
            if (theirs)
            {
                long credit = c.Out == Arrival.Short ? SupplierRules.ShortCredit(missing) : SupplierRules.LateCredit(whole, c.Slip);
                c.Credit += credit;
                c.Failures++;
                if (credit > 0)
                {
                    s.Say(SupplyEventKind.Credit, c, c.Supplier, credit, c.Failures);
                }
                if (c.Failures == SupplierCatalogue.FailuresToLeave)
                {
                    s.Say(SupplyEventKind.MayLeave, c, c.Supplier);
                }
            }
            if (c.Out == Arrival.Whole || c.Out == Arrival.None)
            {
                f.Brought++;
            }
            else
            {
                f.Missed++;
            }
            long used = Math.Min(c.Credit, due), net = due - used;
            bool paid = net == 0;
            if (!paid && w.Cash >= net)
            {
                paid = w.Pay(net, GoodsKey, c.Place, c.Supplier);   // the money leaves here, and the books of it and the contract's step follow with no call of the world between
            }
            long owed = paid ? 0L : SupplierRules.WithFee(net, def.Late);
            if (!paid && (def.Late == LatePolicy.Strict || s.OwedAll() + owed > SupplierRules.AtLevel(SupplierCatalogue.OwedMost, w.Level)))
            {
                // A strict supplier brings nothing without payment, and none delivers on account past the most a customer may owe in all: the goods stay with it and
                // nothing is charged for them.
                c.Withheld++;
                Late(s, w.Day);
                s.Say(SupplyEventKind.Withheld, c, c.Supplier, net, c.Withheld);
                if (c.Withheld >= SupplierCatalogue.WithheldMost)
                {
                    EndUnpaid(s, w, c);
                    return;
                }
                Step(w, c);
                Finish(s, w, c);
                return;
            }
            c.Credit -= used;
            if (paid)
            {
                c.Paid += net;
                s.Record.OnTime += net > 0 ? 1 : 0;
            }
            else
            {
                Owe(s, new OwedLine { Supplier = c.Supplier, Contract = c.Id, Cents = owed, Since = w.Day, Due = w.Day + SupplierCatalogue.GraceDays[(int)def.Late] });
                Late(s, w.Day);
                s.Say(SupplyEventKind.OnAccount, c, c.Supplier, owed, w.Day + SupplierCatalogue.GraceDays[(int)def.Late]);
            }
            Step(w, c);   // the delivery is made: from here a fault leaves the contract at its next delivery with its money paid, never the delivery charged again
            if (paid && def.Owner >= 0 && !f.Sold && net > 0)
            {
                w.OwnerGain(def.Owner, net * SupplierCatalogue.OwnerShare / 1000);
            }
            // Take or pay: everything brought is paid for; what finds no room stays with the supplier.
            int left = 0;
            for (int i = 0; i < c.Lines.Count; i++)
            {
                ContractLine line = c.Lines[i];
                int brought = w.Backorder(line.Item) ? 0 : SupplierRules.Brought(line.Units, line.Kind, outcome, outShare, outKind);
                if (brought <= 0)
                {
                    continue;
                }
                int took = SupplierCatalogue.Clamp(w.Deliver(c.Place, line.Item, brought, SupplierRules.ChargeMills(line.Mills, w.Export(line.Item), w.Market(line.Item))), 0, brought);
                left += brought - took;
                if (took > 0)
                {
                    s.Log.Add(new DeliveryLog { Day = w.Day, Place = c.Place, Item = line.Item, Units = took });
                }
            }
            if (left > 0)
            {
                c.NotTaken += left;
                s.Say(SupplyEventKind.NotTaken, c, c.Supplier, left);
            }
            s.Say(SupplyEventKind.Delivered, c, c.Supplier, net, units, (int)outcome);
            Finish(s, w, c);
        }

        private static void Late(SupplierState s, int day)
        {
            s.Record.Late++;
            s.Record.LateDays.Add(day);
            if (s.Record.LateDays.Count > 8)
            {
                s.Record.LateDays.RemoveAt(0);
            }
        }

        // A delivery is behind the contract: the next one a week after the day this one was due (a late one never moves the weeks after it), or the term's end. Plain state
        // only, no call of the world.
        private static void Step(ISupplierWorld w, Contract c)
        {
            c.Made++;
            c.Out = Arrival.None;
            c.Why = Cause.None;
            c.Slip = 0;
            c.OutShare = 1000;
            c.OutKind = -1;
            if (c.Made < c.Deliveries)
            {
                c.Due += SupplierCatalogue.WeekDays;
                return;
            }
            c.State = ContractState.Done;
            c.Ended = w.Day;
        }

        // What follows a step that ended the term: the contract's end proper (its credit paid out) and, for a fixed price, the buyer's renewal.
        private static void Finish(SupplierState s, ISupplierWorld w, Contract c)
        {
            if (c.State != ContractState.Done)
            {
                return;
            }
            End(s, w, c, ContractState.Done);
            if (c.Fixed)
            {
                s.Say(SupplyEventKind.EndedDone, c, c.Supplier, c.Paid);
                Renew(s, w, c);
            }
        }

        // ---- the buyer's rules

        // True while an offer is inside the price limit of a contract's rules: no more over the market's price than the customer set (the last step is any price).
        private static bool Within(Quote q, Contract rules)
        {
            int limit = SupplierCatalogue.RenewLimits[SupplierCatalogue.Clamp(rules.RenewLimit, 0, SupplierCatalogue.RenewLimits.Length - 1)];
            return q.Value * 1000 <= q.MarketValue * (1000L + limit);
        }

        /// <summary>
        /// The one gate every order a buyer places for the customer goes through (a renewal, a switch after the right to leave, a cover for a failed delivery): somebody
        /// decides at the place today, the offer is inside the price limit of the contract's rules, the cash in hand covers what is already promised for the week and this
        /// offer's first delivery with the investors' floor kept, and the buyer's budget has room for it, which it then spends. The player's own signing is not gated: it is
        /// their choice.
        /// </summary>
        private static bool Allowed(SupplierState s, ISupplierWorld w, Quote q, string place, Contract rules)
        {
            if (!w.Buyer(place) || !w.PlaceOpen(place) || !Within(q, rules))
            {
                return false;
            }
            long promised = s.OwedAll();
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                promised += s.Contracts[i].Running ? Value(s.Contracts[i]) : 0L;
            }
            return w.Cash - promised >= q.Value && w.Spend(place, (q.Value + 99) / 100);
        }

        // A term has ended: where the place has a buyer and the rule says so, the same supplier again or the cheapest that offers, through the gate.
        private static void Renew(SupplierState s, ISupplierWorld w, Contract c)
        {
            if (c.Renew == RenewRule.Off || !w.Buyer(c.Place) || !w.PlaceOpen(c.Place))
            {
                return;
            }
            int step = ShareStep(c.Share);
            if (!Best(s, w, c.Place, c.Weeks, step, c.Renew == RenewRule.Same ? c.Supplier : 0, 0, BestQuote, KindsOf(c)) || !Allowed(s, w, BestQuote, c.Place, c))
            {
                s.Say(SupplyEventKind.NoCover, c, c.Supplier, 1);
                return;
            }
            Contract made = Make(s, w, BestQuote, c.Place, c.PlaceName, c);
            s.Say(made.Supplier == c.Supplier ? SupplyEventKind.Renewed : SupplyEventKind.Switched, made, made.Supplier, Value(made), made.Due, c.Supplier);
        }

        /// <summary>
        /// The cheapest offer a place can have for a term: of one supplier (<paramref name="only"/>) or of all but one (<paramref name="not"/>). False when nobody offers.
        /// </summary>
        /// <param name="kinds">the kinds of goods the offer must bring, a bit each (a renewal brings what the contract brought, from a supplier that trades all of it); -1 for any</param>
        public static bool Best(SupplierState s, ISupplierWorld w, string place, int weeks, int shareStep, int only, int not, Quote into, int kinds = -1)
        {
            bool found = false;
            for (int i = 0; i < SupplierCatalogue.Firms.Length; i++)
            {
                int id = SupplierCatalogue.Firms[i].Id;
                if ((only != 0 && id != only) || id == not || (kinds != -1 && (SupplierCatalogue.Firms[i].Kinds & kinds) != kinds))
                {
                    continue;
                }
                Offer(s, w, id, place, weeks, shareStep, Scratch, kinds);
                // The cheapest for the goods it brings: by its price against the market's, so a supplier that brings less is not taken for cheaper.
                if (Scratch.Why == Refusal.None && (!found || Scratch.AgainstMarket < into.AgainstMarket))
                {
                    Copy(Scratch, into);
                    found = true;
                }
            }
            return found;
        }

        /// <summary>The kinds of goods a contract brings, a bit each.</summary>
        public static int KindsOf(Contract c)
        {
            int kinds = 0;
            for (int i = 0; i < c.Lines.Count; i++)
            {
                kinds |= 1 << c.Lines[i].Kind;
            }
            return kinds;
        }

        private static void Copy(Quote from, Quote to)
        {
            to.Why = from.Why;
            to.Supplier = from.Supplier;
            to.Weeks = from.Weeks;
            to.Share = from.Share;
            to.Discount = from.Discount;
            to.Level = from.Level;
            to.FirstDay = from.FirstDay;
            to.Value = from.Value;
            to.MarketValue = from.MarketValue;
            to.Min = from.Min;
            to.LeftOut = from.LeftOut;
            to.Lines.Clear();
            for (int i = 0; i < from.Lines.Count; i++)
            {
                ContractLine l = from.Lines[i];
                to.Lines.Add(new ContractLine { Item = l.Item, Kind = l.Kind, Units = l.Units, Mills = l.Mills });
            }
        }

        // Tomorrow's delivery is late or short and the rule says cover it: one delivery at the day's price from the quickest other supplier, for what will be missing
        // (a late one: the days' share of the week). Bought by the same rules as any order, so take or pay and the minimum order hold for it too.
        private static void Cover(SupplierState s, ISupplierWorld w, Contract c)
        {
            if (!c.Cover || c.Covering || !w.Buyer(c.Place))
            {
                return;
            }
            Missing.Clear();
            for (int i = 0; i < c.Lines.Count; i++)
            {
                ContractLine line = c.Lines[i];
                int units = c.Out == Arrival.Short ? line.Units - SupplierRules.Brought(line.Units, line.Kind, c.Out, c.OutShare, c.OutKind)
                    : (int)(((long)line.Units * c.Slip + SupplierCatalogue.WeekDays - 1) / SupplierCatalogue.WeekDays);
                if (units > 0)
                {
                    Missing.Add(new ContractLine { Item = line.Item, Kind = line.Kind, Units = units });
                }
            }
            int best = 0, bestDay = int.MaxValue;
            for (int i = 0; i < SupplierCatalogue.Firms.Length; i++)
            {
                int id = SupplierCatalogue.Firms[i].Id;
                if (id == c.Supplier)
                {
                    continue;
                }
                OfferFor(s, w, id, c.Place, Missing, Scratch);
                if (Scratch.Why == Refusal.None && Within(Scratch, c) && (Scratch.FirstDay < bestDay || (Scratch.FirstDay == bestDay && Scratch.AgainstMarket < BestQuote.AgainstMarket)))
                {
                    Copy(Scratch, BestQuote);
                    best = id;
                    bestDay = Scratch.FirstDay;
                }
            }
            Missing.Clear();
            if (best == 0 || !Allowed(s, w, BestQuote, c.Place, c))
            {
                s.Say(SupplyEventKind.NoCover, c, c.Supplier, 0);
                return;
            }
            Contract made = Make(s, w, BestQuote, c.Place, c.PlaceName, null, true);
            s.Say(SupplyEventKind.Covered, made, made.Supplier, Value(made), made.Due, c.Supplier);
        }

        // ---- tomorrow's delivery, judged today

        private static void JudgeNext(SupplierState s, ISupplierWorld w, Contract c)
        {
            if (!c.Running || c.JudgedFor == c.Due)
            {
                return;
            }
            SupplierDef def = SupplierCatalogue.Firm(c.Supplier);
            FirmState f = s.Firm(c.Supplier);
            int kinds = KindsOf(c);
            for (int k = 0; k < Shortages.Length; k++)
            {
                Shortages[k] = w.ShortageOn(k, c.Due);
            }
            Judged j = SupplierRules.Judge(s.Seed, def, f, c.Due, kinds, w.Conditions(c.Due), Shortages);
            c.Out = j.Out;
            c.Why = j.Why;
            c.Slip = j.Out == Arrival.Late ? j.Days : 0;
            c.OutShare = j.Out == Arrival.Short ? j.Share : 1000;
            c.OutKind = j.Kind;
            c.JudgedFor = c.Due;
            if (j.Out == Arrival.Late || j.Out == Arrival.Short)
            {
                s.Say(SupplyEventKind.Tomorrow, c, c.Supplier, j.Out == Arrival.Late ? j.Days : 1000 - j.Share, c.Due, (int)j.Why + (c.Knew ? SupplyEvent.KnewBit : 0));
                Cover(s, w, c);
            }
        }

        // ---- the day

        /// <summary>
        /// A game day, once: every supplier's own books (and the contracts of one that went under), the places that are no longer the customer's, what is owed collected
        /// or acted on, the right to leave used where the rule says so, a term's end told, tomorrow's deliveries judged, and the log and the paper's notes kept short.
        /// </summary>
        public static void Day(SupplierState s, ISupplierWorld w)
        {
            int day = w.Day;
            for (int k = 0; k < Factors.Length; k++)
            {
                Factors[k] = w.Factor(k);
            }
            for (int i = 0; i < s.Firms.Length; i++)
            {
                SupplierDef def = SupplierCatalogue.Firms[i];
                FirmState f = s.Firms[i];
                SupplierRules.FirmDayResult r = SupplierRules.FirmDay(s.Seed, def, f, day, Factors, w.Cycle);
                int held = Held(s, def.Id);
                if (r.Under)
                {
                    Note(s, day, NoteKind.Under, def.Id, f.Customers, 0);
                    s.Say(SupplyEventKind.FirmUnder, null, def.Id, f.ReopenDay, 0, held);
                    Snapshot(s);
                    for (int n = 0; n < Ids.Count; n++)
                    {
                        Contract gone = s.ContractOf(Ids[n]);
                        if (gone != null && gone.Running && gone.Supplier == def.Id)
                        {
                            End(s, w, gone, ContractState.Failed);
                        }
                    }
                    f.Customers = 0;
                }
                if (r.Trouble)
                {
                    Note(s, day, NoteKind.Trouble, def.Id, f.TroubleKind, f.TroubleUntil);
                    s.Say(SupplyEventKind.FirmTrouble, null, def.Id, f.TroubleKind, f.TroubleUntil, held);
                }
                if (r.Back)
                {
                    Note(s, day, NoteKind.Back, def.Id, 0, 0);
                    s.Say(SupplyEventKind.FirmBack, null, def.Id, 0, 0, 0);
                }
                if (r.Grew)
                {
                    Note(s, day, NoteKind.Grew, def.Id, f.Size, 0);
                    s.Say(SupplyEventKind.FirmGrew, null, def.Id, f.Size, 0, held);
                }
            }
            Snapshot(s);
            for (int i = 0; i < Ids.Count; i++)
            {
                Contract c = s.ContractOf(Ids[i]);
                if (c != null && c.Running && !w.PlaceOpen(c.Place))
                {
                    EndPlace(s, w, c);
                }
            }
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                if (!s.Contracts[i].Running)
                {
                    PayOutCredit(s, w, s.Contracts[i]);   // an ended contract's credit that a fault kept from its payout
                }
            }
            Collect(s, w);
            Snapshot(s);
            for (int i = 0; i < Ids.Count; i++)
            {
                Contract c = s.ContractOf(Ids[i]);
                if (c == null || !c.Running)
                {
                    continue;
                }
                if (c.LeaveRule && MayLeave(c) && w.Buyer(c.Place))
                {
                    int supplier = c.Supplier;
                    Leave(s, w, c.Id);
                    if (Best(s, w, c.Place, c.Weeks, ShareStep(c.Share), 0, supplier, BestQuote, KindsOf(c)) && Allowed(s, w, BestQuote, c.Place, c))
                    {
                        Contract made = Make(s, w, BestQuote, c.Place, c.PlaceName, c);
                        s.Say(SupplyEventKind.Switched, made, made.Supplier, Value(made), made.Due, supplier);
                    }
                    else
                    {
                        s.Say(SupplyEventKind.NoCover, c, supplier, 1);   // none inside the limit and the cash: the customer is told, and chooses
                    }
                    continue;
                }
                if (c.Fixed && !c.Reminded && c.Left == 1 && c.Due - day <= SupplierCatalogue.RenewAheadDays)
                {
                    c.Reminded = true;
                    s.Say(SupplyEventKind.Ending, c, c.Supplier, (int)c.Renew, c.Due);
                }
                if (c.Due == day + 1)
                {
                    JudgeNext(s, w, c);
                }
            }
            for (int i = s.Log.Count - 1; i >= 0; i--)
            {
                if (day - s.Log[i].Day > SupplierCatalogue.LogDays)
                {
                    s.Log.RemoveAt(i);
                }
            }
            if (s.Log.Count > SupplierCatalogue.LogMost)
            {
                s.Log.RemoveRange(0, s.Log.Count - SupplierCatalogue.LogMost);
            }
            for (int i = s.Notes.Count - 1; i >= 0; i--)
            {
                if (day - s.Notes[i].Day > SupplierCatalogue.PaperKept)
                {
                    s.Notes.RemoveAt(i);
                }
            }
            for (int i = 0; i < s.RivalBooks.Length; i++)
            {
                if (s.RivalBooks[i].MonthOf != w.Month)
                {
                    s.RivalBooks[i].MonthOf = w.Month;
                    s.RivalBooks[i].Month = 0;
                    s.RivalBooks[i].Failures = 0;
                }
            }
            s.LastDay = day;
        }

        private static int Held(SupplierState s, int supplier)
        {
            int n = 0;
            for (int i = 0; i < s.Contracts.Count; i++)
            {
                n += s.Contracts[i].Running && s.Contracts[i].Supplier == supplier ? 1 : 0;
            }
            return n;
        }

        private static void Note(SupplierState s, int day, NoteKind kind, int firm, int a, int b)
        {
            if (s.Notes.Count >= SupplierCatalogue.NotesMost)
            {
                s.Notes.RemoveAt(0);
            }
            s.Notes.Add(new SupplyNote { Day = day, Kind = kind, Firm = firm, A = a, B = b });
        }

        /// <summary>
        /// What is owed to suppliers, the oldest first: paid from the cash as far as it goes, the very day there is some. A delivery's price still unpaid after the
        /// supplier's days of grace ends its contract, with the settlement, and marks the record; nothing owed is ever dropped or hidden.
        /// </summary>
        public static long Collect(SupplierState s, ISupplierWorld w)
        {
            long paid = 0;
            for (int i = 0; i < s.Owed.Count; i++)
            {
                OwedLine o = s.Owed[i];
                Contract c = s.ContractOf(o.Contract);
                long pay = Math.Min(o.Cents, Math.Max(0L, w.Cash));   // Cash is what may be paid with: never the investors' floor
                if (pay > 0 && w.Pay(pay, OwedKey, c != null ? c.Place : null, o.Supplier))
                {
                    o.Cents -= pay;
                    paid += pay;
                }
                if (o.Cents <= 0)
                {
                    s.Owed.RemoveAt(i--);
                    continue;
                }
                if (w.Day > o.Due && !o.Acted)
                {
                    o.Acted = true;   // acted on once, a delivery's price and a settlement alike: from here it is a debt and the record and the supplier's memory hold it
                    if (c != null && c.Running)
                    {
                        EndUnpaid(s, w, c);
                    }
                    else
                    {
                        s.Record.Defaults++;
                        FirmState f = s.Firm(o.Supplier);
                        if (f != null)
                        {
                            f.SourUntil = w.Day + SupplierCatalogue.SourDays;
                        }
                    }
                }
            }
            if (paid > 0)
            {
                s.Say(SupplyEventKind.OwedPaid, null, 0, paid, s.OwedAll());
            }
            return paid;
        }

        // ---- the rivals

        /// <summary>
        /// One shop of the city on its day: what the supply side takes off (or adds to) the day the game valued, and its supplier's count of customers. The caller writes
        /// the cents into the shop's day.
        /// </summary>
        /// <param name="entryCount">how many valued days the game holds for the shop; -1 where the caller keeps no count</param>
        /// <param name="entryBits">the bits of the last of them, the entry the cents would come off</param>
        public static RivalDay RivalShop(SupplierState s, ISupplierWorld w, string shopKey, int kind, int dayCents, int[] customers, int entryCount = -1, int entryBits = 0)
        {
            return RivalShop(s, w, SupplierRules.Text(shopKey), kind, dayCents, customers, entryCount, entryBits);
        }

        /// <summary>The same by the shop's number (<see cref="SupplierRules.ShopId"/>), which needs no string made: the per shop loop allocates nothing.</summary>
        public static RivalDay RivalShop(SupplierState s, ISupplierWorld w, int shop, int kind, int dayCents, int[] customers, int entryCount = -1, int entryBits = 0)
        {
            RivalDay r;
            if (entryCount >= 0 && s.WasCharged(shop, entryCount, entryBits))
            {
                // The entry is the very one charged already (a load in the hour the game's valuation was part done leaves some shops on the day before): it is not charged again.
                r = new RivalDay { Skipped = true, Firm = SupplierRules.PickFirm(s.Seed, shop, kind, s) };
                if (r.Firm > 0 && customers != null && r.Firm <= customers.Length)
                {
                    customers[r.Firm - 1]++;
                }
                return r;
            }
            for (int k = 0; k < Shortages.Length; k++)
            {
                Shortages[k] = w.ShortageOn(k, w.Day);
            }
            r = SupplierRules.Rival(s.Seed, shop, kind, s, w.Day, w.Factor(kind), w.Conditions(w.Day), Shortages, dayCents);
            if (r.Firm > 0 && customers != null && r.Firm <= customers.Length)
            {
                customers[r.Firm - 1]++;
            }
            if (r.Failed)
            {
                s.ShopsShort++;
                FirmState f = s.Firm(r.Firm);
                if (f != null)
                {
                    f.Missed++;
                }
            }
            return r;
        }

        /// <summary>The cents of a rival shop's day were taken off its last valued day: that entry is remembered, so a day is never charged twice.</summary>
        public static void RivalCharged(SupplierState s, int shop, int entryCount, int entryBits)
        {
            s.Charged(shop, entryCount, entryBits);
        }

        /// <summary>The rival shops' day is done: each supplier's customers counted, and a note for the paper when shops were left short.</summary>
        public static void RivalsDone(SupplierState s, int day, int[] customers)
        {
            for (int i = 0; i < s.Firms.Length && customers != null && i < customers.Length; i++)
            {
                s.Firms[i].Customers = s.Firms[i].Open ? customers[i] : 0;
            }
            if (s.ShopsShort > 0)
            {
                Note(s, day, NoteKind.Short, 0, s.ShopsShort, 0);
            }
            s.ShopsShort = 0;
            s.RivalDay = day;
        }

        /// <summary>
        /// The rival tycoons' outlets, by the same rule as any shop of the city: each buys a day's goods from the supplier that suits it, most on a fixed price, and pays
        /// more or less than calm prices for it; a late or short delivery costs it sales. What it comes to moves the tycoon's worth, and their month's sum is kept for
        /// the rich list.
        /// </summary>
        public static void Outlets(SupplierState s, ISupplierWorld w, IList<RivalOutletRef> outlets)
        {
            for (int k = 0; k < Shortages.Length; k++)
            {
                Shortages[k] = w.ShortageOn(k, w.Day);
            }
            int goods = (int)Math.Min(int.MaxValue / 4, SupplierRules.AtLevel(SupplierCatalogue.OutletDayGoods, w.Level) * 1000 / SupplierCatalogue.RivalGoodsShare);
            for (int i = 0; outlets != null && i < outlets.Count; i++)
            {
                RivalOutletRef o = outlets[i];
                if (o.Tycoon < 0 || o.Tycoon >= s.RivalBooks.Length || o.Kind < 0 || o.Kind >= SupplierCatalogue.Kinds)
                {
                    continue;
                }
                RivalDay r = SupplierRules.Rival(s.Seed, SupplierRules.Text(o.Key), o.Kind, s, w.Day, w.Factor(o.Kind), w.Conditions(w.Day), Shortages, goods,
                    1000);   // their day is valued at calm prices
                RivalSupply book = s.RivalBooks[o.Tycoon];
                book.Firm = r.Firm;
                book.Failures += r.Failed ? 1 : 0;
                long money = r.Cents / 100;   // whole money, the very figure the month's sum keeps
                if (money != 0)
                {
                    book.Month += money;
                    book.Total += money;
                    w.RivalMoved(o.Tycoon, money * 100);
                }
            }
        }
    }
}
