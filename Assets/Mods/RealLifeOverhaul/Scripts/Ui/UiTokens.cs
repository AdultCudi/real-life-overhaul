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
        PromptInk,    // the prompt bar's words on the game's own bottom bar (the same in both themes)
        PromptCap,    // a key cap's face on that bar
        Count
    }

    /// <summary>The typefaces, cut as static weights in the bundle (UiAssets/scripts/fonts.py).</summary>
    public enum UiFamily : byte
    {
        Sora = 0,     // headings, names, buttons, numbers
        Manrope = 1,  // body
        SerifDisplay, // the theme study's: Source Serif 4 at its display size (the Ledger's headlines)
        Serif,        // Source Serif 4 at its text size (the Ledger's reading)
        Franklin,     // Libre Franklin (the Ledger's labels, figures and buttons)
        PlexMono,     // IBM Plex Mono (the Terminal's labels and figures)
        PlexSans,     // IBM Plex Sans (the Terminal's names and messages)
        Rubik         // the game's own face (the Phone's)
    }

    /// <summary>
    /// The looks of the theme study (the owner, 1 Nov: "a proper themed thing"): the same screens in three directions
    /// for the owner to choose from, the owner's wireframe standing until then. Every one is flat: solid surfaces, crisp
    /// edges and rules, no gradient, glow or blur (RLO-UI-015).
    /// </summary>
    public enum UiTheme : byte
    {
        Wireframe = 0,   // the owner's Contacts wireframe (Sora, Manrope, the pink)
        Ledger,          // the city's paper: the financial pages at night, ruled columns, serif heads, the salmon of a markets paper
        Terminal,        // the trading floor's terminal: dense, monospaced figures, steel borders, one amber for state
        Phone            // the player's phone: the game's own face, rounded device surfaces, the owner's pink as its tint
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

        // The theme drawn now, and what it sets besides colour and type: its corners (small, medium, large, panel, px), whether
        // a face is a circle or a square, and the rule a framed panel wears on its head (the Ledger's column rule, px).
        public static UiTheme Theme = UiTheme.Wireframe;
        private static readonly float[][] ThemeRadii =
        {
            new[] { 4f, 8f, 12f, 16f },   // Wireframe: a clear edge, not a soft card
            new[] { 2f, 2f, 2f, 0f },     // Ledger: print is square; a key cap keeps a hair of a corner
            new[] { 2f, 0f, 0f, 0f },     // Terminal: square, as a terminal's cells
            new[] { 6f, 10f, 14f, 22f },  // Phone: a device's continuous corners
        };
        private static readonly bool[] ThemeRoundFaces = { true, true, false, true };
        private static readonly float[] ThemeFrameRules = { 0f, 3f, 0f, 0f };

        public static float RadiusSmall => ThemeRadii[(int)Theme][0];    // px: segments, key caps, tags
        public static float RadiusMedium => ThemeRadii[(int)Theme][1];   // px: chips, fields, bars
        public static float RadiusLarge => ThemeRadii[(int)Theme][2];    // px: rows, buttons, cards
        public static float RadiusPanel => ThemeRadii[(int)Theme][3];    // px: panels and overlays

        /// <summary>Whether faces (avatars) are circles in the theme drawn now; squares otherwise.</summary>
        public static bool RoundFaces => ThemeRoundFaces[(int)Theme];

        /// <summary>The rule a framed panel wears on its head in the theme drawn now, px (0 none).</summary>
        public static float FrameRule => ThemeFrameRules[(int)Theme];

        /// <summary>The family a face's missing letters are measured and drawn in, or the face itself when it has all the mod's.</summary>
        public static UiFamily Backup(UiFamily family)
        {
            switch (family)
            {
                case UiFamily.Sora: return UiFamily.Manrope;
                case UiFamily.PlexMono: return UiFamily.PlexSans;
                case UiFamily.Rubik: return UiFamily.Manrope;
                default: return family;
            }
        }

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

        private static readonly UiType[] Types =
        {
            new UiType(UiFamily.Sora, 800, 44f, -0.02f, 1.0f),                      // Display
            new UiType(UiFamily.Sora, 800, 34f, -0.02f, 1.0f, UiCase.Lower),        // AppTitle: lower case, as the wireframe's "contacts"
            new UiType(UiFamily.Sora, 800, 36f, -0.02f, 1.0f),                      // Hero
            new UiType(UiFamily.Sora, 700, 24f, -0.01f, 1.0f),                      // SectionTitle
            new UiType(UiFamily.Sora, 700, 19f, 0f, 1.0f),                          // Title
            new UiType(UiFamily.Sora, 700, 18f, 0f, 1.0f),                          // Nav
            new UiType(UiFamily.Sora, 700, 18f, 0f, 1.0f),                          // Button
            new UiType(UiFamily.Manrope, 600, 18f, 0f, 1.06f),                      // Body: a little air between wrapped lines
            new UiType(UiFamily.Manrope, 800, 18f, 0f, 1.06f),                      // BodyStrong
            new UiType(UiFamily.Manrope, 600, 16f, 0f, 1.0f),                       // Small
            new UiType(UiFamily.Manrope, 800, 16f, 0f, 1.0f),                       // SmallStrong
            new UiType(UiFamily.Sora, 700, 15f, 0.12f, 1.0f, UiCase.Upper),         // SectionHeader
            new UiType(UiFamily.Sora, 700, 16f, 0.06f, 1.0f, UiCase.Upper),         // Prompt: the game's own bottom bar is in capitals
            new UiType(UiFamily.Manrope, 800, 15f, 0f, 1.0f),                       // Badge
            new UiType(UiFamily.Sora, 800, 28f, -0.01f, 1.0f),                      // Number
            new UiType(UiFamily.Sora, 700, 15f, 0f, 1.0f, UiCase.Upper),            // KeyCap
        };

        // The theme study's type: each direction's faces on the same named styles, so a screen never knows the theme.
        private static readonly UiType[] LedgerTypes =
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

        private static readonly UiType[] TerminalTypes =
        {
            new UiType(UiFamily.PlexMono, 600, 40f, -0.02f, 1.0f),                      // Display
            new UiType(UiFamily.PlexMono, 700, 28f, 0.04f, 1.0f, UiCase.Upper),         // AppTitle: the terminal's function name
            new UiType(UiFamily.PlexSans, 700, 34f, -0.01f, 1.0f),                      // Hero
            new UiType(UiFamily.PlexMono, 600, 20f, 0.04f, 1.0f, UiCase.Upper),         // SectionTitle
            new UiType(UiFamily.PlexSans, 600, 19f, 0f, 1.0f),                          // Title
            new UiType(UiFamily.PlexMono, 500, 16f, 0.02f, 1.0f, UiCase.Upper),         // Nav
            new UiType(UiFamily.PlexMono, 600, 16f, 0.04f, 1.0f, UiCase.Upper),         // Button
            new UiType(UiFamily.PlexSans, 500, 18f, 0f, 1.08f),                         // Body
            new UiType(UiFamily.PlexSans, 700, 18f, 0f, 1.08f),                         // BodyStrong
            new UiType(UiFamily.PlexMono, 500, 15f, 0f, 1.0f),                          // Small
            new UiType(UiFamily.PlexMono, 700, 15f, 0f, 1.0f),                          // SmallStrong
            new UiType(UiFamily.PlexMono, 600, 15f, 0.1f, 1.0f, UiCase.Upper),          // SectionHeader
            new UiType(UiFamily.PlexMono, 600, 16f, 0.04f, 1.0f, UiCase.Upper),         // Prompt
            new UiType(UiFamily.PlexMono, 700, 15f, 0f, 1.0f),                          // Badge
            new UiType(UiFamily.PlexMono, 600, 28f, 0f, 1.0f),                          // Number: every figure in one column width
            new UiType(UiFamily.PlexMono, 600, 15f, 0f, 1.0f, UiCase.Upper),            // KeyCap
        };

        private static readonly UiType[] PhoneTypes =
        {
            new UiType(UiFamily.Rubik, 700, 44f, -0.02f, 1.0f),                         // Display
            new UiType(UiFamily.Rubik, 700, 34f, -0.01f, 1.0f),                         // AppTitle: the app's large title
            new UiType(UiFamily.Rubik, 700, 36f, -0.01f, 1.0f),                         // Hero
            new UiType(UiFamily.Rubik, 600, 24f, 0f, 1.0f),                             // SectionTitle
            new UiType(UiFamily.Rubik, 600, 19f, 0f, 1.0f),                             // Title
            new UiType(UiFamily.Rubik, 500, 18f, 0f, 1.0f),                             // Nav
            new UiType(UiFamily.Rubik, 600, 18f, 0f, 1.0f),                             // Button
            new UiType(UiFamily.Rubik, 500, 18f, 0f, 1.08f),                            // Body
            new UiType(UiFamily.Rubik, 700, 18f, 0f, 1.08f),                            // BodyStrong
            new UiType(UiFamily.Rubik, 500, 16f, 0f, 1.0f),                             // Small
            new UiType(UiFamily.Rubik, 700, 16f, 0f, 1.0f),                             // SmallStrong
            new UiType(UiFamily.Rubik, 600, 15f, 0.1f, 1.0f, UiCase.Upper),             // SectionHeader
            new UiType(UiFamily.Rubik, 600, 16f, 0.06f, 1.0f, UiCase.Upper),            // Prompt
            new UiType(UiFamily.Rubik, 700, 15f, 0f, 1.0f),                             // Badge
            new UiType(UiFamily.Rubik, 700, 28f, -0.01f, 1.0f),                         // Number
            new UiType(UiFamily.Rubik, 600, 15f, 0f, 1.0f, UiCase.Upper),               // KeyCap
        };

        /// <summary>A style's face and size. An index into a fixed table.</summary>
        public static UiType Type(UiStyle style)
        {
            switch (Theme)
            {
                case UiTheme.Ledger: return LedgerTypes[(int)style];
                case UiTheme.Terminal: return TerminalTypes[(int)style];
                case UiTheme.Phone: return PhoneTypes[(int)style];
                default: return Types[(int)style];
            }
        }

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

        // 0xRRGGBBAA. Dark is the wireframe (its oklch values converted, DESIGN_SPEC's table); three are lifted for WCAG AA
        // where the wireframe fell short: Muted on a field (4.2:1), and the words on the accent (2.6:1), which take the
        // wireframe's own dark ink of the app mark. Light keeps every hue and role.
        private static readonly uint[] DarkColours =
        {
            0x00000000,   // None
            0x0C0C0FFF,   // Bg
            0x141518FF,   // Panel
            0xFFFFFF14,   // PanelEdge: white 8%
            0x1A1B1FFF,   // Header
            0x161619FF,   // Row
            0xFFFFFF0A,   // RowEdge: white 4%
            0x1E1E22FF,   // Field
            0x212126FF,   // Bubble
            0x26262CFF,   // Hover
            0x2B1F20FF,   // RowSel
            0xFB718859,   // RowSelEdge: the accent at 35%
            0xEEEEF2FF,   // Text
            0xA4A4ABFF,   // Text2
            0x8E8E96FF,   // Muted: the wireframe's #7F7F86 lifted to 4.6:1 on a field
            0xFB7188FF,   // Accent
            0xFF8296FF,   // AccentText
            0xFB718824,   // AccentSoft: the accent at 14%
            0x120A0DFF,   // OnAccent: the dark ink of the wireframe's app mark, 6.6:1 on the accent
            0x61DA92FF,   // Ok
            0x83E7A8FF,   // OkText
            0x61DA9221,   // OkSoft: 13%
            0xE64343FF,   // Danger
            0xFFABA3FF,   // DangerText
            0xE643432E,   // DangerSoft: 18%
            0xFF9A7AFF,   // Warn
            0xFFB49CFF,   // WarnText
            0xFF9A7A29,   // WarnSoft: 16%
            0x3A3A42FF,   // Notice
            0xD6D6DCFF,   // NoticeText
            0xFFFFFF0F,   // Line: white 6%
            0xFFFFFF12,   // Track: white 7%
            0x0C0C0FD9,   // Scrim: the background at 85%, no blur
            0xFB7188FF,   // Focus
            0xFFFFFFFF,   // PromptInk: white, as the game's own bottom bar
            0x0C0C0F99,   // PromptCap: dark glass behind a key's name
        };

        private static readonly uint[] LightColours =
        {
            0x00000000,   // None
            0xF6F6F9FF,   // Bg
            0xFFFFFFFF,   // Panel
            0x0000001A,   // PanelEdge: black 10%
            0xF3F3F7FF,   // Header
            0xFFFFFFFF,   // Row
            0x00000012,   // RowEdge: black 7%
            0xECECF1FF,   // Field
            0xE7E7EDFF,   // Bubble
            0xEFEFF4FF,   // Hover
            0xFFE7E9FF,   // RowSel
            0xCF315959,   // RowSelEdge
            0x15151AFF,   // Text
            0x4C4C52FF,   // Text2
            0x606068FF,   // Muted: 5.5:1 on a field
            0xCF3159FF,   // Accent: the derived #E74D6D deepened to carry white words at 4.9:1
            0xB8304FFF,   // AccentText
            0xCF31591F,   // AccentSoft: 12%
            0xFFFFFFFF,   // OnAccent
            0x009956FF,   // Ok
            0x00703FFF,   // OkText
            0x0099561F,   // OkSoft
            0xD02B31FF,   // Danger
            0xA81F24FF,   // DangerText
            0xD02B311F,   // DangerSoft
            0xC2410CFF,   // Warn
            0x9A330AFF,   // WarnText
            0xC2410C1F,   // WarnSoft
            0xE2E2E8FF,   // Notice
            0x3E3E44FF,   // NoticeText
            0x00000014,   // Line: black 8%
            0x00000012,   // Track
            0xF6F6F9D9,   // Scrim
            0xCF3159FF,   // Focus
            0xFFFFFFFF,   // PromptInk
            0x0C0C0F99,   // PromptCap
        };

        // The theme study's dark tables (the study is judged dark first; each takes the wireframe's light until one is chosen).
        private static readonly uint[] LedgerDark =
        {
            0x00000000, 0x12110EFF, 0x191814FF, 0xEDE6D84D, 0x1E1D18FF, 0x191814FF, 0xEDE6D826, 0x22201BFF, 0x262420FF, 0x2B2924FF,
            0x2B231EFF, 0xF2A58A73, 0xF1EBDDFF, 0xC7BFAFFF, 0x9B9384FF, 0xF2A58AFF, 0xF6B59EFF, 0xF2A58A26, 0x12110EFF, 0x7FC48CFF,
            0x9AD4A5FF, 0x7FC48C24, 0xE5574BFF, 0xF28B80FF, 0xE5574B2E, 0xE3B341FF, 0xEDC76BFF, 0xE3B34129, 0x34312AFF, 0xE6DFD0FF,
            0xEDE6D82E, 0xEDE6D81F, 0x12110ED9, 0xF2A58AFF, 0xF1EBDDFF, 0x12110E99,
        };

        private static readonly uint[] TerminalDark =
        {
            0x00000000, 0x07090BFF, 0x0C1013FF, 0x2A3843FF, 0x111820FF, 0x0C1013FF, 0x1B252DFF, 0x121A20FF, 0x15202AFF, 0x1A2631FF,
            0x231C08FF, 0xFFB00080, 0xE4ECF1FF, 0xA6B6C2FF, 0x7D909DFF, 0xFFB000FF, 0xFFC24DFF, 0xFFB0001F, 0x07090BFF, 0x37D07AFF,
            0x6BE39CFF, 0x37D07A1F, 0xFF5A4DFF, 0xFF8C82FF, 0xFF5A4D29, 0xFF9A3DFF, 0xFFB774FF, 0xFF9A3D24, 0x1B252DFF, 0xCFDDE6FF,
            0x1B252DFF, 0x1B252DFF, 0x07090BD9, 0xFFB000FF, 0xE4ECF1FF, 0x07090B99,
        };

        private static readonly uint[] PhoneDark =
        {
            0x00000000, 0x0B0B0DFF, 0x1C1C1FFF, 0xFFFFFF14, 0x232327FF, 0x1C1C1FFF, 0xFFFFFF0D, 0x2A2A2FFF, 0x2C2C31FF, 0x313137FF,
            0x3A2228FF, 0xFB71886B, 0xF2F2F5FF, 0xAEAEB4FF, 0x96969DFF, 0xFB7188FF, 0xFF8AA0FF, 0xFB718829, 0x1A0A0EFF, 0x4CD787FF,
            0x7FE4A9FF, 0x4CD78724, 0xFF5A52FF, 0xFF9A94FF, 0xFF5A522E, 0xFFA95CFF, 0xFFC08AFF, 0xFFA95C29, 0x3A3A40FF, 0xE0E0E6FF,
            0xFFFFFF14, 0xFFFFFF14, 0x0B0B0DD9, 0xFB7188FF, 0xFFFFFFFF, 0x0B0B0D99,
        };

        private static readonly uint[][] ThemeDark = { DarkColours, LedgerDark, TerminalDark, PhoneDark };

        /// <summary>A role's colour in a theme, as 0xRRGGBBAA. An index into a fixed table.</summary>
        public static uint Colour(UiRole role, bool dark) => (dark ? ThemeDark[(int)Theme] : LightColours)[(int)role];

        /// <summary>The number of roles each theme's table holds; EconomyCheck holds both tables to it.</summary>
        public static int ThemeLength(bool dark) => (dark ? DarkColours : LightColours).Length;

        // An avatar without a face: initials on a circle tinted by a hue (the wireframe's oklch(0.34 0.07 h) under
        // oklch(0.92 0.06 h) in dark; light turns it round). Lightness and chroma, applied by UiColour.FromOklch.
        public const float AvatarDarkL = 0.34f, AvatarDarkC = 0.07f, AvatarDarkInkL = 0.92f, AvatarDarkInkC = 0.06f;
        public const float AvatarLightL = 0.9f, AvatarLightC = 0.05f, AvatarLightInkL = 0.4f, AvatarLightInkC = 0.08f;
    }
}
