using System;

// Faults found in Scripts/Travel: going up to a hotel room was refused while the game was paused, because the room asked
// the life events' test for a quiet moment, which counts a paused clock as busy.
internal static partial class Program
{
    private static void TravelFixes()
    {
        string life = ModSource(@"Life\LifeEvents.cs");
        Check(life.Contains("public static bool IsSafeForEvent(bool pausedIsFine = false)") && life.Contains("&& (pausedIsFine || !uis.gameSpeed.Paused);"),
            "travel fix: the life events' quiet moment counts a paused clock as busy unless the caller says a pause is fine");
        string director = ModSource(@"Travel\TravelDirector.cs");
        Check(director.Contains("public bool CanSeclude(out TestRefusal why, bool pausedIsFine = false)")
              && director.Contains("!LifeEvents.IsSafeForEvent(pausedIsFine)"),
            "travel fix: being held in place passes the caller's word on a pause to the quiet moment");
        string stay = ModSource(@"Travel\HotelStay.cs");
        Check(MemberText(stay, "public bool EnterRoom(out TestRefusal why)").Contains("_director.CanSeclude(out why, pausedIsFine: true)"),
            "travel fix: going up to the room is allowed while the game is paused");
        Check(FixLineEverywhere("go up to your hotel room while the game is paused", "subir para o seu quarto de hotel com o jogo em pausa",
                                "游戏暂停时也能上楼回酒店房间", "auch bei pausiertem Spiel auf dein Hotelzimmer", "monter dans votre chambre d'hôtel pendant que le jeu est en pause",
                                "subir a tu habitación de hotel con el juego en pausa", "подняться в номер отеля, пока игра на паузе",
                                "naar je hotelkamer gaan terwijl het spel is gepauzeerd", "oyun duraklatılmışken artık otel odanıza çıkabilirsiniz"),
            "travel fix: What's new has the paused room's line, with its fix mark, in some row of every language");
        TravelBoards();
    }

    // A player was sent to the Hamptons cottages and the map put them at a theatre: the board stood by the first of three anchors, which were a theatre,
    // a cinema and a furniture shop. Every anchor is a house of the slot's own district, and every other hotel has houses to be chosen from.
    private static void TravelBoards()
    {
        var city = new System.Collections.Generic.Dictionary<string, string[]>();
        foreach (string row in System.IO.File.ReadAllLines(PopulationFile))
        {
            string[] c = row.Split('|');
            if (c.Length > 5 && c[0] == "B")
            {
                city[c[1] + "|" + c[2]] = c;
            }
        }
        string wrong = string.Empty;
        int anchors = 0;
        foreach (RealLifeOverhaul.HotelSlot slot in RealLifeOverhaul.TravelCatalogue.Hotels)
        {
            if (slot.Anchors == null)
            {
                bool any = false;
                foreach (string[] c in city.Values)
                {
                    any |= c[3] == slot.Neighbourhood && c[4] == "ba:buildingtype_residential" && (slot.Sizes == null || slot.Sizes.IndexOf(c[5][c[5].Length - 1]) >= 0);
                }
                Check(any, "travel board: " + slot.Id + " has a house of its own district and sizes to be chosen from");
                continue;
            }
            foreach (string anchor in slot.Anchors)
            {
                anchors++;
                if (!city.TryGetValue(anchor, out string[] c) || c[3] != slot.Neighbourhood || c[4] != "ba:buildingtype_residential")
                {
                    wrong += " " + anchor + (c != null ? " (" + c[4] + ")" : " (not in the city)");
                }
            }
        }
        Check(anchors > 0 && wrong.Length == 0, "travel board: every anchor of the cottages' board is a house of their own district, never a shop or a theatre (wrong:" + wrong + ")");
        string hotels = ModSource(@"Travel\Hotels.cs");
        string anchor2 = MemberText(hotels, "private static CityBuildingController Anchor(");
        Check(anchor2.Contains("IsCottage(controller.building, slot)") && anchor2.Contains("!Mine(controller.building)")
              && MemberText(hotels, "public void Keep()").Contains("site.Slot.Anchors != null ? Anchor(city, site.Slot)"),
            "travel board: the board is put by a house nobody has bought, checked in the game's own data, and moves off one that is bought");
    }
}
