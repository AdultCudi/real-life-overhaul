using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Designs;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Shops;

// The ready-made designs (Scripts/Designs): the folders the game's installation firm reads, the names, the list of the mod's
// files, the Car Showroom's cut round its lot on made-up rooms, and then the plan the game's start makes from the installed
// game's own design files: every business type of the mod's has a design for every size its building has designs for, every
// design places only the game's own furniture inside the room, a showroom's lot is clear, and its price is the game's (the
// furniture of a design of the game's, or fewer of it, at the game's market prices; the fee is the game's by floor area).
internal static partial class Program
{
    // The game's StreamingAssets, beside the Managed folder that BA_MANAGED names when the game is elsewhere (as ContractCheck).
    private static readonly string DesignGameFiles = System.IO.Path.GetFullPath(System.IO.Path.Combine(GameManagedFolder(), "..", "StreamingAssets"));
    private static readonly string DesignScripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    private static readonly string DesignLocales = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));

    private static void Designs()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        DesignTypes();
        DesignNames();
        DesignList();
        DesignCuts();
        DesignShopCuts();
        DesignTexts();
        DesignGame();
        DesignSandbox();
        Console.WriteLine("  Designs: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // Every business type the mod registers with the game is in the catalogue, by its saved name, and nothing else is.
    private static void DesignTypes()
    {
        var registered = new HashSet<string>(StringComparer.Ordinal);
        int callers = 0;
        foreach (string file in Directory.GetFiles(DesignScripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (!text.Contains("ModdingAPI.RegisterModBusinessType("))
            {
                continue;
            }
            callers++;
            foreach (Match m in Regex.Matches(text, "\"(reallife:businesstype_[a-z]+)\""))
            {
                registered.Add(m.Groups[1].Value);
            }
            Match prefix = Regex.Match(text, "const string Prefix = \"(reallife:businesstype_)\"");
            foreach (Match m in Regex.Matches(text, @"= Prefix \+ ""([a-z]+)"""))
            {
                registered.Add((prefix.Success ? prefix.Groups[1].Value : "?") + m.Groups[1].Value);
            }
            if (text.Contains("DealerCatalogue.TypeName"))
            {
                registered.Add(DealerCatalogue.TypeName);
            }
            if (text.Contains("BranchCatalogue.TypeName"))
            {
                registered.Add(BranchCatalogue.TypeName);
            }
            if (text.Contains("ShopsCatalogue.Kinds"))
            {
                foreach (ShopKind shop in ShopsCatalogue.Kinds)
                {
                    registered.Add(shop.Type);
                }
            }
        }
        Check(callers == 5, "designs: the business types are registered in five files (Tech/Firms/TechTypes, Careers/AcademyType, Economy/Dealer/DealerType, Economy/OwnBank/Branch/BranchType, Shops/ShopsGame), found " + callers);
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DesignKind kind in DesignCatalogue.Kinds)
        {
            Check(kinds.Add(kind.Type), "designs: " + kind.Type + " is in the catalogue twice");
            Check(folders.Add(DesignRules.FolderOf(kind.Type)), "designs: two types share the folder " + DesignRules.FolderOf(kind.Type));
            Check(kind.Donors.Length > 0, "designs: " + kind.Type + " has no designs of the game's to be made from");
            Check(kind.Building == DesignCatalogue.Office || kind.Building == DesignCatalogue.Retail, "designs: " + kind.Type + " is in a building the firms do not serve");
        }
        foreach (string type in registered)
        {
            Check(kinds.Contains(type), "designs: the business type " + type + " the mod registers has no ready-made designs");
        }
        Check(kinds.Count == registered.Count && kinds.Count == 11, "designs: the catalogue holds the mod's eleven business types (" + kinds.Count + " in it, " + registered.Count + " registered)");
        foreach (ShopKind shop in ShopsCatalogue.Kinds)
        {
            int at = Array.FindIndex(DesignCatalogue.Kinds, k => k.Type == shop.Type);
            Check(at >= 0 && DesignCatalogue.Kinds[at].Way == DesignWay.Stock && DesignCatalogue.Kinds[at].Building == ShopsCatalogue.BuildingType,
                "designs: the " + DesignRules.FolderOf(shop.Type) + " is offered its donor's shop designs without the shelves for goods it does not sell, in the building it is founded in");
        }
        int branchKind = Array.FindIndex(DesignCatalogue.Kinds, k => k.Type == BranchCatalogue.TypeName);
        Check(branchKind >= 0 && DesignCatalogue.Kinds[branchKind].Way == DesignWay.Branch && DesignCatalogue.Kinds[branchKind].Building == BranchCatalogue.BuildingType,
            "designs: the bank branch is cut to a bank's furniture, in the building it is opened in");
        Check(DesignCatalogue.Kinds[DesignCatalogue.Kinds.Length - 1].Type == DealerCatalogue.TypeName
              && DesignCatalogue.Kinds[DesignCatalogue.Kinds.Length - 1].Way == DesignWay.Showroom
              && DesignCatalogue.Kinds[DesignCatalogue.Kinds.Length - 1].Building == DealerCatalogue.BuildingType,
            "designs: the Car Showroom is cut for its lot, in the building it is founded in");
    }

    // The game's own naming of its folders, sizes and designs.
    private static void DesignNames()
    {
        Check(DesignRules.FolderOf("ba:buildingtype_office") == "office" && DesignRules.FolderOf("ba:buildingtype_retail") == "retail",
            "designs: a building type's folder is its id's second piece, as the game's GetIdWithoutType");
        Check(DesignRules.FolderOf("reallife:businesstype_gamestudio") == "gamestudio" && DesignRules.FolderOf("reallife:businesstype_academy") == "academy",
            "designs: the tech firms' and the academy's folders");
        Check(DesignRules.FolderOf(DealerCatalogue.TypeName) == "businesstype", "designs: the Car Showroom's saved name has no ':', so the game reads its folder as 'businesstype'");
        Check(DesignRules.FolderOf(BranchCatalogue.TypeName) == "bankbranch", "designs: the bank branch's folder is 'bankbranch', apart from the showroom's");
        Check(DesignRules.FolderOf(ShopsCatalogue.IceCream) == "icecreamparlour" && DesignRules.FolderOf(ShopsCatalogue.TeaBar) == "teabar"
              && DesignRules.FolderOf(ShopsCatalogue.Tobacconist) == "tobacconist", "designs: the new kinds of shop's folders");
        Check(DesignRules.FolderOf("nounderscore") == string.Empty && DesignRules.FolderOf(null) == string.Empty, "designs: an id with no '_' has no folder");
        Check(DesignRules.IsSize("C1") && DesignRules.IsSize("K1") && DesignRules.IsSize("T16") && DesignRules.IsSize("Boat1"), "designs: the game's size folders are sizes");
        Check(!DesignRules.IsSize("c1") && !DesignRules.IsSize("C") && !DesignRules.IsSize("C1x") && !DesignRules.IsSize("") && !DesignRules.IsSize("LawFirm"),
            "designs: a business's folder or a stray name is not a size");
        Check(DesignRules.SizeId("C1", out int v1) == "ba:buildingsize_c" && v1 == 1 && DesignRules.SizeId("T16", out int v16) == "ba:buildingsize_t" && v16 == 16,
            "designs: a size folder gives back the game's size id and version (BuildingSizeInfo.ToString the other way)");
        Check(DesignRules.SizeId("LawFirm", out int _) == null, "designs: no size id for a folder that is not a size");
        Check(DesignRules.NameOf(DonorRoot.Installer, "Modern.json") == "Modern" && DesignRules.NameOf(DonorRoot.Installer, "rustic.json") == "rustic",
            "designs: an installer design is offered under its file's name, as the game's are");
        Check(DesignRules.NameOf(DonorRoot.Layouts, "HamptonsCarDealershipLuxury.json") == "Luxury" && DesignRules.NameOf(DonorRoot.Layouts, "GarmentDistrictCarDealershipCheap.json") == "Cheap",
            "designs: a dealership's layout is offered under the style its name ends in");
        Check(DesignRules.NameOf(DonorRoot.Layouts, "IndustryCityCarDealershipTrucks.json") == null && DesignRules.NameOf(DonorRoot.Installer, "Modern.txt") == null
              && DesignRules.NameOf(DonorRoot.Layouts, "Luxury.json") == null,
            "designs: a layout of no known style, or a file that is not a design, is not offered");

        // The first of each name, the case aside, in the catalogue's donor order.
        var sources = new List<DesignSource>
        {
            Source(1, 1, "C1", "Modern", "InteriorDesigns/Office/C1/WebDevelopmentAgency/Modern.json"),
            Source(1, 0, "C1", "modern", "InteriorDesigns/Office/C1/GraphicDesigner/modern.json"),
            Source(1, 0, "C1", "Rustic", "InteriorDesigns/Office/C1/GraphicDesigner/Rustic.json"),
            Source(1, 1, "C1", "Sky", "InteriorDesigns/Office/C1/WebDevelopmentAgency/Sky.json")
        };
        DesignRules.Order(sources);
        var chosen = new List<DesignSource>();
        DesignRules.Copies(sources, chosen);
        Check(chosen.Count == 3 && chosen[0].Name == "modern" && chosen[0].Donor == 0 && chosen[1].Name == "Rustic" && chosen[2].Name == "Sky",
            "designs: a name two donors share is the first donor's, and every other name is offered once (" + chosen.Count + ")");
    }

    private static DesignSource Source(int kind, int donor, string size, string name, string path, DonorRoot root = DonorRoot.Installer)
    {
        return new DesignSource { Kind = kind, Donor = donor, Size = size, Name = name, Path = path, Root = root, Length = path.Length, Ticks = 1 };
    }

    // The list of the mod's files: its line, what it holds, what it may name, the stamp.
    private static void DesignList()
    {
        var list = new DesignManifest { Stamp = "0123456789abcdef" };
        list.Files["Office/C1/gamestudio/Modern.json"] = 12345;
        list.Files["Retail/M1/businesstype/Luxury.json"] = 678;
        DesignManifest back = DesignManifest.Read(list.Write());
        Check(back.Stamp == list.Stamp && back.Files.Count == 2 && back.Files["Office/C1/gamestudio/Modern.json"] == 12345 && back.Holds("Retail/M1/businesstype/Luxury.json"),
            "designs: the list of the mod's files reads back as written");
        Check(DesignManifest.Read(list.Write().Replace("\n", "\r\n")).Files.Count == 2, "designs: the list reads with Windows line ends");
        Check(DesignManifest.Read(null).Files.Count == 0 && DesignManifest.Read("garbage").Files.Count == 0 && DesignManifest.Read("v=2\nstamp=x\nOffice/C1/gamestudio/Modern.json|1\n").Files.Count == 0,
            "designs: no list, a damaged one or another version's holds nothing (so nothing of anybody's is taken away)");
        Check(DesignRules.Ours("Office/C1/gamestudio/Modern.json") && DesignRules.Ours("Retail/A1/businesstype/Classic.json") && DesignRules.Ours("office/K1/academy/Dark.json"),
            "designs: the mod's own designs are its own");
        Check(!DesignRules.Ours("Office/C1/LawFirm/Modern.json") && !DesignRules.Ours("Retail/C1/gamestudio/Modern.json") && !DesignRules.Ours("Office/C1/../../x.json")
              && !DesignRules.Ours("Office/C1/gamestudio/Modern.txt") && !DesignRules.Ours("Office/gamestudio/Modern.json") && !DesignRules.Ours("Office\\C1\\gamestudio\\Modern.json")
              && !DesignRules.Ours("C:/Office/C1/gamestudio/Modern.json") && !DesignRules.Ours("Office/C1/gamestudio/.json"),
            "designs: a game's design, a type in the wrong building, a path that climbs out or is not a design is never the mod's to touch");
        Check(DesignManifest.Read("v=1\nstamp=s\nOffice/C1/LawFirm/Modern.json|5\nOffice/C1/academy/Dark.json|7\n").Files.Count == 1, "designs: a list naming a file not the mod's drops it");

        var sources = new List<DesignSource> { Source(0, 0, "C1", "Modern", "InteriorDesigns/Office/C1/WebDevelopmentAgency/Modern.json") };
        string stamp = DesignRules.Stamp(sources);
        Check(stamp == DesignRules.Stamp(sources) && stamp.Length == 16, "designs: the stamp is the same for the same files");
        sources[0].Ticks = 2;
        string touched = DesignRules.Stamp(sources);
        sources[0].Ticks = 1;
        sources[0].Length++;
        string longer = DesignRules.Stamp(sources);
        Check(touched != stamp && longer != stamp && longer != touched, "designs: the stamp changes when a file of the game's changes");
        sources.Add(Source(0, 0, "C2", "Sky", "InteriorDesigns/Office/C2/WebDevelopmentAgency/Sky.json"));
        Check(DesignRules.Stamp(sources) != longer, "designs: the stamp changes when the game has another design");
    }

    // The Car Showroom's cut on a made-up room: what is kept, what goes, what stands on what, and which designs are offered.
    private static void DesignCuts()
    {
        var room = new FloorBox { MinX = 0, MaxX = 1500, MinZ = 0, MaxZ = 1500, Any = true };
        var lot = new List<FloorBox>();
        DesignRules.Lot(room, lot);
        var offsets = new List<int>();
        int count = DealerRules.Layout(1500, 1500, offsets, out bool across);
        bool covered = count > 0;
        for (int s = 0; s < count; s++)
        {
            covered &= InAny(lot, 750 + offsets[2 * s], 750 + offsets[2 * s + 1]);
            int hx = (across ? DealerCatalogue.SpotDepth : DealerCatalogue.SpotWidth) / 2, hz = (across ? DealerCatalogue.SpotWidth : DealerCatalogue.SpotDepth) / 2;
            covered &= InAny(lot, 750 + offsets[2 * s] + hx, 750 + offsets[2 * s + 1] + hz) && InAny(lot, 750 + offsets[2 * s] - hx, 750 + offsets[2 * s + 1] - hz);
        }
        Check(covered, "designs: the lot kept clear holds every car's whole place as the showroom lays them out (" + count + " places)");
        Check(!InAny(lot, 30, 30) && !InAny(lot, 1470, 1470), "designs: the corners of a 15 m room are not the lot's");
        bool slack = true;
        foreach (int dw in new[] { -DesignCatalogue.Slack, DesignCatalogue.Slack })
        {
            int n = DealerRules.Layout(1500 + dw, 1500 - dw, offsets, out bool _);
            for (int s = 0; s < n; s++)
            {
                slack &= InAny(lot, 750 + offsets[2 * s], 750 + offsets[2 * s + 1]);
            }
        }
        Check(slack, "designs: a floor a little wider or deeper than the designs reach has its lot inside the cleared floor too");
        var none = new List<FloorBox>();
        DesignRules.Lot(FloorBox.None, none);
        Check(none.Count == 0, "designs: an empty room has no lot");

        var items = new List<DesignItem>
        {
            Item("counter", "counter1", 100, 0, 100),
            Item("till", "cashregister", 100, 90, 100, "counter"),
            Item("shelf", "storageshelf", 100, 0, 1400),
            Item("sofa", "cornersofa01", 750, 0, 750),
            Item("lamp", "ceilinglamptube", 750, 450, 750),
            Item("tv", "wallmountedtv", 750, 250, 700),
            Item("wc", "toiletstall", 1400, 0, 100),
            Item("plant", "groundplant01", 1400, 0, 1400),
            Item("chair1", "regularchair", 1300, 0, 1300),
            Item("chair2", "armchair1", 200, 0, 1300),
            Item("chair3", "armchair1", 300, 0, 1300),
            Item("desk", "officedesk1", 750, 0, 760),
            Item("pc", "computer", 750, 80, 760, "desk"),
            Item("car", "bima320showcase", 400, 0, 750),
            Item("stray", "groundplant02", 0, 0, 0),
            Item("counter", "counter2", 120, 0, 120)
        };
        items[0].Children.Add("till");
        items[11].Children.Add("pc");
        ShowroomCut cut = DesignRules.Cut(items, lot);
        Check(cut.Kept.Contains("counter") && cut.Kept.Contains("till") && cut.Kept.Contains("wc") && cut.Kept.Contains("plant") && cut.Tills == 1,
            "designs: the counter by the wall and its till, the washroom and a plant in a corner are kept");
        Check(!cut.Kept.Contains("sofa") && !cut.Kept.Contains("desk") && cut.InLot >= 2, "designs: a sofa and a desk where the cars go are left out");
        Check(!cut.Kept.Contains("pc"), "designs: what stands on something left out goes with it");
        Check(cut.Kept.Contains("lamp") && cut.Kept.Contains("tv"), "designs: a ceiling lamp and a screen on the wall above the lot are kept");
        Check(!cut.Kept.Contains("shelf") && !cut.Kept.Contains("car") && !cut.Kept.Contains("stray"), "designs: shelves, the game's cars on show and a stray at the origin are left out");
        Check(cut.Keep.Length == items.Count && cut.Keep[0] && !cut.Keep[items.Count - 1], "designs: an id a design gives twice is kept once, the first");
        Check(cut.Furniture == 8 && cut.Toilets == 1 && cut.Valid, "designs: the cut counts its furniture without lamps (" + cut.Furniture + ") and is offered");
        items.RemoveAt(6);
        Check(!DesignRules.Cut(items, lot).Valid, "designs: a showroom design with no washroom is not offered");

        // The offer: the game's own dealerships, then those with a till, then the rest; one of each name; at most ShowroomMost.
        var sources = new List<DesignSource>();
        var cuts = new List<ShowroomCut>();
        void Add(string name, DonorRoot root, int tills, bool valid)
        {
            sources.Add(Source(6, root == DonorRoot.Layouts ? 0 : 1, "C1", name, "x/" + name + sources.Count + ".json", root));
            var c = new ShowroomCut { Furniture = valid ? DesignCatalogue.ShowroomLeast : 1, Toilets = 1, Tills = tills };
            cuts.Add(c);
        }
        Add("Classic", DonorRoot.Installer, 0, true);
        Add("Modern", DonorRoot.Installer, 1, true);
        Add("Cheap", DonorRoot.Layouts, 0, true);
        Add("Modern", DonorRoot.Installer, 2, true);
        Add("Dark", DonorRoot.Installer, 1, false);
        Add("Sky", DonorRoot.Installer, 1, true);
        Add("Rustic", DonorRoot.Installer, 0, true);
        Add("Lemon", DonorRoot.Installer, 1, true);
        var picks = new List<int>();
        DesignRules.Showrooms(sources, cuts, picks);
        Check(picks.Count == DesignCatalogue.ShowroomMost && sources[picks[0]].Name == "Cheap" && sources[picks[1]].Name == "Modern" && picks[1] == 1
              && sources[picks[2]].Name == "Sky" && sources[picks[3]].Name == "Lemon",
            "designs: a showroom is offered the game's dealership first, then designs with a till, one of a name, none that is not valid, at most " + DesignCatalogue.ShowroomMost);
        cuts[1] = null;
        DesignRules.Showrooms(sources, cuts, picks);
        Check(picks.Count == DesignCatalogue.ShowroomMost && sources[picks[1]].Name == "Modern" && picks[1] == 3, "designs: a design that could not be read is passed over for the next of its name");
        BranchCuts();
    }

    // The bank branch's cut: no lot, the bank's furniture kept, a till for its teller, seats to wait on first.
    private static void BranchCuts()
    {
        var items = new List<DesignItem>
        {
            Item("counter", "counter1", 100, 0, 100),
            Item("till", "cashregister", 100, 90, 100, "counter"),
            Item("shelf", "storageshelf", 100, 0, 1400),
            Item("sofa", "cornersofa01", 750, 0, 750),
            Item("barrier", "beltbarrier", 400, 0, 400),
            Item("wc", "toiletstall", 1400, 0, 100),
            Item("plant", "groundplant01", 1400, 0, 1400),
            Item("chair", "waitingroomchairs", 1300, 0, 1300),
            Item("desk", "officedesk1", 750, 0, 760),
            Item("pc", "computer", 750, 80, 760, "desk"),
            Item("case", "displaycase", 500, 0, 500)
        };
        items[0].Children.Add("till");
        items[8].Children.Add("pc");
        ShowroomCut cut = DesignRules.BranchCut(items);
        Check(cut.Kept.Contains("counter") && cut.Kept.Contains("till") && cut.Kept.Contains("sofa") && cut.Kept.Contains("desk") && cut.Kept.Contains("pc") && cut.Kept.Contains("barrier")
              && cut.InLot == 0, "designs: a bank branch keeps its counter and till, the seats and desk in the middle of the floor and the queue's barrier: it has no lot");
        Check(!cut.Kept.Contains("shelf") && !cut.Kept.Contains("case"), "designs: a bank branch keeps no shelves or display cases");
        Check(cut.Tills == 1 && cut.Seats == 2 && cut.ValidBranch, "designs: a branch's cut counts its till and its seats to wait on, and is offered (" + cut.Seats + " seats)");
        items.RemoveAt(1);
        items[0].Children.Clear();
        Check(DesignRules.BranchCut(items).Valid && !DesignRules.BranchCut(items).ValidBranch, "designs: a branch design without a till for its teller is not offered");
        Check(!DesignRules.Cut(new List<DesignItem> { Item("barrier", "beltbarrier", 0, 0, 900) }, new List<FloorBox>()).Kept.Contains("barrier"),
            "designs: a showroom keeps no queue barriers: the branch's extras are the branch's");

        var sources = new List<DesignSource>();
        var cuts = new List<ShowroomCut>();
        void Add(string name, int tills, int seats)
        {
            sources.Add(Source(6, 0, "C1", name, "x/" + name + sources.Count + ".json"));
            cuts.Add(new ShowroomCut { Furniture = DesignCatalogue.ShowroomLeast, Toilets = 1, Tills = tills, Seats = seats });
        }
        Add("Classic", 1, 0);
        Add("Modern", 1, 3);
        Add("Dark", 0, 4);
        Add("Rustic", 1, 0);
        Add("Modern", 2, 2);
        Add("Lemon", 1, 1);
        Add("Sky", 1, 0);
        var picks = new List<int>();
        DesignRules.Branches(sources, cuts, picks);
        Check(picks.Count == DesignCatalogue.BranchMost && picks[0] == 1 && sources[picks[1]].Name == "Lemon" && sources[picks[2]].Name == "Classic" && sources[picks[3]].Name == "Rustic",
            "designs: a branch is offered designs with seats to wait on first, then the rest, each with a till, one of a name, at most " + DesignCatalogue.BranchMost);
    }

    private static bool InAny(List<FloorBox> boxes, int x, int z)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if (boxes[i].Holds(x, z))
            {
                return true;
            }
        }
        return false;
    }

    private static DesignItem Item(string id, string name, int x, int y, int z, string parent = "")
    {
        return new DesignItem { Id = id, Name = DesignCatalogue.ItemPrefix + name, X = x, Y = y, Z = z, Parent = parent };
    }

    // Every text of the feature in the three locales, and the What's new line.
    private static void DesignTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(DesignLocales, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(DesignLocales, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "designs: the three locales are where the checks look (" + DesignLocales + ")");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        var keys = new List<string>
        {
            DesignsText.SaySome, DesignsText.SayWorking, DesignsText.SayFailed, DesignsText.InstallNone, DesignsText.InstallDone, DesignsText.InstallSession,
            DesignsText.ReportRemoved, "reallife_designs_header", "reallife_designs_enabled", "reallife_opt_tip_designs", "reallife_opt_sec_designs",
            "reallife_meta_title_designs", "reallife_designs_test_say", "reallife_designs_test_install", "reallife_guide_designs_about", "reallife_guide_designs"
        };
        foreach (string key in keys)
        {
            Check(en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key), "designs: " + key + " is in en, pt and zh-cn");
        }
        Check(Has(en, DesignsText.SaySome, "{count}") && Has(en, DesignsText.SaySome, "{types}") && Has(pt, DesignsText.SaySome, "{count}") && Has(zh, DesignsText.SaySome, "{types}"),
            "designs: the test's notice names the designs and the types");
        Check(Has(en, DesignsText.InstallDone, "{count}") && Has(en, DesignsText.ReportRemoved, "{count}") && Has(pt, DesignsText.InstallDone, "{count}") && Has(zh, DesignsText.ReportRemoved, "{count}"),
            "designs: the counts are in their texts");
        Check(Has(en, "reallife_guide_designs", "{designs_showroom_most}"), "designs: the Guide page takes the showroom's number of designs from the code");
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            line |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.IndexOf("ready-made designs", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        Check(line, "designs: What's new has the ready-made designs' line, in the next row or a version's");
    }

    // The plan the game's start makes, from the installed game's own design files.
    private static void DesignGame()
    {
        if (!Directory.Exists(System.IO.Path.Combine(DesignGameFiles, DesignCatalogue.Installer)))
        {
            Console.WriteLine("  (the game is not installed at " + DesignGameFiles + ": the designs are not made from its files here)");
            return;
        }
        var watch = Stopwatch.StartNew();
        var sources = new List<DesignSource>();
        var sizes = new Dictionary<int, List<string>>();
        var buildings = new Dictionary<int, string>();
        Check(DesignPlan.Find(DesignGameFiles, sources, sizes, buildings), "designs: the game's installers' folder is found");
        var reads = new Dictionary<string, List<DesignItem>>(StringComparer.Ordinal);
        List<DesignItem> Read(DesignSource s)
        {
            if (!reads.TryGetValue(s.Path, out List<DesignItem> items))
            {
                items = DesignJson(System.IO.Path.Combine(DesignGameFiles, s.Path), out string _, out int _);
                reads[s.Path] = items;
            }
            return items;
        }
        List<PlannedDesign> plan = DesignPlan.Make(sources, sizes, buildings, Read);
        long planned = watch.ElapsedMilliseconds;
        List<PlannedDesign> again = DesignPlan.Make(sources, sizes, buildings, Read);
        bool same = again.Count == plan.Count;
        for (int i = 0; same && i < plan.Count; i++)
        {
            same = plan[i].Path == again[i].Path && plan[i].Source.Path == again[i].Source.Path && (plan[i].Cut == null) == (again[i].Cut == null)
                   && (plan[i].Cut == null || plan[i].Cut.Kept.SetEquals(again[i].Cut.Kept));
        }
        Check(same, "designs: the plan is the same every time it is made from the same files");

        // The sizes: those the game has designs for in each building.
        string installer = System.IO.Path.Combine(DesignGameFiles, DesignCatalogue.Installer);
        var gameItems = new HashSet<string>(StringComparer.Ordinal);
        var rooms = new Dictionary<string, FloorBox>(StringComparer.Ordinal);
        var roomVersions = new Dictionary<string, string>(StringComparer.Ordinal);
        // The room of a size: what the game's own designs and its own businesses' layouts of that size fill, whatever the building.
        string layouts = System.IO.Path.Combine(DesignGameFiles, DesignCatalogue.Layouts);
        var gameFiles = new List<string>(Directory.GetFiles(installer, "*.json", SearchOption.AllDirectories));
        int installerFiles = gameFiles.Count;
        gameFiles.AddRange(Directory.Exists(layouts) ? Directory.GetFiles(layouts, "*.json", SearchOption.AllDirectories) : new string[0]);
        for (int f = 0; f < gameFiles.Count; f++)
        {
            string file = gameFiles[f];
            bool offered = f < installerFiles;
            string rel = file.Substring((offered ? installer : layouts).Length + 1).Replace('\\', '/');
            string[] parts = rel.Split('/');
            if (parts.Length < 3 || offered && IsModFolder(parts.Length == 4 ? parts[2] : string.Empty))
            {
                continue;   // (a design the mod put there on this PC: not the game's)
            }
            List<DesignItem> items = DesignJson(file, out string bsize, out int bversion);
            if (items == null)
            {
                continue;
            }
            string key = offered ? parts[1] : parts[parts.Length - 2];
            FloorBox box = rooms.TryGetValue(key, out FloorBox had) ? had : FloorBox.None;
            foreach (DesignItem item in items)
            {
                if (offered)
                {
                    gameItems.Add(item.Name ?? string.Empty);
                }
                if (!DesignRules.Stray(item))
                {
                    box.Take(item.X, item.Z);
                }
            }
            rooms[key] = box;
            roomVersions[key] = bsize + "|" + bversion;
        }
        Dictionary<string, string> gameWords = LoadWords(System.IO.Path.Combine(DesignGameFiles, "locale", "en.json"));
        var gameKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (gameWords != null)
        {
            foreach (string k in gameWords.Keys)
            {
                gameKeys.Add(k);
            }
        }
        foreach (string keep in DesignCatalogue.BranchExtras)
        {
            Check(gameItems.Contains(DesignCatalogue.ItemPrefix + keep), "designs: the branch's " + keep + " is furniture the game's installation firms put in");
            Check(gameWords == null || gameKeys.Contains(DesignCatalogue.ItemPrefix + keep), "designs: the branch's " + keep + " is an item the game names");
        }
        foreach (string keep in DesignCatalogue.ShowroomKeeps)
        {
            Check(gameItems.Contains(DesignCatalogue.ItemPrefix + keep), "designs: the showroom's " + keep + " is furniture the game's installation firms put in");
            Check(gameWords == null || gameKeys.Contains(DesignCatalogue.ItemPrefix + keep), "designs: the showroom's " + keep + " is an item the game names");
        }

        var perKind = new Dictionary<string, int>(StringComparer.Ordinal);
        int branches = 0, branchNoTill = 0, tills = 0, showrooms = 0, inLot = 0, outside = 0, wrongSize = 0, unknown = 0, notSubset = 0, noDesk = 0, noToilet = 0, lostParent = 0;
        int shops = 0, shopNoPoint = 0, shopNoLead = 0, shopForeign = 0;
        string firstOutside = null;
        foreach (PlannedDesign design in plan)
        {
            DesignKind kind = DesignCatalogue.Kinds[design.Kind];
            string group = design.Kind + "|" + design.Size;
            perKind[group] = perKind.TryGetValue(group, out int n) ? n + 1 : 1;
            List<DesignItem> source = DesignJson(System.IO.Path.Combine(DesignGameFiles, design.Source.Path), out string bsize, out int bversion);
            string sizeId = DesignRules.SizeId(design.Size, out int version);
            wrongSize += bsize == sizeId && bversion == version ? 0 : 1;
            Check(DesignRules.Ours(design.Path), "designs: " + design.Path + " is a path the mod may write");
            string roomKey = design.Size;
            FloorBox room = rooms.TryGetValue(roomKey, out FloorBox r) ? r : FloorBox.None;
            var kept = new List<DesignItem>(source != null ? source.Count : 0);
            for (int i = 0; source != null && i < source.Count; i++)
            {
                if (design.Cut == null || design.Cut.Keep[i])
                {
                    kept.Add(source[i]);
                }
            }
            var keptIds = new HashSet<string>(StringComparer.Ordinal);
            var sourceCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (DesignItem item in source ?? new List<DesignItem>())
            {
                sourceCount[item.Name ?? string.Empty] = sourceCount.TryGetValue(item.Name ?? string.Empty, out int c) ? c + 1 : 1;
            }
            foreach (DesignItem item in kept)
            {
                keptIds.Add(item.Id ?? string.Empty);
                unknown += gameWords == null || gameKeys.Contains(item.Name ?? string.Empty) ? 0 : 1;
                bool inside = DesignRules.Stray(item) || (item.X >= room.MinX - 10 && item.X <= room.MaxX + 10 && item.Z >= room.MinZ - 10 && item.Z <= room.MaxZ + 10);
                if (!inside)
                {
                    outside++;
                    firstOutside = firstOutside ?? design.Path + ": " + item.Name + " at " + item.X + ", " + item.Z;
                }
                string name = item.Name ?? string.Empty;
                if (!sourceCount.TryGetValue(name, out int left) || left <= 0)
                {
                    notSubset++;
                }
                sourceCount[name] = left - 1;
            }
            foreach (DesignItem item in kept)
            {
                lostParent += string.IsNullOrEmpty(item.Parent) || keptIds.Contains(item.Parent) ? 0 : 1;
            }
            Check(design.Cut != null || kept.Count == (source != null ? source.Count : -1), "designs: " + design.Path + " is the game's design whole");
            if (kind.Way == DesignWay.Copy)
            {
                noDesk += Has(kept, "computer") || Has(kept, "desktopcomputer") ? 0 : 1;
                noToilet += Has(kept, "toiletstall") ? 0 : 1;
                continue;
            }
            if (kind.Way == DesignWay.Stock)
            {
                ShopKind shop = ShopsCatalogue.Of(kind.Type);
                shops++;
                noToilet += Has(kept, "toiletstall") ? 0 : 1;
                shopNoPoint += design.Cut.Points > 0 && (Has(kept, "cashregister") || Has(kept, "checkoutcounterleft") || Has(kept, "checkoutcounterright")) ? 0 : 1;
                bool lead = false;
                foreach (string shelf in shop.Leads)
                {
                    lead |= Has(kept, shelf);
                }
                shopNoLead += lead ? 0 : 1;
                foreach (DesignItem item in kept)
                {
                    string bare = (item.Name ?? string.Empty).Replace(DesignCatalogue.ItemPrefix, string.Empty);
                    shopForeign += Array.IndexOf(DesignCatalogue.ShopShelves, bare) >= 0 && Array.IndexOf(shop.Shelves, bare) < 0 ? 1 : 0;
                }
                continue;
            }
            if (kind.Way == DesignWay.Branch)
            {
                branches++;
                branchNoTill += design.Cut.Tills > 0 && Has(kept, "cashregister") ? 0 : 1;
                noToilet += Has(kept, "toiletstall") ? 0 : 1;
                continue;
            }
            showrooms++;
            tills += design.Cut.Tills > 0 ? 1 : 0;
            noToilet += Has(kept, "toiletstall") ? 0 : 1;
            // The lot as the showroom will lay it on this room, with no slack: nothing on the floor stands in it.
            var offsets = new List<int>();
            int count = DealerRules.Layout(room.Width, room.Depth, offsets, out bool across);
            int hx = (across ? DealerCatalogue.SpotDepth : DealerCatalogue.SpotWidth) / 2, hz = (across ? DealerCatalogue.SpotWidth : DealerCatalogue.SpotDepth) / 2;
            foreach (DesignItem item in kept)
            {
                for (int s = 0; s < count && item.Y < DesignCatalogue.FloorTop; s++)
                {
                    int cx = (room.MinX + room.MaxX) / 2 + offsets[2 * s], cz = (room.MinZ + room.MaxZ) / 2 + offsets[2 * s + 1];
                    inLot += Math.Abs(item.X - cx) <= hx && Math.Abs(item.Z - cz) <= hz ? 1 : 0;
                }
            }
        }
        Check(wrongSize == 0, "designs: every design is for the very room of its folder's size and version (" + wrongSize + " not)");
        Check(outside == 0, "designs: every design places its furniture inside the room the game's own designs and businesses fill (" + outside + " outside" + (firstOutside != null ? "; " + firstOutside : string.Empty) + ")");
        Check(unknown == 0, "designs: every design places only items the game names (" + unknown + " not)");
        Check(notSubset == 0, "designs: every design is a design of the game's or a part of one, so its layout price is at most the game's own (" + notSubset + " pieces more)");
        Check(lostParent == 0, "designs: nothing kept stands on something left out (" + lostParent + ")");
        Check(noDesk == 0, "designs: every tech firm's and academy's design has computers on its desks (" + noDesk + " without)");
        Check(noToilet == 0, "designs: every design has washrooms (" + noToilet + " without)");
        Check(inLot == 0, "designs: nothing of a showroom's design stands on the floor where its cars go (" + inLot + ")");
        Check(shops > 0 && shopNoPoint == 0, "designs: every new kind of shop's design keeps a till its customers pay at (" + shopNoPoint + " of " + shops + " without)");
        Check(shopNoLead == 0, "designs: every new kind of shop's design keeps furniture that shows one of its primary goods (" + shopNoLead + " without)");
        Check(shopForeign == 0, "designs: no new kind of shop's design keeps a shelf for goods it does not sell (" + shopForeign + ")");

        // A new kind of shop has a design at every size where a design of its donors shows one of its primary goods.
        var leadSizes = new Dictionary<int, HashSet<string>>();
        foreach (DesignSource src in sources)
        {
            DesignKind srcKind = DesignCatalogue.Kinds[src.Kind];
            ShopKind shop = srcKind.Way == DesignWay.Stock ? ShopsCatalogue.Of(srcKind.Type) : null;
            List<DesignItem> items = shop != null ? Read(src) : null;
            bool lead = false;
            foreach (string shelf in shop != null && items != null ? shop.Leads : new string[0])
            {
                lead |= Has(items, shelf);
            }
            if (lead)
            {
                if (!leadSizes.TryGetValue(src.Kind, out HashSet<string> at))
                {
                    leadSizes[src.Kind] = at = new HashSet<string>(StringComparer.Ordinal);
                }
                at.Add(src.Size);
            }
        }

        // Every type has a design for every size its building has designs for.
        int missing = 0;
        var table = new System.Text.StringBuilder();
        for (int k = 0; k < DesignCatalogue.Kinds.Length; k++)
        {
            List<string> kindSizes = sizes.TryGetValue(k, out List<string> found) ? found : new List<string>();
            Check(kindSizes.Count >= 6, "designs: " + DesignCatalogue.Kinds[k].Type + " is offered designs at the game's sizes (" + kindSizes.Count + " sizes)");
            if (DesignCatalogue.Kinds[k].Way == DesignWay.Stock)
            {
                HashSet<string> shown = leadSizes.TryGetValue(k, out HashSet<string> at) ? at : new HashSet<string>();
                kindSizes = kindSizes.FindAll(shown.Contains);
                // The tea bar's and tobacconist's donors show their goods at every size; the game's designs with an ice cream counter are of the four largest.
                int least = DesignCatalogue.Kinds[k].Type == ShopsCatalogue.IceCream ? 4 : 6;
                Check(kindSizes.Count >= least, "designs: " + DesignCatalogue.Kinds[k].Type + " is offered designs at the sizes the game's designs show its goods at (" + kindSizes.Count + " sizes, " + least + " wanted)");
            }
            table.Append("    ").Append(DesignRules.FolderOf(DesignCatalogue.Kinds[k].Type)).Append(':');
            foreach (string size in kindSizes)
            {
                int had = perKind.TryGetValue(k + "|" + size, out int c) ? c : 0;
                missing += had > 0 ? 0 : 1;
                Check(had > 0, "designs: " + DesignCatalogue.Kinds[k].Type + " has no design at " + size);
                table.Append(' ').Append(size).Append('=').Append(had);
            }
            table.Append('\n');
        }
        Check(missing == 0, "designs: every business type of the mod's has at least one design at every size (" + missing + " missing)");
        Check(sizes.TryGetValue(0, out List<string> office) && string.Join(",", office) == "A3,C1,C2,D2,J1,K1", "designs: the office sizes are the game's six (" + (office != null ? string.Join(",", office) : "none") + ")");
        Check(sizes.TryGetValue(6, out List<string> retail) && string.Join(",", retail) == "A1,A2,C1,C2,D2,M1", "designs: the shop sizes are the game's six (" + (retail != null ? string.Join(",", retail) : "none") + ")");
        Check(branches > 0 && branchNoTill == 0, "designs: every bank branch's design keeps a till for its teller (" + branchNoTill + " of " + branches + " without)");
        Check(showrooms > 0 && tills * 2 >= showrooms, "designs: most showroom designs keep a sales counter with its till (" + tills + " of " + showrooms + ")");
        foreach (PlannedDesign design in plan)
        {
            if (design.Cut != null && DesignCatalogue.Kinds[design.Kind].Way == DesignWay.Branch)
            {
                Console.WriteLine("    branch " + design.Size + " " + design.Name + ": the game's " + DesignCatalogue.Kinds[design.Kind].Donors[design.Source.Donor].Business + ", "
                                  + design.Cut.Furniture + " pieces kept, " + design.Cut.Tills + " till(s), " + design.Cut.Seats + " seat(s) to wait on");
            }
            else if (design.Cut != null && DesignCatalogue.Kinds[design.Kind].Way == DesignWay.Stock)
            {
                Console.WriteLine("    " + DesignRules.FolderOf(DesignCatalogue.Kinds[design.Kind].Type) + " " + design.Size + " " + design.Name + ": the game's "
                                  + DesignCatalogue.Kinds[design.Kind].Donors[design.Source.Donor].Business + ", " + design.Cut.Furniture + " pieces kept, "
                                  + design.Cut.Dropped + " shelves for other goods left out, " + design.Cut.Leads + " showing its primary goods");
            }
            else if (design.Cut != null)
            {
                string donor = DesignCatalogue.Kinds[design.Kind].Donors[design.Source.Donor].Business;
                Console.WriteLine("    showroom " + design.Size + " " + design.Name + ": the game's " + donor + " " + System.IO.Path.GetFileNameWithoutExtension(design.Source.Path)
                                  + ", " + design.Cut.Furniture + " pieces kept, " + design.Cut.InLot + " left out of the lot, " + (design.Cut.Tills > 0 ? "a till" : "no till"));
            }
        }
        Console.WriteLine("  Designs from the game's files: " + plan.Count + " designs (" + showrooms + " showroom, " + tills + " with a till), " + sources.Count + " sources, planned in " + planned + " ms");
        Console.Write(table.ToString());
    }

    private static bool IsModFolder(string folder)
    {
        foreach (DesignKind kind in DesignCatalogue.Kinds)
        {
            if (DesignRules.FolderOf(kind.Type) == folder)
            {
                return true;
            }
        }
        return false;
    }

    private static bool Has(Dictionary<string, string> words, string key, string part)
    {
        return words.TryGetValue(key, out string text) && text != null && text.Contains(part);
    }

    private static bool Has(List<DesignItem> items, string bare)
    {
        foreach (DesignItem item in items)
        {
            if (item.Name == DesignCatalogue.ItemPrefix + bare)
            {
                return true;
            }
        }
        return false;
    }

    // The job itself (DesignFiles), on a copy of some of the game's files in a folder of its own: what it writes, the list, a
    // second start that writes nothing, a game update, a file lost, a file of somebody else's, and the option switched off.
    private static void DesignSandbox()
    {
        if (!Directory.Exists(System.IO.Path.Combine(DesignGameFiles, DesignCatalogue.Installer)))
        {
            return;
        }
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rlo-designs-" + Environment.ProcessId);
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            foreach (string folder in new[] { "InteriorDesigns/Office/C1/WebDevelopmentAgency", "InteriorDesigns/Office/C1/GraphicDesigner", "InteriorDesigns/Office/C1/LawFirm",
                         "InteriorDesigns/Office/C1/travelagency", "InteriorDesigns/Retail/C1/JewelryStore", "InteriorDesigns/Retail/C1/LiquorStore", "InteriorDesigns/Retail/D2/LiquorStore", "InteriorDesigns/Retail/C1/CoffeeShop",
                         "BusinessLayouts/CarDealership/D2" })
            {
                string from = System.IO.Path.Combine(DesignGameFiles, folder), to = System.IO.Path.Combine(root, folder);
                Directory.CreateDirectory(to);
                foreach (string file in Directory.GetFiles(from, "*.json"))
                {
                    File.Copy(file, System.IO.Path.Combine(to, System.IO.Path.GetFileName(file)));
                }
            }
            string installer = System.IO.Path.Combine(root, DesignCatalogue.Installer);
            string foreign = System.IO.Path.Combine(installer, "Office", "C1", "ailab", "Modern.json");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(foreign));
            File.WriteAllText(foreign, "somebody else's");

            DesignsReport first = DesignFiles.Put(root, SandboxRead, SandboxCut);
            DesignManifest list = DesignFiles.List(root);
            Check(!first.Failed && first.Written > 0 && first.Written == list.Files.Count && first.Designs == list.Files.Count && first.Types == 11,
                "designs job: the first start writes every design and lists each (" + first.Written + " written, " + list.Files.Count + " listed, " + first.Types + " types"
                + (first.Why != null ? ", " + first.Why : string.Empty) + ")");
            Check(File.ReadAllText(foreign) == "somebody else's" && !list.Holds("Office/C1/ailab/Modern.json") && first.Skipped == 1,
                "designs job: a file of that name that is not the mod's is left as it is, and not listed (" + first.Skipped + " left out)");
            bool sizes = true, copies = true;
            foreach (KeyValuePair<string, long> file in list.Files)
            {
                string path = System.IO.Path.Combine(installer, file.Key);
                sizes &= File.Exists(path) && new FileInfo(path).Length == file.Value;
                if (file.Key.StartsWith("Office/C1/softwarestudio/", StringComparison.Ordinal))
                {
                    copies &= File.ReadAllText(path) == File.ReadAllText(System.IO.Path.Combine(installer, "Office", "C1", "WebDevelopmentAgency", System.IO.Path.GetFileName(path)));
                }
            }
            Check(sizes && list.Holds("Office/C1/softwarestudio/Modern.json") && copies, "designs job: every listed file is there at its length, a tech firm's the game's own design byte for byte");
            string showroom = System.IO.Path.Combine(installer, "Retail", "C1", "businesstype");
            string[] cutFiles = Directory.Exists(showroom) ? Directory.GetFiles(showroom, "*.json") : new string[0];
            bool cutOk = cutFiles.Length > 0;
            foreach (string file in cutFiles)
            {
                List<DesignItem> items = DesignJson(file, out string bs, out int bv);
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                cutOk &= items != null && items.Count > 0 && bs == "ba:buildingsize_c" && bv == 1
                         && doc.RootElement.GetProperty("BusinessType").GetString() == DealerCatalogue.TypeName && doc.RootElement.TryGetProperty("interiorDesigns", out JsonElement _);
            }
            Check(cutOk, "designs job: a showroom's design is the game's file with its cut items, its walls and floors, marked as the showroom's (" + cutFiles.Length + " at C1)");
            Check(list.Holds("Retail/D2/businesstype/Cheap.json"), "designs job: the game's own dealership's room is a showroom's design at its size");
            string[] teaFiles = Directory.GetFiles(System.IO.Path.Combine(installer, "Retail", "C1", "teabar"), "*.json");
            string[] smokeFiles = Directory.GetFiles(System.IO.Path.Combine(installer, "Retail", "C1", "tobacconist"), "*.json");
            bool tea = teaFiles.Length > 0, smoke = smokeFiles.Length > 0;
            foreach (string file in teaFiles)
            {
                string text = File.ReadAllText(file);
                tea &= text.Contains("\"ba:itemname_cupoftea\"") && !text.Contains("\"ba:itemname_cupofcoffee\"") && text.Contains("\"" + ShopsCatalogue.TeaBar + "\"");
            }
            foreach (string file in smokeFiles)
            {
                string text = File.ReadAllText(file);
                smoke &= text.Contains("ba:itemname_tobaccoshelf") && !text.Contains("\"ba:itemname_wineshelf\"") && !text.Contains("\"ba:itemname_drinksshelf\"");
            }
            Check(tea, "designs job: a tea bar's designs are the game's coffee shops' with their machines set to pour tea (" + teaFiles.Length + " at C1)");
            Check(smoke, "designs job: a tobacconist's designs are the game's liquor stores' with the tobacco shelves and without the wine and spirits shelves (" + smokeFiles.Length + " at C1)");

            DesignsReport second = DesignFiles.Put(root, SandboxRead, SandboxCut);
            Check(second.Unchanged && second.Written == 0 && second.Designs == first.Designs, "designs job: a second start with the same files writes nothing");

            string source = System.IO.Path.Combine(root, "InteriorDesigns", "Office", "C1", "WebDevelopmentAgency", "Modern.json");
            File.SetLastWriteTimeUtc(source, File.GetLastWriteTimeUtc(source).AddMinutes(5));
            DesignsReport update = DesignFiles.Put(root, SandboxRead, SandboxCut);
            Check(!update.Unchanged && update.Written == first.Written && update.Removed == 0, "designs job: a game update makes the designs again (" + update.Written + " written)");

            File.Delete(System.IO.Path.Combine(installer, "Office", "C1", "gamestudio", "Modern.json"));
            DesignsReport lost = DesignFiles.Put(root, SandboxRead, SandboxCut);
            Check(!lost.Unchanged && File.Exists(System.IO.Path.Combine(installer, "Office", "C1", "gamestudio", "Modern.json")), "designs job: a design taken away by hand is put back");

            Directory.Delete(System.IO.Path.Combine(root, "InteriorDesigns", "Office", "C1", "GraphicDesigner"), true);
            DesignsReport fewer = DesignFiles.Put(root, SandboxRead, SandboxCut);
            Check(fewer.Removed == 1 && !File.Exists(System.IO.Path.Combine(installer, "Office", "C1", "gamestudio", "Rustic.json"))
                  && File.Exists(System.IO.Path.Combine(installer, "Office", "C1", "gamestudio", "Modern.json")),
                "designs job: a design the game no longer has is taken away (" + fewer.Removed + ")");

            DesignsReport off = DesignFiles.Take(root);
            Check(off.Removed == fewer.Designs && off.Skipped == 0 && !File.Exists(System.IO.Path.Combine(installer, DesignCatalogue.Manifest)),
                "designs job: switched off, every design of the list goes, and the list (" + off.Removed + " of " + fewer.Designs + ")");
            string web = System.IO.Path.Combine(installer, "Office", "C1", "WebDevelopmentAgency");
            Check(File.ReadAllText(foreign) == "somebody else's" && !Directory.Exists(System.IO.Path.Combine(installer, "Office", "C1", "gamestudio"))
                  && Directory.Exists(web) && Directory.GetFiles(web).Length > 0,
                "designs job: switched off, the game's own designs and somebody else's file stay, the mod's emptied folders go");
            DesignsReport again = DesignFiles.Take(root);
            Check(again.Removed == 0 && !again.Failed, "designs job: switched off twice, nothing more happens");
            File.WriteAllText(System.IO.Path.Combine(installer, DesignCatalogue.Manifest), "v=1\nstamp=x\nOffice/C1/WebDevelopmentAgency/Modern.json|1\nOffice/C1/../../evil.json|1\n");
            DesignFiles.Take(root);
            Check(File.Exists(System.IO.Path.Combine(web, "Modern.json")), "designs job: a list that names a game's design never has it taken away");
        }
        catch (Exception e)
        {
            Check(false, "designs job: " + e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            catch (Exception)
            {
                // (a folder of the checker's own in the temporary folder)
            }
        }
    }

    private static List<DesignItem> SandboxRead(string path)
    {
        return DesignJson(path, out string _, out int _);
    }

    // The checker's own writer of a showroom's cut, as Designs/DesignsGame.WriteCut writes it with the game's JSON library.
    private static bool SandboxCut(string source, ShowroomCut cut, string target, string type, string name)
    {
        var design = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(source).TrimStart('﻿')) as System.Text.Json.Nodes.JsonObject;
        if (!(design?["Items"] is System.Text.Json.Nodes.JsonArray items) || items.Count != cut.Keep.Length)
        {
            return false;
        }
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (!cut.Keep[i])
            {
                items.RemoveAt(i);
                continue;
            }
            if (items[i]?["playerItemPurchaserSettings"]?["itemName"] is System.Text.Json.Nodes.JsonValue good && good.TryGetValue(out string now) && DesignRules.Restocked(cut, now) != now)
            {
                items[i]["playerItemPurchaserSettings"]["itemName"] = DesignRules.Restocked(cut, now);
            }
        }
        design["BusinessType"] = type;
        design["LayoutName"] = name;
        File.WriteAllText(target, design.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    // A design file as the game's start reads it (Designs/DesignsGame.Read): the items in order, positions in centimetres.
    private static List<DesignItem> DesignJson(string path, out string size, out int version)
    {
        size = null;
        version = 0;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path).TrimStart('\uFEFF'));
            JsonElement root = doc.RootElement;
            size = root.TryGetProperty("BuildingSize", out JsonElement s) ? s.GetString() : null;
            version = root.TryGetProperty("BuildingVersion", out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
            if (!root.TryGetProperty("Items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var list = new List<DesignItem>(items.GetArrayLength());
            foreach (JsonElement o in items.EnumerateArray())
            {
                var item = new DesignItem
                {
                    Id = DesignStr(o, "id"), Name = DesignStr(o, "itemName"), Parent = DesignStr(o, "parentId") ?? string.Empty
                };
                if (o.TryGetProperty("position", out JsonElement at) && at.ValueKind == JsonValueKind.Object)
                {
                    item.X = Cm(at, "x");
                    item.Y = Cm(at, "y");
                    item.Z = Cm(at, "z");
                }
                if (o.TryGetProperty("stackedItems", out JsonElement stacked) && stacked.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement child in stacked.EnumerateArray())
                    {
                        string id = DesignStr(child, "childId");
                        if (!string.IsNullOrEmpty(id))
                        {
                            item.Children.Add(id);
                        }
                    }
                }
                list.Add(item);
            }
            return list;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string DesignStr(JsonElement o, string name)
    {
        return o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static int Cm(JsonElement o, string name)
    {
        return o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? (int)Math.Round(v.GetDouble() * 100.0) : 0;
    }
}
