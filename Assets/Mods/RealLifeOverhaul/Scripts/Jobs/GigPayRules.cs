namespace RealLifeOverhaul
{
    /// <summary>
    /// How a gig's pay is shaped over a day, a first week and a shift's end, as rules with no game types, checked offline
    /// (EconomyCheck GigPayChecks). The table's unit pay says what a kind is worth an hour (<see cref="JobCatalogue"/>); these say
    /// what the player's own tiredness and newness do to it, and what a rota shift's basic wage is for. Cost: a few sums when a gig is
    /// asked for or started, never per frame.
    /// </summary>
    public static class GigPayRules
    {
        /// <summary>The share of the usual pay for the first shift of a day, the second and so on: a tired hand works slower and a client knows it.</summary>
        public static readonly float[] Wear = { 1f, 0.9f, 0.7f, 0.55f, 0.4f };

        public const float WearLast = 0.3f;          // ...and for every shift after the table runs out
        public const int NewHandShifts = 21;         // shifts it takes to learn the ropes: three a day for a week
        public const float NewHandStart = 0.6f;     // the share of the pay the very first shift earns; it climbs evenly to the whole at NewHandShifts

        /// <summary>The share of the usual pay for a shift started after <paramref name="shiftsToday"/> gig shifts today (a part shift counts by rounding).</summary>
        public static float WearFactor(float shiftsToday)
        {
            int done = shiftsToday <= 0f ? 0 : (int)(shiftsToday + 0.5f);
            return done < Wear.Length ? Wear[done] : WearLast;
        }

        /// <summary>The share of the usual pay for someone who has worked so many gig shifts in all: the first week's pay is held back, never the later ones.</summary>
        public static float NewHandFactor(int shiftsWorked)
        {
            int done = shiftsWorked < 0 ? 0 : shiftsWorked;
            return done >= NewHandShifts ? 1f : NewHandStart + (1f - NewHandStart) * done / NewHandShifts;
        }

        /// <summary>What a freelance gig's pay is multiplied by for the player's tiredness today and newness to gigs.</summary>
        public static float Strain(float shiftsToday, int shiftsWorked) => WearFactor(shiftsToday) * NewHandFactor(shiftsWorked);

        /// <summary>The shifts a number of units of a gig make: a gig with no set size (ride-hail) is taken as <see cref="GigOffers.TaxiShift"/> fares.</summary>
        public static float ShiftsOf(JobDef def, int units)
        {
            int size = def.ShiftUnits > 0 ? def.ShiftUnits : GigOffers.TaxiShift;
            return units <= 0 ? 0f : units / (float)size;
        }

        /// <summary>The shifts a gig still has in its day, from the units left: a part shift left counts as one.</summary>
        public static int ShiftsLeft(JobDef def, int unitsLeft)
        {
            int size = def.ShiftUnits > 0 ? def.ShiftUnits : GigOffers.TaxiShift;
            return unitsLeft <= 0 ? 0 : (unitsLeft + size - 1) / size;
        }

        /// <summary>
        /// The hours of basic wage a rota shift pays when it ends. A shift of set work (flyers handed out, boxes shelved) that was done
        /// in full pays the whole <see cref="CareerCatalogue.ShiftHours"/> at once, so fast work is never docked and waiting out the
        /// clock adds nothing; a watch, a stall or a cab, which is paid for being there (<see cref="JobDef.ByHour"/>), and a shift
        /// stopped short, pay every hour begun as before. A late start still costs an hour.
        /// </summary>
        public static int WageHours(bool workDone, bool byHour, float elapsedMinutes, bool late)
        {
            if (!workDone || byHour)
            {
                return CareerCatalogue.PaidHours(elapsedMinutes, late);
            }
            return CareerCatalogue.ShiftHours - (late ? 1 : 0);
        }
    }
}
