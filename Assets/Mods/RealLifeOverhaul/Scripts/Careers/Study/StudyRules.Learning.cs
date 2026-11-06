using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What a class and homework teach, homework set and open, and how skills are learnt, used and fade.</summary>
    public static partial class StudyRules
    {
        /// <summary>Thousandths of a point one class adds to a skill at <paramref name="points"/>: the course's gain over its classes, less near the top, by the learning.</summary>
        public static int ClassGain(int gain, int classes, int points, int learning)
        {
            if (gain <= 0 || classes <= 0)
            {
                return 0;
            }
            int per = gain * 1000 / classes;
            int factor = 1000 - (1000 - StudyCatalogue.GainAtTop) * TalentCatalogue.Clamp(points, 0, 100) / 100;
            return per * factor / 1000 * TalentCatalogue.Clamp(learning, 500, 1500) / 1000;
        }

        /// <summary>What a piece of homework teaches: a share of a class, or for a certificate (no classes) its own part of the gain.</summary>
        public static int HomeworkGain(CourseDef c, bool first, int points, int learning)
        {
            if (c == null)
            {
                return 0;
            }
            int gain = first ? c.GainA : c.GainB;
            if (!c.Timetabled)
            {
                return ClassGain(gain, Math.Max(1, c.Homework), points, learning);
            }
            return ClassGain(gain, c.Sessions, points, learning) * StudyCatalogue.HomeworkShare / 1000;
        }

        /// <summary>How well the player learns, thousandths: by the best schooling they hold (the professions' own figures).</summary>
        public static int Learning(int school)
        {
            return TalentCatalogue.SchoolLearning[TalentCatalogue.Clamp(school, 0, TalentCatalogue.Schools - 1)];
        }

        /// <summary>Homework set so far: by the counted classes attended (an excused class sets none and takes none away), up to the course's number (all of a certificate's at once).</summary>
        public static int HomeworkSet(Enrolment e)
        {
            CourseDef c = e.Def;
            if (c == null)
            {
                return 0;
            }
            if (!c.Timetabled)
            {
                return c.Homework;
            }
            int counted = Counted(e);
            return counted <= 0 ? c.Homework : Math.Min(c.Homework, e.Done * c.Homework / counted);
        }

        /// <summary>Game minutes in a piece of homework (or of revision): <see cref="StudyCatalogue.HomeworkHours"/> hours.</summary>
        public const int HomeworkFull = StudyCatalogue.HomeworkHours * 60;

        /// <summary>Game minutes a sitting on the current piece still has to sit: the piece less the minutes already carried on the enrolment.</summary>
        public static int HomeworkLeft(Enrolment e)
        {
            int carried = e == null ? 0 : Math.Max(0, Math.Min(HomeworkFull - 1, e.HomeworkMinutes));
            return HomeworkFull - carried;
        }

        /// <summary>
        /// A homework sitting ends having sat <paramref name="minutes"/> on top of the <paramref name="carried"/> already: true if the piece
        /// is finished (the whole sitting was sat, or the minutes add up to the piece); otherwise <paramref name="kept"/> is what stays on
        /// the enrolment for the next sitting. Counted across sittings, so two hours in two sittings make one piece.
        /// </summary>
        public static bool HomeworkSat(int carried, int minutes, bool whole, out int kept)
        {
            long total = (long)Math.Max(0, carried) + Math.Max(0, minutes);
            if (whole || total >= HomeworkFull)
            {
                kept = 0;
                return true;
            }
            kept = (int)total;
            return false;
        }

        /// <summary>The minutes carried on the piece while a sitting runs: what it began with and what it has sat so far, never a whole piece.</summary>
        public static int HomeworkRunning(int carried, int sat)
        {
            long total = (long)Math.Max(0, carried) + Math.Max(0, sat);
            return (int)Math.Min(HomeworkFull - 1, total);
        }

        /// <summary>True if a piece of homework can be done now: one is set and not done, or the classes are over and revision is still worth something.</summary>
        public static bool HomeworkOpen(Enrolment e)
        {
            CourseDef c = e.Def;
            if (c == null || !e.Live)
            {
                return false;
            }
            if (e.Homework < HomeworkSet(e))
            {
                return true;
            }
            return e.Stage != (int)EnrolStage.Studying && e.Homework < c.Homework + StudyCatalogue.RevisionMost;
        }

        /// <summary>Thousandths added to a skill, to 100 points at most; the best ever is kept and the day it was used.</summary>
        public static void Learn(StudyFile f, int skill, int thousandths, int day)
        {
            if (skill < 0 || skill >= Skills.Count || thousandths <= 0)
            {
                return;
            }
            f.Skill[skill] = Math.Min(100000, f.Skill[skill] + thousandths);
            f.Best[skill] = Math.Max(f.Best[skill], f.Skill[skill]);
            f.Used[skill] = day;
        }

        /// <summary>A skill used at work or in a class without being taught: it stops fading for a while.</summary>
        public static void Use(StudyFile f, int skill, int day)
        {
            if (skill >= 0 && skill < Skills.Count)
            {
                f.Used[skill] = day;
            }
        }

        /// <summary>A day gone by: a skill unused for over <see cref="StudyCatalogue.FadeAfterDays"/> days fades, within its floors; returns the thousandths lost.</summary>
        public static int Fade(StudyFile f, int day)
        {
            int lost = 0;
            for (int s = 0; s < Skills.Count; s++)
            {
                int now = f.Skill[s];
                if (now <= StudyCatalogue.FadeLeast * 1000 || day - f.Used[s] <= StudyCatalogue.FadeAfterDays)
                {
                    continue;
                }
                int floor = Math.Max(StudyCatalogue.FadeLeast * 1000, (int)((long)f.Best[s] * StudyCatalogue.FadeFloor / 1000));
                int next = Math.Max(floor, now - StudyCatalogue.FadePerDay);
                lost += now - next;
                f.Skill[s] = next;
            }
            return lost;
        }

        /// <summary>A skill in whole points.</summary>
        public static int Points(StudyFile f, int skill) => skill >= 0 && skill < Skills.Count ? f.Skill[skill] / 1000 : 0;

        /// <summary>The week's minutes of study, the week starting again each seventh day. True if the week has just passed the stressed mark.</summary>
        public static bool Studied(StudyFile f, int day, int minutes)
        {
            int week = (day - 1) / 7;
            if (week != f.WeekOf)
            {
                f.WeekOf = week;
                f.WeekMinutes = 0;
            }
            int before = f.WeekMinutes;
            f.WeekMinutes += Math.Max(0, minutes);
            int mark = StudyCatalogue.StressedWeekHours * 60;
            return before < mark && f.WeekMinutes >= mark;
        }
    }
}
