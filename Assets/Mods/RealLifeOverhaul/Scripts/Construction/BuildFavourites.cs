using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The designs the player has starred in "Choose a building": a short list of design ids kept on this computer with the game's
    /// settings (PlayerPrefs), as the tour's seen flag is, never in the save and never sent. The list's rules are game-free
    /// (EconomyCheck's --buildmode-ui). Cost: one string read when Build mode opens, one write when a star is pressed.
    /// </summary>
    public static class BuildFavourites
    {
        private const string Pref = "reallife.buildmode.favourites";   // not a locale key: a dot, not an underscore
        public const int Most = 48;                                    // stars kept; the oldest goes when another is added

        private static string _list;

        /// <summary>True when the id is on a list (ids are joined by "|").</summary>
        public static bool Has(string list, string id)
        {
            if (string.IsNullOrEmpty(list) || string.IsNullOrEmpty(id))
            {
                return false;
            }
            foreach (string one in list.Split('|'))
            {
                if (string.Equals(one, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The list with the id put on, or taken off when it is on; the oldest goes past <see cref="Most"/>.</summary>
        public static string Toggled(string list, string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOf('|') >= 0)
            {
                return list ?? string.Empty;
            }
            var ids = new System.Collections.Generic.List<string>(string.IsNullOrEmpty(list) ? new string[0] : list.Split('|'));
            if (!ids.Remove(id))
            {
                ids.Add(id);
                while (ids.Count > Most)
                {
                    ids.RemoveAt(0);
                }
            }
            return string.Join("|", ids);
        }

        /// <summary>How many of the ids are on the list.</summary>
        public static int Count(string list) => string.IsNullOrEmpty(list) ? 0 : list.Split('|').Length;

        /// <summary>The stars now.</summary>
        public static string List
        {
            get
            {
                if (_list == null)
                {
                    try
                    {
                        _list = UnityEngine.PlayerPrefs.GetString(Pref, string.Empty);
                    }
                    catch (Exception)
                    {
                        _list = string.Empty;
                    }
                }
                return _list;
            }
        }

        public static bool Has(string id) => Has(List, id);

        /// <summary>A star pressed: on, or off; kept at once. Returns whether the id is starred now.</summary>
        public static bool Toggle(string id)
        {
            _list = Toggled(List, id);
            try
            {
                UnityEngine.PlayerPrefs.SetString(Pref, _list);
                UnityEngine.PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // The star holds for this session only; nothing else depends on it.
            }
            return Has(_list, id);
        }
    }
}
