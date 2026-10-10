using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own interface pictures by name ("Icon-Calendar", "Icon-SetDestination"). Nothing is shipped: a
    /// picture is found among the sprites the game has in memory, once, the first time one is asked for. A name the
    /// game does not have in memory gives null, and whoever asked shows no picture.
    /// </summary>
    public static class GameSprites
    {
        public static IModLogger Logger;

        private static readonly Dictionary<string, Sprite> Found = new Dictionary<string, Sprite>(32, StringComparer.Ordinal);
        private static readonly HashSet<string> Wanted = new HashSet<string>(StringComparer.Ordinal);
        private static bool _looked;

        /// <summary>Names a picture that will be asked for, before the one look through the game's sprites.</summary>
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

        // One pass over the sprites in memory (a few thousand names compared): only ever on the opening of a menu.
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

        /// <summary>A city was left: its sprites may be gone with it.</summary>
        public static void Forget()
        {
            Found.Clear();
            _looked = false;
        }
    }
}
