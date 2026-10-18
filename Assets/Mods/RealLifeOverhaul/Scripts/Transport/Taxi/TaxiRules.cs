using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of a taxi called from the phone, in plain numbers: fees, start distances, waits, refusals and when a taxi
    /// getting nowhere is planned again or given up. Nothing of the game in it, so NavCheck's TaxiCheck runs every rule;
    /// the Guide states these numbers from here.
    /// </summary>
    public static class TaxiRules
    {
        public static float CallOut => Prices.At(12f);            // the call-out by day
        public static float NightCallOut => Prices.At(18f);       // ...and by night
        public const int NightFrom = 22;             // the night rate runs from 22:00
        public const int NightUntil = 6;             // ...to 05:59
        public const float WetExtra = 0.5f;          // in rain or a storm the call-out is this much more
        public const float CancelShare = 0.5f;       // of the call-out, for a cancel once the taxi is close
        public const float NoShowShare = 0.5f;       // of the call-out, when nobody got in

        public const float RoadWithin = 60f;         // metres from the caller within which there must be a lane a car can use
        public const float RoadLevel = 4f;           // ...and no more than this above or below them (a bridge overhead is not their street)
        public const float StartLeast = 250f;        // metres from the caller a taxi starts from, at least
        public const float StartMost = 450f;         // ...and at most
        public const float WetStartLeast = 400f;     // in rain or a storm fewer cabs are about: the nearest is further off
        public const float WetStartMost = 650f;      // metres
        public const float TestStartLeast = 70f;     // the test that brings one at once: far enough not to appear beside the player
        public const float TestStartMost = 160f;     // metres
        public const float RouteMost = 2.2f;         // a way round that is this many times the furthest start is no way: no taxi comes
        public const float CancelFreeBeyond = 100f;  // metres of road still to drive above which a cancel costs nothing
        public const float FarFromStop = 80f;        // metres from where it stands beyond which the caller is told where it waits
        public const float WaitSeconds = 60f;        // real seconds a standing taxi waits for the caller
        public const float GraceSeconds = 20f;       // ...and once more this long, if the caller is close to it when that time is up
        public const float GraceWithin = 15f;        // metres from the taxi that count as close
        public const float Pace = 8f;                // m/s a taxi makes over a street with its junctions and lights (it cruises at 11)

        public const float ReplanAfter = 40f;        // seconds without a metre gained, out of view, before it is planned again
        public const float ReplanBeyond = 110f;      // ...and only with more than this much road still to drive
        public const float GiveUpAfter = 150f;       // seconds without a metre gained before it is given up
        public const float GiveUpOut = 300f;         // ...or this long on the road without arriving
        public const float ReplanNearer = 45f;       // a second plan starts at least this much nearer than the first had got
        public const float ReplanLeast = 120f;       // ...but never nearer than this: nothing appears in view

        public enum Refusal { None, Off, Riding, Held, OnItsWay, InVehicle, Indoors, NoRoad, NoTaxi }   // why a call is refused

        public enum Watch { CarryOn, Replan, GiveUp }   // what is done about a taxi on its way

        public static bool Night(int hour)
        {
            return hour >= NightFrom || hour < NightUntil;
        }

        /// <summary>The call-out in whole money: by the hour, and half as much again in the wet.</summary>
        public static float Fee(int hour, bool wet)
        {
            float fee = Night(hour) ? NightCallOut : CallOut;
            return (float)Math.Round(wet ? fee * (1f + WetExtra) : fee);
        }

        /// <summary>What a cancel costs with so much road still to drive.</summary>
        public static float CancelFee(float fee, float metresAway)
        {
            return metresAway > CancelFreeBeyond ? 0f : (float)Math.Round(fee * CancelShare);
        }

        /// <summary>What it costs when nobody got in.</summary>
        public static float NoShowFee(float fee)
        {
            return (float)Math.Round(fee * NoShowShare);
        }

        /// <summary>What can be taken of a charge without the money going below nothing, in whole money.</summary>
        public static float Payable(float fee, float money)
        {
            if (fee <= 0f || money <= 0f)
            {
                return 0f;
            }
            return Math.Min(fee, (float)Math.Floor(money));
        }

        /// <summary>The band of distances from the caller a taxi may start in.</summary>
        public static void StartBand(bool wet, bool test, out float least, out float most)
        {
            least = test ? TestStartLeast : wet ? WetStartLeast : StartLeast;
            most = test ? TestStartMost : wet ? WetStartMost : StartMost;
        }

        /// <summary>The band for a second plan, for a taxi that had this much road left.</summary>
        public static void ReplanBand(float remaining, out float least, out float most)
        {
            most = Math.Max(remaining - ReplanNearer, ReplanLeast + 30f);
            least = Math.Min(ReplanLeast, most - 30f);
        }

        public static bool InBand(float metres, float least, float most)
        {
            return metres >= least && metres <= most;
        }

        /// <summary>True if a route this long is one a taxi takes for a band ending at <paramref name="most"/>.</summary>
        public static bool RouteAccepted(float routeMetres, float most)
        {
            return routeMetres > 0f && routeMetres <= most * RouteMost;
        }

        /// <summary>Real seconds a taxi is expected to take over so much road.</summary>
        public static float Seconds(float metres)
        {
            return metres <= 0f ? 0f : metres / Pace;
        }

        /// <summary>The wait as the dispatcher says it: game minutes, rounded up, at least one, at the game's clock rate.</summary>
        public static int Minutes(float metres, float gameMinutesPerSecond)
        {
            float rate = gameMinutesPerSecond > 0f ? gameMinutesPerSecond : 1f;
            int minutes = (int)Math.Ceiling(Seconds(metres) * rate);
            return minutes < 1 ? 1 : minutes;
        }

        /// <summary>Whether a call is taken, from what is known before any road is searched: the first reason that holds.</summary>
        public static Refusal Refuse(bool on, bool riding, bool held, bool onItsWay, bool inVehicle, bool indoors)
        {
            return !on ? Refusal.Off
                : onItsWay ? Refusal.OnItsWay
                : riding ? Refusal.Riding
                : held ? Refusal.Held
                : inVehicle ? Refusal.InVehicle
                : indoors ? Refusal.Indoors
                : Refusal.None;
        }

        /// <summary>A taxi getting nowhere is planned again once, out of view, and given up after minutes or too long out.</summary>
        public static Watch Judge(float noProgressFor, float outFor, float remaining, bool inView, bool replanned)
        {
            if (noProgressFor > GiveUpAfter || outFor > GiveUpOut)
            {
                return Watch.GiveUp;
            }
            if (!replanned && !inView && noProgressFor > ReplanAfter && remaining > ReplanBeyond)
            {
                return Watch.Replan;
            }
            return Watch.CarryOn;
        }

        /// <summary>True if a taxi whose wait is up stays a little longer: once, for a caller close to it.</summary>
        public static bool Grace(float metresFromTaxi, bool given)
        {
            return !given && metresFromTaxi <= GraceWithin;
        }

        /// <summary>True if the caller is far enough from the taxi to be told where it waits.</summary>
        public static bool FarAtArrival(float metresFromStop)
        {
            return metresFromStop > FarFromStop;
        }
    }
}
