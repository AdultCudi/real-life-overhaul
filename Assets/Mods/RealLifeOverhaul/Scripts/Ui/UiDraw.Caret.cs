using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The caret of a field that takes the keys: a thin cyan line where the next letter goes, at the words' height, after the
    /// last letter (or before the placeholder of an empty field). One rule for every text field of the kit: the host says
    /// which target takes the keys (<see cref="UiScene.TypingCommand"/> and <see cref="UiScene.TypingArg"/>, its command and
    /// argument) before it lays a screen out, and each field the screen draws with its own command and argument asks
    /// <see cref="Typing"/>, so no screen has to remember to say so. The one caret is recorded in the scene
    /// (<see cref="UiScene.CaretItem"/>) so the host blinks it by showing and hiding that one item (UiView.SetCaret), never by
    /// laying the screen out again. Game-free. Cost: one item a typing field; nothing while no field is typing.
    /// </summary>
    public static partial class UiDraw
    {
        public const float CaretWidth = 2f;      // px: the caret's line
        private const float CaretGap = 2f;       // px: between the last letter and the caret
        private const float CaretLead = 8f;      // px: an empty field's caret, to its placeholder (UiTokens.S2)

        /// <summary>Whether the target a field is drawn for (its command and argument) is the one that takes the keys now.</summary>
        public static bool Typing(UiScene s, int command, int arg) => s.TypingCommand >= 0 && s.TypingCommand == command && s.TypingArg == arg;

        /// <summary>The room an empty typing field leaves its placeholder: the caret and its gap come first.</summary>
        public static float CaretRoom => CaretWidth + CaretLead;

        /// <summary>
        /// The words of a field while it is typed in: the end of them that fits the room less the caret's, so the caret
        /// always stands after the last letter; the words themselves when they fit.
        /// </summary>
        public static string Typed(string text, UiStyle style, float room) => UiTextRules.Tail(text, style, Math.Max(0f, room - CaretWidth - CaretGap));

        /// <summary>
        /// Draws the caret at a place on a row: its left edge at <paramref name="x"/>, as tall as a line of the style, centred
        /// on the row. It is a decor item of the layout (no focus, no spill), and the scene remembers it for the blink.
        /// Returns the item.
        /// </summary>
        public static int Caret(UiScene s, UiAt at, float x, float rowY, float rowH, UiStyle style)
        {
            float h = UiTextRules.LineHeight(style);
            int caret = Box(s, at, x, rowY + (rowH - h) / 2f, CaretWidth, h, UiRole.Accent, UiRole.None, 0f, UiFlags.Decor);
            s.CaretItem = caret;
            return caret;
        }

        /// <summary>
        /// The caret at the end of a field's words (left aligned from <paramref name="x"/>), or before the placeholder of an
        /// empty one; the width of the words shown is measured here.
        /// </summary>
        public static void CaretAfter(UiScene s, UiAt at, float x, float rowY, float rowH, string shown, UiStyle style, float room)
        {
            float end = string.IsNullOrEmpty(shown) ? 0f : Math.Min(UiTextRules.Width(shown, style) + CaretGap, Math.Max(0f, room - CaretWidth));
            Caret(s, at, x + end, rowY, rowH, style);
        }
    }
}
