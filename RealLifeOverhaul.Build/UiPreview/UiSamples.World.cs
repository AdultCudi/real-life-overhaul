using RealLifeOverhaul.Ui;

/// <summary>
/// The world's pieces over the running game, each with the words the game and the mod write there: a building's panel
/// docked at the top left (a coffee shop rented by the player, a rival's noodle bar, an empty shop to rent, its owners'
/// tab) and its card at the door before it is picked; the box at a door with four floors to go to, at the game's own door
/// at night with the business closed, at a lift and at a bus stop with two routes; the list at a police front desk with
/// eight things to ask, the holding cell's list locked, a list with nothing in it and one over two pages; the job offer,
/// and the same once the job is held. The game's own words come through their keys (the checks read them in every
/// language, the longest previews in German); the words a caller gives a list are its own and stay as they are.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>Which building the panel samples: the player's rented shop, a rival's, an empty one to rent, or its owners' tab.</summary>
    public enum WorldPlace
    {
        Rented,
        Rival,
        ForRent,
        Owners
    }

    // The status bar's words under a list or an offer (BarWords, UiSamples.HudScreens.cs) are the HUD's, not the screen's:
    // never counted as the screen's.

    // ------------------------------------------------------------------------------------------------- a building's panel

    /// <summary>A building's panel as the kit reads it from the game's box: the address on the strip, the business at its head.</summary>
    public static UiInfoModel WorldBuilding(UiWords t, WorldPlace place)
    {
        bool rival = place == WorldPlace.Rival, rent = place == WorldPlace.ForRent;
        string name = rival ? "Lucky Noodle" : rent ? null : "Bloom Café";
        string kind = rival ? t("ba:businesstype_fastfoodrestaurant", "Fast Food Restaurant") : rent ? t("ba:buildingtype_retail", "Retail") : t("ba:businesstype_coffeeshop", "Coffee Shop");
        string district = rival ? "Lower Manhattan" : "Midtown";
        string state = rent
            ? t("common_available_for_rent", "Available for rent")
            : rival ? t("common_closed", "Closed") + ", " + t("buildingresume_closed_opening_in_hours", "Opening in {hours} hours ({time})")
                .Replace("{hours}", "2").Replace("{time}", "09:00").Replace("<br>", string.Empty)
            : t("common_open", "Open") + ", " + t("buildingresume_open_closing_in_hours", "Closing in {hours} hours ({time})")
                .Replace("{hours}", "3").Replace("{time}", "22:00").Replace("<br>", string.Empty);
        string cityOwner = t("building_rival_owner_description", "Building owned by {rivalName}").Replace("{rivalName}", t("the_city_of_new_york", "The City of New York"));
        string businessOwner = t("buildingresume_rented_by_you", "Rented by you");
        string owner = t("reallife_ui_world_owner_one", "Owner");
        UiInfoRow buildingOwner = rent ? new UiInfoRow { Label = cityOwner } : new UiInfoRow { Label = owner, Value = "Hudson Realty", Link = true };
        string buildingState = rent ? t("reallife_ui_world_state_for_rent", "For rent") : rival ? t("reallife_ui_world_state_rival", "A rival's")
            : t("buildingresume_rented_by_you", "Rented by you");
        string businessState = rival ? t("reallife_ui_world_state_rival", "A rival's") : t("reallife_ui_world_state_yours", "Yours");
        UiInfoRow customers = new UiInfoRow
        {
            Label = t("reallife_ui_world_customers", "Customers an hour"), Meter = true, Fill = rival ? 0f : 31f / 48f,
            Value = t("reallife_ui_world_customers_of", "{count} of {most}").Replace("{count}", rival ? "0" : "31").Replace("{most}", rival ? "64" : "48"),
        };
        var m = new UiInfoModel
        {
            Title = rival ? "1250 Avenue of the Americas" : rent ? "72 Fifth Avenue" : "70 Fifth Avenue", Icon = UiIcon.Building,
            Tabs = rent ? new[] { UiIcon.Building, UiIcon.Person } : new[] { UiIcon.Building, UiIcon.Store, UiIcon.Person }, TabTints = new UiRole[0],
            Tab = place == WorldPlace.Owners ? 2 : 0, Lead = name, Mark = name == null ? null : rival ? "LN" : "BC", MarkTone = UiTokens.Chart(rival ? 3 : 1),
            Facts = kind + ", " + district, Status = state, StatusIcon = UiIcon.Clock, StatusTone = rent ? UiRole.Ok : rival ? UiRole.Danger : UiRole.Ok,
            FooterLabel = t("buildingresume_open_in_bizman", "Open in BizMan"), FooterTints = new UiRole[0],
            Footer = place == WorldPlace.Rented || place == WorldPlace.Owners ? new[] { UiIcon.Navigation, UiIcon.PaintBrush, UiIcon.Locate }
                : new[] { UiIcon.Navigation, UiIcon.Locate },
        };
        var building = new[]
        {
            buildingOwner, new UiInfoRow { Label = t("reallife_ui_world_neighbourhood", "Neighbourhood"), Value = district, Link = true },
            new UiInfoRow { Label = t("reallife_ui_world_area", "Floor area"), Value = rival ? "310 m²" : "140 m²" },
            new UiInfoRow { Label = t("reallife_ui_world_capacity", "Customer capacity"), Value = rival ? "64" : "48" },
        };
        if (place == WorldPlace.Owners)
        {
            m.Sections = new[]
            {
                new UiInfoSection { Label = t("reallife_ui_world_owner", "Owners"), Rows = new[] { buildingOwner, new UiInfoRow { Label = businessOwner } } },
            };
            return m;
        }
        if (rent)
        {
            m.Sections = new[]
            {
                new UiInfoSection
                {
                    Label = t("reallife_ui_world_building", "Building"), Value = buildingState,
                    Rows = new[]
                    {
                        building[0], building[1], building[2], building[3], new UiInfoRow { Label = t("reallife_ui_world_kind", "Kind"), Value = kind },
                        new UiInfoRow { Label = t("reallife_ui_world_traffic", "Traffic index"), Value = "8" },
                    }
                },
            };
            return m;
        }
        m.Sections = new[]
        {
            new UiInfoSection { Label = t("reallife_ui_world_building", "Building"), Value = buildingState, Rows = building },
            new UiInfoSection
            {
                Label = t("reallife_ui_world_business", "Business"), Value = businessState,
                Rows = rival
                    ? new[]
                    {
                        new UiInfoRow { Label = t("reallife_ui_world_kind", "Kind"), Value = kind }, new UiInfoRow { Label = t("reallife_ui_world_open", "Hours"), Value = state },
                        new UiInfoRow { Label = t("reallife_ui_world_traffic", "Traffic index"), Value = "9" }, customers,
                        new UiInfoRow { Label = owner, Value = "Viktor Hale", Link = true },
                    }
                    : new[]
                    {
                        new UiInfoRow { Label = t("reallife_ui_world_kind", "Kind"), Value = kind }, new UiInfoRow { Label = t("reallife_ui_world_open", "Hours"), Value = state },
                        new UiInfoRow { Label = t("reallife_ui_world_traffic", "Traffic index"), Value = "7" }, customers,
                    },
            },
        };
        return m;
    }

    /// <summary>The building picked: its panel in the HUD's left column over the street, the HUD's bar under it.</summary>
    public static void WorldBuildingScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, WorldPlace place, bool night = false)
    {
        var m = new UiHudModel
        {
            Info = WorldBuilding(t, place),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("common_close", "Close") },
            },
        };
        m.PromptCount = m.Prompts.Length;
        Hud(s, f, dark, k, t, m, HudBar(t, false, false, night));
    }

    /// <summary>The card at a building's door while the pointer rests on it: its name, kind and place, open or not, and the game's hints.</summary>
    public static UiPromptCardModel WorldBuildingCard(UiWords t) => new UiPromptCardModel
    {
        Title = "Bloom Café", Sub = t("ba:businesstype_coffeeshop", "Coffee Shop") + ", Midtown", State = t("common_open", "Open"), StateTone = UiRole.OkText, Icon = UiIcon.Store,
        Actions = new[]
        {
            new UiPromptAction { Label = t("buildingresume_left_click", "Left click to enter"), Hint = true },
            new UiPromptAction { Label = t("buildingresume_right_click", "Right click for options"), Hint = true },
        },
        ActionCount = 2,
    };

    // ------------------------------------------------------------------------------------------------- the box at a door

    /// <summary>The door of a tower with floors of its own: three of its floors and the next page, on the game's four keys.</summary>
    public static UiPromptCardModel WorldDoor(UiWords t, IUiKeys k) => new UiPromptCardModel
    {
        Title = "Union Tower", Sub = t("reallife_floors_host_door", "Floors: {count}").Replace("{count}", "14"), State = t("common_open", "Open"), StateTone = UiRole.OkText,
        Icon = UiIcon.Up,
        Actions = new[]
        {
            new UiPromptAction { Label = Floor(t, 4, "Delta Law"), Key = GameKey(k, "F", UiIcon.PadX), Main = true },
            new UiPromptAction { Label = Floor(t, 9, "Studio North"), Key = GameKey(k, "P", UiIcon.PadY) },
            new UiPromptAction { Label = Floor(t, 12, "Harbour Clinic"), Key = GameKey(k, "O", UiIcon.PadRb), Disabled = true },
            new UiPromptAction { Label = t("reallife_campus_floor_more", "More floors"), Key = GameKey(k, "Enter", UiIcon.PadA) },
        },
        ActionCount = 4,
    };

    private static string Floor(UiWords t, int floor, string place) =>
        t("reallife_floors_floor", "Floor {floor}: {place}").Replace("{floor}", floor.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{place}", place);

    /// <summary>The game's own box at a business's door at night: closed, so the way in is greyed.</summary>
    public static UiPromptCardModel WorldGameDoor(UiWords t, IUiKeys k) => new UiPromptCardModel
    {
        Title = "Bloom Café", Icon = UiIcon.Building,
        Sub = t("reallife_ui_world_door_opens", "{kind}, opens at {time}").Replace("{kind}", t("ba:businesstype_coffeeshop", "Coffee Shop")).Replace("{time}", "07:00"),
        Actions = new[]
        {
            new UiPromptAction { Label = t("common_enter_building", "Enter building"), Key = GameKey(k, "F", UiIcon.PadX), Main = true, Disabled = true },
            new UiPromptAction { Label = t("open_in_bizman", "Open in BizMan"), Key = GameKey(k, "P", UiIcon.PadY) },
        },
        ActionCount = 2,
    };

    /// <summary>The lift of a tower with floors of its own, on its ninth floor: the others it goes to.</summary>
    public static UiPromptCardModel WorldLift(UiWords t, IUiKeys k) => new UiPromptCardModel
    {
        Title = "Union Tower", Sub = t("reallife_floors_lift_here", "You are on floor {floor}, {place}").Replace("{floor}", "9").Replace("{place}", "Studio North"), Icon = UiIcon.Up,
        Actions = new[]
        {
            new UiPromptAction { Label = t("reallife_floors_lift_up", "Go up to floor {floor}").Replace("{floor}", "11"), Key = GameKey(k, "F", UiIcon.PadX), Main = true },
            new UiPromptAction { Label = Floor(t, 1, "Lobby"), Key = GameKey(k, "P", UiIcon.PadY) },
            new UiPromptAction { Label = Floor(t, 4, "Delta Law"), Key = GameKey(k, "O", UiIcon.PadRb) },
        },
        ActionCount = 3,
    };

    /// <summary>A coffee cart's counter: a coffee to buy on the game's interact key, and a chat with the vendor on the next.</summary>
    public static UiPromptCardModel WorldCart(UiWords t, IUiKeys k) => new UiPromptCardModel
    {
        Title = t("reallife_city_cart_coffee", "Coffee cart"), Icon = UiIcon.Store,
        Actions = new[]
        {
            new UiPromptAction { Label = t("reallife_city_cart_buy", "Buy a coffee, {price}").Replace("{price}", "$4.00"), Key = GameKey(k, "F", UiIcon.PadX), Main = true },
            new UiPromptAction { Label = t("reallife_city_cart_chat", "Have a chat"), Key = GameKey(k, "P", UiIcon.PadY) },
        },
        ActionCount = 2,
    };

    /// <summary>A bus stop served by two routes: each next bus on its own line, the way to board.</summary>
    public static UiPromptCardModel WorldBusStop(UiWords t, IUiKeys k)
    {
        string minutes = t("reallife_transit_status_minutes", "{minutes} min");
        string row = t("reallife_transit_panel_row", "{route} to {to}: {status}");
        return new UiPromptCardModel
        {
            Title = t("reallife_transit_stop_title", "Bus stop: {name}").Replace("{name}", "Broadway and 45th"),
            Sub = row.Replace("{route}", "M104").Replace("{to}", "Harlem").Replace("{status}", minutes.Replace("{minutes}", "4")) + "\n"
                  + row.Replace("{route}", "M7").Replace("{to}", "South Ferry").Replace("{status}", minutes.Replace("{minutes}", "11")),
            State = minutes.Replace("{minutes}", "4"), StateTone = UiRole.Text2, Icon = UiIcon.Train,
            Actions = new[]
            {
                new UiPromptAction { Label = t("reallife_transit_button_choose", "Choose a stop"), Key = GameKey(k, "F", UiIcon.PadX), Main = true },
                new UiPromptAction { Label = t("reallife_transit_button_sit", "Sit down"), Key = GameKey(k, "P", UiIcon.PadY) },
            },
            ActionCount = 2,
        };
    }

    /// <summary>A box over the street beside its point (the door the game projected), the HUD's bar under it.</summary>
    public static void WorldPromptScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiPromptCardModel card, float pointX, float pointY, bool night = false)
    {
        var m = new UiHudModel();
        Walking(m, t);
        WithBar(HudBar(t, false, false, night), () =>
        {
            UiHud.Lay(s, m, f, dark, k);
            float w = UiPromptCard.Width(card), h = UiPromptCard.Height(card);
            UiHudRules.Beside(f.Width * pointX, f.Height * pointY, true, w, h, f.Width, f.BandY, 0f, 0f, 0f, 0f, out float x, out float y);
            UiPromptCard.Lay(s, UiAt.Screen, card, UiDraw.Snap(x), UiDraw.Snap(y));
        });
    }

    // ------------------------------------------------------------------------------------------------- the lists

    /// <summary>The police front desk's list: the sergeant's greeting, eight things to ask, one greyed.</summary>
    public static UiPickerModel WorldDesk(UiWords t, bool twoPages)
    {
        var m = new UiPickerModel
        {
            Title = "FRONT DESK", Icon = UiIcon.ShieldCheck, EmptyIcon = UiIcon.ShieldCheck, Numbered = true, PerPage = 8,
            Said = "Desk sergeant: \"Midtown North Precinct. What can I do for you?\"",
            PageWords = t("reallife_ui_world_pick_page", "{page} of {pages}").Replace("{page}", "{0}").Replace("{pages}", "{1}"),
            EmptyTitle = t("reallife_ui_world_pick_empty", "Nothing to choose"), EmptyBody = t("reallife_ui_world_pick_empty_body", "There is nothing here to choose right now."),
            EmptyAction = t("common_close", "Close"),
        };
        Prompts(m, t, false);
        Foot(m, t);
        m.Entries = new[]
        {
            Desk("Report a crime or an accident"), Desk("Pay your outstanding fines: $420"), Desk("Ask about your record"),
            Shut("Collect a car from the pound (none of yours is held)"), Desk("Apply for a licence or permit"), Desk("Ask for a police report on the crash on Fifth Avenue: $25"),
            Desk("Ask about visiting hours"), Desk("Leave"),
        };
        if (twoPages)
        {
            m.Entries = new[]
            {
                m.Entries[0], m.Entries[1], m.Entries[2], m.Entries[3], m.Entries[4], m.Entries[5], m.Entries[6], Desk("Hand yourself in (half the time)"),
                Desk("Ask about the impound fees"), Desk("Ask where the nearest court is"), m.Entries[7],
            };
        }
        return m;
    }

    // The world's lists as the screen sets them: each entry on its surface, the first chosen at rest, F Choose at the foot.
    private static void Foot(UiPickerModel m, UiWords t)
    {
        m.Surfaces = true;
        m.Chosen = 0;
        m.Choose = t("reallife_ui_world_pick_choose", "Choose");
    }

    private static UiPickEntry Desk(string name) => new UiPickEntry { Icon = UiIcon.ShieldCheck, Name = name };

    private static UiPickEntry Shut(string name) => new UiPickEntry { Icon = UiIcon.ShieldCheck, Name = name, Disabled = true };

    // The list's prompts: move, choose, and Back (the game's menu while the list keeps the player).
    private static void Prompts(UiPickerModel m, UiWords t, bool locked)
    {
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = locked ? t("input_key_menu", "Menu") : t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
    }

    /// <summary>
    /// The holding cell's list, locked: the clock and the sentence, the charge sheet by its heading (whole once P reads the
    /// file, <paramref name="file"/>), the ways to go on with Sit it out chosen, F Choose and P at the foot.
    /// </summary>
    public static UiPickerModel WorldCustody(UiWords t, bool file = false)
    {
        var m = new UiPickerModel
        {
            Title = "HOLDING CELL", Icon = UiIcon.ShieldCheck, EmptyIcon = UiIcon.ShieldCheck, Numbered = true, PerPage = 8, Locked = true,
            LockWords = t("reallife_ui_world_pick_custody", "In custody"), FootNote = t("reallife_ui_world_pick_custody_foot", "You cannot leave until this is settled"),
            Said = "Release in 5 h 25 min\nMidtown North Precinct. Sentence: 12 hours. The clock runs in game time.",
            Context = new[]
            {
                new UiInfoRow
                {
                    Label = t("reallife_law_cell_held", "Held since"),
                    Value = t("reallife_law_cell_held_today", "{time} today, {span}").Replace("{time}", "02:10")
                        .Replace("{span}", t("reallife_law_cell_span", "{hours} h {minutes} min").Replace("{hours}", "6").Replace("{minutes}", "35")),
                },
            },
            PageWords = t("reallife_ui_world_pick_page", "{page} of {pages}").Replace("{page}", "{0}").Replace("{pages}", "{1}"),
            EmptyTitle = t("reallife_ui_world_pick_empty", "Nothing to choose"), EmptyBody = t("reallife_ui_world_pick_empty_body", "There is nothing here to choose right now."),
        };
        Prompts(m, t, true);
        Foot(m, t);
        m.Chosen = 1;
        m.Second = file ? t("reallife_ui_world_pick_file_close", "Close the file") : t("reallife_ui_world_pick_file", "Read the file");
        m.Entries = new[]
        {
            new UiPickEntry
            {
                Note = true, Name = "CHARGE SHEET",
                Detail = file ? "Driving through a red light on Fifth Avenue\nDriving without a licence\nFines and damages charged at your arrest: $1,260" : null,
            },
            new UiPickEntry
            {
                Icon = UiIcon.Clock, Name = "Sit it out", Detail = "Do nothing. The time runs on until you are released.",
                Figure = t("reallife_law_cell_when_today", "Today, {time}").Replace("{time}", "14:10"),
            },
            new UiPickEntry { Icon = UiIcon.Key, Name = "Pay bail", Detail = "You are released at once.", Figure = "$5,000" },
            new UiPickEntry { Icon = UiIcon.Scales, Name = "Lawyer already called", Disabled = true },
            new UiPickEntry
            {
                Icon = UiIcon.Heart, Name = t("reallife_law_cell_doctor", "See the duty doctor"), Detail = t("reallife_law_cell_doctor_detail", "Only if you are unwell."),
                Figure = t("reallife_law_cell_doctor_free", "Free"), Reason = t("reallife_law_cell_doctor_fine", "You are fine"), Disabled = true,
            },
        };
        return m;
    }

    /// <summary>A list with nothing in it: the empty state with the way out.</summary>
    public static UiPickerModel WorldNothing(UiWords t)
    {
        UiPickerModel m = WorldDesk(t, false);
        m.Entries = new UiPickEntry[0];
        return m;
    }

    /// <summary>A list over the street with the HUD's bar, centred high.</summary>
    public static void WorldPickerScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiPickerModel m, bool night = false) =>
        WithBar(Over(HudBar(BarWords, false, false, night)), () => UiWorld.PickerLay(s, m, f, dark, k));

    // ------------------------------------------------------------------------------------------------- the job offer

    /// <summary>The game's job offer at a coffee shop: the job, its hours, days and wage, the boss, and the two answers on the game's keys.</summary>
    public static UiOfferModel WorldOffer(UiWords t, IUiKeys k, bool hired)
    {
        string[] days =
        {
            t("common_monday", "Monday"), t("common_tuesday", "Tuesday"), t("common_wednesday", "Wednesday"), t("common_thursday", "Thursday"), t("common_friday", "Friday"),
            t("common_saturday", "Saturday"), t("common_sunday", "Sunday"),
        };
        string worked = UiWorld.Days(new[] { true, true, true, true, true, false, false }, days, t("reallife_ui_world_offer_run", "{from} to {to}"),
            t("reallife_ui_world_offer_and", "{list} and {last}"), t("playerhud_currentjob_day_off", "Day Off"));
        var m = new UiOfferModel
        {
            Kicker = t("reallife_ui_world_offer_kicker", "Job offer"), Title = "Barista", Emblem = UiIcon.Briefcase, Side = "Bloom Café", SideSub = "70 Fifth Avenue",
            Face = "Dana Ruiz", FaceLine = t("reallife_ui_world_offer_boss", "{name} would be your boss").Replace("{name}", "Dana Ruiz"),
            Facts = new[]
            {
                new UiOfferFact
                {
                    Icon = UiIcon.Clock, Label = t("reallife_ui_world_offer_hours", "Hours"),
                    Value = t("reallife_ui_world_offer_span", "{from} to {to}").Replace("{from}", "07:00").Replace("{to}", "15:00"),
                },
                new UiOfferFact { Icon = UiIcon.Calendar, Label = t("reallife_ui_world_offer_days", "Days"), Value = worked },
                new UiOfferFact
                {
                    Icon = UiIcon.Coin, Label = t("joboffer_wage_per_hour", "Wage per hour"), Value = t("reallife_ui_world_offer_wage", "{wage} an hour").Replace("{wage}", "$18"),
                    Sub = t("reallife_ui_world_offer_week", "About {amount} a week").Replace("{amount}", "$720"),
                },
            },
            BackLabel = t("reallife_ui_world_offer_not_now", "Not now"), BackKey = GameKey(k, "Esc", UiIcon.PadB),
            GoLabel = hired ? t("reallife_ui_world_offer_hired", "You work here") : t("joboffer_accept_job", "Accept job"), GoKey = GameKey(k, "F", UiIcon.PadX), GoOff = hired,
        };
        m.Prompts = hired
            ? new[] { new UiPrompt { Action = UiAction.Back, Label = m.BackLabel } }
            : new[] { new UiPrompt { Action = UiAction.Alt, Label = m.GoLabel }, new UiPrompt { Action = UiAction.Back, Label = m.BackLabel } };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The offer over the coffee shop's floor, the HUD's bar under it.</summary>
    public static void WorldOfferScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool hired) =>
        WithBar(Over(HudBar(BarWords, false, false, false)), () => UiOfferCard.Lay(s, WorldOffer(t, k, hired), f, dark, k));
}
