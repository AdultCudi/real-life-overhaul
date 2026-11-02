namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the game's dialogue panel, its forms and the spoken lines drawn on the RLO interface
    /// (Scripts/Ui/Dialog): the panel the game shows and hides, the entry it shows now with the copy it made of it and the
    /// form that copy asks for, the copies of the lines before it, the name the conversation gives its speaker, the game's
    /// own drop-down, the amount steps of a delivery's line, a slider's figure, the spoken line's panel, picture, words and
    /// queue, and Uncle Fred's face for the interface's test of a spoken line. Each part and its members
    /// are in this file alone; a part file's fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiDialog = ContractBook.Part("ui_dialog", "ui", "the game's dialogue panel, its forms and the spoken lines drawn on the RLO interface");

        // The panel the dialogue stands in, switched on as a conversation starts and off as it ends (or the city map opens).
        public static readonly GameMember DialogPanel = Field(UiDialog, Game, "UI.Dialog.DialogUI", "panel", "GameObject");

        // The entry the game shows now, the copy of its template it made for it (its words and its buttons) and the form that
        // copy asks for; the template the text entries are copied from, whose parent holds every line said so far.
        public static readonly GameMember DialogEntryNow = Field(UiDialog, Game, "DialogController", "_currentEntry", "DialogEntry");
        public static readonly GameMember DialogEntryView = Field(UiDialog, Game, "DialogController", "_currentEntryComponent", "CanvasGroup");
        public static readonly GameMember DialogEntryForm = Field(UiDialog, Game, "DialogController", "_currentInputTemplate", "Transform");
        public static readonly GameMember DialogTextTemplate = Field(UiDialog, Game, "DialogController", "textTemplate", "CanvasGroup");

        // The name the conversation gives its speaker (the bank's Banker, a desk's clerk), where its lines name nobody.
        public static readonly GameMember DialogSpeaker = Optional(Field(UiDialog, Game, "Dialogs.Dialog", "npcNameKey", "String"));

        // The game's own drop-down in a form: its options as it holds them (keys it localises, or words), the words it shows
        // for the chosen one, and the group it holds the drop-down by while it takes no press.
        public static readonly GameMember DialogDropOptions = Field(UiDialog, Game, "UI.Elements.Dropdown", "_options", "List<String>");
        public static readonly GameMember DialogDropLocalised = Field(UiDialog, Game, "UI.Elements.Dropdown", "_localizeOptions", "Boolean");
        public static readonly GameMember DialogDropShown = Field(UiDialog, Game, "UI.Elements.Dropdown", "selectedOptionPlaceholderLabel", "TMP_Text");
        public static readonly GameMember DialogDropGroup = Field(UiDialog, Game, "UI.Elements.Dropdown", "canvasGroup", "CanvasGroup");

        // A delivery's line of goods: its figure's field, its steps and the button that takes the line away.
        public static readonly GameMember DialogAmountField = Field(UiDialog, Game, "UI.AmountSelector", "amountInput", "TMP_InputField");
        public static readonly GameMember DialogAmountLess = Field(UiDialog, Game, "UI.AmountSelector", "decreaseButton", "Button");
        public static readonly GameMember DialogAmountMore = Field(UiDialog, Game, "UI.AmountSelector", "increaseButton", "Button");
        public static readonly GameMember DialogAmountRemove = Field(UiDialog, Game, "UI.AmountSelector", "deleteRowButton", "Button");

        // The spoken line's panel (Uncle Fred, a rival): the panel the game slides in, its picture and words, the lines it holds
        // after this one, and the line's own run (its sound and the wait after it), over once the panel slides away.
        public static readonly GameMember MonologuePanel = Field(UiDialog, Game, "UI.Monologues.MonologueUI", "monologuePanel", "RectTransform");
        public static readonly GameMember MonologueWords = Field(UiDialog, Game, "UI.Monologues.MonologueUI", "monologueText", "TextLocalizationComponent");
        public static readonly GameMember MonologuePicture = Field(UiDialog, Game, "UI.Monologues.MonologueUI", "monologueImage", "Image");
        public static readonly GameMember MonologueQueue = Optional(Field(UiDialog, Game, "UI.Monologues.MonologueUI", "_monologueQueue", null));
        public static readonly GameMember MonologueSpeaking = Optional(Field(UiDialog, Game, "UI.Monologues.MonologueUI", "_showMonologueCoroutine", "Coroutine"));

        // Uncle Fred's face as the tutorial gives it to his lines, for the interface's test of a spoken line.
        public static readonly GameMember FredFace = Optional(Field(UiDialog, Game, "UI.Tutorial.TutorialUI", "uncleFredSprite", "Sprite"));
    }
}
