using System;
using System.Collections.Generic;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Money in and out over a period, read from the game's own transaction history
    /// (SaveGameManager.Current.Transactions, the list EconoView shows) and kept nowhere else. One pass over the
    /// history when the Cash flow tab is shown or its period is changed; nothing is read while the app is closed.
    ///
    /// The history is grouped by the game's transaction type, the grouping EconoView's own filter offers: the game
    /// gives a category to only eleven kinds of transaction (rent, wages, loan costs and a few more), a type to every
    /// one. A type's name is the game's own label for it (the type's key with "_label").
    ///
    /// HOW FAR BACK. The game drops a transaction once more than 1,000 are kept and it is over six days old
    /// (GameManager.ReduceTransactionQueue), so seven days are always there and thirty only in a quiet month:
    /// <see cref="OldestDay"/> says how far the history reaches, and the app says so when the period asks for more.
    ///
    /// A bank's statement and a card's movements read only their own (<see cref="Read(int, int, Func{Dictionary{string, string}, bool})"/>):
    /// the pass over the history is the same, but only what they keep is listed, counted and ranked. The list is shown as a
    /// table written as it scrolls (CashTable), so a month of 30,000 movements makes some fifteen rows of words, not 30,000.
    /// </summary>
    public sealed class CashFlow
    {
        public struct Kind
        {
            public string Type;
            public float In, Out;
            public int Count;
            public float Net => In + Out;
        }

        public struct Item
        {
            public string Type;
            public Dictionary<string, string> Data;
            public float Amount;
            public int Day;
        }

        /// <summary>The names of the categories, for the table's filter.</summary>
        public static string[] CategoryKeys => CashCategory.Keys;

        public const int Yesterday = 0, Week = 1, Month = 2;
        public static readonly int[] PeriodDays = { 1, 7, 30 };
        public const int TopMost = 5;

        private static readonly Comparison<Kind> ByWeight = (a, b) => Mathf.Abs(b.Net).CompareTo(Mathf.Abs(a.Net));

        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(48);

        public readonly List<Kind> Kinds = new List<Kind>(48);
        public readonly Item[] Top = new Item[TopMost];
        /// <summary>Every movement of money in the period (that the read kept), oldest first as the game keeps them.</summary>
        public readonly List<CashEntry> Entries = new List<CashEntry>(256);
        /// <summary>Money in and out by category over the period (out is negative).</summary>
        public readonly float[] CategoryIn = new float[CashCategory.Count], CategoryOut = new float[CashCategory.Count];
        public int TopCount { get; private set; }
        public float In { get; private set; }
        public float Out { get; private set; }
        public int Count { get; private set; }
        /// <summary>The day of the oldest transaction the game still keeps; 0 if it keeps none.</summary>
        public int OldestDay { get; private set; }
        public int FromDay { get; private set; }
        public int ToDay { get; private set; }

        /// <summary>Reads the period: yesterday alone, or the last 7 or 30 days with today as the last of them.</summary>
        public void Read(int period, int today)
        {
            Read(period, today, null);
        }

        /// <summary>
        /// The same, keeping only the movements whose data 'keep' says are wanted (a bank's own, a card's): every figure
        /// (the kinds, the totals, the largest, the count) is of those kept; <see cref="OldestDay"/> is of the whole history.
        /// </summary>
        public void Read(int period, int today, Func<Dictionary<string, string>, bool> keep)
        {
            Kinds.Clear();
            _index.Clear();
            Entries.Clear();
            Array.Clear(CategoryIn, 0, CategoryIn.Length);
            Array.Clear(CategoryOut, 0, CategoryOut.Length);
            TopCount = 0;
            In = Out = 0f;
            Count = 0;
            OldestDay = 0;
            period = Mathf.Clamp(period, 0, PeriodDays.Length - 1);
            ToDay = period == Yesterday ? today - 1 : today;
            FromDay = ToDay - PeriodDays[period] + 1;
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
                if (day < FromDay || day > ToDay || t.amount == 0f || (keep != null && !keep(t.transactionData)))
                {
                    continue;
                }
                string type = t.transactionType ?? string.Empty;
                int at;
                if (!_index.TryGetValue(type, out at))
                {
                    at = Kinds.Count;
                    _index[type] = at;
                    Kinds.Add(new Kind { Type = type });
                }
                Kind kind = Kinds[at];
                kind.Count++;
                int category = CashCategory.For(type);
                Entries.Add(new CashEntry { Type = type, Data = t.transactionData, Amount = t.amount, Day = day, Category = category });
                if (t.amount > 0f)
                {
                    kind.In += t.amount;
                    In += t.amount;
                    CategoryIn[category] += t.amount;
                }
                else
                {
                    kind.Out += t.amount;
                    Out += t.amount;
                    CategoryOut[category] += t.amount;
                }
                Kinds[at] = kind;
                Count++;
                Rank(t, day);
            }
            Kinds.Sort(ByWeight);
        }

        // Keeps the five largest single transactions, largest first.
        private void Rank(Transaction t, int day)
        {
            float size = Mathf.Abs(t.amount);
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
            Top[place] = new Item { Type = t.transactionType, Data = t.transactionData, Amount = t.amount, Day = day };
            TopCount = Mathf.Min(TopCount + 1, TopMost);
        }

        /// <summary>A period's name: "Yesterday", "Last 7 days".</summary>
        public static string PeriodName(int period)
        {
            return period == Yesterday ? FinanceText.T("reallife_econ_period_yesterday") : FinanceText.T("reallife_econ_period_days", "days", PeriodDays[period].ToString());
        }

        /// <summary>The game's own label for a type of transaction (EconoViewFullTransactions lists them by it).</summary>
        public static string KindName(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return FinanceText.T("reallife_econ_cash_other");
            }
            string label = type + "_label";
            return LocalizorManager.IsLocalizedKey(label) ? label.GetLocalization()
                : LocalizorManager.IsLocalizedKey(type) ? type.GetLocalization() : FinanceText.T("reallife_econ_cash_other");
        }

        /// <summary>One transaction in the game's own words, as EconoView's list writes it (the type's text, filled from the transaction's data).</summary>
        public static string ItemName(in Item item)
        {
            return ItemName(item.Type, item.Data);
        }

        /// <summary>The same for a type and its data.</summary>
        public static string ItemName(string type, Dictionary<string, string> data)
        {
            if (string.IsNullOrEmpty(type) || !LocalizorManager.IsLocalizedKey(type))
            {
                return KindName(type);
            }
            return data != null && data.Count > 0 ? type.Localize(data).ToString() : type.GetLocalization();
        }
    }

    /// <summary>The words the apps' tables of money are written in (CashTable): the game's own, as the tabs always wrote them.</summary>
    public sealed class CashWords : ICashWords
    {
        public static readonly CashWords Game = new CashWords();

        public string Day(int day) => FinanceText.Day(day);
        public string Item(string type, Dictionary<string, string> data) => CashFlow.ItemName(type, data);
        public string Category(int category) => FinanceText.T(CashCategory.Keys[category]);
        public string Money(float amount) => FinanceText.Money(amount);
    }
}
