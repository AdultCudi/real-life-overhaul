using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A firm's manager, or the operations executive, assigning its professionals to roles each morning (AssignRules): who answers its customers and who sells, by what the firm needs
    /// now. Off for a firm until the player turns it on; never moves somebody the player put in their role by hand; says what it moved and why in one notice a day, and the Team page
    /// shows who assigned whom (kept for the session only: it is a reading, not a record). Run once a morning with the firms' day, nothing per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private struct Assigned
        {
            public int Day, Why;
            public string By;
        }

        private readonly Dictionary<string, Assigned> _assigned = new Dictionary<string, Assigned>(8);
        private readonly List<AssignPerson> _assignPeople = new List<AssignPerson>(16);
        private readonly List<AssignMove> _assignMoves = new List<AssignMove>(2);

        /// <summary>Whether a firm's manager assigns its professionals (the player's switch).</summary>
        internal bool AssignOn(string place)
        {
            FirmPlace firm = _firmsOn ? FirmOf(place) : null;
            return firm != null && firm.File.Assign;
        }

        internal void SetAssign(string place, bool on)
        {
            FirmPlace firm = _firmsOn ? FirmOf(place) : null;
            if (firm != null && firm.File.Assign != on)
            {
                firm.File.Assign = on;
                ChangedTech();
            }
        }

        /// <summary>Who assigns at a firm: its manager at work, else the operations executive in post; null for nobody, and then nothing is assigned.</summary>
        internal string Assigner(string place)
        {
            if (RealLifeOverhaul.Sessions.SessionBridges.ManagerAt(place, out string manager, out _) && !string.IsNullOrEmpty(manager))
            {
                return manager;
            }
            Economy.ExecutiveFile coo = Economy.CompanyDirector.Instance?.ExecutiveAtWork(Careers.ExecSeat.Coo);
            return coo != null ? coo.Name : null;
        }

        /// <summary>Who assigned a person to their role last, and when and why (the Team page's line), or false.</summary>
        internal bool AssignedLine(string id, out string by, out int day, out int why)
        {
            by = string.Empty;
            day = why = 0;
            if (!_assigned.TryGetValue(id, out Assigned a))
            {
                return false;
            }
            by = a.By;
            day = a.Day;
            why = a.Why;
            return true;
        }

        private bool CanHold(Person person, int role)
        {
            RoleDef r = TalentCatalogue.Role(role);
            return person.Game == null ? !r.InGame || r.GameSkill == person.Skill : r.GameSkill == person.Skill;
        }

        // The morning's assignment, after the people were read and before the crews are counted from them.
        private void AssignDay(int day)
        {
            if (!_peopleOn || _firmsFile == null)
            {
                return;
            }
            string told = null;
            for (int i = 0; i < _firmsFile.Firms.Count; i++)
            {
                Firm file = _firmsFile.Firms[i];
                if (!file.Assign)
                {
                    continue;
                }
                string by = Assigner(file.Place);
                if (by == null)
                {
                    file.AssignedBy = string.Empty;
                    continue;
                }
                _assignPeople.Clear();
                var who = new List<Person>(8);
                for (int k = 0; k < _people.Count; k++)
                {
                    Person p = _people[k];
                    if (p.File.Place != file.Place || p.Skill != TalentCatalogue.CustomerService)
                    {
                        continue;
                    }
                    Family f = p.File.Def.Family;
                    who.Add(p);
                    _assignPeople.Add(new AssignPerson
                    {
                        Id = p.File.Id, Role = p.File.Role, Level = p.File.Level, Points = p.MainPoints, Pinned = p.File.Pinned,
                        Away = p.File.NoticeEnd > 0 || p.File.OnCourse(day), Support = f == Family.Support, Sales = f == Family.Commercial
                    });
                }
                int need = 0, offers = 0, live = 0;
                for (int k = 0; k < _firmsFile.Products.Count; k++)
                {
                    Product pr = _firmsFile.Products[k];
                    if (pr.Place == file.Place && pr.Live)
                    {
                        live++;
                        need += pr.K == ProductKind.Game ? 0 : FirmsRules.SupportNeeded(pr);
                    }
                }
                if (_studioOn && file.Kind == (int)FirmKind.Games)
                {
                    need = StudioRules.SupportFor(StudioTicketsAt(file.Place));
                }
                for (int k = 0; k < _firmsFile.Contracts.Count; k++)
                {
                    Contract w = _firmsFile.Contracts[k];
                    offers += w.Place == file.Place && w.Stage == (int)WorkStage.Offered ? 1 : 0;
                }
                bool takes = FirmsCatalogue.TakesWork[file.Kind] != 0;
                int wanted = (takes && offers > 0) || (file.Kind == (int)FirmKind.Software && live > 0) ? 1 : 0;
                _assignMoves.Clear();
                if (AssignRules.Plan(_assignPeople, need, wanted, takes && offers > 0, _assignMoves) == 0)
                {
                    continue;
                }
                string line = string.Empty;
                for (int m = 0; m < _assignMoves.Count; m++)
                {
                    AssignMove move = _assignMoves[m];
                    for (int k = 0; k < who.Count; k++)
                    {
                        if (who[k].File.Id != move.Id || !CanHold(who[k], move.To))
                        {
                            continue;
                        }
                        Person p = who[k];
                        TalentRules.Move(p.File, move.To);
                        _assigned[p.File.Id] = new Assigned { Day = day, Why = move.Why, By = by };
                        line += (line.Length > 0 ? "; " : string.Empty) + TechText.Fill(move.Why == AssignRules.WhySales ? "reallife_tech_assign_to_sales" : "reallife_tech_assign_to_support",
                            "name", p.Name, "role", TechText.Role(move.To));
                        break;
                    }
                }
                file.AssignedBy = line.Length > 0 ? by + ": " + line : string.Empty;
                if (line.Length > 0)
                {
                    told = (told != null ? told + " " : string.Empty) + TechText.Fill("reallife_tech_assign_firm", "firm", file.Name, "by", by, "moves", line);
                }
            }
            if (told != null)
            {
                TellTech(NotificationType.Info, "reallife_tech_assign_note", Data("moves", told.Length > 200 ? told.Substring(0, 197) + "..." : told), TabTeam, null);
                ChangedTech();
            }
        }
    }
}
