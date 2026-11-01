using System;
using System.Collections.Generic;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The desk sessions with the features they serve: the Company app's managers take the sessions handed to them, and
        /// the tech sector's weekly team meeting is the first kind (MeetingRules), its week read back by the people part's
        /// day and the firms' work (TechBridges.TeamWeek). Later kinds (a studio's concept, a sprint, a training run) add
        /// themselves here the same way.
        /// </summary>
        public static class SessionLinks
        {
            internal static void Connect()
            {
                SessionBridges.Manager = Manager;
                SessionBridges.OpenManagers = OpenManagers;
                TechBridges.TeamWeek = TeamWeek;
                TechBridges.SessionDesk = SessionDesk;
                SessionBridges.WorkHere = WorkHere;
                SessionBridges.Work = Work;
                SessionRegistry.Add(Meeting());
            }

            internal static void Disconnect()
            {
                SessionRegistry.Remove(MeetingRules.Id);
                SessionBridges.Work = null;
                SessionBridges.WorkHere = null;
                TechBridges.SessionDesk = null;
                TechBridges.TeamWeek = null;
                SessionBridges.OpenManagers = null;
                SessionBridges.Manager = null;
            }

            // A business's own manager, appointed in the Company app and found at work there on the last read.
            private static bool Manager(string place, out string name, out int skill)
            {
                name = string.Empty;
                skill = 0;
                try
                {
                    Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                    Economy.ManagerState data = company != null && company.ManagersOn ? company.ManagerData : null;
                    Economy.ManagerFile m = data != null ? data.At(place) : null;
                    if (m == null || m.Region || !m.Seen)
                    {
                        return false;
                    }
                    name = m.Name;
                    skill = m.Skill;
                    return true;
                }
                catch (Exception e)
                {
                    Fault("SessionLinks.Manager", e);
                    return false;
                }
            }

            private static bool OpenManagers()
            {
                try
                {
                    Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                    if (company == null || !company.Running)
                    {
                        return false;
                    }
                    company.Open(Economy.CompanyDirector.TabManagers, null);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("SessionLinks.OpenManagers", e);
                    return false;
                }
            }

            private static bool TeamWeek(string place, int day, out int output, out int burnout)
            {
                try
                {
                    return SessionDirector.Effect(place, MeetingRules.Id, day, out output, out burnout);
                }
                catch (Exception e)
                {
                    Fault("SessionLinks.TeamWeek", e);
                    output = burnout = 0;
                    return false;
                }
            }

            // The owner's desk the sessions hold, and the own desk's work offered through their panel there: one click per desk.
            private static EmployeeStationController SessionDesk()
            {
                return SessionDirector.On ? SessionGame.Target : null;
            }

            private static bool WorkHere(EmployeeStationController desk)
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null && tech.WorksAt(desk);
                }
                catch (Exception e)
                {
                    Fault("SessionLinks.WorkHere", e);
                    return false;
                }
            }

            private static void Work(EmployeeStationController desk)
            {
                try
                {
                    TechDirector.Instance?.WorkAt(desk);
                }
                catch (Exception e)
                {
                    Fault("SessionLinks.Work", e);
                }
            }

            // ---------------------------------------------------------------- the weekly team meeting

            private static readonly string[] FirmTypes =
            {
                TechTypes.SoftwareStudio, TechTypes.GameStudio, TechTypes.Consultancy, TechTypes.Security, TechTypes.AiLab, FirmsGame.WebAgency, FirmsGame.GraphicDesigner
            };

            internal static SessionDef Meeting()
            {
                return new SessionDef
                {
                    Id = MeetingRules.Id,
                    TitleKey = "reallife_session_meeting_title",
                    AboutKey = "reallife_session_meeting_about",
                    Types = FirmTypes,
                    EveryDays = MeetingRules.WeekDays,
                    Hour = 10,
                    Delegable = true,
                    OwnDesk = true,
                    DesksLeast = 2,   // the owner's and one for a team
                    Steps = new[]
                    {
                        new SessionStep
                        {
                            TitleKey = "reallife_session_meeting_step_aim",
                            Choices = new[]
                            {
                                new SessionChoice { NameKey = "reallife_session_meeting_morale", TradeKey = "reallife_session_meeting_morale_trade" },
                                new SessionChoice { NameKey = "reallife_session_meeting_push", TradeKey = "reallife_session_meeting_push_trade" }
                            }
                        },
                        new SessionStep
                        {
                            TitleKey = "reallife_session_meeting_step_length",
                            Choices = new[]
                            {
                                new SessionChoice { NameKey = "reallife_session_meeting_short", TradeKey = "reallife_session_meeting_short_trade", Minutes = MeetingRules.ShortMinutes },
                                new SessionChoice { NameKey = "reallife_session_meeting_full", TradeKey = "reallife_session_meeting_full_trade", Minutes = MeetingRules.FullMinutes }
                            }
                        }
                    },
                    Blocked = MeetingBlocked,
                    Pick = MeetingPick,
                    Apply = MeetingApply
                };
            }

            private static string MeetingBlocked(SessionAsk ask)
            {
                if (!Settings.TechPeople || TechDirector.Instance == null || TechDirector.Instance.Talent == null)
                {
                    return "reallife_session_meeting_no_people_part";
                }
                return Team(ask.Place, out _) == 0 ? "reallife_session_meeting_nobody" : null;
            }

            private static int MeetingPick(SessionAsk ask, int step)
            {
                if (step == 1)
                {
                    return MeetingRules.Full;   // a manager has the hour: never the short one by judgement
                }
                Team(ask.Place, out int burnout);
                return MeetingRules.Pick(burnout);
            }

            // The professionals at a business and their burnout's average, thousandths.
            private static int Team(string place, out int burnout)
            {
                burnout = 0;
                TechDirector tech = TechDirector.Instance;
                if (tech == null || tech.Talent == null)
                {
                    return 0;
                }
                IReadOnlyList<Person> people = tech.People;
                int n = 0;
                long sum = 0;
                for (int i = 0; i < people.Count; i++)
                {
                    Profile p = people[i].File;
                    if (p != null && p.Place == place && p.NoticeEnd == 0)
                    {
                        n++;
                        sum += p.Burnout;
                    }
                }
                burnout = n > 0 ? (int)(sum / n) : 0;
                return n;
            }

            private static SessionOutcome MeetingApply(SessionAsk ask, int[] choices)
            {
                TechDirector tech = TechDirector.Instance;
                if (tech == null || tech.Talent == null)
                {
                    return new SessionOutcome { Done = false, Key = "reallife_session_meeting_no_people_part" };
                }
                int aim = choices != null && choices.Length > 0 ? choices[0] : MeetingRules.Morale;
                int length = choices != null && choices.Length > 1 ? choices[1] : MeetingRules.Full;
                int strength = MeetingRules.Strength(length, SessionRules.Strength(ask.ByManager, ask.Skill));
                MeetingRules.Effect(aim, strength, out int morale, out int ease, out int output, out int burnout);
                IReadOnlyList<Person> people = tech.People;
                int n = 0;
                for (int i = 0; i < people.Count; i++)
                {
                    Profile p = people[i].File;
                    if (p != null && p.Place == ask.Place && p.NoticeEnd == 0)
                    {
                        MeetingRules.Rally(p, morale, ease);
                        n++;
                    }
                }
                if (n == 0)
                {
                    return new SessionOutcome { Done = false, Key = "reallife_session_meeting_nobody" };
                }
                tech.Changed();
                return new SessionOutcome
                {
                    Done = true,
                    Key = aim == MeetingRules.Push ? "reallife_session_meeting_done_push" : "reallife_session_meeting_done_morale",
                    Pairs = new[] { "business", ask.Name, "people", n.ToString(), "morale", SessionText.Signed(morale), "output", SessionText.Signed(output) },
                    A = output,
                    B = burnout,
                    Days = MeetingRules.WeekDays
                };
            }
        }
    }
}
