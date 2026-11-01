using System;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A view of Flare in the city (FlareCityViews: an account, a business, its funnel, the offers, the trends) as a feed,
    /// since Flare is a social app: its entries down a column with their faces (a post's business, a reviewer, a trend),
    /// the account's figures and facts at the side, and the tabs heading it. A business's page puts its name after the
    /// app's in the masthead and its own figures at the side. Only entries a press opens are targets. Residents' names come
    /// in from Flare's own name maker. Game-free. Cost: a model a view, when a screen opens or moves.
    /// </summary>
    public static class FlareCityPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// The feed of a view. <paramref name="account"/> is the account's own view, whose followers stand at the side of
        /// the tabs that have no figures of their own (the trends, the offers), so no tab shows an empty side.
        /// </summary>
        public static UiDashboardModel Model(FlareCityView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, string[] tabs, int tab,
            FlareCityView account = null, string here = null)
        {
            bool tabbed = tabs != null && tabs.Length > 0;
            var m = new UiDashboardModel
            {
                Shape = UiPageShape.Feed,
                Title = tabbed ? null : v.TitleKey ? t(v.Title, v.Title) : v.Title ?? string.Empty,   // a tab says where its page is: no title repeats it
                Subtitle = tabbed || string.IsNullOrEmpty(v.Subtitle) ? null : t(v.Subtitle, v.Subtitle),
                SideTitle = t("reallife_ui_page_details", "Details"),
                Tabs = tabs ?? new string[0],
                Tab = tab,
                EntriesTitle = string.IsNullOrEmpty(v.RowsHeader) ? null : t(v.RowsHeader, v.RowsHeader),
            };
            FlareCityView figures = v.Hero.Count == 0 && account != null ? account : v;
            int count = Math.Min(UiDashboard.MostFigures, figures.Hero.Count);
            m.Figures = new UiFigureModel[count];
            for (int i = 0; i < count; i++)
            {
                FlareViewFigure f = figures.Hero[i];
                m.Figures[i] = new UiFigureModel { Label = t(f.Label, f.Label), Value = Write(f.Value, f.Format, f.Text, t, money) };
            }
            m.FigureCount = count;
            m.Table = new UiTableModel { EmptyTitle = t(v.Empty ?? "reallife_ui_page_empty", "Nothing here yet") };
            m.Entries = new UiRowModel[v.Rows.Count];
            m.EntryLeads = new bool[v.Rows.Count];
            bool leads = false;
            for (int i = 0; i < v.Rows.Count; i++)
            {
                FlareViewRow r = v.Rows[i];
                string title = r.Person != 0 && nameOf != null ? nameOf(r.Person, r.Female) : r.TitleKey ? t(r.Title, r.Title) : r.Title ?? string.Empty;
                if (r.What != null)
                {
                    title = title.Replace("{what}", t(r.What, r.What)).Replace("{where}", r.Where != null ? t(r.Where, r.Where) : string.Empty);
                }
                title = UiPlaces.IsKey(title) ? UiPlaces.Words(title) : title;
                string middle = !string.IsNullOrEmpty(r.Subtitle) ? t(r.Subtitle, r.Subtitle) : r.Second != 0 ? money(r.Second) : string.Empty;
                string time = Write(r.Value, r.Format, null, t, money);
                // A post's and a trend's figures said in words: a bare "+38" beside a name told the player nothing (the critic, 1 Nov).
                if (v.Kind == FlareViewKind.Account)
                {
                    middle = t("reallife_ui_flare_did", "{what}: {came} came, {spent} spent").Replace("{what}", middle).Replace("{came}", r.Value.ToString("#,0", Inv))
                        .Replace("{spent}", money(r.Second));
                    time = string.Empty;
                }
                else if (v.Kind == FlareViewKind.Trends)
                {
                    middle = t("reallife_ui_flare_trend_did", "{stage}: {moved} moved, {days} days left").Replace("{stage}", Capital(middle))
                        .Replace("{moved}", r.Value.ToString("#,0", Inv)).Replace("{days}", r.Second.ToString("#,0", Inv));
                    time = string.Empty;
                }
                m.Entries[i] = new UiRowModel { Name = title, Preview = middle, Time = time, Hue = UiColour.HueOf(title) };
                m.EntryLeads[i] = !string.IsNullOrEmpty(r.Target);
                leads |= m.EntryLeads[i];
            }
            m.EntryCount = m.Entries.Length;
            m.Side = new UiPairModel[v.Details.Count];
            for (int i = 0; i < v.Details.Count; i++)
            {
                FlareViewFigure f = v.Details[i];
                m.Side[i] = new UiPairModel { Key = t(f.Label, f.Label), Value = Write(f.Value, f.Format, f.Text, t, money) };
            }
            m.SideCount = m.Side.Length;
            // Only an action that leads somewhere else stands on the page: an offer's accept and a business's ad are the
            // phone's Flare, and a button that opens the page it stands on would do nothing.
            m.HasAction = !string.IsNullOrEmpty(v.Primary) && !string.IsNullOrEmpty(v.PrimaryTarget) && v.PrimaryTarget != here && !tabbed;
            if (m.HasAction)
            {
                m.Action = new UiActionModel { Label = t(v.Primary, v.Primary), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand };
            }
            var prompts = new System.Collections.Generic.List<UiPrompt>(5) { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
            if (leads)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") });
            }
            if (tabbed)
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

        private static string Capital(string words) => string.IsNullOrEmpty(words) ? string.Empty : char.ToUpper(words[0], CultureInfo.CurrentCulture) + words.Substring(1);

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
