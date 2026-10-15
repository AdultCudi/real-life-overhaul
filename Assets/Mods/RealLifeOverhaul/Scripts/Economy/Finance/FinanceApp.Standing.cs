using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    // The player's standing in the Finance app (Scripts/Fame, through EconomyBridges.Standing): a page of its own that the
    // Overview opens with a button, in the same frame, as the city's page is. Everything on it is written by the page's
    // own writer; this file only hosts it: which page shows, what is picked on it, and the way back.
    public sealed partial class FinanceApp
    {
        private bool _standing;        // the Overview shows the Standing page
        private bool _wantStanding;    // it is to show the next time the app opens
        private string _standingPick;  // the row picked on it

        private static StandingPage StandingOn()
        {
            StandingPage page = EconomyBridges.Standing;
            return page != null && page.Spec != null && page.Write != null && page.On != null && page.On() ? page : null;
        }

        /// <summary>The Standing page is what shows the next time the app opens (a notification of Standing's was clicked).</summary>
        public void WantStanding()
        {
            _wantStanding = true;
            Want(Finance.Overview);
        }

        private void ShowStanding()
        {
            _standing = true;
            _city = false;
            _rivals = false;
            _standingPick = null;
            Refresh();
        }

        private void HideStanding()
        {
            _standing = false;
            _standingPick = null;
            _table.ClearSelection();
            Refresh();
        }

        private void PickStanding(string id)
        {
            _standingPick = id;
            if (id == null)
            {
                _table.ClearSelection();
            }
            Refresh();
        }

        private void WriteStanding(InfoBox box, StandingPage page)
        {
            _spec = page.Spec;
            page.Write(box, Row, _standingPick, PickStanding, HideStanding);
        }
    }
}
