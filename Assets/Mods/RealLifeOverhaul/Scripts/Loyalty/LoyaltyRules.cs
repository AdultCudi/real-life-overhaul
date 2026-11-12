using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>
    /// Every rule of the loyalty cards, game-free and in whole numbers, so the same days give the same members on every
    /// machine and EconomyCheck runs them (LoyaltyChecks.cs). Members grow from the customers really served, by how pleased
    /// they were; they come back more often and take a little more, and fall away from a shop that is shut, empty, dirty or
    /// dear. Each step of the reward rate brings less than the one before and costs as much again, a sign-up push only buys
    /// speed, a members' day keeps members at double points, and rival cards in the district take a share of the lift. What
    /// the player chooses comes into force at the next day's turn (<see cref="Turn"/>).
    /// </summary>
    public static class LoyaltyRules
    {
        public const int WeekDays = 7;            // days the page adds up
        public const int Rates = 4;               // steps of the reward rate
        public const int RateStart = 1;           // the step a new scheme starts on
        public const int Pushes = 3;              // steps of the sign-up push
        public const int FilesMost = 200;         // schemes a save keeps

        public static readonly int[] RateMille = { 10, 20, 30, 50 };          // per mille of a member's basket given back
        public static readonly int[] RateAppeal = { 700, 1000, 1180, 1350 };  // per mille: how readily a customer takes the card
        public static readonly int[] RateVisits = { 55, 90, 112, 133 };       // per mille more often a member comes back
        public static readonly int[] RateBasket = { 20, 35, 45, 55 };         // per mille of members' visits that take one more product
        public static readonly int[] RateKeeps = { 1300, 1000, 880, 780 };    // per mille of the everyday falling away
        public static readonly int[] PushJoin = { 1000, 1500, 2100 };         // per mille of the sign-ups without a push
        public static readonly int[] PushCost = { 0, 50, 150 };               // cents a new member, at the start's prices

        public const int JoinBase = 40;           // per mille of customers without a card who take one in a day, pleased, at the middle rate
        public const int PleasedFrom = 40;        // satisfaction (0 to 100) under which nobody takes a card
        public const int PleasedFull = 80;        // satisfaction from which every pleased customer counts in full
        public const int LapseBase = 15;          // per mille of members who fall away in a day whatever the shop does
        public const int LapseShutMost = 90;      // per mille more a day when no opening hour could serve anybody
        public const int LapseDirtyMost = 50;     // per mille more a day at a filthy shop
        public const int LapseDearMost = 70;      // per mille more a day when every price is turned down
        public const int LapseMost = 250;         // per mille a day at the very worst
        public const int LapseOff = 150;          // per mille a day once the scheme has ended
        public const int CleanFrom = 60;          // cleanliness (0 to 100) under which members begin to leave
        public const int PricesFrom = 70;         // per cent of prices accepted under which members begin to leave
        public const int CutLapse = 80;           // per mille of members lost at once for each step the rate is cut
        public const int PoolMost = 600;          // per mille of a shop's customer base who will ever carry its card
        public const int BaseDays = 5;            // a shop's customer base is this many days' customers
        public const int BaseSmooth = 7;          // days the average of customers served is taken over
        public const int LiftMost = 12;           // per cent of customers: the most a scheme ever asks for (a members' day at the top rate)
        public const int OfferVisits = 1500;      // per mille of a member's extra visits on the members' day
        public const int OfferOther = 920;        // per mille on the other six days: the visits move, they are not made
        public const int OfferJoin = 1150;        // per mille of sign-ups with a members' day
        public const int OfferKeeps = 800;        // per mille of the everyday falling away with a members' day
        public const int OfferPoints = 2;         // times the rewards on the members' day
        public const int OfferNotice = 7;         // days between choosing a members' day and its first
        public const int ChainReach = 60;         // per mille more of the base carries a card for each other shop of the brand that takes it
        public const int ChainReachMost = 240;    // per mille: four shops' worth, and no more
        public const int SetupFee = 1500;         // dollars at the start's prices: the cards, the tills and the leaflets of a scheme
        public const int ChainSetupShare = 400;   // per mille of that for a shop joining its brand's running scheme
        public const int ChangeShare = 200;       // per mille of that to print a new reward rate
        public const int CardDay = 12;            // dollars a day at the start's prices while a scheme runs
        public const int RewardMost = 100;        // per mille of a day's takings: the most a day's rewards ever come to
        public const int RivalFrom = 55;          // how well another owner's shop must serve (its crew's skill, 0 to 100) to run a card
        public const int RivalSetupDays = 14;     // days a shop trades before it starts one
        public const int RivalKeenTycoon = 60;    // per cent of rivals' shops that would run one
        public const int RivalKeenCity = 35;      // per cent of the city's own shops that would
        public const int RivalSettleDays = 60;    // days of its scheme a rival's shop is worked out over
        public const int RivalFloor = 400;        // per mille: the least of a scheme's lift that rival cards leave
        public const int RivalryMost = 1500;      // per mille: the most rival strength that is counted
        public const int MarginUnknown = -1;      // a shop with no books yet

        private static readonly int[] RivalRates = { 0, 1, 1, 2, 1, 1, 2, 3 };   // the steps other owners choose: mostly the middle ones

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        /// <summary>A stable number from a text, the same on every machine (FNV-1a).</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619;
                }
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>0 to 999 from a seed and two numbers, the same on every machine.</summary>
        public static int Roll(int seed, int a, int b)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 40503u ^ (uint)b * 2246822519u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % 1000u);
            }
        }

        /// <summary>The day of the week of a game day, 0 for Monday (day 1 is a Monday, as the game counts).</summary>
        public static int Weekday(int day) => ((day - 1) % WeekDays + WeekDays) % WeekDays;

        /// <summary>How far customers are pleased enough to take a card, per mille.</summary>
        public static int Pleased(int satisfaction)
        {
            return Clamp((satisfaction - PleasedFrom) * 1000 / (PleasedFull - PleasedFrom), 0, 1000);
        }

        /// <summary>A shop's customer base in hundredths of a customer, from its average day (hundredths).</summary>
        public static long BaseCustomers(int baseAverage) => (long)Math.Max(0, baseAverage) * BaseDays;

        /// <summary>The most members a shop can have, hundredths: a share of its base, a little more where the brand's other shops take the card.</summary>
        public static long Cap(int baseAverage, int sisters)
        {
            int reach = Math.Min(ChainReachMost, Math.Max(0, sisters) * ChainReach);
            return BaseCustomers(baseAverage) * PoolMost / 1000 * (1000 + reach) / 1000;
        }

        /// <summary>The members as a share of the customer base, per mille.</summary>
        public static int Share(int members, int baseAverage)
        {
            long customers = BaseCustomers(baseAverage);
            return customers <= 0 || members <= 0 ? 0 : (int)Math.Min(1000L, members * 1000L / customers);
        }

        /// <summary>A member's extra visits on a day of the week, per mille of the rate's own: more on the members' day, fewer on the others.</summary>
        public static int OfferFactor(int offerDay, int weekday)
        {
            return offerDay < 0 ? 1000 : weekday == offerDay ? OfferVisits : OfferOther;
        }

        private static int VisitsMore(int rate, int offerDay, int weekday)
        {
            return RateVisits[Clamp(rate, 0, Rates - 1)] * OfferFactor(offerDay, weekday) / 1000;
        }

        /// <summary>The share of a day's customers who are members, per mille: their share of the base, weighed by how much more often they come.</summary>
        public static int VisitShare(int members, int baseAverage, int rate, int offerDay, int weekday)
        {
            long p = Share(members, baseAverage);
            long often = p * (1000 + VisitsMore(rate, offerDay, weekday));
            long all = often + (1000 - p) * 1000;
            return all <= 0 ? 0 : (int)(often * 1000 / all);
        }

        /// <summary>What rival cards leave of a scheme's lift and of its sign-ups, per mille: less with every rival scheme, never under <see cref="RivalFloor"/>.</summary>
        public static int RivalryKept(int rivalry)
        {
            return Math.Max(RivalFloor, 1000 * 1000 / (1000 + Clamp(rivalry, 0, RivalryMost)));
        }

        /// <summary>The customers a scheme adds on a day, per mille of the shop's own, before it is made a whole per cent.</summary>
        public static int LiftMille(int members, int baseAverage, int rate, int offerDay, int weekday, int rivalry)
        {
            return (int)((long)Share(members, baseAverage) * VisitsMore(rate, offerDay, weekday) / 1000 * RivalryKept(rivalry) / 1000);
        }

        /// <summary>
        /// The per cent of customers a scheme asks for on a day, 0 to <see cref="LiftMost"/>. The game's figure takes whole per cents,
        /// so the tenths are given as whole per cents on that many days in ten: over ten days the lift is the rule's exactly.
        /// </summary>
        public static int Lift(int members, int baseAverage, int rate, int offerDay, int weekday, int rivalry, int day)
        {
            int mille = LiftMille(members, baseAverage, rate, offerDay, weekday, rivalry);
            int tenths = mille % 10, d = Math.Max(0, day);
            int extra = (int)(((long)(d + 1) * tenths) / 10 - ((long)d * tenths) / 10);
            return Clamp(mille / 10 + extra, 0, LiftMost);
        }

        /// <summary>The share of a day's customers who take one more product, per mille.</summary>
        public static int BasketMille(int members, int baseAverage, int rate, int offerDay, int weekday)
        {
            return VisitShare(members, baseAverage, rate, offerDay, weekday) * RateBasket[Clamp(rate, 0, Rates - 1)] / 1000;
        }

        /// <summary>Customers without a card who take one in a day, in millionths.</summary>
        public static int Join(int satisfaction, int rate, int push, bool offer, int rivalry)
        {
            long join = JoinBase * 1000L;
            join = join * Pleased(satisfaction) / 1000;
            join = join * RateAppeal[Clamp(rate, 0, Rates - 1)] / 1000;
            join = join * PushJoin[Clamp(push, 0, Pushes - 1)] / 1000;
            join = join * (offer ? OfferJoin : 1000) / 1000;
            return (int)(join * RivalryKept(rivalry) / 1000);
        }

        /// <summary>Members who fall away in a day, per mille: the everyday share, and more for every hour shut or empty, for dirt and for dear prices.</summary>
        public static int Lapse(int rate, bool offer, int openHours, int badHours, int clean, int pricing)
        {
            int lapse = LapseBase * RateKeeps[Clamp(rate, 0, Rates - 1)] / 1000 * (offer ? OfferKeeps : 1000) / 1000;
            if (openHours > 0 && badHours > 0)
            {
                lapse += LapseShutMost * Math.Min(badHours, openHours) / openHours;
            }
            if (clean < CleanFrom)
            {
                lapse += LapseDirtyMost * (CleanFrom - Math.Max(0, clean)) / CleanFrom;
            }
            if (pricing < PricesFrom)
            {
                lapse += LapseDearMost * (PricesFrom - Math.Max(0, pricing)) / PricesFrom;
            }
            return Clamp(lapse, 0, LapseMost);
        }

        /// <summary>
        /// One day of a running scheme, for the player's shop and another owner's alike: members' visits among the customers
        /// served, the members lost, the new ones (never past the cap), and the average day brought up to date. Members,
        /// the average and what comes out are in hundredths but <paramref name="visits"/>, which is whole.
        /// </summary>
        public static void Step(ref int members, ref int baseAverage, int served, int rate, int push, int offerDay, int weekday, int satisfaction, int pricing, int clean,
            int openHours, int badHours, int rivalry, int sisters, out int visits, out int signups, out int lapsed)
        {
            served = Math.Max(0, served);
            if (baseAverage <= 0)
            {
                baseAverage = served * 100;   // a shop first seen: its base is the day it had
            }
            visits = (int)((long)served * VisitShare(members, baseAverage, rate, offerDay, weekday) / 1000);
            baseAverage = (int)(((long)baseAverage * (BaseSmooth - 1) + served * 100L) / BaseSmooth);
            long cap = Cap(baseAverage, sisters);
            long lost = (long)members * Lapse(rate, offerDay >= 0, openHours, badHours, clean, pricing) / 1000;
            long left = members - lost;
            if (left > cap)
            {
                lost += left - cap;   // a base that shrank holds fewer members
                left = cap;
            }
            long joined = (long)(served - visits) * Join(satisfaction, rate, push, offerDay >= 0, rivalry) / 10000;
            joined = Math.Max(0L, Math.Min(joined, cap - left));
            members = (int)Math.Max(0L, left + joined);
            signups = (int)joined;
            lapsed = (int)lost;
        }

        /// <summary>
        /// Yesterday worked for a business with the settings that were in force: its members, what was given back, what the card
        /// cost at <paramref name="priceLevel"/> and what the scheme brought, written into the file's week. A scheme that is off only
        /// loses members. Rewards are a share of members' takings, never over <see cref="RewardMost"/> of the day's.
        /// </summary>
        public static LoyaltyResult Day(LoyaltyFile f, LoyaltyInput d, int priceLevel)
        {
            var r = new LoyaltyResult();
            if (!f.On)
            {
                int gone = f.Members < 100 ? f.Members : f.Members * LapseOff / 1000;
                f.Members -= gone;
                r.Lapsed = gone;
                f.Base = f.Base <= 0 ? d.Served * 100 : (int)(((long)f.Base * (BaseSmooth - 1) + Math.Max(0, d.Served) * 100L) / BaseSmooth);
                Note(f, d.Day, r);
                return r;
            }
            int share = VisitShare(f.Members, f.Base, f.Rate, f.OfferDay, d.Weekday);
            Step(ref f.Members, ref f.Base, d.Served, f.Rate, f.Push, f.OfferDay, d.Weekday, d.Satisfaction, d.Pricing, d.Clean, f.OpenHours, f.BadHours, f.Rivalry, f.Sisters,
                out r.Visits, out r.Signups, out r.Lapsed);
            long takings = Math.Max(0L, d.Takings);
            long rewards = takings * share / 1000 * RateMille[Clamp(f.Rate, 0, Rates - 1)] / 1000;
            if (f.OfferDay >= 0 && f.OfferDay == d.Weekday)
            {
                rewards *= OfferPoints;
            }
            r.Rewards = Math.Min(rewards, takings * RewardMost / 1000);
            long costs = (long)r.Signups * PushCost[Clamp(f.Push, 0, Pushes - 1)] / 100 + CardDay * 100L;
            r.Costs = costs * Clamp(priceLevel, PriceRules.Least, PriceRules.Most) / PriceRules.Start;   // at today's prices
            r.Extra = Extra(takings, f.Carried, f.Added, d.Items);
            r.Margin = takings > 0 ? r.Extra * Math.Max(0L, takings - Math.Max(0L, d.Goods)) / takings : 0L;
            Note(f, d.Day, r);
            return r;
        }

        private static void Note(LoyaltyFile f, int day, LoyaltyResult r)
        {
            f.Week[(day % WeekDays + WeekDays) % WeekDays] = new LoyaltyDay
            {
                Day = day, Visits = r.Visits, Signups = r.Signups, Lapsed = r.Lapsed, Rewards = r.Rewards, Costs = r.Costs, Extra = r.Extra, Margin = r.Margin
            };
        }

        /// <summary>
        /// The takings a scheme brought on a day, cents: the share of them that came with the customers its lift really added
        /// (<paramref name="carried"/>, per cent), and the products it put in baskets as a share of the products sold.
        /// </summary>
        public static long Extra(long takings, int carried, int added, int items)
        {
            if (takings <= 0)
            {
                return 0L;
            }
            carried = Clamp(carried, 0, LiftMost);
            long lift = takings * carried / (100 + carried);
            long basket = items > 0 && added > 0 ? (takings - lift) * Math.Min(added, items / 2) / items : 0L;
            return lift + basket;
        }

        /// <summary>
        /// The day's turn: what the player chose comes into force. The switch, a new rate (a cut loses members at once), a members'
        /// day after its notice; then today's lift and basket from the members there are, and the hours' count starts again. True
        /// if a new rate came into force on a running scheme: its printing is to be paid.
        /// </summary>
        public static bool Turn(LoyaltyFile f, int today)
        {
            bool printed = false;
            if (f.Want && !f.On)
            {
                f.On = true;
                f.Since = today;
            }
            else if (!f.Want && f.On)
            {
                f.On = false;
            }
            f.NextRate = Clamp(f.NextRate, 0, Rates - 1);
            if (f.NextRate != f.Rate)
            {
                if (f.NextRate < f.Rate)
                {
                    f.Members -= (int)((long)f.Members * CutLapse * (f.Rate - f.NextRate) / 1000);
                }
                printed = f.On && f.Since != today;
                f.Rate = f.NextRate;
            }
            if (f.NextOffer == f.OfferDay)
            {
                f.OfferFrom = 0;
                f.OfferAsked = f.NextOffer;
            }
            else if (f.NextOffer < 0)
            {
                f.OfferDay = f.OfferAsked = LoyaltyFile.NoDay;   // stopping one needs no notice
                f.OfferFrom = 0;
            }
            else if (f.OfferFrom == 0 || f.OfferAsked != f.NextOffer)
            {
                f.OfferDay = LoyaltyFile.NoDay;   // the old day ends as the new one is announced
                f.OfferAsked = f.NextOffer;
                f.OfferFrom = today + OfferNotice;
            }
            else if (today >= f.OfferFrom)
            {
                f.OfferDay = f.NextOffer;
                f.OfferFrom = 0;
            }
            Today(f, today);
            f.OpenHours = f.BadHours = f.Added = 0;
            f.Carried = 0;
            f.LastDay = today;
            return printed;
        }

        /// <summary>Today's lift and basket of a scheme from its members as they stand; nothing while it is off.</summary>
        public static void Today(LoyaltyFile f, int today)
        {
            int weekday = Weekday(today);
            f.Lift = f.On ? Lift(f.Members, f.Base, f.Rate, f.OfferDay, weekday, f.Rivalry, today) : 0;
            f.Basket = f.On ? BasketMille(f.Members, f.Base, f.Rate, f.OfferDay, weekday) : 0;
        }

        /// <summary>What switching a scheme on costs, dollars at the start's prices: less for a shop joining its brand's running scheme.</summary>
        public static int SetupCost(int sistersRunning)
        {
            return sistersRunning > 0 ? SetupFee * ChainSetupShare / 1000 : SetupFee;
        }

        /// <summary>What printing a new reward rate costs, dollars at the start's prices.</summary>
        public static int ChangeCost() => SetupFee * ChangeShare / 1000;

        /// <summary>
        /// The margin on its goods (per mille of takings) from which a scheme's extra visits alone pay for its rewards, with a full
        /// card base over a week: rewards are paid on everything a member spends, the margin is earned only on what the card adds.
        /// The fuller baskets come on top and are not counted, so at this margin a scheme never gives back more than it brings. A
        /// choice under it is the player's own: the page says so before and after.
        /// </summary>
        public static int BreakEven(int rate, bool offer, int rivalry)
        {
            rate = Clamp(rate, 0, Rates - 1);
            const int average = 1000;   // hundredths: any base gives the same shares
            int members = (int)(BaseCustomers(average) * PoolMost / 1000);
            long gain = 0, cost = 0;
            for (int weekday = 0; weekday < WeekDays; weekday++)
            {
                int offerDay = offer ? 0 : LoyaltyFile.NoDay;
                int brought = LiftMille(members, average, rate, offerDay, weekday, rivalry);
                long paid = (long)VisitShare(members, average, rate, offerDay, weekday) * RateMille[rate] * (offer && weekday == 0 ? OfferPoints : 1) * (1000 + brought) / 1000;
                gain += brought;
                cost += paid;
            }
            return gain <= 0 ? 1000 : (int)Math.Min(1000L, (cost + gain - 1) / gain);
        }

        /// <summary>True when a shop's margin carries a rate: at it, a full card base gives back no more than the margin it brings.</summary>
        public static bool Sound(int rate, bool offer, int rivalry, int marginMille)
        {
            return marginMille == MarginUnknown || marginMille >= BreakEven(rate, offer, rivalry);
        }

        /// <summary>The last seven days of a scheme added up (the days before <paramref name="today"/>).</summary>
        public static LoyaltyDay Week(LoyaltyFile f, int today)
        {
            var sum = new LoyaltyDay { Day = today };
            for (int i = 0; i < f.Week.Length; i++)
            {
                LoyaltyDay d = f.Week[i];
                if (d.Day <= 0 || d.Day >= today || d.Day < today - WeekDays)
                {
                    continue;
                }
                sum.Visits += d.Visits;
                sum.Signups += d.Signups;
                sum.Lapsed += d.Lapsed;
                sum.Rewards += d.Rewards;
                sum.Costs += d.Costs;
                sum.Extra += d.Extra;
                sum.Margin += d.Margin;
            }
            return sum;
        }

        // ---- the other owners' shops, by the same rules

        /// <summary>True for a shop of another owner that runs a card: pleasing enough, trading long enough, and one of those that would.</summary>
        public static bool RivalRuns(LoyaltyRival r)
        {
            return r.Quality >= RivalFrom && r.Days >= RivalSetupDays && Roll(r.Seed, 1, 0) % 100 < (r.Tycoon ? RivalKeenTycoon : RivalKeenCity);
        }

        /// <summary>The reward rate's step another owner's shop runs, from its seed.</summary>
        public static int RivalRate(LoyaltyRival r) => RivalRates[Roll(r.Seed, 2, 0) % RivalRates.Length];

        /// <summary>
        /// How strong another owner's scheme is, per mille of its own customer base carrying its card (0 to <see cref="PoolMost"/>):
        /// the days since it began, lived by <see cref="Step"/> with how well it serves in the place of satisfaction, as the player's are.
        /// </summary>
        public static int RivalStrength(LoyaltyRival r)
        {
            if (!RivalRuns(r))
            {
                return 0;
            }
            int rate = RivalRate(r), push = Roll(r.Seed, 3, 0) % Pushes;
            int members = 0, average = 10000, days = Math.Min(RivalSettleDays, r.Days - RivalSetupDays);
            for (int d = 0; d < days; d++)
            {
                Step(ref members, ref average, 100, rate, push, LoyaltyFile.NoDay, d % WeekDays, r.Quality, 100, 100, 10, 0, 0, 0, out _, out _, out _);
            }
            return Share(members, average);
        }

        /// <summary>The strength of every rival scheme of a shop's kind in its district, per mille, and how many of those shops run one.</summary>
        public static int Rivalry(List<LoyaltyRival> rivals, out int running)
        {
            int sum = 0;
            running = 0;
            for (int i = 0; rivals != null && i < rivals.Count; i++)
            {
                int strength = RivalStrength(rivals[i]);
                running += RivalRuns(rivals[i]) ? 1 : 0;
                sum += strength;
            }
            return Math.Min(RivalryMost, sum);
        }

        // ---- the player's presses

        /// <summary>The next step of a choice that goes round: after the last comes the first.</summary>
        public static int Next(int value, int least, int most) => value >= most || value < least ? least : value + 1;

        /// <summary>True for a scheme the save need not keep: off, not wanted, nobody left and nothing in its week.</summary>
        public static bool Spent(LoyaltyFile f, int today)
        {
            if (f.On || f.Want || f.Members >= 100)
            {
                return false;
            }
            LoyaltyDay week = Week(f, today);
            return week.Rewards == 0 && week.Costs == 0 && week.Visits == 0;
        }

        /// <summary>Every figure of a file inside its bounds, for one read from a save.</summary>
        public static void Bound(LoyaltyFile f)
        {
            f.Rate = Clamp(f.Rate, 0, Rates - 1);
            f.NextRate = Clamp(f.NextRate, 0, Rates - 1);
            f.Push = Clamp(f.Push, 0, Pushes - 1);
            f.OfferDay = Clamp(f.OfferDay, LoyaltyFile.NoDay, WeekDays - 1);
            f.NextOffer = Clamp(f.NextOffer, LoyaltyFile.NoDay, WeekDays - 1);
            f.OfferAsked = Clamp(f.OfferAsked, LoyaltyFile.NoDay, WeekDays - 1);
            f.OfferFrom = Math.Max(0, f.OfferFrom);
            f.Members = Clamp(f.Members, 0, 100000000);
            f.Base = Clamp(f.Base, 0, 100000000);
            f.Lift = Clamp(f.Lift, 0, LiftMost);
            f.Basket = Clamp(f.Basket, 0, 1000);
            f.OpenHours = Clamp(f.OpenHours, 0, 24);
            f.BadHours = Clamp(f.BadHours, 0, f.OpenHours);
            f.Added = Math.Max(0, f.Added);
            f.Carried = Clamp(f.Carried, 0, LiftMost);
            f.Paid = Math.Max(0L, f.Paid);
        }
    }
}
