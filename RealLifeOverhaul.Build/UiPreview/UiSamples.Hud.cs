using System;
using RealLifeOverhaul.Ui;

/// <summary>
/// The HUD's pieces over the running game, each with the data a player would see there on a Tuesday afternoon in
/// Midtown: the status bar walking, paused and fast-forwarding, at night with low needs; an info panel for a business and
/// a car; a box at a café's door and the hover form over an item; the shift, the job and the building as side panels;
/// the objectives with every group; a trade's timing gauge; the subway picker with greyed stations and in custody; an
/// activity before it starts and while it runs; the time skip veil; the city map's side panel; and the interior
/// designer's tool row. Plain English, as the owner reads the previews; the labels the game or the mod already has come
/// through their keys.
/// </summary>
internal static partial class UiSamples
{
    // ------------------------------------------------------------------------------------------------- the status bar

    /// <summary>The status bar on the HUD: walking at speed, paused, fast-forwarding, or at night with low needs and money going out.</summary>
    public static UiStatusModel HudBar(UiWords t, bool paused, bool fast, bool night)
    {
        var m = new UiStatusModel
        {
            Time = night ? "22:47" : "14:32", Day = night ? "Fri, Day 415" : "Tue, Day 412", DayWidest = "Wed, Day 888", Paused = paused, Speed = fast ? 3 : 1, PausedWords = t("reallife_ui_hud_paused", "Paused"),
            Season = t("reallife_season_name_autumn", "Autumn"), Weather = night ? UiIcon.CloudRain : UiIcon.CloudSun, Temperature = night ? "9°C" : "14°C",
            Place = night ? "1250 Avenue of the Americas" : "70 Fifth Avenue", District = night ? "Lower Manhattan" : "Midtown", Money = night ? "$2,415" : "$48,210",
            MoneyTrend = night ? -1 : 1, MoneyChange = night ? "-$385" : "+$1,240", MoneyChangeRises = !night,
            Energy = night ? 18 : 72, Hunger = night ? 12 : 48, Happiness = night ? 24 : 64, Hygiene = night ? 22 : 81, Stress = night ? 86 : 34, HomeNeed = night ? 30 : 70,
            Alerts = night ? 12 : 3, PillsPress = true,
        };
        // While walking the bar keeps the frame's app row: the phone's first home page, then All apps (the owner, item 11).
        UiStatusModel row = ShellBar(-1, false);
        m.Apps = row.Apps;
        m.AppTints = row.AppTints;
        m.AppBadges = row.AppBadges;
        m.AllApps = true;
        m.AllAppsBadge = row.AllAppsBadge;
        m.AppsPress = true;
        m.AllAppsLabel = t("reallife_ui_shell_all_apps", "All apps");
        return m;
    }

    // The bar under a screen of the HUD's that takes the keys (a picker, a card, the veil, the map): its pills are pictures there.
    private static UiStatusModel Over(UiStatusModel bar)
    {
        bar.PillsPress = false;
        return bar;
    }

    // The bar's sample in force while one HUD scene is laid out, as the game's bar would read then.
    private static void WithBar(UiStatusModel bar, Action lay)
    {
        Func<UiStatusModel> was = UiChrome.Bar;
        UiChrome.Bar = () => bar;
        try
        {
            lay();
        }
        finally
        {
            UiChrome.Bar = was;
        }
    }

