using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Entities;
using Helpers;
using PlayerActivity;
using UI;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Jobs' director: paying a slip, and a shift carried across a save.</summary>
    public partial class JobDirector
    {
        private const float EnergyFloor = 12f;       // work never takes the player below this (the game faints them at zero)
        private const float WorstEnergyMultiplier = 1.9f;   // the most the game multiplies energy spent by (low mood, hunger)
        private const string SalaryCategory = "ba:transactioncategory_salaryincome";
        private readonly StringBuilder _text = new StringBuilder(256);

        /// <summary>Pays a slip: the game's transaction under the job's name, energy and skill, and the units towards the level and the day.</summary>
        public void Pay(Job job, in PaySlip given, string titleKey)
        {
            PaySlip slip = given;
            job.TakeEventMoney(ref slip);   // a big tip or a speed bonus earned since the last slip goes on this one
            JobDef def = job.Def;
            int total = slip.Total;
            job.Shift.Add(slip);
            // A lead shift trains the employer's own skill; a test button's work is paid but earns no experience.
            PayOut(def, slip, def.Id == JobCatalogue.Lead && _context.Shift != null && _context.Shift.Employer.LeadSkill != null ? _context.Shift.Employer.LeadSkill : def.Skill,
                !_context.Test);
            // One notification for the whole shift, when it ends (SlipNote).
            Logger?.Info("Jobs: '" + def.Id + "' paid " + total + " for " + JobText.T(titleKey) + " (base " + slip.Base + ", distance " + slip.Distance + ", tips " + slip.Tips
                         + ", bonus " + slip.Bonus + ", penalties " + slip.Penalty + ") for " + slip.Units + " unit(s).");
        }

        // What a slip does, for a shift that is on and for one settled from a save.
        private static void PayOut(JobDef def, in PaySlip slip, string skill, bool counts)
        {
            int total = slip.Total;
            if (total > 0)
            {
                GameManager.ChangeMoneySafe(total, new TransactionInfo(def.TransactionKey, SalaryCategory), null, null, force: true);
            }
            if (slip.Units <= 0)
            {
                return;
            }
            SpendEnergy(def.UnitEnergy * slip.Units);
            if (skill != null && def.UnitSkill > 0f)
            {
                EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
                if (self != null)
                {
                    self.IncreaseSkill(skill, def.UnitSkill * slip.Units);
                }
            }
            if (counts && JobState.Worked(def, slip.Units, total))
            {
                int level = JobState.Level(def);
                JobText.Toast(NotificationType.Success,
                    level == JobCatalogue.ReferenceLevel ? "reallife_job_level_reference" : "reallife_job_level_up", "level", level.ToString(), 10f);
            }
        }

        // Tiring, never dangerous: work stops short of the energy at which the game takes over.
        private static void SpendEnergy(float amount)
        {
            float room = (SaveGameManager.Current.Energy - EnergyFloor) / WorstEnergyMultiplier;
            amount = Mathf.Min(amount, room);
            if (amount > 0f)
            {
                EnergyHelper.SpentEnergyOnce(amount);
            }
        }

        /// <summary>The shift's pay slip as one of the game's notifications; shown even when it comes to nothing, so the player sees why.</summary>
        private void SlipNote(Job job)
        {
            PaySlip slip = job.Shift;
            var data = new Dictionary<string, string>(3)
            {
                { "job", JobText.T(job.Def.NameKey) }, { "total", JobText.Money(slip.Total) }, { "lines", SlipLines(slip) }
            };
            Notifications.Show(slip.Total > 0 ? NotificationType.Success : NotificationType.Info, "reallife_job_slip_note", data, 14f);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Jobs: pay slip shown as one of the game's notifications: " + job.Def.Id + ", " + slip.Total + " (" + data["lines"] + ").");
            }
        }

        /// <summary>A slip's lines that are not zero, as one line of words: "Work $66, Tips $14".</summary>
        public string SlipLines(in PaySlip slip)
        {
            _text.Length = 0;
            Part("reallife_job_slip_base", slip.Base, false);
            Part("reallife_job_slip_distance", slip.Distance, false);
            Part("reallife_job_slip_tips", slip.Tips, false);
            Part("reallife_job_slip_bonus", slip.Bonus, false);
            Part("reallife_job_slip_penalty", slip.Penalty, true);
            return _text.Length > 0 ? _text.ToString() : JobText.Money(0f);
        }

        private void Part(string key, int amount, bool minus)
        {
            if (amount != 0)
            {
                _text.Append(_text.Length > 0 ? ", " : string.Empty).Append(JobText.T(key)).Append(' ').Append(minus ? "-" : string.Empty).Append(JobText.Money(amount));
            }
        }

        /// <summary>The copy of the game's job panel that a job writes its figures on: made the first time one asks, null if it cannot be made.</summary>
        public ShiftCard Card()
        {
            if ((_card == null || !_card.Alive) && !_cardFailed)
            {
                _card = ShiftCard.Make(Logger);
                _cardFailed = _card == null;
            }
            return _card;
        }

        private readonly SavedShift _saved = new SavedShift();   // one, written over at each save

        // The save's line for the shift on: paid so far and owed now; empty for no shift, a test, or figures that cannot be read.
        private string LiveLine()
        {
            Job job = _job;
            CareerShift shift = _context.Shift;
            if (job == null || _broken || _context.Test || (shift != null && !shift.Counts))
            {
                return string.Empty;
            }
            try
            {
                _saved.JobId = job.Def.Id;
                _saved.Day = TimeHelper.CurrentDay;
                _saved.Minutes = TimeHelper.NowInMinutes();
                _saved.Paid = job.Shift;
                _saved.Owed = job.Unpaid();
                _saved.Rota = shift != null;
                _saved.Employer = shift != null ? shift.Employer.Id : string.Empty;
                _saved.Hourly = shift != null ? shift.Hourly : 0f;
                _saved.Late = shift != null && shift.Late;
                _saved.StartMinutes = shift != null ? shift.StartMinutes : 0f;
                string line = JobSave.Write(_saved);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Jobs check (saved shift): '" + job.Def.Id + "' is on at the save: paid so far " + _saved.Paid.Total + " for " + _saved.Paid.Units + " unit(s), owed "
                                 + _saved.Owed.Total + " for " + _saved.Owed.Units
                                 + (shift != null ? ", " + shift.Employer.Id + " at " + shift.Hourly.ToString("F1", CultureInfo.InvariantCulture) + " an hour" : string.Empty)
                                 + "; written to the save: " + (line.Length > 0 ? "PASS" : "FAIL") + ".");
                }
                return line;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return string.Empty;
            }
        }

        // After a load a shift on at the save is settled once; the line is emptied first, so it is never settled twice.
        private void SettleSaved(Dictionary<string, string> data)
        {
            if (data == null || !data.TryGetValue(JobSave.Key, out string line) || string.IsNullOrEmpty(line))
            {
                return;
            }
            data[JobSave.Key] = string.Empty;
            try
            {
                JobDef def = JobSave.Read(line, out SavedShift saved) ? JobCatalogue.Find(saved.JobId) : null;
                if (def == null)
                {
                    Logger?.Warn("Jobs check (saved shift): the save's line for a shift in progress does not add up and was dropped (" + line + "): FAIL.");
                    return;
                }
                ShiftSettlement sum = JobSave.Settle(saved);
                Employer employer = saved.Rota ? CareerCatalogue.Find(saved.Employer) : null;
                // What was done and not paid, as any slip.
                PayOut(def, saved.Owed, def.Id == JobCatalogue.Lead && employer != null && employer.LeadSkill != null ? employer.LeadSkill : def.Skill, true);
                if (saved.Rota)
                {
                    _career.SettleSaved(def, saved, sum);   // the basic wage for the hours clocked, and the shift on the rota's count
                }
                else if (sum.Worked && JobCatalogue.RecordOf(def.Id) >= 0)
                {
                    JobState.ShiftCut(def);   // a shift worked; the strikes stay as they were
                }
                var words = new Dictionary<string, string>(5)
                {
                    { "job", JobText.T(def.NameKey) }, { "amount", JobText.Money(sum.Total) }, { "units", sum.UnitsOwed.ToString() },
                    { "hours", sum.Hours.ToString() }, { "employer", employer != null ? JobText.T(employer.NameKey) : string.Empty }
                };
                Notifications.Show(sum.Total > 0 ? NotificationType.Success : NotificationType.Info,
                    saved.Rota && employer != null ? "reallife_job_saved_rota" : sum.Total > 0 ? "reallife_job_saved_paid" : "reallife_job_saved_nothing", words, 14f);
                Logger?.Info("Jobs check (saved shift): settled '" + def.Id + "' saved on day " + saved.Day + ": paid " + sum.Work + " for " + sum.UnitsOwed + " unit(s) owed"
                             + (saved.Rota ? ", basic wage " + sum.Wage + " for " + sum.Hours + " h at " + saved.Hourly.ToString("F1", CultureInfo.InvariantCulture) : string.Empty)
                             + "; " + saved.Paid.Total + " had been paid before the save; no strike, no mark: PASS.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Jobs check (saved shift): settling the saved shift failed (see above); its line is dropped: FAIL.");
            }
        }
    }
}
