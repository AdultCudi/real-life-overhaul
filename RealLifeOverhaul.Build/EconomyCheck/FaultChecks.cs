using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Faults;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Ledger;

// Checks for the city's real faults (Scripts/Faults: FaultModel, FaultRules, FaultDice, FaultShare, FaultStore) and the
// parts of Flare that meet them (FlareLiveRules): the weighting by the city's state (more dark lamps with short upkeep,
// more potholes after rain), the counts held to the option's limits, a true report found and mended and a false one not,
// the escalation of reports of nothing, the city's people reporting real faults (and Flare's people inventing none
// while the faults are real), the save's line, the Going Public part (the host's faults whole to a guest, a guest that
// never rolls, an RLO that knows no part K passing over it, a late joiner's snapshot), the texts in three languages,
// the cost of a roll, and that the live source of chance is a real one, never started from the save. The rules are
// checked with sources of chance made here (the live one is random by design).
internal static partial class Program
{
    private static void Faults()
    {
        FaultsWeighting();
        FaultsLimits();
        FaultsReports();
        FaultsEscalation();
        FaultsSeen();
        FaultsSave();
        FaultsShare();
        FaultsRandom();
        FaultsFlare();
        FaultsTexts();
        FaultsTiming();
        FaultsReview();
    }

    // The review round of the city's faults (merged with main 0.27.6): a fault in hand reported again, the case keeping its
    // fault and verdict in the save, the paper never acting on nothing, a guest never rolling, the lamps put back safely.
    private static void FaultsReview()
    {
        // A case keeps what the faults made of it, and the fault it found, through the save (after main's Counted).
        var n = new FlareNet();
        n.Cases.Add(new FlareCase { Id = 4, Kind = FlareReport.Unsafe, District = "d", Reporter = "c3", Verdict = (int)FaultAnswer.True, Fault = 12, Counted = false });
        n.Cases.Add(new FlareCase { Id = 5, Kind = FlareReport.Lamp, District = "d", Reporter = "me" });
        string line = FlareLiveStore.Write(n);
        var r = new FlareNet();
        FlareLiveStore.Read(r, line);
        Check(r.Cases.Count == 2 && r.Cases[0].Verdict == (int)FaultAnswer.True && r.Cases[0].Fault == 12 && !r.Cases[0].Counted && r.Cases[1].Verdict == -1 && r.Cases[1].Fault == -1,
            "faults, review: a case keeps its verdict and the fault it found in the save, beside main's Counted");
        var old = new FlareNet();
        FlareLiveStore.Read(old, Regex.Replace(line, @"\|1\|-1\|-1(\r?\n|$)", "|1$1"));
        Check(old.Cases.Count == 2 && old.Cases[1].Verdict == -1 && old.Cases[1].Fault == -1, "faults, review: a case from an older save is not judged");

        // The paper's pressure never acts on nothing, sends works to a real unsafe place, and inspects no clean business.
        var net = new FlareNet();
        FlareOrder nothing = FlareLiveRules.EscalationOrder(net, new FlareCase { Kind = FlareReport.Lamp, Verdict = (int)FaultAnswer.Nothing }, 0, true);
        FlareOrder barred = FlareLiveRules.EscalationOrder(net, new FlareCase { Kind = FlareReport.Road, Verdict = (int)FaultAnswer.Barred }, 0, true);
        FlareOrder realUnsafe = FlareLiveRules.EscalationOrder(net, new FlareCase { Kind = FlareReport.Unsafe, Verdict = (int)FaultAnswer.True }, 0, true);
        FlareOrder plainUnsafe = FlareLiveRules.EscalationOrder(net, new FlareCase { Kind = FlareReport.Unsafe }, 0, true);
        FlareOrder clean = FlareLiveRules.EscalationOrder(net, new FlareCase { Kind = FlareReport.Business, Verdict = (int)FaultAnswer.Clean }, 0, true);
        FlareOrder business = FlareLiveRules.EscalationOrder(net, new FlareCase { Kind = FlareReport.Business }, 0, true);
        Check(nothing == FlareOrder.None && barred == FlareOrder.None && realUnsafe == FlareOrder.Works && plainUnsafe == FlareOrder.Patrol && clean == FlareOrder.None && business == FlareOrder.Inspection,
            "faults, review: under the paper's eye the City Office acts on no report found to be nothing, sends works to a real unsafe place, and inspects no clean business");

        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string director = FaultSource(scripts, "Faults/FaultDirector.cs");
        string reports = FaultSource(scripts, "Faults/FaultDirector.Reports.cs");
        string world = FaultSource(scripts, "Faults/FaultWorld.cs");
        string flare = (FaultSource(scripts, "Social/Flare/FlareDirector.Live.cs") + FaultSource(scripts, "Social/Flare/FlareDirector.Cases.cs") + FaultSource(scripts, "Social/Flare/FlareDirector.Posting.cs"));
        Check(director.Contains("if (!Following && !GoingPublic.IsGuest)") && Regex.IsMatch(director, @"public void Crime\(Vector3 where\)\s*\{\s*if \([^)]*GoingPublic\.IsGuest")
              && reports.Contains("|| Following || GoingPublic.IsGuest)"),
            "faults, review: a Going Public guest never rolls, turns or reports faults, nor marks a hot spot, even while the host sends none");
        Check(reports.Contains("fault = FaultRules.Claimable(f) ? f.Id : -1;") && !reports.Contains("_caseFault") && flare.Contains("c.Fault = verdict == (int)FaultAnswer.True ? fault : -1;")
              && Regex.Matches(flare, @"FaultActed\?\.Invoke\(c\.Id, c\.Fault,").Count == 2 && flare.Contains("if (c.Verdict == (int)FaultAnswer.True && (order == FlareOrder.Works"),
            "faults, review: the fault a case found is kept with the case, not in memory, and a crew sent under the paper's eye takes it only for a case found true");
        Match stop = Regex.Match(director, @"private void Stop\(string why\)\s*\{(.*?)\n        \}", RegexOptions.Singleline);
        Match destroy = Regex.Match(director, @"private void OnDestroy\(\)\s*\{(.*?)\n        \}", RegexOptions.Singleline);
        Check(stop.Success && !stop.Groups[1].Value.Contains("FaultGame.Forget") && destroy.Success && destroy.Groups[1].Value.Contains("FaultGame.Forget()"),
            "faults, review: stopping (a new save, the option off) keeps the city's lists; only the director's end lets them go");
        Check(Regex.IsMatch(world, @"if \(ReferenceEquals\(LampLights\.GetValue\(lamp\), NoLights\)\)\s*\{\s*return;")
              && world.IndexOf("ReferenceEquals(LampLights.GetValue(lamp), NoLights)", StringComparison.Ordinal) < world.IndexOf("LampLights.SetValue(lamp, NoLights)", StringComparison.Ordinal),
            "faults, review: a lamp already dark is never darkened twice (its own lists are the ones put back)");
        Check(world.Contains("if (lamp.isActiveAndEnabled && !GameManager.isCitySceneBeingUnloaded && InstanceBehavior<GameManager>.Instance != null)")
              && Regex.IsMatch(world, @"foreach \(Shown s in _shown\.Values\)\s*\{\s*try\s*\{\s*Hide\(s\);"),
            "faults, review: a lamp is relit by the game's call only while the city stands, and one that cannot be put back never leaves the others dark");
        string stale = FaultSource(scripts, "Performance/Tweaks/StaleLampLights.cs");
        string trip = FaultSource(scripts, "Ragdoll/PedestrianIncidents.cs");
        Check(Regex.IsMatch(stale, @"protected override void Restore\(\)[\s\S]*?Faults\.FaultDirector\.KeepDark\(\);[^\n]*\s*\}") && trip.Contains("GoingPublic.RemotePlayers(TripRemotes)") && trip.Contains("IsRemote(p.tpc.transform)"),
            "faults, review: the performance tools putting their lamps back leave the city's dark lamps dark, and a trip never lands on another Going Public player");
    }

