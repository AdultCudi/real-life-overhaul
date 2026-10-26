using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of a bus ride played in real time: how far along the rider is, where a rider who asks to get off is let off,
    /// when a bus getting nowhere is given up, how long a ride takes, and when a bus held up short of its place counts as at
    /// the stop. Nothing of Unity in it; EconomyCheck (BusJourneyChecks) checks it.
    /// </summary>
    public static class RideRules
    {
        public const float StuckDriving = 60f;      // seconds of the game's pace without a metre gained before the rest of the ride is skipped

        public const float StuckAny = 120f;         // and whatever it is doing, standing included (a stand is 34 s at most, a wait to pull out 45)

        public const float LongestRide = 900f;      // seconds of the game's pace a whole ride may take before the rest is skipped

        public const float PastStop = 1f;           // metres from the stop it has just left after which a bus is past it

        // What a ride is made of, for the estimate of how long one takes.
        public const float Cruise = 11f;            // m/s, TransitBus's 40 km/h
        public const float StopApart = 330f;        // metres between two stops of a line, on average (TransitTrips.StopEvery)
        public const float StandUsual = 9f;         // seconds at a stop, at the least (TransitDirector.DwellLeast)
        public const float FastForward = 6f;        // the game's fast forward: its clock and everything on the street six times as fast

        /// <summary>The share of the ride done, 0 to 1, by metres along the line.</summary>
        public static float Progress(float fromMetres, float toMetres, float atMetres)
        {
            if (toMetres <= fromMetres + 0.01f)
            {
                return 1f;
            }
            return Math.Max(0f, Math.Min(1f, (atMetres - fromMetres) / (toMetres - fromMetres)));
        }

        /// <summary>Metres along the line the bus is at: short of its target by what it still has to drive, at a stop it stands at, or just past one.</summary>
        public static float AtMetres(float targetMetres, float remaining, bool approaching, bool standing, float passedMetres)
        {
            if (approaching)
            {
                return Math.Max(passedMetres, targetMetres - Math.Max(0f, remaining));
            }
            return standing ? targetMetres : passedMetres;
        }

        /// <summary>The bar never goes back, though a bus re-routed round a corner reckons its metres afresh.</summary>
        public static float Advance(float shown, float now)
        {
            return Math.Max(shown, Math.Max(0f, Math.Min(1f, now)));
        }

        /// <summary>The stop a rider who asks to get off is let off at: the one the bus stands at with its doors open, else the next; never past theirs.</summary>
        public static int GetOffAt(int passedOrder, int targetOrder, bool standingAtTarget, bool doorsOpen, int finalOrder)
        {
            int at;
            if (standingAtTarget && targetOrder > passedOrder)
            {
                at = doorsOpen ? targetOrder : targetOrder + 1;
            }
            else
            {
                at = targetOrder > passedOrder ? targetOrder : passedOrder + 1;
            }
            return Math.Min(at, finalOrder);
        }

        /// <summary>Stops still to come, the one got off at included.</summary>
        public static int StopsLeft(int nextOrder, int finalOrder)
        {
            return Math.Max(0, finalOrder - nextOrder + 1);
        }

        public static bool Stuck(float stillSeconds, bool standingAtStop, float rideSeconds)
        {
            return rideSeconds >= LongestRide || stillSeconds >= (standingAtStop ? StuckAny : StuckDriving);
        }

        /// <summary>Game minutes left of a ride when the rest is skipped, a minute at the least.</summary>
        public static float MinutesLeft(float rideMinutes, float progress)
        {
            return Math.Max(1f, rideMinutes * (1f - Math.Max(0f, Math.Min(1f, progress))));
        }

        /// <summary>Seconds the bus takes from rest to rest so many metres along a straight road, at BusPath's pace.</summary>
        public static float DriveSeconds(float metres, float cruise = Cruise, float dt = 0.02f)
        {
            float s = 0f, speed = 0f, pull = 0f, time = 0f;
            while (time < 3600f)
            {
                float room = metres - s;
                float wanted = Math.Min(cruise, BusPath.StopIn(room));
                speed = BusPath.Pace(speed, ref pull, wanted, dt);
                s += Math.Min(speed * dt, Math.Max(0f, room));
                time += dt;
                if (metres - s <= 0.02f && speed < 0.25f)
                {
                    break;
                }
            }
            return time;
        }

        /// <summary>Real seconds a ride of so many stops takes, the stands on the way included; lights and traffic not.</summary>
        public static float RideSeconds(int stops, float apart = StopApart, float stand = StandUsual)
        {
            if (stops <= 0)
            {
                return 0f;
            }
            return stops * DriveSeconds(apart) + (stops - 1) * stand;
        }

        public const float StandShortAfter = 2f;    // seconds a bus held still on its way stands before it counts as at the stop where it is

        public const float HoldLogAfter = 5f;       // seconds held still on the road before the hold goes in the log, once

        public const float HoldMoved = 1f;          // metres a bus must move for a hold to be over, so creeping up a queue is one hold

        public const float YieldAfter = 4f;         // seconds behind a car that stands for the bus before the car is told to go: else neither ever moves

        public const float KerbAngleMost = 15f;     // degrees off the lane a bus may stand at for its doors to be by the kerb

        public const float KerbOver = 0.3f;         // metres onto the pavement a door's side may reach and still count as by the kerb

        public const float PlayerHoldsWithin = 16f; // metres of route a bus held by the player in the road may have left and stand there

        /// <summary>Whether a door is by the stop's kerb: along within its kerb strip, on the kerb's side and not over it, near enough parallel.</summary>
        public static bool DoorOnKerb(float frontAlong, float middleAlong, float doorAside, float angle, float stand, float kerbAside)
        {
            if (angle > KerbAngleMost || doorAside <= 0f || doorAside > kerbAside + KerbOver)
            {
                return false;
            }
            float from = BusInside.MiddleDoor + stand - BusInside.KerbBeyond;
            float to = BusInside.FrontDoor + stand + BusInside.KerbBeyond;
            return (frontAlong >= from && frontAlong <= to) || (middleAlong >= from && middleAlong <= to);
        }

        /// <summary>Whether a bus held still on its way is at the stop where it is: a door by the kerb, or held by the player near its place.</summary>
        public static bool StandsWhereItIs(float stillFor, bool doorOnKerb, bool heldByPlayer, float routeLeft)
        {
            return stillFor >= StandShortAfter && (doorOnKerb || (heldByPlayer && routeLeft <= PlayerHoldsWithin));
        }

        /// <summary>Whether a car a bus stands behind is told the bus is not in its way: this machine's traffic, the car waits for the bus, once a hold.</summary>
        public static bool Yield(float heldFor, bool trafficOurs, bool carStanding, bool waitsForBus, bool busOnItsWay, bool yielded)
        {
            return !yielded && trafficOurs && carStanding && waitsForBus && !busOnItsWay && heldFor >= YieldAfter;
        }

        public const float LightPatience = 45f;      // seconds at a red light nobody is looking at before it is taken for one the game does not run

        public const float LightPatienceSeen = 90f;  // and one in view: longer than any of the game's own cycles, but never for good

        public const float RoundClosedAfter = 3f;    // seconds before a closed lane before the bus goes round it, as the traffic does

        public const float RoundStillAfter = 30f;    // seconds behind something standing still in its lane (a car, a person) before it goes round

        /// <summary>
        /// Whether a red light still holds a vehicle the mod drives (the bus, the taxi): it is waited at for as long as any of the
        /// game's light cycles takes, longer while the player can see it, and then taken for one the game is not running (it
        /// runs only the junctions near the player; a light it does not run stays as it was for good). Before, a light in view
        /// was waited at for ever.
        /// </summary>
        public static bool LightHolds(float waitedFor, bool seen)
        {
            return waitedFor < (seen ? LightPatienceSeen : LightPatience);
        }

        /// <summary>
        /// Whether a bus held up goes round what holds it (BusDetour): a lane closed ahead after a moment, something standing
        /// still in its lane after half a minute, or a moment when a lit emergency vehicle waits behind it (MakeWayRules.RoundWaits;
        /// not a car that only stands for the bus: that one is told to go on), once a hold.
        /// </summary>
        public static bool GoRound(bool closed, float heldFor, bool holderStill, bool waitsForBus, bool tried, bool unitBehind = false)
        {
            if (tried)
            {
                return false;
            }
            return closed ? heldFor >= RoundClosedAfter : holderStill && !waitsForBus && heldFor >= MakeWayRules.RoundWaits(RoundStillAfter, unitBehind);
        }

        /// <summary>How long a bus has stood still, looked at four times a second; what is done once a hold is kept here and forgotten with it.</summary>
        public sealed class HoldClock
        {
            private float _x, _z, _since;
            private bool _on;

            public bool Logged;     // the hold has been written to the log

            public bool Yielded;    // a car it stood behind has been told the bus is not in its way

            public bool Rounded;    // a way round what held it has been looked for

            public float For { get; private set; }   // seconds of the hold, as of the last look

            /// <summary>Takes where the bus is now (metres on the ground); returns the seconds it has stood within a metre of where it stopped.</summary>
            public float At(float x, float z, float now)
            {
                float dx = x - _x, dz = z - _z;
                if (!_on || dx * dx + dz * dz > HoldMoved * HoldMoved)
                {
                    _on = true;
                    _x = x;
                    _z = z;
                    _since = now;
                    Logged = false;
                    Yielded = false;
                    Rounded = false;
                }
                For = now - _since;
                return For;
            }

            /// <summary>Not on the road: no hold.</summary>
            public void Clear()
            {
                _on = false;
                Logged = false;
                Yielded = false;
                Rounded = false;
                For = 0f;
            }
        }

        public const float StartLookEvery = 0.5f;    // seconds between looks at whether a bus held at its start can be seen (a few rays a look)

        /// <summary>
        /// A bus held where it was put on the road, so as not to be at its stop early (TransitDirector.Put). It stands there only
        /// while nobody can see it: it is looked at every <see cref="StartLookEvery"/> seconds while it holds, and the look that
        /// finds it seen lets the rest of the hold go, so it drives on at once and is early at its stop by that much (the panel
        /// follows its estimate down; the timetable starts again from when it stood there). Before, a bus put out of sight that
        /// the player then saw stood still in its lane for the rest of its hold, looking stuck. A few comparisons a tick.
        /// </summary>
        public sealed class StartHold
        {
            private float _lookAt;

            /// <summary>When the hold ends by the timetable; 0 when there is none.</summary>
            public float Until { get; private set; }

            /// <summary>Seconds of the hold let go because the bus was seen; 0 if it held to the end or had none.</summary>
            public float Cut { get; private set; }

            /// <summary>Holds it for so many seconds from now; looked at first at once.</summary>
            public void Set(float now, float seconds)
            {
                Until = seconds > 0f ? now + seconds : 0f;
                Cut = 0f;
                _lookAt = now;
            }

            public void Clear()
            {
                Until = 0f;
                Cut = 0f;
                _lookAt = 0f;
            }

            /// <summary>True while it stands at its start.</summary>
            public bool Holds(float now) => now < Until;

            /// <summary>Seconds of the hold still to come.</summary>
            public float Left(float now) => Math.Max(0f, Until - now);

            /// <summary>True when it holds and is due to be looked at.</summary>
            public bool LookDue(float now) => now < Until && now >= _lookAt;

            /// <summary>What a look found. Seen, the rest of the hold is let go and true is returned: it drives on now.</summary>
            public bool Looked(float now, bool seen)
            {
                _lookAt = now + StartLookEvery;
                if (!seen || now >= Until)
                {
                    return false;
                }
                Cut = Until - now;
                Until = now;
                return true;
            }
        }

        /// <summary>
        /// Whether a ride is driven in real time: alone, or as a Going Public host, the bus being this machine's own object. A
        /// guest's street is the host's, so a guest has no buses: nothing of the host's may be taken, stopped or steered.
        /// </summary>
        public static bool RealTime(bool trafficMirrored)
        {
            return !trafficMirrored;
        }

        /// <summary>The panel's fast forward: never in a session, as it sets the game's speed, which is everyone's.</summary>
        public static bool FastForwardOffered(bool running, bool clockShared)
        {
            return running && !clockShared;
        }

        /// <summary>The panel's skip: always; in a session it is the fade that passes no time.</summary>
        public static bool SkipOffered(bool running)
        {
            return running;
        }

        /// <summary>Game minutes a skip of the rest passes: none in a session, the clock being everyone's.</summary>
        public static float SkipMinutes(float minutesLeft, bool clockShared)
        {
            return clockShared ? 0f : minutesLeft;
        }
    }
}
