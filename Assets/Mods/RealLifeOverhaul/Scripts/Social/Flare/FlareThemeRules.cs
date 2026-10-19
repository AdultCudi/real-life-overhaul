namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's light and dark looks, game-free (EconomyCheck's FlareThemeChecks.cs): the look a setting shows
    /// at an hour of the game's clock, and the setting one press of the switch on Flare's top bar gives. The
    /// option on the options page and the switch are one setting, Settings.FlareTheme: 0 follows the phone,
    /// 1 light, 2 dark. Cost: a comparison or two, on a refresh of the app or a press.
    /// </summary>
    internal static class FlareThemeRules
    {
        public const int FollowsPhone = 0, Light = 1, Dark = 2;   // the option's choices, in its drop-down's order
        public const int DarkFrom = 19, DarkUntil = 7;   // game hours: following the phone, dark 19:00 to 07:00

        /// <summary>The options page's words for each setting, in its drop-down's order, and the switch's.</summary>
        public static readonly string[] ChoiceKeys =
        {
            "reallife_flare_theme_0", "reallife_flare_theme_1", "reallife_flare_theme_2"
        };

        /// <summary>
        /// Whether a setting shows the dark look at an hour (0 to 23). A setting with no choice of its own follows the
        /// phone, as the drop-down shows it.
        /// </summary>
        public static bool IsDark(int setting, int hour)
        {
            switch (setting)
            {
                case Light: return false;
                case Dark: return true;
                default: return hour >= DarkFrom || hour < DarkUntil;
            }
        }

        /// <summary>
        /// The setting one press of the switch gives: a look of its own, never following the phone, and the other
        /// look from the one showing. What shows decides for every setting: following the phone it is the clock's,
        /// and a look the app has still to paint again (the clock turned while it was open) is never pressed into
        /// staying as it is.
        /// </summary>
        public static int Next(int setting, bool showingDark)
        {
            return showingDark ? Light : Dark;
        }

        /// <summary>
        /// The key of a setting's words on the options page; a setting with no choice of its own has Follows the
        /// phone's.
        /// </summary>
        public static string ChoiceKey(int setting)
        {
            return setting >= 0 && setting < ChoiceKeys.Length ? ChoiceKeys[setting] : ChoiceKeys[FollowsPhone];
        }
    }
}
