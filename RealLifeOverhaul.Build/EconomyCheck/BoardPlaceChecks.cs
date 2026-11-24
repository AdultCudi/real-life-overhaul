// A help-wanted board that stays where it was, a meeting place a player can search for, and a parked bicycle that nobody is
// stood on or thrown down by (Jobs/JobBoards.cs, Social/SocialPlaces.cs, People/StreetSpots.cs, Ragdoll/CarStrikes.cs); a board that
// is put up late where the first asks failed, a worker let through a cinema's ticket gates, a patrol car never beside a crossing and
// a parked vehicle that is moved from a bus stop's bay told of every time (Jobs/JobGates.cs, Traffic/SceneParking.cs, Transport/BusZones.Cars.cs).
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

        BoardLateChecks();
        BoardGateChecks();
        BoardParkingChecks();
    }

    // A kind whose buildings all failed their asks is not given up for the session: it is asked again near the player, and the employer that
    // was left without an office takes it when it is up, without anyone's office moving.
    private static void BoardLateChecks()
    {
        string boards = ModSource(@"Jobs\JobBoards.cs");
        string search = MemberText(boards, "private void Search()");
        string tick = MemberText(boards, "public void Tick(Vector3 player)");
        string retry = MemberText(boards, "private void Retry(Vector3 player)");
        Check(search.Contains("_late.Add(kind);") && !search.Contains("no building of this kind can hold a board: it has none"),
            "late board: a kind that every one of its buildings failed is kept to be asked again, not dropped for the session");
        Check(tick.Contains("else if (_late.Count > 0 && _boards.Count < Most)") && tick.Contains("Retry(player);"),
            "late board: the ask is made from the one-second tick while the player is on foot, never per frame");
        Check(retry.Contains("now < _retryAt") && retry.Contains("RetryReach") && retry.Contains("Place(building, name)") && retry.Contains("_late.RemoveAt(kind);"),
            "late board: one ask in RetryEvery seconds, only at a building near the player, and a board that is up leaves the list");
        string director = ModSource(@"Jobs\CareerDirector.cs");
        Check(MemberText(director, "public void Slow(Vector3 player, bool onFoot, bool jobOn)").Contains("boards.Count > _mappedBoards") && director.Contains("CareerOfficeRules.Extend(premises, kinds, _mappedBoards, _depot);"),
            "late board: the employers are given a board put up late by the same rule at once, in the second it is up");

        string[][] premises = { new[] { "bank" }, new[] { "gasstation", "supermarket" }, new[] { "supermarket" }, new string[0] };
        string[] kinds = { "ba:businesstype_bank", "ba:businesstype_gasstation", "ba:businesstype_supermarket" };
        int[] depot = { 0, -1, -1, -1 };   // the second kind's board was not up at the first mapping, and the third employer had the leftover
        RealLifeOverhaul.CareerOfficeRules.Extend(premises, kinds, 1, depot);
        Check(depot[0] == 0 && depot[1] == 1 && depot[2] == 2 && depot[3] == -1,
            "late board: an employer with no office takes the board at its kind of building, whoever has one keeps it, and the building trades take none");
        int[] kept = { 0, 1, -1, -1 };
        RealLifeOverhaul.CareerOfficeRules.Extend(premises, new[] { "ba:businesstype_bank", "ba:businesstype_gasstation", "ba:businesstype_hospital" }, 2, kept);
        Check(kept[0] == 0 && kept[1] == 1 && kept[2] == 2 && kept[3] == -1,
            "late board: a board of a kind no employer asks for goes to an employer with premises that has no office, never to one with none");
        int[] none = { -1, -1, -1, -1 };
        RealLifeOverhaul.CareerOfficeRules.Extend(premises, kinds, 3, none);
        Check(none[0] == -1 && none[1] == -1 && none[2] == -1 && none[3] == -1, "late board: no board past the first of the new ones means nobody moves");
    }

    // The ticket gates of a cinema or theatre are opened for a worker on the client's floor and given back to the game when the shift ends.
    private static void BoardGateChecks()
    {
        Check(RealLifeOverhaul.JobGateRules.Decide(true, true, false) == RealLifeOverhaul.JobGateRules.Act.Open
              && RealLifeOverhaul.JobGateRules.Decide(true, true, true) == RealLifeOverhaul.JobGateRules.Act.Open,
            "ticket gates: a shut gate is opened whenever the shift works inside, also after the game switched it on again when the hands changed");
        Check(RealLifeOverhaul.JobGateRules.Decide(true, false, true) == RealLifeOverhaul.JobGateRules.Act.Leave
              && RealLifeOverhaul.JobGateRules.Decide(true, false, false) == RealLifeOverhaul.JobGateRules.Act.Leave,
            "ticket gates: a gate already open is left alone");
        Check(RealLifeOverhaul.JobGateRules.Decide(false, true, true) == RealLifeOverhaul.JobGateRules.Act.GiveBack
              && RealLifeOverhaul.JobGateRules.Decide(false, false, true) == RealLifeOverhaul.JobGateRules.Act.GiveBack
              && RealLifeOverhaul.JobGateRules.Decide(false, true, false) == RealLifeOverhaul.JobGateRules.Act.Leave,
            "ticket gates: what the shift switched off goes back to the game when the worker is out or the shift is over, and a gate it never touched is never touched");
        string director = ModSource(@"Jobs\JobDirector.cs");
        string ending = ModSource(@"Jobs\JobDirector.Ending.cs");
        string tick = MemberText(director, "private void Tick(float now)");
        Check(tick.Contains("BuildingManager.IsInsideBuilding && _job.WorksInside(premises.building)") && tick.Contains("_gates.Keep();") && tick.Contains("_gates.Release();"),
            "ticket gates: the director's tick keeps them open while the job works inside the building the player is in, and gives them back when not");
        Check(MemberText(ending, "private void EndJob(JobEnd why, string reasonKey)").Contains("_gates.Release();") && MemberText(director, "private void OnDestroy()").Contains("_gates.Release();"),
            "ticket gates: every end of a shift and the city going give them back");
        string gates = ModSource(@"Jobs\JobGates.cs");
        Check(gates.Contains("TicketEntryBlocker.UpdateBlockers();") && gates.Contains("\"ba:businesstype_cinema\"") && gates.Contains("\"ba:businesstype_theater\"")
              && gates.Contains("SceneFinds.All<TicketEntryBlocker>(") && !gates.Contains("FindObjectsOfType"),
            "ticket gates: the game's own rule decides again at the end, only a cinema or a theatre is looked into, and a scene search is the last resort, counted");
    }

    // A parked vehicle is never beside a marked crossing, and the player is told every time one of theirs is moved from a bay.
    private static void BoardParkingChecks()
    {
        string parking = ModSource(@"Traffic\SceneParking.cs");
        string stands = MemberText(parking, "public static bool Stands(Vector3 position, Quaternion rotation, float halfWidth, float halfLength, float margin)");
        Check(stands.Contains("ClearOfCrossings(position, rotation, halfWidth, halfLength)") && parking.Contains("CitySpots.ClearOfCrossings("),
            "parked patrol car: every place a car may stand at a kerb is refused beside a marked crossing, where the pedestrians' mesh is not a kerb");
        string cars = ModSource(@"Transport\BusZones.Cars.cs");
        string move = MemberText(cars, "private static int MovePlayerCars(TransitStop stop, Vector3 right, ref int left)");
        Check(move.Contains("Time.unscaledTime - _toldAt >= TellEvery") && !cars.Contains("_told;") && move.Contains("\"vehicle\""),
            "bay move: the player is told each time a vehicle of theirs is moved out of a bus stop's bay, with its name, not once a session");
    }
}
