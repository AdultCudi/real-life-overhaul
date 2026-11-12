using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Where the layout put each scrolling block, so the wheel and a focus reach the one they belong to. Written back by every layout.</summary>
    public sealed class FinanceLayout
    {
        public const int Slots = 4;   // the first table, the second, the third and the statement
        public readonly float[] Offset = new float[Slots];
        public readonly int[] Reveal = { -1, -1, -1, -1 };
        public readonly short[] List = { -1, -1, -1, -1 };
        public readonly short[] Clip = { -1, -1, -1, -1 };

        public void Reset()
        {
            for (int i = 0; i < Slots; i++)
            {
                List[i] = -1;
                Clip[i] = -1;
            }
        }

        public int SlotOfClip(int clip)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (clip >= 0 && Clip[i] == clip)
                {
                    return i;
                }
            }
            return -1;
        }

        public int SlotOfList(int list)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (list >= 0 && List[i] == list)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// The Finance app on the kit: an app window with a sidebar in two groups (your money, and the city), and a page for each of the
    /// seven tabs. Each page is its own layout of its own section of the facts, so no tab can show another's parts. Game-free: the facts
    /// say what, this says where. Cost: a layout when the facts or a choice change; nothing while idle.
    /// </summary>
    public static partial class FinanceFace
    {
        public const int Tabs = 7;
        public const int Overview = 0, Credit = 1, Borrow = 2, CashFlow = 3, Accounts = 4, Rivals = 5, Standing = 6;

        public const int TableA = UiDataTable.First, TableB = 600, TableC = 700;
        public const int CardAct = 810, StepLess = 811, StepMore = 812;
        public const int AmountLess = 820, AmountType = 821, AmountMore = 822;
        public const int Period = 830, Answer = 831, Link = 832;
        public const int StepDeposit = 0, StepTerm = 1, StepPick = 2;
        public const int ArgAmount = 0, ArgPay = 1;

        /// <summary>Every command the screen answers, for the checks: the tables' own ranges and the buttons.</summary>
        public static readonly int[] Commands = AllCommands();

        private static int[] AllCommands()
        {
            var list = new List<int>
            {
                UiAppWindow.NavCommand, UiAppWindow.ActionCommand, CardAct, StepLess, StepMore, AmountLess, AmountType, AmountMore, Period, Answer, Link,
                UiChart.First + UiChart.Point, UiStatement.First + UiStatement.Period, UiStatement.First + UiStatement.Group, UiStatement.First + UiStatement.Line
            };
            foreach (int first in new[] { TableA, TableB, TableC })
            {
                for (int i = 0; i <= UiDataTable.MassAnswer; i++)
                {
                    list.Add(first + i);
                }
            }
            return list.ToArray();
        }

        public static void Lay(UiScene s, FinanceFacts f, FinanceView v, UtilitiesWords w, FinanceLayout lay, UiFrame frame, bool dark, IUiKeys keys)
        {
            lay.Reset();
            s.Begin("finance", frame.Width, frame.Height, dark);
            UiNavEntry[] nav = Nav(f, w);
            int tab = Math.Max(0, Math.Min(Tabs - 1, v.Tab));
            UiAt at = UiAppWindow.Shell(s, frame, w.T("reallife_econ_app", "Finance"), UiIcon.Wallet, nav, tab, false, keys, out float x, out float y, out float cw, out float bottom);
            if (!f.Ready)
            {
                UiDraw.EmptyState(s, at, x, y, cw, bottom - y, UiIcon.Wallet, w.T("reallife_econ_off_title", "Finance is not running"),
                    w.T("reallife_econ_off_body", "Load a save with the economy switched on to see your money."), null, -1, keys);
            }
            else
            {
                switch (tab)
                {
                    case Overview: OverviewPage(s, at, f, w, lay, x, y, cw, bottom, keys); break;
                    case Credit: CreditPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case Borrow: BorrowPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case CashFlow: CashPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case Accounts: AccountsPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    case Rivals: RivalsPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                    default: StandingPage(s, at, f, v, w, lay, x, y, cw, bottom, keys); break;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                s.AddPrompt(i == 0 ? UiAction.Move : i == 1 ? UiAction.Confirm : UiAction.Back,
                    w.T(i == 0 ? "reallife_ui_prompt_move" : i == 1 ? "reallife_ui_prompt_select" : "reallife_ui_prompt_back", i == 0 ? "Move" : i == 1 ? "Select" : "Back"));
            }
            if (f.Ready && Main(f, v, w, out UiActionModel main) && !main.Disabled)
            {
                s.AddPrompt(UiAction.Alt, main.Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The sidebar: your money, then the city.</summary>
        public static UiNavEntry[] Nav(FinanceFacts f, UtilitiesWords w)
        {
            return new[]
            {
                new UiNavEntry { Icon = UiIcon.Home, Label = w.T("reallife_econ_tab_overview", "Overview"), Group = w.T("reallife_fin_group_money", "Your money") },
                new UiNavEntry { Icon = UiIcon.Star, Label = w.T("reallife_econ_tab_credit", "Credit") },
                new UiNavEntry { Icon = UiIcon.Bank, Label = w.T("reallife_econ_tab_borrow", "Borrow") },
                new UiNavEntry { Icon = UiIcon.ChartLineUp, Label = w.T("reallife_econ_tab_cash", "Cash flow") },
                new UiNavEntry { Icon = UiIcon.Wallet, Label = w.T("reallife_econ_tab_accounts", "Accounts") },
                new UiNavEntry { Icon = UiIcon.Trophy, Label = w.T("reallife_money_nav_rivals", "Rivals"), Group = w.T("reallife_fin_group_city", "And the city"),
                    Count = f != null && f.RivalsCount > 0 ? f.RivalsCount.ToString() : null },
                new UiNavEntry { Icon = UiIcon.Flag, Label = w.T("reallife_fame_title", "Standing") }
            };
        }

        // ------------------------------------------------------------------------------------------------- shared parts

        private static int Table(UiScene s, UiAt at, FinanceLayout lay, int slot, UiDataTableModel m, float x, float y, float w, float h, IUiKeys keys, int command)
        {
            if (m == null || h < UiTokens.S16)
            {
                return -1;
            }
            m.Offset = lay.Offset[slot];
            m.Reveal = lay.Reveal[slot];
            lay.Reveal[slot] = -1;
            short list = UiDataTable.Lay(s, at, m, x, y, w, h, keys, command);
            lay.Offset[slot] = m.Offset;
            lay.List[slot] = list;
            lay.Clip[slot] = list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;
            return list;
        }

        private static float Title(UiScene s, UiAt at, string words, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(words))
            {
                return y;
            }
            UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w, UiStyle.SectionHeader, UiRole.Text, words, UiFlags.Label | UiFlags.Truncate);
            return y + UiTokens.Control;
        }

        private static float Sentence(UiScene s, UiAt at, string words, float x, float y, float w, UiRole ink = UiRole.Text2, int lines = 3)
        {
            if (string.IsNullOrEmpty(words))
            {
                return y;
            }
            int id = UiDraw.Text(s, at, x, y, w, UiStyle.Body, ink, words, UiFlags.Wrap | UiFlags.Truncate, lines);
            return UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S2;
        }

        /// <summary>A line with words at the left and others at the right, each in a box of its own so they never overlap.</summary>
        private static void Pair(UiScene s, UiAt at, float x, float y, float h, float w, UiStyle leftStyle, UiRole leftInk, string left, UiStyle rightStyle, UiRole rightInk, string right)
        {
            float rw = string.IsNullOrEmpty(right) ? 0f : Math.Min(UiDraw.SnapDown(w * 0.6f), UiDraw.Snap(UiTextRules.Width(right, rightStyle) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x, y, h, w - rw - (rw > 0f ? UiTokens.S3 : 0f), leftStyle, leftInk, left ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            if (rw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - rw, y, h, rw, rightStyle, rightInk, right, UiFlags.Right | UiFlags.Truncate);
            }
        }

        private static UiAt Band(UiScene s, UiAt at, float x, float y, float w, float h)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            return at.In(band);
        }

        /// <summary>A band with a title and rows of a label and its value; the rows that would not fit are left out. Returns its height.</summary>
        private static float Facts(UiScene s, UiAt at, string title, string[] labels, string[] values, float x, float y, float w, float limit, UiRole[] inks = null)
        {
            int rows = Math.Min(labels.Length, values.Length);
            float want = UiTokens.S2 + (string.IsNullOrEmpty(title) ? 0f : UiTokens.Control) + rows * (UiTokens.PairRow + UiTokens.S1) + UiTokens.S3;
            float h = UiDraw.Snap(Math.Min(limit, want));
            float least = UiTokens.S2 + (string.IsNullOrEmpty(title) ? 0f : UiTokens.Control) + UiTokens.PairRow + UiTokens.S3;
            if (rows == 0 || h < least)
            {
                return 0f;
            }
            UiAt on = Band(s, at, x, y, w, h);
            float pad = UiTokens.S4, ix = x + pad, iw = w - pad * 2f;
            float cy = Title(s, on, title, ix, y + UiTokens.S2, iw);
            for (int i = 0; i < rows && cy + UiTokens.PairRow <= y + h; i++)
            {
                UiDraw.InfoRow(s, on, ix, cy, iw, labels[i], values[i], false, -1, 0, inks != null && i < inks.Length && inks[i] != UiRole.None ? inks[i] : UiRole.Text);
                cy += UiTokens.PairRow + UiTokens.S1;
            }
            return h;
        }

        private static float Buttons(UiScene s, UiAt at, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            float right = x + w;
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref actions[i];
                float bw = Math.Min(w, UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys));
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            return y + UiTokens.Control;
        }

        private static UiActionModel Act(string label, UiButtonKind kind, UiIcon icon, UiAction key, FinanceAct act, int arg = 0, bool disabled = false)
        {
            return new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = key, Command = CardAct, Arg = arg != 0 ? (int)act * 1000 + arg : (int)act, Disabled = disabled };
        }

        /// <summary>The act and its argument a CardAct target carries (an argument rides in the thousands).</summary>
        public static void Split(int arg, out FinanceAct act, out int value)
        {
            act = (FinanceAct)(arg / 1000 > 0 ? arg / 1000 : arg);
            value = arg / 1000 > 0 ? arg % 1000 : 0;
        }

        private static string Number(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The one main action the page has, which F runs (Alt); false where it has none.</summary>
        public static bool Main(FinanceFacts f, FinanceView v, UtilitiesWords w, out UiActionModel main)
        {
            main = default(UiActionModel);
            switch (v.Tab)
            {
                case Credit:
                    main = Act(w.T("reallife_money_apply", "Apply"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, FinanceAct.Apply);
                    main.Disabled = f.Credit.Offers == null || f.Credit.Offers.RowCount == 0;
                    return true;
                case Borrow:
                    if (f.Borrow.Quote == null || string.IsNullOrEmpty(f.Borrow.Quote.TakeLabel))
                    {
                        return false;
                    }
                    main = Act(f.Borrow.Quote.TakeLabel, UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, FinanceAct.Take);
                    main.Disabled = !f.Borrow.Quote.CanTake;
                    return true;
                case Accounts:
                    return LastAlt(f.Accounts.Card, out main);
                case Rivals:
                    if (f.Rivals.Answers.Length == 0)
                    {
                        return false;
                    }
                    main = new UiActionModel { Label = w.T("reallife_fin_answer", "Answer"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = Answer, Arg = -1 };
                    return true;
                case Standing:
                    return LastAlt(f.Standing.Card, out main);
                default:
                    return false;
            }
        }

        /// <summary>The second action, which P runs (Option): the Economy app, Follow, Export; false where there is none.</summary>
        public static bool Second(FinanceFacts f, FinanceView v, UtilitiesWords w, out UiActionModel second)
        {
            second = default(UiActionModel);
            switch (v.Tab)
            {
                case Overview:
                    if (!f.Overview.EconomyApp)
                    {
                        return false;
                    }
                    second = Act(w.T("reallife_money_economy", "Economy"), UiButtonKind.Secondary, UiIcon.Locate, UiAction.Option, FinanceAct.Economy);
                    return true;
                case CashFlow:
                    second = Act(f.Cash.ExportLabel ?? w.T("reallife_money_export", "Export"), UiButtonKind.Secondary, UiIcon.ClipboardText, UiAction.Option, FinanceAct.Export);
                    return true;
                case Rivals:
                    if (!f.Rivals.CanFollow)
                    {
                        return false;
                    }
                    second = Act(f.Rivals.FollowLabel ?? w.T("reallife_money_follow", "Follow"), UiButtonKind.Secondary, UiIcon.Megaphone, UiAction.Option, FinanceAct.Follow);
                    return true;
                default:
                    return false;
            }
        }

        private static bool LastAlt(UtilitiesCard card, out UiActionModel main)
        {
            main = default(UiActionModel);
            if (card == null)
            {
                return false;
            }
            for (int i = card.Actions.Length - 1; i >= 0; i--)
            {
                if (card.Actions[i].Key == UiAction.Alt)
                {
                    main = card.Actions[i];
                    return true;
                }
            }
            return false;
        }
    }
}
