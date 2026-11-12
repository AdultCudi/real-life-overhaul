namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The words a firm, a data centre or a stake in a startup wears in the Tech app's firms table, and how its takings moved against the month before. Pure rules over
    /// numbers the directors already keep, so the table's chips and its change column are the same on every screen and checked offline. Game-free.
    /// </summary>
    public static class TechStatusRules
    {
        public const int InProduction = 0, Running = 1, NeedsProduct = 2, NoTeam = 3, NearlyFull = 4, Down = 5, Expanding = 6, Growing = 7, SeedFunded = 8, Closed = 9, Count = 10;

        /// <summary>The days a firm must have run before a change against the month before is told: a month to fill the ring, a month for the one
        /// before it. Fewer, and the page says the firm is new.</summary>
        public const int ChangeFromDays = 60;

        /// <summary>The days of takings a month holds.</summary>
        public const int MonthDays = 30;

        /// <summary>Per cent of its racks a data centre's racks may reach before it is called nearly full.</summary>
        public const int FullPercent = 90;

        public static int Firm(bool makesProducts, int people, int building, int onSale)
        {
            if (people <= 0)
            {
                return NoTeam;
            }
            if (building > 0)
            {
                return InProduction;
            }
            return !makesProducts || onSale > 0 ? Running : NeedsProduct;
        }

        public static int Centre(int racks, int room, bool down, bool supplied)
        {
            if (down || !supplied)
            {
                return Down;
            }
            return room > 0 && racks * 100 >= room * FullPercent ? NearlyFull : Running;
        }

        /// <summary>A startup the player holds a stake in: seed funded in the first rounds, growing after, expanding at the later ones, closed when it has failed.</summary>
        public static int Startup(bool alive, int stage)
        {
            if (!alive)
            {
                return Closed;
            }
            return stage <= 1 ? SeedFunded : stage <= 3 ? Growing : Expanding;
        }

        /// <summary>The change of a month's takings against the month before, whole per cent; false while the month before has too few days to say.</summary>
        public static bool Change(long now, long before, int beforeDays, out int percent)
        {
            percent = 0;
            if (before <= 0 || beforeDays < ChangeFromDays)
            {
                return false;
            }
            long p = (now - before) * 100 / before;
            percent = (int)(p > 9999 ? 9999 : p < -9999 ? -9999 : p);
            return true;
        }

        /// <summary>
        /// One day's takings gone out of a 30 day ring, aged into the month before: it gathers the days as they leave the ring (the first 30 days of a new firm's ring hold nothing, and
        /// the 30 after them are the month before exactly), then loses a thirtieth of itself a day as it takes the next, so it stays about a month's worth. One number a firm, never a
        /// second ring.
        /// </summary>
        public static void Age(ref long before, ref int beforeDays, long leaving)
        {
            if (beforeDays < ChangeFromDays)
            {
                before += leaving;
                beforeDays++;
                return;
            }
            before += leaving - before / MonthDays;
            if (before < 0)
            {
                before = 0;
            }
        }
    }
}
