using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One of the open contact's actions beside their conversation: its words, its glyph, and whether it clears something.</summary>
    public struct UiContactAction
    {
        public string Label;
        public UiIcon Icon;
        public bool Clears;          // it takes something away (the chat): a quiet button with the bin, not a link
    }

    /// <summary>
    /// What the Contacts window shows: the groups down the sidebar with the actions on all of them at its foot, the people
    /// and firms as a message list, the chosen one's head, conversation, actions, profile and details; or a call in
    /// progress, as a card over the dimmed world in the window's place.
    /// </summary>
    public sealed class UiContactsModel
    {
        public string Title;
        public UiNavEntry[] Groups = new UiNavEntry[0];
        public int GroupShown;
        public float GroupsOffset;                                // written back by the layout
        public UiSideFoot Foot = new UiSideFoot();
        public UiMessageListModel List = new UiMessageListModel();
        public bool HasDetail;
        public string PickTitle, PickBody;                        // nobody chosen yet
        public UiHeadModel Head = new UiHeadModel();
        public UiActionModel[] HeadActions = new UiActionModel[0];   // View on map, then Call (the primary, last)
        public UiConversationModel Talk = new UiConversationModel();
        public string ActionsTitle;
        public UiContactAction[] Actions = new UiContactAction[0];
        public string ProfileTitle;
        public string[] ProfileLabels = new string[0];
        public int[] ProfileValues = new int[0];                  // 0 to 100, graded
        public string DetailsTitle;
        public UiPairModel[] Details = new UiPairModel[0];
        public float AsideOffset;                                 // written back by the layout
        public UiCallModel Call;                                  // a call in progress: the card in the window's place; null for none
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
        public int GroupsClip = -1, ListClip = -1, TalkClip = -1, AsideClip = -1;   // written back: the scroll frames, for the wheel
        public int GroupsList = -1;                                                // written back: the groups' list, for the keys' reveal
    }

    /// <summary>
    /// The game's Contacts app as an app window (DIRECTION_CS2, section 4.3; the approved Contacts window): the groups as
    /// the sidebar's sections, each with its glyph and count, Mark all as read, Delete all and the player's card at its
    /// foot; the message list in the first column; then the chosen person's or firm's head across the rest, with View on
    /// map and Call; under it their conversation, read as a phone's messages with every offer and every reply a numbered
    /// choice and the game's offer forms in its form slot; and beside it what can be done for them, their profile graded
    /// and their details. A call in progress stands as the call card over the dimmed world (UiCallCard). Commands: the
    /// shell's <see cref="UiAppWindow.NavCommand"/>, the list's and the conversation's own, <see cref="UiAppWindow.ActionCommand"/>
    /// (the head's), <see cref="SideCommand"/>, <see cref="ActionCommand"/> and the call card's. Game-free.
    /// Cost: a layout a change, only the rows in view laid out.
    /// </summary>
    public static class UiContacts
    {
        public const int SideCommand = 220, ActionCommand = 221;   // the sidebar's foot (its action's index), the aside's actions (theirs)
        public const float ListColumn = 400f;                      // px: the message list, as the approved window's
        public const float AsideWidth = 344f;                      // px: an action's words beside its arrow, a detail's label beside its value
        public const byte RegionGroups = 1, RegionList = 2, RegionTalk = 3, RegionAside = 4;   // the d-pad's columns

        public static void Lay(UiScene s, UiContactsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (m.Call != null)
            {
                UiCallCard.Lay(s, m.Call, frame, dark, keys);
                return;
            }
            s.Begin("contacts", frame.Width, frame.Height, dark);
            float offset = m.GroupsOffset;
            m.GroupsClip = s.ClipCount;
            m.GroupsList = s.ListCount;
            m.ListClip = m.TalkClip = m.AsideClip = -1;
            UiAt at = UiAppWindow.ShellFooted(s, frame, m.Title, UiIcon.AddressBook, m.Groups, m.GroupShown, ref offset, m.Foot, SideCommand, keys,
                out float x, out float y, out float w, out float bottom);
            m.GroupsOffset = offset;
            float h = bottom - y;
            m.ListClip = s.ClipCount;
            UiMessageList.Lay(s, at.InRegion(RegionList), m.List, x, y, ListColumn, h);
            float rx = x + ListColumn + UiTokens.S6, rw = x + w - rx;
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            UiDraw.Box(s, at, rx - UiTokens.S3, wy + UiFrame.Strip, UiTokens.Hairline, wh - UiFrame.Strip, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            if (!m.HasDetail)
            {
                UiDraw.EmptyState(s, at, rx, y, rw, h, UiIcon.AddressBook, m.PickTitle, m.PickBody);
            }
            else
            {
                Detail(s, at, m, rx, y, rw, bottom, wx + ww, keys);
            }
            // Down at the list's last row stays there: the groups are Left, never a jump up the sidebar.
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Command == UiMessageList.RowCommand && s.Nodes[i].Index == m.List.RowCount - 1)
                {
                    s.Nodes[i].Down = i;
                }
            }
            // A head with no buttons leaves the title strip's close over the aside: Down from it goes into the actions under it.
            int close = s.FindNode(UiDraw.CloseCommand, 0), first = s.FindNode(ActionCommand, 0);
            if (m.HasDetail && m.HeadActions.Length == 0 && close >= 0 && first >= 0)
            {
                s.Nodes[close].Down = first;
            }
            // The groups, the list, the talk and the aside each a column the d-pad crosses in one step.
            UiDraw.JoinColumns(s, wx, x - UiTokens.S5 + UiTokens.S1, rx - UiTokens.S3);
            UiDraw.JoinColumns(s, x, rx - UiTokens.S3, wx + ww);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The chosen one: the head across, then the conversation and, beside it, the aside, a hairline between.
        private static void Detail(UiScene s, UiAt at, UiContactsModel m, float x, float y, float w, float bottom, float windowRight, IUiKeys keys)
        {
            float under = UiAppWindow.Head(s, at.InRegion(RegionAside), m.Head, m.HeadActions, x, y, w, keys);   // its actions head the aside's column for the d-pad
            float rule = under + UiTokens.S4;
            UiDraw.Box(s, at, x - UiTokens.S3, rule, windowRight - x + UiTokens.S3, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float by = rule + UiTokens.S5;
            float ax = x + w - AsideWidth, tw = ax - UiTokens.S6 - x;
            UiDraw.Box(s, at, ax - UiTokens.S3, rule, UiTokens.Hairline, bottom + UiTokens.S4 - rule, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            m.TalkClip = s.ClipCount;
            UiConversation.Body(s, at.InRegion(RegionTalk), m.Talk, x, by, tw, bottom - by, keys);
            m.AsideClip = s.ClipCount;
            Aside(s, at.InRegion(RegionAside), m, ax, by, AsideWidth, bottom - by);
        }

        // What can be done for them, their profile graded and their details, in a frame that scrolls when it runs long.
        private static void Aside(UiScene s, UiAt at, UiContactsModel m, float x, float y, float w, float h)
        {
            float content = AsideHeight(m);
            float vh = UiDraw.SnapDown(h);
            float offset = Math.Max(0f, Math.Min(m.AsideOffset, content - vh));
            m.AsideOffset = offset;
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = content;
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, vh, offset, content);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float cy = 0f;
            if (m.Actions.Length > 0)
            {
                cy += Title(s, inClip, cy, w, m.ActionsTitle);
                for (int i = 0; i < m.Actions.Length; i++)
                {
                    ref UiContactAction a = ref m.Actions[i];
                    if (a.Clears)
                    {
                        cy += UiTokens.S2;
                        float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, a.Label, a.Icon, UiAction.Count, null);
                        UiDraw.Button(s, inClip, 0f, cy, Math.Min(w, bw), UiTokens.ControlSmall, UiButtonKind.Secondary, a.Label, a.Icon, UiAction.Count, null, ActionCommand, i);
                        cy += UiTokens.ControlSmall;
                        continue;
                    }
                    UiDraw.LinkRow(s, inClip, 0f, cy, w, UiTokens.ControlSmall, a.Label, ActionCommand, i, false);
                    cy += UiTokens.ControlSmall;
                }
                cy += UiTokens.S6;
            }
            int bars = Math.Min(m.ProfileLabels.Length, m.ProfileValues.Length);
            if (bars > 0)
            {
                cy += Title(s, inClip, cy, w, m.ProfileTitle);
                float lw = UiDraw.LabelColumn(w, m.ProfileLabels);
                for (int i = 0; i < bars; i++)
                {
                    cy += UiDraw.GradeBar(s, inClip, 0f, cy, w, m.ProfileLabels[i], m.ProfileValues[i], lw);
                }
                cy += UiTokens.S6;
            }
            if (m.Details.Length > 0)
            {
                cy += Title(s, inClip, cy, w, m.DetailsTitle);
                for (int i = 0; i < m.Details.Length; i++)
                {
                    cy += UiDraw.InfoRow(s, inClip, 0f, cy, w, m.Details[i].Key, m.Details[i].Value);
                }
            }
        }

        private static float Title(UiScene s, UiAt at, float y, float w, string title)
        {
            UiDraw.Text(s, at, 0f, y, w, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            return UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
        }

        /// <summary>The aside's height: its three sections, each with its title, as it lays them out.</summary>
        public static float AsideHeight(UiContactsModel m)
        {
            float title = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            float h = 0f;
            if (m.Actions.Length > 0)
            {
                h += title + UiTokens.S6;
                for (int i = 0; i < m.Actions.Length; i++)
                {
                    h += UiTokens.ControlSmall + (m.Actions[i].Clears ? UiTokens.S2 : 0f);
                }
            }
            int bars = Math.Min(m.ProfileLabels.Length, m.ProfileValues.Length);
            if (bars > 0)
            {
                h += title + bars * UiTokens.PairRow + UiTokens.S6;
            }
            if (m.Details.Length > 0)
            {
                h += title + m.Details.Length * UiTokens.PairRow;
            }
            return h;
        }
    }
}
