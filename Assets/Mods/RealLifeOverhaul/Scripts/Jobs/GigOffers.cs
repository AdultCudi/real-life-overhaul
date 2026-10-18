using System;

namespace RealLifeOverhaul
{
    /// <summary>How hard a client drives the fee.</summary>
    public enum GigClient { Tight, Fair, Generous }

    /// <summary>What the day is like for one gig.</summary>
    public enum GigDay { Normal, Busy, Closed }

    /// <summary>How many people are about for a gig worked in the street.</summary>
    public enum GigCrowd { Quiet, Normal, Rush }

    /// <summary>The weather a gig is paid extra for.</summary>
    public enum GigWeather { Fair, Rain, Storm, Heat, Cold, Ice }

    /// <summary>The small events a gig may have; which ones depend on the gig (<see cref="GigOffers.EventsOf"/>).</summary>
    [Flags]
    public enum GigEvents
    {
        None = 0,
        Tipper = 1,    // a big tip from one of the people served
        Speed = 2,     // the client adds a bonus if the work is done quickly
        Redo = 4,      // a difficult customer wants it done again
        Crowd = 8,     // a quiet street or a rush
        Weather = 16   // pay for working out in bad weather
    }

    /// <summary>What makes one shift of a gig differ from another, drawn once when asked for or started. Jobs roll none of it themselves.</summary>
    public readonly struct GigOffer
    {
        public readonly string Id;
        public readonly bool Flat;            // an employer's shift, a booked set or a test: the table's figures, no offer, no events
        public readonly uint Seed;            // day, hour and gig: the same three give the same offer
        public readonly GigDay Day;
        public readonly GigClient Client;
        public readonly GigCrowd Crowd;
        public readonly GigWeather Weather;
        public readonly float ClientFactor;   // 0.80 to 1.375 of the table's pay
        public readonly float DayFactor;      // busy 1.20, otherwise the quiet figure that makes the average 1
        public readonly float WeatherFactor;  // 1 in fair weather
        public readonly float Fee;            // all of that and the calibration: what the shift's pay is multiplied by
        public readonly float CrowdFactor;    // quiet 0.70, normal 1, rush 1.30
        public readonly int Units;            // the size of the shift; 0 for a gig that goes on until the player stops
        public readonly int TipperAt;         // the unit at which a big tipper turns up (1 on), or 0
        public readonly float TipperUnits;    // ...and the tip, in units of the gig's pay
        public readonly float SpeedUnits;     // the speed bonus, in units of the gig's pay, or 0
        public readonly float SpeedShare;     // ...paid if the work is done within this share of the time allowed
        public readonly int RedoAt;           // the customer (1 on) who sends it back, or 0

        public GigOffer(string id, bool flat, uint seed, GigDay day, GigClient client, GigCrowd crowd, GigWeather weather, float clientFactor, float dayFactor,
            float weatherFactor, float fee, float crowdFactor, int units, int tipperAt, float tipperUnits, float speedUnits, float speedShare, int redoAt)
        {
            Id = id;
            Flat = flat;
            Seed = seed;
            Day = day;
            Client = client;
            Crowd = crowd;
            Weather = weather;
            ClientFactor = clientFactor;
            DayFactor = dayFactor;
            WeatherFactor = weatherFactor;
            Fee = fee;
            CrowdFactor = crowdFactor;
            Units = units;
            TipperAt = tipperAt;
            TipperUnits = tipperUnits;
            SpeedUnits = speedUnits;
            SpeedShare = speedShare;
            RedoAt = redoAt;
        }

        public bool Open => Day != GigDay.Closed;
        public bool HasSpeed => SpeedUnits > 0f;

        /// <summary>The offer a shift without one is worked at: the table's figures.</summary>
        public static GigOffer Table(JobDef def)
        {
            return new GigOffer(def.Id, true, 0u, GigDay.Normal, GigClient.Fair, GigCrowd.Normal, GigWeather.Fair, 1f, 1f, 1f, 1f, 1f, def.ShiftUnits, 0, 0f, 0f, 0f, 0);
        }
    }

    /// <summary>
    /// The one place the variation between shifts of a gig is decided; game-free, so CatalogueCheck draws thousands of
    /// offers and tests the ranges and averages. Every draw is seeded by the day, hour and gig, so the offer quoted on the
    /// phone is the one worked; the day's board by the day and gig alone. Over many shifts a gig's pay averages the table's
    /// figure (<see cref="JobCatalogue"/>): the client's and the day's factors each average 1, the size is symmetrical, the
    /// crowd averages 1, and the events' expected money is taken off the fee (<see cref="Calibration"/>). Only weather pay is
    /// extra, and only when it is bad.
    /// </summary>
    public static class GigOffers
    {
        // The client: three contiguous uniform bands; the mean is 0.4 x 0.875 + 0.4 x 1.0125 + 0.2 x 1.225 = 1.
        public const float ClientLow = 0.80f;
        public const float TightTop = 0.95f;
        public const float FairTop = 1.075f;
        public const float ClientHigh = 1.375f;
        public const float TightShare = 0.4f;
        public const float FairShare = 0.4f;     // the generous are the rest: 0.2