    // The game's control hints while walking, as the HUD's bar names them while the keys lead.
    private static void Walking(UiHudModel m, UiWords t)
    {
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Back, Label = t("input_key_menu", "Menu") },
        };
        m.PromptCount = m.Prompts.Length;
    }

    /// <summary>A HUD scene: the model's pieces over the world with the bar's sample.</summary>
    public static void Hud(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiHudModel m, UiStatusModel bar)
    {
        if (m.PromptCount == 0)
        {
            Walking(m, t);
        }
        WithBar(bar, () => UiHud.Lay(s, m, f, dark, k));
    }

    /// <summary>The bar alone over the street: walking at speed, paused, fast-forwarding, or at night.</summary>
    public static void HudBarScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool paused, bool fast, bool night) =>
        Hud(s, f, dark, k, t, new UiHudModel(), HudBar(t, paused, fast, night));

    // ------------------------------------------------------------------------------------------------- info panels

    /// <summary>A business in the world as an info panel: Bloom Café, open, with its owner, takings and staff.</summary>
    public static UiInfoModel HudBusiness(UiWords t) => new UiInfoModel
    {
        Title = "Bloom Café", Icon = UiIcon.Coffee, Tabs = new[] { UiIcon.Store, UiIcon.Coffee, UiIcon.People }, Tab = 1,
        Status = "Open until 22:00", StatusIcon = UiIcon.Check, StatusTone = UiRole.None, Facts = "Coffee shop, 70 Fifth Avenue",
        Sections = new[]
        {
            new UiInfoSection
            {
                Label = "Business", Value = "Yours",
                Rows = new[]
                {
                    new UiInfoRow { Label = "Takings today", Value = "$1,862", Ink = UiRole.OkText }, new UiInfoRow { Label = "Customers today", Value = "143" },
                    new UiInfoRow { Label = "Seats", Value = "24" },
                }
            },
            new UiInfoSection
            {
                Label = "Staff", Value = "4 on shift", Link = true,
                Rows = new[] { new UiInfoRow { Label = "Wages a day", Value = "$612" }, new UiInfoRow { Label = "Next shift change", Value = "18:00" } }
            },
            new UiInfoSection
            {
                Label = "Building", Value = "70 Fifth Avenue", Link = true,
                Rows = new[] { new UiInfoRow { Label = "Rent a month", Value = "$9,400" }, new UiInfoRow { Label = "Landlord", Value = "Hudson Realty", Link = true } }
            },
        },
        Footer = new[] { UiIcon.Locate, UiIcon.Navigation, UiIcon.Phone }, FooterTints = new UiRole[0],
    };

    /// <summary>A car in the world as an info panel: the player's own, parked, with its fuel and its worth.</summary>
    public static UiInfoModel HudVehicle(UiWords t) => new UiInfoModel
    {
        Title = "Honza Mimo", Icon = UiIcon.Car, Tabs = new[] { UiIcon.Car, UiIcon.Person }, Tab = 0,
        Status = "Parked on West 24th Street", StatusIcon = UiIcon.MapPin, StatusTone = UiRole.None, Facts = "Yours since Day 380, hatchback",
        Sections = new[]
        {
            new UiInfoSection
            {
                Label = "Vehicle", Value = "In good order", Rows = new[]
                {
                    new UiInfoRow { Label = "Plate", Value = "NYB 4417" }, new UiInfoRow { Label = "Bought for", Value = "$14,900" },
                    new UiInfoRow { Label = "Worth now", Value = "$11,300" },
                }
            },
            new UiInfoSection
            {
                Label = "Costs", Value = "$214 a week", Rows = new[] { new UiInfoRow { Label = "Fuel", Value = "$118 a week" }, new UiInfoRow { Label = "Insurance", Value = "$96 a week" }, new UiInfoRow { Label = "Parking", Value = "Street, free" } }
            },
        },
        Footer = new[] { UiIcon.Locate, UiIcon.Navigation }, FooterTints = new UiRole[0],
    };

    /// <summary>The HUD with a thing's info panel docked at the top left: a business, or a car.</summary>
    public static void HudInfo(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool vehicle, bool night = false)
    {
        var m = new UiHudModel
        {
            Info = vehicle ? HudVehicle(t) : HudBusiness(t),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("common_close", "Close") },
            },
        };
        m.PromptCount = m.Prompts.Length;
        Hud(s, f, dark, k, t, m, HudBar(t, false, false, night));
    }

    // ------------------------------------------------------------------------------------------------- the world prompt

    /// <summary>The box at Bloom Café's door: the address and what is inside, four actions with the game's keys, one greyed.</summary>
    public static UiWorldPromptModel HudDoor(UiWords t, IUiKeys k) => new UiWorldPromptModel
    {
        Title = "70 Fifth Avenue", Icon = UiIcon.Store,
        Rows = new[] { new UiInfoRow { Label = "Bloom Café", Value = "Open until 22:00", Ink = UiRole.OkText }, new UiInfoRow { Label = "Floors", Value = "Ground and 1st" } },
        Actions = new[]
        {
            new UiPromptAction { Label = "Go inside", Key = GameKey(k, "F", UiIcon.PadX), Main = true },
            new UiPromptAction { Label = "Look at the building", Key = GameKey(k, "P", UiIcon.PadY) },
            new UiPromptAction { Label = "Ring the doorbell", Key = GameKey(k, "O", UiIcon.PadRb) },
            new UiPromptAction { Label = "Rent this space", Key = GameKey(k, "Enter", UiIcon.PadA), Disabled = true, Reason = "Taken" },
        },
    };

    /// <summary>The hover form over an item on a shelf: its name and its price, no actions.</summary>
    public static UiWorldPromptModel HudItem(UiWords t) => new UiWorldPromptModel
    {
        Title = "Espresso machine", Icon = UiIcon.Coffee, Hover = true, FigureLabel = "Price", Figure = "$1,450",
    };

    private static UiGameKey GameKey(IUiKeys k, string name, UiIcon pad) => k != null && k.Pad != UiPad.None ? new UiGameKey { Glyph = pad } : new UiGameKey { Name = name };

    /// <summary>The HUD with a box at a café's door, its point right of centre on the street; or the hover form indoors.</summary>
    public static void HudPrompt(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool hover, bool night = false)
    {
        var m = new UiHudModel { Prompt = hover ? HudItem(t) : HudDoor(t, k), PointX = f.Width * (hover ? 0.42f : 0.62f), PointY = f.Height * (hover ? 0.5f : 0.58f) };
        Hud(s, f, dark, k, t, m, HudBar(t, false, false, night));
    }

    // ------------------------------------------------------------------------------------------------- side panels and objectives

    /// <summary>The shift at the café: its hours as a meter, where and until when, Clock out.</summary>
    public static UiSidePanelModel HudShift(UiWords t, bool collapsed) => new UiSidePanelModel
    {
        Title = "Barista shift", Icon = UiIcon.Briefcase, Collapsed = collapsed,
        Rows = new[]
        {
            new UiInfoRow { Label = "Until", Value = "18:00" },
            new UiInfoRow { Label = "Pay this shift", Value = "$96", Ink = UiRole.OkText },
        },
        HasProgress = true, Progress = new UiProgressModel { Label = "Hours worked", Value = "2 h 32 min of 6 h", Share = 0.42f },
        Actions = new[] { new UiActionModel { Label = "Clock out" } },
    };

    /// <summary>The job the player holds: where and when, Set destination and Quit.</summary>
    public static UiSidePanelModel HudJob(UiWords t, bool collapsed) => new UiSidePanelModel
    {
        Title = "Current job", Icon = UiIcon.Handshake, Collapsed = collapsed,
        Rows = new[]
        {
            new UiInfoRow { Label = "Address", Value = "70 Fifth Avenue", Link = true }, new UiInfoRow { Label = "Tomorrow", Value = "Day off" },
            new UiInfoRow { Label = "Hours a week", Value = "30" },
        },
        Actions = new[] { new UiActionModel { Label = "Quit", Kind = UiButtonKind.Destructive } },   // at work there: no way to set to where you stand
    };

    /// <summary>The building the player stands in: open or closed, its parking.</summary>
    public static UiSidePanelModel HudBuilding(UiWords t, bool collapsed) => new UiSidePanelModel
    {
        Title = "Bloom Café", Icon = UiIcon.Store, Collapsed = collapsed,
        Rows = new[] { new UiInfoRow { Label = "State", Value = "Open", Ink = UiRole.OkText }, new UiInfoRow { Label = "Parking", Value = "3 of 4 spaces free" } },
        Actions = new[] { new UiActionModel { Label = "Close for today" }, new UiActionModel { Label = "Designer", Icon = UiIcon.PaintBrush } },
    };

    /// <summary>The objectives with every group: to-dos, a goal, a quest, a delivery mission, the workout plan and a construction.</summary>
    public static UiObjectivesModel HudGoals(UiWords t, bool collapsed, bool empty)
    {
        var m = new UiObjectivesModel
        {
            Title = t("tutorial_objectives", "Objectives"), Collapsed = collapsed,
            EmptyTitle = "Nothing to do yet", EmptyBody = "Goals, deliveries and what your businesses need show here as they come.",
        };
        if (empty)
        {
            return m;
        }
        m.Groups = new[]
        {
            new UiObjectiveGroup
            {
                Title = "To do", Icon = UiIcon.ClipboardText, Lines = new[]
                {
                    new UiObjectiveLine { Text = "Bloom Café is out of coffee beans", Locate = true },
                    new UiObjectiveLine { Text = "Pay the rent for 70 Fifth Avenue by Day 414", Progress = "$9,400" },
                }
            },
            new UiObjectiveGroup
            {
                Title = "Goal", Icon = UiIcon.Trophy, Lines = new[] { new UiObjectiveLine { Text = "Earn $100,000 in a week", Progress = "$61,280" } }
            },
            new UiObjectiveGroup
            {
                Title = "Uncle Fred", Icon = UiIcon.Lightbulb, Lines = new[]
                {
                    new UiObjectiveLine { Text = "Hire a second barista", Done = true }, new UiObjectiveLine { Text = "Buy a delivery van at the dealer", Locate = true },
                }
            },
            new UiObjectiveGroup
            {
                Title = "Delivery", Icon = UiIcon.Truck, Lines = new[] { new UiObjectiveLine { Text = "Deliver 12 boxes to Hudson Street", Progress = "4 of 12", Locate = true } }
            },
            new UiObjectiveGroup { Title = "Workout plan", Icon = UiIcon.Heartbeat, Collapsed = true, Lines = new[] { new UiObjectiveLine { Text = "Run for 30 minutes" } } },
            new UiObjectiveGroup
            {
                Title = "Construction", Icon = UiIcon.Hammer, Lines = new[] { new UiObjectiveLine { Text = "Walls for 18 Bleecker Street", Progress = "2 of 3 days" } }
            },
        };
        return m;
    }

    /// <summary>The HUD at work: the shift and the job open, the building folded, the objectives at the left.</summary>
    public static void HudSide(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool collapsed)
    {
        var m = new UiHudModel
        {
            Side = new[] { HudShift(t, collapsed), HudJob(t, collapsed), HudBuilding(t, true) }, Objectives = collapsed ? HudGoals(t, true, false) : null,
        };
        Hud(s, f, dark, k, t, m, HudBar(t, false, false, collapsed));
    }

    /// <summary>The HUD with the objectives open with every group, or with nothing to do yet.</summary>
    public static void HudObjectives(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool empty, bool night = false) =>
        Hud(s, f, dark, k, t, new UiHudModel { Objectives = HudGoals(t, false, empty) }, HudBar(t, false, false, night));

    /// <summary>The HUD on a trade job: the timing gauge with its zone and marker, and a delivery's meter running late.</summary>
    public static void HudMeters(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var timing = new UiSidePanelModel
        {
            Title = "Plastering", Icon = UiIcon.Wrench,
            Rows = new[] { new UiInfoRow { Label = "Room", Value = "Kitchen, 18 Bleecker Street" }, new UiInfoRow { Label = "Pay", Value = "$140" } },
            HasTiming = true,
            Timing = new UiTimingModel { Key = GameKey(k, "F", UiIcon.PadX), Label = "Press in the green", Value = "Hits 2 of 3", ZoneFrom = 0.58f, ZoneTo = 0.74f, Marker = 0.41f },
        };
        var delivery = new UiSidePanelModel
        {
            Title = "Delivery to Hudson Street", Icon = UiIcon.Truck,
            Rows = new[] { new UiInfoRow { Label = "Boxes", Value = "4 of 12" }, new UiInfoRow { Label = "Due", Value = "15:00", Ink = UiRole.WarnText } },
            HasProgress = true, Progress = new UiProgressModel { Label = "Time left", Value = "28 min", Share = 0.22f, Tone = UiRole.Warn },
        };
        Hud(s, f, dark, k, t, new UiHudModel { Side = new[] { timing, delivery } }, HudBar(t, false, false, false));
    }

    // ------------------------------------------------------------------------------------------------- the picker

    /// <summary>The subway from Fifth Avenue: eight stations, two greyed with their reasons; or in custody, the court's choice.</summary>
    public static UiPickerModel HudPicker(UiWords t, bool locked, bool empty)
    {
        var m = new UiPickerModel
        {
            Title = locked ? "Choose your lawyer" : "Take the subway", Icon = locked ? UiIcon.Scales : UiIcon.Train, Locked = locked, PerPage = 8, EmptyIcon = UiIcon.Train,
            Context = locked
                ? new[] { new UiInfoRow { Label = "Hearing", Value = "Tomorrow, 09:00" }, new UiInfoRow { Label = "Charge", Value = "Speeding, third time" } }
                : new[] { new UiInfoRow { Label = "From", Value = "23rd Street station" }, new UiInfoRow { Label = "Fare", Value = "$2.75 a ride" } },
            EmptyTitle = "No station is open", EmptyBody = "The subway runs from 05:00. Walk, take a taxi or wait for the first train.",
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = locked ? t("input_key_menu", "Menu") : t("reallife_ui_prompt_back", "Back") },
            },
        };
        m.PromptCount = m.Prompts.Length;
        if (empty)
        {
            return m;
        }
        m.Entries = locked
            ? new[]
            {
                Lawyer("Dana Ruiz", 30f, "Public defender", "Free", null), Lawyer("Min-jun Rhee", 200f, "Traffic cases", "$1,200", null),
                Lawyer("Grace Carver", 320f, "Wins most hearings", "$3,400", null), Lawyer("Owen Vance", 120f, "Partner at Carver Law", null, "Booked until Day 420"),
            }
            : new[]
            {
                Line(1, "Union Square", "3 min"), Line(2, "Herald Square", "4 min"), Line(1, "Times Square", "7 min"), Shut(3, "Grand Central", "Closed for works until Day 414"),
                Line(2, "Bleecker Street", "9 min"), Line(1, "Canal Street", "12 min"), Shut(3, "Brooklyn Bridge", "Closed for repairs"), Line(2, "Wall Street", "16 min"),
                Line(1, "South Ferry", "19 min"), Line(3, "Harlem 125th Street", "22 min"),
            };
        return m;
    }

    // A station by its line's number in the line's own colour, its ride's minutes as the figure that decides.
    private static UiPickEntry Line(int line, string name, string minutes) =>
        new UiPickEntry { Icon = UiIcon.Train, Mark = line.ToString(System.Globalization.CultureInfo.InvariantCulture), MarkTone = UiTokens.Chart(line - 1), Name = name, Figure = minutes };

    // A lawyer by their face, what they do and their fee, or why they cannot take the case.
    private static UiPickEntry Lawyer(string name, float hue, string detail, string fee, string reason) =>
        new UiPickEntry { Icon = UiIcon.Scales, Face = name, Hue = hue, Name = name, Detail = detail, Figure = fee, Disabled = reason != null, Reason = reason };

    private static UiPickEntry Shut(int line, string name, string reason)
    {
        UiPickEntry e = Line(line, name, null);
        e.Disabled = true;
        e.Reason = reason;
        return e;
    }

    /// <summary>The picker over the street with the HUD's bar.</summary>
    public static void HudPickerScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool locked, bool empty) =>
        WithBar(Over(HudBar(t, false, false, locked || empty)), () => UiPicker.Lay(s, HudPicker(t, locked, empty), f, dark, k));

    // ------------------------------------------------------------------------------------------------- the activity

    /// <summary>A shift at the café in each of its states: before it starts, walking there, waiting, running, finished.</summary>
    public static UiActivityModel HudActivity(UiWords t, UiActivityState state, IUiKeys k)
    {
        var m = new UiActivityModel
        {
            State = state, Headline = "Work a shift at Bloom Café", Icon = UiIcon.Coffee,
            Lines = new[]
            {
                new UiInfoRow { Label = "Pay an hour", Value = "$16", Ink = UiRole.OkText }, new UiInfoRow { Label = "Energy an hour", Value = "-6", Ink = UiRole.WarnText },
                new UiInfoRow { Label = "Ends at", Value = "18:32" },
            },
            HasDuration = state == UiActivityState.NotStarted, DurationLabel = "Hours", DurationValue = "4 hours", DurationShare = 0.5f,
            HasProgress = state == UiActivityState.Running || state == UiActivityState.Started, Progress = new UiProgressModel { Share = 0.01f },
            Clock = state == UiActivityState.Finished ? "18:32" : state == UiActivityState.Running || state == UiActivityState.Started ? "14:34" : "14:32",
            StateWords = state == UiActivityState.Moving ? "On your way, 2 min" : state == UiActivityState.Waiting ? "Waiting for the doors to open at 12:00"
                : state == UiActivityState.Finished ? "Done: $64 earned" : "3 hours, 58 minutes remaining",
            CanSkip = true, SkipLabel = t("playeractivityui_timemachine", "Skip time"), SkipKey = GameKey(k, "O", UiIcon.PadRb),
            CanFast = true, Fast = state == UiActivityState.Running, FastLabel = t("playeractivityui_fast_forward", "Fast forward"), FastKey = GameKey(k, "P", UiIcon.PadY),
            CanCancel = true, CancelLabel = t("common_cancel", "Cancel"), StartLabel = "Start",
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Back, Label = t("common_cancel", "Cancel") },
            },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The activity before it starts as its card, or as the strip above the bar in another state.</summary>
    public static void HudActivityScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiActivityState state)
    {
        UiActivityModel a = HudActivity(t, state, k);
        if (state == UiActivityState.NotStarted)
        {
            WithBar(Over(HudBar(t, false, false, false)), () => UiActivity.LayCard(s, a, f, dark, k));
            return;
        }
        UiStatusModel bar = HudBar(t, false, state == UiActivityState.Running, false);
        bar.Time = a.Clock;   // the bar reads the clock the strip does
        Hud(s, f, dark, k, t, new UiHudModel { Activity = a, MoneyChange = false }, bar);
    }

    // ------------------------------------------------------------------------------------------------- the time skip

    /// <summary>Sleeping until the shift: the clock running on, the goal, Cancel.</summary>
    public static UiTimeSkipModel HudSkip(UiWords t, bool locked) => new UiTimeSkipModel
    {
        Title = t("playeractivityui_timemachine", "Skip time"), Clock = "03:40", Day = "Wed, Day 413", Goal = "Until 07:00, when your shift starts", Share = 0.58f, CanCancel = !locked,
        CancelLabel = t("common_cancel", "Cancel"),
        Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = t("common_cancel", "Cancel") } }, PromptCount = 1,
    };

    public static void HudSkipScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t) =>
        WithBar(Skipping(HudBar(t, false, true, true)), () => UiTimeSkip.Lay(s, HudSkip(t, false), f, dark, k));

    // The bar under the veil reads the clock the veil counts.
    private static UiStatusModel Skipping(UiStatusModel bar)
    {
        bar.Time = "03:40";
        bar.Day = "Wed, Day 413";
        return Over(bar);
    }

    // ------------------------------------------------------------------------------------------------- the city map

    /// <summary>The city map's side panel: the places' filters, the rent legend and a picked café.</summary>
    public static UiMapPanelModel HudMap(UiWords t, bool picked) => new UiMapPanelModel
    {
        Title = "Rent", Icon = UiIcon.Coin, Views = new[] { UiIcon.Coin, UiIcon.Store, UiIcon.Key, UiIcon.Factory, UiIcon.People, UiIcon.Car }, View = 0,
        SearchPlaceholder = "Search for a place or an address", FiltersTitle = "Show on the map",
        Filters = new[]
        {
            new UiMapFilter { Icon = UiIcon.Train, Name = "Subway stations", On = true, Locate = true },
            new UiMapFilter { Icon = UiIcon.Store, Name = "Your businesses", On = true, Locate = true },
            new UiMapFilter { Icon = UiIcon.Key, Name = "Homes for rent", On = false, Locate = true },
            new UiMapFilter { Icon = UiIcon.ShoppingCart, Name = "Wholesale stores", On = true, Locate = true },
            new UiMapFilter { Icon = UiIcon.Car, Name = "Car dealers", On = false, Locate = false },
            new UiMapFilter { Icon = UiIcon.Bank, Name = "Banks", On = false, Locate = true },
        },
        LegendTitle = "Map legend", ScaleLabel = "Rent by block, a month", ScaleLow = "$2,000", ScaleHigh = "$40,000",
        Picked = picked, PickedIcon = UiIcon.Coffee, PickedTitle = "Bloom Café",
        PickedRows = new[] { new UiInfoRow { Label = "Address", Value = "70 Fifth Avenue" }, new UiInfoRow { Label = "District", Value = "Midtown" } },
        DestinationLabel = t("common_setdestination", "Set destination"),
        Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("common_close", "Close") },
        },
        PromptCount = 3,
    };

    public static void HudMapScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool picked) =>
        WithBar(Over(HudBar(t, false, false, false)), () => UiMapPanel.Lay(s, HudMap(t, picked), f, dark, k));

    // ------------------------------------------------------------------------------------------------- the tool row

    /// <summary>The interior designer's tools with the furniture in hand: eight pieces to choose from, one in hand, one not sold here.</summary>
    public static UiToolRowModel HudToolRow(UiWords t, bool open) => new UiToolRowModel
    {
        Tools = new[]
        {
            new UiTool { Icon = UiIcon.Sofa, Name = "Furniture" }, new UiTool { Icon = UiIcon.PaintBrush, Name = "Walls" }, new UiTool { Icon = UiIcon.Trash, Name = "Sell" },
        },
        Tool = open ? 0 : -1, Choice = 1,
        Choices = new[]
        {
            new UiToolChoice { Icon = UiIcon.Camera, Name = "Security camera", Figure = "$340" }, new UiToolChoice { Icon = UiIcon.Coffee, Name = "Espresso machine", Figure = "$1,450" },
            new UiToolChoice { Icon = UiIcon.Image, Name = "Framed print", Figure = "$60" }, new UiToolChoice { Icon = UiIcon.Cpu, Name = "Office computer", Figure = "$980" },
            new UiToolChoice { Icon = UiIcon.MusicNotes, Name = "Ceiling speaker", Figure = "$210", Disabled = true, Reason = "Not sold here" },
            new UiToolChoice { Icon = UiIcon.GameController, Name = "Arcade cabinet", Figure = "$2,400" },
        },
    };

    /// <summary>The designer's tool row in the bar, with the floors' panel risen above it or none in hand.</summary>
    public static void HudTools(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool open)
    {
        UiStatusModel bar = HudBar(t, false, false, false);
        bar.Apps = null;   // the tool row stands where the apps were
        bar.AllApps = false;
        bar.MoneyChange = null;
        var m = new UiHudModel
        {
            Tools = HudToolRow(t, open),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Back, Label = t("common_close", "Close") },
            },
        };
        m.PromptCount = m.Prompts.Length;
        Hud(s, f, dark, k, t, m, bar);
    }
}
