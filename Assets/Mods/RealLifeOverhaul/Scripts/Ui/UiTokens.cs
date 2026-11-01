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
        Rule,         // the hairline under a title strip and between sections
        Band,         // a section's darker band inside a panel
        StatusBar,    // the strip along the bottom of the screen
        Pill,         // a status pill on that strip
        Gold,         // a star rating
        Chart1,       // eight hues for a chart's slices and its legend, apart under colour blindness (EconomyCheck)
        Chart2,
        Chart3,
        Chart4,
        Chart5,
        Chart6,
        Chart7,
        Chart8,
        CatFood,      // a business family's colour, on its icons and slices, never on a large fill
        CatRetail,
        CatOffice,
        CatIndustry,
        CatTech,
        CatServices,
        Grade1,       // an attribute's grade, poor to excellent, as a management game colours a skill
        Grade2,
        Grade3,
        Grade4,
        Grade5,
        Tile,         // an icon's tile: a slate a step lighter than the panel, the icon in its own colour on it
        Ramp1,        // five steps of a figure low to high, for a map and its legend: an ordered ramp, never the chart's hues
        Ramp2,
        Ramp3,
        Ramp4,
        Ramp5,
        Count
    }

    /// <summary>The typefaces, cut as static weights in the bundle (UiAssets/scripts/fonts.py).</summary>
    public enum UiFamily : byte
    {
        Overpass = 0        // the one face: every title, row, button and figure, in three weights with tabular figures
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
        public int Weight;          // 400, 600 or 700: the cuts in the bundle
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
    /// The one source of the interface's look, after Cities: Skylines II mixed with a few other games (the owner's choice,
    /// 1 Nov 2026: not-in-repo/design/DIRECTION_CS2.md; docs/standards/ui.md): the colours by role in one look, the type
    /// scale, the 4 px spacing grid and its named steps, radii, strokes, the controls' sizes and the motion. Every number is
    /// in px of a 1920 x 1080 screen; the game's canvas is 3840 x 2160, so a px is <see cref="CanvasPerPx"/> canvas units.
    /// Game-free: EconomyCheck checks every pair of text and surface for contrast, the chart hues under colour blindness,
    /// that every spacing step is on the grid, and that motion keeps to game timing.
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

        // A game's soft corners: panels, buttons and pills at 4 px, portraits round, no shadows (the translucency parts a
        // panel from the world).
        public const float RadiusSmall = 2f;    // px: segments, key caps, tags
        public const float RadiusMedium = 4f;   // px: chips, fields, bars
        public const float RadiusLarge = 4f;    // px: rows, buttons, cards
        public const float RadiusPanel = 4f;    // px: panels and overlays
        public const float FrameRule = 1f;      // px: the hairline under a title strip

        // ------------------------------------------------------------------------------------------------- strokes

        public const float Hairline = 1f;       // px: a panel's edge, a rule
        public const float FocusStroke = 3f;    // px: the focus ring, drawn outside its target
        public const float FocusGap = 3f;       // px: between a target and its focus ring
        public const float SelectBar = 4f;      // px: the accent bar on the left of the selected row
        public const float TabBar = 4f;         // px: the bar under the shown tab

        // ------------------------------------------------------------------------------------------------- sizes

        public const float Control = 40f;       // px: a button, a field, a nav item
        public const float ControlSmall = 32f;  // px: a secondary button, a chip row, a footer's icon button
        public const float Chip = 28f;          // px: a quick reply, a filter, a status pill
        public const float MinTarget = 24f;     // px: the least a focus or pointer target may be either way: a compact row, WCAG 2.2 AA's 24
        public const float PairRow = 24f;       // px: a label and its value, as a game's info panel packs them, no rule between
        public const float TableRow = 32f;      // px: a table's row: dense as a management game's tables
        public const float TableRowMost = 44f;  // px: the tallest a row grows when a short table leaves room: it breathes rather than ending over a hole
        public const float MostSlack = 48f;     // px a frame may hold under its last line (two rows' gap and its padding): more reads as a hole
        public const float Row = 56f;           // px: a list row: a 40 portrait with 8 above and below
        public const float AvatarRow = 40f;     // px
        public const float AvatarHero = 96f;    // px: a profile's portrait
        public const float AvatarSmall = 32f;   // px
        public const float Icon = 20f;          // px: an icon's box in a row (the glyph sits inside with its own margin)
        public const float IconSmall = 16f;     // px: a tick after a name, a link's magnifier
        public const float IconTile = 40f;      // px: an icon on a tile, a toolbar or the phone's grid
        public const float Badge = 20f;         // px: an unread badge's height
        public const float Dot = 12f;           // px: the online dot
        public const float StatusDot = 8f;      // px: a group's or a chip's dot
        public const float KeyCap = 24f;        // px: a prompt's key cap
        public const float Segment = 8f;        // px: a stat bar segment's height
        public const float SegmentGap = 4f;     // px
        public const float MinText = 13f;       // px: the smallest text anywhere, a section's label (DIRECTION_CS2, section 3)

        // ------------------------------------------------------------------------------------------------- type

        // Overpass alone, as Cities: Skylines II sets it: panel titles in cyan capitals, compact rows, figures bold and
        // tabular. At most three sizes on a panel, never serif.
        private static readonly UiType[] Types =
        {
            new UiType(UiFamily.Overpass, 700, 26f, 0f, 1.0f),                          // Display: a big figure
            new UiType(UiFamily.Overpass, 700, 18f, 0.04f, 1.0f, UiCase.Upper),         // AppTitle: a window's title
            new UiType(UiFamily.Overpass, 700, 22f, 0f, 1.0f),                          // Hero: a name on a profile's head
            new UiType(UiFamily.Overpass, 600, 15f, 0.04f, 1.0f, UiCase.Upper),         // SectionTitle: a panel's title in its strip
            new UiType(UiFamily.Overpass, 600, 14f, 0f, 1.0f),                          // Title: a row's name
            new UiType(UiFamily.Overpass, 600, 14f, 0.01f, 1.0f),                       // Nav: a tab, a sidebar's entry
            new UiType(UiFamily.Overpass, 700, 13f, 0.06f, 1.0f, UiCase.Upper),         // Button
            new UiType(UiFamily.Overpass, 400, 14f, 0f, 1.15f),                         // Body: a row's value, a message
            new UiType(UiFamily.Overpass, 600, 14f, 0f, 1.15f),                         // BodyStrong
            new UiType(UiFamily.Overpass, 400, 13f, 0f, 1.0f),                          // Small: a preview, a time, a legend
            new UiType(UiFamily.Overpass, 600, 13f, 0f, 1.0f),                          // SmallStrong
            new UiType(UiFamily.Overpass, 600, 13f, 0.06f, 1.0f, UiCase.Upper),         // SectionHeader: a section's label
            new UiType(UiFamily.Overpass, 700, 13f, 0.06f, 1.0f, UiCase.Upper),         // Prompt
            new UiType(UiFamily.Overpass, 700, 13f, 0f, 1.0f),                          // Badge
            new UiType(UiFamily.Overpass, 700, 18f, 0f, 1.0f),                          // Number: a figure in a strip or a pill
            new UiType(UiFamily.Overpass, 700, 13f, 0f, 1.0f, UiCase.Upper),            // KeyCap
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

        // 0xRRGGBBAA. One look, after Cities: Skylines II (design/DIRECTION_CS2.md): cool slate panels the world shows
        // through, soft white words that never glare, one cyan for titles, links and what is chosen, and the game's status
        // colours. No brown, red or cream surface and no pure white anywhere. The chart hues are Okabe and Ito's, lifted for
        // a dark panel, so a slice stays apart from its neighbour under colour blindness. Category and chart colours go on
        // icons, slices and bars, never on a large fill.
        private static readonly uint[] Colours =
        {
            0x00000000,   // None
            0x00000000,   // Bg: nothing, as the world behind is the ground
            0x2B3A4AFF,   // Panel: slate, solid: the game blends in linear light, where a street's crossings and signs read through any thinner panel unblurred
            0xE6EDF31A,   // PanelEdge: a control's edge where it needs one, 10%
            0x223040EB,   // Header: a panel's title strip, 92%
            0x1A243055,   // Row: a list row, set a breath into the panel, so its quiet words keep their contrast
            0xE6EDF314,   // RowEdge: 8%
            0x1A2430CC,   // Field: a search box, an input, a secondary button, set into the panel
            0x31425AE6,   // Bubble: their message, a shade over the slate so its words keep 4.5:1
            0xE6EDF305,   // Hover: a still lift, under the selection's fill; 2% in the game's linear light looks as 6% would, quiet enough for secondary words
            0x2E5167FF,   // RowSel: the cyan at 18% over the slate as a tone; blended in the game's linear light it came out twice as strong
            0x3DBBEB66,   // RowSelEdge: the cyan at 40%
            0xE6EDF3FF,   // Text: soft white, never paper white
            0xA9B8C7FF,   // Text2
            0x9AAABBFF,   // Muted: a step over the direction's 7A8B9C, so a label on the slate over a bright street reaches 4.5:1
            0x3DBBEBFF,   // Accent: titles, links, chosen fills, the focus ring
            0x6ACDF1FF,   // AccentText: the cyan a step lighter for words, so a title or a link reads on a chosen row too
            0x3DBBEB2E,   // AccentSoft: 18%
            0x0E1822FF,   // OnAccent: dark slate words on the cyan, where a white would not read
            0x74D35AFF,   // Ok
            0x8ADC72FF,   // OkText: a step lighter, for words on its soft fill
            0x74D35A2E,   // OkSoft
            0xEE6352FF,   // Danger
            0xF6A69CFF,   // DangerText: lighter, so small words read on the slate and on its soft fill
            0xEE63522E,   // DangerSoft
            0xF2C14EFF,   // Warn
            0xF2C14EFF,   // WarnText
            0xF2C14E29,   // WarnSoft
            0x34465AFF,   // Notice
            0xE6EDF3FF,   // NoticeText
            0x3A4857FF,   // Line: a hairline, a tone over the slate; the game blends in linear light, where a faint white reads twice as bright
            0xE6EDF31F,   // Track: an empty segment
            0x0B1118D1,   // Scrim: the world dimmed behind a decision; 82% in linear light leaves it at about 45% to the eye
            0x3DBBEBFF,   // Focus
            0xE6EDF3FF,   // PromptInk
            0x0E182299,   // PromptCap
            0x3A4857FF,   // Rule: as Line, under a title strip and between sections
            0x1E2A3799,   // Band: a section's band, 60% over the panel
            0x1A2430FF,   // StatusBar: the bottom strip, solid for the same reason as a panel
            0x263445FF,   // Pill
            0xF5C451FF,   // Gold: a star rating
            0x56B4E9FF,   // Chart1: sky blue
            0xE69F2EFF,   // Chart2: orange
            0x2FBF8FFF,   // Chart3: bluish green
            0xF0E06AFF,   // Chart4: yellow
            0x6D9EEFFF,   // Chart5: blue, lifted so it reads on a slate tile
            0xF07A3AFF,   // Chart6: vermilion
            0xD889BBFF,   // Chart7: reddish purple
            0x6B7785FF,   // Chart8: slate grey, the rest: darker than the purple beside it, so the two part under deuteranopia
            0xF29A3DFF,   // CatFood: orange
            0xEC86BCFF,   // CatRetail: pink, light enough to read on a slate tile
            0x6FAAF3FF,   // CatOffice: blue
            0x9FB1C4FF,   // CatIndustry: steel
            0xB48EF3FF,   // CatTech: violet
            0x3FC7B6FF,   // CatServices: teal
            0xF28477FF,   // Grade1: poor, the bad red a step lighter, as a grade is written in its own colour
            0xF2914AFF,   // Grade2
            0xF2C14EFF,   // Grade3
            0xB4D45AFF,   // Grade4
            0x74D35AFF,   // Grade5: excellent
            0x344457FF,   // Tile: neutral, as a warm colour faded on slate turns brown and olive
            0xB5B3C5FF,   // Ramp1: low, a grey violet. The colour strengthens up the ramp and never runs to white, so a busy map never glares
            0xAAA3D0FF,   // Ramp2: a violet no chart slice comes near, so a map never reads as a donut's category
            0xA291D9FF,   // Ramp3
            0x9B7EE1FF,   // Ramp4
            0x9769E4FF,   // Ramp5: high, the strongest violet
        };

        /// <summary>A role's colour, as 0xRRGGBBAA. One look: the flag stays for the callers that still pass it.</summary>
        public static uint Colour(UiRole role, bool dark = true) => Colours[(int)role];

        /// <summary>The number of roles the table holds; EconomyCheck holds it to the roles.</summary>
        public static int ThemeLength(bool dark = true) => Colours.Length;

        /// <summary>An attribute's grade colour for a value of 0 to 100, poor to excellent in five steps.</summary>
        public static UiRole Grade(int value) => value < 30 ? UiRole.Grade1 : value < 50 ? UiRole.Grade2 : value < 65 ? UiRole.Grade3 : value < 80 ? UiRole.Grade4 : UiRole.Grade5;

        /// <summary>A step of the ramp for a figure from low (0) to high (4), as a map is coloured and its legend keyed.</summary>
        public static UiRole Ramp(int step) => (UiRole)((int)UiRole.Ramp1 + System.Math.Max(0, System.Math.Min(4, step)));

        /// <summary>The chart hue of a slice, in order and then round again.</summary>
        public static UiRole Chart(int slice) => (UiRole)((int)UiRole.Chart1 + ((slice % 8) + 8) % 8);

        // A person without a picture: initials on a circle tinted by a hue (UiColour.FromOklch), slate dim with soft white
        // words, so no face reads brighter than the panel's text.
        public const float AvatarDarkL = 0.42f, AvatarDarkC = 0.08f, AvatarDarkInkL = 0.93f, AvatarDarkInkC = 0.03f;
        public const float AvatarLightL = 0.42f, AvatarLightC = 0.08f, AvatarLightInkL = 0.93f, AvatarLightInkC = 0.03f;   // one look: as dark
    }
}
