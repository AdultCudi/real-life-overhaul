using System;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// The rival tycoons' health, game-free: each forward rival's day is drawn from the save's seed, one who falls ill rests (no move of theirs, their fortune growing at half the
    /// pace) and one in hospital pays its bill from the worth over their floor, and the morning's paper hears of it. COST: a few operations a rival a day.
    /// </summary>
    public static class HealthRivals
    {
        /// <summary>A tycoon's worth as health asks for it: -1 while they are not forward or are out, and for every one in a Going Public session, where the rivals rest.</summary>
        public static long WorthIn(bool inSession, bool active, long worth) => inSession || !active ? -1L : worth;

        /// <summary>
        /// The rivals' day: each forward tycoon is drawn (<see cref="HealthRules.RivalDay"/>). One who falls ill is told to rest until the day after theirs ends, and one whose
        /// case needs the hospital pays its bill (<paramref name="pay"/> answers what really left them) and is noted for the paper. Returns how many fell ill today.
        /// </summary>
        public static int Day(HealthState s, int day, int wave, int level, Func<int, long> worth, Func<int, long, long> pay, Action<int, int> rest)
        {
            if (worth == null)
            {
                return 0;
            }
            int began = 0;
            for (int i = 0; i < s.RivalCases.Length; i++)
            {
                if (worth(i) < 0)
                {
                    continue;   // not forward, out, or resting in a Going Public session
                }
                RivalHealth r = s.RivalCases[i];
                if (!HealthRules.RivalDay(r, s.Seed, i, day, wave))
                {
                    continue;
                }
                began++;
                rest?.Invoke(i, r.Until + 1);
                if (r.Serious)
                {
                    long bill = HealthCover.StayBill(HealthRules.StayDays(r.Kind), false, level);
                    r.Bill = pay != null ? pay(i, bill) : 0L;
                    s.Notes.Add(new HealthNote { Day = day, Rival = i, Kind = r.Kind });
                }
            }
            return began;
        }
    }
}
