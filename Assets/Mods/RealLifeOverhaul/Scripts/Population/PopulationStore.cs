using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using RealLifeOverhaul.DistrictSave;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population as one line of the save's modData (<c>reallife:population</c>): <c>v=1~</c> and the payload, gzip in base64, as the district
    /// save keeps its records (DistrictSaveZip compresses its pieces on the save's workers while this writes them). The payload is binary columns,
    /// each record's figures one column after another so the zip finds their likeness, every number as a variable-length whole (small ones one
    /// byte): a header, the districts and buildings as they were (a load binds the records to the city as it stands, CityTable.Rebind), the
    /// households with their two months of ledger, the people, the employers and businesses by their keys, the region, the books, the districts'
    /// month and the links to other systems' people. A later version adds columns at the end, which this reader does not ask for; a line it cannot
    /// read is kept as it was (Core/SaveLines) and the city is generated again. Game-free. COST: nothing per frame; at a save the writing (the
    /// compressing on workers), at a load the reading, both measured by EconomyCheck.
    /// </summary>
    public static class PopulationStore
    {
        public const string Key = "reallife:population";
        public const int Version = 1;
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
            w.Ints(hh.Id, n);
            w.Ints(hh.Building, n);
            w.Ints(hh.Unit, n);
            w.Bytes(hh.District, n);
            w.Bytes(hh.Class, n);
            w.Bytes(hh.Size, n);
            w.Ints(hh.First, n);
            w.Ints(hh.Savings, n);
            w.Ints(hh.Debt, n);
            w.Ints(hh.Rent, n);
            w.Ints(hh.Landlord, n);
            w.Bytes(hh.Cars, n);
            w.UInts(hh.Tastes, n);
            w.Bytes(hh.Content, n);
            w.Bytes(hh.Flags, n);
            w.Ints(hh.Since, n);
            w.Ints(hh.Follows, n);
            for (int line = 0; line < Ledger.Lines; line++)
            {
                w.Strided(hh.Month, n, Ledger.Lines, line);
            }
            for (int line = 0; line < Ledger.Lines; line++)
            {
                w.Strided(hh.Last, n, Ledger.Lines, line);
            }

            PersonTable pt = s.People;
            n = pt.Count;
            w.Int(n);
            w.Ints(pt.Id, n);
            w.Ints(pt.Household, n);
            w.Ints(pt.Next, n);
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
            w.Ints(pt.Partner, n);
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

            HouseholdTable hh = s.Households;
            int n = Count(r, 1 << 22);
            hh.Grow(Math.Max(64, n));
            hh.Count = n;
            r.Ints(hh.Id, n);
            r.Ints(hh.Building, n);
            r.Ints(hh.Unit, n);
            r.Bytes(hh.District, n);
            r.Bytes(hh.Class, n);
            r.Bytes(hh.Size, n);
            r.Ints(hh.First, n);
            r.Ints(hh.Savings, n);
            r.Ints(hh.Debt, n);
            r.Ints(hh.Rent, n);
            r.Ints(hh.Landlord, n);
            r.Bytes(hh.Cars, n);
            r.UInts(hh.Tastes, n);
            r.Bytes(hh.Content, n);
            r.Bytes(hh.Flags, n);
            r.Ints(hh.Since, n);
            r.Ints(hh.Follows, n);
            for (int line = 0; line < Ledger.Lines; line++)
            {
                r.Strided(hh.Month, n, Ledger.Lines, line);
            }
            for (int line = 0; line < Ledger.Lines; line++)
            {
                r.Strided(hh.Last, n, Ledger.Lines, line);
            }
            hh.Refree();

            PersonTable pt = s.People;
            n = Count(r, 1 << 23);
            pt.Grow(Math.Max(128, n));
            pt.Count = n;
            r.Ints(pt.Id, n);
            r.Ints(pt.Household, n);
            r.Ints(pt.Next, n);
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
            r.Ints(pt.Partner, n);
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
