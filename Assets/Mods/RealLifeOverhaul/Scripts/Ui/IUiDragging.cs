namespace RealLifeOverhaul.Ui
{
    /// <summary>A pointer's press and drag on a target, as the view tells its host.</summary>
    public enum UiDrag : byte
    {
        Down = 0,   // the pointer went down on the target, before its click
        Begin,      // it moved far enough to be a drag
        Move,
        End
    }

    /// <summary>A pointer's press or drag: its phase and its place in px of the layout.</summary>
    public struct UiDragPoint
    {
        public UiDrag Phase;
        public float X, Y;
    }

    /// <summary>
    /// A screen whose targets the pointer can take hold of and drag (a board's shifts, by their ends or their body): the
    /// host tells it where the pointer went down on a target before that target's press, when a drag starts on one, each
    /// place the drag reaches (every frame while it is held, so a board scrolls when the pointer rests at its edge) and
    /// when it ends. Places are px of the 1920 x 1080 layout, as the scene's own boxes. Game-free.
    /// </summary>
    public interface IUiDragging
    {
        /// <summary>The pointer went down on a target, at a place: told before the target's press, so a click can say where it landed (an hour on a lane).</summary>
        void PointerDown(int command, int arg, float x, float y);

        /// <summary>A drag starts on a target; false when the screen does not take it (the pointer then does nothing).</summary>
        bool DragBegin(int command, int arg, float x, float y);

        /// <summary>The drag reached a place.</summary>
        void DragMove(float x, float y);

        /// <summary>The drag ended: released (the edit stands), or cancelled (Back, the screen hidden), when what it moved goes back.</summary>
        void DragEnd(bool cancelled);
    }

    /// <summary>
    /// A screen that holds something chosen to move with the keys (a board's shift): while it does, the arrows or the
    /// d-pad step that thing, not the focus, and Shift held makes a step resize it. Game-free.
    /// </summary>
    public interface IUiSteering
    {
        /// <summary>A step along (dx, dy), one of them 0; false when nothing is held to move, and the focus moves as usual.</summary>
        bool Steer(int dx, int dy, bool shift);
    }

    /// <summary>A screen that turns a page of a long read with Page Up and Page Down (a document, a long list). Game-free.</summary>
    public interface IUiPaging
    {
        /// <summary>A page up (-1) or down (1).</summary>
        void Page(int step);
    }
}
