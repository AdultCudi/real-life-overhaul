namespace RealLifeOverhaul
{
    /// <summary>
    /// What a ride-hail passenger minds and what it costs the ride's comfort (out of 100), in one table for each way the
    /// "Ride fares: easier passengers" option can be: a player found the passengers harsh and asked for an easier ride that
    /// keeps today's for those who like it. Easier passengers mind a hard stop, a swerve, a bump and speeding later, less and
    /// less often; crashes, police stops and how comfort turns into the tip and the slip are the same in both. No game
    /// types, checked offline (EconomyCheck TaxiComfortChecks); the table is picked once a fare, so it costs nothing per frame.
    /// </summary>
    public sealed class TaxiComfortTable
    {
        public readonly float JoltCost;        // comfort lost to a hard stop, a swerve or a bump
        public readonly float JoltGap;         // seconds: two jolts closer than this count as one
        public readonly float HarshScale;      // times the car's own hard stop (TaxiJob.Tune) before the passenger minds
        public readonly float BumpOver;        // m/s2 more than a hard stop in a single tick is a bump
        public readonly float SpeedingAbove;   // m/s with a passenger aboard (26 is about 94 km/h)
        public readonly float SpeedingCost;    // comfort lost a second while above it
        public readonly float CrashHard;       // comfort lost to a crash faster than CrashFast
        public readonly float CrashSoft;       // ...and to a slower one
        public readonly float PoliceCost;      // comfort lost to being pulled over
        public readonly int LowComfort;        // below this comfort at the end, the slip loses LowCut of the fare
        public readonly float LowCut;          // share of the fare

        public TaxiComfortTable(float joltCost, float joltGap, float harshScale, float bumpOver, float speedingAbove, float speedingCost,
                                float crashHard, float crashSoft, float policeCost, int lowComfort, float lowCut)
        {
            JoltCost = joltCost;
            JoltGap = joltGap;
            HarshScale = harshScale;
            BumpOver = bumpOver;
            SpeedingAbove = speedingAbove;
            SpeedingCost = speedingCost;
            CrashHard = crashHard;
            CrashSoft = crashSoft;
            PoliceCost = policeCost;
            LowComfort = lowComfort;
            LowCut = lowCut;
        }
    }

    /// <summary>The two tables and the arithmetic the taxi gig asks of them (<see cref="TaxiComfortTable"/>).</summary>
    public static class TaxiComfortRules
    {
        public const float CrashFast = 8f;     // m/s: a crash above this is a hard one

        /// <summary>The passengers as they have always been: the option off.</summary>
        public static readonly TaxiComfortTable Normal = new TaxiComfortTable(8f, 3f, 1f, 5.5f, 26f, 4f, 40f, 22f, 20f, 30, 0.3f);

        /// <summary>
        /// Easier passengers: half the cost of a jolt and at most one every 6 seconds, a third more braking before they mind
        /// (never past an emergency stop), a bump only past a harder knock, speeding at well under half the cost; the rest as
        /// <see cref="Normal"/>.
        /// </summary>
        public static readonly TaxiComfortTable Easier = new TaxiComfortTable(4f, 6f, 1.35f, 8f, 26f, 1.5f, 40f, 22f, 20f, 30, 0.3f);

        /// <summary>The table for the option.</summary>
        public static TaxiComfortTable For(bool easier)
        {
            return easier ? Easier : Normal;
        }

        /// <summary>
        /// The braking or swerving a passenger minds in this car: the car's own hard stop, raised by the table, but never past
        /// <paramref name="most"/> (an emergency stop) unless the car's own already is.
        /// </summary>
        public static float Harsh(float carHarsh, TaxiComfortTable t, float most)
        {
            float raised = carHarsh * t.HarshScale;
            return raised > most ? (carHarsh > most ? carHarsh : most) : raised;
        }

        /// <summary>The comfort a crash at <paramref name="speed"/> m/s costs.</summary>
        public static float Crash(float speed, TaxiComfortTable t)
        {
            return speed > CrashFast ? t.CrashHard : t.CrashSoft;
        }

        /// <summary>The share of the fare the slip loses for a ride that ended at <paramref name="comfort"/>, on top of crashes and stops.</summary>
        public static float LowShare(int comfort, TaxiComfortTable t)
        {
            return comfort < t.LowComfort ? t.LowCut : 0f;
        }

        /// <summary>The table as one line for the log and the test button.</summary>
        public static string Describe(TaxiComfortTable t)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return "jolt " + t.JoltCost.ToString("0.##", inv) + " comfort, at most every " + t.JoltGap.ToString("0.##", inv) + " s; hard stop at x"
                   + t.HarshScale.ToString("0.##", inv) + " of the car's; bump at " + t.BumpOver.ToString("0.##", inv) + " m/s2 over; speeding above "
                   + t.SpeedingAbove.ToString("0.##", inv) + " m/s, " + t.SpeedingCost.ToString("0.##", inv) + " a second; crash "
                   + t.CrashHard.ToString("0.##", inv) + " or " + t.CrashSoft.ToString("0.##", inv) + "; police " + t.PoliceCost.ToString("0.##", inv)
                   + "; under " + t.LowComfort + " comfort the slip loses " + (t.LowCut * 100f).ToString("0", inv) + "% of the fare";
        }
    }
}
