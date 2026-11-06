using System;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>A small repeatable random source (xorshift) for a shift's draws: the same seed gives the same shift on every machine.</summary>
    public struct TradeDice
    {
        private uint _state;

        public TradeDice(uint seed)
        {
            _state = seed * 2654435761u + 0x6D2B79F5u;
            if (_state == 0)
            {
                _state = 0x2545F491u;
            }
            Next();
        }

        public uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>0 (inclusive) to 1 (exclusive).</summary>
        public float Value() => (Next() >> 8) / 16777216f;

        /// <summary>From <paramref name="low"/> to <paramref name="high"/> (exclusive); low for an empty range.</summary>
        public int Range(int low, int high) => high <= low ? low : low + (int)(Next() % (uint)(high - low));

        public float Range(float low, float high) => low + (high - low) * Value();

        /// <summary>A seed from a few numbers, mixed: the day, the site, the shift, a salt.</summary>
        public static uint Mix(int a, int b, int c, uint salt)
        {
            uint h = salt ^ 0x9E3779B9u;
            h = (h ^ (uint)a) * 0x85EBCA6Bu;
            h ^= h >> 13;
            h = (h ^ (uint)b) * 0xC2B2AE35u;
            h ^= h >> 16;
            h = (h ^ (uint)c) * 0x27D4EB2Fu;
            h ^= h >> 15;
            return h;
        }
    }

    /// <summary>How one press, pick or reading went.</summary>
    public enum StrokeGrade
    {
        Miss = 0,    // outside the zone, the wrong one, or too late: done again
        Fair = 1,    // in the zone
        Best = 2     // in its middle, or the right one quickly
    }

    /// <summary>
    /// The site trades' games and pay, game-free (EconomyCheck runs it): the timing bar's marker and zone, a match against the
    /// clock, a survey's reading, an inspection's checklist and its bribe, a night's watch, what a unit is paid by how it went,
    /// and what a trade asks. Nothing here reads the game, a clock or a random source of its own.
    /// </summary>
    public static class TradeRules
    {
        public const float BestShare = 0.36f;       // the zone's middle that counts as best, a share of the zone
        public const float SkillWidens = 0.006f;    // a point of the helping skill widens the zone by this share of the bar...
        public const float WidestZone = 0.45f;      // ...to this at most
        public const float FairWorth = 0.7f;        // a fair stroke's worth against a best one's 1
        public const float RedoWorth = 0.5f;        // a stroke done again after a miss is worth at most this
        public const float PayFloor = 0.4f;         // a unit done badly still pays this share; done perfectly, all of it
        public const float TrueShare = 0.10f;       // a whole shift's units all at best quality: a tenth on top
        public const int MissesAllowed = 2;         // misses in a row on one stroke before it is laid however it lies
        public const float MatchLeast = 1.6f;       // seconds a pick has at least, however far into the shift
        public const float MatchFast = 0.45f;       // ...a pick in this share of its time is best
        public const float SkillSeconds = 0.02f;    // a point of the helping skill adds this many seconds to a pick

        /// <summary>True if the player holds what a trade or rank asks: any of its courses, or the points of its skill. Null asks nothing.</summary>
        public static bool Holds(TradeNeed need, Func<int, bool> passed, Func<int, int> points)
        {
            if (need == null)
            {
                return true;
            }
            for (int i = 0; i < need.Courses.Length; i++)
            {
                if (passed != null && passed(need.Courses[i]))
                {
                    return true;
                }
            }
            return need.Skill >= 0 && need.Points > 0 && points != null && points(need.Skill) >= need.Points;
        }

        // ---- the timing bar ----

        /// <summary>The marker's place on the bar, 0 to 1, after so many seconds: across and back once a sweep, steady.</summary>
        public static float Marker(float seconds, float sweep)
        {
            if (sweep <= 0.01f)
            {
                return 0.5f;
            }
            float t = seconds / sweep;
            t -= (float)Math.Floor(t);
            return t < 0.5f ? t * 2f : 2f - t * 2f;
        }

        /// <summary>A crane's load swinging under the hook: eased at each end, as a pendulum is, so the middle goes by fastest.</summary>
        public static float Swing(float seconds, float sweep)
        {
            if (sweep <= 0.01f)
            {
                return 0.5f;
            }
            double phase = seconds / sweep * Math.PI * 2.0;
            return (float)(0.5 - 0.5 * Math.Cos(phase));
        }

        /// <summary>The zone's width for a trade at the player's points of its helping skill.</summary>
        public static float ZoneWidth(TradeDef t, int skillPoints)
        {
            float width = t.Zone + SkillWidens * Math.Max(0, Math.Min(100, skillPoints));
            return width > WidestZone ? WidestZone : width;
        }

        /// <summary>A new zone's middle on the bar, drawn so the whole zone stands on it.</summary>
        public static float ZoneCentre(ref TradeDice dice, float width)
        {
            float half = width * 0.5f;
            return dice.Range(half + 0.04f, 1f - half - 0.04f);
        }

        /// <summary>How a press at the marker's place went against a zone.</summary>
        public static StrokeGrade Press(float marker, float centre, float width)
        {
            float off = Math.Abs(marker - centre);
            if (off <= width * 0.5f * BestShare)
            {
                return StrokeGrade.Best;
            }
            return off <= width * 0.5f ? StrokeGrade.Fair : StrokeGrade.Miss;
        }

        /// <summary>What a stroke is worth to its unit: a best one 1, a fair one 0.7, either after a miss no more than 0.5, laid as it lies after two misses 0.</summary>
        public static float Worth(StrokeGrade grade, int missesBefore)
        {
            float worth = grade == StrokeGrade.Best ? 1f : grade == StrokeGrade.Fair ? FairWorth : 0f;
            return missesBefore > 0 ? Math.Min(worth, RedoWorth) : worth;
        }

        /// <summary>A unit's quality, 0 to 1, from the worth of its strokes.</summary>
        public static float Quality(float worthSum, int strokes)
        {
            return strokes <= 0 ? 0f : Clamp01(worthSum / strokes);
        }

        /// <summary>A unit's pay by its quality: <see cref="PayFloor"/> of the unit's pay done badly, all of it done perfectly.</summary>
        public static int UnitPay(float unitPay, float quality)
        {
            return (int)Math.Round(unitPay * (PayFloor + (1f - PayFloor) * Clamp01(quality)));
        }

        /// <summary>The bonus of a whole shift's units, every one at best: a tenth of the base.</summary>
        public static int TrueBonus(int basePay, int unitsDone, int unitsWanted, int unitsPerfect)
        {
            return unitsWanted > 0 && unitsDone >= unitsWanted && unitsPerfect >= unitsWanted ? (int)Math.Round(basePay * TrueShare) : 0;
        }

        // ---- the match: three buttons, one right ----

        /// <summary>The seconds a pick has: the trade's own, less as the shift goes on, more with its skill; never under <see cref="MatchLeast"/>.</summary>
        public static float PickSeconds(TradeDef t, int pickIndex, int skillPoints)
        {
            float seconds = t.Seconds - 0.12f * pickIndex + SkillSeconds * Math.Max(0, Math.Min(100, skillPoints));
            return seconds < MatchLeast ? MatchLeast : seconds;
        }

        /// <summary>The right one of three for a pick, drawn.</summary>
        public static int PickRight(ref TradeDice dice) => dice.Range(0, 3);

        /// <summary>How a pick went: the right one quickly is best, the right one at all fair, the wrong one or none in time a miss.</summary>
        public static StrokeGrade Pick(int chosen, int right, float taken, float allowed)
        {
            if (chosen != right || taken > allowed)
            {
                return StrokeGrade.Miss;
            }
            return taken <= allowed * MatchFast ? StrokeGrade.Best : StrokeGrade.Fair;
        }

        // ---- the survey: four corners and a reading at each ----

        public const float CornerReach = 1.4f;      // metres from a corner's peg within which its reading can be taken
        public const int Corners = 4;

        /// <summary>A corner of the land in its own axes, metres from its middle (0 front left, 1 front right, 2 back right, 3 back left).</summary>
        public static void Corner(int corner, float width, float depth, out float x, out float z)
        {
            int c = ((corner % Corners) + Corners) % Corners;
            x = (c == 0 || c == 3 ? -0.5f : 0.5f) * width;
            z = (c < 2 ? -0.5f : 0.5f) * depth;
        }

        // ---- the inspection: a checklist, sound or fault ----

        public const int Items = 8;                  // items a site's checklist has
        public const int ItemKinds = 12;             // kinds of item the list draws from (their words are reallife_trades_check_N)
        public const float FaultShare = 0.3f;        // an item's chance of being at fault on a site that is safe
        public const int UnsafeFaults = 3;           // an unsafe site has at least this many
        public const float BribeShare = 0.45f;       // the chance the contractor offers a bribe, with faults found to keep quiet about
        public const float CaughtShare = 0.3f;       // the chance a bribe taken comes out
        public const float BribeShifts = 2.5f;       // a bribe is worth this many of the shift's pay

        /// <summary>A site's checklist for a day: which kinds of item are on it and which are at fault. The same for every machine and every look.</summary>
        public static void Checklist(int siteId, int day, bool unsafeSite, int[] kinds, bool[] faults)
        {
            var dice = new TradeDice(TradeDice.Mix(siteId, day, 0, 0x1A5BEC71u));
            int faultCount = 0;
            int used = 0;
            for (int i = 0; i < Items && i < kinds.Length && i < faults.Length; i++)
            {
                int kind;
                do
                {
                    kind = dice.Range(0, ItemKinds);
                }
                while ((used & (1 << kind)) != 0);
                used |= 1 << kind;
                kinds[i] = kind;
                faults[i] = dice.Value() < FaultShare;
                faultCount += faults[i] ? 1 : 0;
            }
            for (int i = 0; unsafeSite && faultCount < UnsafeFaults && i < Items && i < faults.Length; i++)
            {
                int at = (int)(dice.Next() % (uint)Items);
                if (!faults[at])
                {
                    faults[at] = true;
                    faultCount++;
                }
            }
        }

        /// <summary>What the reading of an item says: the measure and its limit in the item's own unit, drawn sound or at fault.</summary>
        public static void Reading(int siteId, int day, int item, int kind, bool fault, out int measured, out int limit, out bool atLeast)
        {
            var dice = new TradeDice(TradeDice.Mix(siteId, day, item, 0x3C6EF372u));
            limit = LimitOf(kind, out atLeast);
            int spread = Math.Max(1, limit / 6);
            int by = dice.Range(1, spread + 1);
            // At fault: short of a limit to be reached, or over one to be kept under; sound the other way.
            measured = atLeast == fault ? limit - by : limit + by;
            if (measured < 0)
            {
                measured = 0;
            }
        }

        /// <summary>True if a reading meets its limit: the inspector's right answer for the item is "sound".</summary>
        public static bool Sound(int measured, int limit, bool atLeast) => atLeast ? measured >= limit : measured <= limit;

        // The limit of a kind of item in its own unit (the words say which), and whether a reading must reach it or keep under it.
        private static int LimitOf(int kind, out bool atLeast)
        {
            switch (kind)
            {
                case 0: atLeast = true; return 95;     // a guard rail's height, centimetres
                case 1: atLeast = false; return 25;    // the gap between scaffold boards, millimetres
                case 2: atLeast = true; return 4;      // ties to the wall a lift of scaffold
                case 3: atLeast = false; return 3;     // a wall's lean, millimetres a metre
                case 4: atLeast = true; return 25;     // the concrete's strength, newtons a square millimetre
                case 5: atLeast = true; return 5;      // fire extinguishers on the site
                case 6: atLeast = false; return 180;   // a stack of blocks' height, centimetres
                case 7: atLeast = true; return 60;     // the cover over a buried cable, centimetres
                case 8: atLeast = true; return 2;      // first aid kits
                case 9: atLeast = false; return 85;    // the noise at the hoarding, decibels
                case 10: atLeast = true; return 240;   // the hoarding's height, centimetres
                default: atLeast = true; return 15;    // a toe board's height, centimetres
            }
        }

        /// <summary>The contractor offers a bribe at the end of an inspection that found faults: drawn from the site and the day.</summary>
        public static bool BribeOffered(int siteId, int day, int faultsFound)
        {
            return faultsFound > 0 && new TradeDice(TradeDice.Mix(siteId, day, faultsFound, 0x5A3C9E01u)).Value() < BribeShare;
        }

        /// <summary>A bribe taken comes out (the city looks again): drawn from the site and the day.</summary>
        public static bool BribeCaught(int siteId, int day)
        {
            return new TradeDice(TradeDice.Mix(siteId, day, 7, 0x77E4D3A1u)).Value() < CaughtShare;
        }

        /// <summary>A bribe's amount: <see cref="BribeShifts"/> of a whole shift's pay at the rate.</summary>
        public static int Bribe(float unitPay, int units)
        {
            return (int)Math.Round(unitPay * units * BribeShifts);
        }

        // ---- the watch: a noise on a round, got to in time ----

        public const int Posts = 4;                  // places round the site a noise may come from
        public const float PostOut = 7f;             // metres out from the site's middle along its front, either side, and back
        public const float CatchReach = 2.5f;        // metres from the noise's place within which the thief is caught
        public const float NoiseChance = 0.45f;      // a round's chance of a noise

        /// <summary>For a round of the watch: whether there is a noise and at which post.</summary>
        public static bool Noise(uint seed, int round, out int post)
        {
            var dice = new TradeDice(TradeDice.Mix((int)seed, round, 0, 0x0B5E55EDu));
            post = dice.Range(0, Posts);
            return dice.Value() < NoiseChance;
        }

        /// <summary>A post round the site in its own axes: 0 left, 1 right, 2 back left, 3 back right of its middle.</summary>
        public static void Post(int post, out float x, out float z)
        {
            int p = ((post % Posts) + Posts) % Posts;
            x = (p % 2 == 0 ? -1f : 1f) * PostOut;
            z = p < 2 ? 0f : PostOut;
        }

        /// <summary>What a round of the watch is worth: a round with a thief caught counts double, one got away half.</summary>
        public static float RoundWorth(bool noise, bool caught)
        {
            return !noise ? 1f : caught ? 2f : 0.5f;
        }

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
