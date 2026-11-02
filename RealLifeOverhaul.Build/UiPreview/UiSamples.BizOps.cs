using System.Globalization;
using RealLifeOverhaul.Ui;

/// <summary>
/// BizMan's operations on the kit in each of their states: a store's schedule as the board (a day with no
/// shifts yet, a full day whose shifts overlap across the stations, a shift being dragged, the picker open for an hour,
/// the opening hours' sheet, no stations at all, a closed day, long names), a headquarters' managers (a pricing plan over forty
/// products, no plans yet, a plan without a manager, a logistics route of three stops, an import contract, an HR plan
/// with its staff and while assigning, a headhunter recruiting and replacing), a factory's machines with the recipe of
/// the one chosen (and one missing a machine, and none placed yet), a warehouse's drivers and its stock (and empty),
/// and the uniform presets' editor beside the game's preview (a preset dressed, none yet, the longest names). The words
/// are the locale's in the language given (the game's own keys where the game has the words), the figures the samples'.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] BizOpsStates =
    {
        "schedule_empty", "schedule_full", "schedule_drag", "schedule_picker", "schedule_hours", "schedule_none", "schedule_closed", "schedule_long", "pricing", "pricing_none",
        "pricing_unassigned", "logistics", "purchasing", "hr", "hr_assign", "headhunters", "headhunters_replace", "factory", "factory_missing", "factory_none",
        "warehouse_drivers", "warehouse_stock", "warehouse_empty"
    };

    public static readonly string[] UniformStates = { "uniforms", "uniforms_colours", "uniforms_new", "uniforms_long" };

    private static readonly string[] OpsStaff =
    {
        "Maya Okafor", "Daniel Reyes", "Priya Natarajan", "Tom Becker", "Aaliyah Brooks", "Lucas Ferreira", "Hannah Kowalski", "Samuel Adeyemi", "Chloe Martin",
        "Ethan Walsh", "Sofia Lindqvist", "Marcus Chen"
    };

    private static readonly string[] OpsLongStaff =
    {
        "Maximiliano Alessandro Bartholomew", "Anastasia Konstantinovna Romanova", "Oluwaseun Adebayo Okonkwo", "Christopher Fitzgerald Montgomery",
        "Guadalupe Esperanza Villalobos", "Bartholomew Wolfgang Richardson", "Siobhan Aoife Fitzpatrick", "Rajendra Venkataraman Iyer", "Evangeline Josephine Whitaker",
        "Leopold Augustin Vandermeer", "Thorvald Sigurdsson Haraldsen", "Annabelle Genevieve Beauchamp"
    };

    private static readonly string[] OpsSkillWords =
    {
        "Customer service", "Customer service", "Customer service", "Customer service", "Barista", "Barista", "Barista", "Customer service", "Customer service",
        "Cleaning", "Customer service", "Cleaning"
    };

    private static readonly int[] OpsSkill = { 88, 74, 92, 61, 79, 55, 83, 67, 71, 49, 90, 64 };
    private static readonly int[] OpsWeek = { 38, 32, 40, 46, 36, 18, 40, 28, 30, 12, 0, 0 };
    private static readonly int[] OpsMood = { 82, 74, 91, 66, 58, 41, 77, 63, 88, 70, 54, 79 };
    private static readonly int[] OpsWage = { 1650, 1500, 1825, 1450, 1700, 1400, 1775, 1550, 1600, 1350, 1900, 1525 };   // cents an hour

    private static readonly string[] OpsLanes = { "Register 1", "Register 2", "Espresso bar", "Pastry counter", "Cleaning" };

    private static readonly string[] OpsLongLanes =
    {
        "Register by the front window", "Register by the pastry cabinet", "Espresso bar and milk station", "Pastry counter and display", "Sandwich and salad counter",
        "Cleaning, front of house and terrace"
    };

    // The full day: each shift's lane, its hours and who works it; Tom works more than the week allows him.
    private static readonly int[,] OpsShifts =
    {
        { 0, 6, 14, 0 }, { 0, 14, 22, 1 }, { 1, 8, 16, 2 }, { 1, 16, 21, 3 }, { 2, 6, 12, 4 }, { 2, 12, 18, 5 }, { 2, 18, 21, 6 }, { 3, 7, 15, 7 },
        { 3, 15, 20, 8 }, { 4, 5, 6, 9 }, { 4, 21, 23, 9 }
    };

    /// <summary>A state of BizMan's operations by its name, as the previews and the checks lay each one out.</summary>
    public static BizManModel BizOps(UiWords t, string state)
    {
        switch (state)
        {
            case "pricing": case "pricing_none": case "pricing_unassigned": return OpsPricing(t, state);
            case "logistics": return OpsLogistics(t);
            case "purchasing": return OpsPurchasing(t);
            case "hr": case "hr_assign": return OpsHr(t, state == "hr_assign");
            case "headhunters": case "headhunters_replace": return OpsHunters(t, state == "headhunters_replace");
            case "factory": case "factory_missing": case "factory_none": return OpsFactory(t, state);
            case "warehouse_drivers": return OpsDrivers(t);
            case "warehouse_stock": case "warehouse_empty": return OpsStock(t, state == "warehouse_empty");
            default: return OpsSchedule(t, state);
        }
    }

    // ------------------------------------------------------------------------------------------------- the frame

    // A store of the player's on its Schedule tab, headed as BizMan heads a running business.
    private static BizManModel OpsStore(UiWords t, bool longNames)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = BizManPart.Schedule };
        string name = longNames ? "Bloom Café and Bakery of the Upper West Side" : Businesses[0];
        m.Head = Head(t, name, t(Kinds[0], KindWords[0]), Kinds[0], longNames ? "1250 Avenue of the Americas" : "12 West 44th Street", true, longNames);
        m.Head.Picture = "bizman:logo";
        m.Nav = new[]
        {
            Tab(t, BizManRules.Presentation, "reallife_ui_bizman_tab_building", "Building"), Tab(t, BizManRules.Insight, "bizman_menu_insight", "Insight"),
            Tab(t, BizManRules.Pricing, "reallife_ui_bizman_tab_prices", "Prices"), Tab(t, BizManRules.Schedule, "bizman_menu_schedule", "Schedule"),
            Tab(t, BizManRules.Deliveries, "bizman_menu_deliveries", "Deliveries"), Tab(t, BizManRules.Marketing, "bizman_menu_marketing", "Marketing"),
            Tab(t, BizManRules.Interiors, "reallife_interior_tab", "Interior"), Tab(t, BizManRules.Settings, "bizman_menu_settings", "Settings")
        };
        m.NavSelected = 3;
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    // A headquarters, a factory or a warehouse of the player's, on the tab given, with the tabs the game shows that kind.
    private static BizManModel OpsPlace(UiWords t, string kind, BizManPart part, int tab)
    {
        var m = new BizManModel { Title = t("smartphone_bizman", "BizMan"), Page = BizManPage.Business, Part = part };
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
        m.NavSelected = tab;
        m.Prompts = BizManPrompts(t, false);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the schedule

    private static BizManModel OpsSchedule(UiWords t, string state)
    {
        bool longNames = state == "schedule_long", none = state == "schedule_none", closed = state == "schedule_closed";
        bool full = state == "schedule_full" || state == "schedule_drag" || state == "schedule_picker" || state == "schedule_hours" || longNames;
        BizManModel m = OpsStore(t, longNames);
        BizOpsModel o = m.Ops;
        var board = new UiBoardModel();
        o.Board = board;
        board.Days = new string[7];
        for (int i = 0; i < 7; i++)
        {
            board.Days[i] = t("reallife_ui_bizops_day_" + (i + 1).ToString(CultureInfo.InvariantCulture), new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }[i]);
        }
        board.Day = closed ? 6 : 2;
        board.FirstHour = 0;
        board.LastHour = BizOpsRules.Hours;
        board.Hours = new string[BizOpsRules.Hours];
        for (int h = 0; h < BizOpsRules.Hours; h++)
        {
            board.Hours[h] = h % 2 == 0 ? h.ToString("00", CultureInfo.InvariantCulture) : string.Empty;
        }
        board.OpenHours = new bool[BizOpsRules.Hours];
        for (int h = 6; h < 22 && !closed; h++)
        {
            board.OpenHours[h] = true;
        }
        board.Faces = true;
        board.Longest = BizOpsRules.Hours * 60;
        board.Now = full && !closed ? 13 * 60 + 40 : -1;
        board.EmptyTitle = t("reallife_ui_bizops_lanes_none", "No workstations here");
        board.EmptyBody = t("reallife_ui_bizops_lanes_none_body", "Place a workstation, a till or a cleaning station, and it gets its own row here.");
        string[] names = longNames ? OpsLongStaff : OpsStaff;
        if (!none)
        {
            string[] lanes = longNames ? OpsLongLanes : OpsLanes;
            board.Lanes = new UiBoardLane[lanes.Length];
            for (int i = 0; i < lanes.Length; i++)
            {
                bool cleaning = i == lanes.Length - 1;
                UiIcon glyph = cleaning ? UiIcon.Spark : UiIcon.Coffee;
                string sub = cleaning ? null : i == 2 ? t("reallife_ui_bizops_and_more", "{first} and {count} more").Replace("{first}", "Coffee machine").Replace("{count}", "2")
                    : t("bizman_schedule_customers_per_hour", "{amount} customers per hour").Replace("{amount}", i < 2 ? "14" : "8");
                board.Lanes[i] = new UiBoardLane { Name = lanes[i], Sub = sub, Icon = glyph, Tint = UiIconTints.Of(glyph) };
            }
            if (full)
            {
                int n = OpsShifts.GetLength(0);
                board.Shifts = new UiBoardShift[n];
                for (int i = 0; i < n; i++)
                {
                    int who = OpsShifts[i, 3];
                    int lane = longNames && OpsShifts[i, 0] == 4 ? lanes.Length - 1 : OpsShifts[i, 0];
                    board.Shifts[i] = new UiBoardShift
                    {
                        Lane = lane, Start = OpsShifts[i, 1] * 60, End = OpsShifts[i, 2] * 60, Name = names[who], Warn = who == 3, Hue = UiColour.HueOf(names[who]),
                        Ring = StaffViews.Mood(OpsMood[who])
                    };
                }
            }
        }
        if (state == "schedule_drag")
        {
            // Priya's shift on Register 2 being stretched to the opening at six: its hours stand on it as it moves, and its
            // end stays where Tom's shift starts, as far as the game's handle goes.
            board.Chosen = 2;
            board.Moving = true;
            board.Shifts[2].Start = 6 * 60;
            board.MovingSpan = t("reallife_ui_bizops_span", "{from} to {to}").Replace("{from}", "06:00").Replace("{to}", "16:00");
        }
        board.Actions = none ? new UiActionModel[0] : new[]
        {
            new UiActionModel { Label = t("bizman_schedule_auto_fill", "Auto-fill all"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Spark, Key = UiAction.Count },
            new UiActionModel { Label = t("reallife_ui_bizops_clear_week", "Clear the week"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Trash, Key = UiAction.Count, Disabled = !full }
        };
        o.DayOpenWords = t("reallife_ui_bizops_open_on", "Open on {day}").Replace("{day}", closed ? "Sunday" : "Wednesday");
        o.DayOpen = !closed;
        o.HoursWords = closed ? t("reallife_ui_bizops_closed_day", "Closed this day") : t("reallife_ui_bizops_hours", "{from} to {to}").Replace("{from}", "06:00").Replace("{to}", "22:00");
        o.HoursPress = !closed;
        o.HoursOpen = state == "schedule_hours";
        o.HoursTitle = t("bizman_schedule_opening_hours", "Opening hours");
        o.HoursLine = t("reallife_ui_bizops_hours_line", "Press an hour to open or close it.");
        o.HourWords = new string[BizOpsRules.Hours];
        for (int h = 0; h < BizOpsRules.Hours; h++)
        {
            o.HourWords[h] = h.ToString("00", CultureInfo.InvariantCulture) + ":00";
        }
        o.DoneWords = t("reallife_ui_bizops_done", "Done");
        o.CancelWords = t("common_cancel", "Cancel");
        o.LaneSearchHint = none ? null : t("bizman_schedule_search_workstation", "Search workstation");
        o.LaneSearch = none ? null : string.Empty;
        if (state == "schedule_drag")
        {
            m.Actions = new[]
            {
                new UiActionModel { Label = t("bizman_schedule_manage_employee_tooltip", "Manage employee"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Person, Key = UiAction.Count },
                new UiActionModel { Label = t("reallife_ui_bizops_remove_shift", "Remove shift"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count },
                new UiActionModel { Label = t("reallife_ui_bizops_done", "Done"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt }
            };
        }
        else if (!none)
        {
            m.Actions = new[]
            {
                new UiActionModel { Label = t("reallife_ui_bizops_copy", "Copy day"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Disabled = !full },
                new UiActionModel { Label = t("reallife_ui_bizops_paste", "Paste day"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Disabled = !full },
                new UiActionModel { Label = t("reallife_ui_bizops_paste_all", "Paste to every day"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Disabled = !full },
                new UiActionModel { Label = t("reallife_ui_bizops_clear", "Clear day"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Trash, Key = UiAction.Count, Disabled = !full },
                new UiActionModel { Label = t("bizman_schedule_auto_fill_day", "Auto-fill day"), Kind = UiButtonKind.Primary, Icon = UiIcon.Spark, Key = UiAction.Alt, Disabled = closed }
            };
        }
        if (state == "schedule_picker")
        {
            OpsPicker(t, o, names);
        }
        return m;
    }

    // The game's picker for Register 2 at two in the afternoon, best fits first: who is free, who is busy then, who would
    // work too long or against what they asked for.
    private static void OpsPicker(UiWords t, BizOpsModel o, string[] names)
    {
        o.Picking = true;
        o.PeopleTitle = t("reallife_ui_bizops_pick_title", "{station} at {hour}").Replace("{station}", "Register 2").Replace("{hour}", "14:00");
        o.PeopleLine = t("reallife_ui_bizops_pick_line", "Choose who works this shift. Those already busy are greyed out.");
        int[] order = { 10, 2, 8, 6, 4, 3, 7, 0, 1, 5, 9, 11 };
        string[] fits = { "best", "free", "free", "free", "demand", "tired", "free", "busy", "busy", "busy", "free", "free" };
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_ui_bizops_col_person", "Person"), Kind = UiCellKind.Person },
                new UiDataColumn { Title = t("reallife_ui_bizops_col_skill", "Skill"), Kind = UiCellKind.Figure, Width = 64f, Graded = true, Sortable = true },
                new UiDataColumn { Title = t("reallife_ui_bizops_col_week", "This week"), Kind = UiCellKind.Figure, Width = 72f, Sortable = true },
                new UiDataColumn { Title = t("reallife_ui_bizops_col_fit", "Fit"), Kind = UiCellKind.Chip, Width = 112f },
            },
            SortColumn = 1, SortDown = true, SearchPlaceholder = t("bizman_schedule_employee_search_placeholder", "e.g. \"Uncle Fred\""), Query = string.Empty,
            Filters = new[] { t("reallife_ui_staff_all_demands", "Every demand") }, EmptyIcon = UiIcon.People,
            EmptyTitle = t("bizman_schedule_employee_no_employees", "No employees found. Contact a recruiter to hire employees for this business.")
        };
        table.Rows = new UiDataRow[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            int p = order[i];
            string fit = fits[i];
            UiRole tone = fit == "busy" ? UiRole.Muted : fit == "demand" || fit == "tired" ? UiRole.Warn : UiRole.Ok;
            string words = fit == "best" ? t("reallife_ui_bizops_fit_best", "Best fit") : fit == "free" ? t("reallife_ui_bizops_fit_free", "Free")
                : fit == "busy" ? t("reallife_ui_bizops_fit_busy", "Busy") : fit == "tired" ? t("reallife_ui_bizops_fit_tired", "Overworked")
                : t("reallife_ui_bizops_fit_demand", "Against their wishes");
            table.Rows[i] = new UiDataRow
            {
                Cells = new[] { names[p], OpsWhole(OpsSkill[p]) + "%", t("reallife_ui_bizops_hours_week", "{hours} h").Replace("{hours}", OpsWhole(OpsWeek[p])), words },
                Values = new[] { 0, OpsSkill[p], 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone },
                Sub = OpsSkillWords[p] + ", " + t("bizman_schedule_hourly_wage", "{wage}/h").Replace("{wage}", OpsMoney(OpsWage[p])), Hue = UiColour.HueOf(names[p]),
                Ring = StaffViews.Mood(OpsMood[p])
            };
        }
        table.RowCount = order.Length;
        o.People = table;
    }

    // ------------------------------------------------------------------------------------------------- the managers

    // The plans' list as the game's own holds them: what each covers, who runs it, the chosen one, one without a manager.
    private static void OpsPlans(UiWords t, BizOpsModel o, string title, string[] names, string[] lines, int chosen, int unassigned, UiIcon glyph)
    {
        o.PlansTitle = title;
        o.Plans = new BizManItem[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            o.Plans[i] = new BizManItem
            {
                Glyph = glyph, Name = names[i], Line = i == unassigned ? t("common_unassigned", "Unassigned") : lines[i], Chosen = i == chosen,
                WarnWords = i == unassigned ? t("reallife_ui_bizops_no_manager", "No manager") : null
            };
        }
        o.FormEmpty = t("reallife_ui_bizops_plan_choose", "Choose a plan");
        o.FormEmptyBody = t("reallife_ui_bizops_plan_choose_body", "Pick a plan on the left to see its settings and goods here.");
    }

    // The plans' foot as the screen builds it: the chosen one moved up or down, and Add plan, the page's main action only
    // while the form has none of its own.
    private static void OpsPlanActions(UiWords t, BizOpsModel o, int chosen)
    {
        bool formMain = false;
        for (int i = 0; i < o.Actions.Length; i++)
        {
            formMain |= o.Actions[i].Kind == UiButtonKind.Primary;
        }
        var actions = new System.Collections.Generic.List<UiActionModel>(3);
        if (chosen >= 0 && o.Plans.Length > 1)
        {
            actions.Add(new UiActionModel { Kind = UiButtonKind.Quiet, Icon = UiIcon.Down, Key = UiAction.Count, Disabled = chosen >= o.Plans.Length - 1 });
            actions.Add(new UiActionModel { Kind = UiButtonKind.Quiet, Icon = UiIcon.Up, Key = UiAction.Count, Disabled = chosen == 0 });
        }
        actions.Add(new UiActionModel { Label = t("bizman_add_plan", "Add plan"), Kind = formMain ? UiButtonKind.Secondary : UiButtonKind.Primary, Icon = UiIcon.Plus,
            Key = formMain ? UiAction.Count : UiAction.Alt });
        o.PlanActions = actions.ToArray();
    }

    private static BizOpsRow OpsChoice(string label, string value, bool right = false) => new BizOpsRow { Kind = BizOpsRowKind.Choice, Label = label, Value = value, Right = right };

    private static BizOpsRow OpsInfo(string label, string value, bool right = false, UiRole tone = UiRole.None) =>
        new BizOpsRow { Kind = BizOpsRowKind.Info, Label = label, Value = value, Right = right, Tone = tone };

    private static BizOpsRow OpsSwitch(string label, bool on, bool right = false) => new BizOpsRow { Kind = BizOpsRowKind.Switch, Label = label, On = on, Right = right };

    private static BizOpsRow OpsHeading(string label, bool right = false) => new BizOpsRow { Kind = BizOpsRowKind.Heading, Label = label, Right = right };

    private static BizOpsRow OpsNote(string words, bool right = false) => new BizOpsRow { Kind = BizOpsRowKind.Note, Value = words, Right = right };

    private static BizOpsRow OpsButton(string words, UiIcon icon, bool danger, bool right = false) =>
        new BizOpsRow { Kind = BizOpsRowKind.Button, Value = words, Icon = icon, Tone = danger ? UiRole.Danger : UiRole.None, Right = right };

    private static readonly string[] OpsGoods =
    {
        "Americano", "Cappuccino", "Caffè latte", "Espresso", "Flat white", "Mocha", "Iced coffee", "Hot chocolate", "Green tea", "Chai latte", "Croissant",
        "Blueberry muffin", "Bagel with cream cheese", "Cinnamon roll", "Chocolate chip cookie", "Banana bread", "Fruit salad", "Orange juice", "Bottled water", "Cola",
        "Ham and cheese sandwich", "Caesar salad", "Cheeseburger", "French fries", "Hot dog", "Pizza slice", "Vanilla ice cream", "Glazed donut", "Red velvet cupcake",
        "Lemonade", "Strawberry smoothie", "Paperback novel", "Spiral notebook", "Bouquet of roses", "Potted orchid", "Phone charger", "Wireless earbuds", "Cotton shirt",
        "Scented candle", "Gift card"
    };

    private static readonly int[] OpsPrices =   // cents
    {
        350, 425, 450, 275, 425, 475, 400, 375, 300, 450, 325, 350, 425, 375, 225, 300, 550, 375, 175, 225, 750, 825, 950, 400, 450, 375, 425, 225, 375, 350,
        600, 1499, 499, 2999, 3499, 1999, 7999, 2499, 1599, 2500
    };

    private static BizManModel OpsPricing(UiWords t, string state)
    {
        BizManModel m = OpsPlace(t, "headquarters", BizManPart.Managers, 2);
        BizOpsModel o = m.Ops;
        o.PlansEmpty = t("reallife_ui_bizops_pricing_none", "No pricing plans yet");
        o.PlansEmptyBody = t("reallife_ui_bizops_pricing_none_body", "Add a plan to have a pricing manager set the prices of a set of shops.");
        if (state == "pricing_none")
        {
            o.PlansTitle = t("bizman_menu_pricing_managers", "Pricing Managers");
            OpsPlanActions(t, o, -1);
            return m;
        }
        string[] districts = { "Midtown", "Hell's Kitchen", "Garment District", "Lower Manhattan" };
        string[] managers = { "Grace Holloway, 84%", "Omar Haddad, 71%", "Ines Duarte, 66%", null };
        int chosen = state == "pricing_unassigned" ? 3 : 0;
        OpsPlans(t, o, t("bizman_menu_pricing_managers", "Pricing Managers"), districts, managers, chosen, 3, UiIcon.MapTrifold);
        o.FormTitle = districts[chosen];
        if (state == "pricing_unassigned")
        {
            // The game's own question for a plan without a manager: who runs it, what that gives, and Delete plan.
            o.Rows = new[]
            {
                OpsHeading(t("bizman_pricingmanagers_not_assigned", "No Pricing Manager assigned")),
                OpsNote(t("bizman_pricingmanagers_not_assigned_description", "Assign a Pricing Manager to get price suggestions for this neighborhood.")),
                OpsChoice(t("reallife_ui_bizops_manager", "Manager"), t("common_unassigned", "Unassigned"))
            };
            o.Actions = new[] { new UiActionModel { Label = t("bizman_delete_plan", "Delete plan"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count } };
            OpsPlanActions(t, o, chosen);
            return m;
        }
        o.Rows = new[]
        {
            OpsChoice(t("reallife_ui_bizops_manager", "Manager"), "Grace Holloway, 84%"),
            OpsChoice(t("common_neighborhood", "Neighborhood"), districts[0])
        };
        o.FormLead = "Owned businesses 4";
        o.Actions = new[]
        {
            new UiActionModel { Label = t("bizman_pricingmanagers_apply_suggested_prices", "Apply Suggested Prices"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt }
        };
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("bizman_pricingmanagers_header_product", "Product"), Kind = UiCellKind.Text, Sortable = true },
                new UiDataColumn { Title = t("bizman_pricingmanagers_header_current_price", "Current Price"), Kind = UiCellKind.Money, Width = 128f, Sortable = true },
                new UiDataColumn { Title = t("bizman_pricingmanagers_header_suggested_price", "Suggested Price"), Kind = UiCellKind.Money, Width = 176f, Sortable = true },
                new UiDataColumn { Title = t("bizman_pricingmanagers_header_rivals_prices", "Market Price"), Kind = UiCellKind.Money, Width = 128f, Sortable = true },
                new UiDataColumn { Title = t("bizman_pricingmanagers_header_new_price", "New Price"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth, OnChosen = true },
            },
            EmptyIcon = UiIcon.Tag, EmptyTitle = t("bizman_pricingmanagers_no_products", "No products to price"), SortColumn = 0, Selected = 1
        };
        table.Rows = new UiDataRow[OpsGoods.Length];
        for (int i = 0; i < OpsGoods.Length; i++)
        {
            int price = OpsPrices[i], low = (price * 95 / 100 + 2) / 5 * 5, high = (price * 110 / 100 + 2) / 5 * 5;
            string suggested = i % 7 == 3 ? OpsMoney(price) : t("reallife_ui_bizops_range", "{from} to {to}").Replace("{from}", OpsMoney(low)).Replace("{to}", OpsMoney(high));
            table.Rows[i] = new UiDataRow
            {
                Cells = new[] { OpsGoods[i], OpsMoney(price), suggested, i % 9 == 5 ? "-" : OpsMoney((price * 104 / 100 + 2) / 5 * 5), OpsMoney(i % 4 == 1 ? high : price) },
                Values = new[] { 0, 0, 0, 0, UiDataTable.CanLess | UiDataTable.CanMore }
            };
        }
        table.RowCount = OpsGoods.Length;
        o.Table = table;
        OpsPlanActions(t, o, chosen);
        return m;
    }

    private static BizManModel OpsLogistics(UiWords t)
    {
        BizManModel m = OpsPlace(t, "headquarters", BizManPart.Managers, 3);
        BizOpsModel o = m.Ops;
        o.Tabs = new[] { t("bizman_warehouses", "Warehouses"), t("reallife_ui_bizops_factories", "Factories") };
        o.Tab = 0;
        o.PlansEmpty = t("reallife_ui_bizops_routes_none", "No delivery routes yet");
        o.PlansEmptyBody = t("reallife_ui_bizops_routes_none_body", "Add a plan to have a logistics manager run a driver between your warehouses and shops.");
        OpsPlans(t, o, t("bizman_menu_logistics_managers", "Logistics Managers"), new[] { "Pier 40 Storage", "Red Hook Depot", "Bronx Terminal" },
            new[] { "Isaac Brennan, 77%", "Mei Tanaka, 69%", null }, 0, 2, UiIcon.Package);
        o.FormTitle = "Pier 40 Storage";
        o.Rows = new[]
        {
            OpsChoice(t("reallife_ui_bizops_manager", "Manager"), "Isaac Brennan, 77%"),
            OpsChoice(t("ba:businesstype_warehouse", "Warehouse"), "Pier 40 Storage", true)
        };
        o.FormLead = "Will deliver up to 4 destinations";
        o.Actions = new[]
        {
            new UiActionModel { Label = t("common_show_warehouse", "Show Warehouse"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Locate, Key = UiAction.Count },
            new UiActionModel { Label = t("bizman_logisticsmanagers_adddestination", "Add new destination"), Kind = UiButtonKind.Primary, Icon = UiIcon.Plus, Key = UiAction.Alt }
        };
        string goods = t("reallife_ui_bizops_goods_count", "{count} goods");
        o.StopsTitle = t("reallife_ui_bizops_stops", "Stops");
        o.Stops = new[]
        {
            new BizOpsStop { Name = Businesses[0], Line = "Destination #1, " + goods.Replace("{count}", "6"), Open = true, Removable = true },
            new BizOpsStop { Name = Businesses[3], Line = "Destination #2, " + goods.Replace("{count}", "4"), Removable = true },
            new BizOpsStop { Name = Businesses[6], Line = "Destination #3, " + goods.Replace("{count}", "5"), Removable = true }
        };
        o.TableLead = t("reallife_ui_bizops_stop_goods", "Goods the route keeps in stock at {place}.").Replace("{place}", Businesses[0]);
        string[] names = { "Coffee beans", "Whole milk", "Croissant", "Paper cups", "Sugar sachets", "Bottled water" };
        string[] stock = { "1,240", "860", "420", "3,600", "540", "960" };
        string[] runs = { "6 days", "3 days", "2 days", "12 days", "9 days", "Never" };
        string[] least = { "120", "200", "80", "500", "60", "100" };
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("common_product", "Product"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("bizman_logisticsmanagers_warehouse_stock", "Warehouse Stock"), Kind = UiCellKind.Figure, Width = 152f },
                new UiDataColumn { Title = t("bizman_logisticsmanagers_runs_out_in", "Runs out in"), Kind = UiCellKind.Text, Width = 136f },
                new UiDataColumn { Title = t("bizman_logisticsmanagers_min_stock_amount", "Minimum stock amount"), Kind = UiCellKind.Stepper, Width = UiDataTable.StepperWidth, OnChosen = true },
            },
            EmptyIcon = UiIcon.Package, EmptyTitle = t("bizman_logisticsmanagers_no_products", "No products available to deliver to this location."), Selected = 1
        };
        table.Rows = new UiDataRow[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            table.Rows[i] = new UiDataRow { Cells = new[] { names[i], stock[i], runs[i], least[i] }, Values = new[] { 0, 0, 0, UiDataTable.CanLess | UiDataTable.CanMore } };
        }
        table.RowCount = names.Length;
        o.Table = table;
        OpsPlanActions(t, o, 0);
        return m;
    }

    private static BizManModel OpsPurchasing(UiWords t)
    {
        BizManModel m = OpsPlace(t, "headquarters", BizManPart.Managers, 4);
        BizOpsModel o = m.Ops;
        o.PlansEmpty = t("reallife_ui_bizops_imports_none", "No purchasing plans yet");
        o.PlansEmptyBody = t("reallife_ui_bizops_imports_none_body", "Add a plan to keep your warehouses stocked from importers.");
        OpsPlans(t, o, t("bizman_menu_purchasing_agents", "Purchasing Agents"), new[] { "Total Produce Trading", "Pacific Coffee Importers", "Atlas Paper Goods" },
            new[] { "Rosa Alvarez", "Rosa Alvarez", null }, 1, 2, UiIcon.Handshake);
        o.FormTitle = "Pacific Coffee Importers";
        o.Rows = new[]
        {
            OpsChoice(t("reallife_ui_bizops_manager", "Manager"), "Rosa Alvarez, 81%"),
            OpsInfo(t("bizman_purchasingagents_next_delivery", "Next delivery"), "Thursday 06:00"),
            OpsInfo(t("bizman_purchasingagents_lock_time_header", "Order must be placed by"), "Wednesday 22:00"),
            OpsInfo(t("bizman_purchasingagents_total", "Total Cost"), "$4,280"),
            OpsSwitch(t("repeating_order", "Repeating order"), true, true),
            OpsSwitch(t("bizman_purchasingagents_smart_delivery", "Smart Delivery"), false, true),
            OpsHeading("Arabica coffee beans", true),
            new BizOpsRow { Kind = BizOpsRowKind.Text, Label = t("reallife_ui_bizops_target", "Target"), Value = "400", Right = true },
            OpsChoice(t("bizman_purchasingagents_designated_warehouse", "Designated warehouse"), "Pier 40 Storage", true),
            OpsSwitch(t("reallife_ui_bizops_pick", "Picked"), true, true)
        };
        o.Actions = new[]
        {
            new UiActionModel { Label = t("bizman_purchasingagents_end_partnership", "End import partnership"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Close, Key = UiAction.Count },
            new UiActionModel { Label = t("bizman_deliveries_urgent_order", "Urgent Order"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Lightning, Key = UiAction.Count },
            new UiActionModel { Label = t("bizman_purchasingagents_order", "Order"), Kind = UiButtonKind.Primary, Icon = UiIcon.ShoppingCart, Key = UiAction.Alt }
        };
        string[] names = { "Arabica coffee beans", "Robusta coffee beans", "Decaf coffee beans", "Oat milk", "Vanilla syrup", "Caramel syrup", "Paper cups", "Cup lids" };
        int[] target = { 400, 120, 60, 240, 40, 40, 2000, 2000 };
        int[] price = { 1180, 940, 1260, 320, 610, 610, 9, 4 };
        int[] stock = { 312, 96, 0, 188, 22, 31, 1460, 1720 };
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("bizman_purchasingagents_product", "Product"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_ui_bizops_target", "Target"), Kind = UiCellKind.Figure, Width = 96f },
                new UiDataColumn { Title = t("bizman_purchasingagents_price", "Price"), Kind = UiCellKind.Money, Width = 128f },
                new UiDataColumn { Title = t("bizman_purchasingagents_warehouse_stock", "Warehouse stock"), Kind = UiCellKind.Figure, Width = 136f },
                new UiDataColumn { Title = t("bizman_purchasingagents_designated_warehouse", "Designated warehouse"), Kind = UiCellKind.Text, Width = 200f },
            },
            EmptyIcon = UiIcon.ShoppingCart, EmptyTitle = t("reallife_ui_bizops_imports_goods_none", "No goods in this plan yet"), Selected = 0
        };
        table.Rows = new UiDataRow[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            bool none = i == 2;
            table.Rows[i] = new UiDataRow
            {
                Cells = new[] { names[i], OpsWhole(target[i]), OpsMoney(price[i]), OpsWhole(stock[i]), none ? t("common_unassigned", "Unassigned") : "Pier 40 Storage" },
                Values = new[] { 0, 0, 0, 0, 0 }, Picked = i < 2,
                Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, none ? UiRole.WarnText : UiRole.None }
            };
        }
        table.RowCount = names.Length;
        o.Table = table;
        OpsPlanActions(t, o, 1);
        return m;
    }

    private static BizManModel OpsHr(UiWords t, bool assigning)
    {
        BizManModel m = OpsPlace(t, "headquarters", BizManPart.Managers, 5);
        BizOpsModel o = m.Ops;
        o.PlansEmpty = t("reallife_ui_bizops_hr_none", "No HR plans yet");
        o.PlansEmptyBody = t("reallife_ui_bizops_hr_none_body", "Add a plan to have an HR manager look after a group of your staff.");
        string line = t("reallife_ui_bizops_hr_line", "Staff: {count}");
        OpsPlans(t, o, t("bizman_menu_hr_managers", "HR Managers"), new[] { "Olivia Bennett", "Marcus Chen", t("common_unassigned", "Unassigned") },
            new[] { line.Replace("{count}", "8/10"), line.Replace("{count}", "5/10"), line.Replace("{count}", "0/10") }, 0, 2, UiIcon.People);
        o.Plans[2].Line = line.Replace("{count}", "0/10");
        o.FormTitle = "Olivia Bennett";
        o.FormLead = "8 out of 10 employees assigned";
        o.Rows = new[]
        {
            OpsChoice(t("reallife_ui_bizops_manager", "Manager"), "Olivia Bennett, 86%"),
            OpsSwitch(t("reallife_ui_bizops_hr_replace", "Replace staff who leave"), true),
            new BizOpsRow { Kind = BizOpsRowKind.Slider, Label = t("reallife_ui_bizops_hr_train", "Training"), Value = "70%", Share = 0.7f },
            OpsInfo(t("bizman_hrmanager_average_salary", "Average Salary"), "$16.40/hr", true),
            new BizOpsRow { Kind = BizOpsRowKind.Meter, Label = t("reallife_ui_bizops_hr_mood", "Average happiness"), Grade = 76, Right = true },
            new BizOpsRow { Kind = BizOpsRowKind.Meter, Label = t("reallife_ui_bizops_hr_skill", "Average skill"), Grade = 64, Right = true },
            OpsHeading(t("reallife_ui_bizops_hr_cover", "Health insurance"), true),
            OpsNote("Basic (daily $4.50 per employee, minimum 5 employees)", true),
            OpsButton(t("bizman_hrmanager_upgrade_insurance_button", "Upgrade Insurance"), UiIcon.ShieldCheck, false, true),
            OpsButton(t("bizman_hrmanager_cancel_insurance_button", "Cancel insurance"), UiIcon.Close, true, true)
        };
        o.Actions = assigning
            ? new[]
            {
                new UiActionModel { Label = t("bizman_hrmanager_clear", "Unassign All"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close, Key = UiAction.Count },
                new UiActionModel { Label = t("bizman_hrmanager_fill", "Fill"), Kind = UiButtonKind.Secondary, Icon = UiIcon.People, Key = UiAction.Count },
                new UiActionModel { Label = t("reallife_ui_bizops_done", "Done"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt }
            }
            : new[] { new UiActionModel { Label = t("bizman_hrmanager_assign_employees", "Assign employees"), Kind = UiButtonKind.Primary, Icon = UiIcon.People, Key = UiAction.Alt } };
        o.TableTitle = assigning ? "Assign employees to Olivia Bennett" : t("reallife_ui_bizops_hr_staff", "Staff in this plan");
        string[] places = { "Bloom Café", "Bloom Café", "Copper Kettle Coffee", "Little Italy Bakery", "Bloom Café", "Harbour Books", "Copper Kettle Coffee", "Little Italy Bakery",
            "Harbour Books", "Bloom Café", "Hudson Jewellers", "Riverside Gifts" };
        string[] cover = { "Basic", "None", "Basic", "Premium", "Basic", "None", "Basic", "Standard", "None", "Basic", "Premium", "Standard" };
        int count = assigning ? 12 : 8;
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_ui_bizops_col_person", "Person"), Kind = UiCellKind.Person },
                new UiDataColumn { Title = t("common_business", "Business"), Kind = UiCellKind.Text, Width = 200f },
                new UiDataColumn { Title = t("reallife_ui_bizops_col_wage", "Wage"), Kind = UiCellKind.Money, Width = 112f },
                new UiDataColumn { Title = t("bizman_hrmanager_insurance_demand", "Insurance Demand"), Kind = UiCellKind.Text, Width = 152f },
                assigning ? new UiDataColumn { Title = t("reallife_ui_bizops_col_assigned", "In this plan"), Kind = UiCellKind.Chip, Width = 128f }
                    : new UiDataColumn { Title = t("reallife_ui_bizops_hr_mood", "Average happiness"), Kind = UiCellKind.Meter, Width = 136f },
            },
            EmptyIcon = UiIcon.People, EmptyTitle = assigning ? t("reallife_ui_bizops_hr_assign_none", "No one to assign") : t("reallife_ui_bizops_hr_staff_none", "No one in this plan yet"),
            EmptyBody = assigning ? null : t("reallife_ui_bizops_hr_staff_none_body", "Assign staff to this plan and the HR manager looks after them.")
        };
        table.Rows = new UiDataRow[count];
        for (int i = 0; i < count; i++)
        {
            bool inPlan = i < 8;
            table.Rows[i] = new UiDataRow
            {
                Cells = new[] { OpsStaff[i], places[i], t("bizman_hrmanager_salary", "{wage}/hr").Replace("{wage}", OpsMoney(OpsWage[i])), cover[i],
                    assigning ? (inPlan ? t("reallife_ui_bizops_assigned", "In the plan") : t("reallife_ui_bizops_not_assigned", "Not in it")) : OpsWhole(OpsMood[i]) + "%" },
                Values = new[] { 0, 0, 0, 0, assigning ? 0 : OpsMood[i] },
                Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, inPlan ? UiRole.Ok : UiRole.Muted },
                Sub = OpsSkillWords[i], Hue = UiColour.HueOf(OpsStaff[i]), Ring = StaffViews.Mood(OpsMood[i])
            };
        }
        table.RowCount = count;
        o.Table = table;
        OpsPlanActions(t, o, 0);
        return m;
    }

    private static BizManModel OpsHunters(UiWords t, bool replacing)
    {
        BizManModel m = OpsPlace(t, "headquarters", BizManPart.Managers, 6);
        BizOpsModel o = m.Ops;
        o.PlansEmpty = t("reallife_ui_bizops_hunt_none", "No headhunters yet");
        o.PlansEmptyBody = t("reallife_ui_bizops_hunt_none_body", "Add a headhunter to recruit and replace staff for your HR plans.");
        string line = t("reallife_ui_bizops_hunt_line", "HR plans: {count}");
        OpsPlans(t, o, t("bizman_menu_headhunters", "Headhunters"), new[] { "Nadia Petrova", "Felix Okoro" }, new[] { line.Replace("{count}", "2/3"), line.Replace("{count}", "1/3") },
            0, -1, UiIcon.IdBadge);
        o.FormTitle = "Nadia Petrova";
        o.Tabs = new[] { t("bizman_menu_headhunters_recruiting_header", "Recruiting"), t("bizman_menu_headhunters_automatic_replacement_header", "Automatic Replacement") };
        o.Tab = replacing ? 1 : 0;
        if (replacing)
        {
            o.Rows = new[]
            {
                OpsChoice(t("reallife_ui_bizops_manager", "Manager"), "Nadia Petrova, 82%"),
                OpsSwitch(t("reallife_ui_bizops_hunt_resign", "Replace staff who resign"), true),
                OpsNote("Replacement Fee: $2.5K"),
                OpsSwitch(t("reallife_ui_bizops_hunt_retire", "Replace staff who retire"), false, true),
                OpsNote("Replacement Fee: $2.5K", true)
            };
            o.TableTitle = t("reallife_ui_bizops_hunt_staff", "Staff who may leave soon");
            string[] reasons = { "Resigns in 3 days", "Retires in 12 days", "Unhappy with wage", "Resigns in 6 days" };
            var table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = t("reallife_ui_bizops_col_person", "Person"), Kind = UiCellKind.Person },
                    new UiDataColumn { Title = t("common_business", "Business"), Kind = UiCellKind.Text, Width = 224f },
                    new UiDataColumn { Title = t("bizman_headhunters_replacement_reason", "Replacement Reason"), Kind = UiCellKind.Text, Width = 240f },
                },
                EmptyIcon = UiIcon.People, EmptyTitle = t("reallife_ui_bizops_hunt_staff_none", "No one is about to leave")
            };
            table.Rows = new UiDataRow[reasons.Length];
            for (int i = 0; i < reasons.Length; i++)
            {
                int p = new[] { 5, 9, 3, 1 }[i];
                table.Rows[i] = new UiDataRow
                {
                    Cells = new[] { OpsStaff[p], Businesses[i * 2], reasons[i] }, Values = new[] { 0, 0, 0 }, Sub = OpsSkillWords[p], Hue = UiColour.HueOf(OpsStaff[p])
                };
            }
            table.RowCount = reasons.Length;
            o.Table = table;
        }
        else
        {
            o.Rows = new[]
            {
                OpsChoice(t("reallife_ui_bizops_manager", "Manager"), "Nadia Petrova, 82%"),
                OpsHeading(t("bizman_headhunters_wage_and_skill_title", "Job Wage & Skill")),
                OpsChoice(t("bizman_headhunters_type_of_employee_title", "Type Of Employee"), "Customer service"),
                new BizOpsRow { Kind = BizOpsRowKind.Slider, Label = t("reallife_ui_bizops_hunt_target", "Target skill"), Value = "70%", Share = 0.7f },
                OpsInfo(t("bizman_headhunters_salary_range_title", "Salary Range"), "$15.2 to $19.8/h"),
                OpsSwitch(t("reallife_ui_bizops_hunt_always", "Recruit continuously"), false),
                OpsSwitch(t("reallife_ui_bizops_hunt_count", "Recruit a number of candidates"), true),
                new BizOpsRow { Kind = BizOpsRowKind.Text, Label = t("reallife_ui_bizops_hunt_amount", "Candidates"), Value = "3" },
                OpsButton(t("bizman_headhunters_start_recruiting_button", "Start Recruiting"), UiIcon.Play, false),
                OpsHeading(t("bizman_headhunters_deal_breakers_title", "Deal Breakers"), true),
                OpsNote("Points used: 3", true),
                OpsSwitch("Smokes on breaks", true, true),
                OpsSwitch("Wants every weekend off", true, true),
                OpsSwitch("Asks for health insurance", false, true),
                OpsSwitch("Works part time only", false, true),
                OpsButton(t("bizman_headhunters_deal_breakers_clear_all_button", "Clear All"), UiIcon.Close, false, true)
            };
        }
        OpsPlanActions(t, o, 0);
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the factory

    private static BizManModel OpsFactory(UiWords t, string state)
    {
        BizManModel m = OpsPlace(t, "factory", BizManPart.Factory, 1);
        BizOpsModel o = m.Ops;
        o.PlansEmpty = t("reallife_ui_bizops_machines_none", "No production machines");
        o.PlansEmptyBody = t("reallife_ui_bizops_machines_none_body", "Place production machines in the factory to set up its workstations.");
        if (state == "factory_none")
        {
            o.Table = new UiDataTableModel();
            return m;
        }
        o.Tabs = new[] { "Coffee roasting", "Packaging" };
        o.Tab = 0;
        o.TableTitle = t("bizman_factory_production_machines", "Production Machines");
        string[] machines = { "Roaster 1", "Roaster 2", "Roaster 3", "Grinder 1", "Grinder 2", "Blending station" };
        string[] makes = { "Roasted coffee beans", "Roasted coffee beans", "Roasted coffee beans", "Ground coffee", "Ground coffee", "House blend" };
        int[] worker = { 0, 2, -1, 4, 6, -1 };
        int[] made = { 36, 41, 0, 52, 48, 0 };
        bool missing = state == "factory_missing";
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_ui_bizops_col_machine", "Workstation"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_ui_bizops_col_makes", "Makes"), Kind = UiCellKind.Text, Width = 152f },
                new UiDataColumn { Title = t("reallife_ui_bizops_worker", "Worker"), Kind = UiCellKind.Person, Width = 208f },
                new UiDataColumn { Title = t("reallife_ui_bizops_output", "Output"), Kind = UiCellKind.Text, Width = 136f },
            },
            EmptyIcon = UiIcon.Factory, EmptyTitle = t("reallife_ui_bizops_machines_none", "No production machines"), Selected = missing ? 5 : 0
        };
        table.Rows = new UiDataRow[machines.Length];
        for (int i = 0; i < machines.Length; i++)
        {
            bool running = worker[i] >= 0;
            string who = running ? OpsStaff[worker[i]] : t("bizman_factory_no_employee_assigned", "No employee assigned");
            string state0 = running ? t("bizman_factory_running", "Running") : i == 5 ? t("bizman_factory_inactive_reason_invalid_workstation", "Missing production machines")
                : t("bizman_factory_not_running", "Not Running");
            table.Rows[i] = new UiDataRow
            {
                Cells = new[] { machines[i], makes[i], who, running ? t("common_per_hour", "{textValue} / hour").Replace("{textValue}", OpsWhole(made[i]) + "x " + makes[i]) : "-" },
                Values = new[] { (int)UiIcon.Factory, 0, 0, 0 },
                Tones = new[] { running ? UiRole.None : UiRole.Warn, UiRole.None, UiRole.None, UiRole.None },
                Subs = new[] { state0, null, running ? t("reallife_ui_bizops_skill_line", "{skill} {level}%").Replace("{skill}", t("ba:skill_factoryworker", "Factory Worker"))
                    .Replace("{level}", OpsWhole(OpsSkill[worker[i]])) : null, null },
                Hue = running ? UiColour.HueOf(who) : 0f, Ring = running ? StaffViews.Mood(OpsMood[worker[i]]) : UiRole.None
            };
        }
        table.RowCount = machines.Length;
        o.Table = table;
        if (missing)
        {
            o.FormTitle = "Blending station";
            o.Rows = new[]
            {
                OpsHeading(t("bizman_factory_inactive_reason_invalid_workstation", "Missing production machines")),
                OpsNote(t("reallife_ui_bizops_machine_missing", "This workstation lacks a machine it needs. Place the missing machine next to it."))
            };
            return m;
        }
        o.FormTitle = "Roaster 1";
        o.Rows = new[]
        {
            OpsChoice(t("reallife_ui_bizops_recipe", "Recipe"), "Roasted coffee beans"),
            OpsSwitch(t("bizman_factory_produce_up_to", "Produce up to"), true),
            new BizOpsRow { Kind = BizOpsRowKind.Stepper, Label = t("reallife_ui_bizops_amount", "Amount"), Value = "500", CanLess = true, CanMore = true },
            OpsSwitch(t("bizman_factory_produce_continuously", "Produce continuously"), false),
            new BizOpsRow { Kind = BizOpsRowKind.Text, Label = t("bizman_schedule_rename_workstation", "Rename workstation"), Value = "Roaster 1",
                Placeholder = t("bizman_factory_workstation_name_placeholder", "Workstation Name...") },
            OpsHeading(t("bizman_factory_production_flow_title", "Production flow"), true),
            OpsInfo(t("reallife_ui_bizops_worker", "Worker"), OpsStaff[0], true),
            OpsButton(t("bizman_schedule_manage_employee_tooltip", "Manage employee"), UiIcon.Person, false, true),
            OpsInfo(t("reallife_ui_bizops_output", "Output"), "36x Roasted coffee beans / hour", true),
            OpsHeading(t("bizman_factory_material_consumption_title", "Material consumption"), true),
            OpsInfo("40x Green coffee beans", t("reallife_ui_bizops_in_stock", "{count} in stock").Replace("{count}", "1,860"), true),
            OpsInfo("4x Burlap sack", t("reallife_ui_bizops_in_stock", "{count} in stock").Replace("{count}", "12"), true, UiRole.DangerText)
        };
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the warehouse

    private static BizManModel OpsDrivers(UiWords t)
    {
        BizManModel m = OpsPlace(t, "warehouse", BizManPart.Drivers, 1);
        BizOpsModel o = m.Ops;
        o.FormTitle = t("bizman_vehicle_slots", "Vehicle Slots");
        o.FormLead = t("bizman_drivers_vehicle_info", "Drive a vehicle through the warehouse's garage door to assign it to this slot");
        o.PlansEmpty = t("reallife_ui_bizops_slots_none", "No vehicle slots");
        o.PlansEmptyBody = o.FormLead;
        string[] vehicles = { "Box truck, NY 4821", "Box truck, NY 5530", "Cargo van, NY 1177", "Cargo van, NY 2096", "Pickup, NY 6604", null };
        string[] drivers = { "Ethan Walsh", "Samuel Adeyemi", "Lucas Ferreira", t("common_unassigned", "Unassigned"), "Tom Becker", t("common_unassigned", "Unassigned") };
        var rows = new System.Collections.Generic.List<BizOpsRow>();
        for (int i = 0; i < vehicles.Length; i++)
        {
            bool right = i >= (vehicles.Length + 1) / 2;
            rows.Add(OpsHeading(t("bizman_drivers_slot_number", "Slot #{number}").Replace("{number}", OpsWhole(i + 1)), right));
            rows.Add(OpsInfo(t("bizman_drivers_vehicle", "Vehicle"), vehicles[i] ?? t("bizman_warehouse_driverstation_no_vehicle_assigned", "No vehicle assigned"), right));
            rows.Add(OpsChoice(t("bizman_drivers_driver", "Driver"), drivers[i], right));
        }
        o.Rows = rows.ToArray();
        return m;
    }

    private static BizManModel OpsStock(UiWords t, bool empty)
    {
        BizManModel m = OpsPlace(t, "warehouse", BizManPart.Stock, 2);
        BizOpsModel o = m.Ops;
        o.FormLead = empty ? "Boxes: 0/2,400" : "Boxes: 1,486/2,400";
        o.Actions = new[] { new UiActionModel { Label = t("bizman_inventory_sell_all", "Sell All"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Coin, Key = UiAction.Count, Disabled = empty } };
        string[] names = { "Arabica coffee beans", "Robusta coffee beans", "Whole milk", "Oat milk", "Croissant", "Blueberry muffin", "Paper cups", "Cup lids", "Sugar sachets",
            "Bottled water", "Orange juice", "Vanilla syrup" };
        int[] stock = { 1240, 310, 860, 0, 420, 180, 3600, 3400, 540, 960, 75, 22 };
        int[] boxes = { 62, 16, 72, 0, 21, 9, 36, 34, 6, 40, 4, 2 };
        int[] import = { 400, 120, 600, 240, 300, 120, 2000, 2000, 300, 480, 120, 40 };
        int[] shipped = { 520, 140, 690, 260, 340, 160, 1800, 1700, 260, 300, 160, 38 };
        int[] days = { 6, 9, 3, -1, 2, 2, 12, 14, 9, int.MaxValue, 1, 4 };
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("common_product", "Product"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("bizman_inventory_in_stock", "In stock"), Kind = UiCellKind.Figure, Width = 112f },
                new UiDataColumn { Title = t("reallife_ui_bizops_col_boxes", "Boxes"), Kind = UiCellKind.Figure, Width = 88f },
                new UiDataColumn { Title = t("bizman_inventory_import", "Imported"), Kind = UiCellKind.Figure, Width = 112f },
                new UiDataColumn { Title = t("bizman_inventory_shipped", "Shipped"), Kind = UiCellKind.Figure, Width = 112f },
                new UiDataColumn { Title = t("bizman_inventory_balance", "Balance"), Kind = UiCellKind.Figure, Width = 112f },
                new UiDataColumn { Title = t("bizman_inventory_days_until_empty", "Days until empty"), Kind = UiCellKind.Text, Width = 176f },
            },
            EmptyIcon = UiIcon.Package, EmptyTitle = t("reallife_ui_bizops_stock_none", "The warehouse is empty"),
            EmptyBody = t("reallife_ui_bizops_stock_none_body", "Goods delivered or made here show with their stock and how long they last.")
        };
        int n = empty ? 0 : names.Length;
        table.Rows = new UiDataRow[n];
        for (int i = 0; i < n; i++)
        {
            int balance = import[i] - shipped[i];
            string left = days[i] < 0 ? t("bizman_inventory_run_out", "Run out") : days[i] == int.MaxValue ? t("bizman_inventory_never_runs_out", "Never")
                : t("bizman_inventory_product_days_until_empty", "{days} days").Replace("{days}", OpsWhole(days[i]));
            table.Rows[i] = new UiDataRow
            {
                Cells = new[] { names[i], OpsWhole(stock[i]), OpsWhole(boxes[i]), OpsWhole(import[i]), OpsWhole(shipped[i]), (balance > 0 ? "+" : string.Empty) + OpsWhole(balance), left },
                Values = new[] { 0, 0, 0, 0, 0, 0, 0 },
                Tones = new[] { UiRole.None, stock[i] == 0 ? UiRole.DangerText : UiRole.None, UiRole.None, UiRole.None, UiRole.None,
                    balance < 0 ? UiRole.DangerText : balance > 0 ? UiRole.OkText : UiRole.None, days[i] >= 0 && days[i] < 3 ? UiRole.WarnText : UiRole.None }
            };
        }
        table.RowCount = n;
        o.Table = table;
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the uniforms

    /// <summary>
    /// The uniform presets' editor by its state: a preset dressed with its changes unsaved, its worn colour's palette open,
    /// no preset yet, the longest names.
    /// </summary>
    public static UiUniformsModel Uniforms(UiWords t, string state)
    {
        bool fresh = state == "uniforms_new", longNames = state == "uniforms_long";
        var m = new UiUniformsModel { Title = t("myemployees_configure_uniform_presets", "Configure Uniform Presets"), Words = t, StandIn = true, CanNew = true };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_bizops_uniform_part", "Part") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        if (fresh)
        {
            return m;
        }
        m.Presets = longNames
            ? new[] { "Weekend baristas, winter", "Front of house, evenings", "Warehouse night crew 2026", "Kitchen whites, full set", "Head office reception desk",
                "Summer terrace staff", "Delivery drivers, rain", "Factory floor, safety" }
            : new[] { "Barista", "Front of house", "Kitchen", "Warehouse crew", "Office" };
        m.PresetCount = m.Presets.Length;
        m.Preset = 0;
        m.Name = m.Presets[0];
        m.CanCopy = m.CanDelete = m.CanSave = true;
        m.Parts = new[]
        {
            t("reallife_ui_bizops_wear_headaccessory", "Headwear"), t("reallife_ui_bizops_wear_torso", "Top"), t("reallife_ui_bizops_wear_legs", "Bottoms"),
            t("reallife_ui_bizops_wear_feet", "Shoes")
        };
        m.Part = 1;
        m.Kinds = new[] { t("reallife_ui_bizops_wear_torso", "Top"), t("reallife_ui_bizops_wear_torsoaccessory", "Over the top") };
        m.Kind = 0;
        m.Pieces = new UiUniformPiece[18];
        for (int i = 0; i < m.Pieces.Length; i++)
        {
            m.Pieces[i] = new UiUniformPiece { Chosen = i == 4, Sport = i == 9 || i == 13 };   // the game's own pictures of its pieces show in the game alone
        }
        m.PieceCount = m.Pieces.Length;
        string[] colours = { "1f2a44", "2e5e8c", "3f8fd2", "8fb8de", "f2f2f2", "c9c2b3", "6b4f3a", "2f6b4f", "8c2f39", "d9822b", "e8c547", "3a3a3a" };
        m.Colours = new UiUniformPiece[colours.Length];
        for (int i = 0; i < colours.Length; i++)
        {
            m.Colours[i] = new UiUniformPiece { Picture = "uniform:rgb:" + colours[i], Chosen = i == 1 };
        }
        m.ColourCount = m.Colours.Length;
        m.ColoursOpen = state == "uniforms_colours";
        return m;
    }

    private static string OpsWhole(int n) => n.ToString("#,0", CultureInfo.InvariantCulture);

    // Money as the game writes a price: dollars and cents.
    private static string OpsMoney(int cents) => "$" + (cents / 100.0).ToString("#,0.00", CultureInfo.InvariantCulture);
}
