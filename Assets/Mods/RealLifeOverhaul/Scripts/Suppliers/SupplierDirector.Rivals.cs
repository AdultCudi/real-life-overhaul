using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// The rivals' side (the NPC rule: whatever the player can do, rivals do too). Once a day, a slice a second: each rival shop of the city, as the game valued its
    /// day, buys from a supplier of its own choosing by the engine's rule, most on a fixed price, pays more or less than calm prices for its goods and loses sales when
    /// its supplier's delivery, judged by the very draw the player's is, comes late or short. Then the rival tycoons' outlets by the same rule, out of their worth. A
    /// Going Public guest leaves the city's shops to the host's books, and the tycoons rest in a session. A day under way is finished before the save is written
    /// (and before the option's switch puts the feature away), so no shop is ever gone through twice for a day.
    /// </summary>
    public sealed partial class SupplierDirector
    {
        private const int RivalsASecond = 40;   // rival shops gone through in one second
        private const int RivalFromHour = 1;    // the game values the city's shops at the turn of the day: their books are read from 01:00

        private readonly List<BuildingRegistration> _rivals = new List<BuildingRegistration>(256);
        private readonly int[] _customers = new int[SupplierCatalogue.Firms.Length];
        private readonly List<RivalOutletRef> _outlets = new List<RivalOutletRef>(8);
        private readonly List<string> _outletTypes = new List<string>(8);
        private int _rivalAt = -1;              // the next rival shop; -1 while no day is under way
        private int _rivalToday;
        private long _rivalCents;
        private int _rivalMoved, _rivalFailed;

        private void RivalsStop()
        {
            _rivals.Clear();
            _outlets.Clear();
            _outletTypes.Clear();
            _rivalAt = -1;
        }

        private void RivalsSecond(int today, int hour)
        {
            if (GoingPublic.IsGuest || _state.RivalDay >= today)
            {
                return;
            }
            if (_rivalAt < 0)
            {
                if (hour < RivalFromHour || SupplierGame.Valuing)
                {
                    return;
                }
                SupplierGame.RivalShops(_rivals);
                Array.Clear(_customers, 0, _customers.Length);
                _rivalAt = 0;
                _rivalToday = today;
                _rivalCents = 0;
                _rivalMoved = _rivalFailed = 0;
            }
            RivalsRun(RivalsASecond);
        }

        // A day under way is finished at once: asked before the save is written and before the feature is put away.
        private void RivalsFinish()
        {
            if (_rivalAt >= 0 && _loaded)
            {
                RivalsRun(int.MaxValue);
            }
        }

        private void RivalsRun(int most)
        {
            for (int n = 0; n < most && _rivalAt < _rivals.Count; n++)
            {
                BuildingRegistration r = _rivals[_rivalAt++];
                int day = SupplierGame.LastDay(r), kind = r != null ? SupplierGame.KindOfShop(r.businessTypeName) : -1;
                if (r == null || r.Address == null || kind < 0)
                {
                    continue;
                }
                RivalDay result = SupplierEngine.RivalShop(_state, _world, SupplierGame.Key(r.Address), kind, day, _customers);
                _rivalFailed += result.Failed ? 1 : 0;
                if (result.Cents != 0)
                {
                    SupplierGame.Charge(r, result.Cents);
                    _rivalCents += result.Cents;
                    _rivalMoved++;
                }
            }
            if (_rivalAt < _rivals.Count)
            {
                return;
            }
            int shops = _rivals.Count;
            Tycoons();
            SupplierEngine.RivalsDone(_state, _rivalToday, _customers);
            RivalsStop();
            _revision++;
            Logger?.Info("Suppliers: the city's rival shops' supply for day " + _rivalToday + ": " + shops + " shop(s), " + _rivalMoved + " paid " + (_rivalCents / 100)
                         + " dollar(s) more than calm prices in all (a saving is negative), " + _rivalFailed + " had a late or short delivery.");
        }

        // The rival tycoons' outlets, out of their worth: only while the rivals are forward and no session keeps them at rest.
        private void Tycoons()
        {
            _outlets.Clear();
            _outletTypes.Clear();
            Action<List<RivalOutletRef>, List<string>> read = SupplierBridges.Outlets;
            if (read == null || GoingPublic.InSession)
            {
                return;
            }
            read(_outlets, _outletTypes);
            for (int i = 0; i < _outlets.Count; i++)
            {
                RivalOutletRef o = _outlets[i];
                o.Kind = i < _outletTypes.Count ? SupplierGame.KindOfShop(_outletTypes[i]) : -1;
                _outlets[i] = o;
            }
            SupplierEngine.Outlets(_state, _world, _outlets);
        }
    }
}
