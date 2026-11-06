using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// A post's working week kept inside its workplace's opening hours (Work/OpenHoursRules): fitted as a vacancy is made, again
    /// each morning and at load for the job, the applications and the vacancies (a save from before it, or a place that changed
    /// its hours), and a working day whose place was shut at its start excused, never a warning. Before, a profession's
    /// catalogue hour was worked wherever it was: the warehouse's 06:00 at a wholesaler that opens at 08:00 was missed day
    /// after day behind a shut door and the player was sacked. Costs a schedule read per post once a game day and one at
    /// the start hour of a working day; nothing per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private readonly int[] _week = new int[7];   // the opening hours read last, Monday first (OpenHoursRules)

        // The post's week fitted to its place's hours as they are now: false if no day there is open long enough for one.
        private bool FitShift(Opening o)
        {
            Profession p = ProCatalogue.Find(o.Prof);
            if (p == null)
            {
                return false;
            }
            int fit = Fitted(p, o);
            if (fit == 0)
            {
                return false;
            }
            o.Shift = fit;
            return true;
        }

        // The week a post of the profession is worked on there: its own where the door keeps no hours or the work is all from home.
        // A start moved by a shift request granted on the management ladder (prefer, hours) is fitted the same way, from the hour asked for.
        private int Fitted(Profession p, Opening o, int prefer = 0)
        {
            int hour = Tech.TalentCatalogue.Clamp(p.Hour + prefer, 0, System.Math.Max(0, 24 - p.Hours));
            if (o.Work == (int)WorkMode.Remote || !WorkGame.OpenWeek(o.Street, o.Number, _week))
            {
                return OpenHoursRules.Pack(p.Days, hour, p.Hours);
            }
            return OpenHoursRules.Fit(p.Days, hour, p.Hours, _week);
        }

        /// <summary>Every post's week against its place's hours now: a vacancy with no working day to be had there goes, the job's moves (and says so).</summary>
        private void KeepShifts(int day, bool load)
        {
            WorkFile w = _study.Work;
            for (int i = w.Openings.Count - 1; i >= 0; i--)
            {
                if (!FitShift(w.Openings[i]))
                {
                    w.Openings.RemoveAt(i);   // (one taken from the board before its place's hours were read)
                }
            }
            for (int i = 0; i < w.Applications.Count; i++)
            {
                FitShift(w.Applications[i].Post);   // one with no working day keeps its week: its days are excused if it becomes the job
            }
            if (w.Job != null)
            {
                KeepJobShift(w.Job, day, load);
            }
        }

        /// <summary>
        /// The job's week fitted again. At load, a week that began while the place was shut (a save from before the fit) has
        /// its warnings and missed days taken back: they were the shut door's, not the player's.
        /// </summary>
        private void KeepJobShift(Job j, int day, bool load)
        {
            Profession own = ProCatalogue.Find(j.Post.Prof);
            if (own == null)
            {
                return;
            }
            int ownWeek = OpenHoursRules.Pack(own.Days, own.Hour, own.Hours);
            int was = j.Post.Shift != 0 ? j.Post.Shift : ownWeek;
            int fit = Fitted(own, j.Post, LadderPrefer(j));
            bool known = j.Post.Work != (int)WorkMode.Remote && OpenHoursRules.Known(_week);
            if (fit == 0)
            {
                if (Verbose)
                {
                    Logger?.Info("Careers check (working hours against opening hours): " + j.Post.Employer + " is open less than " + OpenHoursRules.LeastHours
                                 + " hours in a row on every day; the week is kept and a day it is shut at the start is excused: PASS");
                }
                return;
            }
            bool shutBefore = known && OpenHoursRules.AnyShut(was, _week);
            j.Post.Shift = fit;
            if (fit == was)
            {
                return;
            }
            bool righted = load && shutBefore && (j.Warnings > 0 || j.Absent > 0);
            if (righted)
            {
                j.Due = System.Math.Max(0, j.Due - j.Absent);
                j.Absent = 0;
                j.Warnings = 0;
            }
            string hours = CareersText.Hours(j.Def);
            Tell2(NotificationType.Info, righted ? "reallife_work_note_hours_righted" : "reallife_work_note_hours_moved",
                Data("employer", CareersText.Employer(j.Post.Employer), "hours", hours), TabJob, null);
            Logger?.Info("Careers: the working week at " + j.Post.Employer + " moved to fit its opening hours on day " + day + ": " + Week(was) + " to " + Week(fit)
                         + (righted ? "; the warnings and missed days of the old week taken back" : string.Empty) + ".");
            if (Verbose)
            {
                bool shut = known && OpenHoursRules.AnyShut(fit, _week);
                Logger?.Info("Careers check (working hours against opening hours): " + j.Post.Employer + ", " + Week(fit) + " against its hours: "
                             + (shut ? "FAIL, a working day starts or runs while it is shut" : "PASS"));
            }
            Changed();
        }

        private static string Week(int shift)
        {
            int hour = OpenHoursRules.HourOf(shift);
            return "days " + OpenHoursRules.DaysOf(shift) + " " + hour.ToString("00") + ":00 for " + OpenHoursRules.HoursOf(shift) + " h";
        }

        /// <summary>At a working day's start hour: a place the game has shut now (a temporary closing, its hours changed) excuses the day, said once.</summary>
        private void ShutAtStart(Job j, int day, int hour)
        {
            Profession p = j.Def;
            if (!ProRules.WorkDay(p, day) || hour != p.Hour || day < j.Hired || j.SettledDay >= day || j.ShutDay == day
                || (j.TodayDay == day && j.TodayMinutes > 0) || ProRules.FromHome((WorkMode)j.Work, day) || !WorkGame.ShutNow(j.Post.Street, j.Post.Number))
            {
                return;
            }
            j.ShutDay = day;
            Tell2(NotificationType.Info, "reallife_work_note_shut", Data("employer", CareersText.Employer(j.Post.Employer), "day", CareersText.Day(day)), TabJob, null);
            Logger?.Info("Careers: " + j.Post.Employer + " is shut at the start of the working day " + day + "; the day is excused.");
            Changed();
        }

        /// <summary>True if a working day not worked is excused because its place was shut at its start: seen then, or its hours say so now.</summary>
        private bool ShutExcused(Job j, int day)
        {
            if (ProRules.FromHome((WorkMode)j.Work, day))
            {
                return false;
            }
            if (j.ShutDay == day)
            {
                return true;
            }
            Profession p = j.Def;
            return WorkGame.OpenWeek(j.Post.Street, j.Post.Number, _week)
                   && OpenHoursRules.StartsShut(OpenHoursRules.Pack(p.Days, p.Hour, p.Hours), _week, OpenHoursRules.Weekday(day));
        }
    }
}
