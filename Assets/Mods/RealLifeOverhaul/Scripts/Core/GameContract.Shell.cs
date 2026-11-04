namespace RealLifeOverhaul
{
    /// <summary>
    /// The game contract's lines for the RLO interface's frame, boxes and notices (Scripts/Ui/Shell): each part and its
    /// members in this file alone, so another screen's job adds its own file (GameContract.Area.cs) and never edits one
    /// line of another's. A part file's fields are made in their own order and lean on nothing in another part file.
    /// </summary>
    public static partial class GameContract
    {
        public static readonly GamePart UiQuestions = ContractBook.Part("ui_questions", "ui", "the game's question box drawn on the RLO interface");
        public static readonly GamePart UiAcknowledge = ContractBook.Part("ui_acknowledge", "ui", "the game's acknowledgement box drawn on the RLO interface");
        public static readonly GamePart UiTextBox = ContractBook.Part("ui_text_box", "ui", "the game's text box drawn on the RLO interface");
        public static readonly GamePart UiNoticeList = ContractBook.Part("ui_notice_list", "ui", "the game's list of notices drawn on the RLO interface");

        // The question box (HudConfirmUi, one for the full menu and one for the HUD): read after the game and Going Public have filled it, its buttons pressed.
        public static readonly GameMember AskContainer = Field(UiQuestions, Game, "HudConfirmUi", "container", "RectTransform");
        public static readonly GameMember AskHeader = Field(UiQuestions, Game, "HudConfirmUi", "headerLabel", "TextLocalizationComponent");
        public static readonly GameMember AskBody = Field(UiQuestions, Game, "HudConfirmUi", "bodyLabel", "TextLocalizationComponent");
        public static readonly GameMember AskConfirm = Field(UiQuestions, Game, "HudConfirmUi", "confirmButton", "Button");
        public static readonly GameMember AskCancel = Field(UiQuestions, Game, "HudConfirmUi", "cancelButton", "Button");
        public static readonly GameMember AskConfirmLabel = Field(UiQuestions, Game, "HudConfirmUi", "confirmLabel", "TextLocalizationComponent");
        public static readonly GameMember AskCancelLabel = Field(UiQuestions, Game, "HudConfirmUi", "cancelLabel", "TextLocalizationComponent");
        public static readonly GameMember AskNegative = Field(UiQuestions, Game, "HudConfirmUi", "negativeConfirmButtonSprite", "Sprite");
        public static readonly GameMember AskInMenu = Field(UiQuestions, Game, "HudConfirmUi", "showInFullMenu", "Boolean");

        // The acknowledgement (AcknowledgeWarningUi): its words, its "don't show again" toggle; its buttons are found by the methods they call.
        public static readonly GameMember AckContainer = Field(UiAcknowledge, Game, "AcknowledgeWarningUi", "container", "RectTransform");
        public static readonly GameMember AckHeader = Field(UiAcknowledge, Game, "AcknowledgeWarningUi", "headerLabel", "TextLocalizationComponent");
        public static readonly GameMember AckBody = Field(UiAcknowledge, Game, "AcknowledgeWarningUi", "bodyLabel", "TextLocalizationComponent");
        public static readonly GameMember AckConfirmLabel = Field(UiAcknowledge, Game, "AcknowledgeWarningUi", "confirmLabel", "TextLocalizationComponent");
        public static readonly GameMember AckToggle = Field(UiAcknowledge, Game, "AcknowledgeWarningUi", "acknowledgeToggle", "Toggle");

        // The text box (TextInputUI, UIs.textInputUi): its field is read and set through itself, its confirm pressed.
        public static readonly GameMember TypeField = Field(UiTextBox, Game, "TextInputUI", "inputField", "TMP_InputField");
        public static readonly GameMember TypePlaceholder = Field(UiTextBox, Game, "TextInputUI", "placeholder", "TextLocalizationComponent");
        public static readonly GameMember TypeButtonText = Field(UiTextBox, Game, "TextInputUI", "buttonText", "TextLocalizationComponent");
        public static readonly GameMember TypeConfirm = Field(UiTextBox, Game, "TextInputUI", "confirmButton", "Button");

        // The list of notices (NotificationsListUI): shown as its panel is, Clear all pressed through its own button.
        public static readonly GameMember NoticePanel = Field(UiNoticeList, Game, "UI.Notification.NotificationsListUI", "panel", "GameObject");
        public static readonly GameMember NoticeClear = Field(UiNoticeList, Game, "UI.Notification.NotificationsListUI", "removeAllNotificationsButton", "Button");
    }
}
