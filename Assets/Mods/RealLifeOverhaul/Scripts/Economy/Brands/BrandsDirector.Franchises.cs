using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The director's part for what the player does with applicants and franchisees: signing, inspecting, warning,
    /// retraining, ending an agreement and answering what a franchisee asks. Each runs once, on a press.
    /// </summary>
    public sealed partial class BrandsDirector
    {
        /// <summary>The buildings to let in the district an applicant wants, for the brand's type: read when the applicant's page first shows, kept while it shows.</summary>
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
        /// The agreement is signed at these terms if the applicant takes them, and the shop opens: in the building picked
        /// as a business of the game's where the game has one that fits; otherwise the franchisee takes on a business of
        /// the type that already trades in the district; otherwise in the mod's books only, at the building picked.
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
                    _sitesFor = 0;   // the list was read when the page was opened: the building has been let since
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
                Place(f, b, picked, canConvert ? existing : null, day);
                _sitesFor = 0;
                Pay(f.EntryFee, BrandsGame.EntryKey, f.Name, "the entry fee of " + b.Name + " at today's prices (" + index + ")");
                Logger?.Info("Brands: signed with " + f.Name + " (" + f.Temper + ", ability " + f.Ability + ") for '" + b.Name + "' in " + f.District + ": entry fee " + f.EntryFee
                             + ", royalty " + f.RoyaltyBp + " and levy " + f.LevyBp + " ten-thousandths, " + (f.Exclusive ? "the district alone, " : string.Empty) + "until day " + f.Until
                             + "; the shop is " + f.Hold + " at " + f.Place + ", " + f.Base + " customers on an ordinary day; the franchisee starts with " + f.Health + ".");
                Say(NotificationType.Success, "reallife_brand_note_signed", new Dictionary<string, string>
                {
                    { "name", f.Name }, { "brand", b.Name }, { "amount", BrandsText.Money(f.EntryFee) }, { "how", BrandsText.Hold(f.Hold) }, { "address", BrandsGame.AddressWords(f.Place) }
                });
                _view?.Want(TabAgreements, "fr:" + f.Id);
                Changed();
            });
        }

        // A signed shop is put in the city: at the site, or as the existing business of the type the franchisee takes on
        // where the site gives no business of the game's. The game's market leaves it alone from today, and where the
        // game's formula gives the building no customers for the type, the member shops' average of yesterday stands in.
        private void Place(Franchise f, Brand b, BrandSite site, Franchise existing, int day)
        {
            if (site != null)
            {
                BrandsGame.Open(f, site, b.Type);
            }
            if (f.Hold == Hold.Books && existing != null)
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

        /// <summary>An inspection: in person and free if the player stands in the shop, otherwise an inspector is sent for a fee.</summary>
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
                Logger?.Info("Brands check (inspection, " + f.Name + "): " + (inPerson ? "in person, free" : "by an inspector for " + fee) + "; found standards of " + found
                             + " (kept as " + f.True + " hundredths) against the brand's minimum of " + b.Minimum + ": PASS");
                Say(found < b.Minimum ? NotificationType.Warning : NotificationType.Success, inPerson ? "reallife_brand_note_inspected_self" : "reallife_brand_note_inspected",
                    new Dictionary<string, string> { { "name", f.Name }, { "standards", found.ToString() }, { "minimum", BrandsText.Minimum(b.Minimum) } });
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

        /// <summary>The brand ends the agreement: with cause if there is one, otherwise for compensation and at a cost in recognition.</summary>
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
                    BrandsBridges.WithdrawClaim(f.Suit);   // compensation settles what was claimed
                }
                BrandsGame.Close(f, b.Type);
                int lost = FranchiseRules.End(_state, b, f, cause, Save.Day);
                Logger?.Info("Brands: the agreement with " + f.Name + " ended " + (cause ? "with cause (" + f.Warnings + " warnings, found at " + f.Seen + ")"
                                 : "without cause: compensation " + compensation + ", recognition in " + f.District + " down " + lost + " hundredths") + ".");
                Say(cause ? NotificationType.Info : NotificationType.Warning, cause ? "reallife_brand_note_ended_cause" : "reallife_brand_note_ended", new Dictionary<string, string>
                {
                    { "name", f.Name }, { "amount", BrandsText.Money(compensation) }, { "points", BrandsText.Lost(lost) }
                });
                _view?.Want(TabAgreements, null);
                Changed();
            });
        }

        /// <summary>Yes to what a franchisee asks: the grant, the second shop, the renewal, giving way in a dispute, or selling the brand.</summary>
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
                        Say(NotificationType.Info, "reallife_brand_note_settled", new Dictionary<string, string>
                        {
                            { "name", f.Name }, { "royalty", BrandsText.Bp(f.RoyaltyBp) }, { "levy", BrandsText.Bp(f.LevyBp) }
                        });
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

        /// <summary>The other answer to help: the royalty and the levy are waived for a while.</summary>
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

        /// <summary>No to what a franchisee asks. A dispute before the court stays there.</summary>
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

        // The brand is sold to the franchisee that bid for it: the price is paid, its agreements pass to the buyer, and
        // the player's member shops are plain shops again.
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
            Pay(price, BrandsGame.SaleKey, name, "the bid of " + buyer.Name + ": " + BrandCatalogue.BidDays + " days of the franchises' net and " + BrandCatalogue.BidPerPoint
                                                 + " a point of city-wide recognition");
            BrandRules.Dissolve(_state, b);
            Logger?.Info("Brands: '" + name + "' sold to " + buyer.Name + " for " + price + "; " + members + " member shop(s) trade on under their own names, " + franchises
                         + " franchised shop(s) are the buyer's.");
            Say(NotificationType.Success, "reallife_brand_note_sold", new Dictionary<string, string> { { "brand", name }, { "name", buyer.Name }, { "amount", BrandsText.Money(price) } });
            Reread();
            _view?.Want(TabBrands, null);
            Changed();
        }
    }
}
