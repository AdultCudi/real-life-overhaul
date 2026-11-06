using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Networking;

// Networking (Scripts/Networking): who holds a position, from the save's seed and how they were met (an introduction more
// often), the two favours of each position (never one twice), when each is ready, the gap between favours, the save's
// line and an older one, a job lead's name kept on its vacancy, the sources (the game touched only through
// NetworkGame, nothing on a frame), the texts in every language, and the day's pass timed.
internal static partial class Program
{
    private static void Networking()
    {
        NetworkRollChecks();
        NetworkFavourChecks();
        NetworkStoreChecks();
        NetworkLeadChecks();
        NetworkSourceChecks();
        NetworkTextChecks();
        NetworkTimingChecks();
    }

    private const int NetStreet = 10, NetWork = 3, NetEvent = 9, NetMom = 0, NetAlex = 1;   // SocialOrigin's numbers

    private static void NetworkRollChecks()
    {
        Check(NetworkRules.ChanceOf(NetMom, NetVia.Met) == 0 && NetworkRules.ChanceOf(NetAlex, NetVia.Friend) == 0 && NetworkRules.ChanceOf(NetMom, NetVia.Family) == 0,
            "networking: Mom and the old friend never hold a position, however they are counted");
        Check(NetworkRules.ChanceOf(NetStreet, NetVia.Met) < NetworkRules.ChanceOf(NetWork, NetVia.Met)
              && NetworkRules.ChanceOf(NetWork, NetVia.Met) < NetworkRules.ChanceOf(NetEvent, NetVia.Met)
              && NetworkRules.ChanceOf(NetEvent, NetVia.Met) < NetworkRules.ChanceOf(NetStreet, NetVia.Friend)
              && NetworkRules.ChanceOf(NetStreet, NetVia.Friend) < NetworkRules.ChanceOf(NetStreet, NetVia.Family),
            "networking: by chance in the street least, after a shift more, at an evening more, through a friend more again, through Mom most");
        Check(NetworkRules.ChanceOf(NetWork, NetVia.Friend) == NetworkRules.ChanceFriend && NetworkRules.ChanceOf(NetStreet, NetVia.Family) == NetworkRules.ChanceFamily,
            "networking: an introduction's chance is the introducer's, wherever the person is counted from");

        // Over many people the share holding a position is the chance, within a few points.
        foreach (NetVia via in new[] { NetVia.Met, NetVia.Friend, NetVia.Family })
        {
            int held = 0, n = 20000;
            var roles = new int[(int)NetRole.Landlord + 1];
            for (int i = 0; i < n; i++)
            {
                NetRole r = NetworkRules.RoleOf(424242, i, NetStreet, via);
                held += r != NetRole.None ? 1 : 0;
                roles[(int)r]++;
            }
            int wanted = NetworkRules.ChanceOf(NetStreet, via) * n / 1000;
            Check(Math.Abs(held - wanted) < n / 50, "networking: about " + wanted + " of " + n + " met " + via + " hold a position: " + held);
            bool every = true;
            for (int r = 1; r < roles.Length; r++)
            {
                every &= roles[r] > held / roles.Length / 2;
            }
            Check(every, "networking: every position is dealt among those met " + via);
        }

        // The same person is the same on every machine and every load; another seed is another city of people.
        bool same = true, differs = false;
        for (int i = 0; i < 400; i++)
        {
            same &= NetworkRules.RoleOf(77, i, NetStreet, NetVia.Friend) == NetworkRules.RoleOf(77, i, NetStreet, NetVia.Friend);
            differs |= NetworkRules.RoleOf(77, i, NetStreet, NetVia.Friend) != NetworkRules.RoleOf(78, i, NetStreet, NetVia.Friend);
        }
        Check(same && differs, "networking: a person's roll is fixed by the seed and their place in the cast, and another seed rolls others");
        Check(NetworkRules.Hash(1, 2, 3) == NetworkRules.Hash(1, 2, 3) && NetworkRules.Hash(1, 2, 3) != NetworkRules.Hash(1, 2, 4) && NetworkRules.Hash(1, 2, 3) != NetworkRules.Hash(2, 1, 3),
            "networking: the hash is fixed and every argument counts");
        Check(NetworkRules.Text("Elm Street") == NetworkRules.Text("Elm Street") && NetworkRules.Text("Elm Street") != NetworkRules.Text("Elm Streets")
              && NetworkRules.Text(null) == NetworkRules.Text(string.Empty) && NetworkRules.Text("a") == unchecked((int)((2166136261u ^ 'a') * 16777619u)),
            "networking: a text's number is FNV-1a, the same on every machine (never string.GetHashCode)");
        int orgs = 0;
        for (int i = 0; i < 100; i++)
        {
            int at = NetworkRules.OrgOf(5, i, 6);
            orgs |= at >= 0 && at < 6 ? 1 << at : 1 << 30;
        }
        Check(orgs == 63 && NetworkRules.OrgOf(5, 3, 0) == 0, "networking: the place they work for is one of the names, all of them dealt, none past the list, and none asked of an empty list");
        Check(NetworkRules.PlaceDraw(9, 2, "Elm", 4) == NetworkRules.PlaceDraw(9, 2, "Elm", 4) && NetworkRules.PlaceDraw(9, 2, "Elm", 4) != NetworkRules.PlaceDraw(9, 3, "Elm", 4)
              && NetworkRules.PlaceDraw(9, 2, "Elm", 4) != NetworkRules.PlaceDraw(9, 2, "Elm", 5),
            "networking: an importer or a building for sale is drawn by person and address, needing no order of the game's lists");
        foreach (NetRole role in NetworkRules.Roles)
        {
            string[] names = NetworkCatalogue.OrgsOf(role);
            Check(names != null && names.Length >= 4, "networking: " + role + " has places to work for");
        }
        Check(NetworkCatalogue.OrgsOf(NetRole.None) == null && NetworkRules.Roles.Length == (int)NetRole.Landlord, "networking: every position is dealt, and None is not one");
    }

