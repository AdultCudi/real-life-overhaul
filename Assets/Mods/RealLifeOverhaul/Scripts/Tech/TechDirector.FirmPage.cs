using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A firm's whole page: its presses, shared by the kit's page (StudioPageScreen) and the page drawn on the game's widgets (StudioPageApp).</summary>
    public sealed partial class TechDirector
    {
        private StudioPageApp _firmPage;

        /// <summary>Opens a firm's page on the game's own widgets, the way a firm of the Tech app is looked at as a whole; false while the page is not on the phone.</summary>
        internal bool OpenFirmPage(string place)
        {
            return _firmPage != null && _firmPage.Open(place);
        }

        /// <summary>
        /// What a button of a firm's page does: the Tech app's own screen for a new game, a new product or the contracts, else the studio's or the
        /// sector's own act. False where nothing was done (the firm is gone, or the director refused).
        /// </summary>
        internal bool PageAct(string place, int act, int value)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null)
            {
                return false;
            }
            if (StudioPages.Opens(act))
            {
                OpenTech(act == SectorAct.Contracts ? TabContracts : TabProducts, act == SectorAct.Contracts ? null : TechApp.NewProductRow + place);
                return true;
            }
            if (StudioRuns(firm.File.K))
            {
                StudioDo(act, place, 0, value);
                return true;
            }
            return SectorDo(act, place, value);
        }
    }
}
