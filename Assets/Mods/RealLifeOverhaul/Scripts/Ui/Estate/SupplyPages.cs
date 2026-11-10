using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the suppliers' page asks for when a step is pressed; the live page gives the director's calls, the checks and previews a recorder.</summary>
    public interface ISupplyActions
    {
        void SetSupplier(int kind, int supplier);
        void SetKeep(string place, int kind, int count);
        void ShowYard(string place);
    }

    /// <summary>
    /// The words of the showroom's suppliers that are built from the tables, game-free: a supplier's name, a kind of car, a price
    /// against the wholesaler's, a delivery. The words come through <see cref="UiWords"/> (the game's localisation in play, a locale
    /// file in the checks and previews), each with the English it stands for.
    /// </summary>
    public static class SupplyWords
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] NameEnglish = { "City Motor Wholesale", "Harbour Line Imports", "Kingsway Prestige Trade" };
        private static readonly string[] AboutEnglish =
        {
            "The city's wholesaler. Every kind of car at its listed price, on your lot at once.",
            "A volume importer. Everyday cars and vans for less, the dear kinds for more, and the cars take days to come.",
            "A prestige trade house. Luxury cars, sports cars and SUVs for less, everyday cars for more, and the cars take days to come."
        };
        private static readonly string[] KindEnglish = { "Economy cars", "Saloons", "Luxury cars", "Sports cars", "SUVs and pickups", "Vans" };

        public static string Fill(string text, params string[] pairs)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public static string Name(UiWords t, int supplier)
        {
            int s = DealerCatalogue.Clamp(supplier, 0, CarSupplyCatalogue.Suppliers - 1);
            return t(CarSupplyCatalogue.NameKeys[s], NameEnglish[s]);
        }

        public static string About(UiWords t, int supplier)
        {
            int s = DealerCatalogue.Clamp(supplier, 0, CarSupplyCatalogue.Suppliers - 1);
            return t(CarSupplyCatalogue.AboutKeys[s], AboutEnglish[s]);
        }

        public static string Kind(UiWords t, int kind)
        {
            int k = DealerCatalogue.Clamp(kind, 0, CarSupplyCatalogue.Kinds - 1);
            return t(CarSupplyCatalogue.KindKeys[k], KindEnglish[k]);
        }

        /// <summary>A supplier's price for a kind against the wholesaler's: so many per cent under or over the listed price, or the listed price.</summary>
        public static string Against(UiWords t, int supplier, int kind)
        {
            int share = CarSupplyCatalogue.Price[DealerCatalogue.Clamp(supplier, 0, CarSupplyCatalogue.Suppliers - 1)][DealerCatalogue.Clamp(kind, 0, CarSupplyCatalogue.Kinds - 1)];
            if (share == 1000)
            {
                return t("reallife_carsup_price_same", "the listed price");
            }
            string percent = (Math.Abs(share - 1000) / 10f).ToString("0.#", Inv);
            return Fill(share < 1000 ? t("reallife_carsup_price_under", "{percent}% under the listed price") : t("reallife_carsup_price_over", "{percent}% over the listed price"),
                "percent", percent);
        }

        /// <summary>When a supplier's car comes: at once, or in so many days.</summary>
        public static string Delivery(UiWords t, int supplier)
        {
            int days = CarSupplyRules.Days(supplier);
            return days <= 0 ? t("reallife_carsup_delivery_now", "on the lot at once") : Fill(t("reallife_carsup_delivery_days", "comes in {days} days"), "days", days.ToString(Inv));
        }

        /// <summary>A supplier's terms for a kind on one line: its price against the wholesaler's and its delivery.</summary>
        public static string Terms(UiWords t, int supplier, int kind)
        {
            return Fill(t("reallife_carsup_terms", "{price}, {delivery}"), "price", Against(t, supplier, kind), "delivery", Delivery(t, supplier));
        }

        /// <summary>Which supplier the other dealers buy a kind from, as one line of names and counts; the same all game.</summary>
        public static string Rivals(UiWords t, int seed, int kind)
        {
            var line = new System.Text.StringBuilder(160);
            for (int s = 0; s < CarSupplyCatalogue.Suppliers; s++)
            {
                int count = 0;
                for (int r = 0; r < DealerCatalogue.RivalNames.Length; r++)
                {
                    count += CarSupplyRules.RivalSupplier(seed, r, kind) == s ? 1 : 0;
                }
                if (count > 0)
                {
                    line.Append(line.Length > 0 ? ", " : string.Empty).Append(Fill(t("reallife_carsup_rivals_part", "{supplier} ({count})"), "supplier", Name(t, s), "count", count.ToString(Inv)));
                }
            }
            return line.ToString();
        }
    }

    /// <summary>
    /// The Car Showroom's suppliers as a page of the kit, game-free so the checks and previews lay out exactly what ships: a step a
    /// kind for its supplier with its terms under it, a showroom's standing order with a step a kind, and where the other dealers
    /// buy. Every step runs one call of <see cref="ISupplyActions"/>.
    /// </summary>
    public static class SupplyPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Fill(EstateModel m, EstateRuns runs, UiWords t, DealerState s, CarSupplyState supply, IReadOnlyList<MarketModel> models, string yardPlace,
            Func<DealerYard, bool> managed, ISupplyActions act)
        {
            m.Shape = EstateShape.Detail;
            m.DetailFace = null;
            m.Back = t("reallife_ui_dealer_back", "Back");
            m.DetailIcon = UiIcon.Truck;
            m.DetailTitle = t("reallife_carsup_title", "Suppliers");
            m.DetailSub = t("reallife_carsup_sub", "A supplier for each kind of new car");
            m.DetailChip = null;
            var kinds = new List<UiFactLine>(CarSupplyCatalogue.Kinds + 1);
            for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
            {
                int kind = k, now = CarSupplyRules.SupplierFor(supply, k, true);
                kinds.Add(runs.Step(SupplyWords.Kind(t, k), SupplyWords.Name(t, now), SupplyWords.Terms(t, now, k), now > 0, now < CarSupplyCatalogue.Suppliers - 1,
                    () => act.SetSupplier(kind, now - 1), () => act.SetSupplier(kind, now + 1)));
            }
            kinds.Add(UiFactLine.Say(t("reallife_carsup_kept_terms", "A car already ordered keeps the terms it was ordered on.")));
            m.Left = new[] { new UiFactCardModel { Title = t("reallife_carsup_sec_suppliers", "Suppliers by kind of car"), Heading = true, Lines = kinds.ToArray() } };
            var right = new List<UiFactCardModel>(2);
            DealerYard y = s.Yard(yardPlace) ?? (s.Yards.Count > 0 ? s.Yards[0] : null);
            if (y != null)
            {
                string place = y.Place;
                int at = s.Yards.IndexOf(y);
                var order = new List<UiFactLine>(CarSupplyCatalogue.Kinds + 2);
                if (s.Yards.Count > 1)
                {
                    string before = at > 0 ? s.Yards[at - 1].Place : place, after = at < s.Yards.Count - 1 ? s.Yards[at + 1].Place : place;
                    order.Add(runs.Step(t("reallife_carsup_l_showroom", "Showroom"), y.Name, null, at > 0, at < s.Yards.Count - 1, () => act.ShowYard(before), () => act.ShowYard(after)));
                }
                else
                {
                    order.Add(UiFactLine.Row(t("reallife_carsup_l_showroom", "Showroom"), y.Name));
                }
                for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                {
                    int kind = k, keep = CarSupplyRules.Keep(supply, place, k), have = CarSupplyRules.Have(s, y, models, k);
                    order.Add(runs.Step(SupplyWords.Kind(t, k), keep.ToString(Inv), SupplyWords.Fill(t("reallife_carsup_has", "{have} on the lot or on the way"), "have", have.ToString(Inv)),
                        keep > 0, keep < CarSupplyCatalogue.KeepMost, () => act.SetKeep(place, kind, keep - 1), () => act.SetKeep(place, kind, keep + 1)));
                }
                bool has = managed != null && managed(y);
                string who = has ? t("reallife_carsup_standing_managed", "The showroom's manager orders what is missing each morning.")
                    : t("reallife_carsup_standing_unmanaged", "No manager orders stock here. Appoint one with Stock orders in the Company app, or buy by hand.");
                order.Add(UiFactLine.Say(who, has ? UiRole.None : UiRole.WarnText));
                right.Add(new UiFactCardModel { Title = t("reallife_carsup_sec_standing", "Standing order"), Heading = true, Lines = order.ToArray() });
            }
            var others = new List<UiFactLine>(CarSupplyCatalogue.Kinds);
            for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
            {
                others.Add(UiFactLine.Row(SupplyWords.Kind(t, k), SupplyWords.Rivals(t, s.Seed, k)));
            }
            right.Add(new UiFactCardModel { Title = t("reallife_carsup_sec_rivals", "Where the other dealers buy"), Heading = true, Lines = others.ToArray() });
            m.Right = right.ToArray();
            m.Strip = new[]
            {
                new UiFigureModel { Label = SupplyWords.Name(t, CarSupplyCatalogue.Wholesaler), Value = SupplyWords.Delivery(t, CarSupplyCatalogue.Wholesaler) },
                new UiFigureModel { Label = SupplyWords.Name(t, CarSupplyCatalogue.Importer), Value = SupplyWords.Delivery(t, CarSupplyCatalogue.Importer) },
                new UiFigureModel { Label = SupplyWords.Name(t, CarSupplyCatalogue.Specialist), Value = SupplyWords.Delivery(t, CarSupplyCatalogue.Specialist) },
                new UiFigureModel { Label = t("reallife_carsup_fig_on_order", "Cars on order"), Value = supply.Orders.Count.ToString(Inv) }
            };
        }

        /// <summary>A new car's page: who supplies its kind and when it would come, as two rows.</summary>
        public static void NewLines(List<UiFactLine> lines, UiWords t, int supplier)
        {
            lines.Add(UiFactLine.Row(t("reallife_carsup_l_supplier", "Supplier"), SupplyWords.Name(t, supplier)));
            lines.Add(UiFactLine.Row(t("reallife_carsup_l_delivery", "Delivery"), SupplyWords.Delivery(t, supplier)));
        }
    }
}
