using System;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Giving
{
    /// <summary>What the pages read: the state and the game's figures gathered into <see cref="GiveFacts"/> when a page is written (a press or a changed revision), never per frame.</summary>
    public sealed partial class GivingDirector
    {
        /// <summary>Fills the facts for a page; false, with Ready false, while giving does not run.</summary>
        public bool Facts(GiveFacts f)
        {
            f.Loans.Clear();
            f.People.Clear();
            f.Namings.Clear();
            if (!Running)
            {
                f.Ready = false;
                return false;
            }
            f.Ready = true;
            f.Why = CauseWhy;
            f.Cash = GivingGame.Cash;
            f.Spare = GivingGame.Spare;
            int level = f.Level = GivingGame.Level;
            f.PersonMost = PersonCap(false);
            PopulationState s = GivingGame.People();
            int households = s != null ? s.Households.Live : 0;
            if (f.Charities.Length != (int)Charity.Count)
            {
                f.Charities = new GiveCharityFact[(int)Charity.Count];
            }
            for (int i = 0; i < f.Charities.Length; i++)
            {
                CharityBook b = _state.Books[i];
                StandingGift g = _state.StandingOf(i);
                GiveCharityFact c = f.Charities[i] ?? (f.Charities[i] = new GiveCharityFact());
                c.Id = i;
                c.Pot = b.Pot;
                c.Cap = GivingRules.MonthCap((Charity)i, households, level);
                c.PaidMonth = b.PaidMonth;
                c.PlayerMonth = b.PlayerMonth;
                c.PlayerYear = b.PlayerYear;
                c.OthersYear = b.OthersYear;
                c.PaidYear = b.PaidYear;
                c.LastMoney = b.LastMoney;
                c.LastPeople = b.LastPeople;
                c.PeopleYear = b.PeopleYear;
                c.PlayerTotal = b.PlayerTotal;
                c.Standing = g != null ? GivingRules.At(g.Amount, level) : 0;
                c.StandingSkipped = g != null && g.Skipped;
                c.StandingTag = g != null ? g.Tag : -1;
            }
            GivingGame.Districts(_districts);
            if (f.Districts.Length != _districts.Count)
            {
                f.Districts = new string[_districts.Count];
                f.DistrictTotals = new long[_districts.Count];
            }
            for (int i = 0; i < _districts.Count; i++)
            {
                f.Districts[i] = GivingGame.DistrictName(_districts[i]);
                f.DistrictTotals[i] = _state.UpkeepTotal[i];
            }
            for (int i = 0; i < _state.Loans.Count; i++)
            {
                LoanOut l = _state.Loans[i];
                f.Loans.Add(new GiveLoanFact
                {
                    Name = l.Name, Principal = l.Principal, Remaining = l.Remaining, Paid = l.Paid, Part = l.Part, Made = l.Made, Next = l.Next, Closed = l.Closed, State = l.State, Misses = l.Misses,
                    Parts = l.Parts > 0 ? l.Parts : GivingCatalogue.LoanParts, Extra = l.Extra
                });
            }
            PeopleFacts(f, s);
            for (int i = _state.Namings.Count - 1; i >= 0; i--)
            {
                GivingNaming n = _state.Namings[i];
                f.Namings.Add(new GiveNamingFact { Words = GivingText.Naming(n), Amount = n.Amount, Day = n.Day });
            }
            f.Carried = GivingYear.CarryLeft(_state);
            f.TaxGifts = _state.TaxGifts;
            f.FoodGifts = _state.FoodGifts;
            f.TaxClaimed = GivingYear.Claimed(_state, GivingGame.Deducted());   // what the game's tax will deduct under giving's name
            f.Income = YearIncome(GivingGame.Day);
            f.TaxCap = GivingRules.DeductionCap(f.Income);
            f.NeighbourYear = _state.NeighbourYear;
            f.NeighbourHelped = _state.NeighbourHelped;
            long rivals = 0, best = 0;
            int bestRival = -1;
            for (int i = 0; i < _state.RivalBooks.Length; i++)
            {
                rivals += _state.RivalBooks[i].Year;
                if (_state.RivalBooks[i].Year > best)
                {
                    best = _state.RivalBooks[i].Year;
                    bestRival = i;
                }
            }
            f.RivalsYear = rivals;
            f.RivalLine = bestRival >= 0 ? GivingText.RivalLine(bestRival, _state.RivalBooks[bestRival]) : string.Empty;
            f.Month = Math.Max(0, _state.Month);
            return true;
        }

        // The people the player has given to, lent to, helped or said no to, the most given first: what they are short of now, how they lean on the player's help, what the rent paid for them
        // still covers.
        private void PeopleFacts(GiveFacts f, PopulationState s)
        {
            int day = GivingGame.Day;
            for (int i = 0; i < _state.People.Count; i++)
            {
                PersonGiving g = _state.People[i];
                SocialPerson person = SocialState.Get(g.Person);
                LoanOut loan = _state.LoanOf(g.Person);
                if (person == null || !person.Met || (g.GivenTotal <= 0 && g.Helps <= 0 && g.Nos <= 0 && loan == null))
                {
                    continue;
                }
                f.People.Add(new GivePersonFact
                {
                    Name = person.Name, Need = NeedOfPerson(person), Given = g.GivenTotal, Loan = loan != null ? loan.Remaining : 0, Dependence = GivingRules.Dependence(g, day),
                    CoveredDays = Math.Max(0, g.CoveredUntil - day), Cooling = GivingRules.Cooling(g, day), Proud = g.RefusedDay > 0 && g.Helps <= 0, Nos = g.Nos
                });
            }
            f.People.Sort((a, b) => b.Given.CompareTo(a.Given));
        }

        /// <summary>How things stand between the player's money and one person, for their page: false while giving does not run. A person with no record has everything at nothing.</summary>
        public bool MoneyOf(SocialPerson person, GivePersonFact into)
        {
            if (!Running || person == null)
            {
                return false;
            }
            int day = GivingGame.Day;
            PersonGiving g = _state.PersonOf(person.Index, false);
            LoanOut loan = _state.LoanOf(person.Index);
            into.Name = person.Name;
            into.Need = NeedOfPerson(person);
            into.Given = g != null ? g.GivenTotal : 0;
            into.Loan = loan != null ? loan.Remaining : 0;
            into.Dependence = GivingRules.Dependence(g, day);
            into.CoveredDays = g != null ? Math.Max(0, g.CoveredUntil - day) : 0;
            into.Cooling = GivingRules.Cooling(g, day);
            into.Proud = g != null && g.RefusedDay > 0 && g.Helps <= 0;
            into.Nos = g != null ? g.Nos : 0;
            return true;
        }

        /// <summary>The line the rich list's panel for a rival adds: what they have given this year and to whom; empty when nothing yet.</summary>
        public string RivalGivingLine(int tycoon)
        {
            if (!Running || tycoon < 0 || tycoon >= _state.RivalBooks.Length || _state.RivalBooks[tycoon].Total <= 0)
            {
                return string.Empty;
            }
            return GivingText.RivalLine(tycoon, _state.RivalBooks[tycoon]);
        }

        /// <summary>The morning's pieces for the City Ledger: the big gifts of the last few days, the player's and the rivals', each once.</summary>
        public void Stories(int day, System.Collections.Generic.List<GivingStory> into)
        {
            into.Clear();
            if (!Running)
            {
                return;
            }
            for (int i = 0; i < _state.Notes.Count; i++)
            {
                GivingNote n = _state.Notes[i];
                if (n.Day != day - 1)
                {
                    continue;   // a gift is printed the morning after it: nothing twice
                }
                bool lead = n.Naming == 1 || n.Amount >= GivingRules.At(GivingCatalogue.FrontPageGift, GivingGame.Level);   // a name on a hall leads the paper, and so is Flare's first headline
                into.Add(GivingText.Story(n, lead));
            }
        }
    }
}
