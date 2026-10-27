using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The city's economy in the Finance app: three lines on the Overview, with the Economy app's verdict on the economy, and a button
    /// that opens the Economy app, where the city's economy is drawn in full (docs/features/economy-app.md). The page "The city"
    /// this file used to write has moved there.
    /// </summary>
    public sealed partial class FinanceApp
    {
        /// <summary>A notice about the city asked for the city's page: the Overview shows the next time the app opens (the Economy app is not on the phone).</summary>
        public void WantCity()
        {
            Want(Finance.Overview);
        }

        private static CityState CityState()
        {
            CityEconomyDirector director = CityEconomyDirector.Instance;
            return director != null ? director.State : null;
        }

        // The Overview's lines; false, with nothing written, while the city's economy is not running.
        private static bool CityLines(InfoBox box)
        {
            CityState state = CityState();
            if (state == null)
            {
                return false;
            }
            string verdict = EconomyBridges.EconomyVerdict != null ? EconomyBridges.EconomyVerdict() : null;
            box.Row("reallife_macro_over_rate", CityText.Rate(state.Base));
            box.Row("reallife_macro_over_prices", CityText.Prices(state));
            box.Row("reallife_macro_over_economy", string.IsNullOrEmpty(verdict)
                ? CityText.Economy(state)
                : FinanceText.T("reallife_econapp_finance_economy", "verdict", verdict, "state", CityText.State(state.Cycle), "month", state.MonthsIn.ToString()));
            return true;
        }

        // The button under the lines: the Economy app, if it is on the phone.
        private static bool EconomyAppOn()
        {
            return EconomyBridges.EconomyAppOn != null && EconomyBridges.EconomyAppOn();
        }

        private void OpenEconomyApp()
        {
            if (EconomyBridges.OpenEconomyApp != null)
            {
                EconomyBridges.OpenEconomyApp(0);
            }
        }
    }
}
