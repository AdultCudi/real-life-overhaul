using System;
using System.Collections.Generic;
using RealLifeOverhaul.Shops;
using RealLifeOverhaul.Uptown;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The mod's own kinds of shop (Shops/ShopsCatalogue) for Uptown's mix pass (Uptown/DistrictShops): each kind's type,
        /// the game's type it trades as and its primary goods, read from the catalogue's fixed table whatever the options say,
        /// so every machine of a session opens the same. Asked once a save behind the loading screen, never per frame.
        /// </summary>
        public static class UptownShopsLinks
        {
            private static readonly List<DistrictShopKind> Kinds = new List<DistrictShopKind>(4);

            internal static void Connect()
            {
                DistrictShops.Kinds = ShopKinds;
            }

            internal static void Disconnect()
            {
                DistrictShops.Kinds = null;
            }

            private static IReadOnlyList<DistrictShopKind> ShopKinds()
            {
                try
                {
                    if (Kinds.Count == 0)
                    {
                        for (int i = 0; i < ShopsCatalogue.Kinds.Length; i++)
                        {
                            ShopKind k = ShopsCatalogue.Kinds[i];
                            var primary = new List<string>(2);
                            for (int g = 0; k.Goods != null && g < k.Goods.Length; g++)
                            {
                                if (k.Goods[g].Impact >= ShopsCatalogue.Primary)
                                {
                                    primary.Add(k.Goods[g].Item);
                                }
                            }
                            Kinds.Add(new DistrictShopKind { Type = k.Type, Donor = k.Donor, Primary = primary.ToArray() });
                        }
                    }
                    return Kinds;
                }
                catch (Exception e)
                {
                    Fault("UptownShopsLinks.ShopKinds", e);
                    return null;
                }
            }
        }
    }
}
