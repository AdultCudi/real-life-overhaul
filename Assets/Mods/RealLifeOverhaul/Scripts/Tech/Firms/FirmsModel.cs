using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One tech firm of the player's: a business of the tech sector's types, or the game's web agency or graphic designer. Game-free.</summary>
    public sealed class Firm
    {
        public string Place = string.Empty;
        public int Kind;                 // FirmKind
        public int Founded;
        public int Focus;                // Focus
        public int Debt;                 // technical debt, thousandths
        public int Reputation = 500;     // with clients and customers, thousandths
        public int RegisteredUntil;      // the data protection register: paid up to this day
        public int Breaches, Outages;
        public long RnD;                 // qualifying research and development costs of the open quarter, cents
        public int ReliefDay;            // the day the last relief was given
        public long Revenue30;           // takings of the last 30 days, cents (rolled each day)
        public readonly long[] Days = new long[FirmsCatalogue.MonthDays];   // takings by day, the ring Revenue30 is the sum of
        // ---- read from the game each day, never saved
        public string Name = string.Empty;
        public bool Seen;

        public FirmKind K => (FirmKind)Kind;
    }

    /// <summary>One product of a firm's. Game-free.</summary>
    public sealed class Product
    {
        public int Id;
        public string Place = string.Empty;   // the firm that makes it
        public string Name = string.Empty;
        public int Kind, Segment;
        public int Stage;                     // 0 being built, 1 on sale, 2 withdrawn
        public int Features;
        public int Progress;                  // work points on the next feature (or, for a game, on the whole game)
        public int Bugs;
        public long Users;                    // paying customers (a consumer service: its paying users; a game: copies sold)
        public long Free;                     // a consumer service's free users
        public int Tier = 1, Marketing;       // price tier and marketing level
        public int Share = 100;               // the share of the firm's engineering time it gets, per cent (the firm's products share it out by these)
        public int Launched;
        public int Hype;                      // a game's hype before launch, thousandths
        public int Scope;                     // a game's size: 0 small, 1 mid-sized, 2 big
        public long Revenue;                  // all its takings, cents
        public int RivalFeatures;
        public int RivalTier = 1;
        public int LastRelease;
        public long Carry;                    // millionths of a customer carried from one day's growth to the next

        public ProductKind K => (ProductKind)Kind;
        public Segment S => (Segment)Segment;
        public bool Live => Stage == 1;
    }

    /// <summary>A contract: offered to the player's firms, put out to tender by the council, or under way at one of them. Game-free.</summary>
    public sealed class Contract
    {
        public int Id;
        public string Place = string.Empty;   // the firm doing it; empty while it is offered
        public int Kind;                      // WorkKind
        public string Client = string.Empty;
        public string OwnPlace = string.Empty;   // a business of the player's that is the client
        public bool Council;
        public int Points, Done;
        public long Value;                    // a project's price, or a managed service's fee a month, cents
        public long Paid;
        public int Start, Due, End;           // End: an offer's or a tender's last day, or a managed service's
        public int Satisfaction = 700;        // thousandths
        public int Stage;                     // WorkStage
        public long Bid;                      // the player's bid on a tender, cents
        public int Rivals;                    // firms bidding against the player
        public int LastBilled;

        public WorkKind K => (WorkKind)Kind;
        public WorkStage At => (WorkStage)Stage;
        public bool Monthly => Kind == (int)WorkKind.Managed;
    }

    /// <summary>What a day of one product came to.</summary>
    public struct ProductDay
    {
        public long Won, Lost, Takings, Hosting, Marketing;
        public bool Released, Launched, Outage, Breach, RivalShipped;
        public long Affected, Fine;
    }

    /// <summary>The crew of a firm on a day: what its people can do, as the game layer counted it from their files. Game-free.</summary>
    public struct Crew
    {
        public int Engineering;      // work points a day of the engineering, data, security and infrastructure people
        public int Testing;          // ...of those who are testers
        public int Design;           // ...of the designers
        public int Product;          // ...of the product people (each makes a feature's points go further)
        public int Selling;          // points of the sales and account people
        public int Support;          // how many answer customers
        public int SeniorShare;      // thousandths of the engineering points from seniors and above
        public int Security;         // the security posture, 0 to 1000: the analysts' skill and certificates, a data protection officer
        public int Reliability;      // 0 to 1000: the site reliability and DevOps people
        public int Seats;            // professionals at the firm (the software they use is paid by the seat)
        public int Faults;           // thousandths: how often their work needs doing again, on average (TalentRules.Faults)
        public long Wages;           // their day's pay, cents (for the research and development relief)
    }

    /// <summary>Everything the firms part keeps in a save. Game-free.</summary>
    public sealed class FirmsFile
    {
        public int Seed, FirstDay, LastDay = -1, NextId = 1, LastTender;
        public readonly List<Firm> Firms = new List<Firm>(8);
        public readonly List<Product> Products = new List<Product>(FirmsCatalogue.ProductsMost);
        public readonly List<Contract> Contracts = new List<Contract>(FirmsCatalogue.ContractsMost);

        public Firm FirmAt(string place)
        {
            for (int i = 0; i < Firms.Count; i++)
            {
                if (Firms[i].Place == place)
                {
                    return Firms[i];
                }
            }
            return null;
        }

        public Product ProductOf(int id)
        {
            for (int i = 0; i < Products.Count; i++)
            {
                if (Products[i].Id == id)
                {
                    return Products[i];
                }
            }
            return null;
        }

        public Contract ContractOf(int id)
        {
            for (int i = 0; i < Contracts.Count; i++)
            {
                if (Contracts[i].Id == id)
                {
                    return Contracts[i];
                }
            }
            return null;
        }

        public int Next() => NextId++;
    }
}
