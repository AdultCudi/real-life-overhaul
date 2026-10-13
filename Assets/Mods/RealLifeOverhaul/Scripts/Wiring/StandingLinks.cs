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
        /// Standing, a city that reacts to the player's success (Scripts/Fame/FameBridges), with the rest of the mod.
        /// Standing calls no other feature and none calls it: what they have to do with each other is here, one line each.
        ///
        /// What Standing reads:
        ///   Followers     Flare's followers, a part of the player's recognition (FlareDirector's file)
        ///   Staff         each business's staff as the company knows them: their standing, those who stand low, a strike
        ///                 now or in the last week (CompanyDirector.ShopAt), for the workers' reputation
        ///   Holdings      the buildings the player lets and the rent asked against the market's (EstateDirector's state),
        ///                 for the neighbourhoods' opinion of rents raised
        ///   Brand         the player's brands' recognition in each district (BrandsDirector's state)
        ///   the page      the Standing page is a page of the Finance app (EconomyBridges.Standing, Finance.OpenStanding)
        ///
        /// What moves Standing, heard from the features that keep it:
        ///   Deed          a good deed that counted (Reputation.Deed): integrity
        ///   Closed        one of business pressure's issues closed (BusinessEvents.Closed): a complaint answered or
        ///                 left, praise shared, an inspection passed or failed (the customers), a strike settled or
        ///                 refused, an argument left to fester, somebody poached (the workers)
        ///
        ///   HelpedNow     the player helped somebody (ModState.HelpedNow): walking on afterwards is no refusal
        ///   Need          an accident nobody has helped at yet (TrafficAccidents.Needing) or a crime with a victim in the
        ///                 street (CrimeDirector.Victim): walking past it in front of others costs integrity
        ///
        /// What Standing does elsewhere:
        ///   Visited       a visit to a business whose staff are proud or nervous: their standing a point either way,
        ///                 through the company (CompanyDirector.Visited)
        ///   Footfall      Standing's part of a business's customers (the customers' reputation and the district's
        ///                 opinion, 5% at most either way) is added to <see cref="BusinessGame.Ambient"/> after everybody
        ///                 else's and written by business pressure's own writer (BusinessDirector.Footfall), which keeps the
        ///                 sum inside its own bounds; on a new day a business the others have had drawn already is not drawn again
        ///   WeightOf      business pressure's deal: complaints by the customers and the neighbourhoods, praise by the
        ///                 customers, inspections, licences and audits by integrity and more for somebody well known
        ///                 (BusinessDirector.WeightOf, after the city's economy's own), 0.6 to 1.8 times
        ///   Esteem        the workers' reputation, -10 to 10, for the company's hiring and its rivals (CompanyBridges.Esteem)
        ///   Story         a piece for the City Ledger (Paper.Print, NewsKind.Story)
        ///   the Planner   an invitation accepted is an appointment, one offered a deadline, a promise its day (a source of its own)
        ///
        /// Everything is read from what those features already keep in public or heard through an event they already
        /// raise; nothing of theirs is changed but through an entry of theirs. COST: nothing per frame. The reads are made
        /// once on Standing's daily round (a walk over the player's businesses and buildings) and the page when it is shown.
        /// </summary>
        public static class StandingLinks
        {
            private const int DeedIntegrity = 2;
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
            }

            internal static void Disconnect()
            {
                FameBridges.Reset();
                EconomyBridges.Standing = null;
                Reputation.Deed -= Deed;
                BusinessEvents.Closed -= IssueClosed;
                ModState.HelpedNow -= Helped;
                BusinessGame.Ambient = _ambientBefore;   // (Flare's, the brands', the company's and the city's own pairings take theirs off in a moment)
                _ambientBefore = null;
                BusinessDirector.WeightOf = _weightBefore;
                _weightBefore = null;
                CompanyBridges.Esteem = null;
            }

            private static Func<BuildingRegistration, int> _ambientBefore;
            private static Func<string, int, int> _weightBefore;
            private static readonly StandingSource Source = new StandingSource();

            private static int Ambient(BuildingRegistration reg)
            {
                return (_ambientBefore != null ? _ambientBefore(reg) : 0) + FameDirector.FootfallOf(reg);
            }

            private static bool Footfall(BuildingRegistration reg, bool newDay)
            {
                try
                {
                    bool drawn = newDay && (CityEconomyDirector.FootfallOf(reg) != 0 || CompanyDirector.FootfallOf(reg) != 0 || BrandsDirector.FootfallOf(reg) != 0 || FlareDirector.FootfallOf(reg) != 0);
                    int own, written;
                    return BusinessDirector.Footfall(reg, !drawn, newDay, out own, out written);
                }
                catch (Exception e)
                {
                    Fault("StandingLinks.Footfall", e);
                    return false;
                }
            }

            // Business pressure's share of the deal for a kind of issue: the city's economy's first, then the player's standing on it.
            private static int Weight(string kind, int weight)
            {
                int w = _weightBefore != null ? _weightBefore(kind, weight) : weight;
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

            private static int Esteem()
            {
                FameFile f = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                return f != null ? FamePress.Esteem(f) : 0;
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
                FameDirector.Instance?.Helped();
            }

            /// <summary>
            /// Standing's rows of the Planner: an invitation accepted on its day and hour (to be there), one offered on the
            /// last day to accept it, an open promise on its day. It reads the file and changes nothing. Ids: 1 an
            /// invitation accepted, 2 one offered, 3 a promise, with its number.
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
                FlareFile file = FlareDirector.Instance != null ? FlareDirector.Instance.File : null;
                return file != null ? file.Followers : 0;
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
                EstateState s = EstateDirector.Instance != null ? EstateDirector.Instance.State : null;
                for (int i = 0; s != null && i < s.Holdings.Count; i++)
                {
                    EstateHolding h = s.Holdings[i];
                    into.Add(new FameHolding { District = h.District, Let = h.Let, Ask = h.Managed ? h.Ask : FameRules.AskMarket, Rent = h.Managed && h.Let ? h.Rent : 0 });
                }
            }

            // The best of the player's brands' recognition in each district, points (the brands keep hundredths).
            private static void Brand(Dictionary<string, int> into)
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

            private static void Deed()
            {
                FameDirector.Instance?.Moved(FamePart.Integrity, FameCause.GoodDeed, DeedIntegrity);
            }

            // One of business pressure's issues has closed: what it says about the customers' and the workers' view of the player.
            private static void IssueClosed(string kind, string place, string outcome)
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
        }
    }
}
