using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of a taxi called from the phone, in plain numbers: what the call-out costs by the hour and the
    /// weather, what a cancelled call and a taxi nobody came to cost, how far off a taxi starts, how long it is
    /// said to take, when a call is refused and when a taxi that is getting nowhere is planned again or given up.
    /// Nothing of the game or the engine in it, so every rule is also run in a console test
    /// (RealLifeOverhaul.Build/NavCheck/TaxiCheck.cs). The Guide states these numbers from here
    /// (Meta/FeatureList.cs), and nothing else in the feature holds a number of its own for them.
    /// </summary>
    public static class TaxiRules
    {
        // ---------------------------------------------------------------- money
        public static float CallOut => Prices.At(12f);            // the call-out by day
        public static float NightCallOut => Prices.At(18f);       // ...and by night
        public const int NightFrom = 22;             // the night rate runs from 22:00
        public const int NightUntil = 6;             // ...to 05:59
        public const float WetExtra = 0.5f;          // in rain or a storm the call-out is this much more
        public const float CancelShare = 0.5f;       // of the call-out, for a cancel once the taxi is close
        public const float NoShowShare = 0.5f;       // of the call-out, when nobody got in

        // ---------------------------------------------------------------- distances and times
        public const float RoadWithin = 60f;         // metres from the caller within which there must be a lane a car can use
        public const float RoadLevel = 4f;           // ...and no more than this above or below them (a bridge overhead is not their street)
        public const float StartLeast = 250f;        // metres from the caller a taxi starts from, at least
        public const float StartMost = 450f;         // ...and at most
        public const float WetStartLeast = 400f;     // in rain or a storm fewer cabs are about: the nearest is further off
        public const float WetStartMost = 650f;
        public const float TestStartLeast = 70f;     // the test that brings one at once: far enough not to appear beside the player
        public const float TestStartMost = 160f;
        public const float RouteMost = 2.2f;         // a way round that is this many times the furthest start is no way: no taxi comes
        public const float CancelFreeBeyond = 100f;  // metres of road still to drive above which a cancel costs nothing
        public const float FarFromStop = 80f;        // metres from where it stands beyond which the caller is told where it waits
        public const float WaitSeconds = 60f;        // real seconds a standing taxi waits for the caller
        public const float GraceSeconds = 20f;       // ...and once more this long, if the caller is close to it when that time is up
        public const float GraceWithin = 15f;        // metres from the taxi that count as close
        public const float Pace = 8f;                // m/s a taxi makes over a street with its junctions and lights (it cruises at 11)

        // ---------------------------------------------------------------- a taxi that is getting nowhere (the bus's rules)
        public const float ReplanAfter = 40f;        // seconds without a metre gained, out of view, before it is planned again
        public const float ReplanBeyond = 110f;      // ...and only with more than this much road still to drive
        public const float GiveUpAfter = 150f;       // seconds without a metre gained before it is given up
        public const float GiveUpOut = 300f;         // ...or this long on the road without arriving
        public const float ReplanNearer = 45f;       // a second plan starts at least this much nearer than the first had got
        public const float ReplanLeast = 120f;       // ...but never nearer than this: nothing appears in view

        /// <summary>Why a call is refused.</summary>
        public enum Refusal { None, Off, Riding, Held, OnItsWay, InVehicle, Indoors, NoRoad, NoTaxi }

        /// <summary>What is done about a taxi on its way.</summary>
        public enum Watch { CarryOn, Replan, GiveUp }

        /// <summary>True in the hours the night rate applies.</summary>
        public static bool Night(int hour)
        {
            return hour >= NightFrom || hour < NightUntil;
        }

        /// <summary>The call-out, in whole money: by the hour, and half as much again in the wet.</summary>
        public static float Fee(int hour, bool wet)
        {
            float fee = Night(hour) ? NightCallOut : CallOut;
            return (float)Math.Round(wet ? fee * (1f + WetExtra) : fee);
        }

        /// <summary>What a cancel costs with so much road still between the taxi and where it will stand.</summary>
        public static float CancelFee(float fee, float metresAway)
        {
            return metresAway > CancelFreeBeyond ? 0f : (float)Math.Round(fee * CancelShare);
        }

        /// <summary>What it costs when the taxi stood its time and nobody got in.</summary>
        public static float NoShowFee(float fee)
        {
            return (float)Math.Round(fee * NoShowShare);
        }

        /// <summary>What can be taken of a charge without the caller's money going below nothing: whole money, never more than is there.</summary>
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

        /// <summary>The band for a second plan, for a taxi that had this much road left when it got nowhere.</summary>
        public static void ReplanBand(float remaining, out float least, out float most)
        {
            most = Math.Max(remaining - ReplanNearer, ReplanLeast + 30f);
            least = Math.Min(ReplanLeast, most - 30f);
        }

        /// <summary>True if a place this far from the caller is one a taxi may start from.</summary>
        public static bool InBand(float metres, float least, float most)
        {
            return metres >= least && metres <= most;
        }

        /// <summary>True if a route of this length is one a taxi takes for a start band that ends at <paramref name="most"/>.</summary>
        public static bool RouteAccepted(float routeMetres, float most)
        {
            return routeMetres > 0f && routeMetres <= most * RouteMost;
        }

        /// <summary>Real seconds a taxi is expected to take over so much road.</summary>
        public static float Seconds(float metres)
        {
            return metres <= 0f ? 0f : metres / Pace;
        }

        /// <summary>
        /// The wait as the dispatcher says it: minutes of the game's clock, rounded up, never less than one. The
        /// game's clock runs a minute a real second at its own setting (GameManager.RunMainGameTick), so a taxi that
        /// takes 40 s is 40 minutes away by the clock the player sees, as a bus is on its stop's panel.
        /// <paramref name="gameMinutesPerSecond"/> is that setting (1 at its default; what cannot be read counts as 1).
        /// </summary>
        public static int Minutes(float metres, float gameMinutesPerSecond)
        {
            float rate = gameMinutesPerSecond > 0f ? gameMinutesPerSecond : 1f;
            int minutes = (int)Math.Ceiling(Seconds(metres) * rate);
            return minutes < 1 ? 1 : minutes;
        }

        /// <summary>
        /// Whether a call is taken, from what is known before any road is looked at: the first of these that holds
        /// is the one said. The road and the taxi are asked after them, and only if they all pass (that costs a search).
        /// </summary>
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

        /// <summary>
        /// What is done about a taxi on its way: nothing while it gains ground; planned again once, out of view,
        /// when it has stood long with far to go; given up when it has stood for minutes or been out too long.
        /// </summary>
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

        /// <summary>True if a taxi whose wait is up stays a little longer: once, for a caller who is close to it.</summary>
        public static bool Grace(float metresFromTaxi, bool given)
        {
            return !given && metresFromTaxi <= GraceWithin;
        }

        /// <summary>True if the caller is far enough from where the taxi stands to be told where it waits.</summary>
        public static bool FarAtArrival(float metresFromStop)
        {
            return metresFromStop > FarFromStop;
        }
    }
}
