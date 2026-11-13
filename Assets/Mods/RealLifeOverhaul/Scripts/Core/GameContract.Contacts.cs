namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the Contacts app's conversations drawn on the RLO interface (Scripts/Ui/Contacts): the
    /// line the game's conversation shows now and its buttons, the offers' buttons in the thread, and the salary offer's
    /// fields, read after the game (and other mods) filled them and pressed as a click would. Each part and its members are
    /// in this file alone; a part file's fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiContactsTalk = ContractBook.Part("ui_contacts_talk", "ui", "the Contacts app's calls and offers drawn on the RLO interface");

        // The conversation the game runs on the app (a call, an offer's negotiation): the line it shows now, the entry it
        // copied for it (its words and its buttons in the game's order) and the form an entry asks for.
        public static readonly GameMember TalkEntry = Field(UiContactsTalk, Game, "DialogController", "_currentEntry", "DialogEntry");
        public static readonly GameMember TalkEntryView = Field(UiContactsTalk, Game, "DialogController", "_currentEntryComponent", "CanvasGroup");
        public static readonly GameMember TalkForm = Field(UiContactsTalk, Game, "DialogController", "_currentInputTemplate", "Transform");

        // The template the thread's text entries are copied from: its parent holds every entry, and each text the game shows has a delete button of its own.
        public static readonly GameMember TalkTexts = Optional(Field(ContactsKit, Game, "DialogController", "textTemplate", "CanvasGroup"));

        // The offers' buttons in the thread (an offer of work, a health plan, a reply the game asks for), each a copy the game
        // made and keeps until one of its group is pressed.
        public static readonly GameMember TalkOfferButtons = StaticField(UiContactsTalk, Game, "UI.Smartphone.Apps.Contacts.ContactsApp", "ContextButtons", "List<ContextButton>");

        // The salary offer's two fields and an offer's one: the figure's field and its steps.
        public static readonly GameMember TalkWageField = Field(UiContactsTalk, Game, "AI.Employees.SalaryNegotiation.SalaryOfferSettings", "hourlyWageOfferSettings", "PlayerOfferSettings");
        public static readonly GameMember TalkBonusField = Field(UiContactsTalk, Game, "AI.Employees.SalaryNegotiation.SalaryOfferSettings", "signingBonusOfferSettings", "PlayerOfferSettings");
        public static readonly GameMember TalkAmount = Field(UiContactsTalk, Game, "UI.Dialog.PlayerOfferSettings", "amountField", "InputField");
        public static readonly GameMember TalkLess = Field(UiContactsTalk, Game, "UI.Dialog.PlayerOfferSettings", "decreaseButton", "Button");
        public static readonly GameMember TalkMore = Field(UiContactsTalk, Game, "UI.Dialog.PlayerOfferSettings", "increaseButton", "Button");
    }
}
