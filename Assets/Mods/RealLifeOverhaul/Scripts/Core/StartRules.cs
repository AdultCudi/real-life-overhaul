namespace RealLifeOverhaul
{
    /// <summary>
    /// The seats of the start line (Core/StartLine): every director whose one-second timer reads its file from the save
    /// and lays out its part of the city on its first tick after a save loads, in the one order they start in.
    ///
    /// Why a line: their timers are all started in the same frame, behind the loading screen, as the city's objects are
    /// made (Core/RealLifeOverhaulMod), so each of their ticks lands in one frame with all the others, and the first one
    /// after the load is every first load at once: some 205 ms of a frame of 1381 ms just as the loading screen went
    /// (under "coroutines and timers"). On the line each first load has a frame of its own.
    ///
    /// The order is the order the directors were made in and their timers ran in until now (the economy's followers in
    /// the order they joined its second), so every director still finds what it read of another at its start already
    /// there. <see cref="After"/> lists what each one reads of the others when it starts; the offline check (NavCheck,
    /// StartCheck.cs) fails if a seat comes before one it reads, or if the order here and the order the directors are
    /// made in part.
    /// </summary>
    public static class StartSeats
    {
        public const int Credit = 0;        // Economy/EconomyDirector: the credit file and the banks' terms
        public const int Banks = 1;         // Economy/Banks/BanksDirector, a follower of the credit side's second (as are the eight after it)
        public const int CityEconomy = 2;   // Economy/City/CityEconomyDirector
        public const int Dealer = 3;        // Economy/Dealer/DealerDirector
        public const int Online = 4;        // Economy/Online/OnlineDirector
        public const int Services = 5;      // Economy/Services/ServicesDirector
        public const int Company = 6;       // Economy/Company/CompanyDirector
        public const int Estate = 7;        // Economy/Estate/EstateDirector
        public const int OwnBank = 8;       // Economy/OwnBank/OwnBankDirector
        public const int Brands = 9;        // Economy/Brands/BrandsDirector
        public const int Ventures = 10;     // Economy/Ventures/VenturesDirector
        public const int Markets = 11;      // Markets/MarketDirector
        public const int Ledger = 12;       // Ledger/LedgerDirector
        public const int Tech = 13;         // Tech/TechDirector
        public const int Careers = 14;      // Careers/CareersDirector
        public const int Offices = 15;      // Offices/Conversions
        public const int TechSites = 16;    // Tech/Sites/TechSites
        public const int Flare = 17;        // Social/Flare/FlareDirector
        public const int Fame = 18;         // Fame/FameDirector
        public const int Permadeath = 19;   // Permadeath/PermadeathDirector
        public const int Count = 20;

        /// <summary>What each seat is, for the log's line (constant texts).</summary>
        public static readonly string[] Names =
        {
            "the credit rating", "the bank apps", "the city's economy", "the car dealership", "online shops", "the cleaning service and selling a business",
            "running a business", "property", "the player's own bank", "brands and franchises", "investors", "the stock market", "the City Ledger",
            "the tech sector", "education and careers", "empty shops as offices", "the tech sector's buildings", "Flare", "Standing", "Permadeath",
        };

        /// <summary>
        /// For each seat, the seats whose state it reads when it starts, so must have started before it: the followers read the
        /// credit side (Running), the prices and the market read the city's economy (Prices.Level, CityEconomy.State), the
        /// stock market takes over Ventures' old market (MarketBridges.Old), the paper's first edition reads the market and the
        /// economy (LedgerBridges), careers read the tech sector's roles, the tech buildings its infrastructure (TechDirector.Infra)
        /// and the university's building (TechBridges.OthersPicked), Flare's new account the businesses, brands and career, and
        /// Standing's first round the followers, staff, holdings and brands (FameBridges). A seat that waits for another itself
        /// (the offices for the headquarters: Conversions.OthersPicked) needs no place after it.
        /// </summary>
        public static readonly int[][] After =
        {
            new int[0],                                   // Credit
            new[] { Credit },                             // Banks
            new[] { Credit },                             // CityEconomy
            new[] { Credit, CityEconomy },                // Dealer
            new[] { Credit, CityEconomy },                // Online
            new[] { Credit },                             // Services
            new[] { Credit, CityEconomy },                // Company
            new[] { Credit, CityEconomy },                // Estate
            new[] { Credit },                             // OwnBank
            new[] { Credit, CityEconomy, Company },       // Brands
            new int[0],                                   // Ventures
            new[] { Ventures },                           // Markets
            new[] { CityEconomy, Markets },               // Ledger
            new int[0],                                   // Tech
            new[] { Tech },                               // Careers
            new int[0],                                   // Offices
            new[] { Tech, Careers },                      // TechSites
            new[] { Company, Brands, Careers },           // Flare
            new[] { Company, Estate, Brands, Flare },     // Fame
            new int[0],                                   // Permadeath
        };
    }

    /// <summary>
    /// The rule of the start line: at most one first load a frame, the lowest seat that is waiting first. A director whose
    /// first tick after a load has come asks for its seat in every frame (<see cref="Holds"/>) until it is given it, then
    /// does its tick and never asks again until the next city. A seat is never given in the frame it is first asked for, so
    /// that every director whose tick lands in that frame has asked before the first is chosen; a seat that has stopped
    /// asking (its director gone, or no longer starting) holds nobody up after one frame.
    ///
    /// Nothing here depends on time or on the machine: given the frames the seats ask in, the frames they are given are
    /// the same everywhere. Arrays made once; a question is a few comparisons and, while seats wait, one look along the
    /// seats before it. No Unity or game types: checked offline (NavCheck, StartCheck.cs).
    /// </summary>
    public sealed class StartQueue
    {
        private const int Never = int.MinValue;

        private readonly int[] _first;    // the frame a seat first asked in; Never while it has not asked
        private readonly int[] _last;     // the frame it last asked in
        private readonly int[] _given;    // the frame it was given its seat in; Never while it waits or has not asked
        private int _lastGiven = Never;   // the frame the last seat was given in
        private int _givenCount;

        public StartQueue(int seats)
        {
            _first = new int[seats];
            _last = new int[seats];
            _given = new int[seats];
            Begin();
        }

        /// <summary>The number of seats.</summary>
        public int Seats => _first.Length;

        /// <summary>Seats given since the line began.</summary>
        public int GivenCount => _givenCount;

        /// <summary>The frame the last seat was given in; <see cref="int.MinValue"/> while none has been.</summary>
        public int LastGiven => _lastGiven;

        /// <summary>A new city: every seat waits for its first load again.</summary>
        public void Begin()
        {
            for (int i = 0; i < _first.Length; i++)
            {
                _first[i] = Never;
                _last[i] = Never;
                _given[i] = Never;
            }
            _lastGiven = Never;
            _givenCount = 0;
        }

        /// <summary>
        /// True while the seat's first load must wait in this frame; false once it is given (then for good, until
        /// <see cref="Begin"/>), and for a seat that is not one. Asked once a frame by a director whose first load has come.
        /// </summary>
        public bool Holds(int seat, int frame)
        {
            if (seat < 0 || seat >= _first.Length || _given[seat] != Never)
            {
                return false;
            }
            if (_first[seat] == Never)
            {
                _first[seat] = frame;
                _last[seat] = frame;
                return true;   // never in the frame it is first asked for: the others whose tick lands here ask first
            }
            _last[seat] = frame;
            if (frame <= _first[seat] || frame == _lastGiven)
            {
                return true;   // one first load a frame
            }
            for (int s = 0; s < seat; s++)
            {
                if (_given[s] == Never && _first[s] != Never && _last[s] >= frame - 1)
                {
                    return true;   // a seat before it is waiting
                }
            }
            _given[seat] = frame;
            _lastGiven = frame;
            _givenCount++;
            return false;
        }

        /// <summary>True when no seat that has asked is still waiting (one that stopped asking a frame ago is not waiting).</summary>
        public bool Settled(int frame)
        {
            for (int s = 0; s < _first.Length; s++)
            {
                if (_given[s] == Never && _first[s] != Never && _last[s] >= frame - 1)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The frame the seat was given in, or <see cref="int.MinValue"/> while it has not been.</summary>
        public int Given(int seat) => seat >= 0 && seat < _given.Length ? _given[seat] : Never;

        /// <summary>The frame the seat first asked in, or <see cref="int.MinValue"/> while it has not.</summary>
        public int Asked(int seat) => seat >= 0 && seat < _first.Length ? _first[seat] : Never;
    }
}
