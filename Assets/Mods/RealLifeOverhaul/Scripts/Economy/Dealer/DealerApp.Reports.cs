using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Reports tab: on the right, the chart of a figure's days for one showroom or all of them
    /// (<see cref="DealerReportPage"/>); in the white box, the sums of the last week and of every day kept: cars sold and
    /// their prices, the margin and the extras', the money spent on events and the workshop, visitors and the share who
    /// bought, the lot as it stands and what is owed on finance that is behind. Worked out when the tab is written.
    /// </summary>
    public sealed partial class DealerApp
    {
        private DealerReportPage _report;
        private readonly DealerBook _reportWeek = new DealerBook(), _reportAll = new DealerBook(), _reportNow = new DealerBook(), _reportDay = new DealerBook();

        private TableSpec WriteReports(InfoBox box, DealerState state, int now)
        {
            int today = now / 24;
            if (_report == null)
            {
                box.Title(DealerText.T("reallife_dealer_reports_title"));
                box.Line(DealerText.T("reallife_dealer_reports_off"));
                return DealerColumns.Reports;
            }
            _report.Show(state, today);
            string place = _report.Place;
            DealerYard yard = place != null ? state.Yard(place) : null;
            int week = DealerReport.Sum(state, place, today - DealerCatalogue.WeekBooks + 1, today, today, _reportWeek, _reportDay);
            int kept = DealerReport.Sum(state, place, today - DealerCatalogue.ReportDays + 1, today, today, _reportAll, _reportDay);
            DealerReport.Day(state, place, today, today, _reportNow);
            box.Title(yard != null ? yard.Name : DealerText.T("reallife_dealer_reports_title"));
            Sums(box, "reallife_dealer_reports_week", _reportWeek, week);
            Sums(box, "reallife_dealer_reports_kept", _reportAll, kept);
            box.Line(DealerText.Fill("reallife_dealer_reports_now", "cars", _reportNow.Cars.ToString(), "age", DealerReport.Value(_reportNow, ReportKind.StockAge).ToString(),
                "arrears", DealerText.Money(_reportNow.Arrears), "late", _reportNow.Late.ToString()));
            box.Line(DealerText.T("reallife_dealer_reports_about"));
            Meta.Guide.Point(box, GuideTitle);
            _picked = null;
            return DealerColumns.Reports;
        }

        private static void Sums(InfoBox box, string key, DealerBook b, int days)
        {
            long conversion = DealerReport.Value(b, ReportKind.Conversion);
            box.Line(DealerText.Fill(key, "days", days.ToString(), "sold", b.Sold.ToString(), "revenue", DealerText.Money(b.Revenue), "margin", DealerText.Money(b.Margin),
                "extras", DealerText.Money(b.Extras), "spent", DealerText.Money(b.Spent), "visitors", b.Visitors.ToString(),
                "conversion", (conversion / 10.0).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + "%"));
        }
    }
}
