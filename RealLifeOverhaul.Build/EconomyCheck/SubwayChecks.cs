using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Schools;
using RealLifeOverhaul.Subway;
using RealLifeOverhaul.Uptown;

// The subway stations of Uptown and Cedar Heights (Scripts/Subway, run with --subway; their ground is NavCheck's, UptownSubwayCheck.cs): where a
// station goes in the game's own list, which is in the order of its names, and what a typed search shows; when the stations stand (their option,
// the districts, the game's own network) and when they may go (never under a rider); the people of the line by the trains' clock, the same on
// every machine, more in the rush and few at night, beside the bus stops' own; Birchwood's entrance against the campus's gates, their crowds and
// its fence; the sources (the fare is the game's alone and never the mod's to waive, nothing saved, nothing sent, nothing a frame, the game's own
// Start replaced by one that is taken off again, no second station of a site, what is keyed to the game's twenty leaving the mod's out); and the
// texts in every language.
internal static partial class Program
{
    // The game's twenty stations as its English names them (its locale, Build 3682), in the order the game's own sort gives its list.
    private static readonly string[] SubwayGameNames =
    {
        "Garment District - East Station", "Garment District - Harbor Station", "Garment District - West Station", "Hell's Kitchen - East Station", "Hell's Kitchen - North Station",
        "Hell's Kitchen - South Station", "Hell's Kitchen - West Station", "Industry City - Center Station", "Industry City - Harbor Station", "Industry City - Sunset Park Station",
        "Lower Manhattan - Center Station", "Lower Manhattan - North Station", "Midtown - Grand Central Station", "Midtown - Madison Park Station", "Midtown - North West Station",
        "Midtown - St. Patrick Station", "Midtown - Times Square Station", "Murray Hill - East Station", "Murray Hill - North Station", "Murray Hill - South Station"
    };

    private static readonly string[] SubwaySources =
    {
        @"Subway\StationsRules.cs", @"Subway\StationsGame.cs", @"Subway\StationEntrance.cs", @"Subway\StationsDirector.cs", @"Subway\StationsDirector.Riders.cs",
        @"Subway\StationsDirector.Tests.cs", @"Subway\StationsGuide.cs", @"Subway\StationsText.cs"
    };

    private static void Subway()
    {
        SubwayListChecks();
        SubwayStandChecks();
        SubwayRiderChecks();
        SubwayCampusChecks();
        SubwaySourceChecks();
        SubwayTextChecks();
    }

    // ---- the game's list of stations

    private static void SubwayListChecks()
    {
        Comparison<string> order = (a, b) => string.Compare(a, b, CultureInfo.GetCultureInfo("en-US"), CompareOptions.None);
        var names = new List<string>(SubwayGameNames);
        names.Add("The Hamptons Station - Marina");
        names.Sort(order);
        Check(names.Count == 21 && names[0].StartsWith("Garment", StringComparison.Ordinal) && names[20].StartsWith("The Hamptons", StringComparison.Ordinal), "subway list: the game's own names, sorted as it sorts them");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        string birchwood = en != null && en.TryGetValue(StationsRules.BirchwoodKey, out string b) ? b : null, summit = en != null && en.TryGetValue(StationsRules.SummitKey, out string s) ? s : null,
            east = en != null && en.TryGetValue(StationsRules.EastKey, out string e) ? e : null;
        Check(birchwood != null && summit != null && east != null, "subway list: the three stations have their English names");
        if (birchwood == null || summit == null || east == null)
        {
            return;
        }
        // Each joins at its name's place, and the list stays in order.
        foreach (string name in new[] { east, birchwood, summit })
        {
            int at = StationsRules.InsertAt(names, name, order);
            names.Insert(at, name);
        }
        bool sorted = true;
        for (int i = 1; i < names.Count; i++)
        {
            sorted &= order(names[i - 1], names[i]) <= 0;
        }
        Check(sorted && names.Count == 24, "subway list: with the three stations joined at their names' places the list is still in the order of its names");
        Check(names.IndexOf(birchwood) == 0 && names.IndexOf(summit) == 1 && names.IndexOf(east) == 23,
            "subway list: Cedar Heights' two come first and Uptown's last, by their names (" + names.IndexOf(birchwood) + ", " + names.IndexOf(summit) + ", " + names.IndexOf(east) + ")");
        Check(StationsRules.InsertAt(new List<string>(), "x", order) == 0 && StationsRules.InsertAt(null, "x", order) == 0, "subway list: into an empty list, or none, a station goes first");
        Check(StationsRules.InsertAt(new List<string> { "a", "b", "c" }, "d", order) == 3 && StationsRules.InsertAt(new List<string> { "b", "c" }, "a", order) == 0
              && StationsRules.InsertAt(new List<string> { "a", "c" }, "b", order) == 1, "subway list: after the last, before the first and between two");
        Check(StationsRules.InsertAt(new List<string> { "a", "b", "b", "c" }, "b", order) == 3, "subway list: a name the list has already goes after its like");
        // The search: as the game's own reads a name.
        StringComparison loose = StringComparison.OrdinalIgnoreCase;
        Check(StationsRules.Shown(birchwood, string.Empty, loose) && StationsRules.Shown(birchwood, null, loose) && StationsRules.Shown(birchwood, "   ", loose),
            "subway search: with nothing typed a station is shown");
        Check(StationsRules.Shown(birchwood, "birch", loose) && StationsRules.Shown(birchwood, "  BIRCHWOOD ", loose) && StationsRules.Shown(east, "uptown", loose) && StationsRules.Shown(summit, "park", loose),
            "subway search: a part of its own name shows it, whatever the case and the spaces round it");
        Check(!StationsRules.Shown(birchwood, "midtown", loose) && !StationsRules.Shown(east, "garment", loose) && !StationsRules.Shown(summit, "birchwood", loose) && !StationsRules.Shown(null, "x", loose),
            "subway search: the name of the game's entrance it was copied from does not show it, nor another station's");
        Check(!StationsRules.Shown(birchwood, "BIRCH", StringComparison.Ordinal), "subway search: the comparison is the caller's (the game's ignores case)");
    }

