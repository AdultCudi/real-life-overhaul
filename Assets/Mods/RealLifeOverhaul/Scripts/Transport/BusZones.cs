using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What a stop's bus zone needs done to the street, found when its bay was laid out (TransitMap.LayBay).</summary>
    public sealed class BusZone
    {
        public float From;          // metres along the stop's lane from the pole where the zone begins (behind the pole: negative)
        public float To;            // ...and where it ends
        public float Kerb;          // metres from the lane's line to the kerb, as the bay was laid out to it
        public readonly List<GameObject> Clear = new List<GameObject>(2);   // street furniture standing in the strip: switched off while the zone is there
        public readonly List<Collider> Known = new List<Collider>(2);      // what reaches over the kerb into the strip and was found no hindrance
        public bool Made;           // the street is as the zone wants it
        public int Lanes;           // parking lanes cut for it, for the log
        public float Cut;           // metres of parking taken out of them
        public float Lost;          // ...of which beyond the zone itself
        public int CarsSent;        // the game's parked cars taken in from it
        public string Waiting;      // why it is not made yet, for the log

        public void Reset()
        {
            From = To = Kerb = Cut = Lost = 0f;
            Clear.Clear();
            Known.Clear();
            Made = false;
            Lanes = CarsSent = 0;
            Waiting = null;
        }

        /// <summary>Drops what was added after the counts given (a longer zone was looked at and would not do).</summary>
        public void Trim(int clear, int known)
        {
            if (Clear.Count > clear)
            {
                Clear.RemoveRange(clear, Clear.Count - clear);
            }
            if (Known.Count > known)
            {
                Known.RemoveRange(known, Known.Count - known);
            }
        }

        public BusZone Copy(float from, float to, float kerb)
        {
            var copy = new BusZone { From = from, To = to, Kerb = kerb };
            copy.Clear.AddRange(Clear);
            copy.Known.AddRange(Known);
            return copy;
        }
    }

    /// <summary>
    /// The bus zones: at every stop with a bay, that length of the kerbside strip is the buses'
    /// alone. Made a few at a time once the network is laid out, never in view, and put back as
    /// the game had it when the buses are switched off or the city is left.
    ///
    /// THREE THINGS ARE DONE TO THE STREET THERE, AND NOTHING ELSE.
    /// The game's parking stops short of it. Each run of kerbside parking is a
    /// <c>ParkingLaneGenerator</c> whose trigger box is the parking: the game lays its parked cars
    /// in that box from one end (<c>GenerateParkedVehicles</c>), puts the player's own cars and its
    /// auto-park spots in it, and counts a car as parked legally only while all its wheels stand in
    /// a box of that layer (<c>CarController.UpdateParkingZone</c>). So the box is shortened to end
    /// where the zone begins, or to begin where it ends, and the game itself then never parks there
    /// again; a player who leaves a car in a bus zone is parked illegally, as on a real street.
    /// The parked cars already standing in the zone are handed back the way the game takes them in
    /// (<c>CleanupParkedVehicles</c>: the lane's own release call, then
    /// <c>ParkingSimulator.ReleaseParkedVehicle</c>), out of view only.
    /// Street furniture standing in the strip itself, on the road's side of the kerb (a bin, a
    /// barricade), is switched off whole. Nothing on the pavement is touched: a tree or a lamp at the
    /// kerb's edge stays, and the bus stands that much further out (TransitMap.KerbLine).
    /// The zone is painted (BusStopDressing, RoadPaint): the game's own kind of road paint.
    ///
    /// A stop has a bay only where all of that can be done. A parking lane that is not a plain run
    /// along the kerb (angled bays, a curve, a delivery van's spot, disabled parking, parking kept
    /// for particular vehicles) is never cut; a zone in the middle of a run is laid out only where no
    /// more than <see cref="LossMost"/> metres of parking beyond it are given up (BusBay.Keep).
    /// </summary>
    public static class BusZones
    {
        /// <summary>
        /// Metres of parking beyond a zone that may be given up with it (a zone in the middle of a run keeps the longer part):
        /// some four spaces. The zone itself is some 48 m (the bus's own: BusBay.Lay).
        /// </summary>
        public const float LossMost = 20f;

        private const float Margin = 1f;          // metres of strip kept clear beyond each end of a zone: no bumper over its line
        private const float BoxLeast = 0.02f;     // what is left of a parking box wholly in a zone, at its end
        private const float LaneAngle = 0.995f;   // a parking lane runs along the road to within some 6 degrees, or it is not a run along the kerb
        private const double Slice = 0.002;       // seconds of zone making in one slice of the director's timer

        public static IModLogger Logger;

        private sealed class Original
        {
            public BoxCollider Box;
            public Vector3 Centre;
            public Vector3 Size;
            public int Override;
        }

        // One parking lane beside a zone: where its box lies along the stop's lane, in metres from the pole.
        private struct Run
        {
            public ParkingLaneGenerator Lane;
            public BoxCollider Box;
            public float Start, End;
            public float Middle;       // its box's middle
            public float PerLocal;     // metres along the stop's lane for each unit along the box's own length
        }

        // A parking lane's private settings, resolved once (Core/GameContract): null when a game update took one away.
        private static readonly FieldInfo OverrideField = GameContract.LaneSpotCount.Field;
        private static readonly FieldInfo SideField = GameContract.LaneSideBySide.Field;
        private static readonly FieldInfo CurvedField = GameContract.LaneCurved.Field;
        private static readonly FieldInfo SpecificField = GameContract.LaneVehicles.Field;
        private static readonly FieldInfo SpotField = GameContract.LaneSpotDistance.Field;

        private static readonly Dictionary<ParkingLaneGenerator, Original> Originals = new Dictionary<ParkingLaneGenerator, Original>(32);
        private static readonly Dictionary<ParkingLaneGenerator, string> Verdicts = new Dictionary<ParkingLaneGenerator, string>(64);
        private static readonly List<GameObject> Hidden = new List<GameObject>(16);
        private static readonly List<TransitStop> Pending = new List<TransitStop>(64);
        private static readonly Collider[] Found = new Collider[32];
        private static readonly Run[] Runs = new Run[8];
        private static readonly List<Transform> Cars = new List<Transform>(16);
        private static readonly List<Component> Parts = new List<Component>(32);
        private static int _parkingMask;
        private static bool _queued;
        private static bool _reported;
        private static float _queuedAt;

        // Scripts the game puts on plain scenery, which may be switched off with it.
        private static readonly HashSet<string> Scenery = new HashSet<string>
        {
            "TreeController", "NavMeshModifier", "NavMeshObstacle", "HDAdditionalLightData", "CityMapHider", "StepSound", "DirtSpotObject"
        };

        private static int ParkingMask
        {
            get
            {
                if (_parkingMask == 0)
                {
                    _parkingMask = LayerMask.GetMask("ParkingArea");
                }
                return _parkingMask;
            }
        }

        // ---------------------------------------------------------------- laying out (TransitMap.LayBay)

        /// <summary>
        /// Whether the parking along a zone can be cut for it: null if so (or there is none), else why not, in a few words
        /// for the log. The zone runs from <paramref name="from"/> to <paramref name="to"/> metres along the stop's lane
        /// from the pole, across the strip to the kerb. Nothing is changed here: the cut is made with the zone (Step).
        /// </summary>
        public static string Plan(TransitStop stop, Vector3 right, float from, float to, BusZone zone)
        {
            int runs = Beside(stop, right, from - Margin, to + Margin, out string why);
            if (why != null)
            {
                return why;
            }
            float cut = 0f;
            float lost = 0f;
            for (int i = 0; i < runs; i++)
            {
                if (BusBay.Keep(Runs[i].Start, Runs[i].End, from - Margin, to + Margin, out float keepStart, out float keepEnd, out float beyond))
                {
                    if (beyond > LossMost)
                    {
                        return "the zone would take " + beyond.ToString("F0") + " m of parking beyond it, in the middle of a run";
                    }
                    cut += Runs[i].End - Runs[i].Start - (keepEnd - keepStart);
                    lost += beyond;
                }
            }
            zone.Lanes = runs;
            zone.Cut = cut;
            zone.Lost = lost;
            return null;
        }

        // The parking lanes whose boxes reach into a stretch of the strip beside a stop: their runs in Runs, how many.
        // Why not, if one of them is not a plain run along the kerb (it is then never cut).
        private static int Beside(TransitStop stop, Vector3 right, float from, float to, out string why)
        {
            why = null;
            Vector3 along = stop.Along;
            float inner = BusBay.LaneHalf - 0.3f;   // a parking box may start a little inside the lane's edge
            float outer = stop.KerbAside + 0.3f;
            Vector3 centre = stop.Lane + along * ((from + to) * 0.5f) + right * ((inner + outer) * 0.5f);
            int found = Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - inner) * 0.5f, 1.5f, (to - from) * 0.5f), Found, Quaternion.LookRotation(along),
                ParkingMask, QueryTriggerInteraction.Collide);
            int runs = 0;
            for (int i = 0; i < found; i++)
            {
                Collider hit = Found[i];
                Found[i] = null;
                ParkingLaneGenerator lane = hit != null ? hit.GetComponentInParent<ParkingLaneGenerator>() : null;
                if (lane == null || why != null)
                {
                    continue;   // (parking of no lane is not legal parking to the game either: nothing to cut)
                }
                why = Verdict(lane);
                if (why != null || hit.gameObject != lane.gameObject || !(hit is BoxCollider box))
                {
                    continue;   // (another part of a plain lane: a reserved spot, an auto-park spot; its own box is what counts)
                }
                bool counted = false;
                for (int k = 0; k < runs; k++)
                {
                    counted |= Runs[k].Lane == lane;
                }
                if (counted || runs >= Runs.Length)
                {
                    continue;
                }
                Transform frame = lane.transform;
                Vector3 axis = frame.forward;
                axis.y = 0f;
                float flat = axis.magnitude;
                float dot = flat > 0.9f ? Vector3.Dot(axis / flat, along) : 0f;
                if (Mathf.Abs(dot) < LaneAngle)
                {
                    why = "a parking lane at an angle to the road";
                    continue;
                }
                float perLocal = frame.lossyScale.z * Vector3.Dot(axis, along);   // (the box's own length runs a little shorter along a street that slopes)
                float middle = Vector3.Dot(Flat(frame.TransformPoint(box.center) - stop.Lane), along);
                float half = Mathf.Abs(box.size.z * perLocal) * 0.5f;
                Runs[runs++] = new Run { Lane = lane, Box = box, Start = middle - half, End = middle + half, Middle = middle, PerLocal = perLocal };
            }
            return runs;
        }

        // Whether a parking lane is a plain run along the kerb, which may be cut: null if so, else why not. Asked of each
        // lane once (its settings are the game's, from the scene).
        private static string Verdict(ParkingLaneGenerator lane)
        {
            if (Verdicts.TryGetValue(lane, out string verdict))
            {
                return verdict;
            }
            try
            {
                if (OverrideField == null || SideField == null || CurvedField == null || SpecificField == null || SpotField == null)
                {
                    verdict = "a parking lane this build cannot read (the game's ParkingLaneGenerator has changed)";
                }
                else if ((bool)SideField.GetValue(lane))
                {
                    verdict = "a parking lane of angled bays";
                }
                else if ((bool)CurvedField.GetValue(lane))
                {
                    verdict = "a curved parking lane";
                }
                else if (lane.designatedDeliveryVanSpot)
                {
                    verdict = "a delivery van's spot";
                }
                else if (lane.isHandicapParking)
                {
                    verdict = "disabled parking";
                }
                else if (SpecificField.GetValue(lane) is string[] specific && specific.Length > 0)
                {
                    verdict = "parking kept for particular vehicles";
                }
                else if (!lane.TryGetComponent(out BoxCollider _))
                {
                    verdict = "a parking lane with no box";
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                verdict = "a parking lane that could not be read";
            }
            Verdicts[lane] = verdict;
            return verdict;
        }

        /// <summary>
        /// The piece of street furniture a collider belongs to, to be switched off whole: up from the collider while what
        /// is above it is the same thing (it stands where this does and holds nothing else solid), never a row of them.
        /// Null if it has more to it than models, colliders and the scripts the game puts on scenery (a door, a machine).
        /// </summary>
        public static GameObject PropOf(Collider collider)
        {
            Transform piece = collider.transform;
            for (int up = 0; up < 3; up++)
            {
                Transform above = piece.parent;
                if (above == null || above.childCount > 8 || CitySpots.Flat(above.position - piece.position) > 2.5f * 2.5f || HoldsMore(above, piece))
                {
                    break;
                }
                piece = above;
            }
            return Plain(piece) ? piece.gameObject : null;
        }

        private static bool HoldsMore(Transform above, Transform piece)
        {
            for (int i = 0; i < above.childCount; i++)
            {
                Transform child = above.GetChild(i);
                if (child != piece && child.GetComponentInChildren<Collider>(true) != null)
                {
                    return true;
                }
            }
            return false;
        }

        // Nothing but models, colliders and the scripts the game puts on scenery. Not a piece that switches itself back on:
        // the game's CityMapHider does, as the city map closes and as the player comes out of a building (one further down
        // switches only its own part, which stays hidden with the piece).
        private static bool Plain(Transform prop)
        {
            prop.GetComponentsInChildren(true, Parts);
            bool plain = prop.GetComponent<CityMapHider>() == null;
            for (int i = 0; i < Parts.Count && plain; i++)
            {
                Component part = Parts[i];
                plain = part == null || part is Transform || part is MeshFilter || part is Renderer || part is Collider || part is LODGroup || part is Light
                        || part is ParticleSystem || (!(part is Rigidbody) && Scenery.Contains(part.GetType().Name));
            }
            Parts.Clear();
            return plain;
        }

        // ---------------------------------------------------------------- making them (the director's timer)

        /// <summary>
        /// Makes the zones still to be made, a slice at a time and none while it is in view. Call from the director's
        /// timer while the buses are on and the network is laid out.
        /// </summary>
        public static void Step()
        {
            if (!TransitMap.Ready)
            {
                return;
            }
            if (!_queued)
            {
                _queued = true;
                _reported = false;
                _queuedAt = Time.unscaledTime;
                Pending.Clear();
                for (int i = 0; i < TransitMap.Stops.Count; i++)
                {
                    TransitStop stop = TransitMap.Stops[i];
                    if (stop.HasBay && stop.Zone != null && !stop.Zone.Made)
                    {
                        Pending.Add(stop);
                    }
                }
            }
            if (Pending.Count > 0)
            {
                long until = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(Slice * System.Diagnostics.Stopwatch.Frequency);
                for (int i = Pending.Count - 1; i >= 0 && System.Diagnostics.Stopwatch.GetTimestamp() < until; i--)
                {
                    if (Make(Pending[i]))
                    {
                        Pending.RemoveAt(i);
                    }
                }
            }
            // One line when they are all made, or after a minute with some still waiting to be out of view.
            if (!_reported && (Pending.Count == 0 || Time.unscaledTime - _queuedAt > 60f))
            {
                _reported = true;
                Report();
            }
        }

        // Makes one stop's zone. False if it must wait (some of it is in view); it is then asked again on a later slice.
        private static bool Make(TransitStop stop)
        {
            BusZone zone = stop.Zone;
            if (zone.Made)
            {
                return true;
            }
            try
            {
                Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
                Vector3 middle = stop.Lane + stop.Along * ((zone.From + zone.To) * 0.5f) + right * (zone.Kerb * 0.7f);
                if (CitySpots.InView(middle) || CitySpots.InView(middle + stop.Along * (zone.To - zone.From) * 0.4f) || CitySpots.InView(middle - stop.Along * (zone.To - zone.From) * 0.4f))
                {
                    zone.Waiting = "in view";
                    return false;
                }
                for (int i = 0; i < zone.Clear.Count; i++)
                {
                    if (zone.Clear[i] != null && zone.Clear[i].activeSelf && CitySpots.InView(zone.Clear[i].transform.position))
                    {
                        zone.Waiting = "in view";
                        return false;
                    }
                }
                // The parking: every lane beside it now (another stop's zone may have cut one already).
                float from = zone.From - Margin;
                float to = zone.To + Margin;
                int runs = Beside(stop, right, from, to, out string why);
                for (int i = 0; why == null && i < runs; i++)
                {
                    if (BusBay.Keep(Runs[i].Start, Runs[i].End, from, to, out float keepStart, out float keepEnd, out _))
                    {
                        Shorten(Runs[i], keepStart, keepEnd);
                        zone.CarsSent += SendAway(Runs[i], stop, keepStart, keepEnd, from, to);
                    }
                }
                // The furniture standing in the strip.
                for (int i = 0; i < zone.Clear.Count; i++)
                {
                    GameObject prop = zone.Clear[i];
                    if (prop != null && prop.activeSelf)
                    {
                        prop.SetActive(false);
                        Hidden.Add(prop);
                    }
                }
                zone.Made = true;
                zone.Waiting = why;   // (a lane found to be other than plain since the bay was laid out: left as it is, and said)
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                zone.Made = true;     // not tried again every slice
                zone.Waiting = "failed: " + e.GetType().Name;
                return true;
            }
        }

        // Shortens a lane's box to the part along the stop's lane from keepStart to keepEnd, and its fixed count of spots
        // with it where it has one. The box as the game had it is kept, once, to be put back.
        private static void Shorten(Run run, float keepStart, float keepEnd)
        {
            BoxCollider box = run.Box;
            ParkingLaneGenerator lane = run.Lane;
            if (!Originals.ContainsKey(lane))
            {
                Originals[lane] = new Original { Box = box, Centre = box.center, Size = box.size, Override = (int)OverrideField.GetValue(lane) };
            }
            float per = Mathf.Abs(run.PerLocal) > 0.0001f ? run.PerLocal : 1f;
            float near = box.center.z + (keepStart - run.Middle) / per;
            float far = box.center.z + (keepEnd - run.Middle) / per;
            Vector3 centre = box.center;
            Vector3 size = box.size;
            centre.z = (near + far) * 0.5f;
            size.z = Mathf.Max(BoxLeast, Mathf.Abs(far - near));
            box.center = centre;
            box.size = size;
            int spots = (int)OverrideField.GetValue(lane);
            if (spots > 0)
            {
                // A fixed count of spots is laid from the box's middle: no more of them than the box now holds.
                float spot = Mathf.Max(0.1f, (float)SpotField.GetValue(lane));
                OverrideField.SetValue(lane, Mathf.Min(spots, Mathf.FloorToInt(size.z / spot)));
            }
            ParkingLaneGenerator.RegenerateAutoParkSpotsNear(lane.transform.TransformPoint(centre));
        }

        // The game's parked cars of a lane that stand in a zone or beyond what is left of the lane: handed back to the
        // game's store of them, as the lane does itself, where nobody is looking. How many.
        private static int SendAway(Run run, TransitStop stop, float keepStart, float keepEnd, float from, float to)
        {
            ParkingLaneGenerator lane = run.Lane;
            Transform frame = lane.transform;
            Cars.Clear();
            for (int i = 0; i < frame.childCount; i++)
            {
                Transform child = frame.GetChild(i);
                if (child.gameObject.layer == LayerHelper.ParkedVehiclesLayerIndex && child.gameObject.activeSelf)
                {
                    Cars.Add(child);
                }
            }
            int sent = 0;
            for (int i = 0; i < Cars.Count; i++)
            {
                Transform car = Cars[i];
                MeshCollider body = car.GetComponent<MeshCollider>();
                float half = body != null && body.sharedMesh != null ? body.sharedMesh.bounds.extents.z : 2.5f;
                float at = Vector3.Dot(Flat(car.position - stop.Lane), stop.Along);
                bool inZone = at + half > from && at - half < to;
                bool beyond = at - half < keepStart - 0.3f || at + half > keepEnd + 0.3f;
                if ((inZone || beyond) && !CitySpots.InView(car.position))
                {
                    lane.onReleaseVehicle?.Invoke(car.gameObject);
                    ParkingSimulator.ReleaseParkedVehicle(car.gameObject);
                    sent++;
                }
            }
            Cars.Clear();
            return sent;
        }

        // ---------------------------------------------------------------- putting the street back

        /// <summary>
        /// Puts the street back as the game had it: each lane's box and count of spots, the furniture. The zones are made
        /// again by <see cref="Step"/> when the buses next run. While the city is being unloaded nothing is touched.
        /// </summary>
        public static void Restore()
        {
            if (!GameManager.isCitySceneBeingUnloaded)
            {
                foreach (KeyValuePair<ParkingLaneGenerator, Original> pair in Originals)
                {
                    ParkingLaneGenerator lane = pair.Key;
                    Original original = pair.Value;
                    if (lane == null || original.Box == null)
                    {
                        continue;
                    }
                    try
                    {
                        original.Box.center = original.Centre;
                        original.Box.size = original.Size;
                        OverrideField?.SetValue(lane, original.Override);
                        ParkingLaneGenerator.RegenerateAutoParkSpotsNear(lane.transform.TransformPoint(original.Centre));
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                    }
                }
                for (int i = 0; i < Hidden.Count; i++)
                {
                    if (Hidden[i] != null)
                    {
                        Hidden[i].SetActive(true);
                    }
                }
            }
            Originals.Clear();
            Verdicts.Clear();
            Hidden.Clear();
            Pending.Clear();
            _queued = false;
            for (int i = 0; i < TransitMap.Stops.Count; i++)
            {
                BusZone zone = TransitMap.Stops[i].Zone;
                if (zone != null)
                {
                    zone.Made = false;
                    zone.CarsSent = 0;
                    zone.Waiting = null;
                }
            }
        }

        // ---------------------------------------------------------------- the log

        /// <summary>What was done for a stop's zone, in words for a log line.</summary>
        public static string Describe(BusZone zone)
        {
            if (zone == null)
            {
                return "no zone";
            }
            if (!zone.Made)
            {
                return "its zone not made yet (" + (zone.Waiting ?? "the network is still being laid out") + ")";
            }
            return "its zone made: " + (zone.Lanes > 0 ? zone.Cut.ToString("F0") + " m of parking cut from " + zone.Lanes + " lane(s)" + (zone.Lost > 0.5f ? " (" + zone.Lost.ToString("F0") + " of it beyond the zone)" : "") : "no parking beside it")
                   + ", " + zone.Clear.Count + " piece(s) of street furniture switched off, " + zone.CarsSent + " parked car(s) taken in"
                   + (zone.Known.Count > 0 ? ", " + zone.Known.Count + " street tree(s) at the kerb left standing" : "") + (zone.Waiting != null ? " (" + zone.Waiting + ")" : "");
        }

        private static void Report()
        {
            int zones = 0, made = 0, lanes = 0, furniture = 0, cars = 0, trees = 0;
            float cut = 0f, lost = 0f;
            for (int i = 0; i < TransitMap.Stops.Count; i++)
            {
                BusZone zone = TransitMap.Stops[i].Zone;
                if (zone == null || !TransitMap.Stops[i].HasBay)
                {
                    continue;
                }
                zones++;
                if (!zone.Made)
                {
                    continue;
                }
                made++;
                lanes += zone.Lanes;
                cut += zone.Cut;
                lost += zone.Lost;
                furniture += zone.Clear.Count;
                cars += zone.CarsSent;
                trees += zone.Known.Count;
            }
            Logger?.Info("Transit check (zones): " + made + " of " + zones + " bus zones made: " + cut.ToString("F0") + " m of the game's parking cut from " + lanes + " lane(s) ("
                         + lost.ToString("F0") + " m of it beyond the zones), " + furniture + " piece(s) of street furniture switched off, " + cars + " parked car(s) taken in, "
                         + trees + " street tree(s) at the kerb left standing; " + (made == zones ? "PASS" : (zones - made) + " still waiting to be out of view") + ".");
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
