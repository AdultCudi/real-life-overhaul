using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The rules the kit's parts keep, checked on a laid-out scene beside UiCheckRules': figures stand at the right of
    /// their cell, a centred line sits on the middle of the control it labels (the renderer then puts its capitals there),
    /// a picture keeps its own shape, and a chart's value axis steps by round figures. Each returns how many it found and
    /// says each through <paramref name="say"/>. Game-free: EconomyCheck runs them on every state of the parts, at every
    /// frame and in every language. Cost: offline only.
    /// </summary>
    public static class UiPartRules
    {
        /// <summary>
        /// Figures not at the right: a text that is a figure (money, a count, a share, a change) laid at the left of a box
        /// wider than it, where a column of them would not line up. Times and dates are words of a column of their own.
        /// </summary>
        public static int FiguresLeft(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.Text || (it.Flags & (UiFlags.Right | UiFlags.Centre | UiFlags.Label | UiFlags.Chrome | UiFlags.Prompt | UiFlags.Wrap)) != 0
                    || !Figure(it.Text) || it.W <= UiTextRules.Width(it.Text, it.Style) + UiTokens.S3 || UiTokens.Type(it.Style).Size > 15.5f)
                {
                    continue;
                }
                found++;
                say?.Invoke(s.Name + ": the figure \"" + it.Text + "\" stands at the left of its " + (int)it.W + " px; a figure stands at the right");
            }
            return found;
        }

        /// <summary>Whether words are a figure: a sum, a count, a share or a change, and not a time.</summary>
        public static bool Figure(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(':') >= 0)
            {
                return false;
            }
            char c = text[0];
            bool lead = char.IsDigit(c) || c == '$' || c == '+' || c == '−' || c == '-' || c == '€' || c == '£';
            if (!lead)
            {
                return false;
            }
            for (int i = 1; i < text.Length; i++)
            {
                char d = text[i];
                if (char.IsLetter(d) && d != 'k' && d != 'M')
                {
                    return false;   // "4 people", "20 days": words with a figure in them
                }
            }
            return true;
        }

        /// <summary>
        /// Lines centred on a control that are not on its middle: a single line flagged Middle inside a box no taller than a
        /// control or two of its lines (a button, a chip, a pill, a field, a tab), whose line box is not centred on it. The renderer
        /// puts a centred line's capitals on its line box's middle (UiView, Capline), so this is where they land.
        /// </summary>
        public static int OffCentre(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.Text || (it.Flags & UiFlags.Middle) == 0 || (it.Flags & UiFlags.Wrap) != 0 || it.Parent < 0 || string.IsNullOrEmpty(it.Text))
                {
                    continue;
                }
                ref UiItem p = ref s.Items[it.Parent];
                if (p.Kind != UiKind.Box || p.H > Math.Max(UiTokens.Control, UiTextRules.LineHeight(it.Style) * 2f + UiTokens.S1) || p.H < it.H)
                {
                    continue;
                }
                float off = it.Y + it.H / 2f - (p.Y + p.H / 2f);
                if (Math.Abs(off) > UiCheckRules.Tolerance)
                {
                    found++;
                    say?.Invoke(s.Name + ": \"" + it.Text + "\" sits " + off.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " px off the middle of its " + (int)p.H + " px box");
                }
            }
            return found;
        }

        /// <summary>The px a centred line's capitals stood above its box's middle when the line box was centred, for a style.</summary>
        public static float LineBoxLift(UiStyle style)
        {
            UiType t = UiTokens.Type(style);
            UiFace f = UiFontMetrics.Face(t.Family, t.Weight);
            float capCentre = (f.Ascender - f.CapHeight / 2f) / f.UnitsPerEm * t.Size;   // from the line's top, as the engine sets a top-aligned line
            return UiTextRules.LineHeight(style) / 2f - capCentre;
        }

        /// <summary>Pictures drawn in another shape than their own (stretched), or out of their tile.</summary>
        public static int Stretched(UiScene s, Action<string> say)
        {
            int found = 0;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.Picture)
                {
                    continue;
                }
                bool shape = it.H > 0f && it.Value > 0f && Math.Abs(it.W / it.H - it.Value) <= 0.01f * it.Value;
                bool inside = it.Parent < 0 || (it.X >= s.Items[it.Parent].X - UiCheckRules.Tolerance && it.Y >= s.Items[it.Parent].Y - UiCheckRules.Tolerance
                                                && it.X + it.W <= s.Items[it.Parent].X + s.Items[it.Parent].W + UiCheckRules.Tolerance
                                                && it.Y + it.H <= s.Items[it.Parent].Y + s.Items[it.Parent].H + UiCheckRules.Tolerance);
                if (!shape || !inside)
                {
                    found++;
                    say?.Invoke(s.Name + ": the picture \"" + it.Text + "\" is " + (shape ? "out of its tile" : "drawn " + (int)it.W + " x " + (int)it.H + " for a shape of " + it.Value));
                }
            }
            return found;
        }

        /// <summary>A chart whose value axis does not step by a round figure (1, 2, 2.5 or 5 of a power of ten) from a round figure.</summary>
        public static int RoughTicks(UiChartModel m, Action<string> say)
        {
            if (m == null || m.Points == null || m.Points.Length == 0)
            {
                return 0;
            }
            UiChart.Scale(m, out float lo, out float hi, out float step);
            double power = Math.Pow(10, Math.Floor(Math.Log10(step)));
            double unit = Math.Round(step / power, 3);
            bool round = unit == 1.0 || unit == 2.0 || unit == 2.5 || unit == 5.0;
            int count = (int)Math.Round((hi - lo) / step);
            bool whole = UiChart.OnStep(lo, step) && UiChart.OnStep(hi, step) && count >= 1 && count <= 8;
            if (round && whole)
            {
                return 0;
            }
            say?.Invoke(m.Title + ": its axis steps by " + step + " from " + lo + " to " + hi);
            return 1;
        }
    }
}
