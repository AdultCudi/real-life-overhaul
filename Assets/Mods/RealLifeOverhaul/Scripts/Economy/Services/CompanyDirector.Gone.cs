using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// "This business is gone", for "running a business": the one thing selling a business needs of it and that it
    /// did not have. It lives here, in its own file beside the feature that calls it (through
    /// Wiring/ServicesLinks), so that nothing of the company's own files is changed.
    ///
    /// The company would find out by itself the next morning, when it reads the player's businesses again and drops
    /// the file of one that is no longer theirs (its insurance, licences, campaign, strike and overtime go with the
    /// file). Until then its pages would still list the business, and its hourly look would go on writing the
    /// footfall of a building that is now somebody else's. So the businesses and the staff are read again at once:
    /// the business is no longer among them, and nothing of the company's touches its building again. (What a
    /// campaign had added to the business's promotion figure is not taken back: the figure went with the business,
    /// and the game works it out afresh each night for a building that is the player's.)
    ///
    /// Cost: one reading of the player's businesses and staff, when a business is sold.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        /// <summary>A business of the player's has been sold or given up: by its place, as business pressure writes one. Does nothing if the company does not know it.</summary>
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
                Logger?.Info("Company: '" + name + "' is no longer the player's. Its file is closed (insurance, licences, campaign, strike and overtime go with it); " + dropped + " business file(s) and " + gone
                             + " staff file(s) dropped, " + _shops.Count + " business(es) left.");
            }
            catch (Exception e)
            {
                Fault("a business that is gone", e);
            }
        }
    }
}
