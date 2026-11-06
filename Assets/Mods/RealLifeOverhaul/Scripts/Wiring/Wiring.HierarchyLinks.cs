using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The company's hierarchy with the professions (Tech): the player's professionals handed over as the executives'
        /// pages and reads want them, and for a test one joining the professions' own payroll. Read on the hierarchy's day
        /// and when the Company app opens; nothing per frame.
        /// </summary>
        internal static class HierarchyLinks
        {
            internal static void Connect()
            {
                CompanyBridges.Professionals = Professionals;
                CompanyBridges.HireProfessional = HireProfessional;
            }

            // Every professional the professions follow at the player's places, as they last read them; false while they are off.
            private static bool Professionals(List<ProfessionalHand> into)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    TalentFile talent = tech != null ? tech.Talent : null;
                    if (talent == null)
                    {
                        return false;
                    }
                    IReadOnlyList<Person> people = tech.People;
                    for (int i = 0; i < people.Count; i++)
                    {
                        Person p = people[i];
                        Profile f = p != null ? p.File : null;
                        if (f == null || string.IsNullOrEmpty(f.Id))
                        {
                            continue;
                        }
                        into.Add(new ProfessionalHand
                        {
                            Id = f.Id, Name = p.Name, Place = f.Place, PlaceName = p.Business, Role = f.Role, Level = f.Level, Going = f.Going,
                            Leaving = f.NoticeEnd > 0
                        });
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("HierarchyLinks.Professionals", e);
                    return false;
                }
            }

            private static bool HireProfessional(string place, int role, int level)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null && SaveGameManager.Current != null && tech.JoinPayroll(place, role, level, 1, SaveGameManager.Current.Day) > 0;
                }
                catch (Exception e)
                {
                    Fault("HierarchyLinks.HireProfessional", e);
                    return false;
                }
            }
        }
    }
}
