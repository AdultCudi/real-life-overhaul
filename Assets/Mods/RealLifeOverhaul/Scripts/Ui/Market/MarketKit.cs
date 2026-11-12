using UI;
using UnityEngine;
using GameMarketInsider = UI.Smartphone.Apps.MarketInsider.MarketInsider;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Market Insider drawn on the RLO interface, through the host every game screen uses (UiGameHost): the game's
    /// app stays alive under the kit with only its own pieces (the districts' tabs and their marker, its layout of three
    /// rows) unseen and untouchable, given back exactly as found; its own rect and button, which other mods copy and size
    /// their pages from, are never touched. The kit's screen (MarketScreen) reads what the game filled and presses the
    /// game's own tabs, buttons and toggles. Its switch in Mod settings ("ui_screen_market_insider") gives the game's own
    /// screen back alone. Cost: what the host costs: nothing while closed; while it shows, the input reads and a look at the
    /// app four times a second.
    /// </summary>
    public static class MarketKit
    {
        public const string Id = "market_insider";

        private static readonly UiGameHost Host = new UiGameHost(new MarketGameScreen());

        /// <summary>Where it stands, for the interface's line at load.</summary>
        public static string State => "Market Insider (" + Host.State + ")";

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

        /// <summary>The game's own Market Insider view until the app closes: a press the game has no button for.</summary>
        public static void Classic() => Host.Classic();

        /// <summary>The game's app as it was: the kit gone, its pieces seen and touchable again.</summary>
        public static void Remove(string why) => Host.Remove(why);

        // The game's Market Insider under the full menu, found by its name in the apps.
        private sealed class MarketGameScreen : IUiGameScreen
        {
            private static readonly string[] Pieces = { "SplitterWithIndicator", "Tabs", "Layout 25-25-50" };   // the app's own; anything a mod added stays

            public string Id => MarketKit.Id;

            public string Name => "the Market Insider app";

            public string Title => UiText.T("reallife_ui_market_app");   // the game has only its joined name for the app

            public UiIcon Icon => PhoneLooks.Glyph(MarketViews.App);

            public UiGameKind Kind => UiGameKind.App;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                Transform apps = uis != null && uis.fullMenu != null ? uis.fullMenu.appsContainer : null;
                Transform app = apps != null ? apps.Find(MarketViews.App) : null;
                return app != null ? app.GetComponent<GameMarketInsider>() : null;
            }

            public string Check(Component found)
            {
                var app = (GameMarketInsider)found;
                for (int i = 0; i < Pieces.Length; i++)
                {
                    if (found.transform.Find(Pieces[i]) == null)
                    {
                        return "its " + Pieces[i] + " is not there";
                    }
                }
                bool mapButton = app.neighborhoodData != null && GameContract.MarketViewOnMap.Field?.GetValue(app.neighborhoodData) is UnityEngine.UI.Button;
                string wrong = UiProbe.Need("Market Insider", "viewOnMapButton of the district data", mapButton)
                               ?? UiProbe.Path("Market Insider", found.transform, "Layout 25-25-50/Row 3/RealEstateFilterPanel")   // the search field (typing, no press)
                               ?? UiProbe.Path("Market Insider", found.transform, "Layout 25-25-50/Row 3/MarketDemandFilterPanel");
                if (wrong != null)
                {
                    return wrong;
                }
                return app.tabTemplate == null || app.neighborhoodData == null || app.marketDemandScrollerController == null || app.realEstateScrollerController == null
                       || app.marketEventScrollerController == null
                    ? "its districts, its figures, its tables or its events are not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal)
            {
                for (int i = 0; i < Pieces.Length; i++)
                {
                    conceal.Add(found.transform.Find(Pieces[i]));
                }
            }

            public IUiScreen Make(Component found) => new MarketScreen((GameMarketInsider)found);
        }
    }
}
