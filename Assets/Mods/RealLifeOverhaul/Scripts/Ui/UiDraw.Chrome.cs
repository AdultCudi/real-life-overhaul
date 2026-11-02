using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The frame and the parts of the Cities: Skylines II look (design/DIRECTION_CS2.md): an app's window over the world,
    /// the thin title strip every panel wears, the status bar along the foot, and the pieces its panels are made of: a
    /// section's band and label, compact rows of a label and a value or a cyan link, square icon buttons, status pills, a
    /// donut with its legend, a stepped scale, thin meters, a management game's graded attribute bars, star ratings and
    /// round portraits with a status ring. Game-free.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>A window's close button: the host takes it as Back.</summary>
        public const int CloseCommand = 9000;

        private const float PillH = 32f;     // px: a status pill (Pill)
        private const float TileGap = 8f;    // px: between the status bar's app tiles

        /// <summary>
        /// px between a link's words and its mark's box, measured from the words as drawn (cut to their room first), a step
        /// of the grid whatever the words' width (UiDraw.Links.cs; EconomyCheck measures every link the kit draws).
        /// </summary>
        public const float LinkGap = UiTokens.S1;

        private const float SliceGap = 0.01f;    // of the turn: wide enough to stay a clean hair of slate from rim to hole at every angle

        /// <summary>
        /// An app's window over the world: the slate panel at its share of the screen, its title strip (the app's icon, the
        /// place or the app's name in cyan capitals, the close button). Returns the panel, which the page is drawn inside.
        /// </summary>
        public static int Window(UiScene s, UiFrame frame, string place, IUiKeys keys)
        {
            s.Pointer = frame.Pointer;
            s.Held = frame.Held;
            frame.Window(out float x, out float y, out float w, out float h);
            int panel = Box(s, UiAt.Screen, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel | UiFlags.Chrome);   // fixed in size, so its room is the page's to fill
            TitleStrip(s, UiAt.Screen.In(panel), x, y, w, frame.Icon, string.IsNullOrEmpty(place) ? frame.Title : place, true, UiStyle.AppTitle);
            return panel;
        }

        /// <summary>
        /// A panel's title strip: its icon at the left, its title centred in cyan capitals, a close button at the right, a
        /// hairline under it. Returns the y under it.
        /// </summary>
        public static float TitleStrip(UiScene s, UiAt at, float x, float y, float w, UiIcon icon, string title, bool close, UiStyle style = UiStyle.SectionTitle)
        {
            const float h = UiFrame.Strip;
            int strip = Box(s, at, x, y, w, h, UiRole.Header, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor | UiFlags.Chrome);   // its top corners the panel's, its foot square
            UiAt on = at.In(strip);
            float side = UiTokens.S3 + UiTokens.Icon + UiTokens.S3;   // the title keeps clear of the icon and the close button alike
            if (icon != UiIcon.None)
            {
                Icon(s, on, x + UiTokens.S3, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, icon, UiRole.Accent, UiFlags.Middle | UiFlags.Chrome);
            }
            if (!string.IsNullOrEmpty(title))
            {
                TextMiddle(s, on, x + side, y, h, w - side * 2f, style, UiRole.AccentText, title, UiFlags.Centre | UiFlags.Truncate | UiFlags.Chrome);
            }
            if (close)
            {
                // The least target, inset so its focus ring stays inside the panel's corner.
                float b = UiTokens.MinTarget;
                IconButton(s, at, x + w - UiTokens.S2 - b, y + (h - b) / 2f, b, UiIcon.Close, UiRole.Text, CloseCommand, 0, false, UiAction.Count, false, true);
            }
            Box(s, at, x, y + h - UiTokens.Hairline, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor | UiFlags.Chrome);
            return y + h;
        }

        /// <summary>
        /// A square button bearing an icon (a footer's locate, follow or call; a window's close; a toolbar's app). Chosen,
        /// it is filled cyan with the icon in the words' colour, as a game marks the tool in hand. Returns the target.
        /// </summary>
        public static int IconButton(UiScene s, UiAt at, float x, float y, float size, UiIcon icon, UiRole tint, int command, int arg, bool chosen = false,
            UiAction key = UiAction.Count, bool round = false, bool quiet = false)
        {
            // A quiet button (a window's close) has no tile of its own: only the hover's still fill shows it is a button.
            int face = quiet ? Box(s, at, x, y, size, size, UiRole.None, UiRole.None, UiTokens.RadiusLarge) : Tile(s, at, x, y, size, tint, chosen, round);
            float g = Snap(size * 0.64f);
            // Dark on the cyan when it is the one chosen: a white would not read there.
            Icon(s, at.In(face), x + (size - g) / 2f, y + (size - g) / 2f, g, icon, chosen ? UiRole.OnAccent : tint, UiFlags.Middle | UiFlags.Centre, true);
            return Node(s, at, x, y, size, size, command, arg, face, false, -1, 0, key);
        }

        /// <summary>
        /// An icon's tile: a slate square or disc a step lighter than the panel, the icon in its own colour on it, as Cities:
        /// Skylines II's tool buttons, or the cyan fill when it is the one chosen. The tint stays for the callers that pass it.
        /// Returns the tile.
        /// </summary>
        public static int Tile(UiScene s, UiAt at, float x, float y, float size, UiRole tint, bool chosen, bool round = false) =>
            Box(s, at, x, y, size, size, chosen ? UiRole.Accent : UiRole.Tile, UiRole.None, round ? 0f : UiTokens.RadiusLarge,
                (round ? UiFlags.Circle : UiFlags.None) | (chosen ? UiFlags.Selected : UiFlags.None));

        /// <summary>
        /// The status bar along the foot of the screen, after Cities: Skylines II's bottom bar: one slate strip with one centre
        /// line and even room above and below it. In its middle the app row, big tiles in the player's order (the open one in
        /// the chosen tone, its news counted on a tile's corner, All apps last); at its left and right the pills (UiDraw.HudBar:
        /// the time with the game's speed, the weather, the place; the needs, the alerts, the money and the settings at the
        /// corner). While the keys lead their prompts stand between the row and the right pills; with the mouse leading no
        /// prompt shows, as the mouse does it by pointing, as the game's own bar works.
        /// </summary>
        public static void StatusBar(UiScene s, UiFrame frame, IUiKeys keys) => StatusBar(s, frame, keys, true);

        /// <summary>
        /// The status bar, its prompts in it (<paramref name="prompts"/>) or left to the caller, as the HUD sets the game's
        /// control hints in key chips over the bar's right end (UiDraw.KeyChips).
        /// </summary>
        public static void StatusBar(UiScene s, UiFrame frame, IUiKeys keys, bool prompts)
        {
            UiStatusModel m = UiChrome.Bar != null ? UiChrome.Bar() : null;
            float y = frame.BandY, w = frame.Width, mid = BarMiddle(frame);
            int bar = Box(s, UiAt.Screen, 0f, y, w, frame.Band, UiRole.StatusBar, UiRole.None, 0f, UiFlags.Decor | UiFlags.Chrome);
            UiAt on = UiAt.Screen.In(bar);
            Box(s, on, 0f, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor | UiFlags.Chrome);
            if (AllAppsPrompt(m, keys) && !Prompted(s, UiAction.View))
            {
                s.AddPrompt(UiAction.View, m.AllAppsLabel);   // the pad's View opens All apps from anywhere: said while a pad leads
            }
            float room = frame.Pointer || !prompts ? 0f : PromptsWidth(s, keys);
            UiPillPlan p = PlanPills(m, w, room);
            s.PromptX = 0f;
            s.PromptY = y;
            s.PromptW = w;
            s.PromptH = frame.Band;   // the bar is there for the prompts either way: they show while the keys lead
            int row = s.NodeCount;
            if (m != null)
            {
                AppRow(s, on, mid, ref p, m);   // UiDraw.AppRow.cs
            }
            int rowEnd = s.NodeCount;
            if (!frame.Pointer && prompts)
            {
                Prompts(s, p.PromptX, y, p.PromptX + room, frame.Band, keys);
            }
            if (m != null)
            {
                HudPills(s, on, mid, w, ref p, m);   // UiDraw.HudBar.cs
                JoinPills(s, row, rowEnd - 1);       // one row for the d-pad where the pills are targets
            }
        }

        /// <summary>True where the bar says All apps on the pad's View: a pad leads and the row ends with the tile that opens it.</summary>
        public static bool AllAppsPrompt(UiStatusModel m, IUiKeys keys) =>
            m != null && m.AllApps && m.AppsPress && keys != null && keys.Pad != UiPad.None && !string.IsNullOrEmpty(m.AllAppsLabel);

        private static bool Prompted(UiScene s, UiAction action)
        {
            for (int i = 0; i < s.PromptCount; i++)
            {
                if (s.Prompts[i].Action == action)
                {
                    return true;
                }
            }
            return false;
        }

        // The keys' prompts' width as Prompts lays them: each key's cap and its words, a gap between.
        private static float PromptsWidth(UiScene s, IUiKeys keys)
        {
            float w = 0f;
            for (int i = 0; i < s.PromptCount; i++)
            {
                w += PromptWidth(ref s.Prompts[i], keys) + (i > 0 ? UiTokens.S6 : 0f);
            }
            return Snap(w);
        }

        /// <summary>A status pill: an icon in its tint and its words. Returns its width.</summary>
        public static float Pill(UiScene s, UiAt at, float x, float y, UiIcon icon, UiRole tint, string text, UiRole ink)
        {
            float w = PillWidth(icon, text);
            int face = Box(s, at, x, y, w, PillH, UiRole.Pill, UiRole.None, UiTokens.RadiusLarge, UiFlags.Chrome);
            UiAt inside = at.In(face);
            float cx = x + UiTokens.S2;
            if (icon != UiIcon.None)
            {
                Icon(s, inside, cx, y + (PillH - UiTokens.Icon) / 2f, UiTokens.Icon, icon, tint, UiFlags.Middle | UiFlags.Chrome);
                cx += UiTokens.Icon + UiTokens.S2;
            }
            TextMiddle(s, inside, cx, y, PillH, w - (cx - x) - UiTokens.S2, UiStyle.Body, ink, text, UiFlags.Truncate | UiFlags.Chrome);
            return w;
        }

        private static float PillWidth(UiIcon icon, string text) =>
            Snap(UiTokens.S2 + (icon != UiIcon.None ? UiTokens.Icon + UiTokens.S2 : 0f) + UiTextRules.Width(text, UiStyle.Body) + UiTokens.S1 + UiTokens.S3);

        // The keys' prompts at the right of a row, while the keys lead: each key's cap and its words.
        private static void Prompts(UiScene s, float x, float y, float right, float h, IUiKeys keys)
        {
            s.PromptX = x;
            s.PromptY = y;
            s.PromptW = right - x;
            s.PromptH = h;
            float cx = right;
            float capY = y + (h - UiTokens.KeyCap) / 2f;
            for (int i = s.PromptCount - 1; i >= 0; i--)
            {
                ref UiPrompt p = ref s.Prompts[i];
                float lw = UiTextRules.Width(p.Label, UiStyle.Prompt) + UiTokens.S1;
                float pw = PromptWidth(ref p, keys);
                cx -= pw;
                int first = s.ItemCount;
                float kx = cx + KeyCap(s, UiAt.Screen, cx, capY, p.Action, keys, UiRole.PromptCap, UiRole.PromptInk);
                if (p.Action == UiAction.TabPrev)
                {
                    kx += UiTokens.S1;
                    kx += KeyCap(s, UiAt.Screen, kx, capY, UiAction.TabNext, keys, UiRole.PromptCap, UiRole.PromptInk);
                }
                TextMiddle(s, UiAt.Screen, kx + UiTokens.S2, y, h, lw, UiStyle.Prompt, UiRole.PromptInk, p.Label, UiFlags.Label);
                for (int k = first; k < s.ItemCount; k++)
                {
                    s.Items[k].Flags |= UiFlags.Middle | UiFlags.Prompt | UiFlags.Chrome;
                }
                cx -= UiTokens.S6;
            }
        }

        /// <summary>A section's darker band inside a panel, as Cities: Skylines II parts its sections.</summary>
        public static int Band(UiScene s, UiAt at, float x, float y, float w, float h) => Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, 0f, UiFlags.Decor);

        /// <summary>A section's label in capitals at the left of its first row, its value or link at the right. Returns the row's height.</summary>
        public static float SectionRow(UiScene s, UiAt at, float x, float y, float w, string label, string value, bool link, int command, int arg)
        {
            float h = UiTokens.PairRow;
            float vw = ValueRoom(value, link, w * 0.6f);
            TextMiddle(s, at, x, y, h, w - vw - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text, label, UiFlags.Label | UiFlags.Truncate);
            Value(s, at, x + w - vw, y, h, vw, value, link, command, arg);
            return h;
        }

        /// <summary>
        /// A compact row: its label at the left in the quiet grey blue, its value at the right in soft white, or a cyan link
        /// with its magnifier that opens the thing or moves the camera to it. No rule between rows. Returns the height.
        /// </summary>
        public static float InfoRow(UiScene s, UiAt at, float x, float y, float w, string label, string value, bool link = false, int command = -1, int arg = 0,
            UiRole ink = UiRole.Text)
        {
            float h = UiTokens.PairRow;
            float vw = ValueRoom(value, link, w * 0.62f);
            TextMiddle(s, at, x, y, h, w - vw - UiTokens.S3, UiStyle.Body, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            Value(s, at, x + w - vw, y, h, vw, value, link, command, arg, ink);
            return h;
        }

        private static float ValueRoom(string value, bool link, float most) =>
            Math.Min(SnapDown(most), Snap(UiTextRules.Width(value, UiStyle.Body) + UiTokens.S1 + (link ? UiTokens.IconSmall + UiTokens.S1 : 0f)));

        // A row's value at the right: words, or a link (its magnifier before its words) that is a target of its own.
        private static void Value(UiScene s, UiAt at, float x, float y, float h, float w, string value, bool link, int command, int arg, UiRole ink = UiRole.Text)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            if (!link)
            {
                TextMiddle(s, at, x, y, h, w, UiStyle.Body, ink, value, UiFlags.Right | UiFlags.Truncate);
                return;
            }
            LinkValue(s, at, x, y, h, w, value, UiStyle.Body, command, arg);
        }

        /// <summary>
        /// A donut of shares, slice by slice from the top clockwise in the chart hues, the largest cumulative first so each
        /// later slice lays over the rest of the turn. Shares need not sum to one: they are taken as parts of their sum, with
        /// <paramref name="rest"/> (what the legend does not name, such as the goods past a list's best sellers) as the
        /// track's own grey after the last slice.
        /// </summary>
        public static void Donut(UiScene s, UiAt at, float x, float y, float size, float[] shares, int count, float rest = 0f)
        {
            float sum = Math.Max(0f, rest);
            for (int i = 0; i < count; i++)
            {
                sum += Math.Max(0f, shares[i]);
            }

            if (sum <= 0f)
            {
                Box(s, at, x, y, size, size, UiRole.Track, UiRole.None, 0f, UiFlags.Circle);
                return;
            }
            // Each slice its own arc from where the last ended, so no slice lies under another and tints its edges, with a
            // hair of the panel between neighbours to hide the radial fill's hard seams, as a game's donut parts them.
            float run = 0f;
            for (int i = 0; i < count; i++)
            {
                float start = run;
                run += Math.Max(0f, shares[i]) / sum;
                float end = i == count - 1 && rest <= 0f ? 1f : run;
                float gap = count > 1 && end - start > SliceGap * 3f ? SliceGap : 0f;
                var item = new UiItem
                {
                    Kind = UiKind.Slice, Fill = UiTokens.Chart(i), X = x, Y = y, W = size, H = size, Value = end - start, Points = new[] { start + gap / 2f, end - gap / 2f },
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f, Flags = UiFlags.Circle
                };
                s.Add(ref item);
            }
            if (rest > 0f && run < 1f)
            {
                // The rest of the turn as its own arc in the track's grey, the hole left the panel's as every slice leaves it.
                float gap = 1f - run > SliceGap * 3f ? SliceGap : 0f;
                var item = new UiItem
                {
                    Kind = UiKind.Slice, Fill = UiRole.Track, X = x, Y = y, W = size, H = size, Value = 1f - run, Points = new[] { run + gap / 2f, 1f - gap / 2f },
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f, Flags = UiFlags.Circle
                };
                s.Add(ref item);
            }
        }

        /// <summary>A chart's legend: a swatch in each slice's hue, its name and its share. Returns the height.</summary>
        public static float Legend(UiScene s, UiAt at, float x, float y, float w, string[] labels, string[] values, int count)
        {
            const float row = 20f, swatch = 12f;
            for (int i = 0; i < count; i++)
            {
                float ry = y + i * row;
                Box(s, at, x, ry + (row - swatch) / 2f, swatch, swatch, UiTokens.Chart(i), UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle);
                float vw = values != null && i < values.Length ? UiTextRules.Width(values[i], UiStyle.Small) + UiTokens.S1 : 0f;
                TextMiddle(s, at, x + swatch + UiTokens.S2, ry, row, w - swatch - UiTokens.S2 - vw - UiTokens.S2, UiStyle.Small, UiRole.Text, labels[i], UiFlags.Label | UiFlags.Truncate);
                if (vw > 0f)
                {
                    TextMiddle(s, at, x + w - vw, ry, row, vw, UiStyle.Small, UiRole.Text2, values[i], UiFlags.Right);
                }
            }
            return count * row;
        }

        /// <summary>A stepped scale for a map's colouring: its steps' swatches across, its low and high words under the ends. Returns the height.</summary>
        public static float Scale(UiScene s, UiAt at, float x, float y, float w, string low, string high, UiRole[] steps)
        {
            float gap = UiTokens.Hairline * 2f, each = (w - gap * (steps.Length - 1)) / steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                Box(s, at, x + i * (each + gap), y, each, UiTokens.S2, steps[i], UiRole.None, 0f, UiFlags.Share);
            }
            float ly = y + UiTokens.S2 + UiTokens.S1, lh = UiTextRules.LineHeight(UiStyle.Small);
            Text(s, at, x, ly, w / 2f, UiStyle.Small, UiRole.Muted, low, UiFlags.Label);
            Text(s, at, x + w / 2f, ly, w / 2f, UiStyle.Small, UiRole.Muted, high, UiFlags.Label | UiFlags.Right);
            return Snap(UiTokens.S2 + UiTokens.S1 + lh);
        }

        /// <summary>A thin meter: its track and its fill to a value of 0 to 100 in a tone.</summary>
        public static void Meter(UiScene s, UiAt at, float x, float y, float w, float h, int value, UiRole tone)
        {
            int track = Box(s, at, x, y, w, h, UiRole.Track, UiRole.None, h / 2f, UiFlags.Middle | UiFlags.Chrome);
            float fw = w * Math.Max(0, Math.Min(100, value)) / 100f;
            if (fw > 0f)
            {
                Box(s, at.In(track), x, y, Math.Max(h, fw), h, tone, UiRole.None, h / 2f, UiFlags.Middle | UiFlags.Share | UiFlags.Chrome);
            }
        }

        /// <summary>
        /// An attribute as a management game grades it: its name, a bar filled to its value and the value, both in the
        /// grade's colour from poor red to excellent green. Returns the height.
        /// </summary>
        public static float GradeBar(UiScene s, UiAt at, float x, float y, float w, string label, int value, float labelW = 0f)
        {
            float h = UiTokens.PairRow;
            UiRole grade = UiTokens.Grade(value);
            string figure = Math.Max(0, Math.Min(100, value)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            float fw = UiTextRules.Width("100", UiStyle.BodyStrong) + UiTokens.S1;
            float lw = labelW > 0f ? labelW : LabelColumn(w, label);
            TextMiddle(s, at, x, y, h, lw, UiStyle.Body, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            float bx = x + lw + UiTokens.S3, bw = w - lw - UiTokens.S3 - fw - UiTokens.S2;
            int track = Box(s, at, bx, y + (h - UiTokens.S1) / 2f, bw, UiTokens.S1, UiRole.Track, UiRole.None, UiTokens.S1 / 2f, UiFlags.Middle | UiFlags.Share);
            Box(s, at.In(track), bx, y + (h - UiTokens.S1) / 2f, Math.Max(UiTokens.S1, bw * Math.Max(0, Math.Min(100, value)) / 100f), UiTokens.S1, grade, UiRole.None,
                UiTokens.S1 / 2f, UiFlags.Middle | UiFlags.Share);
            TextMiddle(s, at, x + w - fw, y, h, fw, UiStyle.BodyStrong, grade, figure, UiFlags.Right);
            return h;
        }

        /// <summary>
        /// The width of a grade bar's label column for its labels, so a block of bars starts its tracks at one edge and
        /// draws equal values at equal lengths.
        /// </summary>
        public static float LabelColumn(float w, params string[] labels)
        {
            float most = 0f;
            for (int i = 0; labels != null && i < labels.Length; i++)
            {
                most = Math.Max(most, UiTextRules.Width(labels[i] ?? string.Empty, UiStyle.Body) + UiTokens.S1);
            }
            return Snap(Math.Min(w * 0.42f, most));
        }

        /// <summary>
        /// A list's thin scroll thumb at its right edge, as a city builder's lists show it, when its rows run past its room:
        /// how much is in view and where. It only shows: the wheel and the keys scroll the list.
        /// </summary>
        public static void ScrollThumb(UiScene s, UiAt at, float x, float y, float h, float offset, float content)
        {
            if (content <= h + 0.5f)
            {
                return;
            }
            float th = Math.Max(UiTokens.S6, h * h / content);
            float ty = y + (h - th) * Math.Max(0f, Math.Min(1f, offset / (content - h)));
            Box(s, at, x, ty, UiTokens.S1, th, UiRole.Muted, UiRole.None, UiTokens.S1 / 2f, UiFlags.Decor);
        }

        /// <summary>A rating in stars out of five, by halves: gold for what it has, grey for the rest, as Football Manager rates. Returns its width.</summary>
        public static float Stars(UiScene s, UiAt at, float x, float y, float size, int halves)
        {
            for (int i = 0; i < 5; i++)
            {
                int left = halves - i * 2;
                float sx = x + i * (size + UiTokens.Hairline * 2f);
                // A half star is the grey star with the gold half laid on it, the grey one marked as decoration as the overlap is meant.
                Icon(s, at, sx, y, size, UiIcon.Star, left >= 2 ? UiRole.Gold : UiRole.Muted, UiFlags.Middle | UiFlags.Share | (left == 1 ? UiFlags.Decor : UiFlags.None));
                if (left == 1)
                {
                    Icon(s, at, sx, y, size, UiIcon.StarHalf, UiRole.Gold, UiFlags.Middle | UiFlags.Share);
                }
            }
            return 5f * size + 4f * UiTokens.Hairline * 2f;
        }

        /// <summary>
        /// A round portrait with a status ring in a tone (inZOI's): the person's painted face, picked by their name, on a
        /// disc of their hue. Returns the face.
        /// </summary>
        public static int Portrait(UiScene s, UiAt at, float x, float y, float size, string name, float hue, UiRole ring)
        {
            float stroke = size >= UiTokens.AvatarHero ? UiTokens.S1 : UiTokens.Hairline * 2f;
            if (ring != UiRole.None)
            {
                Circle(s, at, x - stroke, y - stroke, size + stroke * 2f, size + stroke * 2f, ring, UiFlags.Decor);
            }
            var item = new UiItem
            {
                Kind = UiKind.Portrait, Text = name ?? string.Empty, X = x, Y = y, W = size, H = size, Value = hue, Radius = size / 2f, Flags = UiFlags.Circle,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
        }

        /// <summary>A soft status chip, inZOI's: a dot in its tone and its words, cut to a width when given one. Returns its width.</summary>
        public static float Chip(UiScene s, UiAt at, float x, float y, string text, UiRole tone, float most = 0f)
        {
            float tw = UiTextRules.Width(text, UiStyle.SmallStrong) + UiTokens.S1;
            float w = Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + tw + UiTokens.S2);
            if (most > 0f && w > most)
            {
                w = SnapDown(most);   // a longer translation keeps the column: its words end in an ellipsis
                tw = w - UiTokens.S2 * 3f - UiTokens.StatusDot;
            }
            float h = UiTokens.Badge + UiTokens.S1;
            int face = Box(s, at, x, y, w, h, UiRole.Pill, UiRole.None, h / 2f, UiFlags.None);
            UiAt inside = at.In(face);
            Circle(s, inside, x + UiTokens.S2, y + (h - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, UiTokens.StatusDot, tone, UiFlags.Middle);
            TextMiddle(s, inside, x + UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2, y, h, tw, UiStyle.SmallStrong, UiRole.Text, text, UiFlags.Truncate);
            return w;
        }
    }
}
