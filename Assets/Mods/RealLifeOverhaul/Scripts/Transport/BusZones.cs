using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>What a stop's bus zone needs done to the street, found when its bay was laid out (TransitMap.LayBay).</summary>
    public sealed class BusZone
    {
        public float From;          // metres along the stop's lane from the pole where the zone begins (behind the pole: negative)
        public float To;            // ...and where it ends
        public float Kerb;          // metres from the lane's line to the kerb, as the bay was laid out to it
        public readonly List<GameObject> Clear = new List<GameObject>(2);   // street furniture standing in the strip: switched off while the zone is there
        public readonly List<GameObject> Meters = new List<GameObject>(2);  // the game's parking meters along the bay's kerb: no parking is left for them
        public readonly List<Collider> Known = new List<Collider>(2);      // what reaches over the kerb into the strip and was found no hindrance
        public bool Made;           // the street is as the zone wants it
        public bool ParkingCut;     // the game's parking along it is cut (at once, in view or not: nobody sees a parking box)
        public int Lanes;           // parking lanes cut for it, for the log
        public float Cut;           // metres of parking taken out of them
        public float Lost;          // ...of which beyond the zone itself
        public int Spots;           // the game's parking spots given up with them
        public int CarsSent;        // the game's parked cars taken in from it
        public int PlayerCars;      // the player's own cars moved from it to a free space nearby
        public string Waiting;      // why it is not made yet, for the log
        public string Note;         // what was found as it was made (a lane no longer plain), for the log

        public void Reset()
        {
            From = To = Kerb = Cut = Lost = 0f;
            Clear.Clear();
            Meters.Clear();
            Known.Clear();
            Made = ParkingCut = false;
            Lanes = Spots = CarsSent = PlayerCars = 0;
            Waiting = Note = null;
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
            copy.Meters.AddRange(Meters);
            copy.Known.AddRange(Known);
            return copy;
        }
    }

    /// <summary>
    /// The bus zones: at every stop with a bay, that length of the kerbside strip is the buses'
    /// alone. Made once the network is laid out, and put back as the game had it when the buses
    /// are switched off or the city is left. Nothing of it is saved.
    ///
    /// WHAT IS DONE TO THE STREET THERE, AND NOTHING ELSE.
    /// The game's parking stops short of it. Each run of kerbside parking is a
    /// <c>ParkingLaneGenerator</c> whose trigger box is the parking: the game lays its parked cars
    /// in that box from one end (<c>GenerateParkedVehicles</c>, at the city's loading for every lane
    /// however near the player is, and again whenever a lane is regenerated), puts the player's own
    /// cars and its auto-park spots in it, and counts a car as parked legally only while all its
    /// wheels stand in a box of that layer (<c>CarController.UpdateParkingZone</c>). So the box is
    /// shortened to end where the zone begins less <see cref="BusBay.CarMargin"/>, or to begin
    /// where it ends and as much more, and the game itself then never parks there again. That is
    /// done at once, in view or not: a parking box is not seen. (It used to wait, with the rest of
    /// the zone, until all of the zone was out of view; a player who came out of doors at a stop
    /// kept the game's parked cars in its bay, and the bus stopped in the lane beside them.)
    /// The game's parked cars standing in the zone or within the margin of it are handed back the
    /// way the game takes them in (<c>CleanupParkedVehicles</c>: the lane's own release call, then
    /// <c>ParkingSimulator.ReleaseParkedVehicle</c>), each as soon as it is out of view: one in view
    /// is looked at again a second later until it can go, and the zone is not made until the bay
    /// is clear. A car of the player's own standing there (never one they are in, nor one that is
    /// moving) is moved the same way, out of view, to the nearest free space the game's auto-park
    /// would offer outside every bus zone, and the player is told once. Nothing driving in traffic
    /// and no bus is touched. Once made, the zones are looked at again one at a time every few
    /// seconds (the game regenerates its lanes every day, and a lane may come back whole), and a
    /// lane or a car that has come back is dealt with the same way.
    /// Street furniture standing in the strip itself, on the road's side of the kerb (a bin, a
    /// barricade), and the game's parking meters along the bay's kerb are switched off whole, each
    /// as soon as it is out of view (<see cref="Hide"/>). Nothing else on the pavement is touched:
    /// a tree or a lamp at the kerb's edge stays, and the bus stands that much further out
    /// (TransitMap.KerbLine).
    /// The zone is painted (BusStopDressing, RoadPaint): the game's own kind of road paint.
    ///
    /// AT EVERY STOP, bay or not, the boarding strip is cleared too: the game's plain street furniture (a pole, a bin, a
    /// bollard, a sign, a hydrant, a plain bench) standing on the kerb from beyond the middle door to beyond the front door, or
    /// on a walkway 1.5 m wide from the waiting place to either door (BusInside.BoardingStrips, TransitMap.FindBoarding), is
    /// switched off whole the same way and put back with the rest. Never a tree, a building, or the mod's own shelter and sign.
    ///
    /// SWITCHED OFF WHOLE MEANS NOTHING OF IT IS LEFT. The game walks everyone on foot on its navigation mesh, so what stops
    /// a walker is not a collider but a hole in the mesh: a carving <c>NavMeshObstacle</c>, or a hole baked into the mesh
    /// with the city. A piece is its whole object (BusZones.PropOf: up from the collider found while what is above is the
    /// same thing, its other parts standing where it does), switched off with all under it: its models, its colliders, its
    /// triggers and its obstacles. An obstacle of the game's that stands in the piece's footprint but hangs elsewhere in the
    /// scene is switched off with it. A moment later the mesh is asked whether the footprint can now be walked; where it
    /// cannot (the hole is baked in), the piece is put back as it was, and stays for as long as the city is loaded: never
    /// an invisible thing that stops the player. Everything is put back exactly when the buses are switched off or the
    /// city is left.
    ///
    /// A stop has a bay only where all of that can be done. A parking lane that is not a plain run
    /// along the kerb (angled bays, a curve, a delivery van's spot, disabled parking, parking kept
    /// for particular vehicles) is never cut; a zone in the middle of a run is laid out only where no
    /// more than <see cref="LossMost"/> metres of parking beyond it are given up (BusBay.Keep).
    ///
    /// All of it runs from the director's timer, a few milliseconds at a time: nothing each frame.
    ///
    /// IN A GOING PUBLIC SESSION (Core/GoingPublic) every machine lays out the same network from the same streets, so every
    /// machine cuts the same parking, switches off the same pieces and puts back the same ones (the mesh is the city's own):
    /// only when each is done differs, by what each player is looking at. The cars taken in are the game's parked cars of
    /// this machine's own lanes, which each game lays for itself; a car of another player's, shown here on the game's list of
    /// the player's cars, is never moved (GoingPublic.MineVehicle), and the player's own is moved only by their own game.
    /// </summary>
    public static class BusZones
    {
        /// <summary>
        /// Metres of parking beyond a zone that may be given up with it (a zone in the middle of a run keeps the longer part):
        /// some four spaces. The zone itself is some 48 m (the bus's own: BusBay.Lay).
        /// </summary>
        public const float LossMost = 20f;

        private const float Margin = BusBay.CarMargin;   // metres of strip kept clear beyond each end of a zone: no bumper over its line
        private const float BoxLeast = 0.02f;     // what is left of a parking box wholly in a zone, at its end
        private const float LaneAngle = 0.995f;   // a parking lane runs along the road to within some 6 degrees, or it is not a run along the kerb
        private const double Slice = 0.002;       // seconds of zone making in one slice of the director's timer
        private const float LookAgain = 1f;       // seconds before a stop with something in view is looked at again
        private const float WatchEvery = 5f;      // seconds between looks at one made zone after another, for what has come back
        private const float ProbeAfter = 2.5f;    // seconds after a piece is switched off that the mesh is asked about its footprint (the cut closes some half a second after)
        private const float ObstacleMost = 2.6f;  // metres across an obstacle of the game's may be, at the most, to go with a piece (TransitMap.BoardingPieceMost)
        private const float Moving = 0.5f;        // m/s: a car of the player's going faster than this is being driven, and is left alone

        public static IModLogger Logger;

        private sealed class Original
        {
            public BoxCollider Box;
            public Vector3 Centre;
            public Vector3 Size;
            public int Override;
        }

        // A piece of street furniture switched off, with the game's obstacles of it that hang elsewhere.
        private sealed class Hid
        {
            public GameObject Piece;
            public readonly List<Behaviour> Off = new List<Behaviour>(1);
            public Vector3 Foot;       // the middle of its footprint, on the ground
            public float Reach;        // half the widest of its footprint
            public float ProbeAt;      // when the walkable ground under it is looked at; never again once it has been
            public string Where;       // the stop, for the log
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

        private static readonly FieldInfo OverrideField = Field("spotCountOverride");
        private static readonly FieldInfo SideField = Field("sideBySideParkingSpots");
        private static readonly FieldInfo CurvedField = Field("curvedLane");
        private static readonly FieldInfo SpecificField = Field("specificVehicles");
        private static readonly FieldInfo SpotField = Field("parkingSpotDistance");

        private static readonly Dictionary<ParkingLaneGenerator, Original> Originals = new Dictionary<ParkingLaneGenerator, Original>(32);
        private static readonly Dictionary<ParkingLaneGenerator, string> Verdicts = new Dictionary<ParkingLaneGenerator, string>(64);
        private static readonly List<Hid> Hidden = new List<Hid>(32);
        private static readonly HashSet<GameObject> Stay = new HashSet<GameObject>();   // pieces whose hole in the mesh is baked in: never switched off again in this city
        private static readonly List<TransitStop> Pending = new List<TransitStop>(64);
        private static readonly Collider[] Found = new Collider[32];
        private static readonly Run[] Runs = new Run[8];
        private static readonly List<Transform> Cars = new List<Transform>(16);
        private static readonly List<Component> Parts = new List<Component>(32);
        private static readonly List<Collider> Colliders = new List<Collider>(16);
        private static NavMeshObstacle[] _obstacles;   // the game's, found once a city when a piece is first switched off
        private static int _parkingMask;
        private static bool _queued;
        private static bool _reported;
        private static bool _told;      // the player has been told a car of theirs was moved from a bay (once a session)
        private static float _queuedAt;
        private static float _nextWatch;
        private static int _watch;
        private static int _putBack;    // pieces put back because their hole in the mesh is baked in
        private static int _carsSince;  // parked cars taken in after the zones were made (they came back)

        // Scripts the game puts on plain scenery, which may be switched off with it.
        private static readonly HashSet<string> Scenery = new HashSet<string>
        {
            "TreeController", "NavMeshModifier", "NavMeshObstacle", "HDAdditionalLightData", "CityMapHider", "StepSound", "DirtSpotObject"
        };

        private static FieldInfo Field(string name)
        {
            return typeof(ParkingLaneGenerator).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        }

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

        /// <summary>A piece of street furniture that must stay as it is: switching it off would leave a hole in the walkable ground nobody could see (it is baked in).</summary>
        public static bool MustStay(GameObject piece)
        {
            return piece != null && Stay.Contains(piece);
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
        /// is above it is the same thing (it stands where this does and holds nothing else solid that stands elsewhere: a
        /// bin's model, its collider and its obstacle in children of their own are one bin), never a row of them.
        /// Null if it has more to it than models, colliders and the scripts the game puts on scenery (a door, a machine).
        /// </summary>
        public static GameObject PropOf(Collider collider)
        {
            Transform piece = collider.transform;
            Bounds own = collider.bounds;
            own.Expand(new Vector3(0.6f, 0.6f, 0.6f));
            for (int up = 0; up < 3; up++)
            {
                Transform above = piece.parent;
                if (above == null || above.childCount > 8 || CitySpots.Flat(above.position - piece.position) > 2.5f * 2.5f || HoldsMore(above, piece, own))
                {
                    break;
                }
                piece = above;
            }
            return Plain(piece) ? piece.gameObject : null;
        }

        // Whether a parent holds something solid besides the piece that stands elsewhere: another piece of furniture. A part
        // whose colliders all stand within the piece's own (a separate collider or trigger of the same bin) is the same thing.
        private static bool HoldsMore(Transform above, Transform piece, Bounds own)
        {
            for (int i = 0; i < above.childCount; i++)
            {
                Transform child = above.GetChild(i);
                if (child == piece)
                {
                    continue;
                }
                child.GetComponentsInChildren(true, Colliders);
                bool more = false;
                for (int k = 0; k < Colliders.Count && !more; k++)
                {
                    Collider part = Colliders[k];
                    // (A collider switched off has no bounds to go by: taken for something else, and the piece stays smaller.)
                    more = part != null && (!part.enabled || !part.gameObject.activeInHierarchy || !WithinFlat(part.bounds.center, own));
                }
                Colliders.Clear();
                if (more)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool WithinFlat(Vector3 point, Bounds box)
        {
            return point.x >= box.min.x && point.x <= box.max.x && point.z >= box.min.z && point.z <= box.max.z && point.y >= box.min.y - 1f && point.y <= box.max.y + 1f;
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
        /// Makes the zones still to be made, a slice at a time, and keeps them made. Call from the director's timer while the
        /// buses are on and the network is laid out.
        /// </summary>
        public static void Step()
        {
            if (!TransitMap.Ready)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (!_queued)
            {
                _queued = true;
                _reported = false;
                _queuedAt = now;
                _nextWatch = now + WatchEvery;
                Pending.Clear();
                for (int i = 0; i < TransitMap.Stops.Count; i++)
                {
                    TransitStop stop = TransitMap.Stops[i];
                    stop.ZoneLook = 0f;
                    if ((stop.HasBay && stop.Zone != null && !stop.Zone.Made) || (stop.Boarding.Count > 0 && !stop.BoardingMade))
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
                    TransitStop stop = Pending[i];
                    if (now < stop.ZoneLook)
                    {
                        continue;
                    }
                    if (Make(stop))
                    {
                        Pending.RemoveAt(i);
                    }
                    else
                    {
                        stop.ZoneLook = now + LookAgain;
                    }
                }
            }
            Probe(now);
            // One line when they are all made, or after a minute with some still waiting to be out of view.
            if (!_reported && (Pending.Count == 0 || now - _queuedAt > 60f))
            {
                _reported = true;
                Report();
            }
            if (_reported && now >= _nextWatch)
            {
                _nextWatch = now + WatchEvery;
                Watch();
            }
        }

        // Makes one stop's zone and clears its boarding strip. False if either must wait (some of it is in view).
        private static bool Make(TransitStop stop)
        {
            bool zone = !stop.HasBay || stop.Zone == null || MakeZone(stop);
            bool boarding = ClearBoarding(stop);
            return zone && boarding;
        }

        // The game's street furniture on the stop's boarding strip (TransitMap.FindBoarding: the kerb by the doors and the ways
        // to them from the waiting place) is switched off whole, as the furniture in a bay's strip is, and put back with it.
        // Each piece once it is out of view.
        private static bool ClearBoarding(TransitStop stop)
        {
            if (stop.BoardingMade || stop.Boarding.Count == 0)
            {
                return true;
            }
            try
            {
                if (!HideAll(stop.Boarding, stop))
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            stop.BoardingMade = true;
            return true;
        }

        // Switches off each of the pieces that is out of view. True when none is left standing that is to go.
        private static bool HideAll(List<GameObject> pieces, TransitStop stop)
        {
            bool all = true;
            for (int i = 0; i < pieces.Count; i++)
            {
                GameObject piece = pieces[i];
                if (piece == null || !piece.activeSelf || Stay.Contains(piece))
                {
                    continue;
                }
                if (CitySpots.InView(piece.transform.position))
                {
                    all = false;
                    continue;
                }
                Hide(piece, stop);
            }
            return all;
        }

        // Makes one stop's zone. False if it must wait (something of it is in view); it is then asked again a second later.
        private static bool MakeZone(TransitStop stop)
        {
            BusZone zone = stop.Zone;
            if (zone.Made)
            {
                return true;
            }
            try
            {
                Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
                // The parking, at once: nobody sees a parking box (the cars in it are another matter: Sweep).
                if (!zone.ParkingCut)
                {
                    float from = zone.From - Margin;
                    float to = zone.To + Margin;
                    int runs = Beside(stop, right, from, to, out string why);
                    for (int i = 0; why == null && i < runs; i++)
                    {
                        if (BusBay.Keep(Runs[i].Start, Runs[i].End, from, to, out float keepStart, out float keepEnd, out _))
                        {
                            zone.Spots += Shorten(Runs[i], keepStart, keepEnd);
                        }
                    }
                    zone.ParkingCut = true;
                    zone.Note = why;   // (a lane found to be other than plain since the bay was laid out: left as it is, and said)
                }
                bool pieces = HideAll(zone.Clear, stop);
                pieces &= HideAll(zone.Meters, stop);
                bool cars = Sweep(stop, right, out int sent);
                zone.CarsSent += sent;
                zone.Made = pieces && cars;
                zone.Waiting = zone.Made ? zone.Note : pieces ? "a car in it is in view" : "in view";
                return zone.Made;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                zone.Made = true;     // not tried again every second
                zone.Waiting = "failed: " + e.GetType().Name;
                return true;
            }
        }

        // The cars in a zone and within the margin of it: the game's parked cars handed back, the player's own moved to a free
        // space, each once it is out of view. A lane that reaches into it again (the game made it afresh) is cut again first.
        // True when none is left there; how many of the game's went, in sent.
        private static bool Sweep(TransitStop stop, Vector3 right, out int sent)
        {
            BusZone zone = stop.Zone;
            sent = 0;
            int left = 0;
            float from = zone.From - Margin;
            float to = zone.To + Margin;
            int runs = Beside(stop, right, from, to, out string why);
            for (int i = 0; i < runs; i++)
            {
                Run run = Runs[i];
                float keepStart = run.Start, keepEnd = run.End;
                if (why == null && Mathf.Min(run.End, to) - Mathf.Max(run.Start, from) > 0.1f && BusBay.Keep(run.Start, run.End, from, to, out keepStart, out keepEnd, out _))
                {
                    zone.Spots += Shorten(run, keepStart, keepEnd);
                }
                sent += SendAway(run, stop, keepStart, keepEnd, ref left);
            }
            zone.PlayerCars += MovePlayerCars(stop, right, ref left);
            return left == 0;
        }

        // Shortens a lane's box to the part along the stop's lane from keepStart to keepEnd, and its fixed count of spots
        // with it where it has one. The box as the game had it is kept, once, to be put back. How many spots were given up.
        private static int Shorten(Run run, float keepStart, float keepEnd)
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
            float was = size.z;
            centre.z = (near + far) * 0.5f;
            size.z = Mathf.Max(BoxLeast, Mathf.Abs(far - near));
            box.center = centre;
            box.size = size;
            float spot = Mathf.Max(0.1f, (float)SpotField.GetValue(lane));
            int spots = (int)OverrideField.GetValue(lane);
            int lost;
            if (spots > 0)
            {
                // A fixed count of spots is laid from the box's middle: no more of them than the box now holds.
                int now = Mathf.Min(spots, Mathf.FloorToInt(size.z / spot));
                OverrideField.SetValue(lane, now);
                lost = spots - now;
            }
            else
            {
                lost = BusBay.SpotsLost(was, size.z, spot);
            }
            ParkingLaneGenerator.RegenerateAutoParkSpotsNear(lane.transform.TransformPoint(centre));
            return lost;
        }

        // The game's parked cars of a lane that stand in a zone, within its margin, or beyond what is left of the lane: handed
        // back to the game's store of them, as the lane does itself, where nobody is looking. How many; those in view are
        // counted in left.
        private static int SendAway(Run run, TransitStop stop, float keepStart, float keepEnd, ref int left)
        {
            ParkingLaneGenerator lane = run.Lane;
            Transform frame = lane.transform;
            BusZone zone = stop.Zone;
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
                bool inZone = BusBay.CarInZone(at - half, at + half, zone.From, zone.To);
                bool beyond = at - half < keepStart - 0.3f || at + half > keepEnd + 0.3f;
                if (!inZone && !beyond)
                {
                    continue;
                }
                if (CitySpots.InView(car.position))
                {
                    left += inZone ? 1 : 0;   // (one beyond the lane's end but clear of the zone goes when it can, and holds nothing up)
                    continue;
                }
                lane.onReleaseVehicle?.Invoke(car.gameObject);
                ParkingSimulator.ReleaseParkedVehicle(car.gameObject);
                sent++;
            }
            Cars.Clear();
            return sent;
        }

        // The player's own cars standing in the stop's zone or within its margin, between the lane and the kerb: each one that
        // nobody is in, that is standing still and that is out of view is moved to the nearest free space the game's auto-park
        // would offer, outside every zone and out of view (VehicleTow.ParkNearby), and the player is told once. How many; those
        // that must wait are counted in left. One that has nowhere to go stays, parked where the game counts it illegal.
        private static int MovePlayerCars(TransitStop stop, Vector3 right, ref int left)
        {
            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            BusZone zone = stop.Zone;
            int moved = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                VehicleController vehicle = all[i];
                if (vehicle == null || vehicle.controlledByPlayer || !vehicle.gameObject.activeInHierarchy || vehicle.vehicleInstance == null || !GoingPublic.MineVehicle(vehicle))
                {
                    continue;   // (in a Going Public session another player's car is on the game's list too: it is theirs, never moved here)
                }
                Vector3 offset = Flat(vehicle.transform.position - stop.Lane);
                float aside = Vector3.Dot(offset, right);
                if (aside < BusBay.LaneHalf - 0.6f || aside > stop.KerbAside + 0.3f)
                {
                    continue;   // in the lane (it is driving) or up on the pavement: not in the bay
                }
                float along = Vector3.Dot(offset, stop.Along);
                float half = Half(vehicle);
                if (!BusBay.CarInZone(along - half, along + half, zone.From, zone.To))
                {
                    continue;
                }
                Rigidbody body = vehicle.TryGetComponent(out NWH.VehiclePhysics2.VehicleController physics) ? physics.vehicleRigidbody : null;
                if ((body != null && body.velocity.sqrMagnitude > Moving * Moving) || CitySpots.InView(vehicle.transform.position))
                {
                    left++;
                    continue;
                }
                if (!VehicleTow.ParkNearby(vehicle, vehicle.transform.position, Refused))
                {
                    Logger?.Info("Transit: a car of the player's stands in the bay at " + stop.Name + " and no free space was found near it: it is left where it is (parked illegally).");
                    continue;
                }
                moved++;
                Logger?.Info("Transit: a car of the player's stood in the bay at " + stop.Name + ": moved to a free space nearby, out of view.");
                if (!_told)
                {
                    _told = true;
                    Notifications.Show(NotificationType.Info, "reallife_bus_car_moved", null, 10f, trackOnSaveGame: false);
                }
            }
            return moved;
        }

        // A space no car may be moved to: in view, or in any bus zone or within its margin.
        private static bool Refused(Vector3 place)
        {
            return CitySpots.InView(place) || InAnyZone(place, 2.5f);
        }

        /// <summary>Whether a car of the given half length standing at a place would stand in a bus zone, or within its margin of one.</summary>
        public static bool InAnyZone(Vector3 place, float half)
        {
            for (int i = 0; i < TransitMap.Stops.Count; i++)
            {
                TransitStop stop = TransitMap.Stops[i];
                if (!stop.HasBay || stop.Zone == null)
                {
                    continue;
                }
                Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
                Vector3 offset = Flat(place - stop.Lane);
                float aside = Vector3.Dot(offset, right);
                float along = Vector3.Dot(offset, stop.Along);
                if (aside > BusBay.LaneHalf - 1f && aside < stop.KerbAside + 0.5f && Mathf.Abs(offset.y) < 3f
                    && BusBay.CarInZone(along - half, along + half, stop.Zone.From, stop.Zone.To))
                {
                    return true;
                }
            }
            return false;
        }

        private static float Half(VehicleController vehicle)
        {
            try
            {
                float length = VehicleHelper.GetVehicleColliderCenterAndSize(vehicle.vehicleInstance.vehicleTypeName).Item2.z;
                return length > 0.5f ? length * 0.5f : 2.5f;
            }
            catch (Exception)
            {
                return 2.5f;
            }
        }

        // One made zone after another, every few seconds: a lane that has come back whole is cut again and the cars in the
        // zone go; where one of them is in view, the zone is made again from the timer's slices.
        private static void Watch()
        {
            int count = TransitMap.Stops.Count;
            for (int tried = 0; tried < count; tried++)
            {
                _watch = (_watch + 1) % count;
                TransitStop stop = TransitMap.Stops[_watch];
                if (!stop.HasBay || stop.Zone == null || !stop.Zone.Made)
                {
                    continue;
                }
                try
                {
                    bool clear = Sweep(stop, Vector3.Cross(Vector3.up, stop.Along), out int sent);
                    if (sent > 0)
                    {
                        stop.Zone.CarsSent += sent;
                        _carsSince += sent;
                        Logger?.Info("Transit: " + sent + " of the game's parked car(s) had come back into the bay at " + stop.Name + " and were taken in out of view.");
                    }
                    if (!clear)
                    {
                        stop.Zone.Made = false;
                        stop.Zone.Waiting = "a car in it is in view";
                        stop.ZoneLook = 0f;
                        Pending.Add(stop);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                return;
            }
        }

        // ---------------------------------------------------------------- switching a piece off, whole

        // Switches a piece off with everything under it, and the game's obstacles of it that hang elsewhere in the scene, and
        // asks the walkable ground about its footprint a moment later (Probe).
        private static void Hide(GameObject piece, TransitStop stop)
        {
            Bounds bounds = Footprint(piece);
            var hid = new Hid { Piece = piece, Where = stop.Name, Foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), Reach = Mathf.Max(bounds.extents.x, bounds.extents.z), ProbeAt = Time.unscaledTime + ProbeAfter };
            piece.SetActive(false);
            if (_obstacles == null)
            {
                _obstacles = UnityEngine.Object.FindObjectsOfType<NavMeshObstacle>();
            }
            Bounds near = bounds;
            near.Expand(new Vector3(0.6f, 2f, 0.6f));
            for (int i = 0; i < _obstacles.Length; i++)
            {
                NavMeshObstacle obstacle = _obstacles[i];
                if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy)
                {
                    continue;   // (one under the piece went with it)
                }
                Transform at = obstacle.transform;
                Vector3 middle = at.TransformPoint(obstacle.center);
                if (!WithinFlat(middle, near))
                {
                    continue;
                }
                Vector3 scale = at.lossyScale;
                float across = obstacle.shape == NavMeshObstacleShape.Box ? Mathf.Max(Mathf.Abs(obstacle.size.x * scale.x), Mathf.Abs(obstacle.size.z * scale.z)) : obstacle.radius * 2f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                Transform root = at.root;
                if (across > ObstacleMost || (root != null && root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal)))
                {
                    continue;   // a building's, or the mod's own
                }
                if (obstacle.GetComponentInParent<TreeController>() != null || obstacle.GetComponentInChildren<Renderer>(false) != null)
                {
                    continue;   // a thing that is still to be seen (a street tree beside it): its obstacle is its own
                }
                obstacle.enabled = false;
                hid.Off.Add(obstacle);
            }
            MeshCuts.Note("street furniture switched off at a bus stop", Mathf.Max(1, hid.Off.Count));
            Hidden.Add(hid);
        }

        // The piece's footprint, by its colliders (by its models where it has none), while it is still switched on.
        private static Bounds Footprint(GameObject piece)
        {
            piece.GetComponentsInChildren(false, Colliders);
            bool any = false;
            var bounds = new Bounds(piece.transform.position, Vector3.zero);
            for (int i = 0; i < Colliders.Count; i++)
            {
                if (Colliders[i] != null && Colliders[i].enabled)
                {
                    if (any)
                    {
                        bounds.Encapsulate(Colliders[i].bounds);
                    }
                    else
                    {
                        bounds = Colliders[i].bounds;
                        any = true;
                    }
                }
            }
            Colliders.Clear();
            if (!any)
            {
                Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(false);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (any)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    else
                    {
                        bounds = renderers[i].bounds;
                        any = true;
                    }
                }
            }
            return bounds;
        }

        // The pieces switched off a moment ago: where the walkable ground still has a hole under one that the ground round it
        // does not (the hole is baked into the mesh with the city, and switching the piece off cannot close it), the piece is
        // put back as it was and stays: the player then sees what they cannot walk through, and boards round it.
        private static void Probe(float now)
        {
            for (int i = 0; i < Hidden.Count; i++)
            {
                Hid hid = Hidden[i];
                if (hid.ProbeAt <= 0f || now < hid.ProbeAt || hid.Piece == null)
                {
                    continue;
                }
                hid.ProbeAt = 0f;
                if (OnMesh(hid.Foot))
                {
                    continue;
                }
                int around = 0;
                float reach = hid.Reach + 0.45f;
                around += OnMesh(hid.Foot + Vector3.forward * reach) ? 1 : 0;
                around += OnMesh(hid.Foot - Vector3.forward * reach) ? 1 : 0;
                around += OnMesh(hid.Foot + Vector3.right * reach) ? 1 : 0;
                around += OnMesh(hid.Foot - Vector3.right * reach) ? 1 : 0;
                if (around < 3)
                {
                    continue;   // (the edge of the walkable ground: no hole in a way anybody walks)
                }
                hid.Piece.SetActive(true);
                for (int k = 0; k < hid.Off.Count; k++)
                {
                    if (hid.Off[k] != null)
                    {
                        hid.Off[k].enabled = true;
                    }
                }
                Stay.Add(hid.Piece);
                Hidden.RemoveAt(i--);
                _putBack++;
                Logger?.Info("Transit: '" + hid.Piece.name + "' at " + hid.Where + " is put back: the walkable ground under it is baked without it, so with it switched off it would stop the player unseen.");
            }
        }

        private static bool OnMesh(Vector3 place)
        {
            return NavMesh.SamplePosition(place, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) && CitySpots.Flat(hit.position - place) < 0.1f * 0.1f;
        }

        // ---------------------------------------------------------------- putting the street back

        /// <summary>
        /// Puts the street back as the game had it: each lane's box and count of spots, the furniture and its obstacles. The
        /// zones are made again by <see cref="Step"/> when the buses next run. While the city is being unloaded nothing is touched.
        /// The cars taken in are the game's to lay again, as it does every day; a car of the player's that was moved stays where it was parked.
        /// </summary>
        public static void Restore()
        {
            bool unloading = GameManager.isCitySceneBeingUnloaded;
            if (!unloading)
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
                    Hid hid = Hidden[i];
                    if (hid.Piece != null)
                    {
                        hid.Piece.SetActive(true);
                    }
                    for (int k = 0; k < hid.Off.Count; k++)
                    {
                        if (hid.Off[k] != null)
                        {
                            hid.Off[k].enabled = true;
                        }
                    }
                }
            }
            else
            {
                Stay.Clear();
            }
            Originals.Clear();
            Verdicts.Clear();
            Hidden.Clear();
            Pending.Clear();
            _obstacles = null;
            _queued = false;
            _putBack = 0;
            _carsSince = 0;
            for (int i = 0; i < TransitMap.Stops.Count; i++)
            {
                TransitStop stop = TransitMap.Stops[i];
                stop.BoardingMade = false;
                stop.ZoneLook = 0f;
                BusZone zone = stop.Zone;
                if (zone != null)
                {
                    zone.Made = false;
                    zone.ParkingCut = false;
                    zone.Spots = zone.CarsSent = zone.PlayerCars = 0;
                    zone.Waiting = zone.Note = null;
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
                return "its zone not made yet (" + (zone.Waiting ?? "the network is still being laid out") + (zone.ParkingCut ? "; its parking already cut, " + zone.Spots + " spot(s)" : "") + ")";
            }
            return "its zone made: " + (zone.Lanes > 0 ? zone.Cut.ToString("F0") + " m of parking cut from " + zone.Lanes + " lane(s)" + (zone.Lost > 0.5f ? " (" + zone.Lost.ToString("F0") + " of it beyond the zone)" : "") + ", " + zone.Spots + " spot(s)" : "no parking beside it")
                   + ", " + zone.Clear.Count + " piece(s) of street furniture and " + zone.Meters.Count + " parking meter(s) switched off, " + zone.CarsSent + " parked car(s) taken in"
                   + (zone.PlayerCars > 0 ? ", " + zone.PlayerCars + " of the player's moved to a free space" : "")
                   + (zone.Known.Count > 0 ? ", " + zone.Known.Count + " street tree(s) at the kerb left standing" : "") + (zone.Waiting != null ? " (" + zone.Waiting + ")" : "");
        }

        private static void Report()
        {
            int zones = 0, made = 0, lanes = 0, furniture = 0, meters = 0, cars = 0, players = 0, spots = 0, trees = 0, strips = 0, boarding = 0, waiting = 0;
            float cut = 0f, lost = 0f;
            for (int i = 0; i < TransitMap.Stops.Count; i++)
            {
                if (TransitMap.Stops[i].Boarding.Count > 0)
                {
                    strips++;
                    boarding += TransitMap.Stops[i].BoardingMade ? TransitMap.Stops[i].Boarding.Count : 0;
                }
                BusZone zone = TransitMap.Stops[i].Zone;
                if (zone == null || !TransitMap.Stops[i].HasBay)
                {
                    continue;
                }
                zones++;
                spots += zone.Spots;
                cars += zone.CarsSent;
                players += zone.PlayerCars;
                waiting += zone.Made ? 0 : 1;
                if (!zone.Made)
                {
                    continue;
                }
                made++;
                lanes += zone.Lanes;
                cut += zone.Cut;
                lost += zone.Lost;
                furniture += zone.Clear.Count;
                meters += zone.Meters.Count;
                trees += zone.Known.Count;
            }
            Logger?.Info("Transit check (zones): " + made + " of " + zones + " bus zones made: " + cut.ToString("F0") + " m of the game's parking cut from " + lanes + " lane(s) ("
                         + lost.ToString("F0") + " m of it beyond the zones), " + furniture + " piece(s) of street furniture and " + meters + " parking meter(s) switched off, " + cars + " parked car(s) taken in, "
                         + trees + " street tree(s) at the kerb left standing; " + boarding + " piece(s) of street furniture switched off on the boarding strips of " + strips + " stop(s) (the kerb by the doors and the ways to them); "
                         + _putBack + " piece(s) put back (the walkable ground under them is baked without them); "
                         + (made == zones ? "PASS" : (zones - made) + " still waiting to be out of view") + ".");
            Logger?.Info("Transit check (bay cars): in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", " + zones + " bay(s) checked: " + spots + " of the game's parking spot(s) disabled, "
                         + cars + " parked car(s) taken in, " + players + " of the player's car(s) moved to a free space; "
                         + (waiting == 0 ? "every bay clear: PASS" : waiting + " bay(s) with a car or a piece in view, taken as soon as it is out of view") + ".");
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
