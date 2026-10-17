using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>The director's part for the day's rules, the scandals the city hears of, and the Planner's rows.</summary>
    public sealed partial class BrandsDirector
    {
        // One day for every brand, every franchised shop, the applicants and the shops under a city chain. The shops were read just before.
        private void RulesDay(int day)
        {
            int index = Index, yearDays = BrandsGame.DaysPerYear;
            CycleState cycle = CityEconomy.State;
            _districts.Clear();
            for (int i = 0; i < _shops.Count; i++)
            {
                BrandShop shop = _shops[i];
                if (shop.File != null && BrandsBridges.StrikeStarts != null && BrandsBridges.StrikeStarts(shop.Place, day))
                {
                    Heard(shop.Place, Scandal.Strike);
                }
            }
            for (int k = 0; k < _state.Brands.Count; k++)
            {
                Brand b = _state.Brands[k];
                int royalty = 0, levy = 0, support = 0, shops = 0;
                for (int i = _state.Franchises.Count - 1; i >= 0; i--)
                {
                    Franchise f = _state.Franchises[i];
                    if (f.BrandId != b.Id)
                    {
                        continue;
                    }
                    if (!BrandsGame.Stands(f, b.Type))
                    {
                        Gone(b, f, day);
                        continue;
                    }
                    BrandsGame.Keep(f, b.Type, day);
                    if (f.Ask == Ask.Dispute && f.Suit != 0 && (BrandsBridges.ClaimOpen == null || !BrandsBridges.ClaimOpen(f.Suit)))
                    {
                        Logger?.Info("Brands: the court has dealt with claim " + f.Suit + " of " + f.Name + "; the dispute is over.");
                        FranchiseRules.Stand(b, f, day);
                    }
                    FranchiseDay r = FranchiseRules.Day(_state, b, f, day, Mathf.RoundToInt(CityEconomy.DistrictIncome(f.District)), cycle, index);
                    royalty += r.Royalty;
                    levy += r.Levy;
                    support += r.Support;
                    shops++;
                    After(b, f, r.Event, day);
                }
                if (shops > 0)
                {
                    Pay(royalty, BrandsGame.RoyaltyKey, b.Name, "the royalty of " + shops + " franchised shop(s) for day " + (day - 1));
                    Pay(levy, BrandsGame.LevyKey, b.Name, "their marketing levy; the brand's fund is now " + (b.Fund + levy));
                    Cost(support, BrandsGame.SupportKey, b.Name, "supporting them: " + BrandCatalogue.SupportBase + " each at prices of " + index + " and "
                                                                 + BrandsText.Bp(BrandCatalogue.SupportBp) + " of their takings", true);
                    b.Fund += levy;
                    b.Earned += royalty;
                }
                FranchiseRules.NoteNet(b, royalty - support);
                BrandRules.Day(_state, b);
                int good;
                if (b.Offered && !BrandRules.CanOffer(_state, b, out good))
                {
                    b.Offered = false;
                    Tell(NotificationType.Info, "reallife_brand_note_offer_off", new Dictionary<string, string> { { "brand", b.Name } }, TabBrands, "brand:" + b.Id);
                }
                if (FranchiseRules.Arrives(_state.Seed, b, day) && _state.Applicants.Count < BrandCatalogue.ApplicantsMost)
                {
                    if (_districts.Count == 0)
                    {
                        BrandsGame.Districts(_districts);
                    }
                    Applicant a = FranchiseRules.Generate(_state.Seed, _state.NextId++, b.Id, day, _districts);
                    _state.Applicants.Add(a);
                    Tell(NotificationType.Info, "reallife_brand_note_applicant", new Dictionary<string, string>
                    {
                        { "name", a.Name }, { "brand", b.Name }, { "district", BrandsText.District(a.District) }, { "days", BrandCatalogue.ApplicantDays.ToString() }
                    }, TabApplicants, "app:" + a.Id);
                    if (Verbose)
                    {
                        Logger?.Info("Brands check (applicant " + a.Id + " for " + b.Name + "): " + a.Name + ", " + a.Temper + ", capital " + a.Capital + ", " + a.Experience
                                     + " year(s), ability " + a.Ability + ", wants " + a.District + ", waits until day " + a.Until + "; made from the save's seed and the number alone: PASS");
                    }
                }
            }
            FranchiseRules.Lapse(_state, day);
            for (int i = 0; i < _shops.Count; i++)
            {
                BrandShop shop = _shops[i];
                if (shop.Chain != null)
                {
                    ChainDay(shop, day, index, yearDays);
                }
            }
        }

        // What a franchised shop's day ended in: it closed, its agreement ran out, or the franchisee asks for something.
        private void After(Brand b, Franchise f, DayEvent what, int day)
        {
            string pick = "fr:" + f.Id;
            switch (what)
            {
                case DayEvent.Failed:
                {
                    BrandsGame.Close(f, b.Type);
                    int lost = FranchiseRules.Fail(_state, b, f, day);
                    Logger?.Info("Brands: " + f.Name + " (" + b.Name + ", " + f.District + ") has closed with " + f.Health + " in hand; recognition there fell by " + lost + " hundredths.");
                    Tell(NotificationType.Error, "reallife_brand_note_failed", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "brand", b.Name }, { "district", BrandsText.District(f.District) }, { "points", BrandsText.Lost(lost) }
                    }, TabBrands, "brand:" + b.Id);
                    break;
                }
                case DayEvent.Lapsed:
                    BrandsGame.Close(f, b.Type);
                    FranchiseRules.Lapse(_state, b, f, day);
                    Logger?.Info("Brands: the agreement with " + f.Name + " (" + b.Name + ") ran out on day " + day + " and was not renewed.");
                    Tell(NotificationType.Info, "reallife_brand_note_lapsed", new Dictionary<string, string> { { "name", f.Name }, { "brand", b.Name } }, TabAgreements, null);
                    break;
                case DayEvent.AsksHelp:
                    Tell(NotificationType.Warning, "reallife_brand_note_help", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "amount", BrandsText.Money(f.AskAmount) }, { "day", f.AskUntil.ToString() }
                    }, TabAgreements, pick);
                    break;
                case DayEvent.AsksSecond:
                    Tell(NotificationType.Info, "reallife_brand_note_second", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "district", BrandsText.District(f.District) }, { "day", f.AskUntil.ToString() }
                    }, TabAgreements, pick);
                    break;
                case DayEvent.AsksRenewal:
                    Tell(NotificationType.Info, "reallife_brand_note_renewal", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "day", f.Until.ToString() }, { "amount", BrandsText.Money(f.AskAmount) }
                    }, TabAgreements, pick);
                    break;
                case DayEvent.Disputes:
                    f.Suit = BrandsBridges.FileClaim != null ? Math.Max(0, BrandsBridges.FileClaim(f.AskAmount)) : 0;
                    Logger?.Info("Brands: " + f.Name + " disputes the terms and claims " + f.AskAmount
                                 + (f.Suit != 0 ? "; filed with the court as claim " + f.Suit : "; no court to file with, so it waits for an answer until day " + f.AskUntil) + ".");
                    Tell(NotificationType.Warning, f.Suit != 0 ? "reallife_brand_note_dispute_court" : "reallife_brand_note_dispute", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "amount", BrandsText.Money(f.AskAmount) }, { "day", f.AskUntil.ToString() }
                    }, TabAgreements, pick);
                    break;
                case DayEvent.Bids:
                    Tell(NotificationType.Info, "reallife_brand_note_bid", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "brand", b.Name }, { "amount", BrandsText.Money(b.Bid) }, { "day", b.BidUntil.ToString() }
                    }, TabBrands, "brand:" + b.Id);
                    break;
            }
        }

        // A franchised shop that was a business of the game's is no longer that business (the city's market closed it, or
        // the building changed hands): the agreement ends with nobody at fault.
        private void Gone(Brand b, Franchise f, int day)
        {
            FranchiseRules.Lapse(_state, b, f, day);
            Logger?.Info("Brands: '" + f.GameName + "' at " + f.Place + ", the shop of " + f.Name + " (" + b.Name
                         + "), is no longer the business it was; the agreement has ended with nobody at fault.");
            Tell(NotificationType.Warning, "reallife_brand_note_gone", new Dictionary<string, string> { { "name", f.Name }, { "brand", b.Name } }, TabAgreements, null);
        }

        // One day of a shop of the player's under a city chain: the royalty on yesterday's takings, and what the chain does about a breach.
        private void ChainDay(BrandShop shop, int day, int index, int yearDays)
        {
            Taken t = shop.Chain;
            string chain = BrandsText.Chain(t.Chain);
            int royalty = FranchiseRules.ChainRoyalty(t, shop.Takings);
            Cost(royalty, BrandsGame.ChainRoyaltyKey, shop.Name, BrandsText.Bp(BrandCatalogue.ChainRoyaltyBp[t.Chain]) + " of the " + shop.Takings
                                                                 + " the game's order history shows for day " + (day - 1), true);
            int step = FranchiseRules.ChainStep(t, FranchiseRules.ChainBreach(t.Chain, t.Standards, t.Prices), day, yearDays);
            var data = new Dictionary<string, string> { { "chain", chain }, { "business", shop.Name }, { "days", t.Breach.ToString() } };
            if (step == 1)
            {
                Tell(NotificationType.Warning, "reallife_brand_note_chain_warned", data, TabShops, "shop:" + shop.Place);
            }
            else if (step == 2)
            {
                int fine = FranchiseRules.ChainFine(t, Index);
                Charge(fine, BrandsGame.ChainFineKey, shop.Name, "seven days outside what " + chain + " asks");
                data["amount"] = BrandsText.Money(fine);
                Tell(NotificationType.Error, "reallife_brand_note_chain_fined", data, TabShops, "shop:" + shop.Place);
            }
            else if (step == 3)
            {
                int exit = FranchiseRules.ChainExit(t, index);
                Charge(exit, BrandsGame.ChainExitKey, shop.Name, "the chain ended the agreement after fourteen days in breach");
                _state.Taken.Remove(t);
                shop.Chain = null;
                data["amount"] = BrandsText.Money(exit);
                Tell(NotificationType.Error, "reallife_brand_note_chain_ended", data, TabShops, "shop:" + shop.Place);
            }
        }

        /// <summary>
        /// Something the city hears about has happened at a business (Wiring/BrandLinks). If it trades under a brand of
        /// the player's, the brand's recognition falls in the district and a little city-wide.
        /// </summary>
        public void Heard(string place, Scandal kind)
        {
            if (!_running || _broken || _state == null)
            {
                return;
            }
            try
            {
                Member m = _state.Member(place);
                Brand b = m != null ? _state.Brand(m.BrandId) : null;
                if (b == null)
                {
                    return;
                }
                int before = BrandRules.Effective(b, m.District);
                int scale = BrandsBridges.ScandalScale != null ? BrandsBridges.ScandalScale(place) : 1000;
                int lost = BrandRules.Hit(b, m.District, kind, scale);
                BrandShop shop = ShopAt(place);
                if (scale != 1000)
                {
                    Logger?.Info("Brands: the scandal at " + (shop != null ? shop.Name : place) + " spread at " + scale + " thousandths of its loss (Flare).");
                }
                Logger?.Info("Brands check (scandal, " + kind + " at " + (shop != null ? shop.Name : place) + "): " + b.Name + "'s recognition in " + m.District + " " + before + " to "
                             + BrandRules.Effective(b, m.District) + " hundredths (" + lost + " lost of the " + BrandCatalogue.ScandalLoss[(int)kind] + " such a thing costs), city-wide now "
                             + b.City + ": PASS");
                Tell(NotificationType.Warning, "reallife_brand_note_scandal", new Dictionary<string, string>
                {
                    { "what", BrandsText.Scandal(kind) }, { "business", shop != null ? shop.Name : "?" }, { "brand", b.Name }, { "points", BrandsText.Lost(lost) },
                    { "district", BrandsText.District(m.District) }
                }, TabBrands, "brand:" + b.Id);
                Changed(true);
                BrandsBridges.ScandalHeard?.Invoke(place, shop != null ? shop.Name : b.Name, lost, scale);
            }
            catch (Exception e)
            {
                Fault("a scandal", e);
            }
        }

        /// <summary>Recognition a scandal took at a shop is given back (an honest answer on Flare, Wiring/FlareLinks). Returns what came back, hundredths.</summary>
        public int Recover(string place, int points)
        {
            if (!_running || _broken || _state == null)
            {
                return 0;
            }
            try
            {
                Member m = _state.Member(place);
                Brand b = m != null ? _state.Brand(m.BrandId) : null;
                int back = b != null ? BrandRules.Regain(b, m.District, points) : 0;
                if (back > 0)
                {
                    Logger?.Info("Brands: " + b.Name + " won back " + back + " hundredths of recognition in " + m.District + " after an honest answer to the scandal.");
                    Changed(true);
                }
                return back;
            }
            catch (Exception e)
            {
                Fault("a scandal answered", e);
                return 0;
            }
        }

        private void BuildAlerts(int day)
        {
            _alerts.Clear();
            for (int i = 0; i < _state.Applicants.Count; i++)
            {
                Applicant a = _state.Applicants[i];
                Brand b = _state.Brand(a.BrandId);
                Alert(1, a.Id, Math.Max(day, a.Until), BrandsText.T("reallife_brand_plan_applicant_title", "name", a.Name),
                    BrandsText.T("reallife_brand_plan_applicant_what", "brand", b != null ? b.Name : "?", "district", BrandsText.District(a.District)), null,
                    BrandsText.T("reallife_brand_plan_applicant_missed"));
            }
            for (int i = 0; i < _state.Franchises.Count; i++)
            {
                Franchise f = _state.Franchises[i];
                Brand b = _state.Brand(f.BrandId);
                string shop = BrandsText.Shop(b, f);
                if (f.Ask == Ask.Renewal)
                {
                    Alert(2, f.Id, Math.Max(day, f.Until), BrandsText.T("reallife_brand_plan_renewal_title", "name", shop), BrandsText.T("reallife_brand_plan_renewal_what"),
                        PlannerMoney("reallife_planner_pays", f.AskAmount), BrandsText.T("reallife_brand_plan_renewal_missed"));
                }
                else if (f.Ask != Ask.None)
                {
                    Alert(3, f.Id, Math.Max(day, f.AskUntil), BrandsText.T("reallife_brand_plan_ask_title", "name", shop), BrandsText.Ask(f.Ask), null,
                        f.Suit != 0 ? BrandsText.T("reallife_brand_plan_ask_court") : BrandsText.T("reallife_brand_plan_ask_missed"));
                }
                if (FranchiseRules.InspectionDue(f, day))
                {
                    Alert(4, f.Id, day, BrandsText.T("reallife_brand_plan_inspect_title", "name", shop),
                        BrandsText.T("reallife_brand_plan_inspect_what", "days", (day - f.SeenDay).ToString()), null, null);
                }
            }
            for (int i = 0; i < _state.Taken.Count; i++)
            {
                Taken t = _state.Taken[i];
                if (t.Breach > 0)
                {
                    BrandShop shop = ShopAt(t.Place);
                    Alert(5, CompanyDirector.PlaceNumber(t.Place), day, BrandsText.T("reallife_brand_plan_chain_title", "chain", BrandsText.Chain(t.Chain), "business", shop != null ? shop.Name : "?"),
                        BrandsText.T("reallife_brand_plan_chain_what", "days", t.Breach.ToString(), "most", BrandCatalogue.ChainEndDays.ToString()), null, null);
                }
            }
        }

        private static string PlannerMoney(string key, int amount)
        {
            return BrandsText.T(key, "amount", BrandsText.Money(amount));
        }

        private void Alert(int a, int b, int day, string title, string what, string money, string ifMissed)
        {
            if (_alerts.Count < BrandCatalogue.AlertsMost)
            {
                _alerts.Add(new BrandAlert { A = a, B = b, Day = day, Title = title, What = what, Money = money, IfMissed = ifMissed });
            }
        }
    }
}
