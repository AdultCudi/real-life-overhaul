using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// A promotion shown in every district it bought. One district is the ad's own field, so an older save is unchanged.
    /// Game-free. Cost: one delivery a district, on the day's ad pass.
    /// </summary>
    public static partial class FlareCityRules
    {
        public static HeardReport DeliverMasked(FlareCityState c, PopulationState s, FlareAd ad, int b, int kind, int account, int day, int[] accountBiz,
            HearScratch scratch, FlareFunnel funnel)
        {
            int mask = ad == null ? 0 : FlareUseRules.AdMaskOf(c.AdMasks, ad.Id);
            if (mask == 0 || FlareUseRules.MaskCount(mask) <= 1)
            {
                return Deliver(c, s, ad, b, kind, account, day, accountBiz, scratch, funnel);
            }
            int saved = ad.District;
            var sum = new HeardReport();
            for (int d = 0; d < 31; d++)
            {
                if (!FlareUseRules.InMask(mask, d))
                {
                    continue;
                }
                ad.District = d;
                HeardReport part = Deliver(c, s, ad, b, kind, account, day, accountBiz, scratch, funnel);
                sum.Seen += part.Seen;
                sum.Interested += part.Interested;
                sum.Shared += part.Shared;
                sum.Discovered += part.Discovered;
                sum.Followed += part.Followed;
                sum.Lost += part.Lost;
                sum.Investors += part.Investors;
                sum.Impressions += part.Impressions;
            }
            ad.District = saved;
            return sum;
        }
    }
}