    private static NetContact NetAs(NetRole role, int given = 0)
    {
        return new NetContact { Person = 4, Role = role, Given = given, MetDay = 10 };
    }

    private static void NetworkFavourChecks()
    {
        var seen = new HashSet<NetFavour>();
        foreach (NetRole role in NetworkRules.Roles)
        {
            NetFavour first = NetworkRules.FavourOf(role, 0), second = NetworkRules.FavourOf(role, 1);
            Check(first != NetFavour.None && second != NetFavour.None && first != second, "networking: " + role + " offers two different favours: " + first + ", " + second);
            seen.Add(first);
            seen.Add(second);
        }
        Check(seen.Count == (int)NetFavour.Property, "networking: every favour is offered by some position: " + seen.Count);
        Check(NetworkRules.FavourOf(NetRole.None, 0) == NetFavour.None && NetworkRules.FavourOf(NetRole.Recruiter, 2) == NetFavour.None
              && NetworkRules.FavourOf(NetRole.Recruiter, -1) == NetFavour.None && NetworkRules.FavourOf((NetRole)99, 0) == NetFavour.None,
            "networking: nobody and nothing past the two favours");
        Check(NetworkRules.FavourOf(NetRole.Recruiter, 0) == NetFavour.JobLead && NetworkRules.FavourOf(NetRole.Buyer, 0) == NetFavour.Importer
              && NetworkRules.FavourOf(NetRole.Wholesale, 1) == NetFavour.Terms && NetworkRules.FavourOf(NetRole.Investor, 0) == NetFavour.Investor
              && NetworkRules.FavourOf(NetRole.Landlord, 0) == NetFavour.Property,
            "networking: a recruiter's lead, a buyer's importer, a manager's terms, an investor's meeting, a landlord's tip come first or where they belong");

        const int friend = NetworkRules.FirstStage, close = NetworkRules.SecondStage, day = 20, long_ago = -1000;
        NetContact c = NetAs(NetRole.Recruiter);
        Check(NetworkRules.Next(c, friend - 1, 10, day, long_ago) == NetFavour.None, "networking: an acquaintance offers nothing");
        Check(NetworkRules.Next(c, friend, 10, day, long_ago) == NetFavour.JobLead, "networking: a friend offers the first favour");
        Check(NetworkRules.Next(c, friend, 10, 10 + NetworkRules.KnownDays - 1, long_ago) == NetFavour.None && NetworkRules.Next(c, friend, 10, 10 + NetworkRules.KnownDays, long_ago) == NetFavour.JobLead,
            "networking: nothing before they have been known " + NetworkRules.KnownDays + " days");
        Check(NetworkRules.Next(c, friend, 10, day, day - NetworkRules.GapDays + 1) == NetFavour.None && NetworkRules.Next(c, friend, 10, day, day - NetworkRules.GapDays) == NetFavour.JobLead,
            "networking: nothing within " + NetworkRules.GapDays + " days of another favour, from anybody");
        c.Given = NetContact.Bit(NetFavour.JobLead);
        Check(NetworkRules.Next(c, friend, 10, day, long_ago) == NetFavour.None, "networking: with the first done, a friend offers nothing more: no favour twice");
        Check(NetworkRules.Next(c, close, 10, day, long_ago) == NetFavour.Freelance && NetworkRules.Next(c, 5, 10, day, long_ago) == NetFavour.Freelance,
            "networking: a close friend (or a partner) offers the second");
        c.Given |= NetContact.Bit(NetFavour.Freelance);
        Check(NetworkRules.Next(c, 5, 10, day, long_ago) == NetFavour.None && NetworkRules.Next(c, 5, 10, day, long_ago, true) == NetFavour.None,
            "networking: both done, nothing ever again, not even for a test");
        NetContact fresh = NetAs(NetRole.Lawyer);
        Check(NetworkRules.Next(fresh, 0, day, day, day, true) == NetFavour.Freelance, "networking: a test waives the stage, the days known and the gap, never what was done");
        NetContact skipped = NetAs(NetRole.Lawyer, NetContact.Bit(NetFavour.JobLead));
        Check(NetworkRules.Next(skipped, friend, 10, day, long_ago) == NetFavour.Freelance, "networking: a favour of the first step not yet done comes first whatever else was done");
        Check(NetworkRules.Next(NetAs(NetRole.None), 5, 10, day, long_ago, true) == NetFavour.None && NetworkRules.Next(null, 5, 10, day, long_ago) == NetFavour.None,
            "networking: somebody with no position, or nobody, offers nothing");
        var salts = new HashSet<int>();
        for (int p = 0; p < 400; p++)
        {
            for (int f = 1; f <= (int)NetFavour.Property; f++)
            {
                salts.Add(NetworkRules.Salt(p, (NetFavour)f));
            }
        }
        Check(salts.Count == 400 * 7, "networking: every person's every favour has its own salt, so the same favour draws the same thing and two never collide");
        Check(NetContact.Bit(NetFavour.JobLead) != NetContact.Bit(NetFavour.Property) && !NetAs(NetRole.Buyer).Has(NetFavour.Importer) && NetAs(NetRole.Buyer, 8).Has(NetFavour.Importer),
            "networking: a favour done is one bit of its own");
    }

