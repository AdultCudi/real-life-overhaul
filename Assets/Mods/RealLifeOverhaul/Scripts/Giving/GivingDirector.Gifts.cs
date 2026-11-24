using System;
using System.Collections.Generic;
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
        /// A gift to a cause, <paramref name="amount"/> in today's whole money, from the spare cash. It goes in the cause's pot (a district's upkeep is spent at once in the district
        /// <paramref name="tag"/> names), counts for the year's deduction (claimed at once, so a gift on the last day of the tax year is in the game's list before the game reads it) and
        /// for standing. A single gift of <see cref="GivingCatalogue.NamingFrom"/> or more takes the giver's name (a standing gift never does: it is paid each month, and a name is
        /// given once). Its bank line says which kind it is.
        /// </summary>
        public GiveResult GiveToCharity(Charity charity, int amount, int tag = -1, bool standing = false)
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
            string district = charity == Charity.Upkeep ? DistrictOf(tag) : null;
            if (charity == Charity.Upkeep && district == null)
            {
                return GiveResult.NoDistrict;
            }
            int atStart = GivingRules.Back(amount, GivingGame.Level);
            bool naming = !standing && GivingRules.IsNaming(atStart);
            if (!GivingGame.Pay(amount, naming ? GivingGame.NamingKey : standing ? GivingGame.StandingKey : charity == Charity.Upkeep ? GivingGame.UpkeepKey : GivingGame.CharityKey))
            {
                return GiveResult.Broke;
            }
            Roll(GivingGame.Day);
            if (charity != Charity.Upkeep)
            {
                _state.Credit(charity, amount, -1);
            }
            _state.TaxGifts += amount;
            CharityBook b = _state.Book(charity);
            b.PlayerMonth += amount;
            b.PlayerYear += amount;
            b.PlayerTotal += amount;
            _state.MonthGiven += atStart;
            Reward(out int standingPoints, out int press);
            ClaimNow();
            int districtPoints = Upkept(charity, tag, district, atStart, amount);
            bool named = naming && Named(charity, tag, amount);
            if (!named && amount >= GivingRules.At(GivingCatalogue.BigGift, GivingGame.Level))
            {
                _state.Notes.Add(new GivingNote { Day = GivingGame.Day, Charity = (int)charity, Amount = amount, Giver = -1, Tag = district != null ? tag : -1 });
            }
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (a gift of " + amount + " to " + charity + (district != null ? " in " + district : string.Empty) + (named ? ", named" : string.Empty)
                             + "): the pot holds " + b.Pot + ", the year's gifts " + _state.TaxGifts + ", claimed " + _state.TaxClaimed + ", standing +" + standingPoints
                             + " and recognition +" + press / 10f + " from it (" + _state.StandingDone + " point(s) and " + _state.PressDone / 10f + " this month)"
                             + (district != null ? ", " + districtPoints + " point(s) of neighbourhood standing there" : string.Empty) + ": PASS");
            }
            return named ? GiveResult.Named : GiveResult.Done;
        }

        /// <summary>Whether a gift of food in kind can be taken now: giving runs and the city's people are there to be fed (never in a Going Public session).</summary>
        public bool FoodGiftReady => Running && CauseWhy == 0;

        /// <summary>
        /// Food from the player's shops that would have spoiled, given to the food bank in kind. <paramref name="given"/> is what all of it cost (whole money), put in the
        /// food bank's pot for the city's households; <paramref name="earned"/> is the part of that cost that an ordinary shop's leftovers earn (Economy/Logistics,
        /// FoodGiftRules): only that counts as the player's giving, and is deducted from tax within the same cap as any gift. No cash moves, so the gift is always a loss: the food
        /// was paid for already and a deduction saves only a share of its cost. It earns no integrity here: the shops' standing, staff and mood come from the gift's own rules.
        /// Returns whether it was taken.
        /// </summary>
        public bool GiveFood(long given, long earned)
        {
            if (!FoodGiftReady || given <= 0)
            {
                return false;
            }
            earned = Math.Max(0, Math.Min(earned, given));
            Roll(GivingGame.Day);
            _state.Credit(Charity.FoodBank, given, -1);
            if (earned > 0)
            {
                _state.TaxGifts += earned;
                _state.FoodGifts += earned;
                CharityBook b = _state.Book(Charity.FoodBank);
                b.PlayerMonth += earned;
                b.PlayerYear += earned;
                b.PlayerTotal += earned;
                ClaimNow();
            }
            _revision++;
            if (Verbose)
            {
                Logger?.Info("Giving check (food in kind, " + given + " at cost, " + earned + " earning): the pot holds " + _state.Book(Charity.FoodBank).Pot
                             + ", the year's gifts " + _state.TaxGifts + ": PASS");
            }
            return true;
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

        // A gift to a district's upkeep is spent at once there: neighbourhood standing in the district and more custom in its shops for some days (Standing's own levers), by the sum.
        // Returns the points of standing Standing really applied.
        private int Upkept(Charity charity, int tag, string district, int atStart, int amount)
        {
            if (charity != Charity.Upkeep || district == null)
            {
                return 0;
            }
            _state.UpkeepTotal[tag] = Math.Min(long.MaxValue / 4, _state.UpkeepTotal[tag] + amount);
            int days = GivingRules.UpkeepDays(atStart);
            return GivingBridges.Upkeep != null ? Math.Max(0, GivingBridges.Upkeep(district, GivingRules.UpkeepPoints(atStart), days > 0 ? GivingCatalogue.UpkeepBoost : 0, days)) : 0;
        }

        // A gift of the naming size: a hall, a wing or a scholarship takes the giver's name. It is kept for the page, leads the City Ledger the morning after (and Flare's posts of the
        // paper's headlines follow it), and earns once a little integrity and the press's attention, through Standing's own figures.
        private bool Named(Charity charity, int tag, long amount)
        {
            if (_state.Namings.Count >= GivingCatalogue.NamingsKept)
            {
                _state.Namings.RemoveAt(0);
            }
            var naming = new GivingNaming { Day = GivingGame.Day, Charity = (int)charity, Amount = amount, Tag = charity == Charity.Upkeep ? tag : -1, Name = GivingGame.PlayerName };
            _state.Namings.Add(naming);
            LastNaming = GivingText.Naming(naming);
            _state.Notes.Add(new GivingNote { Day = GivingGame.Day, Charity = (int)charity, Amount = amount, Giver = -1, Naming = 1, Tag = charity == Charity.Upkeep ? tag : -1 });
            int integrity = GivingBridges.Integrity != null ? GivingBridges.Integrity(GivingCatalogue.NamingIntegrity) : 0;
            int attention = GivingBridges.Press != null ? GivingBridges.Press(GivingCatalogue.NamingPress) : 0;
            if (Verbose)
            {
                Logger?.Info("Giving check (a naming gift of " + amount + " to " + charity + "): a name for the page and the paper, integrity +" + integrity
                             + ", recognition +" + attention / 10f + ": PASS");
            }
            return true;
        }

        /// <summary>What the last gift that took the player's name is called, for the notice that says so.</summary>
        public string LastNaming { get; private set; } = string.Empty;

        /// <summary>The district a number names, as the game writes it, or null when this city has none by that number.</summary>
        private string DistrictOf(int tag)
        {
            GivingGame.Districts(_districts);
            return tag >= 0 && tag < _districts.Count ? _districts[tag] : null;
        }

        private readonly List<string> _districts = new List<string>(GivingState.Districts);

        /// <summary>A gift every month to a charity, the step's index in the list; replaces the one it had. False when the list is full.</summary>
        public bool SetStanding(Charity charity, int step, int tag = -1)
        {
            if (!Running)
            {
                return false;
            }
            if (charity == Charity.Upkeep && DistrictOf(tag) == null)
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
            g.Tag = charity == Charity.Upkeep ? tag : -1;
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

        // ---- the care fund

        /// <summary>
        /// The care fund pays part of a household's own share of a care bill, before it reaches the household's savings and debt: <see cref="GivingCatalogue.CareShare"/> of it, never more than
        /// the fund holds or may pay out this month (its cap for each household of the city). Health's people's pass asks as each bill falls on a household, so a case is helped as it comes.
        /// Returns what the fund paid, whole money at today's prices. Nothing while giving does not run or the city's people are not there (a session): the pot waits.
        /// </summary>
        public long CareCover(long own, int level)
        {
            if (!Running || own <= 0 || CauseWhy != 0)
            {
                return 0L;
            }
            PopulationState s = GivingGame.People();
            CharityBook b = _state.Book(Charity.Care);
            if (s == null || b.Pot <= 0)
            {
                return 0L;
            }
            long pay = GivingRules.CareCover(own, b.Pot, GivingRules.MonthCap(Charity.Care, s.Households.Live, level), b.PaidMonth);
            if (pay <= 0)
            {
                return 0L;
            }
            b.Pot -= pay;
            b.PaidMonth += pay;
            b.PaidYear += pay;
            b.PeopleYear++;
            b.MonthPeople++;
            return pay;
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
            if (amount > PersonCap(false))
            {
                return GiveResult.Limit;
            }
            int p = GivingPeople.ResidentOf(s, person.Index);
            int h = p >= 0 ? GivingPeople.HouseholdOf(s, p) : -1;
            bool landed = h >= 0 && GivingPeople.Helpable(s, h);
            PersonGiving g = _state.PersonOf(person.Index, true);
            // An ask for more, with no real need, earns what a gift to somebody in no need does.
            bool inNeed = (asked && g.MorePending == 0) || (landed && GivingPeople.NeedOf(s, p, out _) != GivingNeed.None);
            GiveResult said = asked ? GiveResult.Done : Takes(person, HelpKind.Money, false, GivingRules.Back(amount, level), inNeed, g);
            if (said != GiveResult.Done)
            {
                return said;   // they would not take it: nothing is paid
            }
            if (!GivingGame.Pay(amount, GivingGame.PersonKey))
            {
                return GiveResult.Broke;
            }
            if (landed)
            {
                GivingPeople.Help(s, h, amount, out _, out _);
            }
            g.GivenTotal = (int)Math.Min(int.MaxValue / 2, (long)g.GivenTotal + amount);
            GivingBridges.GaveToPerson?.Invoke(amount);
            Warm(person, g, GivingRules.Back(amount, level), inNeed, out int affection, out int trust);
            Helped(g, GivingRules.Back(amount, level));
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

        /// <summary>The most a gift or a loan may be to a person now, in today's whole money: the cap, which doubles for each wealth step the player has passed, and the spare cash.</summary>
        public static int MostFor(bool loan) => (int)Math.Min(PersonCap(loan), GivingGame.Spare);

        /// <summary>The most a gift or a loan to a person may be at today's prices, whatever the cash: the start's most doubled for each wealth step the player has passed.</summary>
        public static int PersonCap(bool loan)
        {
            int tier = GivingBridges.WealthTier != null ? GivingBridges.WealthTier() : 0;
            return GivingRules.At(GivingRules.PersonMost(loan ? GivingCatalogue.LoanMost : GivingCatalogue.GiftsMost, tier), GivingGame.Level);
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
