using System;
using System.Collections.Generic;
using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>Why a site could not be set out where it is. A test button says exactly this.</summary>
    public enum SiteRefusal { None, NoPavement, NoLane, NoKerb, Vehicles, NoWall, NoRoom, CannotClose, NoSlot }

    /// <summary>
    /// Puts the construction sites near the player in the street, and takes them away. A site
    /// is data (CivicWorks); this turns the three nearest into props while the player is within
    /// 125 metres, and back into nothing beyond 175, when the site is handed over, when the
    /// player goes indoors and on every fault.
    ///
    /// EVERY PLACE IS FOUND, never assumed, when a site is set out (some tens of physics and
    /// navigation queries, once; then nothing until its phase changes):
    ///   Roadworks  the pavement in front of the site's building; the nearest traffic lane; the
    ///              kerb between them (where the pedestrians' navigation mesh ends on the way to
    ///              the lane, then where the ground drops to the road); a strip one metre wide in
    ///              the carriageway beside the kerb, clear of parked vehicles, for the trench; the
    ///              crew on the pavement's edge facing it. The lane is closed (RoadClosure) when
    ///              it can be closed safely. Where the lane runs within three metres of the kerb
    ///              and cannot be closed, the works are not set out there at all: nothing of
    ///              theirs ever stands beside live traffic.
    ///   Utility    the same strip beside the kerb, with a shelter over an open cover.
    ///   Scaffold   the wall the building's door is in, by a ray from the pavement to the door;
    ///              bays of 2.4 m fitted along it one at a time, each tested for a flat wall, for
    ///              pavement under both rows of standards, for a door where a standard would
    ///              stand, and for anything solid in its volume; lifts counted by rays up the
    ///              wall. The first deck is 2.4 m up and open underneath, so the pavement stays
    ///              walkable for its whole width. The skip stands in the carriageway beside the
    ///              kerb, and only where there is a strip between the kerb and the lane for it.
    ///   Refit      the same wall and the same bays, with a hoarding panel in each, 0.12 m off
    ///              the wall (never over a door).
    ///
    /// Nothing is cut out of the navigation mesh (the rule for pavements): what stands on a
    /// pavement is thin (scaffold standards, a notice board) or flat against a wall (hoarding),
    /// and everything bulky (skip, shelter, spoil, barriers, cones) stands in the carriageway
    /// beside the kerb, where nobody walks.
    ///
    /// What is drawn follows the site's phase, and is changed only where the camera is not
    /// looking. One site at a time has a crew (CivicCrew): the nearest one that is at work
    /// (working hours, a weekday, no rain, nothing else going on beside it).
    ///
    /// Budget: at most 3 sites set out, at most about 60 prop objects for the three (a scaffold
    /// of five bays and four lifts is 43), 3 people. Nothing per frame.
    /// </summary>
    public sealed class SiteDresser
    {
        private const int MaxDressed = 3;
        private const float DressNear = 125f;
        private const float UndressFar = 175f;
        private const float HalfTrench = 4f;        // the trench is eight metres long
        private const float StripCentre = 0.55f;    // the middle of the works strip, from the kerb
        private const float NeedsClosure = 3f;      // a lane whose middle is nearer the kerb than this must be closed for works in the strip
        private const float BayWidth = 2.4f;
        private const float BayDepth = 1.86f;       // wall to the outer standards
        private const float WallGap = 0.12f;
        private const float DeckHeight = 2.4f;
        private const float LiftHeight = 2.0f;
        private const int MaxBays = 5;
        private const int MaxLifts = 3;
        private const float NoticeReach = 1.7f;

        private sealed class CrewSpot
        {
            public Vector3 At, Face;
            public string Work, Rest;
            public CityLook Look;
            public float Lift;
        }

        private sealed class Dressed
        {
            public SiteState Site;
            public int Phase = -1;
            public bool Unsafe, Halted;
            public bool Now;                   // set out by a test button: being in view does not hold it back
            public bool Gone;                  // the site has left the programme: taken away as soon as nobody is looking
            public readonly List<Transform> Props = new List<Transform>(48);
            public readonly RoadClosure Road = new RoadClosure();
            public float NextSweep;
            public bool LaneShut;

            // Where it is, found once.
            public Vector3 Origin;             // the middle of it, on the ground
            public Vector3 Out;                // from the pavement towards the road (works in the road), or out of the wall
            public Vector3 Along;              // the lane's direction, or along the wall
            public Vector3 Kerb;               // works in the road: the kerb, level with the origin. Wall kinds: the foot of the wall at bay 0
            public Vector3 Edge;               // works in the road: the edge of the pavement people can stand on, level with the origin
            public float LaneFromKerb;
            public int BayMin, BayMax, Lifts;  // wall kinds: which bays stand (indices either side of the first)
            public bool HasSkip;
            public Vector3 SkipAt, SkipAlong;
            public bool HasNotice;
            public Vector3 NoticeAt, NoticeFace, Reader;

            public readonly CrewSpot[] Crew = { new CrewSpot(), new CrewSpot(), new CrewSpot() };
            public int CrewCount;
        }

        private readonly CivicProps _props;
        private readonly CivicWorks _works;
        private readonly CivicCrew _crew;
        private readonly CivicCast _cast;
        private readonly List<Dressed> _dressed = new List<Dressed>(MaxDressed);
        private readonly Stack<Dressed> _spare = new Stack<Dressed>(MaxDressed);
        private readonly Dictionary<int, float> _retry = new Dictionary<int, float>(8);   // site id -> when to try setting it out again
        private readonly RoadClosure _probe = new RoadClosure();   // only ever asked where a lane is; closes nothing
        private Dressed _crewed;
        private NavMeshQueryFilter _pavement;

        /// <summary>Why the last attempt to set a site out failed.</summary>
        public SiteRefusal LastRefusal { get; private set; }

        public SiteDresser(CivicProps props, CivicWorks works, CivicCast cast)
        {
            _props = props;
            _works = works;
            _cast = cast;
            _crew = new CivicCrew(cast);
            _pavement = new NavMeshQueryFilter { agentTypeID = NavMeshHelper.NpcAgentTypeId, areaMask = NavMesh.AllAreas & ~NavMeshHelper.SwimmingAreaMask };
        }

        public int DressedCount => _dressed.Count;

        public int CrewOut => _crew.Count;

        /// <summary>True if a site that is set out stands within so many metres of a place (asked before another scene is staged there).</summary>
        public bool Near(Vector3 at, float metres)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                if (!_dressed[i].Gone && CivicCity.Flat(_dressed[i].Origin - at) < metres * metres)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The site whose notice the player is standing at; null if none.</summary>
        public SiteState NoticeInReach(Vector3 player)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                Dressed dressed = _dressed[i];
                if (!dressed.HasNotice || dressed.Gone)
                {
                    continue;
                }
                Vector3 offset = dressed.Reader - player;
                if (Mathf.Abs(offset.y) < 2f && CivicCity.Flat(offset) < NoticeReach * NoticeReach)
                {
                    return dressed.Site;
                }
            }
            return null;
        }

        private Dressed Find(SiteState site)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                if (_dressed[i].Site == site)
                {
                    return _dressed[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- once a second

        /// <param name="crewAllowed">False while something else of civic life's has the people (a rally).</param>
        public void Tick(float now, Vector3 player, int day, int hour, bool crewAllowed)
        {
            List<SiteState> sites = _works.State.Sites;

            // Sites that are set out: gone, far, or changed.
            for (int i = _dressed.Count - 1; i >= 0; i--)
            {
                Dressed dressed = _dressed[i];
                float sqr = CivicCity.Flat(dressed.Origin - player);
                if (!dressed.Gone && !sites.Contains(dressed.Site))
                {
                    dressed.Gone = true;
                    dressed.Road.Open();   // the road first, whoever is looking
                    dressed.LaneShut = false;
                    if (_crewed == dressed)
                    {
                        _crew.Dismiss();
                    }
                }
                bool seen = CitySpots.InView(dressed.Origin);
                if (sqr > UndressFar * UndressFar || (dressed.Gone && (!seen || dressed.Now)))
                {
                    Undress(i);
                    continue;
                }
                if (dressed.Gone)
                {
                    continue;
                }
                if ((dressed.Phase != dressed.Site.Phase || dressed.Unsafe != dressed.Site.Unsafe || dressed.Halted != dressed.Site.HaltUntil > day) && (!seen || dressed.Now))
                {
                    Rebuild(dressed, day);
                }
                if (dressed.LaneShut && now >= dressed.NextSweep)
                {
                    dressed.NextSweep = now + 20f;
                    dressed.Road.Sweep();
                }
            }

            // Sites near enough that are not set out yet.
            for (int i = 0; i < sites.Count && _dressed.Count < MaxDressed; i++)
            {
                SiteState site = sites[i];
                var at = new Vector3(site.X, site.Y, site.Z);
                if (CivicCity.Flat(at - player) > DressNear * DressNear || Find(site) != null || CitySpots.InView(at))
                {
                    continue;
                }
                if (_retry.TryGetValue(site.Id, out float again) && now < again)
                {
                    continue;
                }
                if (!Dress(site, false, day))
                {
                    _retry[site.Id] = now + 45f;
                }
            }

            Crew(now, player, day, hour, crewAllowed);
        }

        /// <summary>Sets a site out now, in view or not: for the test buttons. False with <see cref="LastRefusal"/> if it has no room here.</summary>
        public bool DressNow(SiteState site, int day)
        {
            Dressed already = Find(site);
            if (already != null)
            {
                already.Now = true;
                return true;
            }
            if (_dressed.Count >= MaxDressed)
            {
                Undress(0);   // one of the others makes way
            }
            return Dress(site, true, day);
        }

        /// <summary>Redraws every site that is set out, now (a test button advanced them).</summary>
        public void RefreshNow(int day)
        {
            for (int i = 0; i < _dressed.Count; i++)
            {
                if (!_dressed[i].Gone && _works.State.Sites.Contains(_dressed[i].Site))
                {
                    Rebuild(_dressed[i], day);
                }
            }
        }

        /// <summary>Everything away at once: props, the crew; every closed lane opened. Safe at any time, any number of times.</summary>
        public void Clear()
        {
            for (int i = _dressed.Count - 1; i >= 0; i--)
            {
                Undress(i);
            }
            _crew.Release();
            _crewed = null;
            _retry.Clear();
        }

        private bool Dress(SiteState site, bool now, int day)
        {
            Dressed dressed = _spare.Count > 0 ? _spare.Pop() : new Dressed();
            dressed.Site = site;
            dressed.Phase = -1;
            dressed.Now = now;
            dressed.Gone = false;
            dressed.HasNotice = false;
            dressed.HasSkip = false;
            dressed.LaneShut = false;
            SiteRefusal why;
            try
            {
                why = Resolve(dressed);
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                why = SiteRefusal.NoRoom;
            }
            if (why == SiteRefusal.None)
            {
                _dressed.Add(dressed);
                Rebuild(dressed, day);
                if (dressed.Gone)
                {
                    why = SiteRefusal.NoRoom;   // a fault while it was being set out: Rebuild has stripped it
                    _dressed.Remove(dressed);
                }
                else if (InRoad(site.Kind) && dressed.LaneFromKerb < NeedsClosure && !dressed.LaneShut && site.Phase < 4)
                {
                    // The works would stand beside a lane that cannot be shut: not here.
                    _dressed.Remove(dressed);
                    Strip(dressed);
                    why = SiteRefusal.CannotClose;
                }
            }
            LastRefusal = why;
            if (why != SiteRefusal.None)
            {
                dressed.Site = null;
                _spare.Push(dressed);
                CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ") could not be set out at " + CivicCity.AddressText(site.Street, site.Number) + ": " + why + ".");
                return false;
            }
            CivicDirector.Logger?.Info("Civic: site " + site.Id + " (" + site.Kind + ") set out at " + CivicCity.AddressText(site.Street, site.Number) + ", stage " + (site.Phase + 1) + ": "
                                       + dressed.Props.Count + " prop(s)" + (dressed.LaneShut ? ", one lane closed" : string.Empty)
                                       + (InRoad(site.Kind) ? ", lane " + dressed.LaneFromKerb.ToString("F1") + " m from the kerb" : ", " + (dressed.BayMax - dressed.BayMin + 1) + " bay(s), " + dressed.Lifts + " lift(s)") + ".");
            return true;
        }

        private static bool InRoad(SiteKind kind)
        {
            return kind == SiteKind.Roadworks || kind == SiteKind.Utility;
        }

        private void Undress(int index)
        {
            Dressed dressed = _dressed[index];
            _dressed.RemoveAt(index);
            if (_crewed == dressed)
            {
                _crew.Release();
                _crewed = null;
            }
            Strip(dressed);
            dressed.Site = null;
            _spare.Push(dressed);
        }

        // Props back on the shelf, the lane open.
        private void Strip(Dressed dressed)
        {
            dressed.Road.Open();
            dressed.LaneShut = false;
            _props.ReturnAll(dressed.Props);
        }

        private void Rebuild(Dressed dressed, int day)
        {
            try
            {
                if (_crewed == dressed)
                {
                    _crew.Release();   // their places change with the phase: they are put out again where the new phase has them
                    _crewed = null;
                }
                _props.ReturnAll(dressed.Props);
                dressed.Phase = dressed.Site.Phase;
                dressed.Unsafe = dressed.Site.Unsafe;
                dressed.Halted = dressed.Site.HaltUntil > day;
                dressed.CrewCount = 0;
                switch (dressed.Site.Kind)
                {
                    case SiteKind.Roadworks: BuildRoad(dressed); break;
                    case SiteKind.Scaffold: BuildScaffold(dressed); break;
                    case SiteKind.Refit: BuildRefit(dressed); break;
                    default: BuildUtility(dressed); break;
                }
                if (dressed.HasNotice)
                {
                    Put(dressed, "site_notice_board", dressed.NoticeAt, dressed.NoticeFace);
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                Strip(dressed);
                dressed.Gone = true;   // taken away; the site itself goes on as data, and is not tried again for ten minutes
                _retry[dressed.Site.Id] = Time.time + 600f;
            }
        }

        // ---------------------------------------------------------------- the crew

        private void Crew(float now, Vector3 player, int day, int hour, bool allowed)
        {
            _crew.Tick(now);
            // Who should have it: the nearest site that is set out and at work.
            Dressed want = null;
            if (allowed && !CivicBridges.WorkStopped() && !CivicBridges.SceneOn())
            {
                float best = float.MaxValue;
                for (int i = 0; i < _dressed.Count; i++)
                {
                    Dressed dressed = _dressed[i];
                    if (dressed.Gone || dressed.CrewCount == 0 || !_works.Working(dressed.Site, day, hour))
                    {
                        continue;
                    }
                    float sqr = CivicCity.Flat(dressed.Origin - player);
                    if (sqr < best)
                    {
                        best = sqr;
                        want = dressed;
                    }
                }
            }
            if (_crewed != null && (_crewed != want || _crew.Disturbed))
            {
                // Hours are up, the rain has come, another site is nearer, or one of them was knocked down: they walk off.
                _crew.Dismiss();
                if (_crew.Count == 0)
                {
                    _crew.Release();
                    _crewed = null;
                }
                return;
            }
            if (_crewed == null && _crew.Count > 0)
            {
                _crew.Dismiss();   // people of a site that has gone: still walking off
                return;
            }
            if (_crewed == null && want != null && _cast.Ready(want.CrewCount, player, now))
            {
                int added = 0;
                for (int i = 0; i < want.CrewCount; i++)
                {
                    CrewSpot spot = want.Crew[i];
                    if (spot.Lift > 0.5f && CitySpots.InView(spot.At))
                    {
                        continue;   // nobody is seen arriving on a scaffold: the deck stays empty this time
                    }
                    if (_crew.Add(spot.Look, spot.At, spot.Face, spot.Work, spot.Rest, spot.Lift))
                    {
                        added++;
                    }
                }
                if (added > 0)
                {
                    _crewed = want;
                    CivicDirector.Logger?.Info("Civic: a crew of " + added + " at site " + want.Site.Id + " (" + want.Site.Kind + ", stage " + (want.Phase + 1) + ").");
                }
            }
        }

        private static void Plan(Dressed dressed, Vector3 at, Vector3 face, CityLook look, string work, string rest, float lift = 0f)
        {
            if (dressed.CrewCount >= dressed.Crew.Length || dressed.Halted)
            {
                return;   // stopped by the inspector: nobody on site
            }
            if (!CitySpots.Stand(at, 0.38f, out Vector3 spot))
            {
                return;   // no room for them here: the crew is one fewer
            }
            CrewSpot planned = dressed.Crew[dressed.CrewCount++];
            planned.At = spot;
            planned.Face = face;
            planned.Look = look;
            planned.Work = work;
            planned.Rest = rest;
            planned.Lift = lift;
        }

        // ---------------------------------------------------------------- finding the place

        private SiteRefusal Resolve(Dressed dressed)
        {
            SiteState site = dressed.Site;
            var door = new Vector3(site.X, site.Y, site.Z);
            if (!CitySpots.Pavement(door, 8f, out Vector3 pavement))
            {
                return SiteRefusal.NoPavement;
            }
            return InRoad(site.Kind) ? ResolveRoad(dressed, pavement) : ResolveWall(dressed, pavement, door, site.Kind == SiteKind.Refit);
        }

        /// <summary>
        /// From a point of pavement towards the nearest lane: where people can stand no further
        /// (the edge of their navigation mesh), where the kerb really is (the ground drops to the
        /// road within 0.8 m beyond that edge; if no drop is found the kerb is taken 0.3 m beyond
        /// it), which way the lane runs and how far its middle is from the kerb.
        /// </summary>
        private bool Kerbside(RoadClosure road, Vector3 from, out Vector3 edge, out Vector3 kerb, out Vector3 outward, out Vector3 lane, out float laneFromKerb, ref SiteRefusal why)
        {
            edge = kerb = outward = lane = default;
            laneFromKerb = 0f;
            if (!road.Lane(from, 14f))
            {
                why = SiteRefusal.NoLane;
                return false;
            }
            lane = road.LaneDirection;
            Vector3 toLane = road.LaneCentre - from;
            toLane.y = 0f;
            float distance = toLane.magnitude;
            if (distance < 2f || distance > 13f)
            {
                why = SiteRefusal.NoLane;
                return false;
            }
            outward = toLane / distance;
            if (!NavMesh.Raycast(from, from + outward * distance, out NavMeshHit hit, _pavement))
            {
                why = SiteRefusal.NoKerb;   // the pavement runs on to the lane with no edge between: no kerb to work at
                return false;
            }
            edge = hit.position;
            float beyond = 0.3f;
            for (float step = 0.1f; step <= 0.8f; step += 0.1f)
            {
                if (CitySpots.Ground(edge + outward * step, out float ground) && ground < edge.y - 0.05f)
                {
                    beyond = step - 0.05f;
                    break;
                }
            }
            kerb = edge + outward * beyond;
            laneFromKerb = distance - hit.distance - beyond;
            if (laneFromKerb < 1.5f)
            {
                why = SiteRefusal.NoKerb;   // the lane's middle is on the kerb: the survey is wrong here
                return false;
            }
            return true;
        }

        private SiteRefusal ResolveRoad(Dressed dressed, Vector3 pavement)
        {
            if (!TrafficTools.Ready)
            {
                return SiteRefusal.NoLane;
            }
            if (!CitySpots.Along(pavement, out Vector3 run))
            {
                return SiteRefusal.NoPavement;
            }
            float half = dressed.Site.Kind == SiteKind.Roadworks ? HalfTrench : 1.2f;
            SiteRefusal why = SiteRefusal.NoLane;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float shift = attempt == 0 ? 0f : (attempt % 2 == 1 ? 1f : -1f) * 7f * ((attempt + 1) / 2);
                if (!CitySpots.Pavement(pavement + run * shift, 2f, out Vector3 here)
                    || !Kerbside(dressed.Road, here, out Vector3 edge, out Vector3 kerb, out Vector3 outward, out Vector3 lane, out float laneFromKerb, ref why))
                {
                    continue;
                }
                Vector3 strip = kerb + outward * StripCentre;
                if (CitySpots.Ground(strip, out float ground))
                {
                    strip.y = ground;
                }
                if (!CivicSpots.BoxFree(strip + Vector3.up * 0.75f, new Vector3(0.8f, 0.5f, half + 0.6f), Quaternion.LookRotation(lane), CivicSpots.StandingVehicles))
                {
                    why = SiteRefusal.Vehicles;
                    continue;
                }
                if (!CitySpots.Stand(edge - outward * 0.08f - lane * Mathf.Min(1.7f, half), 0.38f, out _))
                {
                    why = SiteRefusal.NoRoom;
                    continue;
                }
                dressed.Origin = strip;
                dressed.Out = outward;
                dressed.Along = lane;
                dressed.Kerb = kerb;
                dressed.Edge = edge;
                dressed.LaneFromKerb = laneFromKerb;
                Notice(dressed, edge - outward * 0.9f + lane * (half + 2f), -lane, edge - outward * 0.9f - lane * (half + 2f), lane);
                return SiteRefusal.None;
            }
            return why;
        }

        // The wall the building's door is in, and the bays that fit along it.
        private SiteRefusal ResolveWall(Dressed dressed, Vector3 pavement, Vector3 door, bool hoarding)
        {
            Vector3 toDoor = door - pavement;
            toDoor.y = 0f;
            if (toDoor.sqrMagnitude < 0.04f)
            {
                return SiteRefusal.NoWall;
            }
            toDoor.Normalize();
            RaycastHit wall = default;
            bool found = false;
            Vector3 side = Vector3.Cross(Vector3.up, toDoor);
            for (int attempt = 0; attempt < 5 && !found; attempt++)
            {
                float shift = attempt == 0 ? 0f : (attempt % 2 == 1 ? 1f : -1f) * 1.6f * ((attempt + 1) / 2);
                found = CivicSpots.Wall(pavement + side * shift, toDoor, 1.3f, 10f, out wall) && Mathf.Abs(wall.normal.y) < 0.2f;
            }
            if (!found)
            {
                return SiteRefusal.NoWall;
            }
            Vector3 outward = wall.normal;
            outward.y = 0f;
            outward.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector3 foot = wall.point;
            foot.y = pavement.y;

            // Bays either side of the first, kept in one run.
            float depth = hoarding ? 0.6f : BayDepth;
            if (!Bay(foot, outward, along, 0, depth, hoarding))
            {
                // The first bay is where the door is, as often as not: start one bay along.
                if (Bay(foot + along * BayWidth, outward, along, 0, depth, hoarding))
                {
                    foot += along * BayWidth;
                }
                else if (Bay(foot - along * BayWidth, outward, along, 0, depth, hoarding))
                {
                    foot -= along * BayWidth;
                }
                else
                {
                    return SiteRefusal.NoRoom;
                }
            }
            int min = 0, max = 0;
            bool growUp = true, growDown = true;
            while (max - min + 1 < MaxBays && (growUp || growDown))
            {
                if (growUp)
                {
                    growUp = Bay(foot, outward, along, max + 1, depth, hoarding);
                    if (growUp)
                    {
                        max++;
                    }
                }
                if (growDown && max - min + 1 < MaxBays)
                {
                    growDown = Bay(foot, outward, along, min - 1, depth, hoarding);
                    if (growDown)
                    {
                        min--;
                    }
                }
            }
            if (!hoarding && max - min + 1 < 2)
            {
                return SiteRefusal.NoRoom;
            }
            int lifts = 0;
            if (!hoarding)
            {
                for (int k = 1; k <= MaxLifts; k++)
                {
                    float height = DeckHeight + (k - 1) * LiftHeight + 1f;
                    if (!CivicSpots.Wall(foot + outward * 2.2f, -outward, height, 3f, out RaycastHit high) || high.distance < 1.7f || high.distance > 2.8f)
                    {
                        break;
                    }
                    lifts = k;
                }
                if (lifts == 0)
                {
                    return SiteRefusal.NoWall;
                }
            }
            dressed.Origin = foot + along * ((min + max) * 0.5f * BayWidth) + outward * 0.9f;
            dressed.Kerb = foot;
            dressed.Out = outward;
            dressed.Along = along;
            dressed.BayMin = min;
            dressed.BayMax = max;
            dressed.Lifts = lifts;

            float low = min * BayWidth - BayWidth * 0.5f, top = max * BayWidth + BayWidth * 0.5f;
            Notice(dressed, foot + along * (low - 1.1f) + outward * 0.45f, outward, foot + along * (top + 1.1f) + outward * 0.45f, outward);
            Skip(dressed, foot + along * ((min + max) * 0.5f * BayWidth) + outward * 0.8f);
            return SiteRefusal.None;
        }

        // The skip: in the carriageway beside the kerb in front of the works, where there is a strip between the kerb and the
        // lane wide enough for it (1.7 m, and a car's width clear of the lane's middle) and nothing parked in it. Else there is none.
        private void Skip(Dressed dressed, Vector3 front)
        {
            dressed.HasSkip = false;
            SiteRefusal ignored = SiteRefusal.None;
            if (!TrafficTools.Ready || !CitySpots.Pavement(front, 1.5f, out Vector3 from)
                || !Kerbside(_probe, from, out _, out Vector3 kerb, out Vector3 outward, out Vector3 lane, out float laneFromKerb, ref ignored) || laneFromKerb < 3.1f)
            {
                return;
            }
            Vector3 centre = kerb + outward * 0.95f;
            if (CitySpots.Ground(centre, out float ground))
            {
                centre.y = ground;
            }
            if (CivicSpots.BoxFree(centre + Vector3.up * 0.8f, new Vector3(0.95f, 0.6f, 1.9f), Quaternion.LookRotation(lane), CivicSpots.Solid))
            {
                dressed.HasSkip = true;
                dressed.SkipAt = centre;
                dressed.SkipAlong = lane;
            }
        }

        // One bay of 2.4 m at an index along the wall: a flat wall behind it, pavement under it, no door where a post would stand, nothing in it.
        private static bool Bay(Vector3 foot, Vector3 outward, Vector3 along, int index, float depth, bool hoarding)
        {
            Vector3 centre = foot + along * (index * BayWidth);
            for (int ray = -1; ray <= 1; ray++)
            {
                if (!CivicSpots.Wall(centre + along * (ray * 1.0f) + outward * 1.2f, -outward, 1.3f, 1.7f, out RaycastHit hit)
                    || hit.distance < 0.95f || hit.distance > 1.5f || Vector3.Dot(hit.normal, outward) < 0.85f)
                {
                    return false;
                }
            }
            if (!CitySpots.OnPavement(centre + outward * (depth * 0.5f), out _) || !CitySpots.OnPavement(centre + outward * depth, out _))
            {
                return false;
            }
            if (hoarding)
            {
                if (CivicSpots.DoorNear(centre + outward * 0.4f, 1.7f))
                {
                    return false;   // a hoarding never stands across a door
                }
            }
            else if (CivicSpots.DoorNear(centre + along * 1.18f + outward * 0.3f, 0.9f) || CivicSpots.DoorNear(centre - along * 1.18f + outward * 0.3f, 0.9f))
            {
                return false;       // a standard would stand in a doorway
            }
            return CivicSpots.BoxFree(centre + outward * (WallGap + depth * 0.5f + 0.1f) + Vector3.up * 1.25f, new Vector3(1.15f, 0.9f, depth * 0.5f - 0.12f), Quaternion.LookRotation(outward),
                CivicSpots.Solid & ~CivicSpots.Walls);
        }

        // The notice board: at the first of two places that has room for it and for whoever reads it.
        private static void Notice(Dressed dressed, Vector3 first, Vector3 firstFace, Vector3 second, Vector3 secondFace)
        {
            dressed.HasNotice = false;
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = i == 0 ? first : second;
                Vector3 face = i == 0 ? firstFace : secondFace;
                if (CitySpots.Stand(at, 0.5f, out Vector3 board) && CitySpots.Stand(at + face * 0.9f, 0.4f, out Vector3 reader))
                {
                    dressed.HasNotice = true;
                    dressed.NoticeAt = board;
                    dressed.NoticeFace = face;
                    dressed.Reader = reader;
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- setting it out, phase by phase

        private Transform Put(Dressed dressed, string id, Vector3 at, Vector3 forward)
        {
            Transform prop = _props.Stand(id, at, forward);
            if (prop != null)
            {
                dressed.Props.Add(prop);
            }
            return prop;
        }

        // On the carriageway: at the height of the road there, and (unless it lies flat) not where a vehicle stands now.
        private Transform PutOnRoad(Dressed dressed, string id, Vector3 at, Vector3 forward, bool flat = false)
        {
            if (CitySpots.Ground(at, out float ground))
            {
                at.y = ground;
            }
            if (!flat && Physics.CheckSphere(at + Vector3.up * 0.5f, 0.55f, TrafficTools.VehicleMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }
            return Put(dressed, id, at, forward);
        }

        // The lane beside works in the road: shut while they stand in it (stages 1 to 4), open again for the last.
        private bool Lane(Dressed dressed, float half)
        {
            int phase = dressed.Phase;
            if (phase < 4 && !dressed.LaneShut && dressed.LaneFromKerb < NeedsClosure + 1.5f && dressed.Road.Lane(dressed.Origin, 14f))
            {
                dressed.LaneShut = dressed.Road.Close(dressed.Road.LaneCentre, half + 2.5f);
            }
            else if (phase >= 4 && dressed.LaneShut)
            {
                dressed.Road.Open();
                dressed.LaneShut = false;
            }
            return dressed.LaneShut;
        }

        // What closes a lane to the eye: a barrier across each end, a taper of cones upstream, the sign on the pavement before it.
        private void LaneProps(Dressed dressed, float half)
        {
            Vector3 lane = dressed.Along, outward = dressed.Out;
            Vector3 laneCentre = dressed.Kerb + outward * dressed.LaneFromKerb;
            Vector3 upstream = laneCentre - lane * (half + 1.6f), downstream = laneCentre + lane * (half + 1.6f);
            if (dressed.Unsafe)
            {
                // The barrier at the upstream end has been knocked flat and left, and the taper was never laid.
                if (CitySpots.Ground(upstream, out float ground))
                {
                    upstream.y = ground;
                }
                Transform fallen = Put(dressed, "barrier", upstream + Vector3.up * 0.06f, lane);
                if (fallen != null)
                {
                    fallen.rotation = Quaternion.LookRotation(lane) * Quaternion.Euler(84f, 0f, 0f);
                }
            }
            else
            {
                PutOnRoad(dressed, "barrier", upstream, lane);
                for (int cone = 0; cone < 4; cone++)
                {
                    Vector3 kerbSide = dressed.Kerb + outward * 0.5f, laneSide = laneCentre + outward * 1.4f;
                    PutOnRoad(dressed, "cone", Vector3.Lerp(kerbSide, laneSide, cone / 3f) - lane * (half + 8.5f - cone * 2f), lane);
                }
            }
            PutOnRoad(dressed, "barrier", downstream, lane);
            if (CitySpots.Stand(dressed.Edge - outward * 0.5f - lane * (half + 13f), 0.45f, out Vector3 sign))
            {
                Put(dressed, "road_sign_works", sign, -lane);   // facing the traffic that comes
            }
        }

        private void BuildRoad(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 lane = dressed.Along, outward = dressed.Out, trench = dressed.Origin;
            bool shut = Lane(dressed, HalfTrench);
            if (!shut && phase < 4 && dressed.LaneFromKerb < NeedsClosure)
            {
                return;   // nothing may stand beside a live lane this near: Dress turns the place down
            }

            for (int piece = 0; piece < 4; piece++)
            {
                Vector3 at = trench + lane * (piece * 2f - 3f);
                switch (phase)
                {
                    case 0:
                        PutOnRoad(dressed, "road_marking", at + outward * 0.5f, lane, true);
                        PutOnRoad(dressed, "road_marking", at - outward * 0.45f, lane, true);
                        break;
                    case 1:
                        PutOnRoad(dressed, piece < 2 ? "trench_cover" : "road_marking", piece < 2 ? at : at + outward * 0.5f, lane, true);
                        break;
                    case 2:
                        PutOnRoad(dressed, "trench_cover", at, lane, true);
                        break;
                    default:
                        PutOnRoad(dressed, "asphalt_patch", at, lane, true);
                        break;
                }
            }
            if (phase == 1 || phase == 2)
            {
                PutOnRoad(dressed, "spoil_heap", trench + lane * (HalfTrench + 0.8f), lane);
                if (phase == 2)
                {
                    PutOnRoad(dressed, "spoil_heap", trench - lane * (HalfTrench + 0.8f), -lane);
                }
            }
            if (phase < 4)
            {
                // Cones along the outer edge of the works; with a site left unsafe, most of them are missing.
                for (int cone = 0; cone < 5; cone++)
                {
                    if (dressed.Unsafe && cone != 2)
                    {
                        continue;
                    }
                    PutOnRoad(dressed, "cone", trench + outward * 0.85f + lane * (cone * 2.2f - 4.4f), lane);
                }
                if (shut)
                {
                    LaneProps(dressed, HalfTrench);
                }
                if (CitySpots.Stand(dressed.Edge - outward * 0.8f + lane * 2.4f, 0.3f, out Vector3 box))
                {
                    Put(dressed, "toolbox", box, lane);
                }
            }

            // The crew stands on the pavement's edge and works on the strip in front of it.
            Vector3 edge = dressed.Edge - outward * 0.08f;
            Vector3 near = edge - lane * 1.7f, far = edge + lane * 0.9f, end = edge - lane * (HalfTrench + 3f);
            switch (phase)
            {
                case 0:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                    Plan(dressed, far, far + outward, CityLook.RoadWorker, "jackhammer", "jackhammer_idle");
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    break;
                case 2:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    Plan(dressed, far, far + outward, CityLook.RoadWorker, "shovel_rest", "shovel_dig");
                    break;
                case 3:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    break;
                default:
                    Plan(dressed, near, near + outward, CityLook.RoadWorker, "clipboard_write", null);
                    break;
            }
            if (shut && phase < 4)
            {
                Plan(dressed, end, end - lane, CityLook.RoadWorker, "paddle_hold", null);   // facing the traffic that comes
            }
        }

        private void BuildUtility(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 lane = dressed.Along, outward = dressed.Out;
            bool shut = Lane(dressed, 1.2f);
            if (!shut && phase < 4 && dressed.LaneFromKerb < NeedsClosure)
            {
                return;
            }
            Vector3 hole = dressed.Kerb + outward * 0.8f;
            if (CitySpots.Ground(hole, out float ground))
            {
                hole.y = ground;
            }
            bool open = phase >= 1 && phase <= 3;
            bool tent = phase == 1 || phase == 2;
            Put(dressed, open ? "manhole_open" : "manhole_lid", hole, lane);
            if (open)
            {
                PutOnRoad(dressed, "manhole_lid", hole + lane * 1.25f, lane, true);
            }
            if (tent)
            {
                PutOnRoad(dressed, "works_tent", hole, -outward);   // open towards the pavement, where its people are
            }
            // Cones at the corners; a site left unsafe has an open cover and one cone.
            for (int corner = 0; corner < 4 && phase < 4; corner++)
            {
                if (dressed.Unsafe && corner > 0)
                {
                    continue;
                }
                PutOnRoad(dressed, "cone", hole + lane * (corner % 2 == 0 ? -1.15f : 1.15f) + outward * (corner < 2 ? -0.55f : 0.95f), lane);
            }
            if (shut && phase < 4)
            {
                LaneProps(dressed, 1.2f);
            }
            if (phase == 2 && CitySpots.Stand(dressed.Edge - outward * 0.7f + lane * 1.6f, 0.3f, out Vector3 box))
            {
                Put(dressed, "toolbox", box, lane);
            }
            Vector3 kneel = dressed.Edge - outward * 0.08f, stand = dressed.Edge - outward * 0.5f - lane * 1.3f;
            if (open)
            {
                Plan(dressed, kneel, kneel + outward, CityLook.RoadWorker, "kneel_work", phase == 3 ? "arms_crossed" : null);
            }
            if (phase != 3)
            {
                Plan(dressed, stand, hole, CityLook.RoadWorker, "clipboard_write", null);
            }
        }

        private void BuildScaffold(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 foot = dressed.Kerb, outward = dressed.Out, along = dressed.Along;
            int bays = dressed.BayMax - dressed.BayMin + 1;
            for (int bay = dressed.BayMin; bay <= dressed.BayMax; bay++)
            {
                int order = bay - dressed.BayMin;
                bool partial = (phase == 0 || phase == 4) && order >= (bays + 1) / 2;   // going up, or coming down: half of it has only its first lift
                Vector3 at = foot + along * (bay * BayWidth) + outward * WallGap;
                Put(dressed, "scaffold_bay", at, outward);
                int lifts = partial ? 0 : phase == 0 || phase == 4 ? Mathf.Min(1, dressed.Lifts) : dressed.Lifts;
                for (int lift = 0; lift < lifts; lift++)
                {
                    Vector3 up = at + Vector3.up * (DeckHeight + lift * LiftHeight);
                    Put(dressed, "scaffold_bay_upper", up, outward);
                    if (phase >= 1 && phase <= 3 && !(dressed.Unsafe && order == 0))
                    {
                        Put(dressed, "scaffold_net", up, outward);   // an unsafe site has a bay with its netting off
                    }
                }
                if (phase >= 1 && phase <= 3 && !dressed.Unsafe)
                {
                    Put(dressed, "scaffold_fan", at, outward);       // and no fan over the walkway
                }
            }
            if (dressed.HasSkip && phase >= 1)
            {
                PutOnRoad(dressed, "skip_bin", dressed.SkipAt, Vector3.Cross(dressed.SkipAlong, Vector3.up));   // its long side along the kerb
            }

            // People: on the first deck (a painter, a worker) and on the pavement at the end of the run.
            Vector3 deck = foot + outward * 0.92f;
            Vector3 beside = foot + along * (dressed.BayMin * BayWidth - BayWidth * 0.5f - 1.2f) + outward * 1.5f;
            if (dressed.HasNotice && CivicCity.Flat(dressed.NoticeAt - beside) < 1.6f * 1.6f)
            {
                beside = foot + along * (dressed.BayMax * BayWidth + BayWidth * 0.5f + 1.2f) + outward * 1.5f;
            }
            switch (phase)
            {
                case 0:
                case 4:
                    Plan(dressed, beside, beside - outward, CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    Plan(dressed, beside + along * 1.3f + outward * 0.4f, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                    Plan(dressed, beside, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 2:
                    Plan(dressed, deck, deck - outward, CityLook.RoadWorker, "kneel_work", "arms_crossed", DeckHeight + 0.02f);
                    Plan(dressed, beside, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                default:
                    Plan(dressed, deck, deck - outward, CityLook.Painter, "paint_roll", null, DeckHeight - 0.24f);   // the clip stands 0.24 up, on a ladder's tread
                    Plan(dressed, beside, foot, CityLook.RoadWorker, "arms_crossed", null);
                    break;
            }
        }

        private void BuildRefit(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 foot = dressed.Kerb, outward = dressed.Out, along = dressed.Along;
            int bays = dressed.BayMax - dressed.BayMin + 1;
            for (int bay = dressed.BayMin; bay <= dressed.BayMax; bay++)
            {
                int order = bay - dressed.BayMin;
                bool notice = bay == 0 && phase >= 2;
                if ((phase == 0 && order >= (bays + 1) / 2) || (phase == 4 && !notice && bays > 1) || (dressed.Unsafe && order == bays - 1 && bays > 1))
                {
                    continue;   // not up yet; coming down; or the panel a careless contractor left out
                }
                Put(dressed, notice ? "site_hoarding_notice" : "site_hoarding", foot + along * (bay * BayWidth) + outward * WallGap, outward);
            }
            if (dressed.HasSkip && phase >= 1 && phase <= 2)
            {
                PutOnRoad(dressed, "skip_bin", dressed.SkipAt, Vector3.Cross(dressed.SkipAlong, Vector3.up));
            }
            Vector3 front = foot + outward * 1.6f;
            int paintBay = dressed.BayMax > 0 ? dressed.BayMax : dressed.BayMin < 0 ? dressed.BayMin : 0;
            Vector3 painter = foot + along * (paintBay * BayWidth) + outward * (WallGap + 0.02f + 0.80f);
            switch (phase)
            {
                case 2:
                    Plan(dressed, foot + along * (dressed.BayMin * BayWidth) + outward * 0.8f, foot + along * (dressed.BayMin * BayWidth), CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    break;
                case 3:
                    // The painter on the first tread of a ladder that stands between them and the hoarding
                    // (the ladder is climbed from its -Z side, so its +Z looks at the hoarding as the painter does).
                    if (CitySpots.Stand(painter, 0.45f, out Vector3 stand) && Put(dressed, "step_ladder", stand - outward * 0.24f, -outward) != null)
                    {
                        Put(dressed, "bucket", stand - outward * 0.15f + along * 0.62f, outward);
                        Plan(dressed, stand, stand - outward, CityLook.Painter, "paint_roll", null);
                    }
                    break;
                default:
                    Plan(dressed, front, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
            }
        }
    }

    /// <summary>
    /// The few physics questions civic life asks that city life's CitySpots does not answer:
    /// is this box free, is there a wall this way, is a door near. Layer names as CitySpots has them.
    /// </summary>
    internal static class CivicSpots
    {
        private static bool _ready;
        private static int _walls, _solid, _doors, _standing;

        public static int Walls { get { Setup(); return _walls; } }
        public static int Solid { get { Setup(); return _solid; } }

        /// <summary>Vehicles that are standing where they are: parked ones and the player's own.</summary>
        public static int StandingVehicles { get { Setup(); return _standing; } }

        private static void Setup()
        {
            if (_ready)
            {
                return;
            }
            _ready = true;
            int ground = LayerMask.GetMask("Roads", "Ground", "GroundUnplacable");
            _doors = LayerMask.GetMask("InteractiveItems", "InteractiveItemsOutlined");
            _walls = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
            _solid = ~(ground | LayerMask.GetMask("UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water"));
            _standing = LayerMask.GetMask("ParkedVehicles", "PlayerVehicles", "Vehicles");
        }

        public static bool BoxFree(Vector3 centre, Vector3 halfExtents, Quaternion rotation, int mask)
        {
            Setup();
            return !Physics.CheckBox(centre, halfExtents, rotation, mask, QueryTriggerInteraction.Ignore);
        }

        public static bool Wall(Vector3 from, Vector3 direction, float height, float reach, out RaycastHit hit)
        {
            Setup();
            return Physics.Raycast(from + Vector3.up * height, direction, out hit, reach, _walls, QueryTriggerInteraction.Ignore);
        }

        public static bool DoorNear(Vector3 point, float radius)
        {
            Setup();
            return Physics.CheckSphere(point + Vector3.up, radius, _doors, QueryTriggerInteraction.Collide);
        }
    }
}
