using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The numbers of the Car Showroom's floor: how many of an hour's visitors are seen walking round it, how long they
    /// stay, and how close the showroom's camera comes to a car. None of them changes what the showroom's hour comes to
    /// (DealerRules.Hour decides that); they only say how it is shown.
    /// </summary>
    public static class FloorCatalogue
    {
        public const int ShownMost = 6;              // visitors of an hour seen on the floor at most, however many came
        public const int AtOnce = 3;                 // visitors on the floor at once: with the salesperson, the people pool's four who are only there to be seen
        public const int CarsLeast = 1;              // cars a visitor looks over
        public const int CarsMost = 3;
        public const int LookLeast = 12;             // seconds a visitor stands at a car
        public const int LookMost = 22;
        public const int TalkSeconds = 14;           // seconds a salesperson spends with a visitor
        public const int StayMinutes = 40;           // game minutes a visit is reckoned at (a game minute is a second of play), for one already under way when the player walks in
        public const int Late = 30;                  // game minutes a visitor whose time has come may wait for room on the floor before they are left out

        public const float PitchLeast = 8f;          // degrees over the level the camera looks down at a car from, at least
        public const float PitchMost = 55f;          // ...and at most
        public const float PitchStart = 18f;
        public const float YawStart = 35f;           // degrees round from the car's nose the camera starts at: its front three quarters
        public const float NearLeast = 1.6f;         // metres from the car's edge the camera comes at nearest
        public const float FarMost = 7f;             // metres beyond the car's edge it goes at furthest
        public const float FarStart = 3.6f;
        public const float CeilingMost = 4.2f;       // metres over the floor the camera rises at most round a car
        public const float Wall = 0.4f;              // metres inside the floor's edge the camera keeps to round a car
        public const float FloorPitchLeast = 40f;    // degrees: the floor seen from above while a car's bay is chosen
        public const float FloorPitchMost = 80f;
        public const float FloorPitchStart = 62f;
        public const float FloorNear = 0.55f;        // of the floor's longer side: the nearest the floor's camera comes
        public const float FloorFar = 1.6f;          // ...and the furthest
        public const float FloorStart = 1.05f;
        public const float TurnStep = 90f;           // degrees of one press of the game's rotate keys, as its own cameras turn
        public const float TurnSpeed = 200f;         // degrees a second of that turn, as the game's
        public const float DragDegrees = 0.15f;      // degrees per pixel of a drag with the right button, as the game's preview camera
        public const float ZoomShare = 0.12f;        // of the distance, per notch of the wheel
        public const float Ease = 9f;                // per second: how quickly the camera follows what the controls ask
    }

    /// <summary>One visitor of an hour as the floor shows them: when they come in, the cars they look over, and how it ends.</summary>
    public struct FloorVisit
    {
        public int Minute;                   // the minute of the hour they come in at (0 to 59)
        public int Cars;                     // how many cars they look over (1 to 3)
        public int First, Second, Third;     // ...which, by their place in the lot's list of cars
        public bool Served;                  // a salesperson comes to them
        public bool Offers;                  // they make an offer, on the first car they look at

        public int Car(int i)
        {
            return i == 0 ? First : i == 1 ? Second : Third;
        }
    }

    /// <summary>
    /// The rules of the Car Showroom's floor, game-free: who of an hour's visitors is seen and what each does
    /// (<see cref="Plan"/>), which bay each car stands on (<see cref="Arrange"/>, <see cref="Nearest"/>), and where the
    /// showroom's camera may be (<see cref="Orbit"/> and the limits beside it). EconomyCheck checks them.
    ///
    /// What is shown follows the hour as the showroom's rules worked it out and changes none of it: as many visitors as
    /// came, up to <see cref="FloorCatalogue.ShownMost"/>; as many of them looked after by a salesperson as the rules
    /// served, in proportion; and one visitor for each offer made that hour, at the car the offer is for.
    /// </summary>
    public static class ShowroomFloorRules
    {
        public const int Absent = -2;        // in Arrange: a car that is not on the lot (no vehicle of the game's any more)

        // ---------------------------------------------------------------- an hour's visitors

        /// <summary>How many of an hour's visitors are seen on the floor.</summary>
        public static int Shown(int visitors)
        {
            return Math.Max(0, Math.Min(visitors, FloorCatalogue.ShownMost));
        }

        /// <summary>How many of those seen are looked after: the hour's share of served visitors, and never fewer than the offers shown.</summary>
        public static int ShownServed(int visitors, int served, int offers)
        {
            int shown = Shown(visitors);
            if (shown == 0 || served <= 0)
            {
                return 0;
            }
            served = Math.Min(served, visitors);
            int share = (int)(((long)served * shown + visitors / 2) / visitors);
            return Math.Min(shown, Math.Max(Math.Max(1, share), Math.Min(offers, shown)));
        }

        /// <summary>
        /// The visitors an hour shows, in the order they come in: the minute each comes, the cars each looks over, and
        /// who is looked after and who makes an offer. The same hour of the same showroom always gives the same plan.
        /// <paramref name="offerCars"/> holds, for each offer made that hour, the place of its car in the lot's list.
        /// <paramref name="away"/>, by the same places, marks the cars that are not on show (in the workshop or out on a test
        /// drive): no visitor looks one over, as no customer can take one (DealerRules.Customers). Returns how many there are.
        /// </summary>
        public static int Plan(int seed, int hour, string place, int visitors, int served, IList<int> offerCars, int carCount, List<FloorVisit> into, IList<bool> away = null)
        {
            into.Clear();
            OnShow.Clear();
            for (int c = 0; c < carCount; c++)
            {
                if (Shows(c, carCount, away))
                {
                    OnShow.Add(c);
                }
            }
            int shown = Shown(visitors);
            if (shown == 0 || OnShow.Count == 0)
            {
                return 0;
            }
            int offers = 0;
            for (int i = 0; offerCars != null && i < offerCars.Count; i++)
            {
                offers += Shows(offerCars[i], carCount, away) ? 1 : 0;
            }
            int servedShown = ShownServed(visitors, served, offers);
            int offersShown = Math.Min(offers, servedShown);
            var dice = new DealerDice(seed, hour, DealerRules.Hash(place), 71);
            int slot = 60 / shown;
            int start = dice.Range(0, shown - 1);
            for (int i = 0; i < shown; i++)
            {
                into.Add(new FloorVisit { Minute = Math.Min(59, i * slot + dice.Range(0, Math.Max(0, slot - 1))) });
            }
            int offerAt = 0;
            for (int k = 0; k < shown; k++)
            {
                int at = (start + k) % shown;
                FloorVisit v = into[at];
                v.Served = k < servedShown;
                v.Offers = k < offersShown;
                if (v.Offers)
                {
                    while (!Shows(offerCars[offerAt], carCount, away))
                    {
                        offerAt++;
                    }
                    v.First = offerCars[offerAt++];
                }
                else
                {
                    v.First = OnShow[dice.Range(0, OnShow.Count - 1)];
                }
                v.Cars = dice.Range(FloorCatalogue.CarsLeast, Math.Min(FloorCatalogue.CarsMost, OnShow.Count));
                v.Second = v.Cars > 1 ? Other(ref dice, v.First, -1) : -1;
                v.Third = v.Cars > 2 ? Other(ref dice, v.First, v.Second) : -1;
                into[at] = v;
            }
            return shown;
        }

        // The cars on show for the plan being made, by their places in the lot's list (kept, so that a plan allocates nothing).
        private static readonly List<int> OnShow = new List<int>(16);

        // True for a car of the lot that is on show: neither in the workshop nor out on a test drive.
        private static bool Shows(int car, int carCount, IList<bool> away)
        {
            return car >= 0 && car < carCount && (away == null || car >= away.Count || !away[car]);
        }

        // A car on show that is neither of two others. With every car on show it draws as the plan always has.
        private static int Other(ref DealerDice dice, int not1, int not2)
        {
            int count = OnShow.Count;
            int at = dice.Range(0, count - 1);
            for (int i = 0; i < count && (OnShow[at] == not1 || OnShow[at] == not2); i++)
            {
                at = (at + 1) % count;
            }
            return OnShow[at];
        }

        /// <summary>Where a visit stands at a minute of its hour: 0 still to come, 1 under way (they are on the floor), 2 over.</summary>
        public static int Phase(int visitMinute, int nowMinute)
        {
            if (nowMinute < visitMinute)
            {
                return 0;
            }
            return nowMinute < visitMinute + FloorCatalogue.StayMinutes ? 1 : 2;
        }

        /// <summary>Seconds a visitor stands at a car: the same for the same visitor and car.</summary>
        public static int LookSeconds(int hour, int visit, int car)
        {
            var dice = new DealerDice(hour, visit, car, 29);
            return dice.Range(FloorCatalogue.LookLeast, FloorCatalogue.LookMost);
        }

        // ---------------------------------------------------------------- the bays

        /// <summary>
        /// Which bay each car stands on. A car the player put on a bay (<paramref name="chosen"/>, -1 for none) stands
        /// there, the first of two that chose the same; every other car keeps the bay it stood on (<paramref name="current"/>)
        /// while that is free, and a car with none takes the first bay free. A car marked <see cref="Absent"/> takes none,
        /// nor does a car for which no bay is left (-1).
        /// </summary>
        public static void Arrange(int bays, int[] current, int[] chosen, int count, int[] into)
        {
            for (int c = 0; c < count; c++)
            {
                into[c] = -1;
            }
            for (int c = 0; c < count; c++)
            {
                int b = chosen[c];
                if (current[c] != Absent && b >= 0 && b < bays && !Taken(into, count, b))
                {
                    into[c] = b;
                }
            }
            for (int c = 0; c < count; c++)
            {
                int b = current[c];
                if (into[c] < 0 && b >= 0 && b < bays && !Taken(into, count, b))
                {
                    into[c] = b;
                }
            }
            for (int c = 0; c < count; c++)
            {
                if (into[c] >= 0 || current[c] == Absent)
                {
                    continue;
                }
                for (int b = 0; b < bays; b++)
                {
                    if (!Taken(into, count, b))
                    {
                        into[c] = b;
                        break;
                    }
                }
            }
        }

        private static bool Taken(int[] into, int count, int bay)
        {
            for (int c = 0; c < count; c++)
            {
                if (into[c] == bay)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The bay nearest a point of the floor (centimetres, as the bays are kept); -1 if there are none.</summary>
        public static int Nearest(IList<LotSpot> bays, int x, int z)
        {
            int best = -1;
            long bestSqr = long.MaxValue;
            for (int b = 0; bays != null && b < bays.Count; b++)
            {
                long dx = bays[b].X - x, dz = bays[b].Z - z;
                long sqr = dx * dx + dz * dz;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = b;
                }
            }
            return best;
        }

        /// <summary>The way a car faces on its bay, in degrees: the bay's own, or the other way round.</summary>
        public static int Yaw(int bayYaw, bool turned)
        {
            int yaw = turned ? bayYaw + 180 : bayYaw;
            return ((yaw % 360) + 360) % 360;
        }

        // ---------------------------------------------------------------- the camera

        /// <summary>The nearest and furthest the camera comes to a car of this length (metres, from its middle).</summary>
        public static void Reach(float carLength, out float least, out float most)
        {
            float half = Math.Max(1f, carLength * 0.5f);
            least = half + FloorCatalogue.NearLeast;
            most = half + FloorCatalogue.FarMost;
        }

        /// <summary>The same for the floor seen from above, by its longer side (metres).</summary>
        public static void FloorReach(float longer, out float least, out float most)
        {
            longer = Math.Max(6f, longer);
            least = longer * FloorCatalogue.FloorNear;
            most = longer * FloorCatalogue.FloorFar;
        }

        public static float Clamp(float value, float least, float most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>The distance after a turn of the wheel: a share of the distance per notch, within the reach.</summary>
        public static float Zoom(float distance, float notches, float least, float most)
        {
            return Clamp(distance * (1f - notches * FloorCatalogue.ZoomShare), least, most);
        }

        /// <summary>The next stop of the game's rotate keys: the multiple of the step past the angle in the direction pressed.</summary>
        public static float Step(float yaw, int direction)
        {
            double steps = Math.Round(yaw / FloorCatalogue.TurnStep) + (direction >= 0 ? 1 : -1);
            return (float)(steps * FloorCatalogue.TurnStep);
        }

        /// <summary>
        /// Where the camera stands, from what it looks at: round it by <paramref name="yaw"/> degrees (0 is the world's
        /// north, +z), over it by <paramref name="pitch"/> degrees, <paramref name="distance"/> metres away. The offset in metres.
        /// </summary>
        public static void Orbit(float yaw, float pitch, float distance, out float x, out float y, out float z)
        {
            double a = yaw * Math.PI / 180.0, p = pitch * Math.PI / 180.0;
            double flat = Math.Cos(p) * distance;
            x = (float)(Math.Sin(a) * flat);
            y = (float)(Math.Sin(p) * distance);
            z = (float)(Math.Cos(a) * flat);
        }

        /// <summary>
        /// The camera round a car kept inside the showroom and under its ceiling: a point outside the floor's edge (less
        /// <see cref="FloorCatalogue.Wall"/>) is brought back along the line to the car, so the camera comes nearer rather
        /// than looking through a wall; and it never rises more than <see cref="FloorCatalogue.CeilingMost"/> over the floor.
        /// The car's middle (cx, cz) is taken to be on the floor. False if nothing had to be moved.
        /// </summary>
        public static bool KeepInside(float cx, float cz, ref float x, ref float y, ref float z, float floorY, float minX, float maxX, float minZ, float maxZ)
        {
            bool moved = false;
            float wall = FloorCatalogue.Wall;
            float loX = Math.Min(minX + wall, cx), hiX = Math.Max(maxX - wall, cx), loZ = Math.Min(minZ + wall, cz), hiZ = Math.Max(maxZ - wall, cz);
            float t = 1f;
            float dx = x - cx, dz = z - cz;
            if (x > hiX && dx > 0f)
            {
                t = Math.Min(t, (hiX - cx) / dx);
            }
            if (x < loX && dx < 0f)
            {
                t = Math.Min(t, (loX - cx) / dx);
            }
            if (z > hiZ && dz > 0f)
            {
                t = Math.Min(t, (hiZ - cz) / dz);
            }
            if (z < loZ && dz < 0f)
            {
                t = Math.Min(t, (loZ - cz) / dz);
            }
            if (t < 1f)
            {
                t = Math.Max(0f, t);
                x = cx + dx * t;
                z = cz + dz * t;
                moved = true;
            }
            if (y > floorY + FloorCatalogue.CeilingMost)
            {
                y = floorY + FloorCatalogue.CeilingMost;
                moved = true;
            }
            return moved;
        }

        /// <summary>A value eased towards what it should be over a frame of this many seconds: the camera never jumps.</summary>
        public static float Ease(float now, float wanted, float seconds)
        {
            double k = 1.0 - Math.Exp(-FloorCatalogue.Ease * Math.Max(0f, seconds));
            return (float)(now + (wanted - now) * k);
        }

        /// <summary>The same for an angle, the short way round (degrees).</summary>
        public static float EaseAngle(float now, float wanted, float seconds)
        {
            float d = (float)(((wanted - now) % 360.0 + 540.0) % 360.0 - 180.0);
            return Ease(now, now + d, seconds);
        }
    }
}