    // ---- standing and going

    private static void SubwayStandChecks()
    {
        Check(StationsRules.Wanted(true, true, 20), "subway standing: with the option on, the districts open and the game's stations there, they stand");
        Check(!StationsRules.Wanted(false, true, 20), "subway standing: with the option off they do not stand");
        Check(!StationsRules.Wanted(true, false, 20), "subway standing: with the districts not laid or closed they do not stand");
        Check(!StationsRules.Wanted(true, true, 0) && !StationsRules.Wanted(true, true, -3), "subway standing: with none of the game's own stations there is no network to join");
        Check(StationsRules.Removal(true, true, false) == StationRemoval.AfterRide && StationsRules.Removal(true, true, true) == StationRemoval.AfterRide,
            "subway going: a ride that begins or ends at one of them is let finish first");
        Check(StationsRules.Removal(true, false, false) == StationRemoval.Now, "subway going: a ride between two of the game's own holds nothing back");
        Check(StationsRules.Removal(false, true, true) == StationRemoval.MapFirst && StationsRules.Removal(false, false, true) == StationRemoval.MapFirst,
            "subway going: the game's map open on its stations from one of them is shut first");
        Check(StationsRules.Removal(false, false, false) == StationRemoval.Now && StationsRules.Removal(false, true, false) == StationRemoval.Now, "subway going: otherwise they go at once");
        IReadOnlyList<StationSite> sites = StationsRules.Sites;
        Check(sites.Count == 3 && sites.Select(x => x.Id).Distinct().Count() == 3 && sites.Select(x => x.NameKey).Distinct().Count() == 3 && sites.All(x => Regex.IsMatch(x.Id, "^[a-z]+$")),
            "subway standing: three stations, each with an id of letters and a name of its own");
        Check(sites.All(x => x.Yaw % 90 == 0 && x.Yaw >= 0 && x.Yaw < 360) && sites.All(x => StationsRules.GroundOf(x) < 0f && StationsRules.GroundOf(x) > -0.1f),
            "subway standing: each at a quarter turn, its origin as far into its ground as the game's own stand (" + string.Join(", ", sites.Select(x => StationsRules.GroundOf(x).ToString("F4", CultureInfo.InvariantCulture))) + ")");
        bool lots = sites.All(x => StationsRules.MeetsLot(x, UptownLots.All) == null && StationsRules.MeetsLot(x, UptownLots.Places) == null);
        Check(lots && UptownLots.All.Count > 600 && UptownLots.All.Any(l => l.Open), "subway standing: none on a lot of the plan, of its " + UptownLots.All.Count + " (the open runs the player builds on among them) or its big special places");
    }

    // ---- the people of the line

