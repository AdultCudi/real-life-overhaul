using System;
using System.Globalization;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A population view (PopulationViews, PopDemandViews: the city, a district, a building, a household, a person, a
    /// business, who contributes, the demands) as the kit's dashboard: its figures on top, its list as a table of whole
    /// rows, its details at the side, its one primary action where tabs would be. Names come in from the game's own name
    /// maker and money in the game's form. Game-free. Cost: a model a view, when a screen opens or moves.
    /// </summary>
    public static class PopulationPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static UiDashboardModel Model(PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf)
        {
            var m = new UiDashboardModel
            {
                Title = Title(v.Title, v.TitleKey, v.NameSeed, v.Female, t, nameOf),
                Subtitle = string.IsNullOrEmpty(v.Subtitle) ? null : t(v.Subtitle, v.Subtitle),
                TableTitle = string.IsNullOrEmpty(v.RowsHeader) ? null : t(v.RowsHeader, v.RowsHeader),
                SideTitle = t("reallife_ui_page_details", "Details"),
            };
            int count = Math.Min(UiDashboard.MostFigures, v.Hero.Count);
            m.Figures = new UiFigureModel[count];
            for (int i = 0; i < count; i++)
            {
                m.Figures[i] = new UiFigureModel { Label = t(v.Hero[i].Label, v.Hero[i].Label), Value = Write(v.Hero[i], t, money) };
            }
            m.FigureCount = count;
            m.Table = new UiTableModel
            {
                Columns = new[]
                {
                    new UiColumn { Title = m.TableTitle ?? string.Empty },
                    new UiColumn { Title = string.Empty },
                    new UiColumn { Title = string.Empty, Width = UiTokens.S16 * 3f, Right = true },
                },
                EmptyTitle = t(v.Empty ?? "reallife_ui_page_empty", "Nothing here yet"),
                EmptyBody = null,
            };
            m.Table.Rows = new string[v.Rows.Count][];
            for (int i = 0; i < v.Rows.Count; i++)
            {
                ViewRow r = v.Rows[i];
                m.Table.Rows[i] = new[]
                {
                    Title(r.Title, r.TitleKey, r.NameSeed, r.Female, t, nameOf),
                    string.IsNullOrEmpty(r.Subtitle) ? string.Empty : t(r.Subtitle, r.Subtitle),
                    Write(ViewFigure.Of(null, r.Value, r.Format), t, money)
                };
            }
            m.Table.RowCount = v.Rows.Count;
            m.Table.Selected = v.Rows.Count > 0 ? 0 : -1;
            m.Side = new UiPairModel[v.Details.Count];
            for (int i = 0; i < v.Details.Count; i++)
            {
                m.Side[i] = new UiPairModel { Key = t(v.Details[i].Label, v.Details[i].Label), Value = Write(v.Details[i], t, money) };
            }
            m.SideCount = m.Side.Length;
            m.HasAction = !string.IsNullOrEmpty(v.Primary) && v.PrimaryTarget.Valid;
            if (m.HasAction)
            {
                m.Action = new UiActionModel { Label = t(v.Primary, v.Primary), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand };
            }
            m.Prompts = m.HasAction
                ? new[]
                {
                    new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                    new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                    new UiPrompt { Action = UiAction.Alt, Label = m.Action.Label },
                    new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
                }
                : new[]
                {
                    new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                    new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                    new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
                };
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        /// <summary>A figure in words: a count, money in the game's form, a share as a per cent, a key's words, years or hours.</summary>
        public static string Write(ViewFigure f, UiWords t, Func<long, string> money)
        {
            switch (f.Format)
            {
                case ViewFormat.Money: return money(f.Value);
                case ViewFormat.Permille: return (f.Value / 10.0).ToString("0.#", Inv) + "%";
                case ViewFormat.Key: return string.IsNullOrEmpty(f.Text) ? string.Empty : t(f.Text, f.Text);
                default: return f.Value.ToString("#,0", Inv);
            }
        }

        private static string Title(string title, bool key, int seed, bool female, UiWords t, Func<int, bool, string> nameOf)
        {
            if (seed != 0 && nameOf != null)
            {
                return nameOf(seed, female);
            }
            return key ? t(title, title) : title ?? string.Empty;
        }
    }
}
