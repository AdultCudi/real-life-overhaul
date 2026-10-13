using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the features meet. Every feature of the mod was built to work alone and talks to
    /// the others only through optional bridges with a default; this folder is the one place
    /// that fills those bridges in, one small file per pairing:
    ///
    ///     Standing        the one answer to "has the player a record, and what does the city think of them"
    ///     LawJustice      an arrest from the street to the court: who holds, who charges, who is told
    ///     LicenceLinks    driving licences with the law and the courts
    ///     Scenes          the one question every street scene asks before it starts
    ///     CivicLinks      city hall with the record, the weather, the paper and the scenes; Paper, a story for the City Ledger
    ///     LifeLinks       careers, social life, travel, the garage and the weather with each other
    ///     FamilyLinks     family life with social life, home life and the law
    ///     NavLinks        the minimap and the GPS with the scenes that take the screen, and the police stations
    ///     TaxiLinks       the taxi called from the phone with the holds, the weather, the GPS's roads and the minimap
    ///     EconomyLinks    the credit rating with the legal record and the fines; the Finance rows in the Planner
    ///     DebtLinks       lenders with the courts (a civil claim for a debt), the private lender with the holds and the
    ///                     injury of a mugging, a repossession with what the camera sees
    ///     CityLinks       the city's economy with the calendar and the weather, business pressure (footfall, rivals) and the paper
    ///     VenturesLinks   investors with the city's economy, a pitch with the founder's CV and what has hold of the player;
    ///                     the Ventures rows in the Planner
    ///     MarketLinks     the stock market with the city's economy, the council, the weather, the street, crime, fires and
    ///                     accidents, the player's own takings, the game's rivals and the old Ventures market
    ///     LedgerLinks     the City Ledger with the city's paper, the market's close, the economy, the street's calendar,
    ///                     the council, the other fixtures of the street and the phone's City Ledger contact
    ///     CompanyLinks    running a business with business pressure (footfall, closures, inspections, audits), fires, break-ins, the courts, the paper and the Planner
    ///     EstateLinks     property with the council's rent and permits policies, the event calendar, a hotel's reception and the Planner
    ///     OnlineLinks     the online shops with the company's campaigns, the calendar's seasons, the GPS's roads and the Planner
    ///     ServicesLinks   selling a business with Ventures (investors bought out), the credit side (business loans paid off) and running a business; the Planner
    ///     BrandLinks      brands and franchises with business pressure (footfall), the city's economy (prices), fires, inspections, strikes, the courts and the Planner
    ///     RoommateLinks   the roommate with family life (a partner who moves in), social life (friends, introductions) and the Planner
    ///     FlareLinks      the social media app with the social life (friends who follow and are tagged), the law (its
    ///                     reputation, scrutiny), the council (the election), careers, photo mode, business pressure
    ///                     (footfall), the company (a sponsored post), brands (scandals) and the City Ledger
    ///
    /// <see cref="Connect"/> is called once per city load, after every director has been added;
    /// <see cref="Disconnect"/> on unload puts every bridge back to its default. Nothing here
    /// has an Update: an adapter is called when the feature that owns the bridge asks, and
    /// answers from what the other feature already keeps. An adapter never throws into the
    /// feature that called it: a fault is logged once (<see cref="Fault"/>) and the default
    /// answer is given.
    ///
    /// ADDING A PAIRING: a file here with a nested static class and an internal Connect() /
    /// Disconnect(), one line for each in the two lists below.
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
            Step("civic life", CivicLinks.Connect);
            Step("everyday life", LifeLinks.Connect);
            Step("family life", FamilyLinks.Connect);
            Step("the minimap and the GPS", NavLinks.Connect);
            Step("the taxi by phone", TaxiLinks.Connect);
            Step("the economy", EconomyLinks.Connect);
            Step("credit and debt", DebtLinks.Connect);
            Step("the city's economy", CityLinks.Connect);
            Step("investors", VenturesLinks.Connect);
            Step("the stock market", MarketLinks.Connect);
            Step("the City Ledger", LedgerLinks.Connect);
            Step("running a business", CompanyLinks.Connect);
            Step("property", EstateLinks.Connect);
            Step("online shops", OnlineLinks.Connect);
            Step("the cleaning service and selling a business", ServicesLinks.Connect);
            Step("brands and franchises", BrandLinks.Connect);
            Step("the roommate", RoommateLinks.Connect);
            Step("power and water", UtilityLinks.Connect);
            Step("the social media app", FlareLinks.Connect);
            Logger?.Info("Wiring: the features are connected.");
        }

        public static void Disconnect()
        {
            if (!Connected)
            {
                return;
            }
            Connected = false;
            Step("the social media app", FlareLinks.Disconnect);   // first: what it wrapped is put back before the others take theirs off
            Step("the taxi by phone", TaxiLinks.Disconnect);
            Step("property", EstateLinks.Disconnect);
            Step("the cleaning service and selling a business", ServicesLinks.Disconnect);
            Step("brands and franchises", BrandLinks.Disconnect);
            Step("power and water", UtilityLinks.Disconnect);
            Step("the roommate", RoommateLinks.Disconnect);
            Step("running a business", CompanyLinks.Disconnect);
            Step("online shops", OnlineLinks.Disconnect);
            Step("the city's economy", CityLinks.Disconnect);
            Step("credit and debt", DebtLinks.Disconnect);
            Step("the economy", EconomyLinks.Disconnect);
            Step("the City Ledger", LedgerLinks.Disconnect);
            Step("the stock market", MarketLinks.Disconnect);
            Step("investors", VenturesLinks.Disconnect);
            Step("the minimap and the GPS", NavLinks.Disconnect);
            Step("family life", FamilyLinks.Disconnect);
            Step("everyday life", LifeLinks.Disconnect);
            Step("civic life", CivicLinks.Disconnect);
            Step("licences", LicenceLinks.Disconnect);
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
