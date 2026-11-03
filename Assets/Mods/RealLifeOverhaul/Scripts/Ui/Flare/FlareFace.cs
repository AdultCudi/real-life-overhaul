using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One screen of Flare: which page, and the words already chosen for it.</summary>
    public sealed class FlareFaceModel
    {
        public string Name = "flare";
        public int Page;
        public int NavAt;
        public string[] Nav = new string[0];
        public UiIcon[] NavIcon = new UiIcon[0];
        public string Post = "Post";
        public string Field;
        public string[] Chips = new string[0];
        public int ChipAt = -1;
        public string[] Story = new string[0];
        public float[] StoryHue = new float[0];
        public UiFeedModel Feed;
        public UiMessageListModel Messages;
        public string Head;
        public string[] Label = new string[0];
        public string[] Value = new string[0];
        public int Links;          // the first this many rows are links
        public string EmptyTitle;
        public string EmptyBody;
        public string Action;
    }

    /// <summary>
    /// Flare laid out as a social app: a rail with the flame and a Post button, and one page in the rest.
    /// Home is a feed. The other pages are the creator's tools in the same shell. Game-free.
    /// Cost: a layout when the page changes, nothing per frame.
    /// </summary>
    public static class FlareFace
    {
        public const int NavCommand = 70;
        public const int PostCommand = 71;
        public const int RowCommand = 73;
        public const int ChipCommand = 74;
        public const int StoryCommand = 75;
        public const int FieldCommand = 76;
        public const int ActionCommand = 77;

        private const float Rail = 248f;

        public static readonly int[] Commands =
        {
            NavCommand, PostCommand, RowCommand, ChipCommand, StoryCommand, FieldCommand, ActionCommand,
            UiFeed.AuthorCommand, UiFeed.ReactCommand, UiFeed.ProfileCommand, UiFeed.ListCommand, UiFeed.PersonCommand,
            UiMessageList.RowCommand, UiMessageList.SearchCommand, UiMessageList.ClearCommand
        };

        public static void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys, UiWords words, FlareFaceModel m)
        {
            if (m == null)
            {
                m = new FlareFaceModel();
            }
            if (frame.Icon == UiIcon.None)
            {
                frame.Icon = UiIcon.Fire;
            }
            if (string.IsNullOrEmpty(frame.Title))
            {
                frame.Title = words("reallife_flare_app", "Flare");
            }
            s.Begin(m.Name, frame.Width, frame.Height, dark);
            int panel = UiDraw.Window(s, frame, null, keys);
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float y = wy + UiFrame.Strip;
            float h = UiDraw.SnapDown(wh - UiFrame.Strip - UiTokens.S4);
            float x = wx + UiTokens.S4;
            float w = UiDraw.SnapDown(ww - UiTokens.S8);
            UiAt at = UiAt.Screen.In(panel);
            RailOf(s, at.InRegion(4), m, x, y, h, keys);
            float cx = x + Rail + UiTokens.S4;
            float cw = UiDraw.SnapDown(w - Rail - UiTokens.S4);
            Page(s, at, m, cx, y, cw, h, keys, words);
            s.AddPrompt(UiAction.Move, words("reallife_ui_prompt_move", "Move"));
            s.AddPrompt(UiAction.Confirm, words("reallife_ui_prompt_select", "Select"));
            s.AddPrompt(UiAction.Alt, m.Post);
            s.AddPrompt(UiAction.Back, words("reallife_ui_prompt_back", "Back"));
            UiDraw.PromptBar(s, frame, keys);
        }

        private static void RailOf(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float h, IUiKeys keys)
        {
            int rail = UiDraw.Box(s, at, x, y, Rail, h, UiRole.Band, UiRole.None, UiTokens.RadiusMedium);
            UiAt on = at.In(rail);
            int n = m.Nav != null ? m.Nav.Length : 0;
            float iy = y + UiTokens.S4;
            for (int i = 0; i < n; i++)
            {
                UiIcon icon = m.NavIcon != null && i < m.NavIcon.Length ? m.NavIcon[i] : UiIcon.Info;
                UiDraw.NavItem(s, on, x + UiTokens.S2, iy, Rail - UiTokens.S4, m.Nav[i], null, 0f, i == m.NavAt, NavCommand, i, false, icon, UiRole.None);
                iy += UiTokens.Control;
            }
            float by = y + h - UiTokens.S4 - UiTokens.Control;
            int face = UiDraw.Box(s, on, x + UiTokens.S4, by, Rail - UiTokens.S8, UiTokens.Control, UiRole.Chart6, UiRole.None, UiTokens.RadiusMedium);
            UiDraw.TextMiddle(s, on.In(face), x + UiTokens.S4, by, UiTokens.Control, Rail - UiTokens.S8, UiStyle.Button, UiRole.OnAccent, m.Post, UiFlags.Centre);
            UiDraw.Node(s, on, x + UiTokens.S4, by, Rail - UiTokens.S8, UiTokens.Control, PostCommand, 0, face, false, -1, 0, UiAction.Alt);
        }

        private static void Page(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys, UiWords words)
        {
            if (m.Page == 0 || m.Page == 1)
            {
                Home(s, at, m, x, y, w, h, keys);
                return;
            }
            if (m.Page == 3 && m.Messages != null)
            {
                UiMessageList.Lay(s, at, m.Messages, x, y, w, h);
                return;
            }
            float yy = y;
            if (!string.IsNullOrEmpty(m.Head))
            {
                UiDraw.Text(s, at, x, yy + UiTokens.S2, w, UiStyle.Body, UiRole.Text, m.Head, UiFlags.Truncate);
                yy += UiTokens.Control;
            }
            if (m.Page == 7)
            {
                Field(s, at, m, x, yy, w);
                yy += UiTokens.Control + UiTokens.S3;
                yy += UiDraw.Chips(s, at, x, yy, w, m.Chips, m.Chips != null ? m.Chips.Length : 0, ChipCommand) + UiTokens.S3;
            }
            int rows = m.Label != null ? m.Label.Length : 0;
            if (rows == 0 && !string.IsNullOrEmpty(m.EmptyTitle))
            {
                UiDraw.Empty(s, at, x, yy, w, Math.Max(UiTokens.S16, h - (yy - y)), m.EmptyTitle, m.EmptyBody);
            }
            for (int i = 0; i < rows; i++)
            {
                bool link = i < m.Links;
                yy += UiDraw.InfoRow(s, at, x, yy, w, m.Label[i], m.Value != null && i < m.Value.Length ? m.Value[i] : string.Empty, link, link ? RowCommand : -1, i);
            }
            if (!string.IsNullOrEmpty(m.Action))
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Action, UiIcon.None, UiAction.Option, keys);
                UiDraw.Button(s, at, x, y + h - UiTokens.S4 - UiTokens.Control, bw, UiTokens.Control, UiButtonKind.Secondary, m.Action, UiIcon.None, UiAction.Option, keys, ActionCommand, 0);
            }
        }

        private static void Home(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            Field(s, at, m, x, y, w);
            float yy = y + UiTokens.Control + UiTokens.S3;
            int stories = m.Story != null ? m.Story.Length : 0;
            if (stories > 0)
            {
                float sx = x;
                for (int i = 0; i < stories && i < 6; i++)
                {
                    float hue = m.StoryHue != null && i < m.StoryHue.Length ? m.StoryHue[i] : 20f;
                    int face = UiDraw.Portrait(s, at, sx, yy, UiTokens.AvatarSmall, m.Story[i], hue, UiRole.Ok);
                    UiDraw.Node(s, at, sx, yy, UiTokens.AvatarSmall, UiTokens.AvatarSmall, StoryCommand, i, face);
                    sx += UiTokens.AvatarSmall + UiTokens.S4;
                }
                yy += UiTokens.AvatarSmall + UiTokens.S4;
            }
            if (m.Chips != null && m.Chips.Length > 0)
            {
                yy += UiDraw.Chips(s, at, x, yy, w, m.Chips, m.Chips.Length, ChipCommand) + UiTokens.S3;
            }
            float left = h - (yy - y);
            if (left < UiTokens.S16)
            {
                left = UiTokens.S16;
            }
            if (m.Feed != null)
            {
                UiFeed.Lay(s, at, m.Feed, x, yy, w, left, keys);
            }
            else if (!string.IsNullOrEmpty(m.EmptyTitle))
            {
                UiDraw.Empty(s, at, x, yy, w, left, m.EmptyTitle, m.EmptyBody);
            }
        }

        private static void Field(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w)
        {
            int face = UiDraw.Box(s, at, x, y, w, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.RadiusMedium);
            string words = string.IsNullOrEmpty(m.Field) ? string.Empty : m.Field;
            if (words.Length > 0)
            {
                UiDraw.TextMiddle(s, at.In(face), x + UiTokens.S3, y, UiTokens.Control, w - UiTokens.S6, UiStyle.Body, UiRole.Text, words, UiFlags.Truncate);
            }
            UiDraw.Node(s, at, x, y, w, UiTokens.Control, FieldCommand, 0, face);
        }
    }
}
