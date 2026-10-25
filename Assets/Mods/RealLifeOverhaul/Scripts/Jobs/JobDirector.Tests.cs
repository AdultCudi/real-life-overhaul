using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using NWH.VehiclePhysics2.Damage;
using PlayerActivity;
using UI;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>Jobs' director: the options page's test buttons.</summary>
    public partial class JobDirector
    {
        /// <summary>Options-page test button: starts a job where the player stands, whatever their level, record or day.</summary>
        public void TestStart(string id)
        {
            JobDef def = JobCatalogue.Find(id);
            if (def == null || _broken || !JobState.Loaded)
            {
                return;
            }
            if (_job != null)
            {
                EndJob(JobEnd.Interrupted, null);
            }
            if (!Free(out string reason, def) || !StartJob(def, true, out reason))
            {
                Logger?.Info("Jobs: test of '" + id + "' could not start (" + reason + ").");
                JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now");
            }
        }

        /// <summary>
        /// Options-page test button: a courier round as a heavy load where the player stands, in their own heavy truck if one is
        /// near or a hired one free of charge, whatever their licence. Says why when it cannot start.
        /// </summary>
        public void TestHeavyLoad()
        {
            JobDef def = JobCatalogue.Find(JobCatalogue.Courier);
            if (def == null || _broken || !JobState.Loaded)
            {
                TestRefusals.Tell(Logger, "Heavy load", TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.HeavyLicence || !Settings.Jobs)
            {
                TestRefusals.Tell(Logger, "Heavy load", TestRefusal.FeatureOff, 0, !Settings.Jobs ? "jobs are off" : "the heavy goods licence is off");
                return;
            }
            if (_job != null)
            {
                EndJob(JobEnd.Interrupted, null);
            }
            if (!Free(out string reason) || !StartJob(def, true, out reason, heavy: true))
            {
                Logger?.Info("Jobs: test of a heavy load could not start (" + reason + ").");
                JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now");
            }
        }

        private int _testEmployer = -1;

        /// <summary>Options page: signs on with the next employer in turn (leaving the current one with no mark), on the first rota that works tomorrow.</summary>
        public void TestSignOn()
        {
            if (!CareerTestReady())
            {
                return;
            }
            _testEmployer = (_testEmployer + 1) % CareerCatalogue.All.Length;
            CareerRota rota = CareerCatalogue.FirstRotaOn(TimeHelper.CurrentDay + 1);   // the first shift is tomorrow
            _career.SignOn(_testEmployer, rota, true);
        }

        /// <summary>Options page: the review is due now; it is held with the boss (or by phone where the employer has no office).</summary>
        public void TestReview()
        {
            if (!CareerTestReady())
            {
                return;
            }
            if (!CareerState.Employed)
            {
                TestRefusals.Tell(Logger, "Career review", TestRefusal.NothingFits, 0, "not employed: sign on first");
                JobText.Toast(NotificationType.Warning, "reallife_career_no_employer", 8f);
                return;
            }
            CareerState.ForceReview();
            Employer employer = CareerState.Employer;
            var data = new Dictionary<string, string>(3) { { "boss", JobText.T(employer.BossKey) }, { "employer", JobText.T(employer.NameKey) } };
            Notifications.Show(NotificationType.Info, _career.OfficeKey("reallife_career_review_due", CareerState.EmployerIndex, data), data, 10f);
            if (!BuildingManager.IsInsideBuilding && !UiOpen() && Free(out _))
            {
                CareerPhone.OpenInPerson(CareerState.EmployerIndex);   // the tester need not walk to the office
            }
        }

        /// <summary>Options page: up one rank at the current employer.</summary>
        public void TestPromote()
        {
            if (!CareerTestReady())
            {
                return;
            }
            if (!CareerState.Employed || !CareerState.Promote())
            {
                TestRefusals.Tell(Logger, "Career promotion", TestRefusal.NothingFits, 0, CareerState.Employed ? "already at the top rank" : "not employed");
                JobText.Toast(NotificationType.Warning, CareerState.Employed ? "reallife_career_test_top" : "reallife_career_no_employer", 8f);
                return;
            }
            JobText.Toast(NotificationType.Success, "reallife_career_test_promoted", "title", JobText.T(CareerState.CurrentRank.TitleKey), 9f);
        }

        /// <summary>Options page: a shift of a kind of work here, on no record: the courier firm's desk, a lead shift, or the current rank's shift.</summary>
        public void TestShift(CareerWork work, bool current)
        {
            if (!CareerTestReady())
            {
                return;
            }
            Employer employer;
            CareerRank rank;
            if (current)
            {
                if (!CareerState.Employed)
                {
                    TestRefusals.Tell(Logger, "Career shift", TestRefusal.NothingFits, 0, "not employed: sign on first");
                    JobText.Toast(NotificationType.Warning, "reallife_career_no_employer", 8f);
                    return;
                }
                employer = CareerState.Employer;
                rank = CareerState.CurrentRank;
            }
            else
            {
                employer = CareerCatalogue.Find(work == CareerWork.Dispatch ? CareerCatalogue.Courier : CareerCatalogue.Retail);
                if (CareerState.Employed && CareerState.CurrentRank.Work == work)
                {
                    employer = CareerState.Employer;
                }
                rank = null;
                for (int i = 0; i < employer.Ranks.Length && rank == null; i++)
                {
                    rank = employer.Ranks[i].Work == work ? employer.Ranks[i] : null;
                }
            }
            string reason = null;
            if (rank == null || !_career.TestShift(employer, rank, out reason))
            {
                Logger?.Info("Careers: test shift could not start (" + reason + ").");
                JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now", 8f);
            }
        }

        /// <summary>Options page: the agency books a set at a venue close by.</summary>
        public void TestGig()
        {
            if (!CareerTestReady())
            {
                return;
            }
            if (_career.GigPending || !_career.BookGig(true))
            {
                TestRefusals.Tell(Logger, "Booked set", TestRefusal.NothingFits, 0, _career.GigPending ? "a set is already booked" : "no open business with a clear front within 160 m");
                return;
            }
            Notifications.Show(NotificationType.Info, "reallife_career_gig_booked_toast",
                new Dictionary<string, string>(2) { { "venue", _career.GigVenue }, { "fee", JobText.Money(_career.GigFee) } }, 10f);
        }

        /// <summary>
        /// Options page: the ride-hail passengers' table in effect (the "easier passengers" option, or the fare being driven's
        /// own if one is), both tables in Player.log, and whether the easier one is softer on braking, bumps and speeding and
        /// the same on everything else. Changes nothing.
        /// </summary>
        public void TestTaxiTable()
        {
            const string Test = "Ride fare passengers";
            if (_broken || !JobState.Loaded)
            {
                TestRefusals.Tell(Logger, Test, TestRefusal.NotLoaded);
                return;
            }
            TaxiComfortTable wanted = TaxiComfortRules.For(Settings.JobTaxiEasy);
            TaxiComfortTable riding = _job is TaxiJob taxi ? taxi.Table : null;
            TaxiComfortTable shown = riding ?? wanted;
            TaxiComfortTable n = TaxiComfortRules.Normal, e = TaxiComfortRules.Easier;
            bool softer = e.JoltCost < n.JoltCost && e.JoltGap > n.JoltGap && e.HarshScale > n.HarshScale && e.BumpOver > n.BumpOver
                          && e.SpeedingAbove >= n.SpeedingAbove && e.SpeedingCost < n.SpeedingCost;
            bool same = e.CrashHard == n.CrashHard && e.CrashSoft == n.CrashSoft && e.PoliceCost == n.PoliceCost && e.LowComfort == n.LowComfort && e.LowCut == n.LowCut;
            bool picked = TaxiComfortRules.For(false) == n && TaxiComfortRules.For(true) == e;
            string why = (softer ? string.Empty : " the easier table is not softer on braking, bumps and speeding;") + (same ? string.Empty : " the easier table changes crashes, police or the slip;")
                         + (picked ? string.Empty : " the option picks the wrong table;");
            Logger?.Warn("Jobs check (ride fare passengers): option " + (Settings.JobTaxiEasy ? "easier" : "as always")
                         + (riding != null ? ", the fare being driven " + (riding == e ? "easier" : "as always") + " (a switch counts from the next fare)" : ", no fare being driven")
                         + "; as always: " + TaxiComfortRules.Describe(n) + "; easier: " + TaxiComfortRules.Describe(e) + ": "
                         + (why.Length == 0 ? "PASS" : "FAIL (" + why.Trim() + ")") + ".");   // asked for by a button: written whether or not diagnostics are on
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Notifications.Show(NotificationType.Info, shown == e ? "reallife_job_taxi_table_easier" : "reallife_job_taxi_table_usual",
                new Dictionary<string, string>(3)
                {
                    { "jolt", shown.JoltCost.ToString("0.#", inv) }, { "gap", shown.JoltGap.ToString("0.#", inv) }, { "speed", shown.SpeedingCost.ToString("0.#", inv) }
                }, 10f);
        }

        private bool CareerTestReady()
        {
            TestRefusal why = _broken || !JobState.Loaded || !CareerState.Loaded ? TestRefusal.NotLoaded
                : !CareerDirector.On ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Career", why);
                return false;
            }
            return true;
        }
    }
}