        // The day's board: of the gigs on the Gig Line, on average 2.5 are busy and 0.5 have nothing.
        public const float BusyPerDay = 2.5f;
        public const float ClosedPerDay = 0.5f;
        public const float BusyFee = 1.20f;
        /// <summary>The fee on a day neither busy nor closed: the figure that makes a gig's fee average 1 over the shifts on offer.</summary>
        public static float NormalFee(int gigs)
        {
            float busy = BusyPerDay / (gigs - ClosedPerDay);   // the share of offered shifts that are busy
            return (1f - busy * BusyFee) / (1f - busy);
        }

        // The street: 0.25 x 0.70 + 0.5 x 1.00 + 0.25 x 1.30 = 1.
        public const float QuietCrowd = 0.70f;
        public const float RushCrowd = 1.30f;
        public const float CrowdShare = 0.25f;   // quiet, and the same for a rush

        public const float TipperChance = 0.25f;   // a shift's chance of a big tipper
        public const float TipperLow = 1f;       // the tip, in units of the gig's pay
        public const float TipperHigh = 2f;
        public const float SpeedChance = 0.30f;  // a shift's chance of a speed bonus
        public const float SpeedLow = 0.5f;      // the bonus, in units of the gig's pay
        public const float SpeedHigh = 1.5f;
        public const float SpeedTime = 0.6f;     // paid if done within this share of the time allowed
        public const float RedoChance = 0.20f;   // a shift's chance of a difficult customer
        public const int TaxiShift = 4;          // a ride-hail shift is taken to be this many fares for the events

        // Weather pay, out of doors: a share of the fee more.
        public const float RainPay = 1.15f;
        public const float StormPay = 1.25f;
        public const float HeatPay = 1.10f;
        public const float ColdPay = 1.10f;
        public const float IcePay = 1.20f;

        /// <summary>The events a gig can have.</summary>
        public static GigEvents EventsOf(string id)
        {
            switch (id)
            {
                case JobCatalogue.Flyers: return GigEvents.Speed | GigEvents.Crowd | GigEvents.Weather;
                case JobCatalogue.Cleaner: return GigEvents.Speed | GigEvents.Redo;
                case JobCatalogue.Stocker: return GigEvents.Speed | GigEvents.Tipper | GigEvents.Weather;
                case JobCatalogue.Vendor: return GigEvents.Tipper | GigEvents.Redo | GigEvents.Crowd | GigEvents.Weather;
                case JobCatalogue.Busker: return GigEvents.Tipper | GigEvents.Crowd | GigEvents.Weather;
                case JobCatalogue.Courier: return GigEvents.Tipper | GigEvents.Weather;
                case JobCatalogue.Taxi: return GigEvents.Tipper | GigEvents.Weather;
                default: return GigEvents.None;
            }
        }

        /// <summary>The pay for working out in this weather, as a factor (1 for fair weather).</summary>
        public static float WeatherPay(GigWeather weather)
        {
            switch (weather)
            {
                case GigWeather.Rain: return RainPay;
                case GigWeather.Storm: return StormPay;
                case GigWeather.Heat: return HeatPay;
                case GigWeather.Cold: return ColdPay;
                case GigWeather.Ice: return IcePay;
                default: return 1f;
            }
        }

        /// <summary>The money the events are expected to add to a shift, as a share of its pay, from the chances and the middle of each range, the speed bonus taken as won.</summary>
        public static float EventShare(JobDef def)
        {
            GigEvents events = EventsOf(def.Id);
            float units = def.ShiftUnits > 0 ? def.ShiftUnits : TaxiShift;
            float share = 0f;
            if ((events & GigEvents.Tipper) != 0)
            {
                share += TipperChance * (TipperLow + TipperHigh) * 0.5f / units;
            }
            if ((events & GigEvents.Speed) != 0)
            {
                share += SpeedChance * (SpeedLow + SpeedHigh) * 0.5f / units;
            }
            return share;
        }

        /// <summary>What the fee is multiplied by so that the shift, events included, comes to the table's figure on average.</summary>
        public static float Calibration(JobDef def)
        {
            return 1f / (1f + EventShare(def));
        }

        /// <summary>How the day goes for a gig: busy, closed or neither. Decided by the day and the gig alone.</summary>
        public static GigDay DayOf(string id, int day)
        {
            int gigs = JobCatalogue.All.Length;
            int mine = JobCatalogue.IndexOf(id);
            if (mine < 0)
            {
                return GigDay.Normal;
            }
            // The gigs ranked for the day by a hash: the lowest may be shut, the next two or three are busy. No list is made.
            uint own = Mix((uint)day, (uint)mine, 0u, 0x51ED2701u);
            int rank = 0;
            for (int i = 0; i < gigs; i++)
            {
                if (i != mine && Mix((uint)day, (uint)i, 0u, 0x51ED2701u) < own)
                {
                    rank++;
                }
            }
            var dice = new GigDice(Mix((uint)day, 0u, 0u, 0x7A11ED01u));
            bool someShut = dice.Next() < ClosedPerDay;
            int busy = dice.Next() < BusyPerDay - 2f ? 3 : 2;
            if (rank == 0 && someShut)
            {
                return GigDay.Closed;
            }
            return rank >= 1 && rank <= busy ? GigDay.Busy : GigDay.Normal;
        }

