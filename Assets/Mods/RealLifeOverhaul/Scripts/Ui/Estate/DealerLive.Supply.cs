using System.Collections.Generic;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Car Showroom's suppliers on the kit's Market page: a link over the market's table opens the suppliers' page
    /// (<see cref="CarSupplyPages"/>, the same page the checks and previews lay out), and every step goes to the director's own call.
    /// </summary>
    public sealed partial class DealerLive
    {
        private sealed partial class Page : ICarSupplyActions
        {
            private const string SupplyPick = "sup";
            private static string _supplyYard;   // the showroom whose standing order the page shows

            // The link over the market's table, while the choice of suppliers is on.
            private void SupplyLink()
            {
                if (_d.SupplyOn)
                {
                    _m.TableLink = CarSupplyText.Words("reallife_carsup_link", "Suppliers");
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
                CarSupplyPages.Fill(_m, _runs, CarSupplyText.Words, _s, _d.Supply, _d.Models, _supplyYard, _d.SupplyManaged, this);
                return true;
            }

            void ICarSupplyActions.SetSupplier(int kind, int supplier) => Show(() => _d.SetSupplier(kind, supplier));

            void ICarSupplyActions.SetKeep(string place, int kind, int count) => Show(() => _d.SetKeep(place, kind, count));

            void ICarSupplyActions.ShowYard(string place) => Show(() => _supplyYard = place);

            // A new car's page: who supplies its kind and when it would come.
            private void SupplyLines(List<UiFactLine> lines, DealerListing l)
            {
                if (_d.SupplyOn)
                {
                    CarSupplyPages.NewLines(lines, CarSupplyText.Words, _d.SupplierOf(l));
                }
            }
        }
    }
}
