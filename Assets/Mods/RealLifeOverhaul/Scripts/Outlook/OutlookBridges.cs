using System;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// What the Economy app asks of the rest of the mod: the jobs the cycle moves, the stock market, the council's books and the
    /// districts the mod keeps more of. Filled in by Wiring/OutlookLinks; one left unset gives no figure, and the page says the
    /// figure is not kept. Each is asked when the app is written, once a game hour at most, and only ever read.
    /// </summary>
    public static class OutlookBridges
    {
        /// <summary>
        /// The jobs a state of the economy gives: unemployment among professionals (per mille), vacancies open at once, jobseekers at
        /// once, and the chance a job is lost (per mille a day).
        /// </summary>
        public delegate bool JobsReader(int cycle, out int unemployment, out int vacancies, out int seekers, out int layoffs);

        /// <summary>Fills the market's part of the readings (<see cref="OutlookReadings.MarketOn"/> and what follows); false while the market is off.</summary>
        public delegate bool MarketReader(OutlookReadings into);

        /// <summary>
        /// The index's closes for the last <c>count</c> game days up to <c>endDay</c>, oldest first, in hundredths, with the no-value
        /// mark for a day the market was shut. Returns how many days had one.
        /// </summary>
        public delegate int StocksHistory(int endDay, int count, int[] into);

        /// <summary>A sector's last <c>count</c> closes, oldest first, hundredths. Returns how many.</summary>
        public delegate int SectorCloses(int sector, int count, int[] into);

        /// <summary>Fills the council's books into the readings (<see cref="OutlookReadings.CouncilOn"/> and what follows); false while there are none.</summary>
        public delegate bool CouncilReader(OutlookReadings into);

        /// <summary>
        /// A district the mod keeps more of: its demand for homes, shops, offices and warehouses (0 to 100, -1 where not counted),
        /// its market level (thousandths) and the buildings going up there. False for a district of the game's own.
        /// </summary>
        public delegate bool DistrictReader(string district, int[] demand, out int level, out int growing);

        public static JobsReader Jobs;
        public static MarketReader Market;
        public static StocksHistory Stocks;
        public static SectorCloses Sectors;
        public static CouncilReader Council;
        public static DistrictReader District;

        /// <summary>A sector's name as the market writes it.</summary>
        public static Func<int, string> SectorName;

        /// <summary>Opens the Finance app on its Overview (for a button that leads back to it).</summary>
        public static Action OpenFinance;

        public static void Reset()
        {
            Jobs = null;
            Market = null;
            Stocks = null;
            Sectors = null;
            Council = null;
            District = null;
            SectorName = null;
            OpenFinance = null;
        }
    }
}
