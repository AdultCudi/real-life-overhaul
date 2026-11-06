using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Hosts the rivals' page, which Scripts/Rivals writes (EconomyBridges.Rivals).</summary>
    public sealed partial class FinanceApp
    {
        private bool _rivals;         // the Overview shows the rivals' page
        private bool _wantRivals;     // it is to show the next time the app opens
        private string _rivalsPick;   // the row picked on it

        private static StandingPage RivalsOn()
        {
            return Usable(EconomyBridges.Rivals);
        }

        /// <summary>The rivals' page shows the next time the app opens (its notification was clicked).</summary>
        public void WantRivals()
        {
            _wantRivals = true;
            Want(Finance.Overview);
        }

        // The rivals' page if it was asked for, over the city's and the Standing page.
        private void OpenedRivals()
        {
            _rivals = _wantRivals && _tab == Finance.Overview && RivalsOn() != null;
            _wantRivals = false;
            if (_rivals)
            {
                _standing = false;
                _rivalsPick = null;
            }
        }

        private void ShowRivals()
        {
            _rivals = true;
            _standing = false;
            _rivalsPick = null;
            Refresh();
        }

        private void HideRivals()
        {
            _rivals = false;
            _rivalsPick = null;
            _table.ClearSelection();
            Refresh();
        }

        private void PickRivals(string id)
        {
            _rivalsPick = id;
            if (id == null)
            {
                _table.ClearSelection();
            }
            Refresh();
        }

        private void WriteRivals(InfoBox box, StandingPage page)
        {
            _spec = page.Spec;
            page.Write(box, Row, _rivalsPick, PickRivals, HideRivals);
        }
    }
}
