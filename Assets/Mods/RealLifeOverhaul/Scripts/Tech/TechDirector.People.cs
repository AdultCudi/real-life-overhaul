using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One professional with what was read from the game for them today. Nothing of this is saved.</summary>
    public sealed class Person
    {
        public Profile File;
        public EmployeeInstance Game;   // null for a professional on the mod's own payroll
        public BuildingRegistration At;
        public string Name = string.Empty, Business = string.Empty, Skill = string.Empty;
        public int MainPoints;
        public int HoursYesterday;
        public bool Mentored;
    }

    /// <summary>
    /// The people part: every professional of the player's has a file (Tech/People/TalentRules), made the first day
    /// they are seen and dropped when they are no longer the player's. Each game day it is worked through: learning,
    /// a mentor's help, morale and burnout (and the game's satisfaction a few points towards morale), a day off sick
    /// for the burnt out, reviews that fall due and the requests for a rise they make, a rival's approach, courses and
    /// exams that end, and notice that runs out (a lay-off is paid its severance and is the game's firing; whoever
    /// resigns or goes to a rival leaves as the game has an employee leave). On the hour, who is working is counted.
    /// </summary>
    public sealed partial class TechDirector
    {
        public const int TabTeam = 0;

        private readonly List<Person> _people = new List<Person>(64);
        private readonly List<EmployeeInstance> _employees = new List<EmployeeInstance>(64);
        private readonly Dictionary<string, int> _hours = new Dictionary<string, int>(64);
        private readonly Dictionary<string, int> _lastHours = new Dictionary<string, int>(64);
        private readonly Dictionary<string, int> _mentorRoom = new Dictionary<string, int>(32);
        private readonly int[] _wanted = new int[8];
        private int _hoursDay = -1, _lastHoursDay = -1;
        private int _layoffs, _layoffsDay = -1;
        private TalentFile _talent;
        private bool _peopleOn;
        private PhoneApp _rolesApp;
        private RolesApp _rolesView;
        private bool _rolesGaveUp;

        public bool PeopleRunning => _peopleOn;

        /// <summary>The people's files; null while the part is off.</summary>
        public TalentFile Talent => _peopleOn ? _talent : null;

        /// <summary>The player's professionals with what was read for each today, in the file's order.</summary>
        public IReadOnlyList<Person> People => _people;

        private int Seed => _talent != null ? _talent.Seed : 1;

        internal int TalentSeed => Seed;

        // ---------------------------------------------------------------- starting and stopping

        private void StartPeople()
        {
            GameInstance save = Save;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version;
            TalentFile file = TalentStore.Load(save.modData, out version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new TalentFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day };
            }
            _talent = file;
            _peopleOn = true;
            int made = ReadPeople(save.Day);
            Logger?.Info("Tech: professionals " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + save.Day + ": " + _people.Count + " professional(s)"
                         + (made > 0 ? ", " + made + " seen for the first time" : string.Empty) + ".");
        }

        private void StopPeople(bool keep, bool unloading)
        {
            if (!_peopleOn)
            {
                return;
            }
            if (keep && Save != null && _talent != null)
            {
                if (Save.modData == null)
                {
                    Save.modData = new Dictionary<string, string>();
                }
                WritePeople(Save.modData);
            }
            _peopleOn = false;
            _people.Clear();
            _hours.Clear();
            _lastHours.Clear();
            if (!unloading && _rolesApp != null)
            {
                _rolesApp.Remove();
                _rolesApp = null;
                _rolesView = null;
            }
        }

        private void WritePeople(Dictionary<string, string> data)
        {
            if (_talent != null)
            {
                data[TalentStore.Key] = TalentStore.Write(_talent);
            }
        }

        // ---------------------------------------------------------------- the game's employees and the files

        /// <summary>
        /// Each professional of the player's is tied to their file, a file made for one seen for the first time; the file
        /// of an employee who is no longer the player's is dropped. A professional of the mod's own payroll stays. Returns
        /// how many files were made.
        /// </summary>
        private int ReadPeople(int day)
        {
            _people.Clear();
            for (int i = 0; i < _talent.People.Count; i++)
            {
                _talent.People[i].Seen = _talent.People[i].Payroll;
            }
            TalentGame.Employees(_employees);
            int made = 0;
            for (int i = 0; i < _employees.Count; i++)
            {
                EmployeeInstance e = _employees[i];
                Profile p = _talent.Of(e.id);
                if (p == null)
                {
                    if (_talent.People.Count >= TalentStore.PeopleMost)
                    {
                        continue;
                    }
                    p = NewFile(e, day);
                    if (p == null)
                    {
                        continue;
                    }
                    _talent.People.Add(p);
                    made++;
                }
                p.Seen = true;
                _people.Add(Read(p, e));
            }
            for (int i = _talent.People.Count - 1; i >= 0; i--)
            {
                Profile p = _talent.People[i];
                if (!p.Seen)
                {
                    _talent.People.RemoveAt(i);
                }
                else if (p.Payroll)
                {
                    _people.Add(Read(p, null));
                }
            }
            return made;
        }

        private Person Read(Profile p, EmployeeInstance e)
        {
            var person = new Person { File = p, Game = e };
            if (e != null)
            {
                person.At = TalentGame.Business(e);
                person.Name = TalentGame.NameOf(e);
                person.Skill = TalentGame.Skill(e);
                person.MainPoints = TalentGame.MainPoints(e);
                p.Place = TalentGame.Place(person.At);
                p.GameWage = TalentGame.WageCents(e);
                p.WeekHours = TalentGame.WeekHours(e);
            }
            else
            {
                person.At = TalentGame.Find(p.Place);
                person.Name = p.Name;
                person.Skill = p.Def.GameSkill;
                person.MainPoints = p.Main / 1000;
                p.GameWage = p.Wage;
            }
            person.Business = person.At != null ? TalentGame.NameOf(person.At) : string.Empty;
            p.Going = TalentRules.Going(TalentGame.GameRate(p.Def.GameSkill, person.MainPoints), p.Def, Shortage(p.Def.Family), TalentRules.CertPayBp(p));
            return person;
        }

        /// <summary>A file for an employee of the game's seen for the first time: a role the business wants (else one of the skill's), the level their skill stands at, specialisms near it.</summary>
        private Profile NewFile(EmployeeInstance e, int day)
        {
            string skill = TalentGame.Skill(e);
            int main = TalentGame.MainPoints(e);
            BuildingRegistration at = TalentGame.Business(e);
            int wanted = WantedRoles(TalentGame.TypeOf(at), _wanted);
            int n = TechRandom.Of(e.id);
            int role = TalentRules.Pick(skill, _wanted, wanted, Seed, n);
            if (role < 0)
            {
                return null;
            }
            RoleDef r = TalentCatalogue.Role(role);
            Profile p = TalentRules.Make(Seed, n, role, TalentRules.LevelFor(main, r), day);
            p.Id = e.id;
            p.Main = 0;
            p.A = TalentCatalogue.Clamp(main + TechRandom.Range(Seed, n, 81, 0, -8, 8), 1, 100) * 1000;
            p.B = TalentCatalogue.Clamp(main - 6 + TechRandom.Range(Seed, n, 82, 0, -8, 8), 1, 100) * 1000;
            p.Joined = e.dayHired > 0 && e.dayHired <= day ? e.dayHired : day;
            p.LevelDay = p.Joined;
            p.ReviewDay = day + TechRandom.Range(Seed, n, 83, 0, 7, Math.Max(8, Year / TalentCatalogue.ReviewsAYear));
            p.Work = (int)Arrangement.OnSite;
            p.Record = false;   // (the game hired them without a check: what is not known is not held against them)
            return p;
        }

        /// <summary>
        /// The roles a business of a type wants first, in order: a web development agency its web designers and engineers,
        /// a graphic designer's its designers, a law firm its lawyers, headquarters its HR people and lawyers. The tech
        /// sector's own kinds of business add theirs. Returns how many.
        /// </summary>
        internal static int WantedRoles(string type, int[] into)
        {
            int n = 0;
            if (string.IsNullOrEmpty(type))
            {
                return 0;
            }
            if (type.EndsWith("webdevelopmentagency", StringComparison.Ordinal))
            {
                into[n++] = TalentCatalogue.WebDesigner;
                into[n++] = 1;   // front end
                into[n++] = 2;   // back end
                into[n++] = TalentCatalogue.AccountManager;
            }
            else if (type.EndsWith("graphicdesigner", StringComparison.Ordinal) || type.EndsWith("marketingagency", StringComparison.Ordinal))
            {
                into[n++] = TalentCatalogue.GraphicDesignerRole;
                into[n++] = TalentCatalogue.UxDesigner;
                into[n++] = TalentCatalogue.AccountManager;
            }
            else if (type.EndsWith("lawfirm", StringComparison.Ordinal))
            {
                into[n++] = 27;   // lawyer
                into[n++] = TalentCatalogue.AccountManager;
            }
            else if (type.EndsWith("headquarters", StringComparison.Ordinal))
            {
                into[n++] = 25;   // HR manager
                into[n++] = 26;   // recruiter
                into[n++] = 27;   // lawyer
                into[n++] = TalentCatalogue.ItSupport;
            }
            return n;
        }

        /// <summary>The labour market's shortage of a family, thousandths (1000: as many seekers as places).</summary>
        internal int Shortage(Family family)
        {
            return 1000;
        }

        // ---------------------------------------------------------------- the hour

        // Who is working this hour, counted by employee for the day's learning, burnout and mentoring.
        private void PeopleHour()
        {
            int day = Save.Day;
            if (day != _hoursDay)
            {
                _lastHours.Clear();
                foreach (KeyValuePair<string, int> pair in _hours)
                {
                    _lastHours[pair.Key] = pair.Value;
                }
                _lastHoursDay = _hoursDay;
                _hours.Clear();
                _hoursDay = day;
            }
            for (int i = 0; i < _people.Count; i++)
            {
                EmployeeInstance e = _people[i].Game;
                if (e != null && TalentGame.Working(e))
                {
                    int h;
                    _hours.TryGetValue(e.id, out h);
                    _hours[e.id] = h + 1;
                }
            }
        }

        private int HoursOf(Person person, int day)
        {
            if (person.Game == null)
            {
                return person.File.Sick || IsWeekend(day - 1) ? 0 : TalentCatalogue.DayHours;   // (the mod's own payroll works a plain week)
            }
            int h;
            if (_lastHoursDay == day - 1 && _lastHours.TryGetValue(person.Game.id, out h))
            {
                return h;
            }
            return _hoursDay == day - 1 && _hours.TryGetValue(person.Game.id, out h) ? h : 0;
        }

        private static bool IsWeekend(int day)
        {
            int d = ((day % 7) + 7) % 7;   // day 1 is a Monday (as the market counts)
            return d == 6 || d == 0;
        }

        // ---------------------------------------------------------------- the day

        private void PeopleDay(int day)
        {
            if (_talent.LastDay >= day)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            int made = ReadPeople(day);
            Mentors(day);
            int reviews = 0, sick = 0, left = 0, approached = 0, courses = 0, payroll = 0;
            int esteem = TechBridges.EsteemNow(), year = Year;
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Profile p = person.File;
                int hours = HoursOf(person, day);
                person.HoursYesterday = hours;
                // learning, and a mentor's help on the game's own skill
                int mainGain = TalentRules.Learn(p, hours, person.Mentored);
                if (person.Game != null)
                {
                    TalentGame.Mentored(person.Game, mainGain);
                }
                else
                {
                    p.Main = Math.Min(100000, p.Main + mainGain + hours * TalentCatalogue.MainLearnPerHour);
                }
                // how they feel
                var d = new WorkDay
                {
                    Day = day, Year = year, Hours = hours, WeekHours = p.WeekHours, MainPoints = person.MainPoints, Mentored = person.Mentored, Paid = p.GameWage, Going = p.Going,
                    Esteem = esteem, OnCall = false, Wanted = TalentRules.Content(p, Seed)
                };
                TalentRules.Mood(p, d);
                if (person.Game != null)
                {
                    TalentGame.Mood(person.Game, TalentRules.GameMood(p, person.Game.satisfaction));
                }
                p.Sick = false;
                if (TechRandom.Mille(Seed, TechRandom.Of(p.Id), day, 91) < TalentRules.SickChance(p))
                {
                    sick++;
                    if (person.Game != null)
                    {
                        TalentGame.SickTomorrow(person.Game);
                    }
                    else
                    {
                        p.Sick = true;
                    }
                }
                // the mod's own payroll
                if (person.Game == null && hours > 0)
                {
                    payroll += PayDay(person, hours) ? 1 : 0;
                }
                // courses and exams
                int course = TalentRules.EndCourse(p, Seed, day);
                if (course > 0)
                {
                    courses++;
                    Tell(course == 3 ? NotificationType.Warning : NotificationType.Success, course == 1 ? "reallife_talent_note_course" : course == 2 ? "reallife_talent_note_passed" : "reallife_talent_note_failed",
                        Data("name", person.Name), TabTeam, Row(p));
                }
                // reviews and the requests they make
                if (p.RaiseAsk > 0 && p.AskEnd > 0 && day >= p.AskEnd)
                {
                    TalentRules.Refused(p);
                    TechBridges.Standing?.Invoke(-1);
                }
                if (p.NoticeEnd == 0 && day >= p.ReviewDay)
                {
                    reviews++;
                    int rating = TalentRules.Review(p, Seed, day, year, person.MainPoints);
                    bool promote = TalentRules.CanPromote(p, day, year, person.MainPoints);
                    Tell(rating <= 2 ? NotificationType.Warning : NotificationType.Info, p.RaiseAsk > 0 ? "reallife_talent_note_review_ask" : promote ? "reallife_talent_note_review_promote" : "reallife_talent_note_review",
                        Data("name", person.Name, "rating", rating.ToString(), "percent", (p.RaiseAsk / 100).ToString()), TabTeam, Row(p));
                }
                // burnout, and a rival's approach
                if (TalentRules.BurnsOut(Seed, p, day))
                {
                    TalentRules.Notice(p, day, year, LeaveKind.BurntOut);
                    Tell(NotificationType.Warning, "reallife_talent_note_burnt_out", Data("name", person.Name, "day", p.NoticeEnd.ToString()), TabTeam, Row(p));
                }
                else if (TalentRules.OfferOpen(p, day - 1) && day > p.PoachDay)
                {
                    TalentRules.Answer(p, false, day, year);   // an offer not answered in time: they take it
                    Tell(NotificationType.Warning, "reallife_talent_note_poach_gone", Data("name", person.Name, "rival", p.PoachBy, "day", p.NoticeEnd.ToString()), TabTeam, Row(p));
                }
                else if (TalentRules.Approached(Seed, p, day, Shortage(p.Def.Family), esteem, p.GameWage, p.Going) && !Held(p))
                {
                    approached++;
                    string rival = TechBridges.Rival != null && person.At != null ? TechBridges.Rival(person.At) : null;
                    TalentRules.Offer(p, string.IsNullOrEmpty(rival) ? TechText.T("reallife_talent_rival_default") : rival, day);
                    Tell(NotificationType.Warning, "reallife_talent_note_poach", Data("name", person.Name, "rival", p.PoachBy, "percent", (p.PoachBp / 100).ToString(), "days", TalentCatalogue.PoachAnswerDays.ToString()),
                        TabTeam, Row(p));
                }
                // notice that has run out
                if (p.NoticeEnd > 0 && day >= p.NoticeEnd)
                {
                    left += Go(person, day) ? 1 : 0;
                }
            }
            _talent.LastDay = day;
            ReadPeople(day);
            if (Verbose)
            {
                Logger?.Info("Tech check (professionals, day " + day + "): " + _people.Count + " professional(s)" + (made > 0 ? ", " + made + " new" : string.Empty) + ", " + reviews + " review(s), " + sick + " off sick, "
                             + approached + " approached by a rival, " + courses + " course(s) or exam(s) ended, " + left + " gone, " + payroll + " paid by the mod; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: PASS");
            }
        }

        // Who has a mentor: at each business, every senior or above of a family has room for three juniors or mids of it.
        private void Mentors(int day)
        {
            _mentorRoom.Clear();
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Level >= TalentCatalogue.Senior && p.NoticeEnd == 0 && p.Place.Length > 0)
                {
                    string key = p.Place + "#" + (int)p.Def.Family;
                    int room;
                    _mentorRoom.TryGetValue(key, out room);
                    _mentorRoom[key] = room + TalentCatalogue.MentorsEach;
                }
            }
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Profile p = person.File;
                person.Mentored = false;
                if (p.Level > TalentCatalogue.Mid || p.Place.Length == 0)
                {
                    continue;
                }
                string key = p.Place + "#" + (int)p.Def.Family;
                int room;
                if (_mentorRoom.TryGetValue(key, out room) && room > 0)
                {
                    _mentorRoom[key] = room - 1;
                    person.Mentored = true;
                }
            }
        }

        // A day's wage of a professional on the mod's own payroll: the hours at their wage, at the business they work for.
        private bool PayDay(Person person, int hours)
        {
            Profile p = person.File;
            int cents = p.Wage * hours;
            return TalentGame.Cost(cents, TalentGame.WagesKey, person.At, person.Name, true, true);
        }

        // Notice has run out: a lay-off is paid its severance and let go, a dismissal let go, whoever resigned leaves.
        private bool Go(Person person, int day)
        {
            Profile p = person.File;
            var why = (LeaveKind)p.Leaving;
            if (why == LeaveKind.LaidOff)
            {
                long severance = TalentRules.Severance(p, day, Year, p.GameWage * TalentCatalogue.WeekHours, why);
                if (severance > 0)
                {
                    TalentGame.Cost((int)Math.Min(int.MaxValue, severance), TalentGame.SeveranceKey, person.At, person.Name, true, true);
                }
            }
            if (person.Game != null)
            {
                if (why == LeaveKind.LaidOff || why == LeaveKind.Dismissed)
                {
                    TalentGame.Fire(person.Game);
                }
                else
                {
                    TalentGame.Resign(person.Game);
                }
            }
            _talent.People.Remove(p);
            Tell(NotificationType.Info, "reallife_talent_note_left", Data("name", person.Name), TabTeam, null);
            return true;
        }

        // ---------------------------------------------------------------- what the player does (the app)

        internal Person PersonOf(string id)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                if (_people[i].File.Id == id)
                {
                    return _people[i];
                }
            }
            return null;
        }

        /// <summary>A rise of so many basis points: the game's wage (or the mod's) goes up; a request for one is settled.</summary>
        internal bool GiveRise(string id, int bp)
        {
            Person person = PersonOf(id);
            if (person == null || bp <= 0 || Save == null)
            {
                return false;
            }
            Raise(person, bp);
            TalentRules.Raised(person.File, Save.Day, bp);
            Changed();
            return true;
        }

        private static void Raise(Person person, int bp)
        {
            if (person.Game != null)
            {
                TalentGame.Rise(person.Game, bp);
                person.File.GameWage = TalentGame.WageCents(person.Game);
            }
            else
            {
                person.File.Wage = (int)Math.Min(int.MaxValue, (long)person.File.Wage * (10000 + bp) / 10000);
                person.File.GameWage = person.File.Wage;
            }
        }

        internal void RefuseRise(string id)
        {
            Person person = PersonOf(id);
            if (person != null && person.File.RaiseAsk > 0)
            {
                TalentRules.Refused(person.File);
                TechBridges.Standing?.Invoke(-1);
                Changed();
            }
        }

        internal bool Promote(string id)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || !TalentRules.CanPromote(person.File, Save.Day, Year, person.MainPoints))
            {
                return false;
            }
            TalentRules.Promote(person.File, Save.Day);
            Raise(person, TalentCatalogue.PromotePayBp);
            TechBridges.Standing?.Invoke(1);
            Tell(NotificationType.Success, "reallife_talent_note_promoted", Data("name", person.Name, "level", TechText.Level(person.File.Level)), TabTeam, Row(person.File));
            Changed();
            return true;
        }

        internal void SetRole(string id, int role)
        {
            Person person = PersonOf(id);
            RoleDef r = TalentCatalogue.Role(role);
            if (person != null && (person.Game == null ? !r.InGame || r.GameSkill == person.Skill : r.GameSkill == person.Skill))
            {
                TalentRules.Move(person.File, role);
                if (!r.Remote && person.File.Work == (int)Arrangement.Remote)
                {
                    person.File.Work = (int)Arrangement.Hybrid;
                }
                Changed();
            }
        }

        internal void SetWork(string id, int work)
        {
            Person person = PersonOf(id);
            if (person == null)
            {
                return;
            }
            Arrangement a = (Arrangement)TalentCatalogue.Clamp(work, 0, 2);
            if (a == Arrangement.Remote && (!person.File.Def.Remote || person.Game != null))
            {
                return;   // (the game's own employees work in the building: hybrid is as far as they go)
            }
            person.File.Work = (int)a;
            Changed();
        }

        /// <summary>A course in one of the role's specialisms, paid now by the business; false if the cash does not cover it.</summary>
        internal bool Course(string id, int slot)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || person.File.OnCourse(Save.Day))
            {
                return false;
            }
            int fee = Prices.At(TalentRules.CourseFee(person.File.Slot(slot) / 1000));
            if (!TalentGame.Cost(fee * 100, TalentGame.TrainingKey, person.At, person.Name, false, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            TalentRules.StartCourse(person.File, slot, Save.Day, -1);
            Changed();
            return true;
        }

        /// <summary>An exam for a certificate, paid now; false if the score is short of it or the cash does not cover it.</summary>
        internal bool Exam(string id, int cert)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || person.File.OnCourse(Save.Day) || person.File.Has(cert) || TalentRules.ExamChance(person.File, cert) <= 0)
            {
                return false;
            }
            int fee = Prices.At(TalentCatalogue.Certs[cert].Fee);
            if (!TalentGame.Cost(fee * 100, TalentGame.TrainingKey, person.At, person.Name, false, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            int slot = TalentCatalogue.Certs[cert].Skill == person.File.Def.SkillA ? 0 : 1;
            TalentRules.StartCourse(person.File, slot, Save.Day, cert);
            Changed();
            return true;
        }

        /// <summary>A restraint clause added to the contract: they are paid more for it and rivals come for them half as often.</summary>
        internal void AddClause(string id)
        {
            Person person = PersonOf(id);
            if (person != null && !person.File.NonCompete)
            {
                person.File.NonCompete = true;
                Raise(person, TalentCatalogue.NonCompeteBp);
                Changed();
            }
        }

        internal void AnswerRival(string id, bool match)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || !TalentRules.OfferOpen(person.File, Save.Day))
            {
                return;
            }
            if (match)
            {
                Raise(person, person.File.PoachBp);
            }
            TalentRules.Answer(person.File, match, Save.Day, Year);
            Changed();
        }

        /// <summary>Notice given by the player: a lay-off (with severance at the end) or, where it is fair, a dismissal at once.</summary>
        internal bool LetGo(string id, bool dismiss)
        {
            Person person = PersonOf(id);
            if (person == null || Save == null || person.File.NoticeEnd > 0)
            {
                return false;
            }
            if (dismiss && !TalentRules.FairDismissal(person.File, Save.Day, Year))
            {
                return false;
            }
            TalentRules.Notice(person.File, Save.Day, Year, dismiss ? LeaveKind.Dismissed : LeaveKind.LaidOff);
            TechBridges.Standing?.Invoke(dismiss ? 0 : -1);
            if (!dismiss)
            {
                _layoffs = _layoffsDay == Save.Day ? _layoffs + 1 : 1;
                _layoffsDay = Save.Day;
                if (_layoffs == TalentCatalogue.LayoffsNews && person.At != null)
                {
                    Story("reallife_tech_news_layoffs", "business", person.Business, "count", _layoffs.ToString());
                }
            }
            if (dismiss)
            {
                Go(person, Save.Day);
                ReadPeople(Save.Day);
            }
            Changed();
            return true;
        }

        // ---------------------------------------------------------------- the app

        private void InstallRoles()
        {
            var view = new RolesApp(this);
            GameSprites.Want(RolesGlyphs);
            bool tryAgain;
            _rolesApp = PhoneApp.Install(new PhoneAppDef
            {
                Id = RolesAppId,
                TitleKey = "reallife_roles_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(RolesGlyphs), new Color32(96, 176, 168, 255), new Color32(40, 112, 120, 255), 0.42f, "AppIcon-OpenRoles"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_rolesApp == null)
            {
                _rolesGaveUp = !tryAgain;
                if (_rolesGaveUp)
                {
                    Logger?.Warn("Tech: the game's phone is not built as this version expects, so the Open Roles app is left off. Professionals are still worked through each day and the notifications still say what needs an answer.");
                }
                return;
            }
            _rolesView = view;
            RolesBadge();
        }

        private static readonly string[] RolesGlyphs = { "Icon-Employees", "Icon-Building" };

        private static Sprite Glyph(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Sprite sprite = GameSprites.Get(names[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        public void OpenRoles(int tab, string pick)
        {
            if (_rolesApp != null && _rolesView != null)
            {
                _rolesView.Want(tab, pick);
                _rolesApp.Open();
            }
        }

        /// <summary>The app has been opened: everyone is read again, so what it shows is today's.</summary>
        internal bool RolesOpened()
        {
            if (_broken || Save == null || !_peopleOn)
            {
                return false;
            }
            try
            {
                ReadPeople(Save.Day);
                RolesBadge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>How many of the player's people wait for an answer: a request for a rise, a rival's offer, a promotion open.</summary>
        internal int Waiting()
        {
            int n = 0, day = Save != null ? Save.Day : 0;
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                n += p.RaiseAsk > 0 || TalentRules.OfferOpen(p, day) || TalentRules.CanPromote(p, day, Year, _people[i].MainPoints) ? 1 : 0;
            }
            return n;
        }

        private void RolesBadge()
        {
            if (_rolesApp != null)
            {
                _rolesApp.SetBadge(_rolesApp.Showing ? 0 : Waiting());
            }
        }

        internal static string Row(Profile p) => RolesApp.TeamRow + p.Id;

        // True while the company already has an offer or a departure in hand for the employee.
        private static bool Held(Profile p)
        {
            try
            {
                return !p.Payroll && TechBridges.Held != null && TechBridges.Held(p.Id);
            }
            catch (Exception)
            {
                return true;
            }
        }

        internal static Dictionary<string, string> Data(params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return data;
        }
    }
}
