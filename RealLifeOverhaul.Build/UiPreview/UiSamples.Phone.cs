using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

/// <summary>
/// The phone's states as the kit phone shows them in the game, laid out by its own screen (PhoneScreen) from a phone's
/// worth of apps: the game's eight, the mod's in their folders with the Planner and Flare kept first, and other mods'
/// buttons (one with its own picture, one whose picture cannot be read). Its home's pages, a folder's pages, badges,
/// one page only, the radio row, the private driver, what comes next full and empty, folded down. Plain English, as the
/// owner reads the previews.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>The sample picture of another mod's button, by handle (the preview reads it from that mod's files).</summary>
    public const int PictureBank = 1;

    /// <summary>A phone's worth of apps, as the game file reads them from the grid.</summary>
    public static PhoneItems PhoneApps(UiWords t, bool others, UiWords titles = null, string[] gameTitles = null, int moved = 0)
    {
        var items = new PhoneItems();
        items.Begin();
        titles = titles ?? ((key, english) => english);   // the buttons' words: data the game file reads, never the screen's own labels
        string[] games = { "Persona", "Contacts", "MyEmployees", "BizMan", "EconoView", "VoogleMaps", "MarketInsider", "Rivals" };
        gameTitles = gameTitles ?? new[] { "Persona", "Contacts", "My Employees", "BizMan", "EconoView", "Voogle Maps", "Market Insider", "Rivals" };
        int[] gameBadges = { 0, 5, 2, 0, 0, 0, 0, 0 };
        for (int i = 0; i < games.Length; i++)
        {
            App(items, games[i], gameTitles[i], PhoneItemKind.Game, gameBadges[i]);
        }
        string[][] mods =
        {
            new[] { "RealLifePlanner", "reallife_planner_app", "Planner", "2" }, new[] { "RealLifeFlare", "reallife_flare_app", "Flare", "3" },
            new[] { "RealLifeCalendar", "reallife_calendar_app", "Calendar", "0" }, new[] { "RealLifeCamera", "reallife_phonecam_app", "Camera", "0" },
            new[] { "RealLifeGallery", "reallife_gallery_app", "Gallery", "0" }, new[] { "RealLifeNewsstand", "reallife_ledger_online_app", "Newsstand", "1" },
            new[] { "RealLifeCareer", "reallife_career_app", "Career", "0" }, new[] { "RealLifeCampus", "reallife_campus_app", "Campus", "1" },
            new[] { "RealLifeOpenRoles", "reallife_roles_app", "Open Roles", "0" }, new[] { "RealLifeMarketplace", "reallife_hustle_app", "Marketplace", "0" },
            new[] { "RealLifeFinance", "reallife_econ_app", "Finance", "0" }, new[] { "RealLifeBanks", "reallife_banks_app", "Banks", "0" },
            new[] { "RealLifeOwnBank", "reallife_ob_app", "Your bank", "0" }, new[] { "RealLifeStocks", "reallife_mkt_app", "Stocks", "0" },
            new[] { "RealLifeEconomy", "reallife_econapp_app", "Economy", "0" }, new[] { "RealLifeGiving", "reallife_giving_app", "Giving", "0" }, new[] { "RealLifeCompany", "reallife_co_app", "Company", "1" },
            new[] { "RealLifeBrands", "reallife_brand_app", "Brands", "0" }, new[] { "RealLifeServices", "reallife_svc_app", "Services", "0" },
            new[] { "RealLifeVentures", "reallife_ven_app", "Ventures", "3" }, new[] { "RealLifeOnline", "reallife_online_app", "Web shop", "0" },
            new[] { "RealLifeEstate", "reallife_estate_app", "Property", "0" }, new[] { "RealLifeDealer", "reallife_dealer_app", "Car Showroom", "0" },
            new[] { "RealLifeBuilders", "reallife_bld_app", "Construction", "0" }, new[] { "RealLifeTechSector", "reallife_tech_app", "Tech", "0" },
            new[] { "RealLifeOverhaulUtilities", "reallife_util_app", "Utilities", "0" }, new[] { "RealLifeGuide", "reallife_guide_app", "Guide", "0" },
            new[] { "RealLifeSettings", "reallife_set_app", "Mod settings", "0" }, new[] { "RealLifeStaff", "reallife_staff_app", "Staff and roles", "0" },
        };
        foreach (string[] m in mods)
        {
            ref PhoneItem it = ref App(items, m[0], titles(m[1], m[2]), PhoneItemKind.Mod, int.Parse(m[3]));
            int place = PhoneLayoutRules.DefaultOf(m[0]);
            if (moved > 0 && place == PhoneLayoutRules.Work)
            {
                place = PhoneLayoutRules.Business;   // the player's own moves: work's apps put in Business, so it runs to a second page
                moved--;
            }
            it.First = place == PhoneLayoutRules.Dock;
            it.Folder = place >= 0 ? place : -1;
        }
        if (others)
        {
            ref PhoneItem bank = ref App(items, "EmpireBankApp", "Empire Bank", PhoneItemKind.Other, 0);
            bank.Picture = PictureBank;
            App(items, "BAMP_Chat", "Chat", PhoneItemKind.Other, 2);
        }
        items.End(true);
        return items;
    }

    private static ref PhoneItem App(PhoneItems items, string id, string title, PhoneItemKind kind, int badge)
    {
        ref PhoneItem it = ref items.Add();
        it.Id = id;
        it.Title = title;
        it.Kind = kind;
        it.Badge = badge;
        PhoneLooks.Of(id, out it.Icon, out it.Tint);
        return ref it;
    }

    /// <summary>What comes next, as the Planner and the Calendar give it.</summary>
    public static readonly UiInfoRow[] PhoneNextRows =
    {
        new UiInfoRow { Label = "16:00", Value = "Team meeting at Lantern Bay" }, new UiInfoRow { Label = "18:30", Value = "Gym at Iron Works" },
        new UiInfoRow { Label = "Tomorrow", Value = "Cinder Valley playtest" }, new UiInfoRow { Label = "Thursday", Value = "Rent due on 70 Fifth Avenue" },
        new UiInfoRow { Label = "Friday", Value = "Pay day for 26 staff" }, new UiInfoRow { Label = "Saturday", Value = "Coffee beans delivered to Bloom Café" },
    };

    /// <summary>The phone's screen in a state: a view (-1 home, else a folder) and its page, the radio, the driver, what comes next, folded.</summary>
    public static PhoneScreen Phone(UiWords t, int folder = -1, int page = 0, bool radio = false, bool driver = false, bool next = true, bool collapsed = false,
        bool others = true, PhoneItems items = null, UiInfoRow[] coming = null)
    {
        var screen = new PhoneScreen(items ?? PhoneApps(t, others), null, t);
        UiPhoneModel m = screen.Readings;
        m.Name = "BizPhone 1.0";
        m.Time = "14:32";
        m.Radio = radio;
        m.RadioTitle = "Big Apple FM";
        m.RadioTrack = "Late Night Drive";
        m.RadioVolume = 0.6f;
        m.RadioCanSkip = true;
        m.Driver = driver;
        m.DriverLabel = "Private driver";
        m.Collapsed = collapsed;
        m.Next = next ? coming ?? PhoneNextRows : new UiInfoRow[0];
        m.NextCount = -1;
        screen.Show(folder, page);
        return screen;
    }
}
