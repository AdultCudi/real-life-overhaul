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
        /// A gift to a charity, <paramref name="amount"/> in today's whole money, from the spare cash. It goes in the charity's pot, counts for the year's deduction (claimed
        /// at once, so a gift on the last day of the tax year is in the game's list before the game reads it) and for standing.
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
            _state.Credit(charity, amount, -1);
            _state.TaxGifts += amount;
            CharityBook b = _state.Book(charity);
            b.PlayerMonth += amount;
            b.PlayerYear += amount;
            b.PlayerTotal += amount;
            _state.MonthGiven += GivingRules.Back(amount, GivingGame.Level);
            Reward(out int standing, out int press);
            ClaimNow();
            if (amount >= GivingRules.At(GivingCatalogue.BigGift, GivingGame.Level))
            {
                _state.Notes.Add(new GivingNote { Day = GivingGame.Day, Charity = (int)charity, Amount = amount, Giver = -1 });
            }
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (a gift of " + amount + " to " + charity + "): the pot holds " + b.Pot + ", the year's gifts " + _state.TaxGifts + ", claimed " + _state.TaxClaimed
                             + ", standing +" + standing + " and recognition +" + press / 10f + " from it (" + _state.StandingDone + " point(s) and " + _state.PressDone / 10f + " this month): PASS");
            }
            return GiveResult.Done;
        }

        // Standing and the press earned this month by the player's gifts so far: only what the curve has newly earned is asked, so ten gifts of a tenth earn what one of the whole does.
        // What Fame really applied is what is counted as earned (it holds a part to a most a day, and recognition to its own top), so a part it left over is asked again by the next gift.
        private void Reward(out int standing, out int press)
        {
            standing = press = 0;
            int points = GivingRules.IntegrityOf(_state.MonthGiven);
            if (points > _state.StandingDone && GivingBridges.Integrity != null)
            {
                standing = Math.Max(0, GivingBridges.Integrity(points - _state.StandingDone));
                _state.StandingDone += standing;
            }
            int tenths = GivingRules.PressOf(_state.MonthGiven);
            if (tenths > _state.PressDone && GivingBridges.Press != null)
            {
                press = Math.Max(0, GivingBridges.Press(tenths - _state.PressDone));
                _state.PressDone += press;
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
                g = new StandingGift { Charity = (int)charity, Since = GivingGame.Day, Month = GivingRules.MonthOf(GivingGame.Day, GivingGame.MonthDays) };   // its first turn is the next month's
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
        /// A gift of money to a person the player knows. The money lands in their household as the population holds it (its debt first, then its savings). Where there are no
        /// people for it to reach (a Going Public session, or the living population off) nothing is paid at all. A person the population runs but does not hold as a household that can
        /// be helped (not yet linked, the player's own household, a tenant) gets the gift as the relationship alone, and the call says so. It earns affection and trust with
        /// diminishing returns over a window, so a gift is no shortcut to friendship, and is never deducted from tax. A gift to somebody who is asking answers the ask.
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
            PopulationState s = GivingGame.People();
            if (s == null)
            {
                return GoingPublic.InSession ? GiveResult.Session : GiveResult.NoCause;   // no people to receive it: no money is taken
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
            int p = GivingPeople.ResidentOf(s, person.Index);
            int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
            if (h >= 0 && GivingPeople.Helpable(s, h))
            {
                inNeed |= GivingPeople.NeedOf(s, p, out _) != GivingNeed.None;
                GivingPeople.Help(s, h, amount, out _, out _);
                landed = true;
            }
            PersonGiving g = _state.PersonOf(person.Index, true);
            g.GivenTotal = (int)Math.Min(int.MaxValue / 2, (long)g.GivenTotal + amount);
            GivingBridges.GaveToPerson?.Invoke(amount);
            GivingRules.PersonGain(g, GivingRules.Back(amount, level), GivingGame.Day, inNeed, out int affection, out int trust);
            SocialLife.Contacted(person);
            SocialLife.Change(person, 0f, affection / 10f, trust / 10f);
            Answered(person);
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (a gift of " + amount + " to " + person.Name + "): " + (landed ? "landed in their household" : "no household the population holds, the relationship only")
                             + "; affection +" + affection / 10f + ", trust +" + trust / 10f + (inNeed ? "" : " (in no need of it, half counted)") + ": PASS");
            }
            return landed ? GiveResult.Done : GiveResult.DoneOnly;
        }

        // Money given or lent to somebody who is asking is the answer to the ask: it is closed, so it is never left to run out and cost trust for a favour that was done.
        private static void Answered(SocialPerson person)
        {
            SocialOffer offer = SocialState.Offer;
            if (offer.Active && offer.Kind == SocialOffer.Help && offer.Person == person.Index)
            {
                offer.Clear();
            }
        }

        /// <summary>
        /// A bequest from an estate (Scripts/Dynasty) into a charity's pot, whole money; the estate books the money itself. It is the estate's gift, exempt from estate tax, so it
        /// is no deduction from the income tax, earns the heir no standing and is not counted as the player's giving. Returns what went in: nothing while giving does not run or
        /// the charities cannot work (a session, the city's people not running).
        /// </summary>
        public long Bequest(Charity charity, long amount)
        {
            if (!Running || CauseWhy != 0 || amount <= 0)
            {
                return 0L;
            }
            _state.Credit(charity, amount, 0);   // counted with the others' gifts, never the player's
            _revision++;
            return amount;
        }

        /// <summary>
        /// Somebody the player lent to has become the played head of the family (Scripts/Dynasty): nobody owes themselves, so their open loan is closed as it stands, and their
        /// record of gifts goes with them. Nothing is booked.
        /// </summary>
        public void Merged(int person)
        {
            if (!_loaded || _state == null)
            {
                return;
            }
            LoanOut loan = _state.LoanOf(person);
            if (loan != null)
            {
                loan.State = LoanState.Repaid;
                loan.Remaining = 0;
                loan.Closed = GivingGame.Day;
            }
            for (int i = _state.People.Count - 1; i >= 0; i--)
            {
                if (_state.People[i].Person == person)
                {
                    _state.People.RemoveAt(i);
                }
            }
            _revision++;
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
