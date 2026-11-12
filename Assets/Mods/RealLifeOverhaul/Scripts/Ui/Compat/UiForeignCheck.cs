using System.Collections.Generic;
using BAModAPI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The interface's self-check for other mods, written once a city at load beside the line of the screens it draws over: it
    /// looks at what other mods put on the phone and on the screens the kit is bound over (the screen's top level, the game's
    /// top bar), and says in Player.log which mod's pieces it found, where, and how each screen holds them. A piece found later
    /// (a tab on a business page, a window opened over a screen) is said once as it is met. After it, one short line counts
    /// other mods' apps by where the player finds them (<see cref="Apps"/>), written again whenever that changes, so a
    /// player's log answers "where did that mod's app go" by itself. Cost: one pass over the screens bound, at load; a
    /// comparison of two numbers a second after; never a frame.
    /// </summary>
    public static class UiForeignCheck
    {
        private static readonly List<string> OnPhone = new List<string>(8);
        private static readonly List<string> OnHud = new List<string>(4);
        private static readonly List<string> InBizMan = new List<string>(4);
        private static int _appsSeen = -1, _piecesSeen = -1, _lineSeen;
        private static bool _lineSaid;

        /// <summary>A new city: its line is written afresh.</summary>
        public static void Rearm()
        {
            _appsSeen = _piecesSeen = -1;
            _lineSaid = false;
        }

        /// <summary>
        /// The line of other mods' apps: "Other mods' apps: 2 on the phone (...), 0 on the HUD, 1 in BizMan (...)." Written
        /// once the apps were read, and again only when what it says changed: an app a mod adds late, a tab met on a
        /// business's page. Asked once a second by the interface's slow tick; the names are gathered only when the apps'
        /// version or the count of pieces moved.
        /// </summary>
        public static void Apps(IModLogger logger)
        {
            if (UiShellApps.Version == -1 && UiShellApps.Source != null)
            {
                UiShellApps.Refresh();   // nobody has read the apps yet (the HUD's bar is off): read here once, so the line does not wait for the full menu
            }
            int apps = UiShellApps.Version, pieces = UiForeign.All.Count;
            if (apps == -1 || (apps == _appsSeen && pieces == _piecesSeen))
            {
                return;
            }
            _appsSeen = apps;
            _piecesSeen = pieces;
            UiForeignApps.Census(UiShellApps.Items, UiForeign.All, OnPhone, OnHud, InBizMan);
            int signature = UiForeignApps.Signature(OnPhone, OnHud, InBizMan);
            if (_lineSaid && signature == _lineSeen)
            {
                return;
            }
            _lineSaid = true;
            _lineSeen = signature;
            logger?.Info(UiForeignApps.Line(OnPhone, OnHud, InBizMan));
        }

        /// <summary>The check, once a city.</summary>
        public static void Run(IModLogger logger)
        {
            try
            {
                UiGameHost.Survey();
                HudTopGame.Survey();
                PhoneApps();
                Hooks();
                logger?.Info(UiForeign.Line());
                string watch = UiForeignWindows.Watching
                    ? "another mod's windows opened over a screen of the kit are lifted over it and take the keys first"
                    : "another mod's windows over a screen of the kit are not watched on this Unity";
                logger?.Info("UI: " + watch + "; " + UiBreaker.Count + " screen(s) are the game's own after another mod's fault; " + UiScreenSwitches.All.Length
                             + " screens have a switch in Mod options.");
            }
            catch (System.Exception e)
            {
                logger?.Error(e);
            }
        }

        // Another mod's apps on the phone, with how each is drawn: its own picture, or the kit's mark of its first letter.
        private static void PhoneApps()
        {
            PhoneItems items = PhoneItems.Current;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                PhoneItem it = items.Items[i];
                if (it.Kind != PhoneItemKind.Other)
                {
                    continue;
                }
                string where = it.From == PhoneItemFrom.Row ? "the full menu's own row, which the kit hides: a tile of the phone, the app row and All apps (Other mods)"
                    : it.From == PhoneItemFrom.Frame ? "the phone outside its grid, which the kit covers: a tile of the phone and All apps (Other mods)"
                    : "the phone's grid, the app row and All apps (Other mods)";
                UiForeign.Note("phone", UiPieceKind.App, it.Id, where,
                    it.Picture > 0 ? "drawn with its own picture; its panel opens in the game's own chrome"
                        : "drawn with a mark of its first letter, " + UiForeignMark.Letter(it.Picture) + "; its panel opens in the game's own chrome");
            }
        }

        // Apps other mods gave the kit through KitForOtherMods.
        private static void Hooks()
        {
            foreach (string line in KitForOtherMods.Lines())
            {
                UiForeign.Note("api", UiPieceKind.App, line, "KitForOtherMods", "registered through the kit's own interface");
            }
        }
    }
}
