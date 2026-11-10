using System;
using System.Collections.Generic;
using RealLifeOverhaul.Health;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Health pages do in the game: each is the director's own call, so a page can only reach what the director allows.</summary>
    public interface IHealthActions
    {
        /// <summary>Books the doctor's next hour.</summary>
        void Book();

        void CancelBooking();

        /// <summary>Calls in sick at the player's job for today.</summary>
        void CallInSick();

        /// <summary>Takes or changes the plan: a <see cref="Plan"/> number.</summary>
        void TakePlan(int plan);

        void CancelPlan();

        /// <summary>Pays the whole medical debt off.</summary>
        void PayDebt();
    }

    /// <summary>
    /// The Health pages: how the player is and the doctor's appointment, the plans, the bills with the debt and the sick leave, and the city. A page shows only what the rules
    /// allow, and every number comes from <see cref="HealthFacts"/>, which the director fills from the state and the game. Game-free.
    /// </summary>
    public static class HealthPages
    {
        public const int TabYou = 0, TabCover = 1, TabBills = 2, TabCity = 3;
        public const string PlanKey = "p:";

        public static void Fill(VentModel m, VentView v, HealthFacts f, IHealthActions act, VentWords w)
        {
            m.Title = w.T("reallife_hc_app", "Health");
            m.Icon = UiIcon.Heart;
            m.Prompts = VenPages.Prompts(w);
            var runs = new VentRuns();
            m.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Thermometer, Label = w.T("reallife_hc_tab_you", "You"), Count = f.Ready && f.Serious && !f.InHospital ? "1" : null },
                new UiNavEntry { Icon = UiIcon.Check, Label = w.T("reallife_hc_tab_cover", "Insurance") },
                new UiNavEntry { Icon = UiIcon.Coin, Label = w.T("reallife_hc_tab_bills", "Bills and leave") },
                new UiNavEntry { Icon = UiIcon.People, Label = w.T("reallife_hc_tab_city", "The city") }
            };
            m.Tab = Math.Max(0, Math.Min(3, v.Tab));
            if (!f.Ready)
            {
                m.Shape = VentShape.Off;
                m.OffTitle = w.T("reallife_hc_off_title", "Health is not running");
                m.OffBody = w.T("reallife_hc_off_body", "Load a save with Health switched on to see how you are, book the doctor and take a health plan.");
                m.Runs = runs.ToArray();
                return;
            }
            m.HeroFigures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_hc_fig_you", "How you are"), Value = Now(f, w) },
                new UiFigureModel { Label = w.T("reallife_hc_fig_plan", "Your plan"), Value = PlanWord(f, w) },
                new UiFigureModel { Label = w.T("reallife_hc_fig_owed", "Owed for care"), Value = w.Money(f.Owed) }
            };
            switch (m.Tab)
            {
                case TabCover: Cover(m, v, f, act, w, runs); break;
                case TabBills: Bills(m, f, act, w, runs); break;
                case TabCity: City(m, f, w); break;
                default: You(m, f, act, w, runs); break;
            }
            m.Runs = runs.ToArray();
        }

        private static string Now(HealthFacts f, VentWords w)
        {
            if (f.InHospital)
            {
                return w.T("reallife_hc_now_hospital", "In hospital");
            }
            return f.Kind == Condition.None ? w.T("reallife_hc_now_well", "Well") : w.T(HealthCatalogue.NameKey(f.Kind), ConditionEnglish(f.Kind));
        }

        private static string PlanWord(HealthFacts f, VentWords w)
        {
            if (f.Plan != Plan.None)
            {
                return w.T(HealthCatalogue.PlanKeys[(int)f.Plan], PlanEnglish(f.Plan));
            }
            return f.Employer != Plan.None ? w.T("reallife_hc_plan_employer", "Your employer's plan") : w.T(HealthCatalogue.PlanKeys[0], PlanEnglish(Plan.None));
        }

        private static void You(VentModel m, HealthFacts f, IHealthActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(8);
            left.Add(VentBlock.Section(w.T("reallife_hc_sec_you", "How you are")));
            string illness = w.T(HealthCatalogue.NameKey(f.Kind), ConditionEnglish(f.Kind));
            if (f.InHospital)
            {
                left.Add(VentBlock.Note(w.F("reallife_hc_you_hospital", "You are in hospital until the morning of day {day}.", "day", f.StayUntilDay.ToString())));
            }
            else if (f.Kind == Condition.None)
            {
                left.Add(VentBlock.Note(w.T("reallife_hc_you_well", "Nothing is wrong with you. Sleep, proper meals and exercise keep it that way.")));
            }
            else if (f.Serious)
            {
                left.Add(VentBlock.Note(w.F("reallife_hc_you_serious", "You have {illness} and it has turned serious. It will not clear up by itself. See the doctor at a hospital or clinic to be admitted.",
                    "illness", illness), UiRole.Warn));
            }
            else if (f.DaysLeft > 0)
            {
                left.Add(VentBlock.Note(w.F(f.Treated ? "reallife_hc_you_injury_treated" : "reallife_hc_you_injury", f.Treated ? "You have {illness}, seen to by the doctor. About {days} days to go."
                    : "You have {illness}. About {days} days to go. The doctor can halve that.", "illness", illness, "days", f.DaysLeft.ToString())));
            }
            else
            {
                left.Add(VentBlock.Note(w.F(f.Treated ? "reallife_hc_you_ill_treated" : "reallife_hc_you_ill", f.Treated ? "You have {illness} and are on a prescription. Rest and it will pass."
                    : "You have {illness}. Rest and see the doctor. Left alone with little sleep or food it can turn serious.", "illness", illness), f.Neglect > 0 ? UiRole.Warn : UiRole.None));
            }
            left.Add(VentBlock.Row(w.T("reallife_hc_row_wave", "The flu in the city"), w.T(WaveKey(f.Wave), WaveEnglish(f.Wave)), f.Wave >= 400 ? UiRole.Warn : UiRole.Text));
            left.Add(VentBlock.Row(w.T("reallife_hc_row_home", "Ill at home"), f.IllAtHome.ToString()));
            left.Add(VentBlock.Row(w.T("reallife_hc_row_living", "Your way of living"), w.T(LivingKey(f.Lifestyle), LivingEnglish(f.Lifestyle)),
                f.Lifestyle >= 1500 ? UiRole.Warn : f.Lifestyle <= 750 ? UiRole.Ok : UiRole.Text));
            var right = new List<VentBlock>(6);
            right.Add(VentBlock.Section(w.T("reallife_hc_sec_doctor", "The doctor")));
            if (f.Booked)
            {
                right.Add(VentBlock.Row(w.T("reallife_hc_row_booked", "Your appointment"),
                    w.F("reallife_hc_row_booked_value", "Day {day} at {hour}:00", "day", f.BookedDay.ToString(), "hour", f.BookedHour.ToString())));
                right.Add(VentBlock.Row(w.T("reallife_hc_row_where", "Where"), f.Clinic));
            }
            right.Add(VentBlock.Note(w.F("reallife_hc_doctor_how", "With an appointment you are seen within {wait} minutes of your hour. Go to the hospital or clinic and take a number at the doctor's desk."
                + " An appointment you do not keep costs {fee}.", "wait", HealthCatalogue.AppointmentWait.ToString(), "fee", w.Money(HealthRules.At(HealthCatalogue.MissedFee, f.Level)))));
            if (f.Booked)
            {
                right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_hc_cancel_booking", "Cancel the appointment"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.CancelBooking(); return true; }) }));
            }
            else
            {
                right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_hc_book", "Book an appointment"), Kind = UiButtonKind.Primary, Icon = UiIcon.Calendar, Key = UiAction.Alt,
                    Disabled = !f.CanBook || f.InHospital, Arg = runs.Add(() => { act.Book(); return true; }) }));
            }
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void Cover(VentModel m, VentView v, HealthFacts f, IHealthActions act, VentWords w, VentRuns runs)
        {
            int picked = f.Plan != Plan.None ? (int)f.Plan - 1 : 1;
            for (int i = 0; i < f.Plans.Length; i++)
            {
                picked = v.Pick == PlanKey + i ? i : picked;
            }
            var left = new List<VentBlock>(4);
            left.Add(VentBlock.Section(w.T("reallife_hc_sec_plans", "Health plans")));
            var items = new VentItem[f.Plans.Length];
            for (int i = 0; i < items.Length; i++)
            {
                int index = i, p = (int)f.Plans[i].Plan;
                items[i] = new VentItem
                {
                    Icon = UiIcon.Heart, Title = w.T(HealthCatalogue.PlanKeys[p], PlanEnglish(f.Plans[i].Plan)),
                    Sub = w.F("reallife_hc_plan_sub", "Hospital {hospital}, doctor {doctor}", "hospital", VentWords.Share(HealthCatalogue.HospitalShare[p] * 10),
                        "doctor", VentWords.Share(HealthCatalogue.DoctorShare[p] * 10)),
                    Chip = f.Plan == f.Plans[i].Plan ? w.T("reallife_hc_plan_yours", "Yours") : null, ChipTone = UiRole.Ok,
                    Amount = w.F("reallife_hc_plan_month", "{amount} a month", "amount", w.Money(f.Plans[i].Premium)),
                    Run = runs.Add(() => { v.Pick = PlanKey + index; return true; })
                };
            }
            left.Add(new VentBlock { Kind = VentBlockKind.Items, Items = items });
            if (f.Employer != Plan.None)
            {
                left.Add(VentBlock.Note(w.F("reallife_hc_employer_note", "Your employer gives you the {plan} plan while you work there. Whichever plan pays more of a bill pays it.",
                    "plan", w.T(HealthCatalogue.PlanKeys[(int)f.Employer], PlanEnglish(f.Employer))), UiRole.Ok));
            }
            PlanFact pick = f.Plans[picked];
            int k = (int)pick.Plan;
            var right = new List<VentBlock>(10);
            right.Add(VentBlock.Section(w.T(HealthCatalogue.PlanKeys[k], PlanEnglish(pick.Plan))));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_premium", "A month, at your age and record"), w.Money(pick.Premium)));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_hospital", "Of a hospital stay"), VentWords.Share(HealthCatalogue.HospitalShare[k] * 10)));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_excess", "You pay first, of each stay"), w.Money(HealthRules.At(HealthCatalogue.Excess[k], f.Level))));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_doctor", "Of the doctor and a prescription"), VentWords.Share(HealthCatalogue.DoctorShare[k] * 10)));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_cap", "Pays at most in a year"), w.Money(HealthRules.At(HealthCatalogue.YearCap[k], f.Level))));
            if (f.Plan == pick.Plan)
            {
                right.Add(VentBlock.Row(w.T("reallife_hc_row_next", "Next premium"), w.F("reallife_hc_row_next_value", "Day {day}", "day", f.NextDue.ToString()),
                    f.Missed > 0 ? UiRole.Warn : UiRole.Text));
            }
            right.Add(VentBlock.Note(w.F("reallife_hc_not_covered", "Not covered: anything that began before the plan or in its first {days} days, sick notes, the ambulance and a missed appointment."
                + " A plan never pays more than the bill.", "days", HealthCatalogue.WaitDays.ToString())));
            int plan = k;
            var actions = new List<UiActionModel>(2);
            if (f.Plan != Plan.None)
            {
                actions.Add(new UiActionModel { Label = w.T("reallife_hc_cancel_plan", "Cancel your plan"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close,
                    Arg = runs.Add(() => { act.CancelPlan(); return true; }) });
            }
            actions.Add(new UiActionModel { Label = w.T("reallife_hc_take_plan", "Take this plan"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt,
                Disabled = f.Plan == pick.Plan || pick.Premium > f.Spare, Arg = runs.Add(() => { act.TakePlan(plan); return true; }) });
            right.Add(VentBlock.Buttons(actions.ToArray()));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void Bills(VentModel m, HealthFacts f, IHealthActions act, VentWords w, VentRuns runs)
        {
            var left = new List<VentBlock>(9);
            left.Add(VentBlock.Section(w.T("reallife_hc_sec_bills", "Bills for care")));
            left.Add(VentBlock.Row(w.T("reallife_hc_row_bills", "Bills this year"), w.Money(f.BillsYear)));
            left.Add(VentBlock.Row(w.T("reallife_hc_row_claims", "Paid by a plan"), w.Money(f.ClaimsYear)));
            if (f.LastBillDay > 0)
            {
                left.Add(VentBlock.Row(w.T("reallife_hc_row_last", "The latest bill"),
                    w.F("reallife_hc_row_last_value", "{bill}, a plan paid {claim}", "bill", w.Money(f.LastBill), "claim", w.Money(f.LastClaim))));
            }
            left.Add(VentBlock.Row(w.T("reallife_hc_row_owed", "Owed for care"), w.Money(f.Owed), f.Owed > 0 ? UiRole.Warn : UiRole.Text));
            if (f.Owed > 0)
            {
                left.Add(VentBlock.Row(w.T("reallife_hc_row_per_day", "Taken each day"), w.Money(f.DebtPerDay), f.DebtLate > 0 ? UiRole.Warn : UiRole.Text));
            }
            left.Add(VentBlock.Note(w.F("reallife_hc_debt_how", "A bill you cannot pay on the day is owed with no interest and taken a little each day over {months} months."
                + " After {days} days running with nothing paid it goes to court.", "months", HealthCatalogue.DebtMonths.ToString(), "days", HealthCatalogue.DebtClaimAfter.ToString())));
            if (f.Owed > 0)
            {
                left.Add(VentBlock.Buttons(new UiActionModel { Label = w.T("reallife_hc_pay_off", "Pay it off now"), Kind = UiButtonKind.Primary, Icon = UiIcon.Coin, Key = UiAction.Alt,
                    Disabled = f.Owed > f.Spare, Arg = runs.Add(() => { act.PayDebt(); return true; }) }));
            }
            var right = new List<VentBlock>(9);
            right.Add(VentBlock.Section(w.T("reallife_hc_sec_leave", "Sick leave at your job")));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_paid_days", "Paid sick days this year"),
                w.F("reallife_hc_row_paid_days_value", "{used} of {days}", "used", f.PaidUsed.ToString(), "days", f.PaidDays.ToString())));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_sick_pay", "Sick pay this year"), w.Money(f.SickPay)));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_spell", "Days off sick running"),
                w.F("reallife_hc_row_spell_value", "{n} of {most} without a note", "n", f.SpellDays.ToString(), "most", HealthCatalogue.SelfCertDays.ToString()),
                f.SpellDays >= HealthCatalogue.SelfCertDays ? UiRole.Warn : UiRole.Text));
            if (f.NoteUntil > 0)
            {
                right.Add(VentBlock.Row(w.T("reallife_hc_row_note", "The hospital's note runs to"), w.F("reallife_hc_row_next_value", "Day {day}", "day", f.NoteUntil.ToString())));
            }
            if (f.CalledWell > 0)
            {
                right.Add(VentBlock.Row(w.T("reallife_hc_row_called", "Called in sick while well"),
                    w.F("reallife_hc_row_called_value", "{n}, seen through {seen}", "n", f.CalledWell.ToString(), "seen", f.Noticed.ToString()), UiRole.Warn));
            }
            right.Add(VentBlock.Note(w.F("reallife_hc_leave_how", "Ill, you are excused for {days} working days running. After that you need a doctor's note. A day off sick is paid while paid days are left."
                + " Call in sick while well and your employer may see through it.", "days", HealthCatalogue.SelfCertDays.ToString())));
            right.Add(VentBlock.Buttons(new UiActionModel { Label = w.T(f.CalledIn ? "reallife_hc_called_in" : "reallife_hc_call_in", f.CalledIn ? "Called in sick today" : "Call in sick today"),
                Kind = UiButtonKind.Secondary, Icon = UiIcon.Phone, Disabled = f.CalledIn || !f.ShiftOpen, Arg = runs.Add(() => { act.CallInSick(); return true; }) }));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        private static void City(VentModel m, HealthFacts f, VentWords w)
        {
            var left = new List<VentBlock>(9);
            left.Add(VentBlock.Section(w.T("reallife_hc_sec_city", "The city's people")));
            if (!f.People)
            {
                left.Add(VentBlock.Note(w.T(f.Session ? "reallife_hc_why_session" : "reallife_hc_why_people",
                    f.Session ? "In a Going Public session the city's people are the host's. Their health waits until you are out of it."
                    : "The city's people are not running. Switch on Living population to see how they are."), UiRole.Warn));
            }
            else
            {
                left.Add(VentBlock.Row(w.T("reallife_hc_row_ill_now", "Ill today"),
                    w.F("reallife_hc_row_ill_now_value", "{n} of {people}", "n", f.IllNow.ToString(), "people", f.CityPeople.ToString())));
                left.Add(VentBlock.Row(w.T("reallife_hc_row_wave", "The flu in the city"), w.T(WaveKey(f.Wave), WaveEnglish(f.Wave)), f.Wave >= 400 ? UiRole.Warn : UiRole.Text));
                left.Add(VentBlock.Row(w.T("reallife_hc_row_cases", "Fell ill this month"), f.Cases.ToString()));
                left.Add(VentBlock.Row(w.T("reallife_hc_row_serious", "Needed the hospital"), f.SeriousCases.ToString()));
                left.Add(VentBlock.Row(w.T("reallife_hc_row_lost", "Working days lost"), f.LostDays.ToString()));
                left.Add(VentBlock.Row(w.T("reallife_hc_row_city_bills", "Care paid for by households"), w.Money(f.CityBills)));
                left.Add(VentBlock.Row(w.T("reallife_hc_row_city_debt", "Put on household debts"), w.Money(f.CityDebt)));
            }
            var right = new List<VentBlock>(7);
            right.Add(VentBlock.Section(w.T("reallife_hc_sec_staff", "Your staff")));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_staff_off", "Off tomorrow with what is going round"), f.StaffOff.ToString()));
            right.Add(VentBlock.Row(w.T("reallife_hc_row_policy", "Your sick pay"), w.T(PolicyKey(f.SickPolicy), PolicyEnglish(f.SickPolicy))));
            right.Add(VentBlock.Note(w.T("reallife_hc_staff_how", "In the flu's weeks, and when colleagues are off, your staff fall ill more often. A health plan from your HR manager cuts that."
                + " With no sick pay they come in ill the first day and pass it on.")));
            right.Add(VentBlock.Section(w.T("reallife_hc_sec_rivals", "The rivals")));
            right.Add(VentBlock.Note(string.IsNullOrEmpty(f.RivalLine) ? w.T("reallife_hc_rivals_none", "None of the rivals is off sick.") : f.RivalLine));
            m.Left = left.ToArray();
            m.Right = right.ToArray();
        }

        public static string WaveKey(int wave) =>
            wave <= 0 ? "reallife_hc_wave_none" : wave < 400 ? "reallife_hc_wave_low" : wave < 900 ? "reallife_hc_wave_round" : "reallife_hc_wave_high";

        private static string WaveEnglish(int wave) => wave <= 0 ? "Quiet" : wave < 400 ? "A few cases" : wave < 900 ? "Going round" : "At its height";

        public static string LivingKey(int share) => share <= 750 ? "reallife_hc_living_good" : share < 1500 ? "reallife_hc_living_fair" : "reallife_hc_living_poor";

        private static string LivingEnglish(int share) => share <= 750 ? "Protecting you" : share < 1500 ? "Ordinary" : "Run down";

        public static string PolicyKey(int policy) => policy <= 0 ? "reallife_hc_policy_none" : policy == 1 ? "reallife_hc_policy_statutory" : "reallife_hc_policy_generous";

        private static string PolicyEnglish(int policy) => policy <= 0 ? "None" : policy == 1 ? "Half pay" : "Full pay";

        /// <summary>The English the samples and the checks read a condition's name in.</summary>
        public static string ConditionEnglish(Condition c)
        {
            switch (c)
            {
                case Condition.Cold: return "a cold";
                case Condition.Flu: return "the flu";
                case Condition.FoodPoisoning: return "food poisoning";
                case Condition.BackPain: return "back pain";
                case Condition.Exhaustion: return "exhaustion";
                case Condition.Sprain: return "a sprain";
                case Condition.BrokenArm: return "a broken arm";
                case Condition.Concussion: return "a concussion";
                default: return "nothing";
            }
        }

        /// <summary>The English of a plan's name.</summary>
        public static string PlanEnglish(Plan p) => p == Plan.Basic ? "Basic" : p == Plan.Standard ? "Standard" : p == Plan.Full ? "Full" : "No plan";
    }
}
