using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a shift left to the clock (<see cref="ShiftSkip"/>) says on the game's work panel before it starts and while it runs, as
    /// rules with no game types, checked offline (EconomyCheck ShiftFactsChecks): the part of the shift the slider keeps, the pay of
    /// the units it covers (the same sum the shift settles with), the stress it adds (the same sum the hourly upkeep uses), the skill
    /// it trains, and the hour it has got to. Cost: a few sums when the panel is laid out.
    /// </summary>
    public static class ShiftFactsRules
    {
        /// <summary>The slider's right end: the whole shift in hours, its part hour counted (never under one).</summary>
        public static int MostHours(int wholeMinutes) => Math.Max(1, (wholeMinutes + 59) / 60);

        /// <summary>The hours kept from the slider's value: 0 for the whole shift (the right end, or nothing set).</summary>
        public static int Chosen(int slider, int mostHours) => slider <= 0 || slider >= mostHours ? 0 : slider;

        /// <summary>The game minutes of a whole shift of <paramref name="wholeMinutes"/> that the chosen hours cover.</summary>
        public static int Minutes(int chosen, int wholeMinutes) => chosen > 0 ? Math.Min(Math.Max(0, wholeMinutes), chosen * 60) : Math.Max(0, wholeMinutes);

        /// <summary>
        /// The units of a shift of <paramref name="left"/> units, <paramref name="each"/> game minutes apiece, that the chosen hours
        /// cover: whole units, at least one while any are left, never more than are left.
        /// </summary>
        public static int Units(int chosen, int left, int each)
        {
            if (left <= 0)
            {
                return 0;
            }
            int length = each > 0 ? each : 1;
            return Math.Max(1, Math.Min(left, Minutes(chosen, left * length) / length));
        }

        /// <summary>The pay of some units, whole money: what the shift's slip carries, rounded the way the game's own money is.</summary>
        public static int Pay(float unitPay, int units, float share) => units > 0 ? (int)Math.Round(units * unitPay * share, MidpointRounding.ToEven) : 0;

        /// <summary>The stress points a shift of some game minutes adds, as the hourly upkeep adds them (easy needs halve a rise).</summary>
        public static float Stress(float ratePerHour, int minutes, bool easy) => HomeBalance.Added(ratePerHour, minutes / 60f, easy, ratePerHour > 0f);

        /// <summary>The game's skill a shift trains: a lead shift trains its employer's own, any other job its own; null for none.</summary>
        public static string SkillKey(JobDef def, Employer employer) =>
            def == null ? null : def.Id == JobCatalogue.Lead && employer != null && employer.LeadSkill != null ? employer.LeadSkill : def.Skill;

        /// <summary>The skill points some units train: the job's points a unit.</summary>
        public static float Skill(float unitSkill, int units) => unitSkill > 0f && units > 0 ? unitSkill * units : 0f;

        /// <summary>The hour a shift of <paramref name="totalMinutes"/> has got to after <paramref name="doneMinutes"/>: the first hour is 1.</summary>
        public static int HourOf(int doneMinutes, int totalMinutes)
        {
            int hours = HoursOf(totalMinutes);
            return Math.Min(hours, Math.Max(0, Math.Min(doneMinutes, Math.Max(1, totalMinutes))) / 60 + 1);
        }

        /// <summary>How many hours a shift of some minutes spans, its part hour counted.</summary>
        public static int HoursOf(int totalMinutes) => Math.Max(1, (Math.Max(1, totalMinutes) + 59) / 60);
    }
}
