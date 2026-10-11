using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Business insurance: what the director does with <see cref="CoverRules"/>.
    ///
    /// WHAT THE GAME ALREADY HAS: health insurance for staff (one of their job demands) and nothing for a business.
    /// WHAT THE MOD ALREADY HAS, AND IS LEFT AS IT IS: a fire at a business (a repair bill and some hours shut), a
    /// break-in (stock taken and a repair), and business pressure's inspections (a fine). Each loss stays that
    /// feature's own, in full, as before. <see cref="Loss"/> is told of it through Wiring/CompanyLinks, from the small
    /// public events those features raise (Events/FireLosses, Crime/BreakIns, Business/BusinessEvents).
    ///
    /// WHAT IS ADDED HERE: a policy is a daily premium (a transaction of the mod's own, deductible) and a claim is a
    /// payment some days after it is put in (a transaction of the mod's own): way c only. Nothing of the game's is
    /// changed and no other feature's loss is made smaller: the money comes back afterwards, less the excess.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        partial void CoverDay(int day)
        {
            int yesterday = day - 1, term = CompanyCatalogue.TermDays(CompanyGame.DaysPerYear);
            for (int i = 0; i < _shops.Count; i++)
            {
                CompanyShop shop = _shops[i];
                for (int k = 0; k < shop.File.Cover.Length; k++)
                {
                    Policy p = shop.File.Cover[k];
                    if (!p.Active)
                    {
                        continue;
                    }
                    if (p.From <= yesterday)
                    {
                        bool taken = Cost(p.Premium, CompanyGame.PremiumKey, shop, shop.Name, CostKind.Premiums, yesterday,
                            CompanyText.Cover(k) + " cover with insurer " + p.Insurer + " for day " + yesterday + ", fixed for the term to day " + p.Until);
                        if (CoverRules.Charged(p, taken))
                        {
                            Tell(NotificationType.Warning, "reallife_co_note_cover_lapsed", CoverData(shop, k, 0, 0), TabCover, "pol:" + shop.File.Place + ":" + k);
                            continue;
                        }
                    }
                    if (CoverRules.TermOver(p, day))
                    {
                        int insurer = p.Insurer, claims = p.Claims, before = p.Premium;
                        bool renewed = CoverRules.Renew(p, k, shop.Average, shop.Income, _index, day, term);
                        Tell(renewed ? NotificationType.Info : NotificationType.Warning, renewed ? "reallife_co_note_cover_renewed" : "reallife_co_note_cover_declined", CoverData(shop, k, insurer, p.Premium),
                            TabCover, "pol:" + shop.File.Place + ":" + k);
                        if (Verbose)
                        {
                            Logger?.Info("Company check (cover, " + shop.Name + ", " + CompanyText.Cover(k) + "): the term ended with " + claims + " claim(s); insurer " + insurer + " stands "
                                         + (CompanyCatalogue.InsurerClaimsMost[insurer] - 1) + " in a term: " + (renewed ? "renewed to day " + p.Until + ", premium " + before + " to " + p.Premium + " a day (an average day's takings "
                                                                                                                             + shop.Average + ", district income " + shop.Income + ", prices " + _index + ")" : "declined, the policy has ended") + ": PASS");
                        }
                    }
                }
            }
            for (int i = 0; i < _state.Claims.Count; i++)
            {
                Claim c = _state.Claims[i];
                if (CoverRules.Day(c, day))
                {
                    Pay(c.Payout, CompanyGame.ClaimKey, ShopAt(c.Place), c.Name, null, "claim " + c.Id + " for a loss of " + c.Loss + " on day " + c.Day + ", less an excess of " + c.Excess + ", paid by insurer " + c.Insurer);
                    Tell(NotificationType.Success, "reallife_co_note_claim_paid", new Dictionary<string, string>
                    {
                        { "business", c.Name }, { "insurer", CompanyText.Insurer(c.Insurer) }, { "amount", CompanyText.Money(c.Payout) }
                    }, TabCover, "claim:" + c.Id);
                }
            }
        }

        private static Dictionary<string, string> CoverData(CompanyShop shop, int kind, int insurer, int premium)
        {
            return new Dictionary<string, string>
            {
                { "business", shop.Name }, { "cover", CompanyText.Cover(kind) }, { "insurer", CompanyText.Insurer(insurer) }, { "premium", CompanyText.Money(premium) }
            };
        }

        /// <summary>
        /// Another feature's loss at a business of the player's (through Wiring/CompanyLinks): if a policy of that kind
        /// covers today, a claim is opened that the player can put in. The loss itself is not touched.
        /// </summary>
        public void Loss(BuildingRegistration reg, CoverKind kind, int amount)
        {
            if (!On(CompanyPart.Cover) || reg == null || amount <= 0 || Save == null)
            {
                return;
            }
            try
            {
                CompanyShop shop = null;
                for (int i = 0; i < _shops.Count && shop == null; i++)
                {
                    shop = ReferenceEquals(_shops[i].Reg, reg) ? _shops[i] : null;
                }
                shop = shop ?? ShopAt(CompanyGame.Place(reg));
                if (shop == null)
                {
                    return;   // not a business that takes money: nothing to insure
                }
                Policy p = shop.File.Cover[(int)kind];
                Claim claim = CoverRules.Open(_state, p, shop.File.Place, shop.Name, (int)kind, amount, Save.Day);
                if (Verbose)
                {
                    Logger?.Info("Company check (cover, a loss at " + shop.Name + "): " + kind + ", " + amount + "; "
                                 + (claim != null ? "covered by insurer " + claim.Insurer + ", excess " + claim.Excess + ", pays " + claim.Payout + " if it is put in by day " + (claim.Day + CompanyCatalogue.ClaimWindowDays)
                                     : p.Active ? "the policy pays nothing for a loss of that size" : "no policy: the loss is the player's in full") + ": PASS");
                }
                if (claim == null)
                {
                    return;
                }
                Tell(NotificationType.Info, "reallife_co_note_claim_open", new Dictionary<string, string>
                {
                    { "business", shop.Name }, { "cover", CompanyText.Cover((int)kind) }, { "amount", CompanyText.Money(claim.Payout) }, { "day", (claim.Day + CompanyCatalogue.ClaimWindowDays).ToString() }
                }, TabCover, "claim:" + claim.Id);
                Changed();
            }
            catch (System.Exception e)
            {
                PartFault(CompanyPart.Cover, "a loss", e);
            }
        }

        // ---------------------------------------------------------------- what the app asks

        /// <summary>What an insurer would charge a day for a kind of cover at a business today, with no claims counted.</summary>
        public int Quote(CompanyShop shop, int kind, int insurer)
        {
            return CoverRules.Premium(kind, insurer, shop.Average, shop.Income, 0, _index);
        }

        public void BuyCover(string place, int kind, int insurer)
        {
            CompanyShop shop = On(CompanyPart.Cover) ? ShopAt(place) : null;
            if (shop == null || kind < 0 || kind >= CompanyCatalogue.CoverKinds || insurer < 0 || insurer >= CompanyCatalogue.Insurers)
            {
                return;
            }
            Policy p = shop.File.Cover[kind];
            if (p.Active || !CoverRules.WillWrite(p, insurer))
            {
                return;
            }
            int premium = Quote(shop, kind, insurer);
            CoverRules.Buy(p, insurer, premium, Save.Day, CompanyCatalogue.TermDays(CompanyGame.DaysPerYear));
            Logger?.Info("Company: " + CompanyText.Cover(kind) + " cover bought for " + shop.Name + " from insurer " + insurer + ": " + premium + " a day (an average day's takings " + shop.Average + ", district income "
                         + shop.Income + ", prices " + _index + "), to day " + p.Until + ".");
            Notifications.Show(NotificationType.Success, "reallife_co_note_cover_bought", CoverData(shop, kind, insurer, premium), 8f, null, null, true, false);
            Changed();
        }

        public void CancelCover(string place, int kind)
        {
            CompanyShop shop = On(CompanyPart.Cover) ? ShopAt(place) : null;
            if (shop == null || kind < 0 || kind >= CompanyCatalogue.CoverKinds || !shop.File.Cover[kind].Active)
            {
                return;
            }
            CoverRules.End(shop.File.Cover[kind]);
            Logger?.Info("Company: " + CompanyText.Cover(kind) + " cover at " + shop.Name + " cancelled by the player.");
            Changed();
        }

        public void FileClaim(int id)
        {
            Claim c = On(CompanyPart.Cover) ? _state.ClaimOf(id) : null;
            if (c == null || c.Stage != ClaimStage.Open)
            {
                return;
            }
            ShopFile file = _state.Shop(c.Place);
            CoverRules.File(c, file != null ? file.Cover[c.Kind] : null, Save.Day);
            Logger?.Info("Company: claim " + c.Id + " (" + CompanyText.Cover(c.Kind) + " at " + c.Name + ", loss " + c.Loss + ") put in: " + c.Payout + " is paid on day " + c.PayDay + ".");
            Notifications.Show(NotificationType.Success, "reallife_co_note_claim_filed", new Dictionary<string, string>
            {
                { "insurer", CompanyText.Insurer(c.Insurer) }, { "amount", CompanyText.Money(c.Payout) }, { "day", c.PayDay.ToString() }
            }, 8f, null, null, true, false);
            Changed();
        }

        partial void CoverAlerts(int day)
        {
            int premiums = 0;
            for (int i = 0; i < _shops.Count; i++)
            {
                for (int k = 0; k < _shops[i].File.Cover.Length; k++)
                {
                    premiums += _shops[i].File.Cover[k].Active ? _shops[i].File.Cover[k].Premium : 0;
                }
            }
            if (premiums > 0)
            {
                Alert(2, 0, day + 1, CompanyText.T("reallife_co_plan_premium_title"), CompanyText.T("reallife_co_plan_premium_what"), CompanyText.T("reallife_planner_costs", "amount", CompanyText.Money(premiums)),
                    CompanyText.T("reallife_co_plan_premium_missed", "days", CompanyCatalogue.PremiumMissedMost.ToString()));
            }
            for (int i = 0; i < _state.Claims.Count; i++)
            {
                Claim c = _state.Claims[i];
                if (c.Stage == ClaimStage.Open)
                {
                    Alert(11, c.Id, c.Day + CompanyCatalogue.ClaimWindowDays, CompanyText.T("reallife_co_plan_claim_title", "business", c.Name),
                        CompanyText.T("reallife_co_plan_claim_what", "cover", CompanyText.Cover(c.Kind)), CompanyText.T("reallife_planner_pays", "amount", CompanyText.Money(c.Payout)), CompanyText.T("reallife_co_plan_claim_missed"));
                }
                else if (c.Stage == ClaimStage.Filed && c.PayDay == day + 1)
                {
                    Alert(5, c.Id, c.PayDay, CompanyText.T("reallife_co_plan_paid_title", "business", c.Name), CompanyText.T("reallife_co_plan_paid_what", "insurer", CompanyText.Insurer(c.Insurer)),
                        CompanyText.T("reallife_planner_pays", "amount", CompanyText.Money(c.Payout)), null);
                }
            }
        }
    }
}
