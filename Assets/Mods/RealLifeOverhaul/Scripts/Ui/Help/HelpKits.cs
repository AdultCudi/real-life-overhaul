using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's help window on the RLO interface (Options, the help section): HelpSystem's container through the host (UiGameHost)
    /// like any game screen, with its own switch. Whatever opens the help (F1, the HUD's info glyph, a business's requirement link, the
    /// mod's own links to a kind of shop) calls the game's own Toggle, and the kit shows the moment the game switches its container on.
    /// The game's pages, its list, its search, its history and the pages the mod writes into it stay the game's. The video stays the
    /// game's: a page that holds one opens the game's own view. Cost: nothing while the help is shut; while it shows, a look at the
    /// game's list four times a second.
    /// </summary>
    public static class HelpKits
    {
        public const string HelpId = "game_help";

        private static readonly UiGameHost Help = new UiGameHost(new HelpGame());

        /// <summary>Once a second while the interface is on: the host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            if (UiScreenSwitches.On(HelpId))
            {
                Help.Install();
            }
            else
            {
                Help.Remove("its switch is off");
            }
        }

        /// <summary>The help the game's own again, exactly as found.</summary>
        public static void Remove(string why) => Help.Remove(why);

        /// <summary>A new city: the help looked for again.</summary>
        public static void Rearm() => Help.Rearm();

        /// <summary>The screen's state for the interface's line at load.</summary>
        public static string States() => "the help (" + Help.State + ")";

        /// <summary>True while the kit draws the help, for the tests.</summary>
        internal static bool Showing => Help.Showing;

        /// <summary>The host, for the tests.</summary>
        internal static UiGameHost Host => Help;

        /// <summary>The game's own view of the help until it closes (a page with a video).</summary>
        internal static void Classic() => Help.Classic();
    }

    /// <summary>
    /// The game's help (HelpSystem.container) as the host binds it: the window the game switches on when something opens the help and off
    /// when it closes. A box over the world, keys its own while it stands; the window's own pieces and the dimmer behind it are hidden
    /// under the kit, which holds the clicks off the city as the dimmer did.
    /// </summary>
    internal sealed class HelpGame : IUiGameScreen
    {
        public string Id => HelpKits.HelpId;

        public string Name => "the help window";

        public string Title => UiText.T("reallife_ui_help_title");

        public UiIcon Icon => UiIcon.Info;

        public UiGameKind Kind => UiGameKind.Box;

        public Component Find()
        {
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            return help != null && help.container != null ? help.container.transform : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiHelp.Off)
            {
                return GameContract.UiHelp.Why;
            }
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            return help == null || help.searchField == null ? "its search field is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform window = found.transform;
            for (int i = 0; i < window.childCount; i++)
            {
                conceal.Add(window.GetChild(i));
            }
            conceal.Add(window.GetComponent<UnityEngine.UI.Graphic>());
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            if (help != null && help.dimmer != null)
            {
                conceal.Add(help.dimmer.GetComponent<UnityEngine.UI.Graphic>());   // the dim behind the window: the kit holds the world's clicks itself
            }
        }

        public IUiScreen Make(Component found) => new HelpScreen(InstanceBehavior<HelpSystem>.Instance);
    }
}
