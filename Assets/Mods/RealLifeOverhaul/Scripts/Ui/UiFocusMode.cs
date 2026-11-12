namespace RealLifeOverhaul.Ui
{
    /// <summary>Which input leads a kit screen: the pointer, or the keys and a pad.</summary>
    public enum UiLead : byte
    {
        Pointer = 0,   // no focus ring, the target under the pointer takes a quiet fill
        Keys = 1       // the focus ring, moved by the arrows and the d-pad
    }

    /// <summary>
    /// The input mode of a kit screen, kept the way game menus keep it. A ring that follows the mouse jumps on every
    /// hover, so with the mouse there is no ring and hovering moves nothing. The first arrow, d-pad or Confirm press only
    /// brings the ring back where the focus is, so nobody acts on a target they could not see. Moving the mouse hides it
    /// again. Shortcut keys act in either mode. Game-free, so EconomyCheck drives it. Cost: a few comparisons a frame.
    /// </summary>
    public sealed class UiFocusMode
    {
        public UiFocusMode(UiLead lead)
        {
            Lead = lead;
        }

        public UiLead Lead { get; private set; }

        /// <summary>The target under the pointer, or -1.</summary>
        public int Hover { get; private set; } = -1;

        public bool Ring => Lead == UiLead.Keys;

        /// <summary>A pad player opens screens with the pad, so the ring is there from the start for them.</summary>
        public void Start(bool padUsedLast)
        {
            Lead = padUsedLast ? UiLead.Keys : UiLead.Pointer;
            Hover = -1;
        }

        /// <summary>The target whose face is lifted: the focus with the keys, the hovered one with the mouse.</summary>
        public int Lifted(int focus) => Lead == UiLead.Keys ? focus : Hover;

        /// <summary>True when the mouse took the lead back from the keys.</summary>
        public bool PointerMoved()
        {
            if (Lead == UiLead.Pointer)
            {
                return false;
            }
            Lead = UiLead.Pointer;
            return true;
        }

        /// <summary>A list scrolled by the keys under a resting pointer is not the player pointing, so only the pointer's lead takes a hover.</summary>
        public bool Entered(int node)
        {
            if (Lead != UiLead.Pointer)
            {
                return false;
            }
            Hover = node;
            return true;
        }

        public bool Exited(int node)
        {
            if (Hover != node)
            {
                return false;
            }
            Hover = -1;
            return true;
        }

        /// <summary>A layout renumbers the targets, so the hover is found again under the resting pointer.</summary>
        public void Rehover(int node)
        {
            Hover = Lead == UiLead.Pointer ? node : -1;
        }

        /// <summary>True when an arrow, d-pad or Confirm press only woke the ring and is spent. False when the keys led already and it acts.</summary>
        public bool Woke()
        {
            if (Lead == UiLead.Keys)
            {
                return false;
            }
            Lead = UiLead.Keys;
            Hover = -1;
            return true;
        }
    }
}
