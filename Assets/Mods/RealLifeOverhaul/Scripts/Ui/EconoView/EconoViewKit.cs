using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's EconoView drawn on the RLO interface, through the host every game screen uses (UiGameHost): the game's app
    /// stays alive under the kit with only its two pages ("Overview" and "BusinessDetails") unseen and untouchable, given
    /// back exactly as found; the kit's screen (EconoViewScreen) reads the game's income statement, transactions, loans,
    /// funds, taxes and one business's statement after the game and other mods filled them, and presses the game's own
    /// buttons and methods, so every rule of the game stays the game's. Its switch in Mod settings ("ui_screen_econoview")
    /// gives the game's own app back alone. Cost: what the host costs: nothing while closed; while it shows, the input
    /// reads and one box test a frame.
    /// </summary>
    public static class EconoViewKit
    {
        public const string Id = "econoview";

        private static readonly UiGameHost Host = new UiGameHost(new EconoGameScreen());

        /// <summary>Where it stands, for the interface's line at load.</summary>
        public static string State => "EconoView (" + Host.State + ")";

        /// <summary>True while the kit draws EconoView.</summary>
        public static bool Showing => Host.Showing;

        /// <summary>The kit's screen while it is made, for the tests; null before.</summary>
        internal static EconoViewScreen Screen => Host.Screen as EconoViewScreen;

        /// <summary>Puts the kit over EconoView while its switch is on, and gives the game's own back while it is off; asked once a second.</summary>
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

        /// <summary>A new city: an app that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm() => Host.Rearm();

        /// <summary>The game's app as it was: the kit gone, its pages seen and touchable again.</summary>
        public static void Remove(string why) => Host.Remove(why);

        // The game's EconoView, under the full menu: its overview and its business page the two leaves the kit hides.
        private sealed class EconoGameScreen : IUiGameScreen
        {
            private const string Overview = "Overview";            // the app's own pages; anything else a mod added to the app stays seen
            private const string Business = "BusinessDetails";

            public string Id => EconoViewKit.Id;

            public string Name => "the EconoView app";

            public string Title => UiText.T("smartphone_econoview");

            public UiIcon Icon => UiIcon.ChartPie;

            public UiGameKind Kind => UiGameKind.App;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.fullMenu != null ? uis.fullMenu.econoView : null;   // the phone is built a little after the city
            }

            public string Check(Component found)
            {
                var app = (global::UI.Smartphone.Apps.EconoView.EconoView)found;
                if (found.transform.Find(Overview) == null || found.transform.Find(Business) == null)
                {
                    return "its pages are not there";
                }
                if (app.overview == null || app.overview.fullTransactionsPanel == null || app.businessDetails == null || app.businessDetails.incomeStatement == null)
                {
                    return "its overview, its transactions or its business page is not there";
                }
                return GameContract.EconoBanking.Field == null || GameContract.EconoBanking.Field.GetValue(found) == null ? "its loans, investments and taxes are not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal)
            {
                conceal.Add(found.transform.Find(Overview));
                conceal.Add(found.transform.Find(Business));
            }

            public IUiScreen Make(Component found) => new EconoViewScreen((global::UI.Smartphone.Apps.EconoView.EconoView)found);
        }
    }
}
