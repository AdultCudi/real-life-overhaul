using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Careers: signing on, leaving, dismissal and the review.</summary>
    public sealed partial class CareerDirector
    {
        /// <summary>Whether an employer would take the player on today. If not, the reason is a key with {count}, {day}.</summary>
        public bool CanSignOn(int employer, out string reasonKey, out string value)
        {
            reasonKey = null;
            value = null;
            Employer who = CareerCatalogue.All[employer];
            int day = TimeHelper.CurrentDay;
            if (!On)
            {
                reasonKey = "reallife_career_no_off";
            }
            else if (CareerState.Employed && CareerState.EmployerIndex == employer)
            {
                reasonKey = "reallife_career_no_already";
            }
            else if (CareerState.Employed)
            {
                reasonKey = "reallife_career_no_other";
                value = JobText.T(CareerState.Employer.NameKey);
            }
            else if (CareerState.BannedAt(employer, day))
            {
                reasonKey = "reallife_career_no_banned";
                value = (CareerState.BannedUntil(employer) - day).ToString();
            }
            else if (CareerState.ReferenceScore(JobState.HasReference) < who.References)
            {
                reasonKey = "reallife_career_no_references";
                value = who.References.ToString();
            }
            else if (CareerLinks.Lacks(who.Ranks[0]) != null)
            {
                reasonKey = CareerLinks.Lacks(who.Ranks[0]);   // a building trade's qualification (Scripts/BuildTrades)
            }
            return reasonKey == null;
        }

        public void SignOn(int employer, CareerRota rota, bool test)
        {
            Employer who = CareerCatalogue.All[employer];
            JobDef own = JobCatalogue.Find(who.Job);
            JobDef related = who.Related != null ? JobCatalogue.Find(who.Related) : null;
            int credit = CareerCatalogue.CarryOver(own != null ? JobState.Level(own) : 1, related != null ? JobState.Level(related) : 1);
            if (who.Id == CareerCatalogue.Retail)
            {
                credit = Mathf.Min(CareerCatalogue.ShiftsPerReview - 1, credit + CareerState.CashierHours / 8);   // a day at the game's own till counts
            }
            CareerState.SignOn(employer, rota, TimeHelper.CurrentDay, credit);
            _dismissAfter = false;
            var data = new Dictionary<string, string>(3)
            {
                { "employer", JobText.T(who.NameKey) }, { "title", JobText.T(who.Ranks[0].TitleKey) }, { "rota", JobText.T(rota.NameKey) }
            };
            Notifications.Show(NotificationType.Success, "reallife_career_signed", data, 12f);
            CareerPhone.Text("reallife_career_text_signed", data);
            ModState.AddModifier(ModState.MoodUp);
            CareerLinks.TellMet(who, -1);
            _logger?.Info("Careers: signed on with " + who.Id + (test ? " by the test button" : string.Empty) + ", rota " + rota.Days + "@" + rota.Hour
                          + ", experience credited " + credit + " shift(s).");
        }

        public void GiveNotice()
        {
            if (!CareerState.Employed || CareerState.OnNotice)
            {
                return;
            }
            CareerState.GiveNotice(TimeHelper.CurrentDay);
            _logger?.Info("Careers: notice given at " + CareerState.Employer.Id + ".");
        }

        private void FinishNotice(int day)
        {
            Employer employer = CareerState.Employer;
            CareerReference reference = CareerState.EarnedReference();
            CareerState.Leave(day, reference);
            var data = new Dictionary<string, string>(1) { { "employer", JobText.T(employer.NameKey) } };
            Notifications.Show(NotificationType.Info, reference == CareerReference.Good ? "reallife_career_left_good" : "reallife_career_left_plain", data, 12f);
            CareerPhone.Text(reference == CareerReference.Good ? "reallife_career_text_left_good" : "reallife_career_text_left_plain", data);
            _logger?.Info("Careers: notice worked at " + employer.Id + ", reference " + reference + ".");
        }

        public void WalkOut()
        {
            if (!CareerState.Employed)
            {
                return;
            }
            Employer employer = CareerState.Employer;
            CareerState.Leave(TimeHelper.CurrentDay, CareerReference.MarkedDown);
            _dismissAfter = false;
            _logger?.Info("Careers: walked out of " + employer.Id + ".");
        }

        private void Dismiss(string key)
        {
            if (!CareerState.Employed)
            {
                return;
            }
            Employer employer = CareerState.Employer;
            CareerState.Leave(TimeHelper.CurrentDay, CareerReference.MarkedDown);
            _dismissAfter = false;
            var data = new Dictionary<string, string>(3)
            {
                { "employer", JobText.T(employer.NameKey) }, { "boss", JobText.T(employer.BossKey) }, { "days", CareerCatalogue.BanDays.ToString() }
            };
            Notifications.Show(NotificationType.Error, key, data, 14f);
            CareerPhone.Text("reallife_career_text_dismissed", data);
            ModState.AddModifier(ModState.MoodDownBig);
            _logger?.Info("Careers: dismissed by " + employer.Id + " (" + key + ").");
        }

        /// <summary>Whether the review can be held here: face to face at the office, or by phone for an employer with no office in this city.</summary>
        public bool CanReview(bool inPerson)
        {
            return CareerState.Employed && CareerState.ReviewDue && (inPerson || DepotOf(CareerState.EmployerIndex) < 0);
        }

        /// <summary>Holds the review. The numbers read out are the ones the outcome was decided from.</summary>
        public ReviewOutcome HoldReview(out float score, out int onTime, out int due, out int tips, out int complaints, out int crashes, out int stops)
        {
            onTime = CareerState.OnTime;
            due = CareerState.OnTime + CareerState.Late + CareerState.NoShow;
            tips = CareerState.Tips;
            complaints = CareerState.Complaints;
            crashes = CareerState.Crashes;
            stops = CareerState.Stops;
            Employer employer = CareerState.Employer;
            // A rank that asks a qualification (the building trades') is not given to a player without it, whatever the score.
            CareerRank next = CareerState.Rank + 1 < employer.Ranks.Length ? employer.Ranks[CareerState.Rank + 1] : null;
            string lacks = next != null ? CareerLinks.Lacks(next) : null;
            ReviewOutcome outcome = CareerState.Review(!CareerState.OnNotice && lacks == null, out score);
            if (lacks != null && outcome != ReviewOutcome.Warning && outcome != ReviewOutcome.Dismissed && score >= CareerCatalogue.PromoteScore)
            {
                var held = new Dictionary<string, string>(2) { { "title", JobText.T(next.TitleKey) }, { "needs", JobText.T(lacks) } };
                Notifications.Show(NotificationType.Info, "reallife_career_review_held", held, 12f);
                _logger?.Info("Careers: review at " + employer.Id + " would have promoted to '" + next.TitleKey + "', held back: " + lacks + ".");
            }
            _logger?.Info("Careers: review at " + employer.Id + ": score " + score.ToString("F0") + " (on time " + onTime + "/" + due + ", tips " + tips
                          + ", complaints " + complaints + ", crashes " + crashes + ", stops " + stops + ") -> " + outcome + ".");
            switch (outcome)
            {
                case ReviewOutcome.Promoted:
                    ModState.AddModifier(ModState.MoodUpBig);
                    break;
                case ReviewOutcome.Raise:
                    ModState.AddModifier(ModState.MoodUp);
                    break;
                case ReviewOutcome.Warning:
                    ModState.AddModifier(ModState.MoodDown);
                    break;
                case ReviewOutcome.Dismissed:
                    CareerState.Leave(TimeHelper.CurrentDay, CareerReference.MarkedDown);
                    ModState.AddModifier(ModState.MoodDownBig);
                    break;
            }
            return outcome;
        }
    }
}
