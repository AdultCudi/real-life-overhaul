using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Suppliers;
using RealLifeOverhaul.Ui;

// Suppliers and contracts (Scripts/Suppliers, run with --suppliers). Every check drives the shipped rules and the shipped engine (SupplierRules, SupplierEngine,
// SupplierStore) through a world of the checker's own in place of the game, so a rule broken fails here: the catalogue, a fixed price and what moves it, how a supplier
// judges a customer, an offer's goods, deliveries under take or pay, how a delivery is judged to come (from the seed and the day alone), the remedy and the right to
// leave, every exploit (resale, signing and breaking in a loop, a low locked for ever, loading again, credits farmed), what is owed and how a contract ends, a supplier's
// own books over twenty years, the rivals by the same rules, the buyer's rules, the manager's order, the save (in the middle of a term, an old one, a newer one, a
// damaged one, a culture with decimal commas), the option off and on, the pages and every button, the sources and the texts in every language.
internal static partial class Program
{
    private static readonly string[] SupplierLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };

    private static void Suppliers()
    {
        SupplierCatalogueChecks();
        SupplierPriceChecks();
        SupplierCustomerChecks();
        SupplierOfferChecks();
        SupplierDeliveryChecks();
        SupplierJudgeChecks();
        SupplierRemedyChecks();
        SupplierExploitChecks();
        SupplierOwedChecks();
        SupplierFirmChecks();
        SupplierRivalChecks();
        SupplierBuyerChecks();
        SupplierStoreChecks();
        SupplierPauseChecks();
        SupplierPageChecks();
        SupplierSourceChecks();
        SupplierTextChecks();
    }

    // ---- a world in place of the game: a clock, cash, a market of six products (one a kind), places with room and stock

    private sealed class SupplyWorld : ISupplierWorld
    {
        public static readonly string[] Items = { "apples", "bread", "shirts", "phones", "chairs", "pens" };
        public static readonly long[] Own = { 1250, 2100, 14000, 240000, 60000, 800 };   // mills a unit at calm prices
        public int DayNow = 100, HourNow = SupplierCatalogue.DeliveryHour, LevelNow = 1000, CreditNow = 700, CycleNow = 1, MonthDays = 5;
        public long CashCents = 100000000;
        public readonly int[] Factors = { 1000, 1000, 1000, 1000, 1000, 1000 };
        public int ShortKind = -1, ShortFrom, ShortUntil, ShortSize;        // a market shortage of a kind over days of the calendar, both counted
        public int EventMille = 1000;                                       // the game's own whole-market event on every price
        public int ExportMille = 650;                                       // the game's export multiplier
        public readonly Dictionary<int, CityDay> Days = new Dictionary<int, CityDay>();
        public readonly HashSet<string> Backorders = new HashSet<string>(), Shut = new HashSet<string>(), Buyers = new HashSet<string>();
        public readonly Dictionary<string, int> Room = new Dictionary<string, int>(), Stock = new Dictionary<string, int>(), Sold = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Boxes = new Dictionary<string, int>(), Mosts = new Dictionary<string, int>();
        public readonly List<KeyValuePair<string, long>> Book = new List<KeyValuePair<string, long>>();   // every money line: its key and cents (out negative)
        public readonly long[] Tycoons = new long[3];
        public long OwnerGains;
        public int Far;

        public SupplyWorld()
        {
            for (int i = 0; i < Items.Length; i++)
            {
                Sold[Items[i]] = i < 2 ? 240 : 24;
            }
        }

        public static int KindOf(string item) => Array.IndexOf(Items, item);
        public int Day => DayNow;
        public int Hour => HourNow;
        public int Month => DayNow / MonthDays;
        public long Cash => CashCents;
        public int Level => LevelNow;
        public int Credit => CreditNow;
        public int Cycle => CycleNow;
        public int Factor(int kind) => Factors[kind];
        public int ShortageOn(int kind, int day) => kind == ShortKind && day >= ShortFrom && day <= ShortUntil ? ShortSize : 0;

        public void ShortAhead(int kind, out int from, out int days, out int size)
        {
            from = days = size = 0;
            if (kind == ShortKind && ShortUntil >= DayNow)
            {
                from = Math.Max(0, ShortFrom - DayNow);
                days = ShortUntil - Math.Max(DayNow, ShortFrom) + 1;
                size = ShortSize;
            }
        }

        public CityDay Conditions(int day) => Days.TryGetValue(day, out CityDay d) ? d : new CityDay { Season = 1 };
        public long Base(string item) => KindOf(item) < 0 ? 0 : Own[KindOf(item)] * LevelNow / 1000 * Factors[KindOf(item)] / 1000;
        public long Market(string item) => Base(item) * EventMille / 1000;
        public long Export(string item) => Base(item) * ExportMille / 1000;
        public bool Backorder(string item) => Backorders.Contains(item);
        public int Most(string item) => Mosts.TryGetValue(item, out int m) ? m : 5000;
        public int Box(string item) => Boxes.TryGetValue(item, out int b) ? b : 12;
        public bool PlaceOpen(string place) => !Shut.Contains(place);

        public void Wants(string place, List<ContractLine> into)
        {
            foreach (string item in Items)
            {
                if (Sold.TryGetValue(item, out int units) && units > 0)
                {
                    into.Add(new ContractLine { Item = item, Kind = KindOf(item), Units = units });
                }
            }
        }

        public int Deliver(string place, string item, int units, long mills)
        {
            int room = Room.TryGetValue(place + "|" + item, out int r) ? r : int.MaxValue / 2;
            int took = Math.Min(units, room);
            Stock[place + "|" + item] = (Stock.TryGetValue(place + "|" + item, out int s) ? s : 0) + took;
            if (room < int.MaxValue / 2)
            {
                Room[place + "|" + item] = room - took;
            }
            return took;
        }

        public bool Pay(long cents, string key, string place, int supplier)
        {
            if (cents > CashCents)
            {
                return false;
            }
            CashCents -= cents;
            Book.Add(new KeyValuePair<string, long>(key, -cents));
            return true;
        }

        public void Receive(long cents, string key, string place, int supplier)
        {
            CashCents += cents;
            Book.Add(new KeyValuePair<string, long>(key, cents));
        }

        public bool Buyer(string place) => Buyers.Contains(place);
        public int FarDays(int supplier, string place) => Far;
        public void OwnerGain(int tycoon, long cents) => OwnerGains += cents;
        public void RivalMoved(int tycoon, long cents) => Tycoons[tycoon] -= cents;
        public int StockOf(string place, string item) => Stock.TryGetValue(place + "|" + item, out int s) ? s : 0;
        public long Out(string key) => -Book.Where(b => b.Key == key).Sum(b => b.Value);
    }

    private const string SupplyShop = "Bleecker%20Street~12";

    // A supplier that never fails by its own doing: the catalogue's own, made strong and free of trouble for a check that is about something else.
    private static SupplierState CalmState(int seed = 7)
    {
        return new SupplierState { Seed = seed };
    }

    // Runs the engine from the world's day to another, a day's pass first and then the delivery hour, as the director does.
    private static void RunTo(SupplierState s, SupplyWorld w, int day)
    {
        while (w.DayNow < day)
        {
            w.DayNow++;
            SupplierEngine.Day(s, w);
            SupplierEngine.Hour(s, w);
        }
    }

    private static int CountEvents(SupplierState s, SupplyEventKind kind) => s.Events.Count(e => e.Kind == kind);

    // A day on which a supplier's delivery is whole, found by the rule itself: checks that need a quiet delivery sign for such a day.
    private static bool WholeOn(SupplierState s, SupplyWorld w, int supplier, int day, int kinds)
    {
        var none = new int[SupplierCatalogue.Kinds];
        return SupplierRules.Judge(s.Seed, SupplierCatalogue.Firm(supplier), s.Firm(supplier), day, kinds, w.Conditions(day), none).Out == Arrival.Whole;
    }

    // A seed under which a supplier's next so many weekly deliveries from a day are all whole.
    private static int QuietSeed(SupplyWorld w, int supplier, int firstDue, int deliveries, int kinds)
    {
        for (int seed = 1; seed < 5000; seed++)
        {
            var s = new SupplierState { Seed = seed };
            bool quiet = true;
            for (int i = 0; i < deliveries && quiet; i++)
            {
                quiet = WholeOn(s, w, supplier, firstDue + i * SupplierCatalogue.WeekDays, kinds);
            }
            if (quiet)
            {
                return seed;
            }
        }
        return -1;
    }

    // ---- the catalogue

    private static void SupplierCatalogueChecks()
    {
        SupplierDef[] firms = SupplierCatalogue.Firms;
        Check(firms.Length == 9 && firms.Select((f, i) => f.Id == i + 1).All(x => x) && firms.Select(f => f.NameKey).Distinct().Count() == 9, "suppliers catalogue: nine suppliers, numbered from 1 in order, each with a name of its own");
        for (int k = 0; k < SupplierCatalogue.Kinds; k++)
        {
            int n = firms.Count(f => f.Trades(k));
            Check(n >= 3, "suppliers catalogue: kind " + k + " has a choice of at least three suppliers (" + n + ")");
        }
        Check(SupplierCatalogue.Kinds == MacroCatalogue.SupplyKinds && SupplierCatalogue.KindKeys.Length == SupplierCatalogue.Kinds, "suppliers catalogue: the kinds of goods are the city's own six");
        Check(firms.All(f => f.Level >= 900 && f.Level <= 1100 && f.Reliability >= 850 && f.Reliability < 1000 && f.LeadDays >= 1 && f.LeadDays <= 4 && f.MinOrder > 0 && f.HealthStart >= 500 && f.HealthStart <= 900),
            "suppliers catalogue: every supplier's price, reliability, days to deliver, smallest order and health are inside sensible bounds");
        Check(firms.Where(f => f.Late == LatePolicy.Strict).All(s => firms.Where(f => f.Late == LatePolicy.Lenient).All(l => s.Level < l.Level && s.Reliability < l.Reliability && s.MinOrder > l.MinOrder && s.LeadDays > l.LeadDays)),
            "suppliers catalogue: a character is a trade: the cheap suppliers are less reliable, slower, ask a larger order and forgive nothing; the dear ones the other way");
        Check(firms.Count(f => f.Owner >= 0) == SupplierState.Rivals && firms.Where(f => f.Owner >= 0).Select(f => f.Owner).Distinct().Count() == SupplierState.Rivals && firms.All(f => f.Owner < SupplierState.Rivals),
            "suppliers catalogue: each rival tycoon's holding owns one supplier, the rest are firms of their own");
        Check(SupplierCatalogue.TermWeeks.SequenceEqual(new[] { 2, 4, 8 }) && SupplierCatalogue.TermDiscount.Length == 3 && SupplierCatalogue.TermDiscount[0] < SupplierCatalogue.TermDiscount[1]
              && SupplierCatalogue.TermDiscount[1] < SupplierCatalogue.TermDiscount[2] && SupplierCatalogue.TermDiscount[2] <= 100, "suppliers catalogue: terms of 2, 4 and 8 weeks, a longer one a little cheaper, never more than a tenth off");
        Check(SupplierCatalogue.Shares.SequenceEqual(SupplierCatalogue.Shares.OrderBy(x => x)) && SupplierCatalogue.Shares[SupplierCatalogue.ShareDefault] == 1000, "suppliers catalogue: the quantity steps rise and start at the week's sales");
        Check(SupplierCatalogue.GraceDays.SequenceEqual(new[] { 7, 4, 2 }) && SupplierCatalogue.LateFee.SequenceEqual(new[] { 0, 20, 50 }) && SupplierCatalogue.BreakShare == 250 && SupplierCatalogue.FailuresToLeave == 3
              && SupplierCatalogue.WeekDays == 7 && SupplierCatalogue.DeliveryHour == 8, "suppliers catalogue: the days of grace, the late fees, the settlement's share, the failures that let a customer leave, the week and the hour are as the docs say");
        Check(SupplierCatalogue.Firm(0) == null && SupplierCatalogue.Firm(10) == null && SupplierCatalogue.Firm(9).Id == 9 && SupplierCatalogue.TermStep(4) == 1 && SupplierCatalogue.TermStep(3) == -1,
            "suppliers catalogue: a supplier is found by its number and a term by its weeks, and nothing else is");
    }

    // ---- prices

    private static void SupplierPriceChecks()
    {
        Check(SupplierRules.TermFactor(28, 0, 0, 300) == 1000 && SupplierRules.TermFactor(28, 0, 7, 0) == 1000 && SupplierRules.TermFactor(0, 0, 7, 300) == 1000, "suppliers prices: with no shortage a fixed price is written at the calm price");
        Check(SupplierRules.TermFactor(28, 0, 28, 300) == 1300 && SupplierRules.TermFactor(28, 0, 60, 300) == 1300, "suppliers prices: a shortage over the whole term is priced in whole");
        Check(SupplierRules.TermFactor(28, 0, 7, 400) == 1100 && SupplierRules.TermFactor(28, 1, 7, 400) == 1100 && SupplierRules.TermFactor(14, 10, 8, 400) == 1114, "suppliers prices: a shortage is priced in for the days of the term it covers, running or announced");
        Check(SupplierRules.TermFactor(14, 20, 5, 400) == 1000, "suppliers prices: a shortage that starts after the term ends is not priced in");
        Check(SupplierRules.Calm(1500, 1500) == 1000 && SupplierRules.Calm(700, 700) == 1000 && SupplierRules.Calm(0, 1000) == 0, "suppliers prices: the calm price is the market's without the day's push on its kind");
        long calm = SupplierRules.FixedMills(1250, 1000, 1000, 1000, 0);
        Check(calm == 1250, "suppliers prices: at the market's level with nothing off, a fixed price in a calm market is the market's price (" + calm + ")");
        bool cheaper = true, exact = true;
        foreach (SupplierDef def in SupplierCatalogue.Firms)
        {
            var f = new FirmState { Health = 700 };
            int level = SupplierRules.Level(def, f);
            long spot = SupplierRules.SpotMills(100000, level, 0);
            for (int t = 0; t < SupplierCatalogue.TermWeeks.Length; t++)
            {
                long fixedPrice = SupplierRules.FixedMills(100000, 1000, 1000, level, SupplierCatalogue.TermDiscount[t]);
                cheaper &= fixedPrice < spot;
                exact &= fixedPrice == (long)Math.Ceiling(100000.0 * level / 1000 * (1000 - SupplierCatalogue.TermDiscount[t]) / 1000 - 1e-9);
            }
        }
        Check(cheaper, "suppliers prices: in a calm market every supplier's fixed price is under its own price of the day, for every term");
        Check(exact, "suppliers prices: a fixed price is the calm price, the supplier's level and the term's discount, rounded up to the mill");
        // A glut or a cheap season: the market's price of the day is low, and the fixed price is not written from it.
        long glutSpot = SupplierRules.SpotMills(760, 950, 0), glutFixed = SupplierRules.FixedMills(760, 760, 1000, 950, 50);
        Check(glutFixed > glutSpot && glutFixed == SupplierRules.FixedMills(1000, 1000, 1000, 950, 50), "suppliers prices: in a glut a fixed price is written from the calm price, over the day's low (" + glutFixed + " against " + glutSpot + ")");
        long shortFixed = SupplierRules.FixedMills(1400, 1400, SupplierRules.TermFactor(14, 0, 7, 400), 1000, 0);
        Check(shortFixed == 1200 && shortFixed < 1400, "suppliers prices: in a shortage of a week a two week price is fixed half-way, under the day's high and over the calm price");
        Check(SupplierRules.FixedMills(1, 1000, 1000, 900, 50) == 1 && SupplierRules.FixedMills(0, 1000, 1000, 1000, 0) == 0 && SupplierRules.SpotMills(0, 1000, 0) == 0, "suppliers prices: a price is never rounded to nothing, and what has no market price has none");
        Check(SupplierRules.Cents(12, 1250) == 1500 && SupplierRules.Cents(1, 1251) == 126 && SupplierRules.Cents(0, 5000) == 0 && SupplierRules.Cents(5, 0) == 0, "suppliers prices: a line's cents are rounded up, never down");
        Check(SupplierRules.ChargeMills(900, 650, 1000) == 900 && SupplierRules.ChargeMills(900, 975, 1500) == 975 && SupplierRules.ChargeMills(900, 1800, 1500) == 1500 && SupplierRules.ChargeMills(900, 0, 0) == 900,
            "suppliers prices: a unit is charged at the agreed price and never under what it fetches on export that day, held to the market's price where export pays more than the market");
        Check(SupplierRules.AtLevel(300, 1000) == 30000 && SupplierRules.AtLevel(300, 1500) == 45000, "suppliers prices: a smallest order follows the city's prices");
    }

    // ---- how a supplier judges a customer

    private static void SupplierCustomerChecks()
    {
        long min = 30000;
        Check(SupplierRules.Discount(0, min, min, 650, 0) == 0 && SupplierRules.Discount(2, min, min, 650, 0) == 20 && SupplierRules.Discount(4, min, min, 650, 0) == 35 && SupplierRules.Discount(8, min, min, 650, 0) == 50,
            "suppliers customer: a term's promise is worth its discount and one delivery none");
        Check(SupplierRules.Discount(0, min * 4, min, 650, 0) == 10 && SupplierRules.Discount(0, min * 4 - 1, min, 650, 0) == 0 && SupplierRules.Discount(0, min * 10, min, 650, 0) == 20 && SupplierRules.Discount(0, min * 100, min, 650, 0) == 20,
            "suppliers customer: size earns a volume step at four and at ten smallest orders, and no more however large");
        Check(SupplierRules.Discount(0, min, min, 740, 0) == 10 && SupplierRules.Discount(0, min, min, 739, 0) == 0 && SupplierRules.Discount(0, min, min, 579, 0) == -15 && SupplierRules.Discount(0, min, min, 0, 0) == 0,
            "suppliers customer: a very good credit rating earns a little, a poor one costs, and no rating at all is read as a clean start");
        Check(SupplierRules.Discount(0, min, min, 650, 1) == -10 && SupplierRules.Discount(0, min, min, 650, 3) == -30 && SupplierRules.Discount(0, min, min, 650, 9) == -40,
            "suppliers customer: each late payment on the record costs, up to a most");
        SupplierDef strict = SupplierCatalogue.Firm(2), firm = SupplierCatalogue.Firm(3), lenient = SupplierCatalogue.Firm(1);
        var f = new FirmState { Health = 700 };
        Check(SupplierRules.Refuse(strict, f, 10, 0, min, min, 579) == Refusal.Credit && SupplierRules.Refuse(strict, f, 10, 0, min, min, 580) == Refusal.None && SupplierRules.Refuse(firm, f, 10, 0, min, min, 499) == Refusal.Credit
              && SupplierRules.Refuse(firm, f, 10, 0, min, min, 500) == Refusal.None && SupplierRules.Refuse(lenient, f, 10, 0, min, min, 300) == Refusal.None,
            "suppliers customer: a strict supplier wants a fair rating, a firm one better than very poor, a lenient one takes anybody");
        Check(SupplierRules.Refuse(lenient, f, 10, 1, min, min, 800) == Refusal.Owes, "suppliers customer: nobody offers terms to a customer who owes a supplier money");
        Check(SupplierRules.Refuse(lenient, new FirmState { Health = 700, SourUntil = 10 }, 10, 0, min, min, 800) == Refusal.Sour && SupplierRules.Refuse(lenient, new FirmState { Health = 700, SourUntil = 10 }, 11, 0, min, min, 800) == Refusal.None,
            "suppliers customer: a supplier a customer defaulted on offers nothing until its days are over");
        Check(SupplierRules.Refuse(lenient, f, 10, 0, min - 1, min, 800) == Refusal.Small && SupplierRules.Refuse(lenient, f, 10, 0, 0, min, 800) == Refusal.NoGoods, "suppliers customer: an order under the smallest is refused");
        Check(SupplierRules.Refuse(lenient, new FirmState { ClosedDay = 5, ReopenDay = 33 }, 10, 0, min, min, 800) == Refusal.Shut && SupplierRules.Refuse(null, f, 10, 0, min, min, 800) == Refusal.Off, "suppliers customer: a supplier that went under offers nothing");
    }

    // ---- an offer's goods

    private static void SupplierOfferChecks()
    {
        var w = new SupplyWorld();
        SupplierState s = CalmState();
        var q = new Quote();
        SupplierEngine.Offer(s, w, 1, SupplyShop, 4, SupplierCatalogue.ShareDefault, q);
        Check(q.Why == Refusal.None && q.Lines.Count == 2 && q.Lines.All(l => l.Kind <= 1) && q.Lines.All(l => l.Units == 240), "suppliers offer: a supplier offers only the goods of the kinds it trades, at the week's sales (" + q.Lines.Count + " lines)");
        Check(q.Value == q.Lines.Sum(l => SupplierRules.Cents(l.Units, l.Mills)) && q.Value < q.MarketValue && q.AgainstMarket < 1030 && q.FirstDay == 101, "suppliers offer: the offer's value is its lines', and its first day the supplier's days from today");
        w.Boxes["apples"] = 50;
        SupplierEngine.Offer(s, w, 1, SupplyShop, 4, 1, q);
        Check(q.Lines.First(l => l.Item == "apples").Units == 200 && q.Lines.First(l => l.Item == "bread").Units == 180, "suppliers offer: quantities are a share of the week's sales in whole boxes, rounded up");
        w.Mosts["bread"] = 100;
        SupplierEngine.Offer(s, w, 1, SupplyShop, 4, 4, q);
        Check(q.Lines.First(l => l.Item == "bread").Units == 96, "suppliers offer: no line is over the game's own weekly limit for the product, in whole boxes");
        w.Mosts.Clear();
        w.Boxes.Clear();
        w.Far = 1;
        SupplierEngine.Offer(s, w, 2, SupplyShop, 0, 2, q);
        Check(q.FirstDay == 100 + 3 + 1 && q.Weeks == 0, "suppliers offer: a place far from the depot waits a day longer for its first delivery");
        w.Far = 0;
        SupplierEngine.Offer(s, w, 4, SupplyShop, 4, 2, q);
        Check(q.Why == Refusal.Small && q.Min == 180000 && q.MarketValue < q.Min, "suppliers offer: an order under the supplier's smallest is refused, with the smallest said");
        w.Sold.Clear();
        SupplierEngine.Offer(s, w, 1, SupplyShop, 4, 2, q);
        Check(q.Why == Refusal.NoGoods && q.Lines.Count == 0, "suppliers offer: a place that sells nothing has no offer");
        w.Sold["phones"] = 12;
        SupplierEngine.Offer(s, w, 1, SupplyShop, 4, 2, q);
        Check(q.Why == Refusal.Kind, "suppliers offer: a supplier that trades nothing the place sells says so");
        w.Shut.Add(SupplyShop);
        SupplierEngine.Offer(s, w, 8, SupplyShop, 4, 2, q);
        Check(q.Why == Refusal.Off, "suppliers offer: a place that is not the player's own or not trading has no offer");
        // Many products: a contract holds only so many.
        var many = new SupplyWorldMany();
        SupplierEngine.Offer(s, many, 1, SupplyShop, 4, 2, q);
        Check(q.Lines.Count == SupplierCatalogue.LinesMost, "suppliers offer: a contract holds at most " + SupplierCatalogue.LinesMost + " products (" + q.Lines.Count + ")");
        // One fixed price contract a place and supplier, and a most of contracts running.
        var w2 = new SupplyWorld();
        SupplierState s2 = CalmState();
        Contract first = SupplierEngine.Sign(s2, w2, 1, SupplyShop, "Shop", 4, 2, out Refusal why);
        Contract again = SupplierEngine.Sign(s2, w2, 1, SupplyShop, "Shop", 8, 2, out Refusal held);
        Contract once = SupplierEngine.Sign(s2, w2, 1, SupplyShop, "Shop", 0, 2, out Refusal onceWhy);
        Check(first != null && why == Refusal.None && again == null && held == Refusal.Held && once != null && onceWhy == Refusal.None, "suppliers offer: a place has one fixed price contract with a supplier at a time, and may still order once");
        var s3 = CalmState();
        int signed = 0;
        for (int i = 0; i < SupplierCatalogue.ContractsMost + 5; i++)
        {
            signed += SupplierEngine.Sign(s3, w2, 1, "Place~" + i, "Place", 2, 2, out Refusal full) != null ? 1 : 0;
            if (i >= SupplierCatalogue.ContractsMost)
            {
                Check(full == Refusal.Full, "suppliers offer: past the most contracts running a supplier offers nothing (" + full + ")");
            }
        }
        Check(signed == SupplierCatalogue.ContractsMost && s3.RunningCount() == SupplierCatalogue.ContractsMost, "suppliers offer: at most " + SupplierCatalogue.ContractsMost + " contracts run at once (" + signed + ")");
        Check(SupplierEngine.ShareStep(1000) == 2 && SupplierEngine.ShareStep(10) == 0 && SupplierEngine.ShareStep(9999) == SupplierCatalogue.Shares.Length - 1 && SupplierEngine.ShareStep(760) == 1, "suppliers offer: a share is read back as its nearest step");
    }

    // A world whose place sells twenty products of one kind.
    private sealed class SupplyWorldMany : ISupplierWorld
    {
        private readonly SupplyWorld _w = new SupplyWorld();
        public int Day => _w.Day;
        public int Hour => _w.Hour;
        public int Month => _w.Month;
        public long Cash => _w.Cash;
        public int Level => _w.Level;
        public int Credit => _w.Credit;
        public int Cycle => _w.Cycle;
        public int Factor(int kind) => 1000;
        public int ShortageOn(int kind, int day) => 0;
        public void ShortAhead(int kind, out int from, out int days, out int size) => from = days = size = 0;
        public CityDay Conditions(int day) => new CityDay { Season = 1 };
        public long Market(string item) => 2000;
        public long Base(string item) => 2000;
        public long Export(string item) => 1300;
        public bool Backorder(string item) => false;
        public int Most(string item) => 0;
        public int Box(string item) => 1;
        public bool PlaceOpen(string place) => true;
        public void Wants(string place, List<ContractLine> into)
        {
            for (int i = 0; i < 20; i++)
            {
                into.Add(new ContractLine { Item = "item" + i.ToString("00"), Kind = 1, Units = 500 });
            }
        }
        public int Deliver(string place, string item, int units, long mills) => units;
        public bool Pay(long cents, string key, string place, int supplier) => true;
        public void Receive(long cents, string key, string place, int supplier) { }
        public bool Buyer(string place) => false;
        public int FarDays(int supplier, string place) => 0;
        public void OwnerGain(int tycoon, long cents) { }
        public void RivalMoved(int tycoon, long cents) { }
    }

    // ---- deliveries: the week, the hour, take or pay

    private static void SupplierDeliveryChecks()
    {
        var w = new SupplyWorld();
        int seed = QuietSeed(w, 1, 101, 4, 3);
        Check(seed > 0, "suppliers delivery: a seed is found under which four deliveries running come whole");
        var s = new SupplierState { Seed = seed };
        long before = w.CashCents;
        Contract c = SupplierEngine.Sign(s, w, 1, SupplyShop, "Shop", 4, 2, out _);
        Check(c != null && w.CashCents == before && c.Paid == 0 && c.Due == 101 && c.Deliveries == 4 && c.Left == 4 && w.Book.Count == 0, "suppliers delivery: nothing is paid at signing, and the first delivery is the supplier's days away");
        long value = SupplierEngine.Value(c);
        w.DayNow = 101;
        w.HourNow = 7;
        SupplierEngine.Day(s, w);
        SupplierEngine.Hour(s, w);
        Check(c.Made == 0 && w.StockOf(SupplyShop, "apples") == 0, "suppliers delivery: nothing comes before the delivery hour");
        w.HourNow = 8;
        SupplierEngine.Hour(s, w);
        Check(c.Made == 1 && w.StockOf(SupplyShop, "apples") == 240 && w.StockOf(SupplyShop, "bread") == 240 && before - w.CashCents == value && c.Paid == value && c.Due == 108,
            "suppliers delivery: at the hour the goods are in the place, the agreed price is paid once, and the next delivery is a week on");
        SupplierEngine.Hour(s, w);
        w.HourNow = 9;
        SupplierEngine.Hour(s, w);
        Check(c.Made == 1 && before - w.CashCents == value, "suppliers delivery: an hour looked at twice, or the next hour, brings nothing again");
        Check(s.Brought(SupplyShop, "apples", 101, 7) == 240 && s.Brought(SupplyShop, "apples", 101, 8) == 0 && s.Coming(SupplyShop, "apples", 102, 108) == 240 && s.Coming(SupplyShop, "apples", 102, 122) == 720 && s.Coming(SupplyShop, "pens", 102, 122) == 0,
            "suppliers delivery: what a contract brought and what it is due to bring are counted by place, product and day");
        w.HourNow = 8;
        RunTo(s, w, 122);
        Check(c.State == ContractState.Done && c.Made == 4 && before - w.CashCents == value * 4 && w.StockOf(SupplyShop, "apples") == 960 && c.Left == 0 && s.RunningCount() == 0,
            "suppliers delivery: a term of four weeks brings four deliveries, each paid at the agreed price, and then it is over");
        Check(w.Book.All(b => b.Key == SupplierEngine.GoodsKey) && w.Book.Count == 4 && s.Record.OnTime == 4 && s.Record.Late == 0, "suppliers delivery: every payment is a line of the goods' own key, and the record counts each paid on the day");
        RunTo(s, w, 150);
        Check(before - w.CashCents == value * 4 && w.StockOf(SupplyShop, "apples") == 960, "suppliers delivery: after its term a contract brings and charges nothing more");
        // Take or pay: no room, paid all the same.
        var w2 = new SupplyWorld();
        var s2 = new SupplierState { Seed = QuietSeed(w2, 1, 101, 2, 3) };
        w2.Room[SupplyShop + "|apples"] = 100;
        Contract full = SupplierEngine.Sign(s2, w2, 1, SupplyShop, "Shop", 2, 2, out _);
        long cash2 = w2.CashCents;
        RunTo(s2, w2, 101);
        Check(cash2 - w2.CashCents == SupplierEngine.Value(full) && w2.StockOf(SupplyShop, "apples") == 100 && full.NotTaken == 140 && CountEvents(s2, SupplyEventKind.NotTaken) == 1,
            "suppliers delivery: take or pay: goods with no room are paid for in full, said, and never enter the place");
        // The price index and the agreed price: the market moving afterwards changes nothing of what is charged.
        var w3 = new SupplyWorld();
        var s3 = new SupplierState { Seed = QuietSeed(w3, 1, 101, 2, 3) };
        Contract held = SupplierEngine.Sign(s3, w3, 1, SupplyShop, "Shop", 2, 2, out _);
        long agreed = SupplierEngine.Value(held), cash3 = w3.CashCents;
        w3.Factors[0] = 1300;
        w3.Factors[1] = 800;
        RunTo(s3, w3, 108);
        Check(cash3 - w3.CashCents == agreed * 2, "suppliers delivery: a fixed price holds while the market rises and while it falls: a protection and a burden");
        // The game's own backorder: nobody brings the product, and nothing is charged for it.
        var w4 = new SupplyWorld();
        var s4 = new SupplierState { Seed = QuietSeed(w4, 1, 101, 2, 3) };
        Contract back = SupplierEngine.Sign(s4, w4, 1, SupplyShop, "Shop", 2, 2, out _);
        w4.Backorders.Add("apples");
        long cash4 = w4.CashCents;
        RunTo(s4, w4, 101);
        Check(w4.StockOf(SupplyShop, "apples") == 0 && w4.StockOf(SupplyShop, "bread") == 240 && cash4 - w4.CashCents == SupplierRules.Cents(240, back.Lines.First(l => l.Item == "bread").Mills) && back.Failures == 0 && back.Credit == 0,
            "suppliers delivery: a product on backorder across the whole market is not brought and not charged, and is nobody's failure");
        // A tycoon's supplier: its margin reaches the tycoon.
        var w5 = new SupplyWorld();
        var s5 = new SupplierState { Seed = QuietSeed(w5, 2, 103, 1, 35) };
        w5.Sold["apples"] = w5.Sold["bread"] = 2400;
        Contract owned = SupplierEngine.Sign(s5, w5, 2, SupplyShop, "Shop", 0, 4, out Refusal ownedWhy);
        RunTo(s5, w5, 103);
        Check(owned != null && w5.OwnerGains == owned.Paid * SupplierCatalogue.OwnerShare / 1000 && w5.OwnerGains > 0, "suppliers delivery: what a tycoon's supplier takes earns the tycoon its margin (" + ownedWhy + ")");
        var w6 = new SupplyWorld();
        var s6 = new SupplierState { Seed = QuietSeed(w6, 1, 101, 1, 3) };
        SupplierEngine.Sign(s6, w6, 1, SupplyShop, "Shop", 0, 2, out _);
        RunTo(s6, w6, 101);
        Check(w6.OwnerGains == 0, "suppliers delivery: a supplier that is its own earns no tycoon anything");
    }

    // ---- how a delivery is judged: from the seed and the day

    private static void SupplierJudgeChecks()
    {
        var none = new int[SupplierCatalogue.Kinds];
        var fair = new CityDay { Season = 1 };
        var strong = new FirmState { Health = 800 };
        bool same = true, bounded = true;
        for (int day = 1; day <= 400; day++)
        {
            foreach (SupplierDef def in SupplierCatalogue.Firms)
            {
                Judged a = SupplierRules.Judge(91, def, strong, day, def.Kinds, fair, none), b = SupplierRules.Judge(91, def, strong, day, def.Kinds, fair, none);
                same &= a.Out == b.Out && a.Why == b.Why && a.Days == b.Days && a.Share == b.Share && a.Kind == b.Kind;
                bounded &= a.Out == Arrival.Whole ? a.Share == 1000 && a.Days == 0 && a.Why == Cause.None
                    : a.Out == Arrival.Late ? a.Days >= 1 && a.Days <= SupplierCatalogue.LateDaysMost && a.Share == 1000
                    : a.Share >= SupplierCatalogue.ShortLeast && a.Share <= SupplierCatalogue.ShortMost && a.Days == 0;
            }
        }
        Check(same, "suppliers judge: the same supplier, day and seed are judged the same, however often they are asked");
        Check(bounded, "suppliers judge: a late delivery is one to three days late and whole, a short one brings four to nine tenths, a whole one all of it");
        // The rate in calm conditions is the supplier's own unreliability.
        foreach (int id in new[] { 1, 2, 3 })
        {
            SupplierDef def = SupplierCatalogue.Firm(id);
            int failed = 0, days = 20000;
            for (int day = 1; day <= days; day++)
            {
                failed += SupplierRules.Judge(1234, def, strong, day, 2, fair, none).Out != Arrival.Whole ? 1 : 0;
            }
            int expected = (1000 - def.Reliability) * days / 1000;
            Check(Math.Abs(failed - expected) <= expected / 5 + 20, "suppliers judge: over many days supplier " + id + " fails about its own share, " + expected + " (" + failed + ")");
        }
        // Each cause adds its chance, and names itself.
        SupplierDef steady = SupplierCatalogue.Firm(1);
        int Rate(FirmState f, int kinds, CityDay d, int[] shortage, out Dictionary<Cause, int> causes)
        {
            causes = new Dictionary<Cause, int>();
            int n = 0;
            for (int day = 1; day <= 6000; day++)
            {
                Judged j = SupplierRules.Judge(555, steady, f, day, kinds, d, shortage);
                if (j.Out != Arrival.Whole)
                {
                    n++;
                    causes[j.Why] = causes.TryGetValue(j.Why, out int c) ? c + 1 : 1;
                }
            }
            return n;
        }
        int calmRate = Rate(strong, 2, fair, none, out Dictionary<Cause, int> calmCauses);
        Check(calmCauses.Keys.All(k => k == Cause.Own), "suppliers judge: in calm conditions the only cause is the supplier's own doing");
        int ice = Rate(strong, 2, new CityDay { Season = 1, Weather = CityWeather.Ice }, none, out Dictionary<Cause, int> iceCauses);
        Check(ice > calmRate * 5 && iceCauses.ContainsKey(Cause.Weather) && iceCauses[Cause.Weather] > iceCauses.GetValueOrDefault(Cause.Own), "suppliers judge: ice makes deliveries late far more often, and the weather is named (" + ice + " against " + calmRate + ")");
        int parade = Rate(strong, 2, new CityDay { Season = 1, Event = CityEvent.Parade }, none, out Dictionary<Cause, int> paradeCauses);
        int market = Rate(strong, 2, new CityDay { Season = 1, Event = CityEvent.FarmersMarket }, none, out _);
        Check(parade > calmRate * 2 && paradeCauses.ContainsKey(Cause.Streets) && market == calmRate, "suppliers judge: a parade in the streets delays deliveries and a market does not");
        int heatFresh = Rate(strong, 1, new CityDay { Season = 2, Weather = CityWeather.Heat }, none, out Dictionary<Cause, int> heatCauses);
        int heatDry = Rate(strong, 2, new CityDay { Season = 2, Weather = CityWeather.Heat }, none, out _);
        Check(heatFresh > heatDry * 2 && heatCauses.ContainsKey(Cause.Weather) && heatDry == calmRate, "suppliers judge: a heat wave spoils fresh goods on the way and no others");
        int winterFresh = Rate(strong, 1, new CityDay { Season = 0 }, none, out Dictionary<Cause, int> winterCauses);
        int winterDry = Rate(strong, 2, new CityDay { Season = 0 }, none, out _);
        Check(winterFresh > winterDry && winterCauses.ContainsKey(Cause.Season) && winterDry == calmRate, "suppliers judge: fresh goods are scarcer in winter, other goods are not");
        int trouble = Rate(new FirmState { Health = 800, TroubleKind = 1, TroubleUntil = 99999 }, 2, fair, none, out Dictionary<Cause, int> troubleCauses);
        Check(trouble > calmRate * 5 && troubleCauses.ContainsKey(Cause.Trouble), "suppliers judge: a strike or a fire at the depot makes a supplier fail far more often");
        int weak = Rate(new FirmState { Health = 100 }, 2, fair, none, out Dictionary<Cause, int> weakCauses);
        int sound = Rate(new FirmState { Health = SupplierCatalogue.HealthWeak }, 2, fair, none, out _);
        Check(weak > calmRate * 3 && weakCauses.ContainsKey(Cause.Cash) && sound == calmRate, "suppliers judge: a supplier short of money fails more often, one at or over the line does not");
        var shortGroceries = new[] { 0, 300, 0, 0, 0, 0 };
        int shortage = Rate(strong, 2, fair, shortGroceries, out Dictionary<Cause, int> shortCauses);
        int otherKind = Rate(strong, 1, fair, shortGroceries, out _);
        Check(shortage > calmRate * 3 && shortCauses.ContainsKey(Cause.Shortage) && otherKind == Rate(strong, 1, fair, none, out _), "suppliers judge: a market shortage of a kind rations deliveries of that kind and of no other");
        bool rationed = true;
        for (int day = 1; day <= 6000; day++)
        {
            Judged j = SupplierRules.Judge(555, steady, strong, day, 3, fair, shortGroceries);
            rationed &= j.Why != Cause.Shortage || (j.Out == Arrival.Short && j.Share == 700 && j.Kind == 1);
        }
        Check(rationed, "suppliers judge: a rationed delivery is short by the shortage's size, of the short kind alone");
        Check(SupplierRules.Brought(100, 1, Arrival.Short, 700, 1) == 70 && SupplierRules.Brought(100, 0, Arrival.Short, 700, 1) == 100 && SupplierRules.Brought(100, 0, Arrival.Short, 700, -1) == 70
              && SupplierRules.Brought(100, 0, Arrival.Late, 700, -1) == 100 && SupplierRules.Brought(7, 0, Arrival.Short, 500, -1) == 3, "suppliers judge: a short delivery brings its share of each line it is short of, in whole units rounded down, and a late one all of it");
        // The chance is held under certainty however many causes meet.
        int worst = 0;
        var every = new CityDay { Season = 0, Weather = CityWeather.Ice, Event = CityEvent.Protest };
        var bad = new FirmState { Health = 0, TroubleKind = 2, TroubleUntil = 99999 };
        for (int day = 1; day <= 5000; day++)
        {
            worst += SupplierRules.Judge(8, SupplierCatalogue.Firm(2), bad, day, 3, every, new[] { 400, 400, 0, 0, 0, 0 }).Out != Arrival.Whole ? 1 : 0;
        }
        Check(worst > 4300 && worst < 4700, "suppliers judge: with every cause at once nine deliveries in ten fail and never all (" + worst + " of 5000)");
        // No reload scumming: the outcome does not depend on when the contract was signed, on the hour, or on a save and a load between.
        var w = new SupplyWorld();
        int failDay = 0;
        var probe = new SupplierState { Seed = 4242 };
        for (int day = 120; day < 2000 && failDay == 0; day++)
        {
            failDay = !WholeOn(probe, w, 2, day, 2) ? day : 0;
        }
        Check(failDay > 0, "suppliers judge: a day on which supplier 2 fails is found for the next checks (" + failDay + ")");
        Arrival[] seen = new Arrival[4];
        int[] slips = new int[4];
        for (int run = 0; run < 4; run++)
        {
            var wr = new SupplyWorld { DayNow = failDay - 3 - run * 7 };   // signed on four different days, the same weekday of delivery
            var sr = new SupplierState { Seed = 4242 };
            wr.Sold.Remove("apples");
            wr.Sold["bread"] = 2400;
            Contract cr = SupplierEngine.Sign(sr, wr, 2, SupplyShop, "Shop", 8, 2, out _);
            if (run == 2)
            {
                RunTo(sr, wr, failDay - 2);
                sr = SupplierStore.Read(SupplierStore.Write(sr), out _);   // a save and a load before the day it is told
                cr = sr.Contracts[0];
            }
            while (wr.DayNow < failDay - 1)
            {
                wr.DayNow++;
                wr.HourNow = run == 3 ? 23 : 8;   // looked at late in the day
                SupplierEngine.Day(sr, wr);
                SupplierEngine.Hour(sr, wr);
            }
            seen[run] = cr.Due == failDay ? cr.Out : Arrival.None;
            slips[run] = cr.Slip * 1000 + cr.OutShare;
        }
        Check(seen.All(o => o == seen[0]) && seen[0] != Arrival.Whole && seen[0] != Arrival.None && slips.All(x => x == slips[0]),
            "suppliers judge: the delivery of a day is judged the same whenever the contract was signed, at whatever hour, and across a save and a load (" + seen[0] + ")");
        var wt = new SupplyWorld { DayNow = failDay - 3 };
        var st = new SupplierState { Seed = 4242 };
        wt.Sold.Remove("apples");
        wt.Sold["bread"] = 2400;
        Contract told = SupplierEngine.Sign(st, wt, 2, SupplyShop, "Shop", 8, 2, out _);
        RunTo(st, wt, failDay - 2);
        Check(told.Out == Arrival.None && CountEvents(st, SupplyEventKind.Tomorrow) == 0, "suppliers judge: two days ahead nothing is judged yet");
        RunTo(st, wt, failDay - 1);
        Check(told.Out != Arrival.None && told.JudgedFor == failDay && CountEvents(st, SupplyEventKind.Tomorrow) == 1, "suppliers judge: the customer is told the day before, in time to act");
        long cashBefore = wt.CashCents;
        RunTo(st, wt, failDay);
        Check(told.Out == Arrival.Late ? told.Made == 0 && wt.CashCents == cashBefore : told.Made == 1, "suppliers judge: a late delivery does not come, and is not charged, on its day");
    }

    // ---- the remedy and the right to leave

    private static Contract ForcedContract(SupplierState s, SupplyWorld w, int supplier, int weeks, Arrival arrival, Cause why, int slip, int share)
    {
        Contract c = SupplierEngine.Sign(s, w, supplier, SupplyShop, "Shop", weeks, 2, out _);
        Force(c, arrival, why, slip, share);
        return c;
    }

    private static void Force(Contract c, Arrival arrival, Cause why, int slip, int share)
    {
        c.Out = arrival;
        c.Why = why;
        c.Slip = arrival == Arrival.Late ? slip : 0;
        c.OutShare = arrival == Arrival.Short ? share : 1000;
        c.OutKind = -1;
        c.JudgedFor = c.Due;
    }

    private static void SupplierRemedyChecks()
    {
        Check(SupplierRules.Theirs(Cause.Own) && SupplierRules.Theirs(Cause.Trouble) && SupplierRules.Theirs(Cause.Cash) && !SupplierRules.Theirs(Cause.Weather) && !SupplierRules.Theirs(Cause.Streets)
              && !SupplierRules.Theirs(Cause.Season) && !SupplierRules.Theirs(Cause.Shortage) && !SupplierRules.Theirs(Cause.None), "suppliers remedy: only the supplier's own doing, its trouble and its money are its fault");
        Check(SupplierRules.ShortCredit(100000) == 10000 && SupplierRules.ShortCredit(0) == 0 && SupplierRules.LateCredit(100000, 1) == 5000 && SupplierRules.LateCredit(100000, 3) == 15000 && SupplierRules.LateCredit(100000, 9) == 15000,
            "suppliers remedy: a tenth of what a short delivery did not bring, a twentieth of a late delivery's value a day up to three");
        // Short by the supplier's own doing: pay only for what comes, less the credit.
        var w = new SupplyWorld();
        SupplierState s = CalmState();
        Contract c = ForcedContract(s, w, 1, 4, Arrival.Short, Cause.Own, 0, 600);
        long value = SupplierEngine.Value(c), cash = w.CashCents;
        w.DayNow = c.Due;
        SupplierEngine.Hour(s, w);
        long brought = c.Lines.Sum(l => SupplierRules.Cents(l.Units * 600 / 1000, l.Mills)), missing = c.Lines.Sum(l => SupplierRules.Cents(l.Units - l.Units * 600 / 1000, l.Mills));
        long credit = SupplierRules.ShortCredit(missing);
        Check(w.StockOf(SupplyShop, "apples") == 144 && cash - w.CashCents == brought - credit && c.Failures == 1 && c.Credit == 0 && CountEvents(s, SupplyEventKind.Credit) == 1,
            "suppliers remedy: a delivery the supplier made short is paid only for what came, less a credit of a tenth of what did not (" + (cash - w.CashCents) + " of " + value + ")");
        // Late by its own doing: comes late, whole, with the credit off it.
        Force(c, Arrival.Late, Cause.Trouble, 2, 1000);
        int due = c.Due;
        cash = w.CashCents;
        w.DayNow = due;
        SupplierEngine.Hour(s, w);
        Check(c.Made == 1 && w.CashCents == cash, "suppliers remedy: a late delivery is not brought or charged on its day");
        w.DayNow = due + 2;
        SupplierEngine.Hour(s, w);
        Check(c.Made == 2 && cash - w.CashCents == value - SupplierRules.LateCredit(value, 2) && c.Failures == 2 && c.Due == due + 7, "suppliers remedy: it comes whole two days on with its credit taken off, and the week after is not moved");
        Check(!SupplierEngine.MayLeave(c) && !SupplierEngine.Leave(s, w, c.Id), "suppliers remedy: two failures give no right to leave yet");
        // The weather: no credit, not counted.
        Force(c, Arrival.Late, Cause.Weather, 1, 1000);
        cash = w.CashCents;
        w.DayNow = c.Due + 1;
        SupplierEngine.Hour(s, w);
        Check(c.Made == 3 && cash - w.CashCents == value && c.Failures == 2, "suppliers remedy: a delay that is nobody's fault earns no credit and is not counted against the supplier");
        Force(c, Arrival.Short, Cause.Shortage, 0, 700);
        cash = w.CashCents;
        w.DayNow = c.Due;
        long shortPaid = c.Lines.Sum(l => SupplierRules.Cents(l.Units * 700 / 1000, l.Mills));
        SupplierEngine.Hour(s, w);
        Check(cash - w.CashCents == shortPaid && c.State == ContractState.Done && c.Failures == 2, "suppliers remedy: a delivery rationed by a market shortage is paid only for what came, with no credit");
        // The third failure of its own: the right to leave, with nothing to pay and the credit paid out.
        var w2 = new SupplyWorld();
        SupplierState s2 = CalmState();
        Contract c2 = ForcedContract(s2, w2, 1, 8, Arrival.Short, Cause.Own, 0, 400);
        for (int i = 0; i < 3; i++)
        {
            Force(c2, Arrival.Short, Cause.Cash, 0, 400);
            w2.DayNow = c2.Due;
            SupplierEngine.Hour(s2, w2);
        }
        Check(c2.Failures == 3 && SupplierEngine.MayLeave(c2) && CountEvents(s2, SupplyEventKind.MayLeave) == 1 && SupplierEngine.BreakCost(c2) > 0, "suppliers remedy: after three failures of its own the customer is told they may leave");
        long cash2 = w2.CashCents;
        Check(SupplierEngine.Leave(s2, w2, c2.Id) && c2.State == ContractState.Left && w2.CashCents == cash2 && s2.Owed.Count == 0 && w2.Out(SupplierEngine.SettleKey) == 0, "suppliers remedy: leaving by right costs nothing and leaves nothing owed");
        // A whole delivery missed by the supplier (nothing to pay): the credit is carried, then paid out when the contract ends.
        var w3 = new SupplyWorld();
        SupplierState s3 = CalmState();
        Contract c3 = ForcedContract(s3, w3, 1, 2, Arrival.Late, Cause.Own, 3, 1000);
        w3.Backorders.Add("apples");
        w3.Backorders.Add("bread");
        w3.DayNow = c3.Due + 3;
        long cash3 = w3.CashCents;
        SupplierEngine.Hour(s3, w3);
        long carried = c3.Credit;
        Check(carried == SupplierRules.LateCredit(SupplierEngine.Value(c3), 3) && w3.CashCents == cash3, "suppliers remedy: a credit with nothing to set it against is carried on the contract");
        Force(c3, Arrival.Whole, Cause.None, 0, 1000);
        w3.DayNow = c3.Due;
        SupplierEngine.Hour(s3, w3);
        Check(c3.State == ContractState.Done && c3.Credit == 0 && w3.Out(SupplierEngine.CreditKey) == -carried && CountEvents(s3, SupplyEventKind.CreditBack) == 1, "suppliers remedy: a credit left when a contract ends is paid out, and said");
    }

    // ---- the exploits, each tried

    private static void SupplierExploitChecks()
    {
        // 1. Resale: contract goods never cost less than they fetch on export on the day they come, whatever the market did since signing.
        bool neverUnder = true;
        long gain = 0;
        foreach (int exportMille in new[] { 650, 900, 1200 })
        {
            foreach (int factor in new[] { 700, 1000, 1500 })
            {
                var w = new SupplyWorld { ExportMille = exportMille };
                var s = new SupplierState { Seed = QuietSeed(w, 2, 103, 8, 3) };
                w.Sold["apples"] = 2400;
                w.Sold["bread"] = 2400;
                Contract c = SupplierEngine.Sign(s, w, 2, SupplyShop, "Shop", 8, 4, out _);
                w.Factors[0] = w.Factors[1] = factor;   // the market moves the day after signing
                w.EventMille = 1250;
                long cash = w.CashCents;
                int units = 0;
                for (int i = 0; i < 8; i++)
                {
                    RunTo(s, w, w.DayNow + 1 > c.Due ? w.DayNow + 1 : c.Due);
                    units = w.StockOf(SupplyShop, "apples") + w.StockOf(SupplyShop, "bread");
                }
                long paid = cash - w.CashCents;
                long fetched = SupplierRules.Cents(w.StockOf(SupplyShop, "apples"), Math.Min(w.Export("apples"), w.Market("apples"))) + SupplierRules.Cents(w.StockOf(SupplyShop, "bread"), Math.Min(w.Export("bread"), w.Market("bread")));
                neverUnder &= paid >= fetched - units && c.State == ContractState.Done;   // (a cent a line of rounding)
                gain = Math.Max(gain, fetched - paid);
            }
        }
        Check(neverUnder, "suppliers exploit (resale): over a whole term, at any export rate and after any move of the market, contract goods cost at least what they fetch on export the day they come (most gained " + gain + " cents)");
        // 2. Signing and breaking in a loop gains nothing.
        var wl = new SupplyWorld();
        SupplierState sl = CalmState();
        long start = wl.CashCents;
        bool neverUp = true;
        for (int i = 0; i < 60; i++)
        {
            Contract c = SupplierEngine.Sign(sl, wl, i % 2 == 0 ? 1 : 3, SupplyShop, "Shop", SupplierCatalogue.TermWeeks[i % 3], 2, out _);
            long before = wl.CashCents;
            neverUp &= c != null && SupplierEngine.Break(sl, wl, c.Id) && wl.CashCents < before && wl.StockOf(SupplyShop, "apples") == 0;
        }
        Check(neverUp && wl.CashCents < start && wl.Book.All(b => b.Key == SupplierEngine.SettleKey && b.Value < 0), "suppliers exploit (loop): sixty contracts signed and broken at once bring no goods and only ever cost money (" + (start - wl.CashCents) + " cents)");
        var wb = new SupplyWorld();
        SupplierState sb = CalmState();
        Contract cb = SupplierEngine.Sign(sb, wb, 1, SupplyShop, "Shop", 8, 2, out _);
        Check(SupplierEngine.BreakCost(cb) == SupplierRules.Settlement(SupplierEngine.Value(cb), 8) && SupplierRules.Settlement(100000, 8) == 200000 && SupplierRules.Settlement(100000, 0) == 0 && SupplierRules.Settlement(1, 1) == 1,
            "suppliers exploit (loop): ending early costs a quarter of the value still to come, rounded up");
        Contract one = SupplierEngine.Sign(sb, wb, 1, SupplyShop, "Shop", 0, 2, out _);
        Check(SupplierEngine.BreakCost(one) == SupplierRules.Settlement(SupplierEngine.Value(one), 1) && SupplierEngine.BreakCost(one) > 0, "suppliers exploit (loop): an order placed once and called off costs its quarter too");
        // 3. A momentary low cannot be locked: in a glut the fixed price is over the day's price; and terms end.
        var wg = new SupplyWorld();
        SupplierState sg = CalmState();
        wg.Factors[0] = wg.Factors[1] = 760;
        var q = new Quote();
        bool noLow = true;
        foreach (SupplierDef def in SupplierCatalogue.Firms.Where(f => f.Trades(0)))
        {
            foreach (int weeks in SupplierCatalogue.TermWeeks)
            {
                SupplierEngine.Offer(sg, wg, def.Id, SupplyShop, weeks, 4, q);
                noLow &= q.Lines.Count > 0 && q.Value > q.MarketValue && q.Lines.All(l => l.Mills >= SupplyWorld.Own[l.Kind] * (def.Level - 20) / 1000 * (1000 - 50 - 20 - 10) / 1000);
            }
        }
        Check(noLow, "suppliers exploit (a low locked): in a glut every fixed price offered is over the day's market price, written from the calm price");
        var wa = new SupplyWorld { ShortKind = 1, ShortFrom = 101, ShortUntil = 106, ShortSize = 400 };
        SupplierState sa = CalmState();
        SupplierEngine.Offer(sa, wa, 1, SupplyShop, 2, 2, q);
        long announced = q.Lines.First(l => l.Item == "bread").Mills, plain = q.Lines.First(l => l.Item == "apples").Mills;
        Check(announced == SupplierRules.FixedMills(2100, 1000, SupplierRules.TermFactor(14, 1, 6, 400), q.Level, q.Discount) && announced * 1250 > plain * 2100,
            "suppliers exploit (a low locked): a shortage announced for tomorrow is in today's fixed price already, so signing the day before buys nothing");
        Check(SupplierCatalogue.TermWeeks.Max() == 8 && typeof(SupplierEngine).GetMethods().All(m => m.Name != "Extend" && m.Name != "SetUnits" && m.Name != "SetPrice"),
            "suppliers exploit (a low locked): a term is eight weeks at most and nothing extends one or changes its quantities or prices");
        // 4. Quantities bind: the share cannot be changed after signing, and shelves kept full do not make a delivery free.
        var wq = new SupplyWorld();
        var sq = new SupplierState { Seed = QuietSeed(wq, 1, 101, 4, 3) };
        wq.Room[SupplyShop + "|apples"] = 0;
        wq.Room[SupplyShop + "|bread"] = 0;
        Contract cq = SupplierEngine.Sign(sq, wq, 1, SupplyShop, "Shop", 4, 2, out _);
        long cashQ = wq.CashCents;
        RunTo(sq, wq, 122);
        Check(cashQ - wq.CashCents == SupplierEngine.Value(cq) * 4 && cq.NotTaken == 480 * 4 && wq.StockOf(SupplyShop, "apples") == 0, "suppliers exploit (quantities bind): a place kept full pays every delivery of the term in full and takes nothing");
        // 5. Credits cannot be farmed: over many forced failures a customer gets back less than the goods they did not get were worth.
        long credits = 0, lost = 0, values = 0;
        var wf = new SupplyWorld();
        SupplierState sf = CalmState();
        for (int i = 0; i < 40; i++)
        {
            Contract cf = ForcedContract(sf, wf, 1, 0, i % 2 == 0 ? Arrival.Short : Arrival.Late, Cause.Own, 3, 400);
            long value = SupplierEngine.Value(cf), cash = wf.CashCents;
            wf.DayNow = cf.Due + cf.Slip;
            SupplierEngine.Hour(sf, wf);
            long paid = cash - wf.CashCents;
            long got = i % 2 == 0 ? cf.Lines.Sum(l => SupplierRules.Cents(l.Units * 400 / 1000, l.Mills)) : value;
            credits += got - paid;
            lost += value - got;
            values += value;
            wf.DayNow += 1;
        }
        Check(credits > 0 && credits * 1000 <= values * SupplierCatalogue.LateCreditMost && lost > credits,
            "suppliers exploit (credits): the credits of forty failed deliveries are never over the most a failure earns, and less than the goods not brought were worth (" + credits + " of " + values + ", " + lost + " not brought)");
        // 6. The pause: the option off does not stretch a term's price over more deliveries, and nothing is charged for the days away.
        var wp = new SupplyWorld();
        var sp = new SupplierState { Seed = QuietSeed(wp, 1, 143, 4, 3) };
        Contract cp = SupplierEngine.Sign(sp, wp, 1, SupplyShop, "Shop", 4, 2, out _);
        long cashP = wp.CashCents;
        wp.DayNow = 140;   // away for forty days
        int moved = SupplierEngine.Resume(sp, wp.DayNow);
        Check(moved == 1 && cp.Made == 0 && cp.Due >= 140 && cp.Due < 147 && (cp.Due - 101) % 7 == 0 && wp.CashCents == cashP, "suppliers exploit (the pause): after a pause a contract goes on at its next weekday, nothing brought or charged for the days away");
        RunTo(sp, wp, 200);
        Check(cp.State == ContractState.Done && cp.Made == 4 && cashP - wp.CashCents == SupplierEngine.Value(cp) * 4, "suppliers exploit (the pause): the term is still its four deliveries, no more and no fewer");
    }

    // ---- what is owed, and how a contract ends

    private static void SupplierOwedChecks()
    {
        // Lenient: delivers on account, a week, no fee.
        var w = new SupplyWorld { CashCents = 0 };
        SupplierState s = CalmState();
        Contract c = ForcedContract(s, w, 1, 4, Arrival.Whole, Cause.None, 0, 1000);
        long value = SupplierEngine.Value(c);
        w.DayNow = c.Due;
        SupplierEngine.Hour(s, w);
        Check(w.StockOf(SupplyShop, "apples") == 240 && s.Owed.Count == 1 && s.Owed[0].Cents == value && s.Owed[0].Due == w.DayNow + 7 && s.Record.Late == 1 && CountEvents(s, SupplyEventKind.OnAccount) == 1 && w.Book.Count == 0,
            "suppliers owed: a lenient supplier delivers on account: what is owed is listed with its last day and said, with no fee");
        var q = new Quote();
        SupplierEngine.Offer(s, w, 3, SupplyShop, 4, 2, q);
        Check(q.Why == Refusal.Owes, "suppliers owed: while anything is owed no supplier offers terms");
        w.CashCents = value / 4;
        w.DayNow++;
        SupplierEngine.Day(s, w);
        Check(s.Owed.Count == 1 && s.Owed[0].Cents == value - value / 4 && w.CashCents == 0 && w.Out(SupplierEngine.OwedKey) == value / 4, "suppliers owed: it is taken as far as the cash goes, the very day there is some");
        w.CashCents = value;
        w.DayNow++;
        SupplierEngine.Day(s, w);
        Check(s.Owed.Count == 0 && w.CashCents == value - (value - value / 4) && c.Running && CountEvents(s, SupplyEventKind.OwedPaid) == 2, "suppliers owed: paid in full inside the days of grace, the contract runs on");
        SupplierEngine.Offer(s, w, 3, SupplyShop, 4, 2, q);
        Check(q.Why == Refusal.None && q.Discount == SupplierRules.Discount(4, q.MarketValue, q.Min, w.CreditNow, 1), "suppliers owed: once paid suppliers offer again, at a price that remembers the late payment");
        // Firm: a fee on top, four days.
        var wf = new SupplyWorld { CashCents = 0 };
        SupplierState sf = CalmState();
        Contract cf = ForcedContract(sf, wf, 3, 4, Arrival.Whole, Cause.None, 0, 1000);
        wf.DayNow = cf.Due;
        SupplierEngine.Hour(sf, wf);
        long firmValue = SupplierEngine.Value(cf);
        Check(sf.Owed.Count == 1 && sf.Owed[0].Cents == firmValue + firmValue * 20 / 1000 && sf.Owed[0].Due == wf.DayNow + 4 && SupplierRules.WithFee(100000, LatePolicy.Firm) == 102000 && SupplierRules.WithFee(100000, LatePolicy.Strict) == 105000,
            "suppliers owed: a firm supplier delivers on account with its fee on top and four days to pay");
        // Past the days of grace: the contract ends, the settlement is owed too, the record is marked, the supplier sours.
        long owedBefore = sf.OwedAll();
        RunTo(sf, wf, wf.DayNow + 4);
        Check(cf.Running && sf.Record.Defaults == 0, "suppliers owed: on the last day of grace the contract still runs");
        RunTo(sf, wf, wf.DayNow + 1);
        long settle = SupplierRules.Settlement(firmValue, 3);
        Check(cf.State == ContractState.Unpaid && sf.Record.Defaults == 1 && sf.OwedAll() == owedBefore + settle && sf.Firm(3).SourUntil == wf.DayNow + SupplierCatalogue.SourDays && CountEvents(sf, SupplyEventKind.EndedUnpaid) == 1,
            "suppliers owed: unpaid past its days the contract is ended by the supplier, the settlement owed on top, said, and the supplier offers nothing for a while");
        RunTo(sf, wf, wf.DayNow + 30);
        Check(sf.OwedAll() == owedBefore + settle && sf.Record.Defaults == 1, "suppliers owed: a debt is never dropped, never grows by itself and marks the record once");
        wf.CashCents = 100000000;
        long cashF = wf.CashCents;
        Check(SupplierEngine.Collect(sf, wf) == owedBefore + settle && sf.Owed.Count == 0 && cashF - wf.CashCents == owedBefore + settle, "suppliers owed: the whole of it is paid when the cash is there, to the cent");
        SupplierEngine.Offer(sf, wf, 3, SupplyShop, 4, 2, q);
        Refusal sour = q.Why;
        SupplierEngine.Offer(sf, wf, 1, SupplyShop, 4, 2, q);
        Check(sour == Refusal.Sour && q.Why == Refusal.None, "suppliers owed: after paying, the supplier defaulted on still refuses for its days and the others offer again");
        // Strict: nothing without payment, nothing charged for what it kept, and after two such days the end.
        var ws = new SupplyWorld { CashCents = 0 };
        SupplierState ss = CalmState();
        ws.Sold["apples"] = 2400;
        ws.Sold["bread"] = 2400;
        Contract cs = ForcedContract(ss, ws, 2, 4, Arrival.Whole, Cause.None, 0, 1000);
        ws.DayNow = cs.Due;
        SupplierEngine.Hour(ss, ws);
        Check(cs != null && ws.StockOf(SupplyShop, "apples") == 0 && ss.Owed.Count == 0 && cs.Withheld == 1 && cs.Made == 1 && cs.Running && CountEvents(ss, SupplyEventKind.Withheld) == 1,
            "suppliers owed: a strict supplier brings nothing without payment and charges nothing for the goods it kept");
        Force(cs, Arrival.Whole, Cause.None, 0, 1000);
        ws.DayNow = cs.Due;
        SupplierEngine.Hour(ss, ws);
        Check(cs.State == ContractState.Unpaid && ss.Owed.Count == 1 && ss.Owed[0].Settlement && ss.Owed[0].Cents == SupplierRules.Settlement(SupplierEngine.Value(cs), 3) && ss.Record.Defaults == 1,
            "suppliers owed: after two deliveries kept back it ends the contract, and the settlement is owed and listed");
        // The place closed or sold: the contract ends, the settlement paid or, without the cash, owed and said.
        var wc = new SupplyWorld();
        SupplierState sc = CalmState();
        Contract cc = SupplierEngine.Sign(sc, wc, 1, SupplyShop, "Shop", 8, 2, out _);
        wc.Shut.Add(SupplyShop);
        long cashC = wc.CashCents, settleC = SupplierEngine.BreakCost(cc);
        wc.DayNow++;
        SupplierEngine.Day(sc, wc);
        Check(cc.State == ContractState.Closed && cashC - wc.CashCents == settleC && sc.Owed.Count == 0 && wc.Out(SupplierEngine.SettleKey) == settleC && sc.Events.Any(e => e.Kind == SupplyEventKind.EndedPlace && e.A == settleC && e.B == 0),
            "suppliers owed: a business closed or sold ends its contracts with the settlement paid, and said");
        var wn = new SupplyWorld { CashCents = 100 };
        SupplierState sn = CalmState();
        Contract cn = SupplierEngine.Sign(sn, wn, 1, SupplyShop, "Shop", 8, 2, out _);
        wn.Shut.Add(SupplyShop);
        wn.DayNow = cn.Due;
        SupplierEngine.Hour(sn, wn);
        Check(cn.State == ContractState.Closed && wn.CashCents == 100 && sn.Owed.Count == 1 && sn.Owed[0].Cents == SupplierRules.Settlement(SupplierEngine.Value(cn), 8) && wn.StockOf(SupplyShop, "apples") == 0
              && sn.Events.Any(e => e.Kind == SupplyEventKind.EndedPlace && e.B == sn.Owed[0].Cents), "suppliers owed: with no cash the settlement of a closed business is owed, listed and said, never silent, and nothing is brought to a place that is gone");
        // The customer ends it without the cash: owed, not free.
        var wk = new SupplyWorld { CashCents = 0 };
        SupplierState sk = CalmState();
        Contract ck = SupplierEngine.Sign(sk, wk, 1, SupplyShop, "Shop", 4, 2, out _);
        Check(SupplierEngine.Break(sk, wk, ck.Id) && ck.State == ContractState.Broken && sk.OwedAll() == SupplierRules.Settlement(SupplierEngine.Value(ck), 4) && !SupplierEngine.Break(sk, wk, ck.Id),
            "suppliers owed: a broke customer who ends a contract owes the settlement, and a contract is ended once");
        // The supplier goes under: its contracts end with nothing to pay, a credit it owed is lost and said.
        var wu = new SupplyWorld();
        SupplierState su = CalmState();
        Contract cu = SupplierEngine.Sign(su, wu, 1, SupplyShop, "Shop", 8, 2, out _);
        Contract other = SupplierEngine.Sign(su, wu, 3, SupplyShop, "Shop", 8, 2, out _);
        cu.Credit = 5000;
        su.Firm(1).Health = 1;
        su.Firm(1).TroubleKind = 1;
        su.Firm(1).TroubleUntil = 99999;
        long cashU = wu.CashCents;
        wu.DayNow++;
        SupplierEngine.Day(su, wu);
        Check(!su.Firm(1).Open && cu.State == ContractState.Failed && other.Running && wu.CashCents == cashU && su.Owed.Count == 0 && CountEvents(su, SupplyEventKind.CreditLost) == 1 && CountEvents(su, SupplyEventKind.FirmUnder) == 1,
            "suppliers owed: when a supplier goes under its contracts end with nothing to pay, another supplier's run on, and a credit it owed is lost and said");
        var qu = new Quote();
        SupplierEngine.Offer(su, wu, 1, SupplyShop, 4, 2, qu);
        Check(qu.Why == Refusal.Shut, "suppliers owed: a supplier that went under offers nothing while it is shut");
        // Ended contracts are kept only so many, and never one with money owed on it.
        var wt = new SupplyWorld();
        SupplierState stt = CalmState();
        for (int i = 0; i < SupplierCatalogue.ContractsKept + 8; i++)
        {
            Contract t = SupplierEngine.Sign(stt, wt, 1, SupplyShop, "Shop", 2, 2, out _);
            SupplierEngine.Break(stt, wt, t.Id);
        }
        Check(stt.Contracts.Count == SupplierCatalogue.ContractsKept, "suppliers owed: the page keeps the last " + SupplierCatalogue.ContractsKept + " ended contracts (" + stt.Contracts.Count + ")");
    }

    // ---- a supplier's own books

    private static void SupplierFirmChecks()
    {
        var calm = new[] { 1000, 1000, 1000, 1000, 1000, 1000 };
        SupplierDef def = SupplierCatalogue.Firm(3);
        var f = new FirmState { Health = 300 };
        for (int k = 0; k < f.Book.Length; k++)
        {
            f.Book[k] = 1000;
        }
        var probe = new FirmState { Health = 300 };
        for (int k = 0; k < 6; k++)
        {
            probe.Book[k] = 1000;
        }
        int quietDay = Enumerable.Range(1, 400).First(d => !SupplierRules.FirmDay(5, def, Copy(probe), d, calm, 1).Trouble);
        SupplierRules.FirmDay(5, def, f, quietDay, calm, 1);
        Check(f.Health == 300 + SupplierCatalogue.Recover, "suppliers firm: under where it rests a supplier's health comes back a little each day (" + f.Health + ")");
        var busy = new FirmState { Health = 300, Customers = 40 };
        Fill(busy);
        SupplierRules.FirmDay(5, def, busy, quietDay, calm, 1);
        Check(busy.Health == 300 + SupplierCatalogue.Recover + SupplierCatalogue.RecoverMost && SupplierRules.Rest(def, busy) == def.HealthStart + 120 && SupplierRules.Rest(def, new FirmState { Customers = 999, Size = 3 }) <= 950,
            "suppliers firm: the more shops it serves the faster it recovers and the higher it rests, up to a most");
        var over = new FirmState { Health = 940 };
        Fill(over);
        SupplierRules.FirmDay(5, def, over, quietDay, calm, 1);
        Check(over.Health == 940 - SupplierCatalogue.Regress, "suppliers firm: over where it rests its health comes down");
        var strained = new FirmState { Health = 600 };
        Fill(strained);
        var shortage = new[] { 1400, 1400, 1000, 1000, 1000, 1000 };
        SupplierRules.FirmDay(5, def, strained, quietDay, shortage, 1);
        Check(strained.Health == 600 + SupplierCatalogue.Recover - 400 * SupplierCatalogue.BookShare / 1000 / SupplierCatalogue.StrainDiv && strained.Book[0] == 1000 + 400 / SupplierCatalogue.BookRollDays,
            "suppliers firm: a market over its book of fixed prices costs it on the fixed share, and the book follows the market slowly (" + strained.Health + ")");
        var glut = new FirmState { Health = 600 };
        Fill(glut);
        SupplierRules.FirmDay(5, def, glut, quietDay, new[] { 800, 800, 1000, 1000, 1000, 1000 }, 1);
        Check(glut.Health == 600 + SupplierCatalogue.Recover + 200 * SupplierCatalogue.BookShare / 1000 / SupplierCatalogue.StrainDiv / 2 && glut.Book[0] == 1000, "suppliers firm: a market under its book helps it half as much, and its book is never written under the calm price");
        var recession = new FirmState { Health = 600 };
        Fill(recession);
        SupplierRules.FirmDay(5, def, recession, quietDay, calm, 3);
        Check(recession.Health == 600 + SupplierCatalogue.Recover + SupplierCatalogue.CycleDrift[3] && SupplierCatalogue.CycleDrift[0] > 0 && SupplierCatalogue.CycleDrift[3] < SupplierCatalogue.CycleDrift[2], "suppliers firm: a recession weighs on it and a boom lifts it");
        var troubled = new FirmState { Health = 600, TroubleKind = 1, TroubleUntil = quietDay + 2 };
        Fill(troubled);
        SupplierRules.FirmDay(5, def, troubled, quietDay, calm, 1);
        Check(troubled.Health == 600 + SupplierCatalogue.Recover - SupplierCatalogue.TroubleDrain && troubled.InTrouble(quietDay + 2) && !troubled.InTrouble(quietDay + 3), "suppliers firm: a strike or a fire costs it each day it runs, and ends on its day");
        // Going under, staying shut, coming back under new owners.
        var sinking = new FirmState { Health = 8, TroubleKind = 2, TroubleUntil = 9999, Size = 2 };
        Fill(sinking);
        SupplierDef owned = SupplierCatalogue.Firm(2);
        SupplierRules.FirmDayResult under = SupplierRules.FirmDay(5, owned, sinking, 50, calm, 1);
        Check(under.Under && !sinking.Open && sinking.ClosedDay == 50 && sinking.ReopenDay == 50 + SupplierCatalogue.ReopenDays && sinking.Size == 1 && !sinking.InTrouble(51), "suppliers firm: at nothing a supplier goes under, shut for its days, back to its first size");
        bool shut = true;
        for (int day = 51; day < 50 + SupplierCatalogue.ReopenDays; day++)
        {
            SupplierRules.FirmDayResult r = SupplierRules.FirmDay(5, owned, sinking, day, calm, 1);
            shut &= !sinking.Open && !r.Back && !r.Trouble && !r.Grew;
        }
        SupplierRules.FirmDayResult back = SupplierRules.FirmDay(5, owned, sinking, 50 + SupplierCatalogue.ReopenDays, calm, 1);
        Check(shut && back.Back && sinking.Open && sinking.Health == SupplierCatalogue.ReopenHealth && sinking.Sold && sinking.Book.All(b => b == 1000), "suppliers firm: it stays shut for its days, then new owners open it, and a tycoon's is theirs no longer");
        // Growing.
        var strongFirm = new FirmState { Health = 940, Customers = 50 };
        Fill(strongFirm);
        int grewOn = 0;
        for (int day = 1; day <= 80 && grewOn == 0; day++)
        {
            strongFirm.Health = Math.Max(strongFirm.Health, SupplierCatalogue.GrowHealth);
            strongFirm.TroubleKind = 0;
            grewOn = SupplierRules.FirmDay(5, SupplierCatalogue.Firm(1), strongFirm, day, calm, 0).Grew ? day : 0;
        }
        Check(grewOn == SupplierCatalogue.GrowDays && strongFirm.Size == 2 && SupplierRules.Level(SupplierCatalogue.Firm(1), strongFirm) == 1030 - SupplierCatalogue.SizeOff
              && SupplierRules.Reliability(SupplierCatalogue.Firm(1), strongFirm) == 975 + SupplierCatalogue.SizeReliability, "suppliers firm: strong for " + SupplierCatalogue.GrowDays + " days running it grows a step: a little cheaper, a little more reliable (day " + grewOn + ")");
        Check(SupplierRules.Reliability(SupplierCatalogue.Firm(1), new FirmState { Size = 99 }) <= 995 && SupplierRules.HealthBand(700) == 0 && SupplierRules.HealthBand(500) == 1 && SupplierRules.HealthBand(399) == 2, "suppliers firm: no supplier is ever certain, and its books read sound, stretched or in trouble");
        // Twenty years of every supplier under a moving market and cycle: always inside bounds, some trouble, and the same from the same seed.
        bool inBounds = true, sameAgain = true;
        int troubles = 0, unders = 0, grown = 0, backs = 0;
        foreach (int seed in new[] { 3, 77, 1205 })
        {
            var a = new SupplierState { Seed = seed };
            var b = new SupplierState { Seed = seed };
            var factors = new int[6];
            for (int day = 1; day <= 60 * 20; day++)
            {
                for (int k = 0; k < 6; k++)
                {
                    int roll = SupplierRules.Roll(seed, day / 6, k, 99, 1000);
                    factors[k] = roll < 80 ? 1300 : roll < 140 ? 800 : 1000 + (k == 0 ? (day / 15 % 4 == 0 ? 80 : -40) : 0);
                }
                int cycle = SupplierRules.Roll(seed, day / 20, 0, 98, 10) < 2 ? 3 : SupplierRules.Roll(seed, day / 20, 0, 98, 10) < 4 ? 2 : 1;
                for (int i = 0; i < a.Firms.Length; i++)
                {
                    a.Firms[i].Customers = b.Firms[i].Customers = 10 + i * 4;
                    SupplierRules.FirmDayResult r = SupplierRules.FirmDay(seed, SupplierCatalogue.Firms[i], a.Firms[i], day, factors, cycle);
                    SupplierRules.FirmDay(seed, SupplierCatalogue.Firms[i], b.Firms[i], day, factors, cycle);
                    FirmState fa = a.Firms[i], fb = b.Firms[i];
                    inBounds &= fa.Health >= 0 && fa.Health <= SupplierCatalogue.HealthMost && fa.Size >= 1 && fa.Size <= SupplierCatalogue.SizeMost && fa.Book.All(x => x >= 1000 && x <= 1500) && (fa.Open || fa.ReopenDay > day);
                    sameAgain &= fa.Health == fb.Health && fa.Size == fb.Size && fa.ClosedDay == fb.ClosedDay && fa.TroubleUntil == fb.TroubleUntil;
                    troubles += r.Trouble ? 1 : 0;
                    unders += r.Under ? 1 : 0;
                    grown += r.Grew ? 1 : 0;
                    backs += r.Back ? 1 : 0;
                }
            }
        }
        Check(inBounds, "suppliers firm: over twenty years of a moving market every supplier's health, size and book stay inside their bounds");
        Check(sameAgain, "suppliers firm: the same seed gives the same twenty years");
        Check(troubles > 100 && backs <= unders && unders - backs <= 27, "suppliers firm: strikes and fires come now and then (" + troubles + "), and every supplier that went under came back or is waiting to (" + unders + " under, " + backs + " back, " + grown + " grown)");
        // The engine's day turns a supplier's trouble into a note and an event, and counts the player's contracts with it.
        var w = new SupplyWorld();
        SupplierState s = CalmState(5);
        SupplierEngine.Sign(s, w, 3, SupplyShop, "Shop", 4, 2, out _);
        int troubleDay = Enumerable.Range(101, 3000).First(d => SupplierRules.FirmDay(5, def, Copy(s.Firm(3)), d, calm, 1).Trouble);
        w.DayNow = troubleDay;
        s.Events.Clear();
        s.Contracts[0].Due = troubleDay + 50;
        SupplierEngine.Day(s, w);
        Check(s.Events.Any(e => e.Kind == SupplyEventKind.FirmTrouble && e.Firm == 3 && e.C == 1) && s.Notes.Any(n => n.Kind == NoteKind.Trouble && n.Firm == 3 && n.Day == troubleDay), "suppliers firm: a supplier's trouble is told to its customer and noted for the paper");
        w.DayNow += SupplierCatalogue.PaperKept + 1;
        s.Firm(3).TroubleUntil = 0;
        s.Firm(3).TroubleKind = 0;
        SupplierEngine.Day(s, w);
        Check(!s.Notes.Any(n => n.Day == troubleDay), "suppliers firm: a note is a story for " + SupplierCatalogue.PaperKept + " days and then gone");
    }

    private static void Fill(FirmState f)
    {
        for (int k = 0; k < f.Book.Length; k++)
        {
            f.Book[k] = 1000;
        }
    }

    private static FirmState Copy(FirmState f)
    {
        var c = new FirmState { Health = f.Health, Size = f.Size, TroubleKind = f.TroubleKind, TroubleUntil = f.TroubleUntil, ClosedDay = f.ClosedDay, ReopenDay = f.ReopenDay, Strong = f.Strong, Customers = f.Customers };
        for (int k = 0; k < f.Book.Length; k++)
        {
            c.Book[k] = f.Book[k] == 0 ? 1000 : f.Book[k];
        }
        return c;
    }

    // ---- the rivals: the same suppliers, the same rules

    private static void SupplierRivalChecks()
    {
        SupplierState s = CalmState(31);
        var fair = new CityDay { Season = 1 };
        var none = new int[6];
        bool traders = true, steady = true;
        var counts = new int[9];
        for (int shop = 1; shop <= 600; shop++)
        {
            for (int kind = 0; kind < 6; kind++)
            {
                int pick = SupplierRules.PickFirm(31, shop, kind, s);
                traders &= pick > 0 && SupplierCatalogue.Firm(pick).Trades(kind);
                steady &= pick == SupplierRules.PickFirm(31, shop, kind, s);
                counts[pick - 1]++;
            }
        }
        Check(traders && steady, "suppliers rivals: a shop of the city buys each kind from a supplier that trades it, and stays with its choice");
        Check(counts.All(c => c > 60), "suppliers rivals: the city's shops spread over all nine suppliers (" + string.Join(", ", counts) + ")");
        int pickBefore = SupplierRules.PickFirm(31, 17, 1, s);
        s.Firm(pickBefore).ClosedDay = 5;
        int pickAfter = SupplierRules.PickFirm(31, 17, 1, s);
        Check(pickAfter != pickBefore && pickAfter > 0 && SupplierCatalogue.Firm(pickAfter).Trades(1), "suppliers rivals: when its supplier goes under a shop moves to another that trades its goods");
        foreach (SupplierDef d in SupplierCatalogue.Firms.Where(f => f.Trades(1)))
        {
            s.Firm(d.Id).ClosedDay = 5;
        }
        RivalDay left = SupplierRules.Rival(31, 17, 1, s, 200, 1300, fair, none, 100000);
        Check(SupplierRules.PickFirm(31, 17, 1, s) == 0 && left.Firm == 0 && !left.Contract && left.Cents == 100000 * SupplierCatalogue.RivalGoodsShare / 1000 * 300 / 1000, "suppliers rivals: with no supplier open a shop buys at the wholesale stores, at the market's price of the day");
        s = CalmState(31);
        // A shortage: the market's price reaches the shops on the day's price and not those on a fixed price.
        long spotCost = 0, fixedCost = 0;
        int spotShops = 0, fixedShops = 0;
        bool capped = true;
        for (int shop = 1; shop <= 800; shop++)
        {
            RivalDay r = SupplierRules.Rival(31, shop, 1, s, 3, 1400, fair, none, 100000);
            RivalDay calmDay = SupplierRules.Rival(31, shop, 1, s, 3, 1000, fair, none, 100000);
            capped &= Math.Abs(r.Cents) <= 100000 * SupplierCatalogue.RivalMost / 1000 && r.Firm == calmDay.Firm && r.Contract == calmDay.Contract;
            if (r.Failed || calmDay.Failed)
            {
                continue;
            }
            if (r.Contract)
            {
                fixedCost += r.Cents - calmDay.Cents;
                fixedShops++;
            }
            else
            {
                spotCost += r.Cents - calmDay.Cents;
                spotShops++;
            }
        }
        Check(capped, "suppliers rivals: the supply side never moves a shop's day by more than " + SupplierCatalogue.RivalMost + " thousandths");
        Check(spotShops > 200 && fixedShops > 300 && Math.Abs(fixedShops * 1000 / (spotShops + fixedShops) - SupplierCatalogue.RivalContractShare) < 60, "suppliers rivals: about six shops in ten hold a fixed price (" + fixedShops + " of " + (fixedShops + spotShops) + ")");
        Check(spotCost / spotShops > 10000 && fixedCost == 0, "suppliers rivals: a shortage raises the price for the shops on the day's price, and a fixed price protects the others, as it does the player (" + spotCost / spotShops + " cents a shop)");
        s.Firm(1).Book[1] = s.Firm(2).Book[1] = s.Firm(3).Book[1] = 1300;
        long burden = 0;
        for (int shop = 1; shop <= 800; shop++)
        {
            RivalDay r = SupplierRules.Rival(31, shop, 1, s, 3, 800, fair, none, 100000);
            burden += r.Contract && !r.Failed ? r.Cents : 0;
        }
        Check(burden > 0, "suppliers rivals: a fixed price written high is a burden to a shop when the market falls, as it is to the player");
        Check(SupplierRules.Rival(31, 5, 1, s, 3, 1400, fair, none, 0).Cents == 0 && SupplierRules.Rival(31, 5, 1, s, 3, 1400, fair, none, -500).Cents == 0 && SupplierRules.Rival(31, 5, 9, s, 3, 1400, fair, none, 1000).Cents == 0,
            "suppliers rivals: a shop that made nothing, or sells no goods, pays nothing");
        // The same failure as the player's: on a shop's delivery day its supplier's delivery is judged by the very draw.
        s = CalmState(31);
        int agree = 0, failures = 0, deliveryDays = 0;
        for (int shop = 1; shop <= 300; shop++)
        {
            int weekday = SupplierRules.Roll(31, shop, 0, SupplierRules.PurposeRivalDay, 7);
            for (int day = 700; day < 900; day++)
            {
                RivalDay r = SupplierRules.Rival(31, shop, 1, s, day, 1000, fair, none, 100000);
                bool delivery = day % 7 == weekday;
                Judged j = SupplierRules.Judge(31, SupplierCatalogue.Firm(r.Firm), s.Firm(r.Firm), day, 2, fair, none);
                bool playerFails = j.Out != Arrival.Whole;
                deliveryDays += delivery ? 1 : 0;
                failures += r.Failed ? 1 : 0;
                agree += r.Failed == (delivery && playerFails) ? 1 : 0;
            }
        }
        Check(agree == 300 * 200 && failures > 100 && deliveryDays > 8000, "suppliers rivals: a supplier that fails the player on a day fails every shop it delivers to that day, and no other (" + failures + " failures in " + deliveryDays + " deliveries)");
        // The engine: customers counted, a note when shops were left short, the tycoons' outlets out of their worth.
        var w = new SupplyWorld { DayNow = 700 };
        var es = CalmState(31);
        var customers = new int[9];
        long moved = 0;
        for (int shop = 0; shop < 400; shop++)
        {
            RivalDay r = SupplierEngine.RivalShop(es, w, "Shop Street~" + shop, shop % 6, 100000, customers);
            moved += r.Cents;
        }
        int shortShops = es.ShopsShort;
        SupplierEngine.RivalsDone(es, 700, customers);
        Check(customers.Sum() == 400 && es.Firms.Select((f, i) => f.Customers == customers[i]).All(x => x) && es.RivalDay == 700 && es.ShopsShort == 0
              && (shortShops == 0 || es.Notes.Any(n => n.Kind == NoteKind.Short && n.A == shortShops)), "suppliers rivals: each supplier's customers are counted, and shops left short are a note for the paper (" + shortShops + ")");
        var outlets = new List<RivalOutletRef>();
        for (int i = 0; i < 12; i++)
        {
            outlets.Add(new RivalOutletRef { Tycoon = i % 3, Kind = i % 6, Key = "rival" + i % 3 + "#" + i });
        }
        outlets.Add(new RivalOutletRef { Tycoon = 7, Kind = 1, Key = "x" });
        outlets.Add(new RivalOutletRef { Tycoon = 1, Kind = -1, Key = "y" });
        w.Factors[1] = w.Factors[3] = 1400;
        w.DayNow = 701;
        SupplierEngine.Day(es, w);
        SupplierEngine.Outlets(es, w, outlets);
        Check(es.RivalBooks.Sum(b => b.Month) == -w.Tycoons.Sum() / 100 && es.RivalBooks.Any(b => b.Month > 0) && es.RivalBooks.All(b => b.Total == b.Month && b.MonthOf == w.Month),
            "suppliers rivals: a tycoon's outlets pay for supply out of their worth, to the dollar, and the month's sum is kept for the rich list");
        long monthBefore = es.RivalBooks[0].Month;
        w.DayNow += w.MonthDays;
        SupplierEngine.Day(es, w);
        Check(es.RivalBooks.All(b => b.Month == 0 && b.Failures == 0 && b.MonthOf == w.Month) && es.RivalBooks[0].Total == monthBefore, "suppliers rivals: at the turn of a month the month's sum starts again and the total stays");
    }

    // ---- the buyer's rules, and the manager's order

    private static void SupplierBuyerChecks()
    {
        // Renewal: only where somebody orders stock, only inside the limit.
        var w = new SupplyWorld();
        var s = new SupplierState { Seed = QuietSeed(w, 1, 101, 6, 3) };
        Contract c = SupplierEngine.Sign(s, w, 1, SupplyShop, "Shop", 2, 2, out _);
        c.Renew = RenewRule.Same;
        c.RenewLimit = 4;
        c.Cover = true;
        RunTo(s, w, 108);
        Check(c.State == ContractState.Done && s.RunningCount() == 0 && CountEvents(s, SupplyEventKind.Renewed) == 0, "suppliers buyer: with nobody who orders stock at the place the rules wait and nothing is renewed");
        Check(s.Events.Any(e => e.Kind == SupplyEventKind.Ending && e.Contract == c.Id), "suppliers buyer: the end of a term is told a few days before, buyer or not");
        var w2 = new SupplyWorld();
        var s2 = new SupplierState { Seed = QuietSeed(w2, 1, 101, 6, 3) };
        w2.Buyers.Add(SupplyShop);
        Contract c2 = SupplierEngine.Sign(s2, w2, 1, SupplyShop, "Shop", 2, 2, out _);
        c2.Renew = RenewRule.Same;
        c2.RenewLimit = 4;
        c2.Cover = true;
        c2.LeaveRule = true;
        RunTo(s2, w2, 108);
        Contract renewed = s2.Contracts.FirstOrDefault(x => x.Running);
        Check(c2.State == ContractState.Done && renewed != null && renewed.Supplier == 1 && renewed.Weeks == 2 && renewed.Renew == RenewRule.Same && renewed.Cover && renewed.LeaveRule && renewed.RenewLimit == 4
              && renewed.Due == 108 + 1 && CountEvents(s2, SupplyEventKind.Renewed) == 1, "suppliers buyer: with a buyer and the rule, a term that ends is renewed with the same supplier, its rules carried over");
        var w3 = new SupplyWorld();
        var s3 = new SupplierState { Seed = QuietSeed(w3, 1, 101, 6, 3) };
        w3.Buyers.Add(SupplyShop);
        Contract c3 = SupplierEngine.Sign(s3, w3, 1, SupplyShop, "Shop", 2, 2, out _);
        c3.Renew = RenewRule.Best;
        c3.RenewLimit = 4;
        RunTo(s3, w3, 108);
        Contract best = s3.Contracts.FirstOrDefault(x => x.Running);
        Check(best != null && best.Supplier == 3 && CountEvents(s3, SupplyEventKind.Switched) == 1, "suppliers buyer: the cheapest offer rule moves the place to the cheapest supplier that offers (" + (best != null ? best.Supplier : 0) + ")");
        var w4 = new SupplyWorld();
        var s4 = new SupplierState { Seed = QuietSeed(w4, 1, 101, 6, 3) };
        w4.Buyers.Add(SupplyShop);
        Contract c4 = SupplierEngine.Sign(s4, w4, 1, SupplyShop, "Shop", 2, 2, out _);
        c4.Renew = RenewRule.Same;
        c4.RenewLimit = 0;   // only at or under the market's price
        w4.Factors[0] = w4.Factors[1] = 760;   // a glut at the term's end: the fixed price offered is over the day's market
        RunTo(s4, w4, 108);
        Check(s4.RunningCount() == 0 && s4.Events.Any(e => e.Kind == SupplyEventKind.NoCover && e.A == 1), "suppliers buyer: an offer over the customer's limit is not taken, and the customer is told");
        Check(SupplierCatalogue.RenewLimits[0] == 0 && SupplierCatalogue.RenewLimits.SequenceEqual(SupplierCatalogue.RenewLimits.OrderBy(x => x)) && SupplierCatalogue.RenewLimits.Last() >= 100000, "suppliers buyer: the limits rise from the market's price to any price");
        // Covering a short delivery: one order at the day's price from another supplier, for the missing units.
        var w5 = new SupplyWorld { DayNow = 300 };
        w5.Buyers.Add(SupplyShop);
        w5.Sold["apples"] = 2400;
        w5.Sold["bread"] = 2400;
        var s5 = new SupplierState { Seed = 11 };
        int failDay = Enumerable.Range(320, 3000).First(d => !WholeOn(s5, w5, 3, d, 3) && SupplierRules.Judge(11, SupplierCatalogue.Firm(3), s5.Firm(3), d, 3, w5.Conditions(d), new int[6]).Out == Arrival.Short
                                                             && WholeOn(s5, w5, 3, d - 7, 3));
        w5.DayNow = failDay - 9;
        Contract c5 = SupplierEngine.Sign(s5, w5, 3, SupplyShop, "Shop", 8, 2, out _);
        c5.Cover = true;
        Check(c5.Due == failDay - 7, "suppliers buyer: the check's contract delivers a week before the short delivery and on its day");
        RunTo(s5, w5, failDay - 1);
        Contract cover = s5.Contracts.FirstOrDefault(x => x.Covering);
        int missing = c5.Lines.Sum(l => l.Units - SupplierRules.Brought(l.Units, l.Kind, c5.Out, c5.OutShare, c5.OutKind));
        Check(c5.Out == Arrival.Short && cover != null && cover.Supplier != 3 && cover.Weeks == 0 && cover.Lines.Sum(l => l.Units) >= missing && cover.Lines.Sum(l => l.Units) < missing + 24 + 12
              && CountEvents(s5, SupplyEventKind.Covered) == 1, "suppliers buyer: a short delivery is covered by one order from another supplier for the units missing, in whole boxes (" + missing + " missing)");
        Check(cover != null && !cover.Cover && cover.Lines.All(l => l.Mills == SupplierRules.SpotMills(w5.Market(l.Item), SupplierRules.Level(SupplierCatalogue.Firm(cover.Supplier), s5.Firm(cover.Supplier)), 0)
              || l.Mills <= w5.Market(l.Item) * 1040 / 1000), "suppliers buyer: the cover is bought at the other supplier's price of the day, and is never covered itself");
        var w6 = new SupplyWorld { DayNow = failDay - 9 };
        w6.Sold["apples"] = 2400;
        w6.Sold["bread"] = 2400;
        var s6 = new SupplierState { Seed = 11 };
        Contract c6 = SupplierEngine.Sign(s6, w6, 3, SupplyShop, "Shop", 8, 2, out _);
        c6.Cover = true;
        RunTo(s6, w6, failDay - 1);
        Check(c6.Out == Arrival.Short && !s6.Contracts.Any(x => x.Covering) && s6.Events.Any(e => e.Kind == SupplyEventKind.Tomorrow), "suppliers buyer: with nobody who orders stock nothing is covered, and the customer is told to act");
        // Leaving by rule: the right to leave is used and the place moved to another supplier.
        var w7 = new SupplyWorld();
        w7.Buyers.Add(SupplyShop);
        SupplierState s7 = CalmState();
        Contract c7 = SupplierEngine.Sign(s7, w7, 1, SupplyShop, "Shop", 8, 2, out _);
        c7.LeaveRule = true;
        c7.Failures = SupplierCatalogue.FailuresToLeave;
        c7.Due = 150;
        w7.DayNow++;
        SupplierEngine.Day(s7, w7);
        Contract moved = s7.Contracts.FirstOrDefault(x => x.Running);
        Check(c7.State == ContractState.Left && moved != null && moved.Supplier != 1 && moved.Weeks == 8 && moved.LeaveRule && s7.Owed.Count == 0 && w7.Out(SupplierEngine.SettleKey) == 0,
            "suppliers buyer: by rule the buyer leaves a supplier that failed too often, with nothing to pay, and signs the cheapest other");
        // The manager's order counts what the contracts bring.
        Check(ManagerRules.LessComing(100, 30) == 70 && ManagerRules.LessComing(20, 30) == 0 && ManagerRules.LessComing(-1, 30) == -1 && ManagerRules.LessComing(50, -9) == 50,
            "suppliers buyer: a manager's order is smaller by what a contract brings in its week, never under nothing, and an order not yet known stays so");
        Check(ManagerRules.WithSupply(40, 25) == 65 && ManagerRules.WithSupply(-1, 25) == -1 && ManagerRules.WithSupply(40, -5) == 40 && ManagerRules.WeekAhead == 6,
            "suppliers buyer: what a contract brought counts as come when a manager works out what was used");
        string decide = ModSource(@"Economy\Company\CompanyDirector.ManagerDecisions.cs");
        Check(decide.Contains("ManagerRules.LessComing(fair, coming)") && decide.Contains("ManagerRules.LessComing(amount, coming)") && decide.Contains("CompanyBridges.SupplyComingAt(reg, name, day + 1, day + (toDelivery + 23) / 24 + ManagerRules.WeekAhead)")
              && decide.Contains("ManagerRules.WithSupply(ManagerRules.Delivered(") && decide.Contains("CompanyBridges.SupplyBroughtAt(reg, name, m.SnapDay, m.SnapHour)"),
            "suppliers buyer: the company's stock decision goes through those rules with the contracts' own counts");
        var st = new SupplierState();
        var lateOne = new Contract { Id = 1, Supplier = 1, Place = SupplyShop, Weeks = 4, Due = 50, Slip = 2 };
        lateOne.Lines.Add(new ContractLine { Item = "apples", Kind = 0, Units = 100, Mills = 1000 });
        st.Contracts.Add(lateOne);
        Check(st.Coming(SupplyShop, "apples", 50, 51) == 0 && st.Coming(SupplyShop, "apples", 50, 52) == 100 && st.Coming(SupplyShop, "apples", 53, 57) == 100 && st.Coming(SupplyShop, "apples", 50, 71) == 400 && st.Coming("Other~1", "apples", 50, 71) == 0,
            "suppliers buyer: a late delivery is counted on the day it comes, and the weeks after it are not moved");
    }

    // ---- the save

    private static SupplierState BusyState(out SupplyWorld w)
    {
        w = new SupplyWorld { CashCents = 0 };
        var s = new SupplierState { Seed = 4242, RivalDay = 99, ShopsShort = 0 };
        Contract a = SupplierEngine.Sign(s, w, 1, SupplyShop, "A shop; with | marks ^ and ~ signs, and commas", 4, 2, out _);
        a.Renew = RenewRule.Best;
        a.RenewLimit = 3;
        a.Cover = true;
        a.LeaveRule = true;
        Force(a, Arrival.Late, Cause.Trouble, 2, 1000);
        Contract b = SupplierEngine.Sign(s, w, 3, "Mercer%20Street~40", "Other", 0, 2, out _);
        Force(b, Arrival.Whole, Cause.None, 0, 1000);
        w.DayNow = b.Due;
        SupplierEngine.Hour(s, w);   // delivered on account: something is owed
        Force(a, Arrival.Short, Cause.Own, 0, 600);
        a.OutKind = 1;
        s.Firm(2).ClosedDay = 90;
        s.Firm(2).ReopenDay = 118;
        s.Firm(2).Sold = true;
        s.Firm(4).TroubleKind = 2;
        s.Firm(4).TroubleUntil = 111;
        s.Firm(5).Size = 3;
        s.Firm(5).Book[2] = 1234;
        s.Firm(6).Depot = "Tenth Avenue; odd|name~9";
        s.Firm(7).SourUntil = 400;
        s.Record.Defaults = 2;
        s.RivalBooks[1].Month = -5200;
        s.RivalBooks[1].Total = 91000;
        s.RivalBooks[1].Failures = 3;
        s.RivalBooks[1].Firm = 7;
        s.RivalBooks[1].MonthOf = 20;
        s.Notes.Add(new SupplyNote { Day = 100, Kind = NoteKind.Under, Firm = 2, A = 31 });
        s.Events.Clear();
        return s;
    }

    private static void SupplierStoreChecks()
    {
        SupplierState s = BusyState(out SupplyWorld w);
        string line = SupplierStore.Write(s);
        SupplierState back = SupplierStore.Read(line, out int version);
        Check(back != null && version == SupplierStore.Version && SupplierStore.Write(back) == line, "suppliers save: every part writes and reads back to the very same line");
        Contract a = back.Contracts[0];
        Check(a.PlaceName == "A shop; with | marks ^ and ~ signs, and commas" && a.Out == Arrival.Short && a.Why == Cause.Own && a.OutShare == 600 && a.OutKind == 1 && a.JudgedFor == a.Due && a.Renew == RenewRule.Best
              && a.RenewLimit == 3 && a.Cover && a.LeaveRule && a.Lines.Count == 2 && a.Lines[0].Item == "apples" && a.Lines[0].Mills == s.Contracts[0].Lines[0].Mills,
            "suppliers save: a contract in the middle of its term is read back whole: its products and prices, the delivery judged for tomorrow, its rules, a name with marks in it");
        Check(back.Owed.Count == 1 && back.Owed[0].Cents == s.Owed[0].Cents && back.Owed[0].Due == s.Owed[0].Due && back.Record.Late == 1 && back.Record.LateDays.SequenceEqual(s.Record.LateDays) && back.Record.Defaults == 2,
            "suppliers save: what is owed and the payment record are read back");
        Check(!back.Firm(2).Open && back.Firm(2).ReopenDay == 118 && back.Firm(2).Sold && back.Firm(4).InTrouble(111) && back.Firm(5).Size == 3 && back.Firm(5).Book[2] == 1234 && back.Firm(6).Depot == "Tenth Avenue; odd|name~9"
              && back.Firm(7).SourUntil == 400 && back.RivalBooks[1].Month == -5200 && back.RivalBooks[1].Firm == 7 && back.Notes.Count == 1 && back.Log.Count == s.Log.Count && back.Seed == 4242 && back.RivalDay == 99,
            "suppliers save: each supplier's state, the rivals' sums, the paper's notes and the log are read back");
        Check(SupplierStore.Read(null, out int v0) == null && v0 == 0 && SupplierStore.Read("", out _) == null && SupplierStore.Read("nonsense", out _) == null && SupplierStore.Load(new Dictionary<string, string>(), out int v1) == null && v1 == 0,
            "suppliers save: a save from before the feature has no line and loads with nothing in it");
        var fresh = new SupplierState();
        Check(fresh.Contracts.Count == 0 && fresh.Owed.Count == 0 && fresh.Firms.All(f => f.Open && f.Size == 1 && f.Book.All(b => b == 1000)) && fresh.Firms.Select((f, i) => f.Health == SupplierCatalogue.Firms[i].HealthStart).All(x => x),
            "suppliers save: a new state has every supplier trading at its own health and nothing else");
        SupplierState future = SupplierStore.Read("v=9^h:5|10|4|9|8|0|77|78^z:later^f:500|2|0|0|0|0|0|0|0|4|1|1|1000|1000|1000|1000|1000|1000|depot|99|98;600^c:3|1|4|1000|90|104|0|1|0|0|1000|-1|0|0|0|0|0|100|0|0|1|2|1|0|0|0|place|name|apples,0,12,1250|later|field", out int fv);
        Check(future != null && fv == 9 && future.Seed == 5 && future.Firm(1).Health == 500 && future.Firm(1).Size == 2 && future.Firm(2).Health == 600 && future.Contracts.Count == 1 && future.Contracts[0].Lines[0].Units == 12
              && future.NextId == 4 && future.Firm(1).Depot == "depot", "suppliers save: a line from a newer version is read as far as it goes: fields and parts this reader does not know are skipped");
        SupplierState hostile = SupplierStore.Read("v=1^h:1|-5|-3|-2|77|-1^f:99999|9|7|-4|-1|3|-2|-9|1|-6|-1|-1|1|99999|0|x|1000|1000^c:1|1|3|99999|1|1|9|9|9|9|9|9|9|9|1|1|1|-5|-6|-7|9|9|1|1|1|1|p|n|apples,9,-3,5~bread,1,5,0~,1,5,5;2|44|4|1|1|1|0|0|0|0|1|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|p|n|a,0,1,1;0|1^o:1|1|-5|1|1;55|1|5|1|1^p:-1|-2|-3|x,5,-2^r:1|2|-3|99|4^n:0|1|1|1|1;5|99|99|1|1^l:1|-5|p|i",
            out _);
        Check(hostile != null && hostile.LastDay == 0 && hostile.NextId == 1 && hostile.Firm(1).Health == SupplierCatalogue.HealthMost && hostile.Firm(1).Size == SupplierCatalogue.SizeMost && hostile.Firm(1).TroubleKind == 2
              && hostile.Firm(1).Book[1] == 2000 && hostile.Firm(1).Book[2] == 500 && hostile.Contracts.Count == 0 && hostile.Owed.Count == 0 && hostile.Record.OnTime == 0 && hostile.Record.LateDays.SequenceEqual(new[] { 5 })
              && hostile.RivalBooks[0].Firm == SupplierCatalogue.Firms.Length && hostile.RivalBooks[0].Failures == 0 && hostile.Notes.Count == 1 && hostile.Notes[0].Kind == NoteKind.Short && hostile.Log.Count == 0,
            "suppliers save: a line with figures out of bounds is put inside them, and a contract for nothing, of no supplier, a debt of nothing or a note with no day is dropped");
        SupplierState twice = SupplierStore.Read("v=1^h:1^c:1|1|4|1000|1|8|0|0|0|0|1000|-1|0|0|0|0|0|0|0|0|0|2|0|0|0|0|p|n|apples,0,12,1250;1|3|4|1000|1|8|0|0|0|0|1000|-1|0|0|0|0|0|0|0|0|0|2|0|0|0|0|p|n|apples,0,12,1250", out _);
        Check(twice.Contracts.Count == 1 && twice.NextId == 2, "suppliers save: a contract's number is never read twice, and the next number is past every one read");
        SupplierState done = SupplierStore.Read("v=1^h:1^c:1|1|2|1000|1|8|0|2|0|0|1000|-1|0|0|0|0|0|0|0|0|0|2|0|0|0|0|p|n|apples,0,12,1250", out _);
        Check(done.Contracts[0].State == ContractState.Done, "suppliers save: a contract read as running with its term already made is over");
        // A culture with decimal commas writes and reads the same line.
        CultureInfo before = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            string german = SupplierStore.Write(s);
            SupplierState germanBack = SupplierStore.Read(line, out _);
            Check(german == line && SupplierStore.Write(germanBack) == line, "suppliers save: a language with decimal commas writes and reads the very same line");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
        // A save and a load every day changes nothing: two runs of three hundred days, one saved and loaded each day, end on the same line and the same cash.
        string RunDays(bool saving, out long cash)
        {
            var world = new SupplyWorld { CashCents = 300000000, ShortKind = 1, ShortFrom = 130, ShortUntil = 137, ShortSize = 300 };
            world.Buyers.Add(SupplyShop);
            world.Sold["apples"] = world.Sold["bread"] = 2400;
            world.Days[150] = new CityDay { Season = 0, Weather = CityWeather.Ice };
            world.Days[171] = new CityDay { Season = 1, Event = CityEvent.Parade };
            var state = new SupplierState { Seed = 2026 };
            Contract first = SupplierEngine.Sign(state, world, 2, SupplyShop, "Shop", 8, 4, out _);
            if (first != null)
            {
                first.Renew = RenewRule.Best;
                first.RenewLimit = 4;
                first.Cover = true;
                first.LeaveRule = true;
            }
            Contract second = SupplierEngine.Sign(state, world, 3, SupplyShop, "Shop", 4, 2, out _);
            if (second != null)
            {
                second.Renew = RenewRule.Same;
                second.RenewLimit = 4;
            }
            for (int day = 0; day < 300; day++)
            {
                world.DayNow++;
                world.Factors[1] = world.DayNow >= 130 && world.DayNow <= 137 ? 1300 : 1000;
                if (world.DayNow == 200)
                {
                    world.CashCents = 0;   // broke for a while, in the middle of a term
                }
                if (world.DayNow == 215)
                {
                    world.CashCents = 300000000;
                }
                SupplierEngine.Day(state, world);
                if (saving)
                {
                    state = SupplierStore.Read(SupplierStore.Write(state), out _);   // between the day's pass and the hour's deliveries
                }
                SupplierEngine.Hour(state, world);
                state.Events.Clear();
                if (saving)
                {
                    state = SupplierStore.Read(SupplierStore.Write(state), out _);
                }
            }
            cash = world.CashCents;
            return SupplierStore.Write(state);
        }
        string plain = RunDays(false, out long plainCash), saved = RunDays(true, out long savedCash);
        Check(plain == saved && plainCash == savedCash, "suppliers save: a save and a load every day, in the middle of terms, of a shortage, of a debt and of the day itself, change nothing of three hundred days");
        SupplierState after = SupplierStore.Read(plain, out _);
        Check(after.Contracts.Count > 3 && after.Record.OnTime > 20 && after.Record.Late > 0 && plainCash < 300000000, "suppliers save: those three hundred days really ran: " + after.Contracts.Count + " contracts kept, " + after.Record.OnTime + " deliveries paid, the year turned five times");
    }

    // ---- the option off and on

    private static void SupplierPauseChecks()
    {
        var s = new SupplierState();
        var late = new Contract { Id = 1, Supplier = 1, Place = SupplyShop, Weeks = 4, Due = 100, Slip = 2, Out = Arrival.Late, Why = Cause.Own, JudgedFor = 100, Made = 1 };
        late.Lines.Add(new ContractLine { Item = "apples", Units = 12, Mills = 1000 });
        var today = new Contract { Id = 2, Supplier = 1, Place = SupplyShop, Weeks = 4, Due = 130 };
        today.Lines.Add(new ContractLine { Item = "apples", Units = 12, Mills = 1000 });
        var ended = new Contract { Id = 3, Supplier = 1, Place = SupplyShop, Weeks = 4, Due = 50, State = ContractState.Done };
        s.Contracts.Add(late);
        s.Contracts.Add(today);
        s.Contracts.Add(ended);
        int moved = SupplierEngine.Resume(s, 130);
        Check(moved == 1 && late.Due == 135 && late.Slip == 0 && late.Out == Arrival.None && late.Made == 1 && today.Due == 130 && ended.Due == 50,
            "suppliers pause: coming back, a delivery whose day passed is dropped and the contract goes on at its next weekday; one due today and an ended one are left alone");
        Check(SupplierEngine.Resume(s, 130) == 0, "suppliers pause: coming back twice moves nothing twice");
        string director = ModSource(@"Suppliers\SupplierDirector.cs"), settings = ModSource(@"Core\Settings.cs"), rivals = ModSource(@"Suppliers\SupplierDirector.Rivals.cs");
        Check(director.Contains("public static bool On => Settings.LivingPopulation && Settings.LivingSuppliers;") && director.Contains("public bool Running => _loaded && !_broken && On;") && director.Contains("if (!On)")
              && director.Contains("PutAway();") && settings.Contains("public static bool LivingSuppliers = true;") && settings.Contains("LivingSuppliers = Bool(modId, LivingSuppliersId, LivingSuppliers);"),
            "suppliers pause: the option is on by default under the living economy's master switch, and with either off nothing runs");
        string putAway = director.Substring(director.IndexOf("private void PutAway()", StringComparison.Ordinal), 400);
        Check(putAway.Contains("Write();") && putAway.Contains("RemoveApp();") && putAway.Contains("_loaded = false;") && director.Contains("SupplierEngine.Resume(_state, day)"),
            "suppliers pause: switched off, the line is written and kept, the app leaves the phone, and coming back goes through the engine's resume");
        Check(director.Contains("save.modData.Remove(SupplierStore.Key)") && ModSource(@"Suppliers\SupplierGuide.cs").Contains(".ResetData(") && ModSource(@"Suppliers\SupplierGuide.cs").Contains(".Data(SupplierStore.Key, SupplierDirector.TestKey)"),
            "suppliers pause: the save tools know its lines and can forget them");
        string write = director.Substring(director.IndexOf("private void Write()", StringComparison.Ordinal), 700);
        Check(write.IndexOf("RivalsFinish();", StringComparison.Ordinal) > 0 && write.IndexOf("RivalsFinish();", StringComparison.Ordinal) < write.IndexOf("SupplierStore.Write(_state)", StringComparison.Ordinal)
              && rivals.Contains("if (GoingPublic.IsGuest || _state.RivalDay >= today)") && rivals.Contains("SupplierEngine.RivalsDone(_state, _rivalToday, _customers);"),
            "suppliers pause: the rival shops' day, if one is under way, is finished before the save is written, so no shop is gone through twice after a load");
        Check(director.Contains("if (day != _state.LastDay)") && director.Contains("SupplierEngine.Day(_state, _world);") && director.Contains("if (day != _hourDay || hour != _hour)") && director.Contains("SupplierEngine.Hour(_state, _world);"),
            "suppliers pause: the director runs the engine's day once a day and its hour once an hour, and nothing between");
    }

    // ---- the pages and their buttons

    private static void SupplierPageChecks()
    {
        Check(UiScreens.All.Any(sc => sc.Id == "suppliers" && sc.Section == UiScreenSwitches.Ventures), "suppliers pages: the app has its own switch in Mod settings");
        foreach (string state in UiSamples.SuppliersStates)
        {
            var stub = new UiSamples.SupplyStub();
            VentModel m = UiSamples.Suppliers(ModEnglish(), state, null, stub);
            bool off = state == "off";
            Check(off ? m.Shape == VentShape.Off : m.Shape == VentShape.Page && m.Nav.Length == 3, "suppliers pages: " + state + " builds as " + (off ? "the off page" : "a page of three tabs"));
            if (off)
            {
                continue;
            }
            Check(m.Left.Length >= 2 && m.HeroFigures.Length == 3, "suppliers pages: " + state + " has its figures and blocks (" + (m.Left.Length + m.Right.Length) + ")");
            int primary = 0;
            foreach (VentBlock b in m.Right.Concat(m.Left))
            {
                if (b.Kind == VentBlockKind.Switch || b.Kind == VentBlockKind.Choice)
                {
                    int before = stub.Count;
                    bool ran = b.Disabled || (b.Run >= 0 && b.Run < m.Runs.Length && m.Runs[b.Run]() && stub.Count == before + 1 && stub.Last == "rule");
                    Check(ran, "suppliers pages: " + state + ": the rule '" + b.Text + "' reaches its call");
                }
                if (b.Kind != VentBlockKind.Actions)
                {
                    continue;
                }
                foreach (UiActionModel a in b.Actions)
                {
                    primary += a.Kind == UiButtonKind.Primary ? 1 : 0;
                    if (a.Disabled)
                    {
                        continue;
                    }
                    int before = stub.Count;
                    bool ran = a.Arg >= 0 && a.Arg < m.Runs.Length && m.Runs[a.Arg] != null && m.Runs[a.Arg]();
                    Check(ran && stub.Count == before + 1, "suppliers pages: " + state + ": the button '" + a.Label + "' reaches its call (" + stub.Last + ")");
                }
            }
            Check(primary <= 1, "suppliers pages: " + state + " has one primary action at most (" + primary + ")");
        }
        var press = new UiSamples.SupplyStub();
        VentModel pick = UiSamples.Suppliers(ModEnglish(), "suppliers_pick", new VentView(), press);
        VentBlock sign = pick.Right.First(b => b.Kind == VentBlockKind.Actions);
        pick.Runs[sign.Actions[0].Arg]();
        Check(press.Last == "sign" && press.A == 2 && press.B == 8 && press.C == SupplierCatalogue.ShareDefault && press.Place == "Bleecker%20Street~12", "suppliers pages: Sign the contract signs the chosen supplier, place, term and quantity");
        VentModel once = UiSamples.Suppliers(ModEnglish(), "suppliers_once", new VentView(), press);
        once.Runs[once.Right.First(b => b.Kind == VentBlockKind.Actions).Actions[0].Arg]();
        Check(press.Last == "sign" && press.B == 0 && once.Right.First(b => b.Kind == VentBlockKind.Actions).Actions[0].Label == "Order once", "suppliers pages: with the term at one delivery the button orders once");
        VentModel refused = UiSamples.Suppliers(ModEnglish(), "suppliers_refused", new VentView(), press);
        Check(refused.Right.First(b => b.Kind == VentBlockKind.Actions).Actions[0].Disabled && refused.Right.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Warn && b.Text.Contains("smallest")),
            "suppliers pages: a supplier that refuses says why and cannot be signed with");
        VentModel wholesale = UiSamples.Suppliers(ModEnglish(), "suppliers_wholesale", new VentView(), press);
        Check(!wholesale.Right.Any(b => b.Kind == VentBlockKind.Actions) && wholesale.Right.Count(b => b.Kind == VentBlockKind.Row) == SupplierCatalogue.Kinds && wholesale.Left.First(b => b.Kind == VentBlockKind.Items).Items[0].Title == "The city's wholesale stores",
            "suppliers pages: the game's own wholesale stores are the first of the choices, as they are, with the day's prices and nothing to sign");
        Check(UiSamples.Suppliers(ModEnglish(), "suppliers", new VentView(), press).Left.First(b => b.Kind == VentBlockKind.Items).Items.Length == 4, "suppliers pages: a food shop is offered the wholesale stores and the three suppliers of its kinds, not those that trade nothing it sells");
        var view = new VentView();
        VentModel steps = UiSamples.Suppliers(ModEnglish(), "suppliers", view, press);
        VentBlock[] steppers = steps.Right.Where(b => b.Kind == VentBlockKind.Stepper).ToArray();
        steps.Runs[steppers[0].Run2]();
        steps.Runs[steppers[1].Run]();
        Check(steppers.Length == 2 && SupplyPages.TermOf(view.B) == SupplyPages.TermDefault + 1 && SupplyPages.ShareOf(view.C) == SupplierCatalogue.ShareDefault - 1, "suppliers pages: the steppers move the term and the quantity a step");
        VentModel place = UiSamples.Suppliers(ModEnglish(), "suppliers", view, press);
        place.Runs[place.Left.First(b => b.Kind == VentBlockKind.Stepper).Run2]();
        Check(view.A == 1, "suppliers pages: the place stepper moves to the next business");
        VentModel none = UiSamples.Suppliers(ModEnglish(), "suppliers_none", new VentView(), press);
        Check(none.Left.Any(b => b.Kind == VentBlockKind.Note) && !none.Right.Any(b => b.Kind == VentBlockKind.Actions), "suppliers pages: with no business that sells goods the page says what to do");
        VentModel contracts = UiSamples.Suppliers(ModEnglish(), "contracts", new VentView(), press);
        VentBlock end = contracts.Right.Last(b => b.Kind == VentBlockKind.Actions);
        contracts.Runs[end.Actions.Last().Arg]();
        Check(press.Last == "end" && press.A == 4 && end.Actions.Last().Kind == UiButtonKind.Destructive && end.Actions.Last().Label.Contains("890"), "suppliers pages: End it names the settlement and ends the chosen contract");
        Check(contracts.Right.Count(b => b.Kind == VentBlockKind.Switch) == 2 && contracts.Right.Count(b => b.Kind == VentBlockKind.Choice) == 2 && contracts.Left.First(b => b.Kind == VentBlockKind.Items).Items.Length == 4,
            "suppliers pages: a contract shows its four rules, and the list every contract, running and ended");
        VentModel leave = UiSamples.Suppliers(ModEnglish(), "contracts_leave", new VentView(), press);
        VentBlock leaveBlock = leave.Right.Last(b => b.Kind == VentBlockKind.Actions);
        leave.Runs[leaveBlock.Actions.Last().Arg]();
        Check(press.Last == "leave" && leaveBlock.Actions.All(a => a.Kind != UiButtonKind.Destructive), "suppliers pages: with the right to leave, the page offers to leave with nothing to pay and no settlement");
        VentModel owed = UiSamples.Suppliers(ModEnglish(), "contracts_owed", new VentView(), press);
        VentBlock owedBlock = owed.Right.Last(b => b.Kind == VentBlockKind.Actions);
        owed.Runs[owedBlock.Actions[0].Arg]();
        Check(press.Last == "pay" && owed.Left.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Warn) && owed.Right.Any(b => b.Kind == VentBlockKind.Row && b.Tone == UiRole.Warn), "suppliers pages: what is owed is said on the page, on the contract, and can be paid from it");
        VentModel late = UiSamples.Suppliers(ModEnglish(), "contracts_late", new VentView(), press);
        Check(late.Right.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Warn && b.Text.Contains("2 days late") && b.Text.Contains("the weather")), "suppliers pages: a delivery judged late says by how much and why");
        VentModel ended = UiSamples.Suppliers(ModEnglish(), "contracts_ended", new VentView(), press);
        Check(!ended.Right.Any(b => b.Kind == VentBlockKind.Actions || b.Kind == VentBlockKind.Switch), "suppliers pages: an ended contract has nothing to press");
        VentModel empty = UiSamples.Suppliers(ModEnglish(), "contracts_empty", new VentView(), press);
        Check(empty.Left.Any(b => b.Kind == VentBlockKind.Note) && empty.Right.Length == 0, "suppliers pages: with no contracts the page says where to start");
        VentModel shortage = UiSamples.Suppliers(ModEnglish(), "market_shortage", new VentView(), press);
        VentItem[] kinds = shortage.Left.First(b => b.Kind == VentBlockKind.Items).Items;
        Check(kinds.Length == SupplierCatalogue.Kinds && kinds[1].Chip == "Shortage" && kinds[3].Chip == "Shortage tomorrow" && kinds[2].Chip == "Glut" && kinds[1].Amount == "128%" && shortage.Right.Count(b => b.Kind == VentBlockKind.Note) >= 3,
            "suppliers pages: the market shows each kind's price, a shortage running or announced, a glut, the suppliers' news and the rivals' line");
        Check(SupplyPages.Against(UiSamples.VentWordsOf(ModEnglish()), 955) == "4.5% under today's market price" && SupplyPages.Against(UiSamples.VentWordsOf(ModEnglish()), 1030) == "3% over today's market price"
              && SupplyPages.Against(UiSamples.VentWordsOf(ModEnglish()), 1000) == "At today's market price", "suppliers pages: a price is said against the market's");
        Check(SupplyPages.Late(UiSamples.VentWordsOf(ModEnglish()), LatePolicy.Lenient).Contains("7 days") && SupplyPages.Late(UiSamples.VentWordsOf(ModEnglish()), LatePolicy.Firm).Contains("4 days") && SupplyPages.Late(UiSamples.VentWordsOf(ModEnglish()), LatePolicy.Firm).Contains("2%")
              && SupplyPages.Late(UiSamples.VentWordsOf(ModEnglish()), LatePolicy.Strict).Contains("2 such"), "suppliers pages: how a supplier treats a late payer is said with the catalogue's own days and fee");
        string app = ModSource(@"Suppliers\SupplierApp.cs"), live = ModSource(@"Ui\Ventures\SupplyLive.cs");
        Check(app.Contains("SupplyLive.Instance.Sign(") && app.Contains("SupplyLive.Instance.End(") && app.Contains("SupplyLive.Instance.Leave(") && app.Contains("SupplyLive.Instance.PayOwed()") && app.Contains("SupplyLive.Instance.Rule(")
              && live.Contains("d.Sign(supplier, place, weeks, shareStep, out Refusal why)") && live.Contains("now.End(contract);") && live.Contains("d.Leave(contract);") && live.Contains("d.PayOwed()") && live.Contains("d.Rule(contract, rule);")
              && live.Contains("GameConfirm.Show("), "suppliers pages: the app in the game's frame and the kit's page press the same calls of the director, and ending a contract asks first");
        string bizman = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs"), pressed = ModSource(@"Ui\BizMan\BizManScreen.Press.cs"), overlay = ModSource(@"Ui\Overlays\OverlayFactsLive.cs");
        Check(bizman.Contains("Arg = BizManPages.FactSuppliers") && bizman.Contains("_m.ItemsEmptyAction = supply != null") && pressed.Contains("case BizManPages.Suppliers:") && pressed.Contains("if (arg == BizManPages.FactSuppliers)")
              && pressed.Contains("Suppliers.SupplierDirector.OpenApp(Suppliers.SupplierGame.Key(reg.Address))") && ModSource(@"Ui\BizManPages.Parts.cs").Contains("m.ItemsEmptyAction, Suppliers, keys"),
            "suppliers pages: BizMan's Deliveries page, where stock is bought today, shows the business's supply contracts and opens the app on it");
        Check(overlay.Contains("supply.NextDelivery(manager.buildingRegistration, item, out int supplied)"), "suppliers pages: a shelf's next delivery counts a supply contract's");
    }

    // ---- the sources

    private static void SupplierSourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Suppliers\SupplierDirector.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 16, "suppliers sources: the feature's files are found (" + files.Length + ")");
        string all = string.Empty;
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            all += text;
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("void Update()") && !text.Contains("LateUpdate()") && !text.Contains("FindObjectsByType") && !text.Contains("FindObjectOfType"),
                "suppliers sources: " + name + " books no money outside the one call and has no per-frame code or scene search");
            Check(!text.Contains("TODO") && !text.Contains("NotImplemented") && !text.Contains("ProjectSettings"), "suppliers sources: " + name + " has no stub and no note of work left");
            Check(name == "SupplierGame.cs" || !text.Contains("BizBooks.Book("), "suppliers sources: " + name + " moves no money itself");
            Check(File.ReadAllLines(file).All(l => l.Length < 200), "suppliers sources: " + name + " keeps its lines under 200 columns");
        }
        string game = ModSource(@"Suppliers\SupplierGame.cs"), director = ModSource(@"Suppliers\SupplierDirector.cs"), engine = ModSource(@"Suppliers\SupplierEngine.cs"), rivals = ModSource(@"Suppliers\SupplierDirector.Rivals.cs");
        Check(CountOfText(game, "BizBooks.Book(") == 2 && game.Contains("force: false") && game.Contains("if (cents > CashNow || cents > int.MaxValue / 2)"), "suppliers sources: money leaves through the mod's one booking call, never forced, never over the cash in hand");
        Check(CountOfText(all, "UnityEngine.Random") == 1 && director.Contains("_state.Seed = UnityEngine.Random.Range(1, int.MaxValue);") && !engine.Contains("Random") && !ModSource(@"Suppliers\SupplierRules.cs").Contains("System.Random"),
            "suppliers sources: the only unseeded draw is the save's own seed, made once and kept; every other draw is a hash of it");
        Check(!all.Contains("SharedCity") && !all.Contains("SendOver") && !all.Contains("MPClient") && !all.Contains("MPServer"), "suppliers session: nothing new goes over the network");
        Check(game.Contains("GoingPublic.Mine(reg)") && rivals.Contains("GoingPublic.IsGuest") && rivals.Contains("if (read == null || GoingPublic.InSession)") && game.Contains("cents >= 100 && !GoingPublic.InSession"),
            "suppliers session: only the player's own places are read or delivered to, a guest leaves the city's shops to the host, and the tycoons rest in a session");
        Check(director.Contains("StartCoroutine(Guard.Routine(Tick()))") && director.Contains("catch (Exception e)") && director.Contains("_broken = true;"), "suppliers sources: the timer is a guarded coroutine, once a second, and a fault switches the feature off for the session");
        Check(director.Contains("SupplierEngine.Sign(_state, _world") && director.Contains("SupplierEngine.Break(_state, _world, contract)") && director.Contains("SupplierEngine.Leave(_state, _world, contract)")
              && director.Contains("SupplierEngine.Collect(_state, _world)") && rivals.Contains("SupplierEngine.RivalShop(_state, _world") && rivals.Contains("SupplierEngine.Outlets(_state, _world, _outlets)")
              && ModSource(@"Suppliers\SupplierDirector.Facts.cs").Contains("SupplierEngine.Offer(_state, _world"), "suppliers sources: the director decides nothing itself: every press and every pass goes through the engine these checks drive");
        Check(engine.Contains("SupplierRules.Judge(s.Seed, def, f, c.Due, kinds, w.Conditions(c.Due), Shortages)") && engine.Contains("SupplierRules.ChargeMills(line.Mills, w.Export(line.Item), w.Market(line.Item))")
              && engine.Contains("SupplierRules.FirmDay(s.Seed, def, f, day, Factors, w.Cycle)") && engine.Contains("SupplierRules.Rival(s.Seed"), "suppliers sources: the engine judges, charges and runs the suppliers and the rivals by the rules' own functions");
        Check(game.Contains("ItemHelper.DeliverCargoToBuilding(") && game.Contains("ProductMarketHelper.GetProductExportPrice(item)") && game.Contains("it.GetWholesalePrice()") && game.Contains("ProductMarketHelper.IsProductInMarketEvent(item, MarketEventType.ProductBackorder)")
              && game.Contains("it.maxWholesaleOrderAmount") && game.Contains("CityEconomy.SupplyFactor(kind)") && game.Contains("CityEconomy.SupplyShockOf("), "suppliers sources: goods, prices, limits and shortages are the game's and the city's own, read where the game reads them");
        Check(CountOfText(ModSource(@"Wiring\Wiring.cs"), "SupplierLinks") == 2, "suppliers sources: the wiring connects and disconnects the feature");
        Check(ModSource(@"Meta\FeatureList.cs").Contains("Suppliers.SupplierGuide.Register()") && ModSource(@"Core\FeatureOwners.cs").Contains("\"RealLifeOverhaul.Suppliers\", \"suppliers\"") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"suppliers\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"suppliers\"") && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("Suppliers.SupplierOptions.AddSuppliers(o)")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("Suppliers.SupplierDirector.Attach(_root, context.Logger)"), "suppliers sources: the feature has its option, its Guide page, its tests, its owner and its place in the cities list");
        string links = ModSource(@"Wiring\Wiring.SupplierLinks.cs");
        Check(links.Contains("RivalBridges.SupplyLine =") && links.Contains("News.Extra = Paper;") && links.Contains("News.Extra = _paperBefore;") && ModSource(@"Rivals\RivalPage.cs").Contains("RivalBridges.SupplyLine(who)")
              && ModSource(@"Rivals\RivalPage.cs").Contains("RivalBridges.SupplyLine(i)") && links.Contains("Math.Max(0L, t.Worth - floor)"), "suppliers sources: the rivals' supply shows on the rich list, their panel and in the paper, and a cost never takes a tycoon under their floor");
        Check(links.Contains("return material > 0.0 && !double.IsInfinity(material) ? -1 : CityGame.KindOf(item);"), "suppliers sources: a raw material the exchange prices is never a supplier's to bring");
        Check(CashCategory.For(SupplierEngine.GoodsKey) == CashCategory.Stock && CashCategory.For(SupplierEngine.OwedKey) == CashCategory.Stock && CashCategory.For(SupplierEngine.SettleKey) == CashCategory.Other
              && CashCategory.For(SupplierEngine.CreditKey) == CashCategory.Other && !DayRules.IsTransfer(SupplierEngine.GoodsKey) && !DayRules.IsTransfer(SupplierEngine.SettleKey),
            "suppliers sources: the Finance app's cash flow reads a supplier's delivery and a late payment for one as stock, and a settlement or a credit as neither stock nor a transfer");
        Check(BizLines.Of(SupplierEngine.GoodsKey) == BizLine.Foreign && BizLines.Of(SupplierEngine.SettleKey) == BizLine.Foreign && BizLines.Of(SupplierEngine.OwedKey) == BizLine.Foreign && BizLines.Of(SupplierEngine.CreditKey) == BizLine.Foreign,
            "suppliers sources: a purchase of stock is no running cost of the statement's: the game counts the goods when they sell, at the price they came in at");
    }

    // ---- the texts

    private static void SupplierTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Suppliers\SupplierDirector.cs"));
        var sources = Directory.GetFiles(folder, "*.cs").Select(File.ReadAllText).ToList();
        sources.Add(ModSource(@"Ui\Ventures\SupplyPages.cs"));
        sources.Add(ModSource(@"Ui\Ventures\SupplyLive.cs"));
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(source, "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Remove("reallife_supply_firm_about_");
        for (int i = 1; i <= 9; i++)
        {
            keys.Add("reallife_supply_firm_about_" + i);
        }
        foreach (string k in new[] { "reallife_opt_sec_suppliers", "reallife_opt_tip_living_suppliers", "reallife_ui_screen_suppliers", "reallife_opt_tip_ui_screen_suppliers", "reallife_supply_bizman_label",
                     "reallife_supply_bizman_value", "reallife_supply_bizman_none", "reallife_supply_bizman_open", "reallife_transaction_supply_deliverycontract_label", "reallife_transaction_supply_settlement_label",
                     "reallife_transaction_supply_deliverycontract_owed_label", "reallife_transaction_supply_credit_label" })
        {
            keys.Add(k);
        }
        Check(keys.Count >= 240, "suppliers texts: its keys are found: " + keys.Count);
        string[] facts = { "{supply_firms}", "{supply_terms}", "{supply_discount_least}", "{supply_discount_most}", "{supply_break}", "{supply_failures}", "{supply_short_credit}", "{supply_late_credit}",
            "{supply_grace_most}", "{supply_grace_least}", "{supply_sour}", "{supply_reopen}", "{supply_contracts_most}", "{supply_renew_days}" };
        string guide = ModSource(@"Suppliers\SupplierGuide.cs");
        Check(facts.All(f => guide.Contains(".Fact(\"" + f.Trim('{', '}') + "\"")), "suppliers texts: every fact of the Guide page is filled from the catalogue");
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        const string line = "Suppliers and contracts, a new option under Living population: the Suppliers app lists nine suppliers that are firms of the city";
        string row = english != null ? english.Where(p => p.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !p.Key.EndsWith("_title", StringComparison.Ordinal) && p.Value.Contains(line)).Select(p => p.Key).FirstOrDefault() : null;
        Check(row != null, "suppliers texts: What's new has the feature's line, in whichever row holds it");
        foreach (string code in SupplierLanguages)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "suppliers texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "suppliers texts: " + key + " is in " + code + ".json");
                if (!there)
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains((char)0xFF01) && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !Regex.IsMatch(value.Replace("\n- ", "\n"), @"\S - \S"),
                    "suppliers texts: " + key + " has no exclamation mark or dash in " + code);
                if (english != null && english.TryGetValue(key, out string en))
                {
                    var wanted = new SortedSet<string>(Regex.Matches(en, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    var got = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_0-9]+\}").Cast<Match>().Select(m => m.Value));
                    Check(wanted.SetEquals(got), "suppliers texts: " + key + " keeps its {names} in " + code);
                    Check(Regex.Matches(en, "<b>").Count == Regex.Matches(value, "<b>").Count && Regex.Matches(en, @"\n").Count == Regex.Matches(value, @"\n").Count, "suppliers texts: " + key + " keeps its tags and lines in " + code);
                }
                if (key.StartsWith("reallife_supply_note_", StringComparison.Ordinal) || key.StartsWith("reallife_supply_test_", StringComparison.Ordinal) || key.StartsWith("reallife_supply_refuse_", StringComparison.Ordinal))
                {
                    Check(value.Length <= 240, "suppliers texts: the notice " + key + " is at most 240 characters in " + code + " (" + value.Length + ")");
                }
            }
            if (words.TryGetValue("reallife_guide_suppliers", out string page))
            {
                foreach (string fact in facts)
                {
                    Check(page.Contains(fact), "suppliers texts: the Guide page has " + fact + " in " + code);
                }
            }
            if (row != null)
            {
                Check(words.TryGetValue(row, out string changes) && changes.Length > 0 && words.TryGetValue("reallife_supply_app", out string appName) && changes.Contains(appName),
                    "suppliers texts: What's new names the Suppliers app in " + code + ", in the row the English line is in");
            }
        }
        if (english != null && english.ContainsKey("reallife_supply_late_firm") && english.ContainsKey("reallife_supply_kind_5") && english.ContainsKey("reallife_supply_firm_9"))
        {
            Check(english["reallife_supply_late_firm"].Contains("{days}") && english["reallife_supply_late_firm"].Contains("{fee}") && english["reallife_supply_late_strict"].Contains("{most}")
                  && english["reallife_supply_take_or_pay"].Contains("{share}"), "suppliers texts: the days of grace, the fee and the settlement's share are filled from the catalogue, not written into the words");
            Check(SupplierCatalogue.Firms.All(f => english.ContainsKey(f.NameKey) && english[f.NameKey] == SupplyPages.FirmEnglish(f.Id)) && Enumerable.Range(0, 6).All(k => english[SupplierCatalogue.KindKeys[k]] == SupplyPages.KindEnglish(k)),
                "suppliers texts: the names the samples and the checks read are the names the game shows");
        }
    }
}
