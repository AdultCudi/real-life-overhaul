using System;
using System.Collections.Generic;
using RealLifeOverhaul.Parties;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Parties and company events with the features they meet: startup money's floor (a party or an event never takes the
        /// wallet under what it keeps for investors), the company's staff files and managers, business pressure's closures
        /// (a business is shut for the opening hours an event takes), the social life (the People page's Host a party,
        /// losing touch), the rival tycoons (their outlets' staff events and the paper's line), and the Planner.
        /// </summary>
        public static class PartyLinks
        {
            private static readonly PartySource Source = new PartySource();

            internal static void Connect()
            {
                PartyBridges.Floor = Floor;
                PartyBridges.StaffFile = StaffFile;
                PartyBridges.StaffLevel = StaffLevel;
                PartyBridges.Manager = Manager;
                PartyBridges.Close = Close;
                SocialLinks.PartiesForget = Forget;
                SocialPhone.PartyLabel = PartyLabel;
                SocialPhone.PartyOpen = PartyOpen;
                RivalBridges.StaffEvents = RivalDay;
                RivalBridges.StaffEventsPaper = RivalPaper;
                _paperBefore = News.Extra;
                News.Extra = Paper;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                News.Extra = _paperBefore;
                _paperBefore = null;
                RivalBridges.StaffEventsPaper = null;
                RivalBridges.StaffEvents = null;
                SocialPhone.PartyOpen = null;
                SocialPhone.PartyLabel = null;
                SocialLinks.PartiesForget = null;
                PartyBridges.Close = null;
                PartyBridges.Manager = null;
                PartyBridges.StaffLevel = null;
                PartyBridges.StaffFile = null;
                PartyBridges.Floor = null;
            }

            private static Action<int, List<NewsItem>> _paperBefore;

            // The City Ledger's pieces for a morning: after those of the features before, a large party the player hosted the day before
            // (PartyCatalogue.LargeFrom guests or more came). Read from the file only, so the same day gives the same piece however often it is asked.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("PartyLinks.Paper (the pieces before)", e);
                }
                try
                {
                    PartyDirector d = PartyDirector.Instance;
                    PartyFile f = d != null && PartyDirector.On ? d.File : null;
                    Party large = f != null ? PartyHosted.Large(f, day - 1) : null;
                    if (large != null)
                    {
                        into.Add(new NewsItem
                        {
                            Key = "reallife_party_paper_host",
                            Data = new Dictionary<string, string>(2)
                            {
                                { "place", large.Place == PartyPlace.Home ? large.Street : large.Name }, { "count", PartyText.Whole(large.Came) }
                            }
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("PartyLinks.Paper", e);
                }
            }

            // What the wallet must keep for the investors of the player's firms, whole money, rounded up.
            private static long Floor()
            {
                Tech.TechDirector tech = Tech.TechDirector.Instance;
                return tech != null ? (tech.FloorNow + 99) / 100 : 0L;
            }

            private static Economy.StaffFile StaffFile(string id)
            {
                Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                Economy.CompanyState state = company != null && company.On(Economy.CompanyPart.Staff) ? company.State : null;
                return state != null ? state.Employee(id) : null;
            }

            private static int StaffLevel()
            {
                Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                return company != null ? company.Level : 1;
            }

            // A business's own manager, appointed in the Company app and found at work there, as the desk sessions ask.
            private static bool Manager(string place)
            {
                return Sessions.SessionBridges.ManagerAt(place, out string _, out int _);
            }

            private static bool Close(BuildingRegistration reg, int hours)
            {
                return BusinessDirector.Instance != null && BusinessDirector.Instance.CloseFor(reg, hours, ClosureSource.StaffEvent);
            }

            private static void Forget(int person)
            {
                try
                {
                    PartyDirector.Instance?.ForgetPerson(person);
                }
                catch (Exception e)
                {
                    Fault("PartyLinks.Forget", e);
                }
            }

            private static string PartyLabel()
            {
                return PartyDirector.On ? "reallife_party_people_host" : null;
            }

            private static void PartyOpen()
            {
                try
                {
                    PartyDirector.Open(RealLifeOverhaul.Ui.PartyPages.TabHost);
                }
                catch (Exception e)
                {
                    Fault("PartyLinks.PartyOpen", e);
                }
            }

            private static long RivalDay(RivalFile rivals, int day, int level)
            {
                try
                {
                    PartyDirector d = PartyDirector.Instance;
                    return d != null ? d.RivalDay(rivals, day, level) : 0L;
                }
                catch (Exception e)
                {
                    Fault("PartyLinks.RivalDay", e);
                    return 0L;
                }
            }

            // An outlet's staff event of yesterday, as a piece of the rivals' own for the morning's paper: read from the file only.
            private static int RivalPaper(int day, List<NewsItem> into, int most)
            {
                int added = 0;
                try
                {
                    PartyDirector d = PartyDirector.Instance;
                    PartyFile f = d != null && PartyDirector.On ? d.File : null;
                    for (int i = 0; f != null && i < f.Rivals.Count && added < most; i++)
                    {
                        RivalEvent e = f.Rivals[i];
                        if (e.Day != day - 1)
                        {
                            continue;
                        }
                        TycoonDef who = RivalCatalogue.Tycoons[RivalCatalogue.Clamp(e.Tycoon, 0, RivalCatalogue.TycoonCount - 1)];
                        into.Add(new NewsItem
                        {
                            Key = "reallife_party_paper_rival",
                            Data = new Dictionary<string, string>(4)
                            {
                                { "rival", who.Name }, { "holding", who.Holding }, { "kind", PartyText.T(PartyKeys.Kind(e.Kind)) }, { "count", PartyText.Whole(e.Heads) }
                            }
                        });
                        added++;
                    }
                }
                catch (Exception e)
                {
                    Fault("PartyLinks.RivalPaper", e);
                }
                return added;
            }

            /// <summary>
            /// The Planner's rows: a party the player hosts, an invitation they said yes to, and a company event, each on its
            /// day and hour at its place. It reads the director's file and changes nothing.
            /// </summary>
            private sealed class PartySource : IPlannerSource
            {
                public string Id => "parties";

                public bool Available => PartyDirector.On && PartyDirector.Instance.File != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    PartyFile f = PartyDirector.Instance.File;
                    for (int i = 0; i < f.Parties.Count; i++)
                    {
                        Party p = f.Parties[i];
                        if (p.Over || p.Day < fromDay || p.Day > toDay)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 0, p.Id, PlannerCategory.Social, p.Day);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Start = p.Hour * 60;
                        e.End = Math.Min(24 * 60 - 1, p.Hour * 60 + PartyCatalogue.Minutes[(int)p.Tier]);
                        e.JoinBy = Math.Min(24 * 60 - 1, p.Hour * 60 + PartyCatalogue.MissedMinutes);
                        e.Attend = true;
                        e.Done = p.State == PartyState.Running;
                        e.Street = p.Street;
                        e.Number = p.Number;
                        if (into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_party_plan_host", "count", PartyText.Whole(p.Coming));
                            e.Where = p.Name;
                            e.What = PlannerText.T("reallife_party_plan_host_what");
                            e.IfMissed = PlannerText.T("reallife_party_plan_host_missed");
                        }
                    }
                    for (int i = 0; i < f.Invites.Count; i++)
                    {
                        Invite v = f.Invites[i];
                        if (v.State != InviteState.Accepted || v.Day < fromDay || v.Day > toDay)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 1, v.Id, PlannerCategory.Social, v.Day);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Start = v.Hour * 60;
                        e.End = Math.Min(24 * 60 - 1, v.Hour * 60 + PartyCatalogue.InviteMinutes);
                        e.JoinBy = Math.Min(24 * 60 - 1, v.Hour * 60 + PartyCatalogue.MissedMinutes);
                        e.Attend = true;
                        e.Done = v.Began >= 0;
                        e.Street = v.Street;
                        e.Number = v.Number;
                        if (into.WantText)
                        {
                            SocialPerson host = SocialState.Get(v.Person);
                            e.Title = PlannerText.T("reallife_party_plan_invite", "name", host != null ? host.Name : string.Empty);
                            e.Where = v.Name;
                            e.What = PlannerText.T("reallife_party_plan_invite_what");
                            e.IfMissed = PlannerText.T("reallife_party_plan_invite_missed");
                        }
                    }
                    for (int i = 0; i < f.Events.Count; i++)
                    {
                        StaffEvent s = f.Events[i];
                        if ((s.State != StaffEventState.Planned && s.State != StaffEventState.Running) || s.Day < fromDay || s.Day > toDay)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 2, s.Id, PlannerCategory.Business, s.Day);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Start = s.Hour * 60;
                        e.End = Math.Min(24 * 60 - 1, s.Hour * 60 + PartyCatalogue.EventMinutes[(int)s.Kind]);
                        if (BusinessState.SplitPlace(s.Place, out string street, out int number))
                        {
                            e.Street = street;
                            e.Number = number;
                        }
                        if (into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_party_plan_event", "kind", PartyText.T(PartyKeys.Kind(s.Kind)));
                            e.Where = s.Name;
                            e.What = PlannerText.T("reallife_party_plan_event_what");
                        }
                    }
                }
            }
        }
    }
}
