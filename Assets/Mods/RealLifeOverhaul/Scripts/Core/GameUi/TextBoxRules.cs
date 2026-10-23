namespace RealLifeOverhaul.GameUi
{
    /// <summary>What the game's text box was asked for, and so what must still be there for it to stay up.</summary>
    public enum TextBoxFor
    {
        Nothing,   // asked with nothing of the mod's open: left to its own two buttons
        Owner,     // asked by a conversation that says whether it goes on (a call's composer, a street talk)
        Phone,     // asked from a phone page: kept while the phone is open
        Panel,     // asked over the game's dialogue panel: kept while the panel is open
        Options    // asked from the game's options page (the mod's settings): kept while it is open
    }

    /// <summary>What the watch found of the text box the mod asked for (<see cref="TextBoxRules.Fate"/>).</summary>
    public enum TextBoxFate
    {
        Keep,            // still asked for and still on screen
        Closed,          // its own buttons closed it, or the interface went: only forgotten
        OwnerEnded,      // the conversation it was for is over: hung up, the phone shut, Esc, left, or its time up
        PhoneClosed,     // the phone it was asked from was put away
        PanelClosed,     // the dialogue panel it was asked over is gone
        OptionsClosed,   // the options page it was asked from was closed
        Moved,           // the player is no longer where it was asked: a door, another building, the street
        Unloaded         // the city is unloading or a save is being loaded
    }

    /// <summary>Who holds the game's one handle on the open conversation (DialogController.current), shared by its dialogue panel and the phone's Contacts app.</summary>
    public enum TalkHolder
    {
        None,    // nobody: no conversation, or the phone let go of one
        Panel,   // the dialogue panel (UI.Dialog.DialogUI): a talk in person, a counter, a desk
        Phone    // the phone's Contacts app (ContactsApp): a call
    }

    /// <summary>
    /// When the game's text box and its conversation handle are put right after what they were for ended. The game closes
    /// its box only with its own buttons, and left up it takes every key as typing; and a call clears the shared handle
    /// (DialogController.current) the dialogue panel needs to end, leaving the player stuck. Game-free; checked offline
    /// (EconomyCheck TextBoxChecks).
    /// </summary>
    public static class TextBoxRules
    {
        public const float WatchEvery = 0.25f;   // seconds of real time between two looks of the watch: a box outlives what it was for by a quarter second at most

        /// <summary>What a box is kept for: its owner's test, else the phone (over the panel), else the panel, else the options page, else nothing.</summary>
        public static TextBoxFor For(bool ownerGiven, bool phoneOpen, bool panelOpen, bool optionsOpen)
        {
            if (ownerGiven)
            {
                return TextBoxFor.Owner;
            }
            if (phoneOpen)
            {
                return TextBoxFor.Phone;
            }
            if (panelOpen)
            {
                return TextBoxFor.Panel;
            }
            return optionsOpen ? TextBoxFor.Options : TextBoxFor.Nothing;
        }

        /// <summary>What becomes of the box; <paramref name="moved"/>: the player is not in the building, or the street, it was asked in.</summary>
        public static TextBoxFate Fate(TextBoxFor kept, bool shown, bool ownerLive, bool phoneOpen, bool panelOpen, bool optionsOpen, bool moved, bool unloading)
        {
            if (kept == TextBoxFor.Nothing || !shown)
            {
                return kept == TextBoxFor.Nothing ? TextBoxFate.Keep : TextBoxFate.Closed;
            }
            if (unloading)
            {
                return TextBoxFate.Unloaded;
            }
            switch (kept)
            {
                case TextBoxFor.Owner:
                    if (!ownerLive)
                    {
                        return TextBoxFate.OwnerEnded;
                    }
                    break;
                case TextBoxFor.Phone:
                    if (!phoneOpen)
                    {
                        return TextBoxFate.PhoneClosed;
                    }
                    break;
                case TextBoxFor.Panel:
                    if (!panelOpen)
                    {
                        return TextBoxFate.PanelClosed;
                    }
                    break;
                case TextBoxFor.Options:
                    if (!optionsOpen)
                    {
                        return TextBoxFate.OptionsClosed;
                    }
                    break;
            }
            return moved ? TextBoxFate.Moved : TextBoxFate.Keep;
        }

        /// <summary>The box is to be closed with the game's own close: still up and no longer wanted.</summary>
        public static bool Closes(TextBoxFate fate)
        {
            return fate != TextBoxFate.Keep && fate != TextBoxFate.Closed;
        }

        /// <summary>The dialogue panel is to have the game's handle back: it is open and nobody holds the handle.</summary>
        public static bool HandBack(bool panelOpen, TalkHolder holder)
        {
            return panelOpen && holder == TalkHolder.None;
        }

        /// <summary>The words for the log line of a fate.</summary>
        public static string Words(TextBoxFate fate)
        {
            switch (fate)
            {
                case TextBoxFate.OwnerEnded: return "the conversation it was for has ended";
                case TextBoxFate.PhoneClosed: return "the phone it was asked from was put away";
                case TextBoxFate.PanelClosed: return "the dialogue panel it was asked over has closed";
                case TextBoxFate.OptionsClosed: return "the options page it was asked from was closed";
                case TextBoxFate.Moved: return "the player is no longer where it was asked";
                case TextBoxFate.Unloaded: return "the city is unloading";
                case TextBoxFate.Closed: return "its own buttons closed it";
                default: return "it is still wanted";
            }
        }
    }
}
