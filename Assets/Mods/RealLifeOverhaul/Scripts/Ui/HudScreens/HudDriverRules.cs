namespace RealLifeOverhaul.Ui
{
    /// <summary>Where the player's private driver is, as the bar's driver button shows it.</summary>
    public enum DriverState
    {
        None,      // no contract: no button
        Idle,      // a contract and no car called
        Coming,    // a car called and on its way
        Waiting,   // the car is by the player
        Driving    // the player rides in it
    }

    /// <summary>What a press of the bar's driver button (or its key) does.</summary>
    public enum DriverPress
    {
        Card,      // the game's own driver button: the card of cars, or of the car called with its Dismiss, open or shut
        Call       // the only car of the garage called, as the card's own button for it calls it
    }

    /// <summary>
    /// The private driver on the interface's bar, game-free. The button shows while the player has a contract (the phone's own
    /// driver button shows then, and only while the phone is unfolded); a press does what the phone's button and the card's
    /// car button do together: the garage's only car is called at once, and anything else (several cars to choose from, no car
    /// yet, a car already called and its Dismiss) opens or shuts the card, so the game's own choices stay the game's.
    /// Cost: comparisons.
    /// </summary>
    public static class HudDriverRules
    {
        public const float Near = 20f;   // m: the car is "waiting" this close to the player

        /// <summary>The driver's state from what the game says.</summary>
        public static DriverState State(bool contract, bool called, bool riding, bool near)
        {
            if (!contract)
            {
                return DriverState.None;
            }
            return !called ? DriverState.Idle : riding ? DriverState.Driving : near ? DriverState.Waiting : DriverState.Coming;
        }

        /// <summary>What a press does: call the one car, only when nothing is called, the card is shut and the garage has exactly one; else the card.</summary>
        public static DriverPress Press(DriverState state, int cars, bool cardOpen) =>
            state == DriverState.Idle && cars == 1 && !cardOpen ? DriverPress.Call : DriverPress.Card;

        /// <summary>The glyph's colour for a state: none for a driver at rest, amber on its way, green waiting, the accent while riding.</summary>
        public static UiRole Tint(DriverState state)
        {
            switch (state)
            {
                case DriverState.Coming: return UiRole.WarnText;
                case DriverState.Waiting: return UiRole.OkText;
                case DriverState.Driving: return UiRole.AccentText;
                default: return UiRole.None;
            }
        }

        /// <summary>The key of the words on hover: the state, and what a press does in it.</summary>
        public static string NoteKey(DriverState state, int cars)
        {
            switch (state)
            {
                case DriverState.Coming: return "reallife_driver_tool_coming";
                case DriverState.Waiting: return "reallife_driver_tool_waiting";
                case DriverState.Driving: return "reallife_driver_tool_driving";
                default: return cars == 1 ? "reallife_driver_tool_call" : cars > 1 ? "reallife_driver_tool_choose" : "reallife_driver_tool_none";
            }
        }
    }
}
