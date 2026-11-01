namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A screen whose controls take Left and Right as a step (a held slider), as a game's settings do: while one of them
    /// has focus and the screen says it is held, the host gives it the step instead of moving the focus, and a held key
    /// repeats as the game's HoldRepeatButton does. Otherwise Left and Right move the focus, so the d-pad crosses a page.
    /// </summary>
    public interface IUiStepping
    {
        /// <summary>Whether a target of this command takes Left and Right now (the screen holds it).</summary>
        bool Steps(int command);

        /// <summary>Left (-1) or Right (1) on that target.</summary>
        void Step(int command, int arg, int step);
    }

    /// <summary>
    /// A screen that reads what the mouse rests on (a chart's point in its card), as the keys' focus reads it. The hover
    /// itself stays the host's still fill: this only tells the screen, so it can lay the reading out. (A slider's knob
    /// follows the pointer through IUiDragging.)
    /// </summary>
    public interface IUiPointing
    {
        /// <summary>The mouse rests on a target (its command and argument), or has left it (command -1).</summary>
        void Pointed(int command, int arg);
    }
}
