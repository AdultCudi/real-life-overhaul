using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A manager's duty "Answers reviews": at the staff hour of every day (09:00) the manager answers the waiting reviews of their business on Flare, the ones
    /// no older than ten days, by the very rule "Answer all" uses (thanks for four and five stars, sorry for two and one, a word for the honesty of a three, a
    /// poor review that is out of line with the rating left for the player), so a manager gains nothing a hand answer would not. A mistake of theirs is a day
    /// they do not get to them. The duty costs the manager's allowance and nothing else. Sunday evening the week's answers are told in one notice with the
    /// rating then and now, and the poor reviews that may deserve the player's own answer. Cost: one pass over a business's waiting reviews a day, nothing per frame.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        // At the manager's hour: the business's waiting reviews are answered by Flare's batch rule; a decision is written down when something was answered or a mistake kept it back.
        private void DecideReviews(ManagedPlace mp, int skill, int review, string who, int day, int hour)
        {
            if (!ManagerReviews.May(Settings.FlareManagerReplies, CompanyBridges.ReviewsReady()))
            {
                return;
            }
            string place = mp.Place.Place;
            int roll;
            bool slip = Slips(skill, review, day, place, ManagerTask.Reviews, string.Empty, out roll);
            ReviewRun run = CompanyBridges.AnswerReviews(place, slip);
            if (!run.Ok || (run.Answers == 0 && run.Skipped == 0))
            {
                return;
            }
            Decision d = Record(mp, ManagerTask.Reviews, string.Empty, who, day, hour, run.Skipped > 0);
            d.A = run.Thanks;
            d.B = run.Sorry;
            d.C = run.Mixed;
            d.D = run.Left;
            d.E = run.Before;
            d.F = run.After;
            d.G = run.Skipped;
            d.State = DecisionState.Closed;   // an answer once up cannot be taken back
            ManagerReviews.Record(_mgr.Week, day, place, run.Before, run.Thanks, run.Sorry, run.Mixed);
            Logger?.Info("Company: " + who + " (manager, skill " + skill + ") answered " + run.Answers + " reviews at " + mp.Place.Name + " (" + run.Thanks + " thanks, " + run.Sorry + " sorry, "
                         + run.Mixed + " in between), " + run.Left + " left for the owner" + (run.Skipped > 0 ? ", a mistake: " + run.Skipped + " not got to today" : string.Empty) + ".");
        }

        // Sunday evening: the week's answers told in one notice, and the week's record cleared. Nothing is told for a week with no answer and no review left to the owner.
        private void ReviewsWeekly(int day)
        {
            ReviewWeek w = _mgr.Week;
            if (!ManagerReviews.May(Settings.FlareManagerReplies, CompanyBridges.ReviewsReady()))
            {
                ManagerReviews.Clear(w);
                return;
            }
            int flagged = 0;
            for (int i = 0; i < _managed.Count; i++)
            {
                ManagedPlace mp = _managed[i];
                if (mp.Own != null && ManagerRules.Runs(mp.Own.Tasks, mp.Kind, ManagerTask.Reviews))
                {
                    flagged += CompanyBridges.FlaggedAt(mp.Place.Place);
                }
            }
            if (w.Answered == 0 && flagged == 0)
            {
                ManagerReviews.Clear(w);
                return;
            }
            int before = ManagerReviews.Before(w), after = 0, rated = 0;
            for (int i = 0; i < w.Places.Count; i++)
            {
                int now = w.Places[i].Before > 0 ? CompanyBridges.RatingAt(w.Places[i].Place) : 0;
                if (now > 0)
                {
                    after += now;
                    rated++;
                }
            }
            after = rated > 0 ? after / rated : 0;
            string was = (before / 1000f).ToString("0.00", CultureInfo.InvariantCulture), nowText = (after / 1000f).ToString("0.00", CultureInfo.InvariantCulture);
            string rating = before <= 0 || after <= 0 ? string.Empty
                : was != nowText ? CompanyText.T("reallife_flare_batch_rating_all_moved", "before", was, "after", nowText) : CompanyText.T("reallife_flare_batch_rating_all_same", "after", nowText);
            string left = flagged <= 0 ? string.Empty
                : CompanyText.T(flagged == 1 ? "reallife_flare_batch_left_one" : "reallife_flare_batch_left", "count", flagged.ToString(CultureInfo.InvariantCulture));
            string parts = ReviewParts(w);
            Tell(NotificationType.Info, w.Answered > 0 ? "reallife_mgr_note_reviews" : "reallife_mgr_note_reviews_left",
                new Dictionary<string, string>
                {
                    { "count", w.Answered.ToString(CultureInfo.InvariantCulture) }, { "parts", parts }, { "rating", rating }, { "left", left }
                }, TabManagers, null);
            Logger?.Info("Company: the managers' week of reviews, day " + day + ": " + w.Answered + " answered at " + w.Places.Count + " business(es), rating " + before + " to " + after + ", "
                         + flagged + " poor review(s) left for the owner.");
            ManagerReviews.Clear(w);
        }

        /// <summary>
        /// The self-test's read-only probe: how many managers have the duty Answers reviews, how many rounds of it are in the log with what they answered and missed, and the
        /// week's count so far: "managers 1 duty 1 rounds 3 thanks 9 sorry 2 mixed 1 missed 0 week 12". Changes nothing.
        /// </summary>
        public string ReviewsProbe()
        {
            if (_mgr == null)
            {
                return "no managers";
            }
            int managers = _mgr.Managers.Count, duty = 0, rounds = 0, thanks = 0, sorry = 0, mixed = 0, missed = 0;
            for (int i = 0; i < _mgr.Managers.Count; i++)
            {
                duty += !_mgr.Managers[i].Region && ManagerRules.Has(_mgr.Managers[i].Tasks, ManagerTask.Reviews) ? 1 : 0;
            }
            for (int i = 0; i < _mgr.Decisions.Count; i++)
            {
                Decision d = _mgr.Decisions[i];
                if (d.Task == (int)ManagerTask.Reviews)
                {
                    rounds++;
                    thanks += d.A;
                    sorry += d.B;
                    mixed += d.C;
                    missed += d.G;
                }
            }
            return "managers " + managers + " duty " + duty + " rounds " + rounds + " thanks " + thanks + " sorry " + sorry + " mixed " + mixed + " missed " + missed + " week " + _mgr.Week.Answered;
        }

        private static string ReviewParts(ReviewWeek w)
        {
            string parts = string.Empty;
            parts = ReviewPart(parts, w.Thanks, "reallife_flare_batch_p_thanks");
            parts = ReviewPart(parts, w.Sorry, "reallife_flare_batch_p_sorry");
            return ReviewPart(parts, w.Mixed, "reallife_flare_batch_p_mixed");
        }

        private static string ReviewPart(string text, int count, string key)
        {
            return count <= 0 ? text : (text.Length > 0 ? text + ", " : text) + CompanyText.T(key, "count", count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
