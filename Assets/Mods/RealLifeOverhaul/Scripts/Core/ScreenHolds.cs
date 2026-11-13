using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a part of the mod can leave standing over the game's screen or holding of its input: a canvas that takes the pointer, a
    /// card waiting for an answer, the keys, the selection, the cursor, the player's walk. Whoever raises such a thing registers it
    /// once, with the part it belongs to (a feature's id, or the guard's name of a class of the mod's own machinery) and the way to
    /// let go of it now, which must be safe when nothing is held. When the guard switches a part off after its faults, every hold of
    /// that part is let go, each in a try of its own: what would have answered the hold is off, and nothing else would take it away,
    /// so without this a fault could leave the game taking no click (1.0.4: the bug report's card, opened on the interface the guard
    /// had just switched off, lay unseen over the whole screen until the game was closed). A feature's own clean-up runs first; this
    /// is the net under it. Game-free; checked offline (EconomyCheck, UiGuardChecks). COST: nothing until a part is switched off.
    /// </summary>
    public static class ScreenHolds
    {
        /// <summary>One thing a part can hold.</summary>
        public sealed class Hold
        {
            public readonly string Part;       // the feature's id, or a machinery class's guard name
            public readonly string What;       // in English, for Player.log: "the bug report's card"
            public readonly Action LetGo;      // lets go of it now; safe when nothing is held
            public readonly Func<bool> Held;   // true while it is held, for the log's line; null when nobody can tell

            internal Hold(string part, string what, Action letGo, Func<bool> held)
            {
                Part = part;
                What = what;
                LetGo = letGo;
                Held = held;
            }
        }

        private static readonly List<Hold> All = new List<Hold>(16);

        /// <summary>Every hold registered, in the order they were.</summary>
        public static IReadOnlyList<Hold> List => All;

        /// <summary>Registers a hold of a part; the same part and words again take the place of the first (a class made again in a new city).</summary>
        public static void Add(string part, string what, Action letGo, Func<bool> held = null)
        {
            if (string.IsNullOrEmpty(part) || string.IsNullOrEmpty(what) || letGo == null)
            {
                return;
            }
            lock (All)
            {
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].Part == part && All[i].What == what)
                    {
                        All[i] = new Hold(part, what, letGo, held);
                        return;
                    }
                }
                All.Add(new Hold(part, what, letGo, held));
            }
        }

        /// <summary>True when the part has a hold registered.</summary>
        public static bool Any(string part)
        {
            lock (All)
            {
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].Part == part)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Lets go of everything the part holds: each hold's own way, in a try of its own, so one that fails keeps none of the others.
        /// <paramref name="released"/> gets the words of those that were held (or could not say); <paramref name="failed"/> hears a hold
        /// whose letting go threw. Returns how many were asked. Never throws.
        /// </summary>
        public static int Release(string part, List<string> released, Action<string, Exception> failed)
        {
            if (string.IsNullOrEmpty(part))
            {
                return 0;
            }
            Hold[] mine;
            lock (All)
            {
                int n = 0;
                for (int i = 0; i < All.Count; i++)
                {
                    n += All[i].Part == part ? 1 : 0;
                }
                mine = new Hold[n];
                for (int i = 0, at = 0; i < All.Count; i++)
                {
                    if (All[i].Part == part)
                    {
                        mine[at++] = All[i];
                    }
                }
            }
            for (int i = 0; i < mine.Length; i++)
            {
                Hold hold = mine[i];
                bool held = true;
                try
                {
                    held = hold.Held == null || hold.Held();
                }
                catch (Exception)
                {
                    held = true;   // it cannot say: let go of all the same
                }
                try
                {
                    hold.LetGo();
                    if (held)
                    {
                        released?.Add(hold.What);
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        failed?.Invoke(hold.What, e);
                    }
                    catch (Exception)
                    {
                        // the next hold is let go all the same
                    }
                }
            }
            return mine.Length;
        }

        /// <summary>For the checks: the register emptied.</summary>
        internal static void ForgetForTests()
        {
            lock (All)
            {
                All.Clear();
            }
        }
    }
}
