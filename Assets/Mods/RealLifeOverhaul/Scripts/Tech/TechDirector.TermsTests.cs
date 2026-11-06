using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The test tool of the options page for terms: the first funded firm's lead takes hard terms now, for real, so the pages and the board can be tried.</summary>
    public sealed partial class TechDirector
    {
        /// <summary>The lead of the first funded firm holds a two times participating preference, a full ratchet, a restarted vesting, a look on demand and a spending limit.</summary>
        public void TestTerms()
        {
            Cap c = TestFunded(out FirmPlace firm);
            if (c == null)
            {
                return;
            }
            Holder lead = LeadLine(c);
            if (lead == null)
            {
                Notifications.ShowError("reallife_vc_test_no_funded");
                return;
            }
            lead.T = new Terms
            {
                Pref = 2000, Participating = true, Anti = TermsCatalogue.AntiRatchet, Vest = 1, Info = TermsCatalogue.InfoDemand,
                Provision = VentureCatalogue.BurnByStage[TalentCatalogue.Clamp(c.Stage, 0, (int)RoundStage.Count - 1)] * 30
            };
            lead.Preference = lead.Invested * lead.T.Pref / 1000;
            RefreshProvision();
            ChangedTech();
        }
    }
}
