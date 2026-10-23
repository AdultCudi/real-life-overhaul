using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>How a trade's shift is played: the game's panel and bar for each (TradeJob, DesignJob, TenderJob).</summary>
    public enum TradeMode
    {
        Timing,    // a press while the marker is in the zone on the game's bar: a brick laid, a spit dug, a load set down
        Match,     // the right one of three buttons before the time runs out: a wire to its terminal, a fitting to the run
        Survey,    // walk to each corner the game's marker shows, then a level reading on the bar
        Judge,     // sound or fault for each item of a checklist, and the contractor's bribe taken or not
        Watch,     // a round of the site after hours: a noise somewhere, got to before the thief gets away
        Design,    // at the architects' office: a floor plan, a facade and a frame (Scripts/BuildTrades/DesignRules)
        Tender     // at the architects' office: a tender priced item by item against the clock (TenderRules)
    }

    /// <summary>What a trade or a rank asks: any one of some courses passed, or so many points of a skill (learnt on the job).</summary>
    public sealed class TradeNeed
    {
        public readonly int[] Courses;   // StudyCatalogue ids, any one of them
        public readonly int Skill;       // Careers' Skills or the professions' skills, -1 none
        public readonly int Points;      // ...and the points of it that will do instead; 0: only a course will
        public readonly string LacksKey; // the sentence of what is missing, as a career's boss or the site's notice says it

        public TradeNeed(string lacksKey, int skill, int points, params int[] courses)
        {
            LacksKey = lacksKey;
            Skill = skill;
            Points = points;
            Courses = courses ?? new int[0];
        }
    }

    /// <summary>One building trade: the job it is (JobCatalogue's id), its role for a construction company, where and how it is worked.</summary>
    public sealed class TradeDef
    {
        public readonly string Id;          // JobCatalogue's id, also the role id a construction company gives its staff
        public readonly TradeMode Mode;
        public readonly int Kinds;          // the kinds of works site it is on, a bit each (Civic's SiteKind: roadworks 0 to plot 5); 0 at the office
        public readonly int Phases;         // ...in these of a site's five stages, a bit each
        public readonly bool AfterHours;    // only while the site is not working (night security)
        public readonly TradeNeed Needs;    // null: open to anyone
        public readonly int Learns;         // the skill a unit of it teaches (Careers' skills), -1 none
        public readonly int LearnEach;      // thousandths of a point a unit teaches
        public readonly int Helps;          // the skill whose points make its game kinder (a wider zone, more time), -1 none
        public readonly int Strokes;        // presses, picks or items a unit (a stretch of wall is eight bricks)
        public readonly float Sweep;        // seconds the marker takes across the bar and back (Timing, Survey)
        public readonly float Zone;         // the zone's share of the bar at no skill
        public readonly float Seconds;      // seconds a pick has (Match), or a noise is heard for (Watch)
        public readonly string Clip;        // the mod's clip for a stroke (CityAssets), best first...
        public readonly string ClipElse;    // ...and the one to use while the bundle lacks it
        public readonly bool Painter;       // dressed as the city's painters, not in the hi-vis

        public TradeDef(string id, TradeMode mode, int kinds, int phases, TradeNeed needs, int learns, int learnEach, int helps, int strokes, float sweep, float zone,
            float seconds, string clip, string clipElse, bool afterHours = false, bool painter = false)
        {
            Id = id;
            Mode = mode;
            Kinds = kinds;
            Phases = phases;
            Needs = needs;
            Learns = learns;
            LearnEach = learnEach;
            Helps = helps;
            Strokes = strokes;
            Sweep = sweep;
            Zone = zone;
            Seconds = seconds;
            Clip = clip;
            ClipElse = clipElse;
            AfterHours = afterHours;
            Painter = painter;
        }

        public string NameKey => TradeCatalogue.Key + Id + "_name";
        public bool AtOffice => Kinds == 0;

        /// <summary>True if it is on at a site of this kind in this stage.</summary>
        public bool OnAt(int kind, int phase) => kind >= 0 && kind < 31 && (Kinds & (1 << kind)) != 0 && phase >= 0 && phase < 31 && (Phases & (1 << phase)) != 0;
    }

    /// <summary>A role a construction company gives its people (Scripts/Construction), the same id as the trade or rank it stands for.</summary>
    public sealed class TradeRole
    {
        public readonly string Id;
        public readonly TradeNeed Needs;   // null: anyone

        public TradeRole(string id, TradeNeed needs)
        {
            Id = id;
            Needs = needs;
        }
    }

    /// <summary>
    /// Every building trade and what each asks, game-free (EconomyCheck runs it). The pay of each is in Jobs/JobCatalogue's
    /// Trades; the courses in Careers/Study/StudyCatalogue (bricklaying, the crane ticket, construction management and the
    /// architect's licence are the trades' own, the rest the city's). A site's kind is Civic's: 0 roadworks, 1 scaffold, 2 a
    /// shop's refit, 3 a utility's works, 4 a rebuild, 5 a new building on a plot.
    /// </summary>
    public static class TradeCatalogue
    {
        /// <summary>The start of the trades' text keys; a key is this and the trade's or the word's own part.</summary>
        public const string Key = "reallife" + "_trades_";

        public const int Roadworks = 1 << 0, Scaffold = 1 << 1, Refit = 1 << 2, Utility = 1 << 3, Rebuild = 1 << 4, Plot = 1 << 5, AnySite = 63;
        public const int AllPhases = 31, Early = 1 | 2, Middle = 2 | 4 | 8, Late = 8 | 16, NotFirst = 2 | 4 | 8 | 16;

        // What the trades and ranks ask. A sentence each, said where the player is refused.
        public static readonly TradeNeed Roofing = new TradeNeed("reallife_trades_needs_roofer", Skills.Joinery, 20, StudyCatalogue.Carpenter, StudyCatalogue.Bricklaying);
        public static readonly TradeNeed Wiring = new TradeNeed("reallife_trades_needs_electrician", Skills.Electrical, 35, StudyCatalogue.Electrician);
        public static readonly TradeNeed Piping = new TradeNeed("reallife_trades_needs_plumber", Skills.Plumbing, 35, StudyCatalogue.Plumber);
        public static readonly TradeNeed Ticket = new TradeNeed("reallife_trades_needs_crane", -1, 0, StudyCatalogue.CraneTicket);
        public static readonly TradeNeed Building = new TradeNeed("reallife_trades_needs_inspector", -1, 0,
            StudyCatalogue.ConstructionManagement, StudyCatalogue.CivilEngineering, StudyCatalogue.Architecture);
        public static readonly TradeNeed Surveying = new TradeNeed("reallife_trades_needs_surveyor", -1, 0, StudyCatalogue.CivilEngineering, StudyCatalogue.ConstructionManagement);
        public static readonly TradeNeed Guarding = new TradeNeed("reallife_trades_needs_security", Skills.Guarding, 30, StudyCatalogue.SecurityLicence);
        public static readonly TradeNeed Designing = new TradeNeed("reallife_trades_needs_architect", -1, 0, StudyCatalogue.Architecture);
        public static readonly TradeNeed Licensed = new TradeNeed("reallife_trades_needs_licence", -1, 0, StudyCatalogue.ArchitectLicence);
        public static readonly TradeNeed Engineering = new TradeNeed("reallife_trades_needs_engineer", -1, 0, StudyCatalogue.CivilEngineering, StudyCatalogue.CharteredEngineer);
        public static readonly TradeNeed Pricing = new TradeNeed("reallife_trades_needs_estimator", TalentCatalogue.Accounting, 25,
            StudyCatalogue.ConstructionManagement, StudyCatalogue.CivilEngineering, StudyCatalogue.Bookkeeping);
        // The builder's ladder: a mason by the course or by the labourer's work, a foreman with more of it, a site manager by a degree.
        public static readonly TradeNeed MasonRank = new TradeNeed("reallife_trades_needs_mason_rank", Skills.Masonry, 20, StudyCatalogue.Bricklaying);
        public static readonly TradeNeed ForemanRank = new TradeNeed("reallife_trades_needs_foreman", Skills.Masonry, 35, StudyCatalogue.ConstructionManagement);
        public static readonly TradeNeed ManagerRank = new TradeNeed("reallife_trades_needs_site_manager", -1, 0, StudyCatalogue.ConstructionManagement, StudyCatalogue.CivilEngineering);

        public static readonly TradeDef[] All =
        {
            new TradeDef(JobCatalogue.Labourer, TradeMode.Timing, Roadworks | Utility | Rebuild | Plot, AllPhases, null, Skills.Masonry, 250, Skills.Handling, 4, 1.7f, 0.30f, 0f,
                "shovel_dig", "kneel_work"),
            new TradeDef(JobCatalogue.Mason, TradeMode.Timing, Rebuild | Plot, NotFirst, null, Skills.Masonry, 500, Skills.Masonry, 8, 1.5f, 0.24f, 0f, "brick_lay", "hammer_nail"),
            new TradeDef(JobCatalogue.Scaffolder, TradeMode.Timing, Scaffold | Rebuild | Plot, AllPhases, null, Skills.Handling, 300, Skills.Handling, 6, 1.4f, 0.26f, 0f,
                "hammer_nail", "kneel_work"),
            new TradeDef(JobCatalogue.Painter, TradeMode.Timing, Scaffold | Refit | Rebuild, Middle | 16, null, Skills.Joinery, 150, -1, 6, 1.9f, 0.32f, 0f, "paint_roll", "kneel_work",
                painter: true),
            new TradeDef(JobCatalogue.Roofer, TradeMode.Timing, Rebuild | Plot, Late, Roofing, Skills.Joinery, 400, Skills.Joinery, 6, 1.3f, 0.24f, 0f, "hammer_nail", "kneel_work"),
            new TradeDef(JobCatalogue.Electrician, TradeMode.Match, Refit | Utility | Rebuild | Plot, Middle | 16, Wiring, Skills.Electrical, 450, Skills.Electrical, 4, 0f, 0f, 4.0f,
                "kneel_work", "inspect_look"),
            new TradeDef(JobCatalogue.Plumber, TradeMode.Match, Refit | Utility | Rebuild | Plot, Middle | 16, Piping, Skills.Plumbing, 450, Skills.Plumbing, 4, 0f, 0f, 4.0f,
                "kneel_work", "inspect_look"),
            new TradeDef(JobCatalogue.Crane, TradeMode.Timing, Plot, NotFirst, Ticket, Skills.Handling, 250, Skills.Handling, 1, 2.6f, 0.16f, 0f, "radio_talk", "point_direct"),
            new TradeDef(JobCatalogue.Inspector, TradeMode.Judge, AnySite, AllPhases, Building, TalentCatalogue.Structures, 200, -1, 1, 0f, 0f, 0f, "inspect_look", "clipboard_write"),
            new TradeDef(JobCatalogue.Surveyor, TradeMode.Survey, Plot | Rebuild, Early, Surveying, TalentCatalogue.Planning, 400, TalentCatalogue.Planning, 1, 2.2f, 0.22f, 0f,
                "map_read", "clipboard_write"),
            new TradeDef(JobCatalogue.Security, TradeMode.Watch, Roadworks | Scaffold | Rebuild | Plot, AllPhases, Guarding, Skills.Guarding, 300, -1, 1, 0f, 0f, 28f,
                "guard_stand", "arms_crossed", afterHours: true),
            new TradeDef(JobCatalogue.Architect, TradeMode.Design, 0, AllPhases, Designing, TalentCatalogue.Planning, 600, -1, 1, 0f, 0f, 75f, "desk_plan", "clipboard_write"),
            new TradeDef(JobCatalogue.Draughting, TradeMode.Design, 0, AllPhases, null, TalentCatalogue.Planning, 300, -1, 1, 0f, 0f, 60f, "desk_plan", "clipboard_write"),
            new TradeDef(JobCatalogue.Engineer, TradeMode.Design, 0, AllPhases, Engineering, TalentCatalogue.Structures, 600, -1, 1, 0f, 0f, 60f, "desk_plan", "clipboard_write"),
            new TradeDef(JobCatalogue.Estimator, TradeMode.Tender, 0, AllPhases, Pricing, TalentCatalogue.Accounting, 400, -1, 5, 0f, 0f, 70f, "take_notes", "clipboard_write")
        };

        /// <summary>
        /// The roles a construction company (Scripts/Construction) may give its people and a player, by the same id: each trade,
        /// and the builder's and the practice's ranks. Stable strings: a saved role reads the same on both sides.
        /// </summary>
        public static readonly TradeRole[] Roles =
        {
            new TradeRole(JobCatalogue.Labourer, null),
            new TradeRole(JobCatalogue.Mason, null),
            new TradeRole("foreman", ForemanRank),
            new TradeRole("site_manager", ManagerRank),
            new TradeRole(JobCatalogue.Draughting, null),
            new TradeRole(JobCatalogue.Architect, Designing),
            new TradeRole("licensed_architect", Licensed),
            new TradeRole(JobCatalogue.Engineer, Engineering),
            new TradeRole("crane_operator", Ticket),
            new TradeRole(JobCatalogue.Inspector, Building),
            new TradeRole(JobCatalogue.Surveyor, Surveying),
            new TradeRole("security", Guarding),
            new TradeRole(JobCatalogue.Painter, null),
            new TradeRole(JobCatalogue.Scaffolder, null),
            new TradeRole(JobCatalogue.Roofer, Roofing),
            new TradeRole(JobCatalogue.Electrician, Wiring),
            new TradeRole(JobCatalogue.Plumber, Piping),
            new TradeRole(JobCatalogue.Estimator, Pricing),
            new TradeRole("mason_rank", MasonRank)
        };

        public static TradeDef Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static TradeRole Role(string id)
        {
            for (int i = 0; i < Roles.Length; i++)
            {
                if (Roles[i].Id == id)
                {
                    return Roles[i];
                }
            }
            return null;
        }

        /// <summary>The trades on at a site of this kind and stage now, a bit each by their place in <see cref="All"/> (after hours: the night's only).</summary>
        public static int OnAt(int kind, int phase, bool working)
        {
            int on = 0;
            for (int i = 0; i < All.Length && i < 31; i++)
            {
                TradeDef t = All[i];
                if (!t.AtOffice && t.OnAt(kind, phase) && t.AfterHours != working)
                {
                    on |= 1 << i;
                }
            }
            return on;
        }

        /// <summary>The trades of the architects' office, a bit each by their place in <see cref="All"/>.</summary>
        public static int AtOffice()
        {
            int on = 0;
            for (int i = 0; i < All.Length && i < 31; i++)
            {
                on |= All[i].AtOffice ? 1 << i : 0;
            }
            return on;
        }
    }
}
