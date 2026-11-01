using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using RealLifeOverhaul.DistrictSave;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city as one line of the save's modData (<c>reallife:flare_city</c>): <c>v=1~</c> and the payload, gzip in base64, compressed in pieces
    /// on the save's workers as the population's own line is (DistrictSaveZip). The payload is binary columns, every number a variable-length whole: a
    /// header, the accounts by key, each resident's follows by their person id (a load finds their slot by the id, so the follows go with the person,
    /// never the slot), each business's figures by its address key (a load binds them to the business at that address), the funnel, the ads and the
    /// voices. A later version adds columns at the end, which this reader does not ask for; a line it cannot read gives no state, so the follows are
    /// made again, and the line is left in the save until the next save writes this version's. Game-free. COST: nothing per frame; at a save the
    /// writing (the compressing on workers), at a load the reading on a worker, both measured by EconomyCheck.
    /// </summary>
    public static class FlareCityStore
    {
        public const string Key = "reallife:flare_city";
        public const int Version = 1;
        private static readonly byte[] Magic = { (byte)'R', (byte)'L', (byte)'O', (byte)'F' };

        public static string Head => "v=" + Version + "~";

        /// <summary>The line through the district save's zip, its pieces compressed on the workers; null when a piece failed (write it with <see cref="Encode"/>).</summary>
        public static string Line(FlareCityState c, DistrictSaveZip zip)
        {
            zip.Begin();
            try
            {
                Write(c, zip);
            }
            catch
            {
                zip.Finish(string.Empty);   // every worker done before the fault goes up
                throw;
            }
            return zip.Finish(Head);
        }

        /// <summary>The line through one stream of the runtime's own gzip: for a save whose pieces failed, and for the checks.</summary>
        public static string Encode(FlareCityState c)
        {
            using (var raw = new MemoryStream())
            {
                using (var gz = new GZipStream(raw, CompressionLevel.Optimal, true))
                {
                    Write(c, gz);
                }
                return Head + Convert.ToBase64String(raw.GetBuffer(), 0, (int)raw.Length);
            }
        }

        /// <summary>The state of a line, its follows by person id and its businesses by key (FlareCityStore.Bind places them); null for a line it cannot read.</summary>
        public static FlareCityState Decode(string line, out int version)
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

        public static void Write(FlareCityState c, Stream to)
        {
            var w = new Writer(to);
            w.Bytes(Magic);
            w.Int(Version);
            w.Int(c.Seed);
            w.Int(c.Day);
            w.Int(c.Generated);
            w.Int(c.NextAd);
            w.Int(c.CityFollowers);

            w.Int(c.Accounts.Count);
            for (int a = 0; a < c.Accounts.Count; a++)
            {
                w.Text(c.Accounts[a]);
            }

            // the residents' follows: only slots that hold a person Flare has met, by the person's id (minus for somebody seen too young)
            int people = 0;
            for (int p = 0; p < c.Owner.Length; p++)
            {
                people += c.Owner[p] != 0 ? 1 : 0;
            }
            w.Int(people);
            for (int p = 0; p < c.Owner.Length; p++)
            {
                if (c.Owner[p] != 0)
                {
                    w.Int(c.Owner[p]);
                }
            }
            for (int p = 0; p < c.Owner.Length; p++)
            {
                if (c.Owner[p] == 0)
                {
                    continue;
                }
                int n = 0;
                for (int i = 0; i < FlareCityCatalogue.Slots; i++)
                {
                    n += c.Follows[p * FlareCityCatalogue.Slots + i] != FlareCityCatalogue.None ? 1 : 0;
                }
                w.Int(n);
            }
            for (int p = 0; p < c.Owner.Length; p++)
            {
                if (c.Owner[p] == 0)
                {
                    continue;
                }
                for (int i = 0; i < FlareCityCatalogue.Slots; i++)
                {
                    ushort f = c.Follows[p * FlareCityCatalogue.Slots + i];
                    if (f != FlareCityCatalogue.None)
                    {
                        w.Int(f);
                    }
                }
            }

            // each business with figures, by its key
            int businesses = 0;
            for (int b = 0; b < c.BusinessCount; b++)
            {
                businesses += c.BizKey[b] != null ? 1 : 0;
            }
            w.Int(c.Districts);
            w.Int(businesses);
            for (int b = 0; b < c.BusinessCount; b++)
            {
                if (c.BizKey[b] == null)
                {
                    continue;
                }
                w.Text(c.BizKey[b]);
                for (int d = 0; d < c.Districts; d++)
                {
                    w.Int(c.Heard[b * c.Districts + d]);
                }
                for (int d = 0; d < c.Districts; d++)
                {
                    w.Int(c.Sour[b * c.Districts + d]);
                }
                w.Int(c.Answered[b] ? 1 : 0);
                w.Int(c.Attention[b]);
                w.Int(c.Given[b]);
                w.Long(c.Lift[b]);
                w.Int(c.LiftVisits[b]);
                w.Int(c.Followers[b]);
                w.Long(c.RatingSum[b]);
                w.Long(c.RatingCount[b]);
                w.Int(c.ReviewDue[b]);
                w.Int(c.ReviewCount[b]);
                w.Int(c.ReviewHead[b]);
                for (int r = 0; r < FlareCityCatalogue.ReviewRing; r++)
                {
                    FlareCityReview rv = c.Reviews[b * FlareCityCatalogue.ReviewRing + r];
                    w.Int(rv.Person);
                    w.Int(rv.Stars);
                    w.Int(rv.Day);
                }
            }

            w.Int(c.Funnels.Count);
            foreach (FlareFunnel f in c.Funnels)
            {
                w.Int(f.Post);
                w.Int((int)f.Kind);
                w.Text(f.Place);
                w.Int(f.Day);
                w.Int(f.Seen);
                w.Int(f.Interested);
                w.Int(f.Followed);
                w.Int(f.Came);
                w.Long(f.Spent);
                w.Int(f.Live);
            }

            w.Int(c.Ads.Count);
            foreach (FlareAd ad in c.Ads)
            {
                w.Int(ad.Id);
                w.Text(ad.Place);
                w.Text(ad.Name);
                w.Int(ad.District);
                w.Int(ad.Age);
                w.Int(ad.Class);
                w.Int(ad.Interest);
                w.Int(ad.Reach);
                w.Int(ad.Days);
                w.Int(ad.From);
                w.Long(ad.Cost);
                w.Int(ad.Mine ? 1 : 0);
            }

            w.Int(c.Voices.Length);
            for (int v = 0; v < c.Voices.Length; v++)
            {
                w.Int(c.Voices[v]);
            }

            w.Int(c.NextOffer);
            w.Int(c.CreatorDay);
            w.Int(c.ScandalDay);
            w.Int(c.Offers.Count);
            foreach (FlareOffer o in c.Offers)
            {
                w.Int(o.Id);
                w.Text(o.Key);
                w.Text(o.Name);
                w.Int(o.Posts);
                w.Int(o.Done);
                w.Long(o.Fee);
                w.Int(o.Day);
                w.Int(o.Until);
                w.Int(o.Accepted ? 1 : 0);
                w.Int(o.Next);
            }

            w.Int(c.NextTrend);
            w.Int(c.Trends.Count);
            foreach (FlareTrend t in c.Trends)
            {
                w.Int(t.Id);
                w.Int((int)t.Kind);
                w.Int(t.What);
                w.Int(t.District);
                w.Int(t.Day);
                w.Int(t.Mine ? 1 : 0);
                w.Int(t.Let);
                w.Int(t.Ids.Count);
                int last = 0;
                for (int i = 0; i < t.Ids.Count; i++)
                {
                    w.Int(t.Ids[i] - last);   // each id as its step from the one before: a byte or two
                    last = t.Ids[i];
                }
            }
            w.Flush();
        }

        // ---------------------------------------------------------------- reading

        /// <summary>The follows read, by person id, waiting for a load's people (FlareCityStore.Bind).</summary>
        public sealed class Loaded
        {
            public int[] Ids = new int[0];
            public int[] Counts = new int[0];
            public ushort[] Follows = new ushort[0];
        }

        private static FlareCityState Read(Stream from, int version)
        {
            var r = new Reader(from);
            byte[] magic = r.Bytes(Magic.Length);
            for (int i = 0; i < Magic.Length; i++)
            {
                if (magic[i] != Magic[i])
                {
                    return null;
                }
            }
            if (r.Int() != version)
            {
                return null;
            }
            var c = new FlareCityState { Seed = r.Int(), Day = r.Int(), Generated = r.Int(), NextAd = r.Int(), CityFollowers = r.Int() };
            int accounts = Bounded(r.Int(), FlareCityCatalogue.AccountsMost);
            c.Accounts.Clear();   // the line's registry whole, the player's first as written
            for (int a = 0; a < accounts; a++)
            {
                c.Accounts.Add(r.TextOrNull());
            }
            c.IndexAccounts();

            int people = Bounded(r.Int(), 4000000);
            var loaded = new Loaded { Ids = new int[people], Counts = new int[people] };
            for (int i = 0; i < people; i++)
            {
                loaded.Ids[i] = r.Int();
            }
            int follows = 0;
            for (int i = 0; i < people; i++)
            {
                loaded.Counts[i] = Bounded(r.Int(), FlareCityCatalogue.Slots);
                follows += loaded.Counts[i];
            }
            loaded.Follows = new ushort[follows];
            for (int i = 0; i < follows; i++)
            {
                int f = r.Int();
                loaded.Follows[i] = f >= 0 && f < accounts ? (ushort)f : FlareCityCatalogue.None;
            }
            c.Pending = loaded;

            int districts = Bounded(r.Int(), 64);
            int businesses = Bounded(r.Int(), 1000000);
            c.GrowBusinesses(businesses, Math.Max(1, districts));
            for (int b = 0; b < businesses; b++)
            {
                c.BizKey[b] = r.TextOrNull();
                for (int d = 0; d < districts; d++)
                {
                    c.Heard[b * c.Districts + d] = Math.Max(0, r.Int());
                }
                for (int d = 0; d < districts; d++)
                {
                    c.Sour[b * c.Districts + d] = Math.Max(0, r.Int());
                }
                c.Answered[b] = r.Int() != 0;
                c.Attention[b] = r.Int();
                c.Given[b] = r.Int();
                c.Lift[b] = r.Long();
                c.LiftVisits[b] = r.Int();
                c.Followers[b] = r.Int();
                c.RatingSum[b] = r.Long();
                c.RatingCount[b] = r.Long();
                c.ReviewDue[b] = r.Int();
                c.ReviewCount[b] = r.Int();
                c.ReviewHead[b] = (byte)Math.Max(0, Math.Min(FlareCityCatalogue.ReviewRing - 1, r.Int()));
                for (int k = 0; k < FlareCityCatalogue.ReviewRing; k++)
                {
                    c.Reviews[b * FlareCityCatalogue.ReviewRing + k] = new FlareCityReview { Person = r.Int(), Stars = (byte)Math.Max(0, Math.Min(5, r.Int())), Day = r.Int() };
                }
            }

            int funnels = Bounded(r.Int(), FlareCityCatalogue.FunnelsMost * 4);
            for (int i = 0; i < funnels; i++)
            {
                var f = new FlareFunnel
                {
                    Post = r.Int(), Kind = (FunnelKind)Math.Max(0, Math.Min((int)FunnelKind.Business, r.Int())), Place = r.TextOrNull(), Day = r.Int(), Seen = r.Int(),
                    Interested = r.Int(), Followed = r.Int(), Came = r.Int(), Spent = r.Long(), Live = Math.Max(0, r.Int())
                };
                c.Funnels.Add(f);
            }
            int ads = Bounded(r.Int(), FlareCityCatalogue.AdsMost * 4);
            for (int i = 0; i < ads; i++)
            {
                c.Ads.Add(new FlareAd
                {
                    Id = r.Int(), Place = r.TextOrNull(), Name = r.TextOrNull(), District = r.Int(), Age = r.Int(), Class = r.Int(), Interest = r.Int(), Reach = r.Int(),
                    Days = r.Int(), From = r.Int(), Cost = r.Long(), Mine = r.Int() != 0
                });
            }
            int voices = Bounded(r.Int(), 100000);
            for (int v = 0; v < voices; v++)
            {
                int id = r.Int();
                if (v < c.Voices.Length)
                {
                    c.Voices[v] = id;
                }
            }

            c.NextOffer = r.Int();
            c.CreatorDay = r.Int();
            c.ScandalDay = r.Int();
            int offers = Bounded(r.Int(), FlareCityRules.OffersMost * 4);
            for (int i = 0; i < offers; i++)
            {
                c.Offers.Add(new FlareOffer
                {
                    Id = r.Int(), Key = r.TextOrNull(), Name = r.TextOrNull(), Posts = Math.Max(1, r.Int()), Done = r.Int(), Fee = Math.Max(0, r.Long()), Day = r.Int(), Until = r.Int(),
                    Accepted = r.Int() != 0, Next = r.Int()
                });
            }

            c.NextTrend = Math.Max(1, r.Int());
            int trends = Bounded(r.Int(), FlareCityCatalogue.TrendsMost * 4);
            for (int i = 0; i < trends; i++)
            {
                var t = new FlareTrend
                {
                    Id = r.Int(), Kind = (TrendKind)Math.Max(0, Math.Min((int)TrendKind.Genre, r.Int())), What = r.Int(), District = r.Int(), Day = r.Int(), Mine = r.Int() != 0,
                    Let = r.Int()
                };
                int moved = Bounded(r.Int(), FlareCityCatalogue.TrendMovedMost);
                int last = 0;
                for (int m = 0; m < moved; m++)
                {
                    last += r.Int();
                    t.Ids.Add(last);
                    t.Slots.Add(-1);   // found again by id as it fades (FlareCityRules.TrendsDay)
                }
                t.Let = Math.Max(0, Math.Min(t.Let, moved));
                bool whole = t.Kind == TrendKind.Genre ? t.What >= 0 && t.What < Population.GameRules.Genres : FlareCityRules.TasteBit(t.What) >= 0;
                if (whole && (t.Kind != TrendKind.District || t.District >= 0) && c.Trends.Count < FlareCityCatalogue.TrendsMost)
                {
                    c.Trends.Add(t);
                }
            }
            FlareCityRules.TrendLookups(c);
            return c;
        }

        private static int Bounded(int n, int most)
        {
            if (n < 0 || n > most)
            {
                throw new InvalidDataException("a count out of its bounds");
            }
            return n;
        }

        /// <summary>
        /// The follows read placed on the people of the population as it stands: each person by their id to their slot (a person gone since is left
        /// out). Called once the population is in use, before the first pass. How many residents' follows were placed.
        /// </summary>
        public static int Bind(FlareCityState c, Population.PopulationState s, int[] personSlot)
        {
            Loaded l = c.Pending;
            c.Pending = null;
            if (l == null)
            {
                return 0;
            }
            c.GrowPeople(s.People.Count);
            for (int p = 0; p < c.Owner.Length; p++)
            {
                c.Owner[p] = 0;
                FlareCityRules.ClearFollows(c, p);
            }
            int placed = 0, at = 0;
            for (int i = 0; i < l.Ids.Length; i++)
            {
                int id = l.Ids[i];
                int n = l.Counts[i];
                int slot = FlareCityRules.Slot(personSlot, s, Math.Abs(id));
                if (slot >= 0)
                {
                    c.Owner[slot] = id;
                    for (int k = 0; k < n; k++)
                    {
                        if (l.Follows[at + k] != FlareCityCatalogue.None)
                        {
                            FlareCityRules.Follow(c, slot, l.Follows[at + k]);
                        }
                    }
                    placed++;
                }
                at += n;
            }
            return placed;
        }

        // ---------------------------------------------------------------- the columns

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
