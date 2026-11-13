using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The Brands facts, read from the brands' state, its extras and the game.</summary>
    public sealed partial class BrandsLive
    {
        private BraFacts Facts(BrandsDirector d, VentView v)
        {
            BrandsState state = d.State;
            BrandsExtra extra = d.Extra;
            int day = SaveGameManager.Current.Day;
            int index = BrandsDirector.Index;
            var outlets = new List<RivalOutletFact>(16);
            d.RivalOutlets(outlets);
            var f = new BraFacts { Day = day, WaitingAnswers = d.Waiting() };
            var brands = new BraBrandFact[state.Brands.Count];
            for (int i = 0; i < brands.Length; i++)
            {
                brands[i] = MakeBrand(d, state, extra, state.Brands[i], outlets, index);
            }
            f.Brands = brands;
            IReadOnlyList<BrandShop> shops = d.Shops;
            var list = new List<BraShopFact>(shops.Count + state.Franchises.Count);
            for (int i = 0; i < shops.Count; i++)
            {
                list.Add(MakeShop(d, state, shops[i]));
            }
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                Franchise fr = state.Franchises[i];
                Brand b = state.Brand(fr.BrandId);
                list.Add(new BraShopFact
                {
                    Key = BraPages.FranchiseKey + fr.Id, Place = fr.Place, Name = BrandsText.Shop(b, fr), Brand = b != null ? b.Name : "?",
                        District = BrandsText.District(fr.District), Owner = fr.Name,
                    Kind = "franchise", FranchiseId = fr.Id, SalesDay = fr.Takings, Standards = fr.Seen, Rating = -1,
                    Status = fr.Ask != Ask.None ? BrandsText.Ask(fr.Ask) : fr.Trouble ? BrandsText.T("reallife_brand_status_trouble") : BrandsText.T("reallife_brand_row_in_order")
                });
            }
            f.Shops = list.ToArray();
            var apps = new BraApplicantFact[state.Applicants.Count];
            for (int i = 0; i < apps.Length; i++)
            {
                apps[i] = MakeApplicant(d, state, state.Applicants[i], v, day, index);
            }
            f.Applicants = apps;
            var frs = new BraFranchiseFact[state.Franchises.Count];
            for (int i = 0; i < frs.Length; i++)
            {
                frs[i] = MakeFranchise(d, state, extra, state.Franchises[i], day, index);
            }
            f.Franchises = frs;
            f.ChainNames = new string[BrandCatalogue.Chains];
            f.ChainFees = new long[BrandCatalogue.Chains];
            for (int i = 0; i < BrandCatalogue.Chains; i++)
            {
                f.ChainNames[i] = BrandsText.Chain(i);
                f.ChainFees[i] = FranchiseRules.ChainFee(i, index);
            }
            // The brands a plain shop of the picked kind could join: those of its type.
            string pickedPlace = v.Pick != null && v.Pick.StartsWith(BraPages.ShopKey, StringComparison.Ordinal) ? v.Pick.Substring(BraPages.ShopKey.Length) : null;
            BrandShop plain = pickedPlace != null ? d.ShopAt(pickedPlace) : null;
            var names = new List<string>(4);
            var ids = new List<int>(4);
            for (int i = 0; plain != null && i < state.Brands.Count; i++)
            {
                if (string.Equals(state.Brands[i].Type, plain.Type, StringComparison.Ordinal))
                {
                    names.Add(state.Brands[i].Name);
                    ids.Add(state.Brands[i].Id);
                }
            }
            f.JoinBrands = names.ToArray();
            f.JoinBrandIds = ids.ToArray();
            return f;
        }

        private BraBrandFact MakeBrand(BrandsDirector d, BrandsState state, BrandsExtra extra, Brand b, List<RivalOutletFact> outlets, int index)
        {
            int best = BrandRules.Best(b);
            int count;
            int rival = d.RivalOf(b.Type, outlets, out count);
            int shopsAll = BrandRules.Shops(state, b, null);
            long fees = 0, franchiseSales = 0, sales = 0, royalty = 0;
            int satisfaction = 0, rated = 0;
            string worst = null;
            int low = 101;
            for (int i = 0; i < state.Members.Count; i++)
            {
                Member m = state.Members[i];
                if (m.BrandId != b.Id)
                {
                    continue;
                }
                sales += m.Takings;
                satisfaction += m.Satisfaction;
                rated++;
                if (m.Standards < low)
                {
                    low = m.Standards;
                    worst = d.ShopName(m.Place);
                }
            }
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                Franchise fr = state.Franchises[i];
                if (fr.BrandId == b.Id)
                {
                    franchiseSales += fr.Takings;
                    royalty += fr.Royalty;
                }
            }
            sales += franchiseSales;
            fees = royalty * 30;
            BrandRing ring = extra != null ? extra.RingOf(b.Id, false) : null;
            int weeks = ring != null ? ring.Count : 0;
            var mine = new long[weeks];
            var rv = new long[weeks];
            var names = new string[weeks];
            for (int i = 0; i < weeks; i++)
            {
                mine[i] = ring.Mine[i];
                rv[i] = ring.Rival[i];
                names[i] = _words.F("reallife_ui_bra_week_short", "W{n}", "n", (ring.LastWeek - (weeks - 1 - i)).ToString(Inv));
            }
            var districts = new List<BraDistrictFact>(b.Districts.Count);
            var pushDistricts = new List<string>(4);
            for (int i = 0; i < b.Districts.Count; i++)
            {
                int shops = BrandRules.Shops(state, b, b.Districts[i].Name);
                districts.Add(new BraDistrictFact { Name = BrandsText.District(b.Districts[i].Name), Rec = b.Districts[i].Rec, Shops = shops });
                if (shops > 0)
                {
                    pushDistricts.Add(BrandsText.District(b.Districts[i].Name));
                }
            }
            int good;
            bool can = BrandRules.CanOffer(state, b, out good);
            string pushLine = null;
            if (b.Pushing)
            {
                string where = b.Push == BrandCatalogue.PushLocal ? BrandsText.District(b.PushDistrict) : BrandsText.T("reallife_brand_push_everywhere");
                pushLine = _words.F("reallife_ui_bra_push_line", "{campaign}, {where}, {days} days left", "campaign", BrandsText.Push(b.Push), "where", where,
                    "days", Math.Max(0, b.PushEnd - Math.Max(b.Age + 1, b.PushFrom)).ToString(Inv));
            }
            Franchise bidder = b.Bid > 0 ? state.Franchise(b.BidBy) : null;
            long monthly = fees;
            string flagship = d.ShopName(b.Flagship);
            return new BraBrandFact
            {
                Id = b.Id, Name = b.Name, Type = BrandsText.Type(b.Type), Founded = FinanceText.Day(b.Founded), Standards = b.Standards, Minimum = b.Minimum,
                MinimumStep = Math.Max(0, Array.IndexOf(BrandCatalogue.MinimumSteps, b.Minimum)), MinimumSteps = BrandCatalogue.MinimumSteps.Length,
                Shops = shopsAll, Members = BrandRules.Members(state, b, null), Franchised = BrandRules.Franchised(state, b, null), Rec = best, City = b.City,
                Value = BrandExtraRules.Value(shopsAll, best, (int)Math.Min(int.MaxValue, monthly), index), RivalName = rival >= 0 ? RivalNameOf(rival) : null,
                RivalValue = rival >= 0 ? BrandExtraRules.RivalValue(count, BrandExtraRules.RivalRec(state.Seed, rival, count, 0), index) : 0,
                Rating = rated > 0 ? Math.Min(10, satisfaction / rated / 10) : -1, SalesYesterday = sales, FranchiseSales = franchiseSales, Royalty = royalty, Net = b.Net, Earned = b.Earned,
                Fund = b.Fund, FeesMonth = monthly, Offered = b.Offered, CanOffer = can, Keep = b.Keep, Local = b.Local, Pushing = b.Pushing, PushLine = pushLine, FlagshipName = flagship,
                OfferLine = BrandsText.Fill(b.Offered ? "reallife_brand_set_offered" : can ? "reallife_brand_set_can_offer" : "reallife_brand_set_cannot_offer", "good", good.ToString(Inv),
                    "shops", BrandCatalogue.OfferShops.ToString(Inv), "standards", BrandCatalogue.OfferStandards.ToString(Inv), "city", BrandsText.Points(BrandCatalogue.OfferCityRec),
                    "district", BrandsText.Points(BrandCatalogue.OfferDistrictRec), "mean", BrandsText.Points(BrandCatalogue.OfferMeanRec), "has", BrandsText.Points(b.City),
                    "best", BrandsText.Points(best), "average", BrandsText.Rec(BrandRules.SpreadRec(state, b))),
                BidFranchise = bidder != null ? bidder.Id : 0, BidAmount = b.Bid, WeekNames = names, Mine = mine, Rival = rv, Districts = districts.ToArray(),
                PushKinds = new[] { BrandsText.Push(BrandCatalogue.PushLocal), BrandsText.Push(BrandCatalogue.PushCity), BrandsText.Push(BrandCatalogue.PushCreators) },
                PushDistricts = pushDistricts.ToArray(),
                PushCosts = new long[]
                {
                    pushDistricts.Count > 0 ? BrandRules.PushCost(BrandCatalogue.PushLocal, Math.Max(1, BrandRules.Shops(state, b, FirstDistrict(state, b))), index) : 0,
                    BrandRules.PushCost(BrandCatalogue.PushCity, 0, index), BrandRules.PushCost(BrandCatalogue.PushCreators, 0, index,
                        BrandsDirector.CreatorsQuote(BrandCatalogue.PushCreators))
                },
                PushDays = new[] { BrandRules.PushDays(BrandCatalogue.PushLocal), BrandRules.PushDays(BrandCatalogue.PushCity), BrandRules.PushDays(BrandCatalogue.PushCreators) },
                BestShop = worst
            };
        }

        private static string FirstDistrict(BrandsState state, Brand b)
        {
            for (int i = 0; i < b.Districts.Count; i++)
            {
                if (BrandRules.Shops(state, b, b.Districts[i].Name) > 0)
                {
                    return b.Districts[i].Name;
                }
            }
            return null;
        }

        // A rival brand is named after one of the game's named rivals, in the order the rivals are listed in: the same names the investors' rivals have.
        private static string RivalNameOf(int tycoon)
        {
            var names = new List<string>(4);
            RealLifeOverhaul.Economy.Ventures.VenturesGame.RivalNames(names);
            return names.Count > 0 ? names[Math.Max(0, tycoon) % names.Count] : null;
        }

        private BraShopFact MakeShop(BrandsDirector d, BrandsState state, BrandShop shop)
        {
            Brand b = shop.File != null ? state.Brand(shop.File.BrandId) : null;
            var fact = new BraShopFact
            {
                Key = BraPages.ShopKey + shop.Place, Place = shop.Place, Name = shop.Name, District = BrandsText.District(shop.District),
                    Owner = BrandsText.T("reallife_brand_run_you"), Mine = true,
                SalesDay = shop.Takings, Rating = shop.File != null ? Math.Min(10, shop.File.Satisfaction / 10) : -1, Kind = "plain",
                Brand = b != null ? b.Name : shop.Chain != null ? BrandsText.Chain(shop.Chain.Chain) : string.Empty, Status = BrandsText.T("reallife_brand_status_plain")
            };
            if (b != null)
            {
                fact.Kind = "member";
                fact.Standards = shop.File.Standards;
                fact.Status = BrandsText.T("reallife_brand_status_member");
                if (d.Kept(shop))
                {
                    // the shop's prices follow the flagship's: by how much they are kept at the local band, and whether a manager of the game's would undo that
                    fact.KeptNote = b.Local
                        ? BrandsText.Fill("reallife_brand_member_kept_local", "business", d.ShopName(b.Flagship), "share", BrandsText.Share(d.KeptFactor(shop.Place)))
                        : BrandsText.T("reallife_brand_member_kept", "business", d.ShopName(b.Flagship));
                    fact.ManagerNote = d.ManagerWatches(shop) ? BrandsText.T("reallife_brand_kept_manager", "district", BrandsText.District(shop.District)) : null;
                }
            }
            else if (shop.Chain != null)
            {
                Taken t = shop.Chain;
                fact.Kind = "chain";
                fact.Standards = t.Standards;
                fact.Breach = t.Breach;
                fact.ChainExit = d.ChainExit(t);
                fact.Kept = t.Keep;
                fact.CanKeep = true;
                if (t.Keep)
                {
                    fact.KeptNote = BrandsText.Fill("reallife_brand_chain_kept", "district", BrandsText.District(shop.District), "least", BrandsText.Share(KeptPrices.Least(t.Chain)),
                        "most", BrandsText.Share(KeptPrices.Most(t.Chain)));
                    fact.ManagerNote = d.ManagerWatches(shop) ? BrandsText.T("reallife_brand_kept_manager", "district", BrandsText.District(shop.District)) : null;
                }
                fact.Status = t.Breach > 0 ? BrandsText.T("reallife_brand_row_breach_days", "days", t.Breach.ToString(Inv)) : BrandsText.T("reallife_brand_status_chain");
            }
            else
            {
                fact.Line = BrandsText.T("reallife_brand_plain", "type", BrandsText.Type(shop.Type), "district", BrandsText.District(shop.District));
            }
            return fact;
        }

        private BraApplicantFact MakeApplicant(BrandsDirector d, BrandsState state, Applicant a, VentView v, int day, int index)
        {
            Brand b = state.Brand(a.BrandId);
            IReadOnlyList<BrandSite> sites = d.SitesFor(a);
            var siteNames = new string[sites.Count];
            for (int i = 0; i < siteNames.Length; i++)
            {
                siteNames[i] = BrandsText.T("reallife_brand_deal_site", "address", sites[i].Label, "traffic", sites[i].Traffic.ToString(Inv));
            }
            var lengths = new string[BrandCatalogue.Tiers];
            var fees = new string[BrandCatalogue.Tiers];
            var royalties = new string[BrandCatalogue.Tiers];
            var levies = new string[BrandCatalogue.Tiers];
            for (int i = 0; i < BrandCatalogue.Tiers; i++)
            {
                lengths[i] = BrandsText.T("reallife_brand_deal_length_value", "years", BrandCatalogue.LengthYears[i].ToString(Inv), "days",
                    (BrandCatalogue.LengthYears[i] * BrandsGame.DaysPerYear).ToString(Inv));
                fees[i] = BrandsText.Money(FranchiseRules.EntryFee(a, i, index));
                royalties[i] = BrandsText.Bp(BrandCatalogue.RoyaltyBp[i]);
                levies[i] = BrandsText.Bp(BrandCatalogue.LevyBp[i]);
            }
            int packed = v.C;
            int fee = packed & 3, royalty = (packed >> 2) & 3, levy = (packed >> 4) & 3;
            bool exclusive = ((packed >> 6) & 1) != 0;
            Refusal answer = b != null ? FranchiseRules.Answer(state, b, a, fee, royalty, levy, exclusive, index) : Refusal.None;
            int bears = b != null ? FranchiseRules.Bears(a.Temper, BrandRules.Effective(b, a.District)) : 0;
            return new BraApplicantFact
            {
                Id = a.Id, Name = a.Name, Brand = b != null ? b.Name : "?", District = BrandsText.District(a.District), Temper = BrandsText.Temper(a.Temper),
                    TemperAbout = BrandsText.TemperAbout(a.Temper),
                Wants = BrandsText.Fill(a.From != 0 ? "reallife_brand_app_second" : "reallife_brand_app_wants", "brand", b != null ? b.Name : "?", "district",
                    BrandsText.District(a.District), "day", a.Until.ToString(Inv)),
                Capital = a.Capital, Experience = a.Experience, ShopsRun = a.ShopsRun, ShopsClosed = a.ShopsClosed, DaysLeft = a.Until - day, Second = a.From != 0,
                    Sites = siteNames, Lengths = lengths,
                Fees = fees, Royalties = royalties, Levies = levies, Accepts = answer == Refusal.None, Bears = bears,
                Answer = BrandsText.Answer(answer) + " " + BrandsText.Fill("reallife_brand_deal_burden", "burden", FranchiseRules.Burden(fee, royalty, levy,
                    exclusive).ToString(Inv), "bears", bears.ToString(Inv))
            };
        }

        private BraFranchiseFact MakeFranchise(BrandsDirector d, BrandsState state, BrandsExtra extra, Franchise f, int day, int index)
        {
            Brand b = state.Brand(f.BrandId);
            RoyaltyRing ring = extra != null ? extra.RoyaltyOf(f.Id, false) : null;
            Rebate rebate = extra != null ? extra.Latest(f.Id) : null;
            long ordinary = b != null ? FranchiseRules.Ordinary(f.Base, b.Ticket) : 0;
            long target = ordinary * 30;
            long sales = (long)f.Takings * 30;
            bool cause = b != null && FranchiseRules.HasCause(b, f, day);
            int inside = BrandExtraRules.OwnInside(state, f);
            var fact = new BraFranchiseFact
            {
                Id = f.Id, Name = b != null ? BrandsText.Shop(b, f) : f.Name, Franchisee = f.Name, Brand = b != null ? b.Name : "?", District = BrandsText.District(f.District),
                Place = f.Place, Until = BrandsText.Day(f.Until), Takings = f.Takings, Royalty = f.Royalty, Paid = f.Paid, EntryFee = f.EntryFee, RoyaltyBp = f.RoyaltyBp, LevyBp = f.LevyBp,
                Seen = f.Seen, Minimum = b != null ? b.Minimum : 0, Warnings = f.Warnings, Exclusive = f.Exclusive, Trouble = f.Trouble, Ask = (int)f.Ask,
                Status = f.Ask != Ask.None ? BrandsText.Ask(f.Ask) : f.Trouble ? BrandsText.T("reallife_brand_status_trouble") : BrandsText.T("reallife_brand_row_in_order"),
                Terms = BrandsText.Money(f.EntryFee) + ", " + BrandsText.Bp(f.RoyaltyBp) + ", " + BrandsText.Bp(f.LevyBp), Target = target,
                Overs = target > 0 ? (int)((sales - target) * 100 / target) : 0, OnTime = ring != null ? ring.OnTime : 0, Months = ring != null ? ring.Count : 0, OwnInside = inside,
                ExclusiveLine = f.Exclusive ? _words.F("reallife_ui_bra_exclusive_line", "{metres} m round the shop", "metres",
                    BraPages.Radius.ToString(Inv)) : _words.T("reallife_ui_bra_shared_line", "Shared"),
                StandardsLine = f.Seen < 0 ? BrandsText.T("reallife_brand_fr_never_seen") : _words.F("reallife_ui_bra_standards_line",
                    "Last inspected at {points} points, against a minimum of {minimum}.", "points", f.Seen.ToString(Inv),
                    "minimum", (b != null ? b.Minimum : 0).ToString(Inv)),
                CanAgree = f.Ask != Ask.None && !(f.Ask == Ask.Dispute && f.Suit != 0), CanDecline = f.Ask != Ask.None && !(f.Ask == Ask.Dispute && f.Suit != 0),
                CanWaive = f.Ask == Ask.Help, Cause = cause, EndCost = d.EndCost(f), Court = f.Suit != 0
            };
            if (f.Ask != Ask.None)
            {
                string[] pairs =
                {
                    "amount", BrandsText.Money(f.AskAmount), "day", f.AskUntil.ToString(Inv), "days", BrandCatalogue.WaiveDays.ToString(Inv)
                };
                fact.AskLine = f.Ask == Ask.Dispute && rebate != null
                    ? _words.F("reallife_ui_bra_rebate_line", "A rival opened a {type} in {district}, inside {name}'s exclusive area. They claim {amount} back by day {day}.",
                        "type", BrandsText.Type(rebate.Type),
                        "district", BrandsText.District(rebate.District), "name", f.Name, "amount", BrandsText.Money(f.AskAmount), "day", f.AskUntil.ToString(Inv))
                    : BrandsText.Fill(AskAbout((int)f.Ask), pairs);
            }
            if (f.Suit != 0)
            {
                fact.CourtLine = BrandsText.T("reallife_brand_fr_court", "number", f.Suit.ToString(Inv));
            }
            return fact;
        }

        private static string AskAbout(int ask)
        {
            string[] keys = { null, "reallife_brand_ask_about_help", "reallife_brand_ask_about_second", "reallife_brand_ask_about_renewal", "reallife_brand_ask_about_dispute",
                "reallife_brand_ask_about_bid" };
            return keys[Math.Max(1, Math.Min(5, ask))];
        }
    }
}
