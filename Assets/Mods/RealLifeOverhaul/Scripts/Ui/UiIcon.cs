namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one icon family of the interface: bold strokes with round ends on a 24-unit grid, each carrying a meaning, drawn
    /// in code once a session (UiIcons). A screen names an icon; nothing else draws a glyph of its own.
    /// </summary>
    public enum UiIcon : byte
    {
        None = 0,
        Search,
        Check,
        Close,
        Back,
        Next,
        Up,
        Down,
        Phone,
        Message,
        Send,
        Pin,
        Plus,
        Minus,
        Person,
        People,
        Briefcase,
        Building,
        Coin,
        Truck,
        Sofa,
        Flag,          // a rival
        Star,
        Bell,
        Clock,
        Calendar,
        MapPin,
        Info,
        Warning,
        Trash,
        Chart,
        Heart,
        Home,
        Store,
        Wrench,
        Cap,           // a school or a course
        Bank,
        Gear,
        Sun,           // the light look
        Moon,          // the dark look
        Spark,         // a suggestion from RLO Texts
        TrendUp,
        TrendDown,
        Arrows,        // the keyboard's four arrows, for Move
        PadDpad,
        PadA,
        PadB,
        PadX,
        PadY,
        PadCross,
        PadCircle,
        PadSquare,
        PadTriangle,
        PadLb,
        PadRb,
        PadL1,
        PadR1,
        Count
    }
}