    // A source of chance the checks control: a plain linear congruential sequence from a seed.
    private sealed class FaultSeqDice : IFaultDice
    {
        private uint _s;

        public FaultSeqDice(uint seed)
        {
            _s = seed;
        }

        public int Next(int most)
        {
            if (most <= 1)
            {
                return 0;
            }
            _s = _s * 1664525u + 1013904223u;
            return (int)((_s >> 8) % (uint)most);
        }
    }

    // ...and one that always says the same (0: every chance taken).
    private sealed class FaultSameDice : IFaultDice
    {
        private readonly int _v;

        public FaultSameDice(int v)
        {
            _v = v;
        }

        public int Next(int most)
        {
            return most <= 1 ? 0 : Math.Min(_v, most - 1);
        }
    }

    private static FaultFacts FaultCity(int spending, int litter, long now, int option)
    {
        var f = new FaultFacts { Now = now, Option = option, Litter = litter };
        string[] ids = { "ba:neighborhood_midtown", "ba:neighborhood_garment", "ba:neighborhood_hamptons", "ba:neighborhood_chinatown" };
        for (int i = 0; i < ids.Length; i++)
        {
            f.Districts.Add(new FaultDistrict { Id = ids[i], Spending = spending, Busy = i, Night = 3 - i });
        }
        return f;
    }

    // How many of a kind an hour's roll wants over many hours, in a city that never fills (the file emptied each hour).
    private static int FaultWanted(FaultKind kind, FaultFacts facts, FaultFile file, int hours, uint seed)
    {
        var dice = new FaultSeqDice(seed);
        var wants = new List<FaultWant>(4);
        int n = 0;
        for (int h = 0; h < hours; h++)
        {
            file.Faults.Clear();
            facts.Now = 1440L * 10 + h * 60L;
            FaultRules.Roll(file, facts, dice, wants);
            for (int i = 0; i < wants.Count; i++)
            {
                n += wants[i].Kind == kind ? 1 : 0;
            }
        }
        return n;
    }

