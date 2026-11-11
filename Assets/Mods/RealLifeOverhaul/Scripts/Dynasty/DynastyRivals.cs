using System;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>What a rival's line did on a day.</summary>
    public enum RivalEvent
    {
        None = 0,
        Retired = 1,     // the head handed over alive
        Died = 2,        // the head died in office: the estate passed
        ElderDied = 3    // a retired head died: nothing more passes
    }

    /// <summary>
    /// The rival tycoons' lines, with no game in them: every head has an age, an age to retire at and an age to die at, drawn from the save's seed, the tycoon and the
    /// generation, so they are the same on every machine. A handover is taxed by the player's own bands (<see cref="DynastyRules.Tax"/>) on the whole fortune, and the heir
    /// takes the holding. COST: a few operations a tycoon a day.
    /// </summary>
    public static class DynastyRivals
    {
        /// <summary>The first look at a tycoon: the head's age today, and when they retire and die.</summary>
        public static void Begin(RivalLine r, int seed, int tycoon, int day, int yearDays)
        {
            if (r.Ready)
            {
                return;
            }
            int age = DynastyCatalogue.RivalAgeFrom + (int)(DynastyRules.Hash(seed, 11, tycoon, r.Generation) % (uint)DynastyCatalogue.RivalAgeSpan);
            // born on a day of the year of their own, so the three do not all turn a year older together
            r.Born = day - age * Math.Max(1, yearDays) - (int)(DynastyRules.Hash(seed, 12, tycoon, r.Generation) % (uint)Math.Max(1, yearDays));
            Ages(r, seed, tycoon, age);
        }

        // When this head retires and dies; one in a few never retires and dies in office. Always later than the age they have.
        private static void Ages(RivalLine r, int seed, int tycoon, int age)
        {
            bool office = DynastyRules.Hash(seed, 13, tycoon, r.Generation) % (uint)DynastyCatalogue.RivalInOffice == 0;
            int death = DynastyCatalogue.RivalDeathFrom + (int)(DynastyRules.Hash(seed, 14, tycoon, r.Generation) % (uint)DynastyCatalogue.RivalDeathSpan);
            if (office)
            {
                r.RetireAge = int.MaxValue;
                r.DeathAge = Math.Max(age + 1, DynastyCatalogue.RivalOfficeFrom + (int)(DynastyRules.Hash(seed, 15, tycoon, r.Generation) % (uint)DynastyCatalogue.RivalOfficeSpan));
                return;
            }
            r.RetireAge = Math.Max(age + 1, DynastyCatalogue.RivalRetireFrom + (int)(DynastyRules.Hash(seed, 16, tycoon, r.Generation) % (uint)DynastyCatalogue.RivalRetireSpan));
            r.DeathAge = Math.Max(r.RetireAge + 2, death);
        }

        public static int Age(RivalLine r, int day, int yearDays) => r.Ready ? Math.Max(0, day - r.Born) / Math.Max(1, yearDays) : 0;

        /// <summary>What is due on a day: a retired head's death first, then the head's own retirement or death.</summary>
        public static RivalEvent Due(RivalLine r, int day, int yearDays)
        {
            if (!r.Ready)
            {
                return RivalEvent.None;
            }
            if (r.ElderBorn != int.MinValue && (day - r.ElderBorn) / Math.Max(1, yearDays) >= r.ElderDeath)
            {
                return RivalEvent.ElderDied;
            }
            int age = Age(r, day, yearDays);
            return age >= r.DeathAge ? RivalEvent.Died : age >= r.RetireAge ? RivalEvent.Retired : RivalEvent.None;
        }

        /// <summary>The estate tax on a tycoon's fortune, by the player's own bands.</summary>
        public static long TaxOf(long worth, int level) => DynastyRules.Tax(Math.Max(0L, worth), level);

        /// <summary>
        /// What of the tax really leaves a tycoon: only the worth over their floor, the start's worth at today's prices that the rivals raise a worth to every day; a tax taken
        /// from the floor itself would be made up by the next morning, so it is not said to have been paid.
        /// </summary>
        public static long Paid(long tax, long worth, long floor) => Math.Max(0L, Math.Min(tax, worth - Math.Max(0L, floor)));

        /// <summary>
        /// The handover: the old head retires (and lives on as the family's elder) or dies, the heir takes the holding with an age and a retiring and a dying age of their
        /// own, and the generation counts on. <paramref name="paid"/> is the tax that really left the fortune.
        /// </summary>
        public static void HandOver(RivalLine r, RivalEvent how, int seed, int tycoon, int day, int yearDays, long worth, long paid)
        {
            if (how != RivalEvent.Retired && how != RivalEvent.Died)
            {
                return;
            }
            if (how == RivalEvent.Retired)
            {
                r.ElderBorn = r.Born;
                r.ElderDeath = r.DeathAge;
            }
            r.HandDay = day;
            r.HandMode = how == RivalEvent.Retired ? HandMode.Retire : HandMode.Death;
            r.HandWorth = worth;
            r.HandTax = paid;
            r.Generation++;
            int age = DynastyCatalogue.RivalHeirFrom + (int)(DynastyRules.Hash(seed, 17, tycoon, r.Generation) % (uint)DynastyCatalogue.RivalHeirSpan);
            r.Born = day - age * Math.Max(1, yearDays);
            Ages(r, seed, tycoon, age);
        }

        /// <summary>A retired head's death: the family keeps no elder now.</summary>
        public static void ElderGone(RivalLine r)
        {
            r.ElderBorn = int.MinValue;
            r.ElderDeath = 0;
        }
    }

    /// <summary>
    /// The city's households, in the population's own terms: a household that loses a member keeps its savings and its debts (the ones left inherit, LifeRules.AfterLoss), and
    /// one with nobody left is taken in by family beyond the city, its savings with it. This counts those estates by the game's year, with the tax the player's own bands
    /// put on them and the debts nothing was left to pay. Game-free. COST: a few additions a household that ends.
    /// </summary>
    public static class DynastyCity
    {
        /// <summary>The year's count starts again when the game's year turns; the year that ended is kept beside it.</summary>
        public static void Turn(CityEstates c, int year)
        {
            if (c.Year == year)
            {
                return;
            }
            bool next = c.Year >= 0 && year == c.Year + 1;
            c.LastCount = next ? c.Count : 0;
            c.LastTaxed = next ? c.Taxed : 0;
            c.LastValue = next ? c.Value : 0;
            c.LastTax = next ? c.Tax : 0;
            c.LastUnpaid = next ? c.Unpaid : 0;
            c.Year = year;
            c.Count = c.Taxed = 0;
            c.Value = c.Tax = c.Unpaid = 0;
        }

        /// <summary>A household ended with a death: what it left after its debts, the tax on it, and what its lenders lost. Returns the tax.</summary>
        public static long Ended(CityEstates c, long savings, long debt, int level)
        {
            long net = Math.Max(0L, savings) - Math.Max(0L, debt);
            c.Count++;
            if (net < 0)
            {
                c.Unpaid += -net;
                return 0L;
            }
            long tax = DynastyRules.Tax(net, level);
            c.Value += net;
            c.Tax += tax;
            c.Taxed += tax > 0 ? 1 : 0;
            return tax;
        }
    }
}
