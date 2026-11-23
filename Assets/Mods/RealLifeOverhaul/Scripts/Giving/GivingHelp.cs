using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Giving
{
    /// <summary>What the player can do for a person with money (nothing keeps it, so the numbers may change): a plain gift, the rent, a debt, a small business, a course, a car, a home.</summary>
    public enum HelpKind
    {
        Money = 0,
        Rent = 1,
        Debt = 2,
        Business = 3,
        Course = 4,
        Car = 5,
        Home = 6,
        Count = 7
    }

    /// <summary>How a person answers an offer of help: they take it, or they would not take it from somebody they do not know so well, or pride, or nothing is needed.</summary>
    public enum HelpAnswer
    {
        Takes = 0,
        Distant = 1,    // the two are not close enough for this
        Proud = 2,      // they would not take a gift, not the first time
        Needless = 3    // they give rather than take, and need nothing
    }

    /// <summary>
    /// The rules of helping a person with money beyond a plain gift, with no game in them: who takes what, what each kind costs and what it changes in the person's own household and
    /// self (the population's fields the mod already keeps), and how leaning on the player's help builds. Money buys a person's trouble out of the way and nothing of their
    /// affection: what it earns goes through the same window as every gift (<see cref="GivingRules.PersonGain"/>), so the most it can earn is the same however much it is. Everything is
    /// worked out from its inputs and the save's seed, the same on every machine. COST: a few operations a call, nothing allocated.
    /// </summary>
    public static class GivingHelp
    {
        /// <summary>Whether somebody is proud, from the save's seed and their number: about one in five.</summary>
        public static bool Proud(int seed, int person) => GivingRules.Hash(seed, 61, person, 0) % 1000 < (uint)GivingCatalogue.ProudShare;

        /// <summary>
        /// How a person answers an offer. Not close enough (the kind's line, and a car or a home only from a close friend, a partner or family) is the first reason; somebody who gives
        /// rather than takes and needs nothing is the second; and somebody proud will not take a gift of more than a small kindness the first time (a loan they will), but takes it
        /// once <see cref="GivingCatalogue.ProudGapDays"/> days have passed since they refused. Once they have taken any help, pride is past.
        /// </summary>
        public static HelpAnswer Answer(HelpKind kind, bool loan, int amountAtStart, int affection, int trust, bool intimate, bool inNeed, bool proud, bool generous, PersonGiving g, int day)
        {
            if (affection + trust < GivingCatalogue.HelpLeast[(int)kind] || ((kind == HelpKind.Car || kind == HelpKind.Home) && !intimate))
            {
                return HelpAnswer.Distant;
            }
            if (generous && !inNeed && kind != HelpKind.Car && kind != HelpKind.Home && kind != HelpKind.Business)
            {
                return HelpAnswer.Needless;
            }
            if (proud && !loan && amountAtStart > GivingCatalogue.ProudAbove && (g == null || g.Helps <= 0)
                && (g == null || g.RefusedDay <= 0 || day - g.RefusedDay < GivingCatalogue.ProudGapDays))
            {
                return HelpAnswer.Proud;
            }
            return HelpAnswer.Takes;
        }

        // ---- who can be helped how, from the population's own fields

        /// <summary>A tenant of the city's landlords with a rent to pay: the rent can be paid for them.</summary>
        public static bool CanRent(PopulationState s, int h) => s.Households.Landlord[h] == Landlords.City && s.Households.Rent[h] > 0;

        /// <summary>A household in debt: the debt can be cleared.</summary>
        public static bool CanDebt(PopulationState s, int h) => s.Households.Debt[h] > 0;

        /// <summary>Somebody of working age in the labour force, or at home, or a student: a business can be backed or a course paid for.</summary>
        public static bool CanWork(PopulationState s, int p, int day)
        {
            int age = s.Age(p, day);
            var status = (Status)s.People.Status[p];
            return age >= 18 && age < PopulationCatalogue.RetireAge && (status == Status.Unemployed || status == Status.Worker || status == Status.Home);
        }

        /// <summary>Somebody who can take a course: of working age or a student.</summary>
        public static bool CanStudy(PopulationState s, int p, int day)
        {
            int age = s.Age(p, day);
            var status = (Status)s.People.Status[p];
            bool working = age < PopulationCatalogue.RetireAge && (status == Status.Unemployed || status == Status.Worker || status == Status.Home);
            return s.People.Skill[p] < 100 && age >= 16 && (status == Status.Student || working);
        }

        /// <summary>A household with room for another car.</summary>
        public static bool CanCar(PopulationState s, int h) => s.Households.Cars[h] < GivingCatalogue.CarsMost;

        /// <summary>A tenant of the city's landlords in a flat the city knows: a home of their own can be given.</summary>
        public static bool CanHome(PopulationState s, int h) => s.Households.Landlord[h] == Landlords.City && s.Households.Building[h] >= 0;

        // ---- what each costs, whole money at today's prices

        /// <summary>The rent for so many months: the last closed month's, or the day's rent over the month if the household has none yet. Whole money.</summary>
        public static long RentFor(PopulationState s, int h, int months, int monthDays)
        {
            long month = Math.Max(GivingPeople.MonthRent(s, h), (long)s.Households.Rent[h] * Math.Max(1, monthDays) / 100);
            return month * Math.Max(1, months);
        }

        /// <summary>A household's debt, held to the most a gift to a person can be at today's prices (the part the cap lets be cleared at once).</summary>
        public static long DebtFor(PopulationState s, int h, long most) => Math.Min(Math.Max(0L, (long)s.Households.Debt[h]), Math.Max(0L, most));

        /// <summary>What a home costs: the flat's market rent for a year of the save's days, times the price to rent ratio. Whole money; the rent is held in cents a day.</summary>
        public static long HomePrice(PopulationState s, int h)
        {
            int b = s.Households.Building[h];
            if (b < 0)
            {
                return 0L;
            }
            long rentADay = CityTable.FlatRentNow(s, b, s.Households.Unit[h]);
            return Math.Min(GivingCatalogue.AmountMost, rentADay * Math.Max(1, s.YearDays) / 100 * GivingCatalogue.HomePriceYears);
        }

        // ---- what each changes

        /// <summary>
        /// The rent paid for a household: the money in its savings (rent is paid from savings), its content a little more. Returns the money. The player's own, a tenant of the player's
        /// and an away household are never touched by the caller (<see cref="GivingPeople.Helpable"/>).
        /// </summary>
        public static long PayRent(PopulationState s, int h, long amount)
        {
            HouseholdTable hh = s.Households;
            long give = Math.Max(0L, amount);
            hh.Savings[h] = (int)Math.Min(int.MaxValue / 2, hh.Savings[h] + give);
            hh.Content[h] = (byte)GivingCatalogue.Clamp(hh.Content[h] + GivingCatalogue.ContentLift, 0, 100);
            return give;
        }

        /// <summary>
        /// A debt cleared: down by the amount, never under nothing, the content a little more. Returns what really went to the debt.
        /// </summary>
        public static long ClearDebt(PopulationState s, int h, long amount)
        {
            HouseholdTable hh = s.Households;
            long paid = Math.Min(Math.Max(0L, amount), hh.Debt[h]);
            hh.Debt[h] = (int)Math.Max(0L, hh.Debt[h] - paid);
            hh.Content[h] = (byte)GivingCatalogue.Clamp(hh.Content[h] + GivingCatalogue.ContentLift, 0, 100);
            return paid;
        }

        /// <summary>
        /// A small business backed: the money in the household's savings, the person's skill up by what running it teaches, and for somebody out of work the business is their work: they are
        /// in work, with no employer (the way the player's staff with no place yet are), at a share of what their skill would fetch at first. For somebody in work or at home only the
        /// skill and, for a worker, the pay that goes with it. Returns true when somebody out of work now has work.
        /// </summary>
        public static bool BackBusiness(PopulationState s, int p, int h, long amount, int day)
        {
            PersonTable pt = s.People;
            HouseholdTable hh = s.Households;
            hh.Savings[h] = (int)Math.Min(int.MaxValue / 2, hh.Savings[h] + Math.Max(0L, amount));
            pt.Skill[p] = (byte)Math.Min(100, pt.Skill[p] + GivingCatalogue.BusinessSkill);
            int field = Math.Max(1, (int)pt.Field[p]);
            int cls = hh.Class[h];
            if (pt.Status[p] == (byte)Status.Unemployed)
            {
                pt.Field[p] = (byte)field;
                pt.Job[p] = -1;
                pt.Status[p] = (byte)Status.Worker;
                pt.Since[p] = day;
                pt.Hours[p] = (byte)PopulationCatalogue.FullTimeHours;
                pt.Pay[p] = Math.Max(1, (int)((long)LabourRules.PayFor(s, field, pt.Skill[p], cls) * GivingCatalogue.BusinessPayShare / 1000));
                hh.Content[h] = (byte)GivingCatalogue.Clamp(hh.Content[h] + GivingCatalogue.ContentLift, 0, 100);
                return true;
            }
            if (pt.Status[p] == (byte)Status.Worker)
            {
                pt.Pay[p] = Math.Max(pt.Pay[p], LabourRules.PayFor(s, field, pt.Skill[p], cls));
            }
            hh.Content[h] = (byte)GivingCatalogue.Clamp(hh.Content[h] + GivingCatalogue.ContentLift, 0, 100);
            return false;
        }

        /// <summary>A course paid for: the person's skill up by a course, and the pay their skill fetches follows, for a worker now and for somebody out of work when they are next hired.</summary>
        public static void Teach(PopulationState s, int p, int h)
        {
            PersonTable pt = s.People;
            pt.Skill[p] = (byte)Math.Min(100, pt.Skill[p] + GivingCatalogue.CourseSkill);
            int field = Math.Max(1, (int)pt.Field[p]);
            if (pt.Status[p] == (byte)Status.Worker || pt.Status[p] == (byte)Status.Unemployed)
            {
                pt.Pay[p] = Math.Max(pt.Pay[p], LabourRules.PayFor(s, field, pt.Skill[p], s.Households.Class[h]));
            }
            s.Households.Content[h] = (byte)GivingCatalogue.Clamp(s.Households.Content[h] + GivingCatalogue.ContentLift, 0, 100);
        }

        /// <summary>A car given to a household: one more, and its content up. The most is <see cref="GivingCatalogue.CarsMost"/>.</summary>
        public static void GiveCar(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            hh.Cars[h] = (byte)Math.Min(GivingCatalogue.CarsMost, hh.Cars[h] + 1);
            hh.Content[h] = (byte)GivingCatalogue.Clamp(hh.Content[h] + GivingCatalogue.CarContent, 0, 100);
        }

        /// <summary>
        /// A home given: the household owns its flat, as the population's owners do (its rent becomes an owner's upkeep and tax and no mortgage, it never wants a move for the rent), and
        /// its content rises a good deal.
        /// </summary>
        public static void GiveHome(PopulationState s, int h)
        {
            HouseholdTable hh = s.Households;
            hh.Landlord[h] = Landlords.Own;
            hh.Flags[h] = (byte)(hh.Flags[h] & ~HouseholdFlags.Mortgage & ~HouseholdFlags.WantsMove & ~HouseholdFlags.Regulated);
            HousingRules.SetRent(s, h);
            hh.Content[h] = (byte)GivingCatalogue.Clamp(hh.Content[h] + GivingCatalogue.HomeContent, 0, 100);
        }

        // ---- a business loan's terms

        /// <summary>What is added to a business loan, once, whole money: a friend's share.</summary>
        public static int BusinessExtra(int principal) => (int)Math.Min(GivingCatalogue.AmountMost, (long)Math.Max(0, principal) * GivingCatalogue.BusinessInterest / 1000);
    }
}
