using System;
using System.Collections.Generic;
using System.Text;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.Careers;
using Streets;
using UI;
using UI.CustomUI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's job offer (UI.CustomUI.JobOffer, PlayerHUD.jobOfferPanel, opened by PlayerHUD.ShowJob at a job's desk)
    /// drawn as the kit's offer card while it is open: the bound host every game screen uses, over the world, taking no key
    /// of its own, so the game's keys stay the game's (Interact accepts through GameManager, Cancel closes through
    /// CancelButtonHandler) and the card shows them on its caps. The mouse presses the game's own accept button (its
    /// rules: one job at a time, a refusal after a firing) and its own Cancel. Game side.
    /// </summary>
    public sealed class JobOfferKit : IUiGameScreen
    {
        public string Id => WorldKits.OfferId;

        public string Name => "the job offer";

        public string Title => UiText.T("reallife_ui_world_offer_kicker");

        public UiIcon Icon => UiIcon.Briefcase;

        public UiGameKind Kind => UiGameKind.Overlay;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null ? uis.playerHUD.jobOfferPanel : null;
        }

        public string Check(Component found)
        {
            var offer = found as JobOffer;
            return offer == null || offer.acceptButton == null || offer.title == null || offer.wagePerHourLabel == null || offer.cancelLabel == null
                ? "its labels or its accept button are not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform);

        public IUiScreen Make(Component found) => new JobOfferScreen((JobOffer)found);
    }

    /// <summary>
    /// The job offer as the kit's offer card (UiOfferCard): the job's name under "Job offer", its hours, its days and its
    /// wage as the facts, the business and its address in the side column with the boss's face, Accept on the game's
    /// Interact key and Not now on its Cancel. Read from the game's own job and labels as it opens. Cost: a layout as it
    /// opens; nothing a frame.
    /// </summary>
    public sealed class JobOfferScreen : IUiScreen
    {
        private static readonly string[] DayKeys = { "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday" };

        private readonly JobOffer _offer;
        private readonly UiOfferModel _m = new UiOfferModel();
        private global::Job _read;
        private bool _hired, _dirty = true;

        public JobOfferScreen(JobOffer offer)
        {
            _offer = offer;
        }

        public string Id => WorldKits.OfferId;

        /// <summary>The model last read, for the tests.</summary>
        public UiOfferModel Model => _m;

        public bool Dirty => _dirty || !ReferenceEquals(_offer.job, _read) || _offer.acceptButton.gameObject.activeSelf == _hired;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Read();
            UiOfferCard.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        // The job as the game shows it: its name, its days and hours, its wage, the business and where it is.
        private void Read()
        {
            global::Job job = _offer.job;
            _read = job;
            _hired = !_offer.acceptButton.gameObject.activeSelf;   // already working here: the game hides its accept
            _m.Kicker = UiText.T("reallife_ui_world_offer_kicker");
            _m.Title = WorldHost.Plain(_offer.title.TextContainer != null ? _offer.title.TextContainer.text : null) ?? string.Empty;
            _m.Emblem = UiIcon.Briefcase;
            var facts = new List<UiOfferFact>(3);
            if (job != null && job.scheduleDays != null)
            {
                Schedule(job, out string hours, out bool varies, out string days);
                facts.Add(new UiOfferFact
                {
                    Icon = UiIcon.Clock, Label = UiText.T("reallife_ui_world_offer_hours"), Value = hours, Sub = varies ? UiText.T("reallife_ui_world_offer_hours_vary") : null,
                });
                facts.Add(new UiOfferFact { Icon = UiIcon.Calendar, Label = UiText.T("reallife_ui_world_offer_days"), Value = days });
            }
            string wage = job != null ? job.hourlySalary.ToShortCurrencyFormat() : WorldHost.Plain(_offer.wagePerHourLabel.text);
            facts.Add(new UiOfferFact
            {
                Icon = UiIcon.Coin, Label = UiText.T("joboffer_wage_per_hour"), Value = UiText.T("reallife_ui_world_offer_wage").Replace("{wage}", wage ?? string.Empty),
                Sub = Week(job),
            });
            _m.Facts = facts.ToArray();
            Employer(job);
            _m.BackLabel = UiText.T("reallife_ui_world_offer_not_now");
            _m.BackKey = WorldHost.Key(PlayerAction.Cancel);
            _m.GoLabel = _hired ? UiText.T("reallife_ui_world_offer_hired") : UiText.T("joboffer_accept_job");
            _m.GoKey = WorldHost.Key(PlayerAction.Interact);
            _m.GoOff = _hired || !_offer.acceptButton.interactable;
            // The status bar's: the game's Interact accepts (the kit's Alt is bound to it), its Cancel closes.
            _m.Prompts = _m.GoOff
                ? new[] { new UiPrompt { Action = UiAction.Back, Label = _m.BackLabel } }
                : new[] { new UiPrompt { Action = UiAction.Alt, Label = _m.GoLabel }, new UiPrompt { Action = UiAction.Back, Label = _m.BackLabel } };
            _m.PromptCount = _m.Prompts.Length;
        }

        // The business, where it is and the boss who offers the job, as the game names them (its contact for the boss).
        private void Employer(global::Job job)
        {
            _m.Side = null;
            _m.SideSub = null;
            _m.Face = null;
            _m.FaceLine = null;
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            Buildings.Building building = manager != null ? manager.building : null;
            if (building != null)
            {
                BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(building.Address);
                _m.Side = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : reg != null ? reg.businessTypeName.GetLocalization() : null;
                _m.SideSub = building.Address.ToFormattedString();
            }
            if (job != null && !string.IsNullOrEmpty(job.employerName))
            {
                _m.Face = job.employerName;
                _m.FaceLine = UiText.T("reallife_ui_world_offer_boss").Replace("{name}", job.employerName);
            }
            _m.Body = building != null ? Line(building.Address) : null;
        }

        // How the player got here, in one line and only if it is true: the mod's interview for a post in this very building (its score
        // sets how warmly it is put), else the game's own record of a firing here. Nothing to say, nothing said.
        private static string Line(Address at)
        {
            CareersDirector careers = CareersDirector.Instance;
            Careers.Application a = careers != null && careers.Work != null && at != null ? OfferLineRules.Find(careers.Work.Applications, at.streetName, at.streetNumber) : null;
            if (a != null)
            {
                return UiText.T(OfferLineRules.Key(OfferLineRules.Band(a.Stage, a.Score)));
            }
            JobInstance mine = at != null ? JobHelper.GetJobInstance(at) : null;
            return mine != null && mine.fired ? UiText.Fill("reallife_ui_world_offer_let_go", "day", UiText.Whole(mine.firedDay)) : null;
        }

        // "About $1,520 a week": the wage for the hours of every shift of the week, as the job's schedule has them.
        private static string Week(global::Job job)
        {
            if (job == null || job.scheduleDays == null || job.hourlySalary <= 0f)
            {
                return null;
            }
            int hours = 0;
            for (int i = 0; i < job.scheduleDays.Count && i < 7; i++)
            {
                ScheduleDay day = job.scheduleDays[i];
                for (int s = 0; day != null && day.isOpen && day.workShifts != null && s < day.workShifts.Count; s++)
                {
                    WorkShift shift = day.workShifts[s];
                    int length = shift != null ? shift.endingHour - shift.startingHour : 0;
                    hours += length < 0 ? length + 24 : length;   // a night shift ends the next day
                }
            }
            return hours <= 0 ? null : UiText.T("reallife_ui_world_offer_week").Replace("{amount}", Mathf.Round(job.hourlySalary * hours).ToShortCurrencyFormat());
        }

        // The working days' hours ("09:00 to 17:00", the first day's when they differ) and the days as a run ("Monday to
        // Friday") or a list ("Monday, Wednesday and Friday"), in the game's day names and its clock.
        private static void Schedule(global::Job job, out string hours, out bool varies, out string days)
        {
            hours = null;
            varies = false;
            var on = new bool[7];
            int start = -1, end = -1;
            for (int i = 0; i < job.scheduleDays.Count && i < 7; i++)
            {
                ScheduleDay day = job.scheduleDays[i];
                if (day == null || !day.isOpen || day.workShifts == null || day.workShifts.Count == 0)
                {
                    continue;
                }
                on[i] = true;
                WorkShift shift = day.workShifts[0];
                if (start < 0)
                {
                    start = shift.startingHour;
                    end = shift.endingHour;
                }
                else if (shift.startingHour != start || shift.endingHour != end)
                {
                    varies = true;
                }
            }
            hours = start < 0 ? UiText.T("playerhud_currentjob_day_off")
                : UiText.T("reallife_ui_world_offer_span").Replace("{from}", start.GetFormattedTime()).Replace("{to}", end.GetFormattedTime());
            var names = new string[DayKeys.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = UiText.T(DayKeys[i]);
            }
            days = UiWorld.Days(on, names, UiText.T("reallife_ui_world_offer_run"), UiText.T("reallife_ui_world_offer_and"), UiText.T("playerhud_currentjob_day_off"));
        }

        public bool Press(int command, int arg)
        {
            switch (command)
            {
                case UiOfferCard.GoCommand:
                    return !_m.GoOff && UiWidgets.Press(_offer.acceptButton);
                case UiOfferCard.BackCommand:
                    _offer.Cancel();
                    return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;   // the game's Cancel closes the offer, as without the kit

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
