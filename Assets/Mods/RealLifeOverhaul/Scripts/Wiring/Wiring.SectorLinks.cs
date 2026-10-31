using System;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The other tech firms with the rest of the living economy: their fourteen desk sessions (a software studio's sprint
        /// planning, outage response and big customer, an AI lab's training run and model launch, an IT consultancy's pitch and
        /// scope creep, a cybersecurity firm's incident, a data centre's capacity planning, power price spike and failover, a web
        /// agency's or design studio's client pitch, revision round and portfolio piece), put into the sessions' registry while
        /// the sector runs (the data centres' while the player has one) and taken out when it stops; the city the markets are
        /// sized from (the population's people, its employers as firms by size, its households' digital spending); and the
        /// sessions' records for the firms' pages. Flare's talk bridge (SectorBridges.Talk) is left for Flare's workstream to
        /// wire. Nothing per frame: the city is read once a game day, a session's callback on a click or a manager's hour.
        /// </summary>
        public static class SectorLinks
        {
            private static readonly SessionDef[] Kinds = new SessionDef[SectorRules.Desks];

            internal static void Connect()
            {
                for (int k = 0; k < SectorRules.Desks; k++)
                {
                    Kinds[k] = Kind(k);
                }
                TechBridges.SectorSessions = Hold;
                TechBridges.DeskRecord = Record;
                SectorBridges.City = City;
            }

            internal static void Disconnect()
            {
                Hold(false, false);
                TechBridges.SectorSessions = null;
                TechBridges.DeskRecord = null;
                SectorBridges.Reset();
            }

            // The kinds put in while the sector runs, the data centres' while the player has one, taken out otherwise: no desk lists a session it cannot hold.
            private static void Hold(bool firms, bool centres)
            {
                try
                {
                    for (int k = 0; k < SectorRules.Desks; k++)
                    {
                        bool on = Kinds[k] != null && firms && (SectorRules.DeskFirm[k] >= 0 || centres);
                        if (on)
                        {
                            if (SessionRegistry.Find(SectorRules.DeskIds[k]) == null)
                            {
                                SessionRegistry.Add(Kinds[k]);
                            }
                        }
                        else
                        {
                            SessionRegistry.Remove(SectorRules.DeskIds[k]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("SectorLinks.Hold", e);
                }
            }

            private static bool Record(string place, string session, out int held, out bool delegated)
            {
                try
                {
                    return SessionDirector.Record(place, session, out held, out delegated);
                }
                catch (Exception e)
                {
                    Fault("SectorLinks.Record", e);
                    held = SessionRules.Never;
                    delegated = false;
                    return false;
                }
            }

            // The city now from the population: its people, its employers as firms by size (the employers are not in
            // the public reading, so the table is read here), its households' digital spending today.
            private static bool City(out int people, out int small, out int mid, out int large, out long digital)
            {
                people = small = mid = large = 0;
                digital = 0;
                try
                {
                    PopulationState s = Settings.LivingPopulation && CityPopulation.Running && !CityPopulation.Following ? CityPopulation.Live : null;
                    if (s == null)
                    {
                        return false;
                    }
                    people = CityPopulation.People;
                    SectorRules.FirmsOf(s.Employers.Kind, s.Employers.Slots, s.Employers.Count, out small, out mid, out large);
                    digital = CityPopulation.Spending(Spend.Digital);
                    return people > 0;
                }
                catch (Exception e)
                {
                    Fault("SectorLinks.City", e);
                    return false;
                }
            }

            // ---------------------------------------------------------------- the kinds

            private static readonly string[] Software = { TechTypes.SoftwareStudio };
            private static readonly string[] Lab = { TechTypes.AiLab };
            private static readonly string[] Consultancy = { TechTypes.Consultancy };
            private static readonly string[] Security = { TechTypes.Security };
            private static readonly string[] Agency = { FirmsGame.WebAgency, FirmsGame.GraphicDesigner };
            private static readonly string[] Operations =
            {
                TechTypes.SoftwareStudio, TechTypes.GameStudio, TechTypes.Consultancy, TechTypes.Security, TechTypes.AiLab, FirmsGame.WebAgency, FirmsGame.GraphicDesigner, InfraGame.Warehouse
            };

            private const string Mod = "reallife", D = Mod + "_tech_desk_";   // the keys' start, written so the source holds no key prefix as a literal

            // a choice by its name within its kind (its trade-off is its key and "_trade"), and a step by its number
            private static SessionChoice C(string kind, string choice, int minutes) =>
                new SessionChoice { NameKey = D + kind + "_" + choice, TradeKey = D + kind + "_" + choice + "_trade", Minutes = minutes };

            private static SessionStep S(string kind, int step, params SessionChoice[] choices) => new SessionStep { TitleKey = D + kind + "_step" + step, Choices = choices };

            internal static SessionDef Kind(int kind)
            {
                string name = SectorRules.DeskIds[kind].Substring(5);
                var def = new SessionDef
                {
                    Id = SectorRules.DeskIds[kind], TitleKey = D + name + "_title", AboutKey = D + name + "_about", EveryDays = SectorRules.DeskEvery[kind], Delegable = true, OwnDesk = true,
                    DesksLeast = SectorRules.DeskFirm[kind] >= 0 ? 2 : 1   // the owner's and one for a team; a data centre's control room needs one
                };
                switch (kind)
                {
                    case SectorRules.DeskSprint:
                        def.Types = Software;
                        def.Hour = 10;
                        def.BaseMinutes = 30;
                        def.Steps = new[] { S(name, 1, C(name, "requests", 15), C(name, "balanced", 10), C(name, "debt", 15)), S(name, 2, C(name, "weekly", 0), C(name, "ready", 0)) };
                        break;
                    case SectorRules.DeskOutage:
                        def.Types = Software;
                        def.Hour = 9;
                        def.BaseMinutes = 20;
                        def.Steps = new[]
                        {
                            S(name, 1, C(name, "rollback", 20), C(name, "hotfix", 30), C(name, "status", 10)),
                            S(name, 2, C(name, "postmortem", 30), C(name, "credit", 10), C(name, "moveon", 0))
                        };
                        break;
                    case SectorRules.DeskBigCustomer:
                        def.Types = Software;
                        def.Hour = 14;
                        def.BaseMinutes = 30;
                        def.Steps = new[] { S(name, 1, C(name, "build", 30), C(name, "discount", 15), C(name, "refuse", 10)) };
                        break;
                    case SectorRules.DeskTraining:
                        def.Types = Lab;
                        def.Hour = 10;
                        def.BaseMinutes = 30;
                        def.Steps = new[]
                        {
                            S(name, 1, C(name, "own", 15), C(name, "rented", 10), C(name, "smaller", 5)), S(name, 2, C(name, "licensed", 15), C(name, "scraped", 5), C(name, "synthetic", 10)),
                            S(name, 3, C(name, "full", 30), C(name, "standard", 15), C(name, "skip", 0))
                        };
                        break;
                    case SectorRules.DeskLaunch:
                        def.Types = Lab;
                        def.Hour = 11;
                        def.BaseMinutes = 30;
                        def.Steps = new[]
                        {
                            S(name, 1, C(name, "open", 15), C(name, "api", 15), C(name, "partners", 30)),
                            S(name, 2, C(name, "undercut", 0), C(name, "match", 0), C(name, "premium", 0))
                        };
                        break;
                    case SectorRules.DeskPitch:
                        def.Types = Consultancy;
                        def.Hour = 11;
                        def.BaseMinutes = 30;
                        def.Steps = new[] { S(name, 1, C(name, "seniors", 30), C(name, "mixed", 20), C(name, "juniors", 10)), S(name, 2, C(name, "low", 0), C(name, "fair", 0), C(name, "high", 0)) };
                        break;
                    case SectorRules.DeskScope:
                        def.Types = Consultancy;
                        def.Hour = 15;
                        def.BaseMinutes = 20;
                        def.Steps = new[] { S(name, 1, C(name, "charge", 15), C(name, "absorb", 10), C(name, "pushback", 15)) };
                        break;
                    case SectorRules.DeskIncident:
                        def.Types = Security;
                        def.Hour = 8;
                        def.BaseMinutes = 30;
                        def.Steps = new[]
                        {
                            S(name, 1, C(name, "isolate", 15), C(name, "targeted", 30), C(name, "trace", 45)),
                            S(name, 2, C(name, "now", 10), C(name, "later", 0), C(name, "little", 0))
                        };
                        break;
                    case SectorRules.DeskCapacity:
                        def.Types = Operations;
                        def.Hour = 10;
                        def.BaseMinutes = 30;
                        def.Steps = new[]
                        {
                            S(name, 1, C(name, "buy", 15), C(name, "asneeded", 0), C(name, "let", 10)),
                            S(name, 2, C(name, "undercut", 0), C(name, "match", 0), C(name, "premium", 0))
                        };
                        break;
                    case SectorRules.DeskPower:
                        def.Types = Operations;
                        def.Hour = 9;
                        def.BaseMinutes = 20;
                        def.Steps = new[] { S(name, 1, C(name, "ride", 0), C(name, "throttle", 10), C(name, "hedge", 15)) };
                        break;
                    case SectorRules.DeskFailover:
                        def.Types = Operations;
                        def.Hour = 9;
                        def.BaseMinutes = 20;
                        def.Steps = new[]
                        {
                            S(name, 1, C(name, "failover", 20), C(name, "restore", 30), C(name, "quiet", 0)),
                            S(name, 2, C(name, "postmortem", 30), C(name, "credit", 10), C(name, "moveon", 0))
                        };
                        break;
                    case SectorRules.DeskClientPitch:
                        def.Types = Agency;
                        def.Hour = 11;
                        def.BaseMinutes = 30;
                        def.Steps = new[] { S(name, 1, C(name, "bold", 30), C(name, "safe", 15), C(name, "cheap", 10)), S(name, 2, C(name, "fast", 0), C(name, "usual", 0)) };
                        break;
                    case SectorRules.DeskRevision:
                        def.Types = Agency;
                        def.Hour = 15;
                        def.BaseMinutes = 20;
                        def.Steps = new[] { S(name, 1, C(name, "free", 15), C(name, "charge", 10), C(name, "hold", 10)) };
                        break;
                    default:
                        def.Types = Agency;
                        def.Hour = 14;
                        def.BaseMinutes = 45;
                        def.Steps = new[] { S(name, 1, C(name, "polish", 30), C(name, "award", 15), C(name, "later", 0)) };
                        break;
                }
                def.Blocked = ask => Blocked(kind, ask);
                def.Pick = (ask, step) => Pick(kind, ask, step);
                def.Apply = (ask, choices) => Apply(kind, def, ask, choices);
                return def;
            }

            private static string Blocked(int kind, SessionAsk ask)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.SectorBlocked(kind, ask.Place) : D + "why_off";
                }
                catch (Exception e)
                {
                    Fault("SectorLinks.Blocked", e);
                    return D + "why_off";
                }
            }

            private static int Pick(int kind, SessionAsk ask, int step)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? tech.SectorPick(kind, ask.Place, step) : 0;
                }
                catch (Exception e)
                {
                    Fault("SectorLinks.Pick", e);
                    return 0;
                }
            }

            // The sector applies the choices; the notice's words get the choices' names beside the figures.
            private static SessionOutcome Apply(int kind, SessionDef def, SessionAsk ask, int[] choices)
            {
                TechDirector tech = TechDirector.Instance;
                if (tech == null)
                {
                    return new SessionOutcome { Done = false, Key = D + "why_off" };
                }
                int strength = SessionRules.Strength(ask.ByManager, ask.Skill);
                bool done = tech.SectorSession(kind, ask.Place, ask.Name, strength, choices, out string key, out string[] pairs);
                if (!done)
                {
                    return new SessionOutcome { Done = false, Key = key, Pairs = pairs };
                }
                var all = new string[pairs.Length + def.Steps.Length * 2];
                Array.Copy(pairs, all, pairs.Length);
                for (int i = 0; i < def.Steps.Length; i++)
                {
                    SessionChoice[] c = def.Steps[i].Choices;
                    int at = choices != null && i < choices.Length && choices[i] >= 0 && choices[i] < c.Length ? choices[i] : 0;
                    all[pairs.Length + i * 2] = "choice" + i;
                    all[pairs.Length + i * 2 + 1] = SessionText.T(c[at].NameKey);
                }
                return new SessionOutcome { Done = true, Key = key, Pairs = all };
            }
        }
    }
}
