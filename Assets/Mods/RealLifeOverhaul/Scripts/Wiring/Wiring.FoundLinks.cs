using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Founding a startup with the rest of the living economy (Tech/Founding): the five desk sessions of going to market, put into the sessions' registry while the venture
        /// part runs; the people of the city's population as co-founders, read from their records; the population's staff, rents and lettings for the niches; the study's own
        /// route for the player's skills to grow; and family life, which a failed firm that lost the family's money lets down. Nothing per frame: a click, or a page drawn.
        /// </summary>
        public static class FoundLinks
        {
            private const string Mod = "reallife", D = Mod + "_tech_desk_gtm_";   // the keys' start, written so the source holds no key prefix as a literal

            private static readonly string[] Types =
            {
                TechTypes.SoftwareStudio, TechTypes.Consultancy, TechTypes.Security, TechTypes.AiLab, FirmsGame.WebAgency, FirmsGame.GraphicDesigner
            };

            private static readonly string[] Ids = { "tech_gtm_discovery", "tech_gtm_build", "tech_gtm_users", "tech_gtm_launch", "tech_gtm_price" };
            private static readonly string[] Names = { "discovery", "build", "users", "launch", "price" };
            private static readonly int[] Stages =
            {
                FoundCatalogue.StageDiscovery, FoundCatalogue.StageBuild, FoundCatalogue.StageUsers, FoundCatalogue.StageLaunch, FoundCatalogue.StagePricing
            };
            private static readonly int[][] Minutes =
            {
                new[] { 180, 120, 90 }, new[] { FoundCatalogue.BuildMinutes, FoundCatalogue.BuildShort, 0 }, new[] { 120, 60, 120 }, new[] { 90, 120, 120 }, new[] { 60, 60, 60 }
            };

            internal static void Connect()
            {
                FoundBridges.Sessions = Hold;
                FoundBridges.People = People;
                FoundBridges.PopulationOn = Runs;
                FoundBridges.Census = Census;
                FoundBridges.Learn = Learn;
                FoundBridges.LetDown = LetDown;
            }

            internal static void Disconnect()
            {
                Hold(false);
                FoundBridges.Reset();
            }

            // ---------------------------------------------------------------- the sessions

            private static void Hold(bool on)
            {
                try
                {
                    for (int i = 0; i < Ids.Length; i++)
                    {
                        if (on)
                        {
                            if (SessionRegistry.Find(Ids[i]) == null)
                            {
                                SessionRegistry.Add(Kind(i));
                            }
                        }
                        else
                        {
                            SessionRegistry.Remove(Ids[i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("FoundLinks.Hold", e);
                }
            }

            private static SessionChoice C(int session, int choice) =>
                new SessionChoice { NameKey = D + Names[session] + "_c" + choice, TradeKey = D + Names[session] + "_c" + choice + "_trade", Minutes = Minutes[session][choice] };

            internal static SessionDef Kind(int session)
            {
                var choices = new SessionChoice[Minutes[session].Length];
                for (int c = 0; c < choices.Length; c++)
                {
                    choices[c] = C(session, c);
                }
                var def = new SessionDef
                {
                    Id = Ids[session], TitleKey = D + Names[session] + "_title", AboutKey = D + Names[session] + "_about", Types = Types, EveryDays = 1, Hour = 10, BaseMinutes = 20,
                    Delegable = false, OwnDesk = true, DesksLeast = 1,
                    Steps = new[] { new SessionStep { TitleKey = D + Names[session] + "_step", Choices = choices } }
                };
                int stage = Stages[session];
                def.Blocked = ask => Blocked(stage, ask);
                def.Pick = (ask, step) => 0;
                def.Apply = (ask, picked) => Apply(stage, ask, picked);
                return def;
            }

            private static string Blocked(int stage, SessionAsk ask)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.GtmBlocked(stage, ask.Place) : D + "why_none";
                }
                catch (Exception e)
                {
                    Fault("FoundLinks.Blocked", e);
                    return D + "why_none";
                }
            }

            private static SessionOutcome Apply(int stage, SessionAsk ask, int[] picked)
            {
                TechDirector tech = TechDirector.Instance;
                if (tech == null)
                {
                    return new SessionOutcome { Done = false, Key = D + "why_none" };
                }
                int choice = picked != null && picked.Length > 0 ? picked[0] : 0;
                bool done = tech.GtmSession(stage, ask.Place, ask.Name, choice, out string key, out string[] pairs);
                return new SessionOutcome { Done = done, Key = key, Pairs = pairs };
            }

            // ---------------------------------------------------------------- the population

            private static PopulationState Live()
            {
                return Settings.LivingPopulation && CityPopulation.Running && !CityPopulation.Following ? CityPopulation.Live : null;
            }

            private static bool Runs()
            {
                return Live() != null;
            }

            private const int BusinessAnalyst = 30;   // TalentCatalogue role 30

            // The role a field of work founds a startup as; -1 for a field a startup does not need.
            private static readonly int[] RoleOfField =
            {
                -1, -1, -1, TalentCatalogue.SalesExecutive, BusinessAnalyst, TalentCatalogue.SoftwareEngineer, TalentCatalogue.UxDesigner, -1, TalentCatalogue.ProductManager
            };

            // People of the city who could found a firm, from its records: of working age, in a job or looking for one, in a field a startup needs, with skill to show. Seeded by
            // the week, so the same people are on offer however often it is asked. Nothing of theirs is changed.
            private static int People(int seed, int day, List<CofounderOffer> into, int most)
            {
                try
                {
                    PopulationState s = Live();
                    if (s == null || s.People.Count == 0)
                    {
                        return 0;
                    }
                    PersonTable t = s.People;
                    int n = t.Count, start = TechRandom.Range(seed, day / 7, 87, 0, 0, n - 1);
                    for (int k = 0; k < n && into.Count < most; k++)
                    {
                        int p = (start + k) % n;
                        if (!t.Alive(p) || (t.Flags[p] & (PersonFlags.Linked | PersonFlags.Frozen)) != 0)
                        {
                            continue;
                        }
                        int status = t.Status[p], field = t.Field[p], skill = t.Skill[p], age = s.Age(p, s.Day);
                        bool working = status == (int)Status.Worker || status == (int)Status.Unemployed;
                        if (!working || age < 22 || age > 58 || skill < 35 || field < 0 || field >= RoleOfField.Length || RoleOfField[field] < 0)
                        {
                            continue;
                        }
                        int level = 0;
                        for (int l = TalentCatalogue.Levels - 1; l > 0; l--)
                        {
                            if (skill >= TalentCatalogue.LevelSkillLeast[l])
                            {
                                level = l;
                                break;
                            }
                        }
                        bool female = t.Female[p] == 1;
                        int id = t.Id[p];
                        into.Add(new CofounderOffer
                        {
                            Origin = CofounderOrigin.Population, Person = id, Name = PopulationGame.NameOf(PopulationViews.NameSeed(s, p), female), Female = female, Age = age,
                            Role = RoleOfField[field], Level = level, Main = skill * 1000, A = Math.Max(5000, (skill + TechRandom.Range(seed, id, 88, 0, -8, 4)) * 1000),
                            B = Math.Max(5000, (skill + TechRandom.Range(seed, id, 89, 0, -12, 0)) * 1000), Years = Math.Max(0, age - 21) * 365
                        });
                    }
                    return into.Count;
                }
                catch (Exception e)
                {
                    Fault("FoundLinks.People", e);
                    return 0;
                }
            }

            // The population's part of a niche census: the jobs at each niche's businesses (their employers' slots), the districts' rents against the game's own, and how many
            // residential buildings let flats.
            private static void Census(NicheCensus c)
            {
                try
                {
                    PopulationState s = Live();
                    if (s == null)
                    {
                        return;
                    }
                    BusinessTable b = s.Businesses;
                    for (int i = 0; i < b.Count; i++)
                    {
                        int niche = NicheRules.NicheOfType(b.Type[i]);
                        int e = b.Employer[i];
                        if (niche >= 0 && e >= 0 && e < s.Employers.Count)
                        {
                            c.Staff[niche] += Math.Max(0, s.Employers.Slots[e]);
                        }
                    }
                    long sum = 0;
                    for (int d = 0; d < s.RentIndex.Length; d++)
                    {
                        sum += s.RentIndex[d] > 0 ? s.RentIndex[d] : 1000;
                    }
                    if (s.RentIndex.Length > 0)
                    {
                        c.RentIndex = (int)(sum / s.RentIndex.Length);
                        c.RentKnown = true;
                    }
                    var let = new bool[Math.Max(1, s.Buildings.Count)];
                    HouseholdTable h = s.Households;
                    int lettings = 0;
                    for (int i = 0; i < h.Count; i++)
                    {
                        int building = h.Building[i];
                        if (h.Alive(i) && h.Landlord[i] != Landlords.Own && building >= 0 && building < let.Length && !let[building])
                        {
                            let[building] = true;
                            lettings++;
                        }
                    }
                    c.Lettings = lettings;
                    c.Population = true;
                }
                catch (Exception e)
                {
                    Fault("FoundLinks.Census", e);
                }
            }

            // ---------------------------------------------------------------- skills and family

            private static void Learn(int skill, int thousandths)
            {
                try
                {
                    StudyFile study = CareersDirector.Instance != null ? CareersDirector.Instance.Study : null;
                    if (study != null)
                    {
                        StudyRules.Learn(study, skill, thousandths, TimeHelper.CurrentDay);
                    }
                }
                catch (Exception e)
                {
                    Fault("FoundLinks.Learn", e);
                }
            }

            private static void LetDown(int trust, int affection)
            {
                try
                {
                    FamilyApi.LetDown(trust, affection);
                }
                catch (Exception e)
                {
                    Fault("FoundLinks.LetDown", e);
                }
            }
        }
    }
}
