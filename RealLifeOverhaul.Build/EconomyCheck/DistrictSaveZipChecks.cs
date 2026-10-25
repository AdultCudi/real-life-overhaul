using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.DistrictSave;

// The district save's payload in pieces (Scripts/DistrictSave/DistrictSaveZip.cs, DistrictSaveDeflate.cs), game-free:
// the line a save writes is the old line's records, then a payload that reads back, by the runtime's own gzip, as exactly
// the bytes the old one read back as; the same bytes give the same line on any number of workers and however they are
// written in; a piece that would not read back is caught; the check value and the codes are right at every edge; and a
// district's payload is compressed in a few milliseconds of the caller's thread.
internal static partial class Program
{
    private static void DistrictSaveZipChecks()
    {
        DistrictSaveZipSameAsOld();
        DistrictSaveZipEdges();
        DistrictSaveZipSteady();
        DistrictSaveZipCrc();
        DistrictSaveZipCaught();
        DistrictSaveZipTime();
    }

    // A sample state: the store's records as a save of a district writes them, and bytes standing for what the game's
    // serializer writes for their items (field names, ids and numbers again and again, as Odin's binary is).
    private static List<StoredRecord> ZipRecords()
    {
        var records = new List<StoredRecord>();
        for (int i = 0; i < 40; i++)
        {
            var r = new StoredRecord(i % 5 == 0 ? SaveKind.Demand : SaveKind.Registration, i * 3, i % 5 == 0 ? "ba:itemname_apple" : null) { Slot = i };
            r.Addresses.Add(new PlaceKey("reallife:street_uptown_" + (i % 7) + "_avenue", 100 + i));
            records.Add(r);
        }
        return records;
    }

    private static byte[] ZipSerializerBytes(int length, int seed)
    {
        var rnd = new Random(seed);
        var b = new byte[length];
        string[] words = { "itemInstances", "interiorDesigns", "StreetName", "reallife:street_uptown_3_avenue", "BuildingRegistration", "amount", "price", "employeeId" };
        int at = 0;
        while (at < length)
        {
            int kind = rnd.Next(4);
            if (kind == 0)
            {
                string w = words[rnd.Next(words.Length)];
                for (int k = 0; k < w.Length && at < length; k++)
                {
                    b[at++] = (byte)w[k];
                }
            }
            else if (kind == 1)
            {
                int n = rnd.Next(1, 9);
                for (int k = 0; k < n && at < length; k++)
                {
                    b[at++] = (byte)rnd.Next(256);
                }
            }
            else
            {
                b[at++] = (byte)(kind == 2 ? 0 : rnd.Next(1, 32));
            }
        }
        return b;
    }

    // The old way: one stream of the runtime's gzip, as DistrictSaveCodec.Encode writes it.
    private static string ZipOld(byte[] raw)
    {
        using (var packed = new MemoryStream())
        {
            using (var zip = new GZipStream(packed, CompressionLevel.Optimal, true))
            {
                zip.Write(raw, 0, raw.Length);
            }
            return Convert.ToBase64String(packed.GetBuffer(), 0, (int)packed.Length);
        }
    }

    // What a payload reads back as by the runtime's gzip; null when it cannot be read.
    private static byte[] ZipRead(string base64)
    {
        try
        {
            using (var plain = new MemoryStream())
            {
                using (var zip = new GZipStream(new MemoryStream(Convert.FromBase64String(base64)), CompressionMode.Decompress))
                {
                    zip.CopyTo(plain);
                }
                return plain.ToArray();
            }
        }
        catch (Exception e) when (e is FormatException || e is InvalidDataException)
        {
            return null;
        }
    }

