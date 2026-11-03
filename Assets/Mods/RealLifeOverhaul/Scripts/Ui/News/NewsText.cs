using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The words and axes of the News pages: a title strip's note, an edition's number, the labels of a day axis. Game-free.</summary>
    public static class NewsText
    {
        public const int DaysMonth = 30;
        public const int YearDays = 60;

        /// <summary>The year from the save's own days in a year (60 unless the game says otherwise).</summary>
        public static int YearOf(int day, int perYear) => day < 1 ? 1 : (day - 1) / (perYear < 1 ? YearDays : perYear) + 1;

        /// <summary>A count with its thousands apart, as a title strip prints an edition's number: 1,102.</summary>
        public static string Thousands(int n, CultureInfo culture) => n.ToString("N0", culture);

        /// <summary>The words of a title strip's right note, each part after the first set off by a middle dot: the day, the year and number, the price.</summary>
        public static string StripLine(string[] parts)
        {
            var line = new StringBuilder(96);
            for (int i = 0; parts != null && i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }
                line.Append(line.Length > 0 ? " \u00b7 " : string.Empty).Append(parts[i]);
            }
            return line.ToString();
        }

        /// <summary>
        /// The labels of a day axis: the first point and every <paramref name="step"/> after it carry their text, the others none, so
        /// a month of days is read in fives and a book of days in twenties. A card still reads every point whole.
        /// </summary>
        public static string[] AxisPoints(string[] whole, int step)
        {
            int n = whole == null ? 0 : whole.Length;
            var points = new string[n];
            int every = step < 1 ? 1 : step;
            for (int i = 0; i < n; i++)
            {
                points[i] = i % every == 0 ? whole[i] : string.Empty;
            }
            return points;
        }

        /// <summary>The step of a day axis for a span: 5 for a month, 20 for the book, never crowding.</summary>
        public static int AxisStep(int span) => span <= DaysMonth ? 5 : 20;

        /// <summary>The lead of the prices page by the basket's move: dearer, cheaper or the same as a month ago.</summary>
        public static string BasketKey(int bp) => bp > 0 ? "reallife_news_basket" : bp < 0 ? "reallife_news_basket_less" : "reallife_news_basket_flat";

    }
}
