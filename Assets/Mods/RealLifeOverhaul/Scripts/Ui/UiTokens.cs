namespace RealLifeOverhaul.Ui
{
    /// <summary>The colour roles of the interface: a screen names a role, the theme gives its colour.</summary>
    public enum UiRole : byte
    {
        None = 0,
        Bg,           // the screen behind every panel
        Panel,        // a framed panel: the detail, a sidebar block, an overlay's card
        PanelEdge,    // a panel's clear edge
        Header,       // a panel's header band
        Row,          // a list row
        RowEdge,
        Field,        // a search box, an input, a service card
        Bubble,       // their message
        Hover,        // a row or button under focus
        RowSel,       // the selected row
        RowSelEdge,
        Text,         // the main text
        Text2,        // roles, labels, secondary lines
        Muted,        // times, counts, placeholders, section headers
        Accent,       // the primary button, my message, the unread badge, the focus ring
        AccentText,   // the app's name, links, prices, the selected tab
        AccentSoft,   // a quiet accent fill: a chip, a pinned button
        OnAccent,     // text on the accent
        Ok,           // the online dot, "open", connected
        OkText,
        OkSoft,
        Danger,       // end the call, the destructive confirm
        DangerText,
        DangerSoft,
        Warn,
        WarnText,
        WarnSoft,
        Notice,       // a quiet badge
        NoticeText,
        Line,         // a rule between rows of details
        Track,        // an empty segment, a slider's track
        Scrim,        // behind an overlay
        Focus,        // the focus ring
        PromptInk,    // the prompt bar's words on the band the kit paints flat under the screen
        PromptCap,    // a key cap's face on that bar
        Rule,         // the masthead's rule and a panel's head rule: the accent at low strength, never a bright line
        Count
    }

    /// <summary>The typefaces, cut as static weights in the bundle (UiAssets/scripts/fonts.py).</summary>
    public enum UiFamily : byte
    {
        SerifDisplay = 0,   // Source Serif 4 at its display size: names, titles, the masthead
        Serif = 1,          // Source Serif 4 at its text size: messages and what a thing is
        Franklin = 2        // Libre Franklin: kickers, buttons, prompts, figures, the paper's labels and agate
    }

    /// <summary>The named text styles: a screen picks one, never a size of its own.</summary>
    public enum UiStyle : byte
    {
        Display = 0,      // a big number where it matters (a dashboard's figure)
        AppTitle,         // the app's name over its groups
        Hero,             // the name over a detail
        SectionTitle,     // a list's or a panel's title
        Title,            // a row's name, a card's title
        Nav,              // a group or a tab
        Button,
        Body,             // messages, descriptions, values
        BodyStrong,
        Small,            // a row's preview, a time, a count, a label
        SmallStrong,
        SectionHeader,    // capitals over a block of rows
        Prompt,           // a prompt bar's words
        Badge,            // a count in a badge
        Number,           // a figure in a stat block
        KeyCap,           // the key's name inside a key cap
        Count
    }

    /// <summary>How a style's words are cased.</summary>
    public enum UiCase : byte
    {
        AsWritten = 0,
        Upper,
        Lower
    }

    /// <summary>One text style: its face, size and spacing, in px of a 1920 x 1080 screen.</summary>
    public struct UiType
    {
        public UiFamily Family;
        public int Weight;          // 600, 700 or 800: the cuts in the bundle
        public float Size;          // px
        public float Tracking;      // em: letter spacing, as TextMeshPro's characterSpacing / 100
        public float Leading;       // a line's height as a multiple of the face's own line (ascender to descender and gap)
        public UiCase Case;

        public UiType(UiFamily family, int weight, float size, float tracking, float leading, UiCase textCase = UiCase.AsWritten)
        {
            Family = family;
            Weight = weight;
            Size = size;
            Tracking = tracking;
            Leading = leading;
            Case = textCase;
        }
    }

    /// <summary>The motion kinds, each with its own duration: feedback is quick, a fill takes its time.</summary>
    public enum UiMotionKind : byte
    {
        Press = 0,    // a press's dip
        Focus,        // the focus ring moving to the next target
        Select,       // a selection bar or a tab bar snapping across
        Enter,        // a panel or a row arriving
        Exit,         // an overlay leaving
        Fill,         // a bar filling, a number counting up
        Toast,        // a toast rising
        Count
    }

    /// <summary>The Motion setting: Full, Calm (slower, half the distance) or Off (every change at once).</summary>
    public enum UiMotionSetting : byte
    {
        Full = 0,
        Calm = 1,
        Off = 2
    }

    /// <summary>
    /// The one source of the interface's look, from the owner's Contacts wireframe (not-in-repo/design/DESIGN_SPEC.md;
    /// docs/standards/ui.md): the colours of both themes by role, the type scale, the 4 px spacing grid and its named
    /// steps, radii, strokes, the controls' sizes and the motion. Dark is the wireframe; light is derived from it, same
    /// hues and roles, and waits for the owner's word. Every number is in px of a 1920 x 1080 screen; the game's canvas is
    /// 3840 x 2160, so a px is <see cref="CanvasPerPx"/> canvas units. Game-free: EconomyCheck checks every pair of text and
    /// surface for contrast in both themes, that every spacing step is on the grid, and that motion keeps to game timing.
    /// No screen may set a colour, size, spacing, radius or duration of its own (StandardCheck, RLO-UI-001 to 004).
    /// Cost: constants and two arrays read by index.
    /// </summary>
    public static class UiTokens
    {
        public const float CanvasPerPx = 2f;   // the game's canvas units in a px: its scaler's reference is 3840 x 2160
        public const float Grid = 4f;           // px: every spacing value is a multiple of this

        // ------------------------------------------------------------------------------------------------- spacing

        public const float S1 = 4f;     // px: an icon from its word, a badge's inside
        public const float S2 = 8f;     // px: between rows, between chips
        public const float S3 = 12f;    // px: between a label and its value, a row's inner gap
        public const float S4 = 16f;    // px: a row's padding, between columns
        public const float S5 = 20f;    // px: a panel's inner padding
        public const float S6 = 24f;    // px: between blocks inside a panel
        public const float S8 = 32f;    // px: a panel's padding at its hero, between sections
        public const float S10 = 40f;   // px: an empty state's inset
        public const float S12 = 48f;   // px: the widest gap, between a hero and what follows
        public const float S16 = 64f;   // px: a screen's outer margin on wide frames

        /// <summary>Every spacing step, smallest first: the only gaps and paddings a layout may use.</summary>
        public static readonly float[] Steps = { S1, S2, S3, S4, S5, S6, S8, S10, S12, S16 };

        // ------------------------------------------------------------------------------------------------- radii

        // The City Ledger's shapes: print is square, a key cap keeps a hair of a corner, a panel wears a thin muted rule
        // on its head (the paper's column rule, kept quiet so it never glares), faces are round portraits.
        public const float RadiusSmall = 2f;    // px: segments, key caps, tags
        public const float RadiusMedium = 2f;   // px: chips, fields, bars
        public const float RadiusLarge = 2f;    // px: rows, buttons, cards
        public const float RadiusPanel = 0f;    // px: panels and overlays: square, as a column of print
        public const float FrameRule = 2f;      // px: the column rule on a panel's head and under the masthead

        // ------------------------------------------------------------------------------------------------- strokes

        public const float Hairline = 1f;       // px: a panel's edge, a rule
        public const float FocusStroke = 3f;    // px: the focus ring, drawn outside its target
        public const float FocusGap = 3f;       // px: between a target and its focus ring
        public const float SelectBar = 4f;      // px: the accent bar on the left of the selected row
        public const float TabBar = 4f;         // px: the bar under the shown tab

        // ------------------------------------------------------------------------------------------------- sizes

        public const float Control = 48f;       // px: a button, a field, a nav item
        public const float ControlSmall = 40f;  // px: a secondary button, a chip row
        public const float Chip = 36f;          // px: a quick reply, a filter
        public const float MinTarget = 36f;     // px: the least a focus or pointer target may be either way (a chip): over WCAG 2.2 AA's 24
        public const float PairRow = 44f;       // px: a key and its value with the rule under them
        public const float TableRow = 44f;      // px: a table's row: dense as a management game's tables, still a full target
        public const float TableRowMost = 64f;  // px: the tallest a row grows when a short table leaves room: it breathes rather than ending over a hole
        public const float MostSlack = 48f;     // px a frame may hold under its last line (two rows' gap and its padding): more reads as a hole
        public const float Row = 80f;           // px: a list row: a 48 avatar with 16 above and below
        public const float AvatarRow = 48f;     // px
        public const float AvatarHero = 96f;    // px
        public const float AvatarSmall = 40f;   // px
        public const float Icon = 24f;          // px: an icon's box beside words (the glyph sits inside with its own margin)
        public const float IconSmall = 16f;     // px: a tick after a name
        public const float Badge = 24f;         // px: an unread badge's height
        public const float Dot = 12f;           // px: the online dot
        public const float StatusDot = 8f;      // px: a group's or a chip's dot
        public const float KeyCap = 28f;        // px: a prompt's key cap
        public const float Segment = 8f;        // px: a stat bar segment's height
        public const float SegmentGap = 4f;     // px
        public const float MinText = 15f;       // px: the smallest text anywhere, readable at play distance

        // ------------------------------------------------------------------------------------------------- type

        // Serif heads and messages, Franklin capitals for the paper's labels, buttons, prompts and figures. Every size at or
        // over 15 px (MinText), the body at 18 so a message reads from the couch.
        private static readonly UiType[] Types =
        {
            new UiType(UiFamily.SerifDisplay, 700, 34f, -0.01f, 1.0f),                  // Display: the masthead's size, so a page keeps three sizes of type
            new UiType(UiFamily.SerifDisplay, 700, 34f, -0.01f, 1.0f),                  // AppTitle: a masthead, as written
            new UiType(UiFamily.SerifDisplay, 700, 36f, -0.01f, 1.0f),                  // Hero
            new UiType(UiFamily.SerifDisplay, 600, 20f, 0f, 1.0f),                      // SectionTitle: a question's or a list's title, in the text band so a page keeps three sizes
            new UiType(UiFamily.Serif, 700, 19f, 0f, 1.0f),                             // Title
            new UiType(UiFamily.Franklin, 600, 17f, 0.02f, 1.0f),                       // Nav
            new UiType(UiFamily.Franklin, 700, 16f, 0.08f, 1.0f, UiCase.Upper),         // Button
            new UiType(UiFamily.Serif, 500, 18f, 0f, 1.12f),                            // Body
            new UiType(UiFamily.Serif, 700, 18f, 0f, 1.12f),                            // BodyStrong
            new UiType(UiFamily.Franklin, 600, 16f, 0f, 1.0f),                          // Small
            new UiType(UiFamily.Franklin, 800, 16f, 0f, 1.0f),                          // SmallStrong
            new UiType(UiFamily.Franklin, 800, 15f, 0.14f, 1.0f, UiCase.Upper),         // SectionHeader: the paper's kicker
            new UiType(UiFamily.Franklin, 700, 16f, 0.08f, 1.0f, UiCase.Upper),         // Prompt
            new UiType(UiFamily.Franklin, 800, 15f, 0f, 1.0f),                          // Badge
            new UiType(UiFamily.Franklin, 800, 28f, -0.01f, 1.0f),                      // Number: the market table's figures
            new UiType(UiFamily.Franklin, 700, 15f, 0f, 1.0f, UiCase.Upper),            // KeyCap
        };

        /// <summary>A style's face and size. An index into a fixed table.</summary>
        public static UiType Type(UiStyle style) => Types[(int)style];

        // ------------------------------------------------------------------------------------------------- motion

        public const string Ease = "cubic-bezier(.2,.9,.2,1)";   // the wireframe's curve: a fast start, a soft landing
        public const float EaseX1 = 0.2f, EaseY1 = 0.9f, EaseX2 = 0.2f, EaseY2 = 1f;
        public const float Stagger = 0.02f;      // seconds between rows arriving: a list lands in one beat, not a cascade
        public const float StaggerMost = 8f;     // rows that stagger; the rest arrive with the eighth
        public const float Slide = 8f;           // px a row or panel slides as it arrives
        public const float PressScale = 0.97f;   // a press's dip
        public const float CalmTime = 1.5f;      // Calm: slower...
        public const float CalmDistance = 0.5f;  // ...and half the distance
        public const float ToastHold = 2.4f;     // seconds a toast stays

        // Seconds for each kind of motion: game timing, 80 to 200 ms for feedback, longer only for a fill.
        private static readonly float[] Durations =
        {
            0.08f,   // Press
            0.12f,   // Focus
            0.16f,   // Select
            0.2f,    // Enter
            0.14f,   // Exit
            0.42f,   // Fill
            0.22f,   // Toast
        };

        /// <summary>A kind of motion's duration under a setting, in seconds; 0 with motion off.</summary>
        public static float Duration(UiMotionKind kind, UiMotionSetting setting)
        {
            float d = Durations[(int)kind];
            return setting == UiMotionSetting.Off ? 0f : setting == UiMotionSetting.Calm ? d * CalmTime : d;
        }

        /// <summary>How far things slide under a setting, in px.</summary>
        public static float Distance(UiMotionSetting setting) => setting == UiMotionSetting.Off ? 0f : setting == UiMotionSetting.Calm ? Slide * CalmDistance : Slide;

        /// <summary>The base duration of a kind, the one EconomyCheck holds to the game range.</summary>
        public static float BaseDuration(UiMotionKind kind) => Durations[(int)kind];

        // ------------------------------------------------------------------------------------------------- colours

        // 0xRRGGBBAA. Dark is the paper's late edition: warm ink-black, soft warm words and the salmon of a markets paper as
        // its one accent. Light is a dim warm paper with its ink and the paper's claret for what acts. Green and red stay
        // for figures that rise and fall. The owner asked for nothing that glares (1 Nov): no near-white words or rules in
        // dark, no bright paper in light, panels told apart by tone and space rather than a frame on each.
        private static readonly uint[] DarkColours =
        {
            0x00000000,   // None
            0x12110EFF,   // Bg: ink-black, warm
            0x1A1915FF,   // Panel: a step up from the ground, which is all that sets a panel apart
            0xD9D1C233,   // PanelEdge: a control's edge where it needs one, 20%
            0x171612FF,   // Header: the masthead band
            0x1E1C18FF,   // Row: a table's every other row, a step over the panel
            0xD9D1C21A,   // RowEdge: 10%
            0x24221DFF,   // Field
            0x282621FF,   // Bubble
            0x22201CFF,   // Hover: a quiet step over the panel, well under the selection's fill
            0x34271FFF,   // RowSel: the salmon in the ink, clearly warmer than any hover
            0xE39A8166,   // RowSelEdge: the salmon at 40%
            0xDAD2C3FF,   // Text: a soft warm white, 9.5:1 at worst and easy at night
            0xB3AB9CFF,   // Text2
            0x9A9283FF,   // Muted: 4.6:1 on the hover fill
            0xE39A81FF,   // Accent: the markets paper's salmon, a shade quieter
            0xEBA790FF,   // AccentText
            0xE39A8124,   // AccentSoft: 14%
            0x12110EFF,   // OnAccent: the ink, 8.3:1 on the salmon
            0x7FC48CFF,   // Ok
            0x96CFA1FF,   // OkText
            0x7FC48C24,   // OkSoft
            0xE5574BFF,   // Danger
            0xEE8A7FFF,   // DangerText
            0xE5574B2E,   // DangerSoft
            0xE3B341FF,   // Warn
            0xE6C46DFF,   // WarnText
            0xE3B34129,   // WarnSoft
            0x34312AFF,   // Notice
            0xDAD2C3FF,   // NoticeText
            0xD9D1C21F,   // Line: a hairline rule
            0xD9D1C21C,   // Track
            0x0D0C0AE6,   // Scrim: the ink at 90%, no blur, so nothing bright behind a question reads as part of it
            0xE39A81FF,   // Focus
            0xDAD2C3FF,   // PromptInk: the band is the kit's own and flat
            0x12110E99,   // PromptCap
            0xE39A8152,   // Rule: the salmon at 32%, a dim warm line
        };

        private static readonly uint[] LightColours =
        {
            0x00000000,   // None
            0xDED1C0FF,   // Bg: a dim warm paper, L* 85, no glare
            0xE6DBCCFF,   // Panel: a step lighter, still well short of white
            0x2A221A33,   // PanelEdge: an ink edge at 20% where a control needs one
            0xD7C9B6FF,   // Header: the masthead band, a shade deeper
            0xE0D4C4FF,   // Row: a table's every other row, a step under the panel
            0x2A221A1F,   // RowEdge: 12%
            0xDACDBBFF,   // Field
            0xD5C6B2FF,   // Bubble
            0xDFD3C3FF,   // Hover: a quiet step under the panel, well short of the selection's fill
            0xDEBDA9FF,   // RowSel: the claret in the paper, clearly deeper than any hover
            0x8E1E3F66,   // RowSelEdge: the claret at 40%
            0x221D17FF,   // Text: the ink, a warm black
            0x41382EFF,   // Text2
            0x4F4539FF,   // Muted: 5.4:1 at worst
            0x8E1E3FFF,   // Accent: the paper's claret
            0x7A1434FF,   // AccentText
            0x8E1E3F1C,   // AccentSoft: 11%
            0xEDE3D5FF,   // OnAccent: 6.9:1 on the claret
            0x1E7A47FF,   // Ok
            0x135F36FF,   // OkText
            0x1E7A471C,   // OkSoft
            0xB83A1BFF,   // Danger: vermilion, apart from the claret
            0x972E16FF,   // DangerText
            0xB83A1B1C,   // DangerSoft
            0xA86400FF,   // Warn
            0x7A4A00FF,   // WarnText
            0xA864001C,   // WarnSoft
            0xD0BEA8FF,   // Notice
            0x352E26FF,   // NoticeText
            0x2A221A24,   // Line: a hairline rule
            0x2A221A33,   // Track: 20%, so an empty track or switch still shows on the paper
            0x2A221A8C,   // Scrim: the ink at 55%, so a question dims the page rather than whitening it
            0x8E1E3FFF,   // Focus
            0x221D17FF,   // PromptInk
            0xE6DBCCFF,   // PromptCap
            0x8E1E3F47,   // Rule: the claret at 28%, a dim warm line
        };

        /// <summary>A role's colour in a look, as 0xRRGGBBAA. An index into a fixed table.</summary>
        public static uint Colour(UiRole role, bool dark) => (dark ? DarkColours : LightColours)[(int)role];

        /// <summary>The number of roles each look's table holds; EconomyCheck holds both tables to it.</summary>
        public static int ThemeLength(bool dark) => (dark ? DarkColours : LightColours).Length;

        // An avatar without a face: initials on a circle tinted by a hue (the wireframe's oklch(0.34 0.07 h) under
        // oklch(0.92 0.06 h) in dark; light turns it round). Lightness and chroma, applied by UiColour.FromOklch.
        public const float AvatarDarkL = 0.34f, AvatarDarkC = 0.07f, AvatarDarkInkL = 0.86f, AvatarDarkInkC = 0.06f;   // initials a soft white, never glaring
        public const float AvatarLightL = 0.8f, AvatarLightC = 0.05f, AvatarLightInkL = 0.3f, AvatarLightInkC = 0.08f;   // a circle as dim as the paper, no bright disc on it
    }
}
