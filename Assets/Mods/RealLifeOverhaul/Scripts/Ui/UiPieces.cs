using System;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pieces of the interface's one-second look, each with a breaker of its own: every kit the look puts over a screen of the
    /// game's, the fonts' steps, and the two counts kept whether the interface is on or off. A piece that throws is taken off and
    /// said once in Player.log with what failed, and every other piece goes on; it is asked again in the next city, and after a
    /// second fault it stays off until the game is started again. Before (1.0.4) the pieces shared one try: a piece that threw at
    /// every look failed the whole look, and five looks later the guard switched the whole interface off for what one screen did.
    /// A piece that stands itself up again each second (the HUD, the world's boxes) asks here first too, so its fault is not met
    /// again and again. Game-free; checked offline (EconomyCheck, UiGuardChecks). COST: an array read a piece a look.
    /// </summary>
    public static class UiPieces
    {
        public const int Most = 64;      // pieces at the most: the look has some forty
        public const int Tries = 2;      // faults of one piece in a session before it stays off until the game is started again

        private static readonly string[] Names = new string[Most];
        private static readonly string[] Labels = new string[Most];
        private static readonly int[] Faults = new int[Most];
        private static readonly bool[] Down = new bool[Most];
        private static int _count;

        /// <summary>How many pieces are known.</summary>
        public static int Count => _count;

        /// <summary>
        /// A piece's number, made the first time its name is given: its name in English for Player.log ("the phone") and the key of
        /// its name in the player's language for a notice (a screen's or a section's own label), or null. -1 when no room is left.
        /// </summary>
        public static int Id(string name, string labelKey = null)
        {
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(Names[i], name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            if (string.IsNullOrEmpty(name) || _count >= Most)
            {
                return -1;
            }
            Names[_count] = name;
            Labels[_count] = labelKey;
            return _count++;
        }

        /// <summary>A piece's name in English; "a piece" for a number that is none.</summary>
        public static string Name(int id) => id >= 0 && id < _count ? Names[id] : "a piece";

        /// <summary>The key of a piece's name in the player's language, or null.</summary>
        public static string Label(int id) => id >= 0 && id < _count ? Labels[id] : null;

        /// <summary>True while the piece is off after a fault: it is not asked.</summary>
        public static bool Off(int id) => id >= 0 && id < _count && Down[id];

        /// <summary>True once the piece has failed <see cref="Tries"/> times: it stays off until the game is started again.</summary>
        public static bool Spent(int id) => id >= 0 && id < _count && Faults[id] >= Tries;

        /// <summary>How many faults the piece has had this session.</summary>
        public static int FaultsOf(int id) => id >= 0 && id < _count ? Faults[id] : 0;

        /// <summary>The piece failed: it is off from now. True when it was on (the caller says it once and takes its kit off).</summary>
        public static bool Trip(int id)
        {
            if (id < 0 || id >= _count)
            {
                return false;
            }
            Faults[id]++;
            bool was = !Down[id];
            Down[id] = true;
            return was;
        }

        /// <summary>A new city: every piece that has a try left is asked again. Returns how many came back.</summary>
        public static int Rearm()
        {
            int back = 0;
            for (int i = 0; i < _count; i++)
            {
                if (Down[i] && Faults[i] < Tries)
                {
                    Down[i] = false;
                    back++;
                }
            }
            return back;
        }

        /// <summary>How many pieces are off now.</summary>
        public static int OffCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    n += Down[i] ? 1 : 0;
                }
                return n;
            }
        }

        /// <summary>The pieces off now by name, for the interface's line and a bug report; "none" for none.</summary>
        public static string OffNames()
        {
            var said = new StringBuilder(64);
            for (int i = 0; i < _count; i++)
            {
                if (Down[i])
                {
                    said.Append(said.Length > 0 ? ", " : string.Empty).Append(Names[i]);
                }
            }
            return said.Length > 0 ? said.ToString() : "none";
        }

        /// <summary>For the checks: every piece and fault forgotten.</summary>
        internal static void ForgetForTests()
        {
            for (int i = 0; i < _count; i++)
            {
                Names[i] = Labels[i] = null;
                Faults[i] = 0;
                Down[i] = false;
            }
            _count = 0;
        }
    }
}
