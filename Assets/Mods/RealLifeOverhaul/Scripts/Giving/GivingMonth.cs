using System;

namespace RealLifeOverhaul.Giving
{
    /// <summary>What a month's pass asks of the game, one thing at a time; the director answers it, a check answers it with a stand-in.</summary>
    public interface IMonthWorld
    {
        int Day { get; }

        int Level { get; }

        /// <summary>The standing gift's turn: give it as a gift of the player's, or skip it and say so once when it cannot be paid.</summary>
        void PayStanding(StandingGift gift, int month);

        /// <summary>A tycoon's worth, or under nothing while they are not forward.</summary>
        long RivalWorth(int tycoon);

        /// <summary>A tycoon gives so much: it leaves their worth, down to the floor their worth cannot fall under. Returns what really left them.</summary>
        long RivalPays(int tycoon, long gift);

        /// <summary>A charity pays out what it holds under the month's cap, and notes the month on its book.</summary>
        void PayOut(Charity charity, int month);

        /// <summary>The people of the city help one another.</summary>
        void Neighbours(int month);
    }

    /// <summary>
    /// The month's pass, with no game in it: its steps, and what makes each safe to run again. The pass is done when its last step is, and the step it has reached is kept in the
    /// save (<see cref="GivingState.MonthStep"/>), so a save, a load or the option switched off in the middle goes on at the step it was at. A step that could run twice
    /// (a load can put the state back to before it) says what it has done on its own record: a standing gift its month, a rival its month, a charity the month it paid out in,
    /// the neighbours theirs. So a standing gift is never paid twice in a month, a rival never gives twice and a pot never pays out twice. COST: a pass over a few records a step.
    /// </summary>
    public static class GivingMonth
    {
        public const int CloseStep = 0;       // the counters start again
        public const int StandingStep = 1;    // the standing gifts
        public const int RivalStep = 2;       // the rivals' gifts
        public const int FirstPayStep = 3;    // the charities' pay-outs, one a step, in the charities' order
        public const int NeighbourStep = FirstPayStep + (int)Charity.Count;
        public const int Steps = NeighbourStep + 1;

        /// <summary>Starts the pass for a month if it is due: no pass is under way and the month is past the last done. False for the very first look, which only notes the month.</summary>
        public static bool Begin(GivingState s, int month)
        {
            if (s.Month < 0)
            {
                s.Month = month;
                s.StandingMonth = month;
                return false;
            }
            if (s.MonthStep >= 0 || month <= s.Month)
            {
                return false;
            }
            s.MonthNumber = month;
            s.MonthStep = 0;
            return true;
        }

        /// <summary>Runs the step the pass is at and moves on; the pass is done, and the month marked, when its last step is.</summary>
        public static void Step(GivingState s, IMonthWorld w)
        {
            int month = s.MonthNumber;
            switch (s.MonthStep)
            {
                case CloseStep:
                    Close(s);
                    break;
                case StandingStep:
                    for (int i = 0; i < s.Standing.Count; i++)
                    {
                        StandingGift g = s.Standing[i];
                        if (g.Month == month)
                        {
                            continue;
                        }
                        g.Month = month;   // its turn is taken before the money moves
                        w.PayStanding(g, month);
                    }
                    break;
                case RivalStep:
                    Rivals(s, w, month);
                    break;
                case NeighbourStep:
                    if (s.NeighbourMonth != month)
                    {
                        w.Neighbours(month);
                    }
                    break;
                default:
                    if (s.MonthStep >= FirstPayStep && s.MonthStep < NeighbourStep)
                    {
                        var charity = (Charity)(s.MonthStep - FirstPayStep);
                        if (s.Book(charity).LastMonth != month)
                        {
                            w.PayOut(charity, month);
                        }
                    }
                    break;
            }
            s.MonthStep++;
            if (s.MonthStep >= Steps)
            {
                s.MonthStep = -1;
                s.Month = Math.Max(s.Month, month);
                s.StandingMonth = s.Month;
            }
        }

        /// <summary>The month's counters start again.</summary>
        public static void Close(GivingState s)
        {
            s.MonthGiven = 0;
            s.StandingDone = 0;
            s.PressDone = 0;
            for (int i = 0; i < s.Books.Length; i++)
            {
                CharityBook b = s.Books[i];
                b.PlayerMonth = 0;
                b.OthersMonth = 0;
                b.PaidMonth = 0;
            }
        }

        // One gift a rival a month, from worth above the floor their worth grows back to; what really left them goes in the pot.
        private static void Rivals(GivingState s, IMonthWorld w, int month)
        {
            for (int i = 0; i < s.RivalBooks.Length; i++)
            {
                RivalGiving r = s.RivalBooks[i];
                long worth = w.RivalWorth(i);
                if (worth <= 0 || r.Month == month)
                {
                    continue;
                }
                long gift = GivingRules.RivalGift(s.Seed, i, worth, 12, out int _);
                int charity = GivingRules.RivalCharity(s.Seed, i, month);
                r.Month = month;
                if (gift <= 0)
                {
                    continue;
                }
                long paid = w.RivalPays(i, gift);
                if (paid <= 0)
                {
                    continue;
                }
                s.Credit((Charity)charity, paid, i);
                r.Total += paid;
                r.Year += paid;
                r.Last = paid;
                r.LastDay = w.Day;
                r.LastCharity = charity;
                if (paid >= GivingRules.At(GivingCatalogue.BigGift, w.Level))
                {
                    s.Notes.Add(new GivingNote { Day = w.Day, Charity = charity, Amount = paid, Giver = i });
                }
            }
        }
    }
}
