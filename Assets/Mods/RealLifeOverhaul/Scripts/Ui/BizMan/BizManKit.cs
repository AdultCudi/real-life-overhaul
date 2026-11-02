using UI;
using UI.Smartphone.Apps.BizMan;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's BizMan drawn on the RLO interface, through the host every game screen uses (UiGameHost): the game's app
    /// stays alive under the kit with only its own layout unseen and untouchable (its list, and its business page's
    /// "Layout 30-70"), given back exactly as found; the kit's screen (BizManScreen) reads the game's list, tabs and pages
    /// after the game and other mods filled them, and presses the game's own buttons, so every rule of the game stays the
    /// game's. The tab bar and the pages' parent, which other mods fill, are never given a group. A page only the game's own
    /// view shows (the schedule, the managers, another mod's) opens the classic view until the app closes. Its switch in Mod
    /// settings ("ui_screen_bizman") gives the game's own app back alone. Cost: what the host costs: nothing while the app
    /// is closed; while it shows, the input reads and one box test a frame.
    /// </summary>
    public static class BizManKit
    {
        private static readonly UiGameHost Host = new UiGameHost(new BizManGameScreen());

        /// <summary>Where the kit stands, for the interface's line at load: drawn, waiting for the app, or the game's own and why.</summary>
        public static string State => Host.State;

        /// <summary>True while the kit draws the app.</summary>
        public static bool Showing => Host.Showing;

        /// <summary>Puts the kit over the game's BizMan once per city; asked again once a second while its switches are on.</summary>
        public static void Install() => Host.Install();

        /// <summary>A new city: an app that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm() => Host.Rearm();

        /// <summary>The game's app as it was: the kit gone, the layout seen and touchable again.</summary>
        public static void Remove(string why) => Host.Remove(why);

        /// <summary>The game's own view until the app closes: a page only it shows, another mod's part of it.</summary>
        public static void Classic() => Host.Classic();

        // The game's BizMan as the host needs it: under the full menu, its list and its business page's layout the leaves the kit hides.
        private sealed class BizManGameScreen : IUiGameScreen
        {
            public const string Layout = "Layout 30-70";   // the business page's own layout; anything else a mod put on the page stays

            public string Id => "bizman";

            public string Name => "the BizMan app";

            public string Title => UiText.T("smartphone_bizman");

            public UiIcon Icon => UiIcon.Building;

            public UiGameKind Kind => UiGameKind.App;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.fullMenu != null ? uis.fullMenu.bizMan : null;   // the phone is built a little after the city
            }

            public string Check(Component found)
            {
                var app = (BizMan)found;
                if (app.list == null || app.business == null)
                {
                    return "its list or its business page is not there";
                }
                if (app.business.transform.Find(Layout) == null)
                {
                    return "its business page's layout is not there";
                }
                if (GameContract.BizMenu.Field == null || GameContract.BizContainers.Field == null || GameContract.BizTab.Field == null || GameContract.BizShops.Field == null)
                {
                    return "its tabs or its list are not as this version has them";
                }
                return null;
            }

            public void Conceal(Component found, UiConceal conceal)
            {
                var app = (BizMan)found;
                // The tab bar and the pages' parent are filled by other mods: never grouped, even by a slip of a name.
                conceal.Shared(GameContract.BizMenu.Field.GetValue(app.business) as Transform);
                conceal.Shared(GameContract.BizContainers.Field.GetValue(app.business) as Transform);
                conceal.Add(app.list.transform);
                conceal.Add(app.business.transform.Find(Layout));
                conceal.Add(app.GetComponent<Graphic>());
                conceal.Add(app.business.GetComponent<Graphic>());
            }

            public IUiScreen Make(Component found) => new BizManScreen((BizMan)found);
        }
    }
}
