using System;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A view of Flare in the city (FlareCityViews: an account, a business, its funnel, the offers, the trends) as the
    /// kit's dashboard: its figures on top, its list as a table of whole rows, its details at the side, its one primary
    /// action where tabs would be when it has no tabs. Residents' names come in from Flare's own name maker. Game-free.
    /// Cost: a model a view, when a screen opens or moves.
    /// </summary>
    public static class FlareCityPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static UiDashboardModel Model(FlareCityView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, string[] tabs, int tab)
        {
            var m = new UiDashboardModel
            {
                Title = v.TitleKey ? t(v.Title, v.Title) : v.Title ?? string.Empty,
                Subtitle = string.IsNullOrEmpty(v.Subtitle) ? null : t(v.Subtitle, v.Subtitle),
                SideTitle = t("reallife_ui_page_details", "Details"),
                Tabs = tabs ?? new string[0],
                Tab = tab,
            };
            int count = Math.Min(UiDashboard.MostFigures, v.Hero.Count);
            m.Figures = new UiFigureModel[count];
            for (int i = 0; i < count; i++)
            {
                FlareViewFigure f = v.Hero[i];
                m.Figures[i] = new UiFigureModel { Label = t(f.Label, f.Label), Value = Write(f.Value, f.Format, f.Text, t, money), Change = null };
            }
            m.FigureCount = count;
            m.Table = new UiTableModel
            {
                Columns = new[]
                {
                    new UiColumn { Title = string.IsNullOrEmpty(v.RowsHeader) ? string.Empty : t(v.RowsHeader, v.RowsHeader) },
                    new UiColumn { Title = string.Empty },
                    new UiColumn { Title = string.Empty, Width = UiTokens.S16 * 3f, Right = true },
                },
                EmptyTitle = t(v.Empty ?? "reallife_ui_page_empty", "Nothing here yet"),
            };
            m.Table.Rows = new string[v.Rows.Count][];
            for (int i = 0; i < v.Rows.Count; i++)
            {
                FlareViewRow r = v.Rows[i];
                string title = r.Person != 0 && nameOf != null ? nameOf(r.Person, r.Female) : r.TitleKey ? t(r.Title, r.Title) : r.Title ?? string.Empty;
                if (r.What != null)
                {
                    title = title.Replace("{what}", t(r.What, r.What)).Replace("{where}", r.Where != null ? t(r.Where, r.Where) : string.Empty);
                }
                string middle = !string.IsNullOrEmpty(r.Subtitle) ? t(r.Subtitle, r.Subtitle) : r.Second != 0 ? money(r.Second) : string.Empty;
                m.Table.Rows[i] = new[] { title, middle, Write(r.Value, r.Format, null, t, money) };
            }
            m.Table.RowCount = v.Rows.Count;
            m.Table.Selected = v.Rows.Count > 0 ? 0 : -1;
            m.Side = new UiPairModel[v.Details.Count];
            for (int i = 0; i < v.Details.Count; i++)
            {
                FlareViewFigure f = v.Details[i];
                m.Side[i] = new UiPairModel { Key = t(f.Label, f.Label), Value = Write(f.Value, f.Format, f.Text, t, money) };
            }
            m.SideCount = m.Side.Length;
            m.HasAction = !string.IsNullOrEmpty(v.Primary) && m.Tabs.Length == 0;
            if (m.HasAction)
            {
                m.Action = new UiActionModel { Label = t(v.Primary, v.Primary), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand };
            }
            var prompts = new System.Collections.Generic.List<UiPrompt>(5)
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            };
            if (m.Tabs.Length > 0)
            {
                prompts.Add(new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") });
            }
            if (m.HasAction)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = m.Action.Label });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
            m.Prompts = prompts.ToArray();
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        /// <summary>A figure in words: a count, money, a share as a signed per cent, a key's words, stars out of five, a signed count.</summary>
        public static string Write(long value, FlareViewFormat format, string text, UiWords t, Func<long, string> money)
        {
            switch (format)
            {
                case FlareViewFormat.Money: return money(value);
                case FlareViewFormat.Permille: return (value > 0 ? "+" : string.Empty) + (value / 10.0).ToString("0.#", Inv) + "%";
                case FlareViewFormat.Key: return string.IsNullOrEmpty(text) ? string.Empty : t(text, text);
                case FlareViewFormat.Stars: return (value / 10.0).ToString("0.0", Inv) + " / 5";
                case FlareViewFormat.Signed: return (value > 0 ? "+" : string.Empty) + value.ToString("#,0", Inv);
                default: return value.ToString("#,0", Inv);
            }
        }
    }
}
