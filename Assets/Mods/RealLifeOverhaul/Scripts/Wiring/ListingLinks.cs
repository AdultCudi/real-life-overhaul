using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The player's own company on the city's market (MarketBridges) with their books (the game's daily statements, of
        /// the player's own businesses only, never another player's in a session), the facts the advisers and auditors
        /// read, what the city made of the founder, a sale's hand-over, the Company app's line and the Planner. Asked once
        /// a game day, at a market day's open, or when the app is opened.
        /// </summary>
        public static class ListingLinks
        {
            /// <summary>
            /// The player's company's days in the Planner. Read only. Ids: 1 results, 2 the yearly meeting, 3 an investor's
            /// meeting, 4 a bid's last day, 5 the audit signed, 6 the book priced, 7 the first day, 8 the end of the lock-up,
            /// 9 a hand-over's last day.
            /// </summary>
            private sealed class ListingSource : IPlannerSource
            {
                public string Id => "listing";

                public bool Available => Settings.Markets && MarketDirector.Instance != null && MarketDirector.Instance.State != null && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    MarketDirector market = MarketDirector.Instance;
                    ListingFile f = market.Listing;
                    Stock own = market.MyCompany;
                    if (f == null)
                    {
                        return;
                    }
                    if (own != null)
                    {
                        OwnListing o = own.Own;
                        Day(into, 1, own.ResultsDay, MarketCatalogue.ResultsMinute, false, "reallife_lst_plan_results_title", "reallife_lst_plan_results_what", own.Name);
                        Day(into, 2, o.AgmDay, MarketCatalogue.CloseMinute, true, "reallife_lst_plan_agm_title", "reallife_lst_plan_agm_what", own.Name);
                        Day(into, 3, o.ActBp > 0 ? o.ActDay : 0, MarketCatalogue.CloseMinute, true, "reallife_lst_plan_egm_title", "reallife_lst_plan_egm_what",
                            ListingText.Demand(o.ActDemand));
                        Day(into, 4, own.BidCents > 0 ? own.BidEndDay : 0, MarketCatalogue.CloseMinute, true, "reallife_lst_plan_bid_title", "reallife_lst_plan_bid_what",
                            MarketText.Price(own.BidCents));
                        Day(into, 8, o.LockDay > SaveGameManager.Current.Day ? o.LockDay : 0, 0, false, "reallife_lst_plan_lockup_title", "reallife_lst_plan_lockup_what", own.Name);
                    }
                    Day(into, 5, f.Stage == ListingStage.Auditing ? f.AuditDone : 0, 0, false, "reallife_lst_plan_audit_title", "reallife_lst_plan_audit_what", f.Name);
                    Day(into, 6, f.Stage == ListingStage.Building ? f.BookEnd : 0, MarketCatalogue.OpenMinute, false, "reallife_lst_plan_book_title", "reallife_lst_plan_book_what", f.Name);
                    Day(into, 7, f.Stage == ListingStage.Priced ? f.ListDay : 0, MarketCatalogue.OpenMinute, false, "reallife_lst_plan_first_title", "reallife_lst_plan_first_what",
                        MarketText.Price(f.PriceCents));
                    Day(into, 9, f.HandOver.Count > 0 ? f.HandOverBy : 0, 0, true, "reallife_lst_plan_handover_title", "reallife_lst_plan_handover_what", f.HandOverTo ?? string.Empty);
                }

                private void Day(PlannerList into, int kind, int day, int minute, bool deadline, string titleKey, string whatKey, string name)
                {
                    if (day <= 0)
                    {
                        return;
                    }
                    PlannerEntry e = into.Add(Id, kind, 0, PlannerCategory.Money, day);
                    if (e == null)
                    {
                        return;
                    }
                    if (deadline)
                    {
                        e.Deadline = true;
                        e.End = minute;
                    }
                    else
                    {
                        e.Start = minute;
                    }
                    if (into.WantText)
                    {
                        e.Title = PlannerText.T(titleKey, "name", name);
                        e.What = PlannerText.T(whatKey, "name", name);
                    }
                }
            }

            private static readonly ListingSource Source = new ListingSource();
            private static readonly List<BuildingRegistration> Businesses = new List<BuildingRegistration>(16);
            private static readonly List<int> FirstDays = new List<int>(16);
            private static readonly List<string> Keys = new List<string>(16);

            // The causes of a reputation move that came from what the press made of the player.
            private static readonly FameCause[] PressCauses = { FameCause.Press, FameCause.Reported, FameCause.Interview, FameCause.Show, FameCause.Magazine };

            internal static void Connect()
            {
                MarketBridges.Books = Statements;
                MarketBridges.Facts = Facts;
                MarketBridges.Businesses = Places;
                MarketBridges.HandOver = HandOver;
                MarketBridges.Name = Name;
                MarketBridges.Trade = Trade;
                CompanyBridges.ListingLine = Line;
                CompanyBridges.OpenListing = () => MarketDirector.Instance?.OpenListing();
                ServicesBridges.Sold = (place, price) => MarketDirector.Instance?.BusinessSold(price);
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                MarketBridges.Books = null;
                MarketBridges.Facts = null;
                MarketBridges.Businesses = null;
                MarketBridges.HandOver = null;
                MarketBridges.Name = null;
                MarketBridges.Trade = null;
                CompanyBridges.ListingLine = null;
                CompanyBridges.OpenListing = null;
                ServicesBridges.Sold = null;
            }

            // The Company app's line: the market's own words for where the player's company stands.
            private static string Line()
            {
                try
                {
                    MarketDirector market = MarketDirector.Instance;
                    return market != null && market.Running ? market.ListingSummary() : null;
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Line", e);
                    return null;
                }
            }

            // The player's own businesses now, by the key the statements' addresses are compared under.
            private static void Mine()
            {
                Books.Businesses(Businesses);
                Keys.Clear();
                for (int i = 0; i < Businesses.Count; i++)
                {
                    if (Businesses[i] != null && Businesses[i].Address != null)
                    {
                        Keys.Add(Books.Key(Businesses[i].Address));
                    }
                }
            }

            private static OwnBooks Statements(int from, int to)
            {
                var b = new OwnBooks();
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save == null || save.financialSummaries == null || to <= from)
                    {
                        return b;
                    }
                    Mine();
                    if (Keys.Count == 0)
                    {
                        return b;
                    }
                    List<FinancialSummary> all = save.financialSummaries;
                    double revenue = 0.0, profit = 0.0;
                    for (int i = all.Count - 1; i >= 0; i--)
                    {
                        FinancialSummary day = all[i];
                        if (day == null || day.businessIncomeStatements == null)
                        {
                            continue;
                        }
                        if (day.dayNumber < from - 7)
                        {
                            break;   // the statements are in order of days: none older is wanted
                        }
                        if (day.dayNumber < from || day.dayNumber >= to)
                        {
                            continue;
                        }
                        bool counted = false;
                        for (int k = 0; k < day.businessIncomeStatements.Count; k++)
                        {
                            FinancialSummary.BusinessIncomeStatement s = day.businessIncomeStatements[k];
                            if (s != null && s.Address != null && Keys.Contains(Books.Key(s.Address)))
                            {
                                revenue += Math.Max(0f, s.TotalSales);
                                profit += s.TotalProfit;
                                counted = true;
                            }
                        }
                        b.Days += counted ? 1 : 0;
                    }
                    MarketsBuildLinks.ConstructionBooks(Businesses, from, to, ref revenue, ref profit);   // a construction company's works are not in its office's statement
                    b.Revenue = (long)Math.Round(revenue);
                    b.Profit = (long)Math.Round(profit);
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Statements", e);
                }
                return b;
            }

            private static ListingFacts Facts()
            {
                var f = new ListingFacts { Standing = -1 };
                GameInstance save = SaveGameManager.Current;
                if (save == null)
                {
                    return f;
                }
                int day = save.Day, year = Math.Max(8, GameCalendar.DaysPerYear);
                try
                {
                    // Every business of the player's still running counts for the years, whatever it sells: Books.Businesses leaves out one the game does not
                    // tag as taking money (an older office, say), and the oldest then looked like the newest. Each counts from the day it started, which the
                    // game forgets when the moving company moves the business (Core/FoundingGame keeps it).
                    FirstDays.Clear();
                    for (int i = 0; save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
                    {
                        BuildingRegistration reg = save.BuildingRegistrations[i];
                        if (reg != null && GoingPublic.Mine(reg) && reg.RentedByPlayer && reg.HasEstablishedBusiness && reg.HasValidAddress && !string.IsNullOrEmpty(reg.businessTypeName)
                            && GameRegistrations.Found(reg))
                        {
                            FirstDays.Add(FoundingGame.Of(reg));
                        }
                    }
                    f.FirstDay = ListingBook.EarliestDay(FirstDays);
                    FirstDays.Clear();
                    f.BackTaxes = save.currentBackTaxes > 0.5f || (save.currentUnpaidTaxes != null && day > save.currentUnpaidTaxes.dueDay);
                    for (int i = 0; save.Loans != null && i < save.Loans.Count; i++)
                    {
                        f.Debt += save.Loans[i] != null ? (long)Math.Max(0f, save.Loans[i].remainingAmount) : 0L;
                    }
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Facts (the game's figures)", e);
                }
                try
                {
                    CompanyState company = CompanyDirector.Instance != null ? CompanyDirector.Instance.State : null;
                    if (company != null)
                    {
                        f.LateReturns = company.Tax.LateRun;
                        f.BackTaxes |= company.Tax.Owed > 0;
                        for (int i = 0; Settings.EconomyCompanyHide && i < company.Shops.Count; i++)
                        {
                            f.CashOffBooks |= company.Shops[i].Hide > 0;
                        }
                    }
                    CreditFile credit = CreditLedger.File;
                    if (credit != null)
                    {
                        f.Bankrupt = credit.Relief.Arranged || (credit.Relief.BankruptDay > 0 && day - credit.Relief.BankruptDay < 2 * year);
                    }
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Facts (the company and the credit file)", e);
                }
                try
                {
                    FameFile fame = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                    if (fame != null)
                    {
                        f.Standing = fame.Overall;
                        for (int i = 0; i < fame.Bribes.Count; i++)
                        {
                            f.BribesOut += fame.Bribes[i].Surfaced >= 0 && day - fame.Bribes[i].Surfaced <= year ? 1 : 0;
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Facts (Standing)", e);
                }
                return f;
            }

            /// <summary>
            /// The day's city for the market (MarketLinks.City): the company's statements since the market last counted its
            /// business, and what the city made of its founder. Nothing unless the player has a company listed or a book open.
            /// </summary>
            internal static void Env(ref MarketEnv e, int day)
            {
                try
                {
                    MarketDirector market = MarketDirector.Instance;
                    MarketState s = market != null ? market.State : null;
                    if (s == null || s.Following)
                    {
                        return;
                    }
                    ListingFile f = s.Listing;
                    bool book = f.Stage == ListingStage.Building || f.Stage == ListingStage.Priced;
                    if (!book && market.MyCompany == null)
                    {
                        return;
                    }
                    int from = s.BusinessDay;
                    if (f.SetAsideDay > 0)
                    {
                        from = Math.Min(from, f.SetAsideDay);   // back from another player's city: the days away are counted too
                        f.SetAsideDay = 0;
                    }
                    from = Math.Max(from, day - 400);
                    OwnBooks b = Statements(from, day);
                    e.OwnFrom = from;
                    e.OwnDays = Math.Max(0, day - from);
                    e.OwnRev = b.Revenue;
                    e.OwnProfit = b.Profit;
                    e.OwnDebt = Facts().Debt;
                    FameFile fame = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                    if (fame != null)
                    {
                        e.OwnStanding = fame.Overall;
                        int press = 0;
                        for (int i = 0; i < fame.Notes.Count; i++)
                        {
                            FameNote n = fame.Notes[i];
                            if (n != null && n.Day >= from && n.Day < day && n.Part >= 0 && Array.IndexOf(PressCauses, n.Cause) >= 0)
                            {
                                press += n.Delta * 10;
                            }
                        }
                        e.OwnPress = press;
                        for (int i = 0; i < fame.Bribes.Count; i++)
                        {
                            e.OwnScandal |= fame.Bribes[i].Surfaced >= from && fame.Bribes[i].Surfaced < day ? 1 : 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Fault("ListingLinks.Env", ex);
                }
            }

            private static void Places(List<string> into)
            {
                try
                {
                    into.Clear();
                    Books.Businesses(Businesses);
                    for (int i = 0; i < Businesses.Count; i++)
                    {
                        if (Businesses[i] != null)
                        {
                            into.Add(ServicesGame.Place(Businesses[i]));
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Places", e);
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
                    Fault("ListingLinks.HandOver", e);
                    return false;
                }
            }

            // The player's largest business by yesterday's takings.
            private static BuildingRegistration Largest()
            {
                Books.Businesses(Businesses);
                BuildingRegistration best = null;
                float most = -1f;
                for (int i = 0; i < Businesses.Count; i++)
                {
                    BuildingRegistration r = Businesses[i];
                    float sales = r != null ? Books.SalesYesterday(r.Address) : -1f;
                    if (r != null && sales > most)
                    {
                        most = sales;
                        best = r;
                    }
                }
                return best;
            }

            private static string Name()
            {
                try
                {
                    BuildingRegistration r = Largest();
                    return r != null ? ServicesGame.Name(r) : string.Empty;
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Name", e);
                    return string.Empty;
                }
            }

            private static int Trade()
            {
                try
                {
                    BuildingRegistration r = Largest();
                    return r != null ? MarketInputs.TradeOf(r.businessTypeName) : -1;
                }
                catch (Exception e)
                {
                    Fault("ListingLinks.Trade", e);
                    return -1;
                }
            }
        }
    }
}
