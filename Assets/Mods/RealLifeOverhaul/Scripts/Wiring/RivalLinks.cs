using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The rival tycoons (RivalBridges): every move goes through the feature that owns what it touches, and decides by
        /// its own rules: the stock market (holdings, a stake, a bid, a takeover), the company (an offer to the staff),
        /// business pressure (a smear, inspections, lobbying, footfall), property prices, the courts, Standing, the paper
        /// and the Finance app's page. Nothing per frame; each answer is read when its feature asks.
        /// </summary>
        public static class RivalLinks
        {
            private static Func<BuildingRegistration, int> _ambientBefore;
            private static Func<string, int, int> _weightBefore;
            private static Action<int, List<NewsItem>> _extraBefore;

            internal static void Connect()
            {
                RivalBridges.Worth = Books.NetWorth;
                RivalBridges.Cycle = () => (int)CityEconomy.State;
                RivalBridges.Playthrough = Playthrough;
                RivalBridges.MarketHere = () => Settings.Markets && MarketDirector.Instance != null && MarketDirector.Instance.RivalsMayAct;
                RivalBridges.Listed = () => MarketDirector.Instance != null && MarketDirector.Instance.MyCompany != null;
                RivalBridges.CompanyDays = CompanyDays;
                RivalBridges.YearDays = YearDays;
                RivalBridges.ListHolding = ListHolding;
                RivalBridges.DelistHolding = DelistHolding;
                RivalBridges.Holding = Holding;
                RivalBridges.Stake = id => MarketDirector.Instance != null && MarketDirector.Instance.RivalStake(id);
                RivalBridges.Bid = id => MarketDirector.Instance != null && MarketDirector.Instance.RivalBid(id);
                RivalBridges.TakeOver = (id, cost) => MarketDirector.Instance != null && MarketDirector.Instance.TakeOverHolding(id, cost);
                RivalBridges.OpenStock = id => MarketDirector.Instance?.OpenCompany(id);
                RivalBridges.IssuesOn = () => Settings.Business && BusinessDirector.Instance != null;
                RivalBridges.Smear = place => Start(BusinessCatalogue.Smear, place);
                RivalBridges.Inspect = place => Start(BusinessCatalogue.Health, place) || Start(BusinessCatalogue.Fire, place) || Start(BusinessCatalogue.Licence, place);
                RivalBridges.Footfall = Footfall;
                RivalBridges.StaffOn = () => CompanyDirector.Instance != null && CompanyDirector.Instance.On(CompanyPart.Staff);
                RivalBridges.Offer = Offer;
                RivalBridges.CourtOn = () => DebtClaims.Available;
                RivalBridges.Sue = RivalClaims.File;
                RivalBridges.Withdraw = Withdraw;
                RivalBridges.Revalue = Revalue;
                RivalBridges.Customers = delta => FameDirector.Instance?.Moved(FamePart.Customers, FameCause.Press, delta);
                RivalBridges.PaperOn = () => Settings.SocialNewspaper;
                RivalBridges.ShowList = ShowList;
                RivalBridges.Open = Finance.OpenRivals;
                EconomyBridges.Rivals = new StandingPage { On = RivalPage.On, Spec = RivalPage.Spec, Write = RivalPage.Write };
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
                _weightBefore = BusinessDirector.WeightOf;
                BusinessDirector.WeightOf = Weight;
                EstateBridges.Premium = RivalDirector.PremiumOf;
                _extraBefore = News.Extra;
                News.Extra = Extra;
            }

            internal static void Disconnect()
            {
                RivalBridges.Reset();
                EconomyBridges.Rivals = null;
                BusinessGame.Ambient = _ambientBefore;   // the brands', the company's and the city's pairings take theirs off next
                _ambientBefore = null;
                BusinessDirector.WeightOf = _weightBefore;
                _weightBefore = null;
                EstateBridges.Premium = null;
                News.Extra = _extraBefore;
                _extraBefore = null;
            }

            // A test's claim withdrawn: the claimant is the holding that filed it, read from the court's own record of it.
            private static bool Withdraw(int number)
            {
                try
                {
                    RivalDirector d = RivalDirector.Instance;
                    RivalFile f = d != null ? d.File : null;
                    string claimant = string.Empty;
                    for (int i = 0; f != null && i < f.Moves.Count; i++)
                    {
                        if (f.Moves[i].Kind == RivalMoveKind.Suit && f.Moves[i].Second == number)
                        {
                            claimant = RivalCatalogue.Tycoons[f.Moves[i].Tycoon].Holding;
                        }
                    }
                    return RivalClaims.Withdraw(number, claimant);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Withdraw", e);
                    return false;
                }
            }

            private static int CompanyDays()
            {
                try
                {
                    return MarketDirector.Instance != null ? MarketDirector.Instance.CompanyDays() : -1;
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.CompanyDays", e);
                    return -1;
                }
            }

            private static int YearDays()
            {
                try
                {
                    return MarketDirector.Instance != null ? MarketDirector.Instance.YearDays : 365;
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.YearDays", e);
                    return 365;
                }
            }

            private static bool ListHolding(int id, long worth)
            {
                try
                {
                    return MarketDirector.Instance != null && MarketDirector.Instance.ListHolding(id, worth);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.ListHolding", e);
                    return false;
                }
            }

            private static bool DelistHolding(int id)
            {
                try
                {
                    return MarketDirector.Instance != null && MarketDirector.Instance.DelistHolding(id);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.DelistHolding", e);
                    return false;
                }
            }

            private static void Revalue()
            {
                try
                {
                    EstateDirector.Instance?.Revalue();
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Revalue", e);
                }
            }

            private static ulong Playthrough()
            {
                try
                {
                    MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
                    return state != null ? state.Id : 0UL;
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Playthrough", e);
                    return 0UL;
                }
            }

            private static bool Holding(int id, out long shares, out long held, out long most, out int midCents)
            {
                shares = held = most = 0;
                midCents = 0;
                try
                {
                    return MarketDirector.Instance != null && MarketDirector.Instance.Holding(id, out shares, out held, out most, out midCents);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Holding", e);
                    return false;
                }
            }

            private static bool Offer(string rival, int premium, out string name, out string business)
            {
                name = business = null;
                try
                {
                    return CompanyDirector.Instance != null && CompanyDirector.Instance.RivalOffer(rival, premium, out name, out business);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Offer", e);
                    return false;
                }
            }

            // One of business pressure's issues at the player's business at a place, through its own rules.
            private static bool Start(string kind, string place)
            {
                try
                {
                    BuildingRegistration reg = BusinessGame.Find(place);
                    return reg != null && BusinessDirector.Instance != null && BusinessDirector.Instance.StartIssue(kind, reg);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Start", e);
                    return false;
                }
            }

            private static void Footfall(string place)
            {
                try
                {
                    BuildingRegistration reg = BusinessGame.Find(place);
                    if (reg != null)
                    {
                        BusinessDirector.Footfall(reg, true, false, out int _, out int _);
                    }
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Footfall", e);
                }
            }

            private static int Ambient(BuildingRegistration reg)
            {
                int before = 0;
                try
                {
                    before = _ambientBefore != null ? _ambientBefore(reg) : 0;
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Ambient (the pairings before)", e);
                }
                try
                {
                    return before + RivalDirector.FootfallOf(reg);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Ambient", e);
                    return before;
                }
            }

            // A kind of issue's weight in business pressure's deal: the earlier pairings' first, then a rival's lobbying.
            private static int Weight(string kind, int weight)
            {
                int w = weight;
                try
                {
                    w = _weightBefore != null ? _weightBefore(kind, weight) : weight;
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Weight (the pairings before)", e);
                }
                try
                {
                    switch (kind)
                    {
                        case BusinessCatalogue.Health:
                        case BusinessCatalogue.Fire:
                        case BusinessCatalogue.Licence:
                        case BusinessCatalogue.Audit:
                            return (int)((long)w * (1000 + RivalDirector.InspectionWeight()) / 1000);
                        default:
                            return w;
                    }
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Weight", e);
                    return w;
                }
            }

            private static void Extra(int day, List<NewsItem> into)
            {
                try
                {
                    _extraBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Extra (the pieces before)", e);
                }
                try
                {
                    RivalDirector.Instance?.Paper(day, into);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.Extra", e);
                }
            }

            private static bool ShowList(RichListPage page)
            {
                try
                {
                    LedgerDirector paper = LedgerDirector.Instance;
                    if (paper == null || page == null)
                    {
                        return false;
                    }
                    var special = new LedgerSpecial
                    {
                        Day = page.Day,
                        Header = page.Header,
                        EarLeftLabel = page.EarLeftLabel,
                        EarLeft = page.EarLeft,
                        EarRightLabel = page.EarRightLabel,
                        EarRight = page.EarRight,
                        DateMiddle = page.DateMiddle,
                        DateRight = page.DateRight,
                        Kicker = page.Kicker,
                        Headline = page.Headline,
                        Stand = page.Stand,
                        Byline = page.Byline,
                        Body = page.Body,
                        NoticeHead = page.NoticeHead,
                        Notice = page.Notice
                    };
                    for (int i = 0; i < page.Labels.Count && i < page.Texts.Count; i++)
                    {
                        special.Section(page.Labels[i]).AddRange(page.Texts[i]);
                    }
                    for (int i = 0; i < page.BoxLabels.Count && i < page.BoxLines.Count; i++)
                    {
                        special.Box(page.BoxLabels[i]).AddRange(page.BoxLines[i]);
                    }
                    return paper.ShowSpecial(special, null);
                }
                catch (Exception e)
                {
                    Fault("RivalLinks.ShowList", e);
                    return false;
                }
            }
        }
    }
}
