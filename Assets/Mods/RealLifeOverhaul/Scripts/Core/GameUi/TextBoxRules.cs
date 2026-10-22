namespace RealLifeOverhaul.GameUi
{
    /// <summary>What the game's text box was asked for, and so what must still be there for it to stay up (<see cref="TextBoxRules.For"/>).</summary>
    public enum TextBoxFor
    {
        Nothing,   // asked with nothing of the mod's open (none is kept): left to its own two buttons, as before
        Owner,     // asked by a conversation that says itself whether it goes on (a call's composer, a talk in the street)
        Phone,     // asked from a page of the phone (an app's name, an amount, a note): kept while the phone is open
        Panel,     // asked over the game's dialogue panel: kept while the panel is open
        Options    // asked from the game's options page (the mod's own settings): kept while that page is open
    }

    /// <summary>What the watch found of the text box the mod asked for (<see cref="TextBoxRules.Fate"/>).</summary>
    public enum TextBoxFate
    {
        Keep,            // still asked for and still on screen
        Closed,          // its own buttons closed it (or the game's interface went): nothing to do but forget it
        OwnerEnded,      // the conversation it was asked for is over: hung up, the phone shut, Esc, left, or its own time up
        PhoneClosed,     // the phone it was asked from was put away
        PanelClosed,     // the dialogue panel it was asked over is gone
        OptionsClosed,   // the options page it was asked from was closed
        Moved,           // the player is no longer where it was asked: through a door, into another building, out to the street
        Unloaded         // the city is unloading or a save is being loaded
    }

    /// <summary>Who holds the game's one handle on the conversation open (DialogController.current), which its dialogue panel and the phone's Contacts app share.</summary>
    public enum TalkHolder
    {
        None,    // nobody: no conversation, or the phone let go of one
        Panel,   // the dialogue panel (UI.Dialog.DialogUI): a talk in person, a counter, a desk
        Phone    // the phone's Contacts app (ContactsApp): a call
    }

    /// <summary>
    /// When the game's own text box (UIs.textInputUi) and the game's handle on the open conversation are put right after
    /// what they were for has ended. A player, 0.47.0 (issue 22): after closing a call by shutting the phone or pressing
    /// Esc, rather than with its own button, nothing would work any more.
    ///
    /// THE TEXT BOX. The game's box is a window of its own, over the phone and the dialogue panel. The game closes it only
    /// with its own two buttons (TextInputUI.OnConfirm and OnClose; only its staff schedule also closes it on Esc), and
    /// nothing closes it when what asked for it ends: the phone put away (FullMenu hides the phone, the Contacts app's
    /// OnDisable cancels the call), a call hung up, a talk left, a door gone through, a save loaded. Left up, with its field
    /// selected, the game takes every key as typing (GameManager.ShouldBlockKeyboardShortcuts), and the mod counts the
    /// player as busy with a window (ThirdPersonCamera.IsUiOpen): a building's door by the interact key, the mod's panels
    /// and its activities all say to finish what is open first. So every box the mod asks for is kept for something
    /// (<see cref="For"/>) and the watch closes it, with the game's own close, as soon as that is over (<see cref="Fate"/>).
    ///
    /// THE HANDLE. DialogController.current is one static of the game's: the dialogue panel sets it as it opens and clears it
    /// as it stops, and so does a call on the phone (ContactsApp.StartCall; its CancelDialog and FinishDialog clear it
    /// whatever holds it). The mod's dialogues end themselves, and the guard ends one that fails, through that handle. A call
    /// made while the panel was open therefore leaves the panel without it when the call ends: its last button and Esc then
    /// find no conversation to end, the panel never stops, and the game's navigation blocker it set stays, so the player
    /// can neither walk nor use a door. The watch gives the panel its handle back once nobody holds it (<see cref="HandBack"/>).
    ///
    /// Plain comparisons: no Unity or game types, nothing allocated. Checked offline (EconomyCheck, TextBoxChecks).
    /// </summary>
    public static class TextBoxRules
    {
        public const float WatchEvery = 0.25f;   // seconds of real time between two looks of the watch: a box outlives what it was for by a quarter of a second at most

        /// <summary>
        /// What a box is kept for, from what was open when it was asked: its owner's own test if it gave one; else the phone,
        /// which is on top of the panel when both are open; else the panel; else the options page; else nothing.
        /// </summary>
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

        /// <summary>
        /// What becomes of the box the mod asked for. <paramref name="shown"/>: the game's box is still on screen.
        /// <paramref name="moved"/>: the building the player is in (or the street) is not the one it was asked in.
        /// </summary>
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

        /// <summary>True when the box is to be closed with the game's own close (it is still up and no longer wanted).</summary>
        public static bool Closes(TextBoxFate fate)
        {
            return fate != TextBoxFate.Keep && fate != TextBoxFate.Closed;
        }

        /// <summary>
        /// True when the dialogue panel is to have the game's handle back: it is open, and nobody holds the handle (a call on
        /// the phone took it and let it go). Never while a call holds it: the call is what the player is in then.
        /// </summary>
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
