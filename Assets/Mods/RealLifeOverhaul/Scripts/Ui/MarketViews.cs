using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A district as the game's Market Insider lists it: its key and its name in the game's words.</summary>
    public struct MarketDistrict
    {
        public string Key;
        public string Name;
    }

    /// <summary>One product's demand in the district as the game's table holds it.</summary>
    public struct MarketDemand
    {
        public string Product;
        public int Demand;           // percent
        public string Lowest;        // the lowest market price in the game's words, or null where nobody sells it
        public float Import;         // the import price index
        public bool Service;         // a service has no import price
        public int Providers;        // the businesses selling it
    }

    /// <summary>One of the district's market events, as the game words it.</summary>
    public struct MarketEvent
    {
        public string Kind;          // the game's event type, for its glyph ("Hype")
        public string Title;
        public int Day;
        public string Body;
    }

    /// <summary>
    /// Market Insider as the game filled it: the districts it lists and the one chosen, its people by income, its
    /// buildings' prices in the game's words, the shown table (demand or real estate) in the game's order with its filter,
    /// search and sort, its events, and an open filter's list.
    /// </summary>
    public sealed class MarketView
    {
        public MarketDistrict[] Districts = new MarketDistrict[0];
        public int District;
        public int Working, Middle, Upper;          // percent
        public string[] EstateLabels = new string[0];
        public string[] EstateValues = new string[0];
        public int Table;                           // UiMarket.Demands, RealEstate or Events
        public MarketDemand[] Demands = new MarketDemand[0];
        public RivalBuilding[] Places = new RivalBuilding[0];
        public int All;                             // the table's rows before its filter and search
        public bool ForSaleOnly;
        public string Filter;                       // the filter's value in words
        public string Query;
        public bool Filtered;
        public int SortColumn = -1;
        public bool SortDown;
        public int Selected = -1;
        public float Offset;
        public int Reveal = -1;
        public MarketEvent[] Events = new MarketEvent[0];
        public int EventSelected = -1;
        public float EventOffset;
        public int EventReveal = -1;
        public int OpenList = -1;
        public string[] Options = new string[0];
        public int OptionChosen = -1;
        public bool[] OptionTicks;                 // the filter's types that stand on, or null for a list of one
        public float OptionOffset;
        public Func<float, string> Money;
    }

    /// <summary>
    /// The Market Insider view into the app's model, and the glyphs the market's things wear: a district's in its colour,
    /// a business type's by its family, a building type's, an event's by its kind. Game-free: the game's screen and the
    /// samples hand it the same view. Cost: a few small arrays a layout.
    /// </summary>
    public static class MarketViews
    {
        public const string App = "MarketInsider";
        private const float ImportLow = 0.5f, ImportHigh = 1.3f;   // the game's bounds of an import price index (ProductMarketHelper clamps it so)

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Market(UiMarketModel m, UiWords t, MarketView v)
        {
            m.Title = t("reallife_ui_market_app", "Market Insider");   // the game has only its joined name ("MarketInsider") for the app
            m.Icon = PhoneLooks.Glyph(App);
            m.Districts = new UiNavEntry[v.Districts.Length];
            for (int i = 0; i < v.Districts.Length; i++)
            {
                m.Districts[i] = new UiNavEntry { Icon = UiIcon.MapPin, Tint = DistrictTint(v.Districts[i].Key), Label = v.Districts[i].Name };
            }
            m.District = v.District;
            m.CitizenTitle = t("market_insider_citizen_data", "Citizen Data");
            m.ClassLabels = new[] { t("socialclass_working", "Working class"), t("socialclass_middle", "Middle class"), t("socialclass_upper", "Upper class") };
            m.ClassValues = new[] { Percent(v.Working), Percent(v.Middle), Percent(v.Upper) };
            m.ClassShares = new float[] { Math.Max(0, v.Working), Math.Max(0, v.Middle), Math.Max(0, v.Upper) };
            m.EstateTitle = t("market_insider_real_estate_data", "Real Estate Data");
            m.EstateLabels = v.EstateLabels ?? new string[0];
            m.EstateValues = v.EstateValues ?? new string[0];
            m.MapLink = t("phone_view_on_map_button", "View on map");
            m.TableTabs = new[] { t("common_market_demands", "Market Demands"), t("common_real_estate", "Real Estate"), t("common_events", "Events") };
            m.TableIcons = new[] { UiIcon.ChartLineUp, UiIcon.Building, UiIcon.Newspaper };
            m.TableTab = v.Table == UiMarket.RealEstate || v.Table == UiMarket.Events ? v.Table : UiMarket.Demands;
            m.SwitchLabel = m.TableTab == UiMarket.RealEstate ? t("market_insider_show_only_for_sale", "Show only for sale") : null;
            m.SwitchOn = v.ForSaleOnly;
            m.Table = new UiDataTableModel();
            if (m.TableTab == UiMarket.RealEstate)
            {
                Estate(m.Table, t, v);
            }
            else if (m.TableTab == UiMarket.Demands)
            {
                DemandTable(m.Table, t, v);
            }
            UiDataTableModel table = m.Table;
            table.SortColumn = v.SortColumn;
            table.SortDown = v.SortDown;
            table.Selected = m.TableTab == UiMarket.RealEstate ? v.Selected : -1;   // a building opens; a demand is read only, so none stands chosen
            table.Offset = v.Offset;
            table.Reveal = v.Reveal;
            table.Filters = new[] { v.Filter ?? t(m.TableTab == UiMarket.RealEstate ? "reallife_ui_market_all_buildings" : "reallife_ui_market_all_types",
                m.TableTab == UiMarket.RealEstate ? "All building types" : "All business types") };
            table.SearchPlaceholder = t("common_search", "Search");
            table.Query = string.IsNullOrEmpty(v.Query) ? null : v.Query;
            table.Count = StaffViews.Fill(t("reallife_ui_staff_count", "{shown} of {all}"), "shown", Whole(table.RowCount), "all", Whole(Math.Max(v.All, table.RowCount)));
            if (v.Filtered)
            {
                table.EmptyIcon = UiIcon.Search;
                table.EmptyTitle = t("reallife_ui_market_none_match", "Nothing matches");
                table.EmptyBody = t("reallife_ui_market_none_match_body", "Nothing here matches this filter and this search. Clear them to see the whole district again.");
                table.EmptyAction = t("reallife_ui_staff_clear", "Clear filters");
            }
            Events(m.Events, t, v);
            m.OpenList = v.OpenList;
            m.Options = v.Options ?? new string[0];
            m.OptionChosen = v.OptionChosen;
            m.OptionTicks = v.OptionTicks;
            m.OptionOffset = v.OptionOffset;
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_market_prompt_district", "District") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            };
            if (v.OpenList >= 0)
            {
                // An open list has the keys: the districts' step is the layer's under it, so it is not offered.
                m.Prompts = new[] { m.Prompts[0], m.Prompts[1], m.Prompts[3] };
            }
        }

        // Each product: its name, its demand as a graded meter, the lowest price, the import price graded (a cheaper import
        // the better for the player who buys it in), and how many sell it. What pushes a demand stands in the events.
        private static void DemandTable(UiDataTableModel table, UiWords t, MarketView v)
        {
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("common_product", "Product"), Kind = UiCellKind.Text, Sortable = true },
                new UiDataColumn { Title = t("marketinsider_demand", "Demand"), Kind = UiCellKind.Meter, Width = 160f, Sortable = true },
                new UiDataColumn { Title = t("marketinsider_lowest_market_price", "Lowest Market Price"), Kind = UiCellKind.Money, Width = 136f, Sortable = true },
                new UiDataColumn { Title = t("marketinsider_import_price_index", "Import Price Index"), Kind = UiCellKind.Figure, Width = 128f, Sortable = true, Graded = true },
                new UiDataColumn { Title = t("marketinsider_providers", "Providers"), Kind = UiCellKind.Figure, Width = 96f, Sortable = true },
            };
            table.Rows = new UiDataRow[v.Demands.Length];
            for (int i = 0; i < v.Demands.Length; i++)
            {
                ref MarketDemand d = ref v.Demands[i];
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[]
                    {
                        d.Product, Whole(d.Demand), d.Lowest ?? t("reallife_ui_market_no_seller", "None sold"),
                        d.Service ? t("reallife_ui_market_service", "Service") : d.Import.ToString("0.0", CultureInfo.CurrentCulture), Whole(d.Providers)
                    },
                    Values = new[] { -1, Math.Max(0, Math.Min(100, d.Demand)), -1, d.Service ? -1 : ImportGrade(d.Import), -1 }
                };
            }
            table.RowCount = table.Rows.Length;
            table.EmptyIcon = UiIcon.ShoppingCart;
            table.EmptyTitle = t("reallife_ui_market_no_demand", "No demand to show");
            table.EmptyBody = t("reallife_ui_market_no_demand_body", "This district asks for nothing the businesses you can run sell. Another district may.");
            table.EmptyAction = null;
        }

        /// <summary>An import price index graded 0 to 100: the game's lowest the best, its highest the poorest.</summary>
        public static int ImportGrade(float index) => (int)Math.Round(100f * Math.Max(0f, Math.Min(1f, (ImportHigh - index) / (ImportHigh - ImportLow))));

        // The district's buildings: address, type with its glyph, size, value and price where it is for sale.
        private static void Estate(UiDataTableModel table, UiWords t, MarketView v)
        {
            table.Columns = new[]
            {
                new UiDataColumn { Title = t("common_address", "Address"), Kind = UiCellKind.Tile, Sortable = true },
                new UiDataColumn { Title = t("common_building_type", "Building Type"), Kind = UiCellKind.Text, Width = 160f, Sortable = true },
                new UiDataColumn { Title = t("bizman_building_total_size", "Total size"), Kind = UiCellKind.Figure, Width = 112f, Sortable = true },
                new UiDataColumn { Title = t("real_estate_estimated_value", "Estimated value"), Kind = UiCellKind.Money, Width = 136f, Sortable = true },
                new UiDataColumn { Title = t("common_price", "Price"), Kind = UiCellKind.Money, Width = 120f, Sortable = true },
            };
            var rows = new RivalsView { Money = v.Money };
            table.Rows = new UiDataRow[v.Places.Length];
            for (int i = 0; i < v.Places.Length; i++)
            {
                UiDataRow full = RivalsViews.Building(t, rows, ref v.Places[i]);
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[] { full.Cells[0], full.Cells[2], full.Cells[3], full.Cells[4], full.Cells[5] },
                    Values = new[] { full.Values[0], -1, -1, -1, -1 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }
                };
            }
            table.RowCount = table.Rows.Length;
            table.EmptyIcon = UiIcon.Building;
            table.EmptyTitle = v.ForSaleOnly ? t("reallife_ui_market_none_for_sale", "Nothing for sale here") : t("reallife_ui_market_no_buildings", "No buildings to show");
            table.EmptyBody = v.ForSaleOnly
                ? t("reallife_ui_market_none_for_sale_body", "No building in this district is on the market now. Turn the switch off to see them all.")
                : t("reallife_ui_market_no_buildings_body", "This district's buildings list here with their size, value and price.");
            table.EmptyAction = null;
        }

        // The events, newest first as the game lists them: each its kind's glyph, what it was, its day and its sentence.
        private static void Events(UiEventListModel list, UiWords t, MarketView v)
        {
            list.Rows = new UiEventRow[v.Events.Length];
            for (int i = 0; i < v.Events.Length; i++)
            {
                ref MarketEvent e = ref v.Events[i];
                UiIcon glyph = EventIcon(e.Kind);
                list.Rows[i] = new UiEventRow
                {
                    Icon = glyph, Tint = UiIconTints.Of(glyph), Title = e.Title, Body = e.Body,
                    When = StaffViews.Fill(t("common_day_number", "Day {number}"), "number", Whole(e.Day))
                };
            }
            list.RowCount = list.Rows.Length;
            list.Selected = -1;   // read only: the cursor walks and scrolls the events, none stands chosen
            list.Offset = v.EventOffset;
            list.Reveal = v.EventReveal;
            list.EmptyIcon = UiIcon.Newspaper;
            list.EmptyTitle = t("reallife_ui_market_no_events", "Nothing has happened here yet");
            list.EmptyBody = t("reallife_ui_market_no_events_body", "Openings, closures, hype and shortages in this district show here by the day.");
        }

        /// <summary>A district's colour on its glyph: the game's map colour as the nearest chart hue, the mod's own districts theirs.</summary>
        public static UiRole DistrictTint(string key)
        {
            switch (key)
            {
                case "ba:neighborhood_garmentdistrict": return UiRole.Chart3;   // the game's green
                case "ba:neighborhood_hellskitchen": return UiRole.Chart5;      // its blue
                case "ba:neighborhood_industrycity": return UiRole.Chart2;      // its orange
                case "ba:neighborhood_lowermanhattan": return UiRole.Chart1;    // its cyan
                case "ba:neighborhood_midtown": return UiRole.Chart7;           // its magenta
                case "ba:neighborhood_murrayhill": return UiRole.Chart6;        // its red
                case "ba:neighborhood_thehamptons": return UiRole.Chart4;       // its gold
                default:
                    if (key != null && key.IndexOf("cedar", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return UiRole.CatTech;    // Cedar Heights' violet
                    }
                    // Uptown's mint; a district the mod does not know, the quiet grey
                    return key != null && key.IndexOf("uptown", StringComparison.OrdinalIgnoreCase) >= 0 ? UiRole.CatServices : UiRole.Text2;
            }
        }

        /// <summary>A business type's glyph by its family; one the kit does not know the shop.</summary>
        public static UiIcon BusinessIcon(string key)
        {
            string k = Kind(key);
            switch (k)
            {
                case "coffeeshop": return UiIcon.Coffee;
                case "fastfoodrestaurant": return UiIcon.ForkKnife;
                case "supermarket": case "fruitandvegetablestore": case "liquorstore": case "wholesalestore": return UiIcon.ShoppingCart;
                case "clothingstore": case "giftshop": case "jewelrystore": case "florist": case "bookstore": return UiIcon.ShoppingBag;
                case "electronicsstore": case "appliancestore": return UiIcon.Cpu;
                case "furniturestore": return UiIcon.Sofa;
                case "officesupplystore": return UiIcon.Package;
                case "lawfirm": return UiIcon.Scales;
                case "marketingagency": case "eventplanningagency": return UiIcon.Megaphone;
                case "webdevelopmentagency": case "graphicdesigner": return UiIcon.Code;
                case "recruitmentagency": return UiIcon.IdBadge;
                case "headquarters": case "travelagency": return UiIcon.Briefcase;
                case "bank": return UiIcon.Bank;
                case "warehouse": case "importexport": return UiIcon.Package;
                case "factory": return UiIcon.Factory;
                case "autorepairshop": case "cardealership": case "gasstation": case "truckgarage": case "privatedriverservice": return UiIcon.Car;
                case "movingservice": return UiIcon.Truck;
                case "interiorinstallationfirm": return UiIcon.Hammer;
                case "hairdresser": return UiIcon.PaintBrush;
                case "gym": return UiIcon.Heartbeat;
                case "nightclub": return UiIcon.MusicNotes;
                case "casino": return UiIcon.Star;
                case "cinema": case "theater": return UiIcon.Camera;
                case "clinic": case "hospital": return UiIcon.Heart;
                default: return UiIcon.Store;
            }
        }

        /// <summary>A building type's glyph: a shop, an office, a warehouse, a home; anything else the plain building.</summary>
        public static UiIcon BuildingIcon(string key)
        {
            switch (Kind(key))
            {
                case "retail": return UiIcon.Store;
                case "office": return UiIcon.Briefcase;
                case "warehouse": return UiIcon.Package;
                case "residential": return UiIcon.Home;
                default: return UiIcon.Building;
            }
        }

        /// <summary>A market event's glyph by its kind (the game's MarketEventType's name).</summary>
        public static UiIcon EventIcon(string kind)
        {
            switch (kind)
            {
                case "BusinessOpened": return UiIcon.Store;
                case "BusinessClosed": return UiIcon.Building;
                case "Hype": return UiIcon.Fire;
                case "ProductShortage": return UiIcon.Package;
                case "ProductBackorder": return UiIcon.Truck;
                case "LargePlayerPurchase": case "MaxProvidersExceeded": return UiIcon.Factory;
                default: return UiIcon.Info;
            }
        }

        /// <summary>
        /// The game's rich text as plain words, the words inside its lower-case tag written in lower case as the game draws
        /// them ("Average &lt;lowercase&gt;Retail&lt;/lowercase&gt; price" reads "Average retail price"). Cost: one pass, once a layout.
        /// </summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text;
            }
            var sb = new System.Text.StringBuilder(text.Length);
            bool lower = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int close = c == '<' ? text.IndexOf('>', i + 1) : -1;
                if (close > i)
                {
                    string tag = text.Substring(i + 1, close - i - 1);
                    lower = tag == "lowercase" ? true : tag == "/lowercase" ? false : lower;
                    i = close;
                    continue;
                }
                sb.Append(lower ? char.ToLowerInvariant(c) : c);
            }
            return sb.ToString();
        }

        // A type key's last word: "retail" of "ba:buildingtype_retail".
        private static string Kind(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            int cut = key.LastIndexOf('_');
            return cut >= 0 ? key.Substring(cut + 1) : key;
        }

        private static string Percent(int n) => n.ToString(Inv) + "%";

        private static string Whole(int n) => n.ToString(Inv);
    }
}
