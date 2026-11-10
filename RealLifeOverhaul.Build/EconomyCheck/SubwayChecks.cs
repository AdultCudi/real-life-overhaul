using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Schools;
using RealLifeOverhaul.Subway;
using RealLifeOverhaul.Uptown;

// The subway stations of Uptown and Cedar Heights (Scripts/Subway, run with --subway; their ground is NavCheck's, UptownSubwayCheck.cs): where a
// station goes in the game's own list, which is in the order of its names, and what a typed search shows; when the stations stand (their option,
// the districts, the game's own network) and when they may go (never under a rider); the people of the line by the trains' clock, the same on
// every machine, more in the rush and few at night, beside the bus stops' own; Birchwood's entrance against the campus's gates, their crowds and
// its fence; the sources (the fare is the game's alone and never the mod's to waive, nothing saved, nothing sent, nothing a frame, the game's own
// Start replaced by one that is taken off again, no second station of a site, what is keyed to the game's twenty leaving the mod's out); and the
// texts in every language.
internal static partial class Program
{
    // The game's twenty stations as its own files have them: the members of Enums.SubwayStationName in BigAmbitions.dll (twenty, Midtown four, no Grand Central)
    // and for each the English name its locale gives under "subwaystation_" and the member in lower case. Null when the game is not there or is not as known.
    private static List<string> SubwayGameStationNames(out string why)
    {
        why = null;
        string dll = System.IO.Path.Combine(GameManagedFolder(), "BigAmbitions.dll");
        Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignGameFiles, "locale", "en.json"));
        if (!System.IO.File.Exists(dll) || words == null)
        {
            why = "the game's BigAmbitions.dll or its locale is not at " + GameManagedFolder();
            return null;
        }
        var members = new List<string>();
        using (var pe = new PEReader(System.IO.File.OpenRead(dll)))
        {
            MetadataReader md = pe.GetMetadataReader();
            foreach (TypeDefinitionHandle handle in md.TypeDefinitions)
            {
                TypeDefinition type = md.GetTypeDefinition(handle);
                if (md.GetString(type.Name) != "SubwayStationName" || md.GetString(type.Namespace) != "Enums")
                {
                    continue;
                }
                foreach (FieldDefinitionHandle field in type.GetFields())
                {
                    string name = md.GetString(md.GetFieldDefinition(field).Name);
                    if (name != "value__")
                    {
                        members.Add(name);
                    }
                }
            }
        }
        var names = new List<string>();
        foreach (string member in members)
        {
            if (!words.TryGetValue("subwaystation_" + member.ToLowerInvariant(), out string english))
            {
                why = "the game's locale has no name for " + member;
                return null;
            }
            names.Add(english);
        }
        return names;
    }

    // A source with its comments taken out, so a word in a comment can neither trip nor hide a check of what the code does.
    private static string SubwayCode(string source)
    {
        return Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"//[^\r\n]*", string.Empty);
    }

    // The keys of locales.py's ENGLISH_OK (and the languages of ENGLISH_OK_IN) read from the quotes of its code, its comments taken out first:
    // an apostrophe in a comment pairs with the next quote and drops every key between them.
    private static HashSet<string> SubwayQuotedKeys(string script, string block)
    {
        Match found = Regex.Match(script, block + @" = \{(.*?)\n\}", RegexOptions.Singleline);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var code = new System.Text.StringBuilder();
        foreach (string line in found.Groups[1].Value.Split('\n'))
        {
            bool quoted = false;
            foreach (char c in line)
            {
                if (c == '\'')
                {
                    quoted = !quoted;
                }
                else if (c == '#' && !quoted)
                {
                    break;
                }
                code.Append(c);
            }
            code.Append('\n');
        }
        foreach (Match m in Regex.Matches(code.ToString(), @"'([^']+)'"))
        {
            keys.Add(m.Groups[1].Value);
        }
        return keys;
    }

    private static readonly string[] SubwaySources =
    {
        @"Subway\StationsRules.cs", @"Subway\StationsGoing.cs", @"Subway\StationsGame.cs", @"Subway\StationEntrance.cs", @"Subway\StationsDirector.cs", @"Subway\StationsDirector.Riders.cs",
        @"Subway\StationsDirector.Tests.cs", @"Subway\StationsGuide.cs", @"Subway\StationsText.cs"
    };

    private static void Subway()
    {
        SubwayListChecks();
        SubwayStandChecks();
        SubwayGoingChecks();
        SubwayRideChecks();
        SubwayFigureChecks();
        SubwayRiderChecks();
        SubwayCampusChecks();
        SubwaySourceChecks();
        SubwayTextChecks();
    }

    // ---- the game's list of stations

    private static void SubwayListChecks()
    {
        Comparison<string> order = (a, b) => string.Compare(a, b, CultureInfo.GetCultureInfo("en-US"), CompareOptions.None);
        List<string> names = SubwayGameStationNames(out string readWhy);
        Check(names != null && names.Count == 20, "subway list: the game's own twenty stations are read from its files (the members of its SubwayStationName and its locale's names)" + (readWhy != null ? ": " + readWhy : string.Empty));
        if (names == null || names.Count != 20)
        {
            return;
        }
        names.Sort(order);
        Check(names[0].StartsWith("Garment", StringComparison.Ordinal) && names[19].StartsWith("The Hamptons", StringComparison.Ordinal) && names.Count(n => n.StartsWith("Midtown", StringComparison.Ordinal)) == 4
              && !names.Any(n => n.Contains("Grand Central")) && names.Distinct().Count() == 20, "subway list: the game's own names, sorted as it sorts them: Midtown has four and no Grand Central");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        string birchwood = en != null && en.TryGetValue(StationsRules.BirchwoodKey, out string b) ? b : null, summit = en != null && en.TryGetValue(StationsRules.SummitKey, out string s) ? s : null,
            east = en != null && en.TryGetValue(StationsRules.EastKey, out string e) ? e : null;
        Check(birchwood != null && summit != null && east != null, "subway list: the three stations have their English names");
        if (birchwood == null || summit == null || east == null)
        {
            return;
        }
        // Each joins at its name's place, and the list stays in order.
        foreach (string name in new[] { east, birchwood, summit })
        {
            int at = StationsRules.InsertAt(names, name, order);
            names.Insert(at, name);
        }
        bool sorted = true;
        for (int i = 1; i < names.Count; i++)
        {
            sorted &= order(names[i - 1], names[i]) <= 0;
        }
        Check(sorted && names.Count == 23, "subway list: with the three stations joined at their names' places the list of 23 is still in the order of its names");
        Check(names.IndexOf(birchwood) == 0 && names.IndexOf(summit) == 1 && names.IndexOf(east) == 22,
            "subway list: Cedar Heights' two come first and Uptown's last, by their names (" + names.IndexOf(birchwood) + ", " + names.IndexOf(summit) + ", " + names.IndexOf(east) + ")");
        Check(StationsRules.InsertAt(new List<string>(), "x", order) == 0 && StationsRules.InsertAt(null, "x", order) == 0, "subway list: into an empty list, or none, a station goes first");
        Check(StationsRules.InsertAt(new List<string> { "a", "b", "c" }, "d", order) == 3 && StationsRules.InsertAt(new List<string> { "b", "c" }, "a", order) == 0
              && StationsRules.InsertAt(new List<string> { "a", "c" }, "b", order) == 1, "subway list: after the last, before the first and between two");
        Check(StationsRules.InsertAt(new List<string> { "a", "b", "b", "c" }, "b", order) == 3, "subway list: a name the list has already goes after its like");
        // The search: as the game's own reads a name.
        StringComparison loose = StringComparison.OrdinalIgnoreCase;
        Check(StationsRules.Shown(birchwood, string.Empty, loose) && StationsRules.Shown(birchwood, null, loose) && StationsRules.Shown(birchwood, "   ", loose),
            "subway search: with nothing typed a station is shown");
        Check(StationsRules.Shown(birchwood, "birch", loose) && StationsRules.Shown(birchwood, "  BIRCHWOOD ", loose) && StationsRules.Shown(east, "uptown", loose) && StationsRules.Shown(summit, "park", loose),
            "subway search: a part of its own name shows it, whatever the case and the spaces round it");
        Check(!StationsRules.Shown(birchwood, "midtown", loose) && !StationsRules.Shown(east, "garment", loose) && !StationsRules.Shown(summit, "birchwood", loose) && !StationsRules.Shown(null, "x", loose),
            "subway search: the name of the game's entrance it was copied from does not show it, nor another station's");
        Check(!StationsRules.Shown(birchwood, "BIRCH", StringComparison.Ordinal), "subway search: the comparison is the caller's (the game's ignores case)");
        // The listener's order: the mod's is taken off and put on last, once, each time the window is shown, after whatever the game has put on since.
        var listeners = new List<string> { "game", "mine" };
        Action off = () => listeners.Remove("mine"), on = () => listeners.Add("mine");
        StationsRules.HangLast(off, on);
        bool once = listeners.SequenceEqual(new[] { "game", "mine" });
        listeners.Insert(0, "game later");
        listeners.Remove("mine");
        listeners.Add("game last");
        StationsRules.HangLast(off, on);
        Check(once && listeners.SequenceEqual(new[] { "game later", "game", "game last", "mine" }), "subway search: the mod's listener is on once and always after the game's, however many the game has put on since: " + string.Join(", ", listeners));
    }

    // ---- standing and going

    private static void SubwayStandChecks()
    {
        Check(StationsRules.Wanted(true, true, 20), "subway standing: with the option on, the districts open and the game's stations there, they stand");
        Check(!StationsRules.Wanted(false, true, 20), "subway standing: with the option off they do not stand");
        Check(!StationsRules.Wanted(true, false, 20), "subway standing: with the districts not laid or closed they do not stand");
        Check(!StationsRules.Wanted(true, true, 0) && !StationsRules.Wanted(true, true, -3), "subway standing: with none of the game's own stations there is no network to join");
        IReadOnlyList<StationSite> sites = StationsRules.Sites;
        Check(sites.Count == 3 && sites.Select(x => x.Id).Distinct().Count() == 3 && sites.Select(x => x.NameKey).Distinct().Count() == 3 && sites.All(x => Regex.IsMatch(x.Id, "^[a-z]+$")),
            "subway standing: three stations, each with an id of letters and a name of its own");
        Check(sites.All(x => x.Yaw % 90 == 0 && x.Yaw >= 0 && x.Yaw < 360) && sites.All(x => StationsRules.GroundOf(x) < 0f && StationsRules.GroundOf(x) > -0.1f),
            "subway standing: each at a quarter turn, its origin as far into its ground as the game's own stand (" + string.Join(", ", sites.Select(x => StationsRules.GroundOf(x).ToString("F4", CultureInfo.InvariantCulture))) + ")");
        bool lots = sites.All(x => StationsRules.MeetsLot(x, UptownLots.All) == null && StationsRules.MeetsLot(x, UptownLots.Places) == null);
        Check(lots && UptownLots.All.Count > 600 && UptownLots.All.Any(l => l.Open), "subway standing: none on a lot of the plan, of its " + UptownLots.All.Count + " (the open runs the player builds on among them) or its big special places");
    }

    // ---- going: one rule for every cause, never under a rider

    private static StationHolds Held(bool riding, bool touches, bool taxi, bool map) => new StationHolds(riding, touches, taxi, map);

    private static void SubwayGoingChecks()
    {
        // The rule by what holds them: all sixteen states, written out (riding, the ride touches one of theirs, a taxi trip, the map open from one of theirs).
        StationRemoval N = StationRemoval.Now, M = StationRemoval.MapFirst, R = StationRemoval.AfterRide, T = StationRemoval.AfterTaxi;
        StationRemoval[] expected = { N, M, T, T, N, M, T, T, N, M, T, T, R, R, R, R };
        bool table = true;
        string wrong = string.Empty;
        for (int bits = 0; bits < 16; bits++)
        {
            StationRemoval got = StationsRules.Removal((bits & 8) != 0, (bits & 4) != 0, (bits & 2) != 0, (bits & 1) != 0);
            if (got != expected[bits])
            {
                table = false;
                wrong += " [" + Convert.ToString(bits, 2).PadLeft(4, '0') + " gave " + got + "]";
            }
        }
        Check(table, "subway going: of the sixteen states of (riding, the ride touches theirs, a taxi trip, the map open from theirs) a ride that touches theirs holds them, a taxi trip holds them, "
                     + "the map is shut first, and otherwise they go" + wrong);
        Check(StationsRules.Removal(true, true, true, true) == StationRemoval.AfterRide && StationsRules.Removal(false, false, true, false) == StationRemoval.AfterTaxi,
            "subway going: a taxi trip under way, to one of theirs or not (the game does not say where), holds them as a ride does");

        // Every cause against every state: the three the director waits on, the director going, and the city going.
        StationExit[] waits = { StationExit.OptionOff, StationExit.DistrictClosing, StationExit.CleanUp };
        bool causes = true;
        string causeWrong = string.Empty;
        foreach (StationExit cause in waits)
        {
            for (int bits = 0; bits < 16; bits++)
            {
                var going = new StationsGoing();
                StationHolds holds = Held((bits & 8) != 0, (bits & 4) != 0, (bits & 2) != 0, (bits & 1) != 0);
                StationStep step = going.Go(cause, "w", holds);
                StationStep want = expected[bits] == R || expected[bits] == T ? StationStep.Wait : expected[bits] == M ? StationStep.ShutMapThenRemove : StationStep.Remove;
                bool right = step == want && going.Waiting == (want == StationStep.Wait) && (!going.Waiting || (going.Cause == cause && going.Why == "w" && going.Holding == expected[bits]));
                if (!right)
                {
                    causes = false;
                    causeWrong += " [" + cause + " " + Convert.ToString(bits, 2).PadLeft(4, '0') + " gave " + step + "]";
                }
                var gone = new StationsGoing();
                StationStep byDirector = gone.Go(StationExit.DirectorGone, "w", holds);
                StationStep wantDirector = want == StationStep.Wait ? StationStep.HandOver : want;
                if (byDirector != wantDirector || gone.Waiting)
                {
                    causes = false;
                    causeWrong += " [DirectorGone " + Convert.ToString(bits, 2).PadLeft(4, '0') + " gave " + byDirector + "]";
                }
                var unloading = new StationsGoing();
                if (unloading.Go(StationExit.CityUnloading, "w", holds) != StationStep.Drop || unloading.Waiting)
                {
                    causes = false;
                    causeWrong += " [CityUnloading " + Convert.ToString(bits, 2).PadLeft(4, '0') + "]";
                }
            }
        }
        Check(causes, "subway going: the option off, the districts closing and the clean-up wait for a ride or a taxi trip, shut the map first, or go; the director going hands a wait to a watcher; "
                      + "the city going drops them, whatever is held" + causeWrong);

        // A ride, a taxi trip and the rest over time: asked every look from tick 3; the stations go on the first look nothing holds them, never before.
        Check(Timeline(StationExit.OptionOff, 3, t => Held(t >= 2 && t <= 8, true, false, false)) == 9, "subway going: the option switched off at tick 3 of a ride (ticks 2 to 8) takes them away at tick 9, the first look after the rider is up");
        Check(Timeline(StationExit.CleanUp, 5, t => Held(false, false, t >= 4 && t <= 20, false)) == 21, "subway going: the clean-up at tick 5 of a taxi trip (ticks 4 to 20) takes them away at tick 21, when the rider is down");
        Check(Timeline(StationExit.DistrictClosing, 3, t => Held(t >= 3 && t <= 5, true, t >= 6 && t <= 9, false)) == 10,
            "subway going: a ride followed at once by a taxi trip holds them through both (ticks 3 to 9), they go at tick 10");
        Check(Timeline(StationExit.OptionOff, 3, t => Held(t >= 2 && t <= 8, false, false, false)) == 3, "subway going: a ride between two of the game's own stations holds them back no more than none");
        Check(Timeline(StationExit.OptionOff, 3, t => Held(false, false, false, t >= 1 && t <= 6)) == 3, "subway going: the map open on their list is shut at once and they go in the same look");
        Check(Timeline(StationExit.DirectorGone, 3, t => Held(t >= 2 && t <= 8, true, false, false)) == 9,
            "subway going: the director destroyed during a ride hands it to the watcher, which asks the same rule and takes them away at tick 9");
        Check(Timeline(StationExit.DirectorGone, 3, t => Held(false, false, t >= 2 && t <= 8, false)) == 9, "subway going: the director destroyed during a taxi trip hands it to the watcher too, which takes them away at tick 9");
        Check(Timeline(StationExit.DirectorGone, 3, t => Held(t >= 2, true, false, false), unloadAt: 6) == -6, "subway going: the city unloading while the watcher waits drops them with the scene at tick 6 and takes nothing out under the ride");
        // The option switched back on while a ride was let finish: the going is called off and the look asks no more.
        var back = new StationsGoing();
        StationStep first = back.Go(StationExit.OptionOff, "off", Held(true, true, false, false));
        back.Cancel();
        Check(first == StationStep.Wait && !back.Waiting && back.Why == null && back.Holding == StationRemoval.Now, "subway going: switched back on while a ride is let finish, the going is called off");
        // The rule's own invariant over many seeded sequences of states: no step that takes them away is ever asked of a state that holds them.
        var rng = new Random(20261111);
        int asked = 0;
        bool never = true;
        for (int run = 0; run < 3000; run++)
        {
            var going = new StationsGoing();
            StationExit cause = (StationExit)rng.Next(0, 4);
            bool riding = false, touches = false, taxi = false, map = false;
            for (int tick = 0; tick < 40; tick++)
            {
                riding ^= rng.Next(0, 6) == 0;
                touches ^= rng.Next(0, 5) == 0;
                taxi ^= rng.Next(0, 7) == 0;
                map ^= rng.Next(0, 5) == 0;
                StationHolds holds = Held(riding, touches, taxi, map);
                StationStep step = tick == 0 || going.Waiting || cause == StationExit.DirectorGone ? going.Go(cause, "x", holds) : StationStep.Wait;
                asked++;
                bool held = (riding && touches) || taxi;
                never &= !(held && (step == StationStep.Remove || step == StationStep.ShutMapThenRemove));
                if (step == StationStep.Remove || step == StationStep.ShutMapThenRemove)
                {
                    break;
                }
            }
        }
        Check(never && asked > 5000, "subway going: over 3000 seeded sequences of states (" + asked + " looks) no look takes them away under a ride that touches them or a taxi trip");

        // The order they are let go in: what the game walks or points at first, the entrance itself last.
        IReadOnlyList<StationsRules.StationPart> order = StationsRules.GoingOrder;
        int At(StationsRules.StationPart part)
        {
            for (int k = 0; k < order.Count; k++)
            {
                if (order[k] == part)
                {
                    return k;
                }
            }
            return -1;
        }
        bool whole = order.Count == Enum.GetValues(typeof(StationsRules.StationPart)).Length && order.Distinct().Count() == order.Count;
        Check(whole && At(StationsRules.StationPart.Entrance) == order.Count - 1 && At(StationsRules.StationPart.GameList) == 0 && At(StationsRules.StationPart.GameList) < At(StationsRules.StationPart.Registry)
              && At(StationsRules.StationPart.WindowEntry) < At(StationsRules.StationPart.Entrance) && At(StationsRules.StationPart.HoverCard) < At(StationsRules.StationPart.Entrance)
              && At(StationsRules.StationPart.MapMarker) < At(StationsRules.StationPart.Entrance) && At(StationsRules.StationPart.TravelCard) < At(StationsRules.StationPart.Entrance),
            "subway going: a station leaves the game's list first and the entrance is destroyed last, after its window entry, the cards over it and its marker on the map (" + string.Join(", ", order) + ")");

        // A station is not stood on the player.
        StationSite site = StationsRules.Sites[1];
        StationsRules.Footprint(site, out float x0, out float z0, out float x1, out float z1);
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        Check(StationsRules.Occupied(site, cx, cz) && StationsRules.Occupied(site, x0 - 0.4f, cz) && StationsRules.Occupied(site, cx, z1 + 0.4f) && !StationsRules.Occupied(site, x0 - 0.8f, cz)
              && !StationsRules.Occupied(site, cx, z1 + 0.8f) && !StationsRules.Occupied(site, x1 + 0.8f, z1 + 0.8f),
            "subway standing: the player on an entrance's ground, or within " + StationsRules.PlayerReach + " m of it, is on it; 0.8 m off they are not");
    }

    // The tick a going is carried out at, or -1 if never, or the negative of the tick the scene took them: the going is asked from tick `from` (the cause), then every
    // tick while it waits, as the director's look and the watcher do.
    private static int Timeline(StationExit cause, int from, Func<int, StationHolds> holds, int unloadAt = -1)
    {
        var going = new StationsGoing();
        bool asking = false;
        for (int tick = from; tick < 60; tick++)
        {
            StationExit by = unloadAt >= 0 && tick >= unloadAt ? StationExit.CityUnloading : cause;
            StationStep step;
            if (!asking)
            {
                step = going.Go(by, "t", holds(tick));
                asking = step == StationStep.Wait || step == StationStep.HandOver;
            }
            else
            {
                step = cause == StationExit.DirectorGone || by == StationExit.CityUnloading ? going.Go(by, "t", holds(tick)) : going.Again(holds(tick));
            }
            if (step == StationStep.Drop)
            {
                return -tick;
            }
            if (step == StationStep.Remove || step == StationStep.ShutMapThenRemove)
            {
                return tick;
            }
        }
        return -1;
    }

    // ---- a ride's record: where it began, its stops

    private static void SubwayRideChecks()
    {
        // The game's own order (the frame a ride begins: SubwaySystem.TravelToCoroutine sets IsRiding, then the map's close sets lastSubwayStation to null) leaves the journey
        // nothing but the camera's start; the origin is found from it, or from the station seen while the map was open.
        var x = new List<float> { 0f, 400f, 900f, float.NaN };
        var z = new List<float> { 0f, 0f, 100f, float.NaN };
        Check(SubwayRideRules.Origin(0f, 0f, x, z, -1) == 0 && SubwayRideRules.Origin(12f, -5f, x, z, -1) == 0 && SubwayRideRules.Origin(400f, 3f, x, z, -1) == 1,
            "subway ride: with the game's lastSubwayStation already null, the origin is the station at the camera's start, a few metres on or not");
        Check(SubwayRideRules.Origin(55f, 0f, x, z, -1) == -1 && SubwayRideRules.Origin(200f, 0f, x, z, 0) == -1, "subway ride: a start beyond " + SubwayRideRules.OriginReach + " m from every station is no station's");
        Check(SubwayRideRules.Origin(10f, 0f, x, z, 1) == 0 && SubwayRideRules.Origin(395f, 0f, x, z, 1) == 1, "subway ride: the station seen while the map was open counts only while the start is near it");
        Check(SubwayRideRules.Origin(0f, 0f, x, z, 3) == 0 && SubwayRideRules.Origin(0f, 0f, x, z, 9) == 0 && SubwayRideRules.Origin(0f, 0f, null, null, -1) == -1 && SubwayRideRules.Origin(0f, 0f, x, z, -5) == 0,
            "subway ride: a station that is gone (no position), an index out of range and no list at all do not throw or get chosen");
        Check(SubwayRideRules.Stops(0f) == 1 && SubwayRideRules.Stops(359f) == 1 && SubwayRideRules.Stops(461f) == 1 && SubwayRideRules.Stops(1023f) == 2 && SubwayRideRules.Stops(2000f) == 3
              && SubwayRideRules.MetresPerStop == 650f, "subway ride: a stop for every 650 m of the straight line and one at the least (Midtown North West to Birchwood 1, to the east park 2)");
        string journey = SubwayCode(ModSource(@"Transport\SubwayJourney.cs"));
        string began = MemberText(journey, "private void Began()"), record = MemberText(journey, "private void RecordRide(CityManager city, SubwaySystem system)"),
            landed = MemberText(journey, "private void Landed(float now)"), arrived = MemberText(journey, "private void Arrived()"), watch = MemberText(journey, "private void WatchRide(float now)");
        Check(began.Contains("RecordRide(city, system);") && began.IndexOf("RecordRide(city, system);", StringComparison.Ordinal) < began.IndexOf("Hide();", StringComparison.Ordinal)
              && record.Contains("SubwaySystem.CurrentPosition") && record.Contains("SubwayRideRules.Origin(start.x, start.z, _stationX, _stationZ, knownAt)")
              && record.Contains("system.lastSubwayStation != null ? system.lastSubwayStation : _seen") && watch.Contains("_seen = seen != null && seen.subwaySystem != null ? seen.subwaySystem.lastSubwayStation : null;")
              && !began.Contains("lastSubwayStation"), "subway ride: the origin is taken the frame the ride is seen, from the camera's start or the station seen with the map open, never read from the game's lastSubwayStation alone");
        Check(!landed.Contains(".transform") && !landed.Contains("OwnStations.LogName") && landed.Contains("JourneyMinutes(_rideFromAt, _rideToAt, out int wait)") && landed.Contains("_toName = _rideToName;")
              && arrived.Contains("string name = _toName;") && !arrived.Contains("Name(") && record.Contains("_rideToName = to != null ? Name(to) : null;"),
            "subway ride: the landing asks no station that may be gone: the points and the names were recorded when the ride began, and the arrival note says the recorded name");
        string members = SubwayCode(ModSource(@"Core\GameMembers.cs"));
        Check(MemberText(members, "public static string NameKey(SubwayStation station)").Contains("if (station == null)"), "subway ride: a station that is gone reads no name, never the entrance it was copied from");
    }

    // ---- every figure, as the documentation states it, and each clearance tried a step inside it and a step outside

    private static void SubwayFigureChecks()
    {
        Check(StationsRules.HalfWidth == 1.135f && StationsRules.Back == 3.2f && StationsRules.Front == 2.03f && StationsRules.ArriveAhead == 2.809f && StationsRules.Height == 3.05f
              && StationsRules.CarveShrink == 0.2f && StationsRules.Sunk == 0.115f && StationsRules.LawnTop == 0.0492f && StationsRules.PavementTop == 0.058f,
            "subway figures: the entrance's own measures and heights are the game's (2.27 m wide, 3.2 m behind and 2.03 m ahead of its origin, its travel point 2.809 m ahead, 3.05 m high, 0.115 m into its ground)");
        Check(StationsRules.ParkIn == 1.75f && StationsRules.SpineGap == 0.3f && StationsRules.KerbIn == 1.35f && StationsRules.SchoolAlong == 19.7f && StationsRules.ParksSpineHalf == 1.5f,
            "subway figures: where they stand: 1.75 m inside a park's edge, 0.3 m from its spine, 1.35 m from the avenue's kerb, 19.7 m along the campus's front");
        Check(StationsRules.LotClear == 0.5f && StationsRules.PieceClear == 1.2f && StationsRules.LampClear == 0.6f && StationsRules.StopClear == 28f && StationsRules.WayPast == 1.1f
              && StationsRules.RoadNear == 30f && StationsRules.ArriveRoom == 0.45f && StationsRules.LampIn == 0.95f,
            "subway figures: what they keep: 0.5 m from a lot, 1.2 m from a piece, 0.6 m from a lamp post, 28 m from a wanted stop, 1.1 m of pavement behind, 0.45 m round where a rider comes up");
        Check(StationsRules.LampsNorth.SequenceEqual(new[] { 4.93f, 22.13f, 41.84f }) && StationsRules.LampsSouth.SequenceEqual(new[] { 5.02f, 22.92f, 41.84f }),
            "subway figures: the avenue module's lamp posts are where the game's module has them (Build 3682)");
        // A site on plain ground: its ground runs 1.135 m either side, 3.2 m behind and 2.03 m ahead, its rider up 2.809 m ahead.
        var site = new StationSite("x", "k", 309, StationGround.Park, 0f, 0f, 0);
        string Point(float x, float z, float margin) => StationsRules.MeetsPoint(site, new[] { x }, new[] { z }, margin, "p");
        float edge = StationsRules.HalfWidth;
        Check(Point(edge + 0.5f, 0f, StationsRules.LampClear) != null && Point(edge + 0.7f, 0f, StationsRules.LampClear) == null,
            "subway figures: a lamp post half a metre from an entrance is in its way, one 0.7 m off is not");
        Check(Point(edge + 1.1f, 0f, StationsRules.PieceClear) != null && Point(edge + 1.3f, 0f, StationsRules.PieceClear) == null,
            "subway figures: a tree 1.1 m from an entrance is in its way, one 1.3 m off is not");
        Check(Point(edge + 27f, 0f, StationsRules.StopClear) != null && Point(edge + 29f, 0f, StationsRules.StopClear) == null,
            "subway figures: a wanted bus stop 27 m from an entrance is too near, one 29 m off is not");
        Check(Point(0f, StationsRules.ArriveAhead + 0.5f, StationsRules.LampClear) != null && Point(0f, StationsRules.ArriveAhead + 0.7f, StationsRules.LampClear) == null,
            "subway figures: what stands half a metre from where a rider comes up is in the way, though it is clear of the entrance itself");
        Check(Point(0f, 0f, 0f) != null && Point(0.5f, -1f, -3f) != null && Point(edge, 0f, 0f) != null && Point(edge + 0.01f, 0f, 0f) == null && Point(edge + 0.5f, 0f, -3f) == null,
            "subway figures: what stands on an entrance's own ground is in its way whatever the margin, and a margin below none is none");
        Lot Beside(float gap) => new Lot("l", 3, 9, "s", 1, 1, edge + gap + 5f, 0f, 0, 10, 4, LotClass.Small, false);
        Check(StationsRules.MeetsLot(site, new[] { Beside(0.4f) }) == "l" && StationsRules.MeetsLot(site, new[] { Beside(0.6f) }) == null && StationsRules.MeetsLot(site, new Lot[0]) == null
              && StationsRules.MeetsLot(site, null) == null, "subway figures: a lot 0.4 m from an entrance refuses it, one 0.6 m off does not");
        Check(StationsRules.MeetsBox(site, edge + 0.4f, -1f, edge + 5f, 1f, 0.5f, "b") == "b" && StationsRules.MeetsBox(site, edge + 0.6f, -1f, edge + 5f, 1f, 0.5f, "b") == null
              && StationsRules.MeetsBox(site, edge + 0.1f, -1f, edge + 5f, 1f, -9f, "b") == null && StationsRules.MeetsBox(site, edge + 5f, 1f, edge - 0.1f, -1f, -9f, "b") == "b",
            "subway figures: a crowd's ground within the margin of an entrance refuses it, its corners in any order, and a margin below none is none");
        string page = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\subway-uptown.md")));
        string[] stated =
        {
            "19.7 m east of the block's west corner", "1.75 m inside the park's edge", "0.3 m from the spine's edge", "1.35 m from the kerb", "(1.1 m wanted)", "big special places by 0.5 m",
            "bins by 1.2 m", "lamp posts by 0.6 m", "every terminus by 28 m", "(within 0.45 m)", "(within 70 m, kept to 95)", "walks off 34 m", "nine trains in ten in the weekday rush, six by day",
            "two between midnight and five", "to five trains in ten, two at night", "2.27 m wide, 3.2 m behind its origin and 2.03 m ahead of it, 3.05 m high", "0.115 m into its ground",
            "a lawn's top 0.0492 m, an avenue's pavement 0.058 m", "0.2 m narrower than the entrance each side", "(within 0.3 m)", "from 0.4 m above the ground", "Two people at the most"
        };
        foreach (string words in stated)
        {
            Check(page.Contains(words), "subway figures: the documentation's page states \"" + words + "\", as the code has it");
        }
    }

    // ---- the people of the line

    private static void SubwayRiderChecks()
    {
        Check(StationsRules.Train(0, 5) == 0 && StationsRules.Train(4, 5) == 0 && StationsRules.Train(5, 5) == 1 && StationsRules.Train(1439, 3) == 479 && StationsRules.Train(100, 0) == 100
              && StationsRules.Train(-5, 5) == 0, "subway people: a train's number of the day from the minute and the minutes between trains, never a division by none");
        // The same on every machine: asked twice, the same answer, for every train of a week at every station.
        bool same = true;
        int up = 0, upRush = 0, upNight = 0, down = 0, downNight = 0, trainsDay = 0, trainsRush = 0, trainsNight = 0, women = 0, looks = 0, upWeekend = 0, trainsWeekend = 0;
        bool lookNeverZero = true, upDownDiffer = false;
        for (int day = 1; day <= 120; day++)
        {
            bool weekend = day % 7 >= 5;
            for (int station = 0; station < 3; station++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    bool rush = !weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19));
                    for (int k = 0; k < 6; k++)
                    {
                        int train = hour * 6 + k;
                        bool u = StationsRules.ComesUp(day, train, station, hour, weekend), d = StationsRules.GoesDown(day, train, station, hour, weekend);
                        same &= u == StationsRules.ComesUp(day, train, station, hour, weekend) && d == StationsRules.GoesDown(day, train, station, hour, weekend);
                        int look = StationsRules.RiderLook(day, train, station, false, out bool female), lookDown = StationsRules.RiderLook(day, train, station, true, out bool femaleDown);
                        same &= look == StationsRules.RiderLook(day, train, station, false, out bool again) && female == again;
                        lookNeverZero &= look != 0 && lookDown != 0;
                        upDownDiffer |= look != lookDown || female != femaleDown;
                        women += female ? 1 : 0;
                        looks++;
                        if (hour < 5)
                        {
                            trainsNight++;
                            upNight += u ? 1 : 0;
                            downNight += d ? 1 : 0;
                        }
                        else if (rush)
                        {
                            trainsRush++;
                            upRush += u ? 1 : 0;
                        }
                        else if (weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19)))
                        {
                            trainsWeekend++;
                            upWeekend += u ? 1 : 0;
                        }
                        else
                        {
                            trainsDay++;
                            up += u ? 1 : 0;
                            down += d ? 1 : 0;
                        }
                    }
                }
            }
        }
        float rushShare = upRush / (float)trainsRush, dayShare = up / (float)trainsDay, nightShare = upNight / (float)trainsNight, downShare = down / (float)trainsDay,
            downNightShare = downNight / (float)trainsNight, weekendShare = upWeekend / (float)trainsWeekend;
        Check(same, "subway people: who comes up, who goes down and how they look are the same when asked again: a mixed number, never chance");
        Check(Math.Abs(rushShare - 0.9f) < 0.03f && Math.Abs(dayShare - 0.6f) < 0.03f && Math.Abs(nightShare - 0.2f) < 0.03f,
            "subway people: somebody comes up from nine trains in ten in the weekday rush, six by day and two at night (" + rushShare.ToString("F3") + ", " + dayShare.ToString("F3") + ", " + nightShare.ToString("F3") + ")");
        Check(rushShare - dayShare > 0.2f && dayShare - nightShare > 0.3f && StationsRules.RushShare == 9 && StationsRules.DayShare == 6 && StationsRules.NightShare == 2 && StationsRules.DownShare == 5,
            "subway people: a good deal more in the rush than by day and by day than at night, never every train nor none: the figures are nine, six, two and five trains in ten");
        Check(Math.Abs(weekendShare - 0.6f) < 0.04f, "subway people: a weekend has no rush (" + weekendShare.ToString("F3") + " at the rush's hours)");
        Check(Math.Abs(downShare - 0.5f) < 0.03f && Math.Abs(downNightShare - 0.2f) < 0.03f,
            "subway people: somebody walks down to five trains in ten, two at night (" + downShare.ToString("F3") + ", " + downNightShare.ToString("F3") + ")");
        Check(lookNeverZero && upDownDiffer && women > looks * 0.45f && women < looks * 0.55f, "subway people: a rider's look is seeded (never the game's own sequence), half of them women, the one who comes up not the one who goes down");
        int differ = 0;
        for (int train = 0; train < 144; train++)
        {
            differ += StationsRules.ComesUp(7, train, 0, 12, false) != StationsRules.ComesUp(7, train, 1, 12, false) ? 1 : 0;
        }
        Check(differ > 20, "subway people: each station its own: the trains that bring somebody up at one are not those of another (" + differ + " of 144 differ)");
        // A save of many years: the answers stay in their ranges on far days, and the last train of a day is a train like any other.
        bool far = true;
        foreach (int day in new[] { 0, 1, 365, 36500, 1000000, int.MaxValue / 2, int.MaxValue })
        {
            for (int train = 0; train < 480; train += 37)
            {
                StationsRules.ComesUp(day, train, 2, 23, true);
                StationsRules.GoesDown(day, train, 2, 0, false);
                far &= StationsRules.RiderLook(day, train, 2, true, out _) > 0 && StationsRules.RiderBearing(day, train, 2, false, 7) >= 0 && StationsRules.RiderBearing(day, train, 2, false, 7) < 8;
            }
        }
        Check(far, "subway people: on any day of any year a rider's look is a seed above none and their way one of the eight");
        // The ways a rider walks: one of eight, and eight tries cover all eight.
        bool ways = true;
        for (int day = 1; day <= 30; day++)
        {
            int seen = 0;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int bearing = StationsRules.RiderBearing(day, day * 3, day % 3, day % 2 == 0, attempt);
                ways &= bearing >= 0 && bearing < 8;
                seen |= 1 << bearing;
            }
            ways &= seen == 255;
        }
        Check(ways, "subway people: a rider walks one of eight ways, and eight tries are the eight ways, each once");
        bool along = true;
        for (int bearing = -8; bearing < 16; bearing++)
        {
            StationsRules.Along(10f, 20f, bearing, StationsRules.RiderWalk, out float ox, out float oz);
            along &= Math.Abs(Math.Sqrt((ox - 10f) * (ox - 10f) + (oz - 20f) * (oz - 20f)) - StationsRules.RiderWalk) < 0.01;
        }
        StationsRules.Along(0f, 0f, 0, 10f, out float ex, out float ez);
        StationsRules.Along(0f, 0f, 2, 10f, out float nx, out float nz);
        Check(along && Math.Abs(ex - 10f) < 0.001f && Math.Abs(ez) < 0.001f && Math.Abs(nx) < 0.001f && Math.Abs(nz - 10f) < 0.001f, "subway people: a way is a point that many metres off, east first, then towards north");
        Check(StationsRules.RidersMost == 2 && StationsRules.RidersNear == 70f && StationsRules.RidersKeep == 95f && StationsRules.RiderWalk == 34f && StationsRules.RiderWalkLeast == 20f,
            "subway people: two at the most, shown within " + StationsRules.RidersNear + " m and kept to " + StationsRules.RidersKeep + ", walking " + StationsRules.RiderWalk + " m");
        string cast = ModSource(@"Jobs\JobCast.cs"), riders = ModSource(@"Subway\StationsDirector.Riders.cs");
        Check(cast.Contains("public const int Size = 2;") && riders.Contains("private Rider _up, _down;") && riders.Contains("_cast.Prewarm(JobCast.Size)"), "subway people: the cast holds the two, made behind the loading screen");
        Check(riders.Contains("StationsRules.ComesUp(day, train, place, hour, weekend)") && riders.Contains("StationsRules.GoesDown(day, train, place, hour, weekend)")
              && riders.Contains("StationsRules.RiderLook(day, train, place, false, out bool female)") && riders.Contains("StationsRules.RiderBearing(day, train, place, down, attempt)")
              && riders.Contains("StationsRules.Train(TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute, SubwayJourney.Frequency())"),
            "subway people: the director asks these very rules, with the trains of the service the player's own journey waits on");
        Check(riders.Contains("!Settings.TransitPeople") && !riders.Contains("_waiting") && !riders.Contains("_waiters") && !riders.Contains("TransitDirector"),
            "subway people: under the option for people at the stops, and beside the bus stops' own people, whom nothing here touches");
        Check(riders.Contains("person == null || !person.IsValid || person.Limp || !person.InUse") && riders.Contains("_cast?.ReleaseAll();"),
            "subway people: somebody knocked down or gone is let go, and all are taken in when the station, the player or the city goes");
    }

    // ---- Birchwood's entrance and the campus

    private static void SubwayCampusChecks()
    {
        StationSite site = StationsRules.Get("birchwood");
        Check(site != null && site.Block == LayoutRules.CampusBlock && site.Ground == StationGround.Pavement, "subway campus: Birchwood's entrance is on the pavement by the campus's block");
        if (site == null)
        {
            return;
        }
        SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
        StationsRules.Footprint(site, out float fx0, out float fz0, out float fx1, out float fz1);
        StationsRules.Arrival(site, out float ax, out float az);
        Check(StationsRules.Holds(site, out bool pavement) == LayoutRules.CampusBlock && pavement && fz1 < z0 - SchoolGrounds.FenceIn && fx0 > x0 && fx1 < x1,
            "subway campus: it stands outside the campus's fence, on Uptown Avenue's pavement along the campus's own front");
        var crowds = new List<CampusCrowd>();
        SchoolGrounds.Crowds(crowds);
        bool clear = crowds.Count >= 2;
        float nearest = float.MaxValue;
        foreach (CampusCrowd crowd in crowds)
        {
            bool across = Math.Abs(crowd.Yaw - 90f) < 1f || Math.Abs(crowd.Yaw - 270f) < 1f;
            float halfX = (across ? crowd.Depth : crowd.Width) * 0.5f, halfZ = (across ? crowd.Width : crowd.Depth) * 0.5f;
            clear &= StationsRules.MeetsBox(site, crowd.X - halfX, crowd.Z - halfZ, crowd.X + halfX, crowd.Z + halfZ, StationsRules.LotClear, crowd.Name) == null;
            if (!crowd.Inside && Math.Abs(crowd.Z - az) < 3f)
            {
                nearest = Math.Min(nearest, Math.Max(0f, Math.Max(crowd.X - halfX - Math.Max(fx1, ax), Math.Min(fx0, ax) - (crowd.X + halfX))));
            }
        }
        Check(clear && nearest >= 2f && nearest < 12f, "subway campus: clear of every gate's crowd, its riders up " + nearest.ToString("F1") + " m from where the main gate's crowd gathers");
        SchoolGrounds.Gate(out float gx, out float gz, out _);
        Check(Math.Abs(ax - gx) < 25f && (fx1 < gx - SchoolGrounds.GateWidth * 0.5f || fx0 > gx + SchoolGrounds.GateWidth * 0.5f) && Math.Abs(gz - z0) < 6f,
            "subway campus: beside the main gate and never before it (" + Math.Abs(ax - gx).ToString("F1") + " m from the gate's middle)");
        var before = new StationSite("x", "k", LayoutRules.CampusBlock, StationGround.Pavement, gx, site.Z, 90);
        bool refused = false;
        foreach (CampusCrowd crowd in crowds)
        {
            bool across = Math.Abs(crowd.Yaw - 90f) < 1f || Math.Abs(crowd.Yaw - 270f) < 1f;
            float halfX = (across ? crowd.Depth : crowd.Width) * 0.5f, halfZ = (across ? crowd.Width : crowd.Depth) * 0.5f;
            refused |= StationsRules.MeetsBox(before, crowd.X - halfX, crowd.Z - halfZ, crowd.X + halfX, crowd.Z + halfZ, StationsRules.LotClear, crowd.Name) != null;
        }
        Check(refused, "subway campus: an entrance before the main gate is refused for the crowd's ground");
        string director = ModSource(@"Subway\StationsDirector.cs");
        Check(director.Contains("Schools.SchoolGrounds.Crowds(_crowds);") && director.Contains("StationsRules.MeetsBox(site, crowd.X - halfX, crowd.Z - halfZ, crowd.X + halfX, crowd.Z + halfZ, StationsRules.LotClear"),
            "subway campus: the director asks the same rule of the same crowds before it stands the entrance");
    }

    // ---- the sources

    private static void SubwaySourceChecks()
    {
        string rules = ModSource(@"Subway\StationsRules.cs"), game = ModSource(@"Subway\StationsGame.cs"), entranceFile = ModSource(@"Subway\StationEntrance.cs"),
            director = ModSource(@"Subway\StationsDirector.cs"), riders = ModSource(@"Subway\StationsDirector.Riders.cs"), tests = ModSource(@"Subway\StationsDirector.Tests.cs"),
            guide = ModSource(@"Subway\StationsGuide.cs"), own = ModSource(@"Core\OwnStations.cs");
        string all = SubwayCode(string.Concat(SubwaySources.Select(ModSource)));
        Check(SubwaySources.All(f => ModSource(f).Length > 300) && own.Length > 300, "subway sources: its files are read");
        int cut = entranceFile.IndexOf("public sealed class StationTaxiStop", StringComparison.Ordinal);
        string entrance = cut > 0 ? entranceFile.Substring(0, cut) : entranceFile, stop = cut > 0 ? entranceFile.Substring(cut) : string.Empty;

        // The game-free rules have no Unity or game types.
        string going = ModSource(@"Subway\StationsGoing.cs");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("using BAModAPI") && !rules.Contains("Vector3") && !rules.Contains("SubwayStation ")
              && !going.Contains("UnityEngine") && !going.Contains("using BAModAPI") && !going.Contains("Vector3") && !going.Contains("SubwayStation ") && !ModSource(@"Transport\SubwayRideRules.cs").Contains("UnityEngine"),
            "subway sources: the rules, the going's rule and the ride's rules are game-free");

        // The fare is the game's own: nothing here books, gives or waives money, and travel is the game's own call.
        Check(!all.Contains("BizBooks") && !all.Contains("ChangeMoney") && !Regex.IsMatch(all, @"\.Money\s*(=|\+=|-=)[^=]") && !all.Contains("TransactionInfo") && !all.Contains("Book("),
            "subway sources: nothing of the stations books, gives or takes money: the fare is the game's own, taken by its own travel");
        Check(entrance.Contains("public sealed class StationEntrance : SubwayStation") && !entrance.Contains("override bool Interact") && !entrance.Contains("PricePerRide"),
            "subway sources: the entrance is the game's own station and keeps its Interact, which asks for the fare before the map opens");
        Check(Regex.Matches(all, @"\.TravelTo\(").Count == 1 && tests.Contains("city.subwaySystem.TravelTo(to);") && tests.Contains("SaveGameManager.Current.Money < SubwayStation.PricePerRide"),
            "subway sources: the one travel the mod asks for is its test's, through the game's own call, refused first without the fare");
        Check(entrance.Contains("return base.OnIoLeftClick();") && entrance.Contains("uis.buildingResume.SelectTarget(TaxiStop);") && entrance.Contains("!city.cityMap.isSubwayMode && CityMap.IsOpen && city.cityMap.IsTaxiMode"),
            "subway sources: a click is the game's own but in its taxi mode, where the taxi's destination is the station's stop");
        Check(stop.Contains("TaxiDestinationData data = gameObject.AddComponent<TaxiDestinationData>();") && stop.Contains("data.mapColliders = new Collider[0];") && stop.Contains("data.nameKey = nameKey;"),
            "subway sources: the stop carries the game's own destination data, named by the station's key, its list for the map's toggle never null");

        // Named its own everywhere the game shows a name.
        Check(entrance.Contains("customOverlayHeaderKey = site.NameKey;") && game.Contains("entry.stationNameLabel.Key = stood.Site.NameKey;") && game.Contains("OwnStations.Add(station, site.NameKey,"),
            "subway sources: named by its own key over the street, in the game's window and for every reader of the mod");
        string members = ModSource(@"Core\GameMembers.cs");
        int asked = members.IndexOf("OwnStations.KeyOf(station)", StringComparison.Ordinal), games = members.IndexOf("return StationKey(station);", StringComparison.Ordinal);
        Check(asked > 0 && games > asked, "subway sources: a station's name is asked of the mod's own first, before the game's method that reads the copied value");
        Check(ModSource(@"Transport\TransitMap.cs").Contains("city.subwayStations[i] != null && !OwnStations.Is(city.subwayStations[i])"),
            "subway sources: the bus network's ends are the game's stations alone, so the lines are the same with or without the districts' stations");
        Check(ModSource(@"Leisure\LeisureVenues.cs").Contains("if (station == null || OwnStations.Is(station))"), "subway sources: the leisure landmarks keyed to the game's stations' names leave the mod's out");
        Check(own.Contains("if (ReferenceEquals(station, null) || Stations.Contains(station))") && own.Contains("Stamp++;"), "subway sources: a station joins the mod's own once");

        // The game's Start, which hangs a call on the map's toggle for good, is replaced by one that hangs nothing: the outline is white again in the entity's own leave and enter.
        Check(entrance.Contains("public override void Start()") && !entrance.Contains("base.Start()") && !all.Contains("onCityMapToggle") && !all.Contains("onCityMapClosed")
              && !all.Contains("Delegate.Combine") && entrance.Contains("public override void OnIoExit()") && entrance.Contains("if (!mapFilterIsOn)")
              && entrance.Contains("public override void OnIoEnter()") && entrance.Contains("if (!CityMap.IsOpen)"),
            "subway sources: nothing of the stations is hung on the map's events (the game's own Start would hang a call for good): the outline is white again in the entrance's own leave and enter");
        Check(entrance.Contains("public override void UpdateNavMeshTargets()") && game.Contains("arrival.position = arrive;") && game.Contains("string walk = ComesUp(site, out Vector3 arrive);"),
            "subway sources: the travel point is put on checked ground and never moved by the game's own validation");

        // Stood once, whole or not at all; taken away as it came.
        Check(game.Contains("if (Find(site) != null)") && game.Contains("return \"it stands already\";") && director.Contains("StationsGame.Forget();   // nothing of an earlier city is held"),
            "subway sources: a site is stood once, and nothing of an earlier city is held: no second station after any number of loads");
        int solid = game.IndexOf("string solid = Solid(at, turn);", StringComparison.Ordinal), walk = game.IndexOf("string walk = ComesUp(site, out Vector3 arrive);", StringComparison.Ordinal),
            made = game.IndexOf("UnityEngine.Object.Instantiate(template.gameObject, asleep.transform)", StringComparison.Ordinal), joined = game.IndexOf("city.subwayStations.Add(station);", StringComparison.Ordinal);
        Check(solid > 0 && walk > solid && made > walk && joined > made, "subway sources: something solid on its ground or riders off the walking ground refuse a station before anything of it is made or joined");
        Check(game.Contains("\"Water\", \"Player\", \"Human\");") && game.Contains("Physics.OverlapBoxNonAlloc(centre, half, Overlaps, turn, _solidMask, QueryTriggerInteraction.Ignore)"),
            "subway sources: the look for something solid is one box that counts no ground, no zone and no person: somebody standing there in an older save does not cost the station");
        Check(game.Contains("asleep.SetActive(false);") && game.Contains("UnityEngine.Object.DestroyImmediate(copied);") && game.Contains("!drawn.isPartOfStaticBatch"),
            "subway sources: the copy is made asleep, the game's own station on it never wakes, and an entrance drawn in a static batch is never copied");
        string removeText = MemberText(game, "private static void Remove(Stood stood, CityManager city, UIs uis, List<CityMapSubwayStationEntry> entries)");
        Check(removeText.Contains("IReadOnlyList<StationsRules.StationPart> order = StationsRules.GoingOrder;") && removeText.Contains("switch (order[i])")
              && Enum.GetNames(typeof(StationsRules.StationPart)).All(part => removeText.Contains("case StationsRules.StationPart." + part + ":"))
              && removeText.Contains("city.subwayStations.Remove(station);") && removeText.Contains("OverlayGuard.Release(station);") && removeText.Contains("Discard(stood.Entry, entries);")
              && removeText.Contains("Unmark(city, station.transform);") && removeText.Contains("Release(uis, station);") && removeText.Contains("SceneryTrees.Destroy(station.gameObject);")
              && game.Contains("map.pois.RemoveAt(i);") && game.Contains("map.UpdateNonePermanentPointOfInterests();"),
            "subway sources: a station is let go of the game's list, window, cards and map in the rules' order, each part by its own case, and destroyed last through SceneryTrees");
        string addEntry = MemberText(game, "private static CityMapSubwayStationEntry AddEntry(Stood stood, CityMapSubwayStations list, List<CityMapSubwayStationEntry> entries)"),
            discard = MemberText(game, "private static void Discard(CityMapSubwayStationEntry entry, List<CityMapSubwayStationEntry> entries)");
        Check(addEntry.Contains("catch (Exception)") && addEntry.IndexOf("Discard(entry, entries);", StringComparison.Ordinal) > addEntry.IndexOf("catch (Exception)", StringComparison.Ordinal)
              && addEntry.IndexOf("throw;", StringComparison.Ordinal) > addEntry.IndexOf("Discard(entry, entries);", StringComparison.Ordinal)
              && Regex.Match(addEntry, @"\btry\b").Index > addEntry.IndexOf("Instantiate(", StringComparison.Ordinal) && addEntry.IndexOf("entry.name =", StringComparison.Ordinal) > Regex.Match(addEntry, @"\btry\b").Index
              && discard.IndexOf("entries?.Remove(entry);", StringComparison.Ordinal) >= 0 && discard.IndexOf("SetActive(false);", StringComparison.Ordinal) > discard.IndexOf("entries?.Remove(entry);", StringComparison.Ordinal)
              && discard.IndexOf("Destroy(entry.gameObject);", StringComparison.Ordinal) > discard.IndexOf("SetActive(false);", StringComparison.Ordinal),
            "subway sources: a row of the window that fails to be made is taken out of the list, shut and destroyed there and then, before the failure goes on");
        Check(Regex.Matches(game, @"SceneryTrees\.Destroy\(").Count == 4 && Regex.Matches(game, @"UnityEngine\.Object\.Destroy\(").Count == 3
              && game.Contains("UnityEngine.Object.Destroy(entry.gameObject);") && game.Contains("UnityEngine.Object.Destroy(marker.gameObject);") && game.Contains("UnityEngine.Object.Destroy(_watch);")
              && !all.Contains("RegisterCullable") && !all.Contains("UnregisterCullable"),
            "subway sources: every copy of the game's entrance is destroyed through SceneryTrees, which takes its lamp out of the game's culling lists first; the three plain destroys are the window's entry, the map's marker and the watch");
        int joinedAt = game.IndexOf("city.subwayStations.Add(station);", StringComparison.Ordinal), backAt = game.IndexOf("Remove(entry, city, uis, entries);", StringComparison.Ordinal);
        Check(joinedAt > 0 && backAt > joinedAt && game.Contains("Standing.Remove(entry);") && game.Contains("entry.Entry = AddEntry(entry, list, entries);")
              && removeText.Contains("city.subwayStations.Remove(station);"),
            "subway sources: a station is joined whole or not at all: one whose entry in the game's window cannot be made is taken out of the game's list again and away");
        string gone = MemberText(director, "private void OnDestroy()"), leave = MemberText(director, "private void Leave(StationExit cause, string why)"), leaver = MemberText(game, "private IEnumerator Wait(string why)");
        Check(gone.Contains("_going.Go(cause, \"the mod stopped\", StationsGame.Holds())") && gone.Contains("StationExit.CityUnloading : StationExit.DirectorGone") && gone.Contains("StationsGame.HandOver(_going, \"the mod stopped\");")
              && gone.Contains("StationsGame.Carry(step);") && !director.Contains("SaveTools.Dormant") && !Regex.IsMatch(director, @"StationsGame\.RemoveAll\(") && !Regex.IsMatch(SubwayCode(game).Replace("public static int RemoveAll()", string.Empty), @"(?<!return )RemoveAll\(\)"),
            "subway sources: the director destroyed (whoever stopped the mod) asks the same rule as every other cause, hands a ride or a taxi trip to the watcher, and nothing takes the stations away but the rule's own steps");
        Check(leaver.Contains("_going.Go(cause, why, StationsGame.Holds())") && leaver.Contains("if (step == StationStep.HandOver)") && leaver.Contains("GameManager.isCitySceneBeingUnloaded ? StationExit.CityUnloading : StationExit.DirectorGone")
              && leaver.Contains("StationsGame.Carry(step);") && leaver.Contains("new WaitForSecondsRealtime(Look)") && game.Contains("StartCoroutine(Guard.Routine(Wait(why)));") && game.Contains("public static bool HandedOver => _leaver != null;")
              && MemberText(director, "private void Awake()").Contains("if (!StationsGame.HandedOver)"),
            "subway sources: the watcher that outlives the director asks the rule four times a second on real time, goes on while it says hand over, lets the scene take them when the city unloads, and a new director does not clear what it holds");
        Check(game.Contains("return new StationHolds(SubwaySystem.IsRiding, RideTouches(), TaxiTravelling(), PickingFromOwn());") && game.Contains("return InstanceBehavior<TaxiSystem>.IsInitialized && TaxiSystem.IsTraveling;"),
            "subway sources: what holds the stations is the game's ride, a ride that touches them, any taxi trip (the property the taxi director and the private driver's page already ask) and the open map");
        Check(leave.Contains("_going.Go(cause, why, StationsGame.Holds())") && leave.Contains("if (step == StationStep.Wait)") && leave.Contains("StationsGame.Carry(step);") && !leave.Contains("RemoveAll"),
            "subway sources: every going of the director's own is the rule's answer carried out");
        string closing = MemberText(director, "public void Close(string why)"), look = MemberText(director, "private void OnUpdate()");
        Check(closing.Contains("_closed = true;") && closing.Contains("Leave(StationExit.CleanUp, why);") && !closing.Contains("RemoveAll") && look.Contains("if (_late && _closed)") && look.Contains("Leave(_going.Cause, _going.Why);"),
            "subway sources: the feature's clean-up leaves by the same rule, never under a rider, and the look takes them away once the rider is up");
        Check(director.Contains("StationsGame.Stands(site)") && MemberText(director, "private void StandAll(string from)").IndexOf("PlayerOn(site)", StringComparison.Ordinal) > 0
              && MemberText(director, "private void StandAll(string from)").IndexOf("PlayerOn(site)", StringComparison.Ordinal) < MemberText(director, "private void StandAll(string from)").IndexOf("StationsGame.Stand(site, out _)", StringComparison.Ordinal)
              && MemberText(director, "private static bool PlayerOn(StationSite site)").Contains("StationsRules.Occupied(site, at.x, at.z)") && director.Contains("(_stoodWhy == null || _retry)"),
            "subway sources: no station is stood on the player (the rule's look first), and the stand is tried again at every look until they have moved");
        Check(director.Contains("bool wanted = StationsRules.Wanted(Settings.UptownSubway, UptownLots.Ready && !UptownLots.Closed, StationsGame.GameStations());")
              && director.Contains("UptownLots.Closing += _onClosing;") && director.Contains("UptownLots.Closing -= _onClosing;"),
            "subway sources: whether they stand is the rule's answer from the option, the districts and the game's network, and the districts' closing takes them away");
        Check(director.Contains("StationsRules.Holds(site, out bool pavement)") && director.Contains("StationsRules.MeetsLot(site, UptownLots.All) ?? StationsRules.MeetsLot(site, UptownLots.Places)")
              && director.Contains("StationsRules.PavementRoom(site, out float behind, out float toKerb)") && director.Contains("StationsRules.Lamps(site, _px, _pz);")
              && director.Contains("StreetsRules.StopPoles(_px, _pz);") && director.Contains("ParksRules.Pieces(site.Block, _parkPieces);") && director.Contains("why = Plan(site);") && director.Contains("why = why ?? StationsGame.Stand(site, out _);"),
            "subway sources: the director looks at a station's ground with the rules the checks run before the game's side stands it");
        Check(game.Contains("StationsRules.InsertAt(ListNames, NameOf(stood.Site), Order)") && game.Contains("StationsRules.Shown(NameOf(Standing[i].Site), typed, StringComparison.CurrentCultureIgnoreCase)")
              && game.Contains("StationsRules.Carve(out float ahead, out float across, out float along);") && game.Contains("StationsRules.GroundOf(site)"),
            "subway sources: the window's place, the search, the cut and the height are the rules' own");
        Check(game.Contains("StationsRules.HangLast(() => _search.onValueChanged.RemoveListener(_onSearch), () => _search.onValueChanged.AddListener(_onSearch));") && game.Contains("_onSearch = _onSearch ?? Guard.Listener<string>(OnSearch);")
              && game.Contains("StationsGame.Rehang();") && game.Contains("public sealed class StationListWatch : MonoBehaviour, GameUi.INotCopied"),
            "subway sources: the search's listener is made once and put last by the rule each time the window is shown, by a watch that is marked never to be copied");
        string clone = SubwayCode(ModSource(@"Core\GameUi\UiClone.cs"));
        Check(clone.Contains("public interface INotCopied") && MemberText(clone, "public static GameObject Asleep(GameObject original, Transform dormant, string name)").Contains("TakeOffMarked(copy);")
              && MemberText(clone, "private static void TakeOffMarked(GameObject copy)").Contains("if (all[i] is INotCopied)") && MemberText(clone, "private static void TakeOffMarked(GameObject copy)").Contains("DestroyImmediate(all[i]);"),
            "subway sources: the game's list of stations is copied for the map picker and the travel list with the mod's marked watch taken off the copy while it is asleep");
        string hover = MemberText(game, "private static void TakeOffSound(GameObject copy)");
        Check(hover.Contains("GetComponentsInChildren<SubwaySoundSource>(true)") && hover.Contains("DestroyImmediate(sources[i]);") && game.Contains("carries = Carried(copy);") && game.Contains("TakeOffSound(copy);"),
            "subway sources: the ambience marker of the game's entrance is taken off the copy asleep (its sound looks for its sources once), and what else the copy carries is named in the check's line");
        string rider = SubwayCode(ModSource(@"Subway\StationsDirector.Riders.cs"));
        Check(rider.Contains("float game = Time.time;") && !rider.Contains("unscaledTime") && !rider.Contains("Time.unscaled"), "subway sources: the people's limits run on the game's time, which stops with the game, never on the interface's");

        // Nothing saved, nothing sent, nothing by chance, nothing a frame.
        Check(!all.Contains("SaveHook") && !all.Contains("modData") && !all.Contains("reallife:subway") && !all.Contains("PlayerPrefs"), "subway sources: nothing is saved: fixed places laid again at every load");
        Check(!all.Contains("UnityEngine.Random") && !Regex.IsMatch(all, @"\bRandom\.") && !all.Contains("GoingPublicLink") && !all.Contains("System.Random") && !all.Contains("DateTime.Now"),
            "subway sources: nothing is sent and nothing is chance: the same stations and the same people on every machine");
        Check(!all.Contains("FindObjectsByType") && !all.Contains("FindObjectOfType") && !all.Contains("GameObject.Find(") && !all.Contains("FindWithTag") && !all.Contains("Resources.FindObjects"),
            "subway sources: nothing is searched for in the scene (the code, without its comments): the game's own list and window alone");
        string update = MemberText(director, "private void OnUpdate()");
        int first = update.IndexOf("if (now < _nextLook)", StringComparison.Ordinal), decide = update.IndexOf("Decide(", StringComparison.Ordinal);
        Check(first > 0 && decide > first && update.Contains("_nextLook = now + (busy ? Look : Idle);") && director.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && director.Contains("if (FrameGuard.Off)"),
            "subway sources: a frame costs one time comparison; the looks come once a second, four while somebody of the line is out");
        Check(Regex.Matches(all, @"void (Update|LateUpdate|FixedUpdate)\(").Count == 1, "subway sources: one Update in all of it, the director's");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        int landmarks = mod.IndexOf("Guard.Add<Landmarks.LandmarksDirector>(_root)", StringComparison.Ordinal), stations = mod.IndexOf("Guard.Add<Subway.StationsDirector>(_root)", StringComparison.Ordinal),
            ground = mod.IndexOf("Guard.Add<Uptown.GroundDirector>(_root)", StringComparison.Ordinal), transit = mod.IndexOf("Guard.Add<TransitDirector>(_root)", StringComparison.Ordinal);
        Check(ground > 0 && transit > 0 && landmarks > ground && stations > landmarks && stations > transit, "subway sources: its director is added after the districts' ground, the campus, the landmarks and the buses, whose late pieces run before its own");
        Check(director.Contains("ScenePrewarm.AddLate(") && director.Contains("AtLoad"), "subway sources: the stations are stood in the game's late call, when its own stations and its window of them are there");

        // The option, the feature and its tests.
        string settings = ModSource(@"Core\Settings.cs");
        Check(settings.Contains("public const string UptownSubwayId = \"uptown_subway\";") && settings.Contains("public static bool UptownSubway = true;") && settings.Contains("UptownSubway = Bool(modId, UptownSubwayId, UptownSubway);"),
            "subway sources: its option, on by default");
        Check(ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"uptown_subway\", CityScope.NewYork,") && ModSource(@"Core\FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Subway\", \"uptown_subway\" },")
              && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"uptown_subway\").For(\"uptown_subway\").Opts(\"uptown_subway\")") && director.Contains("public const string FeatureId = \"uptown_subway\";"),
            "subway sources: a feature of New York's own, with its owner, its section of the options and its line of the cities");
        Check(guide.Contains(".Check(\"reallife_uptown_subway_test_check\", StationsDirector.TestStations, \"Subway check (stations)\")") && guide.Contains(".Test(\"reallife_uptown_subway_test_go\", StationsDirector.TestGo)")
              && guide.Contains(".Test(\"reallife_uptown_subway_test_ride\", StationsDirector.TestRide)") && guide.Contains(".Test(\"reallife_uptown_subway_test_rider\", StationsDirector.TestRider)")
              && guide.Contains(".Cleanup(") && guide.Contains("new ToggleOption(Settings.UptownSubwayId, \"reallife_uptown_subway_enabled\", true, on => Settings.UptownSubway = on)") && guide.Contains("Features.Add(\"uptown_subway\", \"reallife_meta_title_uptown_subway\")") && guide.Contains(".Objects(StationsDirector.RootName)"),
            "subway sources: its entry has its option, its four tests, its clean-up and its own object's name, every key written out whole as the options checks read them");
        Check(tests.Contains("public static void TestStations()") && tests.Contains("public static void TestGo()") && tests.Contains("public static void TestRide()") && tests.Contains("public static void TestRider()")
              && Regex.Matches(tests, @"TestRefusals\.Tell\(").Count >= 10 && tests.Contains("Logger?.Info(\"Subway check (stations): "),
            "subway sources: each test is there, says why when it cannot run, and the check writes its line");
        Check(ModSource(@"Core\GameContract.Map.cs").Contains("MapStationEntries = Field(UiMapDock, Game, \"CityMapSubwayStations\", \"_generatedEntries\", \"List<CityMapSubwayStationEntry>\")")
              && game.Contains("GameContract.MapStationEntries.Field?.GetValue(list)") && game.Contains("return \"the game's window of stations is not as the mod knows it\";")
              && !all.Contains("GetField(") && !all.Contains("BindingFlags"),
            "subway sources: the one member of the game's read by name is the contract's line for the window's entries, and a game without it leaves the stations unstood with one line");
    }

    // ---- the texts

    private static void SubwayTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string source in SubwaySources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_opt_sec_uptown_subway");
        keys.Add("reallife_opt_tip_uptown_subway");
        Check(keys.Count == 18, "subway texts: its keys are found: " + keys.Count);
        string[] notices = { "reallife_uptown_subway_check_done", "reallife_uptown_subway_check_wrong", "reallife_uptown_subway_go_done", "reallife_uptown_subway_ride_done", "reallife_uptown_subway_rider_done",
            "reallife_uptown_streets_subway_done" };
        string[] names = { StationsRules.BirchwoodKey, StationsRules.SummitKey, StationsRules.EastKey };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(english != null, "subway texts: en.json is read");
        if (english == null)
        {
            return;
        }
        // The What's new line is found by its English words in whichever row holds it (the next row, or a version's once it is numbered).
        string row = english.Where(p => p.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !p.Key.EndsWith("_title", StringComparison.Ordinal)
                                        && p.Value.Contains("Uptown and Cedar Heights now have three subway stations of their own")).Select(p => p.Key).FirstOrDefault();
        Check(row != null, "subway texts: What's new has the stations' line in English (any of its rows)");
        var subwayWord = new Dictionary<string, string>
        {
            { "en", "subway" }, { "pt", "metrô" }, { "zh-cn", "地铁" }, { "de", "U-Bahn" }, { "fr", "métro" }, { "es", "metro" }, { "ru", "метро" }, { "nl", "metro" }, { "tr", "metro" }, { "pl", "metr" }, { "ko", "지하철" }
        };
        foreach (string code in ModLanguages.All)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "subway texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys.Concat(new[] { "reallife_uptown_streets_subway_done" }))
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "subway texts: " + key + " is in " + code + ".json");
                if (!there || !english.TryGetValue(key, out string source))
                {
                    continue;
                }
                var wanted = new SortedSet<string>(Regex.Matches(source, @"\{[a-z0-9_]+\}").Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
                var held = new SortedSet<string>(Regex.Matches(value, @"\{[a-z0-9_]+\}").Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
                Check(wanted.SetEquals(held), "subway texts: " + key + " keeps its fields in " + code + " (" + string.Join(" ", held) + ")");
                Check(!value.Contains("!") && !value.Contains("！") && !value.Contains("–") && !value.Contains("—") && !value.Contains(" - "), "subway texts: " + key + " has no dash and no exclamation mark in " + code);
                Check(Regex.Matches(value, "<b>").Count == Regex.Matches(source, "<b>").Count && Regex.Matches(value, "</b>").Count == Regex.Matches(source, "</b>").Count,
                    "subway texts: " + key + " keeps its bold names in " + code);
            }
            foreach (string notice in notices)
            {
                Check(words.TryGetValue(notice, out string text) && text.Length <= 240, "subway texts: the notice " + notice + " is at most 240 characters in " + code + " (" + (text != null ? text.Length : 0) + ")");
            }
            // The stations' names: three, each its own, in the language's own script where it has one.
            string[] station = names.Select(k => words.TryGetValue(k, out string v) ? v : string.Empty).ToArray();
            Check(station.All(n => n.Length > 5) && station.Distinct().Count() == 3 && station.All(n => !Regex.IsMatch(n, @"[,:;.]")), "subway texts: three station names, each its own and plain, in " + code);
            if (code == "zh-cn" || code == "ko" || code == "ru")
            {
                Check(station.All(n => n.Any(c => c > 0x2ff)), "subway texts: the stations' names are in " + code + "'s own script");
            }
            // The Guide page: the same lines as the English, its two figures, and its bold names the language's own texts.
            if (words.TryGetValue("reallife_guide_uptown_subway", out string page) && english.TryGetValue("reallife_guide_uptown_subway", out string pageEn))
            {
                string[] lines = page.Split('\n'), linesEn = pageEn.Split('\n');
                bool shaped = lines.Length == linesEn.Length;
                for (int i = 0; shaped && i < lines.Length; i++)
                {
                    shaped &= lines[i].StartsWith("# ", StringComparison.Ordinal) == linesEn[i].StartsWith("# ", StringComparison.Ordinal)
                              && lines[i].StartsWith("- ", StringComparison.Ordinal) == linesEn[i].StartsWith("- ", StringComparison.Ordinal);
                }
                Check(shaped && linesEn.Length == 10, "subway texts: the Guide page has the English page's lines, heads and points in " + code + " (" + lines.Length + ")");
                Check(page.Contains("{uptown_subway_stations}") && page.Contains("{uptown_subway_fare}"), "subway texts: the Guide page has its two figures in " + code);
                bool bold = true;
                foreach (Match m in Regex.Matches(page, "<b>(.*?)</b>"))
                {
                    bold &= words.Values.Contains(m.Groups[1].Value);
                }
                bool named = names.All(k => page.Contains("<b>" + words[k] + "</b>")) && words.TryGetValue("reallife_transport_subway", out string journeys) && page.Contains("<b>" + journeys + "</b>")
                             && page.Contains("<b>" + words["reallife_uptown_subway_enabled"] + "</b>");
                Check(bold && named, "subway texts: the Guide page's bold names are the stations', the journeys' option's and its own option's, as " + code + " has them");
            }
            // Uptown's own page sends the reader on, and no longer says there is none.
            if (words.TryGetValue("reallife_guide_uptown", out string uptown) && words.TryGetValue("reallife_meta_title_uptown_subway", out string title))
            {
                Check(uptown.Contains("<b>" + title + "</b>") && (code != "en" || !uptown.Contains("There is no subway station of their own")),
                    "subway texts: Uptown's Guide page points to the stations' page by its title in " + code);
            }
            // The Transport page said the districts have no station: its third line now begins with the first sentence of the option's own tip.
            if (words.TryGetValue("reallife_guide_transport", out string transport) && words.TryGetValue("reallife_opt_tip_uptown_subway", out string tip))
            {
                string[] lines = transport.Split('\n');
                int stop = code == "zh-cn" ? tip.IndexOf('。') + 1 : tip.IndexOf(". ", StringComparison.Ordinal) + 1;
                Check(lines.Length == 33 && stop > 3 && lines[2].StartsWith("- " + tip.Substring(0, stop), StringComparison.Ordinal)
                      && (code != "en" || (!transport.Contains("have no subway station") && lines[2].StartsWith("- Three subway stations in Uptown and Cedar Heights, joined to the game's subway at its own fare. Buses end", StringComparison.Ordinal))),
                    "subway texts: the Transport page of the Guide says the districts have three stations, in the words of the option's tip, in " + code);
            }
            if (row != null)
            {
                var markOf = new Dictionary<string, string> { { "en", "Fixed: " }, { "pt", "Corrigido: " }, { "zh-cn", "已修复：" }, { "de", "Behoben: " }, { "fr", "Corrigé : " }, { "es", "Corregido: " },
                    { "ru", "Исправлено: " }, { "nl", "Opgelost: " }, { "tr", "Düzeltildi: " }, { "pl", "Naprawiono: " }, { "ko", "수정: " } };
                string mark = markOf[code];
                string tail = words.TryGetValue(row, out string whole) && whole.LastIndexOf(mark, StringComparison.Ordinal) >= 0 ? whole.Substring(whole.LastIndexOf(mark, StringComparison.Ordinal)) : string.Empty;
                Check(tail.Length > 0 && tail.IndexOf(subwayWord[code], StringComparison.OrdinalIgnoreCase) >= 0 && (code != "en" || tail == "Fixed: Subway rides take their time again: the wait for a train and the stops of the journey are added after the game's ride, at every station."),
                    "subway texts: What's new ends with the fix that subway rides take their time again, in " + code);
                bool told = words.TryGetValue(row, out string news) && news.IndexOf(subwayWord[code], StringComparison.OrdinalIgnoreCase) >= 0
                            && (news.Contains("Birchwood") || news.Contains("伯奇伍德"));
                Check(told, "subway texts: What's new has the stations' line in " + code + " (the row English has it in)");
            }
        }
        string localesScript = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Workshop\locales.py")));
        HashSet<string> listed = SubwayQuotedKeys(localesScript, "ENGLISH_OK"), listedIn = SubwayQuotedKeys(localesScript, "ENGLISH_OK_IN");
        Check(listed.Count > 200 && names.All(n => !listed.Contains(n)) && names.All(listedIn.Contains) && Regex.Matches(localesScript, @"'reallife_subway_station_[a-z]+': \('de',\)").Count == 3,
            "subway texts: the stations' names are excused from \"not translated\" in German alone (ENGLISH_OK_IN), not in every language, and the list is read from its code with the comments taken out (" + listed.Count + " keys)");
        foreach (string code in ModLanguages.All)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            bool same = words != null && names.Any(n => words.TryGetValue(n, out string v) && english.TryGetValue(n, out string e) && v == e);
            Check(words != null && (code == "en" || code == "de" ? true : !same), "subway texts: the stations' names are the English unchanged only in English and German, and not in " + code);
        }
        Check(english.TryGetValue("reallife_uptown_streets_subway_done", out string old) && old.Contains("{count}") && !old.Contains("no subway station"),
            "subway texts: the districts' older test no longer says they have no station");
        string coverage = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\StandardCheck\guide-coverage.txt")));
        Check(coverage.Contains("subway-uptown\treallife_guide_uptown_subway,reallife_guide_uptown_subway_about\t"), "subway texts: its page of the documentation is on the Guide's coverage list");
    }
}
