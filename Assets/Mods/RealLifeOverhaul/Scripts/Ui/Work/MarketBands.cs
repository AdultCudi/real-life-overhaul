using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The feet of the Marketplace's lists, made from arrays by the live source and the previews alike: what was sold lately (with the profit it made), the trade's
    /// record (flips, repairs by hand, things sold), the visits done lately with the stars the clients gave, and the standing they add up to. Game-free.
    /// </summary>
    public static class MarketBands
    {
        /// <summary>Things sold lately: a glyph, the thing, to whom and on what day, the price and the profit in brackets.</summary>
        public static WorkFormBand Sold(UtilitiesWords w, UiIcon[] icons, string[] things, string[] hints, string[] values)
        {
            return WorkBands.Tiles(w.T("reallife_work_band_sold_lately", "Sold lately"), icons, things, hints, values);
        }

        /// <summary>The trade's record: what the flips made, the number sold and the repair skill.</summary>
        public static WorkFormBand Trade(UtilitiesWords w, string profit, int repairSkill, int sold)
        {
            var band = new WorkFormBand { Title = w.T("reallife_work_band_trade", "Your trade") };
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_trade_profit", "Made from flips"), Value = profit });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_trade_sold", "Things sold"), Value = sold.ToString() });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_trade_repair", "Repair skill"), Value = Math.Max(0, Math.Min(100, repairSkill)) } });
            return band;
        }

        /// <summary>Visits done lately: the client, the pay and the stars they gave, drawn as people with stars.</summary>
        public static WorkFormBand Done(UtilitiesWords w, string[] clients, string[] hints, string[] pays, int[] halves)
        {
            var band = new WorkFormBand { Title = w.T("reallife_work_band_done_lately", "Done lately") };
            for (int i = 0; i < clients.Length && i < 3; i++)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = clients[i], Role = hints[i], Right = pays[i], Halves = halves[i], Ring = UiRole.Ok } });
            }
            band.Count = band.Rows.Count > 0 ? clients.Length.ToString() : null;
            return band;
        }

        /// <summary>The standing the clients give: the stars on average, the visits done and the ones begun late.</summary>
        public static WorkFormBand Standing(UtilitiesWords w, string stars, int done, int late)
        {
            var band = new WorkFormBand { Title = w.T("reallife_work_band_standing", "Your standing with clients") };
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_rating", "Rating"), Value = stars });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_visits_done", "Visits done"), Value = done.ToString() });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_visits_late", "Begun late"), Value = late.ToString() });
            return band;
        }

        /// <summary>
        /// What the whole market holds that a category filter hides, made from the listings' own rows: the best deals (the furthest under their market price) and the
        /// sellers with the most for sale. One maker for the live listings and the previews.
        /// </summary>
        public static void Market(UtilitiesWords w, System.Collections.Generic.List<WorkFormBand> into, System.Collections.Generic.List<WorkRow> rows)
        {
            if (rows.Count < 3)
            {
                return;
            }
            var deals = new System.Collections.Generic.List<WorkRow>(rows);
            deals.RemoveAll(r => r.Under == null);
            deals.Sort((a, b) => a.UnderValue != b.UnderValue ? a.UnderValue.CompareTo(b.UnderValue) : a.Id.CompareTo(b.Id));   // the furthest under first, the older listing between two alike
            var best = new WorkFormBand { Title = w.T("reallife_work_band_best_deals", "Best deals today") };
            for (int i = 0; i < deals.Count && i < 3; i++)
            {
                best.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Icon = deals[i].Icon, Label = deals[i].Name, Hint = deals[i].Seller, Value = deals[i].Price + " (" + deals[i].Under + ")" });
            }
            best.Count = best.Rows.Count > 0 ? deals.Count.ToString() : null;
            WorkBands.AddIfAny(into, best);
            var sellers = new System.Collections.Generic.List<string>(8);
            var counts = new System.Collections.Generic.List<int>(8);
            for (int i = 0; i < rows.Count; i++)
            {
                int at = string.IsNullOrEmpty(rows[i].Seller) ? -1 : sellers.IndexOf(rows[i].Seller);
                if (at < 0 && !string.IsNullOrEmpty(rows[i].Seller))
                {
                    sellers.Add(rows[i].Seller);
                    counts.Add(1);
                }
                else if (at >= 0)
                {
                    counts[at]++;
                }
            }
            var order = new int[sellers.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : a.CompareTo(b));
            var most = new WorkFormBand { Title = w.T("reallife_work_band_most_listed", "Selling the most today") };
            for (int i = 0; i < order.Length && i < 3; i++)
            {
                most.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = sellers[order[i]],
                    Role = w.F("reallife_work_n_listings", "{n} listings", "n", counts[order[i]].ToString()),
                    Ring = UiRole.Ok } });
            }
            WorkBands.AddIfAny(into, most);
        }

        /// <summary>The player's things on sale now: a glyph, the thing, what it is worth now and the asking price at the right.</summary>
        public static WorkFormBand OnSale(UtilitiesWords w, UiIcon[] icons, string[] things, string[] hints, string[] asks)
        {
            return WorkBands.Tiles(w.T("reallife_work_band_on_sale", "On sale now"), icons, things, hints, asks);
        }

        /// <summary>The things bought and not yet at home: when to collect them or that they are on their way, and what was paid.</summary>
        public static WorkFormBand OnTheWay(UtilitiesWords w, UiIcon[] icons, string[] things, string[] hints, string[] paid)
        {
            return WorkBands.Tiles(w.T("reallife_work_band_on_way", "On its way to you"), icons, things, hints, paid);
        }

        /// <summary>What the sales on record add up to: the average price and profit, the best price, and how many sold for more than they cost.</summary>
        public static WorkFormBand SalesNumbers(UtilitiesWords w, string average, string best, string profitEach, string aboveCost)
        {
            return WorkBands.Pairs(w.T("reallife_work_band_sales_numbers", "Your sales in numbers"),
                new[] { w.T("reallife_work_sales_average", "Average price"), w.T("reallife_work_sales_best", "Best price"), w.T("reallife_work_sales_profit_each", "Average profit a sale"),
                    w.T("reallife_work_sales_above_cost", "Sold for more than they cost") }, new[] { average, best, profitEach, aboveCost });
        }

        /// <summary>Visits by kind of job: a glyph, the kind, how many were done with the stars on average, and what they paid in all.</summary>
        public static WorkFormBand ByKind(UtilitiesWords w, UiIcon[] icons, string[] kinds, string[] hints, string[] pays)
        {
            return WorkBands.Tiles(w.T("reallife_work_band_by_kind", "By kind of job"), icons, kinds, hints, pays);
        }

        /// <summary>How the clients rated the visits: the count of each star, the best first.</summary>
        public static WorkFormBand Rated(UtilitiesWords w, int[] countOfStars)
        {
            var band = new WorkFormBand { Title = w.T("reallife_work_band_rated", "How clients rated you") };
            for (int stars = 5; stars >= 1; stars--)
            {
                int n = stars - 1 < countOfStars.Length ? countOfStars[stars - 1] : 0;
                string label = stars == 1 ? w.T("reallife_work_one_star", "1 star") : w.F("reallife_work_n_stars", "{n} stars", "n", stars.ToString());
                band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = label, Value = n.ToString() });
            }
            return band;
        }

        /// <summary>The helpers sent: how many visits had one, what they took and the rating they earned on average.</summary>
        public static WorkFormBand Helpers(UtilitiesWords w, string sent, string paid, string rating)
        {
            return WorkBands.Pairs(w.T("reallife_work_band_helpers", "Your helpers"),
                new[] { w.T("reallife_work_helpers_sent", "Visits with a helper"), w.T("reallife_work_helpers_paid", "Paid to helpers"),
                    w.T("reallife_work_helpers_rating", "Their rating on average") },
                new[] { sent, paid, rating });
        }

        /// <summary>The last seven days of visits: done, earned and begun late.</summary>
        public static WorkFormBand LastWeek(UtilitiesWords w, string done, string earned, string late)
        {
            return WorkBands.Pairs(w.T("reallife_work_band_last_week", "The last seven days"),
                new[] { w.T("reallife_work_visits_done", "Visits done"), w.T("reallife_work_earned_word", "Earned"), w.T("reallife_work_visits_late", "Begun late") },
                new[] { done, earned, late });
        }
    }
}
