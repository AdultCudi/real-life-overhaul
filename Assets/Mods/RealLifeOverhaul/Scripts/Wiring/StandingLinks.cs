using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Law;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Standing (FameBridges) with what it reads (Flare's followers, the staff, the buildings let, the brands), what
        /// moves it (good deeds, business pressure's issues closed, help given or refused), and what it moves: a visit's
        /// staff, footfall (on top of every other pairing's), business pressure's deal, the company's esteem, the paper,
        /// the Planner, people met at an evening, the council lobbied, bribes and their cases. Nothing per frame: read on
        /// Standing's daily round and when its page is shown.
        /// </summary>
        public static class StandingLinks
        {
            private const int DeedIntegrity = 2;       // integrity points for a good deed that counted
            private const int StrikeDays = 7;          // a strike this recent still counts against the workers' reputation

            internal static void Connect()
            {
                FameBridges.Followers = Followers;
                FameBridges.Staff = Staff;
                FameBridges.Holdings = Holdings;
                FameBridges.Brand = Brand;
                FameBridges.Visited = (place, points) => CompanyDirector.Instance != null ? CompanyDirector.Instance.Visited(place, points) : 0;
                FameBridges.Open = Finance.OpenStanding;
                EconomyBridges.Standing = new StandingPage { On = FamePage.On, Spec = FamePage.Spec, Write = FamePage.Write };
                Reputation.Deed += Deed;
                BusinessEvents.Closed += IssueClosed;
                ModState.HelpedNow += Helped;
                FameBridges.Need = Need;
                FameBridges.Story = Paper.Print;
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
                FameBridges.Footfall = Footfall;
                _weightBefore = BusinessDirector.WeightOf;
                BusinessDirector.WeightOf = Weight;
                CompanyBridges.Esteem = Esteem;
                PlannerDirector.Instance?.AddSource(Source);
                FameBridges.Meet = MeetAtEvening;
                FameBridges.Levers = Levers;
                FameBridges.Lobbied = (lever, position, day) => Settings.Civic && CivicDirector.Instance != null && CivicDirector.Instance.Government != null
                                                               && CivicDirector.Instance.Government.Lobbied((Lever)lever, position, day);
                FameBridges.Investigate = TaxCases.FileBribery;
                BusinessDirector.BribeAsk = (kind, place, ask, seed) => FameDirector.Instance != null ? FameDirector.Instance.BribeAsk(kind, place, ask, seed) : 0;
                BusinessDirector.Bribed = (kind, place, amount) => FameDirector.Instance?.Bribed(kind, place, amount);
            }

            internal static void Disconnect()
            {
                FameBridges.Reset();
                EconomyBridges.Standing = null;
                Reputation.Deed -= Deed;
                BusinessEvents.Closed -= IssueClosed;
                ModState.HelpedNow -= Helped;
                BusinessGame.Ambient = _ambientBefore;   // Flare's, the brands', the company's and the city's pairings take theirs off next
                _ambientBefore = null;
                BusinessDirector.WeightOf = _weightBefore;
                _weightBefore = null;
                CompanyBridges.Esteem = null;
                BusinessDirector.BribeAsk = null;
                BusinessDirector.Bribed = null;
            }

            // Somebody the player has still to meet is met at an evening, a place of meeting of its own.
            private static string MeetAtEvening()
            {
                try
                {
                    SocialPerson stranger = Settings.Social && Settings.SocialMeetings && SocialState.Loaded && SocialDirector.Instance != null ? SocialDirector.NextStranger() : null;
                    if (stranger == null)
                    {
                        return null;
                    }
                    stranger.Origin = SocialOrigin.Event;
                    SocialLife.Meet(stranger, true);
                    return stranger.Name;
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.MeetAtEvening", e);
                    return null;
                }
            }

            // The council's policies for business and growth, and better transport, that neither stand nor are on their way.
            private static void Levers(List<FameLever> into)
            {
                try
                {
                    CivicGovernment gov = Settings.Civic && CivicDirector.Instance != null ? CivicDirector.Instance.Government : null;
                    if (gov == null)
                    {
                        return;
                    }
                    for (int i = 0; i < CivicCatalogue.All.Length; i++)
                    {
                        PolicyDef p = CivicCatalogue.All[i];
                        bool wanted = p.Issue == Issue.Business || p.Issue == Issue.Growth || (p.Lever == Lever.Transit && p.Dir > 0);
                        if (wanted && !gov.Settled(p.Lever, p.Dir))
                        {
                            into.Add(new FameLever { Lever = (int)p.Lever, Position = p.Dir, Name = CivicText.T(p.Name), Effect = CivicText.Effect(p) });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Levers", e);
                }
            }

            private static Func<BuildingRegistration, int> _ambientBefore;
            private static Func<string, int, int> _weightBefore;
            private static readonly StandingSource Source = new StandingSource();

            private static int Ambient(BuildingRegistration reg)
            {
                int before = 0;
                try
                {
                    before = _ambientBefore != null ? _ambientBefore(reg) : 0;
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Ambient (the pairings before)", e);
                }
                try
                {
                    return before + FameDirector.FootfallOf(reg);
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Ambient", e);
                    return before;
                }
            }

            private static bool Footfall(BuildingRegistration reg, bool newDay)
            {
                try
                {
                    // On a new day a business the other pairings have had drawn already is not drawn again.
                    bool drawn = newDay && (CityEconomyDirector.FootfallOf(reg) != 0 || CompanyDirector.FootfallOf(reg) != 0 || BrandsDirector.FootfallOf(reg) != 0
                                            || FlareDirector.FootfallOf(reg) != 0 || Interiors.InteriorDirector.FootfallOf(reg) != 0
                                            || Rivals.RivalDirector.FootfallOf(reg) != 0);
                    int own, written;
                    return BusinessDirector.Footfall(reg, !drawn, newDay, out own, out written);
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Footfall", e);
                    return false;
                }
            }

            // A kind of issue's weight in business pressure's deal: the earlier pairings' first, then the player's standing.
            private static int Weight(string kind, int weight)
            {
                int w = weight;
                try
                {
                    w = _weightBefore != null ? _weightBefore(kind, weight) : weight;
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Weight (the pairings before)", e);
                }
                try
                {
                    FameFile f = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                    if (f == null)
                    {
                        return w;
                    }
                    switch (kind)
                    {
                        case BusinessCatalogue.Complaint: return (int)((long)w * FamePress.IssueScale(f, FameIssue.Complaint) / 1000);
                        case BusinessCatalogue.Praise: return (int)((long)w * FamePress.IssueScale(f, FameIssue.Praise) / 1000);
                        case BusinessCatalogue.Health:
                        case BusinessCatalogue.Fire:
                        case BusinessCatalogue.Licence: return (int)((long)w * FamePress.IssueScale(f, FameIssue.Inspection) / 1000);
                        case BusinessCatalogue.Audit: return (int)((long)w * FamePress.IssueScale(f, FameIssue.Audit) / 1000);
                        default: return w;
                    }
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Weight", e);
                    return w;
                }
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
                    Fault("StandingLinks.Esteem", e);
                    return 0;
                }
            }

            // Somebody in the street who needs help now: an accident nobody has helped at, or a crime's victim.
            private static bool Need(out UnityEngine.Vector3 where)
            {
                try
                {
                    return TrafficAccidents.Needing(out where) || CrimeDirector.Victim(out where);
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Need", e);
                    where = UnityEngine.Vector3.zero;
                    return false;
                }
            }

            private static void Helped()
            {
                try
                {
                    FameDirector.Instance?.Helped();
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Helped", e);
                }
            }

            /// <summary>
            /// Standing's rows of the Planner, read only. Ids, each with its number: 1 an invitation accepted, 2 one offered,
            /// 3 a promise, 4 a request, 5 a push on the council.
            /// </summary>
            private sealed class StandingSource : IPlannerSource
            {
                public string Id => "standing";

                public bool Available => Settings.Standing && FameDirector.Instance != null && FameDirector.Instance.File != null && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    FameFile f = FameDirector.Instance.File;
                    for (int i = 0; i < f.Invites.Count; i++)
                    {
                        FameInvite v = f.Invites[i];
                        if (v.State == FameInviteState.Accepted)
                        {
                            PlannerEntry e = into.Add(Id, 1, v.Id, PlannerCategory.City, v.Day);
                            if (e == null)
                            {
                                continue;
                            }
                            e.Start = v.Hour * 60;
                            e.End = ((v.Hour + 2) % 24) * 60;
                            e.Attend = true;
                            if (into.WantText)
                            {
                                e.Title = FameText.InviteName(v.Kind);
                                e.What = PlannerText.T("reallife_fame_plan_invite_what");
                                e.IfMissed = PlannerText.T("reallife_fame_plan_invite_missed");
                            }
                        }
                        else if (v.State == FameInviteState.Offered)
                        {
                            PlannerEntry e = into.Add(Id, 2, v.Id, PlannerCategory.City, v.AnswerBy);
                            if (e != null && into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_fame_plan_answer_title", "what", FameText.InviteName(v.Kind));
                                e.What = PlannerText.T("reallife_fame_plan_answer_what", "day", v.Day.ToString());
                            }
                        }
                    }
                    for (int i = 0; i < f.Requests.Count; i++)
                    {
                        FameRequest r = f.Requests[i];
                        if (r.State != FameRequestState.Open)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 4, r.Id, PlannerCategory.Money, r.Until);
                        if (e != null && into.WantText)
                        {
                            e.Title = FameText.RequestTitle(r);
                            e.What = PlannerText.T("reallife_fame_plan_request_what");
                            PlannerText.Costs(e, r.Amount, FameText.Money(r.Amount));
                        }
                    }
                    FameLobby lobby = FameCity.Pending(f);
                    if (lobby != null)
                    {
                        PlannerEntry e = into.Add(Id, 5, lobby.Id, PlannerCategory.City, lobby.DecideDay);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_fame_plan_lobby_title");
                            e.What = PlannerText.T("reallife_fame_plan_lobby_what", "chance", lobby.Chance.ToString());
                        }
                    }
                    for (int i = 0; i < f.Promises.Count; i++)
                    {
                        FamePromise p = f.Promises[i];
                        if (p.State != FamePromiseState.Open)
                        {
                            continue;
                        }
                        PlannerEntry e = into.Add(Id, 3, p.Id, PlannerCategory.Business, p.Due);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_fame_plan_promise_title");
                            e.What = PlannerText.T("reallife_fame_plan_promise_what", "promise", FameText.PromiseWords(p.Kind));
                            e.IfMissed = PlannerText.T("reallife_fame_plan_promise_missed");
                        }
                    }
                }
            }

            private static int Followers()
            {
                try
                {
                    FlareFile file = FlareDirector.Instance != null ? FlareDirector.Instance.File : null;
                    return file != null ? file.Followers : 0;
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Followers", e);
                    return 0;
                }
            }

            // A business's staff as the company counts them this morning.
            private static bool Staff(string place, out int count, out int standing, out int low, out bool strike)
            {
                count = standing = low = 0;
                strike = false;
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    CompanyShop shop = company != null && company.On(CompanyPart.Staff) ? company.ShopAt(place) : null;
                    if (shop == null || shop.Staff <= 0)
                    {
                        return false;
                    }
                    int day = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
                    count = shop.Staff;
                    standing = shop.Standing / shop.Staff;
                    low = shop.Low;
                    strike = shop.File.Striking(day) || (shop.File.StrikeEnd > 0 && day - shop.File.StrikeEnd < StrikeDays);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Staff", e);
                    return false;
                }
            }

            private static void Holdings(List<FameHolding> into)
            {
                try
                {
                    EstateState s = EstateDirector.Instance != null ? EstateDirector.Instance.State : null;
                    for (int i = 0; s != null && i < s.Holdings.Count; i++)
                    {
                        // Each unit has its own lease: let if any unit is, the asking rents averaged, the agreed rents summed.
                        EstateHolding h = s.Holdings[i];
                        bool let = false;
                        int ask = 0, rent = 0;
                        for (int u = 0; u < h.Units.Count; u++)
                        {
                            EstateUnit unit = h.Units[u];
                            ask += unit.Ask;
                            if (unit.Let)
                            {
                                let = true;
                                rent += unit.Rent;
                            }
                        }
                        into.Add(new FameHolding
                        {
                            District = h.District,
                            Let = let,
                            Ask = h.Managed && h.Units.Count > 0 ? ask / h.Units.Count : FameRules.AskMarket,
                            Rent = h.Managed ? rent : 0
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Holdings", e);
                }
            }

            // The best of the player's brands' recognition in each district, in points (the brands keep hundredths).
            private static void Brand(Dictionary<string, int> into)
            {
                try
                {
                    BrandsState s = BrandsDirector.Instance != null ? BrandsDirector.Instance.State : null;
                    for (int b = 0; s != null && b < s.Brands.Count; b++)
                    {
                        List<DistrictFile> districts = s.Brands[b].Districts;
                        for (int i = 0; i < districts.Count; i++)
                        {
                            int points = districts[i].Rec / 100, best;
                            if (!string.IsNullOrEmpty(districts[i].Name) && (!into.TryGetValue(districts[i].Name, out best) || points > best))
                            {
                                into[districts[i].Name] = points;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Brand", e);
                }
            }

            private static void Deed()
            {
                try
                {
                    FameDirector.Instance?.Moved(FamePart.Integrity, FameCause.GoodDeed, DeedIntegrity);
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Deed", e);
                }
            }

            // One of business pressure's issues has closed: what it says to the customers and the workers of the player.
            private static void IssueClosed(string kind, string place, string outcome)
            {
                try
                {
                    FameDirector fame = FameDirector.Instance;
                    if (fame == null || fame.File == null || outcome == null)
                    {
                        return;
                    }
                    switch (outcome)
                    {
                        case "reallife_biz_complaint_voucher_done": fame.Moved(FamePart.Customers, FameCause.Complaint, 2); break;
                        case "reallife_biz_complaint_ignored": fame.Moved(FamePart.Customers, FameCause.Complaint, -2); break;
                        case "reallife_biz_praise_shared": fame.Moved(FamePart.Customers, FameCause.Praise, 1); break;
                        case "reallife_biz_health_pass":
                        case "reallife_biz_health_pass_second":
                        case "reallife_biz_fire_pass": fame.Moved(FamePart.Customers, FameCause.Inspection, 1); break;
                        case "reallife_biz_health_fail":
                        case "reallife_biz_health_fail_closed":
                        case "reallife_biz_fire_fail":
                        case "reallife_biz_fire_fail_closed": fame.Moved(FamePart.Customers, FameCause.Inspection, -2); break;
                        case "reallife_biz_strike_raised":
                        case "reallife_biz_strike_bonus_done": fame.Moved(FamePart.Workers, FameCause.Strike, 2); break;
                        case "reallife_biz_strike_walkout":
                        case "reallife_biz_strike_backed_down": fame.Moved(FamePart.Workers, FameCause.Strike, -3); break;
                        case "reallife_biz_argument_festered":
                        case "reallife_biz_poach_lost": fame.Moved(FamePart.Workers, FameCause.Staff, -1); break;
                    }
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.IssueClosed", e);
                }
            }
        }
    }
}
