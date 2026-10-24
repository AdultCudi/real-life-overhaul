using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Current enrollment quotes on detached candidate records, shared by the live owner and offline fixtures.
    /// </summary>
    public static class EducationCompanion
    {
        /// <summary>
        /// Every course's quotes and enrolment, from the study rules on detached records. Read only during a capture,
        /// nothing per frame.
        /// </summary>
        public static CompanionModule Capture(StudyFile study, int day, int level, int cash, int council,
            bool loanOn, Func<CourseDef, bool> offered)
        {
            var rows = new List<object>();
            foreach (CourseDef course in StudyCatalogue.All)
            {
                Enrolment current = study.Live(course.Id);
                var quotes = new List<object>();
                for (int mode = 0; mode <= 2; mode++)
                {
                    if (mode == (int)StudyMode.Distance && !course.Distance)
                    {
                        continue;
                    }
                    for (int loan = 0; loan <= 1; loan++)
                    {
                        if (loan == 1 && (!course.Loanable || !loanOn))
                        {
                            continue;
                        }
                        bool available = offered(course);
                        EnrolRefusal refusal = StudyRules.CanEnrol(study, course, (StudyMode)mode);
                        if (refusal == EnrolRefusal.Passed && course.Id == StudyCatalogue.DrivingRefresher)
                        {
                            refusal = EnrolRefusal.None;
                        }
                        int grant = StudyRules.Grant(study, course, cash, level, council);
                        var candidate = new Enrolment { Course = course.Id, Mode = mode, Start = day,
                            Grant = grant, OnLoan = loan == 1 };
                        int due = PriceRules.At(StudyRules.InstalmentDue(candidate), level);
                        string reason = !available ? "not-offered" : refusal != EnrolRefusal.None
                            ? refusal.ToString().ToLowerInvariant() : loan == 1
                            ? (StudyRules.LoanRoom(study, due, level) ? null : "loan-full")
                            : (cash >= due ? null : "cash-insufficient");
                        bool already = current != null || study.Holds(course.Id) && course.Id != StudyCatalogue.DrivingRefresher;
                        var installments = new List<object>();
                        long total = 0;
                        int parts = StudyRules.Instalments(course);
                        for (int part = 0; part < parts; part++)
                        {
                            candidate.Instalments = part;
                            candidate.Seen = course.Sessions * part / parts;
                            long amount = (long)PriceRules.At(StudyRules.InstalmentDue(candidate), level) * 100;
                            total += amount;
                            installments.Add(CompanionSnapshot.Row("index", part, "afterSessions", candidate.Seen, "amountCents", amount));
                        }
                        quotes.Add(CompanionSnapshot.Row("mode", mode, "loan", loan == 1,
                            "quoteStatus", reason == null ? "ok" : "ineligible", "reason", reason,
                            "feeNowCents", already ? 0 : (long)due * 100,
                            "immediateCashCents", already || loan == 1 ? 0 : (long)due * 100,
                            "totalFeeCents", already ? 0 : total, "installmentsTotal", parts,
                            "installments", installments, "futurePriceBasis", "captured-level",
                            "grantCents", (long)(PriceRules.At(course.Fee, level) - PriceRules.At(StudyRules.Fee(course, grant), level)) * 100,
                            "grantThousandths", grant, "retakeCents", (long)PriceRules.At(StudyRules.RetakeFee(course), level) * 100,
                            "daysMask", StudyRules.Days(course, (StudyMode)mode), "startHour", StudyRules.Hour(course, (StudyMode)mode),
                            "approximateDurationDays", StudyRules.Length(course, (StudyMode)mode)));
                    }
                }
                rows.Add(CompanionSnapshot.Row("courseId", course.Id, "offered", offered(course),
                    "qualified", study.Holds(course.Id), "prerequisitesMet", StudyRules.Qualified(study, course),
                    "enrollment", current == null ? null : CompanionSnapshot.Row("status", current.At.ToString().ToLowerInvariant(),
                        "paidCents", (long)current.Fee * 100, "loanCents", (long)current.Loan * 100,
                        "onLoan", current.OnLoan, "installmentsPaid", current.Instalments,
                        "installmentsTotal", StudyRules.Instalments(course), "owedCents", (long)(current.FeeLate ? PriceRules.At(StudyRules.InstalmentDue(current), level) : 0) * 100,
                        "startDay", current.Start, "mode", current.Mode), "quotes", quotes,
                    "timetable", CompanionSnapshot.Row("daysMask", course.Days, "startHour", course.Hour,
                        "hours", course.Hours, "sessions", course.Sessions, "homework", course.Homework)));
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-education-v1",
                "quoteBasis", "captured-settings-cash-and-loan-room", "courses", rows,
                "loanOwedCents", study.LoanCents, "loanRateBasisPointsPerYear", StudyCatalogue.LoanRateBp,
                "completionGuarantee", false));
        }
    }
}
