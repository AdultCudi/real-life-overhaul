using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the host may hide and what is someone else's, game-free. Other mods put their own objects inside the game's
    /// shared parents (the full menu's canvas, its apps' container and top row, the phone's grids, BizMan's menu and
    /// containers, a template others clone), so the kit never puts a CanvasGroup on one of those: it hides the game's own
    /// leaves by name, and a graphic standing straight on a shared parent by its own renderer alone. A child whose name is
    /// not the game's or the mod's is another mod's, and a screen offers it as an entry that opens the game's own view.
    /// Checked offline (EconomyCheck, UiHostChecks). Cost: a few string comparisons, at a bind or a refresh only.
    /// </summary>
    public static class UiHostRules
    {
        /// <summary>
        /// The game's objects other mods fill or clone, by names no leaf of the game's carries: never hidden by a group of
        /// the kit's. The phone's grids and BizMan's menu and containers have names leaves share ("Container", "Menu"), so
        /// a screen over those passes them to <see cref="UiConceal"/> as shared by reference.
        /// </summary>
        public static readonly string[] SharedParents =
        {
            "Canvas", "Canvases", "AppsContainer", "Topbar", "AppButtons", "AppButtonTemplate", "ItemPanelUI"
        };

        /// <summary>The mod's own objects all start so (RealLife MoreApps, RealLife Folder, RealLife Interface ...), or are its apps' ids.</summary>
        public const string ModPrefix = "RealLife";

        private const string CloneSuffix = "(Clone)";   // Unity's name for a copy made by Instantiate

        /// <summary>True for a name the kit must never put a group on.</summary>
        public static bool Shared(string name)
        {
            return name != null && Array.IndexOf(SharedParents, name) >= 0;
        }

        /// <summary>True for an object the game or the mod made, by its name and the names the game gives that container's children.</summary>
        public static bool Own(string name, string[] gameNames)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            if (name.StartsWith(ModPrefix, StringComparison.Ordinal))
            {
                return true;
            }
            string plain = name.EndsWith(CloneSuffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - CloneSuffix.Length).TrimEnd() : name;
            for (int i = 0; gameNames != null && i < gameNames.Length; i++)
            {
                if (string.Equals(plain, gameNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True when the kit shows the moment it binds: the game's object already stands switched on and the screen is of the kind that
        /// stays on (the toast stack). The watcher on the object can only tell of a switching on after it, so for such an object nothing
        /// else would ever show the kit.
        /// </summary>
        public static bool ShowsAtBind(bool stands, bool objectOn) => stands && objectOn;

        /// <summary>The order a kit canvas takes over a screen drawn at this order: just above it, below the game's boxes, notices and other mods.</summary>
        public static int Above(int covered) => covered + 1;

        /// <summary>
        /// The HUD's order with the kit phone's canvas at <paramref name="phone"/> (int.MinValue: none): its own, or one over
        /// the phone's. Two canvases of one order are drawn in the order they were last switched on, and the map switches the
        /// phone off and on alone, so after it the phone stood over the bar; one order apart, the bar stays over the phone.
        /// </summary>
        public static int OverPhone(int own, int phone) => phone == int.MinValue || phone < own ? own : phone + 1;
    }
}
