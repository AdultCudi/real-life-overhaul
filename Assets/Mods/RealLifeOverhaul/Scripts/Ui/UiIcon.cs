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
                case UiIcon.Check: case UiIcon.ShieldCheck: case UiIcon.SmileyHappy: case UiIcon.TrendUp: case UiIcon.Phone:
                    return UiRole.Ok;
                case UiIcon.Trash: case UiIcon.SmileySad: case UiIcon.TrendDown: case UiIcon.Heartbeat: case UiIcon.Heart:
                    return UiRole.Danger;
                case UiIcon.Warning: case UiIcon.SmileyMeh: case UiIcon.Bell: case UiIcon.Lightning: case UiIcon.Lightbulb:
                    return UiRole.Warn;
                case UiIcon.Info: case UiIcon.Locate: case UiIcon.Navigation: case UiIcon.Send: case UiIcon.Spark: case UiIcon.Eye:
                    return UiRole.Accent;
                case UiIcon.Star: case UiIcon.StarHalf: case UiIcon.Trophy: case UiIcon.Coin: case UiIcon.Key:
                    return UiRole.Gold;
                case UiIcon.Person: case UiIcon.People: case UiIcon.IdCard: case UiIcon.IdBadge: case UiIcon.AddressBook: case UiIcon.Grid: case UiIcon.Clock:
                case UiIcon.Envelope: case UiIcon.GenreSports:
                    return UiRole.Chart1;
                case UiIcon.Message: case UiIcon.Headset: case UiIcon.GenreCasual:
                    return UiRole.CatServices;
                case UiIcon.Briefcase: case UiIcon.Cap: case UiIcon.Presentation: case UiIcon.Scales:
                    return UiRole.CatOffice;
                case UiIcon.Bank: case UiIcon.Home: case UiIcon.Wallet: case UiIcon.Image: case UiIcon.Bug: case UiIcon.GenreSimulation:
                    return UiRole.Chart3;
                case UiIcon.ChartPie: case UiIcon.CloudSun: case UiIcon.Sun: case UiIcon.Percent: case UiIcon.GenrePuzzle: case UiIcon.GenreRacing:
                    return UiRole.Chart4;
                case UiIcon.ChartLineUp: case UiIcon.Tag: case UiIcon.GenreAdventure:
                    return UiRole.Chart2;
                case UiIcon.Cpu: case UiIcon.GameController: case UiIcon.Code: case UiIcon.Database: case UiIcon.HardDrives:
                    return UiRole.CatTech;
                case UiIcon.Fire: case UiIcon.Flag: case UiIcon.Megaphone: case UiIcon.Rocket: case UiIcon.Thermometer: case UiIcon.Pin: case UiIcon.MapPin:
                case UiIcon.GenreAction: case UiIcon.GenreShooter:
                    return UiRole.Chart6;
                case UiIcon.MapTrifold: case UiIcon.Car: case UiIcon.Camera: case UiIcon.Globe: case UiIcon.Chart: case UiIcon.Moon: case UiIcon.GenreStrategy:
                    return UiRole.Chart5;
                case UiIcon.Calendar: case UiIcon.Handshake: case UiIcon.MusicNotes: case UiIcon.Ear: case UiIcon.GenreRpg:
                    return UiRole.Chart7;
                case UiIcon.ShoppingCart: case UiIcon.ShoppingBag: case UiIcon.Store: case UiIcon.Sofa: case UiIcon.Baby: case UiIcon.PiggyBank: case UiIcon.PaintBrush:
                case UiIcon.GenreFamily:
                    return UiRole.CatRetail;
                case UiIcon.ForkKnife: case UiIcon.Coffee:
                    return UiRole.CatFood;
                case UiIcon.Building: case UiIcon.Truck: case UiIcon.Wrench: case UiIcon.Hammer: case UiIcon.Factory: case UiIcon.Package: case UiIcon.GenreHorror:
                    return UiRole.CatIndustry;
                default:
                    return UiRole.Text2;   // the chrome's own: search, close, the arrows, the clock's controls, settings
            }
        }
    }
}
