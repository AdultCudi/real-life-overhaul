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
            if (!Free(out string reason) || !StartJob(def, true, out reason))
            {
                Logger?.Info("Jobs: test of '" + id + "' could not start (" + reason + ").");
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
