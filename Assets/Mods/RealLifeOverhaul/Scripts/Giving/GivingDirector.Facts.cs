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
            }
            for (int i = 0; i < _state.Loans.Count; i++)
            {
                LoanOut l = _state.Loans[i];
                f.Loans.Add(new GiveLoanFact
                {
                    Name = l.Name, Principal = l.Principal, Remaining = l.Remaining, Paid = l.Paid, Part = l.Part, Made = l.Made, Next = l.Next, Closed = l.Closed, State = l.State, Misses = l.Misses
                });
            }
            f.TaxGifts = _state.TaxGifts;
            f.TaxClaimed = GivingGame.Deducted();
            int day = GivingGame.Day;
            f.Income = GivingBridges.Income != null && day - 1 >= _state.PeriodStart ? GivingBridges.Income(_state.PeriodStart, day - 1) : 0L;
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
                bool lead = n.Amount >= GivingRules.At(GivingCatalogue.FrontPageGift, GivingGame.Level);
                into.Add(GivingText.Story(n, lead));
            }
        }
    }
}
