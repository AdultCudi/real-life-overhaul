using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pause menu on the RLO interface (Options, the pause section): the game's MiniMenu through the host (UiGameHost)
    /// like any game screen, its own switch, its panel kept live and its buttons pressed as the player presses them, so
    /// saving, the save's name, the questions before leaving and Going Public's hold on them all stay the game's. Also
    /// keeps the time of the last save for the menu's line. Cost: nothing while the menu is shut; while it shows, its
    /// host's input and a look at the game's panel four times a second; a time read at each save.
    /// </summary>
    public static class PauseKits
    {
        public const string PauseId = "pause_menu";

        private static readonly UiGameHost Pause = new UiGameHost(new PauseGame());
        private static readonly Action OnSave = Saved;
        private static float _lastSave = -1f;   // unscaled seconds of the last save this city, or -1 for none yet

        /// <summary>The player's net worth as the Overview counts it, for the city's card; wired by Wiring/PauseLinks, null leaves it out.</summary>
        public static Func<long> NetWorth;

        /// <summary>How many businesses the player runs that take money, for the city's card; wired by Wiring/PauseLinks, null leaves it out.</summary>
        public static Func<int> Businesses;

        /// <summary>Once a second while the interface is on: the host put on while its switch is on, the game's own while off.</summary>
        public static void Install()
        {
            SaveHook.Keep(OnSave);
            if (UiScreenSwitches.On(PauseId))
            {
                Pause.Install();
            }
            else
            {
                Pause.Remove("its switch is off");
            }
        }

        /// <summary>The pause menu the game's own again, exactly as found.</summary>
        public static void Remove(string why) => Pause.Remove(why);

        /// <summary>A new city: the menu looked for again, the last save's time forgotten.</summary>
        public static void Rearm()
        {
            Pause.Rearm();
            _lastSave = -1f;
        }

        /// <summary>The screens' states for the interface's line at load.</summary>
        public static string States() => "the pause menu (" + Pause.State + ")";

        /// <summary>True while the kit draws the pause menu, for the tests.</summary>
        internal static bool PauseShowing => Pause.Showing;

        /// <summary>Seconds since the last save this city (the game's and its autosave), or -1 when none yet.</summary>
        internal static float SinceSave => _lastSave < 0f ? -1f : Time.unscaledTime - _lastSave;

        private static void Saved() => _lastSave = Time.unscaledTime;
    }
}
