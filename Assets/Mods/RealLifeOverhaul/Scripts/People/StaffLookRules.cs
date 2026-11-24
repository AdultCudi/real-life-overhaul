namespace RealLifeOverhaul
{
    /// <summary>
    /// Who a person of the pool is when they stand for somebody who works at a place: a showroom's salesperson, a data
    /// centre's technician, the people at a fitted floor's desks. Each is the same person at every visit: a seed from
    /// what they are (an employee's id, a place and a seat), the same on every machine, and the pool hands that seed's
    /// person out again while they wait on its shelf (<see cref="Rank"/>). The game rolls the look from the seed as it
    /// rolls anybody's: nothing here chooses a face, a build or a piece of clothing. Game-free; a few comparisons a person
    /// put out, nothing per frame.
    /// </summary>
    public static class StaffLookRules
    {
        public const string Employee = "employee";       // one of the game's employees, by their id: the same person wherever they work
        public const string Sales = "sales";             // a showroom's salesperson while the hour had one and nobody is on shift this moment, by its place
        public const string Technician = "technician";   // a data centre's technician, by its place and their number
        public const string Desk = "desk";               // somebody at a fitted floor's desk or meeting table, by the floor's seed and their seat's number

        public const int Kept = 0;       // on the shelf in this very person's look: put out as they are
        public const int Other = 3;      // in somebody else's look who works somewhere: dressed again only when nobody else is free

        /// <summary>The seed of somebody of a kind: FNV-1a over the kind and what tells them apart, never 0 and never negative.</summary>
        public static int Of(string kind, string key)
        {
            return PersonLook.SeedOf(kind + "|" + key);
        }

        /// <summary>The sex of somebody who has none from the game (a technician, a seat): from their seed, so it is theirs every time.</summary>
        public static bool Female(int seed)
        {
            return ((seed >> 7) & 1) == 1;
        }

        /// <summary>
        /// How soon the pool takes a civilian off its shelf for a request, lowest first (ties: whoever came back longest
        /// ago). For anybody (<paramref name="wanted"/> 0): one in a look of the free roll not yet due a change, then one
        /// due a change, then somebody's own look as it is. For one person: that person as they are (<see cref="Kept"/>),
        /// then a free roll due a change, then any free roll, then somebody else's own look (<see cref="Other"/>).
        /// </summary>
        public static int Rank(int wanted, bool wantedFemale, int worn, bool wornFemale, bool due)
        {
            if (wanted == 0)
            {
                return worn != 0 ? 2 : due ? 1 : 0;
            }
            if (!Redress(wanted, wantedFemale, worn, wornFemale))
            {
                return Kept;
            }
            return worn != 0 ? Other : due ? 1 : 2;
        }

        /// <summary>True if the one taken off the shelf has to be dressed for the request: one person is wanted and this is not their look.</summary>
        public static bool Redress(int wanted, bool wantedFemale, int worn, bool wornFemale)
        {
            return wanted != 0 && (worn != wanted || wornFemale != wantedFemale);
        }

        /// <summary>True if a look on the shelf is due a change by its use (Looks.Worn): never somebody's own, kept for when they are put out again.</summary>
        public static bool Ages(int worn)
        {
            return worn == 0;
        }
    }
}
