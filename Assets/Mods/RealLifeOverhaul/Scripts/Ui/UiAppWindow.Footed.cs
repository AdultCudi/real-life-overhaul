using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What stands at the foot of an app window's sidebar: the actions on all its sections (mark all as read, delete all),
    /// and the player's own card under them; each left out when empty.
    /// </summary>
    public sealed class UiSideFoot
    {
        public UiActionModel[] Actions = new UiActionModel[0];   // each runs the foot's command with its own argument
        public string Player;                                     // the player's name; null for no card
        public float PlayerHue;
        public string PlayerLine;                                 // a line of figures under the name
    }

    public static partial class UiAppWindow
    {
        private const float FootButton = UiTokens.ControlSmall;   // px: a foot action, a secondary button's height

        /// <summary>The footed sidebar's width: the approved Contacts window's groups column, where a translated foot action fits one line.</summary>
        public const float FootedSidebar = 288f;   // px

        /// <summary>
        /// The window and its sidebar, as <see cref="Shell"/> draws it, for an app whose sections may outnumber the sidebar's
        /// room (Contacts' groups, the game's and the mod's): the entries scroll in their frame above a foot of actions and
        /// the player's card, so a long list never runs over the foot. Writes back the scroll it took. The entries answer
        /// <see cref="NavCommand"/> with their index, the foot's actions <paramref name="footCommand"/> with theirs.
        /// Returns the window to lay the page in, with the page's room. Game-free. Cost: a layout a change.
        /// </summary>
        public static UiAt ShellFooted(UiScene s, UiFrame frame, string title, UiIcon icon, UiNavEntry[] nav, int selected, ref float offset, UiSideFoot foot,
            int footCommand, IUiKeys keys, out float x, out float y, out float w, out float bottom)
        {
            frame.Icon = icon;
            int win = UiDraw.Window(s, frame, title, keys);
            UiAt at = UiAt.Screen.In(win);
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float top = wy + UiFrame.Strip, foot0 = wy + wh;
            UiDraw.Band(s, at, wx, top, FootedSidebar, wh - UiFrame.Strip);
            float ex = wx + UiTokens.S2, ew = FootedSidebar - UiTokens.S4;
            float footTop = Foot(s, at, foot, footCommand, ex, foot0 - UiTokens.S4, ew, keys);
            float navTop = top + UiTokens.S3, navH = UiDraw.SnapDown(footTop - UiTokens.S4 - navTop);
            int n = nav == null ? 0 : nav.Length;
            float rowH = NavRow + UiTokens.S1;
            bool grouped = Grouped(nav);
            if (grouped)
            {
                MeasureGroups(nav);
            }
            float content = grouped ? GroupContent : n * rowH;
            offset = Math.Max(0f, Math.Min(offset, content - navH));
            short clip = s.AddClip(ex, navTop, ew, navH, offset);
            s.Clips[clip].ContentHeight = content;
            float entryW = content > navH + 0.5f ? ew - UiTokens.S2 : ew;   // a list that scrolls leaves its thumb a gutter of its own
            int first, last;
            if (!grouped)
            {
                first = Math.Max(0, Math.Min(n - 1, (int)(offset / rowH)));
                last = Math.Max(first, Math.Min(n - 1, (int)((offset + navH - 1f) / rowH)));
            }
            else
            {
                first = 0;
                last = n - 1;
                while (n > 1 && first < n - 1 && GroupBox(first) + rowH <= offset)
                {
                    first++;
                }
                last = first;
                while (n > 1 && last < n - 1 && GroupBox(last + 1) < offset + navH)
                {
                    last++;
                }
            }
            short list = s.AddList(n, first, n == 0 ? 0 : last - first + 1, clip, selected);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            int shownNode = -1, firstNode = -1;
            for (int i = first; n > 0 && i <= last; i++)
            {
                float ey = grouped ? GroupBox(i) : i * rowH;
                if (grouped && StartsGroup(nav, i))
                {
                    GroupCaption(s, inClip, 0f, ey - GroupLabel, entryW, nav[i].Group);
                }
                int node = NavEntry(s, inClip, ref nav[i], i, i == selected, 0f, ey, entryW, list, true);
                shownNode = i == selected ? node : shownNode;
                firstNode = firstNode < 0 ? node : firstNode;
            }
            UiDraw.ScrollThumb(s, at, wx + FootedSidebar - UiTokens.S1, navTop, navH, offset, content);
            if (shownNode >= 0 || firstNode >= 0)
            {
                s.FocusFirst = shownNode >= 0 ? shownNode : firstNode;   // the section in view, or the first in view when it has scrolled away; never the close
            }
            x = wx + FootedSidebar + UiTokens.S5;
            w = wx + ww - UiTokens.S5 - x;
            y = top + UiTokens.S4;
            bottom = wy + wh - UiTokens.S4;
            return at;
        }

        // The foot up from its bottom edge: the player's card at the foot, the actions stacked over it. Returns the foot's top.
        private static float Foot(UiScene s, UiAt at, UiSideFoot foot, int command, float x, float bottom, float w, IUiKeys keys)
        {
            if (foot == null)
            {
                return bottom;
            }
            float y = bottom;
            if (!string.IsNullOrEmpty(foot.Player))
            {
                y -= UiTokens.AvatarSmall;
                UiDraw.PlayerCard(s, at, x + UiTokens.S3, y, w - UiTokens.S3 * 2f, foot.Player, foot.PlayerHue, foot.PlayerLine);   // on the entries' tiles' edge
                y -= UiTokens.S4;
            }
            int n = foot.Actions == null ? 0 : foot.Actions.Length;
            float bh = FootButton;
            for (int i = 0; i < n; i++)
            {
                // A longer translation takes two lines, and its pair matches it.
                bh = Math.Max(bh, UiDraw.ButtonHeight(foot.Actions[i].Kind, foot.Actions[i].Label, foot.Actions[i].Key, keys, w, FootButton));
            }
            for (int i = n - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref foot.Actions[i];
                y -= bh;
                UiDraw.Button(s, at, x, y, w, bh, a.Kind, a.Label, a.Icon, a.Key, keys, command, a.Arg, a.Disabled);
                y -= UiTokens.S2;
            }
            return y;
        }
    }
}
