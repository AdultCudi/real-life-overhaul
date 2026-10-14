using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The book of the special buildings' names (Scripts/Core/SpecialNameBook): who may be named, by address, one building per
// id and one id per building, a feature's own option, the player's buildings, and names kept until the language changes.
internal static partial class Program
{
    private static void SpecialNames()
    {
        var book = new SpecialNameBook();
        int asked = 0;
        Func<string, string> words = key =>
        {
            asked++;
            return key == "reallife_util_desk_title" ? "City Utilities" : key == "reallife_justice_door_courthouse" ? "Courthouse" : key == "reallife_missing" ? string.Empty : "[" + key + "]";
        };

        // ---------------------------------------------------------------- who may be named
        Check(SpecialNameBook.Accepts(SpecialKind.Civic) && SpecialNameBook.Accepts(SpecialKind.Court) && SpecialNameBook.Accepts(SpecialKind.Police)
              && SpecialNameBook.Accepts(SpecialKind.Hotel) && SpecialNameBook.Accepts(SpecialKind.Campus) && SpecialNameBook.Accepts(SpecialKind.Other),
            "special names: the city's offices, the courthouse, police stations, hotels, a campus and any other role are named");
        Check(!SpecialNameBook.Accepts(SpecialKind.Kiosk) && !SpecialNameBook.Accepts(SpecialKind.StreetVendor)
              && !SpecialNameBook.Accepts(SpecialKind.PlayerBusiness) && !SpecialNameBook.Accepts(SpecialKind.RivalBusiness),
            "special names: kiosks, street vendors and the businesses of the player or a rival keep their own names");
        Check(!book.Set("kiosk_1", "Main Street", 4, SpecialKind.Kiosk, "reallife_map_kiosk") && !book.Set("shop", "Main Street", 6, SpecialKind.RivalBusiness, "reallife_x")
              && book.Count == 0 && book.At("Main Street", 4) == null,
            "special names: an excluded kind is refused and never found");
        Check(!book.Set(null, "Main Street", 4, SpecialKind.Civic, "k") && !book.Set("a", "", 4, SpecialKind.Civic, "k") && !book.Set("a", "Main Street", 4, SpecialKind.Civic, "")
              && book.Count == 0, "special names: no id, street or name: refused");

        // ---------------------------------------------------------------- by address
        int stamp = book.Stamp;
        bool utilitiesOn = true;
        Check(book.Set("utilities_office", "Fifth Avenue", 120, SpecialKind.Civic, "reallife_util_desk_title", "reallife_util_card_type", () => utilitiesOn)
              && book.Count == 1 && book.Stamp != stamp, "special names: a feature puts its building in with one line");
        Check(book.NameAt("Fifth Avenue", 120, false, words) == "City Utilities" && book.NameAt("Fifth Avenue", 121, false, words) == null
              && book.NameAt("Fifth Avenue ", 120, false, words) == null && book.NameAt(null, 120, false, words) == null,
            "special names: found by its street and number, nothing else is");
        Check(book.At("Fifth Avenue", 120).TypeKey == "reallife_util_card_type" && book.Of("utilities_office").Kind == SpecialKind.Civic,
            "special names: what it is goes with it, for the card's second line");
        Check(SpecialNameBook.Key("A", 12) != SpecialNameBook.Key("A1", 2) && SpecialNameBook.Key("A", 12) == SpecialNameBook.Key("A", 12),
            "special names: a street and number never run into another's");

        // ---------------------------------------------------------------- the player's own, and the feature's option
        Check(book.NameAt("Fifth Avenue", 120, true, words) == null, "special names: a building the player rents or owns keeps the player's name");
        utilitiesOn = false;
        Check(book.NameAt("Fifth Avenue", 120, false, words) == null && book.At("Fifth Avenue", 120) == null && book.Of("utilities_office") != null,
            "special names: with its feature off the game's own name stands, and the building stays in the book");
        utilitiesOn = true;
        Check(book.NameAt("Fifth Avenue", 120, false, words) == "City Utilities", "special names: back with its feature");

        // ---------------------------------------------------------------- names kept until the language changes
        asked = 0;
        for (int i = 0; i < 50; i++)
        {
            book.NameAt("Fifth Avenue", 120, false, words);
        }
        Check(asked == 0, "special names: a name is looked up once and kept, not on every look");
        book.Forget();
        Check(book.NameAt("Fifth Avenue", 120, false, words) == "City Utilities" && asked == 1, "special names: looked up again once after the language changes");
        book.Set("missing", "Elm Street", 2, SpecialKind.Other, "reallife_missing");
        Check(book.NameAt("Elm Street", 2, false, words) == "reallife_missing", "special names: a key with no words shows the key, never an empty name");
        book.Remove("missing");

        // ---------------------------------------------------------------- one building per id, one id per building
        Check(book.Set("police_0", "Third Street", 8, SpecialKind.Police, "reallife_law_precinct_midtown") && book.NameAt("Third Street", 8, false, words) == "[reallife_law_precinct_midtown]",
            "special names: a police station");
        Check(book.Set("police_0", "Third Street", 30, SpecialKind.Police, "reallife_law_precinct_midtown")
              && book.NameAt("Third Street", 8, false, words) == null && book.NameAt("Third Street", 30, false, words) != null && book.Count == 2,
            "special names: a station that moves is set again under its id, and its old building has the game's name again");
        Check(!book.Set("courthouse", "Fifth Avenue", 120, SpecialKind.Court, "reallife_justice_door_courthouse")
              && book.NameAt("Fifth Avenue", 120, false, words) == "City Utilities" && book.Of("courthouse") == null,
            "special names: a building another feature has named is refused, and the first keeps it");
        Check(book.Set("police_0", "Fifth Avenue", 120, SpecialKind.Police, "reallife_law_precinct_midtown") == false
              && book.Of("police_0") == null && book.NameAt("Third Street", 30, false, words) == null,
            "special names: an id refused a new building lets go of its old one");
        Check(book.Set("utilities_office", "Fifth Avenue", 120, SpecialKind.Civic, "reallife_util_desk_title") && book.Count == 1,
            "special names: an id may set its own building again");
        Check(book.Set("campus", "College Road", 1, SpecialKind.Campus, "reallife_campus_name", "reallife_campus_card_type") && book.Count == 2
              && book.NameAt("College Road", 1, false, words) == "[reallife_campus_name]",
            "special names: a later feature (the university) registers the same way, by address");

        // ---------------------------------------------------------------- a later feature's pick leaves the others' buildings alone
        // The owner's city: the largest office (17 7th Street) is the utilities office, so the university takes the next.
        string[] streets = { "7th Street", "Main Street", "Elm Street", "Bay Road", "Bay Road" };
        int[] numbers = { 17, 4, 9, 2, 1 };
        int[] sqm = { 102502, 80000, 80000, 120000, 500 };
        bool[] office = { true, true, true, false, true };
        Func<int, bool> fits = i => office[i];
        Func<int, int> size = i => sqm[i];
        Func<int, string> street = i => streets[i];
        Func<int, int> number = i => numbers[i];
        Check(SpecialNameBook.PickLargest(5, fits, size, street, number, null) == 0, "special names: the largest office building, as the university chose it in 0.7.0");
        Check(SpecialNameBook.PickLargest(5, fits, size, street, number, book.Holds) == 0 && book.Holds("Fifth Avenue", 120) && !book.Holds("7th Street", 17),
            "special names: with nobody holding it, it is still the one picked");
        book.Set("utilities_office", "7th Street", 17, SpecialKind.Civic, "reallife_util_desk_title");
        int campus = SpecialNameBook.PickLargest(5, fits, size, street, number, book.Holds);
        Check(campus == 2 && streets[campus] == "Elm Street",
            "special names: the university never takes the utilities office's building; of two the same size, the first by address (Elm before Main)");
        utilitiesOn = false;
        Check(book.Holds("7th Street", 17) && SpecialNameBook.PickLargest(5, fits, size, street, number, book.Holds) == 2,
            "special names: a building stays held while its feature's names are off, so the pick does not move with an option");
        utilitiesOn = true;
        book.Set("campus", "Elm Street", 9, SpecialKind.Campus, "reallife_study_campus_university");
        Check(SpecialNameBook.PickLargest(5, fits, size, street, number, book.Holds) == 1, "special names: each further feature skips every building already held");
        Check(SpecialNameBook.PickLargest(5, i => false, size, street, number, book.Holds) == -1 && SpecialNameBook.PickLargest(0, fits, size, street, number, null) == -1,
            "special names: nothing that fits, nothing picked");
        Check(SpecialNameBook.PickLargest(2, null, i => i == 0 ? -50 : 2000000, street, number, null) == 1, "special names: a nonsense floor area is brought within bounds");
        book.Set("campus", "College Road", 1, SpecialKind.Campus, "reallife_campus_name", "reallife_campus_card_type");
        book.Set("utilities_office", "Fifth Avenue", 120, SpecialKind.Civic, "reallife_util_desk_title");

        // ---------------------------------------------------------------- taken out
        stamp = book.Stamp;
        Check(book.Remove("campus") && !book.Remove("campus") && book.NameAt("College Road", 1, false, words) == null && book.Stamp != stamp,
            "special names: taken out, it has the game's name again");
        book.Clear();
        Check(book.Count == 0 && book.At("Fifth Avenue", 120) == null && book.NameAt("Fifth Avenue", 120, false, words) == null,
            "special names: emptied with the city");

        // ---------------------------------------------------------------- a proper name (a tech company's headquarters)
        asked = 0;
        Check(book.Set("techhq_1", "Park Row", 9, SpecialKind.Other, "Skyvault Systems", "reallife_hq_card_type", null, literal: true)
              && book.NameAt("Park Row", 9, false, words) == "Skyvault Systems" && asked == 0,
            "special names: a company's proper name is shown as it is, never looked up as a key");
        book.Forget();
        Check(book.NameAt("Park Row", 9, false, words) == "Skyvault Systems" && asked == 0, "special names: ...and kept when the language changes");
        Check(book.NameAt("Park Row", 9, true, words) == null, "special names: a company's building the player rents keeps the game's name");
        Check(book.HolderOf("Park Row", 9) == "techhq_1" && book.HolderOf("Park Row", 10) == null && book.HolderOf(null, 9) == null,
            "special names: who holds a building, so that another feature passes over it");
        Check(!book.Set("police_3", "Park Row", 9, SpecialKind.Police, "reallife_law_precinct_3") && book.HolderOf("Park Row", 9) == "techhq_1",
            "special names: a building a tech company holds is refused to another feature");
        book.Clear();
        OffTheMarket();
        OfficeSupply();
    }

