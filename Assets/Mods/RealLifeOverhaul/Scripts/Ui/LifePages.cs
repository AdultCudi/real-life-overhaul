using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One row of Upcoming or of Notes: its kind, its words and whether it can be located.</summary>
    public struct UiPlanRow
    {
        public UiIcon Icon;
        public string Title;
        public string Meta;
        public bool Locate;
    }

    /// <summary>
    /// The Planner's page: tabs across, the group chips, a board for today and the week, or rows for what is coming
    /// and for notes. The screen fills it. Game-free.
    /// </summary>
    public sealed class UiPlanModel
    {
        public string Title;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public string[] Chips = new string[0];
        public int Chip = -1;
        public string Week;
        public UiBoardModel Board;
        public UiPlanRow[] Rows = new UiPlanRow[0];
        public int RowCount;
        public string EmptyTitle, EmptyBody;
        public string Add, Second;
        public string LocateWord;
        public string Move, Select, Back, Tabs;
        public float Offset;
    }

    /// <summary>The Calendar's page: the diary, today's weather, and the two actions.</summary>
    public sealed class UiCalModel
    {
        public string Title;
        public UiDiaryModel Diary;
        public string Weather;
        public string Add, Open;
        public string Move, Select, Back;
    }

    /// <summary>
    /// The life apps' pages, laid from the kit's window, board, diary and decision card. Game-free. Cost: a layout a change.
    /// </summary>
    public static partial class LifePages
    {
        public const int Chip = 40, Row = 41, Add = 42, Second = 43, Locate = 44;
        public const int VMode = 50, VFilter = 51, VFrame = 52, VShutter = 53, VClose = 54, VGrid = 55, VOpen = 56;
        private const float PlanRow = 48f;

        /// <summary>The Planner.</summary>
        public static void Planner(UiScene s, UiPlanModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("planner", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, UiIcon.Clock, m.Nav, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            float weekW = WeekWidth(m.Week);
            if (weekW > 0f)
            {
                UiDraw.Chip(s, at, x + w - weekW, y + UiTokens.S1, m.Week, UiRole.AccentText, weekW);
            }
            float chipsW = w - (weekW > 0f ? weekW + UiTokens.S4 : 0f);
            float chips = Chips(s, at, m.Chips, m.Chip, x, y, chipsW);
            y += chips + UiTokens.S3;
            float h = bottom - y;
            if (m.Board != null && (m.Tab == 0 || m.Tab == 2))
            {
                UiBoard.Lay(s, at, m.Board, x, y, w, h, keys);
            }
            else if (m.RowCount <= 0)
            {
                float foot = string.IsNullOrEmpty(m.Add) ? 0f : UiTokens.Control + UiTokens.S4;
                UiDraw.EmptyState(s, at, x, y, w, Math.Max(UiTokens.S16, h - foot), UiIcon.Info, m.EmptyTitle, m.EmptyBody);
                Buttons(s, at, m.Add, m.Second, x, y + h - UiTokens.Control, w, keys);
            }
            else
            {
                float foot = string.IsNullOrEmpty(m.Add) ? 0f : UiTokens.Control + UiTokens.S4;
                Rows(s, at, m, x, y, w, h - foot);
                Buttons(s, at, m.Add, m.Second, x, y + h - UiTokens.Control, w, keys);
            }
            Bar(s, frame, keys, m.Move, m.Select, m.Back, m.Tabs);
        }

        /// <summary>The Calendar.</summary>
        public static void Calendar(UiScene s, UiCalModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("calendar", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, UiIcon.Calendar, new UiNavEntry[0], 0, false, keys, out float x, out float y, out float w, out float bottom);
            if (!string.IsNullOrEmpty(m.Weather))
            {
                UiDraw.Chip(s, at, x, y, m.Weather, UiRole.AccentText, Math.Min(360f, w));
                y += UiTokens.Chip + UiTokens.S3;
            }
            float foot = UiTokens.Control + UiTokens.S4;
            float h = bottom - y - foot;
            if (m.Diary != null)
            {
                UiDiary.Lay(s, at, m.Diary, x, y, w, Math.Max(UiTokens.S16 * 4f, h), keys);
            }
            Buttons(s, at, m.Add, m.Open, x, bottom - UiTokens.Control, w, keys);
            Bar(s, frame, keys, m.Move, m.Select, m.Back, null);
        }

        /// <summary>The camera's refusal, a decision card.</summary>
        public static void Refused(UiScene s, UiDecisionModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            UiDecision.Lay(s, m, frame, dark, keys);
        }

        /// <summary>The Gallery: the album, or one photo.</summary>
        public static void Gallery(UiScene s, UiFrame frame, bool dark, IUiKeys keys, string title, UiNavEntry[] nav, int tab, bool viewing,
            UiAlbumModel album, UiPhotoModel photo, string move, string select, string back, UiPhotoEditModel edit = null)
        {
            s.Begin("gallery", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, title, UiIcon.Image, nav, tab, true, keys, out float x, out float y, out float w, out float bottom);
            if (edit != null)
            {
                UiPhotoEdit.Lay(s, at, edit, x, y, w, bottom - y, keys);
            }
            else if (viewing && photo != null)
            {
                UiPhotoViewer.Lay(s, at, photo, x, y, w, bottom - y, keys);
            }
            else if (album != null)
            {
                UiAlbum.Lay(s, at, album, x, y, w, bottom - y, keys);
            }
            Bar(s, frame, keys, move, select, back, null);
        }

        private static float Choice(UiScene s, UiAt at, float x, float y, string label, bool on, int command, int arg, float limit)
        {
            if (string.IsNullOrEmpty(label) || x >= limit)
            {
                return x;
            }
            float w = UiDraw.Snap(Math.Min(limit - x, UiTextRules.Width(label, UiStyle.Button) + UiTokens.S8));
            if (w < UiTokens.MinTarget)
            {
                return x;
            }
            ChoiceAt(s, at, x, y, w, UiTokens.Chip, label, on, command, arg);
            return x + w + UiTokens.S2;
        }

        private static void ChoiceAt(UiScene s, UiAt at, float x, float y, float w, float h, string label, bool on, int command, int arg)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, on ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusMedium, on ? UiFlags.Selected : UiFlags.None);
            UiDraw.TextMiddle(s, at.In(face), x, y, h, w, UiStyle.Button, on ? UiRole.OnAccent : UiRole.Text, label, UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, h, command, arg, face);
        }

        private static float WeekWidth(string week)
        {
            if (string.IsNullOrEmpty(week))
            {
                return 0f;
            }
            float tw = UiTextRules.Width(week, UiStyle.SmallStrong) + UiTokens.S1;
            float width = UiDraw.Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + tw + UiTokens.S2);
            return Math.Min(width, 240f);
        }

        private static float Chips(UiScene s, UiAt at, string[] labels, int chosen, float x, float y, float w)
        {
            if (labels == null || labels.Length == 0)
            {
                return 0f;
            }
            float cx = x, cy = y, h = UiTokens.Chip;
            for (int i = 0; i < labels.Length; i++)
            {
                float bw = UiDraw.Snap(Math.Min(w, UiTextRules.Width(labels[i], UiStyle.Button) + UiTokens.S8));
                if (cx > x && cx + bw > x + w)
                {
                    cx = x;
                    cy += h + UiTokens.S2;
                }
                ChoiceAt(s, at, cx, cy, bw, h, labels[i], i == chosen, Chip, i);
                cx += bw + UiTokens.S2;
            }
            return cy - y + h;
        }

        private static void Rows(UiScene s, UiAt at, UiPlanModel m, float x, float y, float w, float h)
        {
            int n = Math.Max(0, m.RowCount);
            float content = n * PlanRow;
            float offset = Math.Max(0f, Math.Min(m.Offset, Math.Max(0f, content - h)));
            m.Offset = offset;
            short clip = s.AddClip(x, y, w, h, offset);
            s.Clips[clip].ContentHeight = content;
            var inside = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = at.Order, Region = at.Region };
            int first = Math.Max(0, (int)(offset / PlanRow));
            int last = Math.Min(n - 1, (int)((offset + h) / PlanRow));
            for (int i = first; i <= last; i++)
            {
                PlanRowAt(s, inside, ref m.Rows[i], i, 0f, i * PlanRow, w, m.LocateWord);
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S2, y, h, offset, content);
        }

        private static void PlanRowAt(UiScene s, UiAt at, ref UiPlanRow row, int index, float x, float y, float w, string mLocate)
        {
            int face = UiDraw.Box(s, at, x, y, w, PlanRow - UiTokens.S1, UiRole.None, UiRole.None, UiTokens.RadiusMedium);
            UiAt inside = at.In(face);
            float g = UiTokens.Icon;
            UiDraw.Icon(s, inside, x + UiTokens.S2, y + (PlanRow - UiTokens.S1 - g) / 2f, g, row.Icon, UiRole.None, UiFlags.Middle, true);
            float locate = row.Locate && !string.IsNullOrEmpty(mLocate)
                ? Math.Max(UiTokens.MinTarget, UiDraw.Snap(UiTextRules.Width(mLocate, UiStyle.Small) + UiTokens.S4))
                : 0f;
            float meta = string.IsNullOrEmpty(row.Meta) ? 0f : UiDraw.Snap(Math.Min(w * 0.35f, UiTextRules.Width(row.Meta, UiStyle.Small) + UiTokens.S2));
            float tx = x + UiTokens.S2 + g + UiTokens.S3;
            float tw = w - (tx - x) - meta - locate - UiTokens.S3;
            UiDraw.TextMiddle(s, inside, tx, y, PlanRow - UiTokens.S1, Math.Max(UiTokens.S16, tw), UiStyle.Body, UiRole.Text, row.Title, UiFlags.Truncate);
            if (meta > 0f)
            {
                UiDraw.TextMiddle(s, inside, x + w - locate - meta - UiTokens.S2, y, PlanRow - UiTokens.S1, meta, UiStyle.Small, UiRole.Muted, row.Meta, UiFlags.Right | UiFlags.Truncate);
            }
            if (row.Locate && !string.IsNullOrEmpty(mLocate))
            {
                UiDraw.TextMiddle(s, inside, x + w - locate, y, PlanRow - UiTokens.S1, locate - UiTokens.S2, UiStyle.Small, UiRole.AccentText, mLocate, UiFlags.Right | UiFlags.Truncate);
                UiDraw.Node(s, at, x + w - locate, y, locate, PlanRow - UiTokens.S1, Locate, index, face);
            }
            UiDraw.Node(s, at, x, y, w - locate, PlanRow - UiTokens.S1, Row, index, face);
        }

        private static void Buttons(UiScene s, UiAt at, string add, string second, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(add) && string.IsNullOrEmpty(second))
            {
                return;
            }
            float cx = x;
            if (!string.IsNullOrEmpty(add))
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Primary, add, UiIcon.None, UiAction.Alt, keys);
                UiDraw.Button(s, at, cx, y, bw, UiTokens.Control, UiButtonKind.Primary, add, UiIcon.None, UiAction.Alt, keys, Add, 0);
                cx += bw + UiTokens.S3;
            }
            if (!string.IsNullOrEmpty(second) && cx < x + w - UiTokens.S16)
            {
                float bw = Math.Min(UiDraw.ButtonWidth(UiButtonKind.Secondary, second, UiIcon.None, UiAction.Option, keys), x + w - cx);
                UiDraw.Button(s, at, cx, y, bw, UiTokens.Control, UiButtonKind.Secondary, second, UiIcon.None, UiAction.Option, keys, Second, 0);
            }
        }

        private static void Bar(UiScene s, UiFrame frame, IUiKeys keys, string move, string select, string back, string tabs)
        {
            if (!string.IsNullOrEmpty(move))
            {
                s.AddPrompt(UiAction.Move, move);
            }
            if (!string.IsNullOrEmpty(select))
            {
                s.AddPrompt(UiAction.Confirm, select);
            }
            if (!string.IsNullOrEmpty(tabs))
            {
                s.AddPrompt(UiAction.TabPrev, tabs);
            }
            if (!string.IsNullOrEmpty(back))
            {
                s.AddPrompt(UiAction.Back, back);
            }
            UiDraw.PromptBar(s, frame, keys);
        }
    }
}
