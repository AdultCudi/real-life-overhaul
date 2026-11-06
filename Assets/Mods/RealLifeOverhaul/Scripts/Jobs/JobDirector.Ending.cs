using System;
using System.Globalization;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>Jobs' director: what ends a shift, and how it ends.</summary>
    public partial class JobDirector
    {
        // Anything that takes the player off the street or out of their own hands ends the job; what was done is paid.
        private bool Interrupted(float now, out string reasonKey)
        {
            reasonKey = null;
            var uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (uis == null || save == null || LoadScene.isLoading)
            {
                return true;
            }
            if (_job.PassingTime)
            {
                _lastInside = now;   // the game's clock is working the shift; the street restarts a moment after its skip
            }
            if (GameMembers.PlayerDead || EnergyHelper.goingToHospital)
            {
                reasonKey = "reallife_job_end_hospital";
            }
            else if (uis.timeMachine.isRunning && !_job.PassingTime)
            {
                reasonKey = "reallife_job_end_time";    // custody, sleep, a taxi ride, the game's own work; not the job's own shift left to the clock
            }
            else if (save.currentPlayerMission != null)
            {
                reasonKey = "reallife_job_end_mission";
            }
            else if (BuildingManager.IsInsideBuilding)
            {
                // Indoors ends a shift, except in the one building a job says it works in (a client's shop).
                if (_job.WorksInside(InstanceBehavior<BuildingManager>.Instance.building))
                {
                    _lastInside = now;
                }
                else
                {
                    reasonKey = "reallife_job_end_indoors";
                }
            }
            else if (!TrafficTools.Street && now - _lastInside > 6f)   // the street takes a moment to start up again outside a door
            {
                reasonKey = "reallife_job_end_indoors";
            }
            else if (save.Energy <= 1f)
            {
                reasonKey = "reallife_job_end_exhausted";
            }
            else if (!Settings.Jobs)
            {
                reasonKey = "reallife_job_end_off";
            }
            return reasonKey != null;
        }

        private void EndJob(JobEnd why, string reasonKey)
        {
            Job job = _job;
            if (job == null)
            {
                return;
            }
            _job = null;
            Unhook();
            Unguide();
            _context.Car = null;
            _context.Body = null;
            bool failed = why == JobEnd.Failed || (why == JobEnd.Quit && job.QuitIsFailure);
            if (failed && HomeHealth.SickNoteActive)
            {
                failed = false;   // a doctor's sick note (Home/DoctorVisit): a shift lost while it runs is no strike
                JobText.Toast(NotificationType.Info, "reallife_doctor_note_used");
            }
            SafeEnd(job, why);
            if (why != JobEnd.Fault && job.HasEventMoney)
            {
                try
                {
                    Pay(job, default, "reallife_job_slip_events");   // a tip earned after the job's last slip
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            _cast.ReleaseAll();   // whoever a job forgot
            _stand.Clear();       // the place of work lets go of the player
            _body.Release();      // the player is their own again, whatever the job was doing with them
            _uniform.Off();       // ...and in their own clothes
            if (_card != null)
            {
                _card.Show(false);
            }
            _rental.Return();     // a hired van goes back once the player is out of it
            CareerShift shift = _context.Shift;
            _context.Shift = null;
            _context.Gig = null;
            if (shift != null)
            {
                _career.ShiftEnded(job, shift, why, failed);   // the basic wage and the payslip, after the work's own slip
            }
            else
            {
                try
                {
                    SlipNote(job);   // also after a fault: what was earned before it is paid and said
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            try
            {
                JobDef def = job.Def;
                Logger?.Info("Jobs: '" + def.Id + "' ended (" + why + (reasonKey != null ? ", " + reasonKey : string.Empty) + "), " + job.Shift.Units
                             + " unit(s), " + job.Shift.Total + " earned this shift.");
                if (reasonKey != null)
                {
                    JobText.Toast(NotificationType.Warning, reasonKey, "amount", JobText.Money(job.Shift.Total), 9f);
                }
                if (why == JobEnd.Fault || _context.Test || !JobState.Loaded)
                {
                    return;   // a test or a fault never counts for or against the player
                }
                if ((job.Shift.Units > 0 || failed) && JobCatalogue.RecordOf(def.Id) >= 0)   // the two career-only kinds keep no strikes: the employer keeps marks
                {
                    if (JobState.ShiftEnded(def, failed, TimeHelper.CurrentDay))
                    {
                        JobText.Toast(NotificationType.Error, "reallife_job_sacked", "days", JobCatalogue.SackedDays.ToString(), 12f);
                        ModState.AddModifier(ModState.MoodDownBig);
                    }
                    else if (failed)
                    {
                        JobText.Toast(NotificationType.Warning, "reallife_job_strike", "left",
                            (JobCatalogue.StrikesToSack - JobState.Of(def).Strikes).ToString(), 10f);
                        ModState.AddModifier(ModState.MoodDown);
                    }
                    else if (job.Shift.Tips > 0 || why == JobEnd.Done)
                    {
                        ModState.AddModifier(ModState.MoodUp);   // a good shift: tipped, or seen through to the end
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void SafeEnd(Job job, JobEnd why)
        {
            try
            {
                job.End(why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
