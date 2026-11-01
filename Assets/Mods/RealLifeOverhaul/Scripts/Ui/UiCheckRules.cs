using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The rules that keep every screen the same, checked on a laid-out scene (docs/standards/ui.md, RLO-UI-005 to 016):
    /// nothing overlaps its siblings, nothing spills out of its parent, every box sits on the 4 px grid, every label fits
    /// whole, every target is big enough and reached by the d-pad, every text and icon stands out from what is under it
    /// by WCAG AA, every text is at least the smallest size, one primary action a layer, a prompt bar on every screen, no
    /// frame holds dead space, and the page uses its screen.
    /// Each returns how many it found and says each through <paramref name="say"/>. Game-free: EconomyCheck runs them on
    /// every registered screen, at every frame size and in both themes and all eleven languages. Cost: offline only.
    /// </summary>
    public static class UiCheckRules
    {
        public const float Tolerance = 0.5f;          // px: rounding a layout may leave
        public const float NormalContrast = 4.5f;     // WCAG AA for text
        public const float LargeContrast = 3f;        // WCAG AA for large text (24 px, or 18.66 px bold) and for icons
        public const float LargeText = 24f;           // px
        public const float LargeBoldText = 18.66f;    // px at weight 700 and over
        public const int MostTargets = 48;            // focus targets a layer may hold before the screen should split (README's checklist)

        /// <summary>Siblings (items of one parent, layer and scroll region) whose boxes cross.</summary>
        public static int Overlaps(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem a = ref s.Items[i];
                if (Skip(ref a))
                {
                    continue;
                }
                for (int j = i + 1; j < s.ItemCount; j++)
                {
                    ref UiItem b = ref s.Items[j];
                    if (Skip(ref b) || a.Parent != b.Parent || a.Layer != b.Layer || a.Clip != b.Clip)
                    {
                        continue;
                    }
                    float w = Math.Min(a.X + a.W, b.X + b.W) - Math.Max(a.X, b.X);
                    float h = Math.Min(a.Y + a.H, b.Y + b.H) - Math.Max(a.Y, b.Y);
                    if (w > Tolerance && h > Tolerance)
                    {
                        found++;
                        say?.Invoke(s.Name + ": " + Describe(s, i) + " overlaps " + Describe(s, j));
                    }
                }
            }
            return found;
        }

        /// <summary>Items that spill out of their parent (or the scene, or a scroll region's width).</summary>
        public static int Spills(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if ((it.Flags & (UiFlags.Decor | UiFlags.Overflow)) != 0)
                {
                    continue;
                }
                float px, py, pw, ph;
                if (it.Parent >= 0)
                {
                    ref UiItem p = ref s.Items[it.Parent];
                    px = p.X;
                    py = p.Y;
                    pw = p.W;
                    ph = p.H;
                }
                else if (it.Clip >= 0)
                {
                    px = 0f;
                    py = -1e6f;
                    pw = s.Clips[it.Clip].W;
                    ph = 2e6f;   // a scroll region's content runs on below its frame: only its width bounds it
                }
                else
                {
                    px = 0f;
                    py = 0f;
                    pw = s.Width;
                    ph = s.Height;
                }
                if (it.X < px - Tolerance || it.Y < py - Tolerance || it.X + it.W > px + pw + Tolerance || it.Y + it.H > py + ph + Tolerance)
                {
                    found++;
                    string outer = it.Parent >= 0 ? Describe(s, it.Parent) : it.Clip >= 0 ? "its scroll region" : "the screen";
                    say?.Invoke(s.Name + ": " + Describe(s, i) + " spills out of " + outer);
                }
            }
            return found;
        }

        /// <summary>Boxes off the 4 px grid: a left, top, width or height that is not a grid step from its parent, unless centred or a share.</summary>
        public static int OffGrid(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if ((it.Flags & UiFlags.Decor) != 0 || it.Kind == UiKind.Spark)
                {
                    continue;
                }
                float ox = it.Parent >= 0 ? it.X - s.Items[it.Parent].X : it.X;
                float oy = it.Parent >= 0 ? it.Y - s.Items[it.Parent].Y : it.Y;
                bool freeX = (it.Flags & (UiFlags.Centre | UiFlags.Right | UiFlags.Share | UiFlags.Prompt)) != 0;
                bool freeY = (it.Flags & UiFlags.Middle) != 0;
                bool box = it.Kind != UiKind.Text;
                string why = null;
                if (!freeX && !OnGrid(ox))
                {
                    why = "its left is " + ox.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " px from its parent";
                }
                else if (!freeY && !OnGrid(oy))
                {
                    why = "its top is " + oy.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " px from its parent";
                }
                else if (box && (it.Flags & UiFlags.Share) == 0 && (!OnGrid(it.W) || !OnGrid(it.H)))
                {
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    why = "it is " + it.W.ToString("0.##", inv) + " x " + it.H.ToString("0.##", inv) + " px";
                }
                if (why != null)
                {
                    found++;
                    say?.Invoke(s.Name + ": " + Describe(s, i) + " is off the 4 px grid: " + why);
                }
            }
            return found;
        }

        /// <summary>Labels that do not fit whole, and wrapped texts taller than their box.</summary>
        public static int Unfit(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.Text || string.IsNullOrEmpty(it.Text))
                {
                    continue;
                }
                bool wrap = (it.Flags & UiFlags.Wrap) != 0;
                if ((it.Flags & UiFlags.Label) != 0)
                {
                    if (wrap)
                    {
                        int most = it.Value >= 1f ? (int)it.Value : 1;
                        int lines = UiTextRules.Lines(it.Text, it.Style, it.W, 0, out float _);
                        if (lines > most)
                        {
                            found++;
                            say?.Invoke(s.Name + ": \"" + Cut(it.Text) + "\" takes " + lines + " lines where " + most + " fit (" + it.Style + ", " + (int)it.W + " px)");
                        }
                    }
                    else if (!UiTextRules.Fits(it.Text, it.Style, it.W))
                    {
                        found++;
                        say?.Invoke(s.Name + ": \"" + Cut(it.Text) + "\" is " + (int)UiTextRules.Width(it.Text, it.Style) + " px wide in a box of " + (int)it.W + " (" + it.Style + ")");
                    }
                }
                if (it.H + Tolerance < UiTextRules.LineHeight(it.Style))
                {
                    found++;
                    say?.Invoke(s.Name + ": \"" + Cut(it.Text) + "\" has a box lower than one line of its style");
                }
            }
            return found;
        }

        /// <summary>Focus targets smaller than the least a pointer or a pad can be sure of.</summary>
        public static int SmallTargets(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (n.W + Tolerance < UiTokens.MinTarget || n.H + Tolerance < UiTokens.MinTarget)
                {
                    found++;
                    say?.Invoke(s.Name + ": target " + i + " (command " + n.Command + ") is " + (int)n.W + " x " + (int)n.H + " px, under " + (int)UiTokens.MinTarget);
                }
            }
            return found;
        }

        /// <summary>Texts and icons that do not stand out from what is under them by WCAG AA, in the scene's theme.</summary>
        public static int LowContrast(UiScene s, Action<string> say)
        {
            int found = 0;
            bool dark = s.Dark;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                bool text = it.Kind == UiKind.Text && !string.IsNullOrEmpty(it.Text);
                bool icon = it.Kind == UiKind.Icon;
                if ((!text && !icon) || (it.Flags & UiFlags.Disabled) != 0 || InDisabled(s, i))
                {
                    continue;
                }
                UiColour under = Beneath(s, i, dark);
                UiColour ink = UiColour.Of(text ? it.Ink : it.Fill, dark);
                ink = ink.WithAlpha(ink.A * it.Alpha).Over(under);
                float ratio = UiColour.Contrast(ink, under);
                float want = icon ? LargeContrast : Large(it.Style) ? LargeContrast : NormalContrast;
                if (ratio + 0.005f < want)
                {
                    found++;
                    say?.Invoke(s.Name + " (" + (dark ? "dark" : "light") + "): " + (text ? "\"" + Cut(it.Text) + "\" (" + it.Ink + ")" : "icon " + it.Icon + " (" + it.Fill + ")")
                                + " stands " + ratio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ":1 from what is under it, under " + want + ":1");
                }
            }
            return found;
        }

        /// <summary>Texts smaller than the smallest size the interface allows.</summary>
        public static int TooSmall(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind == UiKind.Text && UiTokens.Type(it.Style).Size + 0.01f < UiTokens.MinText)
                {
                    found++;
                    say?.Invoke(s.Name + ": \"" + Cut(it.Text) + "\" is in " + it.Style + ", under " + UiTokens.MinText + " px");
                }
            }
            return found;
        }

        /// <summary>A layer with more than one primary action, or more targets than a screen should hold.</summary>
        public static int Crowded(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int layer = 0; layer <= 2; layer++)
            {
                int primaries = 0, targets = 0;
                for (int i = 0; i < s.NodeCount; i++)
                {
                    ref UiNode n = ref s.Nodes[i];
                    if (n.Layer != layer)
                    {
                        continue;
                    }
                    targets++;
                    if (n.Face >= 0 && s.Items[n.Face].Fill == UiRole.Accent && (s.Items[n.Face].Flags & UiFlags.Selected) == 0 && n.List < 0)
                    {
                        primaries++;
                    }
                }
                if (primaries > 1)
                {
                    found++;
                    say?.Invoke(s.Name + ": layer " + layer + " has " + primaries + " primary actions; a screen has one");
                }
                if (targets > MostTargets)
                {
                    found++;
                    say?.Invoke(s.Name + ": layer " + layer + " has " + targets + " targets in view, over " + MostTargets + ": split the screen");
                }
            }
            return found;
        }

        /// <summary>
        /// Indicators that contradict their value: a trend arrow pointing against the sign of the change written beside it
        /// ("+2.1%" by a falling arrow), a sparkline running against that arrow, or an arrow, its words and its line not in
        /// one colour (green and red say good and bad for the player, so a figure cannot say both).
        /// </summary>
        public static int Contradictions(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i + 1 < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.Icon || (it.Icon != UiIcon.TrendUp && it.Icon != UiIcon.TrendDown) || (it.Flags & UiFlags.Decor) != 0)
                {
                    continue;   // a specimen on the style sheet is no indicator
                }
                bool up = it.Icon == UiIcon.TrendUp;
                ref UiItem words = ref s.Items[i + 1];
                int sign = words.Kind == UiKind.Text ? UiModels.Sign(words.Text) : 0;
                if (sign != 0 && (sign > 0) != up)
                {
                    found++;
                    say?.Invoke(s.Name + ": a " + (up ? "rising" : "falling") + " arrow beside \"" + words.Text + "\"");
                }
                if (words.Kind == UiKind.Text && Tone(words.Ink) != Tone(it.Fill))
                {
                    found++;
                    say?.Invoke(s.Name + ": an arrow in " + it.Fill + " beside its words in " + words.Ink);
                }
                if (i + 2 < s.ItemCount && s.Items[i + 2].Kind == UiKind.Spark && s.Items[i + 2].Points != null && s.Items[i + 2].Points.Length > 1)
                {
                    ref UiItem line = ref s.Items[i + 2];
                    float run = line.Points[line.Points.Length - 1] - line.Points[0];
                    if (run != 0f && (run > 0f) != up)
                    {
                        found++;
                        say?.Invoke(s.Name + ": a sparkline running " + (run > 0f ? "up" : "down") + " under a " + (up ? "rising" : "falling") + " arrow");
                    }
                    if (Tone(line.Fill) != Tone(it.Fill))
                    {
                        found++;
                        say?.Invoke(s.Name + ": a sparkline in " + line.Fill + " under an arrow in " + it.Fill);
                    }
                }
            }
            return found;
        }

        // Good or bad, whichever shade of the colour says it.
        private static int Tone(UiRole role)
        {
            switch (role)
            {
                case UiRole.Ok:
                case UiRole.OkText:
                case UiRole.OkSoft:
                    return 1;
                case UiRole.Danger:
                case UiRole.DangerText:
                case UiRole.DangerSoft:
                    return -1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Frames holding dead space (a panel, a card, a row with its edge): a stretch of more than <see cref="MostDeadSpace"/>
        /// that nothing inside covers, above its first line, between two of its lines (choices far over their buttons) or
        /// under its last. A scroll region covers what its rows cover, all of its room when they run past it or when its
        /// room is its content's own (a short thread under the line of its beginning); an empty state covers the room it
        /// is given.
        /// </summary>
        public static int DeadSpace(UiScene s, Action<string> say)
        {
            int found = 0;
            var spans = new List<float>(64);
            for (int p = 0; p < s.ItemCount; p++)
            {
                ref UiItem frame = ref s.Items[p];
                bool framed = (frame.Flags & UiFlags.Panel) != 0 || frame.Edge == UiRole.PanelEdge || frame.Edge == UiRole.RowEdge;   // a panel is one by its flag now that panels wear no edge
                if (frame.Kind != UiKind.Box || !framed || frame.Clip >= 0 || (frame.Flags & UiFlags.Chrome) != 0)
                {
                    continue;
                }
                spans.Clear();
                float top = frame.Y, foot = frame.Y + frame.H;
                for (int i = p + 1; i < s.ItemCount; i++)
                {
                    ref UiItem it = ref s.Items[i];
                    if ((it.Flags & UiFlags.Decor) != 0 || it.Layer != frame.Layer || (it.Kind == UiKind.Text && string.IsNullOrEmpty(it.Text)))
                    {
                        continue;   // words that are not there fill nothing: an empty frame with an empty title is still empty
                    }
                    if (it.Clip < 0)
                    {
                        if (UiDraw.Inside(s, i, p))
                        {
                            Span(spans, it.Y, it.Y + it.H, top, foot);
                        }
                    }
                    else if (!Covers(s.Clips[it.Clip]) && ClipInside(s, it.Clip, ref frame))
                    {
                        ref UiClip c = ref s.Clips[it.Clip];
                        float y = c.Y + it.Y - c.Offset;
                        Span(spans, y, y + it.H, Math.Max(top, c.Y), Math.Min(foot, c.Y + c.H));
                    }
                }
                for (int c = 0; c < s.ClipCount; c++)
                {
                    if (Covers(s.Clips[c]) && ClipInside(s, (short)c, ref frame))
                    {
                        Span(spans, s.Clips[c].Y, s.Clips[c].Y + s.Clips[c].H, top, foot);
                    }
                }
                Gap(spans, top, foot, out float worst, out string where);
                if (worst > MostDeadSpace + Tolerance)
                {
                    found++;
                    say?.Invoke(s.Name + ": " + Describe(s, p) + " holds " + (int)worst + " px of nothing " + where);
                }
            }
            return found;
        }

        // A scroll region whose whole room counts as filled: its rows run past it, or its room is its content's own.
        private static bool Covers(UiClip c) => c.Fills || c.ContentHeight >= c.H - Tolerance;

        private static void Span(List<float> spans, float from, float to, float top, float foot)
        {
            from = Math.Max(from, top);
            to = Math.Min(to, foot);
            if (to > from)
            {
                spans.Add(from);
                spans.Add(to);
            }
        }

        // The widest stretch between top and foot that no span covers, and where it lies.
        private static void Gap(List<float> spans, float top, float foot, out float worst, out string where)
        {
            int n = spans.Count / 2;
            var starts = new float[n];
            var ends = new float[n];
            for (int i = 0; i < n; i++)
            {
                starts[i] = spans[i * 2];
                ends[i] = spans[i * 2 + 1];
            }
            Array.Sort(starts, ends);
            float cursor = top;
            worst = 0f;
            where = "under its last line";
            for (int i = 0; i < n; i++)
            {
                if (starts[i] - cursor > worst)
                {
                    worst = starts[i] - cursor;
                    where = cursor == top ? "above its first line" : "between two of its lines";
                }
                cursor = Math.Max(cursor, ends[i]);
            }
            if (foot - cursor > worst)
            {
                worst = foot - cursor;
                where = "under its last line";
            }
        }

        /// <summary>
        /// Keys that do two things on a screen: two targets of a layer owning one key, a target showing Confirm's key (it
        /// presses whatever has focus, so on one target it would lie whenever another has focus), a target showing a key
        /// it does not answer to, and an action named twice in the prompt bar.
        /// </summary>
        public static int KeyClash(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode a = ref s.Nodes[i];
                for (int j = i + 1; a.Key != UiAction.Count && j < s.NodeCount; j++)
                {
                    if (s.Nodes[j].Key == a.Key && s.Nodes[j].Layer == a.Layer)
                    {
                        found++;
                        say?.Invoke(s.Name + ": " + a.Key + " presses two targets (" + a.Command + "/" + a.Arg + " and " + s.Nodes[j].Command + "/" + s.Nodes[j].Arg + ")");
                    }
                }
            }
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.KeyCap || (it.Flags & UiFlags.Prompt) != 0 || it.Node < 0)
                {
                    continue;
                }
                var shown = (UiAction)(int)it.Value;
                if (!UiKeyGlyphs.Ownable(shown))
                {
                    found++;
                    say?.Invoke(s.Name + ": a target shows " + shown + "'s key, which no one target may own");
                }
                else if (s.Nodes[it.Node].Key != shown)
                {
                    found++;
                    say?.Invoke(s.Name + ": a target shows " + shown + "'s key but answers to " + s.Nodes[it.Node].Key);
                }
            }
            for (int i = 0; i < s.PromptCount; i++)
            {
                for (int j = i + 1; j < s.PromptCount; j++)
                {
                    if (s.Prompts[i].Action == s.Prompts[j].Action)
                    {
                        found++;
                        say?.Invoke(s.Name + ": the prompt bar names " + s.Prompts[i].Action + " twice");
                    }
                }
            }
            return found;
        }

        public const float MostDeadSpace = UiTokens.MostSlack;
        public const float MostMargin = UiTokens.S16;   // px of nothing a page may leave at the left, top or right of its room: more and it floats
        public const float LeastFill = 0.25f;           // the share of its room a page's frames and words cover at least

        /// <summary>
        /// A page that does not use its screen (RLO-UI-016), measured on the room under the masthead and over the band
        /// (<see cref="UiFrame.Room"/>) by the screen's own layer, its bars left out: more than <see cref="MostMargin"/> of
        /// nothing at the room's left, top or right (a card floating in the middle), or, at 16:9, the screen the game is
        /// played on, frames and words covering less than <see cref="LeastFill"/> of it. A page anchored under the
        /// masthead and across the width may end where its content ends: a short page is sized to what it holds.
        /// </summary>
        public static int Unfilled(UiScene s, UiFrame frame, Action<string> say)
        {
            if (s.Floats)
            {
                return 0;   // a docked panel, a card or the phone stands over the world on purpose, the city round it
            }
            frame.Room(out float rx, out float ry, out float rw, out float rh);
            var boxes = new List<float>(64);
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                // What a page shows is what stands on its ground: an item inside another counts through the outer one.
                bool onGround = it.Parent < 0 || (s.Items[it.Parent].Flags & UiFlags.Decor) != 0;
                if (!onGround || it.Layer != 0 || (it.Flags & (UiFlags.Decor | UiFlags.Prompt)) != 0 || (it.Kind == UiKind.Text && string.IsNullOrEmpty(it.Text)))
                {
                    continue;
                }
                float x0 = it.X, y0 = it.Y, x1 = it.X + it.W, y1 = it.Y + it.H;
                if (it.Kind == UiKind.Text)
                {
                    // Words cover their ink, not the width they were given: a title's long box never hides a narrow page.
                    float ink = Math.Min(it.W, UiTextRules.Width(it.Text, it.Style));
                    x0 = (it.Flags & UiFlags.Right) != 0 ? x1 - ink : (it.Flags & UiFlags.Centre) != 0 ? it.X + (it.W - ink) / 2f : it.X;
                    x1 = x0 + ink;
                }
                if (it.Clip >= 0)
                {
                    ref UiClip c = ref s.Clips[it.Clip];
                    x0 = Math.Max(c.X, c.X + x0);
                    x1 = Math.Min(c.X + c.W, c.X + x1);
                    y0 = Math.Max(c.Y, c.Y + y0 - c.Offset);
                    y1 = Math.Min(c.Y + c.H, c.Y + y1 - c.Offset);
                }
                x0 = Math.Max(x0, rx);
                y0 = Math.Max(y0, ry);
                x1 = Math.Min(x1, rx + rw);
                y1 = Math.Min(y1, ry + rh);
                if (x1 - x0 <= Tolerance || y1 - y0 <= Tolerance)
                {
                    continue;   // the masthead's words, the band's, or a row scrolled out of view
                }
                boxes.Add(x0);
                boxes.Add(y0);
                boxes.Add(x1);
                boxes.Add(y1);
                left = Math.Min(left, x0);
                top = Math.Min(top, y0);
                right = Math.Max(right, x1);
            }
            if (boxes.Count == 0)
            {
                say?.Invoke(s.Name + ": nothing stands in the room under the masthead");
                return 1;
            }
            int found = Margin(s, left - rx, "at the left of the page", say) + Margin(s, top - ry, "over the page, under the masthead", say)
                + Margin(s, rx + rw - right, "at the right of the page", say);
            bool played = frame.Height <= UiFrame.ScreenHeight + Tolerance && frame.Width * 9f <= frame.Height * 16f + Tolerance;
            float share = Covered(boxes) / (rw * rh);
            if (played && share < LeastFill)
            {
                found++;
                say?.Invoke(s.Name + ": its frames and words cover " + (int)(share * 100f) + "% of the room under the masthead, under " + (int)(LeastFill * 100f) + "%");
            }
            return found;
        }

        private static int Margin(UiScene s, float gap, string where, Action<string> say)
        {
            if (gap <= MostMargin + Tolerance)
            {
                return 0;
            }
            say?.Invoke(s.Name + ": " + (int)gap + " px of nothing " + where + " (a page starts under the masthead and runs across the width)");
            return 1;
        }

        // The area the union of boxes (x0, y0, x1, y1 in fours) covers: a strip between each two edges across, the spans
        // over it merged down its height.
        private static float Covered(List<float> boxes)
        {
            int n = boxes.Count / 4;
            var xs = new List<float>(n * 2);
            for (int i = 0; i < n; i++)
            {
                xs.Add(boxes[i * 4]);
                xs.Add(boxes[i * 4 + 2]);
            }
            xs.Sort();
            var spans = new List<float>(n * 2);
            float area = 0f;
            for (int k = 0; k + 1 < xs.Count; k++)
            {
                float a = xs[k], b = xs[k + 1];
                if (b - a <= 0f)
                {
                    continue;
                }
                spans.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (boxes[i * 4] <= a && boxes[i * 4 + 2] >= b)
                    {
                        spans.Add(boxes[i * 4 + 1]);
                        spans.Add(boxes[i * 4 + 3]);
                    }
                }
                area += (b - a) * Union(spans);
            }
            return area;
        }

        // The length the spans (from, to in pairs) cover together.
        private static float Union(List<float> spans)
        {
            int n = spans.Count / 2;
            if (n == 0)
            {
                return 0f;
            }
            var starts = new float[n];
            var ends = new float[n];
            for (int i = 0; i < n; i++)
            {
                starts[i] = spans[i * 2];
                ends[i] = spans[i * 2 + 1];
            }
            Array.Sort(starts, ends);
            float total = 0f, from = starts[0], to = ends[0];
            for (int i = 1; i < n; i++)
            {
                if (starts[i] > to)
                {
                    total += to - from;
                    from = starts[i];
                    to = ends[i];
                }
                else
                {
                    to = Math.Max(to, ends[i]);
                }
            }
            return total + to - from;
        }

        private static bool ClipInside(UiScene s, short clip, ref UiItem panel)
        {
            ref UiClip c = ref s.Clips[clip];
            return c.X >= panel.X - Tolerance && c.X + c.W <= panel.X + panel.W + Tolerance && c.Y >= panel.Y - Tolerance && c.Y + c.H <= panel.Y + panel.H + Tolerance;
        }

        /// <summary>Texts that are a record's key rather than words (UiPlaces.IsKey), as "ba:street_fifthavenue|70" stood in beta 3's table.</summary>
        public static int RawKeys(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind == UiKind.Text && UiPlaces.IsKey(it.Text))
                {
                    found++;
                    say?.Invoke(s.Name + ": \"" + Cut(it.Text) + "\" is a record's key on the screen, not words");
                }
            }
            return found;
        }

        /// <summary>A page that says the masthead's words again under it ("The city's people" twice in beta 3).</summary>
        public static int Repeats(UiScene s, UiFrame frame, Action<string> say)
        {
            if (string.IsNullOrEmpty(frame.Title))
            {
                return 0;
            }
            int found = 0;
            string app = frame.Title.Trim();
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind == UiKind.Text && it.Y >= frame.Top - Tolerance && (it.Flags & (UiFlags.Prompt | UiFlags.Chrome)) == 0 && it.Text != null
                    && string.Equals(it.Text.Trim(), app, StringComparison.CurrentCultureIgnoreCase))
                {
                    found++;
                    say?.Invoke(s.Name + ": \"" + Cut(it.Text) + "\" repeats the masthead under it");
                }
            }
            return found;
        }

        /// <summary>
        /// Two labels stacked at one left edge, the upper alone on its line ("FIGURE" over "PEOPLE", "MOTION" over the
        /// column "MOTION" in beta 3): one label says what a panel holds. A table's head row is many labels on a line, not a stack.
        /// </summary>
        public static int Stacked(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem a = ref s.Items[i];
                if (a.Kind != UiKind.Text || a.Style != UiStyle.SectionHeader || string.IsNullOrEmpty(a.Text) || Beside(s, i))
                {
                    continue;
                }
                for (int j = 0; j < s.ItemCount; j++)
                {
                    ref UiItem b = ref s.Items[j];
                    float gap = b.Y - (a.Y + a.H);
                    if (j != i && b.Kind == UiKind.Text && b.Style == UiStyle.SectionHeader && !string.IsNullOrEmpty(b.Text) && b.Layer == a.Layer && b.Clip == a.Clip
                        && Math.Abs(b.X - a.X) <= UiTokens.S2 + Tolerance && gap >= -Tolerance && gap <= UiTokens.S6)
                    {
                        found++;
                        say?.Invoke(s.Name + ": \"" + Cut(a.Text) + "\" stands over \"" + Cut(b.Text) + "\", two labels where one would do");
                    }
                }
            }
            return found;
        }

        // Whether a label shares its line with another label of its kind (a table's head row).
        private static bool Beside(UiScene s, int i)
        {
            ref UiItem a = ref s.Items[i];
            for (int j = 0; j < s.ItemCount; j++)
            {
                ref UiItem b = ref s.Items[j];
                if (j != i && b.Kind == UiKind.Text && b.Style == UiStyle.SectionHeader && b.Layer == a.Layer && b.Clip == a.Clip && Math.Abs(b.Y - a.Y) <= Tolerance
                    && !string.IsNullOrEmpty(b.Text))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A panel with nothing in it, as the empty side box at the right of every Flare tab in beta 3: an empty box is a hole.</summary>
        public static int EmptyPanels(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int p = 0; p < s.ItemCount; p++)
            {
                ref UiItem panel = ref s.Items[p];
                if (panel.Kind != UiKind.Box || (panel.Flags & UiFlags.Panel) == 0)
                {
                    continue;
                }
                bool held = false;
                for (int i = p + 1; i < s.ItemCount && !held; i++)
                {
                    ref UiItem it = ref s.Items[i];
                    held = (it.Flags & UiFlags.Decor) == 0 && !(it.Kind == UiKind.Text && string.IsNullOrEmpty(it.Text)) && UiDraw.Inside(s, i, p);
                }
                for (int c = 0; c < s.ClipCount && !held; c++)
                {
                    held = s.Clips[c].ContentHeight > 0f && ClipInside(s, (short)c, ref panel);
                }
                if (!held)
                {
                    found++;
                    say?.Invoke(s.Name + ": " + Describe(s, p) + " holds nothing");
                }
            }
            return found;
        }

        /// <summary>
        /// Words and faces of an overlay with no surface of their own under them, read through over the page (beta 3's call:
        /// the caller's name across the page's Send button). An overlay stands on a card.
        /// </summary>
        public static int Floating(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Layer == 0 || (it.Kind != UiKind.Text && it.Kind != UiKind.Avatar) || (it.Kind == UiKind.Text && string.IsNullOrEmpty(it.Text)))
                {
                    continue;
                }
                bool card = false;
                for (int p = it.Parent, guard = 0; p >= 0 && guard < 32 && !card; p = s.Items[p].Parent, guard++)
                {
                    card = s.Items[p].Kind == UiKind.Box && Surface(s.Items[p].Fill);
                }
                if (!card)
                {
                    found++;
                    say?.Invoke(s.Name + ": \"" + Cut(it.Text) + "\" floats over the page on no card of its own");
                }
            }
            return found;
        }

        private static bool Surface(UiRole fill)
        {
            return fill == UiRole.Panel || fill == UiRole.Field || fill == UiRole.Bubble || fill == UiRole.Row || fill == UiRole.Header || fill == UiRole.Accent
                   || fill == UiRole.Danger || fill == UiRole.RowSel || fill == UiRole.Notice || fill == UiRole.Hover;
        }

        /// <summary>The bands type falls in: labels, text, titles, figures and display. A page keeps to three of them.</summary>
        public static int TypeBand(UiStyle style)
        {
            float size = UiTokens.Type(style).Size;
            return size < 15.5f ? 0 : size < 20f ? 1 : size < 24f ? 2 : 3;   // the rows' and labels' 13 to 15, a window's 18, a name's 22, a big figure's 26
        }

        /// <summary>
        /// More than three sizes of type on a page (the owner, 1 Nov: no slop; DIRECTION_CS2: at most three on a panel): its
        /// big figure, its text and its labels are all it needs. Sizes within a band (13 to 15) read as one; the frame's own
        /// words (a window's strip, the status bar) are the frame's, not the page's.
        /// </summary>
        public static int TypeLevels(UiScene s, Action<string> say)
        {
            int bands = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind == UiKind.Text && !string.IsNullOrEmpty(it.Text) && (it.Flags & (UiFlags.Chrome | UiFlags.Prompt)) == 0)
                {
                    bands |= 1 << TypeBand(it.Style);
                }
            }
            int used = 0;
            for (int b = bands; b != 0; b >>= 1)
            {
                used += b & 1;
            }
            if (used <= 3)
            {
                return 0;
            }
            say?.Invoke(s.Name + ": " + used + " sizes of type where three would do (bands " + Convert.ToString(bands, 2) + ")");
            return 1;
        }

        /// <summary>
        /// Marks lying over a panel they do not belong to, as beta 3 drew the Team's first row inside the Games table: a mark
        /// stands on its own panel or on the ground beside one, never over another's words.
        /// </summary>
        public static int Strays(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if ((it.Flags & (UiFlags.Decor | UiFlags.Prompt | UiFlags.Panel)) != 0 || it.Clip >= 0 || (it.Kind == UiKind.Text && string.IsNullOrEmpty(it.Text)))
                {
                    continue;
                }
                for (int p = 0; p < s.ItemCount; p++)
                {
                    ref UiItem panel = ref s.Items[p];
                    if (p == i || (panel.Flags & UiFlags.Panel) == 0 || panel.Layer != it.Layer || panel.Clip >= 0 || UiDraw.Inside(s, i, p) || UiDraw.Inside(s, p, i))
                    {
                        continue;
                    }
                    float w = Math.Min(it.X + it.W, panel.X + panel.W) - Math.Max(it.X, panel.X);
                    float h = Math.Min(it.Y + it.H, panel.Y + panel.H) - Math.Max(it.Y, panel.Y);
                    if (w > Tolerance && h > Tolerance)
                    {
                        found++;
                        say?.Invoke(s.Name + ": " + Describe(s, i) + " lies over " + Describe(s, p) + ", a panel it does not belong to");
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>Targets whose command their template does not define: a button no press answers.</summary>
        public static int UnknownCommands(UiScene s, int[] known, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Command != UiDraw.CloseCommand && Array.IndexOf(known, s.Nodes[i].Command) < 0)   // a window's close is the host's own Back
                {
                    found++;
                    say?.Invoke(s.Name + ": target " + i + " runs command " + s.Nodes[i].Command + ", which its template answers nowhere");
                }
            }
            return found;
        }

        /// <summary>A screen without its prompt bar, or one whose bar does not say how to go back.</summary>
        public static int NoPrompts(UiScene s, Action<string> say)
        {
            bool back = false;
            for (int i = 0; i < s.PromptCount; i++)
            {
                back |= s.Prompts[i].Action == UiAction.Back;
            }
            if (s.PromptCount == 0 || s.PromptH <= 0f || !back)
            {
                say?.Invoke(s.Name + ": " + (s.PromptCount == 0 ? "no prompt bar" : !back ? "the prompt bar does not say how to go back" : "the prompt bar has no band"));
                return 1;
            }
            return 0;
        }

        /// <summary>Motion outside game timing: feedback, focus, selection and arrivals 60 to 240 ms, a fill at most 600 ms (the stagger: EconomyCheck).</summary>
        public static int SlowMotion(Action<string> say)
        {
            int found = 0;
            for (int k = 0; k < (int)UiMotionKind.Count; k++)
            {
                var kind = (UiMotionKind)k;
                float d = UiTokens.BaseDuration(kind);
                float most = kind == UiMotionKind.Fill ? 0.6f : 0.24f;
                if (d < 0.06f || d > most)
                {
                    found++;
                    say?.Invoke("motion: " + kind + " takes " + (int)(d * 1000f) + " ms, outside 60 to " + (int)(most * 1000f));
                }
            }
            return found;
        }

        // ------------------------------------------------------------------------------------------------- helpers

        private static bool Skip(ref UiItem it) => (it.Flags & (UiFlags.Decor | UiFlags.Overflow)) != 0 || it.Kind == UiKind.Spark || it.Kind == UiKind.Slice;   // a donut's slices lay over each other

        private static bool OnGrid(float v)
        {
            float r = v / UiTokens.Grid;
            return Math.Abs(r - Math.Round(r)) * UiTokens.Grid < 0.01f;
        }

        private static bool Large(UiStyle style)
        {
            UiType t = UiTokens.Type(style);
            return t.Size >= LargeText || (t.Weight >= 700 && t.Size >= LargeBoldText);
        }

        private static bool InDisabled(UiScene s, int item)
        {
            int p = s.Items[item].Parent;
            for (int guard = 0; p >= 0 && guard < 32; guard++)
            {
                if ((s.Items[p].Flags & UiFlags.Disabled) != 0)
                {
                    return true;
                }
                p = s.Items[p].Parent;
            }
            return false;
        }

        /// <summary>
        /// The world behind a see-through surface, as the checks take it: a sunlit pavement, the brightest the game's streets
        /// run, so a word that reads over it reads over any of the city.
        /// </summary>
        public static readonly UiColour World = new UiColour(0.76f, 0.74f, 0.69f, 1f);

        /// <summary>The colour under an item: the world, then each filled parent's colour laid over it, outermost first.</summary>
        public static UiColour Beneath(UiScene s, int item, bool dark)
        {
            if ((s.Items[item].Flags & UiFlags.Prompt) != 0)
            {
                return UiColour.Of(UiRole.StatusBar, dark).Over(World);   // the prompts stand on the status bar over the world
            }
            var chain = new int[32];
            int n = 0;
            for (int p = s.Items[item].Parent; p >= 0 && n < chain.Length; p = s.Items[p].Parent)
            {
                chain[n++] = p;
            }
            UiColour under = World;
            bool panelled = false;
            for (int k = 0; k < n; k++)
            {
                panelled |= (s.Items[chain[k]].Flags & UiFlags.Panel) != 0;
            }
            if (!panelled)
            {
                under = PanelUnder(s, item, dark);   // a scroll region's rows hang from no parent: the panel they scroll in is still under them
            }
            for (int k = n - 1; k >= 0; k--)
            {
                ref UiItem p = ref s.Items[chain[k]];
                if (p.Kind == UiKind.Box && p.Fill != UiRole.None)
                {
                    UiColour fill = UiColour.Of(p.Fill, dark);
                    under = fill.WithAlpha(fill.A * p.Alpha).Over(under);
                }
                else if (p.Kind == UiKind.Avatar)
                {
                    under = UiColour.AvatarFill(p.Value, dark);
                }
            }
            return under;
        }

        // The world under an item through the panel that holds its middle on its layer, the last drawn before it, or the world.
        private static UiColour PanelUnder(UiScene s, int item, bool dark)
        {
            ref UiItem it = ref s.Items[item];
            float cx = it.X + it.W / 2f, cy = it.Y + it.H / 2f;
            if (it.Clip >= 0)
            {
                ref UiClip c = ref s.Clips[it.Clip];
                cx += c.X;
                cy += c.Y - c.Offset;
            }
            for (int p = item - 1; p >= 0; p--)
            {
                ref UiItem box = ref s.Items[p];
                if (box.Kind == UiKind.Box && (box.Flags & UiFlags.Panel) != 0 && box.Layer == it.Layer && box.Clip < 0 && box.Fill != UiRole.None
                    && cx >= box.X && cx <= box.X + box.W && cy >= box.Y && cy <= box.Y + box.H)
                {
                    UiColour fill = UiColour.Of(box.Fill, dark);
                    return fill.WithAlpha(fill.A * box.Alpha).Over(World);
                }
            }
            return World;
        }

        private static string Describe(UiScene s, int i)
        {
            ref UiItem it = ref s.Items[i];
            string what = it.Kind == UiKind.Text ? "\"" + Cut(it.Text) + "\"" : it.Kind == UiKind.Icon ? "icon " + it.Icon : it.Kind + " " + it.Fill;
            return what + " [" + (int)it.X + "," + (int)it.Y + " " + (int)it.W + "x" + (int)it.H + "]";
        }

        private static string Cut(string text) => text == null ? string.Empty : text.Length <= 40 ? text : text.Substring(0, 37) + "...";
    }
}
