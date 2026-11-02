using UI;
using UI.Smartphone.Apps.Persona;
using UnityEngine;
using CharacterInfo = UI.Smartphone.Apps.Persona.CharacterInfo;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Persona app drawn on the RLO interface, through the host every game screen uses (UiGameHost): the game's
    /// app stays alive under the kit with only its own three blocks (the character's facts, the stats with the education
    /// box, the personal goals) unseen and untouchable, given back exactly as found; the kit's screen (PersonaScreen) reads
    /// what the game (and the mod's courses and moods) put there and presses the game's own sort buttons. Its switch in Mod
    /// settings ("ui_screen_persona") gives the game's own screen back alone. Cost: what the host costs: nothing while
    /// closed; while it shows, the input reads and a look at the app four times a second.
    /// </summary>
    public static class PersonaKit
    {
        public const string Id = "persona";

        private static readonly UiGameHost Host = new UiGameHost(new PersonaGameScreen());

        /// <summary>Where it stands, for the interface's line at load.</summary>
        public static string State => "Persona (" + Host.State + ")";

        /// <summary>True while the kit draws Persona.</summary>
        public static bool Showing => Host.Showing;

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

        /// <summary>The game's app as it was: the kit gone, its blocks seen and touchable again.</summary>
        public static void Remove(string why) => Host.Remove(why);

        // The game's Persona under the full menu (no field of the full menu holds it: it is found by its name in the apps).
        private sealed class PersonaGameScreen : IUiGameScreen
        {
            private static readonly string[] Blocks = { "CharacterInfo", "CharacterStats", "PersonalGoals" };   // the app's own; anything a mod added stays

            public string Id => PersonaKit.Id;

            public string Name => "the Persona app";

            public string Title => UiText.T("smartphone_persona");

            public UiIcon Icon => PhoneLooks.Glyph(PersonaViews.App);

            public UiGameKind Kind => UiGameKind.App;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                Transform apps = uis != null && uis.fullMenu != null ? uis.fullMenu.appsContainer : null;
                return apps != null ? apps.Find(PersonaViews.App) : null;   // the phone is built a little after the city
            }

            public string Check(Component found)
            {
                Transform root = found.transform;
                for (int i = 0; i < Blocks.Length; i++)
                {
                    if (root.Find(Blocks[i]) == null)
                    {
                        return "its " + Blocks[i] + " is not there";
                    }
                }
                return root.GetComponentInChildren<CharacterInfo>(true) == null || root.GetComponentInChildren<CharacterStats>(true) == null
                       || root.GetComponentInChildren<EducationInfo>(true) == null || root.GetComponentInChildren<PersonalGoalsUI>(true) == null
                    ? "its facts, stats, education or goals are not there" : null;
            }

            public void Conceal(Component found, UiConceal conceal)
            {
                for (int i = 0; i < Blocks.Length; i++)
                {
                    conceal.Add(found.transform.Find(Blocks[i]));
                }
            }

            public IUiScreen Make(Component found) => new PersonaScreen(found.transform);
        }
    }
}
