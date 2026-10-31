using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using RealLifeOverhaul.DistrictSave;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population as one line of the save's modData (<c>reallife:population</c>): <c>v=2~</c> and the payload, gzip in base64, as the district
    /// save keeps its records (DistrictSaveZip compresses its pieces on the save's workers while this writes them). The payload is binary columns,
    /// each record's figures one column after another so the zip finds their likeness, every number as a variable-length whole (small ones one
    /// byte): a header, the districts and buildings as they were (a load binds the records to the city as it stands, CityTable.Rebind), the
    /// households with their two months of ledger, the people, the employers and businesses by their keys, the region, the books, the districts'
    /// month and the links to other systems' people. Since version 2 (the owner's bigger city: some 20,000 households under the 2 MB budget) ids and
    /// places are told against the one before, days against today, and the ledger in whole money with each line told against what the others make
    /// it (<see cref="WriteLedgers"/>); version 1 is still read. A later version adds columns at the end, which this reader does not ask for; a line
    /// it cannot read is kept as it was (Core/SaveLines) and the city is generated again. Game-free. COST: nothing per frame; at a save the writing
    /// (the compressing on workers), at a load the reading, both measured by EconomyCheck.
    /// </summary>
    public static class PopulationStore
    {
        public const string Key = "reallife:population";
        public const int Version = 2;
        private static readonly byte[] Magic = { (byte)'R', (byte)'L', (byte)'O', (byte)'P' };

        /// <summary>The line through the district save's zip, its pieces compressed on the workers; null when a piece failed (write it with <see cref="Encode"/>).</summary>
        public static string Line(PopulationState s, DistrictSaveZip zip)
        {
            zip.Begin();
            try
            {
                Write(s, zip);
            }
            catch
            {
                zip.Finish(string.Empty);   // every worker done before the fault goes up
                throw;
            }
            return zip.Finish(Head);
        }

        /// <summary>The line through one stream of the runtime's own gzip: for a save whose pieces failed, and for the checks.</summary>
        public static string Encode(PopulationState s)
        {
            using (var raw = new MemoryStream())
            {
                using (var gz = new GZipStream(raw, CompressionLevel.Optimal, true))
                {
                    Write(s, gz);
                }
                return Head + Convert.ToBase64String(raw.GetBuffer(), 0, (int)raw.Length);
            }
        }

        public static string Head => "v=" + Version + "~";

        /// <summary>The population of a line; null for one that is not a population's (or is damaged), with the version it said in <paramref name="version"/>.</summary>
        public static PopulationState Decode(string line, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(line) || !line.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            int cut = line.IndexOf('~');
            if (cut < 3 || !int.TryParse(line.Substring(2, cut - 2), out version) || version < 1)
            {
                return null;
            }
            try
            {
                byte[] packed = Convert.FromBase64String(line.Substring(cut + 1));
                using (var plain = new MemoryStream(packed.Length * 4))
                {
                    using (var gz = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress))
                    {
                        gz.CopyTo(plain);
                    }
                    plain.Position = 0;
                    return Read(plain, version);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- writing

        public static void Write(PopulationState s, Stream to)
        {
            var w = new Writer(to);
            w.Bytes(Magic);
            w.Int(Version);
            w.Int(s.Seed);
            w.Int(s.Day);
            w.Int(s.YearDays);
            w.Int(s.MonthNumber);
            w.Int(s.Generated);
            w.Int(s.NextHousehold);
            w.Int(s.NextPerson);
            w.Int(s.Births);
            w.Int(s.Deaths);
            w.Int(s.BirthsLast);
            w.Int(s.DeathsLast);
            w.Long(s.Median);

            w.Int(s.Districts.Count);
            foreach (PopDistrict d in s.Districts)
            {
                w.Text(d.Id);
                w.Int(d.Working);
                w.Int(d.Middle);
                w.Int(d.Upper);
                w.Int(d.RealEstate);
                w.Int(d.DemandWeight);
                w.Int(d.Pull);
                w.Int((int)d.X);
                w.Int((int)d.Z);
                w.Int(d.Villas ? 1 : 0);
            }
            w.Ints(s.RentIndex, s.RentIndex.Length);
            w.Ints(s.WantFlats, s.WantFlats.Length);
            w.Ints(s.FieldWage, s.FieldWage.Length);
            w.Int(s.IncomeAnchor.Length);
            w.Ints(s.IncomeAnchor, s.IncomeAnchor.Length);

            int nb = s.Buildings.Count;
            w.Int(nb);
            for (int i = 0; i < nb; i++)
            {
                CityBuilding b = s.Buildings[i];
                w.Text(b.Street);
                w.Int(b.Number);
                w.Int(b.District);
                w.Int((int)b.Use);
                w.Int(b.UnitSqm);
                w.Int(b.TotalSqm);
                w.Int(b.Villa ? 1 : 0);
                w.Text(b.Special);
                w.Int(b.Traffic);
                w.Int(b.Rent);
                w.Int((int)b.X);
                w.Int((int)b.Z);
            }

            HouseholdTable hh = s.Households;
            int n = hh.Count;
            w.Int(n);
            w.Deltas(hh.Id, n, 1);
            w.Ints(hh.Building, n);
            w.Ints(hh.Unit, n);
            w.Bytes(hh.District, n);
            w.Bytes(hh.Class, n);
            w.Bytes(hh.Size, n);
            w.Deltas(hh.First, n, 0);
            w.Ints(hh.Savings, n);
            w.Ints(hh.Debt, n);
            w.Ints(hh.Rent, n);
            w.Ints(hh.Landlord, n);
            w.Bytes(hh.Cars, n);
            w.UInts(hh.Tastes, n);
            w.Bytes(hh.Content, n);
            w.Bytes(hh.Flags, n);
            w.Ago(hh.Since, n, s.Day);
            w.Ints(hh.Follows, n);
            WriteLedgers(w, s);

            PersonTable pt = s.People;
            n = pt.Count;
            w.Int(n);
            w.Deltas(pt.Id, n, 1);
            w.Deltas(pt.Household, n, 0);
            w.Near(pt.Next, n);
            w.Ints(pt.Birth, n);
            w.Bytes(pt.Female, n);
            w.Bytes(pt.Role, n);
            w.Bytes(pt.Education, n);
            w.Bytes(pt.Status, n);
            w.Bytes(pt.Field, n);
            w.Bytes(pt.Skill, n);
            w.Bytes(pt.Hours, n);
            w.Ints(pt.Job, n);
            w.Ints(pt.Pay, n);
            w.Near(pt.Partner, n);
            w.Ints(pt.Since, n);
            w.Bytes(pt.Flags, n);

            EmployerTable et = s.Employers;
            n = et.Count;
            w.Int(n);
            for (int i = 0; i < n; i++)
            {
                w.Text(et.Key[i]);
            }
            w.Ints(et.Building, n);
            w.Bytes(et.Kind, n);
            w.Bytes(et.Field, n);
            w.Ints(et.Slots, n);
            w.Ints(et.Filled, n);
            w.Ints(et.Commuters, n);
            w.Ints(et.Business, n);
            for (int i = 0; i < n; i++)
            {
                w.Int(et.Players[i] ? 1 : 0);
            }
            w.Longs(et.Bill, n);
            w.Longs(et.BillLast, n);
            w.Longs(et.Output, n);
            w.Longs(et.OutputLast, n);

            BusinessTable bt = s.Businesses;
            n = bt.Count;
            w.Int(n);
            for (int i = 0; i < n; i++)
            {
                w.Text(bt.Key[i]);
                w.Text(bt.Type[i]);
            }
            w.Ints(bt.Building, n);
            w.Bytes(bt.District, n);
            w.Bytes(bt.Kind, n);
            w.Ints(bt.Room, n);
            w.Bytes(bt.Hours, n);
            w.Ints(bt.Price, n);
            w.Bytes(bt.Quality, n);
            w.Bytes(bt.Content, n);
            w.Ints(bt.Attention, n);
            w.Bytes(bt.Owner, n);
            w.Ints(bt.Employer, n);
            for (int c = 0; c < PopulationCatalogue.Classes; c++)
            {
                w.Strided(bt.Habit, n, PopulationCatalogue.Classes, c);
            }
            w.Longs(bt.Takings, n);
            w.Ints(bt.Visits, n);
            w.Longs(bt.Anchor, n);
            w.Ints(bt.AnchorDay, n);
            w.Ints(bt.AnchorPromotion, n);
            w.Longs(bt.MonthTakings, n);
            w.Longs(bt.LastTakings, n);
            for (int c = 0; c < PopulationCatalogue.Classes; c++)
            {
                w.StridedLong(bt.ByClass, n, PopulationCatalogue.Classes, c);
            }

            RegionState r = s.Region;
            w.Int(r.Pool);
            w.Int(r.Commuting);
            w.Int(r.MovedIn);
            w.Int(r.MovedOut);
            w.Int(r.MovedInLast);
            w.Int(r.MovedOutLast);
            w.Int(r.Wanting);

            CityBooks k = s.Books;
            foreach (long v in new[] { k.IncomeTax, k.SalesTax, k.PropertyTax, k.IncomeTaxLast, k.SalesTaxLast, k.PropertyTaxLast, k.Benefits, k.Pensions, k.BenefitsLast, k.PensionsLast,
                         k.Utilities, k.UtilitiesLast, k.Outside, k.OutsideLast, k.CommuterSpend, k.VisitorSpend, k.CommuterSpendLast, k.VisitorSpendLast, k.Wages, k.WagesLast, k.Rents, k.RentsLast })
            {
                w.Long(v);
            }

            w.Int(s.Figures.Length);
            foreach (DistrictFigures f in s.Figures)
            {
                w.Int(f.Births);
                w.Int(f.Deaths);
                w.Int(f.MovedIn);
                w.Int(f.MovedOut);
                w.Long(f.Spending);
                w.Long(f.Takings);
                w.Long(f.Taxes);
                w.Int(f.WantFlats);
                w.Int(f.MigrantsWanting);
            }

            w.Int(s.Links.Count);
            foreach (PersonLink link in s.Links)
            {
                w.Int((int)link.Kind);
                w.Text(link.Key);
                w.Int(link.Person);
            }
            w.Flush();
        }

        // ---------------------------------------------------------------- reading

        private static PopulationState Read(Stream from, int version)
        {
            var r = new Reader(from);
            byte[] magic = r.Bytes(4);
            if (magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3] || r.Int() < 1)
            {
                return null;
            }
            var s = new PopulationState();
            s.Seed = r.Int();
            s.Day = r.Int();
            s.YearDays = Math.Max(8, r.Int());
            s.MonthNumber = r.Int();
            s.Generated = r.Int();
            s.NextHousehold = r.Int();
            s.NextPerson = r.Int();
            s.Births = r.Int();
            s.Deaths = r.Int();
            s.BirthsLast = r.Int();
            s.DeathsLast = r.Int();
            s.Median = r.Long();

            int nd = Count(r, PopulationCatalogue.DistrictsMost);
            for (int i = 0; i < nd; i++)
            {
                var d = new PopDistrict
                {
                    Id = r.Text(), Working = r.Int(), Middle = r.Int(), Upper = r.Int(), RealEstate = r.Int(), DemandWeight = r.Int(), Pull = r.Int(), X = r.Int(), Z = r.Int(),
                    Villas = r.Int() != 0
                };
                s.Districts.Add(d);
            }
            s.RentIndex = r.Ints(nd);
            s.WantFlats = r.Ints(nd);
            s.FieldWage = r.Ints(PopulationCatalogue.Fields);
            s.IncomeAnchor = r.Ints(Count(r, PopulationCatalogue.DistrictsMost));

            int nb = Count(r, 1 << 20);
            for (int i = 0; i < nb; i++)
            {
                s.Buildings.Add(new CityBuilding
                {
                    Street = r.Text(), Number = r.Int(), District = r.Int(), Use = (Use)r.Int(), UnitSqm = r.Int(), TotalSqm = r.Int(), Villa = r.Int() != 0, Special = r.TextOrNull(),
                    Traffic = r.Int(), Rent = r.Int(), X = r.Int(), Z = r.Int()
                });
            }

            bool one = version < 2;   // the first version's columns: every number as it is
            HouseholdTable hh = s.Households;
            int n = Count(r, 1 << 22);
            hh.Grow(Math.Max(64, n));
            hh.Count = n;
            r.Deltas(hh.Id, n, 1, one);
            r.Ints(hh.Building, n);
            r.Ints(hh.Unit, n);
            r.Bytes(hh.District, n);
            r.Bytes(hh.Class, n);
            r.Bytes(hh.Size, n);
            r.Deltas(hh.First, n, 0, one);
            r.Ints(hh.Savings, n);
            r.Ints(hh.Debt, n);
            r.Ints(hh.Rent, n);
            r.Ints(hh.Landlord, n);
            r.Bytes(hh.Cars, n);
            r.UInts(hh.Tastes, n);
            r.Bytes(hh.Content, n);
            r.Bytes(hh.Flags, n);
            r.Ago(hh.Since, n, s.Day, one);
            r.Ints(hh.Follows, n);
            if (one)
            {
                for (int line = 0; line < Ledger.Lines; line++)
                {
                    r.Strided(hh.Month, n, Ledger.Lines, line);
                }
                for (int line = 0; line < Ledger.Lines; line++)
                {
                    r.Strided(hh.Last, n, Ledger.Lines, line);
                }
            }
            else
            {
                ReadLedgers(r, s);
            }
            hh.Refree();

            PersonTable pt = s.People;
            n = Count(r, 1 << 23);
            pt.Grow(Math.Max(128, n));
            pt.Count = n;
            r.Deltas(pt.Id, n, 1, one);
            r.Deltas(pt.Household, n, 0, one);
            r.Near(pt.Next, n, one);
            r.Ints(pt.Birth, n);
            r.Bytes(pt.Female, n);
            r.Bytes(pt.Role, n);
            r.Bytes(pt.Education, n);
            r.Bytes(pt.Status, n);
            r.Bytes(pt.Field, n);
            r.Bytes(pt.Skill, n);
            r.Bytes(pt.Hours, n);
            r.Ints(pt.Job, n);
            r.Ints(pt.Pay, n);
            r.Near(pt.Partner, n, one);
            r.Ints(pt.Since, n);
            r.Bytes(pt.Flags, n);
            pt.Refree();

            EmployerTable et = s.Employers;
            n = Count(r, 1 << 20);
            et.Grow(Math.Max(64, n));
            et.Count = n;
            for (int i = 0; i < n; i++)
            {
                et.Key[i] = r.Text();
            }
            r.Ints(et.Building, n);
            r.Bytes(et.Kind, n);
            r.Bytes(et.Field, n);
            r.Ints(et.Slots, n);
            r.Ints(et.Filled, n);
            r.Ints(et.Commuters, n);
            r.Ints(et.Business, n);
            for (int i = 0; i < n; i++)
            {
                et.Players[i] = r.Int() != 0;
            }
            r.Longs(et.Bill, n);
            r.Longs(et.BillLast, n);
            r.Longs(et.Output, n);
            r.Longs(et.OutputLast, n);

            BusinessTable bt = s.Businesses;
            n = Count(r, 1 << 20);
            bt.Grow(Math.Max(64, n));
            bt.Count = n;
            for (int i = 0; i < n; i++)
            {
                bt.Key[i] = r.Text();
                bt.Type[i] = r.TextOrNull();
                bt.Seen[i] = true;
            }
            r.Ints(bt.Building, n);
            r.Bytes(bt.District, n);
            r.Bytes(bt.Kind, n);
            r.Ints(bt.Room, n);
            r.Bytes(bt.Hours, n);
            r.Ints(bt.Price, n);
            r.Bytes(bt.Quality, n);
            r.Bytes(bt.Content, n);
            r.Ints(bt.Attention, n);
            r.Bytes(bt.Owner, n);
            r.Ints(bt.Employer, n);
            for (int c = 0; c < PopulationCatalogue.Classes; c++)
            {
                r.Strided(bt.Habit, n, PopulationCatalogue.Classes, c);
            }
            r.Longs(bt.Takings, n);
            r.Ints(bt.Visits, n);
            r.Longs(bt.Anchor, n);
            r.Ints(bt.AnchorDay, n);
            r.Ints(bt.AnchorPromotion, n);
            r.Longs(bt.MonthTakings, n);
            r.Longs(bt.LastTakings, n);
            for (int c = 0; c < PopulationCatalogue.Classes; c++)
            {
                r.StridedLong(bt.ByClass, n, PopulationCatalogue.Classes, c);
            }

            RegionState rg = s.Region;
            rg.Pool = r.Int();
            rg.Commuting = r.Int();
            rg.MovedIn = r.Int();
            rg.MovedOut = r.Int();
            rg.MovedInLast = r.Int();
            rg.MovedOutLast = r.Int();
            rg.Wanting = r.Int();

            CityBooks k = s.Books;
            k.IncomeTax = r.Long();
            k.SalesTax = r.Long();
            k.PropertyTax = r.Long();
            k.IncomeTaxLast = r.Long();
            k.SalesTaxLast = r.Long();
            k.PropertyTaxLast = r.Long();
            k.Benefits = r.Long();
            k.Pensions = r.Long();
            k.BenefitsLast = r.Long();
            k.PensionsLast = r.Long();
            k.Utilities = r.Long();
            k.UtilitiesLast = r.Long();
            k.Outside = r.Long();
            k.OutsideLast = r.Long();
            k.CommuterSpend = r.Long();
            k.VisitorSpend = r.Long();
            k.CommuterSpendLast = r.Long();
            k.VisitorSpendLast = r.Long();
            k.Wages = r.Long();
            k.WagesLast = r.Long();
            k.Rents = r.Long();
            k.RentsLast = r.Long();

            int nf = Count(r, PopulationCatalogue.DistrictsMost);
            s.Figures = new DistrictFigures[nf];
            for (int i = 0; i < nf; i++)
            {
                s.Figures[i] = new DistrictFigures
                {
                    Births = r.Int(), Deaths = r.Int(), MovedIn = r.Int(), MovedOut = r.Int(), Spending = r.Long(), Takings = r.Long(), Taxes = r.Long(), WantFlats = r.Int(),
                    MigrantsWanting = r.Int()
                };
            }

            int nl = Count(r, 1 << 20);
            for (int i = 0; i < nl; i++)
            {
                s.Links.Add(new PersonLink { Kind = (LinkKind)r.Int(), Key = r.Text(), Person = r.Int(), Slot = -1 });
            }
            s.IndexRecords();
            return s;
        }

        // ---------------------------------------------------------------- the ledgers (version 2)

        // The lines of the last month written as they are (the rest are told against them).
        private static readonly int[] PlainLines = { Ledger.Wages, Ledger.Benefits, Ledger.Pension, Ledger.Returns, Ledger.IncomeTax, Ledger.Rent, Ledger.DebtChange };

        /// <summary>
        /// The households' two months of ledger in whole money (no screen shows a line's cents): the last month's income, income tax, rent and debt as
        /// they are; its spending as a total and each kind told against what that total would be at the household's level of income and tastes (the
        /// catalogue's shares, drawn in whole numbers so every machine guesses the same); its sales and property tax against its shopping and rent, its
        /// saving against what its other lines leave; and the running month against the last one's pace. Each is a small number for most households.
        /// </summary>
        private static void WriteLedgers(Writer w, PopulationState s)
        {
            HouseholdTable hh = s.Households;
            int n = hh.Count, lines = Ledger.Lines;
            var last = new int[n * lines];
            for (int i = 0; i < last.Length; i++)
            {
                last[i] = Money(hh.Last[i]);
            }
            var guess = new int[n * Ledger.SpendKinds];
            var spent = new int[n];
            var shares = new int[PopulationCatalogue.Categories];
            int md = GenerateRules.MonthDays(s.YearDays);
            for (int h = 0; h < n; h++)
            {
                spent[h] = Guess(s, last, h, md, shares, guess);
            }
            foreach (int line in PlainLines)
            {
                for (int h = 0; h < n; h++)
                {
                    w.Long(last[h * lines + line]);
                }
            }
            w.Ints(spent, n);
            for (int k = 0; k < Ledger.SpendKinds; k++)
            {
                for (int h = 0; h < n; h++)
                {
                    w.Long((long)last[h * lines + Ledger.SpendFirst + k] - guess[h * Ledger.SpendKinds + k]);
                }
            }
            for (int h = 0; h < n; h++)
            {
                w.Long((long)last[h * lines + Ledger.SalesTax] - SalesTaxOf(last, h));
            }
            for (int h = 0; h < n; h++)
            {
                w.Long((long)last[h * lines + Ledger.PropertyTax] - PropertyTaxOf(last, h));
            }
            for (int h = 0; h < n; h++)
            {
                w.Long((long)last[h * lines + Ledger.Saving] - SavingOf(last, h, spent[h]));
            }
            int into = MonthInto(s, md);
            for (int line = 0; line < lines; line++)
            {
                for (int h = 0; h < n; h++)
                {
                    w.Long((long)Money(hh.Month[h * lines + line]) - Pace(last[h * lines + line], into, md));
                }
            }
        }

        private static void ReadLedgers(Reader r, PopulationState s)
        {
            HouseholdTable hh = s.Households;
            int n = hh.Count, lines = Ledger.Lines;
            var last = new int[n * lines];
            foreach (int line in PlainLines)
            {
                for (int h = 0; h < n; h++)
                {
                    last[h * lines + line] = r.Int();
                }
            }
            int[] spent = r.Ints(n);
            var guess = new int[n * Ledger.SpendKinds];
            var shares = new int[PopulationCatalogue.Categories];
            int md = GenerateRules.MonthDays(s.YearDays);
            for (int h = 0; h < n; h++)
            {
                GuessFrom(s, last, h, md, spent[h], shares, guess);
            }
            for (int k = 0; k < Ledger.SpendKinds; k++)
            {
                for (int h = 0; h < n; h++)
                {
                    last[h * lines + Ledger.SpendFirst + k] = (int)(r.Long() + guess[h * Ledger.SpendKinds + k]);
                }
            }
            for (int h = 0; h < n; h++)
            {
                last[h * lines + Ledger.SalesTax] = (int)(r.Long() + SalesTaxOf(last, h));
            }
            for (int h = 0; h < n; h++)
            {
                last[h * lines + Ledger.PropertyTax] = (int)(r.Long() + PropertyTaxOf(last, h));
            }
            for (int h = 0; h < n; h++)
            {
                last[h * lines + Ledger.Saving] = (int)(r.Long() + SavingOf(last, h, spent[h]));
            }
            int into = MonthInto(s, md);
            for (int line = 0; line < lines; line++)
            {
                for (int h = 0; h < n; h++)
                {
                    hh.Month[h * lines + line] = Cents(r.Long() + Pace(last[h * lines + line], into, md));
                }
            }
            for (int i = 0; i < last.Length; i++)
            {
                hh.Last[i] = Cents(last[i]);
            }
        }

        // A household's spending total, and its guess of each kind, from its last month's lines in whole money.
        private static int Guess(PopulationState s, int[] last, int h, int md, int[] shares, int[] guess)
        {
            long total = 0;
            for (int k = 0; k < Ledger.SpendKinds; k++)
            {
                total += last[h * Ledger.Lines + Ledger.SpendFirst + k];
            }
            int spent = (int)Math.Max(int.MinValue / 4, Math.Min(int.MaxValue / 4, total));
            GuessFrom(s, last, h, md, spent, shares, guess);
            return spent;
        }

        private static void GuessFrom(PopulationState s, int[] last, int h, int md, int spent, int[] shares, int[] guess)
        {
            int row = h * Ledger.Lines;
            long income = (long)last[row + Ledger.Wages] + last[row + Ledger.Benefits] + last[row + Ledger.Pension] + last[row + Ledger.Returns] - last[row + Ledger.IncomeTax];
            int level = SpendRules.Level(income * 100 / Math.Max(1, md), s.Median);
            WholeShares(level, s.Households.Tastes[h], s.Households.Cars[h] > 0, shares);
            long sum = 0;
            for (int k = 0; k < Ledger.SpendKinds; k++)
            {
                sum += shares[(int)Spend.Utilities + k];
            }
            for (int k = 0; k < Ledger.SpendKinds; k++)
            {
                guess[h * Ledger.SpendKinds + k] = sum > 0 ? (int)((long)spent * shares[(int)Spend.Utilities + k] / sum) : 0;
            }
        }

        // SpendRules.Shares drawn in a straight line between the catalogue's rows instead of on a log scale: whole numbers alone, the same on every machine.
        private static void WholeShares(int level, uint tastes, bool car, int[] into)
        {
            int[] low, high;
            int t;   // thousandths of the way from the low row to the high
            if (level <= SpendRules.LevelLow)
            {
                low = high = PopulationCatalogue.SpendShares[0];
                t = 0;
            }
            else if (level < SpendRules.LevelMid)
            {
                low = PopulationCatalogue.SpendShares[0];
                high = PopulationCatalogue.SpendShares[1];
                t = (level - SpendRules.LevelLow) * 1000 / (SpendRules.LevelMid - SpendRules.LevelLow);
            }
            else if (level < SpendRules.LevelHigh)
            {
                low = PopulationCatalogue.SpendShares[1];
                high = PopulationCatalogue.SpendShares[2];
                t = (level - SpendRules.LevelMid) * 1000 / (SpendRules.LevelHigh - SpendRules.LevelMid);
            }
            else
            {
                low = high = PopulationCatalogue.SpendShares[2];
                t = 0;
            }
            for (int k = 0; k < into.Length; k++)
            {
                into[k] = low[k] + (high[k] - low[k]) * t / 1000;
            }
            for (int bit = 0; bit < GenerateRules.TasteBits; bit++)
            {
                if ((tastes & (1u << bit)) != 0)
                {
                    int k = (int)GenerateRules.TasteKinds[bit];
                    into[k] += into[k] * PopulationCatalogue.TasteTilt / 1000;
                }
            }
            into[(int)Spend.Transport] = into[(int)Spend.Transport] * (car ? 1300 : 600) / 1000;
        }

        private static long SalesTaxOf(int[] last, int h)
        {
            long shop = 0;
            for (int k = PopulationCatalogue.ShopCategoryFirst; k <= PopulationCatalogue.ShopCategoryLast; k++)
            {
                shop += last[h * Ledger.Lines + Ledger.SpendFirst + k - (int)Spend.Utilities];
            }
            return shop * PopulationCatalogue.SalesTax / 1000;
        }

        private static long PropertyTaxOf(int[] last, int h)
        {
            return (long)last[h * Ledger.Lines + Ledger.Rent] * PopulationCatalogue.PropertyTaxShare / 1000;
        }

        private static long SavingOf(int[] last, int h, int spent)
        {
            int row = h * Ledger.Lines;
            return (long)last[row + Ledger.Wages] + last[row + Ledger.Benefits] + last[row + Ledger.Pension] + last[row + Ledger.Returns] - last[row + Ledger.IncomeTax]
                   - last[row + Ledger.Rent] - spent;
        }

        // The days of the running month lived by the last day lived (1 to a month's days).
        private static int MonthInto(PopulationState s, int md)
        {
            return Math.Max(0, s.Day - 1) % md + 1;
        }

        // A last month's line at the running month's pace.
        private static long Pace(int last, int into, int md)
        {
            return (long)last * into / md;
        }

        // Cents in whole money, a half away from nothing; and back.
        private static int Money(int cents)
        {
            return cents >= 0 ? (int)(((long)cents + 50) / 100) : -(int)((-(long)cents + 50) / 100);
        }

        private static int Cents(long money)
        {
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, money * 100));
        }

        // A count read: never negative, never past what a city could hold (a damaged line stops here).
        private static int Count(Reader r, int most)
        {
            int n = r.Int();
            if (n < 0 || n > most)
            {
                throw new InvalidDataException("a count of " + n);
            }
            return n;
        }

        // ---------------------------------------------------------------- variable-length wholes

        private sealed class Writer
        {
            private readonly Stream _to;
            private readonly byte[] _buffer = new byte[16384];
            private int _at;

            public Writer(Stream to)
            {
                _to = to;
            }

            private void Byte(byte b)
            {
                if (_at == _buffer.Length)
                {
                    Flush();
                }
                _buffer[_at++] = b;
            }

            public void Flush()
            {
                if (_at > 0)
                {
                    _to.Write(_buffer, 0, _at);
                    _at = 0;
                }
            }

            public void Bytes(byte[] b)
            {
                for (int i = 0; i < b.Length; i++)
                {
                    Byte(b[i]);
                }
            }

            public void Bytes(byte[] b, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    Byte(b[i]);
                }
            }

            // Zigzag, then seven bits a byte: a small number of either sign is one byte.
            public void Long(long v)
            {
                ulong u = (ulong)((v << 1) ^ (v >> 63));
                while (u >= 0x80)
                {
                    Byte((byte)(u | 0x80));
                    u >>= 7;
                }
                Byte((byte)u);
            }

            public void Int(int v) => Long(v);

            public void Ints(int[] a, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    Long(a[i]);
                }
            }

            public void UInts(uint[] a, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    Long(a[i]);
                }
            }

            public void Longs(long[] a, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    Long(a[i]);
                }
            }

            public void Strided(int[] a, int n, int stride, int at)
            {
                for (int i = 0; i < n; i++)
                {
                    Long(a[i * stride + at]);
                }
            }

            // Each against the last one at least 'least' before it (ids and slots run up through the table).
            public void Deltas(int[] a, int n, int least)
            {
                long before = 0;
                for (int i = 0; i < n; i++)
                {
                    Long(a[i] - before);
                    before = a[i] >= least ? a[i] : before;
                }
            }

            // A slot against the row's own place (a household's next member, a partner, are near it); 0 for none.
            public void Near(int[] a, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    long d = (long)a[i] - i;
                    Long(a[i] < 0 ? 0 : d >= 0 ? d + 1 : d);
                }
            }

            // A day as the days before today.
            public void Ago(int[] a, int n, int today)
            {
                for (int i = 0; i < n; i++)
                {
                    Long((long)today - a[i]);
                }
            }

            public void StridedLong(long[] a, int n, int stride, int at)
            {
                for (int i = 0; i < n; i++)
                {
                    Long(a[i * stride + at]);
                }
            }

            public void Text(string t)
            {
                if (t == null)
                {
                    Long(-1);
                    return;
                }
                byte[] b = Encoding.UTF8.GetBytes(t);
                Long(b.Length);
                Bytes(b);
            }
        }

        private sealed class Reader
        {
            private readonly Stream _from;

            public Reader(Stream from)
            {
                _from = from;
            }

            private byte Byte()
            {
                int b = _from.ReadByte();
                if (b < 0)
                {
                    throw new EndOfStreamException();
                }
                return (byte)b;
            }

            public byte[] Bytes(int n)
            {
                var b = new byte[n];
                for (int i = 0; i < n; i++)
                {
                    b[i] = Byte();
                }
                return b;
            }

            public void Bytes(byte[] into, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    into[i] = Byte();
                }
            }

            public long Long()
            {
                ulong u = 0;
                int shift = 0;
                while (true)
                {
                    byte b = Byte();
                    u |= (ulong)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0)
                    {
                        break;
                    }
                    shift += 7;
                    if (shift > 63)
                    {
                        throw new InvalidDataException("a number too long");
                    }
                }
                return (long)(u >> 1) ^ -(long)(u & 1);
            }

            public int Int() => (int)Long();

            public int[] Ints(int n)
            {
                var a = new int[n];
                Ints(a, n);
                return a;
            }

            public void Ints(int[] into, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    into[i] = (int)Long();
                }
            }

            public void UInts(uint[] into, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    into[i] = (uint)Long();
                }
            }

            public void Deltas(int[] into, int n, int least, bool plain)
            {
                long before = 0;
                for (int i = 0; i < n; i++)
                {
                    into[i] = plain ? (int)Long() : (int)(Long() + before);
                    before = into[i] >= least ? into[i] : before;
                }
            }

            public void Near(int[] into, int n, bool plain)
            {
                for (int i = 0; i < n; i++)
                {
                    long v = Long();
                    into[i] = plain ? (int)v : v == 0 ? -1 : v > 0 ? (int)(i + v - 1) : (int)(i + v);
                }
            }

            public void Ago(int[] into, int n, int today, bool plain)
            {
                for (int i = 0; i < n; i++)
                {
                    long v = Long();
                    into[i] = plain ? (int)v : (int)(today - v);
                }
            }

            public void Longs(long[] into, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    into[i] = Long();
                }
            }

            public void Strided(int[] into, int n, int stride, int at)
            {
                for (int i = 0; i < n; i++)
                {
                    into[i * stride + at] = (int)Long();
                }
            }

            public void StridedLong(long[] into, int n, int stride, int at)
            {
                for (int i = 0; i < n; i++)
                {
                    into[i * stride + at] = Long();
                }
            }

            public string Text()
            {
                return TextOrNull() ?? string.Empty;
            }

            public string TextOrNull()
            {
                long length = Long();
                if (length < 0)
                {
                    return null;
                }
                if (length > 1 << 16)
                {
                    throw new InvalidDataException("a text too long");
                }
                return Encoding.UTF8.GetString(Bytes((int)length));
            }
        }
    }
}
