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

        public const int Yesterday = 0, Week = 1, Month = 2;
        public static readonly int[] PeriodDays = { 1, 7, 30 };
        public const int TopMost = 5;

        private static readonly Comparison<Kind> ByWeight = (a, b) => Mathf.Abs(b.Net).CompareTo(Mathf.Abs(a.Net));

        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(48);

        public readonly List<Kind> Kinds = new List<Kind>(48);
        public readonly Item[] Top = new Item[TopMost];
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
            Kinds.Clear();
            _index.Clear();
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
                if (day < FromDay || day > ToDay || t.amount == 0f)
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
                if (t.amount > 0f)
                {
                    kind.In += t.amount;
                    In += t.amount;
                }
                else
                {
                    kind.Out += t.amount;
                    Out += t.amount;
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
            if (string.IsNullOrEmpty(item.Type) || !LocalizorManager.IsLocalizedKey(item.Type))
            {
                return KindName(item.Type);
            }
            return item.Data != null && item.Data.Count > 0 ? item.Type.Localize(item.Data).ToString() : item.Type.GetLocalization();
        }
    }
}
