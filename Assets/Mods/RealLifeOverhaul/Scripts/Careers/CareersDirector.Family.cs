using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The family and the others: a grown child of the family's offered a place at the university (the college fund pays
    /// what it can, the player the rest) and graduating with a class of their own; tutoring sessions given from home; the
    /// player's own training academy (Careers/AcademyType), whose staff teach the city's students for fees each day and
    /// whose courses the player's professional staff take at half the fee.
    /// </summary>
    public sealed partial class CareersDirector
    {
        public const int TabFamily = 4;
        private const string TutorKey = "reallife_transaction_study_tutoring";
        private const string ChildKey = "reallife_transaction_study_child";
        private const string AcademyKey = "reallife_transaction_study_academy";

        private readonly List<ChildView> _kids = new List<ChildView>(4);
        private readonly List<BuildingRegistration> _academies = new List<BuildingRegistration>(4);

        public static bool FamilyOn => Settings.CareersStudy && Settings.CareersFamily;

        // ---------------------------------------------------------------- the family's children

        private void FamilyDay(int day)
        {
            if (!FamilyOn)
            {
                return;
            }
            _kids.Clear();
            try
            {
                CareersBridges.Children?.Invoke(_kids);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            for (int i = 0; i < _kids.Count; i++)
            {
                ChildView k = _kids[i];
                if (!k.Grown || ChildOf(k.Name) != null || _study.Children.Count >= 6)
                {
                    continue;
                }
                int pick = TechRandom.Range(StudySeed, 8801, TechRandom.Of(k.Name), 0, 0, 13);
                var c = new ChildStudy { Name = k.Name, Course = StudyCatalogue.ComputerScience + pick, Stage = (int)ChildStep.Offered, Start = day };
                _study.Children.Add(c);
                Tell(NotificationType.Info, "reallife_family_study_offer", Data("child", k.Name, "course", CareersText.Course(c.Course),
                    "cost", CareersText.Money(Prices.At(StudyRules.ChildCost(StudyCatalogue.Find(c.Course), k.Fund)))), TabFamily, null);
            }
            for (int i = 0; i < _study.Children.Count; i++)
            {
                ChildStudy c = _study.Children[i];
                if (c.Stage != (int)ChildStep.Studying || day < c.End)
                {
                    continue;
                }
                ChildView k = KidOf(c.Name);
                c.Grade = StudyRules.ChildGrade(k.Closeness, k.School == 2, TechRandom.Range(StudySeed, 8802, TechRandom.Of(c.Name), c.Start, -15, 15));
                c.Stage = (int)ChildStep.Graduated;
                CareersGame.Mood(ModState.MoodUpBig);
                string degree = CareersText.T("reallife_study_degree_with", "course", CareersText.Course(c.Course), "grade", CareersText.Grade(StudyCatalogue.Find(c.Course), c.Grade));
                Tell(NotificationType.Success, "reallife_family_study_graduated", Data("child", c.Name, "degree", degree), TabFamily, null);
                Story("reallife_family_study_news", "child", c.Name, "degree", degree);
            }
            AcademyDay(day);
        }

        private ChildView KidOf(string name)
        {
            for (int i = 0; i < _kids.Count; i++)
            {
                if (_kids[i].Name == name)
                {
                    return _kids[i];
                }
            }
            return new ChildView { Name = name, Closeness = 60f };
        }

        internal ChildStudy ChildOf(string name)
        {
            for (int i = 0; _study != null && i < _study.Children.Count; i++)
            {
                if (_study.Children[i].Name == name)
                {
                    return _study.Children[i];
                }
            }
            return null;
        }

        /// <summary>The family's children as the Campus app shows them (read when it opens).</summary>
        internal IReadOnlyList<ChildView> Kids()
        {
            _kids.Clear();
            try
            {
                CareersBridges.Children?.Invoke(_kids);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return _kids;
        }

        /// <summary>The player pays for a grown child's degree (what the college fund does not cover). Null if done, else a reason key.</summary>
        public string PayChild(string name)
        {
            ChildStudy c = ChildOf(name);
            if (!FamilyOn || c == null || c.Stage != (int)ChildStep.Offered)
            {
                return "reallife_family_why_no_place";
            }
            Kids();
            ChildView k = KidOf(name);
            int cost = Prices.At(StudyRules.ChildCost(StudyCatalogue.Find(c.Course), k.Fund));
            if (!CareersGame.Charge(cost, ChildKey, name))
            {
                return "reallife_study_why_money";
            }
            c.Paid = cost;
            c.Stage = (int)ChildStep.Studying;
            c.Start = Save.Day;
            c.End = Save.Day + StudyRules.Length(StudyCatalogue.Find(c.Course), StudyMode.FullTime);
            Tell(NotificationType.Success, "reallife_family_study_started", Data("child", name, "course", CareersText.Course(c.Course), "day", CareersText.Day(c.End)), TabFamily, null);
            Changed();
            return null;
        }

        public string DeclineChild(string name)
        {
            ChildStudy c = ChildOf(name);
            if (c == null || c.Stage != (int)ChildStep.Offered)
            {
                return "reallife_family_why_no_place";
            }
            c.Stage = (int)ChildStep.Declined;
            Changed();
            return null;
        }

        // ---------------------------------------------------------------- tutoring

        public string CanTutor(out int skill)
        {
            skill = _study != null ? StudyRules.TutorSkill(_study) : Skills.Teaching;
            if (!FamilyOn || _study == null)
            {
                return "reallife_family_why_off";
            }
            if (StudyRules.TutorRate(StudyRules.Points(_study, skill)) <= 0)
            {
                return "reallife_family_why_tutor_skill";
            }
            int day = Save.Day, hour = Save.Hour;
            if (_study.TutorDay == day && _study.TutorToday >= StudyCatalogue.TutorPerDay)
            {
                return "reallife_family_why_tutor_enough";
            }
            if (hour < StudyCatalogue.TutorFrom || hour >= StudyCatalogue.TutorTo)
            {
                return "reallife_family_why_tutor_hours";
            }
            if (!CareersGame.AtHome())
            {
                return "reallife_study_why_not_home";
            }
            return StudyRules.NoNetWhy(CareersBridges.HomeNetNow());   // (tutoring has no hour it must be given in: nothing to excuse)
        }

        public string Tutor()
        {
            int skill;
            string why = CanTutor(out skill);
            if (why != null)
            {
                return why;
            }
            int rate = Prices.At(StudyRules.TutorRate(StudyRules.Points(_study, skill)));
            return Offer(new Sitting(this, SittingKind.Tutoring, skill, StudyCatalogue.TutorMinutes, CareersText.T("reallife_family_skip_tutor"), true,
                CareersText.T("reallife_family_skip_subject", "subject", CareersText.Skill(skill)), CareersText.T("reallife_work_skip_pay", "pay", CareersText.Rate(rate))));
        }

        private void TutorEnded(Sitting s, int minutes, bool whole)
        {
            if (!whole || _study == null)
            {
                return;
            }
            int day = Save.Day;
            if (_study.TutorDay != day)
            {
                _study.TutorDay = day;
                _study.TutorToday = 0;
            }
            _study.TutorToday++;
            _study.Taught += minutes;
            int rate = Prices.At(StudyRules.TutorRate(StudyRules.Points(_study, s.Of)));
            float pay = rate * minutes / 60f / 100f;
            CareersGame.Pay(pay, TutorKey, null, CareersText.Skill(s.Of));
            StudyRules.Learn(_study, Skills.Teaching, StudyCatalogue.WorkLearn * 2 * (100 - StudyRules.Points(_study, Skills.Teaching)) / 100, day);
            StudyRules.Use(_study, s.Of, day);
            Tell(NotificationType.Success, "reallife_family_note_tutored", Data("amount", CareersText.Money((long)Math.Round(pay))), TabSkills, null);
        }

        // ---------------------------------------------------------------- the player's own academy

        /// <summary>The player's academies (one pass over their businesses).</summary>
        private void Academies(List<BuildingRegistration> into)
        {
            into.Clear();
            TalentGame.Businesses(_mine);
            _mineDay = Save != null ? Save.Day : -1;
            for (int i = 0; i < _mine.Count; i++)
            {
                if (AcademyType.Is(_mine[i].businessTypeName))
                {
                    into.Add(_mine[i]);
                }
            }
        }

        /// <summary>True while the player has an academy of their own: their professional staff take courses at half the fee.</summary>
        public bool HasAcademy()
        {
            if (!FamilyOn || Save == null)
            {
                return false;
            }
            if (_mineDay != Save.Day)
            {
                _mineDay = Save.Day;
                TalentGame.Businesses(_mine);
            }
            for (int i = 0; i < _mine.Count; i++)
            {
                if (AcademyType.Is(_mine[i].businessTypeName))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A day of each academy: its staff teach the students who come, and the fees are its takings.</summary>
        private void AcademyDay(int day)
        {
            Academies(_academies);
            for (int i = 0; i < _academies.Count; i++)
            {
                BuildingRegistration reg = _academies[i];
                int staff = Staff(reg);
                int students = StudyRules.AcademyStudents(staff, Council);
                long cents = (long)students * Prices.At(StudyCatalogue.AcademyFeeCents);
                if (cents > 0)
                {
                    GameManager.ChangeMoneySafe(cents / 100f, new TransactionInfo(AcademyKey, new Dictionary<string, string>(1) { { "name", TalentGame.NameOf(reg) } }), day, reg.Address, true);
                }
                if (Verbose)
                {
                    Logger?.Info("Careers check (academy " + TalentGame.NameOf(reg) + ", day " + day + "): " + staff + " staff, " + students + " students, " + cents + " cents: PASS");
                }
            }
        }

        private static int Staff(BuildingRegistration reg)
        {
            List<EmployeeInstance> all = SaveGameManager.Current != null ? SaveGameManager.Current.EmployeeInstances : null;
            int n = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                n += e != null && !e.IsCandidate && e.assignedAddress != null && e.assignedAddress == reg.Address ? 1 : 0;
            }
            return n;
        }

    }
}