    private static void SubwayRiderChecks()
    {
        Check(StationsRules.Train(0, 5) == 0 && StationsRules.Train(4, 5) == 0 && StationsRules.Train(5, 5) == 1 && StationsRules.Train(1439, 3) == 479 && StationsRules.Train(100, 0) == 100
              && StationsRules.Train(-5, 5) == 0, "subway people: a train's number of the day from the minute and the minutes between trains, never a division by none");
        // The same on every machine: asked twice, the same answer, for every train of a week at every station.
        bool same = true;
        int up = 0, upRush = 0, upNight = 0, down = 0, downNight = 0, trainsDay = 0, trainsRush = 0, trainsNight = 0, women = 0, looks = 0, upWeekend = 0, trainsWeekend = 0;
        bool lookNeverZero = true, upDownDiffer = false;
        for (int day = 1; day <= 120; day++)
        {
            bool weekend = day % 7 >= 5;
            for (int station = 0; station < 3; station++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    bool rush = !weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19));
                    for (int k = 0; k < 6; k++)
                    {
                        int train = hour * 6 + k;
                        bool u = StationsRules.ComesUp(day, train, station, hour, weekend), d = StationsRules.GoesDown(day, train, station, hour, weekend);
                        same &= u == StationsRules.ComesUp(day, train, station, hour, weekend) && d == StationsRules.GoesDown(day, train, station, hour, weekend);
                        int look = StationsRules.RiderLook(day, train, station, false, out bool female), lookDown = StationsRules.RiderLook(day, train, station, true, out bool femaleDown);
                        same &= look == StationsRules.RiderLook(day, train, station, false, out bool again) && female == again;
                        lookNeverZero &= look != 0 && lookDown != 0;
                        upDownDiffer |= look != lookDown || female != femaleDown;
                        women += female ? 1 : 0;
                        looks++;
                        if (hour < 5)
                        {
                            trainsNight++;
                            upNight += u ? 1 : 0;
                            downNight += d ? 1 : 0;
                        }
                        else if (rush)
                        {
                            trainsRush++;
                            upRush += u ? 1 : 0;
                        }
                        else if (weekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19)))
                        {
                            trainsWeekend++;
                            upWeekend += u ? 1 : 0;
                        }
                        else
                        {
                            trainsDay++;
                            up += u ? 1 : 0;
                            down += d ? 1 : 0;
                        }
                    }
                }
            }
        }
        float rushShare = upRush / (float)trainsRush, dayShare = up / (float)trainsDay, nightShare = upNight / (float)trainsNight, downShare = down / (float)trainsDay,
            downNightShare = downNight / (float)trainsNight, weekendShare = upWeekend / (float)trainsWeekend;
        Check(same, "subway people: who comes up, who goes down and how they look are the same when asked again: a mixed number, never chance");
        Check(Math.Abs(rushShare - StationsRules.RushShare / 10f) < 0.03f && Math.Abs(dayShare - StationsRules.DayShare / 10f) < 0.03f && Math.Abs(nightShare - StationsRules.NightShare / 10f) < 0.03f,
            "subway people: somebody comes up from nine trains in ten in the weekday rush, six by day and two at night (" + rushShare.ToString("F3") + ", " + dayShare.ToString("F3") + ", " + nightShare.ToString("F3") + ")");
        Check(rushShare > dayShare && dayShare > nightShare && StationsRules.RushShare <= 10 && StationsRules.NightShare >= 1, "subway people: more in the rush than by day, more by day than at night, never every train nor none");
        Check(Math.Abs(weekendShare - StationsRules.DayShare / 10f) < 0.04f, "subway people: a weekend has no rush (" + weekendShare.ToString("F3") + " at the rush's hours)");
        Check(Math.Abs(downShare - StationsRules.DownShare / 10f) < 0.03f && Math.Abs(downNightShare - StationsRules.NightShare / 10f) < 0.03f,
            "subway people: somebody walks down to five trains in ten, two at night (" + downShare.ToString("F3") + ", " + downNightShare.ToString("F3") + ")");
        Check(lookNeverZero && upDownDiffer && women > looks * 0.45f && women < looks * 0.55f, "subway people: a rider's look is seeded (never the game's own sequence), half of them women, the one who comes up not the one who goes down");
        // The ways a rider walks: one of eight, and eight tries cover all eight.
        bool ways = true;
        for (int day = 1; day <= 30; day++)
        {
            int seen = 0;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int bearing = StationsRules.RiderBearing(day, day * 3, day % 3, day % 2 == 0, attempt);
                ways &= bearing >= 0 && bearing < 8;
                seen |= 1 << bearing;
            }
            ways &= seen == 255;
        }
        Check(ways, "subway people: a rider walks one of eight ways, and eight tries are the eight ways, each once");
        bool along = true;
        for (int bearing = -8; bearing < 16; bearing++)
        {
            StationsRules.Along(10f, 20f, bearing, StationsRules.RiderWalk, out float ox, out float oz);
            along &= Math.Abs(Math.Sqrt((ox - 10f) * (ox - 10f) + (oz - 20f) * (oz - 20f)) - StationsRules.RiderWalk) < 0.01;
        }
        StationsRules.Along(0f, 0f, 0, 10f, out float ex, out float ez);
        StationsRules.Along(0f, 0f, 2, 10f, out float nx, out float nz);
        Check(along && Math.Abs(ex - 10f) < 0.001f && Math.Abs(ez) < 0.001f && Math.Abs(nx) < 0.001f && Math.Abs(nz - 10f) < 0.001f, "subway people: a way is a point that many metres off, east first, then towards north");
        Check(StationsRules.RidersMost == 2 && StationsRules.RidersKeep > StationsRules.RidersNear && StationsRules.RiderWalk > StationsRules.RiderWalkLeast && StationsRules.RiderWalk < StationsRules.RidersNear,
            "subway people: two at the most, shown within " + StationsRules.RidersNear + " m and kept to " + StationsRules.RidersKeep + ", walking " + StationsRules.RiderWalk + " m");
        string cast = ModSource(@"Jobs\JobCast.cs"), riders = ModSource(@"Subway\StationsDirector.Riders.cs");
        Check(cast.Contains("public const int Size = 2;") && riders.Contains("private Rider _up, _down;") && riders.Contains("_cast.Prewarm(JobCast.Size)"), "subway people: the cast holds the two, made behind the loading screen");
        Check(riders.Contains("StationsRules.ComesUp(day, train, place, hour, weekend)") && riders.Contains("StationsRules.GoesDown(day, train, place, hour, weekend)")
              && riders.Contains("StationsRules.RiderLook(day, train, place, false, out bool female)") && riders.Contains("StationsRules.RiderBearing(day, train, place, down, attempt)")
              && riders.Contains("StationsRules.Train(TimeHelper.CurrentHour * 60 + (int)TimeHelper.CurrentMinute, SubwayJourney.Frequency())"),
            "subway people: the director asks these very rules, with the trains of the service the player's own journey waits on");
        Check(riders.Contains("!Settings.TransitPeople") && !riders.Contains("_waiting") && !riders.Contains("_waiters") && !riders.Contains("TransitDirector"),
            "subway people: under the option for people at the stops, and beside the bus stops' own people, whom nothing here touches");
        Check(riders.Contains("person == null || !person.IsValid || person.Limp || !person.InUse") && riders.Contains("_cast?.ReleaseAll();"),
            "subway people: somebody knocked down or gone is let go, and all are taken in when the station, the player or the city goes");
    }

    // ---- Birchwood's entrance and the campus

    private static void SubwayCampusChecks()
    {
        StationSite site = StationsRules.Get("birchwood");
        Check(site != null && site.Block == LayoutRules.CampusBlock && site.Ground == StationGround.Pavement, "subway campus: Birchwood's entrance is on the pavement by the campus's block");
        if (site == null)
        {
            return;
        }
        SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
        StationsRules.Footprint(site, out float fx0, out float fz0, out float fx1, out float fz1);
        StationsRules.Arrival(site, out float ax, out float az);
        Check(StationsRules.Holds(site, out bool pavement) == LayoutRules.CampusBlock && pavement && fz1 < z0 - SchoolGrounds.FenceIn && fx0 > x0 && fx1 < x1,
            "subway campus: it stands outside the campus's fence, on Uptown Avenue's pavement along the campus's own front");
        var crowds = new List<CampusCrowd>();
        SchoolGrounds.Crowds(crowds);
        bool clear = crowds.Count >= 2;
        float nearest = float.MaxValue;
        foreach (CampusCrowd crowd in crowds)
        {
            bool across = Math.Abs(crowd.Yaw - 90f) < 1f || Math.Abs(crowd.Yaw - 270f) < 1f;
            float halfX = (across ? crowd.Depth : crowd.Width) * 0.5f, halfZ = (across ? crowd.Width : crowd.Depth) * 0.5f;
            clear &= StationsRules.MeetsBox(site, crowd.X - halfX, crowd.Z - halfZ, crowd.X + halfX, crowd.Z + halfZ, StationsRules.LotClear, crowd.Name) == null;
            if (!crowd.Inside && Math.Abs(crowd.Z - az) < 3f)
            {
                nearest = Math.Min(nearest, Math.Max(0f, Math.Max(crowd.X - halfX - Math.Max(fx1, ax), Math.Min(fx0, ax) - (crowd.X + halfX))));
            }
        }
        Check(clear && nearest >= 2f && nearest < 12f, "subway campus: clear of every gate's crowd, its riders up " + nearest.ToString("F1") + " m from where the main gate's crowd gathers");
        SchoolGrounds.Gate(out float gx, out float gz, out _);
        Check(Math.Abs(ax - gx) < 25f && (fx1 < gx - SchoolGrounds.GateWidth * 0.5f || fx0 > gx + SchoolGrounds.GateWidth * 0.5f) && Math.Abs(gz - z0) < 6f,
            "subway campus: beside the main gate and never before it (" + Math.Abs(ax - gx).ToString("F1") + " m from the gate's middle)");
        var before = new StationSite("x", "k", LayoutRules.CampusBlock, StationGround.Pavement, gx, site.Z, 90);
        bool refused = false;
        foreach (CampusCrowd crowd in crowds)
        {
            bool across = Math.Abs(crowd.Yaw - 90f) < 1f || Math.Abs(crowd.Yaw - 270f) < 1f;
            float halfX = (across ? crowd.Depth : crowd.Width) * 0.5f, halfZ = (across ? crowd.Width : crowd.Depth) * 0.5f;
            refused |= StationsRules.MeetsBox(before, crowd.X - halfX, crowd.Z - halfZ, crowd.X + halfX, crowd.Z + halfZ, StationsRules.LotClear, crowd.Name) != null;
        }
        Check(refused, "subway campus: an entrance before the main gate is refused for the crowd's ground");
        string director = ModSource(@"Subway\StationsDirector.cs");
        Check(director.Contains("Schools.SchoolGrounds.Crowds(_crowds);") && director.Contains("StationsRules.MeetsBox(site, crowd.X - halfX, crowd.Z - halfZ, crowd.X + halfX, crowd.Z + halfZ, StationsRules.LotClear"),
            "subway campus: the director asks the same rule of the same crowds before it stands the entrance");
    }

    // ---- the sources

    private static void SubwaySourceChecks()
    {
        string rules = ModSource(@"Subway\StationsRules.cs"), game = ModSource(@"Subway\StationsGame.cs"), entranceFile = ModSource(@"Subway\StationEntrance.cs"),
            director = ModSource(@"Subway\StationsDirector.cs"), riders = ModSource(@"Subway\StationsDirector.Riders.cs"), tests = ModSource(@"Subway\StationsDirector.Tests.cs"),
            guide = ModSource(@"Subway\StationsGuide.cs"), own = ModSource(@"Core\OwnStations.cs");
        string all = string.Concat(SubwaySources.Select(ModSource));
        Check(SubwaySources.All(f => ModSource(f).Length > 300) && own.Length > 300, "subway sources: its files are read");
        int cut = entranceFile.IndexOf("public sealed class StationTaxiStop", StringComparison.Ordinal);
        string entrance = cut > 0 ? entranceFile.Substring(0, cut) : entranceFile, stop = cut > 0 ? entranceFile.Substring(cut) : string.Empty;

        // The game-free rules have no Unity or game types.
        Check(!rules.Contains("UnityEngine") && !rules.Contains("using BAModAPI") && !rules.Contains("Vector3") && !rules.Contains("SubwayStation "), "subway sources: the rules are game-free");

        // The fare is the game's own: nothing here books, gives or waives money, and travel is the game's own call.
        Check(!all.Contains("BizBooks") && !all.Contains("ChangeMoney") && !Regex.IsMatch(all, @"\.Money\s*(=|\+=|-=)[^=]") && !all.Contains("TransactionInfo"),
            "subway sources: nothing of the stations books, gives or takes money: the fare is the game's own, taken by its own travel");
        Check(entrance.Contains("public sealed class StationEntrance : SubwayStation") && !entrance.Contains("override bool Interact") && !entrance.Contains("PricePerRide"),
            "subway sources: the entrance is the game's own station and keeps its Interact, which asks for the fare before the map opens");
        Check(Regex.Matches(all, @"\.TravelTo\(").Count == 1 && tests.Contains("city.subwaySystem.TravelTo(to);") && tests.Contains("SaveGameManager.Current.Money < SubwayStation.PricePerRide"),
            "subway sources: the one travel the mod asks for is its test's, through the game's own call, refused first without the fare");
        Check(entrance.Contains("return base.OnIoLeftClick();") && entrance.Contains("uis.buildingResume.SelectTarget(TaxiStop);") && entrance.Contains("!city.cityMap.isSubwayMode && CityMap.IsOpen && city.cityMap.IsTaxiMode"),
            "subway sources: a click is the game's own but in its taxi mode, where the taxi's destination is the station's stop");
        Check(stop.Contains("TaxiDestinationData data = gameObject.AddComponent<TaxiDestinationData>();") && stop.Contains("data.mapColliders = new Collider[0];") && stop.Contains("data.nameKey = nameKey;"),
            "subway sources: the stop carries the game's own destination data, named by the station's key, its list for the map's toggle never null");

        // Named its own everywhere the game shows a name.
        Check(entrance.Contains("customOverlayHeaderKey = site.NameKey;") && game.Contains("entry.stationNameLabel.Key = stood.Site.NameKey;") && game.Contains("OwnStations.Add(station, site.NameKey,"),
            "subway sources: named by its own key over the street, in the game's window and for every reader of the mod");
        string members = ModSource(@"Core\GameMembers.cs");
        int asked = members.IndexOf("OwnStations.KeyOf(station)", StringComparison.Ordinal), games = members.IndexOf("return StationKey(station);", StringComparison.Ordinal);
        Check(asked > 0 && games > asked, "subway sources: a station's name is asked of the mod's own first, before the game's method that reads the copied value");
        Check(ModSource(@"Transport\TransitMap.cs").Contains("city.subwayStations[i] != null && !OwnStations.Is(city.subwayStations[i])"),
            "subway sources: the bus network's ends are the game's stations alone, so the lines are the same with or without the districts' stations");
        Check(ModSource(@"Leisure\LeisureVenues.cs").Contains("if (station == null || OwnStations.Is(station))"), "subway sources: the leisure landmarks keyed to the game's stations' names leave the mod's out");
        Check(own.Contains("if (ReferenceEquals(station, null) || Stations.Contains(station))") && own.Contains("Stamp++;"), "subway sources: a station joins the mod's own once");

        // The game's Start, which hangs a call on the map's toggle for good, is replaced by one that hangs nothing: the outline is white again in the entity's own leave and enter.
        Check(entrance.Contains("public override void Start()") && !entrance.Contains("base.Start()") && !all.Contains("onCityMapToggle") && !all.Contains("onCityMapClosed")
              && !all.Contains("Delegate.Combine") && entrance.Contains("public override void OnIoExit()") && entrance.Contains("if (!mapFilterIsOn)")
              && entrance.Contains("public override void OnIoEnter()") && entrance.Contains("if (!CityMap.IsOpen)"),
            "subway sources: nothing of the stations is hung on the map's events (the game's own Start would hang a call for good): the outline is white again in the entrance's own leave and enter");
        Check(entrance.Contains("public override void UpdateNavMeshTargets()") && game.Contains("arrival.position = arrive;") && game.Contains("string walk = ComesUp(site, out Vector3 arrive);"),
            "subway sources: the travel point is put on checked ground and never moved by the game's own validation");

        // Stood once, whole or not at all; taken away as it came.
        Check(game.Contains("if (Find(site) != null)") && game.Contains("return \"it stands already\";") && director.Contains("StationsGame.Forget();   // nothing of an earlier city is held"),
            "subway sources: a site is stood once, and nothing of an earlier city is held: no second station after any number of loads");
        int solid = game.IndexOf("string solid = Solid(at, turn);", StringComparison.Ordinal), walk = game.IndexOf("string walk = ComesUp(site, out Vector3 arrive);", StringComparison.Ordinal),
            made = game.IndexOf("UnityEngine.Object.Instantiate(template.gameObject, asleep.transform)", StringComparison.Ordinal), joined = game.IndexOf("city.subwayStations.Add(station);", StringComparison.Ordinal);
        Check(solid > 0 && walk > solid && made > walk && joined > made, "subway sources: something solid on its ground or riders off the walking ground refuse a station before anything of it is made or joined");
        Check(game.Contains("asleep.SetActive(false);") && game.Contains("UnityEngine.Object.DestroyImmediate(copied);") && game.Contains("!drawn.isPartOfStaticBatch"),
            "subway sources: the copy is made asleep, the game's own station on it never wakes, and an entrance drawn in a static batch is never copied");
        int outOfList = game.IndexOf("city.subwayStations.Remove(station);", StringComparison.Ordinal), destroyed = game.IndexOf("SceneryTrees.Destroy(station.gameObject);", StringComparison.Ordinal),
            unmarked = game.IndexOf("Unmark(city, station.transform);", StringComparison.Ordinal);
        Check(outOfList > 0 && unmarked > outOfList && destroyed > unmarked && game.Contains("map.pois.RemoveAt(i);") && game.Contains("map.UpdateNonePermanentPointOfInterests();") && game.Contains("entries?.Remove(stood.Entry);"),
            "subway sources: a station leaves the game's list, window and map before it is destroyed, since the game walks all three");
        Check(Regex.Matches(game, @"SceneryTrees\.Destroy\(").Count == 4 && Regex.Matches(game, @"UnityEngine\.Object\.Destroy\(").Count == 3
              && game.Contains("UnityEngine.Object.Destroy(stood.Entry.gameObject);") && game.Contains("UnityEngine.Object.Destroy(marker.gameObject);") && game.Contains("UnityEngine.Object.Destroy(_watch);")
              && !all.Contains("RegisterCullable") && !all.Contains("UnregisterCullable"),
            "subway sources: every copy of the game's entrance is destroyed through SceneryTrees, which takes its lamp out of the game's culling lists first; the three plain destroys are the window's entry, the map's marker and the watch");
        Check(director.Contains("switch (StationsRules.Removal(SubwaySystem.IsRiding, StationsGame.RideTouches(), StationsGame.PickingFromOwn()))") && director.Contains("case StationRemoval.AfterRide:")
              && director.Contains("StationsGame.ShutMap();"), "subway sources: the director asks the rule before it takes them away, and waits for a rider");
        Check(director.Contains("bool wanted = StationsRules.Wanted(Settings.UptownSubway, UptownLots.Ready && !UptownLots.Closed, StationsGame.GameStations());")
              && director.Contains("UptownLots.Closing += _onClosing;") && director.Contains("UptownLots.Closing -= _onClosing;"),
            "subway sources: whether they stand is the rule's answer from the option, the districts and the game's network, and the districts' closing takes them away");
        Check(director.Contains("StationsRules.Holds(site, out bool pavement)") && director.Contains("StationsRules.MeetsLot(site, UptownLots.All) ?? StationsRules.MeetsLot(site, UptownLots.Places)")
              && director.Contains("StationsRules.PavementRoom(site, out float behind, out float toKerb)") && director.Contains("StationsRules.Lamps(site, _px, _pz);")
              && director.Contains("StreetsRules.StopPoles(_px, _pz);") && director.Contains("ParksRules.Pieces(site.Block, _parkPieces);") && director.Contains("why = Plan(site) ?? StationsGame.Stand(site, out _);"),
            "subway sources: the director looks at a station's ground with the rules the checks run before the game's side stands it");
        Check(game.Contains("StationsRules.InsertAt(ListNames, NameOf(stood.Site), Order)") && game.Contains("StationsRules.Shown(NameOf(Standing[i].Site), typed, StringComparison.CurrentCultureIgnoreCase)")
              && game.Contains("StationsRules.Carve(out float ahead, out float across, out float along);") && game.Contains("StationsRules.GroundOf(site)"),
            "subway sources: the window's place, the search, the cut and the height are the rules' own");
        Check(game.Contains("_search.onValueChanged.RemoveListener(_onSearch);") && game.Contains("_search.onValueChanged.AddListener(_onSearch);") && game.Contains("_onSearch = _onSearch ?? Guard.Listener<string>(OnSearch);")
              && game.Contains("StationsGame.Rehang();"), "subway sources: the search's listener is made once and put after the game's own each time the window is shown");

        // Nothing saved, nothing sent, nothing by chance, nothing a frame.
        Check(!all.Contains("SaveHook") && !all.Contains("modData") && !all.Contains("reallife:subway") && !all.Contains("PlayerPrefs"), "subway sources: nothing is saved: fixed places laid again at every load");
        Check(!all.Contains("UnityEngine.Random") && !Regex.IsMatch(all, @"\bRandom\.") && !all.Contains("GoingPublicLink") && !all.Contains("System.Random") && !all.Contains("DateTime.Now"),
            "subway sources: nothing is sent and nothing is chance: the same stations and the same people on every machine");
        Check(!all.Contains("FindObjectsByType") && !all.Contains("FindObjectOfType") && !all.Contains("GameObject.Find("), "subway sources: nothing is searched for in the scene: the game's own list and window alone");
        string update = MemberText(director, "private void OnUpdate()");
        int first = update.IndexOf("if (now < _nextLook)", StringComparison.Ordinal), decide = update.IndexOf("Decide(", StringComparison.Ordinal);
        Check(first > 0 && decide > first && update.Contains("_nextLook = now + (busy ? Look : Idle);") && director.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && director.Contains("if (FrameGuard.Off)"),
            "subway sources: a frame costs one time comparison; the looks come once a second, four while somebody of the line is out");
        Check(Regex.Matches(all, @"void (Update|LateUpdate|FixedUpdate)\(").Count == 1, "subway sources: one Update in all of it, the director's");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        int landmarks = mod.IndexOf("Guard.Add<Landmarks.LandmarksDirector>(_root)", StringComparison.Ordinal), stations = mod.IndexOf("Guard.Add<Subway.StationsDirector>(_root)", StringComparison.Ordinal),
            ground = mod.IndexOf("Guard.Add<Uptown.GroundDirector>(_root)", StringComparison.Ordinal), transit = mod.IndexOf("Guard.Add<TransitDirector>(_root)", StringComparison.Ordinal);
        Check(ground > 0 && transit > 0 && landmarks > ground && stations > landmarks && stations > transit, "subway sources: its director is added after the districts' ground, the campus, the landmarks and the buses, whose late pieces run before its own");
        Check(director.Contains("ScenePrewarm.AddLate(") && director.Contains("AtLoad"), "subway sources: the stations are stood in the game's late call, when its own stations and its window of them are there");

        // The option, the feature and its tests.
        string settings = ModSource(@"Core\Settings.cs");
        Check(settings.Contains("public const string UptownSubwayId = \"uptown_subway\";") && settings.Contains("public static bool UptownSubway = true;") && settings.Contains("UptownSubway = Bool(modId, UptownSubwayId, UptownSubway);"),
            "subway sources: its option, on by default");
        Check(ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"uptown_subway\", CityScope.NewYork,") && ModSource(@"Core\FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Subway\", \"uptown_subway\" },")
              && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"uptown_subway\").For(\"uptown_subway\").Opts(\"uptown_subway\")") && director.Contains("public const string FeatureId = \"uptown_subway\";"),
            "subway sources: a feature of New York's own, with its owner, its section of the options and its line of the cities");
        Check(guide.Contains(".Check(StationsText.TestCheck, StationsDirector.TestStations, \"Subway check (stations)\")") && guide.Contains(".Test(StationsText.TestGo, StationsDirector.TestGo)")
              && guide.Contains(".Test(StationsText.TestRide, StationsDirector.TestRide)") && guide.Contains(".Test(StationsText.TestRider, StationsDirector.TestRider)")
              && guide.Contains(".Cleanup(") && guide.Contains("new ToggleOption(Settings.UptownSubwayId, StationsText.Enabled, true, on => Settings.UptownSubway = on)") && guide.Contains(".Objects(StationsDirector.RootName)"),
            "subway sources: its entry has its option, its four tests, its clean-up and its own object's name");
        Check(tests.Contains("public static void TestStations()") && tests.Contains("public static void TestGo()") && tests.Contains("public static void TestRide()") && tests.Contains("public static void TestRider()")
              && Regex.Matches(tests, @"TestRefusals\.Tell\(").Count >= 10 && tests.Contains("Logger?.Info(\"Subway check (stations): "),
            "subway sources: each test is there, says why when it cannot run, and the check writes its line");
        Check(ModSource(@"Core\GameContract.Map.cs").Contains("MapStationEntries = Field(UiMapDock, Game, \"CityMapSubwayStations\", \"_generatedEntries\", \"List<CityMapSubwayStationEntry>\")")
              && game.Contains("GameContract.MapStationEntries.Field?.GetValue(list)") && game.Contains("return \"the game's window of stations is not as the mod knows it\";")
              && !all.Contains("GetField(") && !all.Contains("BindingFlags"),
            "subway sources: the one member of the game's read by name is the contract's line for the window's entries, and a game without it leaves the stations unstood with one line");
    }

    // ---- the texts

    private static void SubwayTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string source in SubwaySources)
        {
            foreach (Match m in Regex.Matches(ModSource(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_opt_sec_uptown_subway");
        keys.Add("reallife_opt_tip_uptown_subway");
        Check(keys.Count == 18, "subway texts: its keys are found: " + keys.Count);
        string[] notices = { "reallife_uptown_subway_check_done", "reallife_uptown_subway_check_wrong", "reallife_uptown_subway_go_done", "reallife_uptown_subway_ride_done", "reallife_uptown_subway_rider_done",
            "reallife_uptown_streets_subway_done" };
        string[] names = { StationsRules.BirchwoodKey, StationsRules.SummitKey, StationsRules.EastKey };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(english != null, "subway texts: en.json is read");
        if (english == null)
        {
            return;
        }
        // The What's new line is found by its English words in whichever row holds it (the next row, or a version's once it is numbered).
        string row = english.Where(p => p.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !p.Key.EndsWith("_title", StringComparison.Ordinal)
                                        && p.Value.Contains("Uptown and Cedar Heights now have three subway stations of their own")).Select(p => p.Key).FirstOrDefault();
        Check(row != null, "subway texts: What's new has the stations' line in English (any of its rows)");
        var subwayWord = new Dictionary<string, string>
        {
            { "en", "subway" }, { "pt", "metrô" }, { "zh-cn", "地铁" }, { "de", "U-Bahn" }, { "fr", "métro" }, { "es", "metro" }, { "ru", "метро" }, { "nl", "metro" }, { "tr", "metro" }, { "pl", "metr" }, { "ko", "지하철" }
        };
        foreach (string code in ModLanguages.All)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "subway texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            foreach (string key in keys.Concat(new[] { "reallife_uptown_streets_subway_done" }))
            {
                bool there = words.TryGetValue(key, out string value) && value.Length > 0;
                Check(there, "subway texts: " + key + " is in " + code + ".json");
                if (!there || !english.TryGetValue(key, out string source))
                {
                    continue;
                }
                var wanted = new SortedSet<string>(Regex.Matches(source, @"\{[a-z0-9_]+\}").Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
                var held = new SortedSet<string>(Regex.Matches(value, @"\{[a-z0-9_]+\}").Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
                Check(wanted.SetEquals(held), "subway texts: " + key + " keeps its fields in " + code + " (" + string.Join(" ", held) + ")");
                Check(!value.Contains("!") && !value.Contains("！") && !value.Contains("–") && !value.Contains("—") && !value.Contains(" - "), "subway texts: " + key + " has no dash and no exclamation mark in " + code);
                Check(Regex.Matches(value, "<b>").Count == Regex.Matches(source, "<b>").Count && Regex.Matches(value, "</b>").Count == Regex.Matches(source, "</b>").Count,
                    "subway texts: " + key + " keeps its bold names in " + code);
            }
            foreach (string notice in notices)
            {
                Check(words.TryGetValue(notice, out string text) && text.Length <= 240, "subway texts: the notice " + notice + " is at most 240 characters in " + code + " (" + (text != null ? text.Length : 0) + ")");
            }
            // The stations' names: three, each its own, in the language's own script where it has one.
            string[] station = names.Select(k => words.TryGetValue(k, out string v) ? v : string.Empty).ToArray();
            Check(station.All(n => n.Length > 5) && station.Distinct().Count() == 3 && station.All(n => !Regex.IsMatch(n, @"[,:;.]")), "subway texts: three station names, each its own and plain, in " + code);
            if (code == "zh-cn" || code == "ko" || code == "ru")
            {
                Check(station.All(n => n.Any(c => c > 0x2ff)), "subway texts: the stations' names are in " + code + "'s own script");
            }
            // The Guide page: the same lines as the English, its two figures, and its bold names the language's own texts.
            if (words.TryGetValue("reallife_guide_uptown_subway", out string page) && english.TryGetValue("reallife_guide_uptown_subway", out string pageEn))
            {
                string[] lines = page.Split('\n'), linesEn = pageEn.Split('\n');
                bool shaped = lines.Length == linesEn.Length;
                for (int i = 0; shaped && i < lines.Length; i++)
                {
                    shaped &= lines[i].StartsWith("# ", StringComparison.Ordinal) == linesEn[i].StartsWith("# ", StringComparison.Ordinal)
                              && lines[i].StartsWith("- ", StringComparison.Ordinal) == linesEn[i].StartsWith("- ", StringComparison.Ordinal);
                }
                Check(shaped && linesEn.Length == 10, "subway texts: the Guide page has the English page's lines, heads and points in " + code + " (" + lines.Length + ")");
                Check(page.Contains("{uptown_subway_stations}") && page.Contains("{uptown_subway_fare}"), "subway texts: the Guide page has its two figures in " + code);
                bool bold = true;
                foreach (Match m in Regex.Matches(page, "<b>(.*?)</b>"))
                {
                    bold &= words.Values.Contains(m.Groups[1].Value);
                }
                bool named = names.All(k => page.Contains("<b>" + words[k] + "</b>")) && words.TryGetValue("reallife_transport_subway", out string journeys) && page.Contains("<b>" + journeys + "</b>")
                             && page.Contains("<b>" + words["reallife_uptown_subway_enabled"] + "</b>");
                Check(bold && named, "subway texts: the Guide page's bold names are the stations', the journeys' option's and its own option's, as " + code + " has them");
            }
            // Uptown's own page sends the reader on, and no longer says there is none.
            if (words.TryGetValue("reallife_guide_uptown", out string uptown) && words.TryGetValue("reallife_meta_title_uptown_subway", out string title))
            {
                Check(uptown.Contains("<b>" + title + "</b>") && (code != "en" || !uptown.Contains("There is no subway station of their own")),
                    "subway texts: Uptown's Guide page points to the stations' page by its title in " + code);
            }
            if (row != null)
            {
                bool told = words.TryGetValue(row, out string news) && news.IndexOf(subwayWord[code], StringComparison.OrdinalIgnoreCase) >= 0
                            && (news.Contains("Birchwood") || news.Contains("伯奇伍德"));
                Check(told, "subway texts: What's new has the stations' line in " + code + " (the row English has it in)");
            }
        }
        Check(english.TryGetValue("reallife_uptown_streets_subway_done", out string old) && old.Contains("{count}") && !old.Contains("no subway station"),
            "subway texts: the districts' older test no longer says they have no station");
        string coverage = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\StandardCheck\guide-coverage.txt")));
        Check(coverage.Contains("subway-uptown\treallife_guide_uptown_subway,reallife_guide_uptown_subway_about\t"), "subway texts: its page of the documentation is on the Guide's coverage list");
    }
}
