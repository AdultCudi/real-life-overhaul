using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul.Designs;
using RealLifeOverhaul.Shops;

// The new kinds of shop (Scripts/Shops): the catalogue against the game's own goods and furniture (its items, read from its
// bundles into ShopGoods/shop-goods-<build>.json, and its wholesale stores' layouts on disk), the hours and days, the help
// pages, the icons, the texts in three languages, the wiring and the other features that sort businesses by kind; then the
// designs cut for a shop from the game's own (DesignWay.Stock) on made-up rooms.
internal static partial class Program
{
    private static readonly string ShopGoodsFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\ShopGoods\shop-goods-3682.json"));

    private static void Shops()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        ShopCatalogueChecks();
        ShopGameData();
        ShopRuleChecks();
        ShopGlyphs();
        ShopTexts();
        ShopWiring();
        Console.WriteLine("  Shops: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // The table itself: three kinds, each whole, its bands from midnight to midnight and its shelves among the game's.
    private static void ShopCatalogueChecks()
    {
        Check(ShopsCatalogue.Kinds.Length == 3, "shops: three new kinds of shop (" + ShopsCatalogue.Kinds.Length + ")");
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (ShopKind kind in ShopsCatalogue.Kinds)
        {
            string name = DesignRules.FolderOf(kind.Type);
            Check(types.Add(kind.Type) && kind.Type.StartsWith(ShopsCatalogue.Prefix, StringComparison.Ordinal), "shops: " + kind.Type + " is a type of the mod's own, named once");
            Check(ShopsCatalogue.Of(kind.Type) == kind && ShopsCatalogue.Is(kind.Type), "shops: " + name + " is found by its type");
            Check(ShopsRules.WholeDay(kind), "shops: " + name + "'s hour bands run from midnight to midnight, each from where the last ended, each between 1 and 1000");
            int bestHour = 0, bestDay = 0;
            foreach (ShopHours band in kind.Hours)
            {
                bestHour = Math.Max(bestHour, band.Share);
            }
            bool days = kind.Days.Length == 7;
            foreach (int day in kind.Days)
            {
                days &= day > 0 && day <= 1000;
                bestDay = Math.Max(bestDay, day);
            }
            Check(days && bestDay == 1000 && bestHour == 1000, "shops: " + name + " has seven days and its busiest day and hour are its whole (" + bestDay + ", " + bestHour + ")");
            var goods = new HashSet<string>(StringComparer.Ordinal);
            int primary = 0;
            bool impacts = kind.Goods.Length > 0;
            foreach (ShopGood good in kind.Goods)
            {
                impacts &= goods.Add(good.Item) && good.Item.StartsWith(ShopsCatalogue.ItemPrefix, StringComparison.Ordinal) && good.Impact > 0 && good.Impact <= 1000;
                primary += good.Impact == ShopsCatalogue.Primary ? 1 : 0;
            }
            Check(impacts && primary >= 1 && kind.Goods[0].Impact == ShopsCatalogue.Primary, "shops: " + name + "'s goods are the game's items, each once, the first of them primary (" + primary + " primary)");
            Check(kind.MostEach >= 10 && kind.MostEach <= 50, "shops: " + name + " sells between 1 and 5 of a primary good to one customer, as the game allows (" + kind.MostEach + " tenths)");
            bool sources = kind.Sources.Length > 0;
            foreach (string source in kind.Sources)
            {
                sources &= source == ShopsCatalogue.Wholesaler || source == ShopsCatalogue.Importer;
            }
            Check(sources, "shops: " + name + "'s goods come from the game's wholesalers or importers");
            bool shelves = kind.Shelves.Length > 0, leads = kind.Leads.Length > 0;
            var own = new HashSet<string>(StringComparer.Ordinal);
            foreach (string shelf in kind.Shelves)
            {
                shelves &= own.Add(shelf) && Array.IndexOf(DesignCatalogue.ShopShelves, shelf) >= 0;
            }
            foreach (string lead in kind.Leads)
            {
                leads &= own.Contains(lead);
            }
            Check(shelves && leads, "shops: " + name + "'s shelves are the game's furniture that shows goods, each once, and the ones that show its primary goods are among them");
            bool restock = kind.Restock.Length % 2 == 0;
            for (int i = 0; i + 1 < kind.Restock.Length; i += 2)
            {
                restock &= goods.Contains(ShopsCatalogue.ItemPrefix + kind.Restock[i + 1]) && kind.Restock[i] != kind.Restock[i + 1];
            }
            Check(restock, "shops: " + name + "'s designs restock shelves only with its own goods, in pairs");
            Check(kind.Aliases.Length > 0 && kind.Donor.StartsWith("ba:businesstype_", StringComparison.Ordinal) && kind.Template.StartsWith("ba:businesstype_", StringComparison.Ordinal),
                "shops: " + name + " works as a game type and becomes one when the mod is taken out");
            Check(kind.Red >= 0f && kind.Red <= 1f && kind.Green >= 0f && kind.Green <= 1f && kind.Blue >= 0f && kind.Blue <= 1f, "shops: " + name + "'s map colour is a colour");
        }
        Check(ShopsCatalogue.Of(null) == null && ShopsCatalogue.Of("ba:businesstype_coffeeshop") == null && !ShopsCatalogue.Is(string.Empty), "shops: a type that is not one of these is no kind");
    }

    // The game's own data: every good is a demanded retail product with a margin, sold where the kind buys it, on furniture the
    // kind's designs keep; the donor trades as a shop; the type it becomes without the mod sells one of its primary goods.
    private static void ShopGameData()
    {
        if (!File.Exists(ShopGoodsFile))
        {
            Check(false, "shops: the game's goods are read from " + ShopGoodsFile);
            return;
        }
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(ShopGoodsFile));
        JsonElement items = doc.RootElement.GetProperty("items"), types = doc.RootElement.GetProperty("types");
        var wholesale = WholesaleGoods();
        bool layouts = wholesale.Count > 0;
        Check(!Directory.Exists(System.IO.Path.Combine(DesignGameFiles, "BusinessLayouts")) || layouts, "shops: the game's wholesale stores' goods are read from their layouts (" + wholesale.Count + ")");
        var shelfSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty item in items.EnumerateObject())
        {
            if ((item.Value.GetProperty("type").GetInt32() & 0x40) != 0)
            {
                shelfSet.Add(item.Name.Substring(ShopsCatalogue.ItemPrefix.Length));
            }
        }
        Check(shelfSet.SetEquals(DesignCatalogue.ShopShelves), "shops: the designs' list of the game's furniture that shows goods is the game's whole list (" + shelfSet.Count + " in the game, "
              + DesignCatalogue.ShopShelves.Length + " listed)");
        foreach (ShopKind kind in ShopsCatalogue.Kinds)
        {
            string name = DesignRules.FolderOf(kind.Type);
            bool goodsOk = true, margins = true, shown = true, bought = true;
            string firstBad = null;
            foreach (ShopGood good in kind.Goods)
            {
                if (!items.TryGetProperty(good.Item, out JsonElement it))
                {
                    goodsOk = false;
                    firstBad = firstBad ?? good.Item + " is not an item of the game's";
                    continue;
                }
                bool retail = (it.GetProperty("type").GetInt32() & 2) != 0, demanded = it.GetProperty("demanded").GetBoolean(), rival = it.GetProperty("rivalOnly").GetBoolean();
                goodsOk &= retail && demanded && !rival;
                double cost = it.GetProperty("wholesale").GetDouble(), market = it.GetProperty("market").GetDouble();
                margins &= cost > 0 && market > cost;
                bool onShelf = false;
                foreach (string shelf in kind.Shelves)
                {
                    onShelf |= Shows(items, shelf, good.Item);
                }
                shown &= onShelf;
                bool atWholesale = wholesale.Contains(good.Item), imported = Array.IndexOf(kind.Sources, ShopsCatalogue.Importer) >= 0 && ImportedOnly(types, good.Item);
                bool where = Array.IndexOf(kind.Sources, ShopsCatalogue.Wholesaler) >= 0 && atWholesale || imported;
                bought &= !layouts || where;
                if (!where && layouts)
                {
                    firstBad = firstBad ?? good.Item + " is sold neither at the wholesale stores nor by an importer the kind buys from";
                }
            }
            Check(goodsOk, "shops: " + name + " sells only the game's retail goods, each with a demand by district and none only rivals sell" + (firstBad != null ? " (" + firstBad + ")" : string.Empty));
            Check(margins, "shops: every good of " + name + " sells at the game's market price for more than its wholesale price");
            Check(shown, "shops: every good of " + name + " has a shelf of the kind's to be shown on");
            Check(bought, "shops: every good of " + name + " can be bought where the kind says" + (firstBad != null ? " (" + firstBad + ")" : string.Empty));
            bool leadsShow = true;
            foreach (string lead in kind.Leads)
            {
                bool any = false;
                foreach (ShopGood good in kind.Goods)
                {
                    any |= good.Impact == ShopsCatalogue.Primary && Shows(items, lead, good.Item);
                }
                leadsShow &= any;
            }
            bool primaryShown = true;
            foreach (ShopGood good in kind.Goods)
            {
                bool any = good.Impact != ShopsCatalogue.Primary;
                foreach (string lead in kind.Leads)
                {
                    any |= Shows(items, lead, good.Item);
                }
                primaryShown &= any;
            }
            Check(leadsShow && primaryShown, "shops: " + name + "'s lead shelves each show a primary good, and every primary good has one");
            bool useful = true;
            foreach (string shelf in kind.Shelves)
            {
                bool any = false;
                foreach (ShopGood good in kind.Goods)
                {
                    any |= Shows(items, shelf, good.Item);
                }
                useful &= any;
            }
            Check(useful, "shops: every shelf " + name + "'s designs keep shows one of its goods");
            if (!types.TryGetProperty(kind.Donor, out JsonElement donor) || !types.TryGetProperty(kind.Template, out JsonElement template))
            {
                Check(false, "shops: " + name + "'s donor and template are among the game's types read (" + kind.Donor + ", " + kind.Template + ")");
                continue;
            }
            string sim = donor.GetProperty("simulator").GetString();
            int customers = donor.GetProperty("customerType").GetInt32();
            Check(donor.GetProperty("building").GetString() == ShopsCatalogue.BuildingType && (sim == "FullServiceBusinessSimulator" && customers == 1 || sim == "SelfServiceBusinessSimulator" && customers == 2),
                "shops: " + name + " trades as a game shop of the retail buildings, served or self-serving (" + sim + ", customers " + customers + ")");
            bool requirement = false;
            foreach (JsonElement r in donor.GetProperty("requirements").EnumerateArray())
            {
                requirement |= r.GetString() == "AnyPrimaryProduct";
            }
            Check(requirement, "shops: " + name + " needs at least one of its primary goods on show to trade, as its donor does");
            bool keeps = false;
            foreach (ShopGood good in kind.Goods)
            {
                keeps |= good.Impact == ShopsCatalogue.Primary && template.GetProperty("products").TryGetProperty(good.Item, out JsonElement impact) && impact.GetDouble() >= 1.0;
            }
            Check(keeps && template.GetProperty("customerType").GetInt32() == customers,
                "shops: without the mod a " + name + " becomes " + kind.Template + ", which serves its customers the same way and sells one of its primary goods as its own");
        }
        Check(doc.RootElement.GetProperty("build").GetString() == "3682", "shops: the game's goods were read from the build the mod is made for");
    }

    private static bool Shows(JsonElement items, string shelf, string good)
    {
        if (!items.TryGetProperty(ShopsCatalogue.ItemPrefix + shelf, out JsonElement it))
        {
            return false;
        }
        foreach (JsonElement shows in it.GetProperty("shows").EnumerateArray())
        {
            if (shows.GetString() == good)
            {
                return true;
            }
        }
        return false;
    }

    // A good a game shop that buys only from importers sells: the importers carry it.
    private static bool ImportedOnly(JsonElement types, string good)
    {
        foreach (JsonProperty type in types.EnumerateObject())
        {
            JsonElement sources = type.Value.GetProperty("sources");
            bool importer = sources.GetArrayLength() > 0;
            foreach (JsonElement source in sources.EnumerateArray())
            {
                importer &= source.GetString() == ShopsCatalogue.Importer;
            }
            if (importer && type.Value.GetProperty("products").TryGetProperty(good, out JsonElement _))
            {
                return true;
            }
        }
        return false;
    }

    // What the game's wholesale stores sell: the goods their shelves are set to stock, from their own layouts.
    private static HashSet<string> WholesaleGoods()
    {
        var goods = new HashSet<string>(StringComparer.Ordinal);
        string folder = System.IO.Path.Combine(DesignGameFiles, "BusinessLayouts", "WholesaleStore");
        if (!Directory.Exists(folder))
        {
            return goods;
        }
        foreach (string file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file).TrimStart('﻿'));
                foreach (JsonElement item in doc.RootElement.GetProperty("Items").EnumerateArray())
                {
                    if (item.TryGetProperty("playerItemPurchaserSettings", out JsonElement stock) && stock.ValueKind == JsonValueKind.Object
                        && stock.TryGetProperty("enabled", out JsonElement on) && on.ValueKind == JsonValueKind.True
                        && stock.TryGetProperty("itemName", out JsonElement good) && good.ValueKind == JsonValueKind.String)
                    {
                        goods.Add(good.GetString());
                    }
                }
            }
            catch (Exception)
            {
                // a layout that cannot be read sells nothing here
            }
        }
        return goods;
    }

    private static void ShopRuleChecks()
    {
        ShopKind ice = ShopsCatalogue.Of(ShopsCatalogue.IceCream), smoke = ShopsCatalogue.Of(ShopsCatalogue.Tobacconist);
        Check(ShopsRules.HourShare(ice, 16) == 1000 && ShopsRules.HourShare(ice, 3) == 50 && ShopsRules.HourShare(ice, 23) == 50,
            "shops: an ice cream parlour is busiest in the afternoon and quiet in the small hours");
        Check(ShopsRules.HourShare(smoke, 18) == 1000 && ShopsRules.HourShare(smoke, 8) == 700, "shops: a tobacconist is busiest after work and busy before it");
        Check(ShopsRules.HourShare(ice, 24) == 0 && ShopsRules.HourShare(ice, -1) == 0 && ShopsRules.HourShare(null, 12) == 0, "shops: no share outside the day, or for no kind");
        Check(ShopsRules.DayShare(ice, 6) == 1000 && ShopsRules.DayShare(ice, 1) == 700 && ShopsRules.DayShare(ice, 0) == 0 && ShopsRules.DayShare(ice, 8) == 0 && ShopsRules.DayShare(null, 3) == 0,
            "shops: the day's share by the game's day of the week, Monday 1 to Sunday 7, and none for a day that is not one");
        Check(ShopsRules.HelpSlug(ShopsCatalogue.TeaBar) == "businesstypes-teabar" && ShopsRules.HelpSlug(ShopsCatalogue.IceCream) == "businesstypes-icecreamparlour"
              && ShopsRules.HelpSlug("ba:businesstype_coffeeshop") == "businesstypes-coffeeshop", "shops: a kind's help page is named as the game names its own (the requirement's link)");
        Check(ShopsRules.HelpSlug(null) == null && ShopsRules.HelpSlug("nounderscore") == null && ShopsRules.HelpContentKey(null) == null && ShopsRules.HelpTextKey(" ") == null,
            "shops: no help page for a type with no name");
        Check(ShopsRules.HelpContentKey(ShopsCatalogue.Tobacconist) == "help_businesstypes-tobacconist_content" && ShopsRules.HelpTextKey(ShopsCatalogue.Tobacconist) == "reallife_shops_help_tobacconist",
            "shops: the help page's text is the mod's own key, given to the key the game's help reads");
        Check(ShopsRules.Id(ShopsCatalogue.IceCream) == DesignRules.FolderOf(ShopsCatalogue.IceCream) && ShopsRules.Id("ba:businesstype_coffeeshop") == "coffeeshop",
            "shops: a type's id is the game's GetIdWithoutType, as the designs' folders are named");
        var broken = new ShopKind { Hours = new[] { new ShopHours(0, 10, 500), new ShopHours(11, 24, 1000) } };
        var late = new ShopKind { Hours = new[] { new ShopHours(0, 23, 1000) } };
        var zero = new ShopKind { Hours = new[] { new ShopHours(0, 24, 0) } };
        Check(!ShopsRules.WholeDay(broken) && !ShopsRules.WholeDay(late) && !ShopsRules.WholeDay(zero) && !ShopsRules.WholeDay(null)
              && ShopsRules.WholeDay(new ShopKind { Hours = new[] { new ShopHours(0, 24, 1) } }),
            "shops: a gap in the bands, a day that stops short, a share of nothing or no kind is not a whole day; one band of the whole day is");
    }

    // The icons: the game's size, a shape that fills a fair part of the square, each its own, the same every time, quickly.
    private static void ShopGlyphs()
    {
        var watch = Stopwatch.StartNew();
        var seen = new List<byte[]>();
        foreach (ShopKind kind in ShopsCatalogue.Kinds)
        {
            byte[] a = ShopsRules.Glyph(kind.Glyph), b = ShopsRules.Glyph(kind.Glyph);
            int ink = 0, solid = 0;
            bool same = a.Length == b.Length;
            for (int i = 0; i < a.Length; i++)
            {
                ink += a[i];
                solid += a[i] == 255 ? 1 : 0;
                same &= a[i] == b[i];
            }
            double share = ink / 255.0 / a.Length;
            Check(a.Length == ShopsRules.Size * ShopsRules.Size && ShopsRules.Size == 50, "shops: " + DesignRules.FolderOf(kind.Type) + "'s icon is the game's 50 pixels square");
            Check(share > 0.12 && share < 0.6 && solid > 200, "shops: " + DesignRules.FolderOf(kind.Type) + "'s icon is a shape filling a fair part of its square (" + (share * 100).ToString("0") + "%)");
            Check(same, "shops: " + DesignRules.FolderOf(kind.Type) + "'s icon is drawn the same every time");
            foreach (byte[] other in seen)
            {
                int differ = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    differ += Math.Abs(a[i] - other[i]) > 128 ? 1 : 0;
                }
                Check(differ > 300, "shops: each kind's icon is its own (" + differ + " pixels apart)");
            }
            seen.Add(a);
        }
        byte[] none = ShopsRules.Glyph(99);
        bool empty = true;
        foreach (byte v in none)
        {
            empty &= v == 0;
        }
        Check(empty, "shops: an icon number with no shape draws nothing");
        Check(watch.ElapsedMilliseconds < 200, "shops: the icons are drawn in under 200 ms at the game's start (" + watch.ElapsedMilliseconds + " ms for seven)");
    }

    private static void ShopTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(DesignLocales, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(DesignLocales, "zh-cn.json"));
        Check(en != null && pt != null && zh != null, "shops: the three locales are where the checks look");
        if (en == null || pt == null || zh == null)
        {
            return;
        }
        var keys = new List<string>
        {
            ShopsText.Header, ShopsText.Enabled, ShopsText.TestSay, ShopsText.TestToday, ShopsText.TestHelp, ShopsText.SayOk, ShopsText.SayHidden, ShopsText.SayMissing,
            ShopsText.NoShop, ShopsText.Unmet, ShopsText.Closed, ShopsText.Today, ShopsText.HelpFailed,
            "reallife_opt_tip_new_shops", "reallife_opt_sec_newshops", "reallife_meta_title_shops", "reallife_guide_shops_about", "reallife_guide_shops",
            "reallife_contract_part_shop_help"
        };
        foreach (ShopKind kind in ShopsCatalogue.Kinds)
        {
            keys.Add(kind.Type);
            keys.Add(ShopsRules.HelpTextKey(kind.Type));
        }
        foreach (string key in keys)
        {
            Check(en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && !string.IsNullOrWhiteSpace(en[key]), "shops: " + key + " is in en, pt and zh-cn");
        }
        foreach (Dictionary<string, string> words in new[] { en, pt, zh })
        {
            Check(Has(words, ShopsText.SayOk, "{count}") && Has(words, ShopsText.SayMissing, "{all}") && Has(words, ShopsText.Unmet, "{need}") && Has(words, ShopsText.Unmet, "{name}")
                  && Has(words, ShopsText.Closed, "{name}") && Has(words, ShopsText.Today, "{wanting}") && Has(words, ShopsText.Today, "{count}"),
                "shops: the notices keep their places for the numbers and names in every language");
        }
        foreach (ShopKind kind in ShopsCatalogue.Kinds)
        {
            string page = en.TryGetValue(ShopsRules.HelpTextKey(kind.Type) ?? string.Empty, out string text) ? text : string.Empty;
            bool links = page.Length > 0;
            foreach (ShopGood good in kind.Goods)
            {
                links &= page.Contains("(products-" + good.Item.Substring(ShopsCatalogue.ItemPrefix.Length) + ")");
            }
            foreach (string lead in kind.Leads)
            {
                links &= page.Contains("(furniture-" + lead + ")");
            }
            Check(links, "shops: " + DesignRules.FolderOf(kind.Type) + "'s help page links every good it sells and the furniture that shows its primary goods, as the game's pages do");
            foreach (Dictionary<string, string> words in new[] { pt, zh })
            {
                string other = words.TryGetValue(ShopsRules.HelpTextKey(kind.Type) ?? string.Empty, out string t) ? t : string.Empty;
                bool same = true;
                foreach (ShopGood good in kind.Goods)
                {
                    same &= other.Contains("(products-" + good.Item.Substring(ShopsCatalogue.ItemPrefix.Length) + ")");
                }
                Check(same, "shops: " + DesignRules.FolderOf(kind.Type) + "'s translated help page keeps the game's links");
            }
        }
        foreach (string key in new[] { ShopsText.SayOk, ShopsText.SayHidden, ShopsText.SayMissing, ShopsText.NoShop, ShopsText.Unmet, ShopsText.Closed, ShopsText.Today, ShopsText.HelpFailed })
        {
            Check(en.TryGetValue(key, out string said) && said.Length <= 240, "shops: the notice " + key + " is at most 240 characters (" + (said ?? string.Empty).Length + ")");
        }
        Check(en.TryGetValue("reallife_changes_next", out string next) && next.Contains("ice cream parlour") && next.Contains("tobacconist"), "shops: What's new's next row has the new kinds of shop");
    }

    // Where the feature is wired in, and the other features that sort a business by kind know the new ones.
    private static void ShopWiring()
    {
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), game = ModSource(@"Shops\ShopsGame.cs");
        Check(mod.Contains("Guard.Step(\"the new kinds of shop\", Shops.ShopsDirector.Start);") && mod.Contains("Guard.Step(\"the new kinds of shop\", Shops.ShopsDirector.CityLoaded);")
              && mod.IndexOf("Shops.ShopsDirector.Start", StringComparison.Ordinal) < mod.IndexOf("public Task OnUnloadAsync", StringComparison.Ordinal),
            "shops: the kinds are registered at the game's start, before a save is read, and furnished at each city load");
        Check(ModSource(@"Meta\SaveTools.Steps.cs").Contains("Shops.ShopsGame.Strip(save)"), "shops: preparing a save for removing the mod makes each shop the game's own type");
        Check(game.Contains("ModdingAPI.RegisterModBusinessType(t)") && game.Contains("if (offered)") && !game.Contains("void Update(") && !game.Contains("MonoBehaviour"),
            "shops: the types are registered through the game's mod interface, offered by the option, with nothing per frame");
        Check(game.IndexOf("t.simulator = like.simulator;", StringComparison.Ordinal) > game.IndexOf("t.businessRequirements = like.businessRequirements", StringComparison.Ordinal),
            "shops: a kind trades only once everything it trades like is in place (its simulation is set last)");
        string director = ModSource(@"Shops\ShopsDirector.cs");
        Check(game.Contains("_onLanguage = Guard.Wrap(Rewrite);") && director.Contains("ShopsGame.Listen(true);") && director.Contains("ShopsGame.Listen(false);") && mod.Contains("Shops.ShopsDirector.Stop();")
              && ModSource(@"Core\GameContract.cs").Contains("\"Localizor.LocalizorManager\", \"LocalizationDictionary\""),
            "shops: the help pages are written into the game's language tables through the contract after each load of a language, by a guarded handler taken off again when the mod goes");
        Check(ModSource(@"Business\BusinessGame.cs").Contains("type.Contains(\"icecreamparlour\") || type.Contains(\"teabar\")")
              && ModSource(@"Economy\Company\CompanyGame.cs").Contains("\"icecreamparlour\", \"teabar\" }")
              && ModSource(@"Utilities\UtilitiesGame.cs").Contains("case \"reallife_teabar\":")
              && ModSource(@"Economy\Ventures\VenturesCatalogue.cs").Contains("\"icecream\", \"teabar\" }"),
            "shops: food inspections, food licences, power and water, and investors count the ice cream parlour and the tea bar as food and drink");
    }

    // A shop's cut of one of the game's designs on a made-up room: shelves for other goods go with what stood on them, the
    // shelves for its goods, the till and the washroom stay, and only a design with a till and a lead shelf is offered.
    private static void DesignShopCuts()
    {
        ShopKind smoke = ShopsCatalogue.Of(ShopsCatalogue.Tobacconist), tea = ShopsCatalogue.Of(ShopsCatalogue.TeaBar);
        var items = new List<DesignItem>
        {
            Item("wine", "wineshelf", 300, 0, 300),
            Item("bottle", "decorativedrinksshelf", 300, 100, 300, "wine"),
            Item("tobacco", "tobaccoshelf1", 500, 0, 300),
            Item("fridge", "drinksfridge", 600, 0, 300),
            Item("counter", "counter1", 700, 0, 300),
            Item("till", "cashregister", 700, 90, 300, "counter"),
            Item("baskets", "stackofshoppingbaskets", 800, 0, 300),
            Item("wc", "toiletstall", 900, 0, 300),
            Item("sink", "sink", 950, 0, 300),
            Item("plant", "groundplant02", 400, 0, 600),
            Item("spirits", "drinksshelf", 450, 0, 600),
            Item("stray", "groundplant03", 10, 0, 10)
        };
        ShowroomCut cut = DesignRules.StockCut(items, smoke);
        Check(cut.Kept.Contains("tobacco") && cut.Kept.Contains("fridge") && cut.Kept.Contains("till") && cut.Kept.Contains("baskets") && cut.Kept.Contains("wc") && cut.Kept.Contains("plant"),
            "designs: a tobacconist keeps its tobacco shelf, the drinks fridge, the till on its counter, the baskets, the washroom and the plants");
        Check(!cut.Kept.Contains("wine") && !cut.Kept.Contains("spirits") && !cut.Kept.Contains("bottle") && !cut.Kept.Contains("stray"),
            "designs: a tobacconist leaves out the wine and spirits shelves, what stood on them and a stray at the origin");
        Check(cut.Dropped == 2 && cut.Points == 1 && cut.Leads == 1 && cut.Toilets == 1 && cut.ValidShop, "designs: the cut counts the shelves left out (" + cut.Dropped + "), its till, its lead shelf, and is offered");
        items.RemoveAt(2);
        Check(!DesignRules.StockCut(items, smoke).ValidShop, "designs: a tobacconist's design with no tobacco shelf is not offered");
        items.RemoveAt(4);
        Check(DesignRules.StockCut(items, smoke).Points == 0, "designs: a design without its till has nowhere to pay");
        Check(DesignRules.StockCut(items, null).Kept.Count == 0 && DesignRules.Restocked(null, "ba:itemname_cupofcoffee") == "ba:itemname_cupofcoffee",
            "designs: no kind keeps nothing, and no cut restocks nothing");
        ShowroomCut teaCut = DesignRules.StockCut(new List<DesignItem> { Item("machine", "industrialcoffeemachine", 100, 90, 100) }, tea);
        Check(DesignRules.Restocked(teaCut, "ba:itemname_cupofcoffee") == "ba:itemname_cupoftea" && DesignRules.Restocked(teaCut, "ba:itemname_donut") == "ba:itemname_donut"
              && DesignRules.Restocked(DesignRules.StockCut(items, smoke), "ba:itemname_cigar") == "ba:itemname_cigar",
            "designs: a tea bar's coffee machines are set to pour tea, and nothing else is restocked");

        // One of each name, the donors' order, at most six, only those offered.
        var sources = new List<DesignSource>();
        var cuts = new List<ShowroomCut>();
        var good = new ShowroomCut { Furniture = 10, Toilets = 1, Points = 1, Leads = 1 };
        var bad = new ShowroomCut { Furniture = 10, Toilets = 1, Points = 1, Leads = 0 };
        string[] names = { "Modern", "modern", "Dark", "Sky", "Rustic", "Classic", "Lemon", "Plum", "Bare" };
        for (int i = 0; i < names.Length; i++)
        {
            sources.Add(Source(8, 0, "C1", names[i], "InteriorDesigns/Retail/C1/LiquorStore/" + names[i] + i + ".json"));
            cuts.Add(names[i] == "Bare" ? bad : i == 2 ? null : good);
        }
        var picks = new List<int>();
        DesignRules.Stocks(sources, cuts, picks);
        Check(picks.Count == DesignCatalogue.ShopMost && picks[0] == 0 && picks[1] == 3 && !picks.Contains(1) && !picks.Contains(2) && !picks.Contains(8),
            "designs: a shop is offered one design of each name, in the donors' order, at most " + DesignCatalogue.ShopMost + ", none unreadable or without a lead shelf (" + string.Join(",", picks) + ")");
    }
}
