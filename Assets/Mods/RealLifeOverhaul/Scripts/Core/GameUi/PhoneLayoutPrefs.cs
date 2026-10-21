using System;
using BAModAPI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The player's own home screen (PhoneLayout), kept on this computer with the game's other settings (PlayerPrefs), as
    /// the mod's options are: the player's phone in every save, and in a Going Public session each player's own, with
    /// nothing in the save and nothing sent to anyone. Read the first time a phone asks, written when an app is moved.
    ///
    /// Whoever shows a phone hears <see cref="Changed"/>: a move, the option "App folders" switched on or off, or the
    /// options read again (a preset, an import, a reset). Cost: nothing per frame; a line read once a session and written
    /// on a move.
    /// </summary>
    public static class PhoneLayoutPrefs
    {
        private const string Pref = "reallife.phone.layout";   // (not a locale key: a dot, not an underscore)

        public static IModLogger Logger;

        private static PhoneLayout _layout;
        private static bool _heard;
        private static readonly Action Reread = Tell;

        /// <summary>A move, the option switched, or the options read again: the phones lay themselves out again.</summary>
        public static event Action Changed;

        /// <summary>Whether the home screen has folders and a dock (the option; off, the plain pages of before).</summary>
        public static bool On => Settings.PhoneFolders;

        /// <summary>The layout, read from this computer the first time it is asked for.</summary>
        public static PhoneLayout Layout
        {
            get
            {
                if (!_heard)
                {
                    _heard = true;
                    Settings.Changed += Reread;   // (once a session: a preset or an import may switch the option)
                }
                if (_layout == null)
                {
                    string line = string.Empty;
                    try
                    {
                        line = UnityEngine.PlayerPrefs.GetString(Pref, string.Empty);
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                    }
                    _layout = PhoneLayout.Read(line);
                }
                return _layout;
            }
        }

        /// <summary>Moves an app and keeps the layout; false (nothing changed) if the layout refused the place.</summary>
        public static bool Move(string id, int place)
        {
            if (!Layout.Move(id, place))
            {
                return false;
            }
            Save();
            Tell();
            return true;
        }

        /// <summary>Every app back in its usual folder.</summary>
        public static void Reset()
        {
            Layout.Reset();
            Save();
            Tell();
        }

        /// <summary>The phones lay themselves out again (the option was switched).</summary>
        public static void Tell()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void Save()
        {
            try
            {
                UnityEngine.PlayerPrefs.SetString(Pref, _layout.Write());
                UnityEngine.PlayerPrefs.Save();   // on disk now: a crash later keeps the move
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