    private static void NetworkStoreChecks()
    {
        var f = new NetworkFile { LastFavourDay = 31 };
        f.Contacts.Add(new NetContact { Person = 3, Role = NetRole.Buyer, Via = NetVia.Friend, MetDay = 12, Given = 8, Told = 24, Street = "ba:street_elm", Number = 40, Org = "Pier|Nine\nImports" });
        f.Contacts.Add(new NetContact { Person = 9, Role = NetRole.None, Via = NetVia.Met, MetDay = 14 });
        f.Contacts.Add(new NetContact { Person = 399, Role = NetRole.Official, Via = NetVia.Family, MetDay = 2, Org = "reallife_social_net_org_planning" });
        string line = NetworkStore.Write(f);
        NetworkFile back = NetworkStore.Read(line);
        Check(back != null && NetworkStore.Write(back) == line, "networking: the save's line reads back the same");
        NetContact b = back != null ? back.Of(3) : null;
        Check(b != null && b.Role == NetRole.Buyer && b.Via == NetVia.Friend && b.MetDay == 12 && b.Given == 8 && b.Told == 24 && b.Street == "ba:street_elm" && b.Number == 40
              && b.Org == "Pier Nine Imports", "networking: a person's record comes back whole, the place's dividers made spaces");
        Check(back != null && back.LastFavourDay == 31 && back.Of(9) != null && back.Of(9).Role == NetRole.None && back.Of(399).Org == "reallife_social_net_org_planning",
            "networking: the last favour's day, somebody with no position (rolled once, kept) and an office's key come back");
        Check(NetworkStore.Read("NW2|5") == null && NetworkStore.Read("junk") == null && NetworkStore.Read(null) == null, "networking: another version's line, or none, is not read");
        NetworkFile later = NetworkStore.Read("NW1|7\nX|a later kind\nC|5|4|1|3|0|0||0|Harbour Talent|more|fields\nC|6|99|0|3|0|0||0|x\nC|5|1|0|3|0|0||0|twice\nC|-1|1|0|3|0|0||0|x\nC|7|1|9|3|0|0||0|x");
        Check(later != null && later.LastFavourDay == 7 && later.Contacts.Count == 1 && later.Of(5).Role == NetRole.Recruiter && later.Of(5).Org == "Harbour Talent",
            "networking: a later version's kinds and fields are skipped; a position, a way of meeting or a person this version does not know, or a person twice, is left out");
        var data = new Dictionary<string, string>();
        NetworkFile none = NetworkStore.Load(data);
        Check(none != null && none.Contacts.Count == 0 && none.LastFavourDay < -100, "networking: a save without the line starts with nobody rolled and no favour done");
        NetworkStore.Save(data, f);
        Check(data.TryGetValue(NetworkStore.Key, out string saved) && saved == line && NetworkStore.Key == "reallife:network", "networking: the line is the save's own entry, reallife:network");
        var bad = new Dictionary<string, string> { { NetworkStore.Key, "NW9|1" } };
        Check(NetworkStore.Load(bad) == null && bad[NetworkStore.Key] == "NW9|1", "networking: a line this version cannot read stands the feature down and is kept as it was read");
        var full = new System.Text.StringBuilder("NW1|0");
        for (int i = 0; i < NetworkFile.Most + 10; i++)
        {
            full.Append("\nC|").Append(i % (NetworkFile.Most + 5)).Append("|1|0|1|0|0||0|x");
        }
        NetworkFile many = NetworkStore.Read(full.ToString());
        Check(many != null && many.Contacts.Count == NetworkFile.Most, "networking: never more than the cast's " + NetworkFile.Most + " people: " + (many != null ? many.Contacts.Count : -1));
    }

