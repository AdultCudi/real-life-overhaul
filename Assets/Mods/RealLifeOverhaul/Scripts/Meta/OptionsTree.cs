using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Meta
{
    /// <summary>One row of the options page as the tree names it: a saved option by its id, or a button by its label key.</summary>
    public sealed class TreeRow
    {
        public readonly string Id;            // a saved option's id, or a button's label key
        public readonly bool IsButton;
        public string[] Needs = OptionsTree.None;   // option ids that must be on for this row to do anything
        public bool Free;                     // does not hang on its section's main switch
        public string Words;                  // more words the search finds it by, lower case, or null

        internal TreeRow(string id, bool isButton)
        {
            Id = id;
            IsButton = isButton;
        }

        /// <summary>The key of the one sentence shown when the pointer rests on the row: every option has one.</summary>
        public string TipKey => IsButton ? null : OptionsTree.TipPrefix + Id;
    }

    /// <summary>A section: a button that opens to at most <see cref="OptionsTree.MostRows"/> rows.</summary>
    public sealed class TreeSection
    {
        public readonly string Id;
        public readonly string TitleKey;
        public TreeGroup Group;
        public string Master;                 // the option id of its main switch, or null
        public string[] Needs = OptionsTree.None;   // what every row of it needs
        public string[] Features = OptionsTree.None; // the features whose test tools and Guide page are its
        public string GuideFeature;           // the feature whose Guide page "About this" opens, or null
        public string Words;                  // more words the search finds every row of it by, or null
        public readonly List<TreeRow> Rows = new List<TreeRow>();

        internal TreeSection(string id)
        {
            Id = id;
            TitleKey = OptionsTree.SectionPrefix + id;
        }

        /// <summary>The features this section stands for: their test tools are grouped under it, and the first one's Guide page is its "About this".</summary>
        public TreeSection For(params string[] features)
        {
            Features = features;
            if (GuideFeature == null && features.Length > 0)
            {
                GuideFeature = features[0];
            }
            return this;
        }

        /// <summary>The feature whose Guide page explains this section, when the section is not a feature of its own.</summary>
        public TreeSection Guide(string feature)
        {
            GuideFeature = feature;
            return this;
        }

        /// <summary>The main switch: it is the first row, and every other row is greyed while it is off (unless <see cref="TreeRow.Free"/>).</summary>
        public TreeSection Main(string optionId)
        {
            Master = optionId;
            Rows.Insert(0, new TreeRow(optionId, false));
            return this;
        }

        /// <summary>Options that every row of the section needs (the main switch too).</summary>
        public TreeSection Need(params string[] optionIds)
        {
            Needs = optionIds;
            return this;
        }

        public TreeSection Opts(params string[] optionIds)
        {
            foreach (string id in optionIds)
            {
                Rows.Add(new TreeRow(id, false));
            }
            return this;
        }

        /// <summary>One option that needs others to be on.</summary>
        public TreeSection Opt(string optionId, params string[] needs)
        {
            Rows.Add(new TreeRow(optionId, false) { Needs = needs });
            return this;
        }

        /// <summary>Options that stand in the section but work whatever its main switch says.</summary>
        public TreeSection FreeOpts(params string[] optionIds)
        {
            foreach (string id in optionIds)
            {
                Rows.Add(new TreeRow(id, false) { Free = true });
            }
            return this;
        }

        /// <summary>Buttons, by label key. A button never hangs on the main switch.</summary>
        public TreeSection Buttons(params string[] labelKeys)
        {
            foreach (string key in labelKeys)
            {
                Rows.Add(new TreeRow(key, true) { Free = true });
            }
            return this;
        }

        /// <summary>More words for the search: every row of the section is found by them.</summary>
        public TreeSection Also(string words)
        {
            Words = words;
            return this;
        }

        /// <summary>More words for the search for one row.</summary>
        public TreeSection Also(string rowId, string words)
        {
            foreach (TreeRow row in Rows)
            {
                if (row.Id == rowId)
                {
                    row.Words = words;
                }
            }
            return this;
        }
    }

    /// <summary>A top-level group: a button that opens to its sections.</summary>
    public sealed class TreeGroup
    {
        public readonly string Id;
        public readonly string TitleKey;
        public bool IsFlat;                   // its rows stand straight under its button: no section buttons (the few rows of "Start here")
        public readonly List<TreeSection> Sections = new List<TreeSection>();

        internal TreeGroup(string id, TreeSection[] sections)
        {
            Id = id;
            TitleKey = OptionsTree.GroupPrefix + id;
            foreach (TreeSection section in sections)
            {
                section.Group = this;
                Sections.Add(section);
            }
        }

        /// <summary>No section buttons: opening the group shows its rows at once.</summary>
        public TreeGroup Flat()
        {
            IsFlat = true;
            return this;
        }
    }

    /// <summary>
    /// The structure of the options page and nothing else: the groups, their sections, each section's rows in order and what a
    /// row needs on. <see cref="OptionsLayout"/> arranges the built rows by it and <see cref="OptionsPlan"/> does the
    /// arithmetic; CatalogueCheck reads it as it ships. A new feature adds one S(...) line:
    ///
    ///     S("fuel").For("fuel")                 // section id (its title is reallife_opt_sec_ and the id), the feature's id
    ///         .Main("fuel")                     // its main switch: first row, the others greyed while it is off
    ///         .Opts("fuel_price", "fuel_gauge") // more options by saved id, in the page's order
    ///         .Opt("fuel_cans", "car_ownership")// an option that needs another one on
    ///         .Buttons("&lt;reallife_fuel_page&gt;")  // a button of the player's (not a test), by label key
    ///
    /// Every option row needs its sentence in en.json under <see cref="TipPrefix"/> and its id (the example key is bracketed so
    /// that the locale check skips it). A row no line claims goes to "Other", named in Player.log.
    /// </summary>
    public static class OptionsTree
    {
        private const string Mod = "reallife";                 // apart, so that the locale check does not take a prefix for a key
        public const int MostRows = 10;                       // rows in one section: more than a screenful is a wall again
        public const string GroupPrefix = Mod + "_opt_group_";    // a group's title is this and its id
        public const string SectionPrefix = Mod + "_opt_sec_";    // a section's title
        public const string TipPrefix = Mod + "_opt_tip_";        // an option's sentence is this and its saved id
        public const string OtherId = "other";                // the section for rows nothing claims
        public const string TestsId = "tests";                // the feature that is the test tools themselves
        public const string TroubleId = "trouble";            // the group the test tools and "Other" stand in
        public const string StartId = "start";                // the group that is open the first time

        internal static readonly string[] None = new string[0];

        private static TreeSection S(string id)
        {
            return new TreeSection(id);
        }

        private static TreeGroup G(string id, params TreeSection[] sections)
        {
            return new TreeGroup(id, sections);
        }

        public static readonly TreeGroup[] Groups =
        {
            G(StartId,
                S("guide").Buttons("reallife_meta_settings_button", "reallife_meta_guide_button"),
                S("presets").Buttons("reallife_meta_preset_button_relaxed", "reallife_meta_preset_button_standard", "reallife_meta_preset_button_realistic",
                    "reallife_meta_preset_button_hardcore").Also("difficulty easy hard realism"),
                S("choices").Buttons("reallife_meta_reset_button", "reallife_meta_export_button", "reallife_meta_import_button").Also("settings file share defaults"),
                S("help").Buttons("reallife_meta_bug_button").Also("bug report problem crash error broken discord log help")).Flat(),

            G("you",
                S("needs").For("home").Main("home_life").Opts("home_needs", "home_strip", "home_easy", "home_sleep", "home_morning")
                    .Opt("home_morning_news", "home_morning").Opts("home_illness")
                    .Also("health sick sleep stress hygiene tired").Also("home_morning_news", "notification notifications summary news weather wake"),
                S("routines").Guide("home").Need("home_life").Opts("home_routines", "home_fitness", "home_body", "home_jog")
                    .Also("cooking cook food kitchen exercise gym run diet"),
                S("meals").For("meals").Opts("meals_seated").Opt("meals_cooked", "home_life", "home_routines").Opts("meals_dance")
                    .Also("eat eating meal table seat chair sit plate tray dinner cook dance dancing music radio speaker loudspeaker mood"),
                S("looks").For("character").Opts("character_new_looks", "character_high_detail", "character_hair", "character_skin", "character_lighting")
                    .Also("appearance beard face"),
                S("outfit_m").Guide("character").Opts("look2_top_m", "look2_top_m_colour", "look2_legs_m", "look2_legs_m_colour", "look_feet_m")
                    .Also("clothes wardrobe wear shirt"),
                S("outfit_f").Guide("character").Opts("look2_dress_f", "look2_dress_f_colour", "look2_top_f", "look2_top_f_colour", "look2_legs_f", "look2_legs_f_colour", "look_feet_f")
                    .Also("clothes wardrobe wear skirt"),
                S("shops").Guide("leisure").Need("leisure").Opts("clothes_shops", "clothes_must_be_bought").Also("wardrobe buy outfit"),
                S("roommates").For("home_roommates").Opts("home_roommates").Also("flatmate lodger share"),
                S("utilities").For("utilities").Main("utilities")
                    .Also("power water electricity bills utility office deposit"),
                S("family").For("family").Main("family").Opts("family_at_home", "family_wedding", "family_children", "family_relatives", "family_friction",
                    "family_interest", "family_year_weeks").Buttons("reallife_family_test_page").Also("partner spouse wife husband marriage kids baby"),
                S("partner").Guide("family").Need("family").Opts("family_work", "family_wealth_mood", "family_expect").Also("partner money rich wealth job work half days part time"),
                S("social").For("social").Main("social").Opts("social_meetings", "social_relationships", "social_outings", "social_contact", "social_texts")
                    .Opt("social_texts_max", "social_texts").Opt("social_composer", "texts", "texts_people")
                    .Opt("texts_city", "texts", "texts_people").Also("friends dating date people calls typing staff gossip"),
                S("street").For("street").Main("street_talk").Opts("street_brush").Also("talk passer-by stranger chat directions conversation pedestrian"),
                S("flare").For("flare").Main("social_media")
                    .Opts("social_media_notices", "flare_busy", "flare_city_office", "flare_share_posts", "flare_share_pictures", "flare_filter", "flare_theme", "flare_pictures", "flare_city_photos")
                    .Also("social media app followers posts likes selfie influencer"),
                S("texts").For("texts").Main("texts").Opts("texts_device", "texts_threads", "texts_replies", "texts_posts", "texts_people", "texts_official", "texts_rivals", "texts_ledger")
                    .Buttons("reallife_texts_status").Also("model words messages replies comments texts gpu processor pack lite staff gossip memory language"),
                S("textsai").For("textsai").Need("texts").Opts("texts_service")
                    .Buttons("reallife_textsai_key", "reallife_textsai_chatgpt", "reallife_textsai_model", "reallife_textsai_address", "reallife_textsai_forget",
                        "reallife_textsai_test", "reallife_textsai_status", "reallife_textsai_usage")
                    .Also("ai service chatgpt openai anthropic gemini google openrouter groq mistral cerebras opencode zen ollama lm studio key api sign model"),
                S("standing").For("standing").Main("standing").Opts("standing_street", "standing_press", "standing_corruption", "standing_request_notes")
                    .Also("fame reputation recognition wealth famous reporter interview press bribe lobby charity")
                    .Also("standing_request_notes", "fundraiser fundraising donation sponsor request requests"),
                S("ledger").For("ledger").Main("social_newspaper").Opts("ledger_kiosks", "ledger_online").Opt("ledger_online_notes", "ledger_online")
                    .Also("newspaper paper news kiosk subscription delivery online newsstand internet phone"),
                S("leisure").For("leisure").Main("leisure").Opt("leisure_chess", "city_life").Opts("leisure_fishing", "leisure_night_out", "leisure_sights", "leisure_cinema")
                    .Opt("night_life", "city_life").Opts("photo_mode").Buttons("reallife_leisure_test_records").Also("fun hobby nightclub bar"),
                S("lifeevents").For("life").Main("life_events").Opts("life_frequency").Also("random luck surprise"),
                S("permadeath").For("permadeath").Opts("permadeath").Also("death die dead obituary ending hardcore funeral"),
                S("phone").For("phone", "phonefolders").Opts("phone_contacts", "phone_skin", "phone_folders", "notice_look").Buttons("reallife_phone_layout_reset")
                    .Also("call numbers wallpaper notifications toast icons folder folders dock home screen apps tidy arrange"),
                S("photos").For("photos").Main("phone_camera").Opts("phone_camera_grid").Also("camera gallery photo photos picture pictures selfie grid filter frame wallpaper")),

            G("city",
                S("citylife").For("citylife").Main("city_life").Opts("city_density", "city_workers", "city_everyday", "city_interactions")
                    .Also("pedestrians passers-by pavement crowd"),
                S("events").For("events").Opts("city_fires").Opt("fire_frequency", "city_fires").Opts("weather_matters").Opt("weather_frequency", "weather_matters")
                    .Opts("street_events").Opt("street_event_frequency", "street_events").Opt("street_protest_notes", "street_events")
                    .Also("rain storm market parade protest").Also("street_protest_notes", "demonstration demonstrations notification notifications"),
                S("accidents").For("traffic", "ragdoll", "people").Opts("traffic_accidents").Opt("accident_frequency", "traffic_accidents")
                    .Opt("accident_notices", "traffic_accidents").Opt("accident_marker", "traffic_accidents").Opts("siren", "people",
                    "crash_consequences", "ragdolls").Opt("player_knockdown", "ragdolls").Opt("ped_incidents", "ragdolls")
                    .Also("crash collision ambulance paramedics fire service notification notifications sound waypoint marker pin"),
                S("police").Guide("crime").Main("police").Opt("police_presence", "people").Opt("patrol_officers", "people").Opt("patrol_frequency", "patrol_officers")
                    .Opts("police_pursuit", "player_offences").Also("cop cops officers chase fine fines speeding"),
                S("stations").For("law").Main("law_precincts").Opts("law_station").Opt("law_station_furniture", "law_station").Opt("law_map_markers", "map_places_shown")
                    .Opts("law_custody").FreeOpts("law_arrests", "law_reputation")
                    .Also("precinct cell custody arrest record"),
                S("crime").For("crime").Main("street_crime").Opts("crime_frequency").FreeOpts("crime_against_player").Also("robbery mugging thief theft burglar scam pickpocket"),
                S("courts").For("justice").Main("justice").Opts("justice_lawyers", "justice_courthouse", "justice_no_prison", "justice_prison_cap", "justice_community",
                    "justice_civil", "justice_aftermath").Opt("justice_phone", "phone_contacts").Also("trial judge lawyer prison jail sentence bail"),
                S("faults").For("faults").Main("city_faults").Opts("city_faults_count", "city_faults_false_cost")
                    .Also("street light lamp lamps dark pothole potholes road litter rubbish manhole unsafe broken fault faults report reports crew"),
                S("council").For("civic").Main("civic").Opts("civic_elections").Opt("civic_office", "civic_elections").Opts("civic_construction")
                    .Also("election vote mayor politics roadworks city hall candidate campaign councillor stand office"),
                S("sounds").Guide("leisure").Need("leisure").Main("ambient_sound").Opts("ambient_birds", "ambient_night", "ambient_city", "ambient_crowd").Also("audio volume noise")),

            G("around",
                S("camera").For("camera").Main("camera_enabled").Opts("camera_indoors", "camera_pitch", "camera_distance", "camera_fov", "camera_height", "camera_follow")
                    .Opt("car_camera").Opt("car_camera_distance", "car_camera").Also("view third person zoom"),
                S("carperf").For("driving").Main("car_tuning").Opts("car_top_speed", "car_acceleration").Buttons("reallife_car_measure").Also("speed fast engine"),
                S("wheel").Guide("driving").Opts("smooth_throttle", "speedometer", "radio_hud").Also("pedal dashboard speed music"),
                S("handling").Guide("driving").Opts("car_handling")
                    .Also("weight heavy suspension springs bounce body roll lean dive brakes abs grip slide sliding tyres tires steering"),
                S("owning").Guide("transport").Main("car_ownership").Opts("car_easy").Also("fuel petrol service insurance breakdown wear"),
                S("garage").For("garage").Main("garage").Opts("garage_cosmetics", "garage_tuning", "garage_underglow", "garage_police", "garage_sounds").Buttons("reallife_garage_test_vehicles")
                    .Also("tuning paint parts custom"),
                S("bikes").Guide("garage").Opt("two_wheelers", "garage").Also("bicycle bicycles motorbike motorbikes motorcycle scooter two-wheelers"),
                S("traffic").For("streets").Opts("horn_reactions", "natural_accidents", "solid_streets", "city_drivers").Opt("tow_trucks", "traffic_accidents")
                    .Also("pedestrians cars road tow truck wreck recovery braking parking park pull out drivers"),
                S("transit").For("transport").Opts("transit_buses").Opt("transit_people", "transit_buses").Opts("transit_subway").Also("bus metro train public transport"),
                S("taxi").For("taxi").Opts("transit_taxi_call").Also("cab cabs ride"),
                S("nav").For("navigation").Main("nav_minimap").Opts("nav_rotate", "nav_size", "nav_zoom", "nav_base").FreeOpts("nav_gps").Opt("nav_other_route", "nav_gps")
                    .FreeOpts("map_places_shown", "special_names").Also("map route satnav directions destination names building buildings"),
                S("districtview").For("districtview").Opts("district_view")
                    .Also("district districts neighbourhood neighbourhoods neighborhood map layer layers stats statistics wealth rent prices footfall crime standing coverage "
                        + "internet competition council"),
                S("licences").For("licences").Main("driving_licence").Opts("licence_start_with", "driving_school", "licence_points", "roadside_checks", "heavy_licence")
                    .Also("driving test theory examiner points heavy goods truck lorry cdl hgv"),
                S("travel").For("travel").Opts("travel_hotels", "travel_holidays", "travel_extras").Also("holiday trip hotel stay")),

            G("work",
                S("jobs").For("jobs").Main("jobs").Opts("job_pay").Also("gig gigs shift wage employment"),
                S("careers").For("careers").Opt("careers", "jobs").Also("employer promotion rank review"),
                S("study").For("careers_study").Main("careers_study").Opts("careers_class_start")
                    .Also("school course courses class classes diploma education study learn skill skills homework exam timetable"),
                S("degrees").For("careers_degrees").Opt("careers_degrees", "careers_study")
                    .Also("university degree degrees bachelor master mba law nursing certificate certificates exam student loan grant scholarship graduation tuition fees"),
                S("familystudy").For("careers_family").Opt("careers_family", "careers_study")
                    .Also("children child university tutoring tutor academy training school own business staff degree sponsored"),
                S("projobs").For("careers_jobs").Opt("careers_jobs", "careers_study").Opt("careers_agency_note", "careers_jobs").Opt("careers_reject_reason", "careers_jobs")
                    .Also("profession professional job jobs apply application interview offer salary employer developer lawyer accountant nurse teacher architect engineer rival vacancy vacancies"),
                S("worklife").For("careers_life").Opt("careers_life", "careers_jobs")
                    .Also("review reviews promotion promoted rise raise bonus overtime burnout boss office politics headhunter poached redundancy redundant benefit pension retire "
                        + "retirement options shares conflict interest"),
                S("ownwork").For("careers_own").Opt("careers_own", "careers_jobs").Opt("careers_own_offer_notes", "careers_own")
                    .Also("freelance freelancing contract contracts client clients practice self employed startup found founder spin out colleagues restraint clause non compete side work"),
                S("planner").For("planner").Main("planner").Opts("planner_reminders", "planner_morning").Also("diary notes to do"),
                S("calendar").For("calendar").Main("calendar").Also("calendar month months year date dates payday bills due birthday birthdays")),

            G("money",
                S("credit").For("economy").Opts("economy").Opt("economy_grade_up_notes", "economy").Also("bank borrow score finance"),
                S("debt").Guide("economy").Need("economy").Opts("economy_products", "economy_shark", "economy_consequences", "economy_level")
                    .Also("borrow overdraft card collections repossession").Also("economy_products", "mortgage rent overdraft card finance"),
                S("banks").For("economy_banks").Need("economy").Opts("economy_banks").Also("bank app current account statement card loan apply"),
                S("cityeco").For("economy_city").Need("economy").Main("economy_city").Opts("economy_city_level", "economy_rate_ahead").Also("prices rates boom recession inflation"),
                S("ventures").For("ventures").Opts("ventures_investors", "ventures_level").Also("invest investor pitch deal"),
                S("markets").For("markets", "markets_listing", "markets_bots", "markets_derivatives").Main("ventures_market")
                    .Opts("markets_margin", "markets_listing", "markets_bots", "markets_dividend_notes", "markets_bonds", "markets_bond_so_far", "markets_large", "markets_reinvest",
                        "markets_derivatives")
                    .Also("shares stocks invest dividend broker trading candles chart margin short property trust reit")
                    .Also("markets_listing", "listing flotation offer prospectus shareholders takeover")
                    .Also("markets_bots", "bot bots robot automated algorithm strategy stop loss take profit limit order trend momentum")
                    .Also("markets_bonds", "bond bonds treasury gilt deposit certificate fixed income interest coupon reinvest savings rollover ladder")
                    .Also("markets_large", "large big block limit volume worked order algorithm vwap slice wealth investor")
                    .Also("markets_reinvest", "reinvest drip dividend reinvestment compound distribution trust")
                    .Also("markets_derivatives", "futures future options option call put hedge hedging derivative derivatives strike premium raw materials commodity factory import")),

            G("business",
                S("company").For("economy_company").Need("economy").Main("economy_company").Opts("economy_company_staff", "economy_company_cover",
                    "economy_company_marketing", "economy_company_tax", "economy_company_hide", "economy_company_licences", "economy_company_level")
                    .Also("shop wages insurance advertising accountant permits"),
                S("rota").For("economy_rota").Opt("economy_rota", "economy_company").Also("schedule shifts opening hours staff"),
                S("managers").For("economy_managers").Opt("economy_managers", "economy_company")
                    .Also("manager managers regional bureaucracy delegate automate restock orders prices raises hiring report"),
                S("cleaning").For("economy_cleaning").Need("economy").Opts("economy_cleaning").Also("cleaners contract"),
                S("sale").For("economy_sale").Need("economy").Opts("economy_sale").Also("sell buyer offer"),
                S("ownbank").For("economy_ownbank").Need("economy").Opts("economy_bank").Also("bank licence deposits savers loans regulator capital fund"),
                S("bankbranch").For("economy_bank_branch").Need("economy").Opt("economy_bank_branch", "economy_bank")
                    .Also("bank branch branches teller tellers counter customers accounts deposit withdraw loan high street"),
                S("endscale").For("economy_endscale").Opts("economy_endscale")
                    .Also("end game late scale billions hyperscaler cloud data centre racks broadband network fibre bank maintenance upkeep crews staff payback"),
                S("brands").For("economy_brands").Need("economy").Opts("economy_brands").Also("chain franchise"),
                S("rivals").For("rivals").Main("rivals").Opts("rivals_strength")
                    .Also("rival rivals tycoon tycoons competitor competitors rich list richest takeover outbid poach lobby lawsuit sue press end game late game pressure"),
                S("online").For("economy_online").Need("economy").Opts("economy_online").Opt("economy_online_wait_notes", "economy_online").Also("web shop orders delivery internet"),
                S("people").For("tech_people").Opts("tech_people").Also("professionals staff employees skills skill percent role promotion review course certificate notice severance lay off poach"),
                S("jobsboard").For("tech_jobs").Opt("tech_jobs", "tech_people")
                    .Also("jobs board hire hiring recruit recruitment applicants interview offer headhunter search labour market candidates"),
                S("techsector").For("tech_sector").Opt("tech_sector", "tech_people").Opt("tech_offer_notes", "tech_sector")
                    .Also("tech software saas app game studio consultancy cybersecurity security breach outage product subscription contract tender developer"),
                S("startups").For("tech_startups").Opt("tech_startups", "tech_sector")
                    .Also("startup startups venture capital vc angel fund pitch term sheet valuation equity round board shares options dilution"),
                S("techsites").For("tech_sites").Opt("tech_site_rooms", "tech_sector").Opts("tech_site_hq").Opt("tech_site_cloud", "tech_sector").Opt("tech_site_offices", "tech_sector")
                    .Also("data centre data center server servers rack racks cooling headquarters hq lobby reception office cloud hyperscaler sign buildings"),
                S("dealer").For("economy_dealer").Need("economy").Opts("economy_dealer", "economy_dealer_floor").Also("car dealership showroom auction wholesale used cars finance warranty")
                    .Also("economy_dealer_floor", "visitors customers floor salesperson bay bays camera photo"),
                S("offices").For("office_space").Opts("office_conversion").Also("office offices shop shops empty space building buildings convert vacant"),
                S("estate").For("economy_estate").Need("economy").Opts("economy_estate", "economy_estate_hotel", "economy_estate_hire", "economy_estate_rent")
                    .Also("buildings landlord tenant letting rent rental income").Also("economy_estate", "rent tenant landlord letting"),
                S("bizevents").For("business").Main("business_pressure").Opts("business_frequency", "business_easy").Also("trouble pressure competition"),
                S("bizkinds").Guide("business").Need("business_pressure").Opts("business_rivals", "business_racket", "business_inspections", "business_staff",
                    "business_suppliers", "business_customers", "business_audit", "business_opportunities").Also("trouble competition"),
                S("interiors").For("interiors").Main("interiors").Opts("interiors_prices_strength", "interiors_wear_strength", "interiors_standing", "interiors_staff")
                    .Also("interior design decor decoration plants paintings lighting wear refresh shabby furniture bizman tab income takings prices slider"),
                S("interiorstd").For("interiors_standard").Need("interiors").Opts("interiors_standard_strength", "interiors_basket")
                    .Also("district standard minimum complaints basket products reward strength slider income customers"),
                S("interiordecor").For("interiors_decor").Need("interiors").Opts("interiors_decor_strength").Also("decor ambience customers footfall clients office strength slider"),
                S("designs").For("designs").Opts("designs")
                    .Also("ready made design designs interior installation installer firm layout furniture fit out showroom studio academy office shop")),

            G("controls",
                S("keys").For("keys").Buttons("reallife_keys_reset", "reallife_keys_camera_toggle", "reallife_keys_interact", "reallife_keys_interact_alt",
                    "reallife_keys_free_cursor", "reallife_keys_look_behind", "reallife_keys_gentle_throttle", "reallife_keys_photo_mode",
                    "reallife_keys_walk")
                    .Also("key bindings shortcuts hotkeys rebind keyboard"),
                S("mouse").Guide("camera").Opt("camera_mouselook", "camera_enabled").Opt("camera_sensitivity", "camera_mouselook").Opts("enter_with_key")
                    .Also("input look cursor"),
                S("onfoot").For("walking").Opts("walk_toggle").Also("walk walking run running pace slow stroll speed sprint toggle")),

            G("performance",
                S("speed").For("performance").Opts("perf_refraction", "perf_sky_updates", "perf_stale_lights", "perf_far_districts", "perf_view_radius",
                    "perf_far_props", "perf_textures_whole", "perf_mod_cull").Also("fps frame rate lag stutter slow faster"),
                S("render").Guide("performance").Opts("render_shadow_reach", "render_shadow_cascades", "render_lod_reach")
                    .Also("graphics shadows shadow distance cascades level of detail lod models quality fps"),
                S("picture").For("visuals").Main("visuals_enabled").Opts("visuals_saturation", "visuals_contrast", "visuals_bloom", "visuals_detail")
                    .Also("graphics colour visuals quality")),

            G(TroubleId,
                S("savetools").For("savetools").Buttons("reallife_meta_repair_button", "reallife_meta_resetdata_button", "reallife_meta_remove_button")
                    .Also("uninstall remove backup save"),
                S("diagnostics").For("diagnostics").Opts("dev_diagnostics", "show_test_tools").Buttons("reallife_meta_check_button", "reallife_guard_button", "reallife_meta_welcome_button")
                    .Also("debug log developer"),
                S("multiplayer").For("multiplayer").Buttons("reallife_gp_tell_button", "reallife_gp_city_button")
                    .Also("going public multiplayer online friends host guest session"),
                S("measure").Guide("performance").Opts("perf_step_timing").Buttons("reallife_perf_probe_button", "reallife_perf_bench_button4",
                    "reallife_keys_perf_probe", "reallife_keys_perf_bench", "reallife_keys_character_report")
                    .Also("benchmark fps developer keys"),
                S("tuning").Guide("driving").Opts("car_handling_softness", "car_handling_bounce", "car_handling_roll", "car_handling_grip", "car_handling_steering",
                    "car_handling_brakes").Also("handling suspension springs dampers roll grip steering brakes developer sliders")),
        };

        private static Dictionary<string, TreeSection> _byRow;
        private static Dictionary<string, TreeRow> _rows;
        private static Dictionary<string, TreeSection> _byFeature;

        private static void Index()
        {
            if (_byRow != null)
            {
                return;
            }
            var byRow = new Dictionary<string, TreeSection>(StringComparer.Ordinal);
            var rows = new Dictionary<string, TreeRow>(StringComparer.Ordinal);
            var byFeature = new Dictionary<string, TreeSection>(StringComparer.Ordinal);
            foreach (TreeGroup group in Groups)
            {
                foreach (TreeSection section in group.Sections)
                {
                    foreach (TreeRow row in section.Rows)
                    {
                        if (!byRow.ContainsKey(row.Id))   // (placed twice: the first place counts, and the check fails)
                        {
                            byRow[row.Id] = section;
                            rows[row.Id] = row;
                        }
                    }
                    foreach (string feature in section.Features)
                    {
                        if (!byFeature.ContainsKey(feature))
                        {
                            byFeature[feature] = section;
                        }
                    }
                }
            }
            _rows = rows;
            _byFeature = byFeature;
            _byRow = byRow;
        }

        /// <summary>The section a row stands in (an option id or a button's label key), or null if nothing claims it.</summary>
        public static TreeSection SectionOf(string rowId)
        {
            Index();
            return rowId != null && _byRow.TryGetValue(rowId, out TreeSection section) ? section : null;
        }

        public static TreeRow Row(string rowId)
        {
            Index();
            return rowId != null && _rows.TryGetValue(rowId, out TreeRow row) ? row : null;
        }

        /// <summary>The section a feature's test tools are grouped under, or null.</summary>
        public static TreeSection SectionOfFeature(string featureId)
        {
            Index();
            return featureId != null && _byFeature.TryGetValue(featureId, out TreeSection section) ? section : null;
        }

        public static TreeGroup Group(string id)
        {
            foreach (TreeGroup group in Groups)
            {
                if (group.Id == id)
                {
                    return group;
                }
            }
            return null;
        }
    }
}
