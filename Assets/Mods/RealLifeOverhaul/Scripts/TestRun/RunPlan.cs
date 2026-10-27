using System.Collections.Generic;

namespace RealLifeOverhaul.TestRun
{
    /// <summary>
    /// The city expansion's own checks, as data: each test of the expansion that runs without the player's hands, the
    /// line that decides it, where the player must be and when. <see cref="RunList"/> gives each its work by its id; a
    /// feature added later joins with one <c>.Check(...)</c> in its guide instead (Meta/Features.cs). Game-free, so the
    /// offline check holds every id to a binding, every line to a source that writes it and every label to a text.
    ///
    /// Not here, with the reason (each is in the owner's journeys instead):
    /// - the trades' shifts (mason, electrician, crane operator, inspection, survey, night watch, a design and a tender at
    ///   the practice's desk): each is a game of the player's hands, the bar and the picks, and waits on the keys;
    /// - the trades' work at the nearest site: it opens the site's pages face to face, for the player to choose from;
    /// - Uptown's rent: the player rents the unit in BizMan with the game's own button.
    /// </summary>
    public static class RunPlan
    {
        // The run's own steps (no test button of a feature's): their ids, bound in RunList.
        public const string UptownOff = "testrun_uptown_off";
        public const string RoadOn = "testrun_road_on";
        public const string BuildingOn = "testrun_building_on";
        public const string BuildingMade = "testrun_building_made";
        public const string UptownBack = "testrun_uptown_back";
        public const string UptownLaid = "testrun_uptown_laid";
        public const string RoadAppended = "testrun_road_appended";
        public const string RoadDriven = "testrun_road_driven";
        public const string NextHour = "testrun_next_hour";
        public const string SitesBack = "testrun_sites_back";
        public const string RoadBack = "testrun_road_back";
        public const string BuildingBack = "testrun_building_back";
        public const string PlayerBack = "testrun_player_back";
        public const string LookBuilding = "testrun_look_building";

        /// <summary>The run's own lines for its put backs and its last look: written by RunList's steps.</summary>
        public const string SitesBackLine = "Test run (put back, sites)";
        public const string RoadBackLine = "Test run (put back, test road)";
        public const string BuildingBackLine = "Test run (put back, test building)";
        public const string PlayerBackLine = "Test run (put back, the player)";
        public const string LookBuildingLine = "Test run (for the eyes, test building)";

