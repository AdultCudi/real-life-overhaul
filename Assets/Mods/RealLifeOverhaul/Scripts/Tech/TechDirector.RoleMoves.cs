using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a bulk role change does to what the people cost: their pay, which a move never changes, and the going rate for the roles they leave and take.</summary>
    public struct RoleMoveFigures
    {
        public long PaidHourly;                        // what the people who move are paid, hundredths an hour: the same before and after (a move is no pay change)
        public long GoingBefore, GoingAfter;           // the going rate for their old roles and for the new one, hundredths an hour, summed
        public long GoingMonthly(int monthDays) => RivalMatchRules.Monthly(GoingAfter - GoingBefore, monthDays);
    }

    /// <summary>
    /// Several professionals moved to another role at once, from the Team page: who goes (RoleMoveRules, the best fit first and the newest of equal fit), what it would come to
    /// and one notice for the lot. Each move is the single change's own (<see cref="SetRole"/>: the role, the level kept inside the new role's, the person put in their role by
    /// hand), so nothing is gained by moving them together that moving them one by one would not give. Runs on the player's click and on the manager's morning, never per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly List<MoveFolk> _folks = new List<MoveFolk>(64);
        private readonly List<PlaceFloor> _floors = new List<PlaceFloor>(8);
        private readonly RoleMovePlan _movePlan = new RoleMovePlan();

        /// <summary>The roles of the Team page's people that can be moved from, as the roles somebody shown holds, in the catalogue's order. Returns how many.</summary>
        internal int RolesHeld(IList<string> ids, int[] into)
        {
            Array.Clear(into, 0, into.Length);
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                Person person = PersonOf(ids[i]);
                if (person != null)
                {
                    into[person.File.Role]++;
                }
            }
            int n = 0;
            for (int r = 0; r < into.Length; r++)
            {
                n += into[r] > 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>The roles somebody shown who holds <paramref name="from"/> could be moved to (one of them can hold it), in the catalogue's order. Returns how many.</summary>
        internal int RolesTo(IList<string> ids, int from, int[] into)
        {
            int n = 0;
            for (int r = 0; r < TalentCatalogue.RoleCount && n < into.Length; r++)
            {
                for (int i = 0; r != from && ids != null && i < ids.Count; i++)
                {
                    Person person = PersonOf(ids[i]);
                    if (person != null && person.File.Role == from && CanHold(person, r))
                    {
                        into[n++] = r;
                        break;
                    }
                }
            }
            return n;
        }

        /// <summary>The roles a firm's manager may be asked to keep a number of: those somebody at the firm can hold, in the catalogue's order. Returns how many.</summary>
        internal int RolesAt(string place, int[] into)
        {
            int n = 0;
            for (int r = 0; r < TalentCatalogue.RoleCount && n < into.Length; r++)
            {
                for (int i = 0; i < _people.Count; i++)
                {
                    if (_people[i].File.Place == place && CanHold(_people[i], r))
                    {
                        into[n++] = r;
                        break;
                    }
                }
            }
            return n;
        }

        /// <summary>What a place needs kept, read from the people and the firms' files now: its builders and designers, the products being built and the contracts under way.</summary>
        private CityFloor ReadFloors(List<PlaceFloor> into)
        {
            into.Clear();
            var city = new CityFloor();
            for (int i = 0; i < _people.Count; i++)
            {
                Person person = _people[i];
                Profile p = person.File;
                RoleDef r = p.Def;
                if (p.NoticeEnd > 0)
                {
                    continue;   // somebody leaving is not counted on: the floor is what stays
                }
                city.Reliability += r.Id == TalentCatalogue.Sre || r.Id == TalentCatalogue.DevOps ? 1 : 0;
                city.Chiefs += r.Id == TalentCatalogue.Cto ? 1 : 0;
                city.StaffInfra += r.Family == Family.Infrastructure && p.Level >= InfraCatalogue.HyperStaffLevel ? 1 : 0;
                PlaceFloor at = FloorAt(into, p.Place, person.Business);
                at.Builders += FirmsRules.Builds(r) ? 1 : 0;
                at.Designers += r.Family == Family.Design ? 1 : 0;
            }
            if (_firmsOn && _firmsFile != null)
            {
                for (int i = 0; i < _firmsFile.Products.Count; i++)
                {
                    Product pr = _firmsFile.Products[i];
                    if (pr.Stage == 0)
                    {
                        FloorAt(into, pr.Place, null).Building++;
                    }
                }
                for (int i = 0; i < _firmsFile.Contracts.Count; i++)
                {
                    Contract w = _firmsFile.Contracts[i];
                    if (w.Stage == (int)WorkStage.Active && w.Place.Length > 0)
                    {
                        PlaceFloor at = FloorAt(into, w.Place, null);
                        at.NeedDesigners += w.Kind == (int)WorkKind.Branding ? 1 : 0;
                        at.NeedBuilders += w.Kind == (int)WorkKind.Branding ? 0 : 1;
                    }
                }
                if (_infra != null)
                {
                    city.CloudOpen = _infra.Cloud.Open;
                    city.Hyper = _infra.Cloud.Hyper;
                }
            }
            return city;
        }

        private static PlaceFloor FloorAt(List<PlaceFloor> floors, string place, string name)
        {
            place = place ?? string.Empty;
            for (int i = 0; i < floors.Count; i++)
            {
                if (floors[i].Place == place)
                {
                    if (floors[i].Name.Length == 0 && !string.IsNullOrEmpty(name))
                    {
                        floors[i].Name = name;
                    }
                    return floors[i];
                }
            }
            var f = new PlaceFloor { Place = place, Name = name ?? string.Empty };
            floors.Add(f);
            return f;
        }

        private MoveFolk FolkOf(Person person, int to, int day)
        {
            Profile p = person.File;
            return new MoveFolk
            {
                Id = p.Id, Place = p.Place, Role = p.Role, Level = p.Level, A = p.A, B = p.B, Joined = p.Joined, Holds = CanHold(person, to),
                Away = p.NoticeEnd > 0 || p.OnCourse(day)
            };
        }

        /// <summary>
        /// What moving some of these people (their file ids; the Team page's shown ones) from a role to another would do: who goes, who is left out and why, and what it
        /// does to the going rates. Read now; nothing is moved. <paramref name="asked"/> is how many (0: every one who can).
        /// </summary>
        internal RoleMovePlan PlanRoleMoves(IList<string> ids, int from, int to, int asked, out RoleMoveFigures figures)
        {
            figures = default;
            RoleMovePlan plan = _movePlan;
            plan.Clear();
            if (Save == null || !_peopleOn || from == to)
            {
                plan.To = to;
                return plan;
            }
            int day = Save.Day;
            _folks.Clear();
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                Person person = PersonOf(ids[i]);
                if (person != null && person.File.Role == from)
                {
                    _folks.Add(FolkOf(person, to, day));
                }
            }
            CityFloor city = ReadFloors(_floors);
            RoleMoveRules.Plan(_folks, to, asked, _floors, city, plan);
            RoleDef target = TalentCatalogue.Role(to);
            for (int i = 0; i < plan.Ids.Count; i++)
            {
                Person person = PersonOf(plan.Ids[i]);
                if (person == null)
                {
                    continue;
                }
                figures.PaidHourly += PaidNow(person);
                figures.GoingBefore += person.File.Going;
                figures.GoingAfter += TalentRules.Going(TalentGame.GameRate(target.GameSkill, person.MainPoints), target, _shortageRead[(int)target.Family], TalentRules.CertPayBp(person.File));
            }
            return plan;
        }

        // One person put in a role: the single change's own steps (SetRole), shared with the manager's balancing, which does not pin them.
        private void Reassign(Person person, int role, bool byHand)
        {
            TalentRules.Move(person.File, role);
            person.File.Pinned = byHand;   // put in their role by hand: the firm's manager leaves them there
            _assigned.Remove(person.File.Id);
            if (!TalentCatalogue.Role(role).Remote && person.File.Work == (int)Arrangement.Remote)
            {
                person.File.Work = (int)Arrangement.Hybrid;
            }
        }

        /// <summary>
        /// Moves the people the plan names, as one at a time would, and tells it in one notice. Planned again at the click, so the people moved are those of the moment, and
        /// returned as done. Nothing is moved for a move to the same role, or when the people part is off.
        /// </summary>
        internal RoleMovePlan MoveRoles(IList<string> ids, int from, int to, int asked)
        {
            RoleMovePlan plan = PlanRoleMoves(ids, from, to, asked, out RoleMoveFigures figures);
            if (plan.Moved == 0)
            {
                return plan;
            }
            for (int i = 0; i < plan.Ids.Count; i++)
            {
                Person person = PersonOf(plan.Ids[i]);
                if (person != null)
                {
                    Reassign(person, to, true);
                }
            }
            int month = GameCalendar.MonthDays;
            Tell(NotificationType.Success, "reallife_roles_moved_all", Data("count", plan.Moved.ToString(CultureInfo.InvariantCulture), "from", TechText.Role(from), "to", TechText.Role(to),
                "going", TechText.Cents(figures.GoingMonthly(month))), TabTeam, null);
            if (Verbose)
            {
                bool ok = true;
                for (int i = 0; i < plan.Ids.Count; i++)
                {
                    Person person = PersonOf(plan.Ids[i]);
                    ok &= person != null && person.File.Role == to && person.File.Pinned && PaidNow(person) > 0;
                }
                Logger?.Info("Tech check (roles moved together, day " + Save.Day + "): " + plan.Moved + " " + TalentCatalogue.Role(from).Key + " to " + TalentCatalogue.Role(to).Key + " of "
                             + plan.Able + " who could (" + plan.Refused + " refused for what it would break, " + plan.Left + " left as the count asked): every one holds the new role and keeps their pay: "
                             + (ok ? "PASS" : "FAIL"));
            }
            Changed();
            return plan;
        }
    }
}
