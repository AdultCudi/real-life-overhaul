namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A screen of the kit as its host runs it: it lays its view model out on request and answers what the player does.
    /// The screen owns its view model and the game's logic behind it; the host owns input, focus, sound and drawing.
    /// </summary>
    public interface IUiScreen
    {
        /// <summary>The screen's id in the registry (UiScreens).</summary>
        string Id { get; }

        /// <summary>True when its view model changed since it was last laid out.</summary>
        bool Dirty { get; }

        /// <summary>Lays the view model out into a scene at a frame, in a theme, with the keys bound now; clears Dirty.</summary>
        void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys);

        /// <summary>A target pressed (Confirm, a click); false when it does nothing now (the host plays the error).</summary>
        bool Press(int command, int arg);

        /// <summary>Focus moved onto a target: a list's row selects what the detail shows, as games follow the cursor.</summary>
        void Focused(int command, int arg);

        /// <summary>Back pressed: true when the screen took it (an overlay closed, typing ended); false lets the game close the screen.</summary>
        bool Back();

        /// <summary>The tab keys: the group or tab before (-1) or after (1).</summary>
        void Tab(int step);

        /// <summary>A screen action pressed from anywhere (Alt, Option); false when it does nothing now.</summary>
        bool Act(UiAction action);

        /// <summary>Focus wants a list's row beyond those in view: the screen scrolls so that row is laid out.</summary>
        void Reveal(int list, int index);

        /// <summary>The wheel over a scroll region: notches, positive up.</summary>
        void Wheel(int clip, float notches);
    }

    /// <summary>What the player pressed this frame, read from the game's bindings and a pad by the game file.</summary>
    public struct UiInputState
    {
        public int MoveX, MoveY;   // -1, 0 or 1: held directions; the host repeats them as a game does
        public bool Confirm, Back, TabPrev, TabNext, Alt, Option;
        public UiPad Pad;          // the pad last used, None while the keyboard or mouse was
    }

    /// <summary>The input a host reads: the game's own bindings and a pad, and Back taken so the game does not act on it too.</summary>
    public interface IUiInput
    {
        /// <summary>This frame's presses and held directions.</summary>
        void Read(ref UiInputState state);

        /// <summary>Back taken: the game's own Cancel left untouched for this frame's later readers.</summary>
        void TakeBack();

        /// <summary>The keys bound now, as the prompt bar names them.</summary>
        IUiKeys Keys { get; }
    }

    /// <summary>A sound of the interface: the game's own, so the kit sounds like the game.</summary>
    public enum UiCue : byte
    {
        Move = 0,    // focus to another target: the game's hover tick
        Confirm,     // a press: the game's click
        Back,        // back or close: the game's back
        Error,       // nothing happens: the game's error
        Tab          // another tab or group
    }

    /// <summary>Where a host's sounds go: the game's UI sounds (UiGame), or nowhere offline.</summary>
    public interface IUiSound
    {
        void Play(UiCue cue);
    }
}
