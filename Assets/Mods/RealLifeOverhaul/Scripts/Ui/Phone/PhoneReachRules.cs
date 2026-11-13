namespace RealLifeOverhaul.Ui
{
    /// <summary>Why the game's phone cannot be folded or unfolded now (<see cref="PhoneReachRules.Why"/>).</summary>
    public enum PhoneWhy
    {
        None,
        NoCity,     // no city is loaded: the game's interface is not built
        NoPhone,    // the game's phone or its fold window is not there (another game version, or another mod took them away)
        Hidden,     // the game has its phone switched off: the full menu, the map, a mini game or a scene has it
        Refused     // the game's fold took no press: its animation is still running, or a listener of another mod turned it back
    }

    /// <summary>
    /// How a folded or put away phone comes back on the RLO interface, game-free. The game keeps the phone's fold in the
    /// save and has no key for it: a folded phone shows only its head at the foot of the screen, where the interface's
    /// bar stands, so its head can be neither seen nor pressed. The kit's own phone lifts its head over the bar
    /// (PhoneKit.Lift). The classic phone, which stays whenever another mod pages the phone's apps, another mod's screen is
    /// open on it, the phone's switch is off or the kit's phone met a fault, has nothing of the kind: for it the bar has a
    /// phone button, the Phone key folds and unfolds it, and the Player.log says why one cannot (<see cref="Words"/>).
    /// Cost: comparisons.
    /// </summary>
    public static class PhoneReachRules
    {
        /// <summary>
        /// The bar's phone button shows while the classic phone is the one in play and the game shows it: the kit's phone
        /// keeps its own head over the bar, and a phone the game has hidden has nothing to fold.
        /// </summary>
        public static bool Offer(bool gameShowsPhone, bool kitPhoneShows) => gameShowsPhone && !kitPhoneShows;

        /// <summary>
        /// The Phone key and the bar's button act on the classic phone only: the kit's phone answers its key itself
        /// (PhoneKit.Frame), and two answers to one key would fold it and unfold it in one frame.
        /// </summary>
        public static bool Mine(bool kitPhoneShows) => !kitPhoneShows;

        /// <summary>
        /// The hint that names the key and the button is said once a session, the first time the phone is seen folded while
        /// it shows (the game keeps the fold in the save, so a phone put away last session is folded at the load).
        /// </summary>
        public static bool Remind(bool toldBefore, bool phoneShows, bool folded) => !toldBefore && phoneShows && folded;

        /// <summary>Why the phone cannot be moved now, in the order the causes can be told apart; None when it can.</summary>
        public static PhoneWhy Why(bool city, bool phoneThere, bool windowThere, bool phoneShows)
        {
            if (!city)
            {
                return PhoneWhy.NoCity;
            }
            if (!phoneThere || !windowThere)
            {
                return PhoneWhy.NoPhone;
            }
            return phoneShows ? PhoneWhy.None : PhoneWhy.Hidden;
        }

        /// <summary>
        /// The line for Player.log when a press of the phone key or the bar's button did nothing: the cause, what the player
        /// is told, and where the phone comes back. <paramref name="by"/> names what has it hidden where that is known.
        /// </summary>
        public static string Words(PhoneWhy why, string by, string key)
        {
            switch (why)
            {
                case PhoneWhy.NoCity:
                    return "UI: the phone key (" + key + ") was pressed with no city loaded: the game's phone is not built yet.";
                case PhoneWhy.NoPhone:
                    return "UI: the phone key (" + key + ") was pressed but the game's phone or its fold window is not there (another game version, or another mod took them away).";
                case PhoneWhy.Hidden:
                    return "UI: the phone key (" + key + ") was pressed while the game has its phone switched off" + (string.IsNullOrEmpty(by) ? string.Empty : " (" + by + ")")
                           + ": it is back when that closes, folded or not as it was.";
                case PhoneWhy.Refused:
                    return "UI: the phone key (" + key + ") was pressed and the game's fold took no press: its animation is still running, or a listener of another mod turned it back.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Why the phone key does nothing while a mod panel holds the keyboard or a text field has it: said when the key
        /// went down and the keys' own check refused it, so a player who tried the key finds the cause in the log.
        /// </summary>
        public static string Held(bool capturing, bool suspended, bool typing, string key)
        {
            string cause = capturing ? "a key is being chosen on the options page" : suspended ? "a panel of the mod holds the keyboard" : typing ? "a text field has the keyboard" : null;
            return cause == null ? null : "UI: the phone key (" + key + ") went down but " + cause + ", so it was not read.";
        }
    }
}
