using System;
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

        // ---------------------------------------------------------------- taken out
        stamp = book.Stamp;
        Check(book.Remove("campus") && !book.Remove("campus") && book.NameAt("College Road", 1, false, words) == null && book.Stamp != stamp,
            "special names: taken out, it has the game's name again");
        book.Clear();
        Check(book.Count == 0 && book.At("Fifth Avenue", 120) == null && book.NameAt("Fifth Avenue", 120, false, words) == null,
            "special names: emptied with the city");
    }
}
