using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The company's part that hears a business is gone, kept beside selling a business, which calls it through
    /// Wiring/ServicesLinks. The file closes at once rather than the next morning, so nothing of the company touches a
    /// building that is now somebody else's; what a campaign added to its promotion went with the business. Cost: one
    /// reading of the player's businesses and staff, per sale.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        /// <summary>A business of the player's has been sold or given up, by its place. Does nothing if the company does not know it.</summary>
        public void BusinessGone(string place)
        {
            if (!_running || _broken || Save == null || string.IsNullOrEmpty(place))
            {
                return;
            }
            try
            {
                CompanyShop shop = ShopAt(place);
                if (shop == null)
                {
                    return;
                }
                string name = shop.Name;
                shop.Applied = false;
                shop.Asked = 0;
                int added, dropped, gone;
                Read(Save.Day, false, false, out added, out dropped, out gone);
                RotaRead();
                Changed();
                Logger?.Info("Company: '" + name + "' is no longer the player's. Its file is closed (insurance, licences, campaign, strike and overtime go with it); "
                             + dropped + " business file(s) and " + gone + " staff file(s) dropped, " + _shops.Count + " business(es) left.");
            }
            catch (Exception e)
            {
                Fault("a business that is gone", e);
            }
        }
    }
}
