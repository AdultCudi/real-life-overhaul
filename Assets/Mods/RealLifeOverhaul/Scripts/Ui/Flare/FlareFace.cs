using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a row of a Flare list is.</summary>
    public enum FlareRowKind : byte
    {
        Post = 0,     // a post's card: author, words, tags, picture, counts
        Notice,       // a notification: its glyph, a line, a detail, when
        Thread,       // a conversation of the inbox
        Bubble,       // a message in a conversation
        Review,       // a review of a business, with Reply
        Account,      // an account to follow
        Tag,          // a trending tag
        Result,       // what a post or a campaign brought
        Section,      // a heading over the rows after it
        Influencer,   // an influencer for hire
        Card          // a card of the city: a glyph, a title, words and an action
    }

    /// <summary>What a row shows beyond its words.</summary>
    [Flags]
    public enum FlareRowFlags : ushort
    {
        None = 0,
        Verified = 1,
        Liked = 2,
        Reposted = 4,
        Unread = 8,
        Mine = 16,
        Following = 32,
        Good = 64,       // its figure is good news (green)
        Bad = 128,       // ...or bad (red)
        Answered = 256,  // a review already answered: no Reply
        Off = 512,       // its button cannot be pressed now
        Chosen = 1024,
        Comment = 2048,  // a comment under a post: it is liked and answered, not shared
        Person = 4096    // the account is a person: a painted face, with a ring when the game has a mood for them
    }

    /// <summary>One row of a Flare list. Only what it shows: the screen fills it from the account, the samples by hand.</summary>
    public struct FlareRow
    {
        public FlareRowKind Kind;
        public FlareRowFlags Flags;
        public string Name;      // the account, the tag, a card's title
        public string Handle;    // "@speedybites", or a tag's district line
        public string Time;      // "2h"
        public string Text;      // the words
        public string Line;      // a line over a post ("Promoted · Midtown"), a detail under a notice, a review's answer
        public string Picture;   // a picture's key, or null
        public string[] Tags;    // a post's tags, without the #
        public string Likes, Comments, Reposts, Views;
        public string Value;     // a figure at the right ("+$1,180", "3,104 posts", a price)
        public string Button;    // the row's button ("Reply", "Follow", "Hire")
        public float Hue;
        public UiIcon Icon;      // a notice's or a card's glyph
        public UiRole Tint;      // ...and its colour
        public int Stars;        // a review's stars, 1 to 5
        public UiRole Ring;      // a person's mood as the ring round their face (None: no mood known)
        public float[] Shares;   // an influencer's figures: a bar behind each, 0 to 1 (a negative share: none), parallel to the figures in Tags
        public float Height;     // measured by the face at its list's width
    }

    /// <summary>A list of rows that scrolls: only the rows in view are laid out, the rest are counted.</summary>
    public sealed class FlareList
    {
        public FlareRow[] Rows = new FlareRow[16];
        public int Count;
        public float Offset;
        public int Reveal = -1;
        public bool Foot;            // a conversation: it opens at its newest, at the foot
        public string EmptyTitle, EmptyBody;
        public UiIcon EmptyIcon = UiIcon.Fire;
        public float Measured = -1f; // the width its rows were measured at
        public bool Footed;          // Foot applied: the list opened at its foot once
        public short Clip = -1;      // where it was last laid: its scroll region...
        public short Index = -1;     // ...and its list in the scene, for the wheel and the d-pad

        /// <summary>Empties it for new rows; its arrays are kept.</summary>
        public void Clear()
        {
            Count = 0;
            Measured = -1f;
        }

        /// <summary>A new row at the end, cleared.</summary>
        public ref FlareRow Add()
        {
            if (Count == Rows.Length)
            {
                Array.Resize(ref Rows, Rows.Length * 2);
            }
            Rows[Count] = default(FlareRow);
            return ref Rows[Count++];
        }
    }

    /// <summary>A profile's head: its banner, face, name, handle, words about it and its figures.</summary>
    public sealed class FlareProfileModel
    {
        public string Name, Handle, Bio, Banner;
        public float Hue;
        public string[] Figures = new string[0];
        public string[] Labels = new string[0];
        public string Follow;        // the Follow or Following button; null on the player's own
        public bool Following;
        public bool Verified;
        public string Edit;          // the Edit profile button; null on another's
        public bool Person;          // a person: a painted face...
        public UiRole Ring;          // ...with their mood as its ring, None when it is not known
    }

    /// <summary>The influencer chosen on the hire page: who they are, where their followers live, what the last campaigns did and what hiring them for this business should bring.</summary>
    public sealed class FlareHireModel
    {
        public bool Shown;
        public string Name;
        public float Hue;
        public UiRole Ring;                              // how reliably they deliver (their record), as the ring round their face
        public bool Verified;
        public int Row = -1;                             // the list row of the influencer: the button's argument
        public string AudienceTitle;
        public string[] AudienceLabels = new string[0], AudienceFigures = new string[0];
        public float[] AudienceShares = new float[0];
        public string AudienceEmpty;
        public string LastTitle, LastEmpty;
        public string[] LastLines = new string[0], LastFigures = new string[0];
        public int[] LastTones = new int[0];             // 1 good, -1 bad, 0 plain
        public string[] ForecastLabels = new string[0], ForecastFigures = new string[0];
        public string Note;                              // flops and viral in a hundred
        public string Hire;                              // the button: "Hire for $1,250"; null when it cannot be done
        public bool HireOff;
    }

    /// <summary>A business's page: which business, its stars, what Flare brought it and a warning when its reviews turn.</summary>
    public sealed class FlareBusinessModel
    {
        public string[] Picker = new string[0];
        public int Chosen;
        public string RatingTitle, Rating, RatingLine;   // "Rating", "4.3", "128 reviews"
        public int Halves;                               // the stars by halves, 0 to 10
        public int[] Bars = new int[5];                  // reviews of 5 stars down to 1
        public string VisitsTitle;
        public string[] Figures = new string[0];
        public string[] FigureLabels = new string[0];
        public string Warning;                           // the reviews have turned: what to do; null when they have not
        public string ReviewsTitle;
        public string ReachTitle, ReachEmpty;            // the reach line: its title, and what it says without enough days
        public float[] Reach;                            // ...the account's reach a day, only the days that exist
        public string Rival;                             // the business of its kind across the street and its rating; null when none is rated
        public string Promote;                           // the button that runs an ad for this business; null when it cannot
        public string[] Picker2 = new string[0];         // Hire: the niches to pick from, the first all of them...
        public int Chosen2;
        public string[] Sort = new string[0];            // ...and the orders: by followers, by fit
        public int SortChosen;
    }

    /// <summary>The creator studio: the week in cards, the followers' line, the tier and what it unlocks.</summary>
    public sealed class FlareStudioModel
    {
        public string[] Labels = new string[0], Figures = new string[0], Deltas = new string[0];
        public int[] Tones = new int[0];                 // 1 good, -1 bad, 0 plain
        public string LineTitle;
        public float[] Line;
        public string TierTitle, Tier, TierLine;         // "Sponsorship", "Local", "Next tier at 5,000 followers"
        public float TierShare;
        public string[] Perks = new string[0];
        public string CreatorTitle, Creator, CreatorLine;   // the creator income: what was paid and when the next is due; null title hides it
    }

    /// <summary>In the city: the followers by district as a donut with its legend, and the week of what posts did there.</summary>
    public sealed class FlareCityModel
    {
        public bool Shown;
        public string DonutTitle, DonutEmpty, DonutRest;
        public string DonutTotal;                        // what the donut adds up to, in words: the followers placed in districts out of the account's
        public string[] Names = new string[0], Values = new string[0];
        public float[] Shares = new float[0];
        public float Rest;                               // the share held by the districts not named
        public string WeekTitle;
        public string[] WeekLabels = new string[0], WeekFigures = new string[0];
    }

    /// <summary>
    /// A sheet over the window: the compose sheet, the hire sheet and the reply sheet are the same parts in different
    /// amounts: a title, words, a field, a picture, groups of chips, figures, options and the two buttons.
    /// </summary>
    public sealed class FlareSheetModel
    {
        public string Title, Line;
        public int FieldArg;                             // the field the words are typed into (ComposeField or BioField)
        public string BarsTitle;                         // the bars under the figures: where an audience lives
        public string[] BarLabels = new string[0], BarFigures = new string[0];
        public float[] BarShares = new float[0];
        public int Stars;                                // a review's stars before its words, 0 for none
        public string Words;
        public bool Field;
        public string FieldText, FieldHint, FieldCount;
        public string Picture;
        public string[] Groups = new string[0];          // each chip group's label
        public string[][] Chips = new string[0][];
        public int[] Chosen = new int[0];
        public string[] Figures = new string[0];
        public string[] FigureLabels = new string[0];
        public string Note;                              // the forecast, or why it cannot be done
        public bool NoteBad;
        public string[] Options = new string[0];         // a reply's options...
        public string[] OptionLines = new string[0];     // ...and what each does
        public int Option = -1;                          // the option chosen
        public string Confirm, Cancel;
        public bool ConfirmOff;
    }

    /// <summary>One screen of Flare: the page, its sheet and the words already chosen for it.</summary>
    public sealed class FlareFaceModel
    {
        public string Name = "flare";
        public int Page;
        public int Sheet;
        public string[] Nav = new string[0];
        public UiIcon[] NavIcons = new UiIcon[0];
        public int[] Badges = new int[0];
        public string Post = "Post";
        public string MyName = string.Empty, MyHandle = string.Empty;
        public float MyHue;
        public UiRole MyRing;                            // the player's own mood, as the ring round their face
        public UiRole DetailRing;                        // a conversation's other person
        public bool DetailPerson;
        public string Move = "Move", Select = "Select", Back = "Back";
        public string MarkAll;                           // Notifications: the button that marks them all read; null hides it
        public bool MarkAllOff;
        public bool NewPage;                             // the page just changed: focus starts again (the next layout takes this)
        public bool FromNav;                             // ...from a press on the nav, which keeps focus on its entry as every sidebar of the kit does
        public readonly FlareCityModel City = new FlareCityModel();
        public readonly FlareHireModel Hire = new FlareHireModel();

        // The column beside Home, Explore, Notifications, Profile and a post's thread.
        public string SearchHint, WeekTitle, StudioLink, TrendTitle, WhoTitle, FollowWord, FollowingWord;
        public string[] WeekLabels = new string[0], WeekFigures = new string[0], WeekDeltas = new string[0];
        public int[] WeekTones = new int[0];
        public float[] Line;
        public readonly FlareList Trends = new FlareList();
        public readonly FlareList Who = new FlareList();

        // The page.
        public string[] Tabs = new string[0];
        public int Tab;
        public string Head, HeadLine;
        public string ComposerHint;
        public string[] Chips = new string[0];
        public UiIcon[] ChipIcons = new UiIcon[0];
        public string[] Stories = new string[0];
        public float[] StoryHues = new float[0];
        public bool[] StoryShops = new bool[0];
        public bool[] StoryPeople = new bool[0];         // a person's story: their painted face...
        public UiRole[] StoryRings = new UiRole[0];      // ...inside a ring of their mood (None: the story's own colour)
        public string Search, InboxHint;     // the words typed into Explore's search or the inbox's
        public string Field, FieldHint;      // the words typed to send: a message, a reply under a post
        public int FieldArg = -1;
        public string Send;
        public string[] Quick = new string[0];
        public readonly FlareList List = new FlareList();
        public readonly FlareList Detail = new FlareList();
        public string DetailName, DetailLine, DetailLink;
        public float DetailHue;
        public readonly FlareProfileModel Profile = new FlareProfileModel();
        public readonly FlareBusinessModel Business = new FlareBusinessModel();
        public readonly FlareStudioModel Studio = new FlareStudioModel();
        public readonly FlareSheetModel Sheet0 = new FlareSheetModel();
        public readonly FlarePickerModel Picker = new FlarePickerModel();
    }

    /// <summary>
    /// Flare laid out as a social app in the game's window: a nav column with the flame, the pages and the Post button and
    /// the player's account at its foot; the page in the middle; beside it on the reading pages a column with search, the
    /// week, what is trending and who to follow; a sheet over it all to post, hire or answer. Every list scrolls with only
    /// the rows in view laid out. Game-free (the previews and EconomyCheck lay it as the game does). Cost: a layout when
    /// the page or the account changes, nothing per frame.
    /// </summary>
    public static partial class FlareFace
    {
        // The pages, in the nav's order; a post's thread opens over any of them.
        public const int Home = 0, Explore = 1, Notices = 2, Messages = 3, Profile = 4, Studio = 5, Business = 6, Hire = 7, City = 8, Thread = 9;
        public const int Pages = 9;

        // The sheets.
        public const int NoSheet = 0, ComposeSheet = 1, HireSheet = 2, ReplySheet = 3, PhotosSheet = 4, AdSheet = 5, EditSheet = 6;

        // The text fields (FieldCommand's argument): which one typing fills.
        public const int ComposeField = 0, SearchField = 1, InboxField = 2, ReplyField = 3, ThreadField = 4, PickerField = 5, BioField = 6;

        public const int NavCommand = 300, PostCommand = 301, AccountCommand = 302, TabCommand = 303, ComposerCommand = 304, ChipCommand = 305,
            StoryCommand = 306, FieldCommand = 307, RowCommand = 308, AuthorCommand = 309, LikeCommand = 310, CommentCommand = 311, RepostCommand = 312,
            ShareCommand = 313, TagCommand = 314, FollowCommand = 315, ButtonCommand = 316, SearchCommand = 317, StudioLinkCommand = 318, TrendCommand = 319,
            WhoCommand = 320, WhoFollowCommand = 321, PickCommand = 322, OptionCommand = 323, SheetChipCommand = 324, SheetConfirmCommand = 325,
            SheetCancelCommand = 326, SendCommand = 327, BackCommand = 328, ProfileFollowCommand = 329, QuickCommand = 330, DetailCommand = 331,
            PickerPhotoCommand = 332, PickerAlbumCommand = 333;

        // The audit's block (340 to 359; 300 to 333 are the face's own, 400 up are other parts'): none is a number another part of the window uses.
        public const int MarkAllCommand = 340, SortCommand = 341, NicheCommand = 342, PromoteCommand = 343, EditCommand = 344;

        /// <summary>Every command the face gives its targets, for the checks.</summary>
        public static readonly int[] Commands =
        {
            NavCommand, PostCommand, AccountCommand, TabCommand, ComposerCommand, ChipCommand, StoryCommand, FieldCommand, RowCommand, AuthorCommand,
            LikeCommand, CommentCommand, RepostCommand, ShareCommand, TagCommand, FollowCommand, ButtonCommand, SearchCommand, StudioLinkCommand,
            TrendCommand, WhoCommand, WhoFollowCommand, PickCommand, OptionCommand, SheetChipCommand, SheetConfirmCommand, SheetCancelCommand,
            SendCommand, BackCommand, ProfileFollowCommand, QuickCommand, DetailCommand, PickerPhotoCommand, PickerAlbumCommand, MarkAllCommand, SortCommand, NicheCommand,
            PromoteCommand, EditCommand
        };

        public const int MostTargets = 48;          // a layer's targets at most (UiCheckRules.MostTargets): rows past them wait for the scroll
        public const float NavWidth = 264f;         // the mockup's: room for the longest name beside its count ("Benachrichtigungen  12")
        public const float PictureAspect = 2.4f;    // a post's picture: the bank's photo, cut to a band
        public const float SideShare = 0.47f;       // of the room after the nav, the side column's

        private const byte RegionNav = 11, RegionMain = 12, RegionSide = 13, RegionDetail = 14;

        /// <summary>
        /// A picture item's key: Flare's prefix, the shape its box wants and the account's own key ("flare:2.40:f:..."), so the
        /// game's side cuts the photo to that shape once and keeps it (FlareScreen.Pictures.cs); the previews read the same.
        /// </summary>
        public static string PictureKey(string key, float aspect) => PicturePrefix + aspect.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ":" + key;

        public const string PicturePrefix = "flare:";

        /// <summary>A picture item's key back into its shape and the account's key; false for another key.</summary>
        public static bool PictureOf(string item, out float aspect, out string key)
        {
            aspect = PictureAspect;
            key = null;
            if (item == null || !item.StartsWith(PicturePrefix, StringComparison.Ordinal))
            {
                return false;
            }
            int colon = item.IndexOf(':', PicturePrefix.Length);
            if (colon < 0 || !float.TryParse(item.Substring(PicturePrefix.Length, colon - PicturePrefix.Length), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out aspect) || aspect <= 0.1f)
            {
                aspect = PictureAspect;
                return false;
            }
            key = item.Substring(colon + 1);
            return key.Length > 0;
        }

        /// <summary>Whether a page has the side column beside it.</summary>
        public static bool HasSide(int page) => page == Home || page == Explore || page == Notices || page == Profile || page == City || page == Thread;

        public static void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys, UiWords words, FlareFaceModel m)
        {
            m = m ?? new FlareFaceModel();
            if (frame.Icon == UiIcon.None)
            {
                frame.Icon = UiIcon.Fire;
            }
            if (string.IsNullOrEmpty(frame.Title))
            {
                frame.Title = words("reallife_flare_app", "Flare");
            }
            s.Begin(m.Name, frame.Width, frame.Height, dark);
            UiIcon mark = frame.Icon;
            frame.Icon = UiIcon.None;   // the strip's glyph is Flare's flame in its orange (the kit's own is the accent's), drawn below
            int panel = UiDraw.Window(s, frame, null, keys);
            frame.Icon = mark;
            frame.Window(out float wx, out float wy, out float ww, out float wh);
            float top = wy + UiFrame.Strip, h = UiDraw.SnapDown(wh - UiFrame.Strip);
            UiAt at = UiAt.Screen.In(panel);
            UiDraw.Icon(s, at, wx + UiTokens.S3, wy + (UiFrame.Strip - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Fire, UiRole.Chart2, UiFlags.Middle | UiFlags.Chrome, true);
            int firstOfNav = s.NodeCount;
            Nav(s, at.InRegion(RegionNav), m, frame.Title, wx, top, NavWidth, h);
            float mx = wx + NavWidth, rest = ww - NavWidth;
            UiDraw.Box(s, at, mx, top, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            bool side = HasSide(m.Page);
            int firstOfSide = s.NodeCount;
            float sw = side ? UiDraw.SnapDown(rest * SideShare) : 0f;
            float mw = rest - sw;
            if (side)
            {
                // The side first: what it takes of the targets is known before the page's list takes the rest.
                UiDraw.Box(s, at, mx + mw, top, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                Side(s, at.InRegion(RegionSide), m, mx + mw, top, sw, h);
            }
            int firstOfPage = s.NodeCount;
            Page(s, at.InRegion(RegionMain), m, mx, top, mw, h, keys);
            int lastOfPage = ReadingLinks(s, firstOfPage, s.NodeCount, 0, !side, out int firstOfPageRead);
            // The page is read as its column and then its side, each handing on to the next, and the nav is the door in: right on any entry goes to the first target of the page at
            // its top left, left from that target to the entry the page stands on, and only the account's right goes to the window's close. The nav is never part of the walk
            // across the page, so a player who pressed a nav entry can always press right to enter the page it opened.
            int firstOfSideRead = -1, lastOfSide = -1;
            if (side)
            {
                lastOfSide = ReadingLinks(s, firstOfSide, firstOfPage, 0, false, out firstOfSideRead);
            }
            int close = s.FindNode(UiDraw.CloseCommand, 0);
            NavLinks(s, firstOfNav, firstOfSide, close, firstOfPageRead >= 0 ? firstOfPageRead : firstOfSideRead, m.Page, out int chosenEntry);
            int lastRead = lastOfSide >= 0 ? lastOfSide : lastOfPage;
            if (close >= 0 && lastRead >= 0)
            {
                // The reading ends on the close and not by turning back to the first target, which is what the d-pad's own search finds from the last one.
                s.Nodes[lastRead].Right = close;
                s.Nodes[close].Left = lastRead;
            }
            s.FocusFirst = PageStart(s, firstOfPage, m);
            if (m.NewPage)
            {
                // A page of its own opens with focus where a player starts reading it (its chosen tab, else its first target), and a nav press keeps it on its entry, so right goes in.
                s.FocusReset = true;
                s.FocusFirst = m.FromNav && chosenEntry >= 0 ? chosenEntry : s.FocusFirst;
                m.NewPage = m.FromNav = false;
            }
            if (m.Sheet != NoSheet)
            {
                int firstOfSheet = s.NodeCount;
                Sheet(s, m, wx, wy, ww, wh, keys);
                ReadingLinks(s, firstOfSheet, s.NodeCount, 1, true, out _);
                s.FocusFirst = SheetStart(s, m.Sheet);
            }
            s.AddPrompt(UiAction.Move, m.Move);
            s.AddPrompt(UiAction.Confirm, m.Select);
            if (m.Sheet == NoSheet)
            {
                s.AddPrompt(UiAction.Alt, m.Post);
            }
            s.AddPrompt(UiAction.Back, m.Back);
            UiDraw.PromptBar(s, frame, keys);
        }

        private static int[] _read = new int[64];

        /// <summary>
        /// Right at the end of a row goes on to the first target of the next row, and left at the start of one back to the last of the one before, so the page or the sheet
        /// is walked left to right and top to bottom, group after group, with one run of presses, the commit button last and a sheet's close after it. On a page with a side
        /// column only where the d-pad would find nothing that way (what lies to the right is still what right reaches); on a sheet or a page without one always.
        /// A list keeps its own up and down. Only the nodes from <paramref name="from"/> up to <paramref name="to"/> are read; the last one read is returned and the
        /// first one through <paramref name="firstRead"/>.
        /// </summary>
        public static int ReadingLinks(UiScene s, int from, int to, byte layer, bool always, out int firstRead)
        {
            int n = 0, close = -1;
            for (int i = from; i < to; i++)
            {
                if (layer == 1 && s.Nodes[i].Command == SheetCancelCommand && s.Nodes[i].Arg == 0)
                {
                    close = i;   // the sheet's way out: read last
                }
                else if (s.Nodes[i].Layer == layer && !s.Nodes[i].Disabled && !UiDraw.IsChrome(s.Nodes[i].Command))
                {
                    if (n == _read.Length)
                    {
                        Array.Resize(ref _read, n * 2);
                    }
                    _read[n++] = i;
                }
            }
            // by row, then left to right (an insertion sort: a screen has a few dozen of them)
            for (int a = 1; a < n; a++)
            {
                int key = _read[a], b = a - 1;
                while (b >= 0 && ReadsBefore(s, key, _read[b]))
                {
                    _read[b + 1] = _read[b];
                    b--;
                }
                _read[b + 1] = key;
            }
            firstRead = n > 0 ? _read[0] : -1;
            for (int k = 0; k + 1 < n; k++)
            {
                int a = _read[k], b = _read[k + 1];
                if (s.Nodes[a].Region == s.Nodes[b].Region && Math.Abs(Mid(s, b) - Mid(s, a)) <= ReadRow)
                {
                    // The same row: each to the next along it. The d-pad's own nearest-target search can tie between a neighbour and a target of the row above (left of the
                    // composer's Post found the tab, not the chip beside it), and a chip then had no way in once the nav no longer led into the page.
                    s.Nodes[a].Right = b;
                    s.Nodes[b].Left = a;
                    continue;
                }
                if (always || !Finds(s, a, 1))
                {
                    s.Nodes[a].Right = b;
                }
                if (always || !Finds(s, b, -1))
                {
                    s.Nodes[b].Left = a;
                }
            }
            if (close >= 0 && n > 0)
            {
                s.Nodes[_read[n - 1]].Right = close;
                s.Nodes[close].Left = _read[n - 1];
            }
            return n > 0 ? _read[n - 1] : -1;
        }

        /// <summary>
        /// The nav as the door into the page. Right on every entry (the pages and Post) goes to <paramref name="entry"/>, the first target of the page at its top left, which is
        /// what the kit's own sidebars do, and left from that target goes back to the entry the page stands on (<paramref name="chosen"/> gets it, or -1). Only the account's
        /// right goes to the window's close, which the kit's own links never reach sideways. Up and down are the kit's own: one entry along the column.
        /// </summary>
        private static void NavLinks(UiScene s, int from, int to, int close, int entry, int page, out int chosen)
        {
            chosen = -1;
            for (int i = from; i < to; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (n.Disabled || UiDraw.IsChrome(n.Command))
                {
                    continue;
                }
                if (n.Command == AccountCommand)
                {
                    n.Right = close >= 0 ? close : n.Right;
                }
                else if (entry >= 0)
                {
                    n.Right = entry;
                }
                chosen = n.Command == NavCommand && n.Arg == page ? i : chosen;
            }
            if (entry >= 0 && chosen >= 0)
            {
                s.Nodes[entry].Left = chosen;
            }
        }

        /// <summary>
        /// The targets that right alone reaches from <paramref name="start"/>, in the order it reaches them, written into <paramref name="into"/>; it stops at the end of the
        /// run or at a target it has been to (the run then ends there, a fault the checks look for). The preview draws this walk and the checks assert it, so both are the game's own.
        /// </summary>
        public static int WalkRight(UiScene s, int start, int[] into, out bool revisit)
        {
            revisit = false;
            int n = 0, at = start;
            while (at >= 0 && at < s.NodeCount && n < into.Length)
            {
                for (int i = 0; i < n; i++)
                {
                    if (into[i] == at)
                    {
                        revisit = true;
                        return n;
                    }
                }
                into[n++] = at;
                at = UiFocusRules.Next(s, at, 1, 0);
            }
            return n;
        }

        /// <summary>
        /// Every target the d-pad reaches from <paramref name="start"/>, in the order a player presses to meet them: at each target right first, then down, then left, then up, to the
        /// first one not been to, and where all four lead to places already seen, back to the target before and on from there (a depth first walk, so each number follows the press that
        /// found it and the nav, which only left and up from the page reach, comes last and in order down its column). <paramref name="order"/> gets the targets as they are met and
        /// <paramref name="from"/> the target each was pressed from (the start has -1), so the preview numbers every stop in press order and the checks count what a page leaves
        /// out. Returns how many were met. Cost: a walk over the page's targets, in the previews and the checks only.
        /// </summary>
        public static int Reach(UiScene s, int start, int[] order, int[] from)
        {
            if (start < 0 || start >= s.NodeCount)
            {
                return 0;
            }
            var met = new bool[s.NodeCount];
            met[start] = true;
            order[0] = start;
            from[0] = -1;
            int n = 1;
            // Three rounds of the same walk. First the page, with the nav kept out of it (the page's own presses reach the nav only at its foot, which would number it from the bottom
            // up); then the nav, from the entry that left of the page goes to, down its column and then up it; then whatever only the nav leads to.
            for (int phase = 0; phase <= 2; phase++)
            {
                for (bool more = true; more && n < order.Length; )
                {
                    more = false;
                    for (int i = 0; i < n && !more; i++)
                    {
                        for (int d = 0; d < 4 && !more; d++)
                        {
                            int to = Press(s, order[i], d);
                            if (to >= 0 && !met[to] && Allowed(s, to, phase))
                            {
                                // back at the earliest target with a way not yet taken, press on from there as far as the d-pad leads to places not been to
                                more = true;
                                int at = order[i];
                                while (to >= 0 && n < order.Length)
                                {
                                    met[to] = true;
                                    order[n] = to;
                                    from[n++] = at;
                                    at = to;
                                    to = -1;
                                    for (int k = 0; k < 4 && to < 0; k++)
                                    {
                                        int next = Press(s, at, k);
                                        to = next >= 0 && !met[next] && Allowed(s, next, phase) ? next : -1;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return n;
        }

        // The target a press of the d-pad (0 right, 1 down, 2 left, 3 up) goes to, or -1.
        private static int Press(UiScene s, int node, int d)
        {
            int to = UiFocusRules.Next(s, node, d == 0 ? 1 : d == 2 ? -1 : 0, d == 1 ? 1 : d == 3 ? -1 : 0);
            return to >= 0 && to < s.NodeCount ? to : -1;
        }

        private static bool Allowed(UiScene s, int node, int phase) => phase == 2 || (s.Nodes[node].Region == RegionNav) == (phase == 1);

        // Whether the d-pad finds a target of the page that way. The window's close stands at the top right and is the nearest "right" of anything near it, but it is the end of the
        // walk and not a place a row goes on to, so it counts as nothing.
        private static bool Finds(UiScene s, int node, int dx)
        {
            int to = UiFocusRules.Next(s, node, dx, 0);
            return to >= 0 && to < s.NodeCount && s.Nodes[to].Command != UiDraw.CloseCommand;
        }

        private const float ReadRow = 14f;

        private static float Mid(UiScene s, int node)
        {
            s.NodeBox(node, out _, out float y, out _, out float h);   // on the screen, a list's rows scrolled
            return y + h / 2f;
        }

        private static float Left(UiScene s, int node)
        {
            s.NodeBox(node, out float x, out _, out _, out _);
            return x;
        }

        // Whether a target is read before another: on an earlier row, or on the same row further left.
        private static bool ReadsBefore(UiScene s, int p, int q)
        {
            if (s.Nodes[p].Region != s.Nodes[q].Region)
            {
                return s.Nodes[p].Region < s.Nodes[q].Region;   // a conversation list before the conversation it opens
            }
            float dy = Mid(s, p) - Mid(s, q);
            return Math.Abs(dy) > ReadRow ? dy < 0f : Left(s, p) < Left(s, q);
        }

        // The target focus starts on: the chosen tab of the page, else the first target the page drew; a page with none (news is read, not pressed) starts
        // on its own place in the nav. Never the window's close button, which is the way out, and never the nav's first row, which is not where the player is.
        private static int PageStart(UiScene s, int from, FlareFaceModel m)
        {
            int first = -1;
            for (int i = from; i < s.NodeCount; i++)
            {
                if (s.Nodes[i].Disabled || UiDraw.IsChrome(s.Nodes[i].Command))
                {
                    continue;
                }
                first = first < 0 ? i : first;
                if (s.Nodes[i].Command == TabCommand && s.Nodes[i].Arg == m.Tab)
                {
                    return i;
                }
            }
            for (int i = 0; first < 0 && i < from; i++)
            {
                if (!s.Nodes[i].Disabled && s.Nodes[i].Command == NavCommand && s.Nodes[i].Arg == m.Page)
                {
                    first = i;
                }
            }
            return first;
        }

        // A sheet's first target is its first field, else its first chip or option: not the close button, which is the way out.
        private static int SheetStart(UiScene s, int sheet)
        {
            int field = -1, chip = -1, any = -1;
            for (int i = 0; i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (n.Layer != 1 || n.Disabled || UiDraw.IsChrome(n.Command))
                {
                    continue;
                }
                bool way = n.Command == SheetCancelCommand;
                field = field < 0 && n.Command == FieldCommand ? i : field;
                chip = chip < 0 && (n.Command == SheetChipCommand || n.Command == OptionCommand || n.Command == PickerPhotoCommand) ? i : chip;
                any = any < 0 && !way ? i : any;
            }
            return field >= 0 ? field : chip >= 0 ? chip : any;
        }

        // ------------------------------------------------------------------------------------------------ the nav

        private static void Nav(UiScene s, UiAt at, FlareFaceModel m, string brand, float x, float y, float w, float h)
        {
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8;
            // The flame and the wordmark are Flare's mark, in its orange, as the mockup has them at the head of the nav.
            UiDraw.Icon(s, at, ix + UiTokens.S2, y + UiTokens.S4, UiTokens.S8, UiIcon.Fire, UiRole.Chart2, UiFlags.None, true);
            if (!string.IsNullOrEmpty(brand))
            {
                float wx = ix + UiTokens.S2 + UiTokens.S8 + UiTokens.S2;
                UiDraw.TextMiddle(s, at, wx, y + UiTokens.S4, UiTokens.S8, ix + iw - wx, UiStyle.Display, UiRole.Chart2, brand.ToLowerInvariant(),
                    UiFlags.Label | UiFlags.Truncate | UiFlags.Chrome);   // the mark of the app, not a page's own words: it is the title in its brand's lettering
            }
            float iy = y + UiTokens.S4 + UiTokens.S8 + UiTokens.S4;
            int n = m.Nav != null ? m.Nav.Length : 0;
            int page = m.Page == Thread ? -1 : m.Page;
            for (int i = 0; i < n; i++)
            {
                NavItem(s, at, ix, iy, iw, m.Nav[i], i < m.NavIcons.Length ? m.NavIcons[i] : UiIcon.Info, i < m.Badges.Length ? m.Badges[i] : 0, i == page, i);
                iy += UiTokens.Control + UiTokens.S1;
            }
            iy += UiTokens.S3;
            int face = UiDraw.Box(s, at, ix, iy, iw, UiTokens.Control, UiRole.Chart2, UiRole.None, UiTokens.Control / 2f, UiFlags.Circle);
            UiDraw.TextMiddle(s, at.In(face), ix, iy, UiTokens.Control, iw, UiStyle.Button, UiRole.OnAccent, m.Post, UiFlags.Centre | UiFlags.Label);
            UiDraw.Node(s, at, ix, iy, iw, UiTokens.Control, PostCommand, 0, face, false, -1, 0, UiAction.Alt);
            // The account at the foot, as the mockup has it: the face, the name and the handle; pressed, the profile.
            float ay = y + h - UiTokens.S4 - UiTokens.S12;
            if (ay > iy + UiTokens.Control + UiTokens.S4)
            {
                int card = UiDraw.Box(s, at, ix, ay, iw, UiTokens.S12, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inCard = at.In(card);
                Face(s, inCard, ix + UiTokens.S1, ay + UiTokens.S1, UiTokens.AvatarRow, m.MyName, m.MyHue, true, m.MyRing, false);
                float tx = ix + UiTokens.S1 + UiTokens.AvatarRow + UiTokens.S3, tw = ix + iw - tx - UiTokens.S2;
                Line(s, inCard, tx, ay + UiTokens.S1, tw, UiStyle.Title, UiRole.Text, m.MyName, UiFlags.Truncate);
                Line(s, inCard, tx, ay + UiTokens.S6, tw, UiStyle.Small, UiRole.Text2, m.MyHandle, UiFlags.Truncate);
                UiDraw.Node(s, at, ix, ay, iw, UiTokens.S12, AccountCommand, 0, card);
            }
        }

        private static void NavItem(UiScene s, UiAt at, float x, float y, float w, string label, UiIcon icon, int badge, bool chosen, int arg)
        {
            float h = UiTokens.Control;
            int face = UiDraw.Box(s, at, x, y, w, h, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, x + UiTokens.S3, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, icon, chosen ? UiRole.Text : UiRole.Text2, UiFlags.Middle, chosen);
            float tx = x + UiTokens.S3 + UiTokens.Icon + UiTokens.S3, end = x + w - UiTokens.S4;
            if (badge > 0)
            {
                end = x + w - UiTokens.S3 - BadgeOf(s, inside, x + w - UiTokens.S3, y + (h - UiTokens.Badge) / 2f, badge);
            }
            // A long name of a language's in the smaller strong words, as the kit's own sidebar has it, rather than cut short.
            UiStyle style = UiTextRules.Width(label, UiStyle.Nav) <= end - tx ? UiStyle.Nav : UiStyle.SmallStrong;
            UiDraw.TextMiddle(s, inside, tx, y, h, end - tx, style, UiRole.Text, label, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, w, h, NavCommand, arg, face);
        }

        // An unread count in Flare's orange at a right edge; returns its width with the gap before it.
        private static float BadgeOf(UiScene s, UiAt at, float right, float y, int count)
        {
            string words = count > 99 ? "99+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            float w = Math.Max(UiTokens.Badge, UiDraw.Snap(UiTextRules.Width(words, UiStyle.Badge) + UiTokens.S2 * 2f));
            int b = UiDraw.Box(s, at, right - w, y, w, UiTokens.Badge, UiRole.Chart2, UiRole.None, UiTokens.Badge / 2f, UiFlags.Circle | UiFlags.Middle | UiFlags.Right);
            UiDraw.TextMiddle(s, at.In(b), right - w, y, UiTokens.Badge, w, UiStyle.Badge, UiRole.OnAccent, words, UiFlags.Centre);
            return w + UiTokens.S2;
        }

        // ------------------------------------------------------------------------------------------------ the side column

        private static void Side(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float h)
        {
            float cx = x + UiTokens.S6, cw = w - UiTokens.S12, bottom = y + h - UiTokens.S4;
            float cy = y + UiTokens.S4;
            if (m.Page != Explore)
            {
                // Explore has its own field at the page's head: the side's pill is for the other pages, and pressed it types into Explore's.
                int search = UiDraw.Box(s, at, cx, cy, cw, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.Control / 2f, UiFlags.Circle);
                UiAt inSearch = at.In(search);
                UiDraw.Icon(s, inSearch, cx + UiTokens.S4, cy + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Search, UiRole.Text2, UiFlags.Middle);
                bool caret = UiDraw.Typing(s, FieldCommand, SearchField);
                float tx = cx + UiTokens.S4 + UiTokens.Icon + UiTokens.S3, room = cw - UiTokens.S12 - UiTokens.Icon, lead = caret ? UiDraw.CaretRoom : 0f;
                Hint(s, UiDraw.TextMiddle(s, inSearch, tx + lead, cy, UiTokens.Control, room - lead, UiStyle.Body, UiRole.Muted, m.SearchHint, UiFlags.Label | UiFlags.Truncate));
                if (caret)
                {
                    UiDraw.CaretAfter(s, inSearch, tx, cy, UiTokens.Control, null, UiStyle.Body, room);
                }
                UiDraw.Node(s, at, cx, cy, cw, UiTokens.Control, FieldCommand, SearchField, search);
                cy += UiTokens.Control + UiTokens.S4;
            }
            cy = Week(s, at, m, cx, cy, cw, bottom);
            // Who to follow keeps two rows in every language: the trends take what it leaves
            int whoRows = Math.Min(2, m.Who.Count);
            float whoRoom = whoRows > 0 ? UiTokens.S4 + L(UiStyle.Number) + UiTokens.S2 + whoRows * WhoRow + UiTokens.S2 + UiTokens.S4 : 0f;
            cy = Trends(s, at, m, cx, cy, cw, bottom - whoRoom);
            WhoToFollow(s, at, m, cx, cy, cw, bottom);
        }

        private const float WhoRow = 56f;

        // Your week: followers, reach and rating with what changed, the followers' line, and the way to the studio.
        private static float Week(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float bottom)
        {
            int n = Math.Min(3, m.WeekLabels.Length);
            const float lineH = 48f;
            float tileH = 88f + L(UiStyle.Small);   // the same in every language: a change of a longer language takes the second line, and the rail below holds the same rows
            // An account with nothing to count has no tiles and no title over them: its card is the way to the studio and nothing else.
            float title = n > 0 ? L(UiStyle.Number) : 0f, link = UiTokens.Control;
            float h = UiTokens.S4 + (n > 0 ? title + UiTokens.S3 + tileH + UiTokens.S3 : 0f) + (Drawn(m.Line) ? lineH + UiTokens.S2 : 0f) + link + UiTokens.S2;
            if (y + h > bottom)
            {
                return y;
            }
            int card = Card(s, at, x, y, w, h);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            if (n > 0)
            {
                Line(s, inCard, ix, cy, iw, UiStyle.Number, UiRole.Text, m.WeekTitle, UiFlags.Label | UiFlags.Truncate);
                cy += title + UiTokens.S3;
                float tw = UiDraw.SnapDown((iw - UiTokens.S2 * (n - 1)) / n);
                for (int i = 0; i < n; i++)
                {
                    StatTile(s, inCard, ix + i * (tw + UiTokens.S2), cy, tw, tileH, m.WeekLabels[i], At(m.WeekFigures, i), At(m.WeekDeltas, i), i < m.WeekTones.Length ? m.WeekTones[i] : 0);
                }
                cy += tileH + UiTokens.S3;
            }
            if (Drawn(m.Line))
            {
                UiDraw.Spark(s, inCard, ix, cy, iw, lineH, m.Line, UiRole.Chart2);
                cy += lineH + UiTokens.S2;
            }
            LinkLine(s, inCard, ix, cy, iw, m.StudioLink, StudioLinkCommand, 0);
            return y + h + UiTokens.S4;
        }

        /// <summary>A figure on its tile: its label in small capitals, the figure, and what changed in its tone.</summary>
        private static void StatTile(UiScene s, UiAt at, float x, float y, float w, float h, string label, string figure, string delta, int tone)
        {
            int tile = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusLarge);
            UiAt inTile = at.In(tile);
            float ix = x + UiTokens.S3, iw = w - UiTokens.S6, cy = y + UiTokens.S3;
            Line(s, inTile, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, label, UiFlags.Label | UiFlags.Truncate);
            cy += L(UiStyle.SectionHeader) + UiTokens.S1;
            Line(s, inTile, ix, cy, iw, UiStyle.Number, UiRole.Text, figure, UiFlags.Truncate);
            cy += L(UiStyle.Number) + UiTokens.S1;
            if (!string.IsNullOrEmpty(delta))
            {
                UiRole ink = tone > 0 ? UiRole.OkText : tone < 0 ? UiRole.DangerText : UiRole.Text2;
                Flow(s, inTile, ix, cy, iw, UiStyle.Small, ink, delta, 2);
            }
        }

        // Trending in the city: the district and kind, the tag and its posts.
        private static float Trends(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float bottom)
        {
            FlareList list = m.Trends;
            const float row = 64f;
            float title = L(UiStyle.Number);
            int n = list.Count;
            while (n > 0 && y + UiTokens.S4 + title + UiTokens.S2 + n * row + UiTokens.S2 > bottom)
            {
                n--;
            }
            if (n == 0)
            {
                return y;
            }
            float h = UiTokens.S4 + title + UiTokens.S2 + n * row + UiTokens.S2;
            int card = Card(s, at, x, y, w, h);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            Line(s, inCard, ix, cy, iw, UiStyle.Number, UiRole.Text, m.TrendTitle, UiFlags.Label | UiFlags.Truncate);
            cy += title + UiTokens.S2;
            for (int i = 0; i < n; i++)
            {
                ref FlareRow r = ref list.Rows[i];
                int face = UiDraw.Box(s, inCard, x + UiTokens.S2, cy, w - UiTokens.S4, row, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inRow = inCard.In(face);
                Line(s, inRow, ix, cy + UiTokens.S1, iw, UiStyle.Small, UiRole.Text2, r.Handle, UiFlags.Truncate);
                Line(s, inRow, ix, cy + UiTokens.S6, iw, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
                Line(s, inRow, ix, cy + UiTokens.S10 + UiTokens.S1, iw, UiStyle.Small, UiRole.Text2, r.Value, UiFlags.Truncate);
                UiDraw.Node(s, inCard, x + UiTokens.S2, cy, w - UiTokens.S4, row, TrendCommand, i, face);
                if (i + 1 < n)
                {
                    UiDraw.Box(s, inCard, ix, cy + row - UiTokens.Hairline, iw, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                }
                cy += row;
            }
            return y + h + UiTokens.S4;
        }

        // Who to follow: a face, the name, what they are and their followers, and Follow.
        private static void WhoToFollow(UiScene s, UiAt at, FlareFaceModel m, float x, float y, float w, float bottom)
        {
            FlareList list = m.Who;
            float row = WhoRow;
            float title = L(UiStyle.Number);
            int n = list.Count;
            while (n > 0 && y + UiTokens.S4 + title + UiTokens.S2 + n * row + UiTokens.S2 > bottom)
            {
                n--;
            }
            if (n == 0)
            {
                return;
            }
            // What the rows leave of the column is shared among them (a few pixels each at most), so the card ends at the column's foot and not a row's worth short of it.
            float left = bottom - (y + UiTokens.S4 + title + UiTokens.S2 + n * row + UiTokens.S2);
            row += Math.Min(UiTokens.S2, UiDraw.SnapDown(left / n));
            float h = UiTokens.S4 + title + UiTokens.S2 + n * row + UiTokens.S2;
            int card = Card(s, at, x, y, w, h);
            UiAt inCard = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S8, cy = y + UiTokens.S4;
            Line(s, inCard, ix, cy, iw, UiStyle.Number, UiRole.Text, m.WhoTitle, UiFlags.Label | UiFlags.Truncate);
            cy += title + UiTokens.S2;
            for (int i = 0; i < n; i++)
            {
                ref FlareRow r = ref list.Rows[i];
                bool following = (r.Flags & FlareRowFlags.Following) != 0;
                string word = following ? m.FollowingWord : m.FollowWord;
                float bw = PillWidth(word);
                float bx = ix + iw - bw;
                int face = UiDraw.Box(s, inCard, ix, cy, bx - UiTokens.S2 - ix, row, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
                UiAt inRow = inCard.In(face);
                Face(s, inRow, ix, cy + UiTokens.S2, UiTokens.AvatarRow, r.Name, r.Hue, (r.Flags & FlareRowFlags.Person) != 0, r.Ring, false);
                float tx = ix + UiTokens.AvatarRow + UiTokens.S3, tw = bx - UiTokens.S2 - tx;
                Line(s, inRow, tx, cy + UiTokens.S2, tw, UiStyle.Title, UiRole.Text, r.Name, UiFlags.Truncate);
                Line(s, inRow, tx, cy + UiTokens.S2 + UiTokens.S6, tw, UiStyle.Small, UiRole.Text2, r.Handle, UiFlags.Truncate);
                UiDraw.Node(s, inCard, ix, cy, bx - UiTokens.S2 - ix, row, WhoCommand, i, face);
                Pill(s, inCard, bx, cy + UiDraw.SnapDown((row - UiTokens.ControlSmall) / 2f), bw, word, following ? PillKind.Quiet : PillKind.Cyan, WhoFollowCommand, i);
                cy += row;
            }
        }

        // ------------------------------------------------------------------------------------------------ parts

        /// <summary>
        /// A person's face: painted from their name on the ring of their mood (no ring where the game has no mood for them); an account that is not a
        /// person (a business, the city's offices) keeps its initials on its colour. With <paramref name="across"/> the face may stand over its box's edge.
        /// </summary>
        public static int Face(UiScene s, UiAt at, float x, float y, float size, string name, float hue, bool person, UiRole ring, bool across)
        {
            if (!person)
            {
                int disc = UiDraw.Avatar(s, at, x, y, size, name, hue);
                s.Items[disc].Flags |= across ? UiFlags.Overflow : UiFlags.None;
                return disc;
            }
            float stroke = ring == UiRole.None ? 0f : UiTokens.Hairline * 2f;
            int face = UiDraw.Portrait(s, at, x + stroke, y + stroke, size - stroke * 2f, name, hue, ring);
            if (stroke > 0f)
            {
                s.Items[face].Flags |= UiFlags.Middle | UiFlags.Centre;   // inside its ring by a hairline step, not a grid one
            }
            if (across)
            {
                s.Items[face].Flags |= UiFlags.Overflow;
                if (ring != UiRole.None)
                {
                    s.Items[face - 1].Flags |= UiFlags.Overflow;
                }
            }
            return face;
        }

        /// <summary>The ring of a face for the social life's mood, -2 a rough day to +2 a great one.</summary>
        public static UiRole MoodRing(int mood) => mood > 0 ? UiRole.Ok : mood == 0 ? UiRole.Warn : mood < -1 ? UiRole.None : UiRole.Danger;

        /// <summary>The ring of a review's stars: the customer's mood on leaving, four and five content, three unsure, one and two cross.</summary>
        public static UiRole StarRing(int stars) => stars >= 4 ? UiRole.Ok : stars == 3 ? UiRole.Warn : UiRole.Danger;

        /// <summary>
        /// One line of words in a line box of its own, centred on its capline as every label of the kit is: a face whose ascent is taller than the kit's (Chinese, Korean)
        /// then stays in the band the line is given, not above it.
        /// </summary>
        public static int Line(UiScene s, UiAt at, float x, float y, float w, UiStyle style, UiRole ink, string text, UiFlags flags = UiFlags.None)
        {
            return UiDraw.TextMiddle(s, at, x, y, L(style), w, style, ink, text, flags);
        }

        /// <summary>
        /// Words of up to <paramref name="lines"/> lines: wrapped when they need more than one at this width, else one line in its own line box (<see cref="Line"/>), so a face of
        /// another ascent stays in its band where the words are short.
        /// </summary>
        public static int Flow(UiScene s, UiAt at, float x, float y, float w, UiStyle style, UiRole ink, string text, int lines)
        {
            if (string.IsNullOrEmpty(text) || UiTextRules.WrappedHeight(text, style, w, lines) <= UiTextRules.LineHeight(style) + 0.5f)
            {
                return Line(s, at, x, y, w, style, ink, text, UiFlags.Truncate);
            }
            return UiDraw.Text(s, at, x, y, Balanced(text, style, w, lines), style, ink, text, UiFlags.Wrap | UiFlags.Truncate, lines);
        }

        /// <summary>
        /// The width words wrap in so that their lines are of one length and the last is never a character or a word alone: the narrowest width that keeps the lines the words
        /// take at <paramref name="width"/> (a language that breaks between characters, Chinese, left "红。" on a line of its own), with a character's room to spare so the real
        /// text engine, which measures a little differently, still wraps to the same lines. A width at which a line would begin on a measure word, cut from the figure it counts
        /// ("100" / "条帖子"), is passed over for the next one that keeps the lines (the spaced languages cannot be cut there: <see cref="FlareFigures.Bind"/> joins a figure and its
        /// noun). The width itself for words that fit one line or would be cut at <paramref name="most"/>.
        /// </summary>
        public static float Balanced(string text, UiStyle style, float width, int most)
        {
            int lines = UiTextRules.Lines(text, style, width, 0, out float _);
            if (lines < 2 || lines > most)
            {
                return width;
            }
            float narrow = width;
            while (narrow - 4f > width * 0.5f && UiTextRules.Lines(text, style, narrow - 4f, 0, out float _) == lines)
            {
                narrow -= 4f;
            }
            for (float w = narrow; w <= width; w += 4f)
            {
                if (UiTextRules.Lines(text, style, w, 0, out float _) == lines && !CutsFigure(text, style, w, lines)
                    && UiTextRules.Lines(text, style, Math.Min(width, w + UiTokens.S2), 0, out float _) == lines && !CutsFigure(text, style, Math.Min(width, w + UiTokens.S2), lines))
                {
                    return Math.Min(width, w + UiTokens.S2);
                }
            }
            return Math.Min(width, narrow + UiTokens.S2);
        }

        // Whether a line of the words at a width begins on an ideograph straight after a digit or a per cent sign: the measure word parted from the figure it measures.
        private static bool CutsFigure(string text, UiStyle style, float width, int lines)
        {
            for (int k = 1; k < lines; k++)
            {
                int at = UiTextRules.LineStart(text, style, width, k);
                if (at > 0 && at < text.Length && UiTextRules.Wide(text[at]) && (char.IsDigit(text[at - 1]) || text[at - 1] == '%'))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A placeholder's words a step dimmer than the words typed over it.</summary>
        public static void Hint(UiScene s, int words)
        {
            s.Items[words].Alpha = HintAlpha;
        }

        private const float HintAlpha = 0.8f;   // the most the field's ground leaves a placeholder's words and still 4.5:1

        /// <summary>A line's height in a style, rounded up to the grid.</summary>
        public static float L(UiStyle style) => UiDraw.Snap(UiTextRules.LineHeight(style));

        private static string At(string[] a, int i) => a != null && i >= 0 && i < a.Length ? a[i] : null;

        private static bool Drawn(float[] line) => line != null && line.Length >= 2;

        /// <summary>A card of the side and the dashboards: the slate tile, no edge, no shadow.</summary>
        private static int Card(UiScene s, UiAt at, float x, float y, float w, float h)
        {
            return UiDraw.Box(s, at, x, y, w, h, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
        }

        /// <summary>Words that lead somewhere, in the accent's ink, on a target of their own.</summary>
        private static void LinkLine(UiScene s, UiAt at, float x, float y, float w, string words, int command, int arg)
        {
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            float tw = Math.Min(w, UiDraw.Snap(UiTextRules.Width(words, UiStyle.Title) + UiTokens.S4));
            int face = UiDraw.Box(s, at, x, y, tw, UiTokens.Control, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.TextMiddle(s, at.In(face), x, y, UiTokens.Control, tw, UiStyle.Title, UiRole.AccentText, words, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Node(s, at, x, y, tw, UiTokens.Control, command, arg, face);
        }

        public enum PillKind : byte { Line, Cyan, Orange, Quiet, Chosen }

        /// <summary>A pill's width for its words.</summary>
        public static float PillWidth(string words) => UiDraw.Snap(UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S4 * 2f);

        /// <summary>A pill with a glyph before its words: its width.</summary>
        public static float IconPillWidth(string words) => UiDraw.Snap(UiTokens.S3 + UiTokens.IconSmall + UiTokens.S1 + UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S4);

        /// <summary>
        /// A pill: outlined (Line), the field's slate face with the accent's ink (Cyan: the kit's secondary tone, a Follow or an Accept), Flare's orange (Orange, Post), quiet (Quiet,
        /// Following) or chosen in the accent (Chosen). Nothing is filled in the text's own near white, which outshone the primaries; the accent's soft fill is too faint a ground
        /// for the accent's small words (4.1 to 1 where the kit wants 4.5). Returns its target, or -1 for a pill that only shows.
        /// </summary>
        public static int Pill(UiScene s, UiAt at, float x, float y, float w, string words, PillKind kind, int command, int arg, UiIcon icon = UiIcon.None,
            bool off = false)
        {
            float h = UiTokens.ControlSmall;
            UiRole fill, edge, ink;
            switch (kind)
            {
                case PillKind.Cyan: fill = UiRole.Field; edge = UiRole.None; ink = UiRole.AccentText; break;
                case PillKind.Orange: fill = UiRole.Chart2; edge = UiRole.None; ink = UiRole.OnAccent; break;
                case PillKind.Quiet: fill = UiRole.Field; edge = UiRole.None; ink = UiRole.Text2; break;
                case PillKind.Chosen: fill = UiRole.Accent; edge = UiRole.None; ink = UiRole.OnAccent; break;
                default: fill = UiRole.None; edge = UiRole.Line; ink = UiRole.Text; break;
            }
            UiFlags flags = UiFlags.Circle | (kind == PillKind.Chosen ? UiFlags.Selected : UiFlags.None) | (off ? UiFlags.Disabled : UiFlags.None);
            int face = UiDraw.Box(s, at, x, y, w, h, fill, edge, h / 2f, flags);
            UiAt inside = at.In(face);
            float tx = x + UiTokens.S4, tw = w - UiTokens.S8;
            if (icon != UiIcon.None)
            {
                UiDraw.Icon(s, inside, x + UiTokens.S3, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, icon, ink, UiFlags.Middle);
                tx = x + UiTokens.S3 + UiTokens.IconSmall + UiTokens.S1;
                tw = x + w - UiTokens.S3 - tx;
            }
            UiDraw.TextMiddle(s, inside, tx, y, h, tw, UiStyle.SmallStrong, ink, words, (icon != UiIcon.None ? UiFlags.None : UiFlags.Centre) | UiFlags.Label | UiFlags.Truncate);
            return command >= 0 ? UiDraw.Node(s, at, x, y, w, h, command, arg, face, off) : -1;
        }

        /// <summary>An orange button of Flare's, Post or Confirm; returns its target, or -1 for one that only shows (command -1).</summary>
        private static int Orange(UiScene s, UiAt at, float x, float y, float w, float h, string words, int command, int arg, bool off = false)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, off ? UiRole.Field : UiRole.Chart2, UiRole.None, h / 2f, UiFlags.Circle | (off ? UiFlags.Disabled : UiFlags.None));
            UiDraw.TextMiddle(s, at.In(face), x, y, h, w, UiStyle.Button, off ? UiRole.Text2 : UiRole.OnAccent, words, UiFlags.Centre | UiFlags.Label);
            return command >= 0 ? UiDraw.Node(s, at, x, y, w, h, command, arg, face, off) : -1;
        }

        private static float ButtonWidth(string words) => UiDraw.Snap(UiTextRules.Width(words, UiStyle.Button) + UiTokens.S6 * 2f);

        /// <summary>Tabs across a width, each a share of it, the shown one in the text's ink over the accent's underline; returns the height.</summary>
        private static float TabRow(UiScene s, UiAt at, float x, float y, float w, string[] tabs, int shown)
        {
            int n = tabs != null ? tabs.Length : 0;
            float h = UiTokens.S12;
            if (n == 0)
            {
                return 0f;
            }
            float tw = UiDraw.SnapDown(w / n);
            for (int i = 0; i < n; i++)
            {
                bool on = i == shown;
                float tx = x + i * tw;
                float bw = i == n - 1 ? x + w - tx : tw;
                int face = UiDraw.Box(s, at, tx, y, bw, h, UiRole.None, UiRole.None, 0f, on ? UiFlags.Selected : UiFlags.None);
                UiAt inTab = at.In(face);
                UiDraw.TextMiddle(s, inTab, tx + UiTokens.S2, y, h - UiTokens.S1, bw - UiTokens.S4, UiStyle.Nav, on ? UiRole.Text : UiRole.Text2, tabs[i],
                    UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                if (on)
                {
                    float uw = Math.Min(bw - UiTokens.S4, UiDraw.Snap(UiTextRules.Width(tabs[i], UiStyle.Nav) + UiTokens.S4));
                    UiDraw.Box(s, inTab, tx + (bw - uw) / 2f, y + h - UiTokens.TabBar, uw, UiTokens.TabBar, UiRole.Accent, UiRole.None, UiTokens.TabBar / 2f, UiFlags.Centre);
                }
                UiDraw.Node(s, at, tx, y, bw, h, TabCommand, i, face);
            }
            UiDraw.Box(s, at, x, y + h, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            return h;
        }

        /// <summary>A text field of the page's: its words or its hint, typed into through the game's own text box.</summary>
        private static int TextField(UiScene s, UiAt at, float x, float y, float w, string text, string hint, int field, UiIcon icon)
        {
            float h = UiTokens.Control;
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, h / 2f, UiFlags.Circle);
            UiAt inside = at.In(face);
            float tx = x + UiTokens.S4;
            if (icon != UiIcon.None)
            {
                UiDraw.Icon(s, inside, x + UiTokens.S4, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, icon, UiRole.Text2, UiFlags.Middle);
                tx += UiTokens.Icon + UiTokens.S3;
            }
            bool typed = !string.IsNullOrEmpty(text);
            bool caret = UiDraw.Typing(s, FieldCommand, field);   // the field that takes the keys shows where the next letter goes
            float room = x + w - tx - UiTokens.S4, lead = caret && !typed ? UiDraw.CaretRoom : 0f;
            string shown = caret && typed ? UiDraw.Typed(text, UiStyle.Body, room) : typed ? text : hint;
            int words = UiDraw.TextMiddle(s, inside, tx + lead, y, h, room - lead, UiStyle.Body, typed ? UiRole.Text : UiRole.Muted, shown,
                typed ? UiFlags.Truncate : UiFlags.Label | UiFlags.Truncate);
            if (!typed)
            {
                Hint(s, words);
            }
            if (caret)
            {
                UiDraw.CaretAfter(s, inside, tx, y, h, typed ? shown : null, UiStyle.Body, room);
            }
            return UiDraw.Node(s, at, x, y, w, h, FieldCommand, field, face);
        }

        /// <summary>Targets on a layer so far: what is left of the layer's budget.</summary>
        public static int TargetsLeft(UiScene s, byte layer)
        {
            int used = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                used += s.Nodes[i].Layer == layer && !UiDraw.IsChrome(s.Nodes[i].Command) ? 1 : 0;
            }
            return MostTargets - used;
        }
    }
}
