using System;
using System.Collections.Generic;
using BAModAPI;
using Entities;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Giving
{
    /// <summary>
    /// The one file of giving that touches the game: the clock, the player's cash through the mod's one booking call, the game's own list of deductions, the living population's
    /// state and the notices. Nothing per frame.
    /// </summary>
    public static class GivingGame
    {
        public const string CharityKey = "reallife_transaction_giving_charity";
        public const string PersonKey = "reallife_transaction_giving_person";
        public const string LoanKey = "reallife_transaction_giving_loan";
        public const string LoanBackKey = "reallife_transaction_giving_loan_back";
        public const string DeductKey = "reallife_transaction_giving_deduct";
        private const string DeductName = "reallife_giving_deduct";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        public static bool Ready => Save != null;

        public static int Day => Save != null ? Save.Day : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        public static int MonthDays => MacroCatalogue.MonthDays(DaysPerYear);

        public static int Level => Prices.Level;

        /// <summary>The player's cash in whole money, never under nothing.</summary>
        public static long Cash => Save != null ? (long)Math.Floor(Mathf.Max(0f, Mathf.Min(Save.Money, 1e12f))) : 0L;

        /// <summary>The cash that may be given: over what the investors of the player's firms are owed, which a gift would otherwise take (Tech's firm account counts it as misuse).</summary>
        public static long Spare
        {
            get
            {
                long reserved = 0;
                try
                {
                    reserved = GivingBridges.Reserved != null ? GivingBridges.Reserved() : 0L;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                return Math.Max(0L, Cash - Math.Max(0L, reserved));
            }
        }

        /// <summary>Money out of the player's cash, whole money, through the mod's one call; false, nothing taken, if the spare cash does not cover it.</summary>
        public static bool Pay(long amount, string key)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (amount > Spare || amount > int.MaxValue / 2)
            {
                return false;
            }
            return BizBooks.Book(-(float)amount, new TransactionInfo(key), null, null, force: false, showNotification: false);
        }

        /// <summary>Money into the player's cash, whole money: a loan paid back.</summary>
        public static void Receive(long amount, string key)
        {
            if (amount > 0)
            {
                BizBooks.Book((float)amount, new TransactionInfo(key), null, null, force: true);
            }
        }

        /// <summary>The living population as giving may touch it: running here, this player's own and not in a Going Public session. Null otherwise.</summary>
        public static PopulationState People()
        {
            if (!Settings.LivingPopulation || !CityPopulation.Running || CityPopulation.Following || GoingPublic.InSession)
            {
                return null;
            }
            return CityPopulation.Live;
        }

        // ---- the game's tax

        /// <summary>
        /// The name the deduction goes into the game's list under. The game keeps a deduction by the name it was given and writes the statement from it when the tax falls due
        /// (TaxHelper.GetDeductibleExpenseLabel), translating it if it is a key it knows: its own deductions are keys, so a change of language reaches the statement. Giving's key
        /// is such a key whenever the game's text tables hold it; if they do not, the words of the language in use are what is left to give.
        /// </summary>
        public static string DeductLabel => LocalizorManager.IsLocalizedKey(DeductName) ? DeductName : DeductName.GetLocalization();

        /// <summary>
        /// What the game's list of deductions holds under giving's name for the running tax year, whole money. A name in another language's words is not read, which is why giving
        /// also keeps its own record of what it claimed.
        /// </summary>
        public static long Deducted()
        {
            List<TaxDeductibleExpense> list = Save != null ? Save.currentTaxPeriodDeductibleExpenses : null;
            string words = DeductName.GetLocalization();
            double sum = 0;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                if (list[i] != null && GivingYear.Ours(list[i].key, DeductName, words))
                {
                    sum += Math.Abs(list[i].amount);
                }
            }
            return (long)Math.Min(sum, 1e12);
        }

        /// <summary>Gives the game's tax a deduction for gifts, as it deducts a business's costs (TaxHelper.TrackTransaction); what is due is worked out first (GivingYear.Claim).</summary>
        public static void Deduct(long amount)
        {
            if (amount <= 0 || Save == null)
            {
                return;
            }
            var info = new TransactionInfo(DeductKey);
            info.SetTaxDeductibleName(DeductLabel);
            TaxHelper.TrackTransaction(new Transaction(info) { amount = -(float)amount });
        }

        // ---- words

        public static string Money(long amount) => ((float)amount).ToCurrencyFormat();

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action click = null)
        {
            Notifications.Show(type, key, data, 10f, null, click != null ? Guard.Wrap(click) : null, true, false);
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var d = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                d[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return d;
        }
    }
}
