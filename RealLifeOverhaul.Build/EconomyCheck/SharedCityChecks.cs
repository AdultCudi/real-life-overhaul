using System;
using System.Diagnostics;
using System.Text;
using RealLifeOverhaul;

// The shared city of a Going Public session (Scripts/Core/SharedCity*): its messages written and read back exactly,
// another version's message read but never taken as this one's, and a damaged one dropped whole.
internal static partial class Program
{
    private static void Shared()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        SharedWire();
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
