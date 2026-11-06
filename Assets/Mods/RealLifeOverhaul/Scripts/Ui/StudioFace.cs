using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a tech firm's page shows, as the Tech app's own firm pages do: the next step with the firm's actions, the figures,
    /// its main table over a second, the team as graded bars, and the facts and a note beside them. Plain words and figures,
    /// game-free; the actions are the screen's, by their place in its list.
    /// </summary>
    public sealed class StudioFaceModel
    {
        public string Title;
        public string Next;                                      // what to do next, one sentence under the firm's name
        public UiRole NextInk = UiRole.Text;                     // its weight: good news, a warning, a fault
        public UiActionModel[] Actions = new UiActionModel[0];   // the others first, the main one last
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public UiDataTableModel Main, More, Team;
        public UiFactCardModel Note, Facts;
        public UiPrompt[] Prompts = new UiPrompt[0];

        // Written back by the layout, so a wheel and a focus reach the table they belong to: the main table, the second and the team.
        public readonly short[] Lists = { -1, -1, -1 };
        public readonly short[] Clips = { -1, -1, -1 };
    }

    /// <summary>
    /// A tech firm's page on the app window, beside the Tech app's own firm pages: the next step and the actions at the head,
    /// the figures on their band, the tables at the left (each scrolls by wheel, keys and pad), the team's graded bars and the
    /// facts at the right. Commands 9840 to 9859 are this page's: three tables four apart (a table answers its head at its
    /// base and its rows at base + 3), then the actions. Game-free. Cost: a layout when the model changes.
    /// </summary>
    public static class StudioFace
    {
        public const int MainBase = 9840, MoreBase = 9844, TeamBase = 9848, ActionCommand = 9856;
        public const float ScoreColumn = 88f, FigureColumn = 104f, StatusColumn = 152f, SideMost = 440f, SideLeast = 320f;
        private const byte RegionHead = 4, RegionMain = 1, RegionMore = 2, RegionTeam = 3, RegionFacts = 5;
        private const int EmptyRows = 3;

        /// <summary>Every command the page answers, for the checks.</summary>
        public static readonly int[] Commands =
        {
            MainBase + UiDataTable.Row, MoreBase + UiDataTable.Row, TeamBase + UiDataTable.Row, ActionCommand
        };

        public static void Lay(UiScene s, StudioFaceModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("studio", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, UiIcon.GameController, null, 0, false, keys, out float x, out float y, out float w, out float bottom);
            y = Lead(s, at.InRegion(RegionHead), m, x, y, w, keys);
            y = UiAppWindow.Figures(s, at, m.Figures, x, y, w);
            float room = bottom - y;
            bool side = m.Team != null || m.Facts != null || m.Note != null;
            float sw = side ? Math.Max(Math.Min(SideLeast, UiDraw.SnapDown(w / 2f)), Math.Min(SideMost, UiDraw.SnapDown(w * 0.34f))) : 0f;
            float lw = side ? w - sw - UiTokens.S6 : w;
            Left(s, at, m, x, y, lw, room, keys);
            if (side)
            {
                Right(s, at, m, x + lw + UiTokens.S6, y, sw, room, keys);
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The next step at the left with its mark, the actions at the right with the main one last. A long sentence takes two lines before it is cut.
        private static float Lead(UiScene s, UiAt at, StudioFaceModel m, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(m.Next) && m.Actions.Length == 0)
            {
                return y;
            }
            float h = UiTokens.Control, right = x + w;
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                right -= UiTokens.S3;
            }
            if (string.IsNullOrEmpty(m.Next))
            {
                return y + h + UiTokens.S4;
            }
            float tx = x + UiTokens.Icon + UiTokens.S2, tw = right - UiTokens.S6 - tx;
            float lines = UiDraw.Snap(UiTextRules.WrappedHeight(m.Next, UiStyle.BodyStrong, tw, 2));
            float rowH = Math.Max(h, lines);
            UiDraw.Icon(s, at, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Spark, UiRole.None, UiFlags.Middle, true);
            if (lines > h - UiTokens.S1)
            {
                UiDraw.Text(s, at, tx, y + UiDraw.SnapDown((rowH - lines) / 2f), tw, UiStyle.BodyStrong, m.NextInk, m.Next, UiFlags.Label | UiFlags.Wrap, 2);
            }
            else
            {
                UiDraw.TextMiddle(s, at, tx, y, h, tw, UiStyle.BodyStrong, m.NextInk, m.Next, UiFlags.Label | UiFlags.Truncate);
            }
            return y + rowH + UiTokens.S4;
        }

        // The two tables, each its own height: a short one takes what it needs and the other the rest, two long ones share the room.
        private static void Left(UiScene s, UiAt at, StudioFaceModel m, float x, float y, float w, float room, IUiKeys keys)
        {
            float gap = UiTokens.S4;
            float mainH = Natural(m.Main), moreH = m.More != null ? Natural(m.More) : 0f;
            if (m.More == null)
            {
                mainH = Math.Min(mainH, room);
            }
            else if (mainH + gap + moreH > room)
            {
                float half = UiDraw.SnapDown((room - gap) / 2f);
                if (mainH <= half)
                {
                    moreH = room - gap - mainH;
                }
                else if (moreH <= half)
                {
                    mainH = room - gap - moreH;
                }
                else
                {
                    mainH = half;
                    moreH = room - gap - half;
                }
            }
            Place(s, at.InRegion(RegionMain), m, 0, m.Main, x, y, w, mainH, MainBase, keys);
            if (m.More != null)
            {
                Place(s, at.InRegion(RegionMore), m, 1, m.More, x, y + mainH + gap, w, moreH, MoreBase, keys);
            }
        }

        // The team's bars over the note and the facts, which keep their own height; the bars scroll in what is left.
        private static void Right(UiScene s, UiAt at, StudioFaceModel m, float x, float y, float w, float room, IUiKeys keys)
        {
            float gap = UiTokens.S4;
            float noteH = m.Note != null ? UiFactCard.Height(m.Note, w, keys, true) : 0f;
            float factsH = m.Facts != null ? UiFactCard.Height(m.Facts, w, keys, true) : 0f;
            float gaps = (m.Note != null ? gap : 0f) + (m.Facts != null ? gap : 0f);
            float teamH = 0f;
            if (m.Team != null)
            {
                float least = UiDataTable.Chrome(m.Team) + 2f * UiDataTable.RowHeight(m.Team);
                teamH = Math.Max(least, Math.Min(Natural(m.Team), room - noteH - factsH - gaps));
                factsH = Math.Max(0f, Math.Min(factsH, room - teamH - noteH - gaps));
                Place(s, at.InRegion(RegionTeam), m, 2, m.Team, x, y, w, teamH, TeamBase, keys);
                y += teamH + gap;
            }
            if (m.Note != null)
            {
                y += UiFactCard.Lay(s, at.InRegion(RegionFacts), m.Note, x, y, w, noteH, keys, true) + gap;
            }
            if (m.Facts != null)
            {
                UiFactCard.Lay(s, at.InRegion(RegionFacts), m.Facts, x, y, w, factsH, keys, true);
            }
        }

        private static void Place(UiScene s, UiAt at, StudioFaceModel m, int slot, UiDataTableModel t, float x, float y, float w, float h, int command, IUiKeys keys)
        {
            short list = UiDataTable.Lay(s, at, t, x, y, w, h, keys, command);
            m.Lists[slot] = list;
            m.Clips[slot] = list >= 0 && list < s.ListCount ? s.Lists[list].Clip : (short)-1;
        }

        // What a table takes to show its rows whole; an empty one keeps room for its words.
        private static float Natural(UiDataTableModel t) => UiDraw.Snap(UiDataTable.Chrome(t) + Math.Max(t.RowCount, EmptyRows) * UiDataTable.RowHeight(t));

        // ------------------------------------------------------------------------------------------------- the model's parts, from words alone

        /// <summary>
        /// A table of the page: its columns by their keys (the figures to the right in a column of their own width, the score graded,
        /// the status a little wider) and its rows as written. A row is a target so the keys and the pad can scroll to it, but it opens nothing.
        /// </summary>
        public static UiDataTableModel Table(string[] titles, string[] keys, bool[] figures, IList<string[]> rows, string empty, UiIcon emptyIcon)
        {
            var columns = new UiDataColumn[titles.Length];
            int score = -1;
            for (int i = 0; i < columns.Length; i++)
            {
                bool figure = i < figures.Length && figures[i];
                bool graded = figure && keys[i].EndsWith("page_col_score", StringComparison.Ordinal);
                bool status = figure && keys[i].EndsWith("page_col_status", StringComparison.Ordinal);
                score = graded ? i : score;
                float width = status ? StatusColumn : graded ? ScoreColumn : figure ? FigureColumn : 0f;
                columns[i] = new UiDataColumn { Title = titles[i], Kind = figure ? UiCellKind.Figure : UiCellKind.Text, Graded = graded, Width = width };
            }
            var table = new UiDataTableModel { Columns = columns, Rows = new UiDataRow[rows.Count], RowCount = rows.Count, EmptyTitle = empty, EmptyIcon = emptyIcon };
            for (int r = 0; r < rows.Count; r++)
            {
                var values = new int[columns.Length];
                if (score >= 0)
                {
                    values[score] = Points(rows[r][score]);
                }
                table.Rows[r] = new UiDataRow { Cells = rows[r], Values = values };
            }
            return table;
        }

        // A graded cell's number from its words ("78", "1,204"); -1 for a game not out yet, whose score is blank.
        private static int Points(string words)
        {
            int n = -1;
            for (int i = 0; words != null && i < words.Length; i++)
            {
                if (words[i] >= '0' && words[i] <= '9')
                {
                    n = (n < 0 ? 0 : n) * 10 + (words[i] - '0');
                    if (n > 100000)
                    {
                        break;
                    }
                }
            }
            return n;
        }

        /// <summary>The team: each group's name with its count, a bar of its mean skill (a group the firm needs and lacks reads its word and an empty bar).</summary>
        public static UiDataTableModel TeamTable(string heading, string skill, string[] groups, int[] skills, string[] figures, string empty)
        {
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = heading, Kind = UiCellKind.Text },
                    new UiDataColumn { Title = skill, Kind = UiCellKind.Meter }
                },
                Rows = new UiDataRow[groups.Length], RowCount = groups.Length, EmptyTitle = empty, EmptyIcon = UiIcon.People
            };
            for (int i = 0; i < groups.Length; i++)
            {
                table.Rows[i] = new UiDataRow { Cells = new[] { groups[i], figures[i] }, Values = new[] { 0, skills[i] } };
            }
            return table;
        }

        /// <summary>The facts as pairs, the label at the left and its value at the right.</summary>
        public static UiFactCardModel FactsCard(string title, string[] labels, string[] values)
        {
            var lines = new UiFactLine[labels.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = UiFactLine.Row(labels[i], values[i]);
            }
            return new UiFactCardModel { Title = title, Heading = true, Lines = lines };
        }

        /// <summary>A sentence under the team that warns of what it lacks.</summary>
        public static UiFactCardModel NoteCard(string words) => new UiFactCardModel { Lines = new[] { UiFactLine.Tick(words, false) } };
    }
}
