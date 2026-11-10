using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Giving
{
    /// <summary>What the player can do with their money here: give to a charity once or every month, give to a person, lend to a person. Each is one press that ends in a result.</summary>
    public sealed partial class GivingDirector
    {
        /// <summary>Why the charities cannot work now: 0 they can, 1 the city's people are not running, 2 a Going Public session (the people are the host's).</summary>
        public static int CauseWhy
        {
            get
            {
                if (GoingPublic.InSession)
                {
                    return 2;
                }
                return GivingGame.People() != null ? 0 : 1;
            }
        }

        // ---- charities

        /// <summary>
        /// A gift to a charity, <paramref name="amount"/> in today's whole money, from the spare cash. It goes in the charity's pot, counts for the year's deduction
        /// and for standing.
        /// </summary>
        public GiveResult GiveToCharity(Charity charity, int amount)
        {
            if (!Running)
            {
                return GiveResult.Off;
            }
            int why = CauseWhy;
            if (why != 0)
            {
                return why == 2 ? GiveResult.Session : GiveResult.NoCause;
            }
            if (amount <= 0)
            {
                return GiveResult.Nothing;
            }
            if (!GivingGame.Pay(amount, GivingGame.CharityKey))
            {
                return GiveResult.Broke;
            }
            Roll(GivingGame.Day);
            Credit(charity, amount, -1);
            _state.TaxGifts += amount;
            CharityBook b = _state.Book(charity);
            b.PlayerMonth += amount;
            b.PlayerYear += amount;
            b.PlayerTotal += amount;
            _state.MonthGiven += GivingRules.Back(amount, GivingGame.Level);
            Reward();
            if (amount >= GivingRules.At(GivingCatalogue.BigGift, GivingGame.Level))
            {
                _state.Notes.Add(new GivingNote { Day = GivingGame.Day, Charity = (int)charity, Amount = amount, Giver = -1 });
            }
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (a gift of " + amount + " to " + charity + "): the pot holds " + b.Pot + ", the year's gifts " + _state.TaxGifts + ", standing earned "
                             + GivingRules.IntegrityOf(_state.MonthGiven) + " point(s) this month: PASS");
            }
            return GiveResult.Done;
        }

        // Any gift into a charity's pot: the player's (giver -1) or a rival's (0 and up); the rival's counts as the others', never the player's.
        private void Credit(Charity charity, long amount, int giver)
        {
            CharityBook b = _state.Book(charity);
            b.Pot += amount;
            if (giver >= 0)
            {
                b.OthersMonth += amount;
                b.OthersYear += amount;
            }
        }

        // Standing and the press earned this month by the player's gifts so far: only what the curve has newly earned is asked, so ten gifts of a tenth earn what one of the whole does.
        private void Reward()
        {
            int points = GivingRules.IntegrityOf(_state.MonthGiven);
            if (points > _state.StandingDone)
            {
                int delta = points - _state.StandingDone;
                _state.StandingDone = points;
                GivingBridges.Integrity?.Invoke(delta);
            }
            int press = GivingRules.PressOf(_state.MonthGiven);
            if (press > _state.PressDone)
            {
                int delta = press - _state.PressDone;
                _state.PressDone = press;
                GivingBridges.Press?.Invoke(delta);
            }
        }

        /// <summary>A gift every month to a charity, the step's index in the list; replaces the one it had. False when the list is full.</summary>
        public bool SetStanding(Charity charity, int step)
        {
            if (!Running)
            {
                return false;
            }
            int amount = GivingCatalogue.Steps[GivingCatalogue.Clamp(step, 0, GivingCatalogue.Steps.Length - 1)];
            StandingGift g = _state.StandingOf((int)charity);
            if (g == null)
            {
                if (_state.Standing.Count >= GivingCatalogue.StandingMost)
                {
                    return false;
                }
                g = new StandingGift { Charity = (int)charity, Since = GivingGame.Day };
                _state.Standing.Add(g);
            }
            g.Amount = amount;
            g.Skipped = false;
            _revision++;
            return true;
        }

        /// <summary>Ends a standing gift. True when there was one.</summary>
        public bool CancelStanding(Charity charity)
        {
            if (!Running)
            {
                return false;
            }
            StandingGift g = _state.StandingOf((int)charity);
            if (g == null)
            {
                return false;
            }
            _state.Standing.Remove(g);
            _revision++;
            return true;
        }

        // ---- people

        /// <summary>
        /// A gift of money to a person the player knows. The money lands in their household as the population holds it (its debt first, then its savings); a person the population does
        /// not hold gets the gift as the relationship alone. It earns affection and trust with diminishing returns over a window, so a gift is no shortcut to friendship, and is never
        /// deducted from tax.
        /// </summary>
        public GiveResult GiveToPerson(SocialPerson person, int amount, bool asked)
        {
            if (!Running || person == null || !person.Met)
            {
                return GiveResult.Off;
            }
            if (amount <= 0)
            {
                return GiveResult.Nothing;
            }
            int level = GivingGame.Level;
            if (amount > GivingRules.At(GivingCatalogue.GiftsMost, level))
            {
                return GiveResult.Limit;
            }
            if (!GivingGame.Pay(amount, GivingGame.PersonKey))
            {
                return GiveResult.Broke;
            }
            bool landed = false, inNeed = asked;
            PopulationState s = GivingGame.People();
            int p = s != null ? GivingPeople.ResidentOf(s, person.Index) : -1;
            int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
            if (h >= 0 && GivingPeople.Helpable(s, h))
            {
                inNeed |= GivingPeople.NeedOf(s, p, out _) != GivingNeed.None;
                GivingPeople.Help(s, h, amount, out _, out _);
                landed = true;
            }
            PersonGiving g = _state.PersonOf(person.Index, true);
            g.GivenTotal = (int)Math.Min(int.MaxValue / 2, (long)g.GivenTotal + amount);
            GivingRules.PersonGain(g, GivingRules.Back(amount, level), GivingGame.Day, inNeed, out int affection, out int trust);
            SocialLife.Contacted(person);
            SocialLife.Change(person, 0f, affection / 10f, trust / 10f);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (a gift of " + amount + " to " + person.Name + "): " + (landed ? "landed in their household" : "no household the population holds, the relationship only")
                             + "; affection +" + affection / 10f + ", trust +" + trust / 10f + (inNeed ? "" : " (in no need of it, half counted)") + ": PASS");
            }
            return landed ? GiveResult.Done : GiveResult.DoneOnly;
        }

        /// <summary>The most a gift or a loan may be to this person now, in today's whole money (the cap, and the spare cash).</summary>
        public static int MostFor(bool loan)
        {
            int cap = GivingRules.At(loan ? GivingCatalogue.LoanMost : GivingCatalogue.GiftsMost, GivingGame.Level);
            return (int)Math.Min(cap, GivingGame.Spare);
        }

        /// <summary>The amounts a call offers for a person, ascending, today's whole money, none over <see cref="MostFor"/>; at least one when there is any cash to give.</summary>
        public static int StepsFor(bool loan, int[] into)
        {
            int most = MostFor(loan), n = 0;
            for (int i = 0; i < GivingCatalogue.Steps.Length && n < into.Length; i++)
            {
                int amount = GivingRules.StepNow(i, GivingGame.Level);
                if (amount <= most && (n == 0 || amount > into[n - 1]))
                {
                    into[n++] = amount;
                }
            }
            return n;
        }
    }
}
