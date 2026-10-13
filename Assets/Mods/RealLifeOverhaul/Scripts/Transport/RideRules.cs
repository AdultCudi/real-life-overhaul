using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of a bus ride played in real time (TransitDirector.Journey, BusRidePanel): how far along it the rider is,
    /// which stop a rider who asks to get off is let off at, when a bus that gets nowhere is given up and the rest of the
    /// ride skipped, what a save written on the way gives back, and how long a ride takes as the bus drives it.
    ///
    /// Game-free: nothing here knows of Unity. EconomyCheck (BusInsideChecks.cs) checks it.
    /// </summary>
    public static class RideRules
    {
        /// <summary>
        /// Seconds a bus on the road may go without getting a metre further before the rest of the ride is skipped. Seconds of
        /// the game's own pace, as everything on the street runs: none pass while the game is paused, six a second in fast forward.
        /// </summary>
        public const float StuckDriving = 60f;

        /// <summary>...and whatever it is doing, standing at a stop included (a stand is 34 s at the most, a wait to pull out 45).</summary>
        public const float StuckAny = 120f;

        /// <summary>Seconds (the game's pace) a whole ride may take before the rest is skipped, however it is getting on.</summary>
        public const float LongestRide = 900f;

        /// <summary>The stop a bus has just left is behind it once it has driven this far from it.</summary>
        public const float PastStop = 1f;

        // What a ride is made of, for the estimate of how long one takes (TransitBus, TransitDirector, TransitTrips).
        public const float Cruise = 11f;            // m/s, TransitBus's 40 km/h
        public const float StopApart = 330f;        // metres between two stops of a line, on average (TransitTrips.StopEvery)
        public const float StandEmpty = 4f;         // seconds at a stop where nobody gets on or off (TransitDirector.DwellEmpty)
        public const float StandUsual = 9f;         // ...and otherwise, at the least (TransitDirector.DwellLeast)
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

        /// <summary>The bar never goes back (a bus re-routed round a corner reckons its metres afresh).</summary>
        public static float Advance(float shown, float now)
        {
            return Math.Max(shown, Math.Max(0f, Math.Min(1f, now)));
        }

        /// <summary>
        /// The stop a rider who asks to get off is let off at (as orders along the line): the stop the bus stands at while its
        /// doors are open, else the next one it will call at (the one it is driving to, or after the one it has just left).
        /// Never past the stop the ticket was for.
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

        /// <summary>Whether a bus that has got nowhere for this many seconds (the game's pace) is given up and the rest of the ride skipped.</summary>
        public static bool Stuck(float stillSeconds, bool standingAtStop, float rideSeconds)
        {
            return rideSeconds >= LongestRide || stillSeconds >= (standingAtStop ? StuckAny : StuckDriving);
        }

        /// <summary>Game minutes left of a ride when the rest of it is skipped: the part not yet ridden of the ride's own time, a minute at the least.</summary>
        public static float MinutesLeft(float rideMinutes, float progress)
        {
            return Math.Max(1f, rideMinutes * (1f - Math.Max(0f, Math.Min(1f, progress))));
        }

        /// <summary>
        /// Seconds the bus takes from rest at one stop to rest at the next this many metres along a straight road, at its own
        /// pace (BusPath: it eases away, cruises, and brakes to stand exactly where it should), in steps of the physics.
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
        /// Real seconds a ride of so many stops takes at the game's normal speed: the drive between each two and the stand at each
        /// stop on the way (not the one got off at). Red lights and traffic not counted.
        /// </summary>
        public static float RideSeconds(int stops, float apart = StopApart, float stand = StandUsual)
        {
            if (stops <= 0)
            {
                return 0f;
            }
            return stops * DriveSeconds(apart) + (stops - 1) * stand;
        }

        // ---------------------------------------------------------------- a Going Public session

        /// <summary>
        /// Whether a ride is driven in real time: alone, or in a Going Public session on a machine whose street is its own
        /// (the host's). Riding in real time takes nothing of the shared clock: the bus is this machine's own object, not
        /// the traffic, and the other players see the rider's figure where Going Public sends it, seated, along the road.
        /// A guest's street is the host's (its traffic mirrored, its parked cars the host's), so a guest has no buses at all,
        /// as before: nothing of the host's may be taken, stopped or steered.
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