    // A job lead is a vacancy with the friend's name on it: kept in the save's vacancy line, never in an application's or the job's.
    private static void NetworkLeadChecks()
    {
        var f = new StudyFile { Seed = 5 };
        WorkFile w = f.Work;
        w.Openings.Add(new Opening { Id = 3, Prof = ProCatalogue.Lawyer, Rank = 1, Kind = (int)EmployerKind.City, Employer = "Harbour Law", Street = "Elm", Number = 2, Pay = 5000, From = 1, To = 15,
            Via = (int)Via.Referral, Lead = "Dana Park" });
        w.Openings.Add(new Opening { Id = 4, Prof = ProCatalogue.Lawyer, Rank = 0, Kind = (int)EmployerKind.City, Employer = "Quay Partners", Street = "Elm", Number = 3, Pay = 4000, From = 1, To = 15 });
        var a = new Application { Day = 2, Stage = (int)ApplyStep.Sent, Answer = 5, Score = 60 };
        w.Openings[0].CopyTo(a.Post);
        w.Applications.Add(a);
        Check(a.Post.Lead == "Dana Park", "networking: a lead's name is copied with its post");
        string line = CareersStore.Write(f);
        StudyFile back = CareersStore.Read(line, out _);
        Check(back != null && back.Work.Openings.Count == 2 && back.Work.Openings[0].Lead == "Dana Park" && back.Work.Openings[1].Lead == string.Empty && back.Work.Openings[0].Via == (int)Via.Referral,
            "networking: a lead's vacancy comes back with who passed it on, another with nobody");
        Check(back != null && back.Work.Applications.Count == 1 && back.Work.Applications[0].Score == 60 && back.Work.Applications[0].Answer == 5,
            "networking: the application of a lead reads back its own fields, nothing shifted by the lead");
        Check(CareersStore.Write(back) == line, "networking: the careers line with a lead reads back the same");
        var c = new Candidate { Points = 50, Need = 0, Qualified = true, Referral = false };
        int plain = ProRules.HireScore(c);
        c.Referral = true;
        Check(ProRules.HireScore(c) == plain + 10 || ProRules.HireScore(c) == 100, "networking: a lead counts as a referral, ten more of the hire score: " + plain + " to " + ProRules.HireScore(c));
    }

