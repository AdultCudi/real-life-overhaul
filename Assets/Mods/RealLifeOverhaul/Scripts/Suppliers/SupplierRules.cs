using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>Why a supplier offers nothing. Each has a line of its own on the page.</summary>
    public enum Refusal
    {
        None = 0,
        Off = 1,        // the feature is not running
        Shut = 2,       // the supplier went under
        Kind = 3,       // it does not trade what the place sells
        NoGoods = 4,    // the place has nothing it could bring
        Small = 5,      // the delivery is under its minimum order
        Owes = 6,       // the customer owes a supplier money
        Sour = 7,       // the customer defaulted on this supplier
        Credit = 8,     // the customer's credit rating is under what it asks
        Full = 9,       // too many contracts are running
        Held = 10       // the place has a fixed price contract with it already
    }

    /// <summary>How a delivery was judged to come.</summary>
    public struct Judged
    {
        public Arrival Out;
        public Cause Why;
        public int Days;        // days late
        public int Share;       // thousandths a short delivery brings
        public int Kind;        // the kind of goods it is short of; -1: all of it
    }

    /// <summary>What a rival shop's day came to on the supply side.</summary>
    public struct RivalDay
    {
        public bool Skipped;    // its last valued day was charged already (the entry has not changed since): nothing comes off again
        public int Cents;       // what comes off its day (a saving is negative)
        public int Firm;        // who it buys from; 0 the wholesale stores
        public bool Contract;   // on a fixed price
        public bool Failed;     // its delivery was late or short today
    }

    /// <summary>
    /// The rules of suppliers and contracts, every one a pure function of what it is handed: the seeded draws, a price fixed for a term, how a supplier judges a customer,
    /// how a delivery is judged to come, the remedy, take or pay and the settlement, a supplier's own day and a rival shop's. Game-free: the engine and the director call
    /// these and nothing else decides, and EconomyCheck drives them (SupplierChecks.cs).
    /// </summary>
    public static class SupplierRules
    {
        public const int PurposeJudge = 1, PurposePick = 2, PurposeSize = 3, PurposeTrouble = 4, PurposeTroubleKind = 5, PurposeTroubleDays = 6, PurposeFirm = 7,
            PurposeRivalContract = 8, PurposeRivalDay = 9, PurposeDepot = 10;

        /// <summary>A fixed hash of the save's seed and three numbers: the same on every machine, and every draw of the feature.</summary>
        public static uint Hash(int seed, int a, int b, int purpose)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)purpose) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A draw from 0 to range - 1.</summary>
        public static int Roll(int seed, int a, int b, int purpose, int range)
        {
            return range <= 1 ? 0 : (int)(Hash(seed, a, b, purpose) % (uint)range);
        }

        /// <summary>A rival shop's number from its street and number, the same on every machine and made without building a string.</summary>
        public static int ShopId(string streetName, int streetNumber)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; streetName != null && i < streetName.Length; i++)
                {
                    h = (h ^ streetName[i]) * 16777619u;
                }
                h = (h ^ (uint)streetNumber) * 16777619u;
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>A number for a text (a shop's key), the same on every machine.</summary>
        public static int Text(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>What so many units cost at a price in mills, in cents, rounded up: a supplier never gives the fraction away.</summary>
        public static long Cents(long units, long mills)
        {
            return units <= 0 || mills <= 0 ? 0L : (units * mills + 9) / 10;
        }

        /// <summary>Whole money written at the start's prices, at a price level (tenths of a point), in cents.</summary>
        public static long AtLevel(int money, int level)
        {
            return (long)money * Math.Max(1, level) / 10;
        }

        /// <summary>
        /// A supplier's list price of a product, mills a unit: the item's own wholesale price (the one the city's economy writes by its kind's factor), the game's market-price
        /// multiplier and, for the price of the day, the whole market's event on it (a shortage, a backorder). It never holds the importers' daily price index, which the game
        /// re-rolls for every product every day, on any change of business and at every export, with a roll nobody seeds: a supplier's price (a fixed one above all) is not
        /// the importers' roll of the day, so loading a save again for a low day finds nothing and no price is locked at a momentary low.
        /// </summary>
        public static long ListMills(float wholesale, float multiplier, float market = 1f)
        {
            double price = (double)wholesale * multiplier * market;
            return price > 0.0 && price < 1e9 ? (long)Math.Round(price * 1000.0) : 0L;
        }

        /// <summary>
        /// A day's conditions as a delivery is judged by them, the day before and for the rival shops: what the mod honestly knows of any day from the save's own seed. The
        /// city's economy reads the live sky for today, and a storm that is raging is no forecast (nor the same on every load), so it is dropped; a storm is the seasons'
        /// seeded summer storm day, known ahead like a heat wave or ice.
        /// </summary>
        public static CityDay Forecast(CityDay city, bool weatherMatters, bool seasons, bool seededStormDay)
        {
            if (city.Weather == CityWeather.Storm)
            {
                city.Weather = CityWeather.Fair;
            }
            if (weatherMatters && seasons && seededStormDay && city.Weather == CityWeather.Fair)
            {
                city.Weather = CityWeather.Storm;
            }
            return city;
        }

        // ---- when the director runs what (game-free, so the checks drive the very rules the director asks)

        /// <summary>The feature runs under the living economy's master switch with its own option on.</summary>
        public static bool On(bool livingPopulation, bool suppliersOption) => livingPopulation && suppliersOption;

        /// <summary>True while a save is read, the option is on and nothing has failed.</summary>
        public static bool Running(bool loaded, bool broken, bool livingPopulation, bool suppliersOption) => loaded && !broken && On(livingPopulation, suppliersOption);

        /// <summary>The day's pass is due on a day other than the last one counted.</summary>
        public static bool DayDue(int lastDay, int today) => today != lastDay;

        /// <summary>The hour's deliveries are due on an hour other than the last one looked at.</summary>
        public static bool HourDue(int seenDay, int seenHour, int day, int hour) => day != seenDay || hour != seenHour;

        /// <summary>
        /// The first of the days that went by since the last one counted that is played one by one (a time skip, a long sleep): none (today) for a day that follows; at
        /// most <see cref="SupplierCatalogue.CatchUpDays"/> of them, the days before being a pause (<paramref name="paused"/>).
        /// </summary>
        public static int PlayFrom(int lastDay, int today, out bool paused)
        {
            paused = false;
            int from = lastDay + 1;
            if (from >= today)
            {
                return today;
            }
            if (today - from > SupplierCatalogue.CatchUpDays)
            {
                paused = true;
                return today - SupplierCatalogue.CatchUpDays;
            }
            return from;
        }

        /// <summary>
        /// Whether the city's rival shops' day may be gone through now: never on a Going Public guest (the city's shops are the host's books), once a day, from the hour the
        /// game has valued the shops, and a day under way is finished.
        /// </summary>
        public static bool RivalsMay(bool guest, int rivalDay, int today, bool underWay, int hour, bool valuing)
        {
            if (guest || rivalDay >= today)
            {
                return false;
            }
            return underWay || (hour >= SupplierCatalogue.RivalFromHour && !valuing);
        }

        /// <summary>The rival tycoons' outlets are gone through only while no Going Public session keeps the rivals at rest.</summary>
        public static bool TycoonsMove(bool inSession) => !inSession;

        /// <summary>A supplier's margin reaches the tycoon whose holding owns it: a whole dollar at least, and no session keeps the rivals at rest.</summary>
        public static bool OwnerGainMay(long cents, bool inSession) => cents >= 100 && !inSession;

        /// <summary>A payment to a supplier: never over the cash that may be paid with (over the investors' floor), and never a sum the booking cannot carry.</summary>
        public static bool PayMay(long cents, long spendable) => cents <= spendable && cents <= int.MaxValue / 2;

        // ---- a supplier as it stands

        /// <summary>The price a supplier sells at on the day, thousandths of the market's: a supplier that has grown sells a little cheaper.</summary>
        public static int Level(SupplierDef def, FirmState f)
        {
            return def.Level - (Math.Max(1, f.Size) - 1) * SupplierCatalogue.SizeOff;
        }

        /// <summary>The share of its deliveries that come whole and on the day in calm conditions, thousandths.</summary>
        public static int Reliability(SupplierDef def, FirmState f)
        {
            return Math.Min(995, def.Reliability + (Math.Max(1, f.Size) - 1) * SupplierCatalogue.SizeReliability);
        }

        /// <summary>Where its health rests: its own, a little more for each shop it serves and each step it has grown.</summary>
        public static int Rest(SupplierDef def, FirmState f)
        {
            return Math.Min(950, def.HealthStart + Math.Min(150, Math.Max(0, f.Customers) * 3) + (Math.Max(1, f.Size) - 1) * 25);
        }

        // ---- prices

        /// <summary>The calm price of a product: the market's of the day without the day's push on its kind (the season, the weather, a city event, a shortage or a glut).</summary>
        public static long Calm(long marketMills, int factor)
        {
            return marketMills <= 0 ? 0L : marketMills * 1000 / Math.Max(1, factor);
        }

        /// <summary>
        /// What a supplier writes a fixed price at against the calm price, thousandths: a shortage that is running or announced counts for the days of the term it covers, and a
        /// glut or a cheap season never counts, so a fixed price cannot be locked at a momentary low.
        /// </summary>
        public static int TermFactor(int termDays, int shortFrom, int shortDays, int shortSize)
        {
            if (termDays <= 0 || shortDays <= 0 || shortSize <= 0)
            {
                return 1000;
            }
            int from = Math.Max(0, shortFrom), until = Math.Min(termDays, from + shortDays);
            int covered = Math.Max(0, until - from);
            return 1000 + (int)((long)shortSize * covered / termDays);
        }

        /// <summary>
        /// What a customer's standing moves a price by, thousandths off (negative: dearer): the term's promise, the size of the order against the supplier's minimum, the
        /// credit rating and the payment record.
        /// </summary>
        public static int Discount(int weeks, long valueCents, long minCents, int credit, int lateCount)
        {
            int step = SupplierCatalogue.TermStep(weeks);
            int off = step >= 0 ? SupplierCatalogue.TermDiscount[step] : 0;
            if (minCents > 0 && valueCents >= minCents * SupplierCatalogue.VolumeFrom2)
            {
                off += SupplierCatalogue.VolumeOff2;
            }
            else if (minCents > 0 && valueCents >= minCents * SupplierCatalogue.VolumeFrom1)
            {
                off += SupplierCatalogue.VolumeOff1;
            }
            int rating = credit <= 0 ? SupplierCatalogue.CreditNone : credit;
            off += rating >= SupplierCatalogue.CreditGood ? SupplierCatalogue.CreditOff : rating < SupplierCatalogue.CreditPoor ? -SupplierCatalogue.CreditCost : 0;
            off -= Math.Min(SupplierCatalogue.LateCostMost, Math.Max(0, lateCount) * SupplierCatalogue.LateCost);
            return off;
        }

        /// <summary>Whether a supplier offers this customer anything, and if not, why: it looks at what is owed, the record with it, the size of the order and the credit rating.</summary>
        public static Refusal Refuse(SupplierDef def, FirmState f, int day, long owedCents, long valueCents, long minCents, int credit)
        {
            if (def == null || f == null)
            {
                return Refusal.Off;
            }
            if (!f.Open)
            {
                return Refusal.Shut;
            }
            if (owedCents > 0)
            {
                return Refusal.Owes;
            }
            if (day <= f.SourUntil)
            {
                return Refusal.Sour;
            }
            if (valueCents <= 0)
            {
                return Refusal.NoGoods;
            }
            if (valueCents < minCents)
            {
                return Refusal.Small;
            }
            int rating = credit <= 0 ? SupplierCatalogue.CreditNone : credit;
            if ((def.Late == LatePolicy.Strict && rating < SupplierCatalogue.CreditPoor) || (def.Late == LatePolicy.Firm && rating < SupplierCatalogue.CreditVeryPoor))
            {
                return Refusal.Credit;
            }
            return Refusal.None;
        }

        /// <summary>A unit price fixed for a term, mills, rounded up: the calm price, the term's factor, the supplier's level and what the customer's standing takes off.</summary>
        public static long FixedMills(long baseMills, int factor, int termFactor, int level, int discount)
        {
            long calm = Calm(baseMills, factor);
            long price = calm * Math.Max(1000, termFactor) * Math.Max(1, level) / 1000 * Math.Max(1, 1000 - discount);   // one division at the end but for the level's, so nothing is lost on the way
            return calm <= 0 ? 0L : Math.Max(1L, (price + 999999) / 1000000);
        }

        /// <summary>A unit price for one delivery at the day's price, mills, rounded up.</summary>
        public static long SpotMills(long marketMills, int level, int discount)
        {
            long price = marketMills * Math.Max(1, level) * Math.Max(1, 1000 - discount);
            return marketMills <= 0 ? 0L : Math.Max(1L, (price + 999999) / 1000000);
        }

        /// <summary>
        /// What a unit is charged at on the day it comes, mills: the agreed price, and never under what the product fetches on export that day (held to the market's price
        /// where export pays more than the market itself), so nothing is made by passing contract goods on.
        /// </summary>
        public static long ChargeMills(long agreedMills, long exportMills, long marketMills)
        {
            long floor = marketMills > 0 ? Math.Min(Math.Max(0L, exportMills), marketMills) : Math.Max(0L, exportMills);
            return Math.Max(agreedMills, floor);
        }

        // ---- take or pay

        /// <summary>What ending a contract early costs, cents: a share of the value of the deliveries still to come.</summary>
        public static long Settlement(long deliveryCents, int deliveriesLeft)
        {
            long left = Math.Max(0L, deliveryCents) * Math.Max(0, deliveriesLeft);
            return (left * SupplierCatalogue.BreakShare + 999) / 1000;
        }

        /// <summary>What is owed with a supplier's late fee on it, cents.</summary>
        public static long WithFee(long cents, LatePolicy late)
        {
            return cents + cents * SupplierCatalogue.LateFee[SupplierCatalogue.Clamp((int)late, 0, 2)] / 1000;
        }

        // ---- the remedy

        /// <summary>True for a cause that is the supplier's own: only those earn a credit and count towards the right to leave.</summary>
        public static bool Theirs(Cause why) => why == Cause.Own || why == Cause.Trouble || why == Cause.Cash;

        /// <summary>The credit for a delivery the supplier itself made short, cents: a share of the value it did not bring.</summary>
        public static long ShortCredit(long missingCents)
        {
            return Math.Max(0L, missingCents) * SupplierCatalogue.ShortCredit / 1000;
        }

        /// <summary>
        /// The credit for a delivery the supplier itself made late, cents: the short credit on the share of the week's goods the late days missed, so a delivery that comes
        /// whole is never credited more than the same days short would be (a credit never exceeds the honest loss).
        /// </summary>
        public static long LateCredit(long valueCents, int days)
        {
            long missing = Math.Max(0L, valueCents) * Math.Min(SupplierCatalogue.WeekDays, Math.Max(0, days)) / SupplierCatalogue.WeekDays;
            return ShortCredit(missing);
        }

        // ---- how a delivery comes

        /// <summary>
        /// How a supplier's delivery of a day comes: drawn from the save's seed, the supplier and the day alone, against a chance made of real causes (its own unreliability,
        /// a strike or a fire, its money trouble, the day's weather, a city event in the streets, fresh goods in winter, a market shortage of a kind it brings). The same
        /// for every customer of the supplier that day, and the same however often and whenever it is asked: ordering later or loading again changes nothing.
        /// </summary>
        /// <param name="kinds">the kinds of goods the delivery holds, a bit each</param>
        /// <param name="shortage">by kind, the size of a market shortage running that day in thousandths; 0 for none</param>
        public static Judged Judge(int seed, SupplierDef def, FirmState f, int dueDay, int kinds, CityDay day, int[] shortage)
        {
            var j = new Judged { Out = Arrival.Whole, Why = Cause.None, Share = 1000, Kind = -1 };
            if (def == null || f == null)
            {
                return j;
            }
            bool fresh = (kinds & 1) != 0;
            int own = 1000 - Reliability(def, f);
            int trouble = f.InTrouble(dueDay) ? SupplierCatalogue.TroubleChance : 0;
            int cash = f.Health < SupplierCatalogue.HealthWeak ? (SupplierCatalogue.HealthWeak - f.Health) / SupplierCatalogue.HealthChanceDiv : 0;
            int weather = day.Weather == CityWeather.Ice ? SupplierCatalogue.IceChance : day.Weather == CityWeather.Cold ? SupplierCatalogue.ColdChance
                : day.Weather == CityWeather.Storm ? SupplierCatalogue.StormChance : day.Weather == CityWeather.Heat && fresh ? SupplierCatalogue.HeatFreshChance : 0;
            int streets = day.Event == CityEvent.Parade || day.Event == CityEvent.Protest || day.Event == CityEvent.FunRun || day.Event == CityEvent.FilmShoot
                || day.Event == CityEvent.Fireworks ? SupplierCatalogue.EventChance : 0;
            int season = day.Season == 0 && fresh ? SupplierCatalogue.WinterFreshChance : 0;
            int shortKind = -1, shortSize = 0;
            for (int k = 0; shortage != null && k < SupplierCatalogue.Kinds && k < shortage.Length; k++)
            {
                if ((kinds & (1 << k)) != 0 && shortage[k] > shortSize)
                {
                    shortSize = shortage[k];
                    shortKind = k;
                }
            }
            int market = shortSize / SupplierCatalogue.ShortageDiv;
            int sum = own + trouble + cash + weather + streets + season + market;
            int chance = Math.Min(SupplierCatalogue.ChanceMost, sum);
            if (sum <= 0 || Roll(seed, def.Id, dueDay, PurposeJudge, 1000) >= chance)
            {
                return j;
            }
            int pick = Roll(seed, def.Id, dueDay, PurposePick, sum), size = Roll(seed, def.Id, dueDay, PurposeSize, 1000);
            if (pick < own)
            {
                j.Why = Cause.Own;
                Fill(ref j, size < 600, 1 + size % 2, 600 + size % 301);
            }
            else if (pick < own + trouble)
            {
                j.Why = Cause.Trouble;
                Fill(ref j, size < 600, 2 + size % 2, 400 + size % 301);
            }
            else if (pick < own + trouble + cash)
            {
                j.Why = Cause.Cash;
                Fill(ref j, false, 0, 400 + size % 401);
            }
            else if (pick < own + trouble + cash + weather)
            {
                j.Why = Cause.Weather;
                bool heat = day.Weather == CityWeather.Heat;
                Fill(ref j, !heat, day.Weather == CityWeather.Ice ? 2 : 1, 700);
                j.Kind = heat ? 0 : -1;
            }
            else if (pick < own + trouble + cash + weather + streets)
            {
                j.Why = Cause.Streets;
                Fill(ref j, true, 1, 1000);
            }
            else if (pick < own + trouble + cash + weather + streets + season)
            {
                j.Why = Cause.Season;
                Fill(ref j, false, 0, 700 + size % 201);
                j.Kind = 0;
            }
            else
            {
                j.Why = Cause.Shortage;
                Fill(ref j, false, 0, 1000 - shortSize);
                j.Kind = shortKind;
            }
            return j;
        }

        private static void Fill(ref Judged j, bool late, int days, int share)
        {
            if (late)
            {
                j.Out = Arrival.Late;
                j.Days = SupplierCatalogue.Clamp(days, 1, SupplierCatalogue.LateDaysMost);
                j.Share = 1000;
            }
            else
            {
                j.Out = Arrival.Short;
                j.Days = 0;
                j.Share = SupplierCatalogue.Clamp(share, SupplierCatalogue.ShortLeast, SupplierCatalogue.ShortMost);
            }
        }

        /// <summary>The units of a line a short delivery brings: the share of them, in whole units, only where the line is of the kind that is short.</summary>
        public static int Brought(int units, int lineKind, Arrival outcome, int share, int shortKind)
        {
            if (outcome != Arrival.Short || (shortKind >= 0 && lineKind != shortKind))
            {
                return Math.Max(0, units);
            }
            return (int)((long)Math.Max(0, units) * SupplierCatalogue.Clamp(share, 0, 1000) / 1000);
        }

        // ---- a supplier's own day

        /// <summary>What a supplier's day did.</summary>
        public struct FirmDayResult
        {
            public bool Trouble, Under, Back, Grew;
        }

        /// <summary>
        /// One day of a supplier's own books. Its book of fixed prices follows the market slowly, so a market that runs over the book costs it on every fixed sale; its
        /// health moves by that strain, by the city's cycle, by a strike or a fire that a seeded draw starts, and back towards where it rests. At nothing it goes under and
        /// stays shut until new owners open it; strong for long enough it grows a step.
        /// </summary>
        /// <param name="factors">today's price factor of each kind of goods, thousandths</param>
        /// <param name="cycle">the city's cycle: 0 boom, 1 steady, 2 slowdown, 3 recession</param>
        public static FirmDayResult FirmDay(int seed, SupplierDef def, FirmState f, int day, int[] factors, int cycle)
        {
            var r = new FirmDayResult();
            if (!f.Open)
            {
                if (day >= f.ReopenDay)
                {
                    f.ClosedDay = f.ReopenDay = 0;
                    f.Health = SupplierCatalogue.ReopenHealth;
                    f.Sold = f.Sold || def.Owner >= 0;
                    f.Strong = 0;
                    f.TroubleKind = f.TroubleUntil = 0;
                    for (int k = 0; k < f.Book.Length; k++)
                    {
                        f.Book[k] = 1000;
                    }
                    r.Back = true;
                }
                return r;
            }
            int strain = 0, traded = 0;
            for (int k = 0; k < SupplierCatalogue.Kinds; k++)
            {
                if (!def.Trades(k))
                {
                    continue;
                }
                int factor = factors != null && k < factors.Length && factors[k] > 0 ? factors[k] : 1000;
                strain += factor - f.Book[k];
                traded++;
                int gap = Math.Max(1000, factor) - f.Book[k], step = gap / SupplierCatalogue.BookRollDays;
                f.Book[k] += step != 0 ? step : Math.Sign(gap);
            }
            strain = traded > 0 ? strain / traded : 0;
            int rest = Rest(def, f);
            int move = f.Health < rest ? SupplierCatalogue.Recover + Math.Min(SupplierCatalogue.RecoverMost, Math.Max(0, f.Customers) / SupplierCatalogue.RecoverPerCustomers)
                : f.Health > rest ? -SupplierCatalogue.Regress : 0;
            int cost = strain * SupplierCatalogue.BookShare / 1000 / SupplierCatalogue.StrainDiv;
            move -= strain > 0 ? cost : cost / 2;   // a market under its book helps it half as much as one over it hurts
            move += SupplierCatalogue.CycleDrift[SupplierCatalogue.Clamp(cycle, 0, 3)];
            if (f.InTrouble(day))
            {
                move -= SupplierCatalogue.TroubleDrain;
            }
            else
            {
                f.TroubleKind = f.TroubleUntil = 0;
                if (Roll(seed, def.Id, day, PurposeTrouble, 1000) < (1000 - def.Reliability) / SupplierCatalogue.TroubleDiv)
                {
                    f.TroubleKind = 1 + Roll(seed, def.Id, day, PurposeTroubleKind, 2);
                    f.TroubleUntil = day + SupplierCatalogue.TroubleDaysLeast
                        + Roll(seed, def.Id, day, PurposeTroubleDays, SupplierCatalogue.TroubleDaysMost - SupplierCatalogue.TroubleDaysLeast + 1) - 1;
                    r.Trouble = true;
                }
            }
            f.Health = SupplierCatalogue.Clamp(f.Health + move, 0, SupplierCatalogue.HealthMost);
            if (f.Health <= 0)
            {
                f.ClosedDay = day;
                f.ReopenDay = day + SupplierCatalogue.ReopenDays;
                f.Size = 1;
                f.Strong = 0;
                f.TroubleKind = f.TroubleUntil = 0;
                r.Under = true;
                r.Trouble = false;
                return r;
            }
            f.Strong = f.Health >= SupplierCatalogue.GrowHealth ? f.Strong + 1 : 0;
            if (f.Strong >= SupplierCatalogue.GrowDays && f.Size < SupplierCatalogue.SizeMost)
            {
                f.Size++;
                f.Strong = 0;
                r.Grew = true;
            }
            return r;
        }

        // ---- the rivals

        /// <summary>
        /// The supplier a shop of the city buys a kind of goods from: the one that suits it best among those that trade it and are open, by its price, its record and a
        /// draw of the shop's own, so shops spread over the suppliers and a shop stays with its choice. 0, the wholesale stores, where none is open.
        /// </summary>
        public static int PickFirm(int seed, int shop, int kind, SupplierState s)
        {
            int best = 0, bestScore = int.MinValue;
            for (int i = 0; i < SupplierCatalogue.Firms.Length; i++)
            {
                SupplierDef def = SupplierCatalogue.Firms[i];
                FirmState f = s.Firms[i];
                if (!def.Trades(kind) || !f.Open)
                {
                    continue;
                }
                int score = (1100 - Level(def, f)) * 2 + (Reliability(def, f) - 850) + Roll(seed, shop, def.Id, PurposeFirm, 240);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = def.Id;
                }
            }
            return best;
        }

        /// <summary>
        /// A rival shop's day on the supply side, by the rules the player lives by: a supplier of its own choosing, most on a fixed price written at the supplier's
        /// book, the rest at the day's price; on its delivery day the very draw the player's is judged by, and a late or short one costs it sales. What comes off is what
        /// it paid against the price its day was valued at, so the market's push is never counted twice (docs/features/suppliers.md, "The rivals"). Never more than a share
        /// of its day either way, nothing of a day it made nothing.
        /// </summary>
        /// <param name="baseline">thousandths of calm the day was valued at; 0 the kind's factor of the day (a city shop), 1000 calm (a tycoon's outlet)</param>
        public static RivalDay Rival(int seed, int shop, int kind, SupplierState s, int day, int factor, CityDay conditions, int[] shortage, int dayCents, int baseline = 0)
        {
            var r = new RivalDay();
            if (dayCents <= 0 || kind < 0 || kind >= SupplierCatalogue.Kinds)
            {
                return r;
            }
            r.Firm = PickFirm(seed, shop, kind, s);
            SupplierDef def = SupplierCatalogue.Firm(r.Firm);
            FirmState f = s.Firm(r.Firm);
            int market = factor > 0 ? factor : 1000, paid = market;
            if (def != null)
            {
                r.Contract = Roll(seed, shop, kind, PurposeRivalContract, 1000) < SupplierCatalogue.RivalContractShare;
                int level = Level(def, f);
                paid = r.Contract ? (int)((long)f.Book[kind] * level / 1000 * (1000 - SupplierCatalogue.TermDiscount[1]) / 1000) : (int)((long)market * level / 1000);
            }
            int valued = baseline > 0 ? baseline : market;
            long cost = (long)dayCents * SupplierCatalogue.RivalGoodsShare / 1000 * (paid - valued) / valued;
            if (def != null && Roll(seed, shop, 0, PurposeRivalDay, SupplierCatalogue.WeekDays) == day % SupplierCatalogue.WeekDays)
            {
                Judged j = Judge(seed, def, f, day, 1 << kind, conditions, shortage);
                int loss = j.Out == Arrival.Late ? j.Days * SupplierCatalogue.RivalLateLoss
                    : j.Out == Arrival.Short ? (1000 - j.Share) * SupplierCatalogue.RivalShortLoss / 1000 : 0;
                r.Failed = j.Out == Arrival.Late || j.Out == Arrival.Short;
                cost += (long)dayCents * loss / 1000;
            }
            long most = (long)dayCents * SupplierCatalogue.RivalMost / 1000;
            r.Cents = (int)SupplierCatalogue.Clamp(cost, -most, most);
            return r;
        }

        /// <summary>The words' tone for a supplier's health: 0 sound, 1 stretched, 2 in trouble.</summary>
        public static int HealthBand(int health)
        {
            return health >= 600 ? 0 : health >= SupplierCatalogue.HealthWeak ? 1 : 2;
        }
    }
}
