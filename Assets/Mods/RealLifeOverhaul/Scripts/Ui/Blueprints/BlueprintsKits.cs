using UI.MiniMenu;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pause menu's blueprints window on the RLO interface (Options, the pause section): the game's BlueprintsPanel through the
    /// host (UiGameHost) like any game screen, with its own switch. The game's panel stays alive and hidden under it, so its
    /// lists, its Steam fetches, its buttons and its questions are the game's own. Only the city's copy is drawn; the main menu's
    /// stays the game's. Cost: nothing while the panel is shut; while it shows, a look at the game's panel four times a second.
    /// </summary>
    public static class BlueprintsKits
    {
        public const string BlueprintsId = "blueprints";

        private static readonly UiGameHost Blueprints = new UiGameHost(new BlueprintsGame());

        /// <summary>Once a second while the interface is on: the host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            if (UiScreenSwitches.On(BlueprintsId))
            {
                Blueprints.Install();
            }
            else
            {
                Blueprints.Remove("its switch is off");
            }
        }

        /// <summary>The blueprints window the game's own again, exactly as found.</summary>
        public static void Remove(string why) => Blueprints.Remove(why);

        /// <summary>A new city: the panel looked for again.</summary>
        public static void Rearm() => Blueprints.Rearm();

        /// <summary>The screen's state for the interface's line at load.</summary>
        public static string States() => "blueprints (" + Blueprints.State + ")";

        /// <summary>True while the kit draws the blueprints window, for the tests.</summary>
        internal static bool Showing => Blueprints.Showing;

        /// <summary>The host, for the tests.</summary>
        internal static UiGameHost Host => Blueprints;
    }

    /// <summary>
    /// The game's blueprints panel (MiniMenu.blueprintsPanel) as the host binds it: the pause menu's Blueprints row opens it, the
    /// game's own Cancel and close shut it. A box, keys its own while it stands; the panel's own pieces are hidden under the kit.
    /// </summary>
    internal sealed class BlueprintsGame : IUiGameScreen
    {
        public string Id => BlueprintsKits.BlueprintsId;

        public string Name => "the blueprints window";

        public string Title => UiText.T("common_blueprints");

        public UiIcon Icon => UiIcon.Layout;

        public UiGameKind Kind => UiGameKind.Box;

        public Component Find()
        {
            MiniMenu menu = PauseGame.Menu();
            return menu != null ? menu.blueprintsPanel : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiBlueprints.Off)
            {
                return GameContract.UiBlueprints.Why;
            }
            return PauseGame.Menu() == null ? "the pause menu is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform panel = found.transform;
            for (int i = 0; i < panel.childCount; i++)
            {
                conceal.Add(panel.GetChild(i));
            }
            conceal.Add(panel.GetComponent<UnityEngine.UI.Graphic>());
        }

        public IUiScreen Make(Component found) => new BlueprintsScreen((BlueprintsUI.BlueprintsPanel)found, PauseGame.Menu());
    }
}
