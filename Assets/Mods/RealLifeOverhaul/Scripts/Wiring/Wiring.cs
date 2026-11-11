using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the features meet: each works alone and talks to the others only through optional bridges with a default,
    /// and this folder fills them in, one nested class per pairing (the list is in docs/reference/wiring.md).
    /// <see cref="Connect"/> runs once per city load, after every director has been added; <see cref="Disconnect"/>
    /// puts every bridge back. Nothing here has an Update: an adapter answers when the feature that owns the bridge
    /// asks, and never throws into it: a fault is logged once (<see cref="Fault"/>) and the default answer given.
    /// A new pairing is a file with Connect() and Disconnect(), and a line for each in the two lists below.
    /// </summary>
    public static partial class Wiring
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static IModLogger Logger;

        /// <summary>True between Connect and Disconnect.</summary>
        public static bool Connected { get; private set; }

        public static void Connect(IModLogger logger, GameObject root)
        {
            Logger = logger;
            SaveHook.Logger = logger;
            if (Connected)
            {
                Disconnect();
            }
            Connected = true;
            Reported.Clear();
            Step("law and justice", Arrests.Connect);
            Step("licences", LicenceLinks.Connect);
            Step("police reports for the insurers", ReportLinks.Connect);
            Step("the recorded car sounds", CarSoundLinks.Connect);
            Step("civic life", CivicLinks.Connect);
            Step("everyday life", LifeLinks.Connect);
            Step("the driven car's handling and the custom shop's parts", DrivingLinks.Connect);
            Step("family life", FamilyLinks.Connect);
            Step("family life's difficult moments", HardTimesLinks.Connect);
            Step("the minimap and the GPS", NavLinks.Connect);
            Step("the taxi by phone", TaxiLinks.Connect);
            Step("the economy", EconomyLinks.Connect);
            Step("credit and debt", DebtLinks.Connect);
            Step("the city's economy", CityLinks.Connect);
            Step("investors", VenturesLinks.Connect);
            Step("the stock market", MarketLinks.Connect);
            Step("the player's company on the market", ListingLinks.Connect);
            Step("the City Ledger", LedgerLinks.Connect);
            Step("running a business", CompanyLinks.Connect);
            Step("property", EstateLinks.Connect);
            Step("online shops", OnlineLinks.Connect);
            Step("delivery trips", LogisticsLinks.Connect);
            Step("parties and company events", PartyLinks.Connect);
            Step("the tech sector", TechLinks.Connect);
            Step("the start days of the player's businesses", FoundingLinks.Connect);   // wraps nothing: it only reads the moving companies' messages and the statements
            Step("the pause menu's city card", PauseLinks.Connect);   // wraps nothing: it reads the net worth and the businesses as the Overview counts them
            Step("education and careers", CareersLinks.Connect);
            Step("networking", NetworkLinks.Connect);   // after the careers and the investors: the favours are theirs
            Step("the cleaning service and selling a business", ServicesLinks.Connect);
            Step("brands and franchises", BrandLinks.Connect);
            Step("the roommate", RoommateLinks.Connect);
            Step("household staff", HouseholdLinks.Connect);
            Step("meals and music", MealsLinks.Connect);
            Step("power and water", UtilityLinks.Connect);
            Step("the social media app", FlareLinks.Connect);
            Step("the social media app's living network", FlareLiveLinks.Connect);   // after its own: its part of the paper follows the others'
            Step("invitations in Flare messages", FlareInviteLinks.Connect);
            Step("street talk", StreetLinks.Connect);   // after the invitations': Flare's look for an invitation; footfall on top of the others'
            Step("the camera and the gallery", PhotosLinks.Connect);   // after Flare's: the selfie button it wraps is theirs already
            Step("the planner's deliveries, pay and posts", LifePlanSources.Connect);
            Step("interiors and decor", InteriorLinks.Connect);   // after Flare's: its part of footfall goes on top of the others'
            Step("the city's faults", FaultLinks.Connect);   // after Flare's and the interiors': its part of footfall goes on top of theirs
            Step("RLO Texts", TextsLinks.Connect);
            Step("permadeath", PermadeathLinks.Connect);
            Step("the district view", DistrictLinks.Connect);
            Step("the city map's place card", MapLinks.Connect);
            Step("empty shops as offices", OfficeLinks.Connect);
            Step("the player's bank", OwnBankLinks.Connect);   // after running a business: its pieces of the paper follow the company's
            Step("the end game at scale", EndScaleLinks.Connect);
            Step("the desk sessions", SessionLinks.Connect);   // after running a business and the tech sector: it reads their managers and people
            Step("the game studio's sessions", StudioLinks.Connect);   // after the desk sessions: its kinds join theirs
            Step("the tech firms' markets and sessions", SectorLinks.Connect);   // after the desk sessions: its kinds join theirs (the population is read each game day, not here)
            Step("the seasons", SeasonLinks.Connect);   // before the calendar: its day page asks the seasons for their line
            Step("the calendar", CalendarLinks.Connect);   // its sources read the others' schedules when it is shown
            Step("the Marketplace", HustleLinks.Connect);
            Step("rival tycoons", RivalLinks.Connect);   // after the economy, the company, Flare and the interiors: what it wraps is theirs already
            Step("the public city read API", ModApiLinks.Connect);
            Step("standing", StandingLinks.Connect);   // last: what it wraps of the others' is theirs already
            Step("companion reporting", CompanionLinks.Connect);   // wraps nothing: it only reads what the others own
            Step("the mod's own buildings on the minimap", NewBuildingsLinks.Connect);   // wraps nothing
            Step("the building trades", TradesLinks.Connect);   // wraps nothing
            Step("the construction game", ConstructionLinks.Connect);   // after the paper's and the credit side's: its piece and its account go on top of theirs
            Step("the landmarks", LandmarkLinks.Connect);   // after the construction game's: its piece of the paper goes on top of it
            Step("the schools", SchoolLinks.Connect);   // after the landmarks': its piece of the paper goes on top of theirs
            Step("giving", GivingLinks.Connect);   // after the schools' and the rivals': its piece of the paper goes on top of theirs
            Step("health", HealthLinks.Connect);   // after giving's, the careers', the economy's and Home's: it wraps their doors
            Step("the smaller business asks", BizAsksLinks.Connect);   // parking spaces and the showroom's suppliers: wraps nothing
            Step("suppliers", SupplierLinks.Connect);   // after giving's: its piece of the paper goes on top of it
            Step("the stock market's extras", StockExtrasLinks.Connect);   // after the listing's and the rivals': it wraps the company's books and adds to the rivals' page
            Step("rivals seen", SeenLinks.Connect);   // after giving's: its society lines go on top of its piece of the paper
            Step("Uptown's shops of the mod's own kinds", UptownShopsLinks.Connect);   // wraps nothing
            Step("the living population", PopulationLinks.Connect);   // after the business features: their levers become what people hear of a business
            Step("Flare in the city", FlareCityLinks.Connect);   // after the living population: its residents follow, hear and go where they heard good things
            Step("Flare's desk session", FlareSessionLinks.Connect);   // after the desk sessions: its kind joins theirs while Flare in the city runs
            Step("the mod's own businesses and the city's people", DemandLinks.Connect);   // wraps nothing: each business's bridge reads the population's demand
            Step("the Economy app", OutlookLinks.Connect);   // wraps nothing: it only reads the economy, the market, the council and the districts
            Logger?.Info("Wiring: the features are connected.");
        }

        public static void Disconnect()
        {
            if (!Connected)
            {
                return;
            }
            Connected = false;
            Step("the Economy app", OutlookLinks.Disconnect);
            Step("the mod's own businesses and the city's people", DemandLinks.Disconnect);
            Step("Flare's desk session", FlareSessionLinks.Disconnect);
            Step("Flare in the city", FlareCityLinks.Disconnect);
            Step("the living population", PopulationLinks.Disconnect);
            Step("Uptown's shops of the mod's own kinds", UptownShopsLinks.Disconnect);
            Step("the stock market's extras", StockExtrasLinks.Disconnect);   // first: the books it wrapped go back before the listing's own
            Step("health", HealthLinks.Disconnect);   // before giving's: what it wrapped goes back first
            Step("the smaller business asks", BizAsksLinks.Disconnect);
            Step("suppliers", SupplierLinks.Disconnect);   // before giving's: what it wrapped of the paper goes back first
            Step("rivals seen", SeenLinks.Disconnect);   // before giving's: what it wrapped of the paper goes back first
            Step("giving", GivingLinks.Disconnect);   // before the schools': what it wrapped of the paper goes back first
            Step("the schools", SchoolLinks.Disconnect);   // first: what it wrapped of the paper goes back before the landmarks'
            Step("the landmarks", LandmarkLinks.Disconnect);   // first: what it wrapped of the paper goes back before the construction game's
            Step("the construction game", ConstructionLinks.Disconnect);   // first: what it wrapped of the paper and the accounts goes back before the others take theirs off
            Step("the building trades", TradesLinks.Disconnect);
            Step("the mod's own buildings on the minimap", NewBuildingsLinks.Disconnect);
            Step("companion reporting", CompanionLinks.Disconnect);
            Step("standing", StandingLinks.Disconnect);   // first: what it wrapped goes back before the others take theirs off
            Step("the public city read API", ModApiLinks.Disconnect);
            Step("the calendar", CalendarLinks.Disconnect);
            Step("the Marketplace", HustleLinks.Disconnect);
            Step("the seasons", SeasonLinks.Disconnect);
            Step("rival tycoons", RivalLinks.Disconnect);   // next, for the same reason
            Step("the player's bank", OwnBankLinks.Disconnect);   // before running a business: what it wrapped of the paper goes back first
            Step("the tech firms' markets and sessions", SectorLinks.Disconnect);
            Step("the game studio's sessions", StudioLinks.Disconnect);
            Step("the desk sessions", SessionLinks.Disconnect);
            Step("the end game at scale", EndScaleLinks.Disconnect);
            Step("the city map's place card", MapLinks.Disconnect);
            Step("the district view", DistrictLinks.Disconnect);
            Step("empty shops as offices", OfficeLinks.Disconnect);
            Step("permadeath", PermadeathLinks.Disconnect);
            Step("RLO Texts", TextsLinks.Disconnect);
            Step("the city's faults", FaultLinks.Disconnect);   // before Flare's: what it wrapped of footfall goes back first
            Step("interiors and decor", InteriorLinks.Disconnect);   // before Flare's, for the same reason
            Step("the planner's deliveries, pay and posts", LifePlanSources.Disconnect);
            Step("the camera and the gallery", PhotosLinks.Disconnect);   // before Flare's: the selfie button it wrapped goes back first
            Step("street talk", StreetLinks.Disconnect);   // before the invitations': what it wrapped of footfall goes back first
            Step("invitations in Flare messages", FlareInviteLinks.Disconnect);
            Step("the social media app's living network", FlareLiveLinks.Disconnect);   // what it wrapped of the paper goes back first
            Step("the social media app", FlareLinks.Disconnect);
            Step("the taxi by phone", TaxiLinks.Disconnect);
            Step("property", EstateLinks.Disconnect);
            Step("the cleaning service and selling a business", ServicesLinks.Disconnect);
            Step("brands and franchises", BrandLinks.Disconnect);
            Step("power and water", UtilityLinks.Disconnect);
            Step("meals and music", MealsLinks.Disconnect);
            Step("household staff", HouseholdLinks.Disconnect);
            Step("the roommate", RoommateLinks.Disconnect);
            Step("running a business", CompanyLinks.Disconnect);
            Step("networking", NetworkLinks.Disconnect);
            Step("education and careers", CareersLinks.Disconnect);
            Step("the tech sector", TechLinks.Disconnect);
            Step("the start days of the player's businesses", FoundingLinks.Disconnect);
            Step("the pause menu's city card", PauseLinks.Disconnect);
            Step("online shops", OnlineLinks.Disconnect);
            Step("delivery trips", LogisticsLinks.Disconnect);
            Step("parties and company events", PartyLinks.Disconnect);
            Step("the city's economy", CityLinks.Disconnect);
            Step("credit and debt", DebtLinks.Disconnect);
            Step("the economy", EconomyLinks.Disconnect);
            Step("the City Ledger", LedgerLinks.Disconnect);
            Step("the player's company on the market", ListingLinks.Disconnect);
            Step("the stock market", MarketLinks.Disconnect);
            Step("investors", VenturesLinks.Disconnect);
            Step("the minimap and the GPS", NavLinks.Disconnect);
            Step("family life's difficult moments", HardTimesLinks.Disconnect);
            Step("family life", FamilyLinks.Disconnect);
            Step("the driven car's handling and the custom shop's parts", DrivingLinks.Disconnect);
            Step("everyday life", LifeLinks.Disconnect);
            Step("civic life", CivicLinks.Disconnect);
            Step("police reports for the insurers", ReportLinks.Disconnect);
            Step("licences", LicenceLinks.Disconnect);
            Step("the recorded car sounds", CarSoundLinks.Disconnect);
            Step("law and justice", Arrests.Disconnect);
        }

        // One pairing failing to connect must not cost the others theirs.
        private static void Step(string name, Action what)
        {
            try
            {
                what();
            }
            catch (Exception e)
            {
                Fault(name, e);
            }
        }

        /// <summary>A bridge threw: said once per place and session, never per call.</summary>
        internal static void Fault(string where, Exception e)
        {
            if (Reported.Add(where))
            {
                Logger?.Warn("Wiring: " + where + " failed; the default answer is used from here on. " + e);
            }
        }
    }
}
