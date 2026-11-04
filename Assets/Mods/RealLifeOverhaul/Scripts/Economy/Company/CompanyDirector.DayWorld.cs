using Entities;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The director as the world a day's decisions meet (ICompanyDay): its switches, its cash through the game's money, its notices, its records and the game's own
    /// leave. The decisions are CompanyDay's, the same code the run of the checks calls; this is only what they ask of the game.
    /// </summary>
    public sealed partial class CompanyDirector : ICompanyDay
    {
        int ICompanyDay.Seed => _state.Seed;

        bool ICompanyDay.On(CompanyPart part) => On(part);

        bool ICompanyDay.ManagersOn => ManagersOn;

        bool ICompanyDay.HierarchyOn => HierarchyOn;

        CompanyState ICompanyDay.State => _state;

        ManagerState ICompanyDay.Managers => _mgr;

        HierarchyState ICompanyDay.Hierarchy => HierarchyOn ? _hier : null;

        int ICompanyDay.Rule(string place, PolicyField field) => Rule(ManagedAt(place), field);

        int ICompanyDay.BonusOf(string place) => BonusOf(place);

        int ICompanyDay.BudgetOf(ManagerFile m) => BudgetOf(m);

        bool ICompanyDay.Pay(int amount, string place, string subject, string why)
        {
            return Cost(amount, CompanyGame.BonusKey, ShopAt(place), subject, CostKind.Wages, null, why);
        }

        bool ICompanyDay.Mine(string place)
        {
            ManagedPlace mp = ManagedAt(place);
            return mp != null && mp.Place.Reg != null && GoingPublic.Mine(mp.Place.Reg);
        }

        bool ICompanyDay.PolicyAllows(string place, ManagerTask task)
        {
            ManagedPlace mp = ManagedAt(place);
            return mp != null && PolicyAllows(mp, task);
        }

        bool ICompanyDay.Decider(string place, out int skill, out int review, out string who)
        {
            ManagedPlace mp = ManagedAt(place);
            skill = review = 0;
            who = null;
            return mp != null && Decider(mp, out skill, out review, out who);
        }

        bool ICompanyDay.RegionInPost(string place)
        {
            ManagedPlace mp = ManagedAt(place);
            return mp != null && RegionAtWork(AreaOf(mp)) != null;
        }

        bool ICompanyDay.ExecutiveInPost(ExecSeat seat) => ExecutiveAtWork(seat) != null;

        void ICompanyDay.Raised(Escalation e)
        {
            _ownerNew += e.Level == EscalationRules.LevelOwner ? 1 : 0;
            Logger?.Info("Company: " + e.By + " (manager of " + ShopName(e.Place) + ") escalated " + (EscalationKind)e.Kind + " (" + e.A + ", " + e.B + ", " + e.C + ") to level " + e.Level + ".");
        }

        void ICompanyDay.LeaseNotice(string place, LeaseFile l, int day)
        {
            CompanyShop shop = ShopAt(place);
            if (shop != null)
            {
                TellLease(l, shop, day);
            }
        }

        int ICompanyDay.Hour => Save.Hour;

        bool ICompanyDay.Leave(string employee)
        {
            EmployeeInstance e = CompanyGame.Employee(employee);
            return e != null && CompanyBridges.Leave != null && CompanyBridges.Leave(e);
        }

        Decision ICompanyDay.Record(string place, ManagerTask task, string subject, string who, int day, int hour, bool mistake)
        {
            return Record(ManagedAt(place), task, subject, who, day, hour, mistake);
        }

        void ICompanyDay.Took(string place, string who, int days)
        {
            Took(ManagedAt(place), who, days);
        }
    }
}
