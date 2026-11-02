using System;
using Entities;
using UI;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Contacts app drawn on the RLO interface, through the host every game screen uses (UiGameHost): the
    /// game's app stays alive under the kit with only its layout ("Layout 30-70") unseen and untouchable, given back
    /// exactly as found; the kit's screen (ContactsScreen) reads the game's contacts and messages and presses the game's
    /// own buttons, so every rule of the game stays the game's. A call is drawn as the call card, an offer's negotiation in
    /// the thread with its form; a call's form the kit does not draw yet is answered in the game's own view, the kit back
    /// the moment that line is answered, and another mod's buttons in the header open the classic view until the app
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

        /// <summary>The game's own view until the app closes: another mod's buttons in the header, which only the game's view shows.</summary>
        public static void Classic() => Host.Classic();

        /// <summary>
        /// The tax table's test: a notice drawn in this contact's thread from the tax figures of the period so far, kept in
        /// memory only (never in the contact's messages or the save), until another contact is opened.
        /// </summary>
        public static TextMessage Sample;
        public static Contact SampleFor;

        /// <summary>A line the game must answer in its own view (a call's form the kit does not draw yet): the kit steps aside until <paramref name="over"/>.</summary>
        public static void StepAside(Func<bool> over) => Host.StepAside(over);

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
