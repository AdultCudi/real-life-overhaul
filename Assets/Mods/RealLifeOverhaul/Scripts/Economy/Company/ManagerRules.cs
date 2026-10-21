using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The kinds of decision a manager can be given, each with its own switch. Saved as a bit each: never renumber.</summary>
    public enum ManagerTask
    {
        Raises = 0,     // a request for a raise, answered within the wage band
        Offers = 1,     // a rival's offer to one of the staff, matched within the wage band
        Overtime = 2,   // overtime the staff offer, agreed within the budget
        Claims = 3,     // an insurance claim, put in when it is worth what it adds to the next term
        Stock = 4,      // the week's order of a repeating delivery contract, set to keep the stock floor
        Prices = 5,     // the prices, within the price band, where no pricing manager of the game's covers the district
        Rota = 6,       // the week's rota, written by the rota planner
        Hiring = 7,     // somebody from the place's own job board hired to keep the staff it had
        Count = 8
    }

    /// <summary>Where a decision stands. Saved as a number.</summary>
    public enum DecisionState
    {
        Done = 0,           // made, and can still be undone
        Undone = 1,         // put back by the player
        Overridden = 2,     // the player changed it in the game afterwards: the manager leaves that one alone
        Closed = 3          // made, and too late to undo now
    }

    /// <summary>What a place is, for which kinds of decision it has. Not saved: read from the game each time.</summary>
    public enum ManagedKind
    {
        Shop = 0,           // customers walk in: every kind
        Works = 1,          // nobody walks in (a factory, a tech firm): stock, the rota and hiring
        Warehouse = 2       // no rota and no customers: stock and hiring
    }

    /// <summary>
    /// Every number of the managers, as whole numbers so a seed gives the same decisions: money in whole units at a price
    /// index of 100, a wage in hundredths an hour, a share in thousandths unless named per cent. The limits a player picks
    /// are steps of the arrays below; a manager keeps the step's number. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class ManagerCatalogue
    {
        public const int RolePlace = 0, RoleRegion = 1;

        public static readonly int[] WageBands = { 900, 950, 1000, 1050, 1100, 1200, 1300 };   // thousandths of the going rate the most anybody is paid
        public const int WageBandDefault = 3;
        public static readonly int[] StockDays = { 0, 1, 2, 3, 5, 7 };                          // days of use kept on hand beyond the week's
        public const int StockDaysDefault = 2;
        public static readonly int[] PriceLows = { 700, 800, 900, 1000 };                      // thousandths of the market price, the least
        public const int PriceLowDefault = 2;
        public static readonly int[] PriceHighs = { 1100, 1200, 1300, 1500, 2000 };            // thousandths of the market price, the most
        public const int PriceHighDefault = 2;
        public static readonly int[] Budgets = { 0, 500, 1000, 2500, 5000, 10000, 25000, 50000 };  // a month, at prices: overtime and hires
        public const int BudgetDefault = 3;
        public const int KeepMost = 60;             // staff a place may be told to keep at most

        public const int MistakeMost = 300;         // thousandths: a decision's chance of a mistake at a skill of nothing
        public const int ReviewCut = 400;           // thousandths of that chance a regional manager at a skill of 100 takes away
        public const int RegionalFar = 15;          // points of skill a regional manager lacks standing in for a place's manager off sick
        public const int StockSlipMost = 250;       // thousandths an order is off by, at most, in a mistake at a skill of nothing

        public static readonly int[] AllowanceBase = { 30, 80 };     // a day, at prices, by role: on top of the game's own wage
        public static readonly int[] AllowancePerSkill = { 1, 2 };   // a day for each point of skill, by role

        public const int PriceHour = 8;             // the hour prices are looked at (PricingManagerSettings.updateHour)
        public const int StaffHour = 9;             // the hour requests, offers, overtime and claims are answered
        public const int HireHour = 11;             // the hour the job board's candidates are looked at
        public const int StockHour = 12;            // the hour of a Sunday the week's orders are set: eight hours before the game's lock
        public const int RotaHour = 21;             // the hour of a Sunday the week's rota is written
        public const int DigestHour = 18;           // the hour the day's decisions are told in one notice
        public const int Sunday = 6;                // weekday of the game's own count, Monday 0 (TimeHelper.GetDayOfWeek)
        public const int DeliveryWeekday = 0;       // a repeating contract delivers on a Monday...
        public const int DeliveryHour = 8;          // ...at 8 (DeliveryHelper.DeliveryHour)

        public const int UndoDays = 14;             // days a raise, an offer matched or a price can be undone
        public const int HireUndoDays = 3;          // days a hire can be undone
        public const int HireWeekHours = 40;        // hours: a hire takes a week's pay out of the budget
        public const int PriceStep = 10;            // thousandths a price must move before it is changed
        public const int StockStep = 50;            // thousandths an order must move before it is changed
        public const int LearnHours = 12;           // hours of use at least before an order is worked out from them

        public const int ManagersMost = 64;         // places and districts with a manager
        public const int DecisionsKept = 160;
        public const int ItemsInDecision = 12;      // products one decision changes at most
        public const int SnapsMost = 32;            // products one place's stock is followed for
        public const int HeldMost = 32;             // products one place's manager leaves to the player
        public const int OrderMost = 99999;         // units, where a product has no limit of its own

        public static int Step(int[] steps, int index)
        {
            return steps[CompanyCatalogue.Clamp(index, 0, steps.Length - 1)];
        }
    }

    /// <summary>
    /// The managers' rules, game-free (EconomyCheck compiles them): who runs a place and how well, a mistake's chance and
    /// its bound, and each kind of decision within the player's limit. A mistake never takes a decision past a limit: it
    /// is a meaner answer, a match let go, a claim left for the player, an order a share off or a lower price inside the band.
    /// </summary>
    public static class ManagerRules
    {
        /// <summary>The kinds a place has, a bit each.</summary>
        public static int Kinds(ManagedKind kind)
        {
            switch (kind)
            {
                case ManagedKind.Shop: return (1 << (int)ManagerTask.Count) - 1;
                case ManagedKind.Works: return Bit(ManagerTask.Stock) | Bit(ManagerTask.Rota) | Bit(ManagerTask.Hiring);
                default: return Bit(ManagerTask.Stock) | Bit(ManagerTask.Hiring);
            }
        }

        public static int Bit(ManagerTask task)
        {
            return 1 << (int)task;
        }

        public static bool Has(int tasks, ManagerTask task)
        {
            return (tasks & Bit(task)) != 0;
        }

        /// <summary>The game's weekday of a day, Monday 0 (TimeHelper.GetDayOfWeek counts from day 1, a Monday).</summary>
        public static int Weekday(int day)
        {
            return ((day - 1) % 7 + 7) % 7;
        }

        /// <summary>The skill a regional manager stands in with at a place whose own manager is off sick.</summary>
        public static int FromAfar(int skill)
        {
            return Math.Max(0, CompanyCatalogue.Clamp(skill, 0, 100) - ManagerCatalogue.RegionalFar);
        }

        /// <summary>A decision's chance of a mistake, thousandths: less with skill, less again with a regional manager over them.</summary>
        public static int MistakeChance(int skill, int review)
        {
            int chance = ManagerCatalogue.MistakeMost * (100 - CompanyCatalogue.Clamp(skill, 0, 100)) / 100;
            return chance * (1000 - ManagerCatalogue.ReviewCut * CompanyCatalogue.Clamp(review, 0, 100) / 100) / 1000;
        }

        /// <summary>A number from 0 to 999, always the same for the same save, day, place, kind and subject.</summary>
        public static int Roll(int seed, int day, string place, int task, string subject)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = Mix(h, seed);
                h = Mix(h, day);
                h = Mix(h, task);
                h = Mix(h, place);
                h = Mix(h, subject);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % 1000u);
            }
        }

        private static uint Mix(uint h, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    h = (h ^ (uint)((value >> (i * 8)) & 0xff)) * 16777619u;
                }
                return h;
            }
        }

        private static uint Mix(uint h, string text)
        {
            unchecked
            {
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (h ^ 0xffu) * 16777619u;
            }
        }

        /// <summary>The manager's allowance a day, at today's prices (the index in tenths of a point).</summary>
        public static int Allowance(int role, int skill, int index)
        {
            int r = CompanyCatalogue.Clamp(role, 0, ManagerCatalogue.AllowanceBase.Length - 1);
            return CompanyCatalogue.AtPrices(ManagerCatalogue.AllowanceBase[r] + ManagerCatalogue.AllowancePerSkill[r] * CompanyCatalogue.Clamp(skill, 0, 100), index);
        }

        /// <summary>The most anybody is paid an hour under a wage band, hundredths.</summary>
        public static int Ceiling(int going, int bandStep)
        {
            return CompanyCatalogue.Share(Math.Max(0, going), ManagerCatalogue.Step(ManagerCatalogue.WageBands, bandStep));
        }

        /// <summary>
        /// The answer to a request for a raise: all of it if the pay asked for is inside the ceiling, else "less" if half
        /// of it is, else no. A mistake is one answer meaner, never more generous than the band.
        /// </summary>
        public static RaiseAnswer RaiseAnswerFor(int paid, int askTo, int ceiling, bool mistake)
        {
            int asked = Math.Max(0, askTo - paid);
            RaiseAnswer answer = paid + asked <= ceiling ? RaiseAnswer.Agree
                : paid + asked * CompanyCatalogue.RaiseLessShare / 100 <= ceiling ? RaiseAnswer.Less
                : RaiseAnswer.Refuse;
            if (mistake)
            {
                answer = answer == RaiseAnswer.Agree ? RaiseAnswer.Less : RaiseAnswer.Refuse;
            }
            return answer;
        }

        /// <summary>Whether a rival's offer is matched: only inside the ceiling. A mistake lets somebody go who could have stayed.</summary>
        public static bool Match(int offer, int ceiling, bool mistake)
        {
            return offer > 0 && offer <= ceiling && !mistake;
        }

        /// <summary>The budget left this month, never less than nothing.</summary>
        public static int Left(int budget, int spent)
        {
            return Math.Max(0, budget - Math.Max(0, spent));
        }

        /// <summary>Whether overtime is agreed: all its days must fit the budget left. A mistake turns it down.</summary>
        public static bool Overtime(int costADay, int days, int left, bool mistake)
        {
            long cost = (long)Math.Max(0, costADay) * Math.Max(0, days);
            return cost > 0 && cost <= left && !mistake;
        }

        /// <summary>
        /// Whether a claim is worth putting in: what it pays against what it adds to the next term (the loading on every
        /// day's premium of a term). A mistake leaves it for the player, who has the rest of its days to put it in.
        /// </summary>
        public static bool Claim(int payout, int premium, int termDays, bool mistake)
        {
            long adds = (long)Math.Max(0, premium) * CompanyCatalogue.ClaimLoading * Math.Max(0, termDays) / 1000;
            return payout > 0 && payout >= adds && !mistake;
        }

        /// <summary>How many Mondays at 8, the moment the game turns its weekly delivery counts over, lie after one moment and up to another.</summary>
        public static int Turns(int fromDay, int fromHour, int toDay, int toHour)
        {
            if (toDay < fromDay || (toDay == fromDay && toHour <= fromHour))
            {
                return 0;
            }
            int turns = 0;
            for (int day = fromDay; day <= toDay && turns < 3; day++)
            {
                if (Weekday(day) != ManagerCatalogue.DeliveryWeekday)
                {
                    continue;
                }
                bool after = day > fromDay || fromHour < ManagerCatalogue.DeliveryHour;
                bool upTo = day < toDay || toHour >= ManagerCatalogue.DeliveryHour;
                turns += after && upTo ? 1 : 0;
            }
            return turns;
        }

        /// <summary>
        /// Units a product's contracts brought since a look, from the game's counts of this week and last
        /// (DeliveryContractItem.amountOrderedThisWeek and amountOrderedLastWeek); -1 when more than one week has turned.
        /// </summary>
        public static int Delivered(int turns, int thisWeekThen, int lastWeekNow, int thisWeekNow)
        {
            if (turns == 0)
            {
                return Math.Max(0, thisWeekNow - thisWeekThen);
            }
            return turns == 1 ? Math.Max(0, lastWeekNow - thisWeekThen) + Math.Max(0, thisWeekNow) : -1;
        }

        /// <summary>Thousandths of a unit used a day since a look; -1 with too few hours to tell.</summary>
        public static int Use(int stockThen, int delivered, int stockNow, int hours)
        {
            if (hours < ManagerCatalogue.LearnHours || delivered < 0)
            {
                return -1;
            }
            long used = Math.Max(0L, (long)Math.Max(0, stockThen) + delivered - Math.Max(0, stockNow));
            return (int)Math.Min(int.MaxValue / 4, used * 24000L / hours);
        }

        /// <summary>Hours from one moment of the week to the next Monday at 8.</summary>
        public static int HoursToDelivery(int day, int hour)
        {
            int days = (7 + ManagerCatalogue.DeliveryWeekday - Weekday(day)) % 7;
            int hours = days * 24 + ManagerCatalogue.DeliveryHour - CompanyCatalogue.Clamp(hour, 0, 23);
            return hours <= 0 ? hours + 7 * 24 : hours;
        }

        /// <summary>
        /// The order for a Monday: the week's use and the floor's days on top, less what is expected to be left when the
        /// delivery comes. A mistake moves it by a share (thousandths, signed); inside nothing and the product's limit.
        /// </summary>
        public static int Order(int usePerDay, int stockNow, int hoursToDelivery, int floorDays, int most, int slip)
        {
            if (usePerDay < 0)
            {
                return -1;
            }
            long left = Math.Max(0L, (long)Math.Max(0, stockNow) * 1000 - (long)usePerDay * Math.Max(0, hoursToDelivery) / 24);
            long wanted = (long)usePerDay * (7 + Math.Max(0, floorDays)) - left;
            long units = wanted <= 0 ? 0 : (wanted + 999) / 1000;
            units = units * (1000 + CompanyCatalogue.Clamp(slip, -ManagerCatalogue.StockSlipMost, ManagerCatalogue.StockSlipMost)) / 1000;
            int cap = most > 0 ? most : ManagerCatalogue.OrderMost;
            return (int)Math.Max(0L, Math.Min(cap, units));
        }

        /// <summary>A mistake's share of an order, thousandths: as large as the skill is small, up or down by the roll.</summary>
        public static int Slip(int skill, int roll, bool mistake)
        {
            if (!mistake)
            {
                return 0;
            }
            int size = ManagerCatalogue.StockSlipMost * (100 - CompanyCatalogue.Clamp(skill, 0, 100)) / 100;
            return (roll & 1) == 0 ? size : -size;
        }

        /// <summary>Whether a change from one figure to another is worth making: by at least a share, and by one at least.</summary>
        public static bool Moves(int before, int after, int step)
        {
            int gap = Math.Abs(after - before);
            return gap >= 1 && (long)gap * 1000 >= (long)Math.Abs(before) * step;
        }

        /// <summary>A price in cents inside the band around the market price; -1 when there is no market price to judge by.</summary>
        public static int Price(int suggested, int market, int lowStep, int highStep)
        {
            if (market <= 0 || suggested <= 0)
            {
                return -1;
            }
            int low = CompanyCatalogue.Share(market, ManagerCatalogue.Step(ManagerCatalogue.PriceLows, lowStep));
            int high = CompanyCatalogue.Share(market, ManagerCatalogue.Step(ManagerCatalogue.PriceHighs, highStep));
            return CompanyCatalogue.Clamp(suggested, Math.Max(1, low), Math.Max(1, high));
        }

        /// <summary>Money a hire takes out of the budget: a week of the wage, in whole units.</summary>
        public static int HireCost(int wage)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, wage) * ManagerCatalogue.HireWeekHours / 100);
        }

        /// <summary>
        /// The candidate hired, by their place in the lists; -1 for none. Only those inside the ceiling for their own skill
        /// whose week fits the budget left; of them the most skilled, then the cheapest, then the first. A mistake takes the
        /// next best where there is one.
        /// </summary>
        public static int Pick(int[] wages, int[] goings, int[] skills, int count, int bandStep, int left, bool mistake)
        {
            int best = -1, second = -1;
            for (int i = 0; i < count; i++)
            {
                if (wages[i] <= 0 || wages[i] > Ceiling(goings[i], bandStep) || HireCost(wages[i]) > left)
                {
                    continue;
                }
                if (best < 0 || Better(i, best, wages, skills))
                {
                    second = best;
                    best = i;
                }
                else if (second < 0 || Better(i, second, wages, skills))
                {
                    second = i;
                }
            }
            return mistake && second >= 0 ? second : best;
        }

        private static bool Better(int a, int b, int[] wages, int[] skills)
        {
            return skills[a] != skills[b] ? skills[a] > skills[b] : wages[a] < wages[b];
        }

        /// <summary>
        /// Whether a manager answers this kind of thing at a place, so the company asks the player nothing: only with the
        /// managers on, a manager there who was found at work, the kind given, and never their own request or offer.
        /// Switched off (or the post empty) it is false, and the player is asked again as before.
        /// </summary>
        public static bool Answers(bool on, ManagerFile m, ManagerTask task, string employee)
        {
            return on && m != null && m.Seen && Has(m.Tasks, task) && !string.Equals(m.Id, employee, StringComparison.Ordinal);
        }

        /// <summary>
        /// Who decides at a place today: 1 its manager (with the regional manager's review), 2 the regional manager standing
        /// in for a manager off sick, 0 nobody (the decisions wait a day).
        /// </summary>
        public static int Decider(bool own, bool ownSick, int ownSkill, bool region, bool regionSick, int regionSkill, out int skill, out int review)
        {
            skill = review = 0;
            if (!own)
            {
                return 0;
            }
            if (!ownSick)
            {
                skill = CompanyCatalogue.Clamp(ownSkill, 0, 100);
                review = region ? CompanyCatalogue.Clamp(regionSkill, 0, 100) : 0;
                return 1;
            }
            if (!region || regionSick)
            {
                return 0;
            }
            skill = FromAfar(regionSkill);
            return 2;
        }

        /// <summary>Whether a decision is still inside the days it can be undone in: a hire's few, the rest's fortnight.</summary>
        public static bool InTime(ManagerTask task, int made, int day)
        {
            return day - made <= (task == ManagerTask.Hiring ? ManagerCatalogue.HireUndoDays : ManagerCatalogue.UndoDays);
        }

        /// <summary>Whether the delivery an order was set for has not come yet (the game delivers at 8 on its day).</summary>
        public static bool Undelivered(int deliveryDay, int day, int hour)
        {
            return day < deliveryDay || (day == deliveryDay && hour < ManagerCatalogue.DeliveryHour);
        }

        /// <summary>Whether a figure the manager set has been changed since by somebody else: the player, in the game's own screen.</summary>
        public static bool Changed(bool set, int setTo, int now)
        {
            return set && setTo != now;
        }

        /// <summary>Whether a month has ended since it began.</summary>
        public static bool MonthOver(int from, int day, int monthDays)
        {
            return day - from >= Math.Max(1, monthDays);
        }
    }
}
