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
            Step("the tech sector", TechLinks.Connect);
            Step("education and careers", CareersLinks.Connect);
            Step("the cleaning service and selling a business", ServicesLinks.Connect);
            Step("brands and franchises", BrandLinks.Connect);
            Step("the roommate", RoommateLinks.Connect);
            Step("meals and music", MealsLinks.Connect);
            Step("power and water", UtilityLinks.Connect);
            Step("the social media app", FlareLinks.Connect);
            Step("the social media app's living network", FlareLiveLinks.Connect);   // after its own: its part of the paper follows the others'
            Step("invitations in Flare messages", FlareInviteLinks.Connect);
            Step("street talk", StreetLinks.Connect);   // after the invitations': Flare's look for an invitation; footfall on top of the others'
            Step("the camera and the gallery", PhotosLinks.Connect);   // after Flare's: the selfie button it wraps is theirs already
            Step("interiors and decor", InteriorLinks.Connect);   // after Flare's: its part of footfall goes on top of the others'
            Step("the city's faults", FaultLinks.Connect);   // after Flare's and the interiors': its part of footfall goes on top of theirs
            Step("RLO Texts", TextsLinks.Connect);
            Step("permadeath", PermadeathLinks.Connect);
            Step("the district view", DistrictLinks.Connect);
            Step("empty shops as offices", OfficeLinks.Connect);
            Step("the player's bank", OwnBankLinks.Connect);   // after running a business: its pieces of the paper follow the company's
            Step("the end game at scale", EndScaleLinks.Connect);
            Step("the calendar", CalendarLinks.Connect);   // its sources read the others' schedules when it is shown
            Step("rival tycoons", RivalLinks.Connect);   // after the economy, the company, Flare and the interiors: what it wraps is theirs already
            Step("standing", StandingLinks.Connect);   // last: what it wraps of the others' is theirs already
            Step("companion reporting", CompanionLinks.Connect);   // wraps nothing: it only reads what the others own
            Step("the mod's own buildings on the minimap", NewBuildingsLinks.Connect);   // wraps nothing
            Step("the building trades", TradesLinks.Connect);   // wraps nothing
            Logger?.Info("Wiring: the features are connected.");
        }

        public static void Disconnect()
        {
            if (!Connected)
            {
                return;
            }
            Connected = false;
            Step("the building trades", TradesLinks.Disconnect);
            Step("the mod's own buildings on the minimap", NewBuildingsLinks.Disconnect);
            Step("companion reporting", CompanionLinks.Disconnect);
            Step("standing", StandingLinks.Disconnect);   // first: what it wrapped goes back before the others take theirs off
            Step("the calendar", CalendarLinks.Disconnect);
            Step("rival tycoons", RivalLinks.Disconnect);   // next, for the same reason
            Step("the player's bank", OwnBankLinks.Disconnect);   // before running a business: what it wrapped of the paper goes back first
            Step("the end game at scale", EndScaleLinks.Disconnect);
            Step("the district view", DistrictLinks.Disconnect);
            Step("empty shops as offices", OfficeLinks.Disconnect);
            Step("permadeath", PermadeathLinks.Disconnect);
            Step("RLO Texts", TextsLinks.Disconnect);
            Step("the city's faults", FaultLinks.Disconnect);   // before Flare's: what it wrapped of footfall goes back first
            Step("interiors and decor", InteriorLinks.Disconnect);   // before Flare's, for the same reason
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
            Step("the roommate", RoommateLinks.Disconnect);
            Step("running a business", CompanyLinks.Disconnect);
            Step("education and careers", CareersLinks.Disconnect);
            Step("the tech sector", TechLinks.Disconnect);
            Step("online shops", OnlineLinks.Disconnect);
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
