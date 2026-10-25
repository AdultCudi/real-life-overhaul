namespace RealLifeOverhaul.Networking
{
    /// <summary>
    /// Who holds a position and when they help: rolled from the save's cast seed and the person's place in the cast, so a
    /// person is the same on every machine and every load, and a later change to the tables never moves anybody already
    /// rolled (their record keeps what they were given). Game-free; nothing here allocates.
    /// </summary>
    public static class NetworkRules
    {
        // Per mille that somebody holds a position, by how they came into the player's life. A friend or family vouching
        // for somebody is how a city's useful people are usually met, so an introduction is far likelier to bring one.
        public const int ChanceStreet = 180;    // met by chance: in the street, at a cart, outside home, online
        public const int ChanceWork = 260;      // met after a shift: people of the working city
        public const int ChanceEvening = 300;   // met at an evening out of the Standing feature's: the well connected
        public const int ChanceFriend = 450;    // brought along by a friend
        public const int ChanceFamily = 600;    // brought along by Mom: she knows who matters

        public const int FirstStage = 2;    // SocialStage.Friend: the first favour
        public const int SecondStage = 3;   // SocialStage.CloseFriend (dating and a partner count): the second
        public const int KnownDays = 3;     // game days known before anybody offers anything
        public const int GapDays = 3;       // game days between two favours from anybody: one a few days at most

        private const int OriginWork = 3, OriginEvent = 9;          // SocialOrigin's numbers
        private const int OriginFamily = 0, OriginOldFriend = 1;    // Mom and Alex: known from the start, hold none

        /// <summary>The positions in the order a roll deals them.</summary>
        public static readonly NetRole[] Roles =
        {
            NetRole.Recruiter, NetRole.Buyer, NetRole.Wholesale, NetRole.Lawyer, NetRole.Investor, NetRole.Official, NetRole.Landlord
        };

        // Each position's two favours, the first at friends and the second at close friends; never the same twice.
        private static readonly NetFavour[] FirstOf =
        {
            NetFavour.None, NetFavour.Importer, NetFavour.Wholesaler, NetFavour.Freelance, NetFavour.JobLead, NetFavour.Investor, NetFavour.Property, NetFavour.Property
        };
        private static readonly NetFavour[] SecondOf =
        {
            NetFavour.None, NetFavour.Wholesaler, NetFavour.Terms, NetFavour.JobLead, NetFavour.Freelance, NetFavour.Property, NetFavour.JobLead, NetFavour.Investor
        };

        /// <summary>Per mille that somebody met this way holds a position; 0 for Mom and the old friend.</summary>
        public static int ChanceOf(int origin, NetVia via)
        {
            if (origin == OriginFamily || origin == OriginOldFriend)
            {
                return 0;
            }
            if (via == NetVia.Family)
            {
                return ChanceFamily;
            }
            if (via == NetVia.Friend)
            {
                return ChanceFriend;
            }
            return origin == OriginWork ? ChanceWork : origin == OriginEvent ? ChanceEvening : ChanceStreet;
        }

        /// <summary>The position of person <paramref name="person"/> of the cast, met this way; None for most.</summary>
        public static NetRole RoleOf(int castSeed, int person, int origin, NetVia via)
        {
            int chance = ChanceOf(origin, via);
            if (chance <= 0 || Hash(castSeed, person, 7301) % 1000 >= (uint)chance)
            {
                return NetRole.None;
            }
            return Roles[Hash(castSeed, person, 7302) % (uint)Roles.Length];
        }

        /// <summary>Which of <paramref name="count"/> names they work for; the same for a person on every machine.</summary>
        public static int OrgOf(int castSeed, int person, int count)
        {
            return count <= 0 ? 0 : (int)(Hash(castSeed, person, 7303) % (uint)count);
        }

        /// <summary>A place's draw for person <paramref name="person"/>: the lowest wins, so the pick needs no order of the game's lists.</summary>
        public static uint PlaceDraw(int castSeed, int person, string street, int number)
        {
            return Hash(castSeed ^ Text(street), person, 7304 + number);
        }

        /// <summary>The favour a position offers first (step 0) or second (step 1); None past them.</summary>
        public static NetFavour FavourOf(NetRole role, int step)
        {
            int r = (int)role;
            if (r <= 0 || r >= FirstOf.Length || step < 0 || step > 1)
            {
                return NetFavour.None;
            }
            return step == 0 ? FirstOf[r] : SecondOf[r];
        }

        /// <summary>
        /// The favour somebody would offer today, or None: the first once they are a friend and known <see cref="KnownDays"/>,
        /// the second once the first is done and they are a close friend; and none while another favour was done in the
        /// last <see cref="GapDays"/>. <paramref name="test"/> waives the stage, the days and the gap (a test button's call).
        /// </summary>
        public static NetFavour Next(NetContact c, int stage, int metDay, int day, int lastFavourDay, bool test = false)
        {
            if (c == null || c.Role == NetRole.None)
            {
                return NetFavour.None;
            }
            if (!test && (day - metDay < KnownDays || day - lastFavourDay < GapDays))
            {
                return NetFavour.None;
            }
            for (int step = 0; step < 2; step++)
            {
                NetFavour f = FavourOf(c.Role, step);
                if (f == NetFavour.None || c.Has(f))
                {
                    continue;
                }
                return test || stage >= (step == 0 ? FirstStage : SecondStage) ? f : NetFavour.None;
            }
            return NetFavour.None;
        }

        /// <summary>A favour's own salt for the feature that makes it: the same person and favour draw the same thing.</summary>
        public static int Salt(int person, NetFavour favour)
        {
            return person * 8 + (int)favour;
        }

        /// <summary>A text's own number, stable across machines and versions (string.GetHashCode is neither).</summary>
        public static int Text(string s)
        {
            uint h = 2166136261;
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    h = (h ^ s[i]) * 16777619;
                }
            }
            return (int)h;
        }

        /// <summary>A well-mixed number from three: the rolls' one source.</summary>
        public static uint Hash(int a, int b, int c)
        {
            uint h = (uint)a * 0x9E3779B1u;
            h ^= (uint)b * 0x85EBCA77u + 0x165667B1u + (h << 6) + (h >> 2);
            h ^= (uint)c * 0xC2B2AE3Du + 0x27D4EB2Fu + (h << 6) + (h >> 2);
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }
}
