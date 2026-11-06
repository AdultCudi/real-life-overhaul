using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Networking
{
    /// <summary>
    /// The networking life's entry in the feature list, its keys written out whole as the options checks read them. Its
    /// option and test buttons stand in the social life's group (Core/RealLifeOverhaulMod); its Guide page's numbers are
    /// facts filled from <see cref="NetworkRules"/>.
    /// </summary>
    public static class NetworkGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("networking", "reallife_meta_title_networking")
                .Group("reallife_social_header")
                .About("reallife_guide_networking_about").Help("reallife_guide_networking")
                .Owns(Settings.SocialNetworkingId)
                .Data(NetworkStore.Key)
                .Fact("net_street", () => Percent(NetworkRules.ChanceStreet))
                .Fact("net_friend", () => Percent(NetworkRules.ChanceFriend))
                .Fact("net_family", () => Percent(NetworkRules.ChanceFamily))
                .Fact("net_work", () => Percent(NetworkRules.ChanceWork))
                .Fact("net_evening", () => Percent(NetworkRules.ChanceEvening))
                .Fact("net_lead_days", () => Careers.ProRules.OpeningDaysMost.ToString(Inv))
                .Fact("net_gap", () => NetworkRules.GapDays.ToString(Inv))
                .Fact("net_known", () => NetworkRules.KnownDays.ToString(Inv))
                .Fact("net_tip", () => NetworkGame.TipPercent.ToString(Inv) + "%")
                .Test("reallife_social_net_test_intro", "reallife_social_net_test_favour", "reallife_social_net_test_each")
                .ResetData(NetworkDirector.Reset);
        }

        private static string Percent(int perMille)
        {
            return (perMille / 10).ToString(Inv) + "%";
        }
    }
}
