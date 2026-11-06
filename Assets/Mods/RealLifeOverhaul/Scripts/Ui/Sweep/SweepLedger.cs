using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the interface does for one surface a player can see.</summary>
    public enum SweepState : byte
    {
        Kit,        // drawn on the kit by a registered screen (Screen)
        Follows,    // drawn because the game's mechanism it uses is drawn by a registered screen (Screen)
        WayBack,    // the page the screen's switch gives back; never shown while the kit is on
        Own,        // drawn by the mod in the kit's look, outside the registry (Words says where)
        Decision,   // left the game's by a decision (Words, Date)
        NotScreen,  // nothing to read or press (Words says why)
        Pending     // not converted yet: named in the report; the check counts them
    }

    /// <summary>One line of the audit: a surface, its state, the screen that draws it and the files that build it.</summary>
    public struct SweepRow
    {
        public string Surface;
        public SweepState State;
        public string Screen;     // the registered screen that draws it (Kit, Follows), else null
        public string Words;      // the decision, the reason, or where it is drawn
        public string Date;       // a decision's date
        public string[] Files;    // the mod's files (under Scripts) that build it with the game's UI parts or a canvas of their own
    }

    /// <summary>
    /// The audit of the total conversion: every surface of the game and the mod that a player meets in a city, with how it is
    /// drawn. The files that build interface of their own (a canvas, an image or text made in code, a copy of a game panel, a
    /// phone app) are named here, and a check (EconomyCheck, SweepUiChecks) fails on a file that builds one and is not
    /// named, on a phone app without a kit face, and on a screen that runs without its switch, so a new screen cannot reach
    /// players in the old look by accident. Game-free. Cost: an array, read by the check.
    /// </summary>
    public static class SweepLedger
    {
        public const string Owner = "Owner, 1 Nov 2026";

        static SweepRow Row(string surface, SweepState state, string screen, string words, params string[] files)
            => new SweepRow { Surface = surface, State = state, Screen = screen, Words = words, Files = files ?? new string[0] };

        static SweepRow Kept(string surface, string words, string date) => new SweepRow { Surface = surface, State = SweepState.Decision, Words = words, Date = date, Files = new string[0] };

        public static readonly SweepRow[] Rows =
        {
            // ------------------------------------------------------------------------------------------------ the mod's phone apps (each one's Kit is read from its install)
            Row("Calendar app", SweepState.Kit, "calendar", "an app window over the city", "Calendar/CalendarDirector.cs"),
            Row("Campus app", SweepState.Kit, "campus", "an app window over the city", "Careers/CareersDirector.Study.cs"),
            Row("Career app", SweepState.Kit, "career", "an app window over the city", "Careers/CareersDirector.Work.cs"),
            Row("Construction app", SweepState.Kit, "construction", "an app window over the city", "Construction/ConstructionDirector.App.cs"),
            Row("Bank apps", SweepState.Kit, "banks", "an app window over the city", "Economy/Banking/BankApps/BanksDirector.cs"),
            Row("Finance app", SweepState.Kit, "finance", "an app window over the city", "Economy/Banking/EconomyDirector.App.cs"),
            Row("Brands app", SweepState.Kit, "brands", "an app window over the city", "Economy/Brands/BrandsDirector.cs"),
            Row("Company app", SweepState.Kit, "company", "an app window over the city", "Economy/Company/CompanyDirector.cs"),
            Row("Car Showroom app", SweepState.Kit, "dealer", "an app window over the city", "Economy/Dealer/DealerDirector.cs", "Economy/Dealer/DealerReportPage.cs"),
            Row("Property app", SweepState.Kit, "estate", "an app window over the city", "Economy/Estate/EstateDirector.cs"),
            Row("Web shop app", SweepState.Kit, "webshop", "an app window over the city", "Economy/Online/OnlineDirector.cs"),
            Row("Your bank app", SweepState.Kit, "own_bank", "an app window over the city", "Economy/OwnBank/OwnBankDirector.cs"),
            Row("Services app", SweepState.Kit, "services", "an app window over the city", "Economy/Services/ServicesDirector.cs"),
            Row("Ventures app", SweepState.Kit, "ventures", "an app window over the city", "Economy/Ventures/VenturesDirector.cs"),
            Row("Marketplace app", SweepState.Kit, "marketplace", "an app window over the city", "Hustles/HustleDirector.cs"),
            Row("City Ledger", SweepState.Kit, "ledger", "an app window over the city", "Ledger/LedgerPage.cs", "Ledger/LedgerPage.Flow.cs"),
            Row("Newsstand app", SweepState.Kit, "newsstand", "an app window over the city", "Ledger/NewsstandApp.cs", "Ledger/NewsstandApp.Reading.cs"),
            Row("Stocks app", SweepState.Kit, "stocks", "an app window over the city", "Markets/MarketDirector.App.cs", "Markets/MarketPage.cs"),
            Row("Guide app", SweepState.Kit, "guide", "an app window over the city", "Meta/GuideApp.cs"),
            Row("Mod settings app", SweepState.Kit, "mod_settings", "an app window over the city", "Meta/SettingsApp.cs", "Meta/SettingsView.cs"),
            Row("Staff and roles app", SweepState.Kit, "staff_roles", "an app window over the city", "Meta/StaffApp.cs", "Meta/StaffView.cs"),
            Row("Economy app", SweepState.Kit, "economy", "an app window over the city", "Outlook/OutlookApp.cs", "Outlook/OutlookDirector.App.cs", "Outlook/OutlookKit.cs", "Outlook/OutlookPage.cs"),
            Row("Camera app", SweepState.Kit, "camera", "Decision card, and the viewfinder over the world", "Photos/CameraApp.cs", "Photos/CameraView.cs"),
            Row("Gallery app", SweepState.Kit, "photo_gallery", "an app window over the city", "Photos/GalleryApp.cs", "Photos/GalleryApp.Grid.cs"),
            Row("Planner app", SweepState.Kit, "planner", "an app window over the city", "Planner/PlannerDirector.cs"),
            Row("Flare app", SweepState.Kit, "flare", "an app window over the city",
                "Social/Flare/FlareApp.Sheet.cs", "Social/Flare/FlareDirector.cs", "Social/Flare/FlareKit.cs", "Social/Flare/FlareList.cs"),
            Row("Tech app", SweepState.Kit, "tech", "an app window over the city", "Tech/TechDirector.FirmsApp.cs"),
            Row("Open Roles app", SweepState.Kit, "open_roles", "an app window over the city", "Tech/TechDirector.Roles.cs"),
            Row("Utilities app", SweepState.Kit, "utilities", "an app window over the city", "Utilities/UtilitiesDirector.cs"),

            // ------------------------------------------------------------------------------------------------ the mod's own parts of the HUD and of the game's panels
            Row("The shift card and the objective card (top right)", SweepState.Follows, "hud_side", "the side panels mirror every card shown", "Jobs/ShiftCard.cs"),
            Row("The mod's needs bars and the season under the date", SweepState.Follows, "hud", "the status bar's needs and season readings", "Home/HomeNeedsBar.cs", "Seasons/SeasonBar.cs"),
            Row("The mod's bars copied from the top bar", SweepState.Follows, "hud", "read by the status bar", "Core/GameUi/GameBar.cs"),
            Row("The objectives window's groups", SweepState.Follows, "hud_objectives", "every group is read, the mod's among them", "Core/GameUi/TaskGroup.cs"),
            Row("The minimap and the route it draws", SweepState.Own, null, "the slate card of the map job (w5-map), drawn by Navigation/NavHud in the kit's look",
                "Navigation/NavHud.Destination.cs", "Navigation/NavHud.Make.cs", "Navigation/NavHud.Scene.cs", "Navigation/NavHud.Strip.cs"),
            Row("The dot at the centre during mouse look", SweepState.NotScreen, null, "a dot with no words and nothing to press", "Camera/CrosshairDot.cs"),
            Row("The car's speed and service readings", SweepState.Follows, "item_panel", "the vehicle panel's rows are read, any row a mod copied in among them",
                "Driving/SpeedReading.cs", "Transport/CarDash.cs"),
            Row("Dance in the loudspeaker's panel", SweepState.Follows, "world_overlay_click", "a button of the radio panel, so the card lists it", "Meals/DanceButton.cs"),
            Row("Eat at a seat in the activity panel", SweepState.Follows, "activity_panel", "a button of the activity panel, so the card and the strip list it", "Meals/TakeoutButton.cs"),
            Row("Cook and Family in the fridge, the computer's buttons", SweepState.Follows, "world_overlay_click", "every button of the game's panel is listed on the card",
                "Home/HomeFridgeButton.cs", "Careers/ComputerButtons.cs"),
            Row("The mod's clothes in the clothes screen", SweepState.Follows, "wardrobe", "the mod's tiles are read with the game's", "Character/GridLooks.cs"),
            Row("The Interior tab in BizMan", SweepState.Follows, "bizman", "a page of the BizMan screen", "Interiors/InteriorTab.cs"),
            Row("The claimed buildings' refusals", SweepState.NotScreen, null, "watchers on the game's pages; they draw nothing", "Core/ClaimedOffers.cs"),
            Row("The garage shop's turntable", SweepState.Follows, "world_picker", "the shop's pages are the picker's list", "Garage/GarageShop.View.cs"),
            Row("Build mode, its tour and its design list", SweepState.Kit, "build_mode", "the old bar, legend and window are the way back",
                "Construction/BuildMode.cs", "Construction/BuildModeBar.cs", "Construction/BuildModeKit.cs", "Construction/BuildModeLegend.cs",
                "Construction/BuildModePanel.cs", "Construction/BuildModeTour.cs"),
            Row("The district window and the map's place names", SweepState.Follows, "map_district", "the district window is the way back; the place names are the map's markers",
                "Districts/DistrictPanel.cs", "Core/MapPlaces.Names.cs"),
            Row("The HUD phone's skin", SweepState.WayBack, "phone", "the phone's face is the kit's; the skin shows with the switch off", "Phone/PhoneSkin.cs"),
            Row("The mod's panels in the game's Options", SweepState.Follows, "options_city", "the Mods category lists the mod's groups", "Meta/OptionsLayout.Search.cs", "Core/GameUi/OptionRows.cs"),
            Row("The mod's messages and questions", SweepState.Follows, "mod_box", "MetaUi.Message and Confirm go to the kit's card first", "Meta/MetaUi.cs"),

            // ------------------------------------------------------------------------------------------------ the parts every mod window is made of (Core/GameUi)
            Row("AppShell, InfoBox and their parts", SweepState.WayBack, "shell_frame", "the old pages of an app; the kit's face stands over the app's panel while its switch is on",
                "Core/GameUi/AppShell.cs", "Core/GameUi/InfoBox.cs", "Core/GameUi/InfoBox.Frame.cs", "Core/GameUi/InfoBox.Look.cs", "Core/GameUi/InfoBox.Slider.cs"),
            Row("The help frame", SweepState.WayBack, "game_help", "the old Guide pages and the build mode window", "Core/GameUi/HelpFrame.cs"),
            Row("The pick list (TravelList, DeskList)", SweepState.Follows, "world_picker", "every list window is read by the picker", "Core/GameUi/TravelList.cs"),
            Row("The city map picker", SweepState.Follows, "map_picker", "the choice of places on the open map", "Core/GameUi/MapPicker.cs"),
            Row("The notifications' pictures", SweepState.Follows, "notice_cards", "the cards read the picture the mod gave", "Core/GameUi/NoticeLook.cs"),
            Row("The font of the game", SweepState.NotScreen, null, "a font lookup", "Core/GameUi/GameFont.cs"),
            Row("The phone's apps, folders and pages", SweepState.Follows, "phone", "the kit's phone and All apps draw the tiles",
                "Core/GameUi/PhoneApps.cs", "Core/GameUi/PhoneFolders.cs", "Core/GameUi/PhoneGridPager.More.cs"),

            // ------------------------------------------------------------------------------------------------ the mechanisms: every user of one is drawn by its screen
            Row("Prompts over a door, a desk, a lift or a kiosk (WorldPanel, DoorPanel, JobPanel)", SweepState.Follows, "world_prompt", "the box the game's overlay system shows is read and drawn"),
            Row("A pick list window (DeskList, TravelList users)", SweepState.Follows, "world_picker", "a window of rows to choose from"),
            Row("Notifications.Show, the welcome and the new version notice", SweepState.Follows, "notice_cards", "the game's toasts, the mod's among them"),
            Row("The notification list", SweepState.Follows, "notice_list", "the game's list of past notices"),
            Row("GameConfirm.Show, every yes or no, the build notice, the day roll notices", SweepState.Follows, "question_box", "the game's question box"),
            Row("The game's one-button warning", SweepState.Follows, "acknowledge_box", "in a city it stays the game's box drawn by the kit"),
            Row("TextBox, every typed answer", SweepState.Follows, "text_box", "the game's text input box"),
            Row("Every IPlayerActivity: routines, shifts, classes, study, desk work, the bus ride", SweepState.Follows, "activity_panel", "the game's activity panel"),
            Row("A desk session's pages", SweepState.Follows, "session_panel", "the game's activity panel while a session is chosen"),
            Row("Every Dialog: phone calls and talks face to face, with their forms", SweepState.Follows, "dialog", "the game's dialog panel and the Contacts app"),
            Row("Uncle Fred's monologue and the scene talks", SweepState.Follows, "monologue", "the game's monologue panel"),
            Row("The time machine's veil, the time of day, the funeral", SweepState.Follows, "time_skip", "the game's panels for time passing"),
            Row("The daily summary, goal toast, hospital, casino and skip bridge notes", SweepState.Follows, "hud_notices", "short cards and strips read from the game"),
            Row("The status bar, tools, dance and accessory lists, controls hints", SweepState.Follows, "hud", "the HUD over the running game"),
            Row("The building card, the sign's look, the preview terminal, the items list, the colour picker", SweepState.Follows, "world_building", "the world's smaller windows"),
            Row("The job offer and the private driver's list", SweepState.Follows, "world_job_offer", "the game's job offer and driver panels"),
            Row("Item overlays over shelves, machines, people and the tutorial's arrows", SweepState.Follows, "world_overlay_click", "the small card, the large card and a person's panel"),
            Row("Tooltips of the game and the mod", SweepState.Follows, "tooltips", "every TooltipSystem tooltip"),
            Row("The interior designer", SweepState.Kit, "designer", "the tool row, the catalogue and every panel of the designer"),
            Row("The pause menu, Options in the city, Blueprints, Help and F1", SweepState.Kit, "pause_menu", "the pause menu opens options_city, blueprints and game_help"),
            Row("The save tools, the bug report, the mod's messages", SweepState.Kit, "save_tools", "decision cards of the mod"),
            Row("The map's filters, subway and taxi list, district view and place card", SweepState.Kit, "map_filters", "the panels round the 3D map"),
            Row("The clothes, hair and surgery shops", SweepState.Kit, "wardrobe", "the game's three character shops on the info panel"),
            Row("Checkout, vehicle purchase, item panel, cargo", SweepState.Kit, "checkout", "buying and holding"),

            // ------------------------------------------------------------------------------------------------ not converted yet: the two pages still on the figures, table and side box shape
            Row("The city's people pages: the city, who contributes, a household, a person, one who moved on", SweepState.Pending, "population",
                "On the older dashboard shape (UiDashboard); the people info view of the Economy app (the people screen) and a person's profile are what they move to,"
                + " with their entry points from the work apps and Property."),
            Row("The studio page of a tech firm", SweepState.Pending, "studio",
                "On the older dashboard shape (UiDashboard); an app window page with a hero and a table is what it moves to, with the firm's actions and its next step kept."),

            // ------------------------------------------------------------------------------------------------ left the game's by decision
            Kept("The 3D city map itself", "The 3D map stays the game's; its panels are on the kit.", "1 Nov 2026"),
            Kept("Casino games, golf and tennis", "Mini games stay the game's.", "1 Nov 2026"),
            Kept("Placing and dragging in the designer, the package drag and the markers in a room", "Placement and dragging stay the game's.", "1 Nov 2026"),
            Kept("The main menu, new game, load, welcome, news, credits, the mods panel, the main menu's Options and Blueprints",
                "The main menu and its panels stay the game's; the mod runs in the city.", "1 Nov 2026"),
            Kept("Loading screens", "They run before the city.", "1 Nov 2026"),
            Kept("Character creation, the blueprint creator scene", "Characters are the owner's own work; the creator is a scene of its own.", "1 Nov 2026"),
            Kept("World markers: points of interest, direction guiders, item warning icons", "Left as the game's; sprites in the world.", "1 Nov 2026"),
            Kept("The game's feedback form", "The mod has its own bug report; nothing in the kit opens this one.", "3 Nov 2026"),
            Kept("The mod conflict notice", "The mod loader shows it before the mod is loaded.", "3 Nov 2026"),
            Kept("Debug console, fps meter, test panels, the faint vignette, the gamepad cursor", "Developer tools and input plumbing, not screens.", "3 Nov 2026"),
        };

        /// <summary>The files that build interface of their own, for the check: every file named by a row.</summary>
        public static string[] AllFiles()
        {
            var all = new System.Collections.Generic.List<string>(160);
            for (int i = 0; i < Rows.Length; i++)
            {
                all.AddRange(Rows[i].Files);
            }
            all.Sort(StringComparer.Ordinal);
            return all.ToArray();
        }
    }
}
