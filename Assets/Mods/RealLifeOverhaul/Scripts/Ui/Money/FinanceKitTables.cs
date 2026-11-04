using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A row of one of the Finance tables, before it is a row of the kit's table: the name with a line under it, then the other cells as words.</summary>
    public sealed class FinanceRow
    {
        public string Name, Sub;
        public UiIcon Icon = UiIcon.None;
        public UiRole Tint = UiRole.None;
        public string[] Cells = new string[0];       // the cells after the first
        public UiRole[] Tones;                       // by those cells, or null
        public int[] Values;                          // by those cells (a trend's direction), or null
        public float[] Line;                          // a spark's values, or null
        public float Hue;
        public UiRole Ring = UiRole.None;
    }

    /// <summary>
    /// The columns of every Finance table, in one place: the live screen and the preview's samples both make their tables here, so a
    /// shot shows what ships. Game-free. Cost: an array a table when the facts change.
    /// </summary>
    public static class FinanceKitTables
    {
        private static UiDataColumn Col(string title, UiCellKind kind, float width = 0f) => new UiDataColumn { Title = title, Kind = kind, Width = width };

        private static float Fig => UiTokens.S16 * 1.75f;

        public static UiDataColumn[] DebtColumns(UtilitiesWords w) => new[]
        {
            Col(w.T("reallife_fin_col_debt", "Debt"), UiCellKind.Tile), Col(w.T("reallife_fin_col_owed", "Owed"), UiCellKind.Figure, Fig),
            Col(w.T("reallife_fin_col_rate", "Rate"), UiCellKind.Figure, UiTokens.S16 * 1.25f), Col(w.T("reallife_fin_col_day", "A day"), UiCellKind.Figure, Fig),
            Col(w.T("reallife_fin_col_left", "Left"), UiCellKind.Figure, UiTokens.S16 * 2.25f)
        };

        public static UiDataColumn[] OfferColumns(UtilitiesWords w, bool apply)
        {
            return new[]
            {
                Col(w.T("reallife_fin_col_offer", "Bank or credit"), UiCellKind.Tile), Col(w.T("reallife_fin_col_rate", "Rate"), UiCellKind.Figure, UiTokens.S16 * 1.25f)
            };
        }

        public static UiDataColumn[] EventColumns(UtilitiesWords w) => new[]
        {
            Col(w.T("reallife_fin_col_event", "What moved it lately"), UiCellKind.Text), Col(w.T("reallife_fin_col_when", "When"), UiCellKind.Figure, UiTokens.S16 * 1.25f),
            Col(w.T("reallife_fin_col_points", "Points"), UiCellKind.Trend, UiTokens.S16 * 1.25f)
        };

        public static UiDataColumn[] FeedColumns(UtilitiesWords w) => new[]
        {
            Col(w.T("reallife_fin_col_borrower", "Borrower"), UiCellKind.Person), Col(w.T("reallife_fin_col_amount", "Amount"), UiCellKind.Figure, Fig),
            Col(w.T("reallife_fin_col_day", "Day"), UiCellKind.Figure, UiTokens.S16)
        };

        public static UiDataColumn[] AccountColumns(UtilitiesWords w) => new[]
        {
            Col(w.T("reallife_fin_col_account", "Account"), UiCellKind.Tile), Col(w.T("reallife_fin_col_owed", "Owed"), UiCellKind.Figure, Fig),
            Col(w.T("reallife_fin_col_rate", "Rate"), UiCellKind.Figure, UiTokens.S16 * 1.25f), Col(w.T("reallife_fin_col_next", "Next payment"), UiCellKind.Figure, Fig),
            Col(w.T("reallife_fin_col_state", "State"), UiCellKind.Chip, UiTokens.S16 * 1.75f)
        };

        public static UiDataColumn[] RichColumns(UtilitiesWords w) => new[]
        {
            Col(w.T("reallife_fin_col_tycoon", "Tycoon"), UiCellKind.Person), Col(w.T("reallife_fin_col_worth", "Worth"), UiCellKind.Figure, Fig),
            Col(w.T("reallife_fin_col_path", "Path"), UiCellKind.Spark, UiTokens.S16 * 1.5f), Col(w.T("reallife_fin_col_week", "In a week"), UiCellKind.Trend, UiTokens.S16 * 1.75f)
        };

        public static UiDataColumn[] StandingColumns(UtilitiesWords w) => new[]
        {
            Col(w.T("reallife_fame_col_what", "What"), UiCellKind.Tile), Col(w.T("reallife_fame_col_stands", "How it stands"), UiCellKind.Chip, UiTokens.S16 * 2f),
            Col(w.T("reallife_fame_col_heading", "Heading"), UiCellKind.Figure, UiTokens.S16 * 2f)
        };

        /// <summary>A table from its columns and rows: the first cell is the row's name with its line under it, the rest are the cells given.</summary>
        public static UiDataTableModel Make(UiDataColumn[] columns, List<FinanceRow> rows, int selected, string emptyTitle, string emptyBody, UiIcon emptyIcon)
        {
            var made = new UiDataRow[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                FinanceRow r = rows[i];
                int n = columns.Length;
                var cells = new string[n];
                var values = new int[n];
                var tones = new UiRole[n];
                var subs = new string[n];
                cells[0] = r.Name ?? string.Empty;
                values[0] = (int)r.Icon;
                tones[0] = r.Tint;
                subs[0] = r.Sub;
                for (int c = 1; c < n; c++)
                {
                    cells[c] = c - 1 < r.Cells.Length ? r.Cells[c - 1] : string.Empty;
                    values[c] = r.Values != null && c - 1 < r.Values.Length ? r.Values[c - 1] : 0;
                    tones[c] = r.Tones != null && c - 1 < r.Tones.Length ? r.Tones[c - 1] : UiRole.None;
                }
                made[i] = new UiDataRow { Cells = cells, Values = values, Tones = tones, Subs = subs, Sub = r.Sub, Hue = r.Hue, Ring = r.Ring, Line = r.Line };
            }
            return new UiDataTableModel
            {
                Columns = columns, Rows = made, RowCount = made.Length, Selected = made.Length == 0 ? -1 : selected < 0 ? 0 : selected >= made.Length ? made.Length - 1 : selected,
                EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = emptyIcon
            };
        }
    }
}
