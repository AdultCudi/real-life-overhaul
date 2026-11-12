using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    public sealed partial class LogisticsDirector
    {
        private const int RivalsASecond = 40;   // rival shops charged in one second
        private const int RivalFromHour = 1;    // the game values the city's shops at the turn of the day: their books are read from 01:00

        private readonly List<BuildingRegistration> _rivals = new List<BuildingRegistration>(256);
        private readonly List<string> _supplies = new List<string>(16);
        private readonly List<Vector3> _supplyAt = new List<Vector3>(16);
        private int _rivalAt = -1;              // the next rival charged; -1 while no day is under way
        private long _rivalCents;
        private int _rivalCharged;

        private void RivalsStop()
        {
            _rivals.Clear();
            _supplies.Clear();
            _supplyAt.Clear();
            _rivalAt = -1;
        }

        // Once a day, a slice a second: each rival shop's day as the game valued it pays the supply its kind of shop needs
        // (a van's round trip to the nearest wholesale store or importer, the hours the goods are on the road) and the share of
        // its fresh goods that spoils, by the same rules as the player's. Going Public's guest leaves them to the host's books.
        private void RivalsSecond(int today)
        {
            if ((!_tripsOn && !_spoilOn) || GoingPublic.IsGuest || _trips.RivalDay >= today)
            {
                return;
            }
            if (_rivalAt < 0)
            {
                if (Save.Hour < RivalFromHour || LogisticsGame.Valuing)
                {
                    return;
                }
                LogisticsGame.Rivals(_rivals, _supplies);
                _supplyAt.Clear();
                for (int i = 0; i < _supplies.Count; i++)
                {
                    BuildingRegistration s = LogisticsGame.Reg(_supplies[i]);
                    if (s != null && LogisticsGame.At(_supplies[i], s.Address, out Vector3 at))
                    {
                        _supplyAt.Add(at);
                    }
                }
                _rivalAt = 0;
                _rivalCents = 0;
                _rivalCharged = 0;
            }
            int pump = LogisticsGame.PumpCents();
            for (int n = 0; n < RivalsASecond && _rivalAt < _rivals.Count; n++)
            {
                BuildingRegistration r = _rivals[_rivalAt++];
                int day = LogisticsGame.LastDay(r);
                if (day <= 0 || r.Address == null || !LogisticsGame.At(LogisticsGame.Key(r.Address), r.Address, out Vector3 at))
                {
                    continue;
                }
                int road = TripRules.Road(Nearest(at));
                int trip = _tripsOn ? TripRules.RivalTrip(road, pump) : 0;
                int delay = _tripsOn ? TripRules.RivalDelay(road) : 0;
                int spoil = _spoilOn ? SpoilRules.RivalLoss(r.businessTypeName) : 0;
                int cost = TripRules.RivalDay(day, trip, delay, spoil);
                if (cost > 0)
                {
                    LogisticsGame.Charge(r, cost);
                    _rivalCents += cost;
                    _rivalCharged++;
                }
            }
            if (_rivalAt >= _rivals.Count)
            {
                _trips.RivalDay = today;
                _rivalAt = -1;
                _rivals.Clear();
                Logger?.Info("Delivery trips and food spoilage: the city's rival shops' books for day " + today + ": " + _rivalCharged + " shop(s) paid "
                             + (_rivalCents / 100) + " dollar(s) in supply runs, the hours on the road and spoiled stock, by " + _supplyAt.Count + " supplier(s).");
            }
        }

        // The straight line to the nearest supplier, metres; two kilometres when the city shows none.
        private int Nearest(Vector3 at)
        {
            int best = int.MaxValue;
            for (int i = 0; i < _supplyAt.Count; i++)
            {
                best = Math.Min(best, LogisticsGame.Straight(at, _supplyAt[i]));
            }
            return best == int.MaxValue ? 2000 : best;
        }
    }
}
