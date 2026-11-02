using System;
using System.Collections.Generic;
using IoPath = System.IO.Path;

// The stutters of the owner's log of 2 Nov 2026: the first car got into after a load, a car's handling set up again while
// driving, and the street chess player and an onlooker set up (each over 2 ms in a frame). NavCheck (MomentWarmCheck.cs)
// and StandardCheck (SpreadChecks.cs) hold the code; here its What's new line, with each language's own fix mark, in some
// row of all eleven languages.
internal static partial class Program
{
    private static void FrameFixes()
    {
        Check(FixLineEverywhere("short stutters the first time you got into a car after loading a save",
                                "pequenas travadas na primeira vez que você entrava num carro",
                                "读档后第一次上车时",
                                "kurze Ruckler beim ersten Einsteigen in ein Auto",
                                "de courtes saccades la première fois que vous montiez dans une voiture",
                                "pequeños tirones la primera vez que subías a un coche",
                                "короткие подтормаживания, когда вы впервые садились в машину",
                                "korte haperingen de eerste keer dat je na het laden van een opgeslagen spel in een auto stapte",
                                "bir arabaya ilk kez bindiğinizde"),
            "frame fix: What's new has the line of the first car's and the street chess stutters, with its fix mark, in some row of every language");
        bool marked = true;
        foreach ((string code, string piece) in new[] { ("pl", "krótkie zacięcia przy pierwszym wsiadaniu do samochodu"), ("ko", "처음 차에 탈 때") })
        {
            Dictionary<string, string> own = LoadWords(IoPath.Combine(LocalesFolder(), code + ".json"));
            string row = own != null ? RowHolding(own, piece) : string.Empty;
            int at = row.IndexOf(piece, StringComparison.Ordinal);
            marked &= at > 0 && row.LastIndexOf(FixMarks[code], at, StringComparison.Ordinal) >= 0;
        }
        Check(marked, "frame fix: Polish and Korean have the line too, after their own fix marks");
    }
}
