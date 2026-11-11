using System;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>The two desks: the cab office's fares and the parcel depot's delivery runs.</summary>
    public enum SideKind
    {
        Cab = 0,
        Parcels = 1
    }

    /// <summary>Why a desk will not sign a driver on with a vehicle; the desk says each in words.</summary>
    public enum SideRefusal
    {
        None,
        NoCar,          // no vehicle of the driver's own stands near the desk
        NotMotor,       // nothing with an engine
        Licence,        // no valid licence for the vehicle's class
        Endorsement,    // the cab office: no taxi endorsement
        Seats,          // the cab office: fewer seats than a fare needs
        Room,           // the depot: no room for a run's parcels
        Condition,      // too damaged to be sent out
        Fuel,           // the tank is all but empty
        DayDone         // the desk's day has nothing left for this driver
    }

    /// <summary>What a desk reads of the vehicle a driver brings, as plain figures.</summary>
    public struct SideCar
    {
        public bool Found;        // a vehicle of the driver's own stands near
        public bool Motor;        // it has an engine
        public bool Licensed;     // the driver holds a valid licence for its class
        public bool Endorsed;     // ...and the taxi endorsement
        public int Seats;         // the driver's included
        public int Cargo;         // boxes the game says it holds
        public int Condition;     // per cent: 100 is undamaged
        public int Fuel;          // per cent of its tank
    }

    /// <summary>One day of a firm's books: its other drivers' work by the same sums the player's is paid by.</summary>
    public struct SideBooks
    {
        public int Drivers;
        public int Units;         // fares or parcels
        public int Gross;         // what the customers paid
        public int Kept;          // the firm's share
        public int Best;          // the most one driver cleared, tips included
    }

    /// <summary>
    /// Every rule and figure of the taxi and delivery shifts driven in the player's own vehicle (docs/features/side-jobs.md):
    /// which vehicle a desk takes, the cab's meter by distance and time, the firm's share, how a ride's quality is marked and
    /// tipped, a vehicle's room for parcels, each drop's time and its pay by distance and punctuality, the fuel's cost, and a
    /// firm's books for its other drivers. No game types; EconomyCheck's SideJobChecks runs them. Every sum is whole and the
    /// same on every machine: nothing here is drawn but a firm's books, from the save's seed and the day.
    /// </summary>
    public static class SideJobRules
    {
        public const float CarWithin = 80f;         // metres from the desk (and from the driver at the start) the vehicle may stand
        public const int SignOnMinutes = 10;        // game minutes at the desk: the licence looked at, the vehicle logged, the meter or the sheets handed over
        public const int CabSeats = 4;              // seats, the driver's included: a fare is up to three people
        public const int CabCondition = 60;         // per cent: a cab more damaged than this is not sent out
        public const int ParcelCondition = 40;      // ...and a delivery vehicle
        public const int FuelLeast = 10;            // per cent of the tank
        public const int CargoLeast = 2;            // boxes: less is no delivery vehicle
        public const int DropsMost = 8;             // parcels a run at most, whatever the vehicle (CourierJob keeps ten places)
        public const int FoodMost = 3;              // meals a run: more would go cold
        public const int CabDay = 24;               // fares a driver is given in a day
        public const int ParcelDay = 30;            // parcels
        public const int CommissionMille = 200;     // thousandths of the meter the cab office keeps; tips are the driver's
        public const int MarginMille = 250;         // thousandths of what a client pays for a drop that the depot keeps
        public const float DistanceShare = 0.85f;   // of the gig's fare by distance: the rest is the meter's time
        public const float SecondsRate = 0.21f;     // money a second of the ride at the start's prices: a ride at par comes to the gig's fare
        public const float TimeMost = 1.5f;         // times the ride's par: the meter stops counting time there, so standing still earns nothing
        public const float TripLeast = 250f;        // metres: a shorter ride is no fare at all
        public const int LateMost = 25;             // quality a slow ride can lose
        public const int PickupLate = 5;            // ...and reaching the passenger in the last quarter of their patience
        public const float PickupLateFrom = 0.75f;  // share of the time to the pick-up
        public const int OffenceCost = 8;           // quality lost for each offence on a police bill during the ride
        public const int FledCost = 30;             // ...and for having driven off from the police first
        public const float FoodPay = 1.15f;         // a meal's pay against a parcel's
        public const float FoodTime = 0.8f;         // ...and its time
        public const int EarlyMille = 1100;         // thousandths of a drop's pay when it is made with a quarter of its time to spare
        public const float EarlyShare = 0.25f;
        public const int LateLeastMille = 500;      // the least a late drop pays
        public const float LateFull = 120f;         // seconds late at which it pays that
        public const float RunOvertime = 1.6f;      // times the run's own time before the depot calls the rest of it back

        private static readonly string[] FoodWords =
        {
            "restaurant", "fastfood", "coffee", "pizza", "bakery", "burger", "diner", "icecream", "sushi", "deli", "juice", "donut"
        };

        /// <summary>The fares or parcels a desk gives one driver in a day.</summary>
        public static int DayMost(SideKind kind) => kind == SideKind.Cab ? CabDay : ParcelDay;

        /// <summary>Why a desk refuses a vehicle, the first reason that holds; None when it signs the driver on.</summary>
        public static SideRefusal Refusal(SideKind kind, in SideCar car, int leftToday)
        {
            if (!car.Found)
            {
                return SideRefusal.NoCar;
            }
            if (!car.Motor)
            {
                return SideRefusal.NotMotor;
            }
            if (!car.Licensed)
            {
                return SideRefusal.Licence;
            }
            if (kind == SideKind.Cab && !car.Endorsed)
            {
                return SideRefusal.Endorsement;
            }
            if (kind == SideKind.Cab && car.Seats < CabSeats)
            {
                return SideRefusal.Seats;
            }
            if (kind == SideKind.Parcels && Drops(car.Cargo, false) <= 0)
            {
                return SideRefusal.Room;
            }
            if (car.Condition < (kind == SideKind.Cab ? CabCondition : ParcelCondition))
            {
                return SideRefusal.Condition;
            }
            if (car.Fuel < FuelLeast)
            {
                return SideRefusal.Fuel;
            }
            return leftToday <= 0 ? SideRefusal.DayDone : SideRefusal.None;
        }

        /// <summary>A vehicle's condition in per cent from the game's damage (0 undamaged, 1 a wreck).</summary>
        public static int Condition(float damage)
        {
            float left = 1f - (damage < 0f ? 0f : damage > 1f ? 1f : damage);
            return (int)Math.Round(left * 100f);
        }

        /// <summary>How full a tank is, in per cent.</summary>
        public static int FuelShare(float litres, float tank)
        {
            if (tank <= 0f)
            {
                return 0;
            }
            float share = litres / tank;
            return (int)Math.Round((share < 0f ? 0f : share > 1f ? 1f : share) * 100f);
        }

        // The cab's meter.

        /// <summary>The seconds a ride of this length takes a sensible driver: the gig's own par.</summary>
        public static float Par(float metres) => metres / 7.5f + 20f;

        /// <summary>
        /// The meter for a ride: the flag fall and the distance at <see cref="DistanceShare"/> of the gig's, and the ride's
        /// seconds, counted no further than <see cref="TimeMost"/> times its par. Nothing for a ride under <see cref="TripLeast"/>.
        /// </summary>
        /// <param name="unitPay">The gig's pay a fare before distance (JobDef.UnitPay).</param>
        /// <param name="perMetre">The gig's pay a metre (JobDef.PayPerMetre).</param>
        /// <param name="rate">The driver's level, the pay option and the city's price level as one factor.</param>
        public static int Fare(float unitPay, float perMetre, float metres, float seconds, float rate)
        {
            if (metres < TripLeast || rate <= 0f)
            {
                return 0;
            }
            float most = Par(metres) * TimeMost;
            float counted = seconds < 0f ? 0f : seconds > most ? most : seconds;
            return (int)Math.Round((DistanceShare * (unitPay + perMetre * metres) + SecondsRate * counted) * rate);
        }

        /// <summary>The cab office's share of a metered fare.</summary>
        public static int Kept(int fare) => fare <= 0 ? 0 : (int)(((long)fare * CommissionMille + 500) / 1000);

        /// <summary>
        /// A ride's quality out of 100: the passenger's comfort (hard stops, bumps, speeding, crashes, being pulled over), less for
        /// a ride slower than par, for a pick-up that came late, for each offence the police booked and for driving off from them.
        /// </summary>
        public static int Quality(int comfort, float seconds, float par, float pickupUsed, int offences, bool fled)
        {
            int quality = comfort;
            if (par > 0f && seconds > par)
            {
                int late = (int)Math.Round((seconds - par) / par * 50f);
                quality -= late > LateMost ? LateMost : late;
            }
            if (pickupUsed > PickupLateFrom)
            {
                quality -= PickupLate;
            }
            quality -= (offences > 0 ? offences : 0) * OffenceCost;
            if (fled)
            {
                quality -= FledCost;
            }
            return quality < 0 ? 0 : quality > 100 ? 100 : quality;
        }

        /// <summary>The tip as a share of the meter for a ride of this quality: the middle of what the gig's passengers give.</summary>
        public static float TipShare(int quality)
        {
            return quality >= 90 ? 0.26f : quality >= 75 ? 0.15f : quality >= 55 ? 0.06f : 0f;
        }

        // The depot's runs.

        /// <summary>True for a business whose orders are meals, by its type's name: a quicker, smaller, better paid run.</summary>
        public static bool IsFood(string businessType)
        {
            if (string.IsNullOrEmpty(businessType))
            {
                return false;
            }
            for (int i = 0; i < FoodWords.Length; i++)
            {
                if (businessType.IndexOf(FoodWords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The parcels (or meals) a run a vehicle has room for, from the boxes the game says it holds; 0 for no delivery vehicle.</summary>
        public static int Drops(int cargo, bool food)
        {
            if (cargo < CargoLeast)
            {
                return 0;
            }
            int room = 2 + cargo / 4;
            room = room > DropsMost ? DropsMost : room;
            return food && room > FoodMost ? FoodMost : room;
        }

        /// <summary>The seconds from loading within which the drop at this place of the planned order is on time.</summary>
        /// <param name="metresSoFar">The planned road to it from the pick-up, through the drops before it.</param>
        /// <param name="index">Its place in the planned order, from 0.</param>
        public static float DueIn(float metresSoFar, int index, bool food)
        {
            float seconds = 50f + (metresSoFar > 0f ? metresSoFar : 0f) / 4.5f + 38f * (index + 1);
            return food ? seconds * FoodTime : seconds;
        }

        /// <summary>Thousandths of a drop's pay by when it was made: more with a quarter of its time to spare, less by how late.</summary>
        /// <param name="late">Seconds after its time; negative for before.</param>
        /// <param name="window">The seconds it was given (<see cref="DueIn"/>).</param>
        public static int Punctual(float late, float window)
        {
            if (late <= 0f)
            {
                return -late >= window * EarlyShare && window > 0f ? EarlyMille : 1000;
            }
            float share = late >= LateFull ? 1f : late / LateFull;
            return 1000 - (int)Math.Round((1000 - LateLeastMille) * share);
        }

        /// <summary>A drop's pay: the gig's pay a door and a metre of its leg, at the driver's rate, a meal's more, by punctuality.</summary>
        public static int DropPay(float unitPay, float perMetre, float legMetres, float rate, int punctualMille, bool food)
        {
            if (rate <= 0f)
            {
                return 0;
            }
            float pay = (unitPay + perMetre * (legMetres > 0f ? legMetres : 0f)) * rate * (food ? FoodPay : 1f);
            return (int)Math.Round(pay * punctualMille / 1000f);
        }

        /// <summary>What the depot's client paid for drops that paid the driver this much: the depot keeps <see cref="MarginMille"/> of it.</summary>
        public static int ClientPaid(int driverPay) => driverPay <= 0 ? 0 : (int)(((long)driverPay * 1000 + (1000 - MarginMille) / 2) / (1000 - MarginMille));

        /// <summary>The cost of fuel burned, whole money, at the pump's price a litre in cents.</summary>
        public static int FuelCost(float litres, int pumpCents)
        {
            return litres <= 0f || pumpCents <= 0 ? 0 : (int)Math.Round(litres * pumpCents / 100f);
        }

        // A firm's books.

        /// <summary>
        /// A day of a firm's other drivers, each fare and drop by the sums above: so many drivers, each with a level and a day's
        /// work, the rides' lengths, times and qualities and the drops' legs and lateness drawn from the seed, the day and the
        /// firm. The same answer for the same seed and day on every machine. At most a few hundred sums: asked when a desk's
        /// page is written, never a frame.
        /// </summary>
        /// <param name="level">The city's price level in tenths of a point (1000 at the start).</param>
        public static SideBooks Books(int seed, int day, SideKind kind, int level, float unitPay, float perMetre, float levelBonus)
        {
            var books = new SideBooks();
            uint dice = Mix(seed, day, (int)kind);
            books.Drivers = kind == SideKind.Cab ? 6 + (int)(Next(ref dice) % 7) : 5 + (int)(Next(ref dice) % 6);
            for (int d = 0; d < books.Drivers; d++)
            {
                float rate = (1f + levelBonus * (d % 4)) * level / 1000f;
                int cleared = 0;
                if (kind == SideKind.Cab)
                {
                    int fares = 8 + (int)(Next(ref dice) % 13);
                    for (int f = 0; f < fares; f++)
                    {
                        float metres = 280f + Next(ref dice) % 671;
                        float seconds = Par(metres) * (0.85f + Next(ref dice) % 50 / 100f);
                        int quality = Quality(60 + (int)(Next(ref dice) % 41), seconds, Par(metres), Next(ref dice) % 100 / 100f, 0, false);
                        int fare = Fare(unitPay, perMetre, metres, seconds, rate);
                        int kept = Kept(fare);
                        books.Gross += fare;
                        books.Kept += kept;
                        cleared += fare - kept + (int)Math.Round(fare * TipShare(quality));
                    }
                    books.Units += fares;
                }
                else
                {
                    int drops = 10 + (int)(Next(ref dice) % 17);
                    for (int p = 0; p < drops; p++)
                    {
                        float leg = 110f + Next(ref dice) % 411;
                        float window = DueIn(leg, (int)(Next(ref dice) % 4), false);
                        float late = Next(ref dice) % 100 < 70 ? -window * (Next(ref dice) % 50 / 100f) : Next(ref dice) % 150;
                        int pay = DropPay(unitPay, perMetre, leg, rate, Punctual(late, window), false);
                        int paid = ClientPaid(pay);
                        books.Gross += paid;
                        books.Kept += paid - pay;
                        cleared += pay;
                    }
                    books.Units += drops;
                }
                books.Best = cleared > books.Best ? cleared : books.Best;
            }
            return books;
        }

        private static uint Mix(int seed, int day, int kind)
        {
            uint x = (uint)seed * 2654435761u ^ (uint)day * 40503u ^ (uint)(kind + 1) * 2246822519u;
            return x == 0 ? 0x9E3779B9u : x;
        }

        // A small xorshift: the same run of numbers from the same start on every machine.
        private static uint Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
