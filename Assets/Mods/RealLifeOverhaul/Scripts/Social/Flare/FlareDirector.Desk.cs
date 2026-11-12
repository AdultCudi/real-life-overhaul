using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the desk session (FlareCityRules.Plan; the session itself is the sessions' registry's, Wiring.FlareSessionLinks): the
    /// week's plan made at a desk or by an office's manager, its posts put up from the plan's hour on their days as the player's own
    /// announcements about their businesses, the poor reviews' bad talk answered at once, and a storm answered honestly the day it breaks.
    /// COST: nothing per frame; a look at the plan on the hour.
    /// </summary>
    public sealed partial class FlareDirector
    {
        /// <summary>Whether this week's plan is made already, so a session at another office waits; true while the residents' side does not run.</summary>
        public bool DeskPlanned(int day) => !CityOn || FlareCityRules.Planned(_city.Plan, day);

        /// <summary>
        /// A desk session's plan for the week from its choices and its holder (thousandths of the owner's strength): made now, its first post up
        /// now if it plans any, the poor reviews' bad talk answered, and a storm standing now answered. False while the residents' side does not run.
        /// </summary>
        public bool DeskPlan(int day, int posts, bool answerReviews, bool answerStorm, int strength, bool manager, out int planned, out int answered, out bool stormNow)
        {
            planned = answered = 0;
            stormNow = false;
            if (!CityOn || Save == null)
            {
                return false;
            }
            FlarePlan plan = FlareCityRules.MakePlan(day, posts, answerStorm, strength, manager);
            _city.Plan = plan;
            planned = plan.Posts;
            answered = answerReviews ? AnswerReviews(plan, day) : 0;
            stormNow = answerStorm && CityScandal() && StormAnswer();
            if (FlareCityRules.PlanDue(plan, day))
            {
                PlanPost(plan, day);   // the first post at once
            }
            string storm = stormNow ? ", the storm answered honestly now" : answerStorm ? ", a storm to be answered honestly" : string.Empty;
            Logger?.Info("Flare: this week's plan, " + (manager ? "by the manager at " + strength + " thousandths" : "by the player") + ": " + planned + " post(s), "
                         + answered + " business(es)' poor reviews answered" + storm + ".");
            Changed();
            return true;
        }

        // The plan on the hour: from its hour, the post due today, and a storm standing answered if the plan says so.
        private void PlanHour(int day, int hour)
        {
            FlarePlan plan = CityOn ? _city.Plan : null;
            if (plan == null || hour < FlareCityRules.PlanHour)
            {
                return;
            }
            if (FlareCityRules.PlanDue(plan, day))
            {
                PlanPost(plan, day);
            }
            if (FlareCityRules.PlanAnswers(plan, day) && CityScandal() && StormAnswer())
            {
                Logger?.Info("Flare: this week's plan answers the storm with an honest post.");
                Notice(NotificationType.Info, "reallife_flare_session_storm_note", _openFeed);
            }
        }

        // A planned post: the player's own announcement about the next of their businesses, by Flare's rules (its reach, its followers, the residents
        // who hear it), or, by a manager at their strength, missed.
        private void PlanPost(FlarePlan plan, int day)
        {
            List<BuildingRegistration> shops = FlareGame.Businesses();
            bool make = shops.Count > 0 && FlareCityRules.PlanMakes(plan, _file.Seed, day);
            int turn = plan.Turn;
            FlareCityRules.PlanPosted(plan, day);
            if (!make)
            {
                Logger?.Info("Flare: a post of this week's plan " + (shops.Count == 0 ? "had no business to be about." : "was missed by the manager."));
                return;
            }
            BuildingRegistration reg = shops[turn % shops.Count];
            plan.Turn = turn + 1;
            var c = new FlareCompose
            {
                Kind = FlareSource.Announce, Arg = plan.Made % 2 == 0 ? (int)FlareAnnounce.New : (int)FlareAnnounce.Sale, Place = FlareGame.Place(reg), Name = FlareGame.Name(reg),
                Tone = FlareTone.Proud, ToneChosen = true, Tag = false
            };
            if (Compose(c, out string why))
            {
                plan.Made++;
            }
            else
            {
                Logger?.Info("Flare: a post of this week's plan could not go up (" + why + ").");
            }
        }

        // An honest post of the player's answering the storm standing: about the arrest, or a word of their own after a pile-on. True when posted.
        private bool StormAnswer()
        {
            int day = Save.Day;
            bool arrest = _file.ArrestDay >= 0 && day - _file.ArrestDay <= FlareCityCatalogue.ScandalDays;
            int draft = -1;
            for (int i = 0; arrest && i < _file.Drafts.Count; i++)
            {
                draft = _file.Drafts[i].Source == FlareSource.Arrested ? _file.Drafts[i].Id : draft;
            }
            var c = new FlareCompose { Kind = arrest ? FlareSource.Arrested : FlareSource.Life, Draft = draft, Tone = FlareTone.Honest, ToneChosen = true, Tag = false };
            return Compose(c, out _);
        }

        // The poor reviews' bad talk at each of the player's businesses answered, so it fades faster; a manager's at their strength. How many.
        private int AnswerReviews(FlarePlan plan, int day)
        {
            List<BuildingRegistration> shops = FlareGame.Businesses();
            int answered = 0;
            for (int i = 0; i < shops.Count; i++)
            {
                int b = _cityPass.BusinessOfPlace(FlareGame.Place(shops[i]));
                if (b < 0 || b >= _city.BusinessCount || _city.Answered[b] || !FlareCityRules.AnswerMade(plan.Manager, plan.Strength, _file.Seed, day, b))
                {
                    continue;
                }
                answered += FlareCityRules.Answer(_city, b) ? 1 : 0;
            }
            return answered;
        }

        // The desk session listed while the residents' side runs and taken out when it stops (Wiring.FlareSessionLinks).
        private static void DeskHold(bool on)
        {
            FlareBridges.Desk?.Invoke(on);
        }

        /// <summary>Test: this week's plan made now as the player would at a desk, with the sound choices; the notice says what it planned and did.</summary>
        public void TestResidentsPlan()
        {
            if (!CityTestReady("this week's content plan"))
            {
                return;
            }
            Guarded(() =>
            {
                int made = _city.Plan.Made;
                DeskPlan(Save.Day, FlareCityRules.PlanDaily, true, true, 1000, false, out int planned, out int answered, out bool storm);
                bool pass = _city.Plan.Posts == planned && _city.Plan.Day == Save.Day && (_city.Plan.Made > 0 || FlareGame.Businesses().Count == 0);
                Tell(NotificationType.Info, "reallife_flare_city_test_plan_done", _openFeed, "posts", FlareText.N(planned), "answered", FlareText.N(answered));
                Logger?.Info("Flare check (this week's content plan): " + planned + " post(s) planned, " + _city.Plan.Made + " made now, " + answered + " business(es)' poor reviews answered"
                             + (storm ? ", the storm answered" : string.Empty) + ": " + (pass ? "PASS" : "FAIL"));
            });
        }
    }
}
