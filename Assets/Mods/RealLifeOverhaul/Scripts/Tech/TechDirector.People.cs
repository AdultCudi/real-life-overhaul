using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;
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

    /// <summary>The people part: each professional's file, worked through each game day and counted on the hour; and the rises given.</summary>
    public sealed partial class TechDirector
    {
        public const int TabTeam = 0;

        private readonly List<Person> _people = new List<Person>(64);
        private readonly List<EmployeeInstance> _employees = new List<EmployeeInstance>(64);
        private readonly HashSet<string> _ids = new HashSet<string>();   // the game's employees read, taken or passed over
        private readonly Dictionary<(string Place, int Family), int> _mentorRoom = new Dictionary<(string Place, int Family), int>(32);   // room for juniors
        private readonly Dictionary<string, Profile> _fileOf = new Dictionary<string, Profile>(64);   // the first file of each id
        private Dictionary<Profile, Person> _personOf = new Dictionary<Profile, Person>(64);     // the last read's; swapped with _personNext
        private Dictionary<Profile, Person> _personNext = new Dictionary<Profile, Person>(64);
        private readonly int[] _wanted = new int[8];
        private TalentFile _talent;
        private bool _peopleOn;

        /// <summary>The people's files; null while the part is off.</summary>
        public TalentFile Talent => _peopleOn ? _talent : null;

        /// <summary>The player's professionals with what was read for each today, in the file's order.</summary>
        public IReadOnlyList<Person> People => _people;

        private int Seed => _talent != null ? _talent.Seed : 1;

        internal int TalentSeed => Seed;

        private void StartPeople()
        {
            GameInstance save = Save;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            TalentFile file = TalentStore.Load(save.modData, out int version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new TalentFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day };
            }
            _talent = file;
            _peopleOn = true;
            int made = ReadPeople(save.Day);
            MendWages();
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
            _ids.Clear();
            _fileOf.Clear();
            _personOf.Clear();
            _personNext.Clear();
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

        /// <summary>Ties each professional to their file, making and dropping files as they come and go. Returns how many were made.</summary>
        private int ReadPeople(int day)
        {
            _people.Clear();
            _ids.Clear();
            for (int i = 0; i < _talent.People.Count; i++)
            {
                _talent.People[i].Seen = _talent.People[i].Payroll;
            }
            _talent.Index(_fileOf);
            TalentGame.Employees(_employees);
            int made = 0;
            for (int i = 0; i < _employees.Count; i++)
            {
                EmployeeInstance e = _employees[i];
                _ids.Add(e.id);   // one passed over is not read again on the hour
                Profile p = e.id != null && _fileOf.TryGetValue(e.id, out Profile known) ? known : null;
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
                    if (p.Id != null && !_fileOf.ContainsKey(p.Id))
                    {
                        _fileOf.Add(p.Id, p);
                    }
                    made++;
                }
                p.Seen = true;
                _people.Add(Read(p, e, day));
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
                    _people.Add(Read(p, null, day));
                }
            }
            Dictionary<Profile, Person> last = _personOf;
            _personOf = _personNext;
            _personNext = last;
            _personNext.Clear();   // a file no longer read lets its Person go
            return made;
        }

        // The file's Person of the last read, filled again, so a read of 600 makes no objects.
        private Person Read(Profile p, EmployeeInstance e, int day)
        {
            if (!_personOf.TryGetValue(p, out Person person) && !_personNext.TryGetValue(p, out person))
            {
                person = new Person();
            }
            _personNext[p] = person;
            person.File = p;
            person.Game = e;
            person.At = null;
            person.Name = person.Business = person.Skill = string.Empty;
            person.MainPoints = 0;
            person.HoursYesterday = 0;
            person.Mentored = false;
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
            person.HoursYesterday = HoursOf(person, day);   // the firms' crews are worked out from it
            return person;
        }

        /// <summary>A newcomer's file: a role the business wants (else one of the skill's) at the level their skill stands at.</summary>
        private Profile NewFile(EmployeeInstance e, int day)
        {
            Profile ahead = Ahead(e.id);
            if (ahead != null && ahead.Def.GameSkill == TalentGame.Skill(e))
            {
                ahead.Id = e.id;
                ahead.Main = 0;
                ahead.Joined = e.dayHired > 0 && e.dayHired <= day ? e.dayHired : day;
                ahead.LevelDay = ahead.Joined;
                ahead.ReviewDay = TalentRules.FirstReview(ahead.Joined, Year);
                ahead.Morale = TalentCatalogue.MoraleStart;
                return ahead;
            }
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
            p.Record = false;   // the game hired them without a check
            return p;
        }

        /// <summary>The roles a business of a type wants first, in order. Returns how many.</summary>
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
            else
            {
                n = FirmsRules.Wanted(TechTypes.KindOf(type), into);
            }
            return n;
        }

        /// <summary>Former colleagues joining the mod's own payroll at the player's firm, at the going rate. Returns how many joined.</summary>
        internal int JoinPayroll(string place, int role, int level, int count, int day)
        {
            if (!_peopleOn || _talent == null || string.IsNullOrEmpty(place))
            {
                return 0;
            }
            int joined = 0;
            for (int i = 0; i < count && _talent.People.Count < TalentStore.PeopleMost; i++)
            {
                Profile p = TalentRules.Make(Seed, day * 53 + i * 7 + 3, role, Math.Max(0, level - (i == 0 ? 0 : 1)), day);
                p.Id = "pro:" + _talent.NextPro++;
                p.Name = VentureGame.Founder();
                p.Place = place;
                p.Work = (int)Arrangement.Hybrid;
                p.Wage = Math.Max(TalentCatalogue.RateLeast, TalentRules.Going(TalentGame.GameRate(p.Def.GameSkill, p.Main / 1000), p.Def, Shortage(p.Def.Family), 0));
                p.Joined = day;
                p.LevelDay = day;
                p.ReviewDay = TalentRules.FirstReview(day, Year);
                p.Morale = 700;
                _talent.People.Add(p);
                joined++;
            }
            if (joined > 0)
            {
                ReadPeople(day);
                Changed();
            }
            return joined;
        }

        /// <summary>The labour market's shortage of a family, thousandths (1000: as many seekers as places).</summary>
        internal int Shortage(Family family) => ShortageOf(family);

        // Counts on each file who works this hour: on a shift of the game's at a business that is open. The game adds the
        // hour to its own figure after this event; at midnight that figure still holds the day before, which stands in
        // for a day the count missed. A professional hired since the last read is read first, so their first shift counts.
        private void PeopleHour()
        {
            int day = Save.Day;
            bool midnight = Save.Hour == 0;
            TalentGame.Employees(_employees);
            for (int i = 0; i < _employees.Count; i++)
            {
                if (!_ids.Contains(_employees[i].id))
                {
                    ReadPeople(day);
                    break;
                }
            }
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                EmployeeInstance e = person.Game;
                if (e == null)
                {
                    continue;
                }
                int game = TalentGame.WorkedToday(e);
                TalentRules.RollHours(person.File, day, midnight ? game : -1, midnight ? -1 : game);
                if (TalentGame.Working(e) && !TalentGame.Shut(person.At))
                {
                    person.File.WorkHours = Math.Min(24, person.File.WorkHours + 1);
                }
            }
        }

        // The hours worked the day before: as counted on the hour, or a plain working day for the mod's own payroll.
        private int HoursOf(Person person, int day)
        {
            if (person.Game == null)
            {
                return person.File.Sick || IsWeekend(day - 1) ? 0 : TalentCatalogue.DayHours;
            }
            return TalentRules.HoursOn(person.File, day - 1);
        }

        /// <summary>The hours worked so far today; 0 for the mod's own payroll.</summary>
        internal static int HoursToday(Person person, int day) => person.Game != null ? TalentRules.HoursOn(person.File, day) : 0;

        private static bool IsWeekend(int day)
        {
            int d = ((day % 7) + 7) % 7;   // day 1 is a Monday
            return d == 6 || d == 0;
        }

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
                int mainGain = TalentRules.Learn(p, hours, person.Mentored);
                if (person.Game != null)
                {
                    TalentGame.Mentored(person.Game, mainGain);
                }
                else
                {
                    p.Main = Math.Min(100000, p.Main + mainGain + hours * TalentCatalogue.MainLearnPerHour);
                }
                var d = new WorkDay
                {
                    Day = day, Year = year, Hours = hours, WeekHours = p.WeekHours, MainPoints = person.MainPoints, Mentored = person.Mentored, Paid = p.GameWage, Going = p.Going,
                    Esteem = esteem, OnCall = false, Wanted = TalentRules.Content(p, Seed), Comfort = TechBridges.ComfortAt(p.Place)
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
                if (person.Game == null && hours > 0)
                {
                    payroll += PayDay(person, hours) ? 1 : 0;
                }
                int course = TalentRules.EndCourse(p, Seed, day);
                if (course > 0)
                {
                    courses++;
                    Tell(course == 3 ? NotificationType.Warning : NotificationType.Success,
                        course == 1 ? "reallife_talent_note_course" : course == 2 ? "reallife_talent_note_passed" : course == 4 ? "reallife_talent_note_degree" : "reallife_talent_note_failed",
                        Data("name", person.Name), TabTeam, Row(p));
                }
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
                    Tell(rating <= 2 ? NotificationType.Warning : NotificationType.Info,
                        p.RaiseAsk > 0 ? "reallife_talent_note_review_ask" : promote ? "reallife_talent_note_review_promote" : "reallife_talent_note_review",
                        Data("name", person.Name, "rating", rating.ToString(), "percent", (p.RaiseAsk / 100).ToString()), TabTeam, Row(p));
                }
                if (TalentRules.BurnsOut(Seed, p, day))
                {
                    TalentRules.Notice(p, day, year, LeaveKind.BurntOut);
                    Tell(NotificationType.Warning, "reallife_talent_note_burnt_out", Data("name", person.Name, "day", p.NoticeEnd.ToString()), TabTeam, Row(p));
                }
                else if (TalentRules.OfferOpen(p, day - 1) && day > p.PoachDay)
                {
                    TalentRules.Answer(p, false, day, year);   // not answered in time: they take it
                    Tell(NotificationType.Warning, "reallife_talent_note_poach_gone", Data("name", person.Name, "rival", p.PoachBy, "day", p.NoticeEnd.ToString()), TabTeam, Row(p));
                }
                else if (TalentRules.Approached(Seed, p, day, Shortage(p.Def.Family), esteem, p.GameWage, p.Going) && !Held(p))
                {
                    approached++;
                    string rival = TechBridges.Rival != null && person.At != null ? TechBridges.Rival(person.At) : null;
                    TalentRules.Offer(p, string.IsNullOrEmpty(rival) ? TechText.T("reallife_talent_rival_default") : rival, day);
                    Tell(NotificationType.Warning, "reallife_talent_note_poach",
                        Data("name", person.Name, "rival", p.PoachBy, "percent", (p.PoachBp / 100).ToString(), "days", TalentCatalogue.PoachAnswerDays.ToString()),
                        TabTeam, Row(p));
                }
                if (p.NoticeEnd > 0 && day >= p.NoticeEnd)
                {
                    Go(person, day);
                    left++;
                }
            }
            _talent.LastDay = day;
            ReadPeople(day);
            if (Verbose)
            {
                Logger?.Info("Tech check (professionals, day " + day + "): " + _people.Count + " professional(s)" + (made > 0 ? ", " + made + " new" : string.Empty)
                    + ", " + reviews + " review(s), " + sick + " off sick, "
                             + approached + " approached by a rival, " + courses + " course(s) or exam(s) ended, " + left + " gone, " + payroll + " paid by the mod; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
            }
        }

        // At each business, every senior or above has room for TalentCatalogue.MentorsEach juniors or mids of the family.
        private void Mentors(int day)
        {
            _mentorRoom.Clear();
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Level >= TalentCatalogue.Senior && p.NoticeEnd == 0 && p.Place.Length > 0)
                {
                    var key = (p.Place, (int)p.Def.Family);
                    _mentorRoom.TryGetValue(key, out int room);
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
                var key = (p.Place, (int)p.Def.Family);
                if (_mentorRoom.TryGetValue(key, out int room) && room > 0)
                {
                    _mentorRoom[key] = room - 1;
                    person.Mentored = true;
                }
            }
        }

        // A day's wage on the mod's own payroll, charged to the business they work for.
        private bool PayDay(Person person, int hours)
        {
            return TalentGame.Cost(person.File.Wage * hours, TalentGame.WagesKey, person.At, person.Name, true, true);
        }

        // Notice has run out: a lay-off is paid its severance; the game fires the employee or has them resign.
        private void Go(Person person, int day)
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
        }

        /// <summary>A rise in basis points, settling any request; at most one rise or promotion in WageRules' period.</summary>
        internal bool GiveRise(string id, int bp)
        {
            Person person = PersonOf(id);
            if (person == null || bp <= 0 || Save == null || Rested(person))
            {
                return false;
            }
            Raise(person, bp, Save.Day);
            TalentRules.Raised(person.File, Save.Day, bp);
            Changed();
            return true;
        }

        // A rise bounded by WageRules' cap; the next rise's period is counted from the day given.
        private static void Raise(Person person, int bp, int day)
        {
            Profile p = person.File;
            if (person.Game != null)
            {
                TalentGame.Rise(person.Game, bp);
                p.GameWage = TalentGame.WageCents(person.Game);
            }
            else
            {
                p.Wage = WageRules.Rise(p.Wage, bp, TalentGame.GameRate(p.Def.GameSkill, p.Main / 1000));
                p.GameWage = p.Wage;
            }
            p.RecognisedDay = Math.Max(p.RecognisedDay, day);
        }

        // True, and the player told when the next can be given, while the last rise's period runs.
        private bool Rested(Person person)
        {
            if (TalentRules.CanRise(person.File, Save.Day))
            {
                return false;
            }
            Tell(NotificationType.Info, "reallife_wage_rested", Data("name", person.Name, "day", WageRules.NextDay(person.File.RecognisedDay).ToString()), TabTeam, Row(person.File));
            return true;
        }

        /// <summary>Brings wages over WageRules' cap back to it as a save loads, with one notice. Returns how many.</summary>
        private int MendWages()
        {
            int mended = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Profile p = person.File;
                if (person.Game != null)
                {
                    if (TalentGame.Mend(person.Game))
                    {
                        p.GameWage = TalentGame.WageCents(person.Game);
                        mended++;
                    }
                }
                else
                {
                    int wage = WageRules.Mended(p.Wage, TalentGame.GameRate(p.Def.GameSkill, p.Main / 1000));
                    if (wage < p.Wage)
                    {
                        Logger?.Info("Tech: " + p.Name + "'s wage on the mod's payroll brought back from " + (p.Wage / 100f).ToString("F2", CultureInfo.InvariantCulture) + " to "
                            + (wage / 100f).ToString("F2", CultureInfo.InvariantCulture) + ".");
                        p.Wage = p.GameWage = wage;
                        mended++;
                    }
                }
            }
            if (mended > 0)
            {
                Tell(NotificationType.Info, "reallife_wage_mended", Data("count", mended.ToString(), "times", (WageRules.CapBp / 10000).ToString()), TabTeam, null);
                Changed();
            }
            if (Verbose || mended > 0)
            {
                Logger?.Info("Tech check (wages on load): " + _people.Count + " professional(s), " + mended + " wage(s) over " + (WageRules.CapBp / 10000)
                             + " times the game's own for the skill brought back to it; satisfaction untouched: PASS");
            }
            return mended;
        }

        internal static string Row(Profile p) => RolesApp.TeamRow + p.Id;

        // True while the company feature already has an offer or a departure in hand for the employee.
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
