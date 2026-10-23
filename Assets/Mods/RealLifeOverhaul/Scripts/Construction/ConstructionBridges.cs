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
    /// courts, Flare and Standing, the weather, the trades' careers (Scripts/BuildTrades), and the stock market's builders and
    /// futures (Scripts/Markets). Each is asked on an hour, a day, a page or a press, never per frame.
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

        /// <summary>
        /// A raw material's move on the exchange against its own price at the city's price level (by the exchange's name),
        /// since a load's list price already follows the price level; NaN for none. Default: none.
        /// </summary>
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

        // ------------------------------------------------------------------ the stock market

        /// <summary>
        /// A rival builder's news for the stock market: its number (-1 the player's company winning a tender), what happened
        /// (Markets.BuilderNewsKind's number) and a weight from 1 to 3 by the works' size. Default: nothing.
        /// </summary>
        public static Action<int, int, int> RivalNews;

        /// <summary>A hedge of a project's purchases to come as it would be bought now, its futures into the list. False while the exchange cannot hedge. Default: none.</summary>
        public static HedgePlanCall HedgePlan;

        public delegate bool HedgePlanCall(List<MaterialNeed> needs, List<HedgeLine> lines, out HedgeQuote quote);

        /// <summary>A hedge bought now (a test's by the test tools): its futures' ids into the list; how many, with why not as a text key. Default: none.</summary>
        public static HedgeCall Hedge;

        public delegate int HedgeCall(List<MaterialNeed> needs, bool test, List<int> opened, out string whyKey);

        /// <summary>A future of a hedge as the exchange's book has it now; false while the exchange is not running. Default: not running.</summary>
        public static ContractCall Contract;

        public delegate bool ContractCall(int id, out HedgeLook look);

        /// <summary>Test: the given futures settle now at today's prices. How many. Default: none.</summary>
        public static Func<List<int>, int> SettleTest;

        /// <summary>Test: every future the test tools bought undone, its money given back. How many. Default: none.</summary>
        public static Func<int> UndoTests;

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
            RivalNews = null;
            HedgePlan = null;
            Hedge = null;
            Contract = null;
            SettleTest = null;
            UndoTests = null;
        }
    }
}
