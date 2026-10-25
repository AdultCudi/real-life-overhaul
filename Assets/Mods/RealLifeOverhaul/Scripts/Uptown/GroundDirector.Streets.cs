using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's streets as its buses and pavements use them, at the load: the street furniture laid with the art, behind
    /// the loading screen and before the walking ground grows round it (StreetsGame), and the district's two bus routes
    /// handed to the bus network after its termini (StreetsRules.Routes, TransitMap.AddRoute). Its three tests under
    /// Options > Tests read and count, none changes the city: the buses' reach over every block, the subway's answer, and
    /// the furniture of every street; each writes one "Uptown check (...)" line with Developer diagnostics on.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const string ReachTest = "Uptown: bus reach";
        private const string SubwayTest = "Uptown: the subway";
        private const string FurnitureTest = "Uptown: street furniture";
        private const float StationNear = 60f;    // m from a subway entrance the stop beside it should stand
        private const float StationFind = 30f;    // m from its place in Build 3682 a station is looked for

        private int _furnished;
        private double _furnishMs;

        // With the art, before the ground grows: what of it stands on the pavements is walked round.
        private void Furnish()
        {
            long mark = Stopwatch.GetTimestamp();
            _furnished = StreetsGame.Furnish(GroundGame.ArtRoot);
            _furnishMs = Ms(mark);
        }

        // After the termini: the district's routes, laid by the bus network at this load behind the loading screen.
        private static void AddRoutes()
        {
            var routes = new List<TransitPlan.Route>(4);
            StreetsRules.Routes(routes);
            for (int i = 0; i + 1 < routes.Count; i += 2)
            {
                TransitMap.AddRoute(routes[i], routes[i + 1]);
            }
        }

        private string FurnitureWords()
        {
            return _furnished + " pieces of street furniture of " + StreetsGame.Planned + " planned (" + StreetsGame.Occupied + " places where something of the game's stood, "
                   + StreetsGame.Mouths + " in a big place's mouth, " + StreetsGame.OffStops + " kept off the bus stops"
                   + (StreetsGame.Missing != null ? "; not found: " + StreetsGame.Missing : string.Empty)
                   + ") in " + _furnishMs.ToString("F0", Inv) + " ms";
        }

        public static void TestReach()
        {
            if (Instance == null)
            {
                TestRefusals.Tell(Logger, ReachTest, TestRefusal.NotLoaded);
                return;
            }
            Instance.Reach();
        }

        public static void TestSubway()
        {
            if (Instance == null)
            {
                TestRefusals.Tell(Logger, SubwayTest, TestRefusal.NotLoaded);
                return;
            }
            Instance.Subway();
        }

        public static void TestFurniture()
        {
            if (Instance == null)
            {
                TestRefusals.Tell(Logger, FurnitureTest, TestRefusal.NotLoaded);
                return;
            }
            Instance.Furniture();
        }

        // "Uptown: bus reach": the bus places against every block of the band, its far corners included.
        private void Reach()
        {
            if (!UptownLots.Ready)
            {
                TestRefusals.TellKey(Logger, ReachTest, GroundText.NotLaid, UptownLots.Why);
                return;
            }
            if (!Settings.Buses || !TransitMap.Ready || TransitMap.Places.Count == 0)
            {
                TestRefusals.Tell(Logger, ReachTest, Settings.Buses ? TestRefusal.NoBusLine : TestRefusal.FeatureOff);
                return;
            }
            var x = new List<float>(TransitMap.Places.Count);
            var z = new List<float>(TransitMap.Places.Count);
            for (int i = 0; i < TransitMap.Places.Count; i++)
            {
                x.Add(TransitMap.Places[i].Sign.x);
                z.Add(TransitMap.Places[i].Sign.z);
            }
            var blocks = new List<StreetsRules.BlockReach>(LayoutRules.Rows * LayoutRules.Columns);
            float farthest = StreetsRules.Reach(x, z, blocks);
            StreetsRules.BlockReach worst = blocks[0];
            int fewest = int.MaxValue, inBand = 0;
            var rows = new StringBuilder(512);
            for (int i = 0; i < blocks.Count; i++)
            {
                StreetsRules.BlockReach b = blocks[i];
                if (b.Farthest >= worst.Farthest)
                {
                    worst = b;
                }
                fewest = Mathf.Min(fewest, b.Stops);
                rows.Append(b.Column == 1 ? (b.Row == 1 ? "row 1:" : "; row " + b.Row + ":") : string.Empty).Append(' ').Append(b.Stops);
            }
            for (int i = 0; i < x.Count; i++)
            {
                inBand += LayoutRules.OnGround(x[i], z[i]) ? 1 : 0;
            }
            TransitMap.OwnCounts(out int routes, out int wanted, out int laid, out int missed);
            bool pass = farthest <= StreetsRules.ReachWanted && missed == 0 && fewest > 0;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Uptown check (bus reach): " + TransitMap.Places.Count + " bus places in the city, " + inBand + " on the district's ground; the district's " + routes
                             + " routes both ways wanted " + wanted + " stops, laid " + laid + ", missed " + missed + "; the farthest corner of any block from its nearest stop "
                             + Metres(farthest) + " m (row " + worst.Row + ", block " + worst.Column + "; wanted " + Metres(StreetsRules.ReachWanted) + "), stops within reach of each block's middle, "
                             + "west to east: " + rows + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
            Notice(StreetsText.ReachDone, new Dictionary<string, string>(4)
            {
                { "places", inBand.ToString(Inv) },
                { "farthest", Metres(farthest) },
                { "row", worst.Row.ToString(Inv) },
                { "column", worst.Column.ToString(Inv) }
            });
        }

        // "Uptown: the subway": why the district has no station of its own, and the stops beside the two entrances nearest it.
        private void Subway()
        {
            if (!Settings.Buses || !TransitMap.Ready || TransitMap.Places.Count == 0)
            {
                TestRefusals.Tell(Logger, SubwayTest, Settings.Buses ? TestRefusal.NoBusLine : TestRefusal.FeatureOff);
                return;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            SubwayStation west = Station(city, StreetsRules.NorthWestStationX, StreetsRules.NorthWestStationZ);
            SubwayStation east = Station(city, StreetsRules.EastStationX, StreetsRules.EastStationZ);
            float westAt = Beside(west, out string westLines), eastAt = Beside(east, out string eastLines);
            bool pass = west != null && east != null && westAt <= StationNear && eastAt <= StationNear;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Uptown check (subway): no station of the district's own: a station's name is its SubwayStation.stationName, one of the game's fixed list "
                             + "(SubwayStationName), so a copy of an entrance is listed, shown over the street and named for a taxi as the station it was copied from, and a new value "
                             + "throws in the game's own EnumHelpers.ToStringFast, which the travel list sorts by; the buses instead: "
                             + (west != null ? west.stationName + " " + StopWords(westAt, westLines) : "Midtown North West Station not found") + "; "
                             + (east != null ? east.stationName + " " + StopWords(eastAt, eastLines) : "the station by 2nd Street not found") + " (wanted within " + Metres(StationNear)
                             + " m): " + (pass ? "PASS" : "FAIL") + ".");
            }
            Notice(StreetsText.SubwayDone, new Dictionary<string, string>(4)
            {
                { "first", west != null ? Metres(westAt) : "?" },
                { "station1", west != null ? GameMembers.NameKey(west).GetLocalization() : "?" },
                { "second", east != null ? Metres(eastAt) : "?" },
                { "station2", east != null ? GameMembers.NameKey(east).GetLocalization() : "?" }
            });
        }

        private static SubwayStation Station(CityManager city, float x, float z)
        {
            if (city == null || city.subwayStations == null)
            {
                return null;
            }
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                SubwayStation station = city.subwayStations[i];
                if (station != null && CitySpots.Flat(station.transform.position - new Vector3(x, 0f, z)) <= StationFind * StationFind)
                {
                    return station;
                }
            }
            return null;
        }

        // The bus place nearest an entrance: metres to its pole and the lines that call there.
        private static float Beside(SubwayStation station, out string lines)
        {
            lines = string.Empty;
            if (station == null)
            {
                return float.MaxValue;
            }
            TransitStop place = TransitMap.Nearest(station.transform.position, 400f);
            if (place == null)
            {
                return float.MaxValue;
            }
            lines = TransitMap.Numbers(place);
            return Mathf.Sqrt(CitySpots.Flat(place.Sign - station.transform.position));
        }

        private static string StopWords(float metres, string lines)
        {
            return metres < float.MaxValue ? "has a stop " + Metres(metres) + " m away (" + lines + ")" : "has no stop within 400 m";
        }

        // "Uptown: street furniture": what stands on each street's pavements against the plan.
        private void Furniture()
        {
            if (!UptownLots.Ready)
            {
                TestRefusals.TellKey(Logger, FurnitureTest, GroundText.NotLaid, UptownLots.Why);
                return;
            }
            int benches = 0, bins = 0, trees = 0, racks = 0, most = -1, mostCount = -1;
            var streets = new StringBuilder(1024);
            for (int s = 0; s <= StreetsRules.FirstAvenue; s++)
            {
                int b = StreetsGame.Count(s, StreetPiece.Bench), n = StreetsGame.Count(s, StreetPiece.Bin), t = StreetsGame.Count(s, StreetPiece.Tree), r = StreetsGame.Count(s, StreetPiece.BikeRack);
                benches += b;
                bins += n;
                trees += t;
                racks += r;
                if (b + n + t + r > mostCount && s < StreetsRules.FirstAvenue)
                {
                    mostCount = b + n + t + r;
                    most = s;
                }
                streets.Append(s > 0 ? "; " : string.Empty).Append(s < StreetsRules.FirstAvenue ? LayoutStreets.All[s].Abbreviation : "1st Avenue").Append(' ')
                       .Append(b).Append('/').Append(n).Append('/').Append(t).Append('/').Append(r);
            }
            bool pass = StreetsGame.Missing == null && StreetsGame.Laid > 0 && StreetsGame.Laid + StreetsGame.Occupied + StreetsGame.Mouths == StreetsGame.Planned;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Uptown check (street furniture): " + FurnitureWords() + "; benches " + benches + ", bins " + bins + ", trees " + trees + ", bicycle docks " + racks
                             + "; by street (benches/bins/trees/docks): " + streets + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
            Notice(StreetsText.FurnitureDone, new Dictionary<string, string>(5)
            {
                { "benches", benches.ToString(Inv) },
                { "bins", bins.ToString(Inv) },
                { "trees", trees.ToString(Inv) },
                { "racks", racks.ToString(Inv) },
                { "street", most >= 0 ? LayoutStreets.All[most].NameKey.GetLocalization() : "?" }
            });
        }
    }
}
