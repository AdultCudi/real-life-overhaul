using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Household;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Roommates;
using RealLifeOverhaul.Tech;
using RealLifeOverhaul.Uptown;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The living population with the rest of the mod: the city economy's districts follow its households' incomes; the Economy app's jobs are its
        /// measured market; Uptown's and Cedar Heights' demand comes from its pulls; the mod's copies of the game's customer arithmetic read its scale
        /// (Core/CustomerScale); the business features' levers (issues, the company's campaigns, brands, faults, Flare, the interior), which wrote a clamped
        /// promotion before, are what people hear of a business instead (two sources of PopulationAttention: those levers, and the game's own promotion since
        /// the business's anchor); and the mod's own people (the partner, roommates, household staff, people met, the tech sector's professionals and
        /// jobseekers) are residents too; the game studio's audience is the residents' games (StudioBridges.Population). Nothing per frame: each is asked
        /// once a game day or when a figure is written.
        /// </summary>
        public static class PopulationLinks
        {
            private const string LeversSource = "the mod's levers";
            private const string PromotionSource = "the game's promotion";

            private static readonly Dictionary<BuildingRegistration, int> LeverAttention = new Dictionary<BuildingRegistration, int>(64, ReferenceComparer<BuildingRegistration>.Instance);

            internal static void Connect()
            {
                CityBridges.DistrictIncome = DistrictIncome;
                DemandBridges.PopulationPull = Pull;
                CustomerScale.Of = Scale;
                CustomerScale.Site = Site;
                BusinessGame.Levers = Levers;
                PopulationAttention.Register(LeversSource, LeversOf);
                PopulationAttention.Register(PromotionSource, PromotionOf);
                PopulationBridges.People = People;
                PopulationBridges.UseOf = UseOf;
                StudioBridges.Population = Audience;
            }

            internal static void Disconnect()
            {
                CityBridges.DistrictIncome = null;
                DemandBridges.Reset();
                CustomerScale.Of = null;
                CustomerScale.Site = null;
                BusinessGame.Levers = null;
                PopulationAttention.Unregister(LeversSource);
                PopulationAttention.Unregister(PromotionSource);
                PopulationBridges.Reset();
                StudioBridges.Population = null;
                LeverAttention.Clear();
            }

            // The game studio's audience of a genre on a platform: the residents who like it there times the world's people over the residents at the
            // population's start (GameRules.Audience), so the studio's first day is the documented world's; -1 (the documented world) with no population.
            private static long Audience(int genre, int platform, int day, out long residents)
            {
                residents = 0;
                try
                {
                    if (!Settings.LivingPopulation || !CityPopulation.Players(genre, platform, out long now, out long start))
                    {
                        return -1;
                    }
                    residents = now;
                    return GameRules.Audience(now, start, StudioBridges.World(genre, platform, day));
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.Audience", e);
                    return -1;
                }
            }

            // A district's income on the city economy's scale from the households (the host's, for a guest, comes with the city economy's own part).
            private static int DistrictIncome(string district, int start)
            {
                try
                {
                    return Settings.LivingPopulation && CityPopulation.Running && !CityPopulation.Following ? CityPopulation.IncomeLevel(district, start) : 0;
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.DistrictIncome", e);
                    return 0;
                }
            }

            private static int Pull(string district, int use)
            {
                try
                {
                    return Settings.LivingPopulation && CityPopulation.Running ? CityPopulation.Pull(district, use) : -1000;
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.Pull", e);
                    return -1000;
                }
            }

            private static float Scale(BuildingRegistration reg)
            {
                try
                {
                    return PopulationCustomers.ScaleFor(reg);
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.Scale", e);
                    return 1f;
                }
            }

            private static float Site(string district, string type)
            {
                try
                {
                    return PopulationCustomers.SiteScale(district, type);
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.Site", e);
                    return 1f;
                }
            }

            // The business features' levers as attention, while the population moves the game's customers; true when taken (the game's own promotion is written).
            private static bool Levers(BuildingRegistration reg, int percent, int ambient, int interior)
            {
                try
                {
                    if (!Settings.LivingPopulation || !PopulationCustomers.On || reg == null)
                    {
                        LeverAttention.Remove(reg);
                        return false;
                    }
                    int city = CityEconomyDirector.FootfallOf(reg);   // the city economy's own part is in the people already (their incomes, the cycle, prices)
                    LeverAttention[reg] = CustomerRules.LeverAttention(percent + ambient - city + interior);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.Levers", e);
                    return false;
                }
            }

            private static int LeversOf(BuildingRegistration reg)
            {
                return LeverAttention.TryGetValue(reg, out int perMille) ? perMille : 0;
            }

            // The game's own promotion (a marketing campaign) since the business's anchor, as attention.
            private static int PromotionOf(BuildingRegistration reg)
            {
                if (reg == null || !reg.RentedByPlayer || reg.promotion == null || reg.Address == null)
                {
                    return 0;
                }
                PopulationState s = CityPopulation.Live;
                int b = s != null ? s.BusinessOfKey(PopulationState.KeyOf(reg.Address.streetName, reg.Address.streetNumber)) : -1;
                if (b < 0 || s.Businesses.AnchorDay[b] < 0)
                {
                    return 0;
                }
                return CustomerRules.PromotionAttention(s.Businesses.AnchorPromotion[b], PopulationGame.PromotionNow(reg));
            }

            // A building of the player's whose use the estate changed (homes to offices, say: the redevelopment's Convert): its use in the mod's records, -1 for none.
            private static int UseOf(string street, int number)
            {
                try
                {
                    EstateState estate = EstateDirector.Instance != null ? EstateDirector.Instance.State : null;
                    EstateHolding h = estate != null ? estate.Holding(EstateGame.Place(street, number)) : null;
                    if (h == null)
                    {
                        return -1;
                    }
                    switch ((EstateUse)h.Use)
                    {
                        case EstateUse.Homes: return (int)Use.Homes;
                        case EstateUse.Shops: return (int)Use.Shop;
                        case EstateUse.Offices: return (int)Use.Office;
                        case EstateUse.Storage: return (int)Use.Warehouse;
                        default: return -1;
                    }
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.UseOf", e);
                    return -1;
                }
            }

            // The mod's own people who are residents too, by their system's id.
            private static void People(List<LinkAsk> into)
            {
                try
                {
                    if (SocialState.Loaded)
                    {
                        SocialPerson partner = SocialState.Partner;
                        for (int i = 0; i < SocialState.Count; i++)
                        {
                            SocialPerson p = SocialState.Get(i);
                            if (p != null)
                            {
                                into.Add(new LinkAsk { Kind = ReferenceEquals(p, partner) ? LinkKind.Partner : LinkKind.Met, Key = "social:" + p.Index, Age = p.Age, Female = p.Female });
                            }
                        }
                    }
                    RoommateState mates = RoommateDirector.Instance != null ? RoommateDirector.Instance.State : null;
                    if (mates != null && mates.Mate != null && mates.Mate.Person != null)
                    {
                        SocialPerson mate = mates.Mate.Person;
                        into.Add(new LinkAsk { Kind = LinkKind.Roommate, Key = "mate:" + mate.Index + ":" + mates.MovedIn, Age = mate.Age, Female = mate.Female });
                    }
                    HouseholdState home = HouseholdDirector.Instance != null ? HouseholdDirector.Instance.State : null;
                    for (int i = 0; home != null && i < home.Staff.Count; i++)
                    {
                        StaffMember m = home.Staff[i];
                        into.Add(new LinkAsk { Kind = LinkKind.Staff, Key = "staff:" + (int)m.Kind + ":" + m.HiredDay, Age = m.Age, Female = m.Female });
                    }
                    TechDirector tech = TechDirector.Instance;
                    TalentFile talent = tech != null ? tech.Talent : null;
                    for (int i = 0; talent != null && i < talent.People.Count; i++)
                    {
                        into.Add(new LinkAsk { Kind = LinkKind.Professional, Key = talent.People[i].Id, Age = talent.People[i].Age, Female = talent.People[i].Female });
                    }
                    HiringFile hiring = tech != null ? tech.Hiring : null;
                    for (int i = 0; hiring != null && i < hiring.Seekers.Count; i++)
                    {
                        Seeker k = hiring.Seekers[i];
                        if (!k.Taken)
                        {
                            into.Add(new LinkAsk { Kind = LinkKind.Seeker, Key = "seeker:" + k.Id, Age = k.P.Age, Female = k.P.Female });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("PopulationLinks.People", e);
                }
            }
        }
    }
}
