using System;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// How a kit binds over a screen of the game's, game-free. The game keeps its full menu and its apps switched off
    /// until they are opened (FullMenu.Start, ShowApp), and the engine's searches by default skip what is switched off,
    /// so a kit that looked for its canvas the plain way found nothing at every load and gave up for the session (beta 2,
    /// the Contacts app). Here: which of the canvases above an app is its root and which sets its sort order, the wait
    /// before a kit that found its screen not built yet looks again, and the source rule EconomyCheck runs over the kit
    /// that every search of the scene says whether it takes switched off objects. Cost: a few comparisons; offline, a
    /// pattern a line.
    /// </summary>
    public static class UiBindRules
    {
        public const float FirstWait = 5f;      // seconds before the first look again
        public const float LongestWait = 120f;  // seconds: the wait doubles up to this
        public const int MostTries = 8;         // looks in a city before the game's screen stays its own until the next city

        /// <summary>Seconds to wait after this many looks that found the screen not built yet (1 the first).</summary>
        public static float Wait(int misses)
        {
            if (misses <= 1)
            {
                return FirstWait;
            }
            float w = FirstWait;
            for (int i = 1; i < misses && w < LongestWait; i++)
            {
                w *= 2f;
            }
            return w < LongestWait ? w : LongestWait;
        }

        /// <summary>True when the screen stays the game's own for the rest of the city after this many looks.</summary>
        public static bool Stops(int misses) => misses >= MostTries;

        /// <summary>The root among the canvases above an object, nearest first (as GetComponentsInParent gives them): the last; -1 for none.</summary>
        public static int Root(int count) => count - 1;

        /// <summary>The canvas whose sort order an object is drawn by: the nearest that overrides sorting, else the root; -1 for none.</summary>
        public static int Sorter(bool[] overrides, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (overrides[i])
                {
                    return i;
                }
            }
            return count - 1;
        }

        // A search of the scene called without saying whether it takes switched off objects: GetComponentInParent<T>(),
        // GetComponentsInChildren(typeof(T)), FindObjectOfType<T>() and the like. rootCanvas leans on the engine's record of a
        // canvas's parents, kept up while the canvas is on, so a kit walks the parents itself.
        private static readonly Regex Blind = new Regex(
            @"\b(GetComponentInParent|GetComponentsInParent|GetComponentInChildren|GetComponentsInChildren|FindObjectOfType|FindObjectsOfType)"
            + @"\s*(<[^<>()]*>)?\s*\(\s*(typeof\s*\([^()]*\)\s*)?\)|\.rootCanvas\b",
            RegexOptions.CultureInvariant);

        /// <summary>The blind search in a line of code (its comment left out), or null when there is none.</summary>
        public static string BlindSearch(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            string code = comment >= 0 ? line.Substring(0, comment) : line;
            Match m = Blind.Match(code);
            return m.Success ? m.Value : null;
        }
    }
}
