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
        /// The rival tycoons (Scripts/Rivals/RivalBridges) with the rest of the mod. The rivals call no other feature and none
        /// calls them: every move goes through the feature that owns what it touches, here, one line each.
        ///
        /// What the rivals read:
        ///   Worth         the player's net worth as the Finance app shows it (Economy/Finance, Books.NetWorth)
        ///   Cycle         the city's cycle (Economy/City, CityEconomy.State): their fortunes grow with it
        ///   Playthrough   the stock market's playthrough id, for a new file's seed
        ///
        /// What the rivals do, each through its owner:
        ///   ListHolding   a tycoon's holding comes to the stock market when they come forward (MarketDirector.ListHolding)
        ///   Stake, Bid    a block of the player's listed company with a demand on its board, a bid for it: the market's own
        ///                 investor and bid, decided by its own rules (MarketDirector.RivalStake, RivalBid)
        ///   Offer         an offer to the player's most skilled employee, answered on the Company app's Staff tab
        ///                 (CompanyDirector.RivalOffer)
        ///   Smear, Inspect business pressure's own smear and an inspection or a licence check, started through its own rules
        ///                 (BusinessDirector.StartIssue)
        ///   Footfall      a rival's business near one of the player's: its part is added to <see cref="BusinessGame.Ambient"/>
        ///                 and written by business pressure's one writer (BusinessDirector.Footfall), inside its bounds
        ///   WeightOf      a rival's lobbying: inspections, licences and audits more often (BusinessDirector.WeightOf)
        ///   Premium       a rival buying up a district: its buildings for sale cost more (EstateBridges.Premium)
        ///   Sue           a civil claim in the Legal file (Justice/RivalClaims)
        ///   Customers     a piece in the paper: the customers' reputation (FameDirector.Moved, FameCause.Press)
        ///   the paper     the rivals' pieces in the morning's paper (News.Extra), and the rich list as a page of the City
        ///                 Ledger (LedgerDirector.ShowSpecial)
        ///   the page      the rivals' page is a page of the Finance app (EconomyBridges.Rivals, Finance.OpenRivals)
        ///
        /// What the player does back goes through the same owners: a takeover of a holding is the market's
        /// (MarketDirector.TakeOverHolding). COST: nothing per frame; each answer is read when its feature asks.
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
                RivalBridges.CompanyDays = () => MarketDirector.Instance != null ? MarketDirector.Instance.CompanyDays() : -1;
                RivalBridges.YearDays = () => MarketDirector.Instance != null ? MarketDirector.Instance.YearDays : 365;
                RivalBridges.ListHolding = (id, worth) => MarketDirector.Instance != null && MarketDirector.Instance.ListHolding(id, worth);
                RivalBridges.DelistHolding = id => MarketDirector.Instance != null && MarketDirector.Instance.DelistHolding(id);
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
                RivalBridges.Revalue = () => EstateDirector.Instance?.Revalue();
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
                BusinessGame.Ambient = _ambientBefore;   // (the brands', the company's and the city's own pairings take theirs off in a moment)
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

            private static ulong Playthrough()
            {
                MarketState state = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
                return state != null ? state.Id : 0UL;
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
                return (_ambientBefore != null ? _ambientBefore(reg) : 0) + RivalDirector.FootfallOf(reg);
            }

            // Business pressure's share of the deal for a kind of issue: the others' first, then a rival's lobbying on the inspections.
            private static int Weight(string kind, int weight)
            {
                int w = _weightBefore != null ? _weightBefore(kind, weight) : weight;
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

            private static void Extra(int day, List<NewsItem> into)
            {
                _extraBefore?.Invoke(day, into);
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
