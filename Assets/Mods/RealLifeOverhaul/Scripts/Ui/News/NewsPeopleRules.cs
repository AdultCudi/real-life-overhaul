using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The words and the money of the game's language, as the people's page needs them. The game gives it; a check gives plain ones.</summary>
    public interface INewsWords
    {
        string T(string key);

        /// <summary>A text with one {name} in it filled.</summary>
        string T(string key, string name, string value);

        /// <summary>A sum of whole money in the game's way.</summary>
        string Money(int whole);

        /// <summary>A district's name in the game's language, from its place in the population's table.</summary>
        string District(int place);
    }

    /// <summary>
    /// The people's page and a district's page made from what the records count (PeopleTally): each figure once, a figure that needs a closed month saying
    /// from which day, never a nought. Game-free (EconomyCheck builds it from a generated city). Cost: a pass over the table's rows when a page is made.
    /// </summary>
    public static class NewsPeopleRules
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Num(int n) => n.ToString("N0", CultureInfo.CurrentCulture);

        /// <summary>A share in thousandths as a per cent to a tenth; empty where there is none to give (-1).</summary>
        public static string Permille(int permille) => permille < 0 ? string.Empty : (permille / 10f).ToString("0.0", Inv) + "%";

        public static string[] Percents(float[] shares)
        {
            var words = new string[shares.Length];
            for (int i = 0; i < shares.Length; i++)
            {
                words[i] = Math.Round(shares[i] * 100f).ToString("0", Inv) + "%";
            }
            return words;
        }

        // The last month's moves: no value until a month has closed since the city began, and then the words say from which day.
        private static UiInfoRow Moves(INewsWords w, string labelKey, bool closed, int value, int day)
        {
            return closed
                ? new UiInfoRow { Label = w.T(labelKey), Value = Num(value) }
                : new UiInfoRow { Label = w.T(labelKey), Value = w.T("reallife_news_not_yet", "day", day.ToString(Inv)), Ink = UiRole.Muted };
        }

        /// <summary>
        /// The city's people: the headline figures, the ages and the incomes, and the districts ranked by households (their places in
        /// <paramref name="places"/>: a row's number to a district).
        /// </summary>
        public static NewsPeople City(PeopleTally t, INewsWords w, out int[] places)
        {
            var ages = new float[PeopleTally.Ages];
            var incomes = new float[PeopleTally.Incomes];
            NewsRules.Shares(t.AgeCounts, ages);
            NewsRules.Shares(t.IncomeCounts, incomes);
            var p = new NewsPeople
            {
                Title = w.T("reallife_news_people_title"),
                HeadLabel = w.T("reallife_news_head_people"),
                HeadValue = Num(t.People),
                Rows = new[]
                {
                    new UiInfoRow { Label = w.T("reallife_news_head_homes"), Value = Num(t.Households) },
                    new UiInfoRow { Label = w.T("reallife_pop_fig_unemployment"), Value = Permille(t.OutOfWorkPermille) },
                    new UiInfoRow { Label = w.T("reallife_pop_fig_rent"), Value = w.Money(t.MeanRent) },
                    new UiInfoRow { Label = w.T("reallife_pop_fig_jobs"), Value = Num(t.Jobs) },
                    Moves(w, "reallife_pop_fig_moved_in", t.MonthClosed, t.MovedIn, t.MonthClosesOnDay),
                    Moves(w, "reallife_pop_fig_moved_out", t.MonthClosed, t.MovedOut, t.MonthClosesOnDay)
                },
                TableTitle = w.T("reallife_news_where")
            };
            Donuts(p, w, ages, incomes);
            p.Table = Districts(t, w, out places);
            return p;
        }

        /// <summary>One district's page: its figures, its ages and incomes. The table under them is the caller's (who pays most rent there).</summary>
        public static NewsPeople District(PeopleTally t, int d, INewsWords w)
        {
            var ages = new float[PeopleTally.Ages];
            var incomes = new float[PeopleTally.Incomes];
            NewsRules.Shares(t.DistrictAgeCounts(d), ages);
            NewsRules.Shares(t.DistrictIncomeCounts(d), incomes);
            int rent = t.DistrictMeanRent(d);
            var p = new NewsPeople
            {
                Title = w.District(d),
                HeadLabel = w.T("reallife_news_head_people"),
                HeadValue = Num(t.DistrictPeople[d]),
                Rows = new[]
                {
                    new UiInfoRow { Label = w.T("reallife_news_head_homes"), Value = Num(t.DistrictHouseholds[d]) },
                    new UiInfoRow { Label = w.T("reallife_pop_fig_unemployment"), Value = Permille(t.DistrictOutOfWorkPermille(d)) },
                    new UiInfoRow { Label = w.T("reallife_pop_fig_rent"), Value = rent > 0 ? w.Money(rent) : string.Empty },
                    new UiInfoRow { Label = w.T("reallife_pop_fig_jobs"), Value = Num(t.DistrictJobs[d]) }
                },
                TableTitle = w.T("reallife_news_rent_link")
            };
            Donuts(p, w, ages, incomes);
            return p;
        }

        /// <summary>The links under the people's figures: who pays the most rent, and the busiest district by its name (none while nobody lives anywhere).</summary>
        public static UiActionModel[] Links(PeopleTally t, INewsWords w)
        {
            int busy = t.Busiest();
            var rent = new UiActionModel { Label = w.T("reallife_news_rent_link"), Icon = UiIcon.Home, Command = NewsCommands.Rent, Arg = 0 };
            return busy < 0
                ? new[] { rent }
                : new[] { rent, new UiActionModel { Label = w.T("reallife_news_busy_link") + ": " + w.District(busy), Icon = UiIcon.MapPin, Command = NewsCommands.Busy, Arg = 0 } };
        }

        private static void Donuts(NewsPeople p, INewsWords w, float[] ages, float[] incomes)
        {
            p.ChartTitle = w.T("reallife_news_ages");
            p.ChartLabels = new[] { w.T("reallife_news_age_young"), w.T("reallife_news_age_work"), w.T("reallife_news_age_older") };
            p.ChartValues = Percents(ages);
            p.ChartShares = ages;
            p.Chart2Title = w.T("reallife_news_income_title");
            p.Chart2Labels = new[] { w.T("reallife_news_income_work"), w.T("reallife_news_income_middle"), w.T("reallife_news_income_upper") };
            p.Chart2Values = Percents(incomes);
            p.Chart2Shares = incomes;
        }

        // The districts where people live, the most households first: its name, its residents, its households and what a household pays a day.
        private static UiDataTableModel Districts(PeopleTally t, INewsWords w, out int[] places)
        {
            var order = new List<int>(t.DistrictHouseholds.Length);
            for (int d = 0; d < t.DistrictHouseholds.Length; d++)
            {
                if (t.DistrictHouseholds[d] > 0)
                {
                    order.Add(d);
                }
            }
            order.Sort((x, y) => t.DistrictHouseholds[y] != t.DistrictHouseholds[x] ? t.DistrictHouseholds[y].CompareTo(t.DistrictHouseholds[x]) : x.CompareTo(y));
            places = order.ToArray();
            var rows = new UiDataRow[order.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                int d = order[i];
                int rent = t.DistrictMeanRent(d);
                rows[i] = new UiDataRow
                {
                    Cells = new[] { w.District(d), Num(t.DistrictPeople[d]), Num(t.DistrictHouseholds[d]), rent > 0 ? w.Money(rent) : string.Empty },
                    Values = new[] { (int)UiIcon.MapPin, 0, 0, 0 },
                    Tones = new[] { UiTokens.Chart(d), UiRole.None, UiRole.None, UiRole.None }
                };
            }
            return new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = w.T("reallife_news_col_district"), Kind = UiCellKind.Tile },
                    new UiDataColumn { Title = w.T("reallife_news_head_people"), Kind = UiCellKind.Text, Width = 120f },
                    new UiDataColumn { Title = w.T("reallife_news_head_homes"), Kind = UiCellKind.Text, Width = 120f },
                    new UiDataColumn { Title = w.T("reallife_pop_fig_rent"), Kind = UiCellKind.Text, Width = 120f }
                },
                Rows = rows,
                RowCount = rows.Length,
                EmptyTitle = w.T("reallife_news_empty_city"),
                EmptyBody = w.T("reallife_news_empty_city_body"),
                EmptyIcon = UiIcon.MapTrifold
            };
        }
    }
}
