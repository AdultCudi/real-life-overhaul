using System;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Law;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The player's record and standing, asked in one place. Two features keep a part of it:
        /// Scripts/Law the police record (tickets, stays in a cell, getting away) and both
        /// reputations, Scripts/Justice what a court convicted them of, Scripts/Fame how well the city
        /// knows them and what it thinks of them as an owner. Everybody else (city
        /// hall, employers, friends, a licensing desk) asks here and never the two directly, so
        /// "a record" means the same thing everywhere. Read when something happens, never per
        /// frame: the longest answer walks the forty lines of the police record once.
        /// </summary>
        public static class Standing
        {
            /// <summary>Convictions a court has handed down that still count (sealed after a long clean spell).</summary>
            public static int Convictions
            {
                get
                {
                    try
                    {
                        return JusticeState.Loaded ? Justice.Priors : 0;
                    }
                    catch (Exception e)
                    {
                        Fault("Standing.Convictions", e);
                        return 0;
                    }
                }
            }

            /// <summary>A case is open against the player.</summary>
            public static bool Charged => JusticeState.Loaded && JusticeState.OpenCount > 0;

            /// <summary>
            /// A criminal record somebody would mind: a conviction, a stay in a cell, or being
            /// wanted for driving off after hurting somebody. A ticket is not one.
            /// </summary>
            public static bool HasRecord => Convictions > 0 || Arrested(int.MaxValue);

            /// <summary>Held in a cell, or wanted, within the last so many game days.</summary>
            public static bool Arrested(int withinDays)
            {
                try
                {
                    if (Record.Has(Charge.Wanted, withinDays))
                    {
                        return true;
                    }
                    int since = Record.DaysSinceLastOffence;
                    if (since < 0)
                    {
                        return false;
                    }
                    // Newest first; the record is in order of days, so the first line too old ends it.
                    int today = Record.Entry(Record.Count - 1).Day + since;
                    for (int i = Record.Count - 1; i >= 0; i--)
                    {
                        RecordEntry entry = Record.Entry(i);
                        if (withinDays != int.MaxValue && today - entry.Day > withinDays)
                        {
                            return false;
                        }
                        if (entry.Hours > 0 || entry.Outcome == Law.Outcome.Held)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("Standing.Arrested", e);
                }
                return false;
            }

            /// <summary>What the city thinks of the player, 0 (notorious) to 100 (spotless); 55 for a newcomer.</summary>
            public static int Public => Reputation.Public;

            /// <summary>How well the city knows the player, 0 (nobody) to 100 (everybody): Standing's recognition; 0 while Standing is off.</summary>
            public static int Recognition
            {
                get
                {
                    FameFile file = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                    return file != null ? file.Recognition : 0;
                }
            }

            /// <summary>What the city makes of the player's integrity as an owner, 0 to 100; 50 while Standing is off.</summary>
            public static int Integrity
            {
                get
                {
                    FameFile file = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                    return file != null ? file.Rep[(int)FamePart.Integrity] : FameRules.Start;
                }
            }

            /// <summary>The same on a scale of -100 to 100 around the newcomer's 55.</summary>
            public static int PublicSigned
            {
                get
                {
                    int from = Reputation.Public - Reputation.Start;
                    return from >= 0 ? from * 100 / (100 - Reputation.Start) : from * 100 / Reputation.Start;
                }
            }
        }
    }
}
