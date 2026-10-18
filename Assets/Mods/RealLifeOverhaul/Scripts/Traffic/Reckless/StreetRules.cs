using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules of the live street in plain numbers: horns and what people and officers make of them, how often a reckless
    /// driver turns up and how they drive, how hard a collision was, and when somebody steps out. Game-free, so NavCheck's
    /// StreetCheck runs every rule; People/HornReactions and Traffic/Reckless hold no number of their own.
    /// </summary>
    public static class StreetRules
    {
        // The horn: who hears it.
        public const float HornReach = 14f;          // metres from the middle of the car, ahead of it
        public const float HornTangent = 1.19f;      // the tangent of 50 degrees: how wide the cone ahead opens each side
        public const float HornBeside = 4f;          // metres to either side of the car that hear it whatever the cone says
        public const float HornBesideAhead = 6f;     // ...from this far ahead of its middle
        public const float HornBesideBehind = 3f;    // ...to this far behind it
        public const int HornMost = 6;               // people who react to one blast, the nearest first
        public const float HornRepeat = 2f;          // seconds a held horn takes to count as another blast
        public const float PathMargin = 0.9f;        // metres beyond the car's side that are still in its way

        // The horn: what people do.
        public const int StartleShare = 50;          // of 100 people on the pavement who look round
        public const float StartleSeconds = 1.1f;    // how long that look lasts
        public const float GlareSeconds = 1.8f;      // somebody in the right stops and faces the car this long...
        public const float GlareMore = 0.9f;         // ...and this much longer for each further blast, twice at most
        public const int JoinSecond = 30;            // of 100 people on the pavement who join in at the second blast at people in the right
        public const int JoinThird = 60;             // ...and from the third
        public const float HurrySeconds = 4f;        // somebody in the wrong jogs for this long, then walks on
        public const float HurryFactor = 2.2f;       // times their own walking pace
        public const float HurryLeast = 3.2f;        // m/s: a jog at the least
        public const float HurryMost = 4.5f;         // ...and no sprint
        public const float StepBackWithin = 0.8f;    // metres into the car's way: no further in than this, the kerb behind is nearer
        public const int WaveShare = 35;             // of 100 who hurry and lift a hand in apology
        public const int PointShare = 30;            // of 100 who glare and point at the red light

        // The horn: what the police make of it.
        public const float RageWindow = 60f;         // seconds in which blasts at people in the right are counted
        public const int RagesForWarning = 3;        // that many in the window, with an officer near: a warning
        public const float WarnedFor = 300f;         // seconds after a warning in which the next such blast is fined
        public const float OfficerWithin = 30f;      // metres: how near an officer (or a parked patrol car) must be
        public const float PenaltyPause = 30f;       // seconds after a warning or a fine before either can follow

        // How often.
        public const float CheckSeconds = 20f;       // the street is looked at this often (TrafficAccidents' own interval), in seconds of play
        public const float LooksPerGameHour = 3f;    // a game minute is a second of play at normal speed: three looks a game hour
        public const int DefaultFrequency = 2;       // the option's default
        public const float DayAtDefault = 0.75f;     // car accidents near the player in a game day of street time at the default, fair weather, the day's average hour
        public const float StepFactor = 1.5f;        // each point of the option is half as many again: 1 is 0.5 a day, 10 about 19
        public const float PeopleShare = 0.4f;       // pedestrians struck per car accident (PedestrianIncidents' own share)
        public const float DueMost = 1.1f;           // accidents that may be owing at once: one, and never a second close behind it
        public const float RecklessPerAccident = 3f; // reckless drivers seen for each accident that is due
        public const float RecklessMost = 0.85f;     // the chance of one at a look never passes this
        public const float NightFactor = 1.3f;       // more of them from 21:00 to 05:00
        public const float JaywalkersPerKnock = 3f;  // people who step out for each one who is due to be hit
        public const float JaywalkMost = 0.5f;       // the chance of one at a look never passes this

        // Which car, and for how long.
        public const float PickLeast = 20f;          // metres from the player: nearer than this a change of driving would show
        public const float PickMost = 120f;          // metres
        public const float PickSpeedKmh = 15f;       // a car slower than this is not on the move
        public const float LastsLeast = 20f;         // seconds a driver keeps it up
        public const float LastsMost = 60f;          // seconds

        // How such a driver drives.
        public const float DryGrip = 8.5f;           // m/s2 sideways a car's tyres hold on a dry road (EmergencyDriver's own figure)
        public const float LoseMargin = 1.2f;        // a bend taken this much faster than the road holds ends in a crash
        public const float GentleBend = 40f;         // metres: a bend wider than this is no bend
        public const float CarefulPush = 0.85f;      // of what the road holds, by somebody who gets away with it
        public const float CarefulStopping = 20f;    // m/s2 such a driver brakes with when it matters: late, hard and in time
        public const float CarefulForesight = 1f;    // of the look ahead an emergency driver has
        public const float LateStoppingLeast = 4.5f; // m/s2 a driver with an accident due brakes with, at the least...
        public const float LateStoppingMost = 12f;   // ...and at the most: still too little for what they see of the road
        public const float LateForesightMost = 0.7f; // the most of the road ahead such a driver takes in

        // How hard a collision was.
        public const float MinorBelow = 8f;          // m/s closing speed, about 29 km/h: a bump
        public const float SevereFrom = 14f;         // ...about 50 km/h: a serious crash (CrashConsequences' own line for the player)

        // Afterwards.
        public const float ChaseAfter = 230f;        // metres a driver who ran must be beyond the scene before a patrol car is put behind it
        public const int PullsOverShare = 65;        // of 100 drivers a patrol car lights up who stop for it
        public const float NearMissAhead = 9f;       // metres ahead of a reckless car inside which somebody in its way has had a near miss
        public const float NearMissSpeed = 6f;       // ...if it is going at least this fast, m/s

        /// <summary>Where somebody is when a horn sounds.</summary>
        public enum Place { Pavement, Road, Crossing }

        /// <summary>The light at the stop line ahead of the car that sounds it. None: no light within reach, or the line is behind it.</summary>
        public enum Light { None, Green, Red }

        /// <summary>What one person does about it.</summary>
        public enum Reaction { None, Startle, Hurry, StepBack, Glare }

        /// <summary>What an officer does about a driver who keeps it up.</summary>
        public enum Penalty { None, Warning, Fine }

        /// <summary>The kinds of reckless driver.</summary>
        public enum Kind { Speeder, RedLight, Tailgater, Drunk }

        /// <summary>True on the frame a blast begins: the horn goes down, or has been held for another two seconds.</summary>
        public static bool Blast(bool pressed, bool pressedBefore, float sinceLast)
        {
            return pressed && (!pressedBefore || sinceLast >= HornRepeat);
        }

        /// <summary>True if somebody that far ahead and to the side of the car's middle hears it: in the cone ahead, or close beside.</summary>
        public static bool Hears(float ahead, float side)
        {
            float aside = Math.Abs(side);
            if (aside <= HornBeside && ahead >= -HornBesideBehind && ahead <= HornBesideAhead)
            {
                return true;
            }
            return ahead > 0f && ahead * ahead + side * side <= HornReach * HornReach && aside <= ahead * HornTangent;
        }

        /// <summary>True if they are in the way of a car of that half width: ahead of its middle and within its sides and a step more.</summary>
        public static bool InPath(float ahead, float side, float halfWidth)
        {
            return ahead > 0f && ahead <= HornReach && Math.Abs(side) <= halfWidth + PathMargin;
        }

        /// <summary>True if somebody in the road is there by right: the car's light is red, or they are on a crossing whose light lets them walk.</summary>
        public static bool InTheRight(Place place, Light car, bool peopleMayWalk)
        {
            return place != Place.Pavement && (car == Light.Red || (place == Place.Crossing && peopleMayWalk));
        }

        /// <summary>
        /// What one person does: in the car's way and in the right, they stop and glare; in the wrong, they clear the way
        /// (onwards, or back if they had only just stepped in); anybody else may look round. Roll 0 to 99; sideSpeed is how
        /// fast they cross the car's line, towards positive side.
        /// </summary>
        public static Reaction React(Place place, bool inPath, Light car, bool peopleMayWalk, float side, float sideSpeed, float halfWidth, int roll)
        {
            if (place == Place.Pavement || !inPath)
            {
                return roll < StartleShare ? Reaction.Startle : Reaction.None;
            }
            if (InTheRight(place, car, peopleMayWalk))
            {
                return Reaction.Glare;
            }
            return StepsBack(side, sideSpeed, halfWidth) ? Reaction.StepBack : Reaction.Hurry;
        }

        /// <summary>True if the kerb they came from is the nearer way out: walking in towards the car's line, no more than a step into its way.</summary>
        public static bool StepsBack(float side, float sideSpeed, float halfWidth)
        {
            bool inwards = side * sideSpeed < 0f && Math.Abs(sideSpeed) > 0.2f;
            float entered = halfWidth + PathMargin - Math.Abs(side);
            return inwards && entered >= 0f && entered <= StepBackWithin;
        }

        /// <summary>True if somebody on the pavement joins the glaring: nobody at the first blast at people in the right, more at each one after.</summary>
        public static bool Joins(int blastsAtPeopleInTheRight, int roll)
        {
            int share = blastsAtPeopleInTheRight >= 3 ? JoinThird : blastsAtPeopleInTheRight == 2 ? JoinSecond : 0;
            return roll < share;
        }

        /// <summary>How long somebody in the right stands and glares at the first, second and third blast (and no longer after that).</summary>
        public static float GlareFor(int blastsAtPeopleInTheRight)
        {
            int further = blastsAtPeopleInTheRight - 1;
            further = further < 0 ? 0 : further > 2 ? 2 : further;
            return GlareSeconds + GlareMore * further;
        }

        /// <summary>The pace somebody hurries at, from their own walking pace: a jog, never a sprint.</summary>
        public static float HurryPace(float ownPace)
        {
            float pace = ownPace * HurryFactor;
            return pace < HurryLeast ? HurryLeast : pace > HurryMost ? HurryMost : pace;
        }

        public static bool Waves(int roll) => roll < WaveShare;

        /// <summary>True if somebody who glares also points at the light: only when it is the car's red light they are in the right by.</summary>
        public static bool Points(Light car, int roll) => car == Light.Red && roll < PointShare;

        /// <summary>What an officer near makes of a blast at people in the right: three in a minute earn a warning, the next one a fine.</summary>
        public static Penalty For(int blastsInWindow, bool officerNear, bool warned)
        {
            if (!officerNear || blastsInWindow <= 0)
            {
                return Penalty.None;
            }
            if (warned)
            {
                return Penalty.Fine;
            }
            return blastsInWindow >= RagesForWarning ? Penalty.Warning : Penalty.None;
        }

        // How busy the road is at each hour against the day's average (the mean of the 24 is 1): the shape of a large city's
        // collisions by hour. The higher risk of each car at night is in the drivers instead (NightFactor, the drunk).
        private static readonly float[] HourFactors =
        {
            0.63f, 0.44f, 0.37f, 0.32f, 0.37f, 0.49f,   // 00:00 to 05:00
            0.66f, 0.88f, 1.22f, 1.17f, 1.12f, 1.20f,   // 06:00 to 11:00
            1.29f, 1.34f, 1.51f, 1.49f, 1.63f, 1.63f,   // 12:00 to 17:00
            1.46f, 1.22f, 1.05f, 0.95f, 0.83f, 0.73f    // 18:00 to 23:00
        };

        /// <summary>How busy the road is at an hour of the day (0 to 23; any other number is taken round the clock), 1 being the day's average.</summary>
        public static float HourFactor(int hour)
        {
            return HourFactors[((hour % 24) + 24) % 24];
        }

        /// <summary>
        /// Car accidents near the player in a game day of street time at a setting of the frequency option, in fair weather at
        /// the day's average hour: each point half as many again, 0.5 at 1, 0.75 at the default 2, about 19 at 10, none at 0.
        /// Far more than a real street sees: an accident is an event the player meets now and then.
        /// </summary>
        public static float PerDay(int frequency)
        {
            return frequency <= 0 ? 0f : DayAtDefault * (float)Math.Pow(StepFactor, frequency - DefaultFrequency);
        }

        /// <summary>Accidents that fall due at one look at the street, at a setting, the weather's factor and the hour.</summary>
        public static float Due(int frequency, float weather, int hour)
        {
            float due = PerDay(frequency) / (24f * LooksPerGameHour) * weather * HourFactor(hour);
            return due < 0f ? 0f : due;
        }

        /// <summary>The same per game hour.</summary>
        public static float PerGameHour(int frequency, float weather, int hour)
        {
            return Due(frequency, weather, hour) * LooksPerGameHour;
        }

        /// <summary>The same per hour of play in the street (sixty game hours) at the day's average hour.</summary>
        public static float PerHour(int frequency, float weather)
        {
            float perHour = PerDay(frequency) / 24f * (3600f / CheckSeconds / LooksPerGameHour) * weather;
            return perHour < 0f ? 0f : perHour;
        }

        /// <summary>What is owing after one more look, never more than <see cref="DueMost"/>.</summary>
        public static float Accrue(float owing, int frequency, float weather, int hour, float share = 1f)
        {
            float next = owing + Due(frequency, weather, hour) * share;
            return next > DueMost ? DueMost : next;
        }

        /// <summary>The chance at one look that a driver near the player starts to drive recklessly.</summary>
        public static float RecklessChance(int frequency, float weather, int hour, bool night)
        {
            float chance = Due(frequency, weather, hour) * RecklessPerAccident * (night ? NightFactor : 1f);
            return chance > RecklessMost ? RecklessMost : chance;
        }

        /// <summary>The chance at one look that somebody steps into the road in front of a car.</summary>
        public static float JaywalkChance(int frequency, float weather, int hour)
        {
            float chance = Due(frequency, weather, hour) * PeopleShare * JaywalkersPerKnock;
            return chance > JaywalkMost ? JaywalkMost : chance;
        }

        private static readonly string[] NeverOffenders = { "ambulance", "taxi", "limo", "police", "bus", "truck", "van", "fire", "garbage", "delivery", "tow" };

        /// <summary>True if a vehicle of that name is an ordinary private car: the one list Crime/CrimeCars and the reckless drivers pick by.</summary>
        public static bool IsOrdinaryCarName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return false;
            }
            for (int i = 0; i < NeverOffenders.Length; i++)
            {
                if (prefabName.IndexOf(NeverOffenders[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True if a car that far from the player may be the one: far enough for the change not to show, near enough to be seen.</summary>
        public static bool InPickRange(float metres)
        {
            return metres >= PickLeast && metres <= PickMost;
        }

        /// <summary>Which kind, from a roll of 0 to 99. Nobody drives drunk by day.</summary>
        public static Kind Pick(int roll, bool night)
        {
            if (night)
            {
                return roll < 35 ? Kind.Speeder : roll < 55 ? Kind.RedLight : roll < 70 ? Kind.Tailgater : Kind.Drunk;
            }
            return roll < 45 ? Kind.Speeder : roll < 75 ? Kind.RedLight : Kind.Tailgater;
        }

        /// <summary>Seconds the driver keeps it up, from a roll of 0 to 1.</summary>
        public static float Lasts(float roll)
        {
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            return LastsLeast + (LastsMost - LastsLeast) * roll;
        }

        /// <summary>The pace on a clear road in m/s, from a roll of 0 to 1: a speeder 65 to 85 km/h, the others at or a little over the traffic's.</summary>
        public static float Cruise(Kind kind, float roll)
        {
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            switch (kind)
            {
                case Kind.Speeder: return 18f + 5.5f * roll;
                case Kind.RedLight: return 13f + 3f * roll;
                case Kind.Tailgater: return 13f + 2.5f * roll;
                default: return 9f + 4f * roll;
            }
        }

        /// <summary>
        /// The most the driver can slow by, in m/s2: late but hard and in time for one who gets away with it; for one with an
        /// accident due, by a roll of 0 to 1, as one who was not looking, so what they hit they hit at up to full speed.
        /// </summary>
        public static float Stopping(bool accidentDue, float roll)
        {
            if (!accidentDue)
            {
                return CarefulStopping;
            }
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            return LateStoppingLeast + (LateStoppingMost - LateStoppingLeast) * roll;
        }

        /// <summary>How much of the road ahead the driver takes in, 0 to 1 of an emergency driver's look: the tailgater least of all.</summary>
        public static float Foresight(Kind kind, bool accidentDue, float roll)
        {
            if (!accidentDue)
            {
                return CarefulForesight;
            }
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            // (The drunk goes slowly, and so must see very little for it to matter: 0.3 to 0.42 of the look.)
            float least = kind == Kind.Tailgater || kind == Kind.Drunk ? 0.3f : 0.5f;
            float most = kind == Kind.Drunk ? 0.42f : LateForesightMost;
            return least + (most - least) * roll;
        }

        /// <summary>The fastest such a driver can go and still stop short of something seen at the edge of their look (in metres).</summary>
        public static float StopsFrom(float stopping, float foresight, float look, float gap)
        {
            float room = look * foresight - gap;
            return room <= 0f ? 0f : (float)Math.Sqrt(2f * stopping * room);
        }

        /// <summary>The kind when somebody on foot is due to be hit, from a roll of 0 to 99: mostly the red-light runner, as people cross on red.</summary>
        public static Kind PickForPeople(Kind rolled, int roll)
        {
            return roll < 60 ? Kind.RedLight : rolled;
        }

        /// <summary>True if that kind stops for a red light. Only the one who runs them does not.</summary>
        public static bool ObeysLights(Kind kind)
        {
            return kind != Kind.RedLight;
        }

        /// <summary>
        /// The grip the driver plans a bend with, in m/s2: a margin on today's road for one who gets away with it; a speeder or
        /// a drunk with an accident due plans for a dry road and more, by a roll of 0 to 1.
        /// </summary>
        public static float PlannedGrip(Kind kind, bool accidentDue, float roll, float grip)
        {
            if (!accidentDue || kind == Kind.RedLight || kind == Kind.Tailgater)
            {
                return DryGrip * grip * CarefulPush * CarefulPush;
            }
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            float push = 1f + 0.35f * roll;
            return DryGrip * push * push;
        }

        /// <summary>The fastest a bend of that radius can be taken on today's road, in m/s.</summary>
        public static float BendLimit(float radius, float grip)
        {
            return (float)Math.Sqrt(DryGrip * (grip < 0.1f ? 0.1f : grip) * (radius < 0f ? 0f : radius));
        }

        /// <summary>True if a car going that fast through a bend of that radius has lost it: well over what the road holds, on a real bend.</summary>
        public static bool LosesIt(float speed, float radius, float grip)
        {
            return radius > 0f && radius <= GentleBend && speed > BendLimit(radius, grip) * LoseMargin;
        }

        /// <summary>How far the drunk's car wanders from the middle of its lane, in metres, from a roll of 0 to 1: over the lane's edge when an accident is due.</summary>
        public static float Sway(bool accidentDue, float roll)
        {
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            return accidentDue ? 1.3f + 0.5f * roll : 0.6f + 0.4f * roll;
        }

        /// <summary>How hard two vehicles met, from their closing speed in m/s: 0 minor, 1 moderate, 2 severe.</summary>
        public static int Severity(float closingSpeed)
        {
            return closingSpeed < MinorBelow ? 0 : closingSpeed < SevereFrom ? 1 : 2;
        }

        /// <summary>True if the driver who has hurt somebody drives off, from a roll of 0 to 99.</summary>
        public static bool Flees(Kind kind, bool fatal, int roll)
        {
            int share = kind == Kind.Drunk ? 60 : kind == Kind.Speeder ? 45 : kind == Kind.RedLight ? 35 : 20;
            return roll < share + (fatal ? 10 : 0);
        }

        /// <summary>True if a patrol car that sees this driver goes after it: speed and red lights are what an officer can see from a car.</summary>
        public static bool PatrolNotices(Kind kind)
        {
            return kind == Kind.Speeder || kind == Kind.RedLight;
        }

        public static bool PullsOver(int roll) => roll < PullsOverShare;

        /// <summary>True if somebody that far ahead of a reckless car, and that far to its side, has just had a near miss.</summary>
        public static bool NearMiss(float ahead, float side, float halfWidth, float speed)
        {
            return speed >= NearMissSpeed && ahead > 0f && ahead <= NearMissAhead && Math.Abs(side) <= halfWidth + PathMargin;
        }

        // Stepping out.
        public const float WalkPace = 1.4f;          // m/s somebody crosses at
        public const float TrafficBraking = 5f;      // m/s2 an ordinary driver stops with
        public const float JaywalkRoom = 8f;         // metres to spare when the driver is to stop in time
        public const float JaywalkLate = 2f;         // ...and when not

        /// <summary>How far up the road the car should be as somebody sets off for its lane: what it covers meanwhile, then room to stop (none if late).</summary>
        public static float JaywalkCarDistance(float carSpeed, float metresToLane, bool late)
        {
            float walk = (metresToLane < 0f ? 0f : metresToLane) / WalkPace;
            float room = late ? JaywalkLate : carSpeed * carSpeed / (2f * TrafficBraking) + JaywalkRoom;
            return carSpeed * walk + room;
        }
    }
}
