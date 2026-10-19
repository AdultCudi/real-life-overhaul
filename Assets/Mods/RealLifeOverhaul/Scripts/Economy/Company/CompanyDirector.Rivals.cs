using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The rival tycoons' offers to the player's staff (Scripts/Rivals, through Wiring/RivalLinks), answered as any offer.</summary>
    public sealed partial class CompanyDirector
    {
        /// <summary>
        /// An offer of <paramref name="premium"/> thousandths over the going rate, never less than the company's own rivals
        /// give, to the most skilled employee with nothing waiting, keeping the same waits and strictness as they do
        /// (StaffRules.RivalMay). False with nobody to offer it to; otherwise who and where.
        /// </summary>
        public bool RivalOffer(string rival, int premium, out string name, out string business)
        {
            name = business = null;
            if (!On(CompanyPart.Staff) || _state == null || Save == null || Save.Day < _state.PoachRest)
            {
                return false;   // (a week between any two offers, the company's own rivals' and these alike)
            }
            StaffFile best = null;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (ShopIndex(f.Place) < 0)
                {
                    continue;
                }
                CompanyGame.Offers(f);   // the game's own offer and the tech sector's, as they stand now: one rival's offer at a time
                if (StaffRules.RivalMay(f, _level, Save.Day) && (best == null || f.Skill > best.Skill))
                {
                    best = f;
                }
            }
            if (best == null)
            {
                return false;
            }
            StaffRules.Offer(best, CompanyStore.Clean(rival), Save.Day);
            _state.PoachRest = Save.Day + CompanyCatalogue.PoachCompanyRestDays;
            best.PoachOffer = Math.Max(best.PoachOffer, CompanyCatalogue.Share(best.Going, 1000 + Math.Max(CompanyCatalogue.PoachPremium, premium)));
            name = best.Name;
            business = ShopName(best.Place);
            Tell(NotificationType.Warning, "reallife_co_note_offer", new Dictionary<string, string>
            {
                { "name", best.Name }, { "rival", best.Rival }, { "wage", CompanyText.Wage(best.PoachOffer) }, { "days", CompanyCatalogue.PoachAnswerDays.ToString() }
            }, TabStaff, "emp:" + best.Id);
            Logger?.Info("Company: " + best.Rival + " (a rival tycoon's holding) offers " + best.Name + " at " + business + " " + best.PoachOffer + " an hour (hundredths), skill " + best.Skill
                         + ", paid " + best.Paid + " against a going rate of " + best.Going + "; to be answered within " + CompanyCatalogue.PoachAnswerDays + " day(s).");
            Changed();
            return true;
        }
    }
}