    private static void FaultsWeighting()
    {
        const int Hours = 4000;
        int shortUpkeep = FaultWanted(FaultKind.Lamp, FaultCity(-1, 0, 0, 2), new FaultFile(), Hours, 7);
        int generous = FaultWanted(FaultKind.Lamp, FaultCity(1, 0, 0, 2), new FaultFile(), Hours, 7);
        Check(shortUpkeep > generous * 2, "faults: more street lamps go dark where the council's upkeep is short (" + shortUpkeep + " against " + generous + " in " + Hours + " hours)");

        var dry = new FaultFile();
        var wet = new FaultFile { WetUntil = long.MaxValue };
        int holesDry = FaultWanted(FaultKind.Pothole, FaultCity(0, 0, 0, 2), dry, Hours, 11);
        int holesWet = FaultWanted(FaultKind.Pothole, FaultCity(0, 0, 0, 2), wet, Hours, 11);
        Check(holesWet > holesDry * 3 / 2, "faults: more potholes open after rain (" + holesWet + " against " + holesDry + ")");

        FaultFacts rain = FaultCity(0, 0, 600, 1);
        rain.Rain = true;
        var remember = new FaultFile();
        FaultRules.Weather(remember, rain);
        Check(FaultRules.Wet(remember, 600 + 23 * 60) && !FaultRules.Wet(remember, 600 + 25 * 60), "faults: rain counts for a day after it, and no longer");

        int litterCut = FaultWanted(FaultKind.Litter, FaultCity(0, -1, 0, 2), new FaultFile(), Hours, 13);
        int litterMore = FaultWanted(FaultKind.Litter, FaultCity(0, 1, 0, 2), new FaultFile(), Hours, 13);
        Check(litterCut > litterMore * 2, "faults: litter piles up more where street cleaning was cut (" + litterCut + " against " + litterMore + ")");

        var d = new FaultDistrict { Id = "x", Spending = 0, Busy = 3, Night = 3 };
        var quiet = new FaultDistrict { Id = "y", Spending = 0, Busy = 0, Night = 0 };
        Check(FaultRules.Weight(FaultKind.Litter, d, 0) > FaultRules.Weight(FaultKind.Litter, quiet, 0) && FaultRules.Weight(FaultKind.Lamp, d, FaultRules.AgeDaysFull) > FaultRules.Weight(FaultKind.Lamp, d, 0)
              && FaultRules.Weight(FaultKind.Hotspot, d, 0) == 0 && FaultRules.Weight(FaultKind.Lamp, new FaultDistrict(), 0) == 0,
            "faults: busy, lively districts draw litter, a lamp long unmended is likelier to fail, and a hot spot is never rolled by district");
        Check(FaultRules.Chance(FaultKind.Hotspot, FaultCity(-1, -1, 0, 2), new FaultFile { StormUntil = long.MaxValue }) == 0
              && FaultRules.Chance(FaultKind.Lamp, FaultCity(-1, 0, 19 * 60, 2), new FaultFile { StormUntil = long.MaxValue }) <= 900,
            "faults: a chance an hour stays under nine in ten, and hot spots come only of crimes");
    }

    private static void FaultsLimits()
    {
        bool held = true, filled = true;
        string detail = string.Empty;
        for (int option = 0; option < FaultRules.Most.Length; option++)
        {
            var file = new FaultFile();
            var dice = new FaultSameDice(0);   // every chance taken: the limits alone hold the count
            var wants = new List<FaultWant>(4);
            FaultFacts facts = FaultCity(-1, -1, 0, option);
            for (int h = 0; h < 500; h++)
            {
                facts.Now = h * 60L;
                FaultRules.Roll(file, facts, dice, wants);
                for (int i = 0; i < wants.Count; i++)
                {
                    FaultRules.Add(file, wants[i].Kind, facts.Districts[wants[i].District].Id, null, h, 0f, i, 0f, 0, facts.Now);
                }
                for (int c = 0; c < 20; c++)
                {
                    FaultRules.Crime(file, option, "ba:neighborhood_midtown", null, h * 1000f + c * 400f, 0f, 0f, facts.Now, dice);
                }
                held &= file.Faults.Count <= FaultRules.MostOf(option);
                for (int k = 0; k < FaultRules.Kinds; k++)
                {
                    held &= file.Count((FaultKind)k) <= FaultRules.KindMost(option, (FaultKind)k);
                }
            }
            filled &= file.Faults.Count > 0 && file.Faults.Count <= FaultRules.MostOf(option);
            detail += (detail.Length > 0 ? ", " : string.Empty) + file.Faults.Count + " of " + FaultRules.MostOf(option);
        }
        Check(held && filled && FaultRules.MostOf(-3) == FaultRules.Most[0] && FaultRules.MostOf(9) == FaultRules.Most[2],
            "faults: however often everything breaks, the city holds no more than the option's most, and each kind its share (" + detail + ")");

        var lamps = new FaultFile();
        for (int i = 0; i < FaultRules.KindMost(2, FaultKind.Lamp); i++)
        {
            FaultRules.Add(lamps, FaultKind.Lamp, "ba:neighborhood_midtown", null, i, 0f, 0f, 0f, 0, 0);
        }
        var lampWants = new List<FaultWant>();
        FaultRules.Roll(lamps, FaultCity(-1, -1, 60, 2), new FaultSameDice(0), lampWants);
        bool noLamp = lampWants.Count > 0;
        for (int i = 0; i < lampWants.Count; i++)
        {
            noLamp &= lampWants[i].Kind != FaultKind.Lamp;
        }
        Check(noLamp && FaultRules.KindMost(2, FaultKind.Lamp) == 3 && FaultRules.KindMost(0, FaultKind.Unsafe) == 1,
            "faults: a kind at its share of the city's most gets no more, while the others still can");

        var none = new FaultFile();
        var noDistricts = new FaultFacts { Now = 60, Option = 2 };
        Check(FaultRules.Roll(none, noDistricts, new FaultSameDice(0), new List<FaultWant>()) == 0, "faults: a city with no districts read yet gets nothing");
    }

