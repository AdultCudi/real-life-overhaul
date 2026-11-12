namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Options in the city on the RLO interface: the host over UIs.options, each setting pressed through the game's
    /// own control so its handlers, other mods' patches on them and Going Public's locks all run as ever. Two switches: the
    /// window (options_city) and its custom difficulty panel (options_difficulty); with the second off, that panel is the
    /// game's own view until Options closes. The main menu's Options is never drawn here. Cost: nothing while Options is shut;
    /// while it shows, the host's input and a look at the game's controls four times a second.
    /// </summary>
    public static class OptionsKits
    {
        public const string CityId = "options_city", DifficultyId = "options_difficulty";

        private static readonly UiGameHost City = new UiGameHost(new OptionsGame());

        /// <summary>Once a second while the interface is on: the host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            if (UiScreenSwitches.On(CityId))
            {
                City.Install();
            }
            else
            {
                City.Remove("its switch is off");
            }
        }

        /// <summary>Options the game's own again, exactly as found.</summary>
        public static void Remove(string why) => City.Remove(why);

        /// <summary>A new city: the window looked for again.</summary>
        public static void Rearm() => City.Rearm();

        /// <summary>The screen's state for the interface's line at load.</summary>
        public static string States() => "the game's options (" + City.State + ")";

        /// <summary>True while the kit draws Options, for the tests.</summary>
        internal static bool Showing => City.Showing;

        /// <summary>The game's own view of Options until it closes: another mod's tab, or the custom difficulty with its switch off.</summary>
        internal static void Classic() => City.Classic();
    }
}
