using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>What a person is in their household. Saved as a number.</summary>
    public enum Role : byte
    {
        Head = 0,
        Partner = 1,
        Child = 2,
        Other = 3          // a roommate, a relative, somebody sharing
    }

    /// <summary>The lines of a household's month (POPULATION.md section 6 and the owner's word of 1 Nov 2026: every resident's part in the economy, seen).</summary>
    public static class Ledger
    {
        public const int Wages = 0, Benefits = 1, Pension = 2, Returns = 3;     // what came in
        public const int IncomeTax = 4, SalesTax = 5, PropertyTax = 6;         // what went to the council (property tax is a part of the rent)
        public const int Rent = 7;                                             // rent paid, or an owner's upkeep and tax
        public const int SpendFirst = 8;                                       // then each kind Utilities to Digital, in the order of Spend
        public const int SpendKinds = (int)Spend.Digital - (int)Spend.Utilities + 1;
        public const int Saving = SpendFirst + SpendKinds;                     // what went into savings (less what came out of them)
        public const int DebtChange = Saving + 1;                              // debt taken on (less paid off)
        public const int Lines = DebtChange + 1;

        /// <summary>The ledger line of a kind of spending (Utilities to Digital), or -1.</summary>
        public static int Of(Spend kind)
        {
            int k = (int)kind;
            return k >= (int)Spend.Utilities && k <= (int)Spend.Digital ? SpendFirst + k - (int)Spend.Utilities : -1;
        }
    }

    /// <summary>
    /// The households, as plain arrays (structure of arrays): a slot each, a stable id that is never given twice, a free list for the
    /// slots of households that are gone. About 60 bytes a household, and two months of its ledger (the running one and the last).
    /// Game-free. COST: nothing per frame; an add or a removal is a few writes.
    /// </summary>
    public sealed class HouseholdTable
    {
        public int Count;          // slots handed out (live and free)
        public int Live;
        public int[] Id = new int[0];            // 0 for a free slot
        public int[] Building = new int[0];      // the building's place in the city's table
        public int[] Unit = new int[0];          // the flat in it, from 0
        public byte[] District = new byte[0];
        public byte[] Class = new byte[0];
        public byte[] Size = new byte[0];        // its members
        public int[] First = new int[0];         // its first member's slot, -1 for none
        public int[] Savings = new int[0];       // whole money
        public int[] Debt = new int[0];          // whole money
        public int[] Rent = new int[0];          // cents a day: rent, or an owner's upkeep, tax and mortgage
        public int[] Landlord = new int[0];      // who the rent goes to: Landlords.City, Landlords.Own, Landlords.Player
        public byte[] Cars = new byte[0];
        public uint[] Tastes = new uint[0];      // a bit for each interest (PopulationText names them)
        public byte[] Content = new byte[0];     // how satisfied it is, 0 to 100
        public byte[] Flags = new byte[0];       // HouseholdFlags
        public int[] Since = new int[0];         // the day it moved into its flat
        public int[] Follows = new int[0];       // the business it is most attached to (the account it follows), -1 none
        public int[] Month = new int[0];         // the running month's ledger, Ledger.Lines a household, cents
        public int[] Last = new int[0];          // the last closed month's

        private int[] _free = new int[0];
        private int _freeCount;

        public int Capacity => Id.Length;

        /// <summary>A new household with this id; its slot (a free one first, the most lately freed).</summary>
        public int Add(int id)
        {
            int slot;
            if (_freeCount > 0)
            {
                slot = _free[--_freeCount];
            }
            else
            {
                if (Count == Id.Length)
                {
                    Grow(Math.Max(64, Id.Length * 2));
                }
                slot = Count++;
            }
            Clear(slot);
            Id[slot] = id;
            Live++;
            return slot;
        }

        /// <summary>The household gone: its slot is free for the next.</summary>
        public void Remove(int slot)
        {
            if (slot < 0 || slot >= Count || Id[slot] == 0)
            {
                return;
            }
            Clear(slot);
            Id[slot] = 0;
            Live--;
            if (_freeCount == _free.Length)
            {
                Array.Resize(ref _free, Math.Max(32, _free.Length * 2));
            }
            _free[_freeCount++] = slot;
        }

        public bool Alive(int slot) => slot >= 0 && slot < Count && Id[slot] != 0;

        /// <summary>The free slots, rebuilt from the ids (after a load).</summary>
        public void Refree()
        {
            _freeCount = 0;
            Live = 0;
            for (int i = Count - 1; i >= 0; i--)
            {
                if (Id[i] == 0)
                {
                    if (_freeCount == _free.Length)
                    {
                        Array.Resize(ref _free, Math.Max(32, _free.Length * 2));
                    }
                    _free[_freeCount++] = i;
                }
                else
                {
                    Live++;
                }
            }
        }

        private void Clear(int slot)
        {
            Building[slot] = -1;
            Unit[slot] = -1;
            District[slot] = 0;
            Class[slot] = 0;
            Size[slot] = 0;
            First[slot] = -1;
            Savings[slot] = 0;
            Debt[slot] = 0;
            Rent[slot] = 0;
            Landlord[slot] = Landlords.City;
            Cars[slot] = 0;
            Tastes[slot] = 0;
            Content[slot] = 60;
            Flags[slot] = 0;
            Since[slot] = 0;
            Follows[slot] = -1;
            Array.Clear(Month, slot * Ledger.Lines, Ledger.Lines);
            Array.Clear(Last, slot * Ledger.Lines, Ledger.Lines);
        }

        public void Grow(int capacity)
        {
            if (capacity <= Id.Length)
            {
                return;
            }
            Array.Resize(ref Id, capacity);
            Array.Resize(ref Building, capacity);
            Array.Resize(ref Unit, capacity);
            Array.Resize(ref District, capacity);
            Array.Resize(ref Class, capacity);
            Array.Resize(ref Size, capacity);
            Array.Resize(ref First, capacity);
            Array.Resize(ref Savings, capacity);
            Array.Resize(ref Debt, capacity);
            Array.Resize(ref Rent, capacity);
            Array.Resize(ref Landlord, capacity);
            Array.Resize(ref Cars, capacity);
            Array.Resize(ref Tastes, capacity);
            Array.Resize(ref Content, capacity);
            Array.Resize(ref Flags, capacity);
            Array.Resize(ref Since, capacity);
            Array.Resize(ref Follows, capacity);
            Array.Resize(ref Month, capacity * Ledger.Lines);
            Array.Resize(ref Last, capacity * Ledger.Lines);
        }
    }

    /// <summary>Who a household's rent goes to.</summary>
    public static class Landlords
    {
        public const int Own = -1;      // it owns its flat: upkeep and tax, and a lender's mortgage
        public const int City = 0;      // the city's landlords
        public const int Player = 1;    // the player, who lets it the flat
    }

    /// <summary>A household's marks.</summary>
    public static class HouseholdFlags
    {
        public const byte WantsMove = 1;      // looks for another flat (its rent too high, its flat too small)
        public const byte Mortgage = 2;       // owns its flat with a mortgage
        public const byte Players = 4;        // the player's own household (the player, their partner, children and roommates)
        public const byte Away = 8;           // its building is not standing at this load (Uptown off, say): kept, and lives on, but has no flat to count
        public const byte Commuting = 16;     // one of its people works outside the city
        public const byte Regulated = 32;     // its rent is regulated (New York's rent stabilisation, about half its rented flats): it keeps its rent, never past the market's, until it moves
    }

    /// <summary>A person's marks.</summary>
    public static class PersonFlags
    {
        public const byte Linked = 1;         // a person of another system's (the game's staff, a partner): see PopulationLinks
        public const byte Frozen = 2;         // the player: the population never ages, moves or ends them
        public const byte OutsideJob = 4;     // works outside the city
    }

    /// <summary>
    /// The people, as plain arrays: a slot each, a stable id, linked into their household by <see cref="Next"/>. About 40 bytes a person.
    /// Game-free. COST: nothing per frame.
    /// </summary>
    public sealed class PersonTable
    {
        public int Count;
        public int Live;
        public int[] Id = new int[0];            // 0 for a free slot
        public int[] Household = new int[0];     // slot, -1 none
        public int[] Next = new int[0];          // the next member of the household, -1 the last
        public int[] Birth = new int[0];         // the game day they were born (before day 1 for the generated)
        public byte[] Female = new byte[0];      // 1 for a woman
        public byte[] Role = new byte[0];
        public byte[] Education = new byte[0];
        public byte[] Status = new byte[0];
        public byte[] Field = new byte[0];
        public byte[] Skill = new byte[0];       // experience and ability in their field, 0 to 100
        public byte[] Hours = new byte[0];       // a week, in a job
        public int[] Job = new int[0];           // the employer's slot, -1 none
        public int[] Pay = new int[0];           // cents an hour: in a job its pay, out of one the last
        public int[] Partner = new int[0];       // slot, -1 none
        public int[] Since = new int[0];         // the day of their last change of job or status
        public byte[] Flags = new byte[0];
        public ushort[] Games = new ushort[0];   // the genres they like and the platforms they play on (GameRules: a bit each, genres first)

        private int[] _free = new int[0];
        private int _freeCount;

        public int Capacity => Id.Length;

        public int Add(int id)
        {
            int slot;
            if (_freeCount > 0)
            {
                slot = _free[--_freeCount];
            }
            else
            {
                if (Count == Id.Length)
                {
                    Grow(Math.Max(128, Id.Length * 2));
                }
                slot = Count++;
            }
            Clear(slot);
            Id[slot] = id;
            Live++;
            return slot;
        }

        public void Remove(int slot)
        {
            if (slot < 0 || slot >= Count || Id[slot] == 0)
            {
                return;
            }
            Clear(slot);
            Id[slot] = 0;
            Live--;
            if (_freeCount == _free.Length)
            {
                Array.Resize(ref _free, Math.Max(64, _free.Length * 2));
            }
            _free[_freeCount++] = slot;
        }

        public bool Alive(int slot) => slot >= 0 && slot < Count && Id[slot] != 0;

        public void Refree()
        {
            _freeCount = 0;
            Live = 0;
            for (int i = Count - 1; i >= 0; i--)
            {
                if (Id[i] == 0)
                {
                    if (_freeCount == _free.Length)
                    {
                        Array.Resize(ref _free, Math.Max(64, _free.Length * 2));
                    }
                    _free[_freeCount++] = i;
                }
                else
                {
                    Live++;
                }
            }
        }

        private void Clear(int slot)
        {
            Household[slot] = -1;
            Next[slot] = -1;
            Birth[slot] = 0;
            Female[slot] = 0;
            Role[slot] = 0;   // a head of household until the household says otherwise
            Education[slot] = 0;
            Status[slot] = 0;
            Field[slot] = 0;
            Skill[slot] = 0;
            Hours[slot] = 0;
            Job[slot] = -1;
            Pay[slot] = 0;
            Partner[slot] = -1;
            Since[slot] = 0;
            Flags[slot] = 0;
            Games[slot] = 0;
        }

        public void Grow(int capacity)
        {
            if (capacity <= Id.Length)
            {
                return;
            }
            Array.Resize(ref Id, capacity);
            Array.Resize(ref Household, capacity);
            Array.Resize(ref Next, capacity);
            Array.Resize(ref Birth, capacity);
            Array.Resize(ref Female, capacity);
            Array.Resize(ref Role, capacity);
            Array.Resize(ref Education, capacity);
            Array.Resize(ref Status, capacity);
            Array.Resize(ref Field, capacity);
            Array.Resize(ref Skill, capacity);
            Array.Resize(ref Hours, capacity);
            Array.Resize(ref Job, capacity);
            Array.Resize(ref Pay, capacity);
            Array.Resize(ref Partner, capacity);
            Array.Resize(ref Since, capacity);
            Array.Resize(ref Flags, capacity);
            Array.Resize(ref Games, capacity);
        }
    }

    /// <summary>
    /// The employers: every building with jobs has one (an office building two: the business the game lets there and the firms on its other
    /// floors). Their slots are filled by residents (counted in <see cref="Filled"/>) and by commuters from the region. Game-free.
    /// </summary>
    public sealed class EmployerTable
    {
        public int Count;
        public string[] Key = new string[0];       // "street|number" of its building, and "|t" for an office building's other firms
        public int[] Building = new int[0];
        public byte[] Kind = new byte[0];
        public byte[] Field = new byte[0];         // the field of most of its jobs
        public int[] Slots = new int[0];
        public int[] Base = new int[0];            // an office building's other firms: the jobs its let floors held when measured (CityFirmRules moves Slots from it); not saved
        public int[] Filled = new int[0];          // residents in its jobs
        public int[] Commuters = new int[0];       // people from the region in its jobs
        public int[] Business = new int[0];        // its business's slot, -1 for none (an office's other floors, the public sector)
        public bool[] Players = new bool[0];       // the player's: its people are the game's own staff, hired and let go by the player
        public long[] Bill = new long[0];          // cents of wages paid in the running month (residents and commuters)
        public long[] BillLast = new long[0];
        public long[] Output = new long[0];        // cents of value added in the running month
        public long[] OutputLast = new long[0];

        public int Add(string key, int building, EmployerKind kind, Field field, int slots, int business)
        {
            if (Count == Key.Length)
            {
                Grow(Math.Max(64, Key.Length * 2));
            }
            int e = Count++;
            Key[e] = key;
            Building[e] = building;
            Kind[e] = (byte)kind;
            Field[e] = (byte)field;
            Slots[e] = Math.Max(0, slots);
            Base[e] = Slots[e];
            Filled[e] = 0;
            Commuters[e] = 0;
            Business[e] = business;
            Players[e] = false;
            Bill[e] = BillLast[e] = Output[e] = OutputLast[e] = 0;
            return e;
        }

        public int Vacancies(int e) => Math.Max(0, Slots[e] - Filled[e] - Commuters[e]);

        public void Grow(int capacity)
        {
            if (capacity <= Key.Length)
            {
                return;
            }
            Array.Resize(ref Key, capacity);
            Array.Resize(ref Building, capacity);
            Array.Resize(ref Kind, capacity);
            Array.Resize(ref Field, capacity);
            Array.Resize(ref Slots, capacity);
            Array.Resize(ref Base, capacity);
            Array.Resize(ref Filled, capacity);
            Array.Resize(ref Commuters, capacity);
            Array.Resize(ref Business, capacity);
            Array.Resize(ref Players, capacity);
            Array.Resize(ref Bill, capacity);
            Array.Resize(ref BillLast, capacity);
            Array.Resize(ref Output, capacity);
            Array.Resize(ref OutputLast, capacity);
        }
    }

    /// <summary>Whose a business is.</summary>
    public enum Owner : byte
    {
        City = 0,      // the city's own (the game's AI)
        Player = 1,
        Rival = 2,     // a rival's (the game's or the mod's rival tycoons)
        Guest = 3      // another player's in a Going Public session
    }

    /// <summary>
    /// The businesses that sell to people: every registration of the game's with a kind of spending (PopulationCatalogue.Types). What
    /// they draw from the day's pools, by class, and what they are measured against (their anchor). Game-free.
    /// </summary>
    public sealed class BusinessTable
    {
        public int Count;
        public string[] Key = new string[0];       // "street|number"
        public string[] Type = new string[0];      // the game's business type
        public int[] Building = new int[0];
        public byte[] District = new byte[0];
        public byte[] Kind = new byte[0];          // its kind of spending (Spend)
        public int[] Room = new int[0];            // the customers its size has room for (BuildingSizeData), its pull's base
        public byte[] Hours = new byte[0];         // hours open a day, on average
        public int[] Price = new int[0];           // thousandths: its prices against the game's own (1000 the market)
        public byte[] Quality = new byte[0];       // its interior, 0 to 100 (50 even)
        public byte[] Content = new byte[0];       // its customers' satisfaction, 0 to 100 (50 even)
        public int[] Attention = new int[0];       // thousandths more or less pull from what people hear of it (PopulationAttention)
        public byte[] Owner = new byte[0];
        public int[] Employer = new int[0];
        public int[] Habit = new int[0];           // millionths of each class's pool it took yesterday, Classes a business
        public long[] Takings = new long[0];       // cents its customers spent today (sales tax in)
        public long[] ByClass = new long[0];       // ... by class, Classes a business
        public long[] ByLine = new long[0];        // ... from residents at home, residents near work, commuters and visitors, Lines a business
        public int[] Visits = new int[0];          // its customers today
        public long[] Anchor = new long[0];        // cents of takings on its anchor day: what the game's own count is measured against
        public int[] AnchorDay = new int[0];       // the day it was anchored, -1 for not yet
        public int[] AnchorPromotion = new int[0]; // thousandths: the game's promotion multiplier then
        public long[] MonthTakings = new long[0];
        public long[] LastTakings = new long[0];
        public bool[] Seen = new bool[0];          // seen in the game's registrations at the last look

        public const int Lines = 4;                // home, work, commuters, visitors

        public int Add(string key)
        {
            if (Count == Key.Length)
            {
                Grow(Math.Max(64, Key.Length * 2));
            }
            int b = Count++;
            Key[b] = key;
            Type[b] = null;
            Building[b] = -1;
            District[b] = 0;
            Kind[b] = 0;
            Room[b] = 0;
            Hours[b] = 12;
            Price[b] = 1000;
            Quality[b] = 50;
            Content[b] = 50;
            Attention[b] = 0;
            Owner[b] = 0;
            Employer[b] = -1;
            Array.Clear(Habit, b * PopulationCatalogue.Classes, PopulationCatalogue.Classes);
            Takings[b] = 0;
            Array.Clear(ByClass, b * PopulationCatalogue.Classes, PopulationCatalogue.Classes);
            Array.Clear(ByLine, b * Lines, Lines);
            Visits[b] = 0;
            Anchor[b] = 0;
            AnchorDay[b] = -1;
            AnchorPromotion[b] = 0;
            MonthTakings[b] = LastTakings[b] = 0;
            Seen[b] = true;
            return b;
        }

        public void Grow(int capacity)
        {
            if (capacity <= Key.Length)
            {
                return;
            }
            Array.Resize(ref Key, capacity);
            Array.Resize(ref Type, capacity);
            Array.Resize(ref Building, capacity);
            Array.Resize(ref District, capacity);
            Array.Resize(ref Kind, capacity);
            Array.Resize(ref Room, capacity);
            Array.Resize(ref Hours, capacity);
            Array.Resize(ref Price, capacity);
            Array.Resize(ref Quality, capacity);
            Array.Resize(ref Content, capacity);
            Array.Resize(ref Attention, capacity);
            Array.Resize(ref Owner, capacity);
            Array.Resize(ref Employer, capacity);
            Array.Resize(ref Habit, capacity * PopulationCatalogue.Classes);
            Array.Resize(ref Takings, capacity);
            Array.Resize(ref ByClass, capacity * PopulationCatalogue.Classes);
            Array.Resize(ref ByLine, capacity * Lines);
            Array.Resize(ref Visits, capacity);
            Array.Resize(ref Anchor, capacity);
            Array.Resize(ref AnchorDay, capacity);
            Array.Resize(ref AnchorPromotion, capacity);
            Array.Resize(ref MonthTakings, capacity);
            Array.Resize(ref LastTakings, capacity);
            Array.Resize(ref Seen, capacity);
        }
    }

    /// <summary>The figures of a district the population worked out at its last day: what other features and the Economy app read.</summary>
    public sealed class DistrictFigures
    {
        public int People, Households, Children, Pupils, Students, Workers, Unemployed, Retired, Home;
        public int Flats, Vacant, Jobs, Vacancies, Commuters;
        public int RentIndex = 1000;        // thousandths of the game's own rents
        public int MeanRent;                // cents a day, of the households renting
        public int MeanIncome;              // cents a day of gross income a household
        public long Spending, Takings, Taxes;   // cents in the running month: its households' spending, its businesses' takings, the council's taxes from it
        public int Births, Deaths, MovedIn, MovedOut;   // in the running month
        public int WantFlats;               // households that looked for a flat here and found none free, last month
        public int MigrantsWanting;         // households of the region that wanted to come, last month

        public void Clear()
        {
            People = Households = Children = Pupils = Students = Workers = Unemployed = Retired = Home = 0;
            Flats = Vacant = Jobs = Vacancies = Commuters = 0;
            MeanRent = MeanIncome = 0;
        }
    }

    /// <summary>The council's money from the population, and the city's other flows, by the month: the account the council reads (POPULATION.md section 6).</summary>
    public sealed class CityBooks
    {
        public long IncomeTax, SalesTax, PropertyTax;            // cents in the running month
        public long IncomeTaxLast, SalesTaxLast, PropertyTaxLast; // the last closed month
        public long Benefits, Pensions, BenefitsLast, PensionsLast;   // paid from outside the city (the state)
        public long Utilities, UtilitiesLast;                    // what households pay for utilities
        public long Outside, OutsideLast;                        // spent beyond the city's businesses (online, digital, a kind no business sells here)
        public long CommuterSpend, VisitorSpend, CommuterSpendLast, VisitorSpendLast;
        public long Wages, WagesLast;                            // paid to residents
        public long Rents, RentsLast;                            // paid by residents, the property tax in

        public long Taxes => IncomeTax + SalesTax + PropertyTax;
        public long TaxesLast => IncomeTaxLast + SalesTaxLast + PropertyTaxLast;

        /// <summary>The month closes: the running figures become the last month's.</summary>
        public void Roll()
        {
            IncomeTaxLast = IncomeTax;
            SalesTaxLast = SalesTax;
            PropertyTaxLast = PropertyTax;
            BenefitsLast = Benefits;
            PensionsLast = Pensions;
            UtilitiesLast = Utilities;
            OutsideLast = Outside;
            CommuterSpendLast = CommuterSpend;
            VisitorSpendLast = VisitorSpend;
            WagesLast = Wages;
            RentsLast = Rents;
            IncomeTax = SalesTax = PropertyTax = Benefits = Pensions = Utilities = Outside = CommuterSpend = VisitorSpend = Wages = Rents = 0;
        }
    }

    /// <summary>The region round the city: an aggregate, not records (POPULATION.md section 5).</summary>
    public sealed class RegionState
    {
        public int Pool = PopulationCatalogue.RegionPool;   // its people who could take a job in the city
        public int Commuting;                               // of them, in a job in the city now
        public int MovedIn, MovedOut;                       // households, in the running month
        public int MovedInLast, MovedOutLast;
        public int Wanting;                                 // households that wanted to come and found no flat, in the running month
    }

    /// <summary>
    /// One person of another system's who is a resident too: the link from that system's id to a person record (the brief's step 2:
    /// the links, not a rewrite of those systems; each keeps working, and the link is how later work moves them over).
    /// </summary>
    public struct PersonLink
    {
        public LinkKind Kind;
        public string Key;       // that system's id
        public int Person;       // the person's id
        public int Slot;         // the person's slot, checked against the id before use (slots are kept in the save, so it holds across one)
    }

    /// <summary>
    /// Everything the population is: the city's districts and buildings, the households and people, the employers and businesses, the region
    /// and the council's books, and the links to other systems' people. Game-free; what the save keeps (PopulationStore) and the Going Public
    /// host shares a summary of (PopulationShare).
    /// </summary>
    public sealed class PopulationState
    {
        public const int Version = 1;

        public int Seed;               // the city's seed it was generated from
        public int Day;                // the game day last lived
        public int YearDays = 60;      // the save's year in days
        public int MonthNumber;        // the running month's number, from 0
        public int Generated;          // the day it was generated
        public int Measure = PopulationCatalogue.MeasureVersion;   // the measure its homes were counted by (0: a line from before the measure was written)
        public int NextHousehold = 1, NextPerson = 1;
        public int Births, Deaths;     // in the running month, the city's
        public int BirthsLast, DeathsLast;
        public long Median;            // cents a day: the city's middle household's income after tax at the last day, which spending levels are measured by
        public int HostPeople, HostHouseholds;   // a Going Public guest: the host's city's people and households (the records are the host's to keep); not saved

        public readonly List<PopDistrict> Districts = new List<PopDistrict>(12);
        public readonly List<CityBuilding> Buildings = new List<CityBuilding>(1024);
        public int[] FlatFirst = new int[0];     // a building's first flat's place in Occupant
        public int[] FlatCount = new int[0];     // its flats
        public int[] FlatSqm = new int[0];       // m2 of each of its flats
        public int[] FlatRent = new int[0];      // cents a day: the game's own market rent of one of its flats, before the district's index
        public int[] Occupant = new int[0];      // each flat's household slot, -1 free, -2 the player's own or let by the player outside the population
        public readonly HouseholdTable Households = new HouseholdTable();
        public readonly PersonTable People = new PersonTable();
        public readonly EmployerTable Employers = new EmployerTable();
        public readonly BusinessTable Businesses = new BusinessTable();
        public readonly RegionState Region = new RegionState();
        public readonly CityBooks Books = new CityBooks();
        public readonly List<PersonLink> Links = new List<PersonLink>(64);
        public readonly GrowthState Growth = new GrowthState();
        public int[] RentIndex = new int[0];     // thousandths of the game's own rent, by district
        public DistrictFigures[] Figures = new DistrictFigures[0];
        public int[] WantFlats = new int[0];     // households that looked for a flat in each district this month and found none
        public int[] FieldWage = new int[0];     // thousandths: each field's market pay against its start (wages follow how hard people are to find)
        public int[] IncomeAnchor = new int[0];  // cents a day: each district's mean household income on the population's first day (CityEconomy's district income moves from its start by it)
        public int[] GameCounts = new int[GameRules.Cells];    // residents who like each genre on each platform they play on, at the last day's figures; not saved
        public int[] GameByDistrict = new int[0];               // the same for each district, GameRules.Cells a district; not saved
        public int[] GameByClass = new int[PopulationCatalogue.Classes * GameRules.Cells];   // ... for each class; not saved
        public int[] GameStart = new int[GameRules.Cells];     // the counts at the population's start: the game studio's world factor is the world's then over these

        private readonly Dictionary<string, int> _buildingByKey = new Dictionary<string, int>(1024, StringComparer.Ordinal);
        private readonly Dictionary<string, int> _businessByKey = new Dictionary<string, int>(1024, StringComparer.Ordinal);
        private readonly Dictionary<string, int> _employerByKey = new Dictionary<string, int>(2048, StringComparer.Ordinal);

        /// <summary>The buildings' addresses indexed again (after the table was sorted or added to).</summary>
        public void IndexBuildings()
        {
            _buildingByKey.Clear();
            for (int i = 0; i < Buildings.Count; i++)
            {
                _buildingByKey[KeyOf(Buildings[i].Street, Buildings[i].Number)] = i;
            }
        }

        /// <summary>The businesses' and employers' keys indexed again (after a load).</summary>
        public void IndexRecords()
        {
            _businessByKey.Clear();
            for (int i = 0; i < Businesses.Count; i++)
            {
                if (Businesses.Key[i] != null)
                {
                    _businessByKey[Businesses.Key[i]] = i;
                }
            }
            _employerByKey.Clear();
            for (int i = 0; i < Employers.Count; i++)
            {
                if (Employers.Key[i] != null)
                {
                    _employerByKey[Employers.Key[i]] = i;
                }
            }
        }

        public int BuildingOfKey(string key) => key != null && _buildingByKey.TryGetValue(key, out int b) ? b : -1;
        public int BusinessOfKey(string key) => key != null && _businessByKey.TryGetValue(key, out int b) ? b : -1;
        public int EmployerOfKey(string key) => key != null && _employerByKey.TryGetValue(key, out int e) ? e : -1;
        public void KnowBusiness(string key, int b) => _businessByKey[key] = b;
        public void KnowEmployer(string key, int e) => _employerByKey[key] = e;

        /// <summary>The district table's place of an id, or -1.</summary>
        public int DistrictOf(string id)
        {
            for (int i = 0; i < Districts.Count; i++)
            {
                if (string.Equals(Districts[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A building's place in the table by its address, or -1. A walk: used when a city is put together, never per frame.</summary>
        public int BuildingOf(string street, int number)
        {
            for (int i = 0; i < Buildings.Count; i++)
            {
                if (Buildings[i].Number == number && string.Equals(Buildings[i].Street, street, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The age in whole years of a person on a day.</summary>
        public int Age(int person, int day)
        {
            return AgeOf(People.Birth[person], day, YearDays);
        }

        public static int AgeOf(int birth, int day, int yearDays)
        {
            int days = day - birth;
            return days <= 0 ? 0 : days / Math.Max(1, yearDays);
        }

        /// <summary>The key a building's or a business's records go by: its address.</summary>
        public static string KeyOf(string street, int number)
        {
            return (street ?? string.Empty) + "|" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
