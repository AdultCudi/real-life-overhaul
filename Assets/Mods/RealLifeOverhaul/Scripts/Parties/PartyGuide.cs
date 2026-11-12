using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// Parties' and company events' entry in the mod's feature list: their option, their save line, their tests and the Guide
    /// page's figures, each {name} filled from the catalogue when the page opens. Called once when the list is built.
    /// </summary>
    public static class PartyGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("parties", "reallife_meta_title_parties")
                .About("reallife_guide_parties_about").Help("reallife_guide_parties")
                .Owns(Settings.LivingPartiesId)
                .Data(PartyStore.Key)
                .Test("reallife_party_test_invite", "reallife_party_test_now", "reallife_party_test_round")
                .Cleanup(() => PartyDirector.Instance?.Release(), 0)
                .ResetData(() => PartyDirector.Instance?.Forget())
                .Fact("party_lead", () => Whole(PartyCatalogue.LeadHours))
                .Fact("party_ahead", () => Whole(PartyCatalogue.AheadDays))
                .Fact("party_early", () => Whole(PartyCatalogue.EarlyMinutes))
                .Fact("party_late", () => Whole(PartyCatalogue.LateMinutes))
                .Fact("party_missed", () => Whole(PartyCatalogue.MissedMinutes))
                .Fact("party_window", () => Whole(PartyCatalogue.WindowDays))
                .Fact("party_ceiling", () => Whole((int)PartyCatalogue.Ceiling))
                .Fact("party_refund_early", () => Whole(PartyCatalogue.RefundEarly) + "%")
                .Fact("party_refund_late", () => Whole(PartyCatalogue.RefundLate) + "%")
                .Fact("party_invite_gap", () => Whole(PartyCatalogue.InviteGapDays))
                .Fact("party_staff_window", () => Whole(PartyCatalogue.StaffWindowDays))
                .Fact("party_staff_mood", () => Whole((int)PartyCatalogue.CapMood))
                .Fact("party_staff_standing", () => Whole(PartyCatalogue.CapStanding))
                .Fact("party_staff_gap", () => Whole(PartyCatalogue.EventGapDays))
                .Fact("party_underpaid", () => Whole(PartyCatalogue.UnderpaidShare) + "%");
        }

        private static string Whole(int value) => value.ToString(Inv);
    }
}
