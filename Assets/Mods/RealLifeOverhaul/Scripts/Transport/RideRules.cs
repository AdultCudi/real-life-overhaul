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
        /// <summary>
        /// Seconds a bus may go without getting a metre further before the rest of the ride is skipped. Seconds of the game's
        /// pace, as everything on the street runs: none while paused, six a second in fast forward.
        /// </summary>
        public const float StuckDriving = 60f;

        /// <summary>The same whatever it is doing, standing at a stop included (a stand is 34 s at most, a wait to pull out 45).</summary>
        public const float StuckAny = 120f;

        /// <summary>Seconds (the game's pace) a whole ride may take before the rest is skipped, however it is getting on.</summary>
        public const float LongestRide = 900f;

        /// <summary>Metres from the stop a bus has just left after which it is behind it.</summary>
        public const float PastStop = 1f;

        // What a ride is made of, for the estimate of how long one takes.
        public const float Cruise = 11f;            // m/s, TransitBus's 40 km/h
        public const float StopApart = 330f;        // metres between two stops of a line, on average (TransitTrips.StopEvery)
        public const float StandUsual = 9f;         // seconds at a stop, at the least (TransitDirector.DwellLeast)
        public const float FastForward = 6f;        // the game's fast forward: its clock and everything on the street six times as fast

        /// <summary>The part of the ride done, 0 to 1, by metres along the line: from the stop boarded at to the one got off at.</summary>
        public static float Progress(float fromMetres, float toMetres, float atMetres)
        {
            if (toMetres <= fromMetres + 0.01f)
            {
                return 1f;
            }
            return Math.Max(0f, Math.Min(1f, (atMetres - fromMetres) / (toMetres - fromMetres)));
        }

        /// <summary>
        /// Metres along the line the bus is at: short of the stop it is driving to by what it has still to drive, at a stop
        /// it stands at, or at the stop it has just left.
        /// </summary>
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

        /// <summary>
        /// The order of the stop a rider who asks to get off is let off at: the one the bus stands at with its doors open,
        /// else the next it will call at. Never past the stop the ticket was for.
        /// </summary>
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

        /// <summary>Stops still to come, the one got off at included: what the panel's bar says.</summary>
        public static int StopsLeft(int nextOrder, int finalOrder)
        {
            return Math.Max(0, finalOrder - nextOrder + 1);
        }

        /// <summary>Whether a bus that has got nowhere for so many seconds (the game's pace) is given up.</summary>
        public static bool Stuck(float stillSeconds, bool standingAtStop, float rideSeconds)
        {
            return rideSeconds >= LongestRide || stillSeconds >= (standingAtStop ? StuckAny : StuckDriving);
        }

        /// <summary>Game minutes left of a ride when the rest is skipped: the share not yet ridden, a minute at the least.</summary>
        public static float MinutesLeft(float rideMinutes, float progress)
        {
            return Math.Max(1f, rideMinutes * (1f - Math.Max(0f, Math.Min(1f, progress))));
        }

        /// <summary>
        /// Seconds the bus takes from rest at one stop to rest at the next so many metres along a straight road, driven at
        /// BusPath's pace in physics steps.
        /// </summary>
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

        /// <summary>
        /// Real seconds a ride of so many stops takes at the game's normal speed: each drive and each stand on the way. Red
        /// lights and traffic not counted.
        /// </summary>
        public static float RideSeconds(int stops, float apart = StopApart, float stand = StandUsual)
        {
            if (stops <= 0)
            {
                return 0f;
            }
            return stops * DriveSeconds(apart) + (stops - 1) * stand;
        }

        /// <summary>
        /// Seconds a bus held by something on its way to a stop stands still before it counts as standing at the stop where it
        /// is (<see cref="StandsWhereItIs"/>).
        /// </summary>
        public const float StandShortAfter = 2f;

        /// <summary>Seconds a bus stands still on the road before what holds it is written to the log, once a hold.</summary>
        public const float HoldLogAfter = 5f;

        /// <summary>Metres a bus must move for a hold to be over, so creeping up behind a queue is one hold.</summary>
        public const float HoldMoved = 1f;

        /// <summary>
        /// Seconds a bus stands behind a car of the traffic that stands for the bus itself (the bus is in its sensor) before the
        /// car is told the bus is not in its way: two drivers each waiting for the other never move otherwise.
        /// </summary>
        public const float YieldAfter = 4f;

        /// <summary>Degrees off the lane's direction a bus may stand at for its doors to be by the kerb.</summary>
        public const float KerbAngleMost = 15f;

        /// <summary>Metres onto the pavement a door's side may reach and still count as by the kerb.</summary>
        public const float KerbOver = 0.3f;

        /// <summary>
        /// Metres of route a bus held up by the player may still have and stand there for them: the player in the road at the
        /// stop holds its nose up to 6 m short of them.
        /// </summary>
        public const float PlayerHoldsWithin = 16f;

        /// <summary>
        /// Whether the front or middle door (metres along the lane from the stop's lane point) is within the stop's kerb strip,
        /// <paramref name="stand"/> on from the pole, on the kerb's side of the lane's line and not over the kerb, with the bus
        /// near enough parallel to it. <paramref name="doorAside"/> is metres from the lane's line to the door side.
        /// </summary>
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

        /// <summary>
        /// Whether a bus held still for <paramref name="stillFor"/> seconds on its way to a stop is at the stop where it is: a
        /// door by the kerb, or held by the player near its place (each would wait for the other for ever). Its doors then open.
        /// </summary>
        public static bool StandsWhereItIs(float stillFor, bool doorOnKerb, bool heldByPlayer, float routeLeft)
        {
            return stillFor >= StandShortAfter && (doorOnKerb || (heldByPlayer && routeLeft <= PlayerHoldsWithin));
        }

        /// <summary>
        /// Whether a car of the traffic a bus stands behind is told the bus is not in its way: the traffic is this machine's own,
        /// the car stands for the bus, the bus is not on the car's way ahead, and once a hold.
        /// </summary>
        public static bool Yield(float heldFor, bool trafficOurs, bool carStanding, bool waitsForBus, bool busOnItsWay, bool yielded)
        {
            return !yielded && trafficOurs && carStanding && waitsForBus && !busOnItsWay && heldFor >= YieldAfter;
        }

        /// <summary>
        /// How long a bus has stood still on the road, looked at four times a second: a hold is over once the bus has moved
        /// <see cref="HoldMoved"/> from where it began. What is done once a hold is kept here and forgotten with it.
        /// </summary>
        public sealed class HoldClock
        {
            private float _x, _z, _since;
            private bool _on;

            /// <summary>True once the hold has been written to the log.</summary>
            public bool Logged;

            /// <summary>True once a car it stood behind has been told the bus is not in its way.</summary>
            public bool Yielded;

            /// <summary>Seconds of the hold so far, as of the last look.</summary>
            public float For { get; private set; }

            /// <summary>Takes where the bus is now (metres, on the ground); returns the seconds it has stood within a metre of where it stopped.</summary>
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
                }
                For = now - _since;
                return For;
            }

            /// <summary>Not on the road (standing at a stop, put away): no hold.</summary>
            public void Clear()
            {
                _on = false;
                Logged = false;
                Yielded = false;
                For = 0f;
            }
        }

        /// <summary>
        /// Whether a ride is driven in real time: alone, or as the host of a Going Public session. The bus is this machine's own
        /// object, so nothing of the shared clock is taken. A guest's street is the host's (its traffic mirrored), so a guest
        /// has no buses: nothing of the host's may be taken, stopped or steered.
        /// </summary>
        public static bool RealTime(bool trafficMirrored)
        {
            return !trafficMirrored;
        }

        /// <summary>The panel's fast forward: never in a session (it sets the game's speed, which is everyone's).</summary>
        public static bool FastForwardOffered(bool running, bool clockShared)
        {
            return running && !clockShared;
        }

        /// <summary>The panel's skip: always while it runs; in a session it is the fade that passes no time (TransitRide).</summary>
        public static bool SkipOffered(bool running)
        {
            return running;
        }

        /// <summary>Game minutes a skip of the rest passes: what is left alone, none in a session (the clock is everyone's).</summary>
        public static float SkipMinutes(float minutesLeft, bool clockShared)
        {
            return clockShared ? 0f : minutesLeft;
        }
    }
}
