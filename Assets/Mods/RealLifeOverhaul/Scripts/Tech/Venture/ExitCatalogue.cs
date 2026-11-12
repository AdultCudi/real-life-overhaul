using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The figures of exits. Cents at the start's prices unless said; a day is a game day; shares of things in thousandths.</summary>
    public static class ExitCatalogue
    {
        // Offers for a firm of the player's
        public static long OfferMrrLeast => VentureCatalogue.RoundBar[(int)RoundStage.Seed];   // a month's takings under which nobody makes an offer
        public const int OfferBase = 15;                // per mille a day for a firm that does not grow
        public const int OfferGrowthBp = 25;            // each so many basis points of a month's growth add one per mille a day
        public const int OfferMost = 100;               // per mille a day at the most, before the market's mood
        public const int OfferDays = 10;                // an offer stands this long
        public const int OffersMost = 2;                // standing at once for one firm
        public const int PremiumListedLeast = 100, PremiumListedSpan = 200;   // thousandths over its value a listed company pays
        public const int PremiumRivalSpan = 150;        // thousandths over its value a rival startup pays
        public static readonly int[] CounterSteps = { 100, 200, 300 };   // thousandths more asked
        public const int CounterBase = 600, CounterSelling = 3, CounterAnalysis = 2, CounterRelationships = 1, CounterStep = 2;   // the chance a counter is met, thousandths
        public const int CounterLeast = 80, CounterMost = 900;
        public const int HoldChance = 500;              // of a counter not met, thousandths that the buyer stays at the price (else it walks)

        // The soft landing
        public const int FailingDays = 45;              // a funded firm with less than this many days of its account left, burning, may be taken for its team
        public const int AcquihireChance = 100;         // per mille a day for a failing firm

        // The listing path
        public static long ListMrrLeast => VentureCatalogue.RoundBar[(int)RoundStage.SeriesB];   // a month's takings a listing asks for: what a Series B asks
        public const int RetainerBp = 50;               // of the firm's value
        public const long RetainerLeast = 2500000, RetainerMost = 40000000;
        public const int SpreadBase = 350, SpreadReach = 400;   // basis points of the money raised the bank keeps: 350 and up to 400 more for the bank with the widest reach
        public const int FloatBp = 2000;                // the share of the company the offer sells
        public const int LockUpDays = 180;              // the player sells no shares for this long after the listing
        public static readonly int[] PricingDays = { 5, 5, 2 };   // after the roadshow, by the way it is held
        public static readonly int[] ApproachDemand = { 0, 60, -40 };   // what each way of holding the roadshow adds to the demand
        public const int DemandBase = 250, DemandMood = 300, DemandSkill = 2, DemandReach = 100;
        public const int PriceLeast = 880, PriceSpan = 240;       // the price as a share of fair value: 880 with no demand, up to 1120 with all
        public const int RangeSpread = 100, NoiseSpread = 60;     // the prospectus's range either side, and the pricing day's own noise
        public const int WalkMille = 850;               // the least of fair value the player takes
        public const int QuoteSpread = 20;              // thousandths a market sale of listed shares gives up

        // A later sale of the player's own shares
        public const int SecondaryDays = 30;            // days after a round the investors buy
        public const int SecondaryShare = 200;          // thousandths of the vested shares one round may sell
        public const int DiscountFirst = 250, DiscountLeast = 100;   // thousandths under the round's price; the skills take it down
        public const int QuarterDays = 90;              // a listed firm's shares may be sold again this often

        // The city's startups as places
        public const int FounderAgeLeast = 24, FounderAgeMost = 52;
        public const int WagesShare = 600;              // thousandths of a startup's burn that goes on wages
        public const int HoursMonth = 160;
        public const int HireChance = 150;              // per mille a day a startup short of people takes one from the market
        public const int PeopleMost = 40;
        public const int TowerFloorsMost = 12;          // floors a building holds at most (Floors/FloorsRules.MostFloors)

        /// <summary>The population's field a founder in a sector comes from (Population.Field as a number).</summary>
        public static readonly int[] SectorField = { 5, 4, 7, 5, 6, 3, 2, 5, 5 };   // software, fintech, health, ai, games, marketplace, climate, security, devtools
    }

    /// <summary>A startup of the city that rents a floor of a tower: what the headquarters need to put it on one.</summary>
    public struct StartupTenant
    {
        public int Company;       // after the tech companies' own numbers
        public string Name;
        public int Tower, Slot;   // the tower's place, and its place among the startups of it
    }

    /// <summary>The ways a firm's listing stands. Saved as a number.</summary>
    public static class ListStage
    {
        public const int None = 0, Bankers = 1, Roadshow = 2, Listed = 3, CalledOff = 4;
    }

    /// <summary>An offer for the player's firm: a listed company or a startup of the city buys it, or takes its team. Game-free.</summary>
    public sealed class ExitOffer
    {
        public int Id;
        public int Kind;          // 0 buys the firm, 1 takes the team (the soft landing)
        public int Buyer;         // 0 a listed company, 1 a startup of the city
        public int BuyerId;       // the startup's id; the listed company's stock id
        public string Name = string.Empty;
        public long Price;        // cents at the start's prices
        public int Made, Lapse;
        public bool Countered;

        public bool Hire => Kind == 1;
    }

    /// <summary>What exits keep for one firm of the player's: its offers, its way to the market and its sales of shares. Game-free.</summary>
    public sealed class FirmExit
    {
        public string Place = string.Empty;
        public readonly List<ExitOffer> Offers = new List<ExitOffer>(2);
        // the way to the market
        public int Stage;                 // ListStage
        public int Bank;                  // the bank's place in the list the market gave
        public string BankName = string.Empty;
        public int Reach, Spread;         // the bank's reach (thousandths) and what it keeps of the money raised (basis points)
        public long Fee, Fair;            // the bank's retainer paid; what the firm is worth by its books
        public int Demand, Choice;        // the roadshow's demand (thousandths) and the way it was held
        public int PriceDay;
        public long Low, High;            // the prospectus's range, as the firm's value before the money
        public long Price;                // priced: the firm's value before the money
        public int FloatBp;
        public long Gross, Net;           // what the offer raised, and what reached the firm after the bank
        public int ListDay, LockTo, Called;
        // the player's own shares
        public int StageSeen = -2;        // the last round the day looked at (-2: not yet looked)
        public int RoundDay = -1;         // the day the round that opened the window closed
        public long Sold;                 // shares sold in this window
        // a sale of the firm
        public int SoldDay;
        public long SoldFor;
        public string SoldTo = string.Empty;

        public bool Listed => Stage == ListStage.Listed;
        public bool OnTheWay => Stage == ListStage.Bankers || Stage == ListStage.Roadshow;

        public ExitOffer OfferOf(int id)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].Id == id)
                {
                    return Offers[i];
                }
            }
            return null;
        }

        /// <summary>True for a record with nothing in it: the save has no part for it.</summary>
        public bool Empty => Offers.Count == 0 && Stage == ListStage.None && StageSeen == -2 && SoldDay == 0 && Sold == 0;
    }

    /// <summary>A startup's founder as a person of the city, and where the startup works. Game-free.</summary>
    public sealed class FounderRec
    {
        public int Startup;
        public int Age;
        public bool Female;
        public int Skill;         // 0 to 100: the resident's, or the invented one
        public int Field;         // Population.Field as a number
        public bool Linked;       // a resident of the city's people stands behind them
        public int Tower = -1, Slot;   // the tower it rents in, and its place among the startups there (1 up); -1: not let
        public int Hired;         // people it has taken from the labour market
        public int Seen;          // the day it was first looked at
    }

    /// <summary>Everything exits keep in a save. Game-free.</summary>
    public sealed class ExitFile
    {
        public int Seed, LastDay = -1, NextOffer = 1;
        public readonly List<FirmExit> Firms = new List<FirmExit>(4);
        public readonly List<FounderRec> Founders = new List<FounderRec>(24);

        public FirmExit Exit(string place, bool make)
        {
            for (int i = 0; i < Firms.Count; i++)
            {
                if (Firms[i].Place == place)
                {
                    return Firms[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var e = new FirmExit { Place = place };
            Firms.Add(e);
            return e;
        }

        public FounderRec FounderOf(int startup)
        {
            for (int i = 0; i < Founders.Count; i++)
            {
                if (Founders[i].Startup == startup)
                {
                    return Founders[i];
                }
            }
            return null;
        }

        public int Next() => NextOffer++;
    }
}
