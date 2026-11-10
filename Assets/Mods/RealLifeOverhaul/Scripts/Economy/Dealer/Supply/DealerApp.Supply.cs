using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Market tab's suppliers in the game's frame: under the market's own words, the supplier of each kind of car, and a page a
    /// kind where it is chosen and a showroom's standing order is set. Written only when the page is.
    /// </summary>
    public sealed partial class DealerApp
    {
        private const string SupplyRow = "sup:";

        // The market's own page, with nothing picked: who supplies each kind, and the way to change it.
        private void WriteSupplySummary(InfoBox box)
        {
            if (!_director.SupplyOn)
            {
                return;
            }
            box.Section("reallife_carsup_sec_suppliers");
            CarSupplyState supply = _director.Supply;
            for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
            {
                int s = CarSupplyRules.SupplierFor(supply, k, true);
                box.Line(DealerText.Fill("reallife_carsup_line_kind", "kind", CarSupplyText.Kind(k), "supplier", CarSupplyText.Name(s), "terms", CarSupplyText.Terms(s, k)));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_carsup_button_choose", () =>
            {
                Pick(SupplyRow + "0");
                Refresh();
            });
        }

        // A kind's page: each supplier's terms and a button to buy the kind from it, then the standing order of a showroom. True if it was written.
        private bool WriteSupply(InfoBox box, DealerState state)
        {
            int kind = Picked(_picked, SupplyRow);
            if (kind < 0 || kind >= CarSupplyCatalogue.Kinds || !_director.SupplyOn)
            {
                return false;
            }
            CarSupplyState supply = _director.Supply;
            int now = CarSupplyRules.SupplierFor(supply, kind, true);
            box.Title(CarSupplyText.Kind(kind));
            box.Subtitle(DealerText.T("reallife_carsup_from", "supplier", CarSupplyText.Name(now)));
            box.Section("reallife_carsup_sec_suppliers");
            for (int s = 0; s < CarSupplyCatalogue.Suppliers; s++)
            {
                box.Line(DealerText.Fill("reallife_carsup_line_supplier", "supplier", CarSupplyText.Name(s), "terms", CarSupplyText.Terms(s, kind)), s == now ? CellTone.Green : CellTone.Plain);
            }
            Transform buttons = box.ButtonRow();
            for (int s = 0; s < CarSupplyCatalogue.Suppliers; s++)
            {
                int to = s;
                if (s != now)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, CarSupplyCatalogue.NameKeys[s], () => _director.SetSupplier(kind, to));
                }
            }
            box.Note(CarSupplyText.About(now));
            box.Note(CarSupplyText.Rivals(state.Seed, kind));
            DealerYard y = state.Yard(_yardChoice) ?? (state.Yards.Count > 0 ? state.Yards[0] : null);
            if (y != null)
            {
                _yardChoice = y.Place;
                int keep = CarSupplyRules.Keep(supply, y.Place, kind), have = CarSupplyRules.Have(state, y, _director.Models, kind);
                box.Section("reallife_carsup_sec_standing");
                box.Line(DealerText.Fill("reallife_carsup_standing_line", "name", y.Name, "keep", keep.ToString(), "have", have.ToString()));
                box.Line(DealerText.T(_director.SupplyManaged(y) ? "reallife_carsup_standing_managed" : "reallife_carsup_standing_unmanaged"),
                    _director.SupplyManaged(y) ? CellTone.Plain : CellTone.Yellow);
                Transform order = box.ButtonRow();
                string place = y.Place;
                if (keep < CarSupplyCatalogue.KeepMost)
                {
                    InfoBox.Button(order, InfoBox.Positive, "reallife_carsup_button_more", () => _director.SetKeep(place, kind, keep + 1));
                }
                if (keep > 0)
                {
                    InfoBox.Button(order, InfoBox.Negative, "reallife_carsup_button_fewer", () => _director.SetKeep(place, kind, keep - 1));
                }
                if (state.Yards.Count > 1)
                {
                    int at = state.Yards.IndexOf(y);
                    InfoBox.Button(order, InfoBox.Additional, "reallife_carsup_button_showroom", () =>
                    {
                        _yardChoice = state.Yards[(at + 1) % state.Yards.Count].Place;
                        Refresh();
                    });
                }
            }
            Transform foot = box.ButtonRow();
            InfoBox.Button(foot, InfoBox.Additional, "reallife_carsup_button_next", () =>
            {
                Pick(SupplyRow + ((kind + 1) % CarSupplyCatalogue.Kinds));
                Refresh();
            });
            InfoBox.Button(foot, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, SupplyGuideTitle);
            return true;
        }

        // A new car's page: who supplies its kind and when it would come.
        private void WriteNewSupply(InfoBox box, DealerListing l)
        {
            if (!_director.SupplyOn)
            {
                return;
            }
            int supplier = _director.SupplierOf(l);
            box.Line(DealerText.Fill("reallife_carsup_new_line", "supplier", CarSupplyText.Name(supplier), "delivery", CarSupplyText.Delivery(supplier)));
        }

        private const string SupplyGuideTitle = "reallife_meta_title_economy_dealer_supply";
    }
}
