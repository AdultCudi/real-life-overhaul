using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// What the Brands app asks of the director: everything the player can do. Each runs once, on a press, and ends in
    /// <see cref="Changed"/>, which works the footfall out again, writes it where it differs and makes the page again.
    /// A press that cannot be done says why in one of the game's notifications. Nothing here runs on a timer.
    /// </summary>
    public sealed partial class BrandsDirector
    {
        private bool Ready => _running && !_broken && _state != null && Save != null;

        private void Act(string what, Action work)
        {
            if (!Ready)
            {
                return;
            }
            try
            {
                work();
            }
            catch (Exception e)
            {
                Fault(what, e);
            }
        }

        private static void Say(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 9f, null, null, true, false);
        }

        // ---------------------------------------------------------------- a brand

        /// <summary>A shop becomes a brand, under its own name; the name can be changed afterwards with the game's text input.</summary>
        public void CreateBrand(string place)
        {
            Act("making a brand", () =>
            {
                BrandShop shop = ShopAt(place);
                if (shop == null)
                {
                    return;
                }
                Brand b = BrandRules.Create(_state, shop.Name, shop.Type, shop.Place, shop.District, Save.Day);
                if (b == null)
                {
                    Say(NotificationType.Error, _state.Brands.Count >= BrandCatalogue.BrandsMost ? "reallife_brand_note_brands_most" : "reallife_brand_note_cannot_brand");
                    return;
                }
                shop.File = _state.Member(place);
                Logger?.Info("Brands: '" + b.Name + "' made from " + shop.Name + " (" + b.Type + ") in " + shop.District + " on day " + Save.Day + ".");
                Say(NotificationType.Success, "reallife_brand_note_made", new Dictionary<string, string> { { "brand", b.Name } });
                Reread();
                _view?.Want(TabBrands, "brand:" + b.Id);
                Changed();
            });
        }

        /// <summary>The brand's name, typed with the game's own text input. Said in a notification if the game has none to show.</summary>
        public void Rename(int brandId)
        {
            Act("naming a brand", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null)
                {
                    return;
                }
                bool asked = BrandsGame.AskName(b.Name, typed =>
                {
                    Act("naming a brand", () =>
                    {
                        Brand named = _state.Brand(brandId);
                        string name = BrandRules.Name(typed);
                        if (named == null || name.Length == 0)
                        {
                            return;
                        }
                        Logger?.Info("Brands: '" + named.Name + "' is now '" + name + "'.");
                        named.Name = name;
                        BrandsGame.Remark();
                        Changed(false);
                    });
                });
                if (!asked)
                {
                    Say(NotificationType.Info, "reallife_brand_note_no_input");
                }
            });
        }

        public void Join(int brandId, string place)
        {
            Act("joining a brand", () =>
            {
                Brand b = _state.Brand(brandId);
                BrandShop shop = ShopAt(place);
                if (b == null || shop == null || !BrandRules.Join(_state, b, shop.Place, shop.Type, shop.District, Save.Day))
                {
                    Say(NotificationType.Error, "reallife_brand_note_cannot_join");
                    return;
                }
                Logger?.Info("Brands: " + shop.Name + " joins '" + b.Name + "'.");
                Reread();
                Changed();
            });
        }

        public void Leave(string place)
        {
            Act("leaving a brand", () =>
            {
                Member m = _state.Member(place);
                Brand b = m != null ? _state.Brand(m.BrandId) : null;
                if (m == null || !BrandRules.Leave(_state, place))
                {
                    return;
                }
                Logger?.Info("Brands: " + ShopName(place) + " leaves '" + (b != null ? b.Name : "?") + "'.");
                Reread();
                Changed();
            });
        }

        /// <summary>A brand is given up. Not while it has franchised shops: their agreements are ended one by one first.</summary>
        public void GiveUp(int brandId)
        {
            Act("giving up a brand", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null)
                {
                    return;
                }
                if (BrandRules.Franchised(_state, b, null) > 0)
                {
                    Say(NotificationType.Error, "reallife_brand_note_has_franchises");
                    return;
                }
                Logger?.Info("Brands: '" + b.Name + "' given up; its " + BrandRules.Members(_state, b, null) + " member shop(s) trade on under their own names.");
                BrandRules.Dissolve(_state, b);
                Reread();
                Changed();
            });
        }

        /// <summary>The required minimum for standards, one of the catalogue's steps.</summary>
        public void SetMinimum(int brandId, int step)
        {
            Act("the minimum", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b != null)
                {
                    b.Minimum = BrandCatalogue.MinimumSteps[BrandCatalogue.Clamp(step, 0, BrandCatalogue.MinimumSteps.Length - 1)];
                    Changed(false);
                }
            });
        }

        /// <summary>The brand goes on offer as a franchise, or comes off it. Applicants who wait stay.</summary>
        public void Offer(int brandId, bool on)
        {
            Act("offering a franchise", () =>
            {
                Brand b = _state.Brand(brandId);
                int good;
                if (b == null)
                {
                    return;
                }
                if (on && !BrandRules.CanOffer(_state, b, out good))
                {
                    Say(NotificationType.Error, "reallife_brand_note_cannot_offer", new Dictionary<string, string>
                    {
                        { "shops", BrandCatalogue.OfferShops.ToString() }, { "standards", BrandCatalogue.OfferStandards.ToString() }, { "city", BrandsText.Points(BrandCatalogue.OfferCityRec) },
                        { "district", BrandsText.Points(BrandCatalogue.OfferDistrictRec) }
                    });
                    return;
                }
                b.Offered = on;
                Logger?.Info("Brands: '" + b.Name + "' is " + (on ? "on offer as a franchise" : "no longer on offer") + ".");
                Changed(false);
            });
        }

        /// <summary>What a brand campaign would cost today, and how much of that the levy collected covers.</summary>
        public int PushCost(Brand b, int kind, string district)
        {
            return BrandRules.PushCost(kind, kind == BrandCatalogue.PushCity ? 0 : BrandRules.Shops(_state, b, district), Index);
        }

        public void BookPush(int brandId, int kind, string district)
        {
            Act("a brand campaign", () =>
            {
                Brand b = _state.Brand(brandId);
                if (b == null || b.Push >= 0 || (kind != BrandCatalogue.PushCity && BrandRules.Shops(_state, b, district) == 0))
                {
                    return;
                }
                int cost = PushCost(b, kind, district);
                if (!Cost(cost, BrandsGame.CampaignKey, b.Name, BrandsText.Push(kind) + " at today's prices (" + Index + "), " + Math.Min(b.Fund, cost) + " of it from the levy collected"))
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                int fund = BrandRules.FromFund(b, cost);
                BrandRules.BookPush(b, kind, district);
                Logger?.Info("Brands: " + (kind == BrandCatalogue.PushCity ? "a city-wide campaign" : "a campaign in " + district) + " booked for '" + b.Name + "': cost " + cost + " (" + fund + " of it the franchisees' levy), "
                             + BrandRules.PushDays(kind) + " brand day(s) from the next.");
                Say(NotificationType.Success, "reallife_brand_note_push", new Dictionary<string, string> { { "campaign", BrandsText.Push(kind) }, { "brand", b.Name }, { "days", BrandRules.PushDays(kind).ToString() } });
                Changed(false);
            });
        }

        /// <summary>The flagship's prices at every other member shop, for the products each already sells.</summary>
        public void SharePrices(int brandId)
        {
            Act("sharing prices", () =>
            {
                Brand b = _state.Brand(brandId);
                BrandShop from = b != null ? ShopAt(b.Flagship) : null;
                if (from == null)
                {
                    return;
                }
                int prices = 0, shops = 0;
                for (int i = 0; i < _shops.Count; i++)
                {
                    BrandShop to = _shops[i];
                    if (to.File != null && to.File.BrandId == b.Id && !ReferenceEquals(to, from))
                    {
                        int changed = BrandsGame.SharePrices(from.Reg, to.Reg);
                        prices += changed;
                        shops += changed > 0 ? 1 : 0;
                    }
                }
                Logger?.Info("Brands check (shared prices, " + b.Name + "): " + prices + " price(s) at " + shops + " shop(s) set to those of " + from.Name + ", each in the shop's price list and its stored one as the game's own price cell writes them: PASS");
                Say(NotificationType.Success, "reallife_brand_note_prices", new Dictionary<string, string> { { "count", prices.ToString() }, { "shops", shops.ToString() }, { "business", from.Name } });
                Changed();
            });
        }

        /// <summary>The flagship's opening hours at every other member shop. Shifts are left as they are.</summary>
        public void ShareHours(int brandId)
        {
            Act("sharing hours", () =>
            {
                Brand b = _state.Brand(brandId);
                BrandShop from = b != null ? ShopAt(b.Flagship) : null;
                if (from == null)
                {
                    return;
                }
                int days = 0, shops = 0;
                for (int i = 0; i < _shops.Count; i++)
                {
                    BrandShop to = _shops[i];
                    if (to.File != null && to.File.BrandId == b.Id && !ReferenceEquals(to, from))
                    {
                        int changed = BrandsGame.ShareHours(from.Reg, to.Reg);
                        days += changed;
                        shops += changed > 0 ? 1 : 0;
                    }
                }
                Logger?.Info("Brands check (shared hours, " + b.Name + "): " + days + " day(s) at " + shops + " shop(s) given the opening hours of " + from.Name + ", as the game's own hour switches leave a day; no shift touched: PASS");
                Say(NotificationType.Success, "reallife_brand_note_hours", new Dictionary<string, string> { { "shops", shops.ToString() }, { "business", from.Name } });
                Changed(false);
            });
        }

        // The player's businesses again, now: a shop has just joined or left something.
        private void Reread()
        {
            int left;
            Read(Save.Day, out left);
        }

        // ---------------------------------------------------------------- applicants

        /// <summary>The buildings to let in the district an applicant wants, for the brand's type: read from the game when the applicant's page is first shown, and kept while it shows.</summary>
        public IReadOnlyList<BrandSite> SitesFor(Applicant a)
        {
            if (a == null)
            {
                return _sites;
            }
            if (_sitesFor != a.Id)
            {
                Brand b = _state.Brand(a.BrandId);
                BrandsGame.Sites(a.District, b != null ? b.Type : string.Empty, _sites);
                _sitesFor = a.Id;
            }
            return _sites;
        }

        public void Refuse(int applicantId)
        {
            Act("refusing an applicant", () =>
            {
                Applicant a = _state.Applicant(applicantId);
                if (a != null)
                {
                    _state.Applicants.Remove(a);
                    Logger?.Info("Brands: " + a.Name + " refused.");
                    Changed(false);
                }
            });
        }

        /// <summary>
        /// The agreement is signed at these terms, if the applicant takes them, and the shop opens: in the building
        /// picked as a business of the game's own where the game has one that fits; otherwise the franchisee takes on
        /// a business of the type that already trades in the district; otherwise the shop is in the mod's books only,
        /// at the building picked. With no building to let, only the second of those is possible.
        /// </summary>
        public void Sign(int applicantId, int site, int feeTier, int royaltyTier, int levyTier, bool exclusive, int lengthTier)
        {
            Act("signing a franchise", () =>
            {
                Applicant a = _state.Applicant(applicantId);
                Brand b = a != null ? _state.Brand(a.BrandId) : null;
                if (b == null)
                {
                    return;
                }
                int index = Index, day = Save.Day;
                Refusal why = FranchiseRules.Answer(_state, b, a, feeTier, royaltyTier, levyTier, exclusive, index);
                if (why != Refusal.None)
                {
                    Say(NotificationType.Error, BrandsText.RefusalKeys[(int)why]);
                    return;
                }
                IReadOnlyList<BrandSite> sites = SitesFor(a);
                BrandSite picked = site >= 0 && site < sites.Count ? sites[site] : null;
                if (picked != null && !BrandsGame.Free(picked.Reg))
                {
                    // (the list was read when the page was opened: the building has been let since)
                    _sitesFor = 0;
                    Say(NotificationType.Error, "reallife_brand_note_site_gone");
                    Changed(false);
                    return;
                }
                var existing = new Franchise();
                bool canConvert = BrandsGame.Convert(existing, _state, a.District, b.Type);
                if (picked == null && !canConvert)
                {
                    Say(NotificationType.Error, "reallife_brand_note_no_site", new Dictionary<string, string> { { "district", BrandsText.District(a.District) } });
                    return;
                }
                Franchise f = FranchiseRules.Sign(_state, b, a, feeTier, royaltyTier, levyTier, exclusive, lengthTier, index, day, BrandsGame.DaysPerYear);
                if (picked != null)
                {
                    BrandsGame.Open(f, picked, b.Type);
                }
                if (f.Hold == Hold.Books && canConvert)
                {
                    f.Place = existing.Place;
                    f.Hold = existing.Hold;
                    f.GameName = existing.GameName;
                    f.HasSpot = existing.HasSpot;
                    f.X = existing.X;
                    f.Z = existing.Z;
                    f.Base = existing.Base;
                    f.Born = existing.Born;
                }
                Placed(b, f, day);
                _sitesFor = 0;
                Pay(f.EntryFee, BrandsGame.EntryKey, f.Name, "the entry fee of " + b.Name + " at today's prices (" + index + ")");
                Logger?.Info("Brands: signed with " + f.Name + " (" + f.Temper + ", ability " + f.Ability + ") for '" + b.Name + "' in " + f.District + ": entry fee " + f.EntryFee + ", royalty " + f.RoyaltyBp + " and levy " + f.LevyBp
                             + " ten-thousandths, " + (f.Exclusive ? "the district alone, " : string.Empty) + "until day " + f.Until + "; the shop is " + f.Hold + " at " + f.Place + ", " + f.Base
                             + " customers on an ordinary day; the franchisee starts with " + f.Health + ".");
                Say(NotificationType.Success, "reallife_brand_note_signed", new Dictionary<string, string>
                {
                    { "name", f.Name }, { "brand", b.Name }, { "amount", BrandsText.Money(f.EntryFee) }, { "how", BrandsText.Hold(f.Hold) }, { "address", BrandsGame.AddressWords(f.Place) }
                });
                _view?.Want(TabAgreements, "fr:" + f.Id);
                Changed();
            });
        }

        // A shop that has just been placed: the game's market leaves it alone from today, and where the game's formula
        // gives the building no customers for the type (a type whose customers the game counts another way) the member
        // shops' own average of yesterday stands in.
        private void Placed(Brand b, Franchise f, int day)
        {
            BrandsGame.Keep(f, b.Type, day);
            if (f.Base > 0)
            {
                return;
            }
            int members = BrandRules.Members(_state, b, null);
            long customers = 0;
            for (int i = 0; i < _state.Members.Count; i++)
            {
                customers += _state.Members[i].BrandId == b.Id ? Math.Max(0, _state.Members[i].Customers) : 0;
            }
            f.Base = members > 0 ? (int)(customers / members) : 0;
            Logger?.Info("Brands: the game's formula gives " + f.Place + " no customers for " + b.Type + "; the member shops' average of yesterday, " + f.Base + ", is used.");
        }

        // ---------------------------------------------------------------- keeping a franchisee to the standards

        /// <summary>An inspection: in person, free, if the player stands in the shop; otherwise an inspector is sent for a fee.</summary>
        public void Inspect(int franchiseId)
        {
            Act("an inspection", () =>
            {
                Franchise f = _state.Franchise(franchiseId);
                Brand b = f != null ? _state.Brand(f.BrandId) : null;
                if (b == null)
                {
                    return;
                }
                bool inPerson = BrandsGame.PlayerInside(f);
                int fee = inPerson ? 0 : FranchiseRules.InspectorFee(Index);
                if (!Cost(fee, BrandsGame.InspectorKey, f.Name, "an inspector's visit at today's prices (" + Index + ")"))
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                int found = FranchiseRules.Inspect(f, Save.Day);
                Logger?.Info("Brands check (inspection, " + f.Name + "): " + (inPerson ? "in person, free" : "by an inspector for " + fee) + "; found standards of " + found + " (kept as " + f.True + " hundredths) against the brand's minimum of "
                             + b.Minimum + ": PASS");
                Say(found < b.Minimum ? NotificationType.Warning : NotificationType.Success, inPerson ? "reallife_brand_note_inspected_self" : "reallife_brand_note_inspected", new Dictionary<string, string>
                {
                    { "name", f.Name }, { "standards", found.ToString() }, { "minimum", BrandsText.Minimum(b.Minimum) }
                });
                Changed(false);
            });
        }

        public void Warn(int franchiseId)
        {
            Act("a warning", () =>
            {
                Franchise f = _state.Franchise(franchiseId);
                Brand b = f != null ? _state.Brand(f.BrandId) : null;
                if (b == null || !FranchiseRules.CanWarn(b, f, Save.Day))
                {
                    Say(NotificationType.Error, "reallife_brand_note_cannot_warn", new Dictionary<string, string> { { "days", BrandCatalogue.FreshDays.ToString() } });
                    return;
                }
                FranchiseRules.Warn(f, Save.Day);
                Logger?.Info("Brands: " + f.Name + " warned (warning " + f.Warnings + "); found at " + f.Seen + " on day " + f.SeenDay + " against a minimum of " + b.Minimum + ".");
                Say(NotificationType.Info, "reallife_brand_note_warned", new Dictionary<string, string> { { "name", f.Name }, { "count", f.Warnings.ToString() } });
                Changed(false);
            });
        }

        public void Retrain(int franchiseId)
        {
            Act("retraining", () =>
            {
                Franchise f = _state.Franchise(franchiseId);
                if (f == null)
                {
                    return;
                }
                if (!Cost(FranchiseRules.RetrainFee(Index), BrandsGame.RetrainKey, f.Name, "retraining at today's prices (" + Index + ")"))
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                FranchiseRules.Retrain(f, Save.Day);
                Logger?.Info("Brands: " + f.Name + " retrained; standards now " + f.True + " hundredths.");
                Say(NotificationType.Success, "reallife_brand_note_retrained", new Dictionary<string, string> { { "name", f.Name } });
                Changed();
            });
        }

        /// <summary>What ending the agreement would cost today: nothing with cause.</summary>
        public int EndCost(Franchise f)
        {
            Brand b = _state.Brand(f.BrandId);
            return b == null || FranchiseRules.HasCause(b, f, Save.Day) ? 0 : FranchiseRules.Compensation(b, f, Save.Day);
        }

        /// <summary>The agreement is ended by the brand: with cause if there is one, otherwise for compensation and at a cost in recognition. The shop closes or leaves the brand.</summary>
        public void End(int franchiseId)
        {
            Act("ending an agreement", () =>
            {
                Franchise f = _state.Franchise(franchiseId);
                Brand b = f != null ? _state.Brand(f.BrandId) : null;
                if (b == null)
                {
                    return;
                }
                bool cause = FranchiseRules.HasCause(b, f, Save.Day);
                int compensation = cause ? 0 : FranchiseRules.Compensation(b, f, Save.Day);
                if (compensation > BrandsGame.Cash)
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                Charge(compensation, BrandsGame.CompensationKey, f.Name, "ending the agreement without cause, " + Math.Max(0, f.Until - Save.Day) + " day(s) before its end");
                if (f.Suit != 0 && BrandsBridges.WithdrawClaim != null)
                {
                    BrandsBridges.WithdrawClaim(f.Suit);   // (compensation settles what was claimed)
                }
                BrandsGame.Close(f, b.Type);
                int lost = FranchiseRules.End(_state, b, f, cause, Save.Day);
                Logger?.Info("Brands: the agreement with " + f.Name + " ended " + (cause ? "with cause (" + f.Warnings + " warnings, found at " + f.Seen + ")" : "without cause: compensation " + compensation + ", recognition in "
                             + f.District + " down " + lost + " hundredths") + ".");
                Say(cause ? NotificationType.Info : NotificationType.Warning, cause ? "reallife_brand_note_ended_cause" : "reallife_brand_note_ended", new Dictionary<string, string>
                {
                    { "name", f.Name }, { "amount", BrandsText.Money(compensation) }, { "points", BrandsText.Lost(lost) }
                });
                _view?.Want(TabAgreements, null);
                Changed();
            });
        }

        // ---------------------------------------------------------------- answering what a franchisee asks

        /// <summary>Yes to what a franchisee asks: the grant is paid, the second shop goes to the applicants, the agreement is renewed, the dispute is settled by giving way, or the brand is sold.</summary>
        public void Agree(int franchiseId)
        {
            Act("an answer", () =>
            {
                Franchise f = _state.Franchise(franchiseId);
                Brand b = f != null ? _state.Brand(f.BrandId) : null;
                if (b == null || f.Ask == Ask.None)
                {
                    return;
                }
                int day = Save.Day;
                switch (f.Ask)
                {
                    case Ask.Help:
                    {
                        int grant = f.AskAmount;
                        if (!Cost(grant, BrandsGame.GrantKey, f.Name, BrandCatalogue.HelpDays + " ordinary days' takings to a franchisee short of cash"))
                        {
                            Notifications.ShowInsufficientMoney();
                            return;
                        }
                        FranchiseRules.Help(b, f, grant, day);
                        Say(NotificationType.Success, "reallife_brand_note_helped", new Dictionary<string, string> { { "name", f.Name }, { "amount", BrandsText.Money(grant) } });
                        break;
                    }
                    case Ask.SecondShop:
                    {
                        Applicant a = FranchiseRules.Second(_state, b, f, day);
                        if (a == null)
                        {
                            Say(NotificationType.Error, "reallife_brand_note_applicants_full");
                            return;
                        }
                        Say(NotificationType.Info, "reallife_brand_note_second_agreed", new Dictionary<string, string> { { "name", f.Name } });
                        _view?.Want(TabApplicants, "app:" + a.Id);
                        break;
                    }
                    case Ask.Renewal:
                        Pay(f.AskAmount, BrandsGame.RenewalKey, f.Name, BrandsText.Share(BrandCatalogue.RenewalShare) + " of the entry fee of " + f.EntryFee);
                        FranchiseRules.Renew(f);
                        Say(NotificationType.Success, "reallife_brand_note_renewed", new Dictionary<string, string> { { "name", f.Name }, { "day", f.Until.ToString() } });
                        break;
                    case Ask.Dispute:
                    {
                        int suit = f.Suit;
                        if (!FranchiseRules.Settle(b, f, day))
                        {
                            Say(NotificationType.Error, "reallife_brand_note_nothing_to_give");
                            return;
                        }
                        if (suit != 0 && BrandsBridges.WithdrawClaim != null)
                        {
                            BrandsBridges.WithdrawClaim(suit);
                        }
                        Say(NotificationType.Info, "reallife_brand_note_settled", new Dictionary<string, string> { { "name", f.Name }, { "royalty", BrandsText.Bp(f.RoyaltyBp) }, { "levy", BrandsText.Bp(f.LevyBp) } });
                        break;
                    }
                    case Ask.BuyBrand:
                        Sell(b, f);
                        return;
                }
                Logger?.Info("Brands: agreed with " + f.Name + ".");
                Changed();
            });
        }

        /// <summary>The other answer to help: the royalty and the levy are waived for fourteen days.</summary>
        public void Waive(int franchiseId)
        {
            Act("an answer", () =>
            {
                Franchise f = _state.Franchise(franchiseId);
                Brand b = f != null ? _state.Brand(f.BrandId) : null;
                if (b != null && f.Ask == Ask.Help)
                {
                    FranchiseRules.Waive(b, f, Save.Day);
                    Logger?.Info("Brands: royalty and levy waived for " + f.Name + " until day " + f.WaivedUntil + ".");
                    Say(NotificationType.Info, "reallife_brand_note_waived", new Dictionary<string, string> { { "name", f.Name }, { "day", f.WaivedUntil.ToString() } });
                    Changed(false);
                }
            });
        }

        /// <summary>No to what a franchisee asks. A dispute that is before the court stays there.</summary>
        public void Decline(int franchiseId)
        {
            Act("an answer", () =>
            {
                Franchise f = _state.Franchise(franchiseId);
                Brand b = f != null ? _state.Brand(f.BrandId) : null;
                if (b == null || f.Ask == Ask.None || (f.Ask == Ask.Dispute && f.Suit != 0))
                {
                    return;
                }
                Logger?.Info("Brands: " + f.Ask + " of " + f.Name + " declined.");
                if (f.Ask == Ask.Dispute)
                {
                    FranchiseRules.Stand(b, f, Save.Day);
                }
                else
                {
                    FranchiseRules.Drop(b, f, Save.Day);
                }
                Changed();
            });
        }

        // The brand is sold to the franchisee that bid for it: the price is paid, its agreements pass to the buyer (the
        // shops trade on as the city's), and the player's member shops are plain shops again.
        private void Sell(Brand b, Franchise buyer)
        {
            int price = b.Bid;
            string name = b.Name;
            int members = BrandRules.Members(_state, b, null), franchises = BrandRules.Franchised(_state, b, null);
            for (int i = 0; i < _state.Franchises.Count; i++)
            {
                Franchise f = _state.Franchises[i];
                if (f.BrandId == b.Id && f.Suit != 0 && BrandsBridges.WithdrawClaim != null)
                {
                    BrandsBridges.WithdrawClaim(f.Suit);
                }
            }
            Pay(price, BrandsGame.SaleKey, name, "the bid of " + buyer.Name + ": " + BrandCatalogue.BidDays + " days of the franchises' net and " + BrandCatalogue.BidPerPoint + " a point of city-wide recognition");
            BrandRules.Dissolve(_state, b);
            Logger?.Info("Brands: '" + name + "' sold to " + buyer.Name + " for " + price + "; " + members + " member shop(s) trade on under their own names, " + franchises + " franchised shop(s) are the buyer's.");
            Say(NotificationType.Success, "reallife_brand_note_sold", new Dictionary<string, string> { { "brand", name }, { "name", buyer.Name }, { "amount", BrandsText.Money(price) } });
            Reread();
            _view?.Want(TabBrands, null);
            Changed();
        }

        // ---------------------------------------------------------------- a shop under a city chain

        public void TakeChain(string place, int chain)
        {
            Act("joining a chain", () =>
            {
                BrandShop shop = ShopAt(place);
                if (shop == null || shop.File != null || shop.Chain != null)
                {
                    return;
                }
                chain = FranchiseRules.Chain(chain);
                if (_state.Taken.Count >= BrandCatalogue.TakenMost)
                {
                    Say(NotificationType.Error, "reallife_brand_note_chains_most");
                    return;
                }
                int fee = FranchiseRules.ChainFee(chain, Index);
                if (!Cost(fee, BrandsGame.ChainFeeKey, shop.Name, "joining " + BrandsText.Chain(chain) + " at today's prices (" + Index + ")"))
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                FranchiseRules.Take(_state, place, chain, Save.Day, BrandsGame.DaysPerYear);
                Logger?.Info("Brands: " + shop.Name + " joins the city chain " + BrandsText.Chain(chain) + " for " + fee + "; +" + FranchiseRules.ChainFootfall(chain) + "% of customers while it keeps standards of "
                             + BrandCatalogue.ChainStandards[chain] + " and prices inside " + BrandCatalogue.ChainPriceLeast[chain] + " to " + BrandCatalogue.ChainPriceMost[chain] + " thousandths of the market's.");
                Say(NotificationType.Success, "reallife_brand_note_chain_joined", new Dictionary<string, string> { { "business", shop.Name }, { "chain", BrandsText.Chain(chain) } });
                Reread();
                Changed();
            });
        }

        /// <summary>What leaving its chain would cost a shop today.</summary>
        public int ChainExit(Taken t)
        {
            return FranchiseRules.ChainExit(t, Index);
        }

        public void LeaveChain(string place)
        {
            Act("leaving a chain", () =>
            {
                Taken t = _state.TakenAt(place);
                if (t == null)
                {
                    return;
                }
                int exit = ChainExit(t);
                if (exit > BrandsGame.Cash)
                {
                    Notifications.ShowInsufficientMoney();
                    return;
                }
                Charge(exit, BrandsGame.ChainExitKey, ShopName(place), "leaving " + BrandsText.Chain(t.Chain));
                _state.Taken.Remove(t);
                Logger?.Info("Brands: " + ShopName(place) + " left " + BrandsText.Chain(t.Chain) + " for " + exit + ".");
                Say(NotificationType.Info, "reallife_brand_note_chain_left", new Dictionary<string, string> { { "business", ShopName(place) }, { "chain", BrandsText.Chain(t.Chain) }, { "amount", BrandsText.Money(exit) } });
                Reread();
                Changed();
            });
        }

        /// <summary>A shop's prices put at the middle of what its chain asks.</summary>
        public void ChainPrices(string place)
        {
            Act("a chain's prices", () =>
            {
                BrandShop shop = ShopAt(place);
                if (shop == null || shop.Chain == null)
                {
                    return;
                }
                int changed = BrandsGame.ChainPrices(shop.Reg, shop.Chain.Chain);
                shop.Chain.Prices = BrandsGame.PriceRatio(shop.Reg);
                Logger?.Info("Brands check (chain prices, " + shop.Name + "): " + changed + " price(s) set; its prices now stand at " + shop.Chain.Prices + " thousandths of the game's own market price, wanted "
                             + BrandCatalogue.ChainPriceLeast[shop.Chain.Chain] + " to " + BrandCatalogue.ChainPriceMost[shop.Chain.Chain] + ": "
                             + (FranchiseRules.ChainBreach(shop.Chain.Chain, 100, shop.Chain.Prices) ? "still outside" : "PASS"));
                Say(NotificationType.Success, "reallife_brand_note_chain_prices", new Dictionary<string, string> { { "count", changed.ToString() }, { "business", shop.Name } });
                Changed(false);
            });
        }
    }
}
