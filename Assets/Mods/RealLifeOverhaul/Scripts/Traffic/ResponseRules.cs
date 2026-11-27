using System;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How fast the emergency services answer a call, as plain arithmetic (EconomyCheck's --rescue29 runs every rule over the city's
    /// distances): how long after the call a unit is sent, how far off it starts (by road), how fast it drives, how long the drive
    /// takes, how soon its crews are on their feet and at work, how long a scene lasts, and what the player is told. The one rule for
    /// every road response: a staged accident, somebody knocked down, a 911 report, the fire engine and its patrol car, an arrest.
    /// The game's own police and ambulance have no behaviour of their own to copy (the decompiled game has none), so these figures are
    /// the mod's. Nothing here reads the game or the clock, nothing is allocated except the verdict's text.
    /// </summary>
    public static class ResponseRules
    {
        // ---- Sent: seconds after the call (game seconds: the game's own clock, which a fast-forward speeds up).
        public const float AmbulanceDelayLeast = 3f;    // s: dispatch of the ambulance...
        public const float AmbulanceDelayMost = 6f;     // ...up to this
        public const float PoliceDelayLeast = 3f;       // s: the patrol car...
        public const float PoliceDelayMost = 9f;        // ...up to this
        public const float PoliceBehindAmbulance = 3f;  // s: on a single lane the patrol car waits for the ambulance, which goes first, nearest the hurt
        public const float CallDelay = 1f;              // s: a 911 report sends whoever is still waiting this soon
        public const float RetryAfter = 6f;             // s: no unit could be put on the road (none free, no start): tried once more
        public const float RetryUnseenAfter = 2f;       // s: its start would have shown in the camera: another start, soon

        // ---- Start: metres of road from the unit's start to its place at the scene. Never nearer than it takes to start out of view.
        public const float AmbulanceRouteLeast = 100f;  // m: the walk back along the road from the scene goes on until it is this long...
        public const float AmbulanceRouteMost = 130f;   // ...and is asked for between these
        public const float PoliceRouteLeast = 100f;
        public const float PoliceRouteMost = 140f;
        public const float AmbulanceMinimum = 60f;      // m: a route shorter than this is tried again; a second try settles for the next
        public const float AmbulanceMinimumRetry = 35f;
        public const float PoliceMinimum = 50f;
        public const float PoliceMinimumRetry = 25f;
        public const float WalkOvershoot = 45f;         // m: the walk back stops at the first road point past its length, whose legs are up to this long

        // ---- Drive.
        public const float Speed = 22f;                 // m/s, about 79 km/h: an ambulance or patrol car answering a call; the traffic's own is 14
        public const float FireSpeed = 18f;             // m/s, about 65 km/h: the engine is heavier
        public const float TrafficSpeed = 14f;          // m/s, about 50 km/h: what a unit drove at before, no faster than the cars it came up behind
        public const float Pickup = 6f;                 // m/s2 it gathers speed with up to the traffic's pace (EmergencyDriver.Pickup)...
        public const float PickupAbove = 2.76f;         // ...and on average above it up to 22 m/s (the fast drive's pick-up curve, 6 falling to 1.2: about 2.9 s from 14 to 22)
        public const float StopRun = 30f;               // m before its place within which it plans its stop at 4 m/s2 (EmergencyDriver: a stop of 30 m takes about 3.9 s at any speed)
        public const float BendTax = 0.012f;            // s a metre: slowing for the bends and junctions of a city route
        public const float HoldUpSlack = 12f;           // s a response may take beyond its own drive, held up by traffic and people, and still be on time

        // ---- On foot.
        public const float CrewPace = 1.8f;             // m/s the crew carries the bag, pushes and pulls the stretcher at (a walking clip played 1.5 times its own speed)
        public const float WalkPace = 1.2f;             // m/s the same clips are made for, and what they were held to
        public const float ClipMost = 1.6f;             // times its own speed a walking clip is matched to the ground at most (ActivityAnimator.MatchSpeed)
        public const float RunFrom = 6f;                // m: someone with nothing in their hands runs a way longer than this, and walks a shorter one
        public const float TreatInjured = 6f;           // s the paramedics work at the chest of somebody alive, from...
        public const float TreatInjuredMost = 11f;      // ...to
        public const float TreatFatalLeast = 4f;        // s of compressions for somebody who is dying or dead...
        public const float TreatFatalMost = 6f;         // ...to
        public const float TreatDriverLeast = 6f;       // s at a driver's side...
        public const float TreatDriverMost = 10f;       // ...to

        // ---- Clearing up: how long a scene lasts.
        public const float SingleLeast = 90f;           // s: somebody knocked down, the scene's own limit...
        public const float SingleMost = 130f;           // ...from to

        /// <summary>What the mod did until 1.0.32, kept so that a check can hold the gain.</summary>
        public static class Before
        {
            public const float AmbulanceDelayLeast = 5f, AmbulanceDelayMost = 13f;   // a staged accident (a person knocked down: 4 to 10)
            public const float PoliceDelayLeast = 3f, PoliceDelayMost = 22f;         // (a person knocked down: 3 to 14)
            public const float PoliceBehindAmbulance = 5f;
            public const float AmbulanceRoute = 170f;      // m: the walk back asked for, never under 60 (30 the second time)
            public const float Speed = 14f;                // m/s
            public const float CrewPace = 1.2f;            // m/s
            public const float TreatInjuredMost = 16f;
            public const float SingleMost = 165f;
        }

        // ---- The unit.

        public static float Delay(bool police, float roll)
        {
            return police ? Lerp(PoliceDelayLeast, PoliceDelayMost, roll) : Lerp(AmbulanceDelayLeast, AmbulanceDelayMost, roll);
        }

        /// <summary>When the patrol car goes: its own delay, and on a single lane no sooner than the ambulance's plus the gap.</summary>
        public static float PoliceAt(float own, float ambulanceAt, bool ambulanceGoes, bool farEnd, bool chasePlanned)
        {
            return ambulanceGoes && !farEnd && !chasePlanned ? Math.Max(own, ambulanceAt + PoliceBehindAmbulance) : own;
        }

        public static float RouteLength(bool police, float roll)
        {
            return police ? Lerp(PoliceRouteLeast, PoliceRouteMost, roll) : Lerp(AmbulanceRouteLeast, AmbulanceRouteMost, roll);
        }

        public static float RouteMinimum(bool police, bool retried)
        {
            return police ? (retried ? PoliceMinimumRetry : PoliceMinimum) : (retried ? AmbulanceMinimumRetry : AmbulanceMinimum);
        }

        /// <summary>The farthest a start can be by road: the walk's length and its last leg.</summary>
        public static float StartMost(bool police)
        {
            return (police ? PoliceRouteMost : AmbulanceRouteMost) + WalkOvershoot;
        }

        /// <summary>
        /// Seconds a drive of this many metres of road takes at this top speed, when nothing holds it up: gathering speed (6 m/s2 to the traffic's pace, less above it),
        /// along at speed, the last 30 m slowed into its place (4 m/s2, so the same at any speed), and the bends and junctions of a city route. A short road never gets to speed.
        /// </summary>
        public static float DriveSeconds(float metres, float speed)
        {
            if (metres <= 0f || speed <= 0f)
            {
                return 0f;
            }
            float stop = Math.Min(metres, StopRun);
            float seconds = (float)Math.Sqrt(stop / 2f);   // v = sqrt(2 * 4 * x) over x metres
            float rest = metres - stop;
            if (rest > 0f)
            {
                float first = Math.Min(speed, TrafficSpeed);
                float upTo = first * first / (2f * Pickup);
                if (rest <= upTo)
                {
                    seconds += (float)Math.Sqrt(2f * rest / Pickup);
                    rest = 0f;
                }
                else
                {
                    seconds += first / Pickup;
                    rest -= upTo;
                    if (speed > TrafficSpeed)
                    {
                        float above = (speed * speed - TrafficSpeed * TrafficSpeed) / (2f * PickupAbove);
                        float reached = rest <= above ? (float)Math.Sqrt(TrafficSpeed * TrafficSpeed + 2f * PickupAbove * rest) : speed;
                        seconds += (reached - TrafficSpeed) / PickupAbove;
                        rest = rest <= above ? 0f : rest - above;
                    }
                }
                seconds += rest / speed;
            }
            return seconds + metres * BendTax;
        }

        /// <summary>Seconds from the call to the unit parked at the scene when nothing holds it up.</summary>
        public static float ArrivalSeconds(float delay, float metres, float speed)
        {
            return delay + DriveSeconds(metres, speed);
        }

        /// <summary>The mean wait before a unit is sent, seconds.</summary>
        public static float MeanDelay(bool police)
        {
            return police ? (PoliceDelayLeast + PoliceDelayMost) * 0.5f : (AmbulanceDelayLeast + AmbulanceDelayMost) * 0.5f;
        }

        /// <summary>A unit's typical arrival after an accident, with no call of the player's: the mean delay, the mean route and the drive.</summary>
        public static float TypicalArrival(bool police)
        {
            return TypicalArrival(police, MeanDelay(police));
        }

        /// <summary>The ambulance's typical arrival from a call, for what the player is told: the mean delay and the mean route.</summary>
        public static float TypicalArrival(bool police, float delay)
        {
            float mean = police ? (PoliceRouteLeast + PoliceRouteMost) * 0.5f : (AmbulanceRouteLeast + AmbulanceRouteMost) * 0.5f;
            return ArrivalSeconds(delay, mean, Speed);
        }

        // ---- What the player is told: a promise that is kept in all but a bad hold-up.

        public const int WhenTwenty = 0, WhenHalf = 1, WhenMinute = 2, WhenTwo = 3, WhenFew = 4;

        /// <summary>The words that fit a typical arrival with half as long again to spare: within twenty seconds, half a minute, a minute, two, or a few minutes.</summary>
        public static int When(float seconds)
        {
            float promise = seconds * 1.5f;
            return promise <= 20f ? WhenTwenty : promise <= 30f ? WhenHalf : promise <= 60f ? WhenMinute : promise <= 120f ? WhenTwo : WhenFew;
        }

        /// <summary>One of count lines, by a roll that depends on nothing but the seed, the moment and the line's own salt: the same on every machine, and another next time.</summary>
        public static int Pick(int count, int seed, int moment, int salt)
        {
            if (count <= 1)
            {
                return 0;
            }
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)moment * 0x85EBCA77u ^ (uint)salt * 0xC2B2AE3Du;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (int)(h % (uint)count);
            }
        }

        // ---- On foot.

        /// <summary>Whether somebody with free hands runs the way to where they are going: further than RunFrom.</summary>
        public static bool Runs(float metres)
        {
            return metres > RunFrom;
        }

        /// <summary>The pace the crew holds the stretcher and the bag at: never more than a walking clip can be played at.</summary>
        public static float HeldPace(float wanted)
        {
            return Math.Min(wanted, WalkPace * ClipMost);
        }

        public static float TreatFor(bool fatal, bool down, float roll)
        {
            return fatal ? Lerp(TreatFatalLeast, TreatFatalMost, roll)
                : down ? Lerp(TreatInjured, TreatInjuredMost, roll)
                : Lerp(TreatDriverLeast, TreatDriverMost, roll);
        }

        // ---- Clearing up.

        /// <summary>
        /// How long a staged accident is planned to last from when it happens, by severity (0 minor, 1 moderate, 2 severe) and with the police
        /// attending or not. The scene ends earlier when everyone has done, and runs on, never past the hard limit, only for a casualty still being seen to.
        /// </summary>
        public static float SceneTotal(int severity, bool police, float roll)
        {
            if (severity <= 0)
            {
                return police ? Lerp(55f, 80f, roll) : Lerp(30f, 55f, roll);
            }
            if (severity == 1)
            {
                return police ? Lerp(75f, 105f, roll) : Lerp(65f, 100f, roll);
            }
            return Lerp(100f, 140f, roll);
        }

        /// <summary>The same for somebody knocked down: the ambulance is wanted, and a longer scene is only the crew still at work.</summary>
        public static float SingleScene(float roll)
        {
            return Lerp(SingleLeast, SingleMost, roll);
        }

        /// <summary>How long a parked unit stays at least, seconds, by who it is and how serious the scene is (0 minor, 1 moderate, 2 severe).</summary>
        public static float Stay(bool police, int severity, float roll)
        {
            if (police)
            {
                return severity >= 2 ? Lerp(30f, 50f, roll) : severity == 1 ? Lerp(18f, 35f, roll) : Lerp(14f, 25f, roll);
            }
            return severity >= 2 ? Lerp(32f, 50f, roll) : Lerp(18f, 32f, roll);
        }

        public static float StayMost(bool police, int severity)
        {
            return Stay(police, severity, 1f);
        }

        // ---- The self-check.

        /// <summary>
        /// Null if the response was as quick as it is made to be; else what was wrong, in words for the log. Judged on the dispatch after the call
        /// (given the tries that found no unit to send or showed its start), the start by road, the arrival against its own drive and some
        /// hold-up, and the lights and the siren on while it drove.
        /// </summary>
        public static string Verdict(bool police, float dispatchAfter, int tries, float metres, float arrivalAfter, float speed, bool lights, bool siren, bool sirenWanted)
        {
            var why = new StringBuilder();
            float delayMost = (police ? PoliceDelayMost : AmbulanceDelayMost) + Math.Max(0, tries) * Math.Max(RetryAfter, RetryUnseenAfter) + 1f;
            if (dispatchAfter > delayMost)
            {
                why.Append("sent after ").Append(dispatchAfter.ToString("F0")).Append(" s, more than ").Append(delayMost.ToString("F0")).Append(" s; ");
            }
            if (metres > StartMost(police))
            {
                why.Append("it started ").Append(metres.ToString("F0")).Append(" m off, further than ").Append(StartMost(police).ToString("F0")).Append(" m; ");
            }
            float allowed = DriveSeconds(metres, speed) + HoldUpSlack;
            float drove = arrivalAfter - dispatchAfter;
            if (drove > allowed)
            {
                why.Append("the drive took ").Append(drove.ToString("F0")).Append(" s, more than the ").Append(allowed.ToString("F0")).Append(" s it is allowed (held up on the way); ");
            }
            if (speed < FireSpeed - 0.5f)
            {
                why.Append("its top speed was only ").Append((speed * 3.6f).ToString("F0")).Append(" km/h; ");
            }
            if (!lights)
            {
                why.Append("its lights were not on; ");
            }
            if (sirenWanted && !siren)
            {
                why.Append("its siren was not on; ");
            }
            return why.Length == 0 ? null : why.ToString().TrimEnd(' ', ';');
        }

        private static float Lerp(float least, float most, float roll)
        {
            if (roll < 0f)
            {
                roll = 0f;
            }
            else if (roll > 1f)
            {
                roll = 1f;
            }
            return least + (most - least) * roll;
        }
    }
}
