using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The property business's tenants and contractors from the living population: a unit is let to a person of the city's one people pool
        /// (their age, job and pay read from it), and a booked service is done by a firm of the city's own. Both answer "none" while the
        /// population does not run, and the estate then lets as it always has. Nothing per frame: asked when a lease starts, each morning for a
        /// let unit's tenant, and when a service is booked.
        /// </summary>
        public static class EstatePeopleLinks
        {
            private const string TradeType = "ba:businesstype_interiorinstallationfirm";

            internal static void Connect()
            {
                EstateBridges.Pool = Pool;
                EstateBridges.Contractors = Contractors;
            }

            internal static void Disconnect()
            {
                EstateBridges.Pool = null;
                EstateBridges.Contractors = null;
            }

            private static ITenantPool Pool()
            {
                try
                {
                    PopulationState s = CityPopulation.Live;
                    return s != null && Settings.LivingPopulation ? new PoolOf(s) : null;
                }
                catch (Exception e)
                {
                    Fault("EstatePeople.Pool", e);
                    return null;
                }
            }

            // The city's firms that do a tradesman's work, by their address; empty when there are none.
            private static string[] Contractors()
            {
                try
                {
                    PopulationState s = CityPopulation.Live;
                    if (s == null || !Settings.LivingPopulation)
                    {
                        return new string[0];
                    }
                    var firms = new List<string>(8);
                    for (int b = 0; b < s.Businesses.Count && firms.Count < 24; b++)
                    {
                        if (string.Equals(s.Businesses.Type[b], TradeType, StringComparison.Ordinal) && !string.IsNullOrEmpty(s.Businesses.Key[b]))
                        {
                            firms.Add(s.Businesses.Key[b]);
                        }
                    }
                    return firms.ToArray();
                }
                catch (Exception e)
                {
                    Fault("EstatePeople.Contractors", e);
                    return new string[0];
                }
            }

            private sealed class PoolOf : ITenantPool
            {
                private readonly PopulationState _s;

                public PoolOf(PopulationState s)
                {
                    _s = s;
                }

                public int Count => _s.People.Count;

                public TenantFact At(int slot)
                {
                    PersonTable p = _s.People;
                    if (slot < 0 || slot >= p.Count || p.Id[slot] == 0)
                    {
                        return default(TenantFact);
                    }
                    return Fact(slot);
                }

                public bool Find(int id, out TenantFact fact)
                {
                    PersonTable p = _s.People;
                    int slot = id - 1;   // the people generated first sit in the slot of their number
                    if (slot < 0 || slot >= p.Count || p.Id[slot] != id)
                    {
                        slot = -1;
                        for (int i = 0; i < p.Count; i++)
                        {
                            if (p.Id[i] == id)
                            {
                                slot = i;
                                break;
                            }
                        }
                    }
                    fact = slot >= 0 ? Fact(slot) : default(TenantFact);
                    return slot >= 0;
                }

                private TenantFact Fact(int slot)
                {
                    PersonTable p = _s.People;
                    bool working = p.Status[slot] == (byte)Status.Worker && p.Job[slot] >= 0;
                    return new TenantFact
                    {
                        Id = p.Id[slot],
                        Age = _s.Age(slot, _s.Day),
                        Female = p.Female[slot] == 1,
                        Working = working,
                        PayADay = working ? LabourRules.DailyPay(_s, slot) / 100 : 0,
                        Employee = (p.Flags[slot] & PersonFlags.Linked) != 0 && EmployeeLink(p.Id[slot])
                    };
                }

                private bool EmployeeLink(int id)
                {
                    for (int i = 0; i < _s.Links.Count; i++)
                    {
                        if (_s.Links[i].Person == id && _s.Links[i].Kind == LinkKind.Employee)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }
    }
}
