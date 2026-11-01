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

        // The City Ledger's shapes: print is square, a key cap keeps a hair of a corner, a framed panel wears a column rule
        // on its head (the paper's), faces are round portraits.
        public const float RadiusSmall = 2f;    // px: segments, key caps, tags
        public const float RadiusMedium = 2f;   // px: chips, fields, bars
        public const float RadiusLarge = 2f;    // px: rows, buttons, cards
        public const float RadiusPanel = 0f;    // px: panels and overlays: square, as a column of print
        public const float FrameRule = 3f;      // px: the column rule on a framed panel's head and under the masthead

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
        public const float PromptBand = 76f;    // px: the bottom bar the prompts stand in (the game's own is 150 canvas units)
        public const float MinText = 15f;       // px: the smallest text anywhere, readable at play distance

        // ------------------------------------------------------------------------------------------------- type

        // Serif heads and messages, Franklin capitals for the paper's labels, buttons, prompts and figures. Every size at or
        // over 15 px (MinText), the body at 18 so a message reads from the couch.
        private static readonly UiType[] Types =
        {
            new UiType(UiFamily.SerifDisplay, 700, 44f, -0.01f, 1.0f),                  // Display
            new UiType(UiFamily.SerifDisplay, 700, 34f, -0.01f, 1.0f),                  // AppTitle: a masthead, as written
            new UiType(UiFamily.SerifDisplay, 700, 36f, -0.01f, 1.0f),                  // Hero
            new UiType(UiFamily.SerifDisplay, 600, 24f, 0f, 1.0f),                      // SectionTitle
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

        // 0xRRGGBBAA. Dark is the paper's late edition: warm ink-black, paper-white words and the salmon of a markets paper
        // as its one accent (the owner's pink, turned into the financial pages' own colour). Light is the salmon paper
        // itself with its ink, and the paper's claret for what acts. Green and red stay for figures that rise and fall.
        private static readonly uint[] DarkColours =
        {
            0x00000000,   // None
            0x12110EFF,   // Bg: ink-black, warm
            0x191814FF,   // Panel
            0xEDE6D84D,   // PanelEdge: a paper-white rule at 30%
            0x1E1D18FF,   // Header: the masthead band
            0x191814FF,   // Row
            0xEDE6D826,   // RowEdge: 15%
            0x22201BFF,   // Field
            0x262420FF,   // Bubble
            0x2B2924FF,   // Hover
            0x2B231EFF,   // RowSel: the salmon in the ink
            0xF2A58A73,   // RowSelEdge: the salmon at 45%
            0xF1EBDDFF,   // Text: paper white
            0xC7BFAFFF,   // Text2
            0x9B9384FF,   // Muted: 5.3:1 on a field
            0xF2A58AFF,   // Accent: the markets paper's salmon
            0xF6B59EFF,   // AccentText
            0xF2A58A26,   // AccentSoft: 15%
            0x12110EFF,   // OnAccent: the ink, 9.4:1 on the salmon
            0x7FC48CFF,   // Ok
            0x9AD4A5FF,   // OkText
            0x7FC48C24,   // OkSoft
            0xE5574BFF,   // Danger
            0xF28B80FF,   // DangerText
            0xE5574B2E,   // DangerSoft
            0xE3B341FF,   // Warn
            0xEDC76BFF,   // WarnText
            0xE3B34129,   // WarnSoft
            0x34312AFF,   // Notice
            0xE6DFD0FF,   // NoticeText
            0xEDE6D82E,   // Line: a hairline rule
            0xEDE6D81F,   // Track
            0x12110ED9,   // Scrim: the ink at 85%, no blur
            0xF2A58AFF,   // Focus
            0xF1EBDDFF,   // PromptInk: the band is the kit's own and flat
            0x12110E99,   // PromptCap
        };

        private static readonly uint[] LightColours =
        {
            0x00000000,   // None
            0xFFF1E5FF,   // Bg: the salmon paper of the financial pages
            0xFFF8F2FF,   // Panel: a lighter sheet laid on it
            0x1A17124D,   // PanelEdge: an ink rule at 30%
            0xF2DFCEFF,   // Header: the masthead band, a shade deeper
            0xFFF8F2FF,   // Row
            0x1A171226,   // RowEdge: 15%
            0xF7E6D6FF,   // Field
            0xF2DECBFF,   // Bubble
            0xEDD6C1FF,   // Hover
            0xF5D3C2FF,   // RowSel
            0x990F3D73,   // RowSelEdge: the claret at 45%
            0x1A1712FF,   // Text: the ink
            0x4A4239FF,   // Text2
            0x62574BFF,   // Muted: 5 to 1 on a field and on the selection
            0x990F3DFF,   // Accent: the paper's claret, 8.4:1 under paper-white words
            0x990F3DFF,   // AccentText
            0x990F3D1F,   // AccentSoft: 12%
            0xFFF8F2FF,   // OnAccent
            0x0F8A4FFF,   // Ok
            0x0A6B3FFF,   // OkText: 6:1 on the paper
            0x0F8A4F1F,   // OkSoft
            0xC8401DFF,   // Danger: vermilion, apart from the claret
            0xA8321AFF,   // DangerText
            0xC8401D1F,   // DangerSoft
            0xB86E00FF,   // Warn
            0x8A5200FF,   // WarnText
            0xB86E001F,   // WarnSoft
            0xE9D3BFFF,   // Notice
            0x3A332BFF,   // NoticeText
            0x1A17122E,   // Line: a hairline rule
            0x1A17121F,   // Track
            0xFFF1E5D9,   // Scrim
            0x990F3DFF,   // Focus
            0x1A1712FF,   // PromptInk
            0xFFF8F2FF,   // PromptCap
        };

        /// <summary>A role's colour in a look, as 0xRRGGBBAA. An index into a fixed table.</summary>
        public static uint Colour(UiRole role, bool dark) => (dark ? DarkColours : LightColours)[(int)role];

        /// <summary>The number of roles each look's table holds; EconomyCheck holds both tables to it.</summary>
        public static int ThemeLength(bool dark) => (dark ? DarkColours : LightColours).Length;

        // An avatar without a face: initials on a circle tinted by a hue (the wireframe's oklch(0.34 0.07 h) under
        // oklch(0.92 0.06 h) in dark; light turns it round). Lightness and chroma, applied by UiColour.FromOklch.
        public const float AvatarDarkL = 0.34f, AvatarDarkC = 0.07f, AvatarDarkInkL = 0.92f, AvatarDarkInkC = 0.06f;
        public const float AvatarLightL = 0.9f, AvatarLightC = 0.05f, AvatarLightInkL = 0.4f, AvatarLightInkC = 0.08f;
    }
}
