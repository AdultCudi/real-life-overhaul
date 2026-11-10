using Localizor;
using RealLifeOverhaul.Suppliers;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Suppliers pages from the director and carries every button to it: the same calls the app in the game's frame makes, the same notices. A page is
    /// written when the director's revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed class SupplyLive : IVentSource, ISupplyActions
    {
        public static readonly SupplyLive Instance = new SupplyLive();
        public const string ScreenId = "suppliers";

        private readonly SupplyFacts _facts = new SupplyFacts();
        private readonly VentWords _words = new VentWords((key, english) => key.GetLocalization(), x => SupplierGame.Money((long)System.Math.Round(x * 100.0)))
            .WithPrice(cents => SupplierGame.Money(cents));
        private int _local;

        private SupplyLive()
        {
        }

        public int Generation => (SupplierDirector.Instance != null ? SupplierDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
            Want(_facts, view);
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            SupplierDirector d = SupplierDirector.Instance;
            Want(_facts, v);
            if (d == null || !d.Facts(_facts, v.A, SupplyPages.TermOf(v.B), SupplyPages.ShareOf(v.C)))
            {
                _facts.Ready = false;
            }
            SupplyPages.Fill(m, v, _facts, this, _words);
        }

        // BizMan's Deliveries page asked for a business: the page opens on it, on the Suppliers tab.
        private static void Want(SupplyFacts facts, VentView view)
        {
            string want = SupplierDirector.WantPlace;
            if (string.IsNullOrEmpty(want))
            {
                return;
            }
            SupplierDirector.WantPlace = null;
            SupplierDirector d = SupplierDirector.Instance;
            if (d == null || !d.Facts(facts, 0, SupplyPages.TermDefault, SupplierCatalogue.ShareDefault))
            {
                return;
            }
            for (int i = 0; i < facts.Places.Count; i++)
            {
                if (facts.Places[i].Key == want)
                {
                    view.Tab = SupplyPages.TabSuppliers;
                    view.A = i;
                    view.Pick = null;
                }
            }
        }

        public void Sign(int supplier, string place, int weeks, int shareStep)
        {
            SupplierDirector d = SupplierDirector.Instance;
            if (d == null)
            {
                return;
            }
            Contract c = d.Sign(supplier, place, weeks, shareStep, out Refusal why);
            if (c == null)
            {
                SupplierGame.Tell(NotificationType.Warning, SupplierKeys.RefusalKeys[SupplierCatalogue.Clamp((int)why, 0, SupplierKeys.RefusalKeys.Length - 1)], null);
            }
            _local++;
        }

        public void End(int contract)
        {
            SupplierDirector d = SupplierDirector.Instance;
            Contract c = d != null && d.Running ? d.State.ContractOf(contract) : null;
            if (c == null || !c.Running)
            {
                return;
            }
            GameConfirm.Show("reallife_supply_confirm_end_title".Localize(new { supplier = SupplierText.Firm(c.Supplier) }),
                "reallife_supply_confirm_end_body".Localize(new { amount = SupplierGame.Money(SupplierEngine.BreakCost(c)), business = c.PlaceName }), Guard.Wrap(() =>
                {
                    SupplierDirector now = SupplierDirector.Instance;
                    if (now != null)
                    {
                        now.End(contract);
                    }
                    _local++;
                }), null, null, null, true, true);
        }

        public void Leave(int contract)
        {
            SupplierDirector d = SupplierDirector.Instance;
            if (d != null)
            {
                d.Leave(contract);
            }
            _local++;
        }

        public void PayOwed()
        {
            SupplierDirector d = SupplierDirector.Instance;
            if (d != null && d.PayOwed() <= 0)
            {
                SupplierGame.Tell(NotificationType.Warning, "reallife_supply_note_owed_none", null);
            }
            _local++;
        }

        public void Rule(int contract, int rule)
        {
            SupplierDirector d = SupplierDirector.Instance;
            if (d != null)
            {
                d.Rule(contract, rule);
            }
            _local++;
        }
    }
}