    private static string NetworkSource(string name)
    {
        return ModSource(@"Networking\" + name);
    }

    private static void NetworkSourceChecks()
    {
        string[] free = { "NetworkFile.cs", "NetworkRules.cs", "NetworkStore.cs", "NetworkCatalogue.cs", "NetworkBridges.cs", "NetworkDirector.cs", "NetworkDirector.Tests.cs", "NetworkGuide.cs" };
        foreach (string name in free)
        {
            string code = NetworkSource(name);
            Check(code.Length > 0, "networking sources: " + name + " is there");
            Check(!Regex.IsMatch(code, @"using UnityEngine|SaveGameManager|BuildingHelper|Notifications\.|MonoBehaviour|GoingPublic|GetLocalization"),
                "networking sources: " + name + " does not touch the game or Unity: only NetworkGame does");
        }
        string all = string.Join("\n", Array.ConvertAll(free, NetworkSource)) + NetworkSource("NetworkGame.cs");
        Check(!Regex.IsMatch(all, @"void (Update|LateUpdate|FixedUpdate)\(|StartCoroutine|UnityEngine\.Random|System\.Random|DateTime\.Now|GetHashCode\(\)"),
            "networking sources: nothing on a frame, no coroutine, and no roll but the seeded hash");
        string game = NetworkSource("NetworkGame.cs");
        Check(game.Contains("GoingPublic.InSession") && game.Contains("GoingPublic.Mine(reg)"), "networking sources: a tip lowers no price, and better terms touch no other player's contracts, in a Going Public session");
        Check(game.Contains("GetOrAddBusinessContact(true)"), "networking sources: an introduction is the game's own contact of the importer or wholesaler, as its manager gives it");
        string director = NetworkSource("NetworkDirector.cs");
        Check(director.Contains("c.Given |= NetContact.Bit(favour)") && Regex.Matches(director, @"Given \|=").Count == 1, "networking sources: a favour is kept as done in one place, after it was done");
        string links = ModSource(@"Wiring\Wiring.NetworkLinks.cs");
        Check(links.Contains("SocialLinks.Met = Met") && links.Contains("SocialLinks.Met = null") && links.Contains("NetworkBridges.Reset()"), "networking sources: the links are put on and taken off");
        string meet = ModSource(@"Social\MeetScene.cs");
        Check(meet.Contains("SocialLife.Meet(_who, talk.Warm, _kind == Kind.Introduction ? _friend : null)") && meet.Contains("Via = _kind == Kind.Introduction ? _friend : null"),
            "networking sources: an introduction's friend counts for the roll whether or not they are still beside the person, so the words and the record agree");
    }

    private static void NetworkTextChecks()
    {
        var keys = new List<string>();
        foreach (string file in new[] { "NetworkCatalogue.cs", "NetworkDirector.cs", "NetworkDirector.Tests.cs", "NetworkGuide.cs" })
        {
            foreach (Match m in Regex.Matches(NetworkSource(file), "\"(reallife_[a-z_]+)\""))
            {
                if (!m.Groups[1].Value.EndsWith("_", StringComparison.Ordinal) && !keys.Contains(m.Groups[1].Value))
                {
                    keys.Add(m.Groups[1].Value);
                }
            }
        }
        foreach (NetRole role in NetworkRules.Roles)
        {
            keys.AddRange(new[] { NetworkCatalogue.RoleKey(role), NetworkCatalogue.MeetKey(role), NetworkCatalogue.IntroKey(role), NetworkCatalogue.TextKey(role) });
        }
        for (int f = 1; f <= (int)NetFavour.Property; f++)
        {
            var favour = (NetFavour)f;
            keys.AddRange(new[] { NetworkCatalogue.ReadyKey(favour), NetworkCatalogue.ButtonKey(favour), NetworkCatalogue.DoneKey(favour), NetworkCatalogue.NameKey(favour) });
        }
        keys.AddRange(new[] { "reallife_opt_tip_social_networking", "reallife_career_post_lead", "reallife_career_note_lead", "reallife_ven_note_introduced", "reallife_social_net_why_no_lead",
            "reallife_social_net_why_no_contract", "reallife_social_net_why_no_investor" });
        Check(keys.Count >= 80, "networking texts: its keys are found: " + keys.Count);
        string[] notices = { NetworkCatalogue.Met, "reallife_career_note_lead", "reallife_ven_note_introduced", NetworkCatalogue.TestNobody };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "networking texts: " + code + ".json is read");
            if (words == null || english == null)
            {
                continue;
            }
            foreach (string key in keys)
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "networking texts: " + key + " is in " + code + ".json");
                if (!there)
                {
                    continue;
                }
                Check(!value.Contains('!') && !value.Contains('！') && !value.Contains('—') && !value.Contains('–') && !value.Contains(" - "),
                    "networking texts: " + key + " has no exclamation mark or dash in " + code);
                Check(Holes(value) == Holes(english.TryGetValue(key, out string en) ? en : string.Empty), "networking texts: " + key + " keeps the English placeholders in " + code);
            }
            foreach (string key in notices)
            {
                Check(words.TryGetValue(key, out string value) && value.Length <= 240, "networking texts: the notice " + key + " is at most 240 characters in " + code);
            }
            if (words.TryGetValue("reallife_guide_networking", out string page))
            {
                foreach (string fact in new[] { "{net_street}", "{net_friend}", "{net_family}", "{net_known}", "{net_gap}", "{net_tip}" })
                {
                    Check(page.Contains(fact), "networking texts: the Guide page names " + fact + " in " + code);
                }
            }
        }
        string guide = NetworkSource("NetworkGuide.cs");
        foreach (string fact in new[] { "net_street", "net_friend", "net_family", "net_known", "net_gap", "net_tip" })
        {
            Check(guide.Contains(".Fact(\"" + fact + "\""), "networking texts: the Guide's {" + fact + "} is filled from the rules");
        }
    }

    // The placeholders of a text, in order of their names: what a translation must keep.
    private static string Holes(string text)
    {
        var names = new List<string>();
        foreach (Match m in Regex.Matches(text ?? string.Empty, @"\{[a-z_]+\}"))
        {
            if (!names.Contains(m.Value))
            {
                names.Add(m.Value);
            }
        }
        names.Sort(StringComparer.Ordinal);
        return string.Join(",", names);
    }

    private static void NetworkTimingChecks()
    {
        var f = new NetworkFile();
        for (int i = 0; i < NetworkFile.Most; i++)
        {
            f.Contacts.Add(new NetContact { Person = i, Role = NetworkRules.RoleOf(3, i, NetStreet, NetVia.Friend), MetDay = 1 });
        }
        var watch = Stopwatch.StartNew();
        int ready = 0;
        for (int round = 0; round < 100; round++)
        {
            for (int i = 0; i < f.Contacts.Count; i++)
            {
                ready += NetworkRules.Next(f.Contacts[i], 3, 1, 40, -1000) != NetFavour.None ? 1 : 0;
            }
        }
        watch.Stop();
        Check(ready > 0 && watch.Elapsed.TotalMilliseconds / 100 < 2.0, "networking: a day's pass over " + NetworkFile.Most + " people takes under 2 ms: " + (watch.Elapsed.TotalMilliseconds / 100).ToString("0.000") + " ms");
        watch.Restart();
        string line = NetworkStore.Write(f);
        NetworkFile back = NetworkStore.Read(line);
        watch.Stop();
        Check(back != null && back.Contacts.Count == NetworkFile.Most && watch.Elapsed.TotalMilliseconds < 50, "networking: the whole cast's line is written and read in under 50 ms: " + watch.Elapsed.TotalMilliseconds.ToString("0.0") + " ms");
    }
}
