using System;
using System.IO;
using System.Text.RegularExpressions;

// Over the sources as they ship, the slow spots the performance notes named: the interact key tests its two keys every
// frame and asks the features whether the key is theirs only on a press, every feature it asked before still asked
// (World/NearbyInteraction.cs); and the bus zones lay a shortened parking lane's spots out again one lane a tick, never
// in the frame the zone is made (Transport/BusZones.Cars.cs), with every lane still laid out and none left waiting
// after the street is put back.
internal static partial class Program
{
    private static readonly string[] InteractOwners =
    {
        "JobDirector", "TransitDirector", "CarOwnership", "HomeDirector", "GarageDirector", "Leisure.LeisureDirector", "SocialDirector",
        "TravelDirector", "Justice", "LicenceDirector", "CivicDirector", "Family.FamilyDirector", "Law.LawDirector", "Roommates.RoommateDirector",
        "Ledger.LedgerDirector", "Careers.CareersDirector", "Tech.TechSites", "Street.StreetDirector",
        "Hustles.HustleDirector", "Economy.EstateDirector"
    };

    private static void SlowSpots()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string update = Body(Source(scripts, @"World\NearbyInteraction.cs"), "private void OnUpdate()");
        int option = update.IndexOf("Settings.EnterWithKey", StringComparison.Ordinal);
        int keys = update.IndexOf("Keys.Down(KeyAction.Interact)", StringComparison.Ordinal);
        int pressed = update.IndexOf("if (!pressed)", StringComparison.Ordinal);
        int flags = update.IndexOf("TakesInteractKey", StringComparison.Ordinal);
        Check(option >= 0 && keys > option && pressed > keys && flags > pressed, "interact key: the option, then the two keys, then the features' flags only on a press");
        int asked = 0;
        string lost = null;
        foreach (string owner in InteractOwners)
        {
            if (update.Contains(owner + ".TakesInteractKey"))
            {
                asked++;
            }
            else
            {
                lost = owner;
            }
        }
        Check(asked == InteractOwners.Length && Regex.Matches(update, @"\.TakesInteractKey").Count == InteractOwners.Length,
            "interact key: every feature asked before is asked still, once (" + asked + " of " + InteractOwners.Length + (lost != null ? "; not: " + lost : "") + ")");

        string cars = Source(scripts, @"Transport\BusZones.Cars.cs");
        string zones = Source(scripts, @"Transport\BusZones.cs");
        string shorten = Body(cars, "private static int Shorten(");
        string relay = Body(cars, "private static void RelayOne()");
        Check(!shorten.Contains("RegenerateAutoParkSpotsNear") && shorten.Contains("Relay.Add(lane)"), "bus zones: a shortened lane is queued to be laid out again, not laid out in the frame");
        Check(Regex.Matches(relay, "RegenerateAutoParkSpotsNear").Count == 1 && relay.Contains("Relay.RemoveAt(0)") && !relay.Contains("while") && !relay.Contains("for ("),
            "bus zones: one lane is laid out again a tick, the oldest first");
        Check(Body(zones, "public static void Step()").Contains("RelayOne();"), "bus zones: the director's tick lays out the next lane");
        string restore = Body(zones, "public static void Restore()");
        Check(restore.Contains("RegenerateAutoParkSpotsNear") && restore.Contains("Relay.Clear();"), "bus zones: put back, every lane is laid out as the game had it and none is left waiting");
        Check(Body(zones, "private static bool MakeZone(").Contains("SessionWatch.Piece("), "bus zones: making a zone is named in the session record");
    }
}
