using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The fronts of the mod's buildings (Core/FrontSignRules.cs, with Core/SpecialNameBook.cs and Tech/Sites/RoomPlans.cs):
// which of its buildings lose the game's "For rent" sign and what each shows instead, a tech company's sign in its own
// colours and an institution's in its board's, the game's sign type taken so that it lights up after dark, which of them
// the city map's rows of units to let and buildings for sale leave out, and an unclaimed building left as the game has it.
// And, read as text, that nothing of it is written into the save (Core/FrontSigns.cs and Core/ClaimedMap.cs).
internal static partial class Program
{
    private static void FrontSigns()
    {
        // ---------------------------------------------------------------- what a front shows
        Check(FrontSignRules.Look(SpecialKind.Other, true, true, false, true) == FrontLook.Named,
            "fronts: a tech company's headquarters, to let and refused, carries the company's own sign");
        Check(FrontSignRules.Look(SpecialKind.Other, false, true, false, true) == FrontLook.Game
              && FrontSignRules.Look(SpecialKind.Civic, false, true, false, true) == FrontLook.Game,
            "fronts: a building that is not refused (its feature off, a player's) has the game's own signs");
        Check(FrontSignRules.Look(SpecialKind.Other, true, false, false, true) == FrontLook.Game && FrontSignRules.Look(SpecialKind.Court, true, false, true, true) == FrontLook.Game,
            "fronts: a building the game does not hold as to let (a business in it, the tax office) keeps the game's own sign");
        Check(FrontSignRules.Look(SpecialKind.Civic, true, true, true, true) == FrontLook.Off && FrontSignRules.Look(SpecialKind.Court, true, true, true, true) == FrontLook.Off,
            "fronts: City Utilities and the courthouse with their boards up: the game's signs off, the board is the one sign");
        Check(FrontSignRules.Look(SpecialKind.Campus, true, true, true, true) == FrontLook.Named,
            "fronts: the university's permanent building carries its lit name on the game's signs with its board up at the door too, as the tech towers do");
        Check(FrontSignRules.Look(SpecialKind.Civic, true, true, false, true) == FrontLook.Named && FrontSignRules.Look(SpecialKind.Court, true, true, false, true) == FrontLook.Named
              && FrontSignRules.Look(SpecialKind.Campus, true, true, false, true) == FrontLook.Named,
            "fronts: ...with no room for a board, the game's sign carries the institution's name instead: one sign each, never none");
        Check(FrontSignRules.Look(SpecialKind.Police, true, true, false, true) == FrontLook.Off && FrontSignRules.Look(SpecialKind.Hotel, true, true, false, true) == FrontLook.Off,
            "fronts: a police station and a hotel are dressed by their own features: the game's signs only off");
        Check(FrontSignRules.Look(SpecialKind.Other, true, true, false, false) == FrontLook.Off && FrontSignRules.Look(SpecialKind.Civic, true, true, false, false) == FrontLook.Off,
            "fronts: with the special buildings' names off no name is put up, and still no \"For rent\" on a refused building");
        Check(FrontSignRules.Look(SpecialKind.Kiosk, true, true, false, true) == FrontLook.Game && FrontSignRules.Look(SpecialKind.PlayerBusiness, true, true, false, true) == FrontLook.Game
              && FrontSignRules.Look(SpecialKind.RivalBusiness, true, true, false, true) == FrontLook.Game,
            "fronts: a kind the book never takes is never touched");

        // ---------------------------------------------------------------- refused, as the book has it
        var book = new SpecialNameBook();
        bool hqOn = true;
        Check(book.Set(FrontSignRules.HqPrefix + "1", "Fifth Avenue", 52, SpecialKind.Other, "Skyvault Systems", "reallife_hq_card_type", () => hqOn, literal: true),
            "fronts: a headquarters goes in the book");
        SpecialEntry hq = book.Of(FrontSignRules.HqPrefix + "1");
        Check(SpecialNameBook.RefusesEntry(hq, false, false) && !SpecialNameBook.RefusesEntry(hq, true, false) && !SpecialNameBook.RefusesEntry(hq, false, true)
              && !SpecialNameBook.RefusesEntry(null, false, false),
            "fronts: refused while nobody rents or owns it; a building any player rents or owns (another player's in a session too) is the player's");
        Check(SpecialNameBook.RefusesEntry(hq, false, false) == book.Refuses("Fifth Avenue", 52, false, false)
              && SpecialNameBook.RefusesEntry(hq, true, false) == book.Refuses("Fifth Avenue", 52, true, false),
            "fronts: an entry in hand is refused exactly as its address is");
        hqOn = false;
        Check(!SpecialNameBook.RefusesEntry(hq, false, false)
              && FrontSignRules.Look(hq.Kind, SpecialNameBook.RefusesEntry(hq, false, false), true, hq.Board, true) == FrontLook.Game,
            "fronts: with the feature switched off the building has the game's own signs, \"For rent\" and all");
        hqOn = true;
        int stamp = book.Stamp;
        Check(book.Boarded(FrontSignRules.HqPrefix + "1", true) && hq.Board && book.Stamp != stamp && !book.Boarded("nobody", true),
            "fronts: a feature marks the board it stood, and the book's stamp says so; an id it does not hold is refused");
        book.Boarded(FrontSignRules.HqPrefix + "1", false);
        var all = new List<SpecialEntry>();
        book.Set("utilities_office", "Seventh Street", 17, SpecialKind.Civic, "reallife_util_desk_title");
        book.All(all);
        Check(all.Count == 2, "fronts: every building of the book is looked after, shown or not");
        Check(book.Set(FrontSignRules.HqPrefix + "1", "Fifth Avenue", 15, SpecialKind.Other, "Skyvault Systems", null, () => hqOn, literal: true),
            "fronts: a claim that moves (the player had the building first) is set again under its id");
        book.All(all);
        bool old = false, moved = false;
        for (int i = 0; i < all.Count; i++)
        {
            old |= all[i].Number == 52;
            moved |= all[i].Number == 15 && all[i].Id == FrontSignRules.HqPrefix + "1";
        }
        Check(!old && moved && all.Count == 2, "fronts: ...its old building leaves the book (the game's own signs again) and the new one is in it");

        // ---------------------------------------------------------------- the city map's rows
        Check(FrontSignRules.PutOut(true, false, false, true, false) && FrontSignRules.PutOut(true, false, false, false, true),
            "fronts: a refused building the game lit as to let, or as for sale, is put out of those rows");
        Check(!FrontSignRules.PutOut(false, false, false, true, false) && !FrontSignRules.PutOut(false, false, false, false, true),
            "fronts: an unclaimed building to let or for sale is left in the game's rows as it was");
        Check(!FrontSignRules.PutOut(true, true, false, true, false) && !FrontSignRules.PutOut(true, false, true, true, false) && !FrontSignRules.PutOut(true, false, false, false, false),
            "fronts: the player's own, a business in it, or a building neither to let nor for sale stays lit as the game lit it");

        // ---------------------------------------------------------------- the tech companies' signs
        Check(FrontSignRules.HqIndex("techhq_0") == 0 && FrontSignRules.HqIndex("techhq_11") == 11 && FrontSignRules.HqIndex("techhq_") == -1
              && FrontSignRules.HqIndex("techhq_-1") == -1 && FrontSignRules.HqIndex("techhqx") == -1 && FrontSignRules.HqIndex("police_1") == -1 && FrontSignRules.HqIndex(null) == -1,
            "fronts: a headquarters is known by its id, nothing else is");
        Check(FrontSignRules.HqShapeCount == HqColours.Count, "fronts: every tech company has a logo shape of its own");
        Check(FrontSignRules.TowerIndex("techhq_tower_0") == 0 && FrontSignRules.TowerIndex("techhq_tower_2") == 2 && FrontSignRules.HqIndex("techhq_tower_1") == -1
              && FrontSignRules.TowerIndex("techhq_3") == -1 && FrontSignRules.TowerIndex("techhq_tower_") == -1 && FrontSignRules.TowerIndex(null) == -1,
            "fronts: a tower of the tech companies is known by its id, and is no one company's headquarters");
        FrontStyle tower0 = FrontSignRules.Style(SpecialKind.Other, "techhq_tower_0"), tower1 = FrontSignRules.Style(SpecialKind.Other, "techhq_tower_1");
        Check(tower0.Shape == "headquarter1" && Near(tower0.BackR, 0.14f) && Near(tower0.MarkR, 0.86f) && tower0.Shape == tower1.Shape && tower0.Preferred == 0 && tower1.Preferred == 1
              && FrontSignRules.Look(SpecialKind.Other, true, true, false, true) == FrontLook.Named,
            "fronts: a tower's sign carries its own name in slate and brass, every tower alike but for its sign type in turn");
        var shapes = new HashSet<string>(StringComparer.Ordinal);
        bool colours = true, distinct = true;
        for (int k = 0; k < HqColours.Count; k++)
        {
            FrontStyle s = FrontSignRules.Style(SpecialKind.Other, FrontSignRules.HqPrefix + k);
            HqColours.Of(k, false, out float r, out float g, out float b);
            HqColours.Of(k, true, out float r2, out float g2, out float b2);
            colours &= Near(s.BackR, r) && Near(s.BackG, g) && Near(s.BackB, b) && Near(s.NeonR, r) && Near(s.NeonG, g) && Near(s.NeonB, b)
                       && Near(s.TextR, r2) && Near(s.TextG, g2) && Near(s.TextB, b2) && Near(s.MarkR, r2) && Near(s.MarkG, g2) && Near(s.MarkB, b2)
                       && s.Preferred == k && s.Font == 0;
            distinct &= shapes.Add(s.Shape);
        }
        Check(colours, "fronts: a headquarters' sign is in its company's colours (Tech/Sites/RoomPlans: the main colour as the ground and the neon, the second for the name and the logo)");
        Check(distinct, "fronts: no two tech companies share a logo shape");
        FrontStyle civic = FrontSignRules.Style(SpecialKind.Civic, "utilities_office");
        FrontStyle court = FrontSignRules.Style(SpecialKind.Court, "courthouse");
        FrontStyle campus = FrontSignRules.Style(SpecialKind.Campus, "campus");
        Check(Near(civic.BackR, 0.06f) && Near(civic.BackG, 0.24f) && Near(court.BackB, 0.25f) && Near(campus.BackR, 0.36f) && civic.Shape != court.Shape && court.Shape != campus.Shape,
            "fronts: an institution's sign is in its board's colours, each with its own shape");
        shapes.Add(civic.Shape);
        shapes.Add(court.Shape);
        shapes.Add(campus.Shape);
        shapes.Add(FrontSignRules.Style(SpecialKind.Other, "other_role").Shape);
        string game = Path.Combine(Environment.GetEnvironmentVariable("BA_MANAGED")
            ?? @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed", @"..\StreamingAssets\LogoShapes");
        if (Directory.Exists(game))
        {
            var missing = new List<string>();
            foreach (string shape in shapes)
            {
                if (!File.Exists(Path.Combine(game, shape + ".png")))
                {
                    missing.Add(shape);
                }
            }
            Check(missing.Count == 0, "fronts: every logo shape is one of the game's own (StreamingAssets/LogoShapes)" + (missing.Count > 0 ? ": not there: " + string.Join(", ", missing) : string.Empty));
        }
        FrontStyle once = FrontSignRules.Style(SpecialKind.Other, "techhq_4"), again = FrontSignRules.Style(SpecialKind.Other, "techhq_4");
        Check(once.Shape == again.Shape && FrontSignRules.Mark("Pixel Harbour Games", once) == FrontSignRules.Mark("Pixel Harbour Games", again),
            "fronts: the same company gives the same sign on every machine and in every session");
        Check(FrontSignRules.Mark("Pixel Harbour Games", once) != FrontSignRules.Mark("Pixel Harbour Gamez", once)
              && FrontSignRules.Mark("Pixel Harbour Games", once) != FrontSignRules.Mark("Pixel Harbour Games", FrontSignRules.Style(SpecialKind.Other, "techhq_5")),
            "fronts: another name or another style makes its logo anew");

        // ---------------------------------------------------------------- the game's sign type
        int[] lit = { 2, 5, 9 };
        Check(FrontSignRules.SignType(0, lit, 3, 0) == 2 && FrontSignRules.SignType(1, lit, 3, 0) == 5 && FrontSignRules.SignType(4, lit, 3, 0) == 5 && FrontSignRules.SignType(-1, lit, 3, 0) == 9,
            "fronts: a sign takes one of the game's sign types with lights, in turn by company, so it lights up after dark");
        Check(FrontSignRules.SignType(3, lit, 0, 1) == 1 && FrontSignRules.SignType(3, null, 5, -1) == 0 && FrontSignRules.SignType(3, lit, 9, 0) == lit[0],
            "fronts: a sign with no lit type takes the first it has, and one that has none the game's Type1, as its \"For rent\" sign does");

        // ---------------------------------------------------------------- nothing into the save
        string core = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Core"));
        string fronts = File.Exists(Path.Combine(core, "FrontSigns.cs")) ? Code(File.ReadAllText(Path.Combine(core, "FrontSigns.cs"))) : null;
        string map = File.Exists(Path.Combine(core, "ClaimedMap.cs")) ? Code(File.ReadAllText(Path.Combine(core, "ClaimedMap.cs"))) : null;
        Check(fronts != null && map != null, "fronts: the sources are where they were");
        if (fronts == null || map == null)
        {
            return;
        }
        Check(!Regex.IsMatch(fronts + map, @"\b(SaveGameManager|BuildingRegistrations|SaveHook|SaveGamePathHelper|GetPlayerBusinessLogoPath|PlayerPrefs)\b"),
            "fronts: nothing of the save is read or written, and nothing goes into the save's folder or PlayerPrefs");
        bool onlyStand = true;
        foreach (Match m in Regex.Matches(fronts + map, @"(\w+)\s*\.\s*(signAppearanceSettings|logoSettings|BusinessName|AvailableForRent|RentedByPlayer|BuildingOwnedByPlayer|businessTypeName|Layout|StreetName|StreetNumber)\s*=(?!=)"))
        {
            onlyStand &= m.Groups[1].Value == "_stand";
        }
        Check(onlyStand, "fronts: no registration of the game's is ever written; only the mod's own, never in the save's list, is set (to configure the game's sign from)");
        Check(fronts.Contains("new BuildingRegistration()") && !Regex.IsMatch(fronts, @"\.Add\(\s*_stand"), "fronts: the mod's own registration is made, and never added to any list");
        Check(Regex.IsMatch(fronts, @"BusinessLogoGenerator\.Create\(\s*logo\.Name\s*,\s*settings\s*,\s*logo\.Path\s*,\s*false\s*,")
              && fronts.Contains("Application.temporaryCachePath"),
            "fronts: the logo is made as no player's business, into the mod's folder under the game's temporary folder");
        Check(!Regex.IsMatch(fronts + map, @"\.alwaysOpen\s*=(?!=)"), "fronts: no door of the game's is changed here");
    }
}
