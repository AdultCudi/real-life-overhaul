using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The cards of a page as they are built: each with the steps it holds and the act its switch does, so a press can be carried to the one that owns it.</summary>
    public sealed class BankCards
    {
        private readonly List<UtilitiesCard> _cards = new List<UtilitiesCard>(4);
        private readonly List<MoneyStep[]> _steps = new List<MoneyStep[]>(4);
        private readonly List<int> _switches = new List<int>(4);

        /// <summary>The place the next card will have: what a step's typed field names it by.</summary>
        public int Next => _cards.Count;

        public void Add(UtilitiesCard card, MoneyStep[] steps = null, MoneyAct switchAct = MoneyAct.None)
        {
            _cards.Add(card);
            _steps.Add(steps ?? new MoneyStep[0]);
            _switches.Add((int)switchAct);
        }

        public void Into(MoneyModel m)
        {
            m.Cards = _cards.ToArray();
            m.CardSteps = _steps.ToArray();
            m.CardSwitchActs = _switches.ToArray();
        }
    }

    /// <summary>The small pieces a bank page is built from: lines, buttons, steps, tables and a statement. Game-free.</summary>
    public static class BankParts
    {
        public static readonly int[] AmountSteps = { 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000 };

        public static UtilitiesLine Pair(string label, string value, UiRole ink = UiRole.Text) => new UtilitiesLine { Label = Plain(label), Value = value, Ink = ink };

        /// <summary>A label without the colon the game's old labels end in, which a card's rows do not want.</summary>
        public static string Plain(string label) => label == null ? null : label.TrimEnd(' ', ':');

        /// <summary>The words of one key or the other by a condition, with names filled in; both keys are written out so each is a literal.</summary>
        public static string Either(UtilitiesWords w, bool first, string keyA, string englishA, string keyB, string englishB, params string[] pairs)
        {
            return pairs.Length > 0 ? (first ? w.F(keyA, englishA, pairs) : w.F(keyB, englishB, pairs)) : first ? w.T(keyA, englishA) : w.T(keyB, englishB);
        }

        /// <summary>The page's labels without that colon: the figures, the cards, the steps, the heads of the tables.</summary>
        public static void Tidy(MoneyModel m)
        {
            for (int i = 0; i < m.Figures.Length; i++)
            {
                m.Figures[i].Label = Plain(m.Figures[i].Label);
            }
            for (int k = 0; k < m.Cards.Length; k++)
            {
                m.Cards[k].Title = Plain(m.Cards[k].Title);
                m.Cards[k].Heading = true;   // a card's title is a section's label: a page holds a profile's name,
                // a figure and small type, no more
                for (int i = 0; i < m.Cards[k].Steps.Length; i++)
                {
                    m.Cards[k].Steps[i].Label = Plain(m.Cards[k].Steps[i].Label);
                }
            }
            if (m.Profile != null)
            {
                for (int i = 0; i < m.Profile.FactLabels.Length; i++)
                {
                    m.Profile.FactLabels[i] = Plain(m.Profile.FactLabels[i]);
                }
            }
            if (m.Table != null)
            {
                for (int i = 0; i < m.Table.Columns.Length; i++)
                {
                    m.Table.Columns[i].Title = Plain(m.Table.Columns[i].Title);
                }
            }
            if (m.Table2 != null)
            {
                for (int i = 0; i < m.Table2.Columns.Length; i++)
                {
                    m.Table2.Columns[i].Title = Plain(m.Table2.Columns[i].Title);
                }
            }
        }

        public static UtilitiesLine Say(string text, UiRole ink = UiRole.Text2) => new UtilitiesLine { Value = text, Ink = ink };

        public static UtilitiesLine Check(string text, bool met) => new UtilitiesLine { Value = text, Check = true, Met = met, Ink = met ? UiRole.Text2 : UiRole.WarnText };

        public static UiActionModel Button(string label, UiButtonKind kind, UiIcon icon, MoneyAct act, bool main, bool disabled = false, int id = 0)
        {
            // The act in the low byte, the thing it is for (an account's number) above it.
            return new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = main ? UiAction.Alt : UiAction.Count, Command = MoneyCommands.CardAct, Arg = (int)act | (id << 8),
                Disabled = disabled };
        }

        /// <summary>A button that opens a tab of the app.</summary>
        public static UiActionModel Link(string label, UiIcon icon, int tab)
        {
            return new UiActionModel { Label = label, Kind = UiButtonKind.Secondary, Icon = icon, Key = UiAction.Count, Command = MoneyCommands.Go, Arg = tab };
        }

        public static UiRole Tone(bool good) => good ? UiRole.Ok : UiRole.Danger;

        /// <summary>The tone of a share used of a limit: green, amber from half, red from nine tenths.</summary>
        public static UiRole Used(float share) => share >= 0.9f ? UiRole.DangerText : share >= 0.5f ? UiRole.WarnText : UiRole.OkText;

        /// <summary>An amount a card steps up and down on the round sums and takes typed: the step's figure, and what it does in the view.</summary>
        public static UtilitiesStep AmountStep(UtilitiesWords w, string label, int value, int least, int most, int field, int card, int index, string hint, out MoneyStep move,
            int[] ladder = null, int multiple = 0)
        {
            int[] steps = ladder ?? AmountSteps;
            int up = FinanceRules.StepAmount(value, least, most, true, steps), down = FinanceRules.StepAmount(value, least, most, false, steps);
            move = new MoneyStep
            {
                Act = MoneyAct.None, Field = field, Less = down, More = up, CanLess = down >= 0, CanMore = up >= 0, Typed = true, Low = least, High = Math.Max(least, most), Multiple = multiple
            };
            return new UtilitiesStep
            {
                Label = label, Value = w.Money(value), Hint = hint, CanLess = down >= 0, CanMore = up >= 0, TypeCommand = MoneyCommands.TypeField, TypeArg = card * 8 + index
            };
        }

        /// <summary>A choice among a few (a term, a deposit, a bank, a level) stepped through: its words, and the values a minus and a plus take the figure to.</summary>
        public static UtilitiesStep ChoiceStep(string label, string text, string hint, int index, int count, int field, MoneyAct act, int id, int[] values, out MoneyStep move)
        {
            int less = index > 0 ? (values != null ? values[index - 1] : index - 1) : -1, more = index < count - 1 ? (values != null ? values[index + 1] : index + 1) : -1;
            move = new MoneyStep { Act = act, Field = field, Id = id, Less = less, More = more, CanLess = index > 0, CanMore = index < count - 1 };
            return new UtilitiesStep { Label = label, Value = text, Hint = hint, CanLess = index > 0, CanMore = index < count - 1 };
        }

        /// <summary>
        /// The payment a debt's page starts at, as the old app offered it: an overdraft the whole of what is owed, a card the least of the statement that
        /// covers the minimum, and any other the instalment or what is overdue. In an arrangement the whole of what is owed.
        /// </summary>
        public static int PaySuggest(int kind, int owed, bool frozen, int minimum, int statementLeft, int fallback)
        {
            if (frozen || kind == 2)
            {
                return owed;
            }
            return kind == 3 ? Math.Max(minimum, Math.Min(owed, statementLeft)) : fallback;
        }

        /// <summary>Where a figure starts: the view's, else the suggested one, kept to its range.</summary>
        public static int Chosen(int fromView, int suggested, int least, int most)
        {
            int amount = fromView > 0 ? fromView : suggested;
            return Math.Max(least, Math.Min(Math.Max(least, most), amount));
        }

        /// <summary>The start of an amount a form opens at: half of the most, on the round sums, never under the least.</summary>
        public static int DefaultAmount(int least, int most)
        {
            int pick = least;
            for (int i = 0; i < AmountSteps.Length; i++)
            {
                if (AmountSteps[i] >= least && AmountSteps[i] <= most / 2)
                {
                    pick = AmountSteps[i];
                }
            }
            return Math.Max(least, Math.Min(most, pick));
        }

        public static UiDataTableModel Table(string[] titles, UiCellKind[] kinds, List<UiDataRow> rows, int selected, string emptyTitle, string emptyBody, UiIcon icon)
        {
            var columns = new UiDataColumn[titles.Length];
            for (int i = 0; i < titles.Length; i++)
            {
                columns[i] = new UiDataColumn { Title = titles[i], Kind = kinds != null && i < kinds.Length ? kinds[i] : i == 0 ? UiCellKind.Text : UiCellKind.Figure };
            }
            return new UiDataTableModel
            {
                Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, Selected = selected >= 0 && selected < rows.Count ? selected : -1,
                EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon
            };
        }

        public static UiDataRow Row(string[] cells, UiRole[] tones = null) => new UiDataRow { Cells = cells, Tones = tones ?? new UiRole[cells.Length] };

        /// <summary>The rows a statement shows as one group of lines, oldest first, with the totals under it.</summary>
        public static UiStatementModel Statement(UtilitiesWords w, string account, string balanceLabel, string balance, string period, BankMove[] moves, float came, float went,
            string brought, string emptyTitle, string emptyBody, UiIcon icon)
        {
            var model = new UiStatementModel
            {
                Account = account, AccountIcon = icon, BalanceLabel = balanceLabel, Balance = balance, PeriodLabel = period,
                Columns = new[] { w.T("reallife_money_col_when", "When"), w.T("reallife_money_col_what", "What"), w.T("reallife_money_col_in", "In"),
                    w.T("reallife_money_col_out", "Out"), w.T("reallife_money_col_balance", "Balance") },
                EmptyTitle = emptyTitle, EmptyBody = emptyBody
            };
            if (moves.Length == 0)
            {
                return model;
            }
            var lines = new UiStatementLine[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                lines[i] = new UiStatementLine
                {
                    When = moves[i].Day, Icon = UiIcon.Coin, What = moves[i].What,
                    In = moves[i].Amount > 0f ? w.Money(moves[i].Amount) : null, Out = moves[i].Amount < 0f ? w.Money(-moves[i].Amount) : null, Balance = w.Money(moves[i].Balance)
                };
            }
            model.Groups = new[] { new UiStatementGroup { Label = brought, Total = w.Money(came - went), TotalGood = came >= went, Lines = lines } };
            model.TotalLabels = new[] { w.T("reallife_money_col_in", "In"), w.T("reallife_money_col_out", "Out") };
            model.Totals = new[] { w.Money(came), w.Money(-went) };
            model.ChangeGood = came >= went;
            return model;
        }

        /// <summary>A mark as the table's chip: its words in a tone.</summary>
        public static UiRole[] Tones(int n, int at, UiRole tone)
        {
            var tones = new UiRole[n];
            if (at >= 0 && at < n)
            {
                tones[at] = tone;
            }
            return tones;
        }
    }
}
