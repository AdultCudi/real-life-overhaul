using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks of a shift carried across a save (Jobs/JobSave, Jobs/PaySlip, CareerCatalogue.PaidHours): the line
    /// written is the line read; a line that does not add up is refused; and what a load pays is exactly what the live
    /// rules pay a shift that ends at that moment through nobody's fault, for the same figures. The live rules are
    /// written out here a second time, on their own, so that a change to one side shows up as a failure.
    /// </summary>
    internal static class SavedShiftCheck
    {
        public static void Run(Action<string> fail, Dictionary<string, string> locale, string source)
        {
            Console.WriteLine("\n== Jobs: a shift across a save ==");
            var random = new Random(20260917);
            int lines = 0, settled = 0, refused = 0;

            // ---- every gig and every rank of every employer, at many moments of a shift
            var shifts = new List<SavedShift>();
            foreach (JobDef def in JobCatalogue.All)
            {
                for (int i = 0; i < 40; i++)
                {
                    shifts.Add(Shift(random, def.Id, null, 0f));
                }
            }
            foreach (Employer employer in CareerCatalogue.All)
            {
                foreach (CareerRank rank in employer.Ranks)
                {
                    string job = rank.Work == CareerWork.Dispatch ? JobCatalogue.Dispatch : rank.Work == CareerWork.Lead ? JobCatalogue.Lead : employer.Job;
                    for (int i = 0; i < 40; i++)
                    {
                        // (as the director pays it: the rank's wage times the pay option and the owner's factor)
                        float hourly = rank.Hourly * (0.25f + (float)random.NextDouble() * 2.75f);
                        shifts.Add(Shift(random, job, employer.Id, hourly));
                    }
                }
            }

            foreach (SavedShift shift in shifts)
            {
                string line = JobSave.Write(shift);
                lines++;
                if (line.Length == 0)
                {
                    fail($"{shift.JobId}: nothing was written");
                    continue;
                }
                if (!JobSave.Read(line, out SavedShift read))
                {
                    fail($"{shift.JobId}: the line written was not read back: {line}");
                    continue;
                }
                if (!Same(shift, read))
                {
                    fail($"{shift.JobId}: the line read differs from the shift written: {line}");
                    continue;
                }
                if (JobSave.Write(read) != line)
                {
                    fail($"{shift.JobId}: writing what was read gives another line");
                }
                if (JobCatalogue.Find(read.JobId) == null)
                {
                    fail($"{read.JobId}: the catalogue does not know the job of a saved shift");
                }
                if (read.Rota && CareerCatalogue.Find(read.Employer) == null)
                {
                    fail($"{read.Employer}: the catalogue does not know the employer of a saved shift");
                }

                // ---- the settlement against the live rules, written out again
                ShiftSettlement sum = JobSave.Settle(read);
                settled++;
                int work = Math.Max(0, shift.Owed.Base + shift.Owed.Distance + shift.Owed.Tips + shift.Owed.Bonus - shift.Owed.Penalty);   // Director.Pay: a slip's total
                int units = shift.Paid.Units + shift.Owed.Units;
                int hours = 0;
                if (shift.Rota && units > 0)   // CareerDirector.ShiftEnded: only a shift with work done has hours
                {
                    float elapsed = shift.Minutes - shift.StartMinutes;
                    hours = Math.Min(CareerCatalogue.ShiftHours, Math.Max(1, (int)Math.Ceiling(elapsed / 60f))) - (shift.Late ? 1 : 0);
                }
                int wage = hours > 0 ? (int)Math.Round(hours * shift.Hourly) : 0;
                if (sum.Work != work) fail($"{shift.JobId}: the work settles at {sum.Work}, the live slip pays {work}");
                if (sum.Hours != hours) fail($"{shift.JobId}: {sum.Hours} hours settled, the live rule gives {hours}");
                if (sum.Wage != wage) fail($"{shift.JobId}: the wage settles at {sum.Wage}, the live rule pays {wage}");
                if (sum.Total != work + wage) fail($"{shift.JobId}: the total {sum.Total} is not the work and the wage");
                if (sum.UnitsOwed != shift.Owed.Units) fail($"{shift.JobId}: {sum.UnitsOwed} units counted, {shift.Owed.Units} were owed");
                if (sum.Worked != (units > 0)) fail($"{shift.JobId}: 'worked' is wrong for {units} units");
                if (sum.Total < 0 || sum.Work < 0 || sum.Wage < 0) fail($"{shift.JobId}: a settlement below zero");
                if (!shift.Rota && sum.Wage != 0) fail($"{shift.JobId}: a gig was paid a wage");
                if (sum.Hours > CareerCatalogue.ShiftHours) fail($"{shift.JobId}: more hours than a shift has");

                // ---- nothing paid before the save is paid again: the same shift with nothing paid before it settles at the same sum
                var bare = JobSave.Read(line, out SavedShift again) ? again : null;
                if (bare != null)
                {
                    int paidUnits = bare.Paid.Units;
                    bare.Paid = new PaySlip { Units = paidUnits };
                    ShiftSettlement other = JobSave.Settle(bare);
                    if (other.Total != sum.Total) fail($"{shift.JobId}: what had been paid before the save changed what the load pays ({sum.Total} against {other.Total})");
                }
            }

            // ---- an hour by hour table of the wage rule, as CareerDirector had it before the rule was shared
            for (int minutes = -30; minutes <= 600; minutes += 7)
            {
                for (int late = 0; late < 2; late++)
                {
                    int live = Math.Min(CareerCatalogue.ShiftHours, Math.Max(1, (int)Math.Ceiling(minutes / 60f))) - late;
                    if (CareerCatalogue.PaidHours(minutes, late == 1) != live)
                    {
                        fail($"PaidHours({minutes}, late {late}) is {CareerCatalogue.PaidHours(minutes, late == 1)}, the live rule gives {live}");
                    }
                }
            }

            // ---- two slips paid apart are each bounded at nothing: one slip made of both pays the same
            for (int i = 0; i < 4000; i++)
            {
                var hand = new PaySlip { Units = random.Next(0, 9), Base = random.Next(0, 300), Tips = random.Next(0, 60), Bonus = random.Next(0, 40), Penalty = random.Next(0, 700) };
                var clock = new PaySlip { Units = random.Next(0, 9), Base = random.Next(0, 300) };
                PaySlip one = hand.Bounded();
                one.Add(clock);
                if (one.Total != hand.Total + clock.Total)
                {
                    fail($"a bounded slip and the clock's come to {one.Total}, paid apart they come to {hand.Total + clock.Total}");
                    break;
                }
                if (hand.Bounded().Total != hand.Total)
                {
                    fail("bounding a slip changed what it pays");
                    break;
                }
            }

            // ---- lines that do not add up are refused, and nothing is settled for them
            string good = JobSave.Write(Shift(random, JobCatalogue.All[0].Id, CareerCatalogue.All[0].Id, 30f));
            string[] parts = good.Split('|');
            var bad = new List<string>
            {
                null, string.Empty, "1", "2" + good.Substring(1), good.Substring(0, good.LastIndexOf('|')), "1||3|100|0,0,0,0,0,0|0,0,0,0,0,0|0||0|0|0",
                Replace(parts, 5, "-1,10,0,0,0,0"), Replace(parts, 5, "1,-10,0,0,0,0"), Replace(parts, 5, "1,10,0,0,0"), Replace(parts, 5, "1,x,0,0,0,0"),
                Replace(parts, 3, "NaN"), Replace(parts, 8, "-5"), Replace(parts, 8, "Infinity"), Replace(parts, 7, string.Empty), Replace(parts, 2, "three")
            };
            foreach (string line in bad)
            {
                if (JobSave.Read(line, out SavedShift read) || read != null)
                {
                    fail("a line that does not add up was read: " + (line ?? "(null)"));
                }
                else
                {
                    refused++;
                }
            }
            if (JobSave.Settle(null).Total != 0) fail("settling nothing pays something");
            if (JobSave.Write(null).Length != 0 || JobSave.Write(new SavedShift()).Length != 0) fail("a shift with no job writes a line");

            // ---- the worst a line can do: a penalty larger than everything else pays nothing, never less
            var sunk = new SavedShift { JobId = JobCatalogue.All[0].Id, Day = 3, Minutes = 500f, Owed = new PaySlip { Units = 2, Base = 40, Penalty = 4000 } };
            if (JobSave.Settle(sunk).Total != 0) fail("a slip sunk by penalties settles at something other than nothing");

            // ---- the save's key is a registered one, and the three sentences are there with their blanks
            string features = System.IO.File.ReadAllText(System.IO.Path.Combine(source, "Scripts", "Meta", "FeatureList.cs"));
            if (!features.Contains("\"" + JobSave.Key + "\"")) fail(JobSave.Key + " is not registered in Meta/FeatureList.cs (.Data)");
            Need(fail, locale, "reallife_job_saved_paid", "{job}", "{amount}", "{units}");
            Need(fail, locale, "reallife_job_saved_nothing", "{job}");
            Need(fail, locale, "reallife_job_saved_rota", "{employer}", "{amount}", "{hours}");

            Console.WriteLine($"  {lines} lines written and read back, {settled} settled against the live rules, {refused} bad lines refused");
        }

        private static void Need(Action<string> fail, Dictionary<string, string> locale, string key, params string[] blanks)
        {
            if (!locale.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
            {
                fail(key + " is missing from en.json");
                return;
            }
            foreach (string blank in blanks)
            {
                if (!text.Contains(blank)) fail(key + " has no " + blank);
            }
        }

        private static string Replace(string[] parts, int index, string with)
        {
            var copy = (string[])parts.Clone();
            copy[index] = with;
            return string.Join("|", copy);
        }

        // A shift at some moment: a little or a lot paid already, a little or nothing owed, now and then sunk by a penalty.
        private static SavedShift Shift(Random random, string job, string employer, float hourly)
        {
            float start = 420f + random.Next(0, 5000) + (float)random.NextDouble();
            var shift = new SavedShift
            {
                JobId = job,
                Day = 1 + random.Next(0, 400),
                Paid = new PaySlip { Units = random.Next(0, 20), Base = random.Next(0, 900), Distance = random.Next(0, 200), Tips = random.Next(0, 120), Bonus = random.Next(0, 90) },
                Owed = new PaySlip
                {
                    Units = random.Next(0, 4) == 0 ? 0 : random.Next(1, 30), Base = random.Next(0, 700), Distance = random.Next(0, 150), Tips = random.Next(0, 90),
                    Bonus = random.Next(0, 60), Penalty = random.Next(0, 5) == 0 ? random.Next(0, 1500) : 0
                },
                Rota = employer != null,
                Employer = employer ?? string.Empty,
                Hourly = hourly,
                Late = employer != null && random.Next(0, 3) == 0,
                StartMinutes = employer != null ? start : 0f
            };
            if (random.Next(0, 6) == 0)
            {
                shift.Paid = default;
                shift.Owed.Units = random.Next(0, 2) == 0 ? 0 : shift.Owed.Units;   // nothing done at all, now and then
            }
            shift.Minutes = start + random.Next(0, 420) + (float)random.NextDouble();
            return shift;
        }

        private static bool Same(SavedShift a, SavedShift b)
        {
            return a.JobId == b.JobId && a.Day == b.Day && a.Minutes.Equals(b.Minutes) && Same(a.Paid, b.Paid) && Same(a.Owed, b.Owed) && a.Rota == b.Rota
                   && a.Employer == b.Employer && a.Hourly.Equals(b.Hourly) && a.Late == b.Late && a.StartMinutes.Equals(b.StartMinutes);
        }

        private static bool Same(in PaySlip a, in PaySlip b)
        {
            return a.Units == b.Units && a.Base == b.Base && a.Distance == b.Distance && a.Tips == b.Tips && a.Bonus == b.Bonus && a.Penalty == b.Penalty;
        }
    }
}
