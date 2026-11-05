namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one icon family of the interface: Phosphor (MIT), each icon carrying a meaning, rasterized once a session from
    /// the outlines the mod carries (UiIconPaths, UiIconRaster) into a white texture tinted by its category where shown
    /// (UiIcons). Only the keyboard's and the pad's glyphs, which no icon set draws, stay drawn in code. A screen names an
    /// icon, and nothing else draws a glyph of its own.
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
        StarHalf,      // a rating's half star
        Play,          // the clock's speeds
        Pause,
        FastForward,
        CloudSun,      // the weather
        Wallet,
        Lightning,
        ForkKnife,
        SmileyHappy,   // a mood
        SmileyMeh,
        SmileySad,
        Thermometer,
        Locate,        // a link that moves the camera to its place
        Eye,
        AddressBook,
        IdCard,
        Navigation,
        Handshake,
        Baby,
        Key,
        GenreAction,   // a game's genre, for the studio
        GenreAdventure,
        GenreRpg,
        GenreStrategy,
        GenreSimulation,
        GenreSports,
        GenreRacing,
        GenrePuzzle,
        GenreHorror,
        GenreShooter,
        GenreCasual,
        GenreFamily,
        Code,
        PaintBrush,
        Bug,
        Lightbulb,
        Headset,
        Megaphone,
        Database,
        ShieldCheck,
        HardDrives,
        Presentation,
        Grid,
        GameController,
        ChartLineUp,
        ChartPie,
        Trophy,
        Rocket,
        Package,
        IdBadge,
        Camera,
        Image,
        MusicNotes,
        Newspaper,
        MapTrifold,
        ShoppingCart,
        ShoppingBag,
        Cpu,
        Fire,
        Heartbeat,
        Car,
        Hammer,
        Scales,
        Globe,
        Envelope,
        Ear,
        Factory,
        Coffee,
        Receipt,
        PiggyBank,
        Tag,
        Percent,
        Drop,          // hygiene, a need of the mod's
        Brain,         // stress, a need of the mod's
        Cloud,         // the weather: overcast
        CloudRain,
        CloudSnow,
        SkipForward,   // an activity's jump to its end (the game's time machine)
        Train,         // the subway
        ListChecks,    // the objectives
        ClipboardText, // the game's to-dos
        Ring,          // an open ring: a line still to do, beside the done tick
        FloppyDisk,    // saving the game
        Power,         // quitting the game
        SignOut,       // saving and leaving
        Pencil,        // a field that is typed in
        Layout,        // a blueprint: a business's layout
        Monitor,       // the game's graphics options
        SpeakerHigh,   // the game's audio options
        Keyboard,      // the game's controls
        Sliders,       // the game's own settings
        Cube,          // the game's mods
        Lock,          // a thing another holds (a session's lock)
        Reset,         // back to the defaults
        Upload,        // up to the Workshop
        Books,         // a library
        Download,      // into the library
        ToolCursor,     // the chart's pointer tool
        ToolTrend,      // the chart's trend line
        ToolLevel,      // the chart's horizontal line
        ToolFib,        // the chart's Fibonacci levels
        ToolRay,        // the chart's ray
        ToolRect,       // the chart's rectangle
        ToolMeasure,    // the chart's measure
        ToolText,       // the chart's note
        ToolSnap,       // the chart's level that snaps to a price
        Expand,         // a chart to the whole window
        Shrink,         // a chart back to its place
        Timer,         // the camera's timer
        Crop,          // a photo's shape, cut
        Rotate,        // a photo turned a quarter to the right
        Exposure,      // a photo's light, more or less
        Level,         // the camera's horizon line
        FlipCamera,    // the rear camera and the selfie
        EyeSlash,      // hide a thing from the picture
        FrameCorners,  // the picture alone, no controls
        Funnel,        // a filter of a list
        PortraitFocus, // a portrait, the person sharp
        Walk, // on foot: a route's walk, a quiet street
        Taxi, // a cab: a route by taxi
        Bookmark, // a place the player saved
        Count
    }

    /// <summary>
    /// Each icon's own colour, set once: an app or a section in its category's colour, a genre in its own, a state in the
    /// state's (a tick green, a bin red, a warning amber), so an icon looks the same on every screen and on the kit's style
    /// sheet (DIRECTION_CS2, section 3). A screen that means a state by an icon passes the state's colour instead. Game-free.
    /// </summary>
    public static class UiIconTints
    {
        public static UiRole Of(UiIcon icon)
        {
            switch (icon)
            {
                case UiIcon.Power:
                    return UiRole.Danger;   // quitting: the one that loses unsaved play
                case UiIcon.FloppyDisk: case UiIcon.Layout:
                    return UiRole.Accent;   // saving and the blueprints, the pause menu's own cyan
                case UiIcon.SkipForward:
                    return UiRole.Accent;   // with the clock's other controls
                case UiIcon.Train:
                    return UiRole.CatServices;   // a public service, apart from the maps' blue
                case UiIcon.ListChecks:
                    return UiRole.Chart3;
                case UiIcon.ClipboardText:
                    return UiRole.Chart2;
                case UiIcon.Drop:
                    return UiRole.Chart1;   // hygiene's sky blue, as the mod's own needs bar had it
                case UiIcon.Brain:
                    return UiRole.Chart7;   // stress's purple, as the mod's own needs bar had it
                case UiIcon.Cloud: case UiIcon.CloudRain: case UiIcon.CloudSnow:
                    return UiRole.Chart5;   // the weather's grey days in a cool blue, a fair one keeps the sun's yellow
                case UiIcon.Check: case UiIcon.ShieldCheck: case UiIcon.SmileyHappy: case UiIcon.TrendUp: case UiIcon.Phone: case UiIcon.Walk:
                    return UiRole.Ok;
                case UiIcon.Trash: case UiIcon.SmileySad: case UiIcon.TrendDown: case UiIcon.Heartbeat: case UiIcon.Heart:
                    return UiRole.Danger;
                case UiIcon.Warning: case UiIcon.SmileyMeh: case UiIcon.Bell: case UiIcon.Lightning: case UiIcon.Taxi:   // a cab's amber
                    return UiRole.Warn;
                case UiIcon.Info: case UiIcon.Locate: case UiIcon.Navigation: case UiIcon.Send: case UiIcon.Spark: case UiIcon.Eye: case UiIcon.Newspaper:
                case UiIcon.Play: case UiIcon.Pause: case UiIcon.FastForward:
                    return UiRole.Accent;
                case UiIcon.Star: case UiIcon.StarHalf: case UiIcon.Trophy: case UiIcon.Coin: case UiIcon.Key:
                case UiIcon.Clock: case UiIcon.Home: case UiIcon.Lightbulb: case UiIcon.Bookmark:   // the Planner, Property and the Guide (PhoneLooks), and a saved place
                    return UiRole.Gold;
                case UiIcon.Person: case UiIcon.People: case UiIcon.IdBadge: case UiIcon.Grid:
                case UiIcon.Envelope: case UiIcon.GenreSports:
                    return UiRole.Chart1;
                case UiIcon.Message: case UiIcon.Headset: case UiIcon.GenreCasual: case UiIcon.AddressBook:
                    return UiRole.CatServices;
                case UiIcon.Bank: case UiIcon.Cap: case UiIcon.Presentation: case UiIcon.Scales:
                    return UiRole.CatOffice;
                case UiIcon.Wallet: case UiIcon.Image: case UiIcon.Bug: case UiIcon.GenreSimulation: case UiIcon.Flag:
                    return UiRole.Chart3;
                case UiIcon.ChartPie: case UiIcon.CloudSun: case UiIcon.Sun: case UiIcon.Percent: case UiIcon.GenrePuzzle: case UiIcon.GenreRacing: case UiIcon.Hammer:
                    return UiRole.Chart4;
                case UiIcon.ChartLineUp: case UiIcon.Tag: case UiIcon.GenreAdventure: case UiIcon.Briefcase: case UiIcon.Truck:
                    return UiRole.Chart2;
                case UiIcon.Cpu: case UiIcon.GameController: case UiIcon.Code: case UiIcon.Database: case UiIcon.HardDrives: case UiIcon.IdCard:
                    return UiRole.CatTech;
                case UiIcon.Fire: case UiIcon.Globe: case UiIcon.Megaphone: case UiIcon.Rocket: case UiIcon.Thermometer: case UiIcon.Pin: case UiIcon.MapPin:
                case UiIcon.GenreAction: case UiIcon.GenreShooter:
                    return UiRole.Chart6;
                case UiIcon.Car: case UiIcon.Camera: case UiIcon.Chart: case UiIcon.Moon: case UiIcon.GenreStrategy: case UiIcon.MapTrifold:
                    return UiRole.Chart5;
                case UiIcon.Calendar: case UiIcon.Handshake: case UiIcon.MusicNotes: case UiIcon.Ear: case UiIcon.GenreRpg:
                    return UiRole.Chart7;
                case UiIcon.ShoppingCart: case UiIcon.ShoppingBag: case UiIcon.Store: case UiIcon.Sofa: case UiIcon.Baby: case UiIcon.PiggyBank: case UiIcon.PaintBrush:
                case UiIcon.GenreFamily:
                    return UiRole.CatRetail;
                case UiIcon.ForkKnife: case UiIcon.Coffee:
                    return UiRole.CatFood;
                case UiIcon.Building: case UiIcon.Wrench: case UiIcon.Factory: case UiIcon.Package: case UiIcon.GenreHorror:
                    return UiRole.CatIndustry;
                default:
                    return UiRole.Text2;   // the chrome's own: search, close, the arrows, the clock's controls, settings
            }
        }
    }
}
