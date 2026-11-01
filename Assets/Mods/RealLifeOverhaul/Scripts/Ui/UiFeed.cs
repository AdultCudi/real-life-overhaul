using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A post of the feed: its author's face and name, when, its words, its picture and its counts.</summary>
    public struct UiFeedPost
    {
        public string Author;
        public float Hue;
        public UiIcon Icon;      // a business's or a paper's account has its glyph on a tile in place of a face
        public string Handle;    // "@bloomcafe"
        public string Time;      // "2h"
        public string Text;
        public UiIcon Picture;   // its picture's glyph on a slate tile; None for words alone
        public string Caption;   // under the picture; null for none
        public string Likes, Comments, Shares;
        public bool Liked;
    }

    /// <summary>The account or tag beside the feed, as a short profile: never empty, the player's own when nothing is chosen.</summary>
    public sealed class UiFeedProfile
    {
        public string Name;
        public float Hue;
        public UiIcon Icon;                            // a tag has its glyph on a tile in place of a face
        public string Handle;
        public string About;
        public string[] FactLabels = new string[0];    // "Followers", "Following", "Posts"
        public string[] FactValues = new string[0];
        public UiActionModel[] Actions = new UiActionModel[0];
        public string ListTitle;                       // "Trending in Midtown"
        public string[] List = new string[0];          // its tags or accounts, each a link
        public string PeopleTitle;                     // "People to follow"
        public UiPersonRow[] People = new UiPersonRow[0];   // each a face, a name and a line (Role) under it
    }

    /// <summary>The feed: a column of posts at a reading width beside the chosen account or tag, and how far it has scrolled.</summary>
    public sealed class UiFeedModel
    {
        public UiFeedPost[] Posts = new UiFeedPost[0];
        public int PostCount;
        public float Offset;
        public int Reveal = -1;
        public UiFeedProfile Profile = new UiFeedProfile();
        public string EmptyTitle, EmptyBody;
    }

    /// <summary>
    /// A feed, a block of an app window's page: a column of posts at a reading width, each its author's face and name,
    /// their handle and when, the words, a picture on a slate tile with its caption, and its counts beside small glyph
    /// buttons (the one the player gave filled cyan); beside the column, the chosen account or tag as a short profile (the
    /// player's own when nothing is chosen, so it is never empty) with its facts, its actions and what is trending. The
    /// posts scroll in their frame, only those in view laid out. For Flare. Commands: <see cref="AuthorCommand"/> (a post's
    /// author), <see cref="ReactCommand"/> (post * 3 + 0 like, 1 comment, 2 share), <see cref="ProfileCommand"/> (an action
    /// of the profile), <see cref="ListCommand"/> (a trending line) and <see cref="PersonCommand"/> (a person to follow).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiFeed
    {
        public const int AuthorCommand = 80, ReactCommand = 81, ProfileCommand = 82, ListCommand = 83, PersonCommand = 84;
        public const float ColumnWidth = 640f;       // px: a post's reading width, as a social app's column
        public const float SideLeast = 360f;         // px: the profile beside it
        public const float SideMost = 440f;          // px: at most an info panel's width, so the posts stay the page's first thing
        public const float PictureHeight = 184f;     // px: a post's picture tile
        private const int MostLines = 6;             // a post's words before they end at a whole word
        private const float Face = 40f;              // px
        private const byte RegionPosts = 1, RegionSide = 2;

        /// <summary>The posts' column in a room: the reading width, or less where the room is narrower.</summary>
        public static float Column(float w) => Math.Max(Math.Min(ColumnWidth, w - SideLeast - UiTokens.S6), UiDraw.SnapDown(w - SideMost - UiTokens.S6));

        /// <summary>A post's height at a column's width, the gap under it taken.</summary>
        public static float PostHeight(ref UiFeedPost p, float cw)
        {
            float inner = cw - UiTokens.S4 * 2f;
            float h = UiTokens.S4 + Face + UiTokens.S3;
            if (!string.IsNullOrEmpty(p.Text))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(p.Text, UiStyle.Body, inner, MostLines)) + UiTokens.S3;
            }
            if (p.Picture != UiIcon.None)
            {
                h += PictureHeight + UiTokens.S2 + (string.IsNullOrEmpty(p.Caption) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2);
            }
            return h + UiTokens.ControlSmall + UiTokens.S3 + UiTokens.S3;
        }

        /// <summary>Lays the feed into the room an app window's Shell gave its page; writes back the scroll it took.</summary>
        public static void Lay(UiScene s, UiAt at, UiFeedModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float cw = Column(w);
            Posts(s, at.InRegion(RegionPosts), m, x, y, cw, h);
            float sx = x + cw + UiTokens.S6;
            UiDraw.Box(s, at, sx - UiTokens.S3, y, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            Side(s, at.InRegion(RegionSide), m.Profile, sx, y, x + w - sx, h, keys);
        }

        private static void Posts(UiScene s, UiAt at, UiFeedModel m, float x, float y, float cw, float h)
        {
            float vh = UiDraw.SnapDown(h);
            float contentH = 0f;
            for (int i = 0; i < m.PostCount; i++)
            {
                contentH += PostHeight(ref m.Posts[i], cw);
            }
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < m.PostCount)
            {
                float top = 0f;
                for (int i = 0; i < m.Reveal; i++)
                {
                    top += PostHeight(ref m.Posts[i], cw);
                }
                offset = Math.Min(offset, top);
                offset = Math.Max(offset, top + PostHeight(ref m.Posts[m.Reveal], cw) - vh);
                m.Reveal = -1;
            }
            offset = Math.Max(0f, Math.Min(offset, contentH - vh));
            m.Offset = offset;
            short clip = s.AddClip(x, y, cw, vh, offset);
            s.Clips[clip].ContentHeight = contentH;
            UiDraw.ScrollThumb(s, at, x + cw + UiTokens.S1, y, vh, offset, contentH);
            if (m.PostCount == 0)
            {
                s.AddList(0, 0, 0, clip, -1);
                UiDraw.Empty(s, at, x, y, cw, vh, m.EmptyTitle, m.EmptyBody);
                return;
            }
            int first = -1, shown = 0;
            float py = 0f;
            for (int i = 0; i < m.PostCount; i++)
            {
                float ph = PostHeight(ref m.Posts[i], cw);
                if (py + ph > offset && py < offset + vh)
                {
                    first = first < 0 ? i : first;
                    shown++;
                }
                py += ph;
            }
            short list = s.AddList(m.PostCount, Math.Max(0, first), shown, clip, -1);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            _viewTop = offset;
            _viewBottom = offset + vh;
            py = 0f;
            int before = -1;
            for (int i = 0; i < m.PostCount; i++)
            {
                float ph = PostHeight(ref m.Posts[i], cw);
                if (py + ph > offset && py < offset + vh)
                {
                    Post(s, inClip.Arriving(i - first), ref m.Posts[i], i, list, py, cw, ph - UiTokens.S3, out int author, out int react);
                    // The d-pad walks author, counts, the next author: a list's rows step past what lies between them otherwise.
                    if (author >= 0 && react >= 0)
                    {
                        s.Nodes[author].Down = react;
                    }
                    if (author >= 0 && before >= 0)
                    {
                        s.Nodes[author].Up = before;
                    }
                    before = react;
                }
                py += ph;
            }
        }

        [ThreadStatic] private static float _viewTop, _viewBottom;   // the posts' view in their content, while they are laid

        private static bool Seen(float y, float h) => y + h > _viewTop && y < _viewBottom;

        // A post on its card: the author, the words, the picture, the counts; only the parts in the column's view laid out.
        private static void Post(UiScene s, UiAt at, ref UiFeedPost p, int index, short list, float y, float w, float h, out int author, out int react)
        {
            author = react = -1;
            int card = UiDraw.Box(s, at, 0f, y, w, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(card);
            float ix = UiTokens.S4, iw = w - UiTokens.S4 * 2f, cy = y + UiTokens.S4;
            float tw = string.IsNullOrEmpty(p.Time) ? 0f : UiDraw.Snap(UiTextRules.Width(p.Time, UiStyle.Small) + UiTokens.S1);
            if (Seen(cy, Face))
            {
                // The author is a target of their own: their face, name, handle and the time, a step of room round them for the hover.
                float hw = iw + UiTokens.S4 - (tw > 0f ? tw + UiTokens.S3 : 0f);
                int head = UiDraw.Box(s, inside, ix - UiTokens.S2, cy, hw, Face, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inHead = inside.In(head);
                if (p.Icon != UiIcon.None)
                {
                    int t = UiDraw.Tile(s, inHead, ix, cy, Face, UiRole.None, false);   // a business's account wears its glyph, not a face
                    UiDraw.Icon(s, inHead.In(t), ix + (Face - UiTokens.S6) / 2f, cy + (Face - UiTokens.S6) / 2f, UiTokens.S6, p.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                }
                else
                {
                    UiDraw.Portrait(s, inHead, ix, cy, Face, p.Author, p.Hue, UiRole.None);
                }
                float nx = ix + Face + UiTokens.S3, nw = ix - UiTokens.S2 + hw - UiTokens.S2 - nx;
                float nameH = UiTextRules.LineHeight(UiStyle.Title), handleH = UiTextRules.LineHeight(UiStyle.Small);
                float top = cy + (Face - nameH - UiTokens.S1 - handleH) / 2f;
                UiDraw.Text(s, inHead, nx, top, nw, UiStyle.Title, UiRole.Text, p.Author, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Text(s, inHead, nx, top + nameH + UiTokens.S1, nw, UiStyle.Small, UiRole.Text2, p.Handle, UiFlags.Truncate | UiFlags.Middle);
                author = UiDraw.Node(s, inside, ix - UiTokens.S2, cy, hw, Face, AuthorCommand, index, head, false, list, index);
                if (tw > 0f)
                {
                    UiDraw.Text(s, inside, ix + iw - tw, top, tw, UiStyle.Small, UiRole.Text2, p.Time, UiFlags.Right | UiFlags.Middle);
                }
            }
            cy += Face + UiTokens.S3;
            if (!string.IsNullOrEmpty(p.Text))
            {
                float th = UiDraw.Snap(UiTextRules.WrappedHeight(p.Text, UiStyle.Body, iw, MostLines));
                if (Seen(cy, th))
                {
                    UiDraw.Text(s, inside, ix, cy, iw, UiStyle.Body, UiRole.Text, p.Text, UiFlags.Wrap | UiFlags.Truncate, MostLines);
                }
                cy += th + UiTokens.S3;
            }
            if (p.Picture != UiIcon.None)
            {
                if (Seen(cy, PictureHeight))
                {
                    int tile = UiDraw.Box(s, inside, ix, cy, iw, PictureHeight, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
                    float g = UiTokens.S16 + UiTokens.S4;
                    if (Seen(cy + (PictureHeight - g) / 2f, g))
                    {
                        UiDraw.Icon(s, inside.In(tile), ix + (iw - g) / 2f, cy + (PictureHeight - g) / 2f, g, p.Picture, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                    }
                }
                cy += PictureHeight + UiTokens.S2;
                if (!string.IsNullOrEmpty(p.Caption))
                {
                    float ch = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
                    if (Seen(cy, ch))
                    {
                        UiDraw.Text(s, inside, ix, cy, iw, UiStyle.Small, UiRole.Text2, p.Caption, UiFlags.Truncate);
                    }
                    cy += ch + UiTokens.S2;
                }
            }
            if (Seen(cy, UiTokens.ControlSmall))
            {
                float bx = ix - UiTokens.S1;
                int first = s.NodeCount;
                bx = React(s, inside, bx, cy, UiIcon.Heart, p.Likes, p.Liked, index * 3) + UiTokens.S4;
                bx = React(s, inside, bx, cy, UiIcon.Message, p.Comments, false, index * 3 + 1) + UiTokens.S4;
                React(s, inside, bx, cy, UiIcon.Send, p.Shares, false, index * 3 + 2);
                react = first;
                for (int n = first; n < s.NodeCount && author >= 0; n++)
                {
                    s.Nodes[n].Up = author;
                }
            }
        }

        // A reaction: its glyph on a small button (filled cyan when the player gave it) and its count beside it. Returns its right edge.
        private static float React(UiScene s, UiAt at, float x, float y, UiIcon glyph, string count, bool given, int arg)
        {
            float b = UiTokens.ControlSmall;
            float cw = string.IsNullOrEmpty(count) ? 0f : UiDraw.Snap(UiTextRules.Width(count, UiStyle.SmallStrong) + UiTokens.S1);
            float w = b + (cw > 0f ? cw + UiTokens.S1 : 0f) + UiTokens.S1;
            // Given, it is the chosen toggle: the cyan fill with dark ink, as the kit marks what is chosen; never a recoloured glyph.
            int face = UiDraw.Box(s, at, x, y, w, b, given ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, given ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float g = UiTokens.Icon;
            UiDraw.Icon(s, inside, x + (b - g) / 2f, y + (b - g) / 2f, g, glyph, given ? UiRole.OnAccent : UiRole.None, UiFlags.Middle | UiFlags.Centre, given);
            if (cw > 0f)
            {
                UiDraw.TextMiddle(s, inside, x + b, y, b, cw, UiStyle.SmallStrong, given ? UiRole.OnAccent : UiRole.Text2, count, UiFlags.None);
            }
            UiDraw.Node(s, at, x, y, w, b, ReactCommand, arg, face);
            return x + w;
        }

        // The chosen account or tag as a short profile: its face, name and handle, a line about it, its facts, its actions, what is trending.
        private static void Side(UiScene s, UiAt at, UiFeedProfile p, float x, float y, float w, float h, IUiKeys keys)
        {
            const float face = 64f;
            float ring = UiTokens.S1;
            if (p.Icon != UiIcon.None)
            {
                int tile = UiDraw.Tile(s, at, x, y, face, UiRole.None, false);
                UiDraw.Icon(s, at.In(tile), x + (face - UiTokens.S8) / 2f, y + (face - UiTokens.S8) / 2f, UiTokens.S8, p.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            else
            {
                UiDraw.Portrait(s, at, x + ring, y + ring, face - ring * 2f, p.Name, p.Hue, UiRole.None);
            }
            float tx = x + face + UiTokens.S4, tw = x + w - tx;
            float nameH = UiTextRules.LineHeight(UiStyle.Title), handleH = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (face - nameH - UiTokens.S1 - handleH) / 2f;
            UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, p.Name, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, at, tx, top + nameH + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, p.Handle, UiFlags.Truncate | UiFlags.Middle);
            float cy = y + face + UiTokens.S4;
            if (!string.IsNullOrEmpty(p.About))
            {
                float ah = UiDraw.Snap(UiTextRules.WrappedHeight(p.About, UiStyle.Body, w, 3));
                UiDraw.Text(s, at, x, cy, w, UiStyle.Body, UiRole.Text2, p.About, UiFlags.Wrap | UiFlags.Truncate, 3);
                cy += ah + UiTokens.S4;
            }
            int facts = Math.Min(p.FactLabels.Length, p.FactValues.Length);
            if (facts > 0)
            {
                float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1, valueH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
                float bh = UiTokens.S3 * 2f + labelH + valueH;
                UiDraw.Band(s, at, x, cy, w, bh);
                float slot = UiDraw.SnapDown((w - UiTokens.S4 * 2f) / facts);
                for (int i = 0; i < facts; i++)
                {
                    float fx = x + UiTokens.S4 + i * slot;
                    UiDraw.Text(s, at, fx, cy + UiTokens.S3, slot - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text2, p.FactLabels[i], UiFlags.Label | UiFlags.Truncate);
                    UiDraw.Text(s, at, fx, cy + UiTokens.S3 + labelH, slot - UiTokens.S3, UiStyle.Title, UiRole.Text, p.FactValues[i], UiFlags.Truncate);
                }
                cy += bh + UiTokens.S4;
            }
            if (p.Actions.Length > 0)
            {
                float bx = x;
                for (int i = 0; i < p.Actions.Length; i++)
                {
                    ref UiActionModel a = ref p.Actions[i];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    UiDraw.Button(s, at, bx, cy, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, ProfileCommand, i, a.Disabled);
                    bx += bw + UiTokens.S3;
                }
                cy += UiTokens.Control + UiTokens.S6;
            }
            // What is trending and who to follow side by side where the column is wide, one over the other where it is not.
            bool beside = p.List.Length > 0 && p.People.Length > 0 && w >= SideLeast * 2f;
            float lw = beside ? UiDraw.SnapDown((w - UiTokens.S6) / 2f) : w;
            float most = y + h, listTop = cy;
            if (p.List.Length > 0)
            {
                UiDraw.Text(s, at, x, cy, lw, UiStyle.SectionHeader, UiRole.Text, p.ListTitle, UiFlags.Label | UiFlags.Truncate);
                cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                for (int i = 0; i < p.List.Length && cy + UiTokens.ControlSmall <= most; i++)
                {
                    UiDraw.LinkRow(s, at, x, cy, lw, UiTokens.ControlSmall, p.List[i], ListCommand, i);
                    cy += UiTokens.ControlSmall;
                }
                cy += UiTokens.S6;
            }
            if (p.People.Length > 0)
            {
                float px = beside ? x + lw + UiTokens.S6 : x, py = beside ? listTop : cy;
                UiDraw.Text(s, at, px, py, lw, UiStyle.SectionHeader, UiRole.Text, p.PeopleTitle, UiFlags.Label | UiFlags.Truncate);
                py += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                for (int i = 0; i < p.People.Length && py + UiDraw.PersonRowHeight <= most; i++)
                {
                    ref UiPersonRow person = ref p.People[i];
                    UiDraw.PersonRow(s, at, px, py, lw, person.Name, person.Hue, UiRole.None, person.Role, null, null, UiRole.None, PersonCommand, i);
                    py += UiDraw.PersonRowHeight + UiTokens.S1;
                }
            }
        }
    }
}