    private static void FaultsReports()
    {
        var file = new FaultFile();
        Fault lamp = FaultRules.Add(file, FaultKind.Lamp, "ba:neighborhood_midtown", "12 Main Street", 100f, 0f, 100f, 0f, 0, 600);
        Fault heap = FaultRules.Add(file, FaultKind.Litter, "ba:neighborhood_garment", "3 Elm Street", 900f, 0f, 900f, 0f, 1, 600);
        Check(FaultRules.Match(file, FaultKind.Lamp, "ba:neighborhood_midtown", true, 150f, 120f) == lamp
              && FaultRules.Match(file, FaultKind.Lamp, null, true, 100f + FaultRules.MatchMetres + 50f, 100f) == null
              && FaultRules.Match(file, FaultKind.Litter, "ba:neighborhood_garment", false, 0f, 0f) == heap
              && FaultRules.Match(file, FaultKind.Litter, "ba:neighborhood_midtown", false, 0f, 0f) == null
              && FaultRules.Match(file, FaultKind.Pothole, "ba:neighborhood_midtown", true, 100f, 100f) == null,
            "faults: a report finds the real fault of its kind where it was made (near the player, or in the district named), and nothing elsewhere");

        FaultRules.Taken(lamp, 7, 700, true);
        Check(lamp.Fixing && lamp.FixAt < 0 && file.OfCase(7) == lamp, "faults: a works crew takes a lamp; it stays until the works are handed over");
        var gone = new List<int>();
        FaultRules.Turn(file, 700 + 1440 * 3, 0, gone);
        Check(file.Find(lamp.Id) != null, "faults: a lamp under works is not mended by time alone");
        Check(FaultRules.Fixed(file, lamp.Id, 3) && file.Find(lamp.Id) == null && file.Mended.Count == 1 && FaultRules.AgeDays(file, FaultKind.Lamp, "ba:neighborhood_midtown", 5) == 2,
            "faults: works handed over mend the lamp, and the city remembers when");

        Fault held = FaultRules.Add(file, FaultKind.Litter, "ba:neighborhood_garment", null, 950f, 0f, 950f, 0f, 1, 4000);
        FaultRules.Taken(held, 8, 4000, false);
        Check(FaultRules.Match(file, FaultKind.Litter, null, true, 950f, 950f) == held && !FaultRules.Claimable(held) && FaultRules.Claimable(heap),
            "faults: a fault a crew is already clearing is still there, so a report of it is true; the first case keeps it and a later one takes nothing");
        gone.Clear();
        FaultRules.Turn(file, 4000 + 6 * 60 - 1, 0, gone);
        bool before = file.Find(held.Id) != null;
        FaultRules.Turn(file, 4000 + 6 * 60, 0, gone);
        Check(before && file.Find(held.Id) == null && gone.Contains(held.Id), "faults: a cleaning crew clears its heap when its time is up, not before");

        gone.Clear();
        FaultRules.Turn(file, 600 + 4 * 1440L - 1, 0, gone);
        bool lying = file.Find(heap.Id) != null;
        FaultRules.Turn(file, 600 + 4 * 1440L, 0, gone);
        Check(lying && file.Find(heap.Id) == null, "faults: the city's own sweepers take an old heap after " + FaultRules.LitterDays + " days");

        var hot = new FaultFile();
        var yes = new FaultSameDice(0);
        Fault spot = FaultRules.Crime(hot, 1, "ba:neighborhood_midtown", null, 10f, 0f, 10f, 1000, yes);
        Fault again = FaultRules.Crime(hot, 1, "ba:neighborhood_midtown", null, 60f, 0f, 10f, 2000, yes);
        Check(spot != null && again == null && spot.Crimes == 2 && FaultRules.Crime(hot, 1, null, null, 0f, 0f, 0f, 3000, yes) == null
              && FaultRules.Match(hot, FaultKind.Hotspot, "ba:neighborhood_midtown", true, 20f, 20f) == spot,
            "faults: crimes reported near each other make one hot spot, and a crime report there finds it");
        gone.Clear();
        FaultRules.Turn(hot, 2000 + 5 * 1440L - 1, 0, gone);
        bool still = hot.Faults.Count == 1;
        FaultRules.Turn(hot, 2000 + 5 * 1440L, 0, gone);
        Check(still && hot.Faults.Count == 0, "faults: a hot spot with no crime for " + FaultRules.HotspotDays + " days ends");

        var forget = new FaultFile();
        Fault stuck = FaultRules.Add(forget, FaultKind.Pothole, "x", null, 0f, 0f, 0f, 0f, 0, 0);
        FaultRules.Taken(stuck, 3, 100, true);
        FaultRules.Turn(forget, 100 + FaultRules.CaseForgetDays * 1440L, 0, null);
        Check(!stuck.Fixing && forget.Find(stuck.Id) != null, "faults: a case that took a fault and was never done lets it go; the hole is still there");

        Check(FaultRules.Clean(FaultRules.CleanFrom) && !FaultRules.Clean(FaultRules.CleanFrom - 1), "faults: an inspection finds nothing at a clean business, and something at a dirty one");
    }

    private static void FaultsEscalation()
    {
        var file = new FaultFile();
        FaultVerdict first = FaultRules.Nothing(file, "me", 10);
        FaultVerdict second = FaultRules.Nothing(file, "me", 11);
        bool notYet = !FaultRules.Barred(file, "me", 11);
        FaultVerdict third = FaultRules.Nothing(file, "me", 12);
        Check(first == FaultVerdict.Nothing && second == FaultVerdict.Warned && notYet && third == FaultVerdict.Barred,
            "faults: a player's reports of nothing: answered so, then a warning, then no crews (" + first + ", " + second + ", " + third + ")");
        Check(FaultRules.Barred(file, "me", 12) && FaultRules.Barred(file, "me", 14) && !FaultRules.Barred(file, "me", 15),
            "faults: no crews for that player's reports for " + FaultRules.BarDays + " days, then again");
        Check(!FaultRules.Barred(file, "p:other", 12) && FaultRules.Nothing(file, "p:other", 12) == FaultVerdict.Nothing, "faults: another player's reports count for them alone");

        var slow = new FaultFile();
        FaultRules.Nothing(slow, "me", 1);
        FaultVerdict later = FaultRules.Nothing(slow, "me", 1 + FaultRules.StrikeDays);
        Check(later == FaultVerdict.Nothing, "faults: reports of nothing more than a week apart start again");
        Check(FlareLiveRules.FalseReport(50) == 50 - FlareLiveRules.FalseReportCost && FlareLiveRules.FalseReport(1) == 0, "faults: a report of nothing costs a little Flare reputation, never below none");
    }

