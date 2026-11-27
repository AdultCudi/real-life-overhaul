using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// What the physical jobs ask of the player's body (Careers/Work/FitnessJobRules): a refusal that names the fitness needed, the player's and how to get there, asked wherever a
    /// post is applied for or an offer taken (with the police's own asks, Careers/Police), and a warning for a worker far below what the job asks, which never loses them the job
    /// by itself. The figure comes from Home through <see cref="CareersBridges.Fitness"/>. Cost: nothing per frame; a few comparisons at an application and once a day.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private static readonly CultureInfo FitInv = CultureInfo.InvariantCulture;

        /// <summary>
        /// The reason fitness keeps the player from a post, with its figures, or null: the option off, a profession that asks nothing, a fitness that is not kept, or one that
        /// is enough. The police's is the academy's fitness test.
        /// </summary>
        private string FitnessWhy(Profession p)
        {
            if (p == null || !Settings.CareersFitness)
            {
                return null;
            }
            int have = CareersBridges.FitnessNow();
            if (!FitnessJobRules.Short(p.Id, have, true, out int need))
            {
                return null;
            }
            int gap = need - have;
            int day = Save != null ? Save.Day : 0;
            string tip = CareersText.Fill(FitnessJobRules.TipKey(p.Id, have, gap, day), "weeks", FitnessJobRules.Weeks(gap).ToString(FitInv), "gap", gap.ToString(FitInv));
            return CareersWhy.With(p.Id == ProCatalogue.Police ? "reallife_fitness_why_police" : "reallife_fitness_why_job",
                "need", need.ToString(FitInv), "have", have.ToString(FitInv), "job", CareersText.ProName(p.Id), "tip", tip);
        }

        /// <summary>Options > Tests: runs today's fitness look at the player's job now (a warning, a strike or the lifting of one, as the day's turn would), and says what the figures were.</summary>
        public void TestFitnessJob()
        {
            if (!WorkReady())
            {
                return;
            }
            try
            {
                Job j = Work != null ? Work.Job : null;
                if (j == null || j.Def == null || !FitnessJobRules.Physical(j.Def.Id))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_fitness_test_no_job", null, 8f);
                    return;
                }
                int before = j.Warnings;
                FitnessDay(j, Save.Day);
                Notifications.Show(NotificationType.Info, "reallife_fitness_test_job", Data("have", CareersBridges.FitnessNow().ToString(FitInv), "need",
                    FitnessJobRules.Minimum(j.Def.Id).ToString(FitInv), "far", FitnessJobRules.FarBelow.ToString(FitInv)), 10f);
                Logger?.Info("Careers check (fitness for the job): the day's look ran on " + ProCatalogue.Find(j.Def.Id).Key + "; warnings " + before + " then " + j.Warnings + ": PASS");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// A day's look at the player's fitness against the job's (FitnessJobRules.Judge): far below, a warning with fourteen days to put it right; still far below after that, a
        /// strike on the ladder of warnings, up to the last warning and never past it; back in reach, the warning lifted. Said by the boss in a text, or a notification where it
        /// cannot be sent. Never the sack.
        /// </summary>
        private void FitnessDay(Job j, int day)
        {
            if (j == null || j.Def == null || !Settings.CareersFitness || day < j.Hired)
            {
                return;
            }
            int have = CareersBridges.FitnessNow();
            FitnessCheck check = FitnessJobRules.Judge(j.Def.Id, have, true, j.FitWarnDay, j.FitStrikeDay, j.Warnings, WarningsToGo, day);
            if (check == FitnessCheck.None)
            {
                return;
            }
            int need = FitnessJobRules.Minimum(j.Def.Id);
            var data = new Dictionary<string, string>(6)
            {
                { "employer", CareersText.Employer(j.Post.Employer) }, { "need", need.ToString(FitInv) }, { "have", have.ToString(FitInv) },
                { "days", FitnessJobRules.WarnDays.ToString(FitInv) }, { "weeks", FitnessJobRules.Weeks(Math.Max(1, need - have)).ToString(FitInv) },
                { "count", (j.Warnings + (check == FitnessCheck.Strike ? 1 : 0)).ToString(FitInv) }
            };
            data["most"] = WarningsToGo.ToString(FitInv);
            int pick = FitnessJobRules.Variant(j.Def.Id, have, day, FitnessJobRules.WarnTexts.Length);
            switch (check)
            {
                case FitnessCheck.Warn:
                    j.FitWarnDay = day;
                    j.FitStrikeDay = 0;
                    BossSays(j, FitnessJobRules.WarnTexts[pick], data, NotificationType.Warning, "reallife_fitness_note_warning", TabJob);
                    break;
                case FitnessCheck.Strike:
                    j.FitStrikeDay = day;
                    j.Warnings++;
                    BossSays(j, FitnessJobRules.StrikeTexts[pick], data, NotificationType.Warning, "reallife_fitness_note_strike", TabJob);
                    break;
                case FitnessCheck.Clear:
                    j.FitWarnDay = 0;
                    j.FitStrikeDay = 0;
                    Tell2(NotificationType.Success, "reallife_fitness_note_clear", data, TabJob, null);
                    break;
            }
            Logger?.Info("Careers: fitness " + have + " against the job's " + need + " on day " + day + ": " + check
                         + (check == FitnessCheck.Strike ? ", warnings " + j.Warnings + " of " + WarningsToGo : string.Empty) + ".");
            Changed();
        }
    }
}
