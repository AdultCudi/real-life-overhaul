using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;

// Checks for what a long session keeps: the session record's count of the mod's objects by holder and by kind
// (Diagnostics/ObjectKinds.cs, compiled as it ships), which names what grows in the line a minute, and over the sources
// that the record counts them once a minute and never in a frame; and a record of the game's pooled people
// (Core/CacheRules.cs, People/StreetPeople.cs) that starts again once it has outgrown the pools; and over the sources,
// that what a long session used to keep is let go on every path: a render target held across frames, the clip players
// of characters the game destroyed, a replaced director's handlers, a desk's pointer, a car fire's hazard lights, a
// half-built cordon or tow truck, a portrait handed back unreadable, Flare's rows' heights.
internal static partial class Program
{
    private static void Memory()
    {
        KindNames();
        KindGrowth();
        KindCap();
        KindSources();
        PooledRecord();
        LetGoSources();
    }

    private static void LetGoSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string camera = Source(scripts, @"Social\Flare\FlareCityCamera.cs");
        Check(Body(camera, "public static IEnumerator Take(").Contains("_pending = target;") && Body(camera, "public static void Clear()").Contains("Release();")
              && Body(camera, "private static void Release()").Contains("RenderTexture.ReleaseTemporary(_pending)"),
            "let go: a city photo's render target is held where Clear can release it, so a photo stopped while its pixels come back does not keep it");
        string drawn = Body(Source(scripts, @"Social\Flare\FlareApp.Pictures.cs"), "private static Texture2D Drawn(");
        int fin = drawn.IndexOf("finally", StringComparison.Ordinal), release = drawn.IndexOf("RenderTexture.ReleaseTemporary(rt)", StringComparison.Ordinal);
        Check(fin >= 0 && release > fin && drawn.IndexOf("RenderTexture.active = before", fin, StringComparison.Ordinal) > fin,
            "let go: a post's picture lets its render target go in a finally, a fault while drawing included");
        Check(Body((Source(scripts, @"Social\Flare\FlareApp.Cells.cs") + Source(scripts, @"Social\Flare\FlareApp.Cards.cs")), "private void Measured(").Contains("_heights.Count >= HeightsMost"),
            "let go: Flare's rows' heights are kept for HeightsMost rows at most");
        string animators = Source(scripts, @"CityLife\Animation\ActivityAnimators.cs");
        string sweep = Body(animators, "private static void SweepDead()");
        Check(Body(animators, "public static ActivityAnimator For(").Contains("Players.Count >= SweepFrom") && sweep.Contains("Dispose()") && sweep.Contains("Players.Remove(")
              && sweep.Contains("Stepping.Remove("),
            "let go: the clip players of characters the game's pools destroyed are disposed and forgotten once SweepFrom are kept");
        string economy = Body(Source(scripts, @"Economy\Banking\EconomyDirector.cs"), "private void OnDestroy()");
        int off = economy.IndexOf("CreditLedger.GradeChanged -= _onGrade", StringComparison.Ordinal), early = economy.IndexOf("if (Instance != this)", StringComparison.Ordinal);
        Check(off >= 0 && early > off && economy.IndexOf("CreditLedger.Posted -= _onPosted", StringComparison.Ordinal) < early,
            "let go: a replaced economy director takes its handlers off the credit ledger's static events before it returns early");
        string desk = Body(Source(scripts, @"Careers\CareersDirector.Desk.cs"), "private void DeskRelease(");
        int pointer = desk.IndexOf("Destroy(_deskPointer.gameObject)", StringComparison.Ordinal);
        Check(pointer >= 0 && desk.IndexOf("_deskPointer = null", StringComparison.Ordinal) > pointer, "let go: the desk's pointer is destroyed when the desk is let go");
        string fire = Source(scripts, @"Events\FireScene.cs");
        int hazards = fire.IndexOf("_hazards = TrafficEffects.CreateFlasher(", StringComparison.Ordinal);
        Check(hazards > 0 && fire.LastIndexOf("Object.Destroy(_hazards)", hazards, StringComparison.Ordinal) > fire.LastIndexOf("private ", hazards, StringComparison.Ordinal),
            "let go: a car fire's hazard lights are taken off a car before new ones are put on");
        string cordon = Body(Source(scripts, @"Traffic\SceneCordon.cs"), "private bool Prewarm(");
        int caught = cordon.IndexOf("catch (Exception e)", StringComparison.Ordinal);
        Check(caught >= 0 && cordon.IndexOf("Destroy(_props)", caught, StringComparison.Ordinal) > caught, "let go: a cordon that fails half built in its prewarm is destroyed");
        string tow = Body(Source(scripts, @"Traffic\Tow\TowTruck.cs"), "public static TowTruck Make(");
        int towCaught = tow.IndexOf("catch", StringComparison.Ordinal);
        Check(towCaught >= 0 && tow.IndexOf("Destroy(root)", towCaught, StringComparison.Ordinal) > towCaught && tow.IndexOf("throw;", towCaught, StringComparison.Ordinal) > towCaught,
            "let go: a tow truck that fails half built is destroyed, and the fault still reaches its caller");
        string portrait = Body(Source(scripts, @"Phone\Contacts\ContactPainter.cs"), "private void Drawn(");
        int unreadable = portrait.IndexOf("!source.isReadable", StringComparison.Ordinal), requeue = portrait.IndexOf("Requeue(p)", StringComparison.Ordinal);
        Check(unreadable >= 0 && requeue > unreadable && portrait.IndexOf("Destroy(sprite)", unreadable, StringComparison.Ordinal) is int gone && gone > unreadable && gone < requeue,
            "let go: a portrait the game hands back unreadable is destroyed before the face is asked for again");
    }

    // The game's pools destroy what they overflow with and make new people: a record of every id ever seen must not grow
    // with them. Ten thousand seconds of a pool of 60 that swaps 3 people a second, looked at once a second as StreetPeople does.
    private static void PooledRecord()
    {
        const int live = 60, swaps = 3;
        var known = new HashSet<int>();
        var pool = new List<int>(live);
        int next = 0, most = 0, mostWithout = 0;
        for (int i = 0; i < live; i++)
        {
            pool.Add(next++);
        }
        var without = new HashSet<int>();
        for (int second = 0; second < 10000; second++)
        {
            for (int s = 0; s < swaps; s++)
            {
                pool[(second * 7 + s * 13) % live] = next++;   // one destroyed, a new one made in its place
            }
            if (CacheRules.Stale(known.Count, pool.Count))
            {
                known.Clear();
            }
            for (int i = 0; i < pool.Count; i++)
            {
                known.Add(pool[i]);
                without.Add(pool[i]);
            }
            most = Math.Max(most, known.Count);
            mostWithout = Math.Max(mostWithout, without.Count);
        }
        Check(most <= 3 * live + CacheRules.Slack && mostWithout > 100 * live,
            "pooled record: the record of the game's pooled people stays within three times the pool (" + most + " at most), where one never started again holds every id ever seen (" + mostWithout + ")");
        Check(!CacheRules.Stale(0, 0) && !CacheRules.Stale(2 * live + CacheRules.Slack, live) && CacheRules.Stale(2 * live + CacheRules.Slack + 1, live),
            "pooled record: it starts again only past twice the pool and the slack");
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string refresh = Body(Source(scripts, @"People\StreetPeople.cs"), "private static void Refresh(");
        int stale = refresh.IndexOf("CacheRules.Stale(Known.Count, live)", StringComparison.Ordinal), add = refresh.IndexOf("Known[id] = human", StringComparison.Ordinal);
        Check(stale >= 0 && add > stale && refresh.Contains("live += Pools[p] != null ? Pools[p].childCount : 0"),
            "pooled record: StreetPeople counts what its pools hold and starts its record again before it adds to it");
    }

    // A kind is a name without the mod's prefix, a copy's mark and the numbers at its end.
    private static void KindNames()
    {
        Check(ObjectKinds.Kind("RealLifeOverhaul Bus Stop 12") == "Bus Stop", "kinds: a numbered stop is a bus stop");
        Check(ObjectKinds.Kind("Cone (3)") == "Cone" && ObjectKinds.Kind("Burst 3(Clone)") == "Burst" && ObjectKinds.Kind("Smoke(Clone)") == "Smoke",
            "kinds: the engine's copy marks and numbers in brackets are taken off");
        Check(ObjectKinds.Kind("RealLife Flare face") == "Flare face" && ObjectKinds.Kind("Prefab_FireHydrant (45)") == "Prefab_FireHydrant" && ObjectKinds.Kind("Lamp 2a") == "Lamp 2a",
            "kinds: the short prefix goes, and a name that does not end in a number stays whole");
        Check(ObjectKinds.Kind("RealLifeOverhaul") == "RealLifeOverhaul" && ObjectKinds.Kind("12") == "12" && ObjectKinds.Kind(null) == "(unnamed)",
            "kinds: a name with nothing left is kept whole, and no name is named so");
        Check(ObjectKinds.Kind("RealLifeOverhaul " + new string('x', 90)).Length == ObjectKinds.LongestName, "kinds: a long name is cut");
        Check(ObjectKinds.Key("RealLifeOverhaul", "RealLifeOverhaul Civic Props", "cone") == "Civic Props > cone"
              && ObjectKinds.Key("RealLifeOverhaul", "RealLifeOverhaul Civic Props", null) == "Civic Props"
              && ObjectKinds.Key("RealLifeOverhaul", null, null) == "RealLifeOverhaul",
            "kinds: under the mod's own root a key starts at the holder");
        Check(ObjectKinds.Key("RealLifeOverhaul Character", "Light Holder", "Rim Light 2") == "Character > Light Holder > Rim Light"
              && ObjectKinds.Key("RealLifeOverhaul Bus Stop 4", null, null) == "Bus Stop",
            "kinds: under any other root the root's kind comes first, and roots made per thing count as one kind");
    }

    // Growth is against the first count; the most grown first, the same growth by name; what did not grow is not named.
    private static void KindGrowth()
    {
        var tally = new ObjectKinds();
        Check(tally.FirstMinute == -1, "kinds: no first count before the first minute");
        tally.Begin();
        tally.Add("A", 10);
        tally.Add("B", 5);
        tally.Add("Gone", 4);
        tally.End(2);
        Check(tally.FirstMinute == 2 && tally.Rank() == 0, "kinds: the first count is the measure, and nothing has grown against itself");
        tally.Begin();
        tally.Add("A", 10);
        tally.Add("B", 20);
        tally.Add("B", 5);
        tally.Add("D", 1);
        tally.Add("C", 3);
        tally.Add("E", 3);
        tally.End(9);
        Check(tally.FirstMinute == 2, "kinds: a later count does not move the measure");
        int found = tally.Rank();
        tally.Top(0, out string first, out int firstGrown, out int firstNow);
        tally.Top(1, out string second, out int secondGrown, out _);
        tally.Top(2, out string third, out _, out _);
        Check(found == 3 && first == "B" && firstGrown == 20 && firstNow == 25 && second == "C" && secondGrown == 3 && third == "E",
            "kinds: the three that grew the most, most first, the same growth by name, and one that grew less left out");
        tally.Begin();
        tally.Add("A", 9);
        tally.End(10);
        Check(tally.Rank() == 0, "kinds: a kind that shrank or went is not named as grown");
    }

    // However many names a scene has, the tally keeps a bounded number of kinds.
    private static void KindCap()
    {
        var tally = new ObjectKinds();
        tally.Begin();
        for (int i = 0; i < ObjectKinds.MostKinds + 300; i++)
        {
            tally.Add("kind" + i, 1);
        }
        tally.End(1);
        tally.Begin();
        for (int i = 0; i < ObjectKinds.MostKinds + 300; i++)
        {
            tally.Add("kind" + i, 2);
        }
        tally.End(2);
        int found = tally.Rank();
        tally.Top(0, out string top, out int grown, out _);
        Check(tally.Count == ObjectKinds.MostKinds + 1 && found == ObjectKinds.Shown && top == ObjectKinds.Others && grown == 300,
            "kinds: past the most kept, the rest are counted together, and their growth is still seen");
    }

    // The record counts the objects in its line a minute, through lists it keeps, and the scene kept across loads is read once.
    private static void KindSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string watch = Source(scripts, @"Diagnostics\SessionWatch.cs");
        string minute = Body(watch, "private void Minute(");
        Check(minute.Contains("CountObjects(ref objects, ref shown, ref roots, minute)") && minute.Contains("Grew(minute)") && !Body(watch, "private void OnUpdate()").Contains("CountObjects"),
            "kinds: the objects are counted, and what grew written, once a minute and never in a frame");
        string scene = Body(watch, "private void CountScene(");
        Check(scene.Contains("GetComponentsInChildren(true, _under)") && scene.Contains("_kinds.Add(ObjectKinds.Key(") && scene.Contains("_holders.Add(ObjectKinds.Key("),
            "kinds: the walk fills a list it keeps and counts every holder and kind");
        Check(Body(watch, "private Scene KeptScene()").Contains("if (!_keptRead)"), "kinds: the scene kept across loads is found once");
        string update = Body(watch, "private void OnUpdate()"), grew = Body(watch, "private void Grew(");
        Check(update.Contains("if (collections != collectionsBefore)") && update.Contains("_afterCollection = heap;") && grew.Contains("after its last collection") && grew.Contains("Meta.Dev.Lines"),
            "kinds: the line a minute says what the heap keeps after a collection (garbage aside) and how many lines the mod logged, which the game keeps all run");
    }
}
