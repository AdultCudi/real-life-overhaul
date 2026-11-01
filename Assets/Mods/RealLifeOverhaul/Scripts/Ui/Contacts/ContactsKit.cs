using UI;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Contacts app drawn on the RLO interface, through the host every game screen uses (UiGameHost): the
    /// game's app stays alive under the kit with only its layout ("Layout 30-70") unseen and untouchable, given back
    /// exactly as found; the kit's screen (ContactsScreen) reads the game's contacts and messages and presses the game's
    /// own buttons, so every rule of the game stays the game's. A call runs in the game's own view, the kit coming back the
    /// moment the game frees its call button; a message only the game can answer opens the classic view until the app
    /// closes. Its switch in Mod settings ("ui_screen_contacts") gives the game's own app back alone. Cost: what the host
    /// costs: nothing while the app is closed; while it shows, the input reads and one box test a frame.
    /// </summary>
    public static class ContactsKit
    {
        private static readonly UiGameHost Host = new UiGameHost(new ContactsGameScreen());

        /// <summary>Where the kit stands, for the interface's line at load: drawn, waiting for the app, or the game's own and why.</summary>
        public static string State => Host.State;

        /// <summary>True once the kit has an answer for this city: over the app, waiting to look again, or the game's own.</summary>
        public static bool Settled => Host.Settled;

        /// <summary>True while the kit draws the app.</summary>
        public static bool Showing => Host.Showing;

        /// <summary>Puts the kit over the game's Contacts app once per city; asked again once a second while its switches are on.</summary>
        public static void Install() => Host.Install();

        /// <summary>A new city: an app that never looked as expected in the last one is looked at again.</summary>
        public static void Rearm() => Host.Rearm();

        /// <summary>The game's app as it was: the kit gone, the layout seen and touchable again.</summary>
        public static void Remove(string why) => Host.Remove(why);

        /// <summary>The game's own view until the app closes: an offer with its buttons, a table only it can show.</summary>
        public static void Classic() => Host.Classic();

        /// <summary>A call the game runs in its own view: the kit steps aside and comes back when the game frees its call button.</summary>
        public static void Call(Button call) => Host.StepAside(() => call == null || call.interactable);

        // The game's Contacts app as the host needs it: under the full menu, its layout the one leaf the kit hides.
        private sealed class ContactsGameScreen : IUiGameScreen
        {
            private const string Layout = "Layout 30-70";   // the app's own layout, the leaf the kit hides; anything else a mod added to the app stays

            public string Id => "contacts";

            public string Name => "the Contacts app";

            public string Title => UiText.T("reallife_ui_contacts_app");

            public UiIcon Icon => UiIcon.AddressBook;

            public UiGameKind Kind => UiGameKind.App;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.fullMenu != null ? uis.fullMenu.contactsApp : null;   // the phone is built a little after the city
            }

            public string Check(Component found) => found.transform.Find(Layout) == null ? "its layout is not there" : null;

            public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform.Find(Layout));

            public IUiScreen Make(Component found) => new ContactsScreen((ContactsApp)found);
        }
    }
}
