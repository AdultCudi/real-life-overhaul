using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The tax reserve and the figures round it. The game's tax is one bill a year; the reserve is money the player (or the finance chief, when a quarter closes)
    /// puts out of reach in the meantime, and it goes back to the cash when the bill comes or the game's tax year turns over. The quarter that is running is
    /// shown from the game's own takings and the costs that count here, at the game's rate; the past returns kept are the last few settled. Nothing per frame.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private TaxRules.QuarterFigures _quarterFigures;
        private long _quarterStamp = -1;
        private readonly List<CompanyGame.PropertyTaxLine> _property = new List<CompanyGame.PropertyTaxLine>(4);
        private int _propertyDay = -1;

        /// <summary>The quarter that is running as the page shows it: worked out again when the day, its costs or the reserve change, otherwise as it was.</summary>
        public TaxRules.QuarterFigures QuarterNow()
        {
            TaxFile t = _state.Tax;
            long stamp = (long)Today * 1000003L + TaxRules.CostsSum(t) * 31L + t.Reserve * 7L + t.YearTax + (long)t.Quarter * 13L;
            if (stamp != _quarterStamp)
            {
                int to = Today - 1, takings = Today > t.QuarterFrom ? CompanyGame.Sales(t.QuarterFrom, to) + CompanyBridges.Other(t.QuarterFrom, to)
                    : 0;
                _quarterFigures = TaxRules.Quarter(t, takings, CompanyGame.TaxPercent);
                _quarterStamp = stamp;
            }
            return _quarterFigures;
        }

        /// <summary>The property tax of each building the player owns, read once a day; none for a business in rented premises (it is inside the rent).</summary>
        internal IReadOnlyList<CompanyGame.PropertyTaxLine> PropertyTax()
        {
            if (_propertyDay != Today)
            {
                CompanyGame.PropertyTaxes(Today, _property);
                _propertyDay = Today;
            }
            return _property;
        }

        /// <summary>The day the finance chief tops the reserve up: the day the running quarter closes.</summary>
        public int ReserveTopUpDay => _state != null ? TaxRules.TopUpDay(_state.Tax, QuarterDays) : 0;

        /// <summary>The finance chief in post, who tops the reserve up; null for none (or the hierarchy off).</summary>
        public ExecutiveFile ReserveChief => HierarchyOn ? ExecutiveAtWork(ExecSeat.Cfo) : null;

        // The game's tax year turning over, or the yearly bill coming: what was set aside goes back to the cash, so that the bill can be paid from it.
        private void ReserveYear(int day)
        {
            TaxFile t = _state.Tax;
            int released;
            if (TaxRules.Year(t, TaxRules.YearStart(day, CompanyGame.DaysPerYear), out released))
            {
                if (released > 0)
                {
                    GiveBack(released, "the game's tax year turned over");
                }
                _quarterStamp = -1;
            }
            else if (t.Reserve > 0 && CompanyGame.BillWaiting())
            {
                GiveBack(TaxRules.Release(t), "the game's yearly tax bill of " + CompanyGame.BillAmount() + " has come");
                _quarterStamp = -1;
            }
        }

        // The tax part is switched off (or stopped after an error) with money set aside: it goes back to the cash, once, so that it is never held for a part that does not run.
        private void ReserveFreed()
        {
            int back = TaxRules.Freed(_state.Tax, On(CompanyPart.Tax));
            if (back <= 0)
            {
                return;
            }
            GiveBack(back, "the tax option is switched off");
            _quarterStamp = -1;
        }

        private void GiveBack(int amount, string how)
        {
            Pay(amount, CompanyGame.ReserveBackKey, null, string.Empty, null, how);
            Tell(NotificationType.Info, "reallife_co_note_reserve_back", new Dictionary<string, string> { { "amount", CompanyText.Money(amount) } }, TabTax, null);
            Logger?.Info("Company: " + amount + " of the tax reserve went back to the cash: " + how + ".");
        }

        // A quarter has closed: with a finance chief in post and the top-up on, the reserve is topped up to the year's tax so far, as far as a share of the cash goes.
        private void ReserveClose(int day)
        {
            TaxFile t = _state.Tax;
            ExecutiveFile chief = t.ReserveAuto ? ReserveChief : null;
            if (chief == null)
            {
                return;
            }
            int moved = TaxRules.TopUp(t, CompanyGame.Cash);
            if (moved <= 0)
            {
                return;
            }
            if (!Charge(moved, CompanyGame.ReserveKey, null, chief.Name, null, false, "put into the tax reserve by the finance chief " + chief.Name + " when quarter " + t.ReturnQuarter + " closed"))
            {
                t.Reserve -= moved;   // the game refused it: nothing moved
                return;
            }
            chief.Decided++;
            HierarchyEntry entry = _hier.Log(HierarchyLog.Reserve, (int)Rung.Executive, chief.Name, string.Empty, day, Save.Hour);
            entry.A = moved;
            entry.B = t.Reserve;
            entry.C = t.YearTax;
            Tell(NotificationType.Info, "reallife_co_note_reserve_topped",
                new Dictionary<string, string> { { "name", chief.Name }, { "amount", CompanyText.Money(moved) } }, TabTax, null);
            Logger?.Info("Company: " + chief.Name + " (finance) put " + moved + " into the tax reserve as quarter " + t.ReturnQuarter + " closed: it holds " + t.Reserve
                         + " against the year's " + t.YearTax + ".");
            _quarterStamp = -1;
        }

        /// <summary>The player sets money aside: out of the cash into the reserve, as far as the cash goes.</summary>
        public void ReserveSet(int amount)
        {
            if (!On(CompanyPart.Tax) || amount <= 0)
            {
                return;
            }
            TaxFile t = _state.Tax;
            int moved = TaxRules.SetAside(t, amount, CompanyGame.Cash);
            if (moved <= 0 || !Charge(moved, CompanyGame.ReserveKey, null, string.Empty, null, false, "set aside for the year's tax bill by the player"))
            {
                t.Reserve -= moved;
                Notifications.ShowInsufficientMoney();
                return;
            }
            Notify(NotificationType.Success, "reallife_co_note_reserve_set", new Dictionary<string, string> { { "amount", CompanyText.Money(moved) } }, 8f);
            Logger?.Info("Company: " + moved + " set aside in the tax reserve by the player: it holds " + t.Reserve + ".");
            _quarterStamp = -1;
            Changed();
        }

        /// <summary>The reserve goes back to the cash, all of it.</summary>
        public void ReserveRelease()
        {
            TaxFile t = _state.Tax;
            if (!On(CompanyPart.Tax) || t.Reserve <= 0)
            {
                return;
            }
            GiveBack(TaxRules.Release(t), "released by the player");
            _quarterStamp = -1;
            Changed();
        }

        /// <summary>Whether the finance chief tops the reserve up when a quarter closes.</summary>
        public void ReserveSetAuto(bool on)
        {
            if (On(CompanyPart.Tax))
            {
                _state.Tax.ReserveAuto = on;
                Changed();
            }
        }
    }
}