        /// <summary>
        /// The offer for a gig in an hour of a day. <paramref name="weather"/> is the sky at the moment of asking, for gigs worked
        /// out of doors.
        /// </summary>
        public static GigOffer Draw(JobDef def, int day, int hour, GigWeather weather)
        {
            GigEvents events = EventsOf(def.Id);
            int index = JobCatalogue.IndexOf(def.Id);
            if (index < 0 || events == GigEvents.None)
            {
                return GigOffer.Table(def);
            }
            uint seed = Mix((uint)day, (uint)index, (uint)hour, 0x6E3A9C55u);
            var dice = new GigDice(seed);
            GigDay board = DayOf(def.Id, day);

            float pick = dice.Next();
            GigClient client;
            float clientFactor;
            if (pick < TightShare)
            {
                client = GigClient.Tight;
                clientFactor = ClientLow + dice.Next() * (TightTop - ClientLow);
            }
            else if (pick < TightShare + FairShare)
            {
                client = GigClient.Fair;
                clientFactor = TightTop + dice.Next() * (FairTop - TightTop);
            }
            else
            {
                client = GigClient.Generous;
                clientFactor = FairTop + dice.Next() * (ClientHigh - FairTop);
            }
            float dayFactor = board == GigDay.Busy ? BusyFee : NormalFee(JobCatalogue.All.Length);
            float weatherFactor = (events & GigEvents.Weather) != 0 ? WeatherPay(weather) : 1f;
            weather = weatherFactor > 1f ? weather : GigWeather.Fair;
            float fee = clientFactor * dayFactor * Calibration(def) * weatherFactor;

            int units = def.ShiftUnits > 0 ? def.ShiftUnits + dice.Range(-def.UnitsSpread, def.UnitsSpread) : 0;
            int span = units > 0 ? units : TaxiShift;

            GigCrowd crowd = GigCrowd.Normal;
            float crowdFactor = 1f;
            float street = dice.Next();
            if ((events & GigEvents.Crowd) != 0)
            {
                if (street < CrowdShare)
                {
                    crowd = GigCrowd.Quiet;
                    crowdFactor = QuietCrowd;
                }
                else if (street >= 1f - CrowdShare)
                {
                    crowd = GigCrowd.Rush;
                    crowdFactor = RushCrowd;
                }
            }

            // Every number is drawn whatever the gig's events, so each part of an offer always comes from the same draw.
            int tipperAt = 0;
            float tipperUnits = 0f;
            float chance = dice.Next();
            float amount = dice.Next();
            int where = dice.Range(1, span);
            if ((events & GigEvents.Tipper) != 0 && chance < TipperChance)
            {
                tipperAt = where;
                tipperUnits = TipperLow + amount * (TipperHigh - TipperLow);
            }
            float speedUnits = 0f;
            chance = dice.Next();
            amount = dice.Next();
            if ((events & GigEvents.Speed) != 0 && chance < SpeedChance)
            {
                speedUnits = SpeedLow + amount * (SpeedHigh - SpeedLow);
            }
            int redoAt = 0;
            chance = dice.Next();
            where = dice.Range(2, Math.Max(2, span));
            if ((events & GigEvents.Redo) != 0 && chance < RedoChance)
            {
                redoAt = where;
            }
            return new GigOffer(def.Id, false, seed, board, client, crowd, weather, clientFactor, dayFactor, weatherFactor, fee, crowdFactor, units, tipperAt, tipperUnits,
                speedUnits, SpeedTime, redoAt);
        }

        /// <summary>A hash of up to three numbers and a salt: the seed of a draw.</summary>
        public static uint Mix(uint a, uint b, uint c, uint salt)
        {
            unchecked
            {
                uint h = salt ^ 0x9E3779B9u;
                h = (h ^ a) * 0x85EBCA6Bu;
                h ^= h >> 13;
                h = (h ^ b) * 0xC2B2AE35u;
                h ^= h >> 16;
                h = (h ^ c) * 0x27D4EB2Fu;
                h ^= h >> 15;
                return h;
            }
        }
    }

    /// <summary>
    /// A small seeded source of numbers, the same on every machine and in the offline check (not UnityEngine.Random, whose
    /// state the game uses). A struct, so a draw makes nothing.
    /// </summary>
    public struct GigDice
    {
        private uint _state;

        public GigDice(uint seed)
        {
            _state = seed == 0u ? 0x1234567u : seed;
        }

        /// <summary>The next number, 0 up to but not including 1.</summary>
        public float Next()
        {
            unchecked
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state >> 8) * (1f / 16777216f);
            }
        }

        /// <summary>A whole number from <paramref name="low"/> to <paramref name="high"/>, both included.</summary>
        public int Range(int low, int high)
        {
            if (high <= low)
            {
                return low;
            }
            int value = low + (int)(Next() * (high - low + 1));
            return value > high ? high : value;
        }
    }
}
