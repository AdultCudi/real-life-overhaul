using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The company's staff as a rival tycoon sees them (Scripts/Rivals, through Wiring/RivalLinks): a tycoon's holding makes
    /// the company's own offer (StaffRules.Offer) to the player's most skilled employee who has nothing waiting, for more
    /// over the going rate than the company's own rivals offer. It is answered as every offer is, on the Company app's Staff
    /// tab: matched, they are paid it; not, they work their notice and leave by the game's own resignation. On the rivals'
    /// day or a test; never per frame.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        /// <summary>
        /// A rival's offer, of <paramref name="premium"/> thousandths over the going rate (never less than the company's own
        /// rivals'), to the most skilled of the player's staff with no offer, request or notice waiting. False with nobody to
        /// offer it to; otherwise who and where.
        /// </summary>
        public bool RivalOffer(string rival, int premium, out string name, out string business)
        {
            name = business = null;
            if (!On(CompanyPart.Staff) || _state == null || Save == null)
            {
                return false;
            }
            StaffFile best = null;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (ShopIndex(f.Place) >= 0 && f.Going > 0 && f.PoachDay == 0 && f.LeaveDay == 0 && f.AskDay == 0 && (best == null || f.Skill > best.Skill))
                {
                    best = f;
                }
            }
            if (best == null)
            {
                return false;
            }
            StaffRules.Offer(best, CompanyStore.Clean(rival), Save.Day);
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
