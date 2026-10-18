using System.Globalization;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The bus network's part for its lines in the log: the network, the stations served, and where the poles stand.</summary>
    public static partial class TransitMap
    {
        // The network in the log: every line, how many lines each place and each station has, and the changes of bus.
        private static void Report()
        {
            int bothWays = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                for (int k = 0; k < Lines.Count; k++)
                {
                    if (k != i && Lines[k].Number == Lines[i].Number)
                    {
                        bothWays++;
                        break;
                    }
                }
            }
            int[] byLines = new int[6];
            int transfers = 0;
            for (int i = 0; i < Places.Count; i++)
            {
                byLines[Mathf.Min(5, Places[i].Calls.Count)]++;
                transfers += Places[i].Calls.Count >= 2 || Places[i].Near.Count > 0 ? 1 : 0;
            }
            var station = new StringBuilder(256);
            int stationsServed = 0;
            int stationsThree = 0;
            for (int s = 0; s < Stations.Count; s++)
            {
                int serving = StationLines(Stations[s].transform.position);
                stationsServed += serving > 0 ? 1 : 0;
                stationsThree += serving >= 3 ? 1 : 0;
                station.Append(' ').Append(Stations[s].stationName).Append(' ').Append(serving).Append(';');
            }
            var text = new StringBuilder("Transit check (network): " + Lines.Count + " bus lines (" + _routes + " routes, " + bothWays
                                         + " of the lines with their other direction), " + Stops.Count + " stops at " + Places.Count + " places, " + Bays
                                         + " of the places with a bay (the bus stands out of the lane there; a bay wants the kerb "
                                         + BusBay.KerbNeeded(TransitBus.BodyHalfWidth).ToString("F2", CultureInfo.InvariantCulture) + " m or more from the lane's line), "
                                         + _shared + " stops shared with a line laid before; places served by 1 line " + byLines[1] + ", by 2 " + byLines[2] + ", by 3 "
                                         + byLines[3] + ", by 4 or more " + (byLines[4] + byLines[5]) + "; " + transfers
                                         + " places where a change of bus can be made (another line there or within " + TransferWalk.ToString("F0")
                                         + " m); subway stations with a line within " + TransitPlan.ServedWithin.ToString("F0") + " m " + stationsServed + " of "
                                         + Stations.Count + ", with 3 or more " + stationsThree + " (" + station + "); " + _searches + " searches of "
                                         + (_points != null ? _points.Length : 0) + " lane points (" + _laneChanges + " lane changes offered), " + _refused
                                         + " pairs refused: " + (bothWays == Lines.Count ? "PASS" : "FAIL (a line runs one way only)") + ".");
            for (int i = 0; i < Lines.Count; i++)
            {
                TransitLine line = Lines[i];
                int sheltered = 0;
                int bays = 0;
                int shared = 0;
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    sheltered += line.Stops[s].Shelter ? 1 : 0;
                    bays += line.Stops[s].HasBay ? 1 : 0;
                    shared += line.Stops[s].At.Calls.Count > 1 ? 1 : 0;
                }
                text.Append(" ").Append(line.Number).Append(line.Main ? " (main)" : string.Empty).Append(" to ").Append(line.To).Append(' ')
                    .Append((line.Metres[line.Metres.Length - 1] / 1000f).ToString("F1", CultureInfo.InvariantCulture)).Append(" km ")
                    .Append(line.Stops.Count).Append(" stops (").Append(shared).Append(" shared with another line, ").Append(sheltered).Append(" with a shelter, ")
                    .Append(bays).Append(" with a bay; the kerb at");
                // Each stop's kerb, and in a word why a stop has no bay.
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    TransitStop stop = line.Stops[s];
                    text.Append(' ').Append(stop.KerbAside.ToString("F2", CultureInfo.InvariantCulture));
                    if (!stop.HasBay)
                    {
                        text.Append(" [").Append(stop.NoBay ?? "no bay").Append(']');
                    }
                }
                text.Append(" m);");
            }
            Logger?.Info(text.ToString());
            if (Notes.Length > 0)
            {
                Logger?.Info("Transit: the pairs of stations not joined:" + Notes);
                Notes.Length = 0;
            }
            LogPoles();
        }

        // How many routes have a stop within reach of a station.
        private static int StationLines(Vector3 at)
        {
            int count = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                bool earlier = false;
                for (int k = 0; k < i && !earlier; k++)
                {
                    earlier = Lines[k].Number == Lines[i].Number;
                }
                if (earlier)
                {
                    continue;
                }
                bool near = false;
                for (int k = i; k < Lines.Count && !near; k++)
                {
                    if (Lines[k].Number != Lines[i].Number)
                    {
                        continue;
                    }
                    for (int s = 0; s < Lines[k].Stops.Count && !near; s++)
                    {
                        near = CitySpots.Flat(Lines[k].Stops[s].Sign - at) < (TransitPlan.ServedWithin + 30f) * (TransitPlan.ServedWithin + 30f);
                    }
                }
                count += near ? 1 : 0;
            }
            return count;
        }

        // Where the poles stand and the buses stop by them, in one line.
        private static void LogPoles()
        {
            int clear = 0, shifted = 0, things = 0;
            for (int i = 0; i < Stops.Count; i++)
            {
                TransitStop stop = Stops[i];
                Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
                Vector3 toWait = Flat(stop.Wait - stop.Lane), toPole = Flat(stop.Sign - stop.Lane);
                float kerb = Vector3.Dot(Flat(stop.Kerb - stop.Lane), right);
                clear += BusInside.SignClear(Vector3.Dot(toPole, stop.Along), Vector3.Dot(toPole, right), kerb, Vector3.Dot(toWait, stop.Along), Vector3.Dot(toWait, right), stop.Stand) ? 1 : 0;
                shifted += Mathf.Abs(stop.Stand) > 0.01f ? 1 : 0;
                things += stop.KerbThings;
            }
            Logger?.Info("Transit check (poles): " + clear + " of " + Stops.Count + " stops' poles stand clear of both doors and the ways to them; the bus stands on from its pole or short of it at "
                         + shifted + " stop(s) so that neither door lines up with what stays on the kerb (" + things + " thing(s) in all, the poles counted): "
                         + (clear == Stops.Count ? "PASS" : "FAIL") + ".");
        }
    }
}