    private static void FaultsSeen()
    {
        var file = new FaultFile();
        Fault lamp = FaultRules.Add(file, FaultKind.Lamp, "a", null, 0f, 0f, 0f, 0f, 0, 0);
        Fault hole = FaultRules.Add(file, FaultKind.Pothole, "a", null, 0f, 0f, 0f, 0f, 0, 0);
        Fault taken = FaultRules.Add(file, FaultKind.Litter, "a", null, 0f, 0f, 0f, 0f, 0, 0);
        FaultRules.Taken(taken, 1, 0, false);
        var seen = new List<Fault>();
        var always = new FaultSameDice(0);
        FaultRules.Seen(file, 12 * 60, always, seen);
        bool day = seen.Count == 1 && seen[0] == hole;
        FaultRules.Seen(file, 22 * 60, always, seen);
        bool night = seen.Count == 2 && seen.Contains(lamp) && seen.Contains(hole);
        Check(day && night && hole.Reports == 2 && lamp.Reports == 1 && taken.Reports == 0,
            "faults: the city's people report real faults (a dark lamp only after dark), and never one a crew has in hand");

        int young = 0, old = 0;
        var dice = new FaultSeqDice(5);
        for (int i = 0; i < 4000; i++)
        {
            var one = new FaultFile();
            FaultRules.Add(one, FaultKind.Litter, "a", null, 0f, 0f, 0f, 0f, 0, 0);
            young += FaultRules.Seen(one, 60, dice, seen);
            old += FaultRules.Seen(one, 60 + FaultRules.SeenAgeDays * 1440L, dice, seen);
        }
        Check(old > young * 2, "faults: the longer a fault stands, the likelier the city's people report it (" + old + " against " + young + ")");
    }

    private static void FaultsSave()
    {
        var file = new FaultFile { LastRoll = 1234, WetUntil = 5000, StormUntil = -1, FrostUntil = 777 };
        Fault f = FaultRules.Add(file, FaultKind.Unsafe, "ba:neighborhood_x|y", "1 Main^Street %", 12.5f, -3.25f, 400.125f, 270f, 999, 3600);
        f.Reports = 4;
        FaultRules.Taken(f, 17, 4000, true);
        Fault h = FaultRules.Add(file, FaultKind.Hotspot, "d", string.Empty, 1f, 2f, 3f, 0f, 0, 10);
        h.Crimes = 3;
        h.LastCrime = 99;
        FaultRules.Nothing(file, "p:76561198000000000", 3);
        FaultRules.Fixed(file, FaultRules.Add(file, FaultKind.Lamp, "m", null, 0f, 0f, 0f, 0f, 0, 0).Id, 6);
        string line = FaultStore.Write(file);
        FaultFile back = FaultStore.Read(line);
        bool same = back != null && back.Faults.Count == 2 && back.NextId == file.NextId && back.LastRoll == 1234 && back.WetUntil == 5000 && back.FrostUntil == 777
                    && back.Strikes.Count == 1 && back.Strikes[0].Reporter == "p:76561198000000000" && back.Strikes[0].Count == 1 && back.Mended.Count == 1 && back.Mended[0].Day == 6;
        Fault b = same ? back.Find(f.Id) : null;
        same &= b != null && b.Kind == FaultKind.Unsafe && b.District == f.District && b.Address == f.Address && b.X == 12.5f && b.Y == -3.25f && b.Z == 400.125f && b.Heading == 270f
                && b.Variant == 999 && b.Since == 3600 && b.Reports == 4 && b.Case == 17 && b.CaseAt == 4000 && b.FixAt == -1;
        Fault bh = same ? back.Find(h.Id) : null;
        same &= bh != null && bh.Crimes == 3 && bh.LastCrime == 99 && bh.Address == string.Empty;
        Check(same, "faults: the save's line reads back every fault, its place and its case, the players' reports of nothing and what was mended");
        Check(back != null && FaultStore.Read(FaultStore.Write(back)) != null && FaultStore.Write(back) == line, "faults: the line written again is the same line");
        Check(FaultStore.Read(null) != null && FaultStore.Read(null).Faults.Count == 0 && FaultStore.Read("v=2^f=1") == null && FaultStore.Read("v=1^f=1|2") == null && FaultStore.Read("v=1^q") == null,
            "faults: no line is a city with none; a later version's line or a broken one is not read (it is kept in the save as it was)");
        FaultFile later = FaultStore.Read(line.Replace("^m=", "^z=later^m=") + "|extra");
        Check(later != null && later.Mended.Count == 1, "faults: a record of a later kind is passed over, and a later field at a record's end is not read");
    }

