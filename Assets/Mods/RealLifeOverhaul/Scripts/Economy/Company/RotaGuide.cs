using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The figures of the Guide page "Staff rota planner", from the catalogues, as the page opens.</summary>
    public static class RotaGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void AddFacts(Feature f)
        {
            f.Fact("rota_shift", () => Whole(RotaCatalogue.ShiftCap))
                .Fact("rota_day", () => Whole(RotaCatalogue.DayCap))
                .Fact("rota_blocks", () => Whole(RotaCatalogue.BlocksMost))
                .Fact("rota_block_hours", () => Whole(RotaCatalogue.BlockLeast))
                .Fact("rota_week", () => Whole(RotaCatalogue.FullWeek))
                .Fact("rota_books_days", () => Whole(RotaCatalogue.BooksDays))
                .Fact("rota_books_customers", () => Whole(RotaCatalogue.BooksCustomers))
                .Fact("rota_late_hour", () => CompanyCatalogue.LateHour.ToString("00", Inv))
                .Fact("rota_early_hour", () => CompanyCatalogue.EarlyHour.ToString("00", Inv))
                .Fact("rota_stations_most", () => Whole(RotaCatalogue.StationsMost))
                .Fact("rota_staff_most", () => Whole(RotaCatalogue.WorkersMost))
                .Fact("rota_hires_most", () => Whole(RotaLadder.HiresMost))
                .Fact("rota_share_least", () => Whole(RotaLadder.ShareLeast))
                .Fact("rota_gain_least", () => CompanyText.Money(RotaLadder.GainLeast / 100f))
                .Fact("rota_replace_days", () => Whole(RotaLadder.ReplaceDays));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
