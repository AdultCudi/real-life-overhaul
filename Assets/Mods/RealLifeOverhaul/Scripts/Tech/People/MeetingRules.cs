using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The weekly team meeting at a tech firm's desk (the desk sessions' first kind, Wiring/Wiring.SessionLinks): lift the team's
    /// morale, or push the week's output at the cost of burnout, which the people part's own rules then bite with
    /// (TalentRules.Output, MoraleTarget, SickChance, BurnsOut). A short meeting does less than a full one; a manager does
    /// less than the owner, more with skill. Game-free: EconomyCheck's SessionChecks hold it.
    /// </summary>
    public static class MeetingRules
    {
        public const string Id = "tech_team_meeting";   // the session's id, saved with its records

        public const int Morale = 0, Push = 1;          // the first step: the week's aim
        public const int Short = 0, Full = 1;           // the second: how long

        public const int MoraleLift = 90;               // thousandths of morale each professional gains from a meeting that lifts it, at full strength
        public const int BurnoutEase = 60;              // ...and burnout it eases
        public const int MoraleWeekOutput = -30;        // thousandths of the week's output a morale week gives up to the time it takes
        public const int PushOutput = 120;              // thousandths more output a pushed week brings at full strength...
        public const int PushBurnout = 12;              // ...and thousandths of burnout each day worked of that week adds, beyond the hours' own
        public const int ShortStrength = 650;           // thousandths of a full meeting's strength a short one has
        public const int ShortMinutes = 30, FullMinutes = 60;   // game minutes
        public const int WeekDays = 7;                  // days a meeting's week lasts
        public const int BurnoutHigh = 450;             // a team's burnout average from which a manager lifts morale rather than push

        /// <summary>A meeting's strength, thousandths: its length's, times the holder's share (the session's own rule for a manager).</summary>
        public static int Strength(int length, int holderStrength)
        {
            int l = length == Short ? ShortStrength : 1000;
            return l * Math.Max(0, Math.Min(1000, holderStrength)) / 1000;
        }

        /// <summary>What a meeting does: morale and burnout eased at once for each professional, then the week's extra output and burnout a day worked.</summary>
        public static void Effect(int aim, int strength, out int morale, out int ease, out int output, out int burnout)
        {
            int s = Math.Max(0, Math.Min(1000, strength));
            if (aim == Push)
            {
                morale = ease = 0;
                output = PushOutput * s / 1000;
                burnout = Math.Max(1, PushBurnout * s / 1000);
                return;
            }
            morale = MoraleLift * s / 1000;
            ease = BurnoutEase * s / 1000;
            output = MoraleWeekOutput;   // the time is spent whoever holds it
            burnout = 0;
        }

        /// <summary>A professional at the meeting: morale up and burnout down by the meeting's at once, within the people part's bounds.</summary>
        public static void Rally(Profile p, int morale, int ease)
        {
            p.Morale = TalentCatalogue.Clamp(p.Morale + Math.Max(0, morale), 0, 1000);
            p.Burnout = TalentCatalogue.Clamp(p.Burnout - Math.Max(0, ease), 0, 1000);
        }

        /// <summary>A day of a pushed week: burnout a day worked adds, nothing on a day not worked.</summary>
        public static int Day(int burnout, int hours, int pushBurnout)
        {
            return hours > 0 && pushBurnout > 0 ? TalentCatalogue.Clamp(burnout + pushBurnout, 0, 1000) : burnout;
        }

        /// <summary>A day's work points with the week's output (thousandths, signed) on them; never below nothing.</summary>
        public static int Points(int points, int output)
        {
            return output == 0 ? points : Math.Max(0, (int)((long)points * (1000 + output) / 1000));
        }

        /// <summary>The aim a manager picks for a team: morale for a tired team (its burnout average at <see cref="BurnoutHigh"/> or more), else output.</summary>
        public static int Pick(int burnoutAverage)
        {
            return burnoutAverage >= BurnoutHigh ? Morale : Push;
        }
    }
}
