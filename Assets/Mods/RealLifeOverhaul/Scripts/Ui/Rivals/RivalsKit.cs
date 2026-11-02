using UI;
using UI.Smartphone.Apps.Rivals;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Rivals app drawn on the RLO interface, through the host every game screen uses (UiGameHost): the game's app
    /// stays alive under the kit with only its layout (the leaderboard, the chosen rival's panel and its two tables) unseen
    /// and untouchable, given back exactly as found; the kit's screen (RivalsScreen) reads what the game filled and presses
    /// the game's own buttons, so its events (a rival chosen, the app closed) and its map and contact links run as a click
    /// runs them. Its switch in Mod settings ("ui_screen_rivals") gives the game's own screen back alone. Cost: what the
    /// host costs: nothing while closed; while it shows, the input reads and a look at the app four times a second.
    /// </summary>
    public static class RivalsKit
    {
        public const string Id = "rivals";

        private static readonly UiGameHost Host = new UiGameHost(new RivalsGameScreen());

        /// <summary>Where it stands, for the interface's line at load.</summary>
        public static string State => "Rivals (" + Host.State + ")";

        /// <summary>Puts the kit over the app while its switch is on, and gives it back while it is off; asked once a second.</summary>
        public static void Install()
        {
            if (UiScreenSwitches.On(Id))
            {
                Host.Install();
            }
            else
            {
                Host.Remove("its switch is off");
            }
        }

        /// <summary>A new city: a screen that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm() => Host.Rearm();

        /// <summary>The game's app as it was: the kit gone, its layout seen and touchable again.</summary>
        public static void Remove(string why) => Host.Remove(why);

        // The game's Rivals under the full menu, found by its name in the apps: its layout the one leaf the kit hides.
        private sealed class RivalsGameScreen : IUiGameScreen
        {
            private const string Layout = "Layout 30-70 (1)";   // the app's own layout; anything a mod added to the app stays

            public string Id => RivalsKit.Id;

            public string Name => "the Rivals app";

            public string Title => UiText.T("smartphone_rivals");

            public UiIcon Icon => PhoneLooks.Glyph(RivalsViews.App);

            public UiGameKind Kind => UiGameKind.App;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                Transform apps = uis != null && uis.fullMenu != null ? uis.fullMenu.appsContainer : null;
                Transform app = apps != null ? apps.Find(RivalsViews.App) : null;
                return app != null ? app.GetComponent<RivalsApp>() : null;
            }

            public string Check(Component found)
            {
                if (found.transform.Find(Layout) == null)
                {
                    return "its layout is not there";
                }
                return GameContract.RivalsBoard.Field?.GetValue(found) == null || GameContract.RivalsChosen.Field?.GetValue(found) == null
                       || GameContract.RivalsBusinessTable.Field?.GetValue(found) == null || GameContract.RivalsBuildingTable.Field?.GetValue(found) == null
                    ? "its leaderboard, its chosen rival or its tables are not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform.Find(Layout));

            public IUiScreen Make(Component found) => new RivalsScreen((RivalsApp)found);
        }
    }
}
