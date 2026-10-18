using System;
using System.Collections.Generic;
using System.IO;
using IoPath = System.IO.Path;
using RealLifeOverhaul;

// Faults found in Scripts/Home: the roommate's talk test read the player's character without testing it, inside the tick
// whose fault switches roommates off; a door to social life (RoommateBridges.Friend) was filled but never read; the rain
// reached a player up in a hotel room; and a night there counted as hours awake.
internal static partial class Program
{
    private static void HomeFixes()
    {
        string flat = ModSource(@"Home\Roommates\RoommateFlat.cs");
        string reach = MemberText(flat, "private bool Reachable(Vector3 player)");
        int tested = reach.IndexOf("controller.Character != null", StringComparison.Ordinal);
        int read = reach.IndexOf("controller.Character.transform", StringComparison.Ordinal);
        Check(tested > 0 && read > tested, "home fix: the roommate's talk test tests the player's character before it reads where they face");

        string bridges = ModSource(@"Home\Roommates\RoommateSettings.cs");
        string links = ModSource(@"Wiring\RoommateLinks.cs");
        Check(bridges.Contains("public static Func<List<SocialPerson>, int> Friends;") && !bridges.Contains("Func<int, SocialPerson> Friend;")
              && !bridges.Contains(" Friend = null;") && !links.Contains("RoommateBridges.Friend = "),
            "home fix: RoommateBridges has no Friend door, filled by the wiring and read by nobody");
        string reader = null;
        foreach (string path in Directory.GetFiles(IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")), "*.cs", SearchOption.AllDirectories))
        {
            if (File.ReadAllText(path).Contains("RoommateBridges.Friend("))
            {
                reader = IoPath.GetFileName(path);
            }
        }
        Check(reader == null, "home fix: nothing calls a RoommateBridges.Friend door" + (reader != null ? " (" + reader + ")" : string.Empty));
        Check(FixLineEverywhere("roommate no longer goes quiet", "colega de casa não fica mais calado", "室友不会再因此", "Mitbewohner verstummt nicht mehr",
                                "colocataire ne se tait plus", "compañero de piso ya no se queda callado", "сосед по квартире больше не замолкает",
                                "een huisgenoot wordt niet meer stil", "ev arkadaşı artık oturumun geri kalanında sessiz kalmıyor"),
            "home fix: What's new has the roommate's line, with its fix mark, in some row of every language");

        // Up in a hotel room the player stands at the hotel's door in the street, so the rain reached them there.
        string director = ModSource(@"Home\HomeDirector.cs");
        Check(director.Contains("bool sheltered = HomeBridges.Sheltered != null && HomeBridges.Sheltered();")
              && director.Contains("bool onFootOutside = !inside && !sheltered &&"),
            "home fix: the rain and the cold it brings miss a player the bridges say is sheltered");
        string life = ModSource(@"Wiring\LifeLinks.cs");
        Check(life.Contains("HomeBridges.Sheltered = () => TravelState.InRoom;") && life.Contains("HomeBridges.Sheltered = null;"),
            "home fix: up in a hotel room counts as sheltered, and the wiring takes that back on disconnect");
        Check(FixLineEverywhere("hotel room no longer gives you a cold", "quarto de hotel não faz mais você pegar um resfriado", "酒店房间里休息或睡觉不会再让你感冒",
                                "Hotelzimmer ruht oder schläft, erkältet sich nicht mehr", "chambre d'hôtel ne donne plus de rhume",
                                "habitación de hotel ya no te resfría", "номере отеля больше не вызывает простуду"),
            "home fix: What's new has the hotel room's rain line, with its fix mark, in some row of every language");

        // A night in a hotel room is the room's own time skip, never the game's sleep, so it counted as hours awake.
        Check(director.Contains("int awayBed = HomeBridges.SleepingAway != null ? HomeBridges.SleepingAway() : -1;")
              && director.Contains("doing = Doing.Sleeping;") && director.Contains("Sleep(doing, minutes, atHome, awayBed);")
              && director.Contains("_bedQuality = awayBed;"),
            "home fix: sleep away from the game's beds counts as sleep, in the bed the bridges name");
        string hotel = ModSource(@"Travel\HotelStay.Sleep.cs");
        int set = hotel.IndexOf("SleepingBed = BedQuality(room);", StringComparison.Ordinal);
        int skip = hotel.IndexOf("yield return _director.Skip(goal, night ?", StringComparison.Ordinal);
        Check(set > 0 && skip > set && MemberText(hotel, "public static void EndSleepEffects()").Contains("SleepingBed = -1;")
              && life.Contains("HomeBridges.SleepingAway = () => HotelStay.SleepingBed;") && life.Contains("HomeBridges.SleepingAway = null;"),
            "home fix: a hotel room names its bed through the sleep's time skip and takes it back on every way out");
        Check(FixLineEverywhere("hotel room now counts as sleep", "quarto de hotel agora conta como sono", "在酒店房间过夜或小睡现在算作睡眠",
                                "Hotelzimmer zählt jetzt als Schlaf", "chambre d'hôtel compte maintenant comme du sommeil",
                                "habitación de hotel ahora cuenta como sueño", "номере отеля теперь считаются сном"),
            "home fix: What's new has the hotel night's sleep line, with its fix mark, in some row of every language");
    }

    // A fix's line, by a piece of it in each language (in the order of ModLanguages.All: en, pt, zh-cn, de, fr, es, ru, nl, tr),
    // in some What's new row of each with the language's fix mark: the next row until a release numbers it into its own.
    private static bool FixLineEverywhere(params string[] pieces)
    {
        string[] order = ModLanguages.All;
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        bool all = codes.Length == order.Length && pieces.Length == order.Length;
        for (int l = 0; all && l < codes.Length; l++)
        {
            int at = Array.IndexOf(order, codes[l]);
            bool found = false;
            foreach (KeyValuePair<string, string> row in langs[l])
            {
                int piece = row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal)
                    ? row.Value.IndexOf(pieces[at], StringComparison.Ordinal) : -1;
                found |= piece > 0 && row.Value.LastIndexOf(FixMarks[codes[l]], piece, StringComparison.Ordinal) >= 0;
            }
            all &= at >= 0 && found;
        }
        return all;
    }
}
