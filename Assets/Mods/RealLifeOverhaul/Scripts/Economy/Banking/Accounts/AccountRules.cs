using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The player's cash as the accounts use it, in whole money: <see cref="Wallet"/> in the game, a counter offline.</summary>
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
        public const string ConsolidationOff = "reallife_transaction_consolidation_paid_off";
        public const string ConsolidationFee = "reallife_transaction_consolidation_fee";
        public const string SharkLent = "reallife_transaction_private_loan";
        public const string SharkPaid = "reallife_transaction_private_loan_paid";
        public const string SharkTaken = "reallife_transaction_private_loan_taken";
        public const string SharkDamage = "reallife_transaction_damage_repair";
        public const string Arrangement = "reallife_transaction_arrangement";
        public const string Bankruptcy = "reallife_transaction_bankruptcy";
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
    /// The accounts' arithmetic in whole money: interest in hundredths of a per cent over the game's days in a year, what is
    /// less than a unit carried to the next day (<see cref="CreditAccount.Carry"/>), so a long run loses nothing. Game-free.
    /// </summary>
    public static class AccountRules
    {
        public static int DaysPerYear = 60;        // the game's days in a year, set by the credit desk; 60 is the game's default

        // The central bank's rate (hundredths of a per cent a year) charged on an overdraft and a card today, and the game's
        // interest multiplier it was worked out with: set by the city's economy, nothing without it.
        public static int BaseBp;
        public static float BaseMultiplier = 1f;

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

        /// <summary>A day's interest on a balance, nothing carried: what an offer shows.</summary>
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

        /// <summary>Opens an account of a kind with its terms. The caller gives it its id and puts it in the file.</summary>
        public static CreditAccount Open(ProductKind kind, int agreed, int rateBp, int today)
        {
            return new CreditAccount
            {
                Kind = kind, Agreed = Math.Max(0, agreed), RateBp = Math.Max(0, rateBp), OpenedDay = today,
                NextDay = kind == ProductKind.CreditCard ? today + ProductCatalogue.CardStatementDays : 0
            };
        }

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

        /// <summary>The overdraft covers a shortfall, up to what is free of its limit. Returns what it paid (the caller puts it in cash).</summary>
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

        // Money off what is owed and an open statement, never more than is owed; a non-revolving account paid off closes.
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

        /// <summary>Money paid in by the borrower, with no fee, off what is owed and what is overdue. Returns what was used.</summary>
        public static int Pay(CreditAccount a, int amount)
        {
            int used = Reduce(a, amount);
            if (used == 0)
            {
                return 0;
            }
            Settled(a, used);
            if (a.Kind == ProductKind.CreditCard && a.MinimumLeft == 0 && a.Charged)
            {
                a.DueDay = 0;   // an overdue minimum is paid: nothing is due until the next statement
            }
            return used;
        }

        // Money paid comes off what is overdue; with nothing overdue or owed the debt is in order again. True if the penalty rate ends.
        private static bool Settled(CreditAccount a, int used)
        {
            LadderState l = a.Ladder;
            if (l.Overdue <= 0 && l.Misses <= 0)
            {
                return false;
            }
            l.Overdue = Math.Max(0, l.Overdue - used);
            l.PlanLeft = Math.Min(l.PlanLeft, l.Overdue);
            if (l.Overdue > 0 && a.Owed > 0)
            {
                return false;
            }
            l.PlanLeft = 0;
            return LadderRules.Paid(l);
        }

        /// <summary>The security is taken and sold against the debt; anything over is the borrower's. Returns what came off the debt.</summary>
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

        /// <summary>The collections call's payment plan. False if the account has had its one plan or nothing is overdue.</summary>
        public static bool AgreePlan(CreditAccount a)
        {
            if (!LadderRules.AgreePlan(a.Ladder))
            {
                return false;
            }
            if (a.Kind == ProductKind.CreditCard)
            {
                a.MinimumLeft = 0;   // the plan stands in for the overdue minimum
                a.DueDay = 0;
            }
            return true;
        }

        /// <summary>The end of one account's day: interest, the payment due and, when it is missed, the ladder's next step (only reported with consequences off).</summary>
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
                // a plan on a limit is a payment of its own each day (an instalment account takes it with the instalment)
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
                    // an instalment account's plan money is overdue already; a card's broken plan leaves all of it overdue
                    int planPart = onPlan && ProductCatalogue.Instalment(a.Kind) ? Math.Min(o.Missed, Math.Min(a.Ladder.PlanPerDay, a.Ladder.PlanLeft)) : 0;
                    int overdue = ProductCatalogue.Instalment(a.Kind) ? a.Ladder.Overdue + o.Missed - planPart
                        : onPlan && a.Kind == ProductKind.CreditCard ? Math.Max(o.Missed, a.Ladder.PlanLeft) : o.Missed;
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

        // Interest goes on what is drawn; cash above the float pays it back; what stands over the limit is due at once.
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

        // The repayment and the day's interest are taken together; if they cannot be, nothing is taken and the interest goes on what is owed.
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

        // A minimum not taken is tried again each day, but only its due day (when Charged is set) counts as a miss: one miss
        // and at most one late fee a statement, as a card counts it. What is unpaid of the statement then bears its interest.
        private static void Card(CreditAccount a, int today, IWallet wallet, string name, ref DayOutcome o)
        {
            if (a.DueDay > 0 && today >= a.DueDay)
            {
                bool first = !a.Charged;
                int minimum = Math.Min(a.MinimumLeft, a.Owed);
                if (minimum > 0 && wallet.Take(minimum, Money.Payment, name))
                {
                    o.Paid = Reduce(a, minimum);
                    a.MinimumLeft = 0;
                    if (!first)
                    {
                        o.PenaltyEnded = Settled(a, o.Paid);   // paid late: as a payment by hand, not one on time
                    }
                }
                else if (minimum > 0 && first)
                {
                    o.Missed = minimum;
                }
                o.WasDue = first;
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
                    // an unpaid minimum stays due on its own day, on top of this statement's
                    bool overdue = a.DueDay > 0 && a.MinimumLeft > 0;
                    int carried = Math.Max(overdue ? a.MinimumLeft : 0, a.Ladder.Overdue);
                    a.StatementLeft = a.Owed;
                    a.MinimumLeft = (int)Math.Min(a.Owed, (long)Minimum(a.Owed) + carried);
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
}