        /// <summary>The expansion's test buttons the run does not press, each with the reason (the owner's journeys cover them).</summary>
        public static readonly string[,] NotRun =
        {
            { "reallife_uptown_test_office_job", "it gives the player a job at an Uptown office, ending the one they hold" },
            { "reallife_bld_test_desk_talk", "it opens a builder's front desk face to face, for the player to choose from; every builder's desk pages are in the run" },
            { "reallife_uptown_test_special_go", "it only sets the destination at one special place a press; the special places' census is in the run" },
            { "reallife_uptown_test_dealer_sit", "it seats the player in a chair of the dealership's room, so the player must be inside it first; its purchase panel's check is in the run" },
            { "reallife_trades_test_mason", "a mason's shift is a game of the player's hands: the bar waits on the keys" },
            { "reallife_trades_test_electrician", "an electrician's shift waits on the player's picks" },
            { "reallife_trades_test_crane", "a crane operator's shift waits on the player's keys" },
            { "reallife_trades_test_inspector", "an inspection waits on the player's readings and the bribe's answer" },
            { "reallife_trades_test_surveyor", "a survey waits on the player's corners" },
            { "reallife_trades_test_security", "a night watch waits on the player's rounds" },
            { "reallife_trades_test_site_work", "it opens the site's pages face to face, for the player to choose from" },
            { "reallife_trades_test_design", "a design at the practice's desk is drawn by the player" },
            { "reallife_trades_test_tender", "a tender at the practice's desk is priced by the player" },
            { "reallife_uptown_test_rent", "the player rents the unit in BizMan with the game's own button" },
            { "reallife_uptown_test_store_order", "it needs a business of the player's that takes wholesale deliveries" },
            { "reallife_uptown_test_station_fill", "it needs the player at the wheel of a car of their own" },
            { "reallife_uptown_test_warehouse", "it needs the player at the wheel of a car of their own, and puts it through a warehouse's garage door and out again" },
            { "reallife_bld_test_demolish", "it knocks a building of the player's own down for good, so only the player presses it" },
            { "reallife_bld_mode_test_open", "it opens and shuts the city map over two seconds, which would hold the run's own steps, so only the player presses it" },
            { "reallife_bld_mode_test_bulldoze", "it needs an emptied Uptown building of the player's own, which the run never has" },
            { "reallife_bld_mode_test_stand", "it hands a building to the district for good, which only the player decides" },
            { "reallife_corner_test_stand", "it hands a corner building to the district on a free corner for good, which only the player decides" },
            { "reallife_grow_test_tenants", "it finishes a grown building in the district for good and lets its unit, which only the player decides" },
            { "reallife_bld_mode_test_layer", "it opens build mode over the city map and leaves it open for the player to look at, which would hold the run's own steps" },
            { "reallife_bld_mode_test_zoning", "it opens build mode over the city map and leaves it open for the player to look at, which would hold the run's own steps" },
            { "reallife_bld_mode_test_window", "it opens build mode over the city map and goes through every tool and view, which would hold the run's own steps" },
            { "reallife_bld_mode_test_hints", "it opens build mode over the city map and walks the hint bar through each tool, which would hold the run's own steps" },
            { "reallife_bld_mode_test_tools", "it opens build mode over the city map and leaves it open for the player to look at the toolbar, which would hold the run's own steps" },
            { "reallife_bld_mode_test_tour", "it opens build mode over the city map and walks the tour's tips, which would hold the run's own steps" },
            { "reallife_bld_mode_test_city", "it opens build mode and glides the city map over a district of the game's, which would hold the run's own steps" },
            { "reallife_bld_mode_test_cursor", "it opens build mode, sets the pointer over the map and a panel, and opens it again, which would hold the run's own steps" },
            { "reallife_bld_mode_test_plot", "it opens build mode, glides the city map over a building and leaves its panel open for the player to look at, which would hold the run's own steps" },
            { "reallife_bld_test_cleared_build", "it builds on ground the player cleared by knocking a building of theirs down, which the run never does" },
            { "reallife_bld_test_extend", "it puts a storey on a building of the player's own for good, so only the player presses it" },
            { "reallife_uptown_ground_test_surfaces", "it moves the player to the two places on 7th Street where the road flickered, which would carry the run off "
                + "its own places; the surfaces' check is read at every load" },
            { "reallife_bld_test_convert", "it changes a building of the player's own to another use for good, emptying it first, so only the player presses it" },
            { "reallife_bld_test_city_redevelop", "it has the city's developers knock down, build, extend and convert Uptown's own buildings for good, so only the player presses it" },
        };

        /// <summary>The expansion's test buttons the run presses through a step of its own (it asks for exactly what it needs), and that step.</summary>
        public static readonly string[,] ByStep =
        {
            { "reallife_newbuildings_test_make", BuildingOn },   // asked for on the open grass alone, and no longer asked for at the end
            { "reallife_newground_test_road", RoadOn },          // switched on only when off, and off again at the end
        };

