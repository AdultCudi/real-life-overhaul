using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A line of the game's leaderboard: a rival or the player, in the game's order (by weekly income).</summary>
    public struct RivalEntry
    {
        public string Name;
        public bool You;
        public bool Defeated;
        public bool Rivalry;         // a rival at war with the player now
        public float WeeklyIncome;
        public int Businesses;
        public float WeekAgo;        // their weekly income a week before, or NaN where the game has no history yet
        public string District;      // the district they are busiest in, in the game's words; null for none
        public int Age;
    }

    /// <summary>One of the chosen rival's businesses as the game's table holds it.</summary>
    public struct RivalBusiness
    {
        public string Name;
        public string Type;          // the business type in the game's words
        public string TypeKey;       // its key, for its glyph
        public float WeeklyIncome;
        public string Address;
    }

    /// <summary>One of the chosen rival's buildings as the game's table holds it.</summary>
    public struct RivalBuilding
    {
        public string Address;
        public string District;
        public string Type;
        public string TypeKey;
        public string Size;          // in the game's area words
        public float Value;
        public float Price;          // 0 while it is not for sale
    }

    /// <summary>
    /// The Rivals app as the game filled it: its leaderboard (rivals and the player), the chosen one's development as the
    /// game charts it under its filter, their rivalry's lines, their businesses and buildings in the game's order and sort,
    /// and the row the cursor is on.
    /// </summary>
    public sealed class RivalsView
    {
        public int Tab;
        public RivalEntry[] League = new RivalEntry[0];
        public int Chosen;                          // the leaderboard's line the game shows
        public bool Contacts;                       // the game offers Open in Contacts for them
        public string[] Filters = new string[0];    // the chart's filters in the game's words
        public int Filter;
        public float[] Chart = new float[0];        // the chart's values, oldest first
        public int[] ChartDays = new int[0];        // the day each value is of
        public bool ChartMoney;                     // the chart is in money (the weekly income), not a count
        public int ChartFocus = -1;
        public UiRivalThreat[] Threats = new UiRivalThreat[0];
        public RivalBusiness[] Businesses = new RivalBusiness[0];
        public RivalBuilding[] Buildings = new RivalBuilding[0];
        public int SortColumn = -1;
        public bool SortDown;
        public int Selected;                        // the row of the tab's table the cursor is on
        public float Offset;
        public int Reveal = -1;
        public Func<float, string> Money;           // a sum in the game's short money words
    }

    /// <summary>
    /// The Rivals view into the app's model: the league and the chosen rival beside it, or their businesses and buildings.
    /// Game-free: the game's screen and the samples hand it the same view. Cost: a few small arrays a layout.
    /// </summary>
    public static class RivalsViews
    {
        public const string App = "Rivals";
        public const int ColumnRank = 0, ColumnChange = 4;
        private const float ChangeSpan = 0.5f;   // a week's change that grades as the best (or worst): half again, or half lost

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Rivals(UiRivalsModel m, UiWords t, RivalsView v)
        {
            m.Title = t("smartphone_rivals", "Rivals");
            m.Icon = PhoneLooks.Glyph(App);
            m.Tab = Math.Max(0, Math.Min(UiRivals.Buildings, v.Tab));
            int chosen = v.League.Length > 0 ? Math.Max(0, Math.Min(v.League.Length - 1, v.Chosen)) : -1;
            RivalEntry who = chosen >= 0 ? v.League[chosen] : default;
            m.Tabs = new[]
            {
                Tab(UiIcon.Trophy, t("rivals_leaderboard", "Leaderboard"), v.League.Length > 0 ? Whole(v.League.Length) : null),
                Tab(UiIcon.Store, t("common_businesses", "Businesses"), chosen >= 0 ? Whole(v.Businesses.Length) : null),
                Tab(UiIcon.Building, t("rivals_real_estate_properties", "Real Estate Properties"), chosen >= 0 ? Whole(v.Buildings.Length) : null),
            };
            m.LeagueTitle = t("rivals_overview", "Rivals Overview");
            Head(m, t, v, chosen, who);
            m.ChartTabs = v.Filters ?? new string[0];
            m.ChartTab = v.Filter;
            Chart(m.Chart, t, v);
            m.ThreatsTitle = t("reallife_ui_rivals_rivalry", "Their moves against you");
            m.Threats = v.Threats ?? new UiRivalThreat[0];
            m.Table = new UiDataTableModel();
            m.RowName = null;
            m.RowLink = null;
            m.RowActions = new UiActionModel[0];
            switch (m.Tab)
            {
                case UiRivals.Businesses:
                    BusinessTable(m, t, v);
                    break;
                case UiRivals.Buildings:
                    BuildingTable(m, t, v);
                    break;
                default:
                    LeagueTable(m.Table, t, v, chosen);
                    break;
            }
            m.Table.Offset = v.Offset;
            m.Table.Reveal = v.Reveal;
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_staff_prompt_tab", "Tab") }, new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            };
        }

        private static UiNavEntry Tab(UiIcon icon, string label, string count) => new UiNavEntry { Icon = icon, Tint = UiIconTints.Of(icon), Label = label, Count = count };

        // The chosen one's head: their face, their age, the district they are busiest in behind its badge, a state chip
        // (defeated, at war with the player, or the player themself), the weekly income and businesses, stars by their place.
        private static void Head(UiRivalsModel m, UiWords t, RivalsView v, int chosen, RivalEntry who)
        {
            UiProfileModel p = m.Profile;
            p.Name = chosen >= 0 ? who.Name : t("smartphone_rivals", "Rivals");
            p.Hue = UiColour.HueOf(p.Name ?? string.Empty);
            p.Ring = UiRole.None;   // a rival's mood is not the player's to know: the ring means mood, so none
            p.Role = chosen >= 0 ? StaffViews.Fill(t("common_years_amount", "{years} years"), "years", Whole(who.Age)) : null;
            p.RoleIcon = UiIcon.Person;
            p.RoleTint = UiRole.None;
            p.Employer = chosen >= 0 && !string.IsNullOrEmpty(who.District) ? who.District : null;
            p.EmployerIcon = UiIcon.MapPin;
            p.Status = chosen < 0 ? null : who.Defeated ? t("reallife_ui_rivals_defeated", "Defeated") : who.You ? t("reallife_ui_rivals_you", "You")
                : who.Rivalry ? t("rivals_defense_icons_active_rivalry_title", "Active Rivalry") : null;
            p.StatusTone = who.Defeated ? UiRole.Muted : who.You ? UiRole.Accent : UiRole.Danger;
            p.FactLabels = new[] { t("common_weekly_income", "Weekly Income"), t("common_businesses", "Businesses") };
            p.FactValues = chosen >= 0 ? new[] { Money(v, who.WeeklyIncome), Whole(who.Businesses) } : new[] { "", "" };
            p.RatingLabel = t("reallife_ui_rivals_strength", "Strength");
            p.Rating = chosen >= 0 ? Stars(chosen, v.League.Length, who.Defeated) : -1;
            m.ProfileActions = v.Contacts && chosen >= 0
                ? new[] { new UiActionModel { Label = t("open_in_contacts", "Open in Contacts"), Kind = UiButtonKind.Secondary, Icon = UiIcon.AddressBook, Key = UiAction.Option } }
                : new UiActionModel[0];
        }

        /// <summary>A place's stars, by halves: the top of the league five, the foot one half, a defeated rival none.</summary>
        public static int Stars(int place, int count, bool defeated)
        {
            if (defeated || count <= 0 || place < 0 || place >= count)
            {
                return defeated ? 0 : -1;
            }
            return count == 1 ? 10 : 1 + (int)Math.Round(9.0 * (count - 1 - place) / (count - 1));
        }

        /// <summary>A week's change as a share of where it stood (NaN without a week's history, or from nothing).</summary>
        public static float Change(float now, float weekAgo) => float.IsNaN(weekAgo) || Math.Abs(weekAgo) < 1f ? float.NaN : (now - weekAgo) / Math.Abs(weekAgo);

        /// <summary>A change graded 0 to 100: half lost the poorest, flat the middle, half again the best.</summary>
        public static int ChangeGrade(float change) => float.IsNaN(change) ? -1 : (int)Math.Round(50f + 50f * Math.Max(-1f, Math.Min(1f, change / ChangeSpan)));

        // The leaderboard: the rank (none for the defeated), a face with the name and district, the weekly income with its
        // arrow, the businesses, and the week's change graded.
        private static void LeagueTable(UiDataTableModel table, UiWords t, RivalsView v, int chosen)
        {
            table.Columns = new[]
            {
                new UiDataColumn { Title = "#", Kind = UiCellKind.Figure, Width = 40f },
                new UiDataColumn { Title = t("common_name", "Name"), Kind = UiCellKind.Person },
                new UiDataColumn { Title = t("common_weekly_income", "Weekly Income"), Kind = UiCellKind.Trend, Width = 152f },
                new UiDataColumn { Title = t("common_businesses", "Businesses"), Kind = UiCellKind.Figure, Width = 104f },
                new UiDataColumn { Title = t("reallife_ui_rivals_week", "Week"), Kind = UiCellKind.Figure, Width = 80f, Graded = true },
            };
            table.Rows = new UiDataRow[v.League.Length];
            int rank = 0;
            for (int i = 0; i < v.League.Length; i++)
            {
                ref RivalEntry e = ref v.League[i];
                rank++;
                float change = Change(e.WeeklyIncome, e.WeekAgo);
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        e.Defeated ? string.Empty : Whole(rank), e.Name, Money(v, e.WeeklyIncome), Whole(e.Businesses),
                        float.IsNaN(change) ? string.Empty : PersonaViews.Signed((int)Math.Round(change * 100f))
                    },
                    Values = new[] { -1, -1, float.IsNaN(change) || Math.Abs(change) < 0.005f ? 0 : Math.Sign(change), -1, ChangeGrade(change) },
                    Sub = e.Defeated ? t("reallife_ui_rivals_defeated", "Defeated") : e.You ? t("reallife_ui_rivals_you", "You") : e.District,
                    Hue = UiColour.HueOf(e.Name ?? string.Empty), Ring = UiRole.None
                };
            }
            table.RowCount = table.Rows.Length;
            table.Selected = chosen;
            table.EmptyIcon = UiIcon.Flag;
            table.EmptyTitle = t("reallife_ui_rivals_none", "No rivals yet");
            table.EmptyBody = t("reallife_ui_rivals_none_body", "The city's other tycoons show here once rivals are on in this game, ranked with you by their weekly income.");
        }

        private static void BusinessTable(UiRivalsModel m, UiWords t, RivalsView v)
        {
            UiDataTableModel table = m.Table;
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("common_business_name", "Business Name"), Kind = UiCellKind.Text, Sortable = true },
                new UiDataColumn { Title = t("common_business_type", "Business Type"), Kind = UiCellKind.Tile, Width = 256f, Sortable = true },
                new UiDataColumn { Title = t("common_weekly_income", "Weekly Income"), Kind = UiCellKind.Money, Width = 152f, Sortable = true },
                new UiDataColumn { Title = t("common_address", "Address"), Kind = UiCellKind.Text, Width = 320f, Sortable = true },
            };
            table.SortColumn = v.SortColumn;
            table.SortDown = v.SortDown;
            table.Rows = new UiDataRow[v.Businesses.Length];
            for (int i = 0; i < v.Businesses.Length; i++)
            {
                ref RivalBusiness b = ref v.Businesses[i];
                UiIcon glyph = MarketViews.BusinessIcon(b.TypeKey);
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[] { b.Name, b.Type, Money(v, b.WeeklyIncome), b.Address },
                    Values = new[] { -1, (int)glyph, -1, -1 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                };
            }
            table.RowCount = table.Rows.Length;
            int row = Row(v.Selected, table.RowCount);
            table.Selected = row;
            table.EmptyIcon = UiIcon.Store;
            table.EmptyTitle = t("reallife_ui_rivals_no_businesses", "No businesses");
            table.EmptyBody = t("reallife_ui_rivals_no_businesses_body", "They run nothing in the city now. A rival with no business left is out of the league.");
            if (row >= 0)
            {
                m.RowName = v.Businesses[row].Name;
                m.RowLink = t("common_show_on_map", "Show On Map");
                m.RowActions = new[]
                {
                    new UiActionModel { Label = t("reallife_ui_rivals_open", "Open in BizMan"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Briefcase, Key = UiAction.Count },
                    new UiActionModel { Label = t("common_setdestination", "Set destination"), Kind = UiButtonKind.Primary, Icon = UiIcon.Navigation, Key = UiAction.Alt },
                };
            }
        }

        private static void BuildingTable(UiRivalsModel m, UiWords t, RivalsView v)
        {
            UiDataTableModel table = m.Table;
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("common_address", "Address"), Kind = UiCellKind.Text, Sortable = true },
                new UiDataColumn { Title = t("common_neighborhood", "Neighborhood"), Kind = UiCellKind.Text, Width = 176f, Sortable = true },
                new UiDataColumn { Title = t("common_building_type", "Building Type"), Kind = UiCellKind.Tile, Width = 200f, Sortable = true },
                new UiDataColumn { Title = t("bizman_building_total_size", "Total size"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true },
                new UiDataColumn { Title = t("real_estate_estimated_value", "Estimated value"), Kind = UiCellKind.Money, Width = 152f, Sortable = true },
                new UiDataColumn { Title = t("common_price", "Price"), Kind = UiCellKind.Money, Width = 120f, Sortable = true },
            };
            table.SortColumn = v.SortColumn;
            table.SortDown = v.SortDown;
            table.Rows = new UiDataRow[v.Buildings.Length];
            for (int i = 0; i < v.Buildings.Length; i++)
            {
                ref RivalBuilding b = ref v.Buildings[i];
                table.Rows[i] = Building(t, v, ref b);
            }
            table.RowCount = table.Rows.Length;
            int row = Row(v.Selected, table.RowCount);
            table.Selected = row;
            table.EmptyIcon = UiIcon.Building;
            table.EmptyTitle = t("reallife_ui_rivals_no_buildings", "No buildings");
            table.EmptyBody = t("reallife_ui_rivals_no_buildings_body", "They own no building in the city now. What they rent shows under Businesses.");
            if (row >= 0)
            {
                m.RowName = v.Buildings[row].Address;
                m.RowActions = new[] { new UiActionModel { Label = t("reallife_ui_rivals_open", "Open in BizMan"), Kind = UiButtonKind.Primary, Icon = UiIcon.Briefcase, Key = UiAction.Alt } };
            }
        }

        /// <summary>A building as the game's real estate tables show it: address, district, type with its glyph, size, value and price.</summary>
        public static UiDataRow Building(UiWords t, RivalsView v, ref RivalBuilding b) => new UiDataRow
        {
            Cells = new[] { b.Address, b.District, b.Type, b.Size, Money(v, b.Value), b.Price > 0f ? Money(v, b.Price) : t("reallife_ui_market_not_for_sale", "Not for sale") },
            Values = new[] { -1, -1, (int)MarketViews.BuildingIcon(b.TypeKey), -1, -1, -1 },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }
        };

        // The chosen rival's development as the game charts it: its last days under the filter chosen, money or a count.
        private static void Chart(UiChartModel c, UiWords t, RivalsView v)
        {
            c.Kind = UiChartKind.Line;
            c.Title = t("rivals_business_development", "Business Development");
            int n = v.Chart != null ? v.Chart.Length : 0;
            c.Points = new string[n];
            for (int i = 0; i < n; i++)
            {
                c.Points[i] = StaffViews.Fill(t("common_day_number", "Day {number}"), "number", Whole(v.ChartDays != null && i < v.ChartDays.Length ? v.ChartDays[i] : i + 1));
            }
            c.Reads = null;
            string label = v.Filters != null && v.Filter >= 0 && v.Filter < v.Filters.Length ? v.Filters[v.Filter] : c.Title;
            c.Series = new[] { new UiSeries { Label = label, Values = v.Chart ?? new float[0], Tint = UiRole.Accent } };
            Func<float, string> count = f => Whole((int)Math.Round(f));
            c.Format = v.ChartMoney ? (v.Money ?? count) : count;
            c.AxisFormat = c.Format;
            c.Focus = v.ChartFocus;
            c.EmptyTitle = t("reallife_ui_rivals_chart_empty", "No history yet");
            c.EmptyBody = t("reallife_ui_rivals_chart_empty_body", "Their week by week development draws here as the days pass.");
        }

        private static int Row(int selected, int count) => count == 0 ? -1 : Math.Max(0, Math.Min(count - 1, selected));

        private static string Money(RivalsView v, float amount) => v.Money != null ? v.Money(amount) : amount.ToString("0", Inv);

        private static string Whole(int n) => n.ToString(Inv);
    }
}
