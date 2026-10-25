using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The tech sector and the professions (TechBridges) with Standing (esteem as an employer, its parts moved), the
        /// game's rivals, the company's own offers (<see cref="Holds"/> the other way), the books, the stock market, the
        /// courts, the company's cover, power, water and the internet, and the paper. The headquarters are chosen only once
        /// the utilities office, the courthouse, the police stations and the university have chosen theirs.
        /// </summary>
        public static class TechLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<TechStory> Stories = new List<TechStory>(4);

            internal static void Connect()
            {
                TechBridges.Esteem = Esteem;
                TechBridges.Rival = Rival;
                TechBridges.Standing = Standing;
                TechBridges.Held = CompanyLinks.Holds;
                TechBridges.Books = Books;
                TechBridges.Stock = Stock;
                TechBridges.Prosecute = TaxCases.FileDataProtection;
                TechBridges.Claim = Claim;
                TechBridges.Fame = Fame;
                TechBridges.Supplied = Supplied;
                TechBridges.Ipo = Ipo;
                TechBridges.Mbps = Mbps;
                TechBridges.PlanMbps = reg => UtilityLinks.Speed(reg, -1, false);
                TechBridges.Net = Net;
                TechBridges.NetDays = () => Utilities.InternetCatalogue.InstallDays;
                TechBridges.OpenInternet = () => Utilities.UtilitiesDirector.Instance != null && Utilities.UtilitiesDirector.Instance.OpenInternet();
                TechBridges.UtilitiesByPhone = () => Utilities.UtilitiesDirector.Instance != null && Utilities.UtilitiesDirector.Instance.State != null
                                                     && Utilities.UtilitiesDirector.Instance.ByPhone;
                TechBridges.OpenRota = OpenRota;
                TechBridges.OthersPicked = OthersPicked;
                TechBridges.Employer = Employer;
                TechBridges.UtilityCents = (service, use) => Utilities.UtilitiesRules.DayCents(service == 0 ? Utilities.Service.Power : Utilities.Service.Water, use, Prices.Level, false);
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                TechBridges.Reset();
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static int Esteem()
            {
                try
                {
                    FameFile f = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                    return f != null ? FamePress.Esteem(f) : 0;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Esteem", e);
                    return 0;
                }
            }

            private static string Rival(BuildingRegistration reg)
            {
                try
                {
                    RivalData rival = reg != null ? BusinessGame.AnyRival(reg) : null;
                    return rival != null ? rival.rivalName : null;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Rival", e);
                    return null;
                }
            }

            /// <summary>
            /// The tech sector's own rival's offer is open to an employee (the game's id), or a departure is in hand for them:
            /// the company and business pressure make them no offer then.
            /// </summary>
            internal static bool Holds(string id)
            {
                try
                {
                    TechDirector director = TechDirector.Instance;
                    return director != null && director.Holds(id);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Holds", e);
                    return false;
                }
            }

            // A firm's listing is the player's company's: its page opened, while the stock market and its listing run.
            private static bool Ipo()
            {
                try
                {
                    Markets.MarketDirector m = Markets.MarketDirector.Instance;
                    if (m == null || !Settings.MarketsListing || m.Listing == null)
                    {
                        return false;
                    }
                    m.OpenListing();
                    return true;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Ipo", e);
                    return false;
                }
            }

            // The tech company of the player's job, by name: its floor of a tower is offered as theirs.
            private static string Employer()
            {
                try
                {
                    Careers.WorkFile work = Careers.CareersDirector.Instance != null ? Careers.CareersDirector.Instance.Work : null;
                    Careers.Job job = work != null ? work.Job : null;
                    return job != null && job.Post != null && job.Post.Kind == (int)Careers.EmployerKind.TechFirm ? job.Post.Employer : null;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Employer", e);
                    return null;
                }
            }

            private static bool OthersPicked()
            {
                try
                {
                    Careers.CareersDirector careers = Careers.CareersDirector.Instance;
                    bool campus = !Careers.CareersDirector.UniversityOpen || (careers != null && careers.University != null && careers.University.Searched);
                    return OfficeLinks.CityOfficesPicked() && campus;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.OthersPicked", e);
                    return false;
                }
            }

            private static bool Supplied(BuildingRegistration reg, int service)
            {
                try
                {
                    return Utilities.UtilitiesDirector.Supplied(reg, service == 0 ? Utilities.Service.Power : Utilities.Service.Water);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Supplied", e);
                    return true;
                }
            }

            // Why a business that needs the internet is closed for want of it, as the utilities close it each hour: 1 no
            // plan, 2 one being installed, 3 an outage. 0 for one that needs none, one still in its grace, or utilities off.
            private static int Net(BuildingRegistration reg, out int when)
            {
                when = 0;
                try
                {
                    Utilities.UtilitiesDirector d = Utilities.UtilitiesDirector.Instance;
                    Utilities.UtilitiesState state = d != null ? d.State : null;
                    if (state == null || !Settings.Utilities || reg == null || Array.IndexOf(Utilities.InternetCatalogue.Needs, reg.businessTypeName) < 0)
                    {
                        return 0;
                    }
                    string place = Utilities.UtilitiesGame.Place(reg);
                    int today = SaveGameManager.Current.Day;
                    if (Utilities.InternetRules.InGrace(state, place, today))
                    {
                        return 0;
                    }
                    Utilities.Account a = state.Find(place, Utilities.Service.Internet);
                    if (a == null || a.State == Utilities.SupplyState.Closed || a.State == Utilities.SupplyState.Cut)
                    {
                        return 1;
                    }
                    if (a.State == Utilities.SupplyState.Pending)
                    {
                        when = a.ConnectDay;
                        return 2;
                    }
                    int hour = today * 24 + SaveGameManager.Current.Hour;
                    if (Utilities.InternetRules.InOutage(a, hour))
                    {
                        when = a.OutageUntil % 24;
                        return 3;
                    }
                    return 0;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Net", e);
                    return 0;
                }
            }

            // The Company app's Rota tab with a business picked, while the company runs.
            private static bool OpenRota(string place)
            {
                try
                {
                    Economy.CompanyDirector company = Economy.CompanyDirector.Instance;
                    if (company == null || !company.Running)
                    {
                        return false;
                    }
                    company.Open(Economy.CompanyDirector.TabRota, "rota:" + place);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.OpenRota", e);
                    return false;
                }
            }

            // A building's connection: its plan's speed while it is up, 0 with none, cut or in an outage.
            private static int Mbps(BuildingRegistration reg)
            {
                try
                {
                    Utilities.UtilitiesDirector d = Utilities.UtilitiesDirector.Instance;
                    Utilities.UtilitiesState state = d != null ? d.State : null;
                    if (state == null || !Settings.Utilities || reg == null)
                    {
                        return int.MaxValue;   // no utilities to ask: the data centre is taken to be connected
                    }
                    Utilities.Account a = state.Find(Utilities.UtilitiesGame.Place(reg), Utilities.Service.Internet);
                    if (a == null || a.State != Utilities.SupplyState.Active || Utilities.InternetRules.InOutage(a, SaveGameManager.Current.Day * 24 + SaveGameManager.Current.Hour))
                    {
                        return 0;
                    }
                    Utilities.Provider p = Utilities.InternetRules.Get(a.Provider);
                    return p != null && p.Mbps != null && a.Plan >= 0 && a.Plan < p.Mbps.Length ? p.Mbps[a.Plan] : 0;
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Mbps", e);
                    return int.MaxValue;
                }
            }

            private static void Books(int day)
            {
                try
                {
                    Economy.GameBooks.Refresh(day);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Books", e);
                }
            }

            private static void Stock(int stock, int bp)
            {
                try
                {
                    Markets.MarketDirector.Instance?.Engine?.Event(Markets.MarketInputs.EventCompany, stock, bp * 100);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Stock", e);
                }
            }

            private static void Claim(BuildingRegistration reg, int amount)
            {
                try
                {
                    Economy.CompanyDirector.Instance?.Loss(reg, Economy.CoverKind.Liability, amount);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Claim", e);
                }
            }

            private static void Fame(int part, int points)
            {
                try
                {
                    if (points != 0)
                    {
                        FameDirector.Instance?.Moved((FamePart)part, points < 0 ? FameCause.Scandal : FameCause.Press, points);
                    }
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Fame", e);
                }
            }

            private static void Standing(int points)
            {
                try
                {
                    if (points != 0)
                    {
                        FameDirector.Instance?.Moved(FamePart.Workers, FameCause.Staff, points);
                    }
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Standing", e);
                }
            }

            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Paper (the pieces before)", e);
                }
                try
                {
                    TechDirector director = TechDirector.Instance;
                    if (director == null)
                    {
                        return;
                    }
                    Stories.Clear();
                    director.Stories(day, Stories);
                    AddStories(Stories, into);
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("TechLinks.Paper", e);
                }
            }

            /// <summary>Stories of the tech sector or of careers as the paper's pieces, with their one or two names.</summary>
            internal static void AddStories(List<TechStory> stories, List<NewsItem> into)
            {
                for (int i = 0; i < stories.Count; i++)
                {
                    TechStory s = stories[i];
                    var data = new Dictionary<string, string>(2);
                    if (s.AName.Length > 0)
                    {
                        data[s.AName] = s.A;
                    }
                    if (s.BName.Length > 0)
                    {
                        data[s.BName] = s.B;
                    }
                    into.Add(new NewsItem { Key = s.Key, Data = data });
                }
            }
        }
    }
}
