using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// Helping a person beyond a plain gift: the rent, a debt, a small business, a course, a car, a home. Each is one press that ends in a result. The person answers first (some refuse, by
    /// the social rules: not close enough, too proud, in no need), and only then is money taken; what it changes is the person's own household and self in the population's fields
    /// (<see cref="GivingHelp"/>), and what it earns in affection and trust goes through the same window as every gift, so money is never a shortcut to the top.
    /// </summary>
    public sealed partial class GivingDirector
    {
        private static readonly HelpKind[] Offered = { HelpKind.Rent, HelpKind.Debt, HelpKind.Business, HelpKind.Course, HelpKind.Car, HelpKind.Home };
        private static readonly int[] RentMonths = { 3, 1, 6 };   // the answers' months: the usual first, then one and six
        private readonly string[] _carNames = new string[3];
        private readonly int[] _carPrices = new int[3];

        // ---- how a person takes it, and what it earns

        // The person's answer to an offer of help: a result that is Done when they take it. Pride refused is remembered with its day, so the same offer is taken after a few days.
        private GiveResult Takes(SocialPerson person, HelpKind kind, bool loan, int atStart, bool inNeed, PersonGiving g)
        {
            bool intimate = person.IsFamily || person.Together || person.Stage >= SocialStage.CloseFriend;
            int day = GivingGame.Day;
            HelpAnswer a = GivingHelp.Answer(kind, loan, atStart, (int)person.Affection, (int)person.Trust, intimate, inNeed, GivingHelp.Proud(_state.Seed, person.Index),
                person.Has(SocialTrait.Generous), g, day);
            if (a == HelpAnswer.Proud && g.RefusedDay <= 0)
            {
                g.RefusedDay = day;
            }
            if (a != HelpAnswer.Takes)
            {
                SocialLife.Contacted(person);
                _revision++;
                if (Verbose)
                {
                    Logger?.Info("Giving check (" + person.Name + " is offered " + kind + " of " + atStart + "): " + a + ", nothing taken: PASS");
                }
            }
            return a == HelpAnswer.Takes ? GiveResult.Done : a == HelpAnswer.Proud ? GiveResult.Proud : a == HelpAnswer.Needless ? GiveResult.Needless : GiveResult.Distant;
        }

        // What help earns in the person's window, with the social rules for money: the same curve for every kind, half when they needed nothing, and an envious person feels less trust.
        private void Warm(SocialPerson person, PersonGiving g, int countedAtStart, bool inNeed, out int affection, out int trust)
        {
            GivingRules.PersonGain(g, countedAtStart, GivingGame.Day, inNeed, out affection, out trust);
            if (person.Has(SocialTrait.Envious))
            {
                trust = trust * GivingCatalogue.EnviousTrustShare / 1000;
            }
            SocialLife.Contacted(person);
            SocialLife.Change(person, 0f, affection / 10f, trust / 10f);
        }

        // A help counted: the dependence as it stands, one more, today, and an ask for more is answered.
        private void Helped(PersonGiving g, int atStart)
        {
            int day = GivingGame.Day;
            g.Helps = GivingRules.Dependence(g, day) + 1;
            g.HelpDay = day;
            g.LastHelp = atStart;
            g.MorePending = 0;
        }

        /// <summary>Whether the player has paid this person's rent and it is still theirs to pay.</summary>
        public bool RentCovered(SocialPerson person)
        {
            PersonGiving g = person != null ? _state.PersonOf(person.Index, false) : null;
            return g != null && g.CoveredUntil > GivingGame.Day;
        }

        // ---- what can be offered

        /// <summary>
        /// The kinds of help this person could be offered now, in the order a call lists them. Money and loans are the call's own; these are the rest: a rent to pay, a debt to clear, a business
        /// to back, a course to pay for, a car or a home from somebody the player is close to. Fills the array and returns how many.
        /// </summary>
        public int HelpKinds(SocialPerson person, HelpKind[] into)
        {
            int n = 0;
            if (!Running || person == null || !person.Met || CauseWhy != 0)
            {
                return 0;
            }
            for (int i = 0; i < Offered.Length && n < into.Length; i++)
            {
                if (PriceOf(person, Offered[i], 0, out _, out _))
                {
                    into[n++] = Offered[i];
                }
            }
            return n;
        }

        /// <summary>
        /// The price of an answer to an offer of help, today's whole money, and how many answers the offer has (the months of rent, how much of the debt, the sums a business is backed with,
        /// the cars). False when the person has no use for this kind of help, or the population does not hold them as somebody who can be helped.
        /// </summary>
        public bool PriceOf(SocialPerson person, HelpKind kind, int option, out int amount, out int options)
        {
            amount = options = 0;
            PopulationState s = GivingGame.People();
            int p = s != null && person != null ? GivingPeople.ResidentOf(s, person.Index) : -1;
            int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
            if (h < 0 || !GivingPeople.Helpable(s, h))
            {
                return false;
            }
            int level = GivingGame.Level, day = GivingGame.Day;
            bool close = person.IsFamily || person.Together || person.Stage >= SocialStage.Friend;
            long most = PersonCap(false);
            switch (kind)
            {
                case HelpKind.Rent:
                    if (!GivingHelp.CanRent(s, h))
                    {
                        return false;
                    }
                    options = RentMonths.Length;
                    amount = (int)Math.Min(GivingCatalogue.AmountMost, GivingHelp.RentFor(s, h, RentMonths[option % options], GivingGame.MonthDays));
                    return amount > 0 && amount <= most;
                case HelpKind.Debt:
                    if (!GivingHelp.CanDebt(s, h))
                    {
                        return false;
                    }
                    options = 2;
                    amount = (int)GivingHelp.DebtFor(s, h, most);
                    amount = option % options == 0 ? amount : Math.Max(1, amount / 2);
                    return amount > 0;
                case HelpKind.Business:
                    if (!GivingHelp.CanWork(s, p, day))
                    {
                        return false;
                    }
                    options = BusinessAmounts(level, most, option, out amount);
                    return options > 0 && amount > 0;
                case HelpKind.Course:
                    if (!GivingHelp.CanStudy(s, p, day))
                    {
                        return false;
                    }
                    options = 1;
                    amount = GivingRules.At(GivingCatalogue.CourseCost, level);
                    return amount <= most;
                case HelpKind.Car:
                    if (!close || !GivingHelp.CanCar(s, h) || GivingGame.CarsOffered(_carNames, _carPrices) == 0)
                    {
                        return false;
                    }
                    options = 3;
                    amount = _carPrices[option % options];
                    return amount > 0;
                case HelpKind.Home:
                    if (!close || !GivingHelp.CanHome(s, h))
                    {
                        return false;
                    }
                    options = 1;
                    amount = (int)GivingHelp.HomePrice(s, h);
                    return amount > 0;
                default:
                    return false;
            }
        }

        /// <summary>The name of the car an answer to the offer of a car names.</summary>
        public string CarName(int option)
        {
            GivingGame.CarsOffered(_carNames, _carPrices);
            return _carNames[Math.Abs(option) % 3] ?? string.Empty;
        }

        // The sums a business is backed with: the steps from the least up to the most the player may give a person, ascending. How many there are, and the one an option names.
        private static int BusinessAmounts(int level, long most, int option, out int amount)
        {
            int n = 0;
            amount = 0;
            int least = GivingRules.At(GivingCatalogue.BusinessLeast, level);
            for (int i = 0; i < GivingCatalogue.Steps.Length; i++)
            {
                int step = GivingRules.StepNow(i, level);
                if (step < least || step > most)
                {
                    continue;
                }
                n++;
            }
            if (n == 0)
            {
                return 0;
            }
            int pick = option % n, seen = 0;
            for (int i = 0; i < GivingCatalogue.Steps.Length; i++)
            {
                int step = GivingRules.StepNow(i, level);
                if (step >= least && step <= most && seen++ == pick)
                {
                    amount = step;
                }
            }
            return n;
        }

        // ---- the help itself

        /// <summary>
        /// Helps a person: the answer <paramref name="option"/> of <paramref name="kind"/>. A business can be a gift or a loan (<paramref name="loan"/>, twelve parts and a share on top).
        /// The person answers first; nothing is taken from the player unless they take it. The money lands in their household as the population holds it, and what it changes in them
        /// is in <see cref="GivingHelp"/>. Not in a Going Public session or with the living population off: nothing is taken and the call says why.
        /// </summary>
        public GiveResult GiveHelp(SocialPerson person, HelpKind kind, int option, bool loan)
        {
            if (!Running || person == null || !person.Met)
            {
                return GiveResult.Off;
            }
            PopulationState s = GivingGame.People();
            if (s == null)
            {
                return GoingPublic.InSession ? GiveResult.Session : GiveResult.NoCause;
            }
            int p = GivingPeople.ResidentOf(s, person.Index);
            int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
            if (h < 0 || !GivingPeople.Helpable(s, h))
            {
                return GiveResult.NoPerson;
            }
            if (!PriceOf(person, kind, option, out int amount, out int options))
            {
                return GiveResult.NotNeeded;
            }
            int level = GivingGame.Level, day = GivingGame.Day, atStart = GivingRules.Back(amount, level);
            if (kind == HelpKind.Business && loan)
            {
                return LendBusiness(person, amount, s, p, h);
            }
            bool inNeed = GivingPeople.NeedOf(s, p, out _) != GivingNeed.None;
            PersonGiving g = _state.PersonOf(person.Index, true);
            GiveResult said = Takes(person, kind, false, atStart, inNeed, g);
            if (said != GiveResult.Done)
            {
                return said;
            }
            if (!GivingGame.Pay(amount, KeyOf(kind)))
            {
                return GiveResult.Broke;
            }
            int months = kind == HelpKind.Rent ? RentMonths[option % options] : 0;
            switch (kind)
            {
                case HelpKind.Rent:
                    GivingHelp.PayRent(s, h, amount);
                    g.CoveredUntil = Math.Max(g.CoveredUntil, day + months * GivingGame.MonthDays);
                    break;
                case HelpKind.Debt:
                    GivingHelp.ClearDebt(s, h, amount);
                    break;
                case HelpKind.Business:
                    GivingHelp.BackBusiness(s, p, h, amount, day);
                    break;
                case HelpKind.Course:
                    GivingHelp.Teach(s, p, h);
                    break;
                case HelpKind.Car:
                    GivingHelp.GiveCar(s, h);
                    break;
                default:
                    GivingHelp.GiveHome(s, h);
                    break;
            }
            g.GivenTotal = (int)Math.Min(int.MaxValue / 2, (long)g.GivenTotal + amount);
            GivingBridges.GaveToPerson?.Invoke(amount);
            Warm(person, g, atStart, inNeed, out int affection, out int trust);
            Helped(g, atStart);
            Answered(person);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (" + kind + " of " + amount + " for " + person.Name + "): in their household"
                             + (kind == HelpKind.Rent ? ", their rent theirs again from day " + g.CoveredUntil : string.Empty) + "; affection +" + affection / 10f
                             + ", trust +" + trust / 10f + (inNeed ? string.Empty : " (in no need of it, half counted)") + ", dependence " + GivingRules.Dependence(g, day) + ": PASS");
            }
            return GiveResult.Done;
        }

        private static string KeyOf(HelpKind kind)
        {
            switch (kind)
            {
                case HelpKind.Rent: return GivingGame.RentKey;
                case HelpKind.Debt: return GivingGame.DebtKey;
                case HelpKind.Business: return GivingGame.BusinessKey;
                case HelpKind.Course: return GivingGame.CourseKey;
                case HelpKind.Car: return GivingGame.CarKey;
                case HelpKind.Home: return GivingGame.HomeKey;
                default: return GivingGame.PersonKey;
            }
        }

        // ---- the player says no

        /// <summary>
        /// The player says no to a person's plea. The first no costs nothing: the person understands. A later one costs a little affection. Closes the ask and its call for more. Returns
        /// true when it cost nothing.
        /// </summary>
        public bool Declined(SocialPerson person)
        {
            if (!Running || person == null)
            {
                return true;
            }
            PersonGiving g = _state.PersonOf(person.Index, true);
            int cost = GivingRules.NoCost(g.Nos);
            g.Nos = Math.Min(1000, g.Nos + 1);
            g.MorePending = 0;
            SocialLife.Contacted(person);
            if (cost > 0)
            {
                SocialLife.Change(person, 0f, -cost / 10f, 0f);
            }
            Answered(person);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (the player says no to " + person.Name + "): " + (cost > 0 ? "a little affection lost" : "the first no, nothing lost") + ", nos " + g.Nos + ": PASS");
            }
            return cost == 0;
        }
    }
}
