namespace RealLifeOverhaul.Ui
{
    /// <summary>A screen that takes typed words on one of its targets (a search): the host opens a text field for it.</summary>
    public interface IUiTyping
    {
        /// <summary>The command whose target, pressed, starts typing.</summary>
        int TypingCommand { get; }

        /// <summary>The words already there when typing starts.</summary>
        string TypingText { get; }

        /// <summary>The words as they are typed.</summary>
        void Typed(string text);
    }

    /// <summary>A typing screen that sets its own limit and hears Enter (a text box of the game's, whose Enter confirms).</summary>
    public interface IUiTypingSubmit : IUiTyping
    {
        /// <summary>The most characters the field takes.</summary>
        int TypingLimit { get; }

        /// <summary>Enter pressed in the field: the screen confirms what was typed.</summary>
        void Submitted(string text);
    }

    /// <summary>
    /// A typing screen that asks for its field to take the keys as a page shows it (the game's own name box for a new business
    /// is focused as it opens). The host asks once with its other reads and opens the field for each yes.
    /// </summary>
    public interface IUiTypingFocus : IUiTyping
    {
        /// <summary>True once each time the field should take the keys: asking clears it.</summary>
        bool TakeFocus();
    }

    /// <summary>A typing screen whose fields wait to be pressed (a social app's): the host does not start typing as it opens.</summary>
    public interface IUiTypingQuiet
    {
    }
}
