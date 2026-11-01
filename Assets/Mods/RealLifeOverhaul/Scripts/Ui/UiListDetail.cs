using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a list-and-detail screen shows: its groups, its list and the detail of the row selected, all words given.</summary>
    public sealed class UiListDetailModel
    {
        public string AppName;                                  // the app's name, as the frame's top bar shows it
        public string GroupsLabel;                              // the capitals over the groups
        public UiNavModel[] Groups = new UiNavModel[0];
        public int GroupCount;
        public float GroupsOffset;                              // px the groups have scrolled
        public UiActionModel[] SideActions = new UiActionModel[0];
        public int SideActionCount;
        public string SideNote;                                 // a line under the side actions, or null
        public string PlayerName;
        public float PlayerHue;
        public string PlayerSummary;

        public string ListTitle;
        public string ListCount;
        public string SearchPlaceholder;
        public string Query;
        public string SearchCount;
        public UiRowModel[] Rows = new UiRowModel[0];
        public int RowCount;
        public float RowsOffset;                                // px the rows have scrolled
        public string ListEmptyTitle;
        public string ListEmptyBody;

        public bool HasDetail;
        public string Name;
        public float Hue;
        public string Role;
        public string Status;
        public UiTone Tone;
        public UiActionModel[] Actions = new UiActionModel[0];
        public int ActionCount;
        public UiMessageModel[] Messages = new UiMessageModel[0];
        public int MessageCount;
        public float ThreadOffset = -1f;                        // px the thread has scrolled; -1 shows its newest
        public bool Typing;
        public string ThreadEmptyTitle;
        public string ThreadEmptyBody;
        public string ThreadStart;                              // the line over the first message ("Start of your messages with Maya"), or null
        public string ClassicLabel;                             // the chip under a message only the game's own view can answer
        public string[] Replies = new string[0];
        public int ReplyCount;
        public bool Composer;                                   // the line takes replies
        public string ComposerPlaceholder;
        public string Draft;
        public string SendLabel;
        public string NoReply;                                  // said where a line takes no replies
        public string ServicesTitle;
        public UiServiceModel[] Services = new UiServiceModel[0];
        public int ServiceCount;
        public string StatsTitle;
        public UiStatModel[] Stats = new UiStatModel[0];
        public int StatCount;
        public string DetailsTitle;
        public UiPairModel[] Details = new UiPairModel[0];
        public int DetailCount;
        public float SideOffset;                                // px the side column has scrolled
        public string NoDetailTitle;
        public string NoDetailBody;

        public UiOverlay Overlay;
        public string OverlayTitle;                             // a question's title, or the call's state
        public string OverlayBody;                              // a question's words
        public UiActionModel[] Answers = new UiActionModel[0];
        public int AnswerCount;
        public bool CallConnected;
        public string CallEnd;
        public string ToastTitle;
        public string ToastSub;

        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>What lies over a screen: nothing, a question, or a call.</summary>
    public enum UiOverlay : byte
    {
        None = 0,
        Question,
        Call
    }

    /// <summary>
    /// The list-and-detail template, the wireframe's three parts as a game screen: the groups at the left (the app's mark,
    /// the groups as bars that the tab keys step through, the side actions, the player), the list in the middle (title,
    /// search, rows in a fixed frame that scrolls), and the detail at the right as a framed panel (the hero with its
    /// actions, the thread and the composer, and a side column of services, stats and details). An open question or call
    /// lies over it with the focus held in it, a toast over everything, the prompt bar in the band under it. Fixed widths
    /// that sum to the frame; heights given to the lists. Commands are the constants below. Game-free.
    /// Cost: a layout a change; nothing allocated once the scene's arrays have grown.
    /// </summary>
    public static class UiListDetail
    {
        public const int Group = 1, SideAction = 2, Search = 3, Row = 4, Action = 5, Service = 6, Reply = 7, Compose = 8, Send = 9, Answer = 10,
            CallOption = 11, CallEnd = 12, Classic = 13;
        public const byte RegionGroups = 1, RegionList = 2, RegionDetail = 3, RegionSide = 4;   // the d-pad's columns

        public const float SideWidth = 264f;      // px: the groups' column
        public const float ListWidth = 400f;      // px: the list's column
        public const float AsideWidth = 380f;     // px: the detail's side column: a service's name beside its price
        public const float RowGap = UiTokens.S2;  // px between rows

        /// <summary>Lays a list-and-detail screen out into a scene at a frame, in a theme, with the keys bound now.</summary>
        public static void Lay(UiScene s, UiListDetailModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("list-detail", frame.Width, frame.Height, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt onBg = screen.In(bg);
            Sidebar(s, onBg.InRegion(RegionGroups), m, cx, cy, SideWidth, ch, keys);
            float lx = cx + SideWidth + UiTokens.S4;
            List(s, onBg.InRegion(RegionList), m, lx, cy, ListWidth, ch);
            float dx = lx + ListWidth + UiTokens.S4;
            float dw = cx + cw - dx;
            Detail(s, onBg.InRegion(RegionDetail), m, dx, cy, dw, ch, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, 0f, frame.BandY, frame.Width, frame.Band, keys);
            UiDraw.Chrome(s, frame);
            if (m.Overlay == UiOverlay.Question)
            {
                UiDraw.Question(s, 0f, frame.ContentTop, frame.Width, frame.BandY - frame.ContentTop, m.OverlayTitle, m.OverlayBody, m.Answers, m.AnswerCount, keys);
            }
            else if (m.Overlay == UiOverlay.Call)
            {
                UiDraw.Call(s, dx, cy, dw, ch, m.Name, m.Hue, m.OverlayTitle, m.CallConnected, m.Services, m.ServiceCount, m.CallEnd, CallOption, CallEnd);
            }
            if (!string.IsNullOrEmpty(m.ToastTitle))
            {
                UiDraw.Toast(s, 0f, frame.ContentTop, frame.Width, frame.BandY - frame.ContentTop, m.ToastTitle, m.ToastSub);
            }
        }

        private static void Sidebar(UiScene s, UiAt at, UiListDetailModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float top = y;   // the app's name stands in the frame's top bar, so the column opens on its groups
            // The groups' capitals with the tab keys that step through them.
            float capW = keys != null ? UiDraw.KeyCapWidth(UiAction.TabPrev, keys) + UiTokens.S1 + UiDraw.KeyCapWidth(UiAction.TabNext, keys) : 0f;
            UiDraw.Text(s, at, x + UiTokens.S3, y + (UiTokens.KeyCap - UiTextRules.LineHeight(UiStyle.SectionHeader)) / 2f, w - capW - UiTokens.S6, UiStyle.SectionHeader,
                UiRole.Muted, m.GroupsLabel, UiFlags.Label | UiFlags.Middle);
            if (keys != null)
            {
                float kx = x + w - UiTokens.S3 - capW;
                kx += UiDraw.KeyCap(s, at, kx, y, UiAction.TabPrev, keys, UiRole.Track, UiRole.Text2) + UiTokens.S1;
                UiDraw.KeyCap(s, at, kx, y, UiAction.TabNext, keys, UiRole.Track, UiRole.Text2);
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Right;
                s.Items[s.ItemCount - 2].Flags |= UiFlags.Right;
            }
            y += UiTokens.KeyCap + UiTokens.S3;
            // The foot first, so the groups know their room: the player, the note and the side actions above it.
            float foot = top + h - UiTokens.AvatarSmall;
            UiDraw.PlayerCard(s, at, x + UiTokens.S2, foot, w - UiTokens.S2 * 2f, m.PlayerName, m.PlayerHue, m.PlayerSummary);
            float by = foot - UiTokens.S6;
            if (!string.IsNullOrEmpty(m.SideNote))
            {
                float nh = UiTextRules.WrappedHeight(m.SideNote, UiStyle.Small, w - UiTokens.S4, 3);
                by -= UiDraw.Snap(nh);
                UiDraw.Text(s, at, x + UiTokens.S2, by, w - UiTokens.S4, UiStyle.Small, UiRole.Muted, m.SideNote, UiFlags.Wrap | UiFlags.Centre | UiFlags.Label, 3);
                by -= UiTokens.S2;
            }
            for (int i = m.SideActionCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.SideActions[i];
                float bh = UiDraw.ButtonHeight(a.Kind, a.Label, a.Key, keys, w, a.Kind == UiButtonKind.Primary ? UiTokens.Control : UiTokens.ControlSmall);
                by -= bh;
                UiDraw.Button(s, at, x, by, w, bh, a.Kind, a.Label, a.Icon, a.Key, keys, SideAction, i, a.Disabled);
                by -= UiTokens.S2;
            }
            // The groups, in a frame that scrolls when they outnumber its room.
            float gh = UiDraw.SnapDown(by - UiTokens.S4 - y);
            float itemH = UiTokens.Control + UiTokens.S1;
            float offset = Math.Max(0f, Math.Min(m.GroupsOffset, m.GroupCount * itemH - gh));
            short clip = s.AddClip(x, y, w, gh, offset);
            s.Clips[clip].ContentHeight = m.GroupCount * itemH;
            int first = Math.Max(0, (int)(offset / itemH)), last = Math.Min(m.GroupCount - 1, (int)((offset + gh) / itemH));
            int selected = -1;
            for (int i = 0; i < m.GroupCount; i++)
            {
                selected = m.Groups[i].Selected ? i : selected;
            }
            short list = s.AddList(m.GroupCount, first, Math.Max(0, last - first + 1), clip, selected);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int i = first; i <= last; i++)
            {
                ref UiNavModel g = ref m.Groups[i];
                int node = UiDraw.NavItem(s, inClip.Arriving(i - first), 0f, i * itemH, w, g.Label, g.Count, g.Hue, g.Selected, Group, i, g.Loud);
                s.Nodes[node].List = list;
                s.Nodes[node].Index = i;
            }
        }

        private static void List(UiScene s, UiAt at, UiListDetailModel m, float x, float y, float w, float h)
        {
            float top = y;
            y += UiDraw.PanelTitle(s, at, x + UiTokens.S1, y, w - UiTokens.S2, m.ListTitle, m.ListCount) + UiTokens.S4;
            UiDraw.Search(s, at, x, y, w, m.SearchPlaceholder, m.Query, m.SearchCount, Search);
            y += UiTokens.Control + UiTokens.S4;
            float vh = UiDraw.SnapDown(top + h - y);
            // Each row is a section header (when it opens one) and its row: their tops worked out once, only those in view drawn.
            float contentH = 0f;
            for (int i = 0; i < m.RowCount; i++)
            {
                contentH += (m.Rows[i].Section != null ? SectionHeight : 0f) + UiTokens.Row + RowGap;
            }
            float offset = Math.Max(0f, Math.Min(m.RowsOffset, contentH - vh));
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            if (m.RowCount == 0)
            {
                s.AddList(0, 0, 0, clip, -1);
                UiDraw.Empty(s, at, x, y, w, vh, m.ListEmptyTitle, m.ListEmptyBody);
                return;
            }
            int selected = -1, first = -1, shown = 0;
            float ry = 0f;
            for (int i = 0; i < m.RowCount; i++)
            {
                selected = m.Rows[i].Selected ? i : selected;
                float rowTop = ry + (m.Rows[i].Section != null ? SectionHeight : 0f);
                float bottom = rowTop + UiTokens.Row;
                if (bottom > offset && ry < offset + vh)
                {
                    first = first < 0 ? i : first;
                    shown++;
                }
                ry = bottom + RowGap;
            }
            short list = s.AddList(m.RowCount, Math.Max(0, first), shown, clip, selected);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            ry = 0f;
            for (int i = 0; i < m.RowCount; i++)
            {
                ref UiRowModel row = ref m.Rows[i];
                float rowTop = ry + (row.Section != null ? SectionHeight : 0f);
                float bottom = rowTop + UiTokens.Row;
                if (bottom > offset && ry < offset + vh)
                {
                    UiAt arriving = inClip.Arriving(i - first);
                    if (row.Section != null)
                    {
                        UiDraw.SectionHeader(s, arriving, 0f, ry, w, row.Section);
                    }
                    UiDraw.ListRow(s, arriving, 0f, rowTop, w, ref row, Row, i, list, i);
                }
                ry = bottom + RowGap;
            }
        }

        private static float SectionHeight => UiDraw.Snap(UiTokens.S4 + UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;

        private static void Detail(UiScene s, UiAt at, UiListDetailModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = at.In(panel);
            if (!m.HasDetail)
            {
                UiDraw.Empty(s, inside, x, y, w, h, m.NoDetailTitle, m.NoDetailBody);
                return;
            }
            float heroH = UiDraw.Hero(s, inside, x, y, w, m.Name, m.Hue, m.Role, m.Status, m.Tone, m.Actions, m.ActionCount, keys);
            float by = y + heroH;
            UiDraw.Box(s, inside, x, by, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float bodyH = y + h - by;
            float tw = w - AsideWidth;
            UiDraw.Box(s, inside, x + tw, by, UiTokens.Hairline, bodyH, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            Thread(s, inside, m, x, by, tw, bodyH);
            Aside(s, inside.InRegion(RegionSide), m, x + tw, by, AsideWidth, bodyH);
        }

        private static void Thread(UiScene s, UiAt at, UiListDetailModel m, float x, float y, float w, float h)
        {
            // The foot: the quick replies over the composer, or the line that takes no replies.
            float foot;
            float inner = w - UiTokens.S6 * 2f;
            float bottom = y + h - UiTokens.S5;
            if (m.Composer)
            {
                float composerH = UiTokens.Control + UiTokens.S2;
                UiDraw.Composer(s, at, x + UiTokens.S6, bottom - composerH, inner, m.ComposerPlaceholder, m.Draft, m.SendLabel, Compose, Send);
                foot = bottom - composerH - UiTokens.S3;
                if (m.ReplyCount > 0)
                {
                    float rh = ChipsHeight(m.Replies, m.ReplyCount, inner);
                    UiDraw.Chips(s, at, x + UiTokens.S6, foot - rh, inner, m.Replies, m.ReplyCount, Reply);
                    foot -= rh + UiTokens.S3;
                }
            }
            else if (!string.IsNullOrEmpty(m.NoReply))
            {
                float nh = UiTextRules.LineHeight(UiStyle.Small);
                UiDraw.Text(s, at, x + UiTokens.S6, bottom - UiDraw.Snap(nh), inner, UiStyle.Small, UiRole.Muted, m.NoReply, UiFlags.Centre | UiFlags.Label | UiFlags.Middle);
                foot = bottom - UiDraw.Snap(nh) - UiTokens.S3;
            }
            else
            {
                foot = bottom;
            }
            float vy = y + UiTokens.S5;
            float vh = UiDraw.SnapDown(foot - vy);
            if (m.MessageCount == 0 && !m.Typing)
            {
                s.AddClip(x, vy, w, vh, 0f);
                UiDraw.Empty(s, at, x, vy, w, vh, m.ThreadEmptyTitle, m.ThreadEmptyBody);
                return;
            }
            float startH = string.IsNullOrEmpty(m.ThreadStart) ? 0f : UiTokens.S8 + UiTokens.S4;
            float contentH = startH;
            for (int i = 0; i < m.MessageCount; i++)
            {
                contentH += UiDraw.BubbleHeight(ref m.Messages[i], inner) + (m.Messages[i].HasChoices ? UiTokens.S2 + UiTokens.Chip : 0f) + UiTokens.S3;
            }
            float typingH = m.Typing ? UiTokens.S10 + UiTokens.S3 : 0f;
            contentH += typingH;
            float most = Math.Max(0f, contentH - vh);
            float offset = m.ThreadOffset < 0f ? most : Math.Min(m.ThreadOffset, most);
            short clip = s.AddClip(x, vy, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            s.Clips[clip].Fills = true;   // a short thread sits at the foot under the line of its beginning: the room above is the thread's
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float my = contentH < vh ? vh - contentH : 0f;   // a short thread sits at the bottom, by the composer
            my = UiDraw.SnapDown(my);
            if (startH > 0f)
            {
                if (my + startH > offset && my < offset + vh)
                {
                    UiDraw.Divider(s, inClip, 0f, my, w, m.ThreadStart);
                }
                my += startH;
            }
            for (int i = 0; i < m.MessageCount; i++)
            {
                ref UiMessageModel msg = ref m.Messages[i];
                float bh = UiDraw.BubbleHeight(ref msg, inner) + (msg.HasChoices ? UiTokens.S2 + UiTokens.Chip : 0f);
                if (my + bh > offset && my < offset + vh)
                {
                    UiAt arriving = inClip.Arriving(i);
                    UiDraw.Bubble(s, arriving, UiTokens.S6, my, inner, ref msg);
                    if (msg.HasChoices)
                    {
                        float cw = UiDraw.ButtonWidth(UiButtonKind.Chip, m.ClassicLabel, UiIcon.Next, UiAction.Count, null);
                        float chx = msg.From == UiFrom.Me ? UiTokens.S6 + inner - cw : UiTokens.S6;
                        UiDraw.Button(s, arriving, chx, my + bh - UiTokens.Chip, cw, UiTokens.Chip, UiButtonKind.Chip, m.ClassicLabel, UiIcon.Next, UiAction.Count, null, Classic, i);
                    }
                }
                my += bh + UiTokens.S3;
            }
            if (m.Typing)
            {
                int bubble = UiDraw.Box(s, inClip, UiTokens.S6, my, UiTokens.S16 + UiTokens.S4, UiTokens.S10, UiRole.Bubble, UiRole.None, UiTokens.RadiusLarge);
                var dots = new UiItem
                {
                    Kind = UiKind.Busy, Fill = UiRole.AccentText, X = UiTokens.S6 + UiTokens.S4, Y = my + UiTokens.S4 + UiTokens.S1, W = UiTokens.S12, H = UiTokens.S2,
                    Clip = clip, Node = -1, Layer = at.Layer, Order = -1, Parent = bubble, Alpha = 1f
                };
                s.Add(ref dots);
            }
        }

        private static float ChipsHeight(string[] labels, int count, float w)
        {
            float cx = 0f, rows = 1f;
            for (int i = 0; i < count; i++)
            {
                float cw = Math.Min(w, UiDraw.ButtonWidth(UiButtonKind.Chip, labels[i], UiIcon.None, UiAction.Count, null));
                if (cx > 0f && cx + cw > w)
                {
                    cx = 0f;
                    rows++;
                }
                cx += cw + UiTokens.S2;
            }
            return count > 0 ? rows * UiTokens.Chip + (rows - 1f) * UiTokens.S2 : 0f;
        }

        private static void Aside(UiScene s, UiAt at, UiListDetailModel m, float x, float y, float w, float h)
        {
            float ix = UiTokens.S5, iw = w - UiTokens.S5 * 2f;
            float vy = y + UiTokens.S5, vh = UiDraw.SnapDown(h - UiTokens.S5 * 2f);
            float contentH = AsideHeight(m, iw);
            float offset = Math.Max(0f, Math.Min(m.SideOffset, contentH - vh));
            short clip = s.AddClip(x, vy, w, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float cy = 0f;
            if (m.ServiceCount > 0)
            {
                cy += BlockTitle(s, inClip, ix, cy, iw, m.ServicesTitle);
                for (int i = 0; i < m.ServiceCount; i++)
                {
                    UiDraw.ServiceCard(s, inClip.Arriving(i), ix, cy, iw, ref m.Services[i], Service, i, out float sh);
                    cy += sh + UiTokens.S2;
                }
                cy += UiTokens.S4;
            }
            if (m.StatCount > 0)
            {
                cy += BlockTitle(s, inClip, ix, cy, iw, m.StatsTitle);
                for (int i = 0; i < m.StatCount; i++)
                {
                    cy += UiDraw.Snap(UiDraw.StatBar(s, inClip, ix, cy, iw, ref m.Stats[i])) + UiTokens.S4;
                }
                cy += UiTokens.S2;
            }
            if (m.DetailCount > 0)
            {
                cy += BlockTitle(s, inClip, ix, cy, iw, m.DetailsTitle);
                for (int i = 0; i < m.DetailCount; i++)
                {
                    cy += UiDraw.KeyValue(s, inClip, ix, cy, iw, m.Details[i].Key, m.Details[i].Value);
                }
            }
        }

        private static float BlockTitle(UiScene s, UiAt at, float x, float y, float w, string title)
        {
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Muted, title, UiFlags.Label);
            return UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
        }

        private static float AsideHeight(UiListDetailModel m, float iw)
        {
            float blockTitle = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            float h = 0f;
            if (m.ServiceCount > 0)
            {
                h += blockTitle + UiTokens.S4;
                for (int i = 0; i < m.ServiceCount; i++)
                {
                    float pw = UiTextRules.Width(m.Services[i].Price, UiStyle.SmallStrong) + UiTokens.S1;
                    float titleH = UiTextRules.WrappedHeight(m.Services[i].Title, UiStyle.Title, iw - UiTokens.S4 * 2f - pw - UiTokens.S3, 2);
                    float descH = string.IsNullOrEmpty(m.Services[i].About) ? 0f : UiTextRules.WrappedHeight(m.Services[i].About, UiStyle.Small, iw - UiTokens.S4 * 2f, 2);
                    h += UiDraw.Snap(UiTokens.S4 * 2f + titleH + (descH > 0f ? UiTokens.S1 + descH : 0f)) + UiTokens.S2;
                }
            }
            if (m.StatCount > 0)
            {
                h += blockTitle + UiTokens.S2;
                h += m.StatCount * (UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2 + UiTokens.Segment) + UiTokens.S4);
            }
            if (m.DetailCount > 0)
            {
                h += blockTitle + m.DetailCount * UiTokens.PairRow;
            }
            return h;
        }
    }
}
