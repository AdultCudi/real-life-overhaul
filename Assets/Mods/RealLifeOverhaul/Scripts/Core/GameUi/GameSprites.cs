using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own interface pictures by name ("Icon-Calendar"), never shipped: found among the sprites in memory in one
    /// look behind the loading screen (<see cref="Prewarm"/>, a piece of ScenePrewarm), for every name the features asked
    /// for in their Awake (<see cref="Want"/>). A name first asked for later is looked for only while nothing is seen,
    /// the loading screen, the game's black screen or a pause (RLO-PERF-005); in play it gives null until then. A name the
    /// game does not have gives null, and the caller shows no picture. Costs nothing a frame.
    /// </summary>
    public static class GameSprites
    {
        public static IModLogger Logger;

        private static readonly Dictionary<string, Sprite> Found = new Dictionary<string, Sprite>(32, StringComparer.Ordinal);
        private static readonly HashSet<string> Wanted = new HashSet<string>(StringComparer.Ordinal);
        private static bool _stale;   // a name is wanted that no look has been made for since

        /// <summary>Names pictures that will be asked for, so the one look finds them all. Called from Awake.</summary>
        public static void Want(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != null && Wanted.Add(names[i]))
                {
                    _stale = true;
                }
            }
        }

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            Sprite sprite;
            if (Found.TryGetValue(name, out sprite) && sprite != null)
            {
                return sprite;
            }
            if (Wanted.Add(name))
            {
                _stale = true;
            }
            if (_stale && Unseen())
            {
                Look();
                return Found.TryGetValue(name, out sprite) && sprite != null ? sprite : null;
            }
            return null;
        }

        /// <summary>Behind the loading screen: the one look for every name wanted so far. Always done.</summary>
        public static bool Prewarm()
        {
            Look();
            return true;
        }

        // While a long frame is not seen: the loading screen or its late part, the game's black screen at full strength, a pause.
        private static bool Unseen()
        {
            return SceneFinds.Black || ScenePrewarm.InLate || Looks.Hidden;
        }

        // One pass over the sprites in memory, a few thousand names compared.
        private static void Look()
        {
            _stale = false;
            try
            {
                Sprite[] all = SceneFinds.InMemory<Sprite>("game UI: the game's interface pictures being found by name");
                for (int i = 0; i < all.Length; i++)
                {
                    Sprite sprite = all[i];
                    string name = sprite != null ? sprite.name : null;
                    if (name != null && Wanted.Contains(name) && (!Found.TryGetValue(name, out Sprite held) || held == null))
                    {
                        Found[name] = sprite;   // the first of a name, or one in place of a picture that has gone
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    int missing = 0;
                    string first = null;
                    foreach (string wanted in Wanted)
                    {
                        if (!Found.ContainsKey(wanted))
                        {
                            missing++;
                            first = first ?? wanted;
                        }
                    }
                    Logger?.Info("Game UI: " + (Wanted.Count - missing) + " of " + Wanted.Count + " of the game's pictures found among " + all.Length
                                 + (missing > 0 ? " (first one missing: " + first + ")." : "."));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>A city was left: its sprites may have gone with it, and the next load's look finds them again.</summary>
        public static void Forget()
        {
            Found.Clear();
            _stale = Wanted.Count > 0;
        }
    }
}
