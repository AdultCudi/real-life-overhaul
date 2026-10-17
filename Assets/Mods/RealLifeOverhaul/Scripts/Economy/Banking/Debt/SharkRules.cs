using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the lender of last resort does about a debt that is past its day.</summary>
    public enum SharkAct
    {
        None = 0,
        Warn = 1,       // a text
        Assault = 2,    // the borrower is set upon in the street, and the cash on them is taken against the debt
        Damage = 3      // one of the borrower's businesses is damaged: a repair bill
    }

    /// <summary>
    /// The one debt to the lender of last resort, as the save keeps it (<see cref="CreditStore"/>, part "k"). It is
    /// not a credit account: it is not on the credit file, it is not in the rating, and paying it builds nothing.
    /// </summary>
    public sealed class SharkDebt
    {
        public bool Known;      // the lender has made itself known: its contact is on the phone
        public int Borrowed;    // the sum borrowed; 0 while nothing is owed
        public int Owed;
        public int TakenDay;
        public int DueDay;
        public int GrownDay;    // the last day interest was added for
        public bool Extended;   // the one extension has been had
        public int ActedDay;    // the last day past due on which something was done about it
        public SharkAct Waiting;// an act that could not be carried out yet (the borrower was held, or not on foot)
        public bool Test;

        public bool Active => Borrowed > 0 && Owed > 0;

        public void Clear()
        {
            Borrowed = Owed = TakenDay = DueDay = GrownDay = ActedDay = 0;
            Extended = Test = false;
            Waiting = SharkAct.None;
        }
    }

    /// <summary>
    /// The lender of last resort's terms, in whole money. Simple interest on the sum borrowed each day, the whole of
    /// it due in one sum, one extension for a fee, and never more owed than a multiple of the sum. Game-free.
    /// </summary>
    public static class SharkRules
    {
        /// <summary>The most it lends: its usual most, or so many days of the borrower's average daily income if that is more, to the step below.</summary>
        public static int Most(float dailyIncome)
        {
            double byIncome = Math.Max(0f, dailyIncome) * (double)ProductCatalogue.SharkIncomeDays;
            int most = (int)Math.Min(int.MaxValue / 4, Math.Max(ProductCatalogue.SharkMost, Math.Floor(byIncome)));
            return most / ProductCatalogue.SharkStep * ProductCatalogue.SharkStep;
        }

        public static int Cap(int borrowed)
        {
            return borrowed * ProductCatalogue.SharkCapMultiple;
        }

        /// <summary>A day's interest on a sum.</summary>
        public static int ADay(int borrowed)
        {
            return ProductCatalogue.ShareUp(borrowed, ProductCatalogue.SharkDailyShare);
        }

        /// <summary>What a sum borrowed today comes to on its due day.</summary>
        public static int OwedOnDueDay(int borrowed)
        {
            return Math.Min(Cap(borrowed), borrowed + ADay(borrowed) * ProductCatalogue.SharkDueDays);
        }

        public static ProductAnswer CanLend(SharkDebt debt, int amount, float dailyIncome)
        {
            if (debt.Active)
            {
                return ProductAnswer.AlreadyOpen;
            }
            return amount < ProductCatalogue.SharkLeast ? ProductAnswer.TooLittle : amount > Most(dailyIncome) ? ProductAnswer.TooMuch : ProductAnswer.Yes;
        }

        public static void Lend(SharkDebt debt, int amount, int today)
        {
            debt.Borrowed = amount;
            debt.Owed = amount;
            debt.TakenDay = today;
            debt.GrownDay = today;
            debt.DueDay = today + ProductCatalogue.SharkDueDays;
            debt.Extended = false;
            debt.ActedDay = 0;
            debt.Waiting = SharkAct.None;
        }

        /// <summary>Adds the interest of every day since it was last added, up to the cap. Returns what was added.</summary>
        public static int Grow(SharkDebt debt, int today)
        {
            if (!debt.Active || today <= debt.GrownDay)
            {
                return 0;
            }
            long added = (long)ADay(debt.Borrowed) * (today - debt.GrownDay);
            int room = Math.Max(0, Cap(debt.Borrowed) - debt.Owed);
            int add = (int)Math.Min(added, room);
            debt.Owed += add;
            debt.GrownDay = today;
            return add;
        }

        /// <summary>Money against the debt. Returns what was used; with nothing left the debt is over.</summary>
        public static int Pay(SharkDebt debt, int amount)
        {
            int used = Math.Max(0, Math.Min(amount, debt.Owed));
            debt.Owed -= used;
            if (debt.Owed == 0)
            {
                debt.Clear();
            }
            return used;
        }

        /// <summary>The fee for the one extension, on what is owed now.</summary>
        public static int ExtendFee(SharkDebt debt)
        {
            return Math.Min(Math.Max(0, Cap(debt.Borrowed) - debt.Owed), ProductCatalogue.ShareUp(debt.Owed, ProductCatalogue.SharkExtendFeeShare));
        }

        /// <summary>The one extension: the fee goes on the debt and the due day moves on. False if it has been had.</summary>
        public static bool Extend(SharkDebt debt, int today)
        {
            if (!debt.Active || debt.Extended)
            {
                return false;
            }
            debt.Owed += ExtendFee(debt);
            debt.Extended = true;
            debt.DueDay = Math.Max(debt.DueDay, today) + ProductCatalogue.SharkDueDays;
            debt.ActedDay = 0;
            debt.Waiting = SharkAct.None;
            return true;
        }

        /// <summary>
        /// What is done on a given number of days past the due day: a warning on the first, an assault on the third,
        /// damage on the sixth, and then one or the other in turn every few days. With <paramref name="violence"/>
        /// off (the mildest level) every one of them is a warning.
        /// </summary>
        public static SharkAct ActOn(int daysPast, bool violence)
        {
            SharkAct act;
            if (daysPast == ProductCatalogue.SharkWarnDay)
            {
                act = SharkAct.Warn;
            }
            else if (daysPast == ProductCatalogue.SharkAssaultDay)
            {
                act = SharkAct.Assault;
            }
            else if (daysPast >= ProductCatalogue.SharkDamageDay && (daysPast - ProductCatalogue.SharkDamageDay) % ProductCatalogue.SharkRepeatDays == 0)
            {
                act = (daysPast - ProductCatalogue.SharkDamageDay) / ProductCatalogue.SharkRepeatDays % 2 == 0 ? SharkAct.Damage : SharkAct.Assault;
            }
            else
            {
                return SharkAct.None;
            }
            return violence || act == SharkAct.None ? act : SharkAct.Warn;
        }

        /// <summary>The act for today, once: nothing if today's has been dealt with or the debt is not past its day.</summary>
        public static SharkAct Today(SharkDebt debt, int today, bool violence)
        {
            if (!debt.Active || today <= debt.DueDay || debt.ActedDay == today)
            {
                return SharkAct.None;
            }
            debt.ActedDay = today;
            return ActOn(today - debt.DueDay, violence);
        }

        /// <summary>The repair bill of a damaged business.</summary>
        public static int DamageBill(int owed)
        {
            int bill = ProductCatalogue.ShareUp(owed, ProductCatalogue.SharkDamageShare);
            return Math.Max(ProductCatalogue.SharkDamageLeast, Math.Min(ProductCatalogue.SharkDamageMost, bill));
        }
    }
}
