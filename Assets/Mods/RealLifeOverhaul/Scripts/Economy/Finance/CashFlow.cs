using System;
using System.Collections.Generic;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Money in and out over a period, read in one pass over the game's transaction history when a page asks. The game drops
    /// a transaction over six days old once it keeps 1,000 (GameManager.ReduceTransactionQueue): a month may not be there.
    /// </summary>
    public sealed class CashFlow
    {
        public const int Yesterday = 0, Week = 1, Month = 2;
        public static readonly int[] PeriodDays = { 1, 7, 30 };
        public const int TopMost = 5;

        public static string[] CategoryKeys => CashCategory.Keys;

        /// <summary>Every movement of money read, oldest first as the game keeps them.</summary>
        public readonly List<CashEntry> Entries = new List<CashEntry>(256);
        /// <summary>The largest movements, largest first; <see cref="TopCount"/> of them are set.</summary>
        public readonly CashEntry[] Top = new CashEntry[TopMost];
        /// <summary>Money in and out by category (out is negative).</summary>
        public readonly float[] CategoryIn = new float[CashCategory.Count], CategoryOut = new float[CashCategory.Count];
        public int TopCount { get; private set; }
        public float In { get; private set; }
        public float Out { get; private set; }
        /// <summary>The day of the oldest transaction the game still keeps, of any kind; 0 if it keeps none.</summary>
        public int OldestDay { get; private set; }
        public int FromDay { get; private set; }

        /// <summary>Reads yesterday, or the last 7 or 30 days; with <paramref name="keep"/>, only the movements it accepts.</summary>
        public void Read(int period, int today, Func<Dictionary<string, string>, bool> keep)
        {
            Entries.Clear();
            Array.Clear(CategoryIn, 0, CategoryIn.Length);
            Array.Clear(CategoryOut, 0, CategoryOut.Length);
            TopCount = 0;
            In = Out = 0f;
            OldestDay = 0;
            period = Mathf.Clamp(period, 0, PeriodDays.Length - 1);
            int toDay = period == Yesterday ? today - 1 : today;
            FromDay = toDay - PeriodDays[period] + 1;
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.Transactions == null)
            {
                return;
            }
            foreach (Transaction t in save.Transactions)
            {
                if (t == null)
                {
                    continue;
                }
                int day = t.timestamp.Day;
                if (OldestDay == 0 || day < OldestDay)
                {
                    OldestDay = day;
                }
                if (day < FromDay || day > toDay || t.amount == 0f || (keep != null && !keep(t.transactionData)))
                {
                    continue;
                }
                string type = t.transactionType ?? string.Empty;
                int category = CashCategory.For(type);
                var entry = new CashEntry { Type = type, Data = t.transactionData, Amount = t.amount, Day = day, Category = category };
                Entries.Add(entry);
                if (t.amount > 0f)
                {
                    In += t.amount;
                    CategoryIn[category] += t.amount;
                }
                else
                {
                    Out += t.amount;
                    CategoryOut[category] += t.amount;
                }
                Rank(entry);
            }
        }

        private void Rank(in CashEntry entry)
        {
            float size = Mathf.Abs(entry.Amount);
            int place = TopCount;
            while (place > 0 && Mathf.Abs(Top[place - 1].Amount) < size)
            {
                place--;
            }
            if (place >= TopMost)
            {
                return;
            }
            for (int i = Mathf.Min(TopCount, TopMost - 1); i > place; i--)
            {
                Top[i] = Top[i - 1];
            }
            Top[place] = entry;
            TopCount = Mathf.Min(TopCount + 1, TopMost);
        }

        /// <summary>A period's name: "Yesterday", "Last 7 days".</summary>
        public static string PeriodName(int period)
        {
            return period == Yesterday ? FinanceText.T("reallife_econ_period_yesterday") : FinanceText.T("reallife_econ_period_days", "days", PeriodDays[period].ToString());
        }

        /// <summary>One transaction as EconoView's list writes it: its type's text, filled from its data.</summary>
        public static string ItemName(string type, Dictionary<string, string> data)
        {
            if (string.IsNullOrEmpty(type))
            {
                return FinanceText.T("reallife_econ_cash_other");
            }
            if (LocalizorManager.IsLocalizedKey(type))
            {
                return data != null && data.Count > 0 ? type.Localize(data).ToString() : type.GetLocalization();
            }
            string label = type + "_label";   // the type's name in EconoView's filter
            return LocalizorManager.IsLocalizedKey(label) ? label.GetLocalization() : FinanceText.T("reallife_econ_cash_other");
        }
    }

    /// <summary>The game's words for the apps' tables of money (CashTable).</summary>
    public sealed class CashWords : ICashWords
    {
        public static readonly CashWords Game = new CashWords();

        public string Day(int day) => FinanceText.Day(day);
        public string Item(string type, Dictionary<string, string> data) => CashFlow.ItemName(type, data);
        public string Category(int category) => FinanceText.T(CashCategory.Keys[category]);
        public string Money(float amount) => FinanceText.Money(amount);
    }
}
