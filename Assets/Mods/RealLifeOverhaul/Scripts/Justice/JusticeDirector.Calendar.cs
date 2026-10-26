using System;
using System.Collections.Generic;
using Streets;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    // Once an in-game hour: court dates, reminders and warrants; once a day, everything that runs out or falls due.
    public sealed partial class JusticeDirector
    {
        private void OnHour(int today, int hour)
        {
            if (today != JusticeState.Day)
            {
                JusticeState.Day = today;
                NewDay(today);
            }
            for (int i = 0; i < JusticeState.Cases.Count; i++)
            {
                JusticeCase c = JusticeState.Cases[i];
                if (c.Stage == CaseStage.Pending)
                {
                    Pending(c, today, hour);
                }
                else if (c.Stage == CaseStage.Warrant)
                {
                    if (today - c.WarrantDay >= JusticeLaw.WarrantSelfArrestDays && hour >= 8 && hour <= 20 && !JusticeState.ServeWarrant)
                    {
                        JusticeState.ServeWarrant = true;   // nobody has come across them: officers come to them
                        Logger?.Info("Justice: the warrant in case " + c.Number + " is " + (today - c.WarrantDay) + " days old; it is served at the player's door.");
                    }
                }
                else if (c.Stage == CaseStage.Bail && today > c.OpenedDay + 1)
                {
                    // Bail could not be put to them for two days: released on their own recognisance, with a date.
                    SetDate(c, today);
                }
            }
        }

        private void Pending(JusticeCase c, int today, int hour)
        {
            // The prosecutor reads the file the day after: charges the evidence cannot carry are dropped, and a case left with none ends.
            if (!c.Reviewed && today > c.OpenedDay && c != (_hearing != null ? _hearing.Case : null))
            {
                c.Reviewed = true;
                var dropped = new List<string>(2);
                for (int k = 0; k < c.Charges.Count; k++)
                {
                    if (!c.Charges[k].Dropped && c.Charges[k].Evidence < JusticeLaw.DismissBelow)
                    {
                        c.Charges[k].Dropped = true;
                        dropped.Add(JusticeText.Charge(c.Charges[k].Id));
                    }
                }
                if (c.Standing == 0)
                {
                    Dismiss(c, announce: true);
                    return;
                }
                if (dropped.Count > 0)
                {
                    LegalPhone.Send("reallife_text_justice_dropped", Data("number", JusticeText.N(c.Number), "charges", string.Join(", ", dropped)));
                }
            }
            bool late = today > c.CourtDay || (today == c.CourtDay && hour >= JusticeLaw.CourtCloses);
            if (late)
            {
                bool inCell = _lawHolds || LawHolding();
                bool excused = JusticeState.InPrison || _routine || inCell || (_hearing != null && _hearing.Case == c) || JusticeState.HearNow == c.Number
                               || Helpers.EnergyHelper.goingToHospital;
                if (excused && (c.Postponed < 5 || JusticeState.InPrison || _routine || _lawHolds))   // nobody in a cell is ever counted absent
                {
                    c.Postponed++;
                    c.CourtDay = today + 1;
                    c.Reminded = 0;
                    Logger?.Info("Justice: case " + c.Number + " could not be attended (held elsewhere); the hearing is moved to day " + c.CourtDay + ".");
                }
                else if (_hearing == null || _hearing.Case != c)
                {
                    IssueWarrant(c, today);
                }
                return;
            }
            if (today == c.CourtDay - 1 && hour >= 19 && (c.Reminded & 1) == 0)
            {
                c.Reminded |= 1;
                LegalPhone.Send("reallife_text_justice_tomorrow", Data("number", JusticeText.N(c.Number), "place", CourtPlaceName()));
            }
            if (today == c.CourtDay && hour >= 7 && (c.Reminded & 2) == 0)
            {
                c.Reminded |= 2;
                LegalPhone.Send("reallife_text_justice_today", Data("number", JusticeText.N(c.Number), "place", CourtPlaceName()));
                JusticeText.Toast(NotificationType.Warning, "reallife_justice_toast_today", "place", CourtPlaceName(), 12f);
                PointTheWay();
            }
            if (today == c.CourtDay && hour == (int)JusticeLaw.CourtCloses - 1 && (c.Reminded & 4) == 0)
            {
                c.Reminded |= 4;
                JusticeText.Toast(NotificationType.Error, "reallife_justice_toast_last_hour", 12f);
            }
        }

        // The courthouse as the player's map destination if they have none set: the game's own guider, saved by the game.
        private void PointTheWay()
        {
            try
            {
                var save = SaveGameManager.Current;
                if (!JusticeSettings.Courthouse || !_court.Usable || _court.Address == null || save == null
                    || (save.customDestination != null && !save.customDestination.IsUndefined()))
                {
                    return;
                }
                GuidersManager.SetGuiderTarget(_court.Address, DirectionGuiderType.Destination);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void NewDay(int today)
        {
            PayInstalments(today);
            if (JusticeState.ProbationUntil > 0 && today > JusticeState.ProbationUntil)
            {
                JusticeState.ProbationUntil = 0;
                JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_probation_over", 10f);
            }
            if (JusticeState.SuspendedUntil > 0 && today > JusticeState.SuspendedUntil)
            {
                JusticeState.SuspendedUntil = 0;
                JusticeText.Toast(NotificationType.Success, "reallife_justice_toast_licence_back", 10f);
            }
            if (JusticeState.CommunityHours > 0 && JusticeState.CommunityDue > 0 && today > JusticeState.CommunityDue && !_community.Active)
            {
                // Not done in time: the hours left become a final fine (bounded) and probation runs two days longer.
                int hours = JusticeState.CommunityHours;
                int fine = JusticeLaw.Bound(hours * 100, Crime.Cash, 0.10f, 100);
                JusticeState.CommunityHours = 0;
                JusticeState.CommunityDue = 0;
                JusticeState.AddDebt(JusticeDebt.Fine, fine, JusticeLaw.FinePlanDays, JusticeDebt.Final);
                JusticeState.ProbationUntil = Mathf.Min(Mathf.Max(JusticeState.ProbationUntil, today) + 2, today + JusticeLaw.ProbationCapDays);
                LegalPhone.Send("reallife_text_justice_community_missed", Data("hours", JusticeText.N(hours), "amount", JusticeText.Money(fine)));
            }
            for (int i = JusticeState.Suits.Count - 1; i >= 0; i--)
            {
                JusticeSuit suit = JusticeState.Suits[i];
                if (today > suit.DueDay)
                {
                    JusticeState.Suits.RemoveAt(i);
                    JudgeSuit(suit);
                }
            }
            OwnClaims.Day(today);   // the claims the player brings, heard on their day
            if (JusticeSettings.Aftermath && JusticeState.Priors > 0 && JusticeState.OpenCount == 0 && !JusticeState.OnProbation(today) && !JusticeState.InPrison
                && today - JusticeState.LastConvictionDay >= JusticeLaw.SealAfterCleanDays)
            {
                int count = JusticeState.Seal();
                CallBridge(() => Justice.Bridges.Law.RecordSealed());
                ModState.AddModifier(ModState.MoodUp);
                LegalPhone.Send("reallife_text_justice_sealed", Data("count", JusticeText.N(count), "days", JusticeText.N(JusticeLaw.SealAfterCleanDays)));
                Logger?.Info("Justice: the record was sealed (" + count + " entries) after " + JusticeLaw.SealAfterCleanDays + " clean days.");
            }
            JusticeState.Trim();
        }
    }
}
