using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A firm's people by what they do, counted by the director from their files for the screens.</summary>
    public struct SectorTeam
    {
        public int People, Builders, Testers, Product, Designers, Web, Support, Sellers, Reliability, Security, Ml, Seniors;
    }

    /// <summary>One of a firm's desk sessions as its page shows it: the kind, whether it is due, who takes it, why it cannot be held now.</summary>
    public struct SectorDue
    {
        public int Kind;                 // SectorRules.Desk*
        public bool Due;
        public int Days;                 // days until it is due when it is not
        public bool Delegated;           // a manager takes it
        public string Blocked;           // the key of why it cannot be held now, or null
    }

    /// <summary>What the screens know of a firm from the game, filled by the director when a page is drawn.</summary>
    public sealed class SectorPlace
    {
        public string Place = string.Empty, Name = string.Empty;
        public int Kind;                 // FirmKind
        public int Reputation = 500;
        public SectorTeam Team;
        public Crew Crew;
        public int Products, ProductsAll;   // being built or on sale, here and across the firms
        public int Ready;                // products ready to launch here
        public string ReadyName;         // ...the first of them
        public int ReadyId;
        public long Customers;           // on its products, the free users in
        public long SupportNeeded;       // support people its products' tickets need
        public int Contracts, Offers;    // contracts under way and offers or tenders waiting at it
        public string OfferClient;       // the first offer's client
        public long OffersMonth;         // thousandths of an offer a month its share of the city's IT work brings
        public int Idle;                 // FirmsRules.Why now (Idle)
        public string IdleWords;         // ...in words
        public bool Desks = true;        // desk sessions run
        public string Manager;           // its manager's name, or null
        public readonly List<SectorDue> Due = new List<SectorDue>(4);
    }

    /// <summary>What a screen's button asks the director or the app to do (TechDirector.SectorDo).</summary>
    public static class SectorAct
    {
        public const int OpenRoles = 101, NewProduct = 102, Launch = 103, Desk = 104, Schedule = 105, Contracts = 106, Close = 107, Infra = 108;
    }

    /// <summary>
    /// The other tech firms' screens as data (the owner, 1 Nov 2026: "The most important thing in the overhaul is the UI and
    /// UX ... not cluttered"; players, Oct 2026: "my employees aren't doing anything ... not sure how to actually create
    /// products", "tell us exactly what employees are needed"): a firm's page says what to do next, which roles it needs and
    /// why, its markets, its desk sessions and every limit in words; a product's market and the data centres' markets are
    /// sections of their pages. The Tech app draws them today; the mod's design system (Scripts/Ui) will draw the same data.
    /// Game-free: EconomyCheck's SectorChecks build every screen offline.
    /// </summary>
    public static class SectorViews
    {
        private const string Mod = "reallife", P = Mod + "_tech_mkt_", D = Mod + "_tech_desk_";   // the keys' start, written so the source holds no key prefix as a literal

        public static string MarketKey(int market) => P + "market_" + ((SectorMarket)SectorRules.Clamp(market, 0, (int)SectorMarket.Count - 1)).ToString().ToLowerInvariant();

        public static string DeskKey(int kind) => D + SectorRules.DeskIds[SectorRules.Clamp(kind, 0, SectorRules.Desks - 1)].Substring(5) + "_title";

        public static string BuyersKey(int who) => P + "buyers_" + ((SectorBuyers)SectorRules.Clamp(who, 0, (int)SectorBuyers.Count - 1)).ToString().ToLowerInvariant();

        private static ViewLine Line(ViewSection s, string key, ViewTone tone, params string[] pairs)
        {
            var l = new ViewLine { Key = key, Pairs = pairs, Tone = tone };
            s?.Lines.Add(l);
            return l;
        }

        private static ViewLine Words(string key, ViewTone tone, params string[] pairs) => new ViewLine { Key = key, Pairs = pairs, Tone = tone };

        // ---------------------------------------------------------------- a firm's page

        /// <summary>
        /// A firm's page under Living population: what to do next with the one action that does it, its markets (who buys,
        /// its share, who leads), its team against what its work needs and why, its desk sessions, and its limits in words.
        /// </summary>
        public static StudioScreen Firm(SectorFile f, FirmsFile ff, SectorPlace s, int day, IStudioFormat fmt)
        {
            var v = new StudioScreen { Kind = 10, TitleKey = P + "firm_title", TitlePairs = new[] { "firm", s.Name }, ChipKey = P + "chip_market" };
            v.Next = Next(s, fmt, out ViewAction primary);
            v.Primary = primary;
            v.ChipTone = v.Next.Tone == ViewTone.Bad ? ViewTone.Bad : v.Next.Tone == ViewTone.Warn ? ViewTone.Warn : ViewTone.Plain;
            bool makes = FirmsCatalogue.MakesProducts[SectorRules.Clamp(s.Kind, 0, (int)FirmKind.Count - 1)] != 0;
            if (makes)
            {
                Markets(v.Section(P + "sec_markets"), f, s, fmt);
            }
            if (FirmsCatalogue.TakesWork[SectorRules.Clamp(s.Kind, 0, (int)FirmKind.Count - 1)] != 0)
            {
                ViewSection work = v.Section(P + "sec_work");
                Line(work, P + "work_share", ViewTone.Plain, "offers", Tenths(s.OffersMonth, fmt), "contracts", fmt.Whole(s.Contracts));
            }
            Team(v.Section(P + "sec_team"), s, fmt);
            if (s.Due.Count > 0)
            {
                Desk(v.Section(P + "sec_desk"), s, fmt);
            }
            ViewSection limits = v.Section(P + "sec_limits");
            if (makes)
            {
                Line(limits, P + "limit_products", s.Products >= SectorCatalogue.ProductsPerFirm ? ViewTone.Warn : ViewTone.Plain, "firm", fmt.Whole(s.Products),
                    "firm_most", fmt.Whole(SectorCatalogue.ProductsPerFirm), "all", fmt.Whole(s.ProductsAll), "all_most", fmt.Whole(SectorCatalogue.ProductsMost));
                Line(limits, P + "limit_market", ViewTone.Plain);
            }
            if (FirmsCatalogue.TakesWork[SectorRules.Clamp(s.Kind, 0, (int)FirmKind.Count - 1)] != 0)
            {
                Line(limits, P + "limit_contracts", ViewTone.Plain, "offers", fmt.Whole(FirmsCatalogue.OffersMost), "kept", fmt.Whole(SectorCatalogue.ContractsMost));
            }
            v.Others.Add(new ViewAction { Key = "common_close", Act = SectorAct.Close });
            return v;
        }

        /// <summary>What to do next with a firm, always said, and the action that does it: the first thing in the way, in the order a new firm meets them.</summary>
        public static ViewLine Next(SectorPlace s, IStudioFormat fmt, out ViewAction primary)
        {
            primary = null;
            int kind = SectorRules.Clamp(s.Kind, 0, (int)FirmKind.Count - 1);
            bool makes = FirmsCatalogue.MakesProducts[kind] != 0, takes = FirmsCatalogue.TakesWork[kind] != 0;
            if (s.Team.People == 0)
            {
                primary = new ViewAction { Key = P + "button_roles", Act = SectorAct.OpenRoles };
                return Words(P + "next_hire_first", ViewTone.Bad, "roles", fmt.Text(FirstRoles(kind)));
            }
            if (s.Team.Builders + s.Team.Designers + s.Team.Ml == 0)
            {
                primary = new ViewAction { Key = P + "button_roles", Act = SectorAct.OpenRoles };
                return Words(P + "next_hire_builder", ViewTone.Bad, "roles", fmt.Text(FirstRoles(kind)));
            }
            if (s.Idle != (int)Idle.None)
            {
                primary = new ViewAction { Key = P + "button_schedule", Act = SectorAct.Schedule };
                return Words(P + "next_idle", ViewTone.Bad, "why", s.IdleWords ?? string.Empty);
            }
            if (makes && s.Products == 0)
            {
                primary = new ViewAction { Key = P + "button_product", Act = SectorAct.NewProduct };
                return Words(P + "next_product", ViewTone.Warn);
            }
            if (s.Ready > 0)
            {
                primary = new ViewAction { Key = P + "button_launch", Act = SectorAct.Launch, Arg = s.ReadyId };
                return Words(P + "next_launch", ViewTone.Good, "product", s.ReadyName ?? string.Empty);
            }
            for (int i = 0; i < s.Due.Count; i++)
            {
                if (s.Due[i].Due && s.Due[i].Blocked == null && !s.Due[i].Delegated && s.Desks)
                {
                    primary = new ViewAction { Key = P + "button_desk", Act = SectorAct.Desk };
                    return Words(P + "next_desk", ViewTone.Plain, "session", fmt.Text(DeskKey(s.Due[i].Kind)), "firm", s.Name);
                }
            }
            if (takes && s.Offers > 0)
            {
                primary = new ViewAction { Key = P + "button_contracts", Act = SectorAct.Contracts };
                return Words(P + "next_offer", ViewTone.Plain, "client", s.OfferClient ?? string.Empty);
            }
            string gap = Gap(s, kind);
            if (gap != null)
            {
                primary = new ViewAction { Key = P + "button_roles", Act = SectorAct.OpenRoles };
                return Words(gap, ViewTone.Warn);
            }
            return makes ? Words(P + "next_selling", ViewTone.Plain) : Words(P + "next_working", ViewTone.Plain);
        }

        /// <summary>The roles a new firm of a kind hires first, as a key.</summary>
        public static string FirstRoles(int kind) => P + "roles_first_" + ((FirmKind)kind).ToString().ToLowerInvariant();

        // The most important role a firm lacks for its work, as the key of its line; null with nothing lacking.
        private static string Gap(SectorPlace s, int kind)
        {
            SectorTeam t = s.Team;
            if (s.SupportNeeded > t.Support && s.Customers > 0)
            {
                return P + "next_gap_support";
            }
            if ((kind == (int)FirmKind.Software || kind == (int)FirmKind.AiLab) && s.Customers > 0 && t.Reliability == 0)
            {
                return P + "next_gap_reliability";
            }
            if (s.Customers > 1000 && t.Security == 0)
            {
                return P + "next_gap_security";
            }
            if (t.Builders >= 4 && t.Testers == 0 && FirmsCatalogue.MakesProducts[kind] != 0)
            {
                return P + "next_gap_testers";
            }
            if (t.Sellers == 0 && (FirmsCatalogue.TakesWork[kind] != 0 || kind == (int)FirmKind.Software))
            {
                return P + "next_gap_sellers";
            }
            return null;
        }

        // The markets the firm sells in: their buyers, the player's share, who leads.
        private static void Markets(ViewSection sec, SectorFile f, SectorPlace s, IStudioFormat fmt)
        {
            int shown = 0;
            for (int m = 0; m < (int)SectorMarket.Count; m++)
            {
                if (!SectorRules.Sells(s.Kind, m))
                {
                    continue;
                }
                SectorMarketDay md = f.Markets[m];
                int mine = md.Buyers > 0 ? (int)Math.Min(1000L, md.Mine * 1000 / md.Buyers) : 0;
                RivalProduct lead = md.Leader >= 0 && md.Leader < f.Rivals.Count ? f.Rivals[md.Leader] : null;
                int theirs = lead != null && md.Buyers > 0 ? (int)Math.Min(1000L, lead.Customers * 1000 / md.Buyers) : 0;
                Line(sec, lead != null ? P + "market_line" : P + "market_line_alone", mine > theirs && mine > 0 ? ViewTone.Good : ViewTone.Plain,
                    "market", fmt.Text(MarketKey(m)), "buyers", fmt.Whole(md.Buyers), "who", fmt.Text(BuyersKey(md.Who)), "mine", fmt.Mille(mine),
                    "leader", lead != null ? lead.Name : string.Empty, "theirs", fmt.Mille(theirs));
                shown++;
            }
            if (shown == 0)
            {
                Line(sec, P + "market_none", ViewTone.Plain);
            }
            if (!f.City.Known)
            {
                Line(sec, P + "market_documented", ViewTone.Plain);
            }
        }

        // The team against what its work needs, a line a role the kind uses: who they are, how many, and why it matters.
        private static void Team(ViewSection sec, SectorPlace s, IStudioFormat fmt)
        {
            SectorTeam t = s.Team;
            int kind = SectorRules.Clamp(s.Kind, 0, (int)FirmKind.Count - 1);
            bool makes = FirmsCatalogue.MakesProducts[kind] != 0;
            int builders = kind == (int)FirmKind.Design ? t.Designers : kind == (int)FirmKind.AiLab ? t.Builders + t.Ml : t.Builders;
            Line(sec, P + "team_builders_" + ((FirmKind)kind).ToString().ToLowerInvariant(), builders == 0 ? ViewTone.Bad : ViewTone.Plain, "count", fmt.Whole(builders),
                "points", fmt.Whole(kind == (int)FirmKind.Design ? s.Crew.Design : s.Crew.Engineering));
            if (makes)
            {
                Line(sec, P + "team_testers", t.Testers == 0 && t.Builders >= 4 ? ViewTone.Warn : ViewTone.Plain, "count", fmt.Whole(t.Testers));
                int designLift = SectorRules.Lift(s.Crew.Design, s.Crew.Engineering + 1, SectorCatalogue.DesignGain);
                Line(sec, P + "team_designers", ViewTone.Plain, "count", fmt.Whole(t.Designers), "lift", fmt.Mille(designLift));
                Line(sec, P + "team_product", ViewTone.Plain, "count", fmt.Whole(t.Product), "boost", fmt.Mille(SectorRules.ProductBoost(s.Crew)));
                Line(sec, P + "team_support", s.SupportNeeded > t.Support ? ViewTone.Warn : ViewTone.Plain, "count", fmt.Whole(t.Support), "need", fmt.Whole(s.SupportNeeded));
                Line(sec, P + "team_reliability", t.Reliability == 0 && s.Customers > 0 ? ViewTone.Warn : ViewTone.Plain, "count", fmt.Whole(t.Reliability),
                    "posture", fmt.Mille(s.Crew.Reliability));
            }
            Line(sec, P + "team_security", t.Security == 0 && s.Customers > 1000 ? ViewTone.Warn : ViewTone.Plain, "count", fmt.Whole(t.Security), "posture", fmt.Mille(s.Crew.Security));
            Line(sec, P + "team_sellers", ViewTone.Plain, "count", fmt.Whole(t.Sellers), "lift", fmt.Mille(SectorRules.Lift(s.Crew.Selling, SectorCatalogue.SellingHalf, SectorCatalogue.SellingGain)));
        }

        // The firm's desk sessions: due or when, who takes them, or why not now.
        private static void Desk(ViewSection sec, SectorPlace s, IStudioFormat fmt)
        {
            for (int i = 0; i < s.Due.Count; i++)
            {
                SectorDue d = s.Due[i];
                string title = fmt.Text(DeskKey(d.Kind));
                if (d.Blocked != null)
                {
                    Line(sec, P + "desk_blocked", ViewTone.Plain, "session", title, "why", fmt.Text(d.Blocked));
                }
                else if (d.Due)
                {
                    Line(sec, d.Delegated ? P + "desk_due_manager" : P + "desk_due", d.Delegated ? ViewTone.Plain : ViewTone.Warn, "session", title, "manager", s.Manager ?? string.Empty);
                }
                else
                {
                    Line(sec, P + "desk_later", ViewTone.Plain, "session", title, "days", fmt.Whole(d.Days));
                }
            }
            if (!s.Desks)
            {
                Line(sec, P + "desk_off", ViewTone.Plain);
            }
        }

        // An amount in thousandths written with one decimal place at most (offers a month).
        private static string Tenths(long thousandths, IStudioFormat fmt)
        {
            long tenths = (Math.Max(0L, thousandths) + 50) / 100;
            return tenths % 10 == 0 ? fmt.Whole(tenths / 10) : fmt.Whole(tenths / 10) + "." + (tenths % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- a product's market

        /// <summary>
        /// A product's market as a section of its page: who buys and how many, the share it holds and the share its pull
        /// earns it (where its customers are heading), how many know of it, the market's going features and price, and the
        /// three rivals with the most customers. The share is the only limit there is, and the section says so.
        /// </summary>
        public static ViewSection Market(SectorFile f, Product p, int appeal, int awareness, IStudioFormat fmt)
        {
            int m = SectorRules.MarketOf(p);
            var sec = new ViewSection { TitleKey = P + "sec_market" };
            if (m < 0)
            {
                return sec;
            }
            SectorMarketDay md = f.Markets[m];
            long attraction = SectorRules.Attraction(appeal, awareness);
            long target = SectorRules.Target(md, attraction);
            long have = SectorRules.Have(p);
            int share = md.Buyers > 0 ? (int)Math.Min(1000L, have * 1000 / md.Buyers) : 0;
            int heading = ScaleRules.Share(attraction, md.All);
            Line(sec, P + "product_buyers", ViewTone.Plain, "market", fmt.Text(MarketKey(m)), "buyers", fmt.Whole(md.Buyers), "who", fmt.Text(BuyersKey(md.Who)));
            Line(sec, target > have ? P + "product_heading_up" : target < have ? P + "product_heading_down" : P + "product_heading_flat",
                target > have ? ViewTone.Good : target < have ? ViewTone.Warn : ViewTone.Plain, "share", fmt.Mille(share), "heading", fmt.Mille(heading), "target", fmt.Whole(target));
            Line(sec, P + "product_known", awareness < 200 ? ViewTone.Warn : ViewTone.Plain, "known", fmt.Mille(awareness), "appeal", fmt.Mille(appeal));
            Line(sec, p.Features < md.Average ? P + "product_going_behind" : P + "product_going", p.Features < md.Average ? ViewTone.Warn : ViewTone.Plain,
                "features", fmt.Whole(p.Features), "going", fmt.Whole(md.Average), "price", fmt.Mille(FirmsCatalogue.PriceTiers[SectorRules.Clamp(p.Tier, 0, 2)]), "going_price", fmt.Mille(md.Going));
            Rivals(sec, f, m, fmt);
            Line(sec, P + "product_no_cap", ViewTone.Plain);
            return sec;
        }

        // The rivals in a market with the most customers, three at most.
        private static void Rivals(ViewSection sec, SectorFile f, int market, IStudioFormat fmt)
        {
            SectorMarketDay md = f.Markets[market];
            int shown = 0;
            long below = long.MaxValue;
            while (shown < 3)
            {
                RivalProduct best = null;
                for (int i = 0; i < f.Rivals.Count; i++)
                {
                    RivalProduct r = f.Rivals[i];
                    if (r.Market == market && r.Customers < below && (best == null || r.Customers > best.Customers))
                    {
                        best = r;
                    }
                }
                if (best == null)
                {
                    break;
                }
                below = best.Customers;
                Line(sec, best.Startups ? P + "rival_startup" : P + "rival_company", ViewTone.Plain, "name", best.Name, "features", fmt.Whole(best.Features),
                    "share", fmt.Mille(md.Buyers > 0 ? (int)Math.Min(1000L, best.Customers * 1000 / md.Buyers) : 0), "price", fmt.Mille(FirmsCatalogue.PriceTiers[SectorRules.Clamp(best.Tier, 0, 2)]));
                shown++;
            }
            if (shown == 0)
            {
                Line(sec, P + "rival_none", ViewTone.Plain);
            }
        }

        // ---------------------------------------------------------------- the data centres' markets

        /// <summary>
        /// The data centres' markets as a section of the Infra tab: the racks the city's firms rent and the player's share of
        /// them, the compute and AI compute the city buys and the cloud's share, and the power price (a spike and how it was
        /// answered). In place of the old fixed 40 racks and the rival clouds' fixed half.
        /// </summary>
        public static ViewSection Infra(SectorFile f, InfraFile infra, int cycle, int share, int day, IStudioFormat fmt)
        {
            var sec = new ViewSection { TitleKey = P + "sec_infra" };
            int tier = 0, let = 0;
            for (int i = 0; i < infra.Centres.Count; i++)
            {
                tier = Math.Max(tier, infra.Centres[i].Tier);
                let += infra.Centres[i].Colo;
            }
            int racks = SectorRules.RacksWanted(f.City, cycle, share);
            Line(sec, P + "infra_racks", ViewTone.Plain, "racks", fmt.Whole(racks), "share", fmt.Mille(SectorRules.RackShare(tier, infra.Cloud.Reputation)), "let", fmt.Whole(let));
            int regions = InfraRules.Regions(infra.Centres);
            long cloud = SectorRules.CloudMarket(f, false, cycle, share, infra.Cloud.Hyper, regions), gpu = SectorRules.CloudMarket(f, true, cycle, share, infra.Cloud.Hyper, regions);
            Line(sec, infra.Cloud.Open ? P + "infra_cloud" : P + "infra_cloud_closed", ViewTone.Plain, "units", fmt.Whole(cloud), "gpu", fmt.Whole(gpu),
                "share", fmt.Mille(SectorRules.CloudShare(infra.Cloud)));
            if (SectorRules.Spiking(f, day))
            {
                Line(sec, f.Hedged ? P + "infra_spike_hedged" : day < f.ThrottleUntil ? P + "infra_spike_throttled" : P + "infra_spike", f.Hedged ? ViewTone.Plain : ViewTone.Warn,
                    "tariff", fmt.Mille(f.SpikeFactor), "day", fmt.Day(f.SpikeUntil));
            }
            else
            {
                Line(sec, P + "infra_power_usual", ViewTone.Plain);
            }
            return sec;
        }
    }
}
