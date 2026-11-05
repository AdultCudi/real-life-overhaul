using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The designs the player has starred in "Choose a building", kept on this computer with the game's settings (PlayerPrefs), as the tour's seen flag is: never in the save
    /// and never sent. The list's rules are BuildFavouriteRules. Cost: one string read when first asked, one write when a star is pressed.
    /// </summary>
    public static class BuildFavourites
    {
        private const string Pref = "reallife.buildmode.favourites";   // not a locale key: a dot, not an underscore

        private static string _list;

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

        public static bool Has(string id) => BuildFavouriteRules.Has(List, id);

        /// <summary>A star pressed: on, or off; kept at once. Returns whether the id is starred now.</summary>
        public static bool Toggle(string id)
        {
            _list = BuildFavouriteRules.Toggled(List, id);
            try
            {
                UnityEngine.PlayerPrefs.SetString(Pref, _list);
                UnityEngine.PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // The star holds for this session only; nothing else depends on it.
            }
            return BuildFavouriteRules.Has(_list, id);
        }
    }
}
