using BAModAPI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The interface's self-check for other mods, written once a city at load beside the line of the screens it draws over: it
    /// looks at what other mods put on the phone and on the screens the kit is bound over (the screen's top level, the game's
    /// top bar), and says in Player.log which mod's pieces it found, where, and how each screen holds them. A piece found later
    /// (a tab on a business page, a window opened over a screen) is said once as it is met. Cost: one pass over the screens
    /// bound, at load; never a frame.
    /// </summary>
    public static class UiForeignCheck
    {
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
                UiForeign.Note("phone", UiPieceKind.App, it.Id, "the phone's grid, the app row and All apps (Other mods)",
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
