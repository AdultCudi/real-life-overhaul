using RealLifeOverhaul.Ui;

/// <summary>
/// BizMan on the kit in every state the brief asks for: the portfolio with ten businesses (a filter open, the warehouses,
/// none at all, long names), a store's page on each of its sections (its business list and its needs open, the settings
/// with changes not saved, the schedule and another mod's page handed to the game's own view), a warehouse's, a factory's
/// and a headquarters' page with the game's own entries, starting a business, a building to rent, another's shop with the
/// offers, a house in the Hamptons, and a store with long names everywhere. The words are the locale's in the language
/// given (the game's own keys where the game has the words), the figures the samples' dollars.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] BizManStates =
    {
        "portfolio", "portfolio_filter_open", "portfolio_warehouses", "portfolio_empty", "portfolio_long", "store_building", "store_insight", "store_pricing",
        "store_deliveries", "store_marketing", "store_settings", "store_settings_unsaved", "store_estate", "store_switch_open", "store_needs_open", "store_classic",
        "store_foreign", "store_interior", "warehouse", "factory", "headquarters", "start", "rent", "rival", "villa", "store_long", "portfolio_german", "store_german", "store_logo", "store_logo_colours"
    };

    private static readonly string[] Businesses =
    {
        "Bloom Café", "Harbour Books", "Northside Fitness", "Copper Kettle Coffee", "Union Square Florist", "Tidewater Electronics", "Little Italy Bakery",
        "Riverside Gifts", "Hudson Jewellers", "Garment Row Clothing"
    };

    private static readonly string[] Kinds =
    {
        "ba:businesstype_coffeeshop", "ba:businesstype_bookstore", "ba:businesstype_gym", "ba:businesstype_coffeeshop", "ba:businesstype_florist",
        "ba:businesstype_electronicsstore", "ba:businesstype_fastfoodrestaurant", "ba:businesstype_giftshop", "ba:businesstype_jewelrystore", "ba:businesstype_clothingstore"
    };

    private static readonly string[] KindWords =
    {
        "Coffee Shop", "Bookstore", "Gym", "Coffee Shop", "Florist", "Electronics Store", "Fast Food Restaurant", "Gift Shop", "Jewelry Store", "Clothing Store"
    };

    private static readonly string[] Districts =
    {
        "Midtown", "Lower Manhattan", "Hell's Kitchen", "Murray Hill", "Midtown", "Garment District", "Lower Manhattan", "Hell's Kitchen", "Midtown", "Garment District"
    };

    private static readonly string[] LongNames =
    {
        "Bloom Café and Patisserie of Greenwich", "Harbour Books and Rare Prints Emporium", "Northside Fitness and Wellness Centre", "Copper Kettle Coffee Roasting House",
        "Union Square Florist and Garden Supply", "Tidewater Electronics and Repair Workshop", "Little Italy Bakery and Pastry Kitchen", "Riverside Gifts and Souvenir Boutique",
        "Hudson Jewellers and Watchmakers of Fifth", "Garment Row Clothing and Tailoring Company"
    };

    private static readonly int[] Daily = { 410, 1260, 3410, 980, 610, -140, 1920, 450, 5120, 2210 };
    private static readonly int[] Satisfied = { 82, 74, 91, 66, 58, 41, 77, 63, 88, 70 };
    private static readonly int[] Alerting = { 1, 1, 0, 2, 0, 3, 0, 0, 1, 0 };   // the café's one: its barista to hire

    /// <summary>A state of BizMan by its name, as the previews and the checks lay each one out.</summary>
    public static BizManModel BizMan(UiWords t, string state)
    {
        switch (state)
        {
            case "portfolio": return Portfolio(t, 10, false, -1, 0);
            case "portfolio_filter_open": return Portfolio(t, 10, false, 0, 0);
            case "portfolio_warehouses": return Portfolio(t, 10, false, -1, 2);
            case "portfolio_empty": return Portfolio(t, 0, false, -1, 0);
            case "portfolio_long": return Portfolio(t, 10, true, -1, 0);
            case "portfolio_german": return Portfolio(t, 10, false, -1, 0);
            case "store_german": return Store(t, BizManPart.Settings, state);
            case "store_insight": return Store(t, BizManPart.Insight, state);
            case "store_pricing": return Store(t, BizManPart.Pricing, state);
            case "store_deliveries": return Store(t, BizManPart.Deliveries, state);
            case "store_marketing": return Store(t, BizManPart.Marketing, state);
            case "store_settings": case "store_settings_unsaved": case "store_logo": case "store_logo_colours": return Store(t, BizManPart.Settings, state);
            case "store_estate": return Store(t, BizManPart.Estate, state);
            case "store_classic": case "store_foreign": return Store(t, BizManPart.Classic, state);
            case "store_interior": return Store(t, BizManPart.Interior, state);
            case "warehouse": return Building(t, "warehouse");
            case "factory": return Building(t, "factory");
            case "headquarters": return Building(t, "headquarters");
            case "start": return Start(t);
            case "rent": return Rent(t);
            case "rival": return Rival(t);
            case "villa": return Villa(t);
            default: return Store(t, BizManPart.Building, state);
        }
    }

    // ------------------------------------------------------------------------------------------------- the portfolio

    private static BizManModel Portfolio(UiWords t, int count, bool longNames, int openFilter, int group)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Portfolio };
        m.Nav = new[]
        {
            new UiNavEntry { Icon = UiIcon.Store, Tint = UiIconTints.Of(UiIcon.Store), Label = t("common_businesses", "Businesses"), Count = Whole(count) },
            new UiNavEntry { Icon = UiIcon.Home, Tint = UiIconTints.Of(UiIcon.Home), Label = t("reallife_ui_bizman_homes", "Homes"), Count = count > 0 ? "1" : null },
            new UiNavEntry { Icon = UiIcon.Package, Tint = UiIconTints.Of(UiIcon.Package), Label = t("bizman_warehouses", "Warehouses"), Count = count > 0 ? "2" : null },
            new UiNavEntry { Icon = UiIcon.Building, Tint = UiIconTints.Of(UiIcon.Building), Label = t("bizman_headquarters", "Headquarters"), Count = count > 0 ? "1" : null },
        };
        if (count == 0)
        {
            m.Nav = new[] { m.Nav[0] };
        }
        m.NavSelected = group;
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += Daily[i];
        }
        if (count > 0 && group == 0)
        {
            m.SummaryIcons = new[] { UiIcon.Store, UiIcon.SmileyHappy, UiIcon.Trophy };
            m.Summary = new[]
            {
                t("reallife_ui_bizman_sum_open", "{open} of {count} open now").Replace("{open}", Whole(count - 2)).Replace("{count}", Whole(count)),
                t("reallife_ui_bizman_sum_mood", "Customers {percent}% satisfied on average").Replace("{percent}", "71"),
                t("reallife_ui_bizman_sum_best", "Earns the most: {name}").Replace("{name}", longNames ? LongNames[8] : Businesses[8])
            };
        }
        m.Table = group == 2 ? Warehouses(t) : ShopTable(t, count, longNames);
        if (openFilter >= 0)
        {
            m.FilterOpen = openFilter;
            m.FilterOptions = new[]
            {
                t("reallife_ui_bizman_any_district", "All districts"), "Midtown", "Hell's Kitchen", "Garment District", "Murray Hill", "Lower Manhattan", "Industry City"
            };
            m.FilterChosen = 0;
        }
        m.Prompts = BizManPrompts(t, true);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static UiDataTableModel ShopTable(UiWords t, int count, bool longNames)
    {
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("common_business_name", "Business name"), Kind = UiCellKind.Tile, Sortable = true },
                new UiDataColumn { Title = t("common_neighborhood", "Neighborhood"), Kind = UiCellKind.Text, Width = 200f },
                new UiDataColumn { Title = t("bizman_avg_daily_income", "Avg Daily Income"), Kind = UiCellKind.Money, Width = 152f, Sortable = true },
                new UiDataColumn { Title = t("bizman_insight_satisfaction", "Satisfaction"), Kind = UiCellKind.Meter, Width = 168f, Sortable = true },
                new UiDataColumn { Title = t("bizman_alerts", "Alerts"), Kind = UiCellKind.Figure, Width = 88f, Sortable = true },
                new UiDataColumn { Title = t("bizman_status", "Status"), Kind = UiCellKind.Chip, Width = 216f, Sortable = true },
                Destination(),
            },
            Filters = new[]
            {
                t("reallife_ui_bizman_any_district", "All districts"), t("reallife_ui_bizman_any_kind", "Any kind"), t("reallife_ui_bizman_any_building", "Any building"),
                t("reallife_ui_bizman_any_status", "Any status")
            },
            SearchPlaceholder = t("reallife_ui_bizman_search", "Search businesses"), SortColumn = 2, SortDown = true, Count = Whole(count),
            EmptyTitle = t("reallife_ui_bizman_none_title", "No businesses yet"),
            EmptyBody = t("reallife_ui_bizman_none_body", "Rent a building on the map or in the Buildings for sale list, then start a business there. It shows here."),
            EmptyIcon = UiIcon.Store
        };
        table.Rows = new UiDataRow[count];
        var order = new int[count];
        for (int k = 0; k < count; k++)
        {
            order[k] = k;
        }
        System.Array.Sort(order, (a, b) => Daily[b].CompareTo(Daily[a]));   // sorted as the head shows: income, highest first
        for (int k = 0; k < count; k++)
        {
            int i = order[k];
            string name = longNames ? LongNames[i] : Businesses[i];
            bool closed = i == 5;
            UiIcon icon = BizManRules.KindIcon(Kinds[i], "ba:buildingtype_retail");
            table.Rows[k] = new UiDataRow
            {
                Cells = new[] { name, Districts[i], Signed(Daily[i]), Satisfied[i].ToString(), Alerting[i] > 0 ? Alerting[i].ToString() : "0",
                    closed ? t("bizman_temporarily_closed", "Temporarily closed") : i == 3 ? t("common_closed", "Closed") : t("common_open", "Open"),
                    t("common_setdestination", "Set destination") },
                Values = new[] { (int)icon, 0, 0, Satisfied[i], -1, 0, (int)UiIcon.Locate },
                Tones = new[] { UiIconTints.Of(icon), UiRole.None, UiRole.None, UiRole.None, UiRole.None, closed ? UiRole.Warn : i == 3 ? UiRole.Muted : UiRole.Ok, UiRole.None },
                Sub = t(Kinds[i], KindWords[i])   // the game's own word for the kind, in the player's language
            };
        }
        table.RowCount = count;
        if (count > 0)
        {
            table.TotalsLabel = t("common_total", "Total");
            table.Totals = new string[] { null, null, Dollars(System.Linq.Enumerable.Sum(Daily)), null, Whole(System.Linq.Enumerable.Sum(Alerting)), null, null };
        }
        return table;
    }

    private static UiDataTableModel Warehouses(UiWords t)
    {
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("bizman_warehouses", "Warehouses"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_ui_bizman_runs_out", "First to run out"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("bizman_inventory_days_until_empty", "Days until empty"), Kind = UiCellKind.Figure, Width = 176f },
                new UiDataColumn { Title = t("bizman_vehicle_slots", "Vehicle Slots"), Kind = UiCellKind.Figure, Width = 136f },
                new UiDataColumn { Title = t("bizman_drivers", "Drivers"), Kind = UiCellKind.Figure, Width = 112f },
                Destination(),
            },
            Filters = new string[0], RowCount = 2, EmptyTitle = t("reallife_ui_bizman_none_title", "No businesses yet"), EmptyIcon = UiIcon.Package
        };
        table.Rows = new[]
        {
            new UiDataRow { Cells = new[] { "Pier 40 Storage", "Croissant", "4", "3", "2", null },
                Values = new[] { (int)UiIcon.Package, 0, -1, -1, -1, (int)UiIcon.Locate },
                Tones = new[] { UiIconTints.Of(UiIcon.Package), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                Sub = "112 West Street" },
            new UiDataRow { Cells = new[] { "Industry City Depot", "Paper bag", "12", "4", "4", null },
                Values = new[] { (int)UiIcon.Package, 0, -1, -1, -1, (int)UiIcon.Locate },
                Tones = new[] { UiIconTints.Of(UiIcon.Package), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                Sub = "48 Second Avenue" },
        };
        return table;
    }

    // A row's Set destination at its right, as the game's own rows offer it.
    private static UiDataColumn Destination() => new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = UiTokens.ControlSmall };

    private static UiPrompt[] BizManPrompts(UiWords t, bool portfolio)
    {
        return new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = portfolio ? t("reallife_ui_prompt_group", "Group") : t("reallife_ui_bizman_prompt_section", "Section") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
    }

    // ------------------------------------------------------------------------------------------------- a store's page

    private static UiNavEntry Tab(UiWords t, string tab, string key, string english) =>
        new UiNavEntry { Icon = BizManRules.TabIcon(tab), Tint = UiIconTints.Of(BizManRules.TabIcon(tab)), Label = key != null ? t(key, english) : english };

    private static UiFirmHeadModel Head(UiWords t, string name, string kind, string kindType, string place, bool switches, bool longNames)
    {
        UiIcon icon = BizManRules.KindIcon(kindType, "ba:buildingtype_retail");
        var h = new UiFirmHeadModel
        {
            Name = name, Glyph = icon, KindIcon = icon, Kind = kind, Place = place, Chosen = 0,
            Chips = new[] { t("bizman_security", "Current Security:") + " " + t("bizman_security_level_good", "Good") + " (72%)" },
            ChipTones = new[] { UiRole.Ok },
            Todo = t("reallife_ui_bizman_needs", "{missing} of {count} requirements missing").Replace("{missing}", "1").Replace("{count}", "4"), TodoTone = UiRole.Warn,
            TodoTitle = t("common_requirements", "Requirements"),
            Checks = new[] { "Cash register", "Customer seating (4 required)", "Coffee machine", longNames ? "Storage shelves for the goods delivered every morning" : "Storage shelf" },
            Met = new[] { true, true, true, false },
            Alerts = new[] { "Hire a barista for the morning shift" }, Urgent = new[] { false },
            HasSwitch = true, On = true, SwitchWord = t("common_open", "Open"),
            Actions = new[] { new UiActionModel { Label = t("common_open_in_econoview", "Open in EconoView"), Kind = UiButtonKind.Secondary, Icon = UiIcon.ChartPie, Key = UiAction.Option,
                Command = BizManPages.Action, Arg = BizManPages.DoEconoView } }
        };
        if (switches)
        {
            h.Others = (string[])Businesses.Clone();
        }
        return h;
    }

    private static BizManModel Store(UiWords t, BizManPart part, string state)
    {
        bool longNames = state == "store_long", owned = part == BizManPart.Estate;
        string name = owned ? Businesses[2] : longNames ? "Bloom Café and Bakery of the Upper West Side" : Businesses[0];
        string place = owned ? "301 West 42nd Street" : longNames ? "1250 Avenue of the Americas" : "12 West 44th Street";
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = part };
        m.Head = Head(t, name, t(Kinds[owned ? 2 : 0], KindWords[owned ? 2 : 0]), Kinds[owned ? 2 : 0], place, true, longNames);
        m.Head.Chosen = owned ? 2 : 0;
        m.Head.Picture = owned ? null : "bizman:logo";
        m.Head.ListOpen = state == "store_switch_open";
        m.Head.TodoOpen = state == "store_needs_open";
        var nav = new System.Collections.Generic.List<UiNavEntry>
        {
            Tab(t, BizManRules.Presentation, "reallife_ui_bizman_tab_building", "Building"), Tab(t, BizManRules.Insight, "bizman_menu_insight", "Insight"),
            Tab(t, BizManRules.Pricing, "reallife_ui_bizman_tab_prices", "Prices"), Tab(t, BizManRules.Schedule, "bizman_menu_schedule", "Schedule"),
            Tab(t, BizManRules.Deliveries, "bizman_menu_deliveries", "Deliveries"), Tab(t, BizManRules.Marketing, "bizman_menu_marketing", "Marketing"),
            Tab(t, BizManRules.Interiors, "reallife_interior_tab", "Interior"), Tab(t, BizManRules.Settings, "bizman_menu_settings", "Settings")
        };
        if (owned)
        {
            nav.Add(Tab(t, BizManRules.RealEstate, "reallife_ui_bizman_tab_estate", "Real estate"));   // the game shows it only for a building the player owns
        }
        if (state == "store_foreign")
        {
            nav.Insert(7, new UiNavEntry { Icon = UiIcon.Code, Tint = UiIconTints.Of(UiIcon.Code), Label = FakeOtherMod.TabWords });
        }
        m.Nav = nav.ToArray();
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        switch (part)
        {
            case BizManPart.Insight: m.NavSelected = 1; Insight(t, m); break;
            case BizManPart.Pricing: m.NavSelected = 2; Pricing(t, m); break;
            case BizManPart.Deliveries: m.NavSelected = 4; Deliveries(t, m); break;
            case BizManPart.Marketing: m.NavSelected = 5; Marketing(t, m); break;
            case BizManPart.Settings: m.NavSelected = state == "store_foreign" ? 8 : 7; Settings(t, m, state == "store_settings_unsaved", name); if (state.StartsWith("store_logo")) { LogoEditor(t, m, state == "store_logo_colours" ? 1 : 0); } break;
            case BizManPart.Estate: m.NavSelected = 8; Estate(t, m); break;
            case BizManPart.Interior: m.NavSelected = 6; Interior(t, m); break;
            case BizManPart.Classic:
                bool foreign = state == "store_foreign";
                string page = foreign ? FakeOtherMod.TabWords : t("bizman_menu_schedule", "Schedule");
                m.NavSelected = foreign ? 7 : 3;
                m.ClassicIcon = foreign ? UiIcon.Code : UiIcon.Calendar;
                m.ClassicTitle = foreign ? t("reallife_ui_bizman_foreign_title", "Another mod adds this page").Replace("{page}", page)
                    : t("reallife_ui_bizman_classic_title", "{page} opens in the game's own view").Replace("{page}", page);
                m.ClassicBody = foreign ? t("reallife_ui_bizman_foreign_body", "Open it to use it as that mod made it. BizMan stays in the game's own view until you close the app.")
                    : t("reallife_ui_bizman_classic_body", "Open it to work there with the game's own controls. BizMan stays in the game's own view until you close the app.");
                m.Actions = new[] { new UiActionModel { Label = t("reallife_ui_bizman_classic_open", "Open {page}").Replace("{page}", page),
                    Kind = UiButtonKind.Primary, Icon = UiIcon.Next, Key = UiAction.Alt, Command = BizManPages.Classic, Arg = BizManPages.DoClassic } };
                break;
            default:
                m.NavSelected = 0;
                BuildingOf(t, m, true, "Retail", "Midtown", "180 m²", "8", "40", "$1,240,000");
                break;
        }
        return m;
    }

    // A place's building: its photo, its own facts, and for a business the player rents the offer for the building.
    private static void BuildingOf(UiWords t, BizManModel m, bool rented, string kind, string district, string size, string traffic, string capacity, string value,
        string picture = "bizman:building")
    {
        m.Picture = picture;
        m.PictureAspect = 1.6f;
        m.FactsTitle = t("reallife_ui_bizman_the_building", "The building");
        m.Facts = new[]
        {
            new BizManFact { Label = t("common_building_type", "Building type"), Value = kind },
            new BizManFact { Label = t("common_neighborhood", "Neighborhood"), Value = district },
            new BizManFact { Label = t("bizman_building_total_size", "Total size"), Value = size },
            new BizManFact { Label = t("bizman_traffic_index", "Traffic Index"), Value = traffic },
            new BizManFact { Label = t("bizman_customers_capacity", "Customer capacity"), Value = capacity },
            new BizManFact { Label = t("bizman_market_value", "Market value"), Value = value },
        };
        if (rented)
        {
            m.Amount = new BizManAmount
            {
                Title = t("bizman_buy_building", "Buy building"), Line = "Owned by Marsh Holdings. " + t("bizman_not_for_sale", "This building is not for sale"),
                Figure = "1,250,000", Field = BizManPages.FieldBuy, CanLess = true, CanMore = true,
                Send = new UiActionModel { Label = t("bizman_send_purchase_offer", "Send building purchase offer"), Kind = UiButtonKind.Primary, Icon = UiIcon.Send, Key = UiAction.Alt,
                    Command = BizManPages.Action, Arg = BizManPages.DoBuy }
            };
            m.Facts[5].Value = null;   // the offer's line gives the valuation, as the game's own presentation does
        }
    }

    private static void Insight(UiWords t, BizManModel m)
    {
        m.PartTitle = t("bizman_insight_customers_over_time", "Customers over time");
        m.Periods = new[] { t("bizman_insight_customers_chart_yesterday", "Yesterday"), t("bizman_insight_customers_chart_last_week", "7 Days") };
        m.Period = 1;
        m.Figures = new[] { new UiFigureModel { Label = t("reallife_ui_bizman_customers_total", "Customers, {period}").Replace("{period}", m.Periods[1]), Value = "1,872" } };
        m.Chart = new UiChartModel
        {
            Kind = UiChartKind.Line, Points = Days(t, 405, 7), Focus = -1,
            Series = new[] { new UiSeries { Label = t("bizman_insight_customers", "Customers"), Values = new[] { 212f, 248f, 236f, 281f, 302f, 274f, 319f } } },
            Format = v => ((int)v).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture)
        };
        m.GradesTitle = t("bizman_insight_promotion", "Promotion");
        m.Grades = new[]
        {
            new BizManGrade { Label = t("reallife_ui_bizman_overall", "Overall"), Value = 63 }, new BizManGrade { Label = t("bizman_traffic_index", "Traffic Index"), Value = 8 },
            new BizManGrade { Label = t("bizman_insight_marketing", "Marketing"), Value = 46 },   // the traffic plus the marketing at Midtown's strength, as the game sums them
        };
        m.SecondTitle = t("bizman_insight_customer_satisfaction", "Customer Satisfaction");
        m.SecondGrades = new[]
        {
            new BizManGrade { Label = t("reallife_ui_bizman_overall", "Overall"), Value = 82 }, new BizManGrade { Label = t("bizman_insight_customer_service", "Customer Service"), Value = 76 },
            new BizManGrade { Label = t("bizman_insight_pricing", "Pricing"), Value = 88 }, new BizManGrade { Label = t("bizman_insight_interior", "Interior"), Value = 61 },
            new BizManGrade { Label = t("bizman_insight_cleanliness", "Cleanliness"), Value = 93 },
        };
        m.CapacityTitle = t("bizman_customers_capacity", "Customer capacity");
        m.CapacityLine = t("reallife_ui_bizman_capacity_line", "Up to {now} customers an hour of the building's {limit}").Replace("{now}", "32").Replace("{limit}", "40");
        m.Capacity = new[]
        {
            new BizManCapacity { Name = "Cash register (2)", Figure = "32 / 40", Share = 80, Tone = UiRole.Warn },
            new BizManCapacity { Name = "Coffee machine (1)", Figure = "36 / 40", Share = 90, Tone = UiRole.Ok },
            new BizManCapacity { Name = "Seating (12)", Figure = "40 / 40", Share = 100, Tone = UiRole.Ok },
            new BizManCapacity { Name = "Bakery display (1)", Figure = "38 / 40", Share = 95, Tone = UiRole.Ok },
        };
    }

    private static void Pricing(UiWords t, BizManModel m)
    {
        m.MoreTitle = t("reallife_ui_bizman_this_week", "This week");
        m.More = new[]
        {
            new BizManFact { Label = t("reallife_ui_bizman_sold_week", "Items sold, 7 days"), Value = "2,140 (+6%)", Tone = UiRole.OkText },
            new BizManFact { Label = t("reallife_ui_bizman_takings_week", "Takings, 7 days"), Value = "$6,861 (-3%)", Tone = UiRole.DangerText },
        };
        string[] goods = { "Coffee", "Croissant", "Cupcake", "Bottle of water", "Muffin", "Sandwich", "Paper bag" };
        int[] sold = { 980, 412, 252, 226, 158, 112, 0 };
        int[] stock = { 420, 86, 40, 120, 18, 0, 900 };
        string[] market = { "$3.20", "$2.90", "$3.40", "$1.40", "$2.60", "$5.80", "-" };
        string[] price = { "$3.40", "$2.90", "$3.80", "$1.50", "$2.60", "$5.60", "0" };
        UiRole[] marks = { UiRole.Warn, UiRole.None, UiRole.Warn, UiRole.Warn, UiRole.None, UiRole.None, UiRole.None };
        m.Goods = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("common_product", "Product"), Kind = UiCellKind.Text, Sortable = true },
                new UiDataColumn { Title = t("bizman_sold_last_7_days", "Sold last 7 days"), Kind = UiCellKind.Figure, Width = 128f, Sortable = true },
                new UiDataColumn { Title = t("bizman_stock_count", "Stock count"), Kind = UiCellKind.Figure, Width = 112f, Sortable = true },
                new UiDataColumn { Title = t("bizman_market_price", "Lowest market price"), Kind = UiCellKind.Money, Width = 152f, Sortable = true },
                new UiDataColumn { Title = t("bizman_retail_price", "Your price"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth + UiDataTable.MarkRoom, Sortable = true },
            },
            SortColumn = 1, SortDown = true, RowCount = goods.Length, EmptyTitle = t("reallife_ui_bizman_goods_none", "Nothing for sale yet"),
            EmptyBody = t("reallife_ui_bizman_goods_none_body", "Place shelves or machines that sell goods, then set their prices here."), EmptyIcon = UiIcon.Tag
        };
        m.Goods.Rows = new UiDataRow[goods.Length];
        for (int i = 0; i < goods.Length; i++)
        {
            m.Goods.Rows[i] = new UiDataRow
            {
                Cells = new[] { goods[i], sold[i].ToString("#,0"), stock[i].ToString("#,0"), market[i], price[i] },
                Values = new[] { 0, -1, -1, 0, goods[i] == "Paper bag" ? 0 : UiDataTable.CanLess | UiDataTable.CanMore },   // a bag's price is fixed at nothing
                Tones = new[] { UiRole.None, UiRole.None, stock[i] == 0 ? UiRole.DangerText : UiRole.None, UiRole.None, marks[i] }
            };
        }
        m.SharesTitle = t("reallife_ui_bizman_best_sellers", "Best sellers");
        m.Shares = new[] { 46f, 19f, 12f, 11f };
        m.ShareRest = 12f;   // muffins and sandwiches, past the game's four best sellers
        m.ShareLabels = new[] { "Coffee", "Croissant", "Cupcake", "Bottle of water" };
        m.ShareValues = new[] { "46%", "19%", "12%", "11%" };
    }

    private static void Deliveries(UiWords t, BizManModel m)
    {
        m.ItemsTitle = t("bizman_delivery_contracts", "Delivery Contracts");
        m.GoodsTitle = t("common_products", "Products");
        m.Items = new[]
        {
            new BizManItem { Glyph = UiIcon.Package, Name = "NY Distro", Line = "Delivery Fee: $120.00", Chosen = true },
            new BizManItem { Glyph = UiIcon.Package, Name = "Total Produce Trading", Line = "Delivery Fee: $90.00", WarnWords = t("reallife_ui_bizman_nothing_ordered", "Nothing ordered") },
        };
        m.PartTitle = "Contract with NY Distro";
        m.Facts = new[]
        {
            new BizManFact { Label = t("bizman_purchasingagents_next_delivery", "Next delivery"), Value = "Day 413 at 08:00" },
            new BizManFact { Label = t("common_total", "Total"), Value = "$976.00" },
        };
        m.Lead = "Changes are possible until day 413 at 00:00 (10 hours left)";
        m.HasRepeat = true;
        m.Repeat = true;
        m.RepeatWords = t("repeating_order", "Repeating order");
        m.Actions = new[]
        {
            new UiActionModel { Label = t("bizman_delivery_contract_cancel_contract", "Cancel contract"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoEndContract },
            new UiActionModel { Label = t("reallife_ui_bizman_pause", "Pause deliveries"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Pause, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoCancelContract },
            new UiActionModel { Label = t("bizman_deliveries_urgent_order", "Urgent order"), Kind = UiButtonKind.Primary, Icon = UiIcon.Lightning, Key = UiAction.Alt,
                Command = BizManPages.Action, Arg = BizManPages.DoUrgent },
        };
        string[] goods = { "Coffee beans", "Croissant", "Cupcake", "Bottle of water", "Paper bag" };
        int[] stock = { 42, 86, 40, 120, 900 };
        string[] each = { "$1.10", "$0.80", "$1.20", "$0.30", "$0.02" };
        string[] amount = { "400", "240", "120", "200", "1,000" };
        m.Goods = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("common_product", "Product"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("bizman_deliveries_stock", "Stock"), Kind = UiCellKind.Figure, Width = 96f },
                new UiDataColumn { Title = t("common_price", "Price"), Kind = UiCellKind.Money, Width = 104f },
                new UiDataColumn { Title = t("common_amount", "Amount"), Kind = UiCellKind.Stepper, Width = 176f },
            },
            RowCount = goods.Length, EmptyTitle = t("reallife_ui_bizman_goods_none", "Nothing for sale yet"), EmptyIcon = UiIcon.Package
        };
        m.Goods.Rows = new UiDataRow[goods.Length];
        for (int i = 0; i < goods.Length; i++)
        {
            m.Goods.Rows[i] = new UiDataRow { Cells = new[] { goods[i], stock[i].ToString(), each[i], amount[i] }, Values = new[] { 0, -1, 0, 0 } };   // a running contract holds its amounts
        }
        m.ItemsEmpty = t("reallife_ui_bizman_contracts_none", "No delivery contracts");
        m.ItemsEmptyBody = t("reallife_ui_bizman_contracts_none_body", "Visit a wholesale store and agree a contract with it. Its deliveries show here.");
    }

    private static void Marketing(UiWords t, BizManModel m)
    {
        m.Grades = new[] { new BizManGrade { Label = t("bizman_marketing_efficiency", "Marketing Efficiency"), Value = 46 } };
        m.Facts = new[] { new BizManFact { Label = t("bizman_marketing_total_daily_expenses", "Total daily expenses"), Value = "$340.00" } };
        m.ItemsTitle = t("reallife_ui_bizman_campaigns", "Campaigns");
        m.Items = new[]
        {
            new BizManItem { Name = "Blue Chip Media", Value = "$220.00 / day", Link = t("bizman_marketing_cancel_campaign", "Cancel campaign") },
            new BizManItem { Name = "Five Boroughs Advertising", Value = "$120.00 / day", Link = t("bizman_marketing_cancel_campaign", "Cancel campaign") },
        };
        m.Toggles = new[]
        {
            new BizManToggle { Words = "Billboards (reach 40%, $120 a day)", On = true, Group = 0 },
            new BizManToggle { Words = "Radio (reach 25%, $100 a day)", On = true, Group = 0 },
            new BizManToggle { Words = "Internet ads (reach 30%, $80 a day)", On = false, Group = 0 },
            new BizManToggle { Words = "Flyers (reach 15%, $120 a day)", On = true, Group = 1 },
        };
        m.OthersTitle = t("reallife_ui_bizman_agencies", "Agencies");
        m.Others = new[]
        {
            new BizManItem { Glyph = UiIcon.Megaphone, Name = "Blue Chip Media", Line = "Marketing Agency", Link = t("phone_call_button", "Call") },
            new BizManItem { Glyph = UiIcon.Megaphone, Name = "Five Boroughs Advertising", Line = "Marketing Agency", Link = t("phone_call_button", "Call") },
            new BizManItem { Glyph = UiIcon.Megaphone, Name = "Lighthouse Promotions", Line = "Marketing Agency", Link = t("phone_call_button", "Call") },
        };
        m.ItemsEmpty = t("reallife_ui_bizman_campaigns_none", "No campaigns running");
        m.ItemsEmptyBody = t("bizman_marketing_no_campaigns", "You don't have any active marketing campaigns. Visit a marketing agency to set one up.");
        m.SecondEmpty = t("reallife_ui_bizman_agencies_none", "Call a marketing agency once you have its number. Visit one to meet it.");
    }

    private static void Settings(UiWords t, BizManModel m, bool unsaved, string name)
    {
        m.PartTitle = t("bizman_settings_manage_business_label", "Manage Business");
        m.NameLabel = t("common_business_name", "Business name");
        m.NameText = unsaved ? "Bloom Café Express" : name;
        m.KindLabel = t("common_business_type", "Business type");
        m.Kinds = new[] { t("ba:businesstype_coffeeshop", "Coffee Shop"), t("ba:businesstype_fastfoodrestaurant", "Fast Food Restaurant"), t("ba:businesstype_giftshop", "Gift Shop") };
        m.KindChosen = 0;
        m.Uniforms = t("bizman_set_up_uniforms", "Set up uniforms");
        m.LogoTitle = t("bizman_settings_sign_appearance_info", "Sign appearance");
        m.LogoLabel = t("reallife_ui_bizman_logo", "Logo");
        m.Picture = m.Head.Picture;
        m.FontLabel = t("common_font", "Font");
        m.Fonts = new[] { "Overpass", "Lobster", "Bebas" };
        m.FontChosen = 1;
        m.Actions = new[]
        {
            new UiActionModel { Label = t("reallife_ui_bizman_randomise", "Randomise"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Spark, Key = UiAction.Count, Command = BizManPages.Action, Arg = BizManPages.DoRandom },
            new UiActionModel { Label = t("common_save_changes", "Save changes"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = BizManPages.Action,
                Arg = BizManPages.DoSave, Disabled = !unsaved },
            new UiActionModel { Label = t("bizman_settings_save_logo", "Save logo changes"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoSaveLogo, Disabled = true },
            new UiActionModel { Label = t("reallife_ui_bizman_logo_more", "Shape and colours"), Kind = UiButtonKind.Quiet, Key = UiAction.Count, Command = BizManPages.Action,
                Arg = BizManPages.DoLogoEdit },
            new UiActionModel { Label = t("bizman_terminate_contract_button", "Terminate contract"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoTerminate },
            new UiActionModel { Label = t("bizman_shutdown_business", "Shut down business"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoShutdown },
        };
        m.Lead = unsaved ? t("reallife_ui_bizman_unsaved", "Changes not saved. The game asks before you leave this page.") : null;
    }

    private static void Estate(UiWords t, BizManModel m)
    {
        m.MoreTitle = t("bizman_real_estate_rent_management", "Rent management");
        m.More = new[]
        {
            Split(t("bizman_real_estate_current_rent", "Current rent: <b>{rent}/{unit}</b>").Replace("{rent}", "$9.40").Replace("{unit}", "m\u00b2")),
            Split(t("bizman_real_estate_average_market_price", "Avg market price: <b>{price}/{unit}</b>").Replace("{price}", "$9.10").Replace("{unit}", "m\u00b2")),
            new BizManFact { Label = t("bizman_real_estate_occupancy", "Occupancy"), Value = "68%", Tone = UiRole.WarnText },
            new BizManFact { Label = t("bizman_real_estate_total_daily_rent", "Total daily rent ({occupancy}% of {totalSqm})").Replace("{occupancy}", "68").Replace("{totalSqm}", "180 m\u00b2"),
                Value = "$1,151" },
        };
        m.SliderLabel = t("reallife_ui_bizman_rent_new", "New rent");
        m.SliderFigure = "$9.80";
        m.SliderShare = 0.62f;
        m.Lead = t("bizman_real_estate_rent_not_applied", "New rent price not applied");
        m.SecondTitle = t("bizman_real_estate_self_occupied_unit", "Self-occupied unit");
        m.SecondEmpty = t("bizman_real_estate_unit_owned_by_player", "Unit is currently owned by you and won't generate any rent.");
        m.PartTitle = t("bizman_real_estate_set_for_sale", "Set for sale");
        m.Facts = new[]
        {
            new BizManFact { Label = t("bizman_real_estate_price_at_purchase", "Your price at purchase (Day {day})").Replace("{day}", "210"), Value = "$980,000" },
            new BizManFact { Label = t("bizman_real_estate_estimated_market_price", "Estimated market price"), Value = "$1,240,000" },
            new BizManFact { Label = t("bizman_real_estate_your_potential_gain", "Your potential gain"), Value = "$260,000 (27%)", Tone = UiRole.OkText },
        };
        m.Amount = new BizManAmount
        {
            Title = t("common_sales_price", "Sales Price"), Placeholder = t("bizman_real_estate_enter_sales_price", "Enter sales price"), Field = BizManPages.FieldSale,
            CanLess = false, CanMore = true,
            Send = new UiActionModel { Label = t("bizman_real_estate_mark_for_sale", "Mark for sale"), Kind = UiButtonKind.Primary, Icon = UiIcon.Tag, Key = UiAction.Alt,
                Command = BizManPages.Action, Arg = BizManPages.DoMarkSale }
        };
        m.Actions = new[]
        {
            new UiActionModel { Label = t("bizman_real_estate_apply_new_rent", "Apply new rent"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoApplyRent },
            new UiActionModel { Label = t("bizman_real_estate_takeover_unit", "Takeover unit"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Key, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoTakeoverUnit, Disabled = true },
        };
        m.FactsTitle = t("reallife_ui_bizman_the_building", "The building");
        m.BuildingFacts = new[]
        {
            new BizManFact { Label = t("common_neighborhood", "Neighborhood"), Value = "Hell's Kitchen" },
            new BizManFact { Label = t("common_building_type", "Building type"), Value = "Retail" },
            new BizManFact { Label = t("bizman_real_estate_taxes_info", "{daysInterval}-day Estate Tax:").Replace("{daysInterval}", "60").TrimEnd(':', ' '), Value = "$14,880" },
        };
    }

    // The Interior tab as its pages write it for a café whose finishes are worn: the scores at the left, what the interior
    // does at the right, with the refresh its page offers.
    private static void Interior(UiWords t, BizManModel m)
    {
        BizManLine L(BizManLineKind kind, string words, UiRole tone = UiRole.None) => new BizManLine { Kind = kind, Words = words, Tone = tone };
        m.Lines = new[]
        {
            L(BizManLineKind.Heading, t("reallife_interior_head_place", "The place")),
            L(BizManLineKind.Line, "Overall: 58 of 100"),
            L(BizManLineKind.Line, "Standard expected in Midtown: 64"),
            L(BizManLineKind.Line, "Interior design: 61 of 100"),
            L(BizManLineKind.Line, "Decor: 52 of 100"),
            L(BizManLineKind.Heading, t("reallife_interior_head_decor", "Decor by kind")),
            L(BizManLineKind.Line, "Plants: 80% of its share, 6 placed"),
            L(BizManLineKind.Line, "Art: 40% of its share, 2 placed"),
            L(BizManLineKind.Line, "Lighting: 65% of its share, 5 placed"),
            L(BizManLineKind.Line, "Seating: 100% of its share, 12 placed"),
            L(BizManLineKind.Line, "Floor area: 180 square metres"),
            L(BizManLineKind.Heading, t("reallife_interior_head_condition", "Condition")),
            L(BizManLineKind.Line, "Wear: 46%", UiRole.WarnText),
            L(BizManLineKind.Line, "Cleanliness: 93 of 100"),
        };
        m.Pages = new[]
        {
            t("reallife_interior_page_effects", "What it does"), t("reallife_interior_page_items", "Items that count"),
            t("reallife_interior_page_customers", "Today's customers"), t("reallife_interior_page_rules", "The game's own rules")
        };
        m.PageShown = 0;
        m.SecondLines = new[]
        {
            L(BizManLineKind.Line, "Midtown expects 64, this shop is at 58: 9% fewer customers", UiRole.None),
            L(BizManLineKind.Line, "Decor at 52: 4% more customers"),
            L(BizManLineKind.Line, "Prices: customers here accept up to 3% more before they find them dear"),
            L(BizManLineKind.Line, "In all today: -14 customers and about -$62 takings from its interior"),
            L(BizManLineKind.Line, "Basket: about 2% more products on each order, 6% at most here (at an overall figure of 80)"),
            L(BizManLineKind.Line, "Complaints: 3% chance a day of one about the place"),
            L(BizManLineKind.Heading, t("reallife_interior_head_next", "What to add next")),
            L(BizManLineKind.Line, "Art on the walls: two more pieces bring decor to its share."),
            L(BizManLineKind.Line, "The finishes are worn: refreshing them brings the scores back."),
            L(BizManLineKind.Line, "Refreshing costs $5,400 and takes the wear back to nothing."),
            L(BizManLineKind.Note, "Guide: Interiors and decor"),
        };
        m.Actions = new[]
        {
            new UiActionModel { Label = t("reallife_set_link", "Settings"), Kind = UiButtonKind.Quiet, Icon = UiIcon.Gear, Key = UiAction.Count, Command = BizManPages.Action,
                Arg = BizManPages.DoLine + 1 },
            new UiActionModel { Label = t("reallife_interior_refresh_button", "Refresh the finishes"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Wrench, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoLine },
        };
    }

    // ------------------------------------------------------------------------------------------------- other places

    private static BizManModel Building(UiWords t, string kind)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = BizManPart.Building };
        string place = kind == "warehouse" ? "112 West Street" : kind == "factory" ? "220 36th Street" : "350 Fifth Avenue";
        string name = kind == "warehouse" ? "Pier 40 Storage" : kind == "factory" ? "Industry City Roasters" : "Bloom Group";
        string kindWords = kind == "warehouse" ? "Warehouse" : kind == "factory" ? "Factory" : "Headquarters";
        m.Head = Head(t, name, kindWords, "ba:businesstype_" + kind, place, false, false);
        m.Head.Glyph = m.Head.KindIcon = BizManRules.KindIcon("ba:businesstype_" + kind, null);
        m.Head.Chips = new string[0];
        m.Head.ChipTones = new UiRole[0];
        m.Head.Todo = null;
        m.Head.HasSwitch = kind == "factory";
        var nav = new System.Collections.Generic.List<UiNavEntry> { Tab(t, BizManRules.Presentation, "reallife_ui_bizman_tab_building", "Building") };
        if (kind == "warehouse")
        {
            nav.Add(Tab(t, BizManRules.Drivers, "bizman_menu_drivers", "Drivers"));
            nav.Add(Tab(t, BizManRules.Inventory, "bizman_menu_inventory", "Inventory"));
        }
        else if (kind == "factory")
        {
            nav.Add(Tab(t, BizManRules.Factory, "bizman_menu_factory", "Factory"));
            nav.Add(Tab(t, BizManRules.Schedule, "bizman_menu_schedule", "Schedule"));
            nav.Add(Tab(t, BizManRules.Drivers, "bizman_menu_drivers", "Drivers"));
            nav.Add(Tab(t, BizManRules.Inventory, "bizman_menu_inventory", "Inventory"));
        }
        else
        {
            nav.Add(Tab(t, BizManRules.Schedule, "bizman_menu_schedule", "Schedule"));
            nav.Add(Tab(t, BizManRules.PricingManagers, "reallife_ui_bizman_tab_pricing", "Pricing"));
            nav.Add(Tab(t, BizManRules.LogisticsManagers, "reallife_ui_bizman_tab_logistics", "Logistics"));
            nav.Add(Tab(t, BizManRules.PurchasingAgents, "reallife_ui_bizman_tab_purchasing", "Purchasing"));
            nav.Add(Tab(t, BizManRules.HrManagers, "reallife_ui_bizman_tab_hr", "Staffing"));
            nav.Add(Tab(t, BizManRules.Headhunters, "bizman_menu_headhunters", "Headhunters"));
        }
        nav.Add(Tab(t, BizManRules.Settings, "bizman_menu_settings", "Settings"));
        m.Nav = nav.ToArray();
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        if (kind == "warehouse")
        {
            BuildingOf(t, m, true, null, "Lower Manhattan", "1,400 m²", "3", null, "$2,850,000", "bizman:warehouse");
        }
        else if (kind == "factory")
        {
            BuildingOf(t, m, true, "Industrial", "Industry City", "2,600 m²", "2", null, "$4,100,000", "bizman:factory");
        }
        else
        {
            BuildingOf(t, m, true, "Office", "Midtown", "640 m²", "9", null, "$6,300,000");
        }
        m.Lead = t("reallife_ui_bizman_classic_hint", "Its other pages open in the game's own view from the sections at the left.");
        m.PartTitle = null;
        m.Amount = null;
        return m;
    }

    private static BizManModel Start(UiWords t)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = BizManPart.Start };
        m.Head = new UiFirmHeadModel { Name = "88 Mott Street", Glyph = UiIcon.Building, KindIcon = UiIcon.Building, Kind = "Retail", Place = "Lower Manhattan",
            Chips = new[] { t("reallife_ui_bizman_rented", "Rented, empty") }, ChipTones = new[] { UiRole.Accent } };
        m.Nav = new[] { Tab(t, BizManRules.Presentation, "reallife_ui_bizman_tab_building", "Building") };
        m.NameLabel = t("common_business_name", "Business name");
        m.NamePlaceholder = t("reallife_ui_bizman_name_hint", "Name your business");
        m.NameText = "Mott Street Bakery";
        string[] kinds = { "Florist", "Fruit And Vegetable Store", "Coffee Shop", "Supermarket", "Fast Food Restaurant", "Hairdresser", "Gym", "Gift Shop", "Bookstore", "Liquor Store",
            "Nightclub", "Jewelry Store", "Clothing Store", "Electronics Store", "Car Showroom", "Bank", "Headquarters", "Warehouse" };
        string[] types = { "florist", "fruitandvegetablestore", "coffeeshop", "supermarket", "fastfoodrestaurant", "hairdresser", "gym", "giftshop", "bookstore", "liquorstore",
            "nightclub", "jewelrystore", "clothingstore", "electronicsstore", "cardealership", "bank", "headquarters", "warehouse" };
        string[] rivals = { "6", "6", "7", "3", "5", "4", "3", "6", "2", "1", "2", "1", "1", "1", "0", "0", "-", "-" };
        var rows = new UiDataRow[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            rows[i] = new UiDataRow { Cells = new[] { kinds[i], rivals[i] }, Values = new[] { (int)BizManRules.KindIcon("ba:businesstype_" + types[i], null), 0 } };
        }
        m.StartKinds = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("common_business_type", "Business type"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_ui_bizman_competitors", "Competitors"), Kind = UiCellKind.Figure, Width = 128f }
            },
            Rows = rows, RowCount = rows.Length, Selected = 2, SearchPlaceholder = t("reallife_ui_bizman_find_kind", "Find a type of business"),
            EmptyIcon = UiIcon.Store, EmptyTitle = t("reallife_ui_bizman_kinds_none", "No kind of business fits this building")
        };
        var detail = new BizManStart { Kicker = t("common_business_type", "Business type"), Name = kinds[2], Icon = UiIcon.Coffee };
        var list = new System.Collections.Generic.List<BizManStartRow>();
        list.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = t("reallife_ui_bizman_competitors_here", "Competitors in the neighbourhood"), Value = "7" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = t("bizman_primary_products", "Primary products") });
        string[] goods = { "Cappuccino", "Croissant", "Espresso", "Latte", "Muffin", "Tea", "Iced Coffee", "Sandwich", "Donut", "Cookie", "Brownie", "Hot Chocolate" };
        for (int i = 0; i < goods.Length; i++)
        {
            bool can = i != 7 && i != 10;
            list.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = goods[i], Mark = can ? UiIcon.None : UiIcon.Warning, Tone = can ? UiRole.None : UiRole.Warn });
        }
        list.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = t("reallife_ui_bizman_start_sources", "Where the goods come from") });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "Wholesaler", Mark = UiIcon.Check, Tone = UiRole.Ok });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "Importer", Mark = UiIcon.Warning, Tone = UiRole.Warn });
        list.Add(new BizManStartRow
        {
            Kind = BizManStartKind.Note,
            Label = t("reallife_ui_bizman_start_source_missing", "A source marked with a warning is one you cannot use yet. The game asks you to confirm when you choose the type.")
        });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = t("reallife_ui_bizman_start_needs", "What it needs to open") });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "2 x Coffee Machine" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "4 x Table" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "Cash Register" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = t("reallife_ui_bizman_start_diploma", "Diploma") });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Item, Label = "Hospitality", Mark = UiIcon.Warning, Tone = UiRole.Warn });
        list.Add(new BizManStartRow
        {
            Kind = BizManStartKind.Note, Label = t("reallife_ui_bizman_start_diploma_missing", "You have not completed this diploma yet, so the game will not let you start this type.")
        });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Heading, Label = t("reallife_ui_bizman_the_building", "The building") });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = t("common_building_type", "Building type"), Value = "Retail" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = t("bizman_building_total_size", "Total size"), Value = "220 m2" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = t("bizman_traffic_index", "Traffic index"), Value = "7" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = t("bizman_customers_capacity", "Customer capacity"), Value = "48" });
        list.Add(new BizManStartRow { Kind = BizManStartKind.Fact, Label = t("bizman_market_value", "Market value"), Value = "$1,480,000" });
        detail.Rows = list.ToArray();
        m.StartDetail = detail;
        m.StartEmptyTitle = t("reallife_ui_bizman_start_pick", "Choose a type of business");
        m.StartEmptyBody = t("reallife_ui_bizman_start_pick_body", "Pick one from the list to see what it sells, what it needs to open and who already trades near you.");
        m.Actions = new[]
        {
            new UiActionModel { Label = t("reallife_ui_bizman_randomise", "Randomise"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Spark, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoRandomName },
            new UiActionModel { Label = t("bizman_preview_button", "Preview"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Eye, Key = UiAction.Option,
                Command = BizManPages.Action, Arg = BizManPages.DoPreview },
            new UiActionModel { Label = t("bizman_start_business_button", "Start business"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                Command = BizManPages.Action, Arg = BizManPages.DoStartHere },
        };
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static BizManModel Rent(UiWords t)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = BizManPart.Rent };
        m.Head = new UiFirmHeadModel { Name = "240 Lafayette Street", Glyph = UiIcon.Building, KindIcon = UiIcon.Building, Kind = "Retail",
            Place = "Lower Manhattan", Chips = new[] { t("reallife_ui_bizman_for_rent", "For rent") }, ChipTones = new[] { UiRole.Accent } };
        m.Nav = new[] { Tab(t, BizManRules.Presentation, "reallife_ui_bizman_tab_building", "Building") };
        BuildingOf(t, m, false, "Retail", "Lower Manhattan", "220 m²", "7", "48", "$1,480,000");
        m.MoreTitle = t("reallife_ui_bizman_lease", "The lease");
        m.More = new[]
        {
            new BizManFact { Label = "Daily rent", Value = "$412" },
            new BizManFact { Label = t("ba:transaction_deposit_label", "Deposit"), Value = "$12,360" },
            new BizManFact { Label = t("reallife_ui_bizman_appliances", "Electrical appliances"), Value = "$2,400" },
            new BizManFact { Label = t("reallife_ui_bizman_first_day", "First day's rent"), Value = "$412" },
            new BizManFact { Label = "Total initial payment", Value = "$15,172", Tone = UiRole.Text },
        };
        m.Actions = new[]
        {
            new UiActionModel { Label = t("bizman_preview_button", "Preview"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Eye, Key = UiAction.Option, Command = BizManPages.Action, Arg = BizManPages.DoPreview },
            new UiActionModel { Label = t("bizman_rent_building_button", "Rent building"), Kind = UiButtonKind.Primary, Icon = UiIcon.Key, Key = UiAction.Alt, Command = BizManPages.Action, Arg = BizManPages.DoRent },
        };
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static BizManModel Rival(UiWords t)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = BizManPart.Rival };
        m.Head = new UiFirmHeadModel { Name = "Sunrise Donuts", Glyph = UiIcon.ForkKnife, KindIcon = UiIcon.ForkKnife, Kind = "Fast Food Restaurant", Place = "Lower Manhattan",
            Chips = new[] { t("reallife_ui_bizman_rival", "Owned by a rival") }, ChipTones = new[] { UiRole.Muted } };
        m.Nav = new[] { Tab(t, BizManRules.Presentation, "reallife_ui_bizman_tab_building", "Building") };
        BuildingOf(t, m, false, "Retail", "Lower Manhattan", "140 m²", "9", "36", null);
        m.PartTitle = t("reallife_ui_bizman_the_business", "The business");
        m.Lead = "Owned by Hartley Foods, a chain of 6 businesses in the city.";
        m.MoreTitle = t("bizman_schedule_opening_hours", "Opening hours");
        string[] days = { "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday" };
        string[] english = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        m.More = new BizManFact[7];
        for (int i = 0; i < 7; i++)
        {
            m.More[i] = new BizManFact { Label = t(days[i], english[i]), Value = i == 6 ? t("common_closed", "Closed") : "07:00 - 15:00" };
        }
        m.Amount = new BizManAmount
        {
            Title = t("reallife_ui_bizman_take_over", "Take it over"), Line = "Estimated Valuation: $412,000", Figure = "420,000", Field = BizManPages.FieldOffer, CanLess = true, CanMore = true,
            Send = new UiActionModel { Label = t("bizman_send_offer_button", "Send offer"), Kind = UiButtonKind.Primary, Icon = UiIcon.Send, Key = UiAction.Alt, Command = BizManPages.Action, Arg = BizManPages.DoTakeover }
        };
        m.Second = new BizManAmount
        {
            Title = t("bizman_buy_building", "Buy building"), Line = t("bizman_not_for_sale", "This building is not for sale"), Figure = "1,250,000", Field = BizManPages.FieldBuy,
            CanLess = true, CanMore = true,
            Send = new UiActionModel { Label = t("bizman_send_purchase_offer", "Send building purchase offer"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Send, Key = UiAction.Count,
                Command = BizManPages.Action, Arg = BizManPages.DoBuy }
        };
        m.Actions = new[]
        {
            new UiActionModel { Label = t("bizman_presentation_show_employees", "Show employees"), Kind = UiButtonKind.Secondary, Icon = UiIcon.People, Key = UiAction.Count, Command = BizManPages.Action, Arg = BizManPages.DoEmployees },
            new UiActionModel { Label = t("bizman_browse_store_inventory", "Browse store inventory"), Kind = UiButtonKind.Secondary, Icon = UiIcon.ShoppingBag, Key = UiAction.Option, Command = BizManPages.Action, Arg = BizManPages.DoBrowse },
        };
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static BizManModel Villa(UiWords t)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = BizManPart.Villa };
        m.Head = new UiFirmHeadModel { Name = "3 Ocean Road", Glyph = UiIcon.Home, KindIcon = UiIcon.Home, Kind = "Residential", Place = "The Hamptons",
            Chips = new[] { t("reallife_ui_bizman_for_sale", "For sale") }, ChipTones = new[] { UiRole.Accent } };
        m.Nav = new[] { Tab(t, BizManRules.Presentation, "reallife_ui_bizman_tab_building", "Building") };
        BuildingOf(t, m, false, "Residential", "The Hamptons", "640 m²", null, null, null, "bizman:villa");
        m.PartTitle = t("bizman_buy_building", "Buy building");
        m.Amount = new BizManAmount
        {
            Line = "Building price: $8,400,000", Figure = "8,400,000", Field = BizManPages.FieldBuy, CanLess = true, CanMore = true,
            Send = new UiActionModel { Label = t("bizman_send_purchase_offer", "Send building purchase offer"), Kind = UiButtonKind.Primary, Icon = UiIcon.Send, Key = UiAction.Alt,
                Command = BizManPages.Action, Arg = BizManPages.DoBuy }
        };
        m.Actions = new[] { new UiActionModel { Label = t("bizman_preview_button", "Preview"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Eye, Key = UiAction.Option, Command = BizManPages.Action, Arg = BizManPages.DoPreview } };
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    // A line the game writes as its label and its figure in one, its rich text taken off, as a fact.
    private static BizManFact Split(string words)
    {
        words = System.Text.RegularExpressions.Regex.Replace(words, "<[^>]+>", string.Empty);
        int cut = words.IndexOf(':');
        return cut > 0 ? new BizManFact { Label = words.Substring(0, cut).Trim(), Value = words.Substring(cut + 1).Trim() } : new BizManFact { Value = words };
    }

    private static string[] Days(UiWords t, int first, int count)
    {
        var days = new string[count];
        for (int i = 0; i < count; i++)
        {
            days[i] = t("common_day_number", "Day {number}").Replace("{number}", Whole(first + i));
        }
        return days;
    }

    private static string Whole(int n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

    // Money as the game writes it, a loss with its sign before the dollar.
    private static string Signed(int n) => (n < 0 ? "-" : string.Empty) + Dollars(System.Math.Abs(n));
}

internal static partial class UiSamples
{
    // The logo editor open: 40 of the game's shapes (the third chosen), the three colour rows with the add button, Done and Save logo.
    private static void LogoEditor(UiWords t, BizManModel m, int tab)
    {
        m.LogoEdit = true;
        m.LogoTabs = new[] { t("reallife_ui_bizman_logo_shapes", "Shapes"), t("reallife_ui_bizman_logo_colours", "Colours") };
        m.LogoTab = tab;
        m.PartTitle = t("reallife_ui_bizman_logo_more", "Shape and colours");
        m.ShapesTitle = t("reallife_ui_bizman_logo_shapes", "Shapes");
        m.ShapesNone = t("reallife_ui_bizman_logo_shapes_none", "The shapes are still loading.");
        m.ShapesOwn = t("reallife_ui_bizman_logo_own", "Add your own shapes");
        m.LogoNote = t("reallife_ui_bizman_logo_note", "The shape and background show as they will on your sign. Your name appears in the font and colour you pick once you save the logo.");
        m.ShapeCount = 40;
        m.ShapePics = new string[40];
        for (int i = 0; i < 40; i++)
        {
            m.ShapePics[i] = "bizman:shape:" + i;
        }
        m.ShapeChosen = 2;
        m.PreviewPic = "bizman:logopreview";
        int[] palette = { 0x000000, 0xFFFFFF, 0xE53935, 0xFB8C00, 0xFDD835, 0x43A047, 0x1E88E5, 0x5E35B1 };
        string[] labels = { "reallife_ui_bizman_logo_colour_shape", "reallife_ui_bizman_logo_colour_text", "reallife_ui_bizman_logo_colour_back" };
        string[] english = { "Shape colour", "Name colour", "Background colour" };
        int[] chosen = { 0, 0, 1 };
        m.Colours = new BizManColours[3];
        for (int r = 0; r < 3; r++)
        {
            var pics = new string[palette.Length];
            for (int i = 0; i < pics.Length; i++)
            {
                pics[i] = UiDraw.SwatchKey(palette[i]);
            }
            m.Colours[r] = new BizManColours { Label = t(labels[r], english[r]), Pics = pics, Count = pics.Length, Chosen = chosen[r], CanAdd = true };
        }
        m.Actions = new[]
        {
            new UiActionModel { Label = t("reallife_ui_bizman_logo_done", "Done"), Kind = UiButtonKind.Quiet, Key = UiAction.Count, Command = BizManPages.Action, Arg = BizManPages.DoLogoDone },
            new UiActionModel { Label = t("bizman_settings_save_logo", "Save logo changes"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                Command = BizManPages.Action, Arg = BizManPages.DoSaveLogo },
        };
    }
}
