using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The Economy app's screens of what the mod's own businesses take from the city's people (the brief's fourth step: each business type's demand and where
    /// it comes from), as view models the design system draws (D:/BigAmbitionsMod/not-in-repo/design/DESIGN_SPEC.md; the owner, 1 Nov 2026: "absolutely
    /// beautiful ... not cluttered"): the overview (the business types whose demand moved most since its first day) and one business type (each of its demands
    /// now, and each source's part of it). Only what the page shows; built when a screen opens, never per frame. Game-free (EconomyCheck builds every one).
    /// </summary>
    public static class PopDemandViews
    {
        /// <summary>The business types as the screens group their demands, each with its title's key; the first demand of each leads it.</summary>
        public static readonly (string Key, PopDemand[] Demands)[] Groups =
        {
            ("reallife_pop_dem_group_bank_savers", new[] { PopDemand.SaversPeople, PopDemand.SaversFirms }),
            ("reallife_pop_dem_group_bank_borrowers", new[] { PopDemand.BorrowFair, PopDemand.BorrowPoor, PopDemand.BorrowGood, PopDemand.BorrowShops, PopDemand.BorrowFirms }),
            ("reallife_pop_dem_group_hotels", new[] { PopDemand.HotelGuests }),
            ("reallife_pop_dem_group_car_hire", new[] { PopDemand.CarHire }),
            ("reallife_pop_dem_group_landlord", new[] { PopDemand.Tenants, PopDemand.TenantBudget }),
            ("reallife_pop_dem_group_agency", new[] { PopDemand.Instructions }),
            ("reallife_pop_dem_group_construction", new[] { PopDemand.FlatBuyers, PopDemand.ClientTenders, PopDemand.PublicTenders }),
            ("reallife_pop_dem_group_showroom", new[] { PopDemand.CarBuyers }),
            ("reallife_pop_dem_group_online", new[] { PopDemand.Online }),
            ("reallife_pop_dem_group_marketplace", new[] { PopDemand.Bargains, PopDemand.Listings }),
            ("reallife_pop_dem_group_academy", new[] { PopDemand.Students }),
            ("reallife_pop_dem_group_council", new[] { PopDemand.CouncilRevenue, PopDemand.Satisfaction }),
            ("reallife_pop_dem_group_jobs", new[] { PopDemand.Openings, PopDemand.TechSeekers }),
        };

        /// <summary>Each demand's name's key, in the order of PopDemand.</summary>
        public static readonly string[] DemandKeys =
        {
            "reallife_pop_dem_savers_people", "reallife_pop_dem_savers_firms", "reallife_pop_dem_borrow_poor", "reallife_pop_dem_borrow_fair", "reallife_pop_dem_borrow_good",
            "reallife_pop_dem_borrow_shops", "reallife_pop_dem_borrow_firms", "reallife_pop_dem_hotel_guests", "reallife_pop_dem_car_hire", "reallife_pop_dem_tenants",
            "reallife_pop_dem_tenant_budget", "reallife_pop_dem_instructions", "reallife_pop_dem_flat_buyers", "reallife_pop_dem_client_tenders", "reallife_pop_dem_public_tenders",
            "reallife_pop_dem_car_buyers", "reallife_pop_dem_online", "reallife_pop_dem_listings", "reallife_pop_dem_bargains", "reallife_pop_dem_students",
            "reallife_pop_dem_council_revenue", "reallife_pop_dem_satisfaction", "reallife_pop_dem_tech_seekers", "reallife_pop_dem_openings"
        };

        /// <summary>Each source's name's key, in the order of PopSource.</summary>
        public static readonly string[] SourceKeys =
        {
            "reallife_pop_dem_src_savings", "reallife_pop_dem_src_borrow_poor", "reallife_pop_dem_src_borrow_fair", "reallife_pop_dem_src_borrow_good", "reallife_pop_dem_src_turnover",
            "reallife_pop_dem_src_wages", "reallife_pop_dem_src_visitors", "reallife_pop_dem_src_leisure", "reallife_pop_dem_src_carless", "reallife_pop_dem_src_housing",
            "reallife_pop_dem_src_budget", "reallife_pop_dem_src_sellers", "reallife_pop_dem_src_starters", "reallife_pop_dem_src_wealth", "reallife_pop_dem_src_taxes",
            "reallife_pop_dem_src_pull", "reallife_pop_dem_src_car_want", "reallife_pop_dem_src_online", "reallife_pop_dem_src_listings", "reallife_pop_dem_src_bargains",
            "reallife_pop_dem_src_learners", "reallife_pop_dem_src_content", "reallife_pop_dem_src_pro_seekers", "reallife_pop_dem_src_pro_openings", "reallife_pop_dem_src_adults",
            "reallife_pop_dem_src_households"
        };

        /// <summary>
        /// The overview: the city's households, their savings and the council's taxes today at a glance; the business types whose demand moved most since its first
        /// day, each leading to its own screen; and the way to who contributes. Before the anchor the list is empty and says why.
        /// </summary>
        public static PopulationView Demands(PopDemandState m)
        {
            var v = new PopulationView { Kind = ViewKind.Demands, Index = 0, Title = "reallife_pop_dem_view", TitleKey = true, Empty = "reallife_pop_dem_empty" };
            if (m == null)
            {
                return v;
            }
            Hero(v, m, PopSource.Households, "reallife_pop_fig_households");
            Hero(v, m, PopSource.Savings, "reallife_pop_dem_fig_savings");
            Hero(v, m, PopSource.Taxes, "reallife_pop_dem_fig_taxes");
            v.RowsHeader = "reallife_pop_dem_rows_moved";
            if (m.AnchorDay >= 0)
            {
                var order = new List<int>(Groups.Length);
                for (int g = 0; g < Groups.Length; g++)
                {
                    order.Add(g);
                }
                order.Sort((a, b) =>
                {
                    int ma = Math.Abs(Lead(m, a) - PopDemandCatalogue.Neutral), mb = Math.Abs(Lead(m, b) - PopDemandCatalogue.Neutral);
                    return ma != mb ? mb.CompareTo(ma) : a.CompareTo(b);
                });
                for (int i = 0; i < order.Count && v.Rows.Count < PopulationViews.RowsMost; i++)
                {
                    int g = order[i];
                    PopDemand lead = Groups[g].Demands[0];
                    v.Rows.Add(new ViewRow
                    {
                        Title = Groups[g].Key, TitleKey = true, Subtitle = DemandKeys[(int)lead], Value = Lead(m, g), Format = ViewFormat.Permille,
                        Target = ViewTarget.To(ViewKind.Demand, g)
                    });
                }
            }
            Detail(v, m, PopSource.Visitors);
            Detail(v, m, PopSource.Housing);
            Detail(v, m, PopSource.Adults);
            v.Primary = "reallife_pop_act_contributors";
            v.PrimaryTarget = ViewTarget.To(ViewKind.Contributors, 0);
            return v;
        }

        /// <summary>One business type: each of its demands now against its first day, each source's part of them (the parts of a demand sum to it), and each source's figure today.</summary>
        public static PopulationView Demand(PopDemandState m, int group)
        {
            var v = new PopulationView { Kind = ViewKind.Demand, Index = group, Empty = "reallife_pop_dem_empty" };
            if (m == null || group < 0 || group >= Groups.Length)
            {
                return v;
            }
            v.Title = Groups[group].Key;
            v.TitleKey = true;
            PopDemand[] demands = Groups[group].Demands;
            for (int i = 0; i < demands.Length; i++)
            {
                v.Hero.Add(ViewFigure.Of(DemandKeys[(int)demands[i]], m.Factors[(int)demands[i]], ViewFormat.Permille));
            }
            v.RowsHeader = "reallife_pop_dem_rows_from";
            var shown = new List<PopSource>(8);
            for (int i = 0; i < demands.Length; i++)
            {
                (PopSource Source, int Weight)[] recipe = PopDemandCatalogue.Recipe(demands[i]);
                for (int r = 0; r < recipe.Length; r++)
                {
                    if (m.AnchorDay >= 0 && v.Rows.Count < PopulationViews.RowsMost)
                    {
                        v.Rows.Add(new ViewRow
                        {
                            Title = SourceKeys[(int)recipe[r].Source], TitleKey = true, Subtitle = DemandKeys[(int)demands[i]], Value = PopDemandRules.Part(m, demands[i], r),
                            Format = ViewFormat.Permille, Target = ViewTarget.None
                        });
                    }
                    if (!shown.Contains(recipe[r].Source))
                    {
                        shown.Add(recipe[r].Source);
                    }
                }
            }
            for (int i = 0; i < shown.Count; i++)
            {
                Detail(v, m, shown[i]);
            }
            GrowthState growth = CityPopulation.Live != null ? CityPopulation.Live.Growth : null;
            if (growth != null && demands[0] == PopDemand.FlatBuyers)
            {
                // the builders' work the city's growth asks for: the homes it wants a year and those started and finished this year
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_homes_wanted", growth.Wanted, ViewFormat.Count));
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_homes_started", growth.Started, ViewFormat.Count));
                v.Details.Add(ViewFigure.Of("reallife_pop_fig_homes_built", growth.Built, ViewFormat.Count));
            }
            v.Primary = "reallife_pop_dem_act_all";
            v.PrimaryTarget = ViewTarget.To(ViewKind.Demands, 0);
            return v;
        }

        /// <summary>A source's figure today as a screen writes it: money in whole units, people and households counted, shares in thousandths.</summary>
        public static long SourceValue(PopDemandState m, PopSource src, out ViewFormat format)
        {
            long v = m != null ? m.Now[(int)src] : 0;
            switch (src)
            {
                case PopSource.Savings:
                case PopSource.Wealth:
                    format = ViewFormat.Money;
                    return v;
                case PopSource.BorrowPoor:
                case PopSource.BorrowFair:
                case PopSource.BorrowGood:
                case PopSource.Turnover:
                case PopSource.Wages:
                case PopSource.Leisure:
                case PopSource.Taxes:
                case PopSource.Online:
                case PopSource.Bargains:
                    format = ViewFormat.Money;
                    return v / 100;
                case PopSource.Budget:
                case PopSource.Content:
                    format = ViewFormat.Permille;
                    return v;
                case PopSource.Visitors:
                case PopSource.Sellers:
                case PopSource.CarWant:
                    format = ViewFormat.Count;
                    return v / 1000;
                default:
                    format = ViewFormat.Count;
                    return v;
            }
        }

        // The factor of a group's first demand.
        private static int Lead(PopDemandState m, int group)
        {
            return m.Factors[(int)Groups[group].Demands[0]];
        }

        private static void Hero(PopulationView v, PopDemandState m, PopSource src, string label)
        {
            long value = SourceValue(m, src, out ViewFormat format);
            v.Hero.Add(ViewFigure.Of(label, value, format));
        }

        private static void Detail(PopulationView v, PopDemandState m, PopSource src)
        {
            long value = SourceValue(m, src, out ViewFormat format);
            v.Details.Add(ViewFigure.Of(SourceKeys[(int)src], value, format));
        }
    }
}