    private static void FaultsShare()
    {
        var host = new FaultFile();
        FaultRules.Add(host, FaultKind.Lamp, "ba:neighborhood_midtown", "12 Main Street", 1f, 2f, 3f, 90f, 5, 600);
        FaultRules.Add(host, FaultKind.Pothole, "ba:neighborhood_garment", "3 Elm|Street", 4f, 5f, 6f, 180f, 6, 660);
        string text = FaultShare.Write(host);
        var guest = new FaultFile { NextId = 1 };
        FaultRules.Add(guest, FaultKind.Litter, "own", null, 0f, 0f, 0f, 0f, 0, 0);
        bool applied = FaultShare.Apply(guest, text);
        Check(applied && guest.Faults.Count == 2 && guest.Faults[1].Address == "3 Elm|Street" && guest.Faults[0].Heading == 90f && guest.NextId == host.NextId,
            "faults, Going Public: a guest takes the host's faults whole, in place of its own");
        int before = guest.Faults.Count;
        Check(!FaultShare.Apply(guest, "K9\n1|0|x") && !FaultShare.Apply(guest, FaultShare.Head + "\nbroken") && !FaultShare.Apply(guest, null) && guest.Faults.Count == before,
            "faults, Going Public: a part of another layout or a broken one changes nothing");
        Check(FaultShare.Write(new FaultFile()) == FaultShare.Head && FaultShare.Apply(guest, FaultShare.Head) && guest.Faults.Count == 0,
            "faults, Going Public: the host's city with no faults leaves a guest with none");
        Check(text.Length < 2000, "faults, Going Public: the part is a short line (" + text.Length + " characters for two faults)");

        // Through a whole message of the shared city, beside another system's part, and a letter of its own.
        string through = SharedThrough(SharedKind.Snapshot, FaultShare.Part, text);
        var m = new SharedMessage { Kind = SharedKind.Snapshot, Version = "0.3.8", Sequence = 3 };
        m.Parts.Add(new SharedPart('E', "economy"));
        m.Parts.Add(new SharedPart(FaultShare.Part, text));
        SharedRead read = SharedCityWire.Read(SharedCityWire.Write(m), "0.3.8", out SharedMessage back);
        Check(through == text && read == SharedRead.Ok && back.Part('E') == "economy" && back.Part(FaultShare.Part) == text,
            "faults, Going Public: the faults' part goes through the shared city's message with the other parts (a late joiner's snapshot carries it)");
        Check(FaultShare.Part != 'E' && FaultShare.Part != 'G' && FaultShare.Part != 'W' && FaultShare.Part != 'N' && FaultShare.Part != 'S' && FaultShare.Part != 'M'
              && FaultShare.Part != LedgerShare.Part && FaultShare.Part != FlareNetShare.Part && char.IsUpper(FaultShare.Part),
            "faults, Going Public: the faults have a letter of their own (" + FaultShare.Part + ")");

        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string shared = FaultSource(scripts, "Core/SharedCity.cs");
        string director = FaultSource(scripts, "Faults/FaultDirector.cs");
        Check(Regex.IsMatch(shared, @"SharedCityPart part = Find\(change \? char\.ToUpperInvariant\(id\) : id\);\s*if \(part == null\)\s*\{\s*continue;")
              && Regex.IsMatch(shared, @"for \(int i = 0; i < Parts\.Count; i\+\+\)\s*\{\s*SharedCityPart part = Parts\[i\];\s*string text = m\.Part\(part\.Id\);"),
            "faults, Going Public: an RLO that knows no part K passes over it (an update looks a part up by its letter and skips one it has not; a snapshot applies only the parts it knows)");
        Check(Regex.IsMatch(director, @"if \(!Following && !GoingPublic\.IsGuest\)[^{]*\{[^}]*FaultRules\.Turn") && director.Contains("changed |= Roll(now);") && director.IndexOf("changed |= Roll(now);", StringComparison.Ordinal) < director.IndexOf("if (changed)", StringComparison.Ordinal)
              && director.Contains("Id = FaultShare.Part") && director.Contains("Write = () => _loaded && !_failed && Settings.CityFaults ? FaultShare.Write(_file) : null")
              && director.Contains("SharedCity.Changed(FaultShare.Part)") && Regex.IsMatch(director, @"public void Crime\(Vector3 where\)\s*\{\s*if \([^)]*Following"),
            "faults, Going Public: only the city's runner rolls (a guest that follows never does), the host sends the faults whole when they change and to a joining guest");
    }

