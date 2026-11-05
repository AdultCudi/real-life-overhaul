using System;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A person of the people pool as a landlord's page shows them: their name, age, job and employer.</summary>
    internal struct EstatePerson
    {
        public bool Found;
        public string Name;
        public int Age;
        public string Job;      // "Healthcare, Murray Hill Clinic", or the work they do not have
        public int PayADay;
        public int Slot;
    }

    public sealed partial class EstateLive
    {
        /// <summary>Reads a person of the shared pool by their id: the name their seed gives, their age and where they work. Found is false when the pool does not hold them.</summary>
        internal static EstatePerson ReadPerson(int id)
        {
            PopulationState s = CityPopulation.Live;
            var p = new EstatePerson { Slot = -1 };
            if (s == null || id <= 0)
            {
                return p;
            }
            PersonTable t = s.People;
            int slot = id - 1;
            if (slot < 0 || slot >= t.Count || t.Id[slot] != id)
            {
                slot = -1;
                for (int i = 0; i < t.Count; i++)
                {
                    if (t.Id[i] == id)
                    {
                        slot = i;
                        break;
                    }
                }
            }
            if (slot < 0)
            {
                return p;
            }
            p.Found = true;
            p.Slot = slot;
            p.Name = PopulationGame.NameOf(PopulationViews.NameSeed(s, slot), t.Female[slot] == 1);
            p.Age = s.Age(slot, s.Day);
            bool working = t.Status[slot] == (byte)Status.Worker && t.Job[slot] >= 0;
            p.PayADay = working ? LabourRules.DailyPay(s, slot) / 100 : 0;
            string field = EstateText.T(PopulationViews.FieldKey(t.Field[slot]));
            if (!working)
            {
                p.Job = EstateText.T(PopulationViews.StatusKey(t.Status[slot]));
                return p;
            }
            int e = t.Job[slot];
            string where = null;
            if (e >= 0 && e < s.Employers.Count)
            {
                int b = s.Employers.Business[e];
                where = b >= 0 && b < s.Businesses.Count ? PopulationGame.BusinessName(s.Businesses.Key[b])
                    : !string.IsNullOrEmpty(s.Employers.Key[e]) ? PopulationGame.BusinessName(s.Employers.Key[e].Replace("|t", string.Empty))
                        : EstateText.T(PopulationViews.EmployerKey((EmployerKind)s.Employers.Kind[e]));
            }
            p.Job = string.IsNullOrEmpty(where) ? field : EstateText.Pair(field, where);
            return p;
        }

        /// <summary>The person's page in the people view, where there is one: false when the pool does not hold them.</summary>
        internal static bool OpenPerson(int id)
        {
            EstatePerson p = ReadPerson(id);
            if (!p.Found)
            {
                return false;
            }
            PopulationScreen.Wanted = ViewTarget.To(ViewKind.Person, p.Slot);
            return GameUi.PhoneApp.OpenById(UiShellApps.ShellPeople);
        }
    }
}
