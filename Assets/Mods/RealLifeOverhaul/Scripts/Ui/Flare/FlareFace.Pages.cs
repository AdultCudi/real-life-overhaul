using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's reading pages: Home with its tabs, composer, stories and feed; Explore with its search, tags and accounts; the
    /// notifications; the inbox beside a conversation; a profile; a post's thread. Game-free.
    /// </summary>
    public static partial class FlareFace
    {
        public const float ComposerHeight = 112f, StoriesHeight = 104f, StoryWidth = 72f, StoryRing = 56f;
        public const int MostStories = 8;

        private static void Page(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            switch (m.Page)
            {
                case Home: HomePage(s, at, m, x, y, w, h, keys); break;
                case Explore: ExplorePage(s, at, m, x, y, w, h, keys); break;
                case Notices: NoticesPage(s, at, m, x, y, w, h, keys); break;
                case Messages: MessagesPage(s, at, m, x, y, w, h, keys); break;
                case Profile: ProfilePage(s, at, m, x, y, w, h, keys); break;
                case Studio: StudioPage(s, at, m, x, y, w, h, keys); break;
                case Business: BusinessPage(s, at, m, x, y, w, h, keys); break;
                case Hire: HirePage(s, at, m, x, y, w, h, keys); break;
                case City: CityPage(s, at, m, x, y, w, h, keys); break;
                case Thread: ThreadPage(s, at, m, x, y, w, h, keys); break;
            }
        }

        // ------------------------------------------------------------------------------------------------ Home

        private static void HomePage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float bottom = y + h;
            float cy = y + TabRow(s, at, x, y, w, m.Tabs, m.Tab);
            Composer(s, at, m, x, cy, w);
            cy += ComposerHeight;
            int stories = Math.Min(MostStories, m.Stories != null ? m.Stories.Length : 0);
            if (stories > 0)
            {
                Stories(s, at, m, x, cy, w, stories);
                cy += StoriesHeight;
            }
            List(s, at, m.List, x, cy, w, bottom - cy, keys);
        }

        // What's happening: the player's face, the words that open the composer, its chips and Post.
        private static void Composer(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w)
        {
            int box = UiDraw.Box(s, at, x, y, w, ComposerHeight, UiRole.None, UiRole.None, 0f);
            UiAt inBox = at.In(box);
            Face(s, inBox, x + UiTokens.S4, y + UiTokens.S4, UiTokens.AvatarRow, m.MyName, m.MyHue, true, m.MyRing, false);
            float tx = x + ContentX, tw = w - ContentX - UiTokens.S4;
            int hint = UiDraw.Box(s, inBox, tx, y + UiTokens.S4, tw, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            // on the panel's own slate, where Muted is already as dim as the words may be
            UiDraw.TextMiddle(s, inBox.In(hint), tx, y + UiTokens.S4, UiTokens.Control, tw, UiStyle.Body, UiRole.Muted, m.ComposerHint, UiFlags.Truncate);
            UiDraw.Node(s, inBox, tx, y + UiTokens.S4, tw, UiTokens.Control, ComposerCommand, 0, hint);
            float py = y + UiTokens.S4 + UiTokens.Control + UiTokens.S3;
            float pw = ButtonWidth(m.Post);
            float right = tx + tw - pw;
            Orange(s, inBox, right, py, pw, UiTokens.ControlSmall, m.Post, PostCommand, 1);
            float cx = tx;
            int n = Math.Min(m.Chips.Length, m.ChipIcons.Length);
            for (int i = 0; i < n; i++)
            {
                float cw = IconPillWidth(m.Chips[i]);
                if (cx + cw > right - UiTokens.S2)
                {
                    break;   // a narrow window keeps the chips that fit; the sheet holds them all
                }
                Pill(s, inBox, cx, py, cw, m.Chips[i], PillKind.Line, ChipCommand, i, m.ChipIcons[i]);
                cx += cw + UiTokens.S2;
            }
            UiDraw.Box(s, at, x, y + ComposerHeight - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
        }

        // The stories: a ring for each account followed with something new, the player's businesses in the accent's ring.
        private static void Stories(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, int n)
        {
            float sx = x + UiTokens.S4, sy = y + UiTokens.S3;
            float boxH = StoryRing + UiTokens.S1 + L(UiStyle.Small);
            for (int i = 0; i < n; i++)
            {
                if (sx + StoryWidth > x + w - UiTokens.S4)
                {
                    break;
                }
                bool shop = i < m.StoryShops.Length && m.StoryShops[i];
                int box = UiDraw.Box(s, at, sx, sy, StoryWidth, boxH, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inBox = at.In(box);
                float rx = sx + (StoryWidth - StoryRing) / 2f;
                bool person = i < m.StoryPeople.Length && m.StoryPeople[i];
                UiRole mood = person && i < m.StoryRings.Length ? m.StoryRings[i] : UiRole.None;
                int ring = UiDraw.Circle(s, inBox, rx, sy, StoryRing, StoryRing, mood != UiRole.None ? mood : shop ? UiRole.Accent : UiRole.Chart2);   // a person's ring is their mood
                int gap = UiDraw.Circle(s, inBox.In(ring), rx + 2f, sy + 2f, StoryRing - UiTokens.S1, StoryRing - UiTokens.S1, UiRole.Panel, UiFlags.Middle | UiFlags.Centre);
                int face = Face(s, inBox.In(gap), rx + UiTokens.S1, sy + UiTokens.S1, StoryRing - UiTokens.S2, m.Stories[i], i < m.StoryHues.Length ? m.StoryHues[i] : 0f, person, UiRole.None, false);
                s.Items[face].Flags |= UiFlags.Middle | UiFlags.Centre;
                Line(s, inBox, sx, sy + StoryRing + UiTokens.S1, StoryWidth, UiStyle.Small, UiRole.Text, m.Stories[i], UiFlags.Centre | UiFlags.Truncate);
                UiDraw.Node(s, at, sx, sy, StoryWidth, boxH, StoryCommand, i, box);
                sx += StoryWidth + UiTokens.S3;
            }
            UiDraw.Box(s, at, x, y + StoriesHeight - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
        }

        // ------------------------------------------------------------------------------------------------ Explore

        private static void ExplorePage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float cy = y + UiTokens.S4;
            TextField(s, at, x + UiTokens.S4, cy, w - UiTokens.S8, m.Search, m.SearchHint, SearchField, UiIcon.Search);
            cy += UiTokens.Control + UiTokens.S3;
            if (!string.IsNullOrEmpty(m.Head))
            {
                cy += PageHead(s, at, x, cy, w, m.Head, m.HeadLine, true);
            }
            UiDraw.Box(s, at, x, cy - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            List(s, at, m.List, x, cy, w, y + h - cy, keys);
        }

        /// <summary>A page's head: the way back when it has one, its title and a line under it. Returns its height.</summary>
        private static float PageHead(UiScene s, UiAt at, float x, float y, float w, string title, string line, bool back, float reserve = 0f)
        {
            float h = UiTokens.S16;
            float tx = x + UiTokens.S4;
            if (back)
            {
                float by = y + UiTokens.S3;
                int b = UiDraw.Box(s, at, x + UiTokens.S2, by, UiTokens.Control, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.Icon(s, at.In(b), x + UiTokens.S2 + UiTokens.S3 - 2f, by + UiTokens.S3 - 2f, UiTokens.Icon, UiIcon.Back, UiRole.Text, UiFlags.Middle | UiFlags.Centre);
                UiDraw.Node(s, at, x + UiTokens.S2, by, UiTokens.Control, UiTokens.Control, BackCommand, 0, b);
                tx = x + UiTokens.S2 + UiTokens.Control + UiTokens.S2;
            }
            float tw = x + w - tx - UiTokens.S4 - reserve;
            bool lined = !string.IsNullOrEmpty(line);
            float ty = lined ? y + UiTokens.S2 : y + (h - L(UiStyle.Number)) / 2f;
            Line(s, at, tx, UiDraw.SnapDown(ty), tw, UiStyle.Number, UiRole.Text, title, UiFlags.Truncate);
            if (lined)
            {
                Line(s, at, tx, y + UiTokens.S2 + L(UiStyle.Number), tw, UiStyle.Small, UiRole.Text2, line, UiFlags.Truncate);
            }
            return h;
        }

        // ------------------------------------------------------------------------------------------------ Notifications, In the city

        // A page that is its head and one list: the notifications, the city's cards.
        private static void ListPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false);
            UiDraw.Box(s, at, x, cy - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float lx = x + (m.Page == City ? UiTokens.S4 : 0f), lw = w - (m.Page == City ? UiTokens.S8 : 0f);
            List(s, at, m.List, lx, cy + (m.Page == City ? UiTokens.S3 : 0f), lw, y + h - cy - (m.Page == City ? UiTokens.S3 : 0f), keys);
        }

        // The notifications: the head with Mark all read, the tabs by what a notice is about, and the list.
        private static void NoticesPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, false, string.IsNullOrEmpty(m.MarkAll) ? 0f : IconPillWidth(m.MarkAll) + UiTokens.S4);
            if (!string.IsNullOrEmpty(m.MarkAll))
            {
                float pw = IconPillWidth(m.MarkAll);
                Pill(s, at, x + w - UiTokens.S4 - pw, y + (UiTokens.S16 - UiTokens.ControlSmall) / 2f, pw, m.MarkAll, PillKind.Line, MarkAllCommand, 0, UiIcon.Check, m.MarkAllOff);
            }
            cy += TabRow(s, at, x, cy, w, m.Tabs, m.Tab);
            List(s, at, m.List, x, cy, w, y + h - cy, keys);
        }

        // ------------------------------------------------------------------------------------------------ Messages

        public const float InboxShare = 0.34f, InboxLeast = 360f, InboxMost = 480f;

        private static void MessagesPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float iw = Math.Max(InboxLeast, Math.Min(InboxMost, UiDraw.SnapDown(w * InboxShare)));
            float bottom = y + h;
            // The conversation first: its field and its answers are laid before the inbox's rows take what targets are left.
            float dx = x + iw, dw = w - iw;
            UiDraw.Box(s, at, dx, y, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            Conversation(s, at.InRegion(RegionDetail), m, dx, y, dw, h, keys);
            float cy = y + PageHead(s, at, x, y, iw, m.Head, m.HeadLine, false);
            TextField(s, at, x + UiTokens.S4, cy, iw - UiTokens.S8, m.Search, m.InboxHint, InboxField, UiIcon.Search);
            cy += UiTokens.Control + UiTokens.S3;
            List(s, at, m.List, x, cy, iw, bottom - cy, keys);
        }

        // One conversation: who it is with, its messages from the oldest, the quick answers and the field to write in.
        private static void Conversation(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float bottom = y + h;
            if (string.IsNullOrEmpty(m.DetailName))
            {
                UiDraw.EmptyState(s, at, x + UiTokens.S8, y + UiTokens.S8, w - UiTokens.S16, h - UiTokens.S16, UiIcon.Message, m.Detail.EmptyTitle, m.Detail.EmptyBody);
                return;
            }
            const float headH = 72f;
            int head = UiDraw.Box(s, at, x, y, w, headH, UiRole.None, UiRole.None, 0f);
            UiAt inHead = at.In(head);
            float hx = x + UiTokens.S4;
            Face(s, inHead, hx, y + UiTokens.S4, UiTokens.AvatarRow, m.DetailName, m.DetailHue, m.DetailPerson, m.DetailRing, false);
            float lw = string.IsNullOrEmpty(m.DetailLink) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(m.DetailLink, UiStyle.Title) + UiTokens.S4), UiDraw.SnapDown(w * 0.4f));
            float tx = hx + UiTokens.AvatarRow + UiTokens.S3, tw = x + w - tx - lw - UiTokens.S8;
            Line(s, inHead, tx, y + UiTokens.S4, tw, UiStyle.Title, UiRole.Text, m.DetailName, UiFlags.Truncate);
            Line(s, inHead, tx, y + UiTokens.S4 + UiTokens.S5, tw, UiStyle.Small, UiRole.Text2, m.DetailLine, UiFlags.Truncate);
            if (lw > 0f)
            {
                float lx = x + w - UiTokens.S4 - lw;
                int link = UiDraw.Box(s, inHead, lx, y + UiTokens.S4, lw, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiDraw.TextMiddle(s, inHead.In(link), lx, y + UiTokens.S4, UiTokens.Control, lw, UiStyle.Title, UiRole.AccentText, m.DetailLink, UiFlags.Right | UiFlags.Truncate);
                UiDraw.Node(s, inHead, lx, y + UiTokens.S4, lw, UiTokens.Control, DetailCommand, 0, link);
            }
            UiDraw.Box(s, at, x, y + headH - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            // The foot: the quick answers over the field and Send.
            float fy = bottom;
            bool field = m.Field != null || !string.IsNullOrEmpty(m.FieldHint);
            if (field)
            {
                fy -= UiTokens.Control + UiTokens.S4;
                float sw = ButtonWidth(m.Send);
                TextField(s, at, x + UiTokens.S4, fy, w - UiTokens.S12 - sw, m.Field, m.FieldHint, m.FieldArg >= 0 ? m.FieldArg : ThreadField, UiIcon.None);
                Orange(s, at, x + w - UiTokens.S4 - sw, fy, sw, UiTokens.Control, m.Send, SendCommand, 0, string.IsNullOrEmpty(m.Field));
            }
            int quick = m.Quick != null ? m.Quick.Length : 0;
            if (quick > 0)
            {
                fy -= UiTokens.ControlSmall + UiTokens.S3;
                float qx = x + UiTokens.S4;
                for (int i = 0; i < quick; i++)
                {
                    float qw = PillWidth(m.Quick[i]);
                    if (qx + qw > x + w - UiTokens.S4)
                    {
                        break;
                    }
                    Pill(s, at, qx, fy, qw, m.Quick[i], i == 0 && !field ? PillKind.Cyan : PillKind.Line, QuickCommand, i);
                    qx += qw + UiTokens.S2;
                }
            }
            float ly = y + headH + UiTokens.S3;
            List(s, at, m.Detail, x, ly, w, fy - UiTokens.S3 - ly, keys);
        }

        // ------------------------------------------------------------------------------------------------ Profile

        public const float BannerHeight = 144f;

        private static void ProfilePage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            FlareProfileModel p = m.Profile;
            float bottom = y + h;
            int banner = UiDraw.Box(s, at, x, y, w, BannerHeight, UiRole.Tile, UiRole.None, 0f);
            if (!string.IsNullOrEmpty(p.Banner))
            {
                var pic = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = x, Y = y, W = w, H = BannerHeight, Text = PictureKey(p.Banner, w / BannerHeight),
                    Value = w / BannerHeight, Clip = -1, Node = -1, Layer = at.Layer, Order = -1, Parent = banner, Alpha = 1f
                };
                s.Add(ref pic);
            }
            // The face over the banner's foot, on a ring of the page's own slate.
            float ax = x + UiTokens.S4, ay = y + BannerHeight - UiTokens.AvatarHero / 2f - UiTokens.S1;
            int ring = UiDraw.Circle(s, at.In(banner), ax, ay, UiTokens.AvatarHero + UiTokens.S2, UiTokens.AvatarHero + UiTokens.S2, UiRole.Panel);
            s.Items[ring].Flags |= UiFlags.Overflow;
            Face(s, at.In(ring), ax + UiTokens.S1, ay + UiTokens.S1, UiTokens.AvatarHero, p.Name, p.Hue, p.Person, p.Ring, false);
            float cy = y + BannerHeight + UiTokens.S3;
            float pillRight = x + w - UiTokens.S4;
            if (!string.IsNullOrEmpty(p.Follow))
            {
                float fw = PillWidth(p.Follow);
                Pill(s, at, pillRight - fw, cy, fw, p.Follow, p.Following ? PillKind.Quiet : PillKind.Cyan, ProfileFollowCommand, 0);
                pillRight -= fw + UiTokens.S2;
            }
            else if (!string.IsNullOrEmpty(p.Edit))
            {
                float ew = IconPillWidth(p.Edit);
                Pill(s, at, pillRight - ew, cy, ew, p.Edit, PillKind.Line, EditCommand, 0, UiIcon.Pencil);
            }
            // The further buttons of another's account (Message, Mute, Hide) stand left of Follow, as many as the banner's width holds.
            for (int i = 0; i < p.Pills.Length && i < p.PillActs.Length; i++)
            {
                float bw = PillWidth(p.Pills[i]);
                if (pillRight - bw < ax + UiTokens.AvatarHero + UiTokens.S4)
                {
                    break;
                }
                Pill(s, at, pillRight - bw, cy, bw, p.Pills[i], PillKind.Line, ProfileActCommand, p.PillActs[i]);
                pillRight -= bw + UiTokens.S2;
            }
            cy = ay + UiTokens.AvatarHero + UiTokens.S2 + UiTokens.S3;
            float tx = x + UiTokens.S4, tw = w - UiTokens.S8;
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(p.Name, UiStyle.Hero) + UiTokens.S1), tw - (p.Verified ? UiTokens.S6 : 0f));
            Line(s, at, tx, cy, nameW, UiStyle.Hero, UiRole.Text, p.Name, UiFlags.Truncate);
            if (p.Verified)
            {
                Verified(s, at, tx + nameW + UiTokens.S1, cy + UiTokens.S1);
            }
            cy += L(UiStyle.Hero);
            Line(s, at, tx, cy, tw, UiStyle.Small, UiRole.Text2, p.Handle, UiFlags.Truncate);
            cy += L(UiStyle.Small) + UiTokens.S2;
            if (!string.IsNullOrEmpty(p.Bio))
            {
                UiDraw.Text(s, at, tx, cy, tw, UiStyle.Body, UiRole.Text, p.Bio, UiFlags.Wrap | UiFlags.Truncate, 2);
                cy += UiDraw.Snap(UiTextRules.WrappedHeight(p.Bio, UiStyle.Body, tw, 2)) + UiTokens.S2;
            }
            float fx = tx;
            int figures = Math.Min(p.Figures.Length, p.Labels.Length);
            for (int i = 0; i < figures; i++)
            {
                float vw = UiDraw.Snap(UiTextRules.Width(p.Figures[i], UiStyle.BodyStrong) + UiTokens.S1);
                float lw = UiDraw.Snap(UiTextRules.Width(p.Labels[i], UiStyle.Small) + UiTokens.S1);
                if (fx + vw + lw > tx + tw)
                {
                    break;
                }
                Line(s, at, fx, cy, vw, UiStyle.BodyStrong, UiRole.Text, p.Figures[i]);
                Line(s, at, fx + vw, cy, lw, UiStyle.Small, UiRole.Text2, p.Labels[i], UiFlags.Label);
                fx += vw + lw + UiTokens.S4;
            }
            cy += L(UiStyle.BodyStrong) + UiTokens.S2;
            cy += TabRow(s, at, x, cy, w, m.Tabs, m.Tab);
            List(s, at, m.List, x, cy, w, bottom - cy, keys);
        }

        // ------------------------------------------------------------------------------------------------ a post's thread

        private static void ThreadPage(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float bottom = y + h;
            float cy = y + PageHead(s, at, x, y, w, m.Head, m.HeadLine, true);
            UiDraw.Box(s, at, x, cy - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float fy = bottom;
            if (m.Field != null || !string.IsNullOrEmpty(m.FieldHint))
            {
                fy -= UiTokens.Control + UiTokens.S4;
                float sw = ButtonWidth(m.Send);
                TextField(s, at, x + UiTokens.S4, fy, w - UiTokens.S12 - sw, m.Field, m.FieldHint, ReplyField, UiIcon.None);
                Orange(s, at, x + w - UiTokens.S4 - sw, fy, sw, UiTokens.Control, m.Send, SendCommand, 0, string.IsNullOrEmpty(m.Field));
                fy -= UiTokens.S3;
            }
            List(s, at, m.List, x, cy, w, fy - cy, keys);
        }
    }
}