    // The mod's buildings off the market (Core/ClaimedOffers): which are refused, and that no feature's building is ever
    // one the player rents or owns, chosen at the load or after the player has taken one.
    private static void OffTheMarket()
    {
        var book = new SpecialNameBook();
        Func<string, string> words = key => key == "reallife_util_desk_title" ? "City Utilities" : "[" + key + "]";

        // ---------------------------------------------------------------- which buildings are refused
        Check(SpecialNameBook.MaySit(false, false) && !SpecialNameBook.MaySit(true, false) && !SpecialNameBook.MaySit(false, true) && !SpecialNameBook.MaySit(true, true),
            "off the market: a feature takes only a building nobody of the players rents or owns");
        bool hqOn = true;
        book.Set("techhq_10", "Wall Street", 40, SpecialKind.Other, "Signalhill Telecom", "reallife_hq_card_type", () => hqOn, literal: true);
        book.Set("utilities_office", "Fifth Avenue", 120, SpecialKind.Civic, "reallife_util_desk_title", "reallife_util_card_type");
        Check(book.Refuses("Wall Street", 40, false, false) && book.Refuses("Fifth Avenue", 120, false, false),
            "off the market: a building a feature holds is not to let, for sale or to take over");
        Check(book.NameAt("Wall Street", 40, false, words) == "Signalhill Telecom" && book.NameAt("Fifth Avenue", 120, false, words) == "City Utilities",
            "off the market: the line says whose it is (Taken by Signalhill Telecom)");
        Check(!book.Refuses("Wall Street", 40, true, false) && !book.Refuses("Wall Street", 40, false, true) && !book.Refuses("Wall Street", 40, true, true),
            "off the market: what the player already rents or owns is never refused (theirs to keep, run and give up)");
        Check(!book.Refuses("Wall Street", 41, false, false) && !book.Refuses("Wall Street ", 40, false, false) && !book.Refuses(null, 40, false, false),
            "off the market: every other building is to let as the game has it");
        hqOn = false;
        Check(!book.Refuses("Wall Street", 40, false, false) && book.Refuses("Fifth Avenue", 120, false, false),
            "off the market: with its feature off a building is on the market again, the others stay off");
        hqOn = true;
        var shown = new List<SpecialEntry> { null, null };
        book.Shown(shown);
        Check(shown.Count == 2 && !shown.Contains(null), "off the market: the buildings to keep out of the map's rows, the list cleared first");
        hqOn = false;
        book.Shown(shown);
        Check(shown.Count == 1 && shown[0].Id == "utilities_office", "off the market: ...only those whose feature is on");
        hqOn = true;
        // The refusal follows whatever building a feature holds at the moment, not a list: the university moves.
        book.Set("campus", "College Road", 1, SpecialKind.Campus, "reallife_study_campus_university");
        Check(book.Refuses("College Road", 1, false, false), "off the market: the university's building, wherever it is chosen");
        book.Set("campus", "Campus Walk", 7, SpecialKind.Campus, "reallife_study_campus_university");
        Check(!book.Refuses("College Road", 1, false, false) && book.Refuses("Campus Walk", 7, false, false),
            "off the market: when the university moves, its old building is to let again and the new one is not");
        book.Remove("campus");
        Check(!book.Refuses("Campus Walk", 7, false, false), "off the market: a building let go of is to let again");

        // ---------------------------------------------------------------- never on a building the player has
        // A city of office buildings, some rented or owned by the players; the features choose the largest that may serve,
        // one after another, as the utilities office and the university do (the courthouse, a precinct, a hotel and a
        // tech company's headquarters take the same test before they choose).
        var rnd = new Random(20261013);
        bool neverPlayers = true, alwaysSame = true, movesOff = true, othersStay = true;
        for (int city = 0; city < 300; city++)
        {
            int count = 4 + rnd.Next(40);
            var sqm = new int[count];
            var rented = new bool[count];
            var owned = new bool[count];
            for (int i = 0; i < count; i++)
            {
                sqm[i] = 200 + rnd.Next(5) * 400 + rnd.Next(3);   // many ties: broken by address
                rented[i] = rnd.Next(4) == 0;
                owned[i] = rnd.Next(9) == 0;
            }
            int[] first = Claims(count, sqm, rented, owned);
            int[] second = Claims(count, sqm, rented, owned);
            for (int f = 0; f < first.Length; f++)
            {
                alwaysSame &= first[f] == second[f];
                neverPlayers &= first[f] < 0 || SpecialNameBook.MaySit(rented[first[f]], owned[first[f]]);
            }
            // The player takes the utilities office's building (an old save, another player in a session): the office moves
            // off it to a building nobody has, and the university, which never stood there, stays where it is.
            if (first[0] >= 0)
            {
                int was = first[0];
                rented[was] = true;
                int[] after = Claims(count, sqm, rented, owned, first[1]);
                movesOff &= after[0] != was && (after[0] < 0 || SpecialNameBook.MaySit(rented[after[0]], owned[after[0]])) && (after[0] < 0 || after[0] != after[1]);
                othersStay &= after[1] == first[1];
            }
        }
        Check(neverPlayers, "off the market: in 300 cities no feature's building is one the player rents or owns");
        Check(alwaysSame, "off the market: the same city gives the same buildings every time (the same on every machine of a session)");
        Check(movesOff, "off the market: a feature whose building becomes the player's moves to one nobody has");
        Check(othersStay, "off the market: only the feature that lost its building moves");

        // ---------------------------------------------------------------- the cost of the look
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int refused = 0;
        for (int i = 0; i < 1000000; i++)
        {
            refused += book.Refuses(i % 3 == 0 ? "Wall Street" : "Broadway", 40, false, false) ? 1 : 0;
        }
        watch.Stop();
        Check(refused == 333334 && watch.ElapsedMilliseconds < 400,
            "off the market: a million looks at whether a building is refused in " + watch.ElapsedMilliseconds + " ms (under 400)");
    }

