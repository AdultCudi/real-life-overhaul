using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The mod's dark colours: Flare's night look (Social/Flare/FlareKit, FlareLook.Night, since 0.40.5) for the frame, the
    /// panels and the words, and the game's own blue for the page shown. A copy of a light window of the game's
    /// (the help screen, HelpFrame) is painted in them. Plain values; nothing runs.
    /// </summary>
    public static class DarkLook
    {
        public static readonly Color Back = C(33, 37, 42);        // tints the help's white frame: its top bar this, its body (a light grey) Flare's raised colour, 30/33/38
        public static readonly Color Raised = C(46, 50, 57);      // a button of the list on the left: a step above Flare's chip (36/39/45), so it stands off the body
        public static readonly Color Sunk = C(25, 27, 32);        // behind an open list of pages: between Flare's back and card
        public static readonly Color Field = C(46, 50, 57);       // the search box and the round buttons
        public static readonly Color Text = C(231, 233, 234);     // words: Flare's text
        public static readonly Color Soft = C(176, 186, 196);     // a description under a name, a page in the list
        public static readonly Color Muted = C(139, 152, 165);    // small capitals over a heading: Flare's muted
        public static readonly Color Chosen = C(102, 163, 255);   // the page shown: the game's blue (its help screen's topic, 38/113/231), lighter for a dark ground
        public static readonly Color Warn = C(240, 190, 90);      // what keeps a row from working: the game's orange, softened
        public static readonly Color Good = C(60, 200, 140);      // Flare's good
        public static readonly Color Track = C(70, 76, 85);       // a scroll bar's groove
        public static readonly Color Handle = C(132, 142, 154);   // and its handle

        /// <summary>A colour as rich text writes it ("#B0BAC4"), for a second line of another colour in one text.</summary>
        public static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        private static Color C(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