    private static string FaultSource(string scripts, string file)
    {
        string path = System.IO.Path.Combine(scripts, file.Replace('/', System.IO.Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void FaultsRandom()
    {
        var a = new FaultDice();
        var b = new FaultDice();
        int same = 0;
        for (int i = 0; i < 64; i++)
        {
            same += a.Next(1 << 30) == b.Next(1 << 30) ? 1 : 0;
        }
        Check(same < 4, "faults: two live sources of chance started together give different numbers (" + same + " of 64 alike): no fixed seed");
        bool ranged = true;
        for (int i = 0; i < 2000; i++)
        {
            int v = a.Next(7);
            ranged &= v >= 0 && v < 7 && a.Next(1) == 0 && a.Next(0) == 0;
        }
        Check(ranged, "faults: the live source gives numbers in range, and 0 when there is one choice");

        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string dice = FaultSource(scripts, "Faults/FaultDice.cs");
        string code = Regex.Replace(dice, @"///[^\n]*|//[^\n]*", string.Empty);
        string director = FaultSource(scripts, "Faults/FaultDirector.cs");
        Check(code.Contains("RandomNumberGenerator.Create()") && code.Contains("Environment.TickCount") && !Regex.IsMatch(code, @"Save|Day|Seed|District|modData|GameInstance")
              && Regex.IsMatch(director, @"IFaultDice _dice = new FaultDice\(\);") && !Regex.IsMatch(director, @"new FaultDice\([^)]"),
            "faults: the live source of chance is started from the system's cryptographic source and the clock, never from the save, the day or a district");
        string all = director + FaultSource(scripts, "Faults/FaultDirector.Reports.cs") + FaultSource(scripts, "Faults/FaultDirector.Tests.cs") + FaultSource(scripts, "Faults/FaultGame.cs") + FaultSource(scripts, "Faults/FaultWorld.cs");
        Check(!Regex.IsMatch(all, @"UnityEngine\.Random|Random\.(Range|value)|FlareRules\.Roll|new System\.Random|new Random\("),
            "faults: every roll of the faults goes through their one source of chance");
    }

    private static void FaultsFlare()
    {
        // While the faults are real, Flare's own day invents no report of a road, a lamp, crime, litter or an unsafe place.
        bool none = true;
        int reports = 0, invented = 0;
        for (int day = 1; day <= 60; day++)
        {
            FlareFile f = FlareLiveFile(day);
            FlareCityFacts facts = FlareFakeCity(day, 2, true);
            facts.RealFaults = true;
            var made = new List<FlarePost>();
            FlareLiveRules.CityDay(f, facts, FlareCitizensOf(f, facts), made);
            FlareFile g = FlareLiveFile(day);
            FlareCityFacts plain = FlareFakeCity(day, 2, true);
            var made2 = new List<FlarePost>();
            FlareLiveRules.CityDay(g, plain, FlareCitizensOf(g, plain), made2);
            for (int i = 0; i < made.Count; i++)
            {
                if (made[i].Source == FlareSource.Report)
                {
                    reports++;
                    none &= made[i].Arg == (int)FlareReport.Transit;
                }
            }
            for (int i = 0; i < made2.Count; i++)
            {
                invented += made2[i].Source == FlareSource.Report && made2[i].Arg != (int)FlareReport.Transit ? 1 : 0;
            }
        }
        Check(none && invented > 0, "faults: while they are real the city's people make up no reports of faults (" + reports + " about the buses only; " + invented + " made up with the faults off)");

        Check(FlareLiveRules.Sooner(1000, 900) == 960 && FlareLiveRules.Sooner(930, 900) == 920,
            "faults: the same real fault reported again brings the City Office's answer sooner, never at once");

        var net = new FlareNet();
        var unsafeCase = new FlareCase { Id = 1, Kind = FlareReport.Unsafe, District = "d" };
        FlareOrder real = FlareLiveRules.Decide(net, unsafeCase, 0, true, false, true, out FlareAnswer realAnswer);
        FlareOrder plainOrder = FlareLiveRules.Decide(net, unsafeCase, 0, true, false, false, out FlareAnswer plainAnswer);
        FlareOrder off = FlareLiveRules.Decide(net, unsafeCase, 0, false, false, true, out _);
        Check(real == FlareOrder.Works && realAnswer == FlareAnswer.Works && plainOrder == FlareOrder.Patrol && off == FlareOrder.Answered,
            "faults: a real unsafe place gets a works crew; without the faults it is the police's as before; with the City Office not acting, an answer only");

        Check((int)FaultAnswer.True == (int)FaultVerdict.True && (int)FaultAnswer.Nothing == (int)FaultVerdict.Nothing && (int)FaultAnswer.Warned == (int)FaultVerdict.Warned
              && (int)FaultAnswer.Barred == (int)FaultVerdict.Barred && (int)FaultAnswer.Clean == (int)FaultVerdict.Clean,
            "faults: Flare reads the faults' verdicts by the same numbers");
        bool kinds = true;
        for (int k = 0; k < FaultRules.Kinds; k++)
        {
            kinds &= FaultRules.KindOf(FaultRules.ReportOf((FaultKind)k), out FaultKind back) && back == (FaultKind)k;
        }
        kinds &= FaultRules.ReportOf(FaultKind.Pothole) == (int)FlareReport.Road && FaultRules.ReportOf(FaultKind.Lamp) == (int)FlareReport.Lamp && FaultRules.ReportOf(FaultKind.Hotspot) == (int)FlareReport.Crime
                 && FaultRules.ReportOf(FaultKind.Litter) == (int)FlareReport.Litter && FaultRules.ReportOf(FaultKind.Unsafe) == (int)FlareReport.Unsafe
                 && !FaultRules.KindOf((int)FlareReport.Business, out _) && !FaultRules.KindOf((int)FlareReport.Transit, out _);
        Check(kinds, "faults: each Flare report of a road, a lamp, crime, litter or an unsafe place is about its own kind of fault, and the others are not judged");

        Check(FaultRules.Over(0f, 0f, 10f, 0f, 5f, 0.5f) && !FaultRules.Over(0f, 0f, 10f, 0f, 5f, 1.5f) && FaultRules.Over(0f, 0f, 0f, 0f, 0.2f, 0f) && !FaultRules.Over(0f, 0f, 3f, 0f, 5f, 0f),
            "faults: a wheel's path over a hole is felt, beside it or short of it not");
        Check(FaultRules.TyreWear(14f) > FaultRules.TyreWear(3f) * 3f && FaultRules.Jolt(20f) > FaultRules.Jolt(2f) && FaultRules.Jolt(100f) <= 0.8f
              && FaultRules.SlowLimit(50) == 35 && FaultRules.SlowLimit(25) == FaultRules.PotholeSlowest && FaultRules.SlowLimit(10) == 10,
            "faults: a pothole wears tyres and jolts more at speed, and slows its lane a little, never below 20 km/h");
        var litter = new FaultFile();
        FaultRules.Add(litter, FaultKind.Litter, "a", null, 10f, 0f, 0f, 0f, 0, 0);
        FaultRules.Add(litter, FaultKind.Litter, "a", null, 20f, 0f, 0f, 0f, 0, 0);
        FaultRules.Add(litter, FaultKind.Litter, "a", null, 30f, 0f, 0f, 0f, 0, 0);
        Check(FaultRules.LitterFootfall(FaultRules.LitterAt(litter, 0f, 0f, 15f)) == -2 && FaultRules.LitterFootfall(FaultRules.LitterAt(litter, 20f, 0f, 15f)) == -4
              && FaultRules.LitterFootfall(0) == 0 && FaultRules.LitterIn(litter, "a") && !FaultRules.LitterIn(litter, "b"),
            "faults: litter costs a business near it customers, more for more heaps up to a most, none without");
        Check(FaultRules.CrimeScale(true, 22) == FaultRules.DarkCrime && FaultRules.CrimeScale(true, 12) == 1f && FaultRules.CrimeScale(false, 22) == 1f,
            "faults: a dark street makes crime likelier only after dark");
    }

    private static void FaultsTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("Faults", words, codes))
        {
            return;
        }
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var key = new Regex("\"(reallife_(?:faults|meta_title_faults|guide_faults|opt_sec_faults|flare_answer_1[6-9]|flare_report_real|flare_note_nothing|flare_note_warned|flare_note_barred)[a-z0-9_]*)\"");
        foreach (string file in new[] { "Faults/FaultGuide.cs", "Faults/FaultDirector.Tests.cs", "Social/Flare/FlareText.Live.cs", "Social/Flare/FlareDirector.Live.cs", "Social/Flare/FlareDirector.Cases.cs", "Social/Flare/FlareDirector.Posting.cs" })
        {
            foreach (Match m in key.Matches(FaultSource(scripts, file)))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string id in new[] { "city_faults", "city_faults_count", "city_faults_false_cost" })
        {
            keys.Add("reallife_opt_tip_" + id);
        }
        keys.Add("reallife_opt_sec_faults");
        keys.Add("reallife_contract_part_street_lamps_out");
        var missing = new List<string>();
        foreach (string k in keys)
        {
            string lacking = LanguagesLacking(words, codes, k);
            if (lacking.Length > 0 || (words[2].TryGetValue(k, out string zh) && !HasHan(zh)))
            {
                missing.Add(k + (lacking.Length > 0 ? " (" + lacking + ")" : " (zh-cn not Chinese)"));
            }
        }
        Check(keys.Count >= 35 && missing.Count == 0, "faults: every text of the city's faults is in en, pt and zh-cn, with the same names in curly brackets (" + keys.Count + " keys"
                                                       + (missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : string.Empty) + ")");
        bool notes = true;
        foreach (string k in keys)
        {
            if ((k.StartsWith("reallife_flare_note_", StringComparison.Ordinal) || k.EndsWith("_done", StringComparison.Ordinal)) && words[0].TryGetValue(k, out string t))
            {
                notes &= t.Length <= 240;
            }
        }
        Check(notes, "faults: every notification is short enough for a toast");
        bool facts = true;
        foreach (string fact in new[] { "{faults_few}", "{faults_some}", "{faults_many}", "{faults_match}", "{faults_warn_at}", "{faults_bar_at}", "{faults_bar_days}", "{faults_strike_days}",
                     "{faults_reputation}", "{faults_hotspot_days}", "{faults_litter_days}", "{faults_litter_percent}", "{faults_litter_most}", "{faults_dark_crime}", "{faults_slow_kmh}" })
        {
            for (int l = 0; l < 3; l++)
            {
                facts &= words[l].TryGetValue("reallife_guide_faults", out string page) && page.Contains(fact);
            }
        }
        string guide = FaultSource(scripts, "Faults/FaultGuide.cs");
        foreach (Match m in Regex.Matches(words[0].TryGetValue("reallife_guide_faults", out string en) ? en : string.Empty, @"\{([a-z_]+)\}"))
        {
            facts &= guide.Contains(".Fact(\"" + m.Groups[1].Value + "\"");
        }
        Check(facts, "faults: the Guide's page gives the faults' figures from the code, in every language");
        bool flarePage = true;
        string[] marks = { "City faults", "Defeitos da cidade", "城市故障" };
        for (int l = 0; l < 3; l++)
        {
            flarePage &= words[l].TryGetValue("reallife_guide_flare", out string page) && page.Contains(marks[l]);
        }
        Check(flarePage, "faults: Flare's Guide page says reports are checked against the city's faults, in every language");
        string[] said = { "faults", "defeitos", "故障" };
        bool news = true;
        for (int l = 0; l < 3; l++)
        {
            bool found = false;
            foreach (KeyValuePair<string, string> pair in words[l])
            {
                found |= pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !pair.Key.EndsWith("_title", StringComparison.Ordinal) && pair.Value.Contains(said[l])
                         && pair.Value.Contains("Flare");
            }
            news &= found;
        }
        Check(news, "faults: What's new has the line in en, pt and zh-cn");
    }

    private static void FaultsTiming()
    {
        long ticks = FaultsRollMany(2000, out int faults);
        double ms = ticks * 1000.0 / Stopwatch.Frequency;
        Check(ms < 200.0 && faults > 0, "faults: two thousand hours rolled, reported and turned in a full city take " + ms.ToString("F1") + " ms (under 200: a fraction of a millisecond an hour)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long FaultsRollMany(int hours, out int faults)
    {
        var file = new FaultFile();
        var dice = new FaultSeqDice(3);
        var wants = new List<FaultWant>(4);
        var seen = new List<Fault>(8);
        var gone = new List<int>(8);
        FaultFacts facts = FaultCity(-1, -1, 0, 2);
        long start = Stopwatch.GetTimestamp();
        for (int h = 0; h < hours; h++)
        {
            facts.Now = h * 60L;
            gone.Clear();
            FaultRules.Turn(file, facts.Now, -1, gone);
            FaultRules.Roll(file, facts, dice, wants);
            for (int i = 0; i < wants.Count; i++)
            {
                FaultRules.Add(file, wants[i].Kind, facts.Districts[wants[i].District].Id, null, h, 0f, i, 0f, wants[i].Variant, facts.Now);
            }
            FaultRules.Seen(file, facts.Now, dice, seen);
            FaultRules.Match(file, FaultKind.Lamp, "ba:neighborhood_midtown", true, 0f, 0f);
        }
        faults = file.Faults.Count;
        return Stopwatch.GetTimestamp() - start;
    }
}
