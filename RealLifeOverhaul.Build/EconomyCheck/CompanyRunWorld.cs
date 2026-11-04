using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Careers;
using PolicyField = RealLifeOverhaul.Economy.PolicyField;

// The world the run of the Company checks gives the shared day entry (CompanyDay): the same members the director answers with the game, answered here with a seeded
// city: the cash the run keeps (and a short spell when it is not there), the notices counted as they are said, a manager off sick on some days, the leave that puts
// a person back on the board. The decisions are CompanyDay's; nothing of them is written again here.
internal static partial class Program
{
    private sealed class CoDayWorld : ICompanyDay
    {
        private readonly CoWorld _w;

        public CoDayWorld(CoWorld w)
        {
            _w = w;
        }

        public int Seed => _w.Seed;

        public int Hour => _w.Hour;

        public bool On(CompanyPart part) => !_w.PartOff[(int)part];

        public bool ManagersOn => _w.ManagersOn;

        public bool HierarchyOn => _w.HierarchyOn;

        public CompanyState State => _w.S;

        public ManagerState Managers => _w.M;

        public HierarchyState Hierarchy => _w.HierarchyOn ? _w.H : null;

        public int Rule(string place, PolicyField field)
        {
            return _w.HierarchyOn ? PolicyRules.Effective(_w.H.Company, null, null, field) : CompanyDay.RuleOff(_w.M.At(place), field);
        }

        public int BonusOf(string place) => StaffRules.BonusFor(Rule(place, PolicyField.PayBand), RunIndex);

        public int BudgetOf(ManagerFile m) => CompanyCatalogue.AtPrices(ManagerCatalogue.Step(ManagerCatalogue.Budgets, m.Budget), RunIndex);

        public bool Pay(int amount, string place, string subject, string why)
        {
            if (_w.Broke || _w.Cash < amount)
            {
                _w.PayFailed++;
                return false;
            }
            _w.Cash -= amount;
            return true;
        }

        public bool Mine(string place) => true;

        public bool PolicyAllows(string place, ManagerTask task)
        {
            int field = PolicyRules.SwitchOf(task);
            return field < 0 || Rule(place, (PolicyField)field) == 1;
        }

        // The place's manager is off sick on some days (a seeded draw): then nobody can decide, and what waits waits a day.
        public bool Decider(string place, out int skill, out int review, out string who)
        {
            ManagerFile own = _w.M.At(place);
            bool sick = own != null && CityModel.Draw(_w.Seed, 77, _w.Day * 7 + own.Skill, 1000) < 70;
            int by = ManagerRules.Decider(own != null && own.Seen, sick, own != null ? own.Skill : 0, false, false, 0, out skill, out review);
            who = by == 1 ? own.Name : null;
            _w.OffSick += by == 0 && own != null ? 1 : 0;
            return by != 0;
        }

        public bool RegionInPost(string place) => false;

        public bool ExecutiveInPost(ExecSeat seat) => _w.H.Executives.Exists(x => x.Area == (int)seat);

        public void Raised(Escalation e)
        {
            _w.Escalated++;
            _w.OwnerNew += e.Level == EscalationRules.LevelOwner ? 1 : 0;
        }

        public void LeaseNotice(string place, LeaseFile l, int day)
        {
            _w.LeaseNotices++;
            _w.NoticeLog.Add(place + "@" + day);
        }

        // The game's own leave: the person goes, and is on the board for a rival or the player to hire.
        public bool Leave(string employee)
        {
            StaffFile f = _w.S.Staff.Find(x => x.Id == employee);
            if (f == null)
            {
                return false;
            }
            _w.S.Staff.Remove(f);
            _w.Board.Add(new StaffFile { Id = f.Id + "r" + _w.Day, Name = f.Name, Going = f.Going, Wage = f.Going, Skill = f.Skill });
            _w.LetGo++;
            return true;
        }

        public Decision Record(string place, ManagerTask task, string subject, string who, int day, int hour, bool mistake)
        {
            return CompanyDay.Written(_w.M, _w.M.At(place), place, task, subject, who, day, hour, mistake);
        }

        public void Took(string place, string who, int days)
        {
            ManagerFile m = _w.M.At(place);
            if (m != null)
            {
                ManagerRules.AddSpeed(m.Speeds, days);
                _w.Speeds++;
            }
        }
    }

    private const int RunIndex = 100;   // the city's price index of the run: whole money at the starting prices
}
