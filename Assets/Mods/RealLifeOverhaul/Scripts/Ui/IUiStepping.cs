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

    /// <summary>
    /// A screen that follows the mouse across one target (a chart's plot reads the candle under it, a rail names the tool): the
    /// host tells it each move over a target whose command it asks for, in px of the layout. A target that is one box, not a
    /// row of them, keeps a screen under the targets a layer may hold (UiCheckRules.MostTargets). Cost: one comparison a pointer
    /// move over any target; the screen lays out again only when what the pointer reads changes.
    /// </summary>
    public interface IUiTracking
    {
        /// <summary>Whether the target of this command wants the pointer's moves.</summary>
        bool Tracks(int command);

        /// <summary>The pointer moved over a target of that command and argument, to a place in px of the layout.</summary>
        void Moved(int command, int arg, float x, float y);
    }
}
