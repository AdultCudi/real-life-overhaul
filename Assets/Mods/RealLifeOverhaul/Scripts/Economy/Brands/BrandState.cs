using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>A brand's recognition in one district, by the game's own name for the neighbourhood.</summary>
    public sealed class DistrictFile
    {
        public string Name = string.Empty;
        public int Rec;             // hundredths of a point, 0 to 10000
    }

    /// <summary>A brand of the player's.</summary>
    public sealed class Brand
    {
        public int Id;
        public string Name = string.Empty;
        public string Type = string.Empty;       // the game's business type it covers ("ba:businesstype_...")
        public string Flagship = string.Empty;   // the place of the shop it was made from: shared prices and hours are copied from it
        public int Founded;
        public int Age;                          // the brand's own clock, in days: what "a year passes" advances
        public int City;                         // city-wide recognition, hundredths
        public int Standards;                    // points, as last worked out
        public int Minimum;                      // the required minimum the player set, points; 0 for none
        public bool Offered;                     // on offer as a franchise
        public int Push = -1;                    // a brand campaign: BrandCatalogue.PushLocal or PushCity; -1 for none
        public int PushFrom, PushEnd;            // brand days: runs from PushFrom up to (not including) PushEnd
        public string PushDistrict = string.Empty;
        public int Fund;                         // levy collected and not yet spent on a brand campaign
        public int Ticket;                       // what a customer spends at a member shop, hundredths, from the game's own order history
        public int Net;                          // what the franchises bring in a day after support, a moving average over a week
        public int Earned;                       // royalties in all
        public int Failed;                       // franchisees that closed
        public int Bid, BidBy, BidUntil;         // a franchisee's bid for the brand: the price, who, and the last day to answer
        public int AskRest;                      // no new second shop or bid before this day
        public readonly List<DistrictFile> Districts = new List<DistrictFile>(8);

        public bool Pushing => Push >= 0 && Age >= PushFrom && Age < PushEnd;

        public DistrictFile District(string name)
        {
            for (int i = 0; name != null && i < Districts.Count; i++)
            {
                if (string.Equals(Districts[i].Name, name, StringComparison.Ordinal))
                {
                    return Districts[i];
                }
            }
            return null;
        }
    }

    /// <summary>A shop of the player's that trades under a brand. The figures after the first three are read from the game each day and are not saved.</summary>
    public sealed class Member
    {
        public string Place = string.Empty;
        public int BrandId;
        public int Joined;

        public string District;
        public int Satisfaction = 50, Clean = 100, Stock = 100, Skill = 50;   // the game's own figures, 0 to 100 (Stock: the share of its main goods on the shelves)
        public int Standards = 50;
        public int Customers, Takings;           // yesterday, by the game's order history
        public int X, Z;                         // its door, in whole metres
        public bool HasSpot, Seen;
        public int PriceLoss;                    // the customers its prices cost by the city's economy, per cent (0 or negative)
        public int Rec, Forgiven, Cannibal;      // the three parts of what the brand asks for here, per cent of customers
    }

    /// <summary>Somebody who wants to open a shop under a brand.</summary>
    public sealed class Applicant
    {
        public int Id;
        public int BrandId;
        public string Name = string.Empty;
        public Temper Temper;
        public int Capital;
        public int Experience;                   // years in the trade
        public int ShopsRun, ShopsClosed;        // their record: shops run before, and how many of those closed
        public int Ability;                      // 0 to 100: not shown, only what it is made of
        public string District = string.Empty;
        public int Until;                        // the last day they wait
        public int From;                         // the franchise whose owner this is, for a second shop; 0 for a newcomer
    }

    /// <summary>A franchise agreement and its shop.</summary>
    public sealed class Franchise
    {
        public int Id;
        public int BrandId;
        public string Name = string.Empty;
        public Temper Temper;
        public int Ability;
        public string District = string.Empty;
        public string Place = string.Empty;      // the building, written as a business's place is
        public string GameName = string.Empty;   // what the game calls the business there (its own name for it is kept: see BrandsGame)
        public Hold Hold = Hold.Books;
        public int X, Z;
        public bool HasSpot;
        public int Base;                         // customers an ordinary day brings by the game's own formula for the building and the type
        public int Signed, Until;                // game days
        public int Term;                         // the agreement's length in days: a renewal adds it again
        public bool NoRenewal;                   // a renewal was asked for and not given: the agreement runs out on its day
        public int EntryFee, RoyaltyBp, LevyBp;
        public bool Exclusive;
        public int True = 6000;                  // its standards, hundredths: what an inspection finds
        public int Seen = -1;                    // points, as last inspected; -1 for never
        public int SeenDay;
        public int Warnings, WarnedUntil, RetrainedUntil;
        public int Health;                       // the franchisee's cash
        public bool Trouble;
        public Ask Ask;
        public int AskUntil, AskAmount, AskRest;
        public int Suit;                         // the court's number for a dispute; 0 for none
        public int WaivedUntil;                  // no royalty before this day
        public int Takings, Royalty, Customers;  // yesterday
        public int Paid;                         // royalties and levy in all

        public int StandardsNow => True / 100;
    }

    /// <summary>A shop of the player's that trades under one of the city's chains.</summary>
    public sealed class Taken
    {
        public string Place = string.Empty;
        public int Chain;
        public int From, Until;
        public int Breach;                       // days running outside the chain's standards or prices
        public int Average;                      // an ordinary day's royalty, a moving average
        public int Paid;

        public int Standards = 50, Prices = 1000;   // read each day, not saved
        public bool Seen;
    }

    /// <summary>Everything brands and franchises keep for one save.</summary>
    public sealed class BrandsState
    {
        public int Seed;
        public int FirstDay, LastDay;
        public int NextId = 1;
        public readonly List<Brand> Brands = new List<Brand>(4);
        public readonly List<Member> Members = new List<Member>(16);
        public readonly List<Applicant> Applicants = new List<Applicant>(4);
        public readonly List<Franchise> Franchises = new List<Franchise>(16);
        public readonly List<Taken> Taken = new List<Taken>(4);

        public Brand Brand(int id)
        {
            for (int i = 0; i < Brands.Count; i++)
            {
                if (Brands[i].Id == id)
                {
                    return Brands[i];
                }
            }
            return null;
        }

        public Member Member(string place)
        {
            for (int i = 0; place != null && i < Members.Count; i++)
            {
                if (string.Equals(Members[i].Place, place, StringComparison.Ordinal))
                {
                    return Members[i];
                }
            }
            return null;
        }

        public Franchise Franchise(int id)
        {
            for (int i = 0; i < Franchises.Count; i++)
            {
                if (Franchises[i].Id == id)
                {
                    return Franchises[i];
                }
            }
            return null;
        }

        public Applicant Applicant(int id)
        {
            for (int i = 0; i < Applicants.Count; i++)
            {
                if (Applicants[i].Id == id)
                {
                    return Applicants[i];
                }
            }
            return null;
        }

        public Taken TakenAt(string place)
        {
            for (int i = 0; place != null && i < Taken.Count; i++)
            {
                if (string.Equals(Taken[i].Place, place, StringComparison.Ordinal))
                {
                    return Taken[i];
                }
            }
            return null;
        }
    }
}
