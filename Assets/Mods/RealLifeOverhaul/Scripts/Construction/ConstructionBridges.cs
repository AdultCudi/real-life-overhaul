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

    /// <summary>A show flat's design as the construction game reads it (Scripts/Designs, scored by Scripts/Interiors).</summary>
    public struct ShowDesignView
    {
        public string Id;          // Designs' id for it, kept in the project's line
        public string Name;        // in the player's language: the game's design and its finish
        public string Size;        // the game's size id of the apartment structure it stands on
        public int Version;
        public int Sqm;            // that structure's floor area
        public string Design;      // the game's design's file name, laid by the floors core
        public int Interior;       // the game's interior score of its walls and floors
        public int Decor;          // Interiors' decor score of its pieces
        public long Furniture;     // its pieces at the game's market prices
        public long Fee;           // the installation firm's fee
        public int Pieces;
        public Func<string, bool> Keeps;   // the pieces of the game's design it keeps, by their id
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

        /// <summary>True for a lot of an earlier plan of the district's, which is no more: what a save holds on it is retired. Default: none.</summary>
        public static Func<string, bool> LotRetired;

        /// <summary>True when a lot of the district takes a building of one of the game's sizes (its class's sizes). Default: any size.</summary>
        public static Func<string, string, bool> SizeTaken;

        /// <summary>The district's price a square metre (its base price times its real estate multiplier). Default: not known.</summary>
        public static Func<double> LandPrice;

        /// <summary>A finished building handed to the district: from the next load it is one of its buildings, the player's or the city's.</summary>
        public static FinishCall Finish;

        public delegate bool FinishCall(string lot, string design, string size, int version, string type, int storeys, bool player, double price, out string why);

        /// <summary>A finished building handed to the district standing over a site's other spaces as well as its first: one building, one address. Default: none.</summary>
        public static FinishOverCall FinishOver;

        public delegate bool FinishOverCall(string lot, IList<string> over, string design, string size, int version, string type, int storeys, bool player, double price, out string why);

        /// <summary>The spaces of the district's grid a size and version takes, 0 for one off the grid. Default: none known.</summary>
        public static Func<string, int, int> SpacesOf;

        /// <summary>A rival builder's office (its index among the city's contractors) as the game writes its address, or null when it has none standing at this load. Default: none.</summary>
        public static Func<int, string> RivalOffice;

        /// <summary>The game's own destination set at a rival builder's office; false when it has none standing. Default: no.</summary>
        public static Func<int, bool> RivalOfficeGo;

        /// <summary>The rival builder whose office the player is inside now, while it may receive them (its own, nobody's to rent or own); -1 elsewhere. Default: none.</summary>
        public static Func<int> RivalHere;

        /// <summary>The rival builder whose standing office's door is nearest a point on the ground (m); -1 when none stands. Default: none.</summary>
        public static Func<float, float, int> RivalNearest;

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

        /// <summary>A site's works set to so many of its hours done (a project's site set out again where its works had reached). Default: nothing.</summary>
        public static Action<int, float> SiteDone;

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

        // ------------------------------------------------------------------ the city's council and its courts

        /// <summary>A change of the permits policy on its way (its position and day) and the next election's day; false for no change. Default: none.</summary>
        public static CodeChange CodeAhead;

        public delegate bool CodeChange(out int position, out int day, out int election);

        /// <summary>The day the city's one council next hears planning business, from a day. Default: a Thursday's sitting (LandRules.CouncilDay).</summary>
        public static Func<int, int> CouncilDay;

        /// <summary>The council hears a zoning application: 1 carried, 0 refused, -1 before it still (a meeting the player sits in). Default: the planners' own odds.</summary>
        public static CouncilHearing Council;

        public delegate int CouncilHearing(string lot, int lotClass, int use, int suit, int reputation, bool now, out int ayes, out int noes);

        /// <summary>A civil claim the company brings for a debt: the amount, the defendant in words, its strength in thousandths. Its number, or 0. Default: none.</summary>
        public static Func<long, string, int, int> Sue;

        /// <summary>Where a claim the company brought stands: -1 before the court, 1 won (paid), 0 lost, -2 not known. Default: not known.</summary>
        public static Func<int, int> SuitResult;

        /// <summary>A civil claim against the player for a debt of the company's: the amount, the claimant, what it is for. Its number, or 0. Default: none.</summary>
        public static Func<long, string, string, int> Unpaid;

        /// <summary>A case opened for bid rigging that came out: the cut taken. Its number, or 0. Default: none.</summary>
        public static Func<long, int> BidRigging;

        /// <summary>Test: a case (0), a claim against the player (1) or a claim of the company's (2) taken back unheard. Default: no.</summary>
        public static Func<int, int, bool> Withdraw;

        /// <summary>The player's followers on Flare, for the city's vote on the building of the year. Default: none.</summary>
        public static Func<int> Followers;

        /// <summary>True while the courts take cases and claims (the justice system running). Default: no.</summary>
        public static Func<bool> CourtOpen;

        /// <summary>Test: the court hears one claim of the company's now; false for none to hear. Default: no.</summary>
        public static Func<int, bool> HearClaim;

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

        /// <summary>The day a claim against the player (its number) must be answered by while the court has it; -1 once it has not. Default: -1.</summary>
        public static Func<int, int> ClaimDue;

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

        // ------------------------------------------------------------------ show flats and refits: designs, interiors, the businesses

        /// <summary>The show flat designs for flats of a floor area (Scripts/Designs, scored by Interiors), into the caller's list (cleared); false for none. Default: none.</summary>
        public static ShowList ShowDesigns;

        public delegate bool ShowList(int flatArea, List<ShowDesignView> into);

        /// <summary>A show flat's design by its id; false for one the game's files no longer give. Default: none.</summary>
        public static ShowFind ShowDesign;

        public delegate bool ShowFind(string id, out ShowDesignView design);

        /// <summary>The standard a district of the game's expects of an interior (Interiors), 0 to 100; -1 for not known. Default: not known.</summary>
        public static Func<string, int> Standard;

        /// <summary>A refit done at a business of the player's (street, number, the new fittings' grade): Interiors' wear and decor. False when not recorded. Default: no.</summary>
        public static Func<string, int, int, bool> Refitted;

        /// <summary>A business of the player's kept closed for its refit so many hours more (business pressure's closure). False when it cannot be. Default: no.</summary>
        public static Func<string, int, int, bool> CloseShop;

        /// <summary>A refit's closure of a business ended (it opens unless something else of the mod's keeps it closed). Default: nothing.</summary>
        public static Action<string, int> OpenShop;

        /// <summary>A business's decor score, the points a refit still gives it and its wear now (Interiors). Default: not known.</summary>
        public static InteriorRead Interior;

        public delegate bool InteriorRead(string street, int number, out int decor, out int refit, out int wear);

        /// <summary>Test: a business's interior record kept as it is now, to be put back. Default: nothing.</summary>
        public static Action<string, int> KeepInterior;

        /// <summary>Test: every interior record kept put back as it was; how many. Default: none.</summary>
        public static Func<int> InteriorsBack;

        public static void Reset()
        {
            Lots = null;
            LotsStamp = null;
            DistrictLives = null;
            LotTaken = null;
            LotBuilt = null;
            LotRetired = null;
            SizeTaken = null;
            LandPrice = null;
            Finish = null;
            FinishOver = null;
            SpacesOf = null;
            RivalOffice = null;
            RivalOfficeGo = null;
            RivalHere = null;
            RivalNearest = null;
            Designs = null;
            Footprint = null;
            StartPlot = null;
            StartWorks = null;
            Site = null;
            SetContractor = null;
            SiteDone = null;
            FinishSite = null;
            DropSite = null;
            SiteCount = null;
            AdvanceSite = null;
            Ground = null;
            Permits = null;
            CodeAhead = null;
            CouncilDay = null;
            Council = null;
            Sue = null;
            SuitResult = null;
            Unpaid = null;
            BidRigging = null;
            Withdraw = null;
            Followers = null;
            CourtOpen = null;
            HearClaim = null;
            BusinessRate = null;
            BankName = null;
            Credit = null;
            MaterialFactor = null;
            Record = null;
            Lawyer = null;
            Claim = null;
            ClaimDue = null;
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
            ShowDesigns = null;
            ShowDesign = null;
            Standard = null;
            Refitted = null;
            CloseShop = null;
            OpenShop = null;
            Interior = null;
            KeepInterior = null;
            InteriorsBack = null;
        }
    }
}
