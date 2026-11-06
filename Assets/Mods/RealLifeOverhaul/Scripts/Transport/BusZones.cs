using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>What a stop's bus zone needs done to the street, found when its bay was laid out (TransitMap.LayBay).</summary>
    public sealed class BusZone
    {
        public float From;          // metres along the stop's lane from the pole where the zone begins (behind the pole: negative)
        public float To;            // and where it ends
        public float Kerb;          // metres from the lane's line to the kerb, as the bay was laid out to it
        public readonly List<GameObject> Clear = new List<GameObject>(2);   // street furniture standing in the strip: switched off while the zone is there
        public readonly List<GameObject> Meters = new List<GameObject>(2);  // the game's parking meters along the bay's kerb: no parking is left for them
        public readonly List<Collider> Known = new List<Collider>(2);      // what reaches over the kerb into the strip and was found no hindrance
        public bool Made;           // the street is as the zone wants it
        public bool ParkingCut;     // the game's parking along it is cut (at once, in view or not: nobody sees a parking box)
        public int Lanes;           // parking lanes cut for it, for the log
        public float Cut;           // metres of parking taken out of them
        public float Lost;          // of which beyond the zone itself
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
    /// The bus zones: at every stop with a bay, that length of the kerbside strip is the buses' alone. The game's parking box
    /// is shortened to stop <see cref="BusBay.CarMargin"/> short of it, so the game never parks there again; its parked cars
    /// there are handed back the way it takes them in and a car of the player's moved, each once out of view; the furniture in
    /// the strip and on the boarding strips is switched off whole, unless its hole in the navigation mesh is baked in. All put
    /// back when the buses are switched off or the city is left; nothing saved. On the director's timer, about a millisecond a
    /// tick. Another player's car is never moved (GoingPublic.MineVehicle).
    /// </summary>
    public static partial class BusZones
    {
        public const float LossMost = 20f;        // metres of parking beyond a zone in the middle of a run that may go with it: some four spaces

        private const float Margin = BusBay.CarMargin;   // metres of strip kept clear beyond each end of a zone
        private const float BoxLeast = 0.02f;     // metres left of a parking box wholly in a zone, at its end
        private const float LaneAngle = 0.995f;   // a parking lane runs along the road to within some 6 degrees, or it is not a run along the kerb
        private const double Slice = 0.001;       // seconds of zone making a timer slice (a zone begun is finished)
        private const float LookAgain = 1f;       // seconds before a stop with something in view is looked at again
        private const float WatchEvery = 5f;      // seconds between looks at one made zone after another, for what has come back

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
            public float ProbeAt;      // when the walkable ground under it is looked at; 0 once it has been
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

        // A parking lane's private settings: null when a game update took one away.
        private static readonly FieldInfo OverrideField = GameContract.LaneSpotCount.Field;
        private static readonly FieldInfo SideField = GameContract.LaneSideBySide.Field;
        private static readonly FieldInfo CurvedField = GameContract.LaneCurved.Field;
        private static readonly FieldInfo SpecificField = GameContract.LaneVehicles.Field;
        private static readonly FieldInfo SpotField = GameContract.LaneSpotDistance.Field;

        private static readonly Dictionary<ParkingLaneGenerator, Original> Originals = new Dictionary<ParkingLaneGenerator, Original>(32);
        private static readonly Dictionary<ParkingLaneGenerator, string> Verdicts = new Dictionary<ParkingLaneGenerator, string>(64);
        private static readonly List<Hid> Hidden = new List<Hid>(32);
        private static readonly HashSet<GameObject> Stay = new HashSet<GameObject>();   // pieces whose hole in the mesh is baked in: never switched off again in this city
        private static readonly List<TransitStop> Pending = new List<TransitStop>(64);
        private static readonly Collider[] Found = new Collider[32];
        private static readonly Run[] Runs = new Run[8];
        private static readonly List<Component> Parts = new List<Component>(32);
        private static readonly List<Collider> Colliders = new List<Collider>(16);
        private static NavMeshObstacle[] _obstacles;   // the game's, found once a city behind the loading screen
        private static int _parkingMask;
        private static bool _queued;
        private static bool _reported;
        private static float _queuedAt;
        private static float _nextWatch;
        private static int _watch;
        private static int _putBack;    // pieces put back because their hole in the mesh is baked in

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

        /// <summary>Behind the loading screen: lists the game's navigation obstacles, a search of the whole scene once a city.</summary>
        public static bool Prewarm()
        {
            _obstacles = SceneFinds.All<NavMeshObstacle>("transit: the game's navigation obstacles being listed for the bus stops (a search of the whole scene, once a city)");
            return true;
        }

        /// <summary>True for a piece of street furniture whose hole in the mesh is baked in: switched off, it would stop the player unseen.</summary>
        public static bool MustStay(GameObject piece)
        {
            return piece != null && Stay.Contains(piece);
        }

        /// <summary>Why the parking along a zone cannot be cut for it, for the log, or null. Nothing is changed: the cut is made with the zone.</summary>
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

        // The parking lanes whose boxes reach into a stretch of strip, into Runs; why not, if one is not a plain run (never cut).
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
                    continue;   // parking of no lane is not legal parking to the game either
                }
                why = Verdict(lane);
                if (why != null || hit.gameObject != lane.gameObject || !(hit is BoxCollider box))
                {
                    continue;   // another part of a plain lane (a reserved or auto-park spot): its own box is what counts
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
                float perLocal = frame.lossyScale.z * Vector3.Dot(axis, along);   // the box's own length runs a little shorter along a street that slopes
                float middle = Vector3.Dot(Flat(frame.TransformPoint(box.center) - stop.Lane), along);
                float half = Mathf.Abs(box.size.z * perLocal) * 0.5f;
                Runs[runs++] = new Run { Lane = lane, Box = box, Start = middle - half, End = middle + half, Middle = middle, PerLocal = perLocal };
            }
            return runs;
        }

        // Whether a parking lane is a plain run along the kerb, which may be cut: null if so, else why not. Asked once a lane.
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
        /// The piece of street furniture a collider belongs to, to switch off whole: up from it while what is above is the same
        /// thing, never a row of them. Null if it has more than models, colliders and the game's scenery scripts (a door, a machine).
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

        // Whether a parent holds something solid that stands elsewhere: another piece of furniture.
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
                    // A collider switched off has no bounds to go by: taken for something else, so the piece stays smaller.
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

        // Nothing but models, colliders and scenery scripts, and not a piece that switches itself back on (the game's CityMapHider).
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

        private static readonly int HotStep = HotSpots.Slot("Transport: BusZones.Step");

        /// <summary>Makes the zones still to be made, a slice at a time, and keeps them made. From the director's timer.</summary>
        public static void Step()
        {
            if (!TransitMap.Ready)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotStep);
            RelayOne();
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

        // Makes one stop's zone and clears its boarding strip; false if either must wait. A stop's furniture goes in one frame
        // once none of it is in view, so the walkable ground is cut once for the stop.
        private static bool Make(TransitStop stop)
        {
            bool unseen = !AnyInView(stop);
            bool zone = !stop.HasBay || stop.Zone == null || MakeZone(stop, unseen);
            bool boarding = ClearBoarding(stop, unseen);
            return zone && boarding;
        }

        private static bool AnyInView(TransitStop stop)
        {
            BusZone zone = stop.HasBay && stop.Zone != null && !stop.Zone.Made ? stop.Zone : null;
            return (zone != null && (InView(zone.Clear) || InView(zone.Meters))) || (!stop.BoardingMade && InView(stop.Boarding));
        }

        private static bool InView(List<GameObject> pieces)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                GameObject piece = pieces[i];
                if (piece != null && piece.activeSelf && !Stay.Contains(piece) && CitySpots.InView(piece.transform.position))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ClearBoarding(TransitStop stop, bool unseen)
        {
            if (stop.BoardingMade || stop.Boarding.Count == 0)
            {
                return true;
            }
            try
            {
                if (!HideAll(stop.Boarding, stop, unseen))
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

        // Switches off every piece still to go when none of the stop's is in view; true when none is left standing.
        private static bool HideAll(List<GameObject> pieces, TransitStop stop, bool unseen)
        {
            bool all = true;
            for (int i = 0; i < pieces.Count; i++)
            {
                GameObject piece = pieces[i];
                if (piece == null || !piece.activeSelf || Stay.Contains(piece))
                {
                    continue;
                }
                if (!unseen)
                {
                    all = false;
                    continue;
                }
                Hide(piece, stop);
            }
            return all;
        }

        // False if something of it is in view: it is asked again a second later.
        private static bool MakeZone(TransitStop stop, bool unseen)
        {
            BusZone zone = stop.Zone;
            if (zone.Made)
            {
                return true;
            }
            using var piece = SessionWatch.Piece("transit: a bus zone made (parking cut, furniture and parked cars cleared)");
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
                    zone.Note = why;   // a lane found to be other than plain since the bay was laid out is left as it is, and said
                }
                bool pieces = HideAll(zone.Clear, stop, unseen);
                pieces &= HideAll(zone.Meters, stop, unseen);
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

        /// <summary>Puts the street back as the game had it, except while the city unloads; <see cref="Step"/> makes the zones again.</summary>
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
                int carves = 0;
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
                    carves += Carves(hid);
                }
                MeshCuts.Note("the street furniture of the bus stops put back", carves);   // all in one frame
            }
            else
            {
                Stay.Clear();
                _obstacles = null;   // the city's list: kept while the buses are switched off and on again
            }
            Originals.Clear();
            Verdicts.Clear();
            Hidden.Clear();
            Pending.Clear();
            Relay.Clear();   // every lane was laid out again above, as the game had it
            RelayAt.Clear();
            _queued = false;
            _putBack = 0;
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

        /// <summary>What was done for a stop's zone, for a log line.</summary>
        public static string Describe(BusZone zone)
        {
            if (zone == null)
            {
                return "no zone";
            }
            if (!zone.Made)
            {
                return "its zone not made yet (" + (zone.Waiting ?? "the network is still being laid out")
                       + (zone.ParkingCut ? "; its parking already cut, " + zone.Spots + " spot(s)" : "") + ")";
            }
            return "its zone made: " + (zone.Lanes > 0
                       ? zone.Cut.ToString("F0") + " m of parking cut from " + zone.Lanes + " lane(s)"
                         + (zone.Lost > 0.5f ? " (" + zone.Lost.ToString("F0") + " of it beyond the zone)" : "") + ", " + zone.Spots + " spot(s)"
                       : "no parking beside it")
                   + ", " + zone.Clear.Count + " piece(s) of street furniture and " + zone.Meters.Count + " parking meter(s) switched off, " + zone.CarsSent
                   + " parked car(s) taken in"
                   + (zone.PlayerCars > 0 ? ", " + zone.PlayerCars + " of the player's moved to a free space" : "")
                   + (zone.Known.Count > 0 ? ", " + zone.Known.Count + " street tree(s) at the kerb left standing" : "")
                   + (zone.Waiting != null ? " (" + zone.Waiting + ")" : "");
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
            Logger?.Info("Transit check (zones): " + made + " of " + zones + " bus zones made: " + cut.ToString("F0") + " m of the game's parking cut from "
                         + lanes + " lane(s) (" + lost.ToString("F0") + " m of it beyond the zones), " + furniture + " piece(s) of street furniture and " + meters
                         + " parking meter(s) switched off, " + cars + " parked car(s) taken in, " + trees + " street tree(s) at the kerb left standing; " + boarding
                         + " piece(s) of street furniture switched off on the boarding strips of " + strips + " stop(s) (the kerb by the doors and the ways to them); "
                         + _putBack + " piece(s) put back (the walkable ground under them is baked without them); "
                         + (made == zones ? "PASS" : (zones - made) + " still waiting to be out of view") + ".");
            Logger?.Info("Transit check (bay cars): in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", " + zones + " bay(s) checked: " + spots
                         + " of the game's parking spot(s) disabled, " + cars + " parked car(s) taken in, " + players + " of the player's car(s) moved to a free space; "
                         + (waiting == 0 ? "every bay clear: PASS" : waiting + " bay(s) with a car or a piece in view, taken as soon as it is out of view") + ".");
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
