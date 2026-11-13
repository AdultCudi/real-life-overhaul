using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The scroll of the one open drop-down list, held by the scene across layouts and moved by the host, so the wheel, a drag of the list or its thumb and the keys
    /// work for every screen that draws <see cref="UiDraw.ChoiceList"/> without each screen keeping an offset and answering the wheel for it. A list is the same one
    /// while it is laid in every layout; a layout that lays none lets it go, so the next one opens on its chosen option. Game-free so the checks run it.
    /// </summary>
    public struct UiChoiceScroll
    {
        /// <summary>The px one wheel notch moves an open list: two rows.</summary>
        public const float Notch = UiTokens.ControlSmall * 2f;

        private int _key;
        private bool _seen;
        private float _offset;

        /// <summary>A new layout begins: the list laid in the one before stays held, one that was not laid is let go (it was closed); another screen holds none.</summary>
        public void Begin(bool otherScreen)
        {
            if (otherScreen || !_seen)
            {
                _key = 0;
            }
            _seen = false;
        }

        /// <summary>
        /// The offset a list lays at: the held one while it is the list held, else its opening one, which shows the chosen option (the first of several ticked) in view.
        /// <paramref name="seed"/> is the screen's own offset, kept when it already shows the option.
        /// </summary>
        public float Open(int key, float seed, int shown, float rowH, float viewH, float contentH)
        {
            key = key == 0 ? 1 : key;
            _seen = true;
            if (key != _key)
            {
                _key = key;
                _offset = InView(seed, shown, rowH, viewH);
            }
            _offset = Math.Max(0f, Math.Min(_offset, Math.Max(0f, contentH - viewH)));
            return _offset;
        }

        /// <summary>Holds the offset the host moved the list to.</summary>
        public void Set(float offset) => _offset = Math.Max(0f, offset);

        /// <summary>The offset that keeps a row in view: the given one when the row already is, else the row centred.</summary>
        public static float InView(float offset, int row, float rowH, float viewH)
        {
            if (row < 0)
            {
                return Math.Max(0f, offset);
            }
            float top = row * rowH;
            if (top >= offset - 0.5f && top + rowH <= offset + viewH + 0.5f)
            {
                return Math.Max(0f, offset);
            }
            return Math.Max(0f, top - (viewH - rowH) / 2f);
        }

        /// <summary>The offset for a row the keys moved to: scrolled only as far as the row needs, not centred.</summary>
        public static float Reveal(float offset, int row, float rowH, float viewH)
        {
            float top = row * rowH;
            if (top < offset)
            {
                return Math.Max(0f, top);
            }
            if (top + rowH > offset + viewH)
            {
                return Math.Max(0f, top + rowH - viewH);
            }
            return offset;
        }

        /// <summary>The key of a list: what tells it from another list of the same scene (its command, how many options it has and its first and last words).</summary>
        public static int Key(int command, string[] options, int count)
        {
            unchecked
            {
                int h = command * 397 ^ count * 31;
                if (options != null && count > 0)
                {
                    h = h * 31 + (options[0] ?? string.Empty).GetHashCode();
                    h = h * 31 + (options[Math.Min(count, options.Length) - 1] ?? string.Empty).GetHashCode();
                }
                return h;
            }
        }

        /// <summary>The widest a list's rows need, so a long option shows whole: its text, the tick's room, the paddings and the thumb's room.</summary>
        public static float RowsNeed(string[] options, int count, bool scrolls)
        {
            float widest = 0f;
            for (int i = 0; options != null && i < count && i < options.Length; i++)
            {
                widest = Math.Max(widest, UiTextRules.Width(options[i], UiStyle.Body));
            }
            return widest + UiTokens.S3 * 2f + UiTokens.Icon + UiTokens.S2 + UiTokens.S2 + (scrolls ? UiTokens.S4 : 0f) + 1f;
        }
    }
}
