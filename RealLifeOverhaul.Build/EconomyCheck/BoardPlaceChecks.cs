// A help-wanted board that stays where it was, a meeting place a player can search for, and a parked bicycle that nobody is
// stood on or thrown down by (Jobs/JobBoards.cs, Social/SocialPlaces.cs, People/StreetSpots.cs, Ragdoll/CarStrikes.cs).
internal static partial class Program
{
    private static void BoardPlaces()
    {
        string boards = ModSource(@"Jobs\JobBoards.cs");
        string search = MemberText(boards, "private void Search()");
        Check(search.Contains("_candidates") && search.Contains("Spares") && search.Contains("Tries") && !search.Contains("|| !Place(building, kind)"),
            "board places: which building stands for a kind is settled by the city's list, and a clear pavement on one second is not what decides it");
        Check(search.Contains("++_tries >= Tries") && search.Contains("_spare++") && search.Contains("if (Place(list[_spare], kind))"),
            "board places: the first building of a kind is asked again before the next of its kind is used");
        Check(search.Contains("string kind = _order[_head];") && boards.Contains("private readonly List<string> _order"),
            "board places: boards are put up in the order the city's list first shows their kinds, so an employer's board index is the same every load");

        string rules = ModSource(@"Social\SocialRules.cs");
        Check(rules.Contains("public static string PlaceWords(string business, string address, string district)"), "meeting place words: one rule tells a place");
        Check(RealLifeOverhaul.SocialRules.PlaceWords("Premium Produce", "17 Hudson Street", "Chelsea") == "Premium Produce (17 Hudson Street, Chelsea)",
            "meeting place words: a business is told by name and then its address with the district, as the game composes a business's name");
        Check(RealLifeOverhaul.SocialRules.PlaceWords(null, "17 Hudson Street", "Chelsea") == "17 Hudson Street, Chelsea"
              && RealLifeOverhaul.SocialRules.PlaceWords("", "17 Hudson Street", null) == "17 Hudson Street",
            "meeting place words: a building with no business is its address, with the district when it has one");
        Check(RealLifeOverhaul.SocialRules.PlaceWords("17 Hudson Street", "17 Hudson Street", null) == "17 Hudson Street",
            "meeting place words: an address that is also the name is not said twice");
        string places = ModSource(@"Social\SocialPlaces.cs");
        string make = MemberText(places, "internal static bool Make(CityBuildingController building, out Venue venue)");
        string family = ModSource(@"Family\FamilyPlaces.cs");
        Check(make.Contains("venue.Name = Say(building, registration != null ? registration.BusinessName : null);") && family.Contains("name = SocialPlaces.Say(building, name);"),
            "meeting place words: the place of a social outing and of a family errand are both told by their address");
        Check(!places.Contains(": building.building.Address.ToFormattedString();"), "meeting place words: no venue is named by a business's name alone");

        string spots = MemberText(ModSource(@"People\StreetSpots.cs"), "public static bool OutOfVehicles(ref Vector3 spot)");
        Check(spots.Contains("_vehicleMask | _itemMask") && spots.Contains("GetComponentInParent<ScooterController>()"),
            "parked bicycle: a place to stand is moved off a parked scooter, bicycle or motorbike, which the vehicle layers do not hold");
        string strikes = ModSource(@"Ragdoll\CarStrikes.cs");
        string sweep = MemberText(strikes, "private void Sweep(");
        Check(sweep.Contains("player && _twoWheeler && now - _boardedAt < MountGrace") && strikes.Contains("_boardedAt = Time.time;"),
            "parked bicycle: nobody is thrown down in the seconds after the player gets on a two-wheeler");
    }
}
