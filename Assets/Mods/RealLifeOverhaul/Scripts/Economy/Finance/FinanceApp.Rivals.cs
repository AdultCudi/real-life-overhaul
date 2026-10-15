using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    // The rival tycoons and the city's rich list in the Finance app (Scripts/Rivals, through EconomyBridges.Rivals): a page of
    // its own that the Overview opens with a button, in the same frame, as the Standing page is. Everything on it is written
    // by the page's own writer; this file only hosts it: which page shows, what is picked on it, and the way back.
    public sealed partial class FinanceApp
    {
        private bool _rivals;          // the Overview shows the rivals' page
        private bool _wantRivals;      // it is to show the next time the app opens
        private string _rivalsPick;    // the row picked on it

        private static StandingPage RivalsOn()
        {
            StandingPage page = EconomyBridges.Rivals;
            return page != null && page.Spec != null && page.Write != null && page.On != null && page.On() ? page : null;
        }

        /// <summary>The rivals' page is what shows the next time the app opens (a notification of the rivals' was clicked).</summary>
        public void WantRivals()
        {
            _wantRivals = true;
            Want(Finance.Overview);
        }

        // As the app opens: the rivals' page if it was asked for, over the city's and the Standing page.
        private void OpenedRivals()
        {
            _rivals = _wantRivals && _tab == Finance.Overview && RivalsOn() != null;
            _wantRivals = false;
            if (_rivals)
            {
                _standing = false;
                _city = false;
                _rivalsPick = null;
            }
        }

        private void ShowRivals()
        {
            _rivals = true;
            _standing = false;
            _city = false;
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

        private void RivalsClicked(TableRow row)
        {
            if (row != null && row.Pick)
            {
                _rivalsPick = row.Id;
                Refresh();
            }
            else
            {
                _table.ClearSelection();
            }
        }

        private void WriteRivals(InfoBox box, StandingPage page)
        {
            _spec = page.Spec;
            page.Write(box, Row, _rivalsPick, PickRivals, HideRivals);
        }
    }
}
