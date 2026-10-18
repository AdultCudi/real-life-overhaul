using System;
using System.Collections.Generic;
using System.Text;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The Work contact's conversation, by phone or face to face with a boss: each page a dialogue entry of the game's panel.</summary>
    public sealed class CareerDialog : Dialog
    {
        private static readonly StringBuilder Text = new StringBuilder(256);
        private static readonly string[] DayNames =
        {
            "reallife_day_monday", "reallife_day_tuesday", "reallife_day_wednesday", "reallife_day_thursday", "reallife_day_friday",
            "reallife_day_saturday", "reallife_day_sunday"
        };

        public CareerDialog()
        {
            int at = CareerPhone.At;
            CareerPhone.At = -1;
            bool inPerson = DialogController.current.dialogType != DialogType.PhoneCall;
            if (!inPerson)
            {
                at = -1;
            }
            npcNameKey = at >= 0 ? CareerCatalogue.All[at].BossKey : CareerPhone.ContactId;
            DialogEntry entry;
            try
            {
                entry = Start(at);
            }
            catch (Exception e)
            {
                JobDirector.Logger?.Error(e);
                entry = PhoneContacts.Closing("reallife_career_no_off");
            }
            DialogController.current.ShowEntry(Guard.Entry(entry));
        }

        private static CareerDirector Career => JobDirector.Instance != null ? JobDirector.Instance.Career : null;

        // A button of the game's dialogue panel runs this: nothing thrown in here may reach the game's own UI code.
        private static Func<DialogEntry> G(Func<DialogEntry> page)
        {
            return () =>
            {
                try
                {
                    return page();
                }
                catch (Exception e)
                {
                    JobDirector.Logger?.Error(e);
                    return PhoneContacts.Closing("reallife_career_no_off");
                }
            };
        }

        private static DialogEntry Start(int at)
        {
            if (Career == null || !CareerDirector.On || !CareerState.Loaded)
            {
                return PhoneContacts.Closing("reallife_career_no_off");
            }
            if (at >= 0)
            {
                return CareerState.Employed && CareerState.EmployerIndex == at ? Staff(true) : Hire(at);
            }
            return CareerState.Employed ? Staff(false) : Freelance();
        }

        private static DialogEntry Hire(int at)
        {
            Employer employer = CareerCatalogue.All[at];
            CareerRank first = employer.Ranks[0];
            bool can = Career.CanSignOn(at, out string reason, out string value);
            float rate = JobDirector.PayScale();   // the wage as it will be paid, owners' cut included
            var entry = new DialogEntry
            {
                headerKey = employer.BossKey,
                messageData = employer.Pitch.Localize(new
                {
                    title = first.TitleKey.GetLocalization(),
                    wage = (first.Hourly * rate).ToCurrencyFormat(),
                    status = can
                        ? "reallife_career_hire_ready".GetLocalization()
                        : reason.GetLocalization().Replace("{value}", value ?? string.Empty)
                }),
                OnCancel = PhoneContacts.HangUp
            };
            // By phone, the second button shows the way to the office (the gig board is the Gig Line's, and the board's own).
            bool showWay = DialogController.current.dialogType == DialogType.PhoneCall && CareerOfficeRules.Has(Career.OfficeAddress(at));
            if (can)
            {
                entry.ConfirmTextOverride = "reallife_career_sign_on".Localize();
                entry.OnConfirm = G(() => Rota(at, 0));
                entry.SecondOptionTextOverride = showWay ? "reallife_career_office_show" : "reallife_career_gig_board";
                entry.OnSecondOption = showWay ? G(() => ShowOffice(at)) : G(() => JobLineDialog.Offer(0));
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_career_gig_board".Localize();
                entry.OnConfirm = G(() => JobLineDialog.Offer(0));
                if (showWay)
                {
                    entry.SecondOptionTextOverride = "reallife_career_office_show";
                    entry.OnSecondOption = G(() => ShowOffice(at));
                }
            }
            return entry;
        }

        private static DialogEntry Rota(int at, int index)
        {
            CareerRota[] rotas = CareerCatalogue.Rotas;
            CareerRota rota = rotas[index % rotas.Length];
            Employer employer = CareerCatalogue.All[at];
            return new DialogEntry
            {
                headerKey = employer.BossKey,
                messageData = "reallife_career_rota_offer".Localize(new
                {
                    rota = rota.NameKey.GetLocalization(),
                    time = CareerClock.Span(rota.Hour)
                }),
                ConfirmTextOverride = "reallife_career_rota_take".Localize(),
                OnConfirm = G(() =>
                {
                    CareerDirector career = Career;
                    if (career == null || !career.CanSignOn(at, out _, out _))
                    {
                        return PhoneContacts.Closing("reallife_career_no_off");
                    }
                    career.SignOn(at, rota, false);
                    return PhoneContacts.Closing(employer.Welcome, new { rota = rota.NameKey.GetLocalization() });
                }),
                SecondOptionTextOverride = "reallife_career_rota_other",
                OnSecondOption = G(() => Rota(at, index + 1)),
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static string NextShift()
        {
            int day = TimeHelper.CurrentDay;
            int next = CareerState.NextShiftDay(day);
            if (next < 0)
            {
                return "reallife_career_next_none".GetLocalization();
            }
            string time = CareerClock.SpanWithWindow(CareerState.RotaHour);   // start to end, when clocking on opens and from when it is late
            return next == day
                ? "reallife_career_next_today".GetLocalization().Replace("{time}", time)
                : "reallife_career_next_day".GetLocalization().Replace("{time}", time).Replace("{day}", DayNames[(next - 1) % 7].GetLocalization());
        }

        private static string RotaName()
        {
            CareerRota[] rotas = CareerCatalogue.Rotas;
            for (int i = 0; i < rotas.Length; i++)
            {
                if (rotas[i].Days == CareerState.RotaDays && rotas[i].Hour == CareerState.RotaHour)
                {
                    return rotas[i].NameKey.GetLocalization();
                }
            }
            return CareerClock.Span(CareerState.RotaHour);
        }

        private static DialogEntry Staff(bool inPerson)
        {
            Employer employer = CareerState.Employer;
            CareerDirector career = Career;
            float rate = JobDirector.PayScale();   // the wage as it will be paid, owners' cut included
            string note = CareerState.ReviewDue
                ? (career.CanReview(inPerson) ? "reallife_career_note_review_here".GetLocalization()
                    : CareerOfficeRules.Key("reallife_career_note_review", career.OfficeAddress(CareerState.EmployerIndex)).GetLocalization()
                        .Replace("{address}", career.OfficeAddress(CareerState.EmployerIndex) ?? string.Empty))
                : CareerState.OnNotice
                    ? "reallife_career_note_notice".GetLocalization().Replace("{left}", CareerState.NoticeShiftsLeft.ToString())
                    : string.Empty;
            var entry = new DialogEntry
            {
                headerKey = inPerson ? employer.BossKey : null,
                messageData = "reallife_career_status".Localize(new
                {
                    employer = employer.NameKey.GetLocalization(),
                    boss = employer.BossKey.GetLocalization(),
                    title = CareerState.CurrentRank.TitleKey.GetLocalization(),
                    wage = (CareerState.Hourly * rate).ToCurrencyFormat(),
                    rota = RotaName(),
                    next = NextShift(),
                    marks = CareerState.Marks.ToString(),
                    most = CareerCatalogue.MarksToDismiss.ToString(),
                    note
                }),
                SecondOptionTextOverride = "reallife_career_more",
                OnSecondOption = G(() => Other(inPerson)),
                OnCancel = PhoneContacts.HangUp
            };
            if (career.CanReview(inPerson))
            {
                entry.ConfirmTextOverride = "reallife_career_review_start".Localize();
                entry.OnConfirm = G(() => ReviewOpen(employer));
            }
            else if (career.ClockOnRefusal() == null)
            {
                entry.ConfirmTextOverride = "reallife_career_clock_on".Localize();
                entry.OnConfirm = G(ClockOn);
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_career_cv".Localize();
                entry.OnConfirm = G(() => Cv(0));
            }
            return entry;
        }

        private static DialogEntry ClockOn()
        {
            CareerDirector career = Career;
            if (career == null)
            {
                return PhoneContacts.Closing("reallife_career_no_off");
            }
            // Asked again now: the hour may have turned while they talked.
            return career.ClockOn(out string reason)
                ? PhoneContacts.Closing(CareerState.Employer != null && CareerState.Employer.Job == JobCatalogue.Vendor && CareerState.CurrentRank.Work == CareerWork.Gig
                    ? "reallife_career_clocked_on_cart" : "reallife_career_clocked_on_time", new { time = CareerClock.Span(CareerState.RotaHour) })
                : PhoneContacts.Closing(reason ?? "reallife_job_no_now", new
                {
                    time = CareerClock.Span(CareerState.RotaHour),
                    address = career.OfficeAddress(CareerState.EmployerIndex) ?? string.Empty
                });
        }

        private static DialogEntry Other(bool inPerson)
        {
            bool shiftToday = CareerState.Employed && CareerState.ShiftOpen(TimeHelper.CurrentDay);
            var entry = new DialogEntry
            {
                messageData = "reallife_career_other".Localize(),
                SecondOptionTextOverride = "reallife_career_more",
                OnSecondOption = G(() => Other2(inPerson)),
                OnCancel = PhoneContacts.HangUp
            };
            if (shiftToday)
            {
                entry.ConfirmTextOverride = "reallife_career_sick".Localize();
                entry.OnConfirm = G(Sick);
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_career_gig_ask".Localize();
                entry.OnConfirm = G(Gig);
            }
            return entry;
        }

        private static DialogEntry Other2(bool inPerson)
        {
            var entry = new DialogEntry
            {
                messageData = "reallife_career_other_2".Localize(),
                ConfirmTextOverride = "reallife_career_cv".Localize(),
                OnConfirm = G(() => Cv(0)),
                OnCancel = PhoneContacts.HangUp
            };
            entry.SecondOptionTextOverride = "reallife_career_more";
            entry.OnSecondOption = G(() => OfficeOffered() ? Office() : Other3());
            return entry;
        }

        // The office of the job the player has, when this city has a board for it.
        private static bool OfficeOffered()
        {
            CareerDirector career = Career;
            return career != null && CareerState.Employed && CareerOfficeRules.Has(career.OfficeAddress(CareerState.EmployerIndex));
        }

        // A page of "something else": where the employer's office is, and a button that puts the game's destination marker on it.
        private static DialogEntry Office()
        {
            CareerDirector career = Career;
            string address = career != null && CareerState.Employed ? career.OfficeAddress(CareerState.EmployerIndex) : null;
            if (!CareerOfficeRules.Has(address))
            {
                return Other3();
            }
            return new DialogEntry
            {
                messageData = "reallife_career_office_page".Localize(new { employer = CareerState.Employer.NameKey.GetLocalization(), address }),
                ConfirmTextOverride = "reallife_career_office_show".Localize(),
                OnConfirm = G(() => ShowOffice(CareerState.EmployerIndex)),
                SecondOptionTextOverride = "reallife_career_more",
                OnSecondOption = G(() => Other3()),
                OnCancel = PhoneContacts.HangUp
            };
        }

        // Puts the game's destination marker on an employer's board, and says where it is.
        private static DialogEntry ShowOffice(int employer)
        {
            CareerDirector career = Career;
            string address = career != null && employer >= 0 ? career.OfficeAddress(employer) : null;
            if (!CareerOfficeRules.Has(address))
            {
                return PhoneContacts.Closing("reallife_career_office_none");
            }
            string name = CareerCatalogue.All[employer].NameKey.GetLocalization();
            return career.ShowOffice(employer)
                ? PhoneContacts.Closing("reallife_career_office_shown", new { employer = name, address })
                : PhoneContacts.Closing("reallife_career_office_busy", new { employer = name, address });
        }

        // Last page of "something else": a job with another employer, and leaving the one the player has.
        private static DialogEntry Other3()
        {
            var entry = new DialogEntry
            {
                messageData = "reallife_career_other_3".Localize(),
                ConfirmTextOverride = "reallife_career_apply".Localize(),
                OnConfirm = G(() => Apply(0)),
                OnCancel = PhoneContacts.HangUp
            };
            if (CareerState.Employed)
            {
                entry.SecondOptionTextOverride = "reallife_career_leaving";
                entry.OnSecondOption = G(Leaving);
            }
            return entry;
        }

        private static DialogEntry Sick()
        {
            int day = TimeHelper.CurrentDay;
            if (!CareerState.Employed || !CareerState.ShiftOpen(day))
            {
                return PhoneContacts.Closing("reallife_career_no_shift_today");
            }
            if (!CareerLinks.SickNote(day))
            {
                return PhoneContacts.Closing("reallife_career_sick_no");
            }
            CareerState.Excused(day);
            JobDirector.Logger?.Info("Careers: called in sick for day " + day + ".");
            return PhoneContacts.Closing("reallife_career_sick_ok");
        }

        private static DialogEntry Leaving()
        {
            if (!CareerState.Employed)
            {
                return PhoneContacts.Closing("reallife_career_no_employer");
            }
            Employer employer = CareerState.Employer;
            if (CareerState.OnNotice)
            {
                return new DialogEntry
                {
                    messageData = "reallife_career_on_notice".Localize(new { left = CareerState.NoticeShiftsLeft.ToString() }),
                    ConfirmTextOverride = "reallife_career_walk_out".Localize(),
                    OnConfirm = G(() => WalkOut(employer)),
                    OnCancel = PhoneContacts.HangUp
                };
            }
            return new DialogEntry
            {
                messageData = "reallife_career_leaving_ask".Localize(new
                {
                    shifts = CareerCatalogue.NoticeShifts.ToString(),
                    days = CareerCatalogue.BanDays.ToString()
                }),
                ConfirmTextOverride = "reallife_career_give_notice".Localize(),
                OnConfirm = G(() =>
                {
                    Career?.GiveNotice();
                    return PhoneContacts.Closing(employer.Notice, new { shifts = CareerCatalogue.NoticeShifts.ToString() });
                }),
                SecondOptionTextOverride = "reallife_career_walk_out",
                OnSecondOption = G(() => WalkOut(employer)),
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry WalkOut(Employer employer)
        {
            Career?.WalkOut();
            ModState.AddModifier(ModState.MoodDown);
            return PhoneContacts.Closing(employer.Walkout);
        }

        private static DialogEntry ReviewOpen(Employer employer)
        {
            return new DialogEntry
            {
                headerKey = employer.BossKey,
                messageData = employer.Review.Localize(),
                ConfirmTextOverride = "reallife_career_review_go_on".Localize(),
                OnConfirm = G(() => ReviewNumbers(employer)),
                OnCancel = PhoneContacts.HangUp   // put off: it stays due
            };
        }

        private static DialogEntry ReviewNumbers(Employer employer)
        {
            int due = CareerState.OnTime + CareerState.Late + CareerState.NoShow;
            return new DialogEntry
            {
                headerKey = employer.BossKey,
                messageData = "reallife_career_review_numbers".Localize(new
                {
                    shifts = CareerState.Shifts.ToString(),
                    ontime = CareerState.OnTime.ToString(),
                    due = due.ToString(),
                    late = CareerState.Late.ToString(),
                    missed = CareerState.NoShow.ToString(),
                    tips = ((float)CareerState.Tips).ToCurrencyFormat(),
                    complaints = CareerState.Complaints.ToString(),
                    crashes = CareerState.Crashes.ToString(),
                    stops = CareerState.Stops.ToString()
                }),
                ConfirmTextOverride = "reallife_career_review_verdict".Localize(),
                OnConfirm = G(() => Verdict(employer)),
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Verdict(Employer employer)
        {
            CareerDirector career = Career;
            if (career == null || !CareerState.Employed || !CareerState.ReviewDue)
            {
                return PhoneContacts.Closing("reallife_career_no_off");
            }
            ReviewOutcome outcome = career.HoldReview(out float score, out _, out _, out _, out _, out _, out _);
            float rate = JobDirector.PayScale();   // the wage as it will be paid, owners' cut included
            string key;
            switch (outcome)
            {
                case ReviewOutcome.Promoted: key = "reallife_career_review_promoted"; break;
                case ReviewOutcome.Raise: key = "reallife_career_review_raise"; break;
                case ReviewOutcome.Warning: key = "reallife_career_review_warning"; break;
                case ReviewOutcome.Dismissed: key = "reallife_career_review_dismissed"; break;
                default: key = "reallife_career_review_steady"; break;
            }
            return PhoneContacts.Closing(key, new
            {
                score = Mathf.RoundToInt(score).ToString(),
                title = CareerState.Employed ? CareerState.CurrentRank.TitleKey.GetLocalization() : string.Empty,
                wage = (CareerState.Hourly * rate).ToCurrencyFormat(),
                marks = CareerState.Marks.ToString(),
                most = CareerCatalogue.MarksToDismiss.ToString(),
                days = CareerCatalogue.BanDays.ToString()
            });
        }

        private static DialogEntry Freelance()
        {
            return new DialogEntry
            {
                messageData = "reallife_career_freelance".Localize(new
                {
                    references = CareerState.ReferenceScore(JobState.HasReference).ToString()
                }),
                ConfirmTextOverride = "reallife_career_apply".Localize(),
                OnConfirm = G(() => Apply(0)),
                SecondOptionTextOverride = "reallife_career_more",
                OnSecondOption = G(() => Other(false)),
                OnCancel = PhoneContacts.HangUp
            };
        }

        // Applying by phone: the six employers in turn. Asking one for work goes to the same page as speaking to the boss
        // (Hire), so the checks, the rota and the sign-on are one code.
        private static DialogEntry Apply(int index)
        {
            Employer[] all = CareerCatalogue.All;
            int at = ((index % all.Length) + all.Length) % all.Length;
            Employer employer = all[at];
            return new DialogEntry
            {
                messageData = "reallife_career_apply_pick".Localize(new
                {
                    employer = employer.NameKey.GetLocalization(),
                    boss = employer.BossKey.GetLocalization(),
                    number = (at + 1).ToString(),
                    count = all.Length.ToString()
                }),
                ConfirmTextOverride = "reallife_career_apply_ask".Localize(),
                OnConfirm = G(() => Hire(at)),
                SecondOptionTextOverride = "reallife_career_apply_next",
                OnSecondOption = G(() => Apply(at + 1)),
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static DialogEntry Gig()
        {
            CareerDirector career = Career;
            if (career == null)
            {
                return PhoneContacts.Closing("reallife_career_no_off");
            }
            if (career.GigPending)
            {
                return PhoneContacts.Closing("reallife_career_gig_pending", new { venue = career.GigVenue });
            }
            string refusal = career.GigRefusal();
            if (refusal != null)
            {
                return PhoneContacts.Closing(refusal, new { level = CareerCatalogue.GigNeedsLevel.ToString(), days = JobCatalogue.SackedDays.ToString() });
            }
            return new DialogEntry
            {
                messageData = "reallife_career_gig_offer".Localize(new { fee = ((float)career.GigFee).ToCurrencyFormat() }),
                ConfirmTextOverride = "reallife_career_gig_book".Localize(),
                OnConfirm = G(() =>
                {
                    CareerDirector now = Career;
                    return now != null && now.GigRefusal() == null && now.BookGig(false)
                        ? PhoneContacts.Closing("reallife_career_gig_booked", new { venue = now.GigVenue, fee = ((float)now.GigFee).ToCurrencyFormat() })
                        : PhoneContacts.Closing("reallife_career_gig_none");
                }),
                OnCancel = PhoneContacts.HangUp
            };
        }

        private static string ReferenceName(CareerReference reference)
        {
            return (reference == CareerReference.Good ? "reallife_career_ref_good"
                : reference == CareerReference.MarkedDown ? "reallife_career_ref_bad" : "reallife_career_ref_none").GetLocalization();
        }

        private static DialogEntry Cv(int page)
        {
            if (page == 0)
            {
                Text.Length = 0;
                IReadOnlyList<CareerState.Spell> history = CareerState.History;
                string line = "reallife_career_cv_line".GetLocalization();
                for (int i = history.Count - 1; i >= 0 && i >= history.Count - 4; i--)
                {
                    CareerState.Spell spell = history[i];
                    Employer employer = CareerCatalogue.All[spell.Employer];
                    Text.Append(line.Replace("{employer}", employer.NameKey.GetLocalization())
                        .Replace("{title}", employer.Ranks[spell.Rank].TitleKey.GetLocalization())
                        .Replace("{from}", spell.From.ToString()).Replace("{to}", spell.To.ToString())
                        .Replace("{shifts}", spell.Shifts.ToString()).Replace("{reference}", ReferenceName(spell.Reference))).Append('\n');
                }
                if (history.Count == 0)
                {
                    Text.Append("reallife_career_cv_no_history".GetLocalization());
                }
                string current = CareerState.Employed
                    ? "reallife_career_cv_current".GetLocalization().Replace("{employer}", CareerState.Employer.NameKey.GetLocalization())
                        .Replace("{title}", CareerState.CurrentRank.TitleKey.GetLocalization()).Replace("{day}", CareerState.HiredDay.ToString())
                    : "reallife_career_cv_freelance".GetLocalization();
                return new DialogEntry
                {
                    messageData = "reallife_career_cv_1".Localize(new
                    {
                        current,
                        history = Text.ToString(),
                        shifts = CareerState.TotalShifts.ToString(),
                        earned = ((float)CareerState.TotalEarned).ToCurrencyFormat(),
                        references = CareerState.ReferenceScore(JobState.HasReference).ToString()
                    }),
                    ConfirmTextOverride = "reallife_career_cv_more".Localize(),
                    OnConfirm = G(() => Cv(1)),
                    OnCancel = PhoneContacts.HangUp
                };
            }

            Text.Length = 0;
            string level = "reallife_career_cv_level".GetLocalization();
            JobDef[] jobs = JobCatalogue.All;
            for (int i = 0; i < jobs.Length; i++)
            {
                if (JobState.Of(jobs[i]).Units > 0)
                {
                    Text.Append(level.Replace("{job}", jobs[i].NameKey.GetLocalization()).Replace("{level}", JobState.Level(jobs[i]).ToString())).Append("  ");
                }
            }
            if (Text.Length == 0)
            {
                Text.Append("reallife_career_cv_no_gigs".GetLocalization());
            }
            string law = CareerLinks.RecordLine(out bool clean);
            if (string.IsNullOrEmpty(law))
            {
                law = (JobState.CleanRecord(TimeHelper.CurrentDay) && clean ? "reallife_career_cv_record_clean" : "reallife_career_cv_record_stop").GetLocalization();
            }
            return PhoneContacts.Closing("reallife_career_cv_2", new
            {
                gigs = Text.ToString(),
                letter = (JobState.HasReference ? "reallife_career_cv_letter_yes" : "reallife_career_cv_letter_no").GetLocalization(),
                cashier = CareerState.CashierHours.ToString(),
                record = law
            });
        }
    }
}