    private static bool ZipSame(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    // Writes the bytes into the zip in uneven runs (and some single bytes), as a serializer's buffer does.
    private static string ZipNew(DistrictSaveZip zip, byte[] raw, string head, int seed)
    {
        var rnd = new Random(seed);
        zip.Begin();
        int at = 0;
        while (at < raw.Length)
        {
            if (rnd.Next(8) == 0)
            {
                zip.WriteByte(raw[at++]);
                continue;
            }
            int n = Math.Min(raw.Length - at, rnd.Next(1, 40000));
            zip.Write(raw, at, n);
            at += n;
        }
        return zip.Finish(head);
    }

    private static void DistrictSaveZipSameAsOld()
    {
        List<StoredRecord> records = ZipRecords();
        byte[] raw = ZipSerializerBytes(900000, 7);
        string oldLine = DistrictSaveStore.Write(records, ZipOld(raw));
        string head = DistrictSaveStore.Head(records);
        string newLine = ZipNew(new DistrictSaveZip(3), raw, head, 1);
        Check(newLine != null && DistrictSaveStore.Write(records, "PAYLOAD") == head + "PAYLOAD" && oldLine.StartsWith(head, StringComparison.Ordinal)
              && newLine.StartsWith(head, StringComparison.Ordinal) && head.EndsWith("^p:", StringComparison.Ordinal),
            "district save zip: the line a save writes has the old line's records, character for character, before its payload");
        StoredLine oldBack = DistrictSaveStore.Read(oldLine, out int _);
        StoredLine newBack = DistrictSaveStore.Read(newLine ?? string.Empty, out int _);
        bool records2 = oldBack != null && newBack != null && oldBack.Records.Count == newBack.Records.Count;
        for (int i = 0; records2 && i < oldBack.Records.Count; i++)
        {
            StoredRecord a = oldBack.Records[i], b = newBack.Records[i];
            records2 = a.Code == b.Code && a.Index == b.Index && a.Parent == b.Parent && a.Slot == b.Slot && a.Addresses.Count == b.Addresses.Count
                       && a.Addresses[0].Equals(b.Addresses[0]);
        }
        Check(records2, "district save zip: the store reads the same records from the line a save writes as from the old line");
        Check(newBack != null && oldBack != null && ZipSame(ZipRead(newBack.Payload), raw) && ZipSame(ZipRead(oldBack.Payload), raw),
            "district save zip: its payload reads back, by the runtime's own gzip (as DistrictSaveCodec.Decode reads every payload), "
            + "as exactly the bytes the old payload reads back as");
        Check(newBack != null && oldBack != null && newBack.Payload.Length < oldBack.Payload.Length * 6 / 5,
            "district save zip: its payload is within a fifth of the old one's size (" + (newBack != null ? newBack.Payload.Length : 0) + " characters against "
            + (oldBack != null ? oldBack.Payload.Length : 0) + ")");
        string empty = DistrictSaveStore.Head(new List<StoredRecord>());
        Check(empty == DistrictSaveStore.Write(new List<StoredRecord>(), string.Empty), "district save zip: a section with no payload is its records' head alone, as before");
    }

    private static void DistrictSaveZipEdges()
    {
        var zip = new DistrictSaveZip(2);
        int piece = DistrictSaveZip.PieceBytes;
        var rnd = new Random(11);
        var noise = new byte[piece * 2 + 77];
        rnd.NextBytes(noise);
        var cases = new List<KeyValuePair<string, byte[]>>
        {
            new KeyValuePair<string, byte[]>("nothing", new byte[0]),
            new KeyValuePair<string, byte[]>("one byte", new byte[] { 42 }),
            new KeyValuePair<string, byte[]>("three bytes", new byte[] { 1, 2, 3 }),
            new KeyValuePair<string, byte[]>("a piece exactly", ZipSerializerBytes(piece, 3)),
            new KeyValuePair<string, byte[]>("a piece and a byte", ZipSerializerBytes(piece + 1, 4)),
            new KeyValuePair<string, byte[]>("pieces and a window's end", ZipSerializerBytes(piece * 3 + DistrictSaveDeflate.Window - 1, 5)),
            new KeyValuePair<string, byte[]>("noise that does not compress", noise),
            new KeyValuePair<string, byte[]>("all zeros", new byte[piece * 2 + 5]),
            new KeyValuePair<string, byte[]>("one byte over and over", Repeat(new byte[] { 7 }, 300000)),
            new KeyValuePair<string, byte[]>("a long pattern across pieces", Repeat(ZipSerializerBytes(20000, 9), piece * 2 + 4321))
        };
        foreach (KeyValuePair<string, byte[]> c in cases)
        {
            string line = ZipNew(zip, c.Value, "h^p:", c.Value.Length);
            bool ok = line != null && line.StartsWith("h^p:", StringComparison.Ordinal) && ZipSame(ZipRead(line.Substring(4)), c.Value) && zip.Written == c.Value.Length;
            Check(ok, "district save zip: " + c.Key + " (" + c.Value.Length + " bytes) reads back as written" + (zip.Fault != null ? " (" + zip.Fault + ")" : string.Empty));
        }
    }

    private static byte[] Repeat(byte[] pattern, int length)
    {
        var b = new byte[length];
        for (int i = 0; i < length; i++)
        {
            b[i] = pattern[i % pattern.Length];
        }
        return b;
    }

    private static void DistrictSaveZipSteady()
    {
        byte[] raw = ZipSerializerBytes(700000, 21);
        string alone = ZipNew(new DistrictSaveZip(0), raw, "x^p:", 1);
        var four = new DistrictSaveZip(4);
        string many = ZipNew(four, raw, "x^p:", 2);
        string again = ZipNew(four, raw, "x^p:", 3);
        Check(alone != null && alone == many && many == again,
            "district save zip: the same bytes give the same line on the caller's thread alone and on four workers, however they are written in, and again with the buffers kept");
        string other = ZipNew(four, ZipSerializerBytes(250000, 22), "x^p:", 4);
        string back = ZipNew(four, raw, "x^p:", 5);
        Check(other != null && back == alone && ZipSame(ZipRead(other.Substring(4)), ZipSerializerBytes(250000, 22)),
            "district save zip: a smaller payload after a larger one, and the larger again, each read back as written (nothing of the last one left)");
        Check(DistrictSaveZip.WorkersHere >= 0 && DistrictSaveZip.WorkersHere <= Math.Max(0, Environment.ProcessorCount - 1) && new DistrictSaveZip(99).Workers <= 6,
            "district save zip: its workers are one fewer than the machine's processors, and never more than its most");
    }

    private static void DistrictSaveZipCrc()
    {
        byte[] raw = ZipSerializerBytes(400001, 31);
        byte[] gz = Convert.FromBase64String(ZipOld(raw));
        uint theirs = (uint)(gz[gz.Length - 8] | (gz[gz.Length - 7] << 8) | (gz[gz.Length - 6] << 16) | (gz[gz.Length - 5] << 24));
        uint whole = DistrictSaveDeflate.Crc(0, raw, 0, raw.Length);
        var even = new uint[32];
        var odd = new uint[32];
        var op = new uint[32];
        uint joined = DistrictSaveDeflate.Combine(DistrictSaveDeflate.Crc(0, raw, 0, 123457), DistrictSaveDeflate.Crc(0, raw, 123457, raw.Length - 123457), raw.Length - 123457, even, odd);
        DistrictSaveDeflate.Shift(raw.Length - 200000, op, even, odd);
        uint across = DistrictSaveDeflate.Across(DistrictSaveDeflate.Crc(0, raw, 0, 200000), DistrictSaveDeflate.Crc(0, raw, 200000, raw.Length - 200000), op);
        uint carried = DistrictSaveDeflate.Crc(DistrictSaveDeflate.Crc(0, raw, 0, 5), raw, 5, raw.Length - 5);
        Check(whole == theirs && joined == theirs && across == theirs && carried == theirs && DistrictSaveDeflate.Crc(0, raw, 0, 0) == 0u,
            "district save zip: the check value is the runtime's gzip's, made whole, joined from two runs (both ways) and carried on");
    }

    private static void DistrictSaveZipCaught()
    {
        byte[] raw = ZipSerializerBytes(60000, 41);
        var s = new DistrictSaveDeflate.Scratch();
        var buf = new byte[10 + DistrictSaveDeflate.Bound(raw.Length) + 10];
        Buffer.BlockCopy(DistrictSaveDeflate.GzipHeader, 0, buf, 0, 10);
        int size = DistrictSaveDeflate.Piece(raw, 0, raw.Length, buf, 10, s);
        uint crc = DistrictSaveDeflate.Crc(0, raw, 0, raw.Length);
        buf[10 + size] = DistrictSaveDeflate.LastBlock[0];
        buf[11 + size] = DistrictSaveDeflate.LastBlock[1];
        for (int i = 0; i < 4; i++)
        {
            buf[12 + size + i] = (byte)(crc >> (8 * i));
            buf[16 + size + i] = (byte)(raw.Length >> (8 * i));
        }
        int length = size + 20;
        Check(DistrictSaveZip.ReadsBack(buf, length, raw, raw.Length, s), "district save zip: a piece framed as its own gzip reads back by the runtime's gzip");
        byte[] changed = (byte[])raw.Clone();
        changed[raw.Length / 2] ^= 0x10;
        bool caughtChange = !DistrictSaveZip.ReadsBack(buf, length, changed, raw.Length, s);
        bool caughtShort = !DistrictSaveZip.ReadsBack(buf, length, raw, raw.Length + 1, s);
        byte[] broken = (byte[])buf.Clone();
        broken[10 + size / 2] ^= 0x5A;
        bool caughtBroken = !DistrictSaveZip.ReadsBack(broken, length, raw, raw.Length, s);
        Check(caughtChange && caughtShort && caughtBroken,
            "district save zip: a piece that reads back as other bytes, fewer bytes, or not at all is caught before anything is written (the save then writes the old way)");
    }

    private static void DistrictSaveZipTime()
    {
        byte[] raw = ZipSerializerBytes(3600000, 51);
        var zip = new DistrictSaveZip(DistrictSaveZip.WorkersHere);
        ZipNew(zip, raw, "h^p:", 1);   // warm: the buffers made, the code compiled
        double best = double.MaxValue;
        long allocated = long.MaxValue;
        string line = null;
        for (int round = 0; round < 3; round++)
        {
            ZipTimed(zip, raw, out double ms, out long bytes, out line);
            best = Math.Min(best, ms);
            allocated = Math.Min(allocated, bytes);
        }
        Check(line != null && best < 120.0,
            "district save zip: a district's 3.6 MB of the serializer's bytes written in and compressed in " + best.ToString("F1") + " ms on " + (zip.Workers + 1) + " thread(s) (at most 120)");
        long lineBytes = line != null ? line.Length * 2L : 0;
        Check(allocated < lineBytes + 512 * 1024,
            "district save zip: with its buffers kept, a save allocates little on the caller's thread beyond the line itself (" + (allocated / 1024) + " KB, the line " + (lineBytes / 1024) + " KB)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void ZipTimed(DistrictSaveZip zip, byte[] raw, out double ms, out long bytes, out string line)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        zip.Begin();
        for (int at = 0; at < raw.Length; at += 32768)
        {
            zip.Write(raw, at, Math.Min(32768, raw.Length - at));
        }
        line = zip.Finish("h^p:");
        ms = watch.Elapsed.TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
