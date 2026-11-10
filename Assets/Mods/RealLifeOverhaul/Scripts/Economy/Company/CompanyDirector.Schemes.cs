namespace RealLifeOverhaul.Economy
{
    // The doors a scheme uses on the company's insurance (Scripts/Schemes, through the wiring): a loss that never happened
    // put in as a claim by the insurer's own rules, whether a claim is still to be paid, and a claim found out.
    public sealed partial class CompanyDirector
    {
        /// <summary>
        /// A loss that never happened is put in as a claim on a cover in force: counted in the policy's term and paid on
        /// the insurer's day, as any claim. False, with nothing done, where no policy would pay.
        /// </summary>
        public bool InventClaim(string place, int kind, int loss, out int id, out int payout, out int payDay)
        {
            id = payout = payDay = 0;
            CompanyShop shop = On(CompanyPart.Cover) && Save != null ? ShopAt(place) : null;
            if (shop == null || kind < 0 || kind >= CompanyCatalogue.CoverKinds || loss <= 0)
            {
                return false;
            }
            Policy p = shop.File.Cover[kind];
            Claim claim = CoverRules.Open(_state, p, shop.File.Place, shop.Name, kind, loss, Save.Day);
            if (claim == null)
            {
                return false;
            }
            CoverRules.File(claim, p, Save.Day);
            id = claim.Id;
            payout = claim.Payout;
            payDay = claim.PayDay;
            Changed();
            return true;
        }

        /// <summary>True while a claim put in is still to be paid.</summary>
        public bool ClaimUnpaid(int id)
        {
            Claim c = _state != null ? _state.ClaimOf(id) : null;
            return c != null && (c.Stage == ClaimStage.Open || c.Stage == ClaimStage.Filed);
        }

        /// <summary>
        /// A claim was found to be for a loss that never happened: one not yet paid is refused, and its insurer ends that
        /// cover there and will not write it again. True if it was refused before it paid.
        /// </summary>
        public bool ClaimFoundOut(int id)
        {
            Claim c = _state != null ? _state.ClaimOf(id) : null;
            if (c == null)
            {
                return false;
            }
            bool unpaid = c.Stage == ClaimStage.Open || c.Stage == ClaimStage.Filed;
            if (unpaid)
            {
                c.Stage = ClaimStage.Lapsed;
            }
            ShopFile file = _state.Shop(c.Place);
            Policy p = file != null && c.Kind >= 0 && c.Kind < file.Cover.Length ? file.Cover[c.Kind] : null;
            if (p != null && c.Insurer >= 0 && c.Insurer < CompanyCatalogue.Insurers)
            {
                p.Declined |= 1 << c.Insurer;
                if (p.Active && p.Insurer == c.Insurer)
                {
                    CoverRules.End(p);
                }
            }
            Logger?.Info("Company: claim " + c.Id + " at " + c.Name + " was found to be for a loss that never happened: " + (unpaid ? "refused before it paid" : "paid already")
                         + "; insurer " + c.Insurer + " ends the cover there and will not write it again.");
            Changed();
            return unpaid;
        }
    }
}