        /// <summary>Every check of the table, in the order registered (a new list each call).</summary>
        public static List<RunCheck> All()
        {
            var all = new List<RunCheck>(72);

            // ---------------- before the load, where the player stands: reads first, then what changes the save
            Add(all, "reallife_held_test", "held", "Held buildings test: ", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_uptown_ground_test_census", "uptown", "Uptown check (census): ", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_uptown_ground_test_path", "uptown", "Uptown check (path, Player)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_uptown_test_census", "uptown", "Uptown check (census, the test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_uptown_test_business", "uptown", "Uptown check (a business opens)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_uptown_test_save", "uptown", "Uptown check (save and read back)", RunPlace.Here, RunPhase.BeforeLoad, 150f);
            Add(all, "reallife_test_district_save", "savetools", "District save test: ", RunPlace.Here, RunPhase.BeforeLoad, 150f);
            Add(all, "reallife_trades_test_commission", "buildtrades", "Trades check (commission)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_trades_test_bribe", "buildtrades", "Trades check (bribe):", RunPlace.Here, RunPhase.BeforeLoad, 30f);
            Add(all, "reallife_trades_test_apprentice", "buildtrades", "Trades check (apprentice)", RunPlace.Here, RunPhase.BeforeLoad);

            // The construction game's twenty, in its own order (its put back runs at the end).
            Add(all, "reallife_bld_test_lot", "construction", "Construction check (test lot)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_company", "construction", "Construction check (company)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_licence", "construction", "Construction check (licence)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_plans", "construction", "Construction check (plans)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_planning", "construction", "Construction check (planning test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_council", "construction", "Construction check (council)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_stage", "construction", "Construction check (stage)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_inspection", "construction", "Construction check (inspection test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_deliveries", "construction", "Construction check (deliveries)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_handover", "construction", "Construction check (handover test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_opening", "construction", "Construction check (opening test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_opening_missed", "construction", "Construction check (missed opening test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_tender", "construction", "Construction check (tender test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_award", "construction", "Construction check (award test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_developers", "construction", "Construction check (developers test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_poach", "construction", "Construction check (poach test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_strike", "construction", "Construction check (strike test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_fault", "construction", "Construction check (fault test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_bribe", "construction", "Construction check (bribe test)", RunPlace.Here, RunPhase.BeforeLoad);
            Add(all, "reallife_bld_test_buyer", "construction", "Construction check (buyer test)", RunPlace.Here, RunPhase.BeforeLoad);

            // ---------------- before the load, out of doors: first into buildings by the game's own doors (the next check takes the player out)
            Add(all, "reallife_floors_test_place", "diagnostics", "Floors check (floor)", RunPlace.Outdoors, RunPhase.BeforeLoad, 90f);
            Add(all, "reallife_trades_test_office", "buildtrades", "Trades check (practice floor)", RunPlace.Outdoors, RunPhase.BeforeLoad);
            Add(all, "reallife_newground_test_census", "newground", "Roads check (census)", RunPlace.Outdoors, RunPhase.BeforeLoad);
            Add(all, "reallife_newground_test_under", "newground", "Walk check (navmesh here)", RunPlace.Outdoors, RunPhase.BeforeLoad).Pass = null;
            RunCheck traffic = Add(all, "reallife_uptown_ground_test_traffic", "uptown", "Uptown check (traffic)", RunPlace.Outdoors, RunPhase.BeforeLoad, 300f);
            traffic.Look = "reallife_testrun_look_uptown";
            traffic.Eyes = "Uptown's streets, lights and crosswalks, and the join with Manhattan underfoot: the traffic check left you on 1st Avenue's north pavement at the foot of a "
                           + "street into Uptown; walk north with the keys.";

            // ---------------- before the load, at the street door of a building of three to six storeys
            RunCheck wrap = Add(all, "reallife_construction_test_wrap", "civic", "Construction check (wrap):", RunPlace.BesideBuilding, RunPhase.BeforeLoad);
            wrap.Look = "reallife_testrun_look_wrap";
            wrap.Eyes = "The game's scaffolding round a building of three to six storeys, up to its roof: the run stood you at that building's street door.";
            Add(all, "reallife_construction_test_rebuild", "civic", "Civic: site ", RunPlace.BesideBuilding, RunPhase.BeforeLoad).Pass = null;
            Add(all, "reallife_construction_test_stage", "civic", "Construction test: ", RunPlace.BesideBuilding, RunPhase.BeforeLoad).Pass = null;
            Add(all, "reallife_newground_test_parking", "newground", "Parking check (copy)", RunPlace.BesideBuilding, RunPhase.BeforeLoad);

            // ---------------- before the load, on the airport's open grass (each its own lot)
            Add(all, "reallife_construction_test_storey", "civic", "Construction test: the plot at", RunPlace.OpenGround, RunPhase.BeforeLoad).Pass = null;
            RunCheck crane = Add(all, "reallife_construction_test_crane", "civic", "Construction check (crane)", RunPlace.OpenGround, RunPhase.BeforeLoad);
            crane.Look = "reallife_testrun_look_plot";
            crane.Eyes = "The tower crane slewing, and the frame storey on another lot of the same grass: the run stood you at the front of an open lot on the airport's grass, facing the crane.";
            Add(all, "reallife_newbuildings_test_copy", "newbuildings", "Construction check (copy)", RunPlace.OpenGround, RunPhase.BeforeLoad);
            Add(all, "reallife_newbuildings_test_cost", "newbuildings", "Copy check (frame cost)", RunPlace.OpenGround, RunPhase.BeforeLoad);

            // ---------------- before the load, on the pavement at the grass's edge
            Add(all, "reallife_newground_test_patch", "newground", "Walk check (runtime build)", RunPlace.PavementEdge, RunPhase.BeforeLoad).Pass = "COMPLETE";
            Add(all, "reallife_newground_test_link", "newground", "Walk check (link)", RunPlace.PavementEdge, RunPhase.BeforeLoad);
            RunCheck grow = Add(all, "reallife_newground_test_grow", "newground", "Walk check (given back", RunPlace.PavementEdge, RunPhase.BeforeLoad, 150f);
            grow.Look = "reallife_testrun_look_walk";
            grow.Eyes = "Walking across the join onto the grown walking ground with the keys: the run stood you on the pavement at the edge of the airport's grass for a minute.";

            // ---------------- last before the load: stand Uptown down in play, another city and back, and ask for the load's own
            Add(all, UptownOff, "uptown", "Uptown check (census, the test)", RunPlace.Here, RunPhase.ForTheLoad, 30f);
            Add(all, "reallife_test_city_away", "diagnostics", "City check, as the test asks: another city", RunPlace.Here, RunPhase.ForTheLoad, 20f).Pass = null;
            Add(all, "reallife_test_city_back", "diagnostics", "City check: New York", RunPlace.Here, RunPhase.ForTheLoad, 20f).Pass = null;
            Add(all, RoadOn, "newground", "New ground: the test road is added", RunPlace.Here, RunPhase.ForTheLoad, 10f).Pass = null;
            Add(all, BuildingOn, "newbuildings", "New buildings test: the test building is made", RunPlace.Here, RunPhase.ForTheLoad, 10f).Pass = null;

            // ---------------- after the load: what the load wrote, then the test building's tests
            Load(all, BuildingMade, "newbuildings", "Made buildings check (test building", null);
            Load(all, UptownBack, "uptown", "Uptown check (census, the load)", null);
            Load(all, UptownLaid, "uptown", "Uptown: laid behind the loading screen", "Uptown: not laid at this load").Pass = null;
            Load(all, RoadAppended, "newground", "Roads check (append)", null);
            Add(all, "reallife_newbuildings_test_walk", "newbuildings", "Made buildings check (way out)", RunPlace.Outdoors, RunPhase.AfterLoad, 90f);
            Add(all, "reallife_newbuildings_test_save", "newbuildings", "Save check: the written save", RunPlace.Outdoors, RunPhase.AfterLoad, 150f);
            Add(all, "reallife_newbuildings_test_off", "newbuildings", "Made buildings check (switched off)", RunPlace.Outdoors, RunPhase.AfterLoad, 20f);
            Add(all, NextHour, "newbuildings", "Made buildings check (hidden, the next hour)", RunPlace.Outdoors, RunPhase.AfterLoad, 150f);
            RunCheck driven = Load(all, RoadDriven, "newground", "Roads check (test road)", null);
            driven.Place = RunPlace.Outdoors;
            driven.Seconds = 400f;
            driven.Pass = null;

            // ---------------- the put backs, last (and when the run is stopped), then the test building for the eyes
            Add(all, "reallife_bld_test_back", "construction", "Construction check (back)", RunPlace.Here, RunPhase.End);
            Add(all, SitesBack, "civic", SitesBackLine, RunPlace.Here, RunPhase.End, 20f).Pass = null;
            Add(all, RoadBack, "newground", RoadBackLine, RunPlace.Here, RunPhase.End, 10f).Pass = null;
            Add(all, BuildingBack, "newbuildings", BuildingBackLine, RunPlace.Here, RunPhase.End, 10f).Pass = null;
            Add(all, PlayerBack, "testrun", PlayerBackLine, RunPlace.Here, RunPhase.End, 40f).Pass = null;
            RunCheck look = Add(all, LookBuilding, "newbuildings", LookBuildingLine, RunPlace.TestBuilding, RunPhase.End, 10f);
            look.Pass = null;
            look.Look = "reallife_testrun_look_building";
            look.Eyes = "The test building at 1 Proving Lane, and walking in and out by its door with the keys: the run ends with you at its door.";
            return all;
        }

        private static RunCheck Add(List<RunCheck> all, string id, string feature, string line, RunPlace place, RunPhase phase, float seconds = RunRules.DefaultSeconds)
        {
            var check = new RunCheck(id, feature, line, place, phase, seconds) { Order = all.Count };
            all.Add(check);
            return check;
        }

        // A line the load writes: looked for among what was heard since the load began, then waited for a little.
        private static RunCheck Load(List<RunCheck> all, string id, string feature, string line, string fails)
        {
            RunCheck check = Add(all, id, feature, line, RunPlace.Here, RunPhase.AfterLoad, 60f);
            check.AtLoad = true;
            check.Fails = fails;
            return check;
        }

        /// <summary>True for an id of the run's own steps (not a feature's test button).</summary>
        public static bool Own(string id)
        {
            return id != null && id.StartsWith("testrun_", System.StringComparison.Ordinal);
        }
    }
}
