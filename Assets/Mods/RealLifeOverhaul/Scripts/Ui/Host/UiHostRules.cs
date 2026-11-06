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

        /// <summary>
        /// True when the kit shows the moment it binds because the screen's object was not there at the host's earlier looks: the game builds some
        /// screens on their first use, so the first look that finds one finds it already opened by the player, and the watcher on it can only tell
        /// of the openings after. Without this the first opening of such a screen is the game's own window and only the second is the kit's. An
        /// overlay over the world is excluded: it stands on while nothing is shown.
        /// </summary>
        public static bool ShowsLate(bool builtLate, bool objectOn, bool overlay) => builtLate && objectOn && !overlay;

        /// <summary>The order a kit canvas takes over a screen drawn at this order: just above it, below the game's boxes, notices and other mods.</summary>
        public static int Above(int covered) => covered + 1;

        /// <summary>The order a box the game opens from inside an app of the full menu takes: over the app and the frame one above it (All apps), whatever order its own canvas has.</summary>
        public static int OverMenu(int own, int app, bool inMenu) => inMenu ? Math.Max(own, app + 2) : own;

        /// <summary>
        /// The order the minimap's card takes while the kit's HUD is up: one over the HUD's own canvas, so it is drawn over the
        /// bottom bar and every piece on it, and still under every window. The windows that stand with the HUD up are the
        /// game's dialogs and boxes (its canvases of order 19 to 21) and the kit's boxes over them (one or two more); the full menu,
        /// the pause menu and an app hide the game's HUD, the card with it. The notice cards (one over the game's notifications,
        /// order 21) are over the card by their order and keep clear of it by their place.
        /// </summary>
        public static int MinimapOrder(int hud) => Above(hud);

        /// <summary>The order of the HUD's own modal piece (a picker, All apps while walking): one over the minimap's card, which would otherwise be drawn over it.</summary>
        public static int ModalOrder(int hud) => Above(MinimapOrder(hud));

        /// <summary>
        /// The HUD's order with the kit phone's canvas at <paramref name="phone"/> (int.MinValue: none): its own, or one over
        /// the phone's. Two canvases of one order are drawn in the order they were last switched on, and the map switches the
        /// phone off and on alone, so after it the phone stood over the bar; one order apart, the bar stays over the phone.
        /// </summary>
        public static int OverPhone(int own, int phone) => phone == int.MinValue || phone < own ? own : phone + 1;

        /// <summary>
        /// Why a change on a screen is not laid out now, or null when it is: switched off with what it stands on (hidden), put
        /// to sleep by its owner, or leaving (the full menu closing on the way to the map), so drawn for nobody. Its keys resting
        /// under a box or another mod's window is no reason: a held screen is in front and takes the mouse, so a press on it
        /// is laid out in the same press. Also the words the press line gives.
        /// </summary>
        public static string Gone(bool active, bool running, bool leaving) => !active ? "hidden" : !running ? "asleep" : leaving ? "leaving" : null;
    }
}
