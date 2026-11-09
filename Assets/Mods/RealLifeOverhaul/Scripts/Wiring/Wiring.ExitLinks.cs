using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Exits (Tech/Venture/ExitRules) with the rest of the mod (ExitBridges): the city's banks and listed companies from the stock market, a firm handed to its
        /// buyer as the game's sale of a business ends one, a founder's record from the living population, a skill learnt through the careers' own learning, and two
        /// desk sessions (an offer for the firm; the roadshow before a listing) put into the sessions' registry while the venture part runs. Nothing per frame:
        /// each is asked on a day, a click or a session.
        /// </summary>
        public static class ExitLinks
        {
            public const string OfferId = "tech_exit_offer", RoadId = "tech_exit_road";
            private const string Mod = "reallife", D = Mod + "_tech_desk_exit_";   // the keys' start, written so the source holds no key prefix as a literal
            private const int BanksMost = 3;
            private static readonly string[] Listers = { "Software", "Hardware", "Telecoms", "Media" };

            private static readonly string[] Types =
            {
                TechTypes.SoftwareStudio, TechTypes.GameStudio, TechTypes.Consultancy, TechTypes.Security, TechTypes.AiLab, FirmsGame.WebAgency, FirmsGame.GraphicDesigner
            };

            internal static void Connect()
            {
                ExitBridges.Banks = Banks;
                ExitBridges.Listed = Listed;
                ExitBridges.HandOver = HandOver;
                ExitBridges.CanHandOver = CanHandOver;
                ExitBridges.Resident = Resident;
                ExitBridges.People = () => Settings.LivingPopulation && CityPopulation.Running;
                ExitBridges.Practised = (skill, hours) => CareersDirector.Instance?.PractisedSkill(skill, hours);
                ExitBridges.Sessions = Hold;
                ExitBridges.Fixtures = Fixtures;
                CompanyBridges.StartupGains = (from, to) => TechDirector.Instance != null ? TechDirector.Instance.GainsBetween(from, to) : 0;   // a startup stake's gain joins the quarter's return
            }

            internal static void Disconnect()
            {
                Hold(false);
                ExitBridges.Reset();
                CompanyBridges.StartupGains = null;
            }

            // The founders of the city's startups as residents of the field their startup works in; none while the venture part is off.
            internal static void Founders(List<LinkAsk> into)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    ExitFile exits = tech != null ? tech.Exits : null;
                    VentureFile venture = tech != null ? tech.Ventures : null;
                    for (int i = 0; exits != null && venture != null && i < exits.Founders.Count; i++)
                    {
                        FounderRec r = exits.Founders[i];
                        Startup s = venture.StartupOf(r.Startup);
                        if (s != null && s.Alive)
                        {
                            into.Add(new LinkAsk { Kind = LinkKind.Founder, Key = ExitBridges.KeyOf(r.Startup), Age = r.Age, Female = r.Female, Prefer = ExitPlaces.FieldOf(s.Sector) });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.Founders", e);
                }
            }

            private static bool Resident(string key, out ResidentInfo info)
            {
                info = default(ResidentInfo);
                try
                {
                    PopulationState live = CityPopulation.Live;
                    if (live == null || SaveGameManager.Current == null
                        || !Population.PopulationLinks.Record(live, LinkKind.Founder, key, SaveGameManager.Current.Day, out int age, out bool female, out int field, out int skill))
                    {
                        return false;
                    }
                    info = new ResidentInfo { Age = age, Female = female, Field = field, Skill = skill };
                    return true;
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.Resident", e);
                    return false;
                }
            }

            // The city's three largest banks, the largest first, as the stock market names them for its bonds.
            private static void Banks(List<string> names)
            {
                names.Clear();
                try
                {
                    IReadOnlyList<Lender> banks = BankTerms.Banks;
                    for (int i = 0; banks != null && i < banks.Count && i < BanksMost; i++)
                    {
                        if (banks[i] != null && !string.IsNullOrEmpty(banks[i].Name))
                        {
                            names.Add(banks[i].Name);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.Banks", e);
                    names.Clear();
                }
            }

            // The listed companies of the market that work in software, hardware, telecoms or media: those that would buy a tech firm.
            private static void Listed(List<KeyValuePair<int, string>> into)
            {
                into.Clear();
                try
                {
                    MarketEngine engine = MarketDirector.Instance != null ? MarketDirector.Instance.Engine : null;
                    List<Stock> stocks = engine != null && engine.State != null ? engine.State.Stocks : null;
                    for (int i = 0; stocks != null && i < stocks.Count && into.Count < 8; i++)
                    {
                        Stock s = stocks[i];
                        if (s != null && s.Listed && s.RivalId == null && Array.IndexOf(Listers, s.Sector.ToString()) >= 0)
                        {
                            into.Add(new KeyValuePair<int, string>(s.Id, s.Name));
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.Listed", e);
                    into.Clear();
                }
            }

            private static bool HandOver(string place, string buyer)
            {
                try
                {
                    ServicesDirector services = ServicesDirector.Instance;
                    return services != null && services.HandOver(place, buyer);
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.HandOver", e);
                    return false;
                }
            }

            private static int Fixtures(string place)
            {
                try
                {
                    return ServicesGame.Fixtures(ServicesGame.Find(place));
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.Fixtures", e);
                    return 0;
                }
            }

            private static bool CanHandOver(string place)
            {
                try
                {
                    BuildingRegistration reg = ServicesGame.Find(place);
                    return reg == null || !ServicesGame.IsSellable(reg) || ServicesGame.Blocked(reg) == SaleBlock.None;
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.CanHandOver", e);
                    return false;
                }
            }

            // ------------------------------------------------------------------------------------------------- the desk sessions

            private static void Hold(bool on)
            {
                try
                {
                    if (on)
                    {
                        SessionRegistry.Add(Offer());
                        SessionRegistry.Add(Road());
                    }
                    else
                    {
                        SessionRegistry.Remove(OfferId);
                        SessionRegistry.Remove(RoadId);
                    }
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.Hold", e);
                }
            }

            private static SessionChoice C(string name, int minutes) => new SessionChoice { NameKey = D + name, TradeKey = D + name + "_trade", Minutes = minutes };

            private static SessionDef Make(string id, string stem, SessionChoice[] choices, int baseMinutes)
            {
                var def = new SessionDef
                {
                    Id = id, TitleKey = D + stem + "_title", AboutKey = D + stem + "_about", Types = Types, EveryDays = 1, Hour = 11, BaseMinutes = baseMinutes, Delegable = false, OwnDesk = true,
                    DesksLeast = 1, Steps = new[] { new SessionStep { TitleKey = D + stem + "_step", Choices = choices } }
                };
                def.Pick = (ask, step) => 0;
                return def;
            }

            internal static SessionDef Offer()
            {
                SessionDef def = Make(OfferId, "offer", new[] { C("offer_accept", 20), C("offer_counter", 40), C("offer_decline", 0) }, 30);
                def.Blocked = ask => Blocked(ask, 0);
                def.Apply = (ask, choices) => Apply(ask, choices, 0);
                return def;
            }

            internal static SessionDef Road()
            {
                SessionDef def = Make(RoadId, "road", new[] { C("road_wide", 60), C("road_deep", 90), C("road_quick", 30) }, 30);
                def.Blocked = ask => Blocked(ask, 1);
                def.Apply = (ask, choices) => Apply(ask, choices, 1);
                return def;
            }

            private static string Blocked(SessionAsk ask, int which)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.ExitBlocked(ask.Place, which) : D + "why_none";
                }
                catch (Exception e)
                {
                    Fault("ExitLinks.Blocked", e);
                    return D + "why_none";
                }
            }

            private static SessionOutcome Apply(SessionAsk ask, int[] choices, int which)
            {
                TechDirector tech = TechDirector.Instance;
                if (tech == null)
                {
                    return new SessionOutcome { Done = false, Key = D + "why_none" };
                }
                int choice = choices != null && choices.Length > 0 ? choices[0] : 0;
                bool done = tech.ExitSession(ask.Place, ask.Name, which, choice, out string key, out string[] pairs);
                return new SessionOutcome { Done = done, Key = key, Pairs = pairs };
            }
        }
    }
}
