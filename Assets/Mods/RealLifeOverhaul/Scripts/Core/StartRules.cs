namespace RealLifeOverhaul
{
    /// <summary>
    /// The seats of the start line (<see cref="StartLine"/>): every director that reads its file and lays out its part of
    /// the city on its first tick after a load. Their timers all start in one frame, so the first ticks would land
    /// together (some 205 ms). The order is the order the directors are made in, so each finds what it reads of another
    /// already there; <see cref="After"/> lists what each reads.
    /// </summary>
    public static class StartSeats
    {
        public const int Credit = 0;        // Economy/Banking/EconomyDirector: the credit file and the banks' terms
        public const int Banks = 1;         // Economy/Banking/BankApps/BanksDirector: a follower of the credit side's second, as are the eight after it
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
        public const int Law = 20;          // Law/LawDirector: the record, the precincts chosen and the nearest front put out (58 ms in the owner's session of 25 Oct 2026)
        public const int Garage = 21;       // Garage/GarageDirector: the builds and the two-wheelers' places found (40 ms)
        public const int Outlook = 22;      // Outlook/OutlookDirector: the Economy app, on a timer of its own (it reads the city's economy and the stock market, so it starts after them)
        public const int Count = 23;

        /// <summary>What each seat is, for the log's line (constant texts).</summary>
        public static readonly string[] Names =
        {
            "the credit rating", "the bank apps", "the city's economy", "the car dealership", "online shops", "the cleaning service and selling a business",
            "running a business", "property", "the player's own bank", "brands and franchises", "investors", "the stock market", "the City Ledger",
            "the tech sector", "education and careers", "empty shops as offices", "the tech sector's buildings", "Flare", "Standing", "Permadeath",
            "the police and the record", "the Garage", "the Economy app",
        };

        /// <summary>For each seat, the seats whose state it reads when it starts, so must start before it.</summary>
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
            new int[0],                                   // Law
            new int[0],                                   // Garage
            new[] { CityEconomy, Markets },               // Outlook
        };
    }

    /// <summary>
    /// The rule of the start line: at most one first load a frame, the lowest waiting seat first. A seat is never given in
    /// the frame it is first asked for, so every director whose tick lands there has asked; one that stops asking holds
    /// nobody up after a frame. The same frames asked give the same frames given on every machine. Game-free; a few
    /// comparisons a question.
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


        public int GivenCount => _givenCount;

        /// <summary>The frame the last seat was given in; <see cref="int.MinValue"/> while none has been.</summary>
        public int LastGiven => _lastGiven;

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

        /// <summary>True while the seat's first load must wait this frame; false once given, until <see cref="Begin"/>.</summary>
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
