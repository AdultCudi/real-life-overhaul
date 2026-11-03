using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What asking a bank for a lower rate comes to now.</summary>
    public enum LowerRateAnswer : byte
    {
        Yes = 0,
        Rating = 1,     // the rating is under the line a bank takes a point off from
        Wait = 2,       // asked at a bank too lately
        Floor = 3,      // the rate is at the least a bank lends at
        Given = 4,      // agreed already in this talk
        Off = 5         // the rating is not running (the economy is off)
    }

    /// <summary>A loan's figures for one term and rate, as the game's loan would run them.</summary>
    public struct LoanFigures
    {
        public int Interest;     // a day, on the whole amount, every day until it is paid off (the game's simple interest)
        public int Principal;    // of the amount a day
        public int Days;         // until it is paid off at that
        public long Total;       // the amount and every day's interest
        public int PerDay => Interest + Principal;
    }

    /// <summary>
    /// The bank's loan talk past what the game's form holds: the term in years (the bank's own, a shorter and a longer),
    /// the payment and the whole sum to repay for it, worked out by the game's own formulas (LoanHelper, GameLoanMath), and
    /// a point off the rate for a good rating, asked once in a while. The game keeps a loan's daily interest and daily
    /// payment, not its term or rate, so a term is the daily payment it sets. Game-free; checked offline (EconomyCheck).
    /// </summary>
    public static class LoanTalkRules
    {
        public const int TermsMost = 3;
        public const int LowerRateFrom = 700;        // the rating from which a bank takes a point off its loan rate when asked
        public const int LowerRateEveryDays = 30;    // game days from one ask at any bank to the next
        public const int LowerRatePoints = 1;        // per cent a year off
        public const int LeastDailyPayment = 5;      // the game's own (LoanHelper.MinimumDailyPayment)

        /// <summary>The terms offered, shortest first: two years under the bank's own, its own, a year over it. Returns how many.</summary>
        public static int Terms(int bankYears, int[] into)
        {
            int own = Math.Max(1, bankYears), n = 0;
            int shorter = Math.Max(1, own - 2);
            if (shorter < own)
            {
                into[n++] = shorter;
            }
            into[n++] = own;
            into[n++] = own + 1;
            return n;
        }

        /// <summary>The term's index the talk starts on: the bank's own.</summary>
        public static int OwnTerm(int bankYears, int[] terms, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (terms[i] == Math.Max(1, bankYears))
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>LoanHelper.CalculateDailyPayment for a term in years: the amount over the term's days, never under the game's least.</summary>
        public static int DailyPayment(float amount, int years, int daysPerYear)
        {
            if (!(amount > 0f))
            {
                return 0;
            }
            int b = (int)Math.Floor(amount / (float)(Math.Max(1, daysPerYear) * Math.Max(1, years)));
            return Math.Max(LeastDailyPayment, b);
        }

        /// <summary>The loan's figures for a term and an annual rate, as the game's loan would run.</summary>
        public static LoanFigures Figures(float amount, int years, float annualRate, float multiplier, int daysPerYear)
        {
            var f = new LoanFigures
            {
                Interest = GameLoanMath.DailyInterest(Math.Max(0f, amount), annualRate, multiplier, Math.Max(1, years), daysPerYear),
                Principal = DailyPayment(amount, years, daysPerYear)
            };
            f.Days = f.Principal > 0 ? (int)Math.Ceiling(amount / f.Principal) : 0;
            f.Total = amount > 0f ? (long)Math.Round(amount) + (long)f.Interest * f.Days : 0L;
            return f;
        }

        /// <summary>The rate a bank lends at with the points it agreed off, never under the least a bank lends at.</summary>
        public static int Rate(int ownRate, int pointsOff) => Math.Max(CreditCatalogue.RateFloor, ownRate - Math.Max(0, pointsOff));

        /// <summary>Whether a bank takes a point off now: the rating, the last ask, the rate's floor.</summary>
        public static LowerRateAnswer CanLower(bool running, int score, int today, int askedDay, int rate, bool given, out int waitDays)
        {
            waitDays = 0;
            if (!running)
            {
                return LowerRateAnswer.Off;
            }
            if (given)
            {
                return LowerRateAnswer.Given;
            }
            if (score < LowerRateFrom)
            {
                return LowerRateAnswer.Rating;
            }
            if (askedDay > 0 && today - askedDay < LowerRateEveryDays)
            {
                waitDays = LowerRateEveryDays - (today - askedDay);
                return LowerRateAnswer.Wait;
            }
            return rate - LowerRatePoints < CreditCatalogue.RateFloor ? LowerRateAnswer.Floor : LowerRateAnswer.Yes;
        }
    }
}
