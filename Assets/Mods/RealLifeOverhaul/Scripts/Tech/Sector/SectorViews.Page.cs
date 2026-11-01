using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    public static partial class SectorViews
    {
        private static readonly TeamGroup[] FirmGroups =
        {
            TeamGroup.Engineers, TeamGroup.Data, TeamGroup.Designers, TeamGroup.Testers, TeamGroup.Product, TeamGroup.Infrastructure, TeamGroup.Security,
            TeamGroup.Support, TeamGroup.Sales, TeamGroup.Business
        };

        /// <summary>
        /// A firm's whole page as the mod's design system draws it: what to do next and its actions as its page has them,
        /// then by what the firm lives on. A software house and an AI lab live on their products: their products and
        /// customers with the last 30 days' takings, their products, and the rivals in their markets. A consultancy, a
        /// security firm, an agency and a design studio live on their work: their contracts and the offers waiting, the work
        /// under way, and their products or their finished work. Then the team by group with the groups its work needs and
        /// lacks, and its markets, support and limits as facts.
        /// </summary>
        public static FirmPage Page(SectorFile f, FirmsFile ff, SectorPlace s, FirmReading r, int day, IStudioFormat fmt)
        {
            var page = new FirmPage { Screen = Firm(f, ff, s, day, fmt), TeamKey = StudioViews.TeamKey, FactsKey = P + "page_facts" };
            int kind = SectorRules.Clamp(s.Kind, 0, (int)FirmKind.Count - 1);
            bool makes = FirmsCatalogue.MakesProducts[kind] != 0, takes = FirmsCatalogue.TakesWork[kind] != 0;
            bool products = makes && (!takes || kind == (int)FirmKind.Software || kind == (int)FirmKind.AiLab);
            if (products)
            {
                page.Figures.Add(new ViewFigure { LabelKey = P + "page_products", Value = fmt.Whole(s.Products) });
                page.Figures.Add(new ViewFigure { LabelKey = P + "page_customers", Value = fmt.Whole(s.Customers) });
            }
            else
            {
                page.Figures.Add(new ViewFigure { LabelKey = P + "page_contracts", Value = fmt.Whole(s.Contracts) });
                page.Figures.Add(new ViewFigure { LabelKey = P + "page_offers", Value = fmt.Whole(s.Offers) });
            }
            page.Figures.Add(StudioViews.TakingsFigure(r, P + "page_takings", fmt));
            if (products)
            {
                Products(page.Main, ff, s, day, fmt);
                RivalsTable(page.More, f, kind, fmt);
            }
            else
            {
                Work(page.Main, page.More, ff, s, fmt);
                if (makes)
                {
                    Products(page.More, ff, s, day, fmt);
                }
            }
            SectorTeam t = s.Team;
            int builders = kind == (int)FirmKind.Design ? t.Designers : kind == (int)FirmKind.AiLab ? t.Builders + t.Ml : t.Builders;
            for (int i = 0; i < FirmGroups.Length; i++)
            {
                TeamGroup g = FirmGroups[i];
                // The groups a firm's work needs, as its page's team lines weigh them: its builders, testers once there are four
                // builders, support while its customers write in, reliability and security as its customers grow, sellers to win work.
                bool needs = (g == TeamGroup.Engineers && kind != (int)FirmKind.Design) || (g == TeamGroup.Designers && kind == (int)FirmKind.Design)
                             || (g == TeamGroup.Data && kind == (int)FirmKind.AiLab) || (g == TeamGroup.Testers && makes && builders >= 4)
                             || (g == TeamGroup.Support && makes && s.SupportNeeded > 0)
                             || (g == TeamGroup.Infrastructure && s.Customers > 0 && (kind == (int)FirmKind.Software || kind == (int)FirmKind.AiLab))
                             || (g == TeamGroup.Security && s.Customers > 1000)
                             || (g == TeamGroup.Sales && (FirmsCatalogue.TakesWork[kind] != 0 || kind == (int)FirmKind.Software));
                StudioViews.AddRole(page, r, g, needs);
            }
            if (!products && takes)
            {
                StudioViews.Fact(page, P + "page_offers_month", Tenths(s.OffersMonth, fmt));
            }
            if (makes)
            {
                Shares(page, f, s, fmt);
                StudioViews.Fact(page, P + "page_support", Cell(fmt, P + "page_support_value", "have", fmt.Whole(t.Support), "need", fmt.Whole(s.SupportNeeded)));
                StudioViews.Fact(page, P + "page_limit", Cell(fmt, P + "page_limit_value", "count", fmt.Whole(s.Products), "most", fmt.Whole(SectorCatalogue.ProductsPerFirm)));
            }
            if (products && takes)
            {
                StudioViews.Fact(page, P + "page_offers_month", Tenths(s.OffersMonth, fmt));
            }
            int due = 0;
            for (int i = 0; i < s.Due.Count; i++)
            {
                due += s.Due[i].Due && s.Due[i].Blocked == null ? 1 : 0;
            }
            StudioViews.Fact(page, P + "page_desk", due > 0 ? Cell(fmt, P + "page_desk_value", "count", fmt.Whole(due)) : StudioViews.NoneWords(fmt));
            StudioViews.Fact(page, P + "page_reputation", fmt.Mille(s.Reputation));
            return page;
        }

        // The firm's products, those on sale first, the newest launch first.
        private static void Products(ViewTable table, FirmsFile ff, SectorPlace s, int day, IStudioFormat fmt)
        {
            StudioViews.Head(table, new[] { P + "page_col_product", P + "page_col_kind", P + "page_col_customers", P + "page_col_takings", P + "page_col_status" },
                new[] { false, false, true, true, true });
            table.EmptyKey = P + "page_no_products";
            var list = new List<Product>(8);
            for (int i = 0; ff != null && i < ff.Products.Count; i++)
            {
                Product p = ff.Products[i];
                if (p.Place == s.Place && p.Stage < 2)
                {
                    list.Add(p);
                }
            }
            list.Sort((a, b) => a.Stage != b.Stage ? b.Stage.CompareTo(a.Stage) : b.Launched.CompareTo(a.Launched));
            for (int i = 0; i < list.Count; i++)
            {
                Product p = list[i];
                table.Rows.Add(new[]
                {
                    p.Name, fmt.Text(Mod + "_tech_product_" + p.K.ToString().ToLowerInvariant()), p.Live ? fmt.Whole(SectorRules.Have(p)) : string.Empty,
                    p.Live ? fmt.Money(p.Revenue) : string.Empty, p.Live ? StudioViews.AgeDays(day - p.Launched, fmt) : fmt.Text(P + "page_building")
                });
            }
        }

        // The rivals in the markets the firm sells in, the most customers first: the list the firm's own share is won from.
        private static void RivalsTable(ViewTable table, SectorFile f, int kind, IStudioFormat fmt)
        {
            StudioViews.Head(table, new[] { P + "page_col_rival", P + "page_col_market", P + "page_col_customers", P + "page_col_features" },
                new[] { false, false, true, true });
            table.EmptyKey = P + "page_no_rivals";
            var list = new List<RivalProduct>(16);
            for (int i = 0; f != null && i < f.Rivals.Count; i++)
            {
                if (SectorRules.Sells(kind, f.Rivals[i].Market))
                {
                    list.Add(f.Rivals[i]);
                }
            }
            list.Sort((a, b) => b.Customers.CompareTo(a.Customers));
            for (int i = 0; i < list.Count; i++)
            {
                RivalProduct rp = list[i];
                table.Rows.Add(new[] { rp.Name, fmt.Text(MarketKey(rp.Market)), fmt.Whole(rp.Customers), fmt.Whole(rp.Features) });
            }
        }

        // A firm that lives on its work: its contracts under way and the offers waiting at it, and its work done or failed,
        // the newest first.
        private static void Work(ViewTable now, ViewTable done, FirmsFile ff, SectorPlace s, IStudioFormat fmt)
        {
            StudioViews.Head(now, new[] { P + "page_col_work", P + "page_col_client", P + "page_col_value", P + "page_col_due" }, new[] { false, false, true, true });
            now.EmptyKey = P + "page_no_work";
            StudioViews.Head(done, new[] { P + "page_col_done", P + "page_col_client", P + "page_col_paid", P + "page_col_stage" }, new[] { false, false, true, true });
            done.EmptyKey = P + "page_no_done";
            for (int i = ff != null ? ff.Contracts.Count - 1 : -1; i >= 0; i--)
            {
                Contract w = ff.Contracts[i];
                if (w.Place != s.Place)
                {
                    continue;
                }
                string work = fmt.Text(Mod + "_tech_work_" + w.K.ToString().ToLowerInvariant());
                if (w.Stage == (int)WorkStage.Active || w.Stage == (int)WorkStage.Offered)
                {
                    string due = w.Stage == (int)WorkStage.Offered ? fmt.Text(Mod + "_tech_stage_offered") : fmt.Day(w.Due);
                    now.Rows.Add(new[] { work, w.Client, fmt.Money(w.Value), due });
                }
                else if (w.Stage == (int)WorkStage.Done || w.Stage == (int)WorkStage.Failed)
                {
                    done.Rows.Add(new[] { work, w.Client, fmt.Money(w.Paid), fmt.Text(Mod + "_tech_stage_" + w.At.ToString().ToLowerInvariant()) });
                }
            }
        }

        // The player's share of each market the firm sells in, and who leads it.
        private static void Shares(FirmPage page, SectorFile f, SectorPlace s, IStudioFormat fmt)
        {
            for (int m = 0; f != null && m < (int)SectorMarket.Count; m++)
            {
                if (!SectorRules.Sells(s.Kind, m))
                {
                    continue;
                }
                SectorMarketDay md = f.Markets[m];
                int mine = md.Buyers > 0 ? (int)Math.Min(1000L, md.Mine * 1000 / md.Buyers) : 0;
                RivalProduct lead = md.Leader >= 0 && md.Leader < f.Rivals.Count ? f.Rivals[md.Leader] : null;
                int theirs = lead != null && md.Buyers > 0 ? (int)Math.Min(1000L, lead.Customers * 1000 / md.Buyers) : 0;
                // The leader's name stands in the rivals' table; the fact keeps to the two shares, so it fits beside any market's name.
                StudioViews.Fact(page, MarketKey(m), lead != null ? Cell(fmt, P + "page_share_value", "mine", fmt.Mille(mine), "theirs", fmt.Mille(theirs)) : fmt.Mille(mine));
            }
        }

        private static string Cell(IStudioFormat fmt, string key, params string[] pairs) => StudioViews.Cell(fmt, key, pairs);
    }
}
