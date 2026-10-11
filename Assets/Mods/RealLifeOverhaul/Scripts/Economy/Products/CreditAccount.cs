using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The player's cash as the accounts use it. The game's side is Economy/Products/Wallet.cs (the game's own
    /// transaction call); the offline check has one that only counts. Money is whole: the accounts never keep a fraction.
    /// </summary>
    public interface IWallet
    {
        /// <summary>Cash in hand, never below nothing.</summary>
        int Cash { get; }
        /// <summary>Takes an amount. False, and nothing taken, if the cash is not there.</summary>
        bool Take(int amount, string why, string name);
        void Give(int amount, string why, string name);
    }

    /// <summary>The transaction keys of the accounts' money (each has a text and a "_label" in en.json).</summary>
    public static class Money
    {
        public const string Payment = "reallife_transaction_credit_payment";
        public const string Drawn = "reallife_transaction_credit_drawn";
        public const string Cover = "reallife_transaction_overdraft_cover";
        public const string Advance = "reallife_transaction_finance_advance";
        public const string AdvanceBack = "reallife_transaction_finance_returned";
        public const string AdvanceCharge = "reallife_transaction_finance_charge";
        public const string LateFee = "reallife_transaction_late_fee";
        public const string Surplus = "reallife_transaction_repossession_surplus";
        public const string Diverted = "reallife_transaction_takings_diverted";
        public const string ConsolidationOut = "reallife_transaction_consolidation_paid_out";
        public const string ConsolidationOff = "reallife_transaction_consolidation_paid_off";
        public const string ConsolidationFee = "reallife_transaction_consolidation_fee";
        public const string SharkLent = "reallife_transaction_private_loan";
        public const string SharkPaid = "reallife_transaction_private_loan_paid";
        public const string SharkTaken = "reallife_transaction_private_loan_taken";
        public const string SharkDamage = "reallife_transaction_damage_repair";
        public const string Arrangement = "reallife_transaction_arrangement";
        public const string Bankruptcy = "reallife_transaction_bankruptcy";
    }

    /// <summary>
    /// Where one debt stands with its lender: how many due payments running have been missed and what has been done
    /// about it. One of these is kept for every account of the mod's and for every one of the game's loans
    /// (<see cref="LoanMark.Ladder"/>); <see cref="Ladder"/> moves it.
    /// </summary>
    public sealed class LadderState
    {
        public int Misses;          // due payments missed in a row
        public int Overdue;         // the money of those payments
        public int PaidRun;         // payments made in a row since the penalty rate began
        public int Step;            // the furthest step this run of misses has reached (LadderStep)
        public bool Penalty;        // the penalty rate is on
        public int PlanLeft;        // a payment plan: money still to pay under it
        public int PlanPerDay;
        public bool PlanUsed;       // one plan per account
        public bool CallDue;        // the collections call has not been answered yet
        public int RepoDay;         // the day the security was taken or the takings began to be diverted; 0 for not
        public bool RepoPending;    // the vehicle is to be taken as soon as it stands out of sight
        public int ClaimNumber;     // the court's number of the claim; 0 for none

        public bool InOrder => Misses == 0 && PlanLeft == 0 && RepoDay == 0 && !RepoPending && ClaimNumber == 0;

        public void Clear()
        {
            Misses = Overdue = PaidRun = Step = PlanLeft = PlanPerDay = RepoDay = ClaimNumber = 0;
            Penalty = PlanUsed = CallDue = RepoPending = false;
        }
    }

    /// <summary>
    /// One account of the mod's own, whatever its kind: an overdraft, a credit card, a vehicle or a building on
    /// finance, or late fees left over from one of the game's loans. It is credit like the game's loans are
    /// (<see cref="ICreditAccount"/>), so what is owed on it counts in the rating and in the Finance app. Saved in the
    /// credit file's line (<see cref="CreditStore"/>, part "a"). Game-free; <see cref="AccountRules"/> has the arithmetic.
    /// </summary>
    public sealed class CreditAccount : ICreditAccount
    {
        public int Id;                  // unique in its file, never reused
        public ProductKind Kind;
        public int Owed;                // whole money owed now
        public int Agreed;              // the limit (overdraft, card) or the amount financed (car, building)
        public int RateBp;              // per cent a year in hundredths, as agreed (the penalty rate is not in it)
        public long Carry;              // interest not yet a whole unit of money: nothing is lost to rounding
        public int OpenedDay;
        public int Term;                // days, of an instalment account
        public int Instalment;          // its daily repayment, before interest
        public int NextDay;             // card: the next statement day
        public int DueDay;              // card: the day the statement's minimum is taken; 0 for no statement open
        public int StatementLeft;       // card: of the last statement, still unpaid
        public int MinimumLeft;         // card: of its minimum, still unpaid
        public bool Charged;            // card: this statement's interest has been charged
        public string Security = string.Empty;      // a vehicle's id, or a building's street and number as "street#number"
        public string SecurityName = string.Empty;  // the vehicle's type or the building's address, for the app
        public int SecurityValue;       // the price it was bought for
        public bool Secured;            // false once the security is sold, lost or taken
        public string Street = string.Empty;        // the lending bank's address, as the credit file writes one
        public int Number;
        public bool Frozen;             // in a debt arrangement: no interest, no payments, no steps
        public bool Closed;
        public bool Test;               // made by a test tool: "Test: put everything back" removes it
        public readonly LadderState Ladder = new LadderState();

        public string Key => ProductCatalogue.KindSources[(int)Kind];
        public string NameKey => ProductCatalogue.KindKeys[(int)Kind];
        public float Balance => Closed ? 0f : Owed;
        public float Limit => Closed ? 0f : Agreed;
        public float PaymentDue => Closed || Frozen ? 0f : AccountRules.DueTomorrow(this, CreditLedger.Today);

        /// <summary>What can still be drawn on a limit.</summary>
        public int Free => Closed || !ProductCatalogue.Revolving(Kind) ? 0 : Math.Max(0, Agreed - Owed);

        /// <summary>The rate as agreed with the central bank's rate of the day on it: only an overdraft and a card follow that; anything else keeps the rate it was made at.</summary>
        public int RateAgreedNow => ProductCatalogue.Revolving(Kind) ? ProductTerms.WithBase(RateBp, AccountRules.BaseBp, AccountRules.BaseMultiplier) : RateBp;

        /// <summary>The rate charged now, the penalty rate included.</summary>
        public int RateNow => RateAgreedNow + (Ladder.Penalty ? ProductCatalogue.PenaltyPoints * 100 : 0);
    }

    /// <summary>What one day did to one account.</summary>
    public struct DayOutcome
    {
        public bool WasDue;         // a payment was due today
        public int Paid;            // money taken today
        public int Missed;          // the payment that could not be taken; 0 if none was missed
        public int Interest;        // charged with the payment, or added to what is owed
        public int Fee;             // a late fee added today
        public LadderStep Step;     // a step taken today
        public bool PenaltyEnded;
        public bool Statement;      // card: a statement was drawn up today
        public bool Closed;         // paid off today
    }

    /// <summary>
    /// The accounts' arithmetic. All of it is whole money and exact: interest is worked out on the balance in
    /// hundredths of a per cent over the game's days in a year, and what is less than a unit is carried to the next
    /// day (<see cref="CreditAccount.Carry"/>), so a long run neither gains nor loses anything. Game-free.
    /// </summary>
    public static class AccountRules
    {
        /// <summary>Interest on a balance for a number of days, with the remainder carried.</summary>
        public static int Interest(int balance, int rateBp, int daysPerYear, int days, ref long carry)
        {
            if (balance <= 0 || rateBp <= 0 || days <= 0)
            {
                return 0;
            }
            long unit = 10000L * Math.Max(1, daysPerYear);
            long sum = (long)balance * rateBp * days + Math.Max(0L, Math.Min(carry, unit - 1));
            carry = sum % unit;
            return (int)Math.Min(int.MaxValue, sum / unit);
        }

        /// <summary>A day's interest on a balance, to the nearest unit: what an offer shows.</summary>
        public static int InterestADay(int balance, int rateBp, int daysPerYear)
        {
            long carry = 0;
            return Interest(balance, rateBp, daysPerYear, 1, ref carry);
        }

        /// <summary>The daily repayment that pays an amount off in a term: the amount over the days, rounded up.</summary>
        public static int InstalmentFor(int amount, int term)
        {
            return amount <= 0 ? 0 : (amount + Math.Max(1, term) - 1) / Math.Max(1, term);
        }

        /// <summary>A card statement's minimum payment.</summary>
        public static int Minimum(int balance)
        {
            return balance <= 0 ? 0 : Math.Min(balance, Math.Max(ProductCatalogue.CardMinimumLeast, ProductCatalogue.ShareUp(balance, ProductCatalogue.CardMinimumShare)));
        }

        /// <summary>The fee for drawing cash on the card.</summary>
        public static int CashFee(int amount)
        {
            return amount <= 0 ? 0 : Math.Max(ProductCatalogue.CardCashFeeLeast, ProductCatalogue.ShareUp(amount, ProductCatalogue.CardCashFeeShare));
        }

        /// <summary>The most cash that can be drawn on a card now: what is free of the limit, less the fee it would cost.</summary>
        public static int MostDraw(CreditAccount card)
        {
            int free = card.Free;
            int most = (int)Math.Floor(free / (1.0 + ProductCatalogue.CardCashFeeShare));
            while (most > 0 && most + CashFee(most) > free)
            {
                most--;
            }
            return Math.Max(0, most);
        }

        /// <summary>The late fee for a missed payment of this size.</summary>
        public static int LateFee(int payment, int level)
        {
            int fee = Math.Max(ProductCatalogue.LateFeeLeast, ProductCatalogue.ShareUp(payment, ProductCatalogue.LateFeeShare));
            return level <= ProductCatalogue.Relaxed ? (fee + 1) / 2 : fee;
        }

        /// <summary>What the account takes at the next day's end, interest included: for "leaves tomorrow" and the Planner.</summary>
        public static int DueTomorrow(CreditAccount a, int today)
        {
            if (a.Closed || a.Frozen || a.Owed <= 0)
            {
                return 0;
            }
            switch (a.Kind)
            {
                case ProductKind.CarFinance:
                case ProductKind.Mortgage:
                    {
                        int principal = Math.Min(a.Instalment, a.Owed);
                        int plan = Math.Min(Math.Min(a.Ladder.PlanPerDay, a.Ladder.PlanLeft), a.Owed - principal);
                        long carry = a.Carry;
                        return principal + plan + Interest(a.Owed, a.RateNow, DaysPerYear, 1, ref carry);
                    }
                case ProductKind.CreditCard:
                    return a.DueDay > 0 && a.DueDay <= today + 1 ? a.MinimumLeft : 0;
                case ProductKind.Overdraft:
                    return Math.Max(0, a.Owed - a.Agreed);
                default:
                    return a.Owed;
            }
        }

        /// <summary>The game's days in a year, for interest. Set by the economy's director; 60 is the game's own default.</summary>
        public static int DaysPerYear = 60;

        /// <summary>
        /// The central bank's rate in hundredths of a per cent a year, as an overdraft and a card are charged it today
        /// (<see cref="CreditAccount.RateAgreedNow"/>), and the game's interest multiplier it was worked out with.
        /// Nothing unless the city's economy (Economy/City) has set them.
        /// </summary>
        public static int BaseBp;
        public static float BaseMultiplier = 1f;

        /// <summary>Opens an account of a kind with its terms. The caller gives it its id and puts it in the file.</summary>
        public static CreditAccount Open(ProductKind kind, int agreed, int rateBp, int today)
        {
            return new CreditAccount
            {
                Kind = kind, Agreed = Math.Max(0, agreed), RateBp = Math.Max(0, rateBp), OpenedDay = today,
                NextDay = kind == ProductKind.CreditCard ? today + ProductCatalogue.CardStatementDays : 0
            };
        }

        /// <summary>Opens a vehicle or a building on finance for the amount financed.</summary>
        public static CreditAccount Finance(ProductKind kind, int financed, int term, int rateBp, int today, string security, string name, int value)
        {
            CreditAccount a = Open(kind, financed, rateBp, today);
            a.Owed = Math.Max(0, financed);
            a.Term = Math.Max(1, term);
            a.Instalment = InstalmentFor(financed, term);
            a.Security = security ?? string.Empty;
            a.SecurityName = name ?? string.Empty;
            a.SecurityValue = Math.Max(0, value);
            a.Secured = !string.IsNullOrEmpty(security);
            return a;
        }

        /// <summary>
        /// The overdraft covers a shortfall: it pays what is missing into cash, up to what is free of its limit, and
        /// that goes on what is drawn. Returns what it paid (the caller puts it into the wallet).
        /// </summary>
        public static int Cover(CreditAccount overdraft, int shortfall)
        {
            if (overdraft == null || overdraft.Closed || overdraft.Frozen || overdraft.Kind != ProductKind.Overdraft || shortfall <= 0)
            {
                return 0;
            }
            int paid = Math.Min(shortfall, overdraft.Free);
            overdraft.Owed += paid;
            return paid;
        }

        /// <summary>Cash drawn on the card: the amount and its fee go on the balance. False if it does not fit the limit.</summary>
        public static bool Draw(CreditAccount card, int amount, out int fee)
        {
            fee = CashFee(amount);
            if (card.Closed || card.Frozen || card.Kind != ProductKind.CreditCard || amount < ProductCatalogue.CardDrawLeast || amount + fee > card.Free)
            {
                return false;
            }
            card.Owed += amount + fee;
            return true;
        }

        // Money off what is owed: off an open card statement too, and never more than is owed. An instalment account or
        // a leftover of fees with nothing left on it is closed.
        private static int Reduce(CreditAccount a, int amount)
        {
            int used = Math.Max(0, Math.Min(amount, a.Owed));
            if (used == 0)
            {
                return 0;
            }
            a.Owed -= used;
            a.StatementLeft = Math.Max(0, a.StatementLeft - used);
            a.MinimumLeft = Math.Max(0, a.MinimumLeft - used);
            a.Ladder.PlanLeft = Math.Min(a.Ladder.PlanLeft, a.Owed);
            if (a.Owed == 0)
            {
                a.Carry = 0;
                a.StatementLeft = a.MinimumLeft = 0;
                a.Closed = !ProductCatalogue.Revolving(a.Kind);
            }
            return used;
        }

        /// <summary>
        /// Money paid in by the borrower at any time, with no fee. It comes off what is owed and off what is overdue;
        /// once nothing is overdue the account is in order again. Returns what was used (never more than is owed).
        /// </summary>
        public static int Pay(CreditAccount a, int amount)
        {
            int used = Reduce(a, amount);
            if (used == 0)
            {
                return 0;
            }
            LadderState l = a.Ladder;
            if (l.Overdue > 0 || l.Misses > 0)
            {
                l.Overdue = Math.Max(0, l.Overdue - used);
                l.PlanLeft = Math.Min(l.PlanLeft, l.Overdue);
                if (l.Overdue == 0 || a.Owed == 0)
                {
                    l.PlanLeft = 0;
                    LadderRules.Paid(l);
                }
            }
            if (a.Kind == ProductKind.CreditCard && a.MinimumLeft == 0 && a.Charged)
            {
                a.DueDay = 0;   // a minimum that was overdue is paid: nothing is due until the next statement
            }
            return used;
        }

        /// <summary>
        /// The security is taken and sold: what it fetches (<see cref="LadderRules.RepossessionValue"/>) comes off what
        /// is owed, anything over that is the borrower's (<paramref name="surplus"/>), and what is left stays owed
        /// with nothing behind it. Returns what came off the debt.
        /// </summary>
        public static int Repossess(CreditAccount a, int today, out int surplus)
        {
            int fetched = LadderRules.RepossessionValue(a.SecurityValue);
            int used = Reduce(a, fetched);
            surplus = fetched - used;
            a.Secured = false;
            a.Ladder.RepoPending = false;
            a.Ladder.RepoDay = Math.Max(1, today);
            return used;
        }

        /// <summary>The security has gone (sold, or lost): what is owed stays owed with nothing behind it, on the same payments.</summary>
        public static void Unsecure(CreditAccount a)
        {
            a.Secured = false;
            a.Ladder.RepoPending = false;
        }

        /// <summary>
        /// The payment plan of the collections call: what is overdue is spread over the plan's days on top of the
        /// normal payment, and the account counts as in order while the plan is kept. False if it has had its one plan.
        /// </summary>
        public static bool AgreePlan(CreditAccount a)
        {
            if (!LadderRules.AgreePlan(a.Ladder))
            {
                return false;
            }
            if (a.Kind == ProductKind.CreditCard)
            {
                a.MinimumLeft = 0;   // the plan stands in for the minimum that was overdue
                a.DueDay = 0;
            }
            return true;
        }

        /// <summary>
        /// The end of a day for one account: interest, the payment that is due, and, when it cannot be taken, the
        /// next step of <see cref="LadderRules"/>. With <paramref name="consequences"/> off a miss is only reported
        /// (the rating falls, nothing else happens). An account in a debt arrangement stands still.
        /// </summary>
        public static DayOutcome RunDay(CreditAccount a, int today, IWallet wallet, int level, bool consequences, string name)
        {
            var o = new DayOutcome();
            if (a.Closed || a.Frozen)
            {
                return o;
            }
            bool onPlan = a.Ladder.PlanLeft > 0;
            switch (a.Kind)
            {
                case ProductKind.Overdraft: Overdraft(a, wallet, name, onPlan, ref o); break;
                case ProductKind.CreditCard: Card(a, today, wallet, name, ref o); break;
                case ProductKind.CarFinance:
                case ProductKind.Mortgage: Instalments(a, wallet, name, ref o); break;
                default:
                    if (a.Owed > 0 && wallet.Take(a.Owed, Money.LateFee, name))
                    {
                        o.Paid = Reduce(a, a.Owed);
                    }
                    break;
            }
            if (onPlan && ProductCatalogue.Revolving(a.Kind) && !a.Closed)
            {
                // A plan on a limit is a payment of its own each day (an instalment account takes it with the instalment).
                int plan = Math.Min(Math.Min(a.Ladder.PlanPerDay, a.Ladder.PlanLeft), a.Owed);
                o.WasDue = true;
                if (plan > 0 && wallet.Take(plan, Money.Payment, name))
                {
                    a.Ladder.PlanLeft -= plan;
                    a.Ladder.Overdue = Math.Max(0, a.Ladder.Overdue - plan);
                    o.Paid += Reduce(a, plan);
                }
                else if (plan > 0)
                {
                    o.Missed += plan;
                }
            }
            if (o.WasDue)
            {
                if (o.Missed == 0)
                {
                    o.PenaltyEnded = LadderRules.Paid(a.Ladder);
                }
                else if (consequences)
                {
                    // (an instalment account's plan money is overdue already: only the instalment itself is new)
                    int planPart = onPlan && ProductCatalogue.Instalment(a.Kind) ? Math.Min(o.Missed, Math.Min(a.Ladder.PlanPerDay, a.Ladder.PlanLeft)) : 0;
                    int overdue = ProductCatalogue.Instalment(a.Kind) ? a.Ladder.Overdue + o.Missed - planPart : o.Missed;
                    o.Step = LadderRules.Missed(a.Ladder, level, a.Secured, overdue);
                    if (o.Step == LadderStep.LateFee)
                    {
                        o.Fee = LateFee(o.Missed, level);
                        a.Owed += o.Fee;
                    }
                }
            }
            o.Closed = a.Closed;
            return o;
        }

        // Interest is added to what is drawn; cash above the float pays it back; what stands over the limit is due at once.
        private static void Overdraft(CreditAccount a, IWallet wallet, string name, bool onPlan, ref DayOutcome o)
        {
            if (a.Owed <= 0)
            {
                return;
            }
            o.Interest = Interest(a.Owed, a.RateNow, DaysPerYear, 1, ref a.Carry);
            a.Owed += o.Interest;
            int pay = Math.Min(a.Owed, Math.Max(0, wallet.Cash - ProductCatalogue.OverdraftFloat));
            if (pay > 0 && wallet.Take(pay, Money.Payment, name))
            {
                o.Paid = Reduce(a, pay);
            }
            if (onPlan)
            {
                return;   // what is over the limit is being paid under the plan
            }
            if (a.Owed > a.Agreed)
            {
                o.WasDue = true;
                o.Missed = a.Owed - a.Agreed;
            }
            else if (a.Ladder.Misses > 0)
            {
                o.WasDue = true;   // back within its limit: in order again
            }
        }

        // The repayment and the day's interest are taken together. If they cannot be, nothing is taken and the interest is added to what is owed.
        private static void Instalments(CreditAccount a, IWallet wallet, string name, ref DayOutcome o)
        {
            if (a.Owed <= 0)
            {
                a.Closed = true;
                return;
            }
            o.WasDue = true;
            o.Interest = Interest(a.Owed, a.RateNow, DaysPerYear, 1, ref a.Carry);
            int principal = Math.Min(a.Instalment, a.Owed);
            int plan = Math.Min(Math.Min(a.Ladder.PlanPerDay, a.Ladder.PlanLeft), a.Owed - principal);
            if (wallet.Take(principal + plan + o.Interest, Money.Payment, name))
            {
                o.Paid = principal + plan + o.Interest;
                a.Ladder.PlanLeft -= plan;
                a.Ladder.Overdue = Math.Max(0, a.Ladder.Overdue - plan);
                Reduce(a, principal + plan);
                return;
            }
            a.Owed += o.Interest;
            o.Missed = principal + plan;
        }

        // A statement every few days; its minimum is taken on the due day, and what is then still unpaid of it bears
        // interest for the days of the statement. A minimum that could not be taken is tried again each day.
        private static void Card(CreditAccount a, int today, IWallet wallet, string name, ref DayOutcome o)
        {
            if (a.DueDay > 0 && today >= a.DueDay)
            {
                o.WasDue = !a.Charged || a.MinimumLeft > 0;
                int minimum = Math.Min(a.MinimumLeft, a.Owed);
                if (minimum > 0 && wallet.Take(minimum, Money.Payment, name))
                {
                    o.Paid = Reduce(a, minimum);
                    a.MinimumLeft = 0;
                }
                else if (minimum > 0)
                {
                    o.Missed = minimum;
                }
                if (!a.Charged)
                {
                    a.Charged = true;
                    o.Interest = Interest(Math.Min(a.StatementLeft, a.Owed), a.RateNow, DaysPerYear, ProductCatalogue.CardStatementDays, ref a.Carry);
                    a.Owed += o.Interest;
                }
                if (a.MinimumLeft == 0)
                {
                    a.DueDay = 0;
                }
            }
            if (a.NextDay <= 0)
            {
                a.NextDay = today + ProductCatalogue.CardStatementDays;
            }
            if (today >= a.NextDay)
            {
                while (a.NextDay <= today)
                {
                    a.NextDay += ProductCatalogue.CardStatementDays;
                }
                if (a.Owed > 0 && a.Ladder.PlanLeft == 0)
                {
                    bool overdue = a.DueDay > 0 && a.MinimumLeft > 0;
                    a.StatementLeft = a.Owed;
                    a.MinimumLeft = Minimum(a.Owed);
                    a.Charged = false;
                    if (!overdue)
                    {
                        a.DueDay = today + ProductCatalogue.CardGraceDays;
                    }
                    o.Statement = true;
                }
            }
        }
    }

    /// <summary>
    /// The game's own loan figures, as its bank works them out (LoanHelper), for the places where the mod has to
    /// reckon with a rate of its own: a business loan a point under the bank's rate, and the penalty rate on a loan in
    /// default. Game-free, so the offline check holds it against a model of the game's formula.
    /// </summary>
    public static class GameLoanMath
    {
        /// <summary>
        /// LoanHelper.CalculateDailyInterestPayment, term for term: the amount at the rate and the game's interest
        /// multiplier, times the bank's years to pay, over the days of those years, rounded down.
        /// </summary>
        public static int DailyInterest(float amount, float annualRate, float multiplier, int years, int daysPerYear)
        {
            years = Math.Max(1, years);
            return (int)Math.Floor(amount * annualRate * multiplier / 100f * (float)years / (float)(Math.Max(1, daysPerYear) * years));
        }

        /// <summary>
        /// A loan's daily interest with penalty points on its rate. The game keeps a loan's interest, not its rate, so
        /// the points are worked out on the amount borrowed by the game's formula and added to the loan's own interest:
        /// the same as the formula at the loan's rate plus the points, to within the rounding of one unit.
        /// </summary>
        public static int PenaltyInterest(int ownInterest, float amount, int points, float multiplier, int daysPerYear)
        {
            return Math.Max(0, ownInterest) + DailyInterest(amount, points, multiplier, 1, daysPerYear);
        }
    }
}
