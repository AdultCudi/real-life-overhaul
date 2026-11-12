namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the player can do on any screen of the kit, read from the game's own bindings (UiGame) or a pad: a screen
    /// names actions, never keys, and its prompt bar shows whatever each is bound to now.
    /// </summary>
    public enum UiAction : byte
    {
        Move = 0,     // the arrows or the d-pad: focus to the next target that way
        Confirm,      // the game's Confirm (Enter) or A: press what has focus
        Back,         // the game's Cancel (Escape) or B: close the overlay, then the screen
        TabPrev,      // the game's RotateLeft (Q) or LB: the tab or group before
        TabNext,      // the game's RotateRight (E) or RB: the tab or group after
        Alt,          // the game's Interact (F) or X: the screen's main action beside Confirm
        Option,       // the game's SecondaryInteract (P) or Y: the screen's second action (view on the map, the other look)
        Pick1,        // the number keys 1 to 9: one of a list of choices (a session's), the keyboard's alone; a pad moves and presses
        Pick2,
        Pick3,
        Pick4,
        Pick5,
        Pick6,
        Pick7,
        Pick8,
        Pick9,
        View,         // the pad's View: All apps in the full menu; no key of the keyboard's, so its prompt shows only with a pad
        Count
    }

    /// <summary>The pad a player is using, for its buttons' glyphs: Xbox's letters or PlayStation's shapes.</summary>
    public enum UiPad : byte
    {
        None = 0,     // keyboard and mouse: key caps with the bound key's name
        Xbox,
        PlayStation
    }

    /// <summary>
    /// The names of the keys bound to the actions now, as a prompt bar shows them: the game's own key labels while the
    /// keyboard was last used, the pad's buttons when a pad was. Game-free: the game file fills it, checks use the defaults.
    /// </summary>
    public interface IUiKeys
    {
        /// <summary>The pad last used, or None for the keyboard.</summary>
        UiPad Pad { get; }

        /// <summary>The bound key's short name ("Enter", "Q"), or null when the action shows an icon (the arrows, a pad's buttons).</summary>
        string Name(UiAction action);

        /// <summary>A key or button drawn as an icon (the arrows, a pad's glyphs), or None for a written name.</summary>
        UiIcon Glyph(UiAction action);
    }

    /// <summary>The game's default bindings, for layouts made before the game has said what is bound (and for the checks).</summary>
    public sealed class UiDefaultKeys : IUiKeys
    {
        public static readonly UiDefaultKeys Keyboard = new UiDefaultKeys(UiPad.None);
        public static readonly UiDefaultKeys XboxPad = new UiDefaultKeys(UiPad.Xbox);

        private readonly UiPad _pad;

        private UiDefaultKeys(UiPad pad)
        {
            _pad = pad;
        }

        public UiPad Pad => _pad;

        public string Name(UiAction action)
        {
            if (_pad != UiPad.None)
            {
                return null;
            }
            switch (action)
            {
                case UiAction.Confirm: return "Enter";
                case UiAction.Back: return "Esc";
                case UiAction.TabPrev: return "Q";
                case UiAction.TabNext: return "E";
                case UiAction.Alt: return "F";
                case UiAction.Option: return "P";
                default: return UiKeyGlyphs.Number(action);
            }
        }

        public UiIcon Glyph(UiAction action) => UiKeyGlyphs.For(action, _pad);
    }

    /// <summary>Which glyph stands for an action on a pad, and the arrows on the keyboard.</summary>
    public static class UiKeyGlyphs
    {
        private static readonly string[] Digits = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        /// <summary>A number key's name ("1" for Pick1), or null for any other action.</summary>
        public static string Number(UiAction action) => action >= UiAction.Pick1 && action <= UiAction.Pick9 ? Digits[action - UiAction.Pick1] : null;

        /// <summary>True for a key a screen may give one of its targets (not Move, not Confirm, which presses whatever has focus).</summary>
        public static bool Ownable(UiAction action) => action != UiAction.Move && action != UiAction.Confirm && action != UiAction.Count;

        /// <summary>The icon for an action on a pad (or the keyboard's arrows), or None for a written key name.</summary>
        public static UiIcon For(UiAction action, UiPad pad)
        {
            if (action == UiAction.Move)
            {
                return pad == UiPad.None ? UiIcon.Arrows : UiIcon.PadDpad;
            }
            if (pad == UiPad.None)
            {
                return UiIcon.None;
            }
            bool ps = pad == UiPad.PlayStation;
            switch (action)
            {
                case UiAction.Confirm: return ps ? UiIcon.PadCross : UiIcon.PadA;
                case UiAction.Back: return ps ? UiIcon.PadCircle : UiIcon.PadB;
                case UiAction.Alt: return ps ? UiIcon.PadSquare : UiIcon.PadX;
                case UiAction.Option: return ps ? UiIcon.PadTriangle : UiIcon.PadY;
                case UiAction.TabPrev: return ps ? UiIcon.PadL1 : UiIcon.PadLb;
                case UiAction.TabNext: return ps ? UiIcon.PadR1 : UiIcon.PadRb;
                case UiAction.View: return UiIcon.Grid;   // the pad's View button, as All apps' own tile shows it
                default: return UiIcon.None;
            }
        }
    }
}
