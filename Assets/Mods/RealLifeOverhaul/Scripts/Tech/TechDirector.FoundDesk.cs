using System;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The five desk sessions of going to market (Wiring/Wiring.FoundLinks): customer interviews, the first version, early users, the launch channel and the price. Each is held at
    /// the owner's own desk, only while the firm is at its stage, and takes the game's own time at the desk (so its energy); the player's skills decide what it brings and
    /// grow through the study's own route.
    /// </summary>
    public sealed partial class TechDirector
    {
        private const string GtmHead = "reallife", Gtm = GtmHead + "_tech_desk_gtm_";   // the keys' start, written so the source holds no key prefix as a literal

        /// <summary>Why a go to market session cannot be held at a firm now (a key), or null when it is open.</summary>
        internal string GtmBlocked(int stage, string place)
        {
            FoundedFirm f = FoundingOf(place);
            FirmPlace firm = FirmOf(place);
            if (f == null || firm == null || !_ventureOn)
            {
                return Gtm + "why_none";
            }
            if (f.Stage != stage)
            {
                return Gtm + "why_stage";
            }
            bool building;
            Product live = BestLive(place, -1, out building);
            if (stage == FoundCatalogue.StageBuild && live == null && !building)
            {
                return Gtm + "why_product";
            }
            if ((stage == FoundCatalogue.StageUsers || stage == FoundCatalogue.StagePricing) && live == null)
            {
                return Gtm + "why_live";
            }
            return null;
        }

        private void Practised(int skill)
        {
            FoundBridges.LearnNow(skill, FoundCatalogue.SessionLearn * Math.Max(1, 100 - TechBridges.SkillNow(skill)) / 100);
        }

        /// <summary>
        /// A session answered at the desk: its choice applied to the firm. False, with the reason, when nothing could be done. A placed firm's player who holds one takes up its niche,
        /// fit and stages from then.
        /// </summary>
        internal bool GtmSession(int stage, string place, string name, int choice, out string key, out string[] pairs)
        {
            pairs = new[] { "firm", name ?? string.Empty };
            string why = GtmBlocked(stage, place);
            FoundedFirm f = FoundingOf(place);
            FirmPlace firm = FirmOf(place);
            if (why != null || f == null || firm == null || Save == null)
            {
                key = why ?? Gtm + "why_none";
                return false;
            }
            int day = Save.Day;
            f.Engaged |= f.Placed;
            int selling = TechBridges.SkillNow(TalentCatalogue.Selling), relationships = TechBridges.SkillNow(TalentCatalogue.Relationships);
            int analysis = TechBridges.SkillNow(TalentCatalogue.Analysis), sense = TechBridges.SkillNow(TalentCatalogue.ProductSense);
            Product live = BestLive(place, -1, out _);
            switch (stage)
            {
                case FoundCatalogue.StageDiscovery:
                {
                    int how = Math.Max(0, Math.Min(2, choice));
                    int gain = FoundRules.Interview(how, selling, relationships, analysis, sense);
                    bool moved = FoundRules.Interviewed(f, gain);
                    Practised(how == 0 ? TalentCatalogue.Selling : how == 1 ? TalentCatalogue.Relationships : TalentCatalogue.Analysis);
                    Practised(TalentCatalogue.ProductSense);
                    key = moved ? Gtm + "discovery_next" : Gtm + "discovery_done";
                    int left = Math.Max(0, FoundCatalogue.DiscoveryRounds - f.Rounds);
                    pairs = new[] { "firm", firm.File.Name, "learned", TechText.Mille(f.Learned), "left", left.ToString(CultureInfo.InvariantCulture) };
                    break;
                }
                case FoundCatalogue.StageBuild:
                {
                    int minutes = choice == 0 ? FoundCatalogue.BuildMinutes : choice == 1 ? FoundCatalogue.BuildShort : 0;
                    if (minutes > 0)
                    {
                        OwnDeskRules.Credit(firm.File, day, minutes);   // tomorrow's crew counts them as the owner's own work, as a stint at a desk does
                        Practised(TalentCatalogue.Coding);
                        Practised(TalentCatalogue.SystemDesign);
                    }
                    key = minutes > 0 ? Gtm + "build_done" : Gtm + "build_team";
                    pairs = new[] { "firm", firm.File.Name, "hours", (minutes / 60).ToString(CultureInfo.InvariantCulture) };
                    break;
                }
                case FoundCatalogue.StageUsers:
                {
                    if (choice == 0)
                    {
                        int cleared = FoundRules.Fixed(live.Bugs, sense, analysis);
                        live.Bugs = Math.Max(0, live.Bugs - cleared);
                        Practised(TalentCatalogue.ProductSense);
                        key = Gtm + "users_fix";
                        pairs = new[] { "firm", firm.File.Name, "fixed", cleared.ToString(CultureInfo.InvariantCulture) };
                    }
                    else if (choice == 1)
                    {
                        f.Wom = Math.Max(f.Wom, FoundRules.Word(relationships));
                        f.WomDay = day;
                        Practised(TalentCatalogue.Relationships);
                        key = Gtm + "users_word";
                    }
                    else
                    {
                        live.Progress += FirmsRules.FeatureCost(live.Features) * (300 + 3 * sense) / 1000;
                        Practised(TalentCatalogue.ProductSense);
                        key = Gtm + "users_ship";
                    }
                    break;
                }
                case FoundCatalogue.StageLaunch:
                {
                    int channel = Math.Max(1, Math.Min(3, choice + 1));
                    int partner = -1;
                    if (channel == FoundCatalogue.ChannelPartner)
                    {
                        partner = Reseller(live != null ? SectorRules.MarketOf(live) : (int)SectorMarket.BusinessSoftware, day);
                        if (partner < 0)
                        {
                            key = Gtm + "launch_no_partner";
                            return false;
                        }
                    }
                    FoundRules.Launched(f, channel, partner);
                    Practised(channel == FoundCatalogue.ChannelSales ? TalentCatalogue.Selling : channel == FoundCatalogue.ChannelProduct ? TalentCatalogue.Marketing : TalentCatalogue.Relationships);
                    key = Gtm + "launch_done";
                    pairs = new[] { "firm", firm.File.Name, "channel", TechText.T(FoundCatalogue.ChannelKey(channel)), "partner", partner >= 0 ? HiringCatalogue.RivalFirms[partner] : "-" };
                    break;
                }
                default:
                {
                    int tier = Math.Max(0, Math.Min(2, choice));
                    int going = f.Niche >= 0 ? NicheNow(f.Niche).Going : 1000;
                    int bears = (int)((long)going * FoundRules.Bear(f) / 1000);
                    for (int i = 0; i < _firmsFile.Products.Count; i++)
                    {
                        Product p = _firmsFile.Products[i];
                        if (p.Place == place && p.Live && p.K != ProductKind.Game && (f.Niche < 0 || NicheServes(f, p)))
                        {
                            p.Tier = tier;
                        }
                    }
                    FoundRules.Priced(f, tier);
                    Practised(TalentCatalogue.Analysis);
                    key = Gtm + "price_done";
                    pairs = new[]
                    {
                        "firm", firm.File.Name, "price", TechText.Mille(FirmsCatalogue.PriceTiers[tier]), "bears", TechText.Mille(bears), "tier", TechText.Tier(tier)
                    };
                    break;
                }
            }
            ChangedTech();
            return true;
        }

        // A listed company that sells in the market and would resell the product, seeded among those that do; -1 when none does.
        private int Reseller(int market, int day)
        {
            int count = 0;
            for (int i = 0; i < SectorCatalogue.CompanyMarkets.Length; i++)
            {
                count += (SectorCatalogue.CompanyMarkets[i] & (1 << market)) != 0 ? 1 : 0;
            }
            if (count == 0)
            {
                return -1;
            }
            int pick = TechRandom.Range(_venture.Seed, day, 91, market, 0, count - 1);
            for (int i = 0; i < SectorCatalogue.CompanyMarkets.Length; i++)
            {
                if ((SectorCatalogue.CompanyMarkets[i] & (1 << market)) != 0 && pick-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
