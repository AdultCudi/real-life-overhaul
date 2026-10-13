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
        SharedGovernment();
        SharedSites();
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

    private static GovState HostGov()
    {
        var s = new GovState();
        s.Reset();
        s.Seed = 991;
        s.FirstDay = 3;
        s.LastHour = 24 * 40 + 13;
        s.Mayor.First = 2;
        s.Mayor.Last = 5;
        s.Mayor.Party = 1;
        s.Mayor.Incumbent = true;
        s.TermStart = 12;
        s.Approval = 48.5f;
        s.Council[0] = 1;
        s.Council[3] = 2;
        s.Levers[2] = 1;
        s.Levers[5] = -1;
        s.Pending.Add(new PendingChange { Lever = 4, Position = 1, Day = 42 });
        s.Number = 3;
        s.ElectionDay = 45;
        s.Phase = ElectionPhase.Campaign;
        s.CandidateCount = 3;
        for (int c = 0; c < 3; c++)
        {
            s.Candidates[c].First = c + 1;
            s.Candidates[c].Party = c;
            s.Candidates[c].Planks = 2;
            s.Candidates[c].Platform[0] = c;
            s.Candidates[c].Platform[1] = c + 3;
            s.Polls[c] = 0.2f + 0.1f * c;
            s.Momentum[c] = 0.05f * c;
        }
        s.Voted = 1;                 // the host's own ballot
        s.OthersVotes[0] = 2;        // two guests' already in
        s.Donated[1] = 1500;         // the host's own gifts
        s.Favours = 2;
        s.CrimeHeat = 1.5f;
        s.PetitionPolicy = 4;
        s.PetitionSigned = 300;
        s.PetitionNeeded = 900;
        s.PetitionEnds = 50;
        s.PetitionMine = true;
        s.OwedTax = 999f;
        return s;
    }

    private static int AllVotes(GovState s, int c)
    {
        return s.OthersVotes[c] + (s.Voted == c ? 1 : 0);
    }

    // The government (part G): the host's election, policies and petition are the guest's; the guest's part in them stays its own.
    private static void SharedGovernment()
    {
        GovState host = HostGov();
        var guest = new GovState();
        guest.Reset();
        guest.Seed = 17;
        guest.Number = 3;
        guest.Phase = ElectionPhase.Campaign;
        guest.CandidateCount = 4;
        guest.Donated[2] = 500;
        guest.Endorsed = 2;
        guest.Favours = 1;
        guest.OwedTax = 12.5f;
        guest.SettleDay = 44;

        string snapshot = SharedThrough(SharedKind.Snapshot, 'G', CivicShare.WriteGov(host));
        Check(CivicShare.ApplyGov(guest, snapshot, out GovChange joined), "shared city, government: a guest takes the host's line");
        Check(CivicShare.SameGov(guest, host), "shared city, government: the guest's government is then the host's: mayor, council, levers, what is on its way, the election, polls, momentum, petition");
        Check(guest.Donated[2] == 500 && guest.Endorsed == 2 && guest.Voted == -1 && guest.Favours == 1 && guest.OwedTax == 12.5f && guest.SettleDay == 44 && !guest.PetitionMine,
            "shared city, government: and what the guest gave, whom it endorsed, its ballot, its favours and its own account stay its own (the host's petition is not the guest's)");
        Check(AllVotes(guest, 0) == AllVotes(host, 0) && AllVotes(guest, 1) == AllVotes(host, 1) && AllVotes(guest, 2) == AllVotes(host, 2),
            "shared city, government: every player's ballot counts the same on the guest as on the host (the host's own among them)");
        Check(joined.LeversMoved == ((1 << 2) | (1 << 5)) && !joined.NewElection, "shared city, government: the guest is told which policies stand elsewhere now");

        // The guest votes: its own ballot at once, and the host counts it with the next request.
        guest.Voted = 2;
        host.OthersVotes[2]++;
        host.Momentum[2] += 0.04f;
        host.PetitionSigned += 6;
        string update = SharedThrough(SharedKind.Update, 'G', CivicShare.WriteGov(host));
        Check(CivicShare.ApplyGov(guest, update, out GovChange voted) && CivicShare.SameGov(guest, host), "shared city, government: an update applied to the snapshot gives the host's state");
        Check(guest.Voted == 2 && AllVotes(guest, 2) == AllVotes(host, 2) && AllVotes(guest, 1) == AllVotes(host, 1), "shared city, government: the guest's ballot is counted once, not twice");

        // The count, then a new election.
        host.Phase = ElectionPhase.Counted;
        host.Winner = 2;
        host.Turnout = 21000;
        Check(CivicShare.ApplyGov(guest, CivicShare.WriteGov(host), out GovChange counted) && counted.Counted && counted.Phase && guest.Winner == 2 && guest.Turnout == 21000,
            "shared city, government: the host's count is the guest's, and the guest knows its election has been counted (to settle its favours and its mood)");
        host.Number = 4;
        host.Phase = ElectionPhase.Campaign;
        host.PetitionPolicy = 7;
        guest.PetitionMine = guest.PetitionISigned = true;
        Check(CivicShare.ApplyGov(guest, CivicShare.WriteGov(host), out GovChange next) && next.NewElection && guest.Voted == -1 && guest.Endorsed == -1 && guest.Donated[2] == 0
              && !guest.PetitionMine && !guest.PetitionISigned && guest.Favours == 1,
            "shared city, government: another election puts away the guest's part in the last one, and another petition its signature; the favours it is owed stay");

        // The save's line: the other players' ballots are kept, and a line from before them still reads.
        guest.OthersVotes[1] = 3;
        string line = guest.Write();
        var back = new GovState();
        Check(back.Read(line) && back.OthersVotes[1] == 3, "shared city, government: the save's line keeps the other players' ballots");
        string older = line.Substring(0, line.Length - 1);
        for (int i = 0; i < guest.OthersVotes.Length; i++)
        {
            older = older.Substring(0, older.LastIndexOf('|'));
        }
        older += "|";
        Check(back.Read(older) && back.OthersVotes[1] == 0 && back.Seed == guest.Seed, "shared city, government: a save's line from before the shared city still reads, with no other ballots");

        string before = guest.Write();
        Check(!CivicShare.ApplyGov(guest, "1|2|x", out _) && !CivicShare.ApplyGov(guest, "9|" + snapshot, out _) && guest.Write() == before,
            "shared city, government: a damaged line or one of another layout is refused and the guest's state is untouched");
    }

    private static SiteState FindSite(WorksState s, int id)
    {
        foreach (SiteState site in s.Sites)
        {
            if (site.Id == id)
            {
                return site;
            }
        }
        return null;
    }

    private static SiteState Site(int id, string street, int number, float done, bool mine)
    {
        return new SiteState { Id = id, Kind = SiteKind.Scaffold, Street = street, Number = number, Needed = 50f, Done = done, Mine = mine, UnsafeFrom = -1f };
    }

    // The building sites (part W): the host's programme, with the guest's own refit kept apart.
    private static void SharedSites()
    {
        var host = new WorksState { NextId = 9 };
        host.Sites.Add(Site(5, "ba:street_a", 10, 12f, false));
        host.Sites.Add(Site(7, "ba:street_b", 22, 30f, true));   // the host's own refit
        host.Sites.Add(Site(8, "ba:street_c", 4, 1f, false));
        host.Sites[0].Complaints = 2;
        host.Sites[0].QuietUntil = 41;

        var guest = new WorksState { NextId = 3, RefreshedDay = 20, RefreshedStreet = "ba:street_z", RefreshedNumber = 1 };
        guest.Sites.Add(Site(2, "ba:street_z", 9, 5f, true));    // the guest's own refit
        SiteState kept = Site(5, "ba:street_a", 10, 3f, false);    // the host's site as the guest last had it
        guest.Sites.Add(kept);
        guest.Sites.Add(Site(6, "ba:street_d", 1, 40f, false));  // one the host has handed over since

        string text = SharedThrough(SharedKind.Snapshot, 'W', CivicShare.WriteWorks(host));
        Check(CivicShare.ApplyWorks(guest, text), "shared city, sites: a guest takes the host's programme");
        Check(guest.Sites.Count == 4 && FindSite(guest, 6) == null && FindSite(guest, 8) != null && FindSite(guest, 7) != null && !FindSite(guest, 7).Mine,
            "shared city, sites: the guest has the host's three sites, none of them its own (the host's refit is a site of the city's), and not the one handed over");
        Check(FindSite(guest, 5) == kept && kept.Done == 12f && kept.Complaints == 2 && kept.QuietUntil == 41,
            "shared city, sites: a site it had is the same one, brought up to the host's (what stands in the street for it stays up)");
        SiteState own = null;
        foreach (SiteState s in guest.Sites)
        {
            own = s.Mine ? s : own;
        }
        Check(own != null && own.Id >= CivicShare.OwnSites && own.Street == "ba:street_z" && own.Done == 5f && guest.RefreshedDay == 20,
            "shared city, sites: the guest's own refit stays its own, with an id the host never gives, and so does when its shopfront was last refitted");
        Check(CivicShare.NextOwnId(guest) > own.Id && guest.NextId == 9, "shared city, sites: the next refit of the guest's is its own too, and the host's count of sites is the host's");
        host.Sites.RemoveAt(0);
        host.Sites.Add(Site(9, "ba:street_e", 2, 0f, false));
        Check(CivicShare.ApplyWorks(guest, CivicShare.WriteWorks(host)) && FindSite(guest, 5) == null && FindSite(guest, 9) != null && guest.Sites.Count == 4,
            "shared city, sites: an update applied to the snapshot gives the host's sites");
        Check(!CivicShare.ApplyWorks(guest, "1|x|") && guest.Sites.Count == 4, "shared city, sites: a damaged line is refused, the sites untouched");
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
