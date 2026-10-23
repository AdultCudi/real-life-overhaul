using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The construction game (Construction/ConstructionBridges, filled in by Wiring.ConstructionLinks) with the stock
        /// market (Markets):
        ///
        ///   RivalNews                 a rival builder's tender won or lost, a site on time or late, the district's growth:
        ///                             the builder news list (Markets/BuilderMarket), which the market puts on its tape
        ///   HedgePlan, Hedge          a project's purchases to come as futures on the raw materials exchange, planned or
        ///                             bought (MarketDirector.HedgePlan, Hedge), each material by its exchange name
        ///   Contract                  a future of a hedge on the exchange's book
        ///   SettleTest, UndoTests     the test tools' hedge settled now, or undone with its money
        ///
        /// and the player's listed company with the construction company's own books (ConstructionBooks, from ListingLinks).
        /// Asked on a page, a press, the day's pass or a market day's open; never per frame. A fault is said once
        /// (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class MarketsBuildLinks
        {
            private const string ConstructionType = "reallife:businesstype_construction";

            private static readonly List<HedgeNeed> Needs = new List<HedgeNeed>(24);
            private static readonly List<HedgeLeg> Legs = new List<HedgeLeg>(8);

            private static MarketDirector Market => MarketDirector.Instance;

            internal static void RivalNews(int rival, int kind, int weight)
            {
                try
                {
                    BuilderMarket.Add(rival, (BuilderNewsKind)kind, weight);
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.RivalNews", e);
                }
            }

            // A project's purchases to come, each material by the exchange's name of what its price follows.
            private static void ToNeeds(List<MaterialNeed> needs)
            {
                Needs.Clear();
                for (int i = 0; needs != null && i < needs.Count; i++)
                {
                    int m = (int)needs[i].Material;
                    if (m >= 0 && m < BuildCatalogue.ExchangeItem.Length)
                    {
                        Needs.Add(new HedgeNeed { Material = BuildCatalogue.ExchangeItem[m], Day = needs[i].Day, Money = needs[i].Money });
                    }
                }
            }

            private static Material MaterialOf(string exchangeName)
            {
                int m = Array.IndexOf(BuildCatalogue.ExchangeItem, exchangeName);
                return m >= 0 ? (Material)m : Material.Cement;
            }

            private static string WhyKey(DerivWhy why, int legs)
            {
                if (why == DerivWhy.None)
                {
                    return null;
                }
                return legs == 0 && why == DerivWhy.NotOffered ? "reallife_bld_hedge_too_near" : DerivText.WhyKey(why);
            }

            internal static bool HedgePlan(List<MaterialNeed> needs, List<HedgeLine> lines, out HedgeQuote quote)
            {
                quote = new HedgeQuote { WhyKey = "reallife_bld_hedge_no_exchange" };
                lines.Clear();
                try
                {
                    MarketDirector market = Market;
                    if (market == null)
                    {
                        return false;
                    }
                    ToNeeds(needs);
                    bool planned = market.HedgePlan(Needs, Legs, out HedgeSums sums);
                    for (int i = 0; i < Legs.Count; i++)
                    {
                        lines.Add(new HedgeLine { Material = MaterialOf(Legs[i].Material), SettleDay = Legs[i].SettleDay, Units = Legs[i].Units, Money = Legs[i].Money });
                    }
                    quote = new HedgeQuote
                    {
                        Legs = sums.Legs, Money = sums.Money, Margin = sums.Margin, Fees = sums.Fees, TooNear = sums.TooNear, Unpriced = sums.Unpriced, Cut = sums.Cut,
                        WhyKey = planned ? WhyKey(sums.Why, sums.Legs) : sums.Why == DerivWhy.Off ? "reallife_bld_hedge_futures_off" : "reallife_bld_hedge_no_exchange"
                    };
                    return planned;
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.HedgePlan", e);
                    return false;
                }
            }

            internal static int Hedge(List<MaterialNeed> needs, bool test, List<int> opened, out string whyKey)
            {
                whyKey = "reallife_bld_hedge_no_exchange";
                try
                {
                    MarketDirector market = Market;
                    if (market == null)
                    {
                        return 0;
                    }
                    ToNeeds(needs);
                    int bought = market.Hedge(Needs, test, opened, out DerivWhy why);
                    whyKey = WhyKey(why, bought);
                    return bought;
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.Hedge", e);
                    return 0;
                }
            }

            // False only while the exchange is not running: a future it has not got is known as gone.
            internal static bool Contract(int id, out HedgeLook look)
            {
                look = default(HedgeLook);
                try
                {
                    MarketDirector market = Market;
                    if (market == null || market.State == null)
                    {
                        return false;
                    }
                    look.Known = market.ContractState(id, out look.Open, out look.Result);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.Contract", e);
                    return false;
                }
            }

            internal static int SettleTest(List<int> ids)
            {
                try
                {
                    return Market != null ? Market.HedgeSettleTest(ids) : 0;
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.SettleTest", e);
                    return 0;
                }
            }

            internal static int UndoTests()
            {
                try
                {
                    return Market != null ? Market.HedgeUndoTests(out _) : 0;
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.UndoTests", e);
                    return 0;
                }
            }

            /// <summary>The construction company's own takings over some days, for the listing's check; under nothing with no company of the player's.</summary>
            internal static long BuilderTakings(int from, int to)
            {
                try
                {
                    BuildState state = ConstructionDirector.Instance != null ? ConstructionDirector.Instance.State : null;
                    if (state == null || state.Firm.Founded <= 0)
                    {
                        return -1L;
                    }
                    state.BooksOver(from, to, out long earned, out _);
                    return earned;
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.BuilderTakings", e);
                    return -1L;
                }
            }

            /// <summary>
            /// The listed company's books with the construction company's own: its works' takings and costs are the player's
            /// money, not its office's statement (which has only its wages and rent), so a company holding a construction
            /// business is valued on what it builds. Only when the office is one of the player's businesses being read.
            /// </summary>
            internal static void ConstructionBooks(List<BuildingRegistration> businesses, int from, int to, ref double revenue, ref double profit)
            {
                try
                {
                    bool holds = false;
                    for (int i = 0; businesses != null && i < businesses.Count && !holds; i++)
                    {
                        holds = businesses[i] != null && businesses[i].businessTypeName == ConstructionType;
                    }
                    BuildState state = holds && ConstructionDirector.Instance != null ? ConstructionDirector.Instance.State : null;
                    if (state == null)
                    {
                        return;
                    }
                    state.BooksOver(from, to, out long earned, out long spent);
                    revenue += earned;
                    profit += earned - spent;
                }
                catch (Exception e)
                {
                    Fault("MarketsBuildLinks.ConstructionBooks", e);
                }
            }
        }
    }
}
