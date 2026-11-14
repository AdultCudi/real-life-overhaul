using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>An app the full menu can switch to, as the frame's app row and All apps show it.</summary>
    public struct UiShellApp
    {
        public string Id;          // its button's object name: the AppName, the mod's app id, another mod's own name
        public string Title;       // in the game's language, as its button shows it
        public UiIcon Icon;
        public UiRole Tint;
        public int Badge;
        public UiShellKind Kind;
        public int Folder;         // its All apps tab: UiShellApps.Money .. Setup, or Other for another mod's
        public bool InRow;         // on the app row: the player's bar (UiAppDeck), else the phone's first home page
        public bool DefaultRow;    // on the phone's first home page, as the source gave it: what the row is until the player chooses
        public bool Favourite;     // the player marked it in All apps (UiAppDeck)
        public int Picture;        // another mod's own app picture (UiDraw.PictureKey), shown instead of the glyph; 0 for none
        public PhoneItemFrom From; // where its own button stands: the phone's grid, or the full menu's row or the phone itself for another mod's
    }

    /// <summary>Whose an app is.</summary>
    public enum UiShellKind : byte
    {
        Game = 0,
        Mod,
        Other,
        Page    // a page of the overhaul's own on the kit, opened from All apps (no phone button)
    }

    /// <summary>
    /// Every app the full menu can switch to, for the status bar's app row (the phone's first home page in the player's
    /// order, then the first apps other mods put on the full menu's own row, then All apps) and the All apps panel (the
    /// phone's folders as tabs, every app in one), filled through one
    /// delegate (<see cref="Source"/>) by the game side: the phone's own model while the kit phone stands (<see cref="FromPhone"/>),
    /// else the full menu's own row (Shell/ShellApps). Which tab an app stands in is <see cref="FolderOf"/>: the
    /// folder the player keeps it in, else its kind's. A small provider gives an app's one status line (<see cref="Status"/>).
    /// Game-free. Cost: a list filled when the full menu opens and when the source says it changed; nothing a frame.
    /// </summary>
    public static class UiShellApps
    {
        public const int Money = 0, Work = 1, Life = 2, Business = 3, Setup = 4;   // the phone's folders, in PhoneLayoutRules' order (checked offline)
        public const int Other = 5;                                                 // another mod's apps: a tab of their own
        public const int Folders = Other + 1;

        /// <summary>The All apps tab for "Other mods": the key of its name.</summary>
        public const string OtherKey = "reallife_ui_shell_other_mods";

        // Each folder's name, as the phone's own folder tiles say it (PhoneLayoutRules.FolderKeys), then Other mods.
        private static readonly string[] FolderKeys =
        {
            "reallife_phone_folder_money", "reallife_phone_folder_work", "reallife_phone_folder_life", "reallife_phone_folder_business", "reallife_phone_folder_setup", OtherKey
        };

        /// <summary>Fills the list in the phone's order and returns its version (it moves whenever the apps, their order or a badge change).</summary>
        public static Func<List<UiShellApp>, int> Source;

        /// <summary>An app's one line of what waits for the player ("3 unread"), by its id and its news, or null: the apps that give one answer here.</summary>
        public static Func<string, int, string> Status;

        /// <summary>Every app, in the phone's order, as last filled.</summary>
        public static readonly List<UiShellApp> Items = new List<UiShellApp>(64);

        /// <summary>The fill's version: the frame lays the row out again only when it moved.</summary>
        public static int Version { get; private set; } = -1;

        /// <summary>The apps' indexes in the order the row shows them (the player's bar, else the phone's first page), as last filled.</summary>
        public static readonly List<int> Row = new List<int>(16);

        /// <summary>How many of the row's apps stood on the strip when it was last laid out: the strip's room may cut the bar shorter than the player chose. 0 until a bar has been laid.</summary>
        public static int RowShown;

        /// <summary>The player changed the bar or the favourites: whatever draws the row lays it out again.</summary>
        public static event Action Rearranged;

        private static int _source;

        /// <summary>Asks the source again; true when anything changed.</summary>
        public static bool Refresh()
        {
            if (Source == null)
            {
                return false;
            }
            Items.Clear();
            _source = Source(Items);
            for (int i = 0; i < Items.Count; i++)
            {
                UiShellApp a = Items[i];
                a.DefaultRow = a.InRow;
                Items[i] = a;
            }
            UiAppDeck.Current.Apply(Items, Row);
            int version = Mixed();
            bool changed = version != Version;
            Version = version;
            return changed;
        }

        /// <summary>
        /// The deck changed: its flags and the row's order put on the apps again without asking the source, the version moved,
        /// and (unless a drag is still going) whatever draws the row told to lay it out again.
        /// </summary>
        public static void Rearrange(bool tell = true)
        {
            UiAppDeck.Current.Apply(Items, Row);
            Version = Mixed();
            if (tell)
            {
                Rearranged?.Invoke();
            }
        }

        private static int Mixed() => unchecked(_source * 31 + UiAppDeck.Current.Revision);

        private static int[] _home = new int[64];
        private static int[] _inFolder = new int[64];

        /// <summary>
        /// The phone's own model (PhoneItems) as the frame's list, so the app row, All apps and the phone show one order, one
        /// glyph and one colour an app: the home screen in the player's order, each folder's apps where its tile stands,
        /// other mods' buttons with their own pictures. The row is the home screen's first apps, as many as the phone's first
        /// page holds (a folder's tile is no app: All apps holds its apps), then the first of the apps another mod put on
        /// the full menu's own row (UiForeignApps.RowMost): the kit's row stands for that row, so what a mod put there is
        /// not lost from it. Returns a version that moves with the phone's.
        /// </summary>
        public static int FromPhone(PhoneItems items, List<UiShellApp> into, int rowMost)
        {
            if (_home.Length < items.Count + PhoneLayoutRules.FolderCount)
            {
                _home = new int[(items.Count + PhoneLayoutRules.FolderCount) * 2];
                _inFolder = new int[_home.Length];
            }
            int n = items.Home(_home), inRow = 0, carried = 0;
            for (int e = 0; e < n; e++)
            {
                int entry = _home[e];
                if (entry >= 0)
                {
                    bool first = inRow < rowMost;
                    bool kept = !first && UiForeignApps.OnRow(KindOf(items.Items[entry].Kind), items.Items[entry].From, carried);
                    carried += kept ? 1 : 0;
                    FromItem(into, items, entry, first || kept);
                    inRow++;
                    continue;
                }
                int m = items.InFolder(-1 - entry, _inFolder);
                for (int k = 0; k < m; k++)
                {
                    FromItem(into, items, _inFolder[k], false);
                }
            }
            return unchecked(17 * 31 + items.Version);
        }

        private static void FromItem(List<UiShellApp> into, PhoneItems items, int index, bool inRow)
        {
            ref PhoneItem it = ref items.Items[index];
            UiShellKind kind = KindOf(it.Kind);
            UiIcon icon = it.Icon != UiIcon.None ? it.Icon : PhoneLooks.OtherMods;
            into.Add(new UiShellApp
            {
                Id = it.Id, Title = it.Title, Icon = icon, Tint = it.Tint != UiRole.None ? it.Tint : UiIconTints.Of(icon), Badge = it.Badge, Kind = kind, InRow = inRow, DefaultRow = inRow,
                Folder = FolderOf(it.Id, kind, it.Folder, PhoneLayoutRules.DefaultOf(it.Id)), Picture = it.Picture, From = it.From
            });
        }

        private static UiShellKind KindOf(PhoneItemKind kind) => kind == PhoneItemKind.Game ? UiShellKind.Game : kind == PhoneItemKind.Mod ? UiShellKind.Mod : UiShellKind.Other;

        /// <summary>A folder's glyph on its tab: the phone's own folders' (PhoneLooks), Other mods' its tab's.</summary>
        public static UiIcon FolderIcon(int folder) => PhoneLooks.Folder(folder, out UiIcon icon, out UiRole _) ? icon : folder == Other ? PhoneLooks.OtherMods : UiIcon.Grid;

        /// <summary>A folder's name's key.</summary>
        public static string FolderKey(int folder) => folder >= 0 && folder < FolderKeys.Length ? FolderKeys[folder] : OtherKey;

        /// <summary>
        /// An app's glyph, one an app, in its own colour (UiIconTints): the phone's settled one (PhoneLooks) for every app it
        /// knows, Other mods' glyph for another mod's (its own picture where the phone has one), the overhaul's pages theirs.
        /// </summary>
        public static UiIcon IconOf(string id, UiShellKind kind)
        {
            if (kind == UiShellKind.Other)
            {
                return PhoneLooks.OtherMods;
            }
            UiIcon settled = PhoneLooks.Glyph(id);
            if (settled != UiIcon.None)
            {
                return settled;   // the phone's one glyph an app (PhoneLooks): the same on the phone, the row and All apps
            }
            switch (id)
            {
                case "Persona": return UiIcon.Person;
                case "Contacts": return UiIcon.AddressBook;
                case "MyEmployees": return UiIcon.IdBadge;
                case "BizMan": return UiIcon.Building;
                case "EconoView": return UiIcon.Chart;
                case "VoogleMaps": return UiIcon.MapTrifold;
                case "MarketInsider": return UiIcon.ChartPie;
                case "Rivals": return UiIcon.Flag;
                case "RealLifePlanner": return UiIcon.Clock;
                case "RealLifeFlare": return UiIcon.Fire;
                case "RealLifeCalendar": return UiIcon.Calendar;
                case "RealLifeCamera": return UiIcon.Camera;
                case "RealLifeGallery": return UiIcon.Image;
                case "RealLifeNewsstand": return UiIcon.Tag;
                case "RealLifeLedger": return UiIcon.Newspaper;
                case "RealLifeCareer": return UiIcon.Briefcase;
                case "RealLifeCampus": return UiIcon.Cap;
                case "RealLifeOpenRoles": return UiIcon.IdCard;
                case "RealLifeMarketplace": return UiIcon.ShoppingCart;
                case "RealLifeFinance": return UiIcon.Wallet;
                case "RealLifeBanks": return UiIcon.Bank;
                case "RealLifeOwnBank": return UiIcon.PiggyBank;
                case "RealLifeStocks": return UiIcon.ChartLineUp;
                case "RealLifeEconomy": return UiIcon.Globe;
                case "RealLifeGiving": return UiIcon.Percent;
                case "RealLifeInsurance": return UiIcon.Lock;
                case "RealLifeHealth": return UiIcon.Thermometer;
                case "RealLifeDynasty": return UiIcon.Key;
                case "RealLifeCompany": return UiIcon.Presentation;
                case "RealLifeSuppliers": return UiIcon.Truck;
                case "RealLifeBrands": return UiIcon.Trophy;
                case "RealLifeServices": return UiIcon.Handshake;
                case "RealLifeVentures": return UiIcon.Rocket;
                case "RealLifeOnline": return UiIcon.ShoppingBag;
                case "RealLifeEstate": return UiIcon.Home;
                case "RealLifeDealer": return UiIcon.Car;
                case "RealLifeBuilders": return UiIcon.Hammer;
                case "RealLifeTechSector": return UiIcon.Cpu;
                case "RealLifeOverhaulUtilities": return UiIcon.Lightning;
                case "RealLifeGuide": return UiIcon.Info;
                case "RealLifeSettings": return UiIcon.Gear;
                case "RealLifeStaff": return UiIcon.People;
                case "RealLifeParties": return UiIcon.ForkKnife;
                case ShellPeople: return UiIcon.People;
                case ShellFirm: return UiIcon.GameController;
                default: return UiIcon.Store;
            }
        }

        /// <summary>The overhaul's pages the frame opens from All apps (Shell/ShellPages): no phone button, a panel among the apps.</summary>
        public const string ShellPeople = "RealLifePagePeople", ShellFirm = "RealLifePageFirm";

        /// <summary>
        /// The tab an app stands in: the folder the player keeps it in; else its kind's (the game's eight by what they are,
        /// the mod's by the phone's own defaults, the old dock's two by theirs); another mod's in Other.
        /// </summary>
        public static int FolderOf(string id, UiShellKind kind, int playerFolder, int kindFolder = -1)
        {
            if (kind == UiShellKind.Other)
            {
                return Other;
            }
            if (playerFolder >= Money && playerFolder <= Setup)
            {
                return playerFolder;
            }
            switch (id)
            {
                case "Persona": case "Contacts": case "VoogleMaps": return Life;
                case "EconoView": return Money;
                case "MyEmployees": case "BizMan": case "MarketInsider": case "Rivals": return Business;
                case "RealLifePlanner": return Work;
                case "RealLifeFlare": case ShellPeople: return Life;
                case ShellFirm: return Business;
            }
            return kindFolder >= Money && kindFolder <= Setup ? kindFolder : Life;
        }
    }
}
