using System;
using System.IO;
using RealLifeOverhaul;

// Checks for what a long session keeps: the session record's count of the mod's objects by holder and by kind
// (Diagnostics/ObjectKinds.cs, compiled as it ships), which names what grows in the line a minute, and over the sources
// that the record counts them once a minute and never in a frame.
internal static partial class Program
{
    private static void Memory()
    {
        KindNames();
        KindGrowth();
        KindCap();
        KindSources();
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
    }
}