    // The university (index 1 of the answer) where it stood, if it may still stand there, then the utilities office (0),
    // the largest building that may serve and that no other holds, as Utilities/UtilitiesOffice and Careers/CampusBuilding
    // choose; a university with no building yet takes the largest after the office.
    private static int[] Claims(int count, int[] sqm, bool[] rented, bool[] owned, int keepCampus = -1)
    {
        var book = new SpecialNameBook();
        Func<int, string> street = i => "Street " + (i % 5);
        Func<int, int> number = i => i;
        Func<int, bool> fits = i => SpecialNameBook.MaySit(rented[i], owned[i]);
        Func<int, int> size = i => sqm[i];
        var answer = new int[2];
        bool kept = keepCampus >= 0 && fits(keepCampus);
        if (kept)
        {
            book.Set("campus", street(keepCampus), number(keepCampus), SpecialKind.Campus, "reallife_study_campus_university");
        }
        answer[0] = SpecialNameBook.PickLargest(count, fits, size, street, number, book.Holds);
        if (answer[0] >= 0)
        {
            book.Set("utilities_office", street(answer[0]), number(answer[0]), SpecialKind.Civic, "reallife_util_desk_title");
        }
        answer[1] = kept ? keepCampus : SpecialNameBook.PickLargest(count, fits, size, street, number, book.Holds);
        return answer;
    }
}
