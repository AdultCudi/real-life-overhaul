using System;
using BAModAPI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The player's home screen kept on this computer with the game's settings (PlayerPrefs), never in the save or sent:
    /// each Going Public player's own. <see cref="Changed"/> tells the phones to lay themselves out again. Costs nothing a frame.
    /// </summary>
    public static class PhoneLayoutPrefs
    {
        private const string Pref = "reallife.phone.layout";   // not a locale key: a dot, not an underscore

        public static IModLogger Logger;

        private static PhoneLayout _layout;
        private static bool _heard;
        private static readonly Action Reread = Tell;

        /// <summary>A move, the option switched, or the options read again: the phones lay themselves out again.</summary>
        public static event Action Changed;

        /// <summary>The home screen has folders and a dock (the option; off, the plain pages).</summary>
        public static bool On => Settings.PhoneFolders;

        /// <summary>The layout, read from this computer the first time it is asked for.</summary>
        public static PhoneLayout Layout
        {
            get
            {
                if (!_heard)
                {
                    _heard = true;
                    Settings.Changed += Reread;   // once a session: a preset or an import may switch the option
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

        /// <summary>Moves an app and keeps the layout; false, nothing changed, if the layout refused the place.</summary>
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
