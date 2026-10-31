using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A rival's product in one of the shared markets: a listed company's (one for each market it sells in) or a startup's
    /// while it runs. It is built, priced, known and bought by the same rules as the player's products. Game-free.
    /// </summary>
    public sealed class RivalProduct
    {
        public int Id;
        public int Company = -1;              // its company, an index of HiringCatalogue.RivalFirms; -1 for a startup's
        public int Startup;                   // the startup's id (Tech/Venture) while it is one's; 0 for a company's
        public int Stage;                     // a startup's rounds raised, for its team (RoundStage)
        public int Market;                    // SectorMarket
        public string Name = string.Empty;
        public int Features, Bugs;
        public long Progress;                 // work points on its next feature
        public long Compute;                  // an AI model's training so far towards its next generation, AI compute unit-days (the player's rule)
        public int Tier = 1;                  // its price tier, as the player's (FirmsCatalogue.PriceTiers)
        public long Customers;                // everybody who uses it (a consumer product's free users too)
        public long Peak;                     // the most customers it had lately...
        public int PeakDay;                   // ...on this day
        public int LastShip, LastCut;         // days: its last feature, its last change of price
        public long Carry;                    // millionths of a customer carried from one day to the next
        // worked out each day, never saved
        public int Appeal, Awareness;
        public long Attraction;

        public bool Startups => Company < 0;
    }

    /// <summary>
    /// What the sector keeps of one of the player's firms under Living population, beside the firms' own record: the events
    /// its desk sessions answer, the offers its share of the city's IT market has earned, a portfolio piece's run. Game-free.
    /// </summary>
    public sealed class SectorFirm
    {
        public string Place = string.Empty;
        public int LastOutage = -1000;        // the day one of its products last went down
        public long LostToOutage;             // ...and the customers it lost then (what a roll back brings back)
        public int LastBigAsk = -1000;        // the day a big customer last asked for something
        public long OfferCarry;               // millionths of an offer earned towards the next
        public int PortfolioUntil;            // a portfolio piece wins bigger clients until this day (not counted)
        public int PitchTender, PitchWeight;  // a council tender pitched at the desk, and thousandths its bid weighs better or worse
        public int SprintUntil, Sprint, Release;   // a sprint planned at the desk: until this day, its aim and its release (SectorRules.Sprint*)
        public int DivertUntil;               // a big customer's feature takes engineering points until this day
        public int ReliableUntil;             // a post-mortem keeps its services sounder until this day
        public int IncidentFrom;              // the incident contract its response session answers, 0 none
    }

    /// <summary>What the sector keeps of a product of the player's beside the firms' record: an AI model's benchmark, a launch's run, a big customer's discount. Game-free.</summary>
    public sealed class SectorProduct
    {
        public int Id;                        // Product.Id
        public int Benchmark;                 // an AI model's last benchmark score, 0 to 100; 0 none yet
        public int Generation;                // the features its last launch session was held at (a model's generations come two features a time)
        public int BoostUntil, Boost;         // awareness a launch added, thousandths, until this day
        public int DiscountUntil;             // a big customer's discount runs until this day
        public int TrainedDay = -1000;        // the day of its last training run at the desk
        public int Data;                      // the data its last run was trained on (SectorRules.Data*), for its next benchmark
    }

    /// <summary>One shared market on a day: its buyers and everything sold into it, worked out each day and never saved.</summary>
    public sealed class SectorMarketDay
    {
        public long Buyers;                   // people or firms who could buy, the world beyond the city in
        public long All;                      // the attraction of every product in it, the player's and the rivals'
        public long Features;                 // the features of every product in it, summed...
        public int Count;                     // ...and how many there are
        public long PriceSum;                 // the rivals' prices, thousandths of the segment's price, summed
        public int Rivals;                    // ...and how many rivals
        public int Spend = 1000;              // what the economy does to its buyers, thousandths
        public int Who;                       // SectorBuyers
        public int Leader = -1;               // the rival product with the most customers, its index in the file's list
        public long Mine;                     // the player's customers in it
        public int MineFeatures;              // the features of the player's best product on sale in it
        public int RivalFeatures;             // the features of the best rival product in it

        /// <summary>The market's going features: the products' average, at least one.</summary>
        public int Average => Count > 0 ? (int)System.Math.Max(1L, Features / Count) : 1;

        /// <summary>The market's going price, thousandths of the segment's price: the rivals' average, the list price with none.</summary>
        public int Going => Rivals > 0 ? (int)(PriceSum / Rivals) : 1000;

        public void Clear()
        {
            Buyers = All = Features = PriceSum = Mine = 0;
            Count = Rivals = MineFeatures = RivalFeatures = 0;
            Spend = 1000;
            Leader = -1;
        }
    }

    /// <summary>
    /// Everything the sector keeps in a save: its seed, the city it last read (so a Going Public guest sizes the markets as the
    /// host does), the markets' anchors from an older save, the rivals' products and their companies' names, the power price's
    /// spikes, and its records of the player's firms and products. On a Going Public guest the city, the rivals and the spikes
    /// are the host's, applied from the shared city. Game-free.
    /// </summary>
    public sealed class SectorFile
    {
        public int Seed, FirstDay, LastDay = -1;
        public bool Converted;                // an older save's products, customers, contracts and racks have been taken in
        public int NextRival = 1;
        public long DigitalAnchor;            // cents a person a day the city's households spent on digital on the first day read; 0 not yet
        public readonly SectorCityFigures City = new SectorCityFigures();
        public readonly int[] Anchor = new int[(int)SectorMarket.Count];   // thousandths each market is held at for an older save's customers, fading to 1000
        public int AnchorDay;                 // ...from this day
        public readonly int[] CompanyRep = new int[SectorCatalogue.CompanyReputation.Length];
        public readonly int[] CompanyWon = new int[SectorCatalogue.CompanyReputation.Length];   // the day each company last won a council tender
        public int NextSpike, SpikeFrom, SpikeUntil, SpikeFactor = 1000;   // the power price's next spike, the one running and its tariff, thousandths
        public bool Hedged;                   // the running spike's price was fixed at the desk
        public int ThrottleUntil;             // the AI racks are throttled for a spike until this day
        public int FailoverUntil;             // a data centre's post-mortem keeps outages down until this day
        public int LastCapacity = -1000;      // the day capacity was last planned
        public int ColoTarget = -1;           // racks capacity planning wants let to the city's firms; -1 as the market takes
        public readonly List<RivalProduct> Rivals = new List<RivalProduct>(32);
        public readonly List<SectorFirm> Firms = new List<SectorFirm>(8);
        public readonly List<SectorProduct> Products = new List<SectorProduct>(16);
        // worked out each day, never saved
        public readonly SectorMarketDay[] Markets = NewMarkets();
        public long Hosting, Training;        // the compute the rivals' software customers and labs take a day: the cloud's market beyond the firms

        private static SectorMarketDay[] NewMarkets()
        {
            var m = new SectorMarketDay[(int)SectorMarket.Count];
            for (int i = 0; i < m.Length; i++)
            {
                m[i] = new SectorMarketDay();
            }
            return m;
        }

        public SectorFirm FirmAt(string place)
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

        /// <summary>The firm's record, made when there is none yet.</summary>
        public SectorFirm FirmFor(string place)
        {
            SectorFirm f = FirmAt(place);
            if (f == null)
            {
                f = new SectorFirm { Place = place ?? string.Empty };
                Firms.Add(f);
            }
            return f;
        }

        public SectorProduct ProductAt(int id)
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

        /// <summary>The product's record, made when there is none yet.</summary>
        public SectorProduct ProductFor(int id)
        {
            SectorProduct p = ProductAt(id);
            if (p == null)
            {
                p = new SectorProduct { Id = id };
                Products.Add(p);
            }
            return p;
        }

        public RivalProduct RivalOf(int company, int market)
        {
            for (int i = 0; i < Rivals.Count; i++)
            {
                if (Rivals[i].Company == company && Rivals[i].Market == market && company >= 0)
                {
                    return Rivals[i];
                }
            }
            return null;
        }

        public RivalProduct StartupProduct(int startup)
        {
            for (int i = 0; i < Rivals.Count; i++)
            {
                if (Rivals[i].Startup == startup && Rivals[i].Company < 0)
                {
                    return Rivals[i];
                }
            }
            return null;
        }
    }

    /// <summary>The city a day's markets are sized from: its people, its firms by size and its mood for digital spending. Saved, so a guest sizes as the host.</summary>
    public sealed class SectorCityFigures
    {
        public int People = SectorCatalogue.ReferencePeople;
        public int Small = SectorCatalogue.ReferenceSmall, Mid = SectorCatalogue.ReferenceMid, Large = SectorCatalogue.ReferenceLarge;
        public int Digital = 1000;            // thousandths: the households' digital spending a person against the first day's
        public bool Known;                    // read from the population (false: the documented city)

        public void Set(int people, int small, int mid, int large, int digital, bool known)
        {
            People = System.Math.Max(0, people);
            Small = System.Math.Max(0, small);
            Mid = System.Math.Max(0, mid);
            Large = System.Math.Max(0, large);
            Digital = digital;
            Known = known;
        }

        public void CopyFrom(SectorCityFigures c)
        {
            Set(c.People, c.Small, c.Mid, c.Large, c.Digital, c.Known);
        }
    }

    /// <summary>What happened in a day of the rivals, for the director to tell: a feature that took a market's lead, a price cut, a startup's product.</summary>
    public struct RivalNote
    {
        public int Rival;                     // its index in the file's list on the day
        public int Kind;                      // SectorRules.NoteShipped, NoteCut, NoteNew, NoteRaised
    }
}
