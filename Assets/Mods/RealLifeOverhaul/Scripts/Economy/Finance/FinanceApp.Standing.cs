using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Hosts the Standing page, which Scripts/Fame writes (EconomyBridges.Standing).</summary>
    public sealed partial class FinanceApp
    {
        private bool _standing;         // the Overview shows the Standing page
        private bool _wantStanding;     // it is to show the next time the app opens
        private string _standingPick;   // the row picked on it

        private static StandingPage StandingOn()
        {
            return Usable(EconomyBridges.Standing);
        }

        // A page another feature writes, if it is there and switched on; null otherwise.
        private static StandingPage Usable(StandingPage page)
        {
            return page != null && page.Spec != null && page.Write != null && page.On != null && page.On() ? page : null;
        }

        /// <summary>The Standing page shows the next time the app opens (its notification was clicked).</summary>
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
