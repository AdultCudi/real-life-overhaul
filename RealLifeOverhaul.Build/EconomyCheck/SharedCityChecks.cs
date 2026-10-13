using System;
using System.Diagnostics;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The shared city of a Going Public session (Scripts/Core/SharedCity*): its messages written and read back exactly,
// another version's message read but never taken as this one's, and a damaged one dropped whole.
internal static partial class Program
{
    private static void Shared()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        SharedWire();
        SharedEconomy();
        Console.WriteLine("  Shared city: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static SharedMessage SharedSample(string version)
    {
        var m = new SharedMessage { Kind = SharedKind.Update, Version = version, Sequence = 41 };
        m.Parts.Add(new SharedPart('E', "v=1~h:1|2|3"));
        m.Parts.Add(new SharedPart('t', "D 12 00000000DEADBEEF\nF 12 40\n"));
        m.Parts.Add(new SharedPart('G', string.Empty));
        m.Parts.Add(new SharedPart('W', "pão, café e ação: " + new string('x', 20000)));
        return m;
    }

    // A part's text through the whole path a message takes: written into a message, as bytes, read back.
    private static string SharedThrough(SharedKind kind, char id, string text)
    {
        var m = new SharedMessage { Kind = kind, Version = "0.3.8", Sequence = 7 };
        m.Parts.Add(new SharedPart(id, text));
        SharedRead read = SharedCityWire.Read(SharedCityWire.Write(m), "0.3.8", out SharedMessage back);
        return read == SharedRead.Ok ? back.Part(id) : null;
    }

    private static void EconomyDays(CityState s, int from, int to)
    {
        for (int day = from; day <= to; day++)
        {
            CityModel.Day(s, day, new CityDay { Season = day / 15 % 4 }, MacroCatalogue.Standard, 5);
        }
    }

    // The economy (part E): a guest's state takes the city's figures of the host's, whole, and keeps its own.
    private static void SharedEconomy()
    {
        CityState host = CityModel.Open(4711, 1);
        CityModel.Know(host, "ba:neighborhood_a", 900);
        CityModel.Know(host, "ba:neighborhood_b", 1200);
        EconomyDays(host, 2, 140);
        host.CarryRent = 999;
        host.Buys = 63;
        host.News.Add(new CityNews { Day = 140, Kind = CityNewsKind.Rival, Name = "The host's shop" });

        CityState guest = CityModel.Open(17, 1);
        CityModel.Know(guest, "ba:neighborhood_a", 900);
        EconomyDays(guest, 2, 60);
        guest.CarryRent = 120;
        guest.CarryWages = 45;
        guest.CarryTakings = 7;
        guest.CarryLiving = 3;
        guest.LastCopycatDay = 55;
        guest.Buys = 5;
        guest.News.Add(new CityNews { Day = 60, Kind = CityNewsKind.Rival, Name = "The guest's shop" });

        string snapshot = SharedThrough(SharedKind.Snapshot, 'E', CityShare.Write(host));
        Check(snapshot != null && !snapshot.Contains("The host's shop"), "shared city, economy: the host's line goes whole through a message, without the paper's piece on the host's own rival");
        Check(CityShare.Apply(guest, snapshot, 5, out CityTick joined), "shared city, economy: a guest takes the host's line");
        Check(CityShare.SameCity(guest, host), "shared city, economy: the guest's city is then the host's in every figure (rate, cycle, index, inflation, districts, supply)");
        Check(guest.CarryRent == 120 && guest.CarryWages == 45 && guest.CarryTakings == 7 && guest.CarryLiving == 3 && guest.LastCopycatDay == 55 && guest.Buys == 5,
            "shared city, economy: and what the guest carries, its last rival and the goods its businesses stock stay the guest's");
        bool ownRival = false, hostRival = false;
        foreach (CityNews n in guest.News)
        {
            ownRival |= n.Kind == CityNewsKind.Rival && n.Name == "The guest's shop";
            hostRival |= n.Kind == CityNewsKind.Rival && n.Name == "The host's shop";
        }
        Check(ownRival && !hostRival, "shared city, economy: the guest's paper keeps its own rival and is never told of the host's");

        // The host goes on alone for a month and more; the guest counts no day; the update makes them one again.
        int rateBefore = host.Base;
        EconomyDays(host, 141, 152);
        Check(!CityShare.SameCity(guest, host), "shared city, economy: (the host's days have moved its city on)");
        string update = SharedThrough(SharedKind.Update, 'E', CityShare.Write(host));
        Check(CityShare.Apply(guest, update, 5, out CityTick tick) && CityShare.SameCity(guest, host), "shared city, economy: an update applied to the snapshot gives the host's state");
        Check(tick.MonthTurned && tick.RateMoved == host.Base - rateBefore, "shared city, economy: the guest hears that a month has turned and how far the rate moved in all (" + tick.RateMoved + ")");
        Check(guest.CarryRent == 120 && guest.Buys == 5, "shared city, economy: the update leaves the guest's own figures alone too");

        // A guest that leaves goes on from the host's last state: the same days give the same city as the host's.
        EconomyDays(host, 153, 200);
        EconomyDays(guest, 153, 200);
        Check(CityShare.SameCity(guest, host), "shared city, economy: a guest that leaves carries on from the host's last state, with no jump: its days are the host's days");

        // A line that cannot be read changes nothing.
        string before = CityStore.Write(guest);
        Check(!CityShare.Apply(guest, "v=1~x:damaged", 5, out _) && !CityShare.Apply(guest, null, 5, out _) && CityStore.Write(guest) == before,
            "shared city, economy: a damaged line is refused and the guest's state is untouched");
    }

    private static void SharedWire()
    {
        Check(SharedCityWire.Crc(Encoding.ASCII.GetBytes("123456789"), 0, 9) == 0xCBF43926u, "shared city: the CRC is the common CRC-32 (123456789 gives CBF43926)");

        SharedMessage sent = SharedSample("0.3.8");
        byte[] bytes = SharedCityWire.Write(sent);
        Check(bytes != null && bytes.Length < 2000, "shared city: a message is written, deflated (" + (bytes != null ? bytes.Length : 0) + " bytes for some 20,000 characters)");
        SharedRead read = SharedCityWire.Read(bytes, "0.3.8", out SharedMessage back);
        bool same = read == SharedRead.Ok && back.Kind == sent.Kind && back.Version == "0.3.8" && back.Sequence == 41 && back.Parts.Count == sent.Parts.Count;
        for (int i = 0; same && i < sent.Parts.Count; i++)
        {
            same = back.Parts[i].Id == sent.Parts[i].Id && back.Parts[i].Text == sent.Parts[i].Text;
        }
        Check(same, "shared city: a message reads back exactly: its kind, version, sequence and every part, letters and texts (accents included)");
        Check(back != null && back.Part('t') == sent.Parts[1].Text && back.Part('Z') == null, "shared city: a part is found by its letter, and a missing one is null");

        // Another version: read whole, so that its version can be named, but never Ok.
        read = SharedCityWire.Read(bytes, "0.3.9", out back);
        Check(read == SharedRead.OtherVersion && back != null && back.Version == "0.3.8", "shared city: a message of another version of the mod is read but is not taken as this version's");

        // Another layout: only the version is known.
        var later = SharedSample("0.4.0");
        later.Format = SharedCityWire.Format + 1;
        read = SharedCityWire.Read(SharedCityWire.Write(later), "0.3.8", out back);
        Check(read == SharedRead.OtherFormat && back != null && back.Version == "0.4.0" && back.Parts.Count == 0, "shared city: a message of a later layout names its version and gives nothing else");

        // Damage anywhere: dropped whole.
        int damaged = 0, tried = 0;
        for (int at = 0; at < bytes.Length; at += Math.Max(1, bytes.Length / 97))
        {
            var bad = (byte[])bytes.Clone();
            bad[at] ^= 0x5A;
            tried++;
            if (SharedCityWire.Read(bad, "0.3.8", out back) != SharedRead.Ok || back == null)
            {
                damaged++;
            }
        }
        Check(damaged == tried, "shared city: a byte changed anywhere in a message makes it unreadable, never a different message (" + damaged + " of " + tried + ")");
        var cut = new byte[bytes.Length - 7];
        Array.Copy(bytes, cut, cut.Length);
        Check(SharedCityWire.Read(cut, "0.3.8", out back) == SharedRead.Damaged && back == null, "shared city: a message cut short is damaged, and nothing of it is given");
        var longer = new byte[bytes.Length + 5];
        Array.Copy(bytes, longer, bytes.Length);
        Check(SharedCityWire.Read(longer, "0.3.8", out back) == SharedRead.Damaged, "shared city: a message with bytes after its end is damaged");
        Check(SharedCityWire.Read(null, "0.3.8", out back) == SharedRead.Damaged && SharedCityWire.Read(new byte[3], "0.3.8", out back) == SharedRead.Damaged
              && SharedCityWire.Read(Encoding.ASCII.GetBytes("{\"t\":250,\"from\":\"x\",\"d\":\"\"}"), "0.3.8", out back) == SharedRead.Damaged,
            "shared city: nothing, three bytes or another add-on's envelope are not a message of the mod's");
        var huge = new SharedMessage { Kind = SharedKind.Snapshot, Version = "0.3.8" };
        var noise = new StringBuilder(3 << 20);
        var random = new Random(5);
        for (int i = 0; i < (3 << 20); i++)
        {
            noise.Append((char)('!' + random.Next(90)));
        }
        huge.Parts.Add(new SharedPart('M', noise.ToString()));
        Check(SharedCityWire.Write(huge) == null, "shared city: a message larger than may be sent is not written at all");
        var hello = new SharedMessage { Kind = SharedKind.Hello, Version = "0.3.8", Sequence = 1 };
        read = SharedCityWire.Read(SharedCityWire.Write(hello), "0.3.8", out back);
        Check(read == SharedRead.Ok && back.Kind == SharedKind.Hello && back.Parts.Count == 0, "shared city: a hello, with no parts, reads back");
    }
}
