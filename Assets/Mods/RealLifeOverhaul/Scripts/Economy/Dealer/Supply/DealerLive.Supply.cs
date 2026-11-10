using System.Collections.Generic;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Car Showroom's suppliers on the kit's Market page: a link over the market's table opens one page with a step a kind for its
    /// supplier, and beside it a showroom's standing order with a step a kind. Every press goes to the director's own call.
    /// </summary>
    public sealed partial class DealerLive
    {
        private sealed partial class Page
        {
            private const string SupplyPick = "sup";
            private static string _supplyYard;   // the showroom whose standing order the page shows

            // The link over the market's table, while the choice of suppliers is on.
            private void SupplyLink()
            {
                if (_d.SupplyOn)
                {
                    _m.TableLink = DealerText.T("reallife_carsup_link");
                    _m.TableLinkRun = _runs.Do(() => Show(() => _v.Pick = SupplyPick));
                }
            }

            // The suppliers' page, if it is the one picked. True if it was written.
            private bool SupplyPage()
            {
                if (_v.Pick != SupplyPick || !_d.SupplyOn)
                {
                    return false;
                }
                CarSupplyState supply = _d.Supply;
                DetailHead(UiIcon.Truck, DealerText.T("reallife_carsup_title"), DealerText.T("reallife_carsup_sub"), null, UiRole.None);
                var kinds = new List<UiFactLine>(CarSupplyCatalogue.Kinds * 2);
                for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                {
                    int kind = k, s = CarSupplyRules.SupplierFor(supply, k, true);
                    kinds.Add(_runs.Step(CarSupplyText.Kind(k), CarSupplyText.Name(s), CarSupplyText.Terms(s, k), s > 0, s < CarSupplyCatalogue.Suppliers - 1,
                        () => Show(() => _d.SetSupplier(kind, s - 1)), () => Show(() => _d.SetSupplier(kind, s + 1))));
                }
                kinds.Add(UiFactLine.Say(DealerText.T("reallife_carsup_kept_terms")));
                _m.Left = new[] { new UiFactCardModel { Title = DealerText.T("reallife_carsup_sec_suppliers"), Heading = true, Lines = kinds.ToArray() } };
                var right = new List<UiFactCardModel>(2);
                DealerYard y = _s.Yard(_supplyYard) ?? (_s.Yards.Count > 0 ? _s.Yards[0] : null);
                if (y != null)
                {
                    _supplyYard = y.Place;
                    string place = y.Place;
                    int at = _s.Yards.IndexOf(y);
                    var order = new List<UiFactLine>(CarSupplyCatalogue.Kinds + 3);
                    if (_s.Yards.Count > 1)
                    {
                        order.Add(_runs.Step(DealerText.T("reallife_carsup_l_showroom"), y.Name, null, at > 0, at < _s.Yards.Count - 1,
                            () => Show(() => _supplyYard = _s.Yards[at - 1].Place), () => Show(() => _supplyYard = _s.Yards[at + 1].Place)));
                    }
                    else
                    {
                        order.Add(UiFactLine.Row(DealerText.T("reallife_carsup_l_showroom"), y.Name));
                    }
                    for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                    {
                        int kind = k, keep = CarSupplyRules.Keep(supply, place, k), have = CarSupplyRules.Have(_s, y, _d.Models, k);
                        order.Add(_runs.Step(CarSupplyText.Kind(k), keep.ToString(Inv), DealerText.T("reallife_carsup_has", "have", have.ToString(Inv)), keep > 0,
                            keep < CarSupplyCatalogue.KeepMost, () => Show(() => _d.SetKeep(place, kind, keep - 1)), () => Show(() => _d.SetKeep(place, kind, keep + 1))));
                    }
                    bool managed = _d.SupplyManaged(y);
                    order.Add(UiFactLine.Say(DealerText.T(managed ? "reallife_carsup_standing_managed" : "reallife_carsup_standing_unmanaged"), managed ? UiRole.None : UiRole.WarnText));
                    right.Add(new UiFactCardModel { Title = DealerText.T("reallife_carsup_sec_standing"), Heading = true, Lines = order.ToArray() });
                }
                var others = new List<UiFactLine>(CarSupplyCatalogue.Kinds);
                for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                {
                    others.Add(UiFactLine.Row(CarSupplyText.Kind(k), CarSupplyText.Rivals(_s.Seed, k)));
                }
                right.Add(new UiFactCardModel { Title = DealerText.T("reallife_carsup_sec_rivals"), Heading = true, Lines = others.ToArray() });
                _m.Right = right.ToArray();
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = CarSupplyText.Name(CarSupplyCatalogue.Wholesaler), Value = CarSupplyText.Delivery(CarSupplyCatalogue.Wholesaler) },
                    new UiFigureModel { Label = CarSupplyText.Name(CarSupplyCatalogue.Importer), Value = CarSupplyText.Delivery(CarSupplyCatalogue.Importer) },
                    new UiFigureModel { Label = CarSupplyText.Name(CarSupplyCatalogue.Specialist), Value = CarSupplyText.Delivery(CarSupplyCatalogue.Specialist) },
                    new UiFigureModel { Label = DealerText.T("reallife_carsup_fig_on_order"), Value = _d.Supply.Orders.Count.ToString(Inv) }
                };
                return true;
            }

            // A new car's page: who supplies its kind and when it would come.
            private void SupplyLines(List<UiFactLine> lines, DealerListing l)
            {
                if (!_d.SupplyOn)
                {
                    return;
                }
                int supplier = _d.SupplierOf(l);
                lines.Add(UiFactLine.Row(DealerText.T("reallife_carsup_l_supplier"), CarSupplyText.Name(supplier)));
                lines.Add(UiFactLine.Row(DealerText.T("reallife_carsup_l_delivery"), CarSupplyText.Delivery(supplier)));
            }
        }
    }
}
