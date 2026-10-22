using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own interface pictures by name ("Icon-Calendar"), never shipped: found among the sprites in memory in one
    /// look, the first time a new name is asked for. A name the game does not have gives null, and the caller shows no
    /// picture. Costs nothing a frame.
    /// </summary>
    public static class GameSprites
    {
        public static IModLogger Logger;

        private static readonly Dictionary<string, Sprite> Found = new Dictionary<string, Sprite>(32, StringComparer.Ordinal);
        private static readonly HashSet<string> Wanted = new HashSet<string>(StringComparer.Ordinal);
        private static bool _looked;

        /// <summary>Names pictures that will be asked for, so the one look finds them all.</summary>
        public static void Want(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != null && Wanted.Add(names[i]))
                {
                    _looked = false;
                }
            }
        }

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            if (Wanted.Add(name))
            {
                _looked = false;
            }
            if (!_looked)
            {
                Look();
            }
            Sprite sprite;
            return Found.TryGetValue(name, out sprite) && sprite != null ? sprite : null;
        }

        // One pass over the sprites in memory, a few thousand names compared: only on a menu's opening or behind a load.
        private static void Look()
        {
            _looked = true;
            try
            {
                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                for (int i = 0; i < all.Length; i++)
                {
                    Sprite sprite = all[i];
                    if (sprite != null && Wanted.Contains(sprite.name) && !Found.ContainsKey(sprite.name))
                    {
                        Found[sprite.name] = sprite;
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    int missing = 0;
                    string first = null;
                    foreach (string name in Wanted)
                    {
                        if (!Found.ContainsKey(name))
                        {
                            missing++;
                            first = first ?? name;
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

        /// <summary>A city was left: its sprites may have gone with it.</summary>
        public static void Forget()
        {
            Found.Clear();
            _looked = false;
        }
    }
}
