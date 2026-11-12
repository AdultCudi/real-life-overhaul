namespace RealLifeOverhaul
{
    /// <summary>Where the saved options and this rule's own marks are kept, by id: the game's PlayerPrefs in the game, a table in the checks.</summary>
    internal interface IUiOffStore
    {
        bool Has(string id);

        int Get(string id);

        void Set(string id, int value);
    }

    /// <summary>
    /// The RLO interface switched off once for everyone (1.0.3). It shipped switched on, and in its first day players could not
    /// eat at a counter, could not pick items when ordering by phone, lost the vehicle's fuel and cargo and saw no Korean or
    /// Chinese text on it. So its default is off, and a switch saved as on is written off at one start, with a mark of its own so
    /// that it happens once: whoever switches it on afterwards keeps it on, at every later start and in every later version.
    /// Game-free, checked by EconomyCheck (UiOffChecks.cs). COST: nothing a frame; two or three keys read when the options are.
    /// </summary>
    internal static class UiOffRules
    {
        public const string Done = "ui_interface_reset";        // the mark's id: the number of the version that last switched the interface off for everyone
        public const int DoneAt = 103;                          // version 1.0.3: a mark below this, or none, means the step has not run on this computer
        public const string Owed = "ui_interface_reset_note";   // 1 from the step until its notice was shown: only where a saved "on" was written off
        public const int NoticeMost = 240;                      // characters: what the game's toast holds (Workshop/STYLE.md, NoticeCheck)

        /// <summary>
        /// Once per computer, before the switch is read: a saved "on" under <paramref name="option"/> is written off and its notice
        /// is owed; a saved "off", or nothing saved (the default is off now), is left as it is and owes nothing. The mark is written
        /// either way, so no later start comes here again. True if the switch was written off.
        /// </summary>
        public static bool SwitchOffOnce(IUiOffStore store, string option)
        {
            if (store.Has(Done) && store.Get(Done) >= DoneAt)
            {
                return false;
            }
            bool wasOn = store.Has(option) && store.Get(option) != 0;
            if (wasOn)
            {
                store.Set(option, 0);
                store.Set(Owed, 1);
            }
            store.Set(Done, DoneAt);
            return wasOn;
        }

        /// <summary>True from the step that wrote a saved "on" off until its notice was shown, across starts: a player may quit before any city loads.</summary>
        public static bool NoticeOwed(IUiOffStore store)
        {
            return store.Has(Owed) && store.Get(Owed) != 0;
        }

        /// <summary>The notice was shown: it is never owed again.</summary>
        public static void NoticeShown(IUiOffStore store)
        {
            store.Set(Owed, 0);
        }
    }
}
