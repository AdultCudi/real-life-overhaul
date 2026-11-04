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
    }
}
