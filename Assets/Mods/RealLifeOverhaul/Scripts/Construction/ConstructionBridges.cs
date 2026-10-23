using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A design of the city's own that fits some ground, as the copy engine lists it (Scripts/NewBuildings).</summary>
    public struct DesignChoice
    {
        public string Name;
        public string Size;
        public int Version;
        public string Type;
        public float Height;    // m
        public float Width;     // m along its door's side
        public float Depth;     // m back from it
        public int TotalSqm;    // the floor area of the city's own building it is measured on
    }

    /// <summary>What a site of the city's programme is, for a tender: its kind, address, hours and place.</summary>
    public struct SiteFacts
    {
        public int Id;
        public int Kind;        // the programme's kind (Civic's SiteKind)
        public string Street;
        public int Number;
        public float Hours;     // its working hours
        public float Done;
        public Vector3 At;
        public int Contractor;
        public bool Known;      // the site is in the programme
    }

    /// <summary>
    /// What the construction game asks of the rest of the mod, each with a default so it works alone; Wiring/Wiring.ConstructionLinks
    /// fills them in. The district's lots and buildings (Scripts/Uptown), the city's sites (Scripts/Civic), the copy engine's
    /// designs (Scripts/NewBuildings), the credit side and the raw materials exchange (Economy, Markets), the record, the
    /// courts, Flare and Standing, the weather, and the trades' careers (Scripts/BuildTrades). Each is asked on an hour, a day,
    /// a page or a press, never per frame.
    /// </summary>
    public static class ConstructionBridges
    {
        // ------------------------------------------------------------------ the district

        /// <summary>The district's lots at this load, into the caller's list (cleared). Default: none, so no land is for sale.</summary>
        public static Action<List<LotView>> Lots;

        /// <summary>Changes whenever a lot opens, closes or holds a building now. Default: never.</summary>
        public static Func<int> LotsStamp;

        /// <summary>True while the district is a district of the game's at this load. Default: no.</summary>
        public static Func<bool> DistrictLives;

        /// <summary>True when a building of the district stands on a lot, or one finished waits for the next load. Default: no.</summary>
        public static Func<string, bool> LotTaken;

        /// <summary>True when a building of the district stands on a lot at this load. Default: no.</summary>
        public static Func<string, bool> LotBuilt;

        /// <summary>True when a lot of the district takes a building of one of the game's sizes (its class's sizes). Default: any size.</summary>
        public static Func<string, string, bool> SizeTaken;

        /// <summary>The district's price a square metre (its base price times its real estate multiplier). Default: not known.</summary>
        public static Func<double> LandPrice;

        /// <summary>A finished building handed to the district: from the next load it is one of its buildings, the player's or the city's.</summary>
        public static FinishCall Finish;

        public delegate bool FinishCall(string lot, string design, string size, int version, string type, int storeys, bool player, double price, out string why);

        // ------------------------------------------------------------------ the copy engine

        /// <summary>A building's footprint as the copy engine measures it, with its slack round it: m along its door's side and back from it. Default: not known.</summary>
        public static FootprintCall Footprint;

        public delegate bool FootprintCall(string street, int number, out float width, out float depth);

        /// <summary>The designs that fit ground for a type of the game's, the best covering first; false while they are being looked through. Default: none.</summary>
        public static DesignList Designs;

        public delegate bool DesignList(float width, float depth, string type, int most, List<DesignChoice> into);

        // ------------------------------------------------------------------ the city's sites

        /// <summary>A new building's site on a lot's ground: its middle, yaw, width and depth, storeys, contractor and hours. The site's id, or 0 with why.</summary>
        public static PlotStart StartPlot;

        public delegate int PlotStart(Vector3 middle, int yaw, int width, int depth, int storeys, int contractor, int hours, out string why);

        /// <summary>A site of a kind at one of the city's buildings (a rebuild, a scaffold), the player's own or a contractor's. The site's id, or 0 with why.</summary>
        public static WorksStart StartWorks;

        public delegate int WorksStart(string street, int number, int kind, bool mine, int contractor, int hours, out string why);

        /// <summary>What a site of the programme is now. Default: not known.</summary>
        public static Func<int, SiteFacts> Site;

        /// <summary>A site's contractor set (a rival who won its tender). Default: nothing.</summary>
        public static Action<int, int> SetContractor;

        /// <summary>A site handed over now, its works done. False for one not in the programme. Default: no.</summary>
        public static Func<int, bool> FinishSite;

        /// <summary>A site taken out of the programme with nothing handed over. Default: no.</summary>
        public static Func<int, bool> DropSite;

        /// <summary>How many sites the programme holds now, every builder's included. Default: none.</summary>
        public static Func<int> SiteCount;

        /// <summary>Test: a site of the programme moves on to its next stage now; false once its last has handed it over. Default: no.</summary>
        public static Func<int, bool> AdvanceSite;

        /// <summary>Test: whether a lot's ground is clear of buildings, with the ground's height at its middle. Default: no.</summary>
        public static GroundCall Ground;

        public delegate bool GroundCall(Vector3 middle, int yaw, int width, int depth, out float ground);

        /// <summary>Where the council's permits policy stands, -1 to 1. Default: 0.</summary>
        public static Func<int> Permits;

        // ------------------------------------------------------------------ money

        /// <summary>The business loan's rate at the player's rating, per cent a year; NaN for none. Default: none.</summary>
        public static Func<double> BusinessRate;

        /// <summary>The bank that lends, by name. Default: none (the text says "the bank").</summary>
        public static Func<string> BankName;

        /// <summary>Tells the credit file of a loan drawn (0, with the amount) or repaid in full (1). Default: nothing.</summary>
        public static Action<int, int> Credit;

        /// <summary>A raw material's price on the exchange against its own (by the game's item name); NaN for none. Default: none.</summary>
        public static Func<string, double> MaterialFactor;

        // ------------------------------------------------------------------ the record, the courts, the paper, Standing

        /// <summary>True while a conviction counts against the player. Default: no.</summary>
        public static Func<bool> Record;

        /// <summary>True while the player has a lawyer of their own. Default: no.</summary>
        public static Func<bool> Lawyer;

        /// <summary>A civil claim filed against the player for faults: the amount, the claimant's name, the building. Its number, or 0. Default: none.</summary>
        public static Func<long, string, string, int> Claim;

        /// <summary>A case opened for a bribe to an inspector that came out: the amount. Its number, or 0. Default: none.</summary>
        public static Func<int, int> Bribery;

        /// <summary>The player's own moment on Flare: the place and the building's name (an opening). Default: nothing.</summary>
        public static Action<string, string> Opened;

        /// <summary>Standing moved: points of recognition, and whether it is a scandal. Default: nothing.</summary>
        public static Action<int, bool> Standing;

        /// <summary>True while a storm is on (damage to a frame). Default: no.</summary>
        public static Func<bool> Storm;

        // ------------------------------------------------------------------ the trades' careers

        /// <summary>True when the player is qualified for a role of the trades (by its id: "architect", "engineer"). Default: no.</summary>
        public static Func<string, bool> Qualified;

        /// <summary>A brief put to the player at the architects' office: the project's id, storeys, units and budget. Default: none.</summary>
        public static Action<string, int, int, long> Commission;

        /// <summary>The player's own design for a project, once finished: its score (0 to 100), its cost against the budget in thousandths, its storeys and its flats.</summary>
        public static DesignAnswer DesignOf;

        public delegate bool DesignAnswer(string projectId, out int total, out int costPermille, out int storeys, out int units);

        // ------------------------------------------------------------------ the district's landmarks (Scripts/Landmarks)

        /// <summary>
        /// Each landmark's site with its ground settled, as a lot of the construction game's (its id the site's, its ground
        /// the works' plot), into the caller's list (cleared). Default: none.
        /// </summary>
        public static Action<List<LotView>> LandmarkSites;

        /// <summary>True for a lot of the district a landmark's site holds: not for sale, never a development's. Default: no.</summary>
        public static Func<string, bool> LandmarkHeld;

        /// <summary>A landmark's name by its site's id, in the player's language; null for none. Default: none.</summary>
        public static Func<string, string> LandmarkName;

        /// <summary>Where a landmark stands, in words for the Land tab (out to tender, being built by whom and when it is due, open), by its site's id; null for none. Default: none.</summary>
        public static Func<string, string> LandmarkWords;

        /// <summary>The design a landmark's main building wears, by its site's id; false for none (the stadium's is the mod's own) or not known yet. Default: none.</summary>
        public static LandmarkDesignCall LandmarkDesign;

        public delegate bool LandmarkDesignCall(string site, out DesignChoice design);

        /// <summary>A landmark's tender awarded: its site, the winner (-1 the player, else a rival), the price and the days bid. Default: nothing.</summary>
        public static Action<string, int, long, int> LandmarkAwarded;

        /// <summary>The player's company handed a landmark over: its site's id. True once the landmark has it. Default: no.</summary>
        public static Func<string, bool> LandmarkHanded;

        /// <summary>The landmark's site a site of the programme shows, when its works are a rival's or the city's own; null for none. Default: none.</summary>
        public static Func<int, string> LandmarkWorks;

        /// <summary>The hours a landmark's own works site does in one of its working hours (its timetable's pace). Default: as given.</summary>
        public static Func<int, double, double, float, float> LandmarkPace;

        /// <summary>True when a landmark's site needs no look of the programme's on it now (the stadium's own, or the district's building standing). Default: no.</summary>
        public static Func<string, bool> LandmarkBuilt;

        public static void Reset()
        {
            Lots = null;
            LotsStamp = null;
            DistrictLives = null;
            LotTaken = null;
            LotBuilt = null;
            SizeTaken = null;
            LandPrice = null;
            Finish = null;
            Designs = null;
            Footprint = null;
            StartPlot = null;
            StartWorks = null;
            Site = null;
            SetContractor = null;
            FinishSite = null;
            DropSite = null;
            SiteCount = null;
            AdvanceSite = null;
            Ground = null;
            Permits = null;
            BusinessRate = null;
            BankName = null;
            Credit = null;
            MaterialFactor = null;
            Record = null;
            Lawyer = null;
            Claim = null;
            Bribery = null;
            Opened = null;
            Standing = null;
            Storm = null;
            Qualified = null;
            Commission = null;
            DesignOf = null;
            LandmarkSites = null;
            LandmarkHeld = null;
            LandmarkName = null;
            LandmarkWords = null;
            LandmarkDesign = null;
            LandmarkAwarded = null;
            LandmarkHanded = null;
            LandmarkWorks = null;
            LandmarkPace = null;
            LandmarkBuilt = null;
        }
    }
}
