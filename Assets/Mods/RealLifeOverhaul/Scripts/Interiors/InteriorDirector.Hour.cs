using System.Collections.Generic;
using AI.Customers.CustomerEntries;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>Interiors' hour: whether each business sells, the baskets, and the offices' extra clients, given again after a redraw.</summary>
    public sealed partial class InteriorDirector
    {
        // What waited for a redraw that was not made after all, given now within the hour it fell due in.
        private void Due(int hour)
        {
            _dueScratch.Clear();
            foreach (KeyValuePair<BuildingRegistration, BasketDue> due in _baskets)
            {
                if (!CustomerDraw.Waits(due.Key))
                {
                    _dueScratch.Add(due.Key);
                }
            }
            for (int i = 0; i < _dueScratch.Count; i++)
            {
                BasketDue due = _baskets[_dueScratch[i]];
                _baskets.Remove(_dueScratch[i]);
                if (due.Hour == hour && BasketOn)
                {
                    _basketToday += InteriorGame.Basket(_dueScratch[i], due.Share, due.Office, due.Hour, due.Soonest);
                }
            }
            _dueScratch.Clear();
            foreach (KeyValuePair<BuildingRegistration, int> due in _officesDue)
            {
                if (!CustomerDraw.Waits(due.Key))
                {
                    _dueScratch.Add(due.Key);
                }
            }
            for (int i = 0; i < _dueScratch.Count; i++)
            {
                int dueHour = _officesDue[_dueScratch[i]];
                _officesDue.Remove(_dueScratch[i]);
                InteriorShop s = At(_dueScratch[i]);
                if (dueHour == hour && s != null && s.OfficeBoost > 0)
                {
                    Book(_dueScratch[i], s);
                }
            }
            _dueScratch.Clear();
        }

        private void Hourly()
        {
            _baskets.Clear();   // an hour's basket is of that hour
            _officesDue.Clear();
            CustomerDraw.Hold("the interiors' hour");
            try
            {
                Selling();
                Offices();
                if (!BasketOn)
                {
                    return;
                }
                int hour = TimeHelper.CurrentHour;
                float soonest = TimeHelper.CurrentMinute + InteriorGame.BasketLead;
                for (int i = 0; i < _shops.Count; i++)
                {
                    InteriorShop s = _shops[i];
                    if (s.Basket <= 0)
                    {
                        continue;
                    }
                    BuildingRegistration reg = Business(i);
                    if (reg != null && CustomerDraw.Waits(reg))
                    {
                        // about to be drawn again: the basket goes on the new customers (Redrawn)
                        _baskets[reg] = new BasketDue { Share = s.Basket, Office = s.Kind == InteriorKind.Office, Hour = hour, Soonest = soonest };
                        continue;
                    }
                    _basketToday += reg != null ? InteriorGame.Basket(reg, s.Basket, s.Kind == InteriorKind.Office, hour, soonest) : 0;
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
        }

        // Each office's extra clients for the coming hour; one waiting for a redraw is booked in Redrawn, or failing it Due.
        private void Offices()
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].OfficeBoost > 0)
                {
                    BuildingRegistration reg = Business(i);
                    if (reg != null && CustomerDraw.Waits(reg))
                    {
                        _officesDue[reg] = TimeHelper.CurrentHour;
                    }
                    else if (reg != null)
                    {
                        Book(reg, _shops[i]);
                    }
                }
            }
        }

        private void Book(BuildingRegistration reg, InteriorShop s)
        {
            int carry;
            _carry.TryGetValue(s.Place, out carry);
            int n = InteriorGame.BookOffice(reg, s.OfficeBoost, ref carry, _ours);
            _carry[s.Place] = carry;
            if (n > 0)
            {
                int had;
                _booked.TryGetValue(s.Place, out had);
                _booked[s.Place] = had + n;
                _officeToday += n;
            }
        }

        // A redraw took an office's extra clients with it, so they are booked again; a waiting basket goes on the new customers.
        private void Redrawn(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            if (s == null || _broken)
            {
                return;
            }
            _officesDue.Remove(reg);   // booked now
            if (s.OfficeBoost > 0)
            {
                Book(reg, s);
            }
            if (_baskets.Count > 0 && _baskets.TryGetValue(reg, out BasketDue due))
            {
                _baskets.Remove(reg);
                if (due.Hour == TimeHelper.CurrentHour && BasketOn)
                {
                    _basketToday += InteriorGame.Basket(reg, due.Share, due.Office, due.Hour, due.Soonest);
                }
            }
        }

        /// <summary>Clients booked at an office today, and how many are still in the game's list (some go with a redraw).</summary>
        public int BookedToday(BuildingRegistration reg, out int standing)
        {
            standing = 0;
            InteriorShop s = At(reg);
            int n = 0;
            if (s == null || !_booked.TryGetValue(s.Place, out n))
            {
                return 0;
            }
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                standing += entries[i] != null && _ours.Contains(entries[i]) ? 1 : 0;
            }
            return n;
        }

        /// <summary>True for a customer of today's list the feature booked (an office's extra client).</summary>
        public bool Ours(CustomerEntry e)
        {
            return e != null && _ours.Contains(e);
        }

        // Whether each business sells anything this hour; its footfall is written again when that changes what it brings.
        private void Selling()
        {
            InteriorParts parts = Parts;
            foreach (KeyValuePair<BuildingRegistration, InteriorShop> pair in _byReg)
            {
                InteriorShop s = pair.Value;
                SaleStop stop = InteriorGame.Stop(pair.Key);
                if (stop == s.Stop)
                {
                    continue;
                }
                int was = s.Footfall;
                SaleStop before = s.Stop;
                s.Stop = stop;
                InteriorRules.Work(s, s.Wear, parts);
                Logger?.Info("Interiors: " + s.Name + (stop != SaleStop.None ? " sells nothing just now (" + stop + "): its interior brings nobody extra until it does."
                                                                              : " sells again (it was " + before + "): its interior's customers are back."));
                if (s.Kind == InteriorKind.Shop && s.Footfall != was)
                {
                    Write(pair.Key, false);
                }
            }
        }
    }
}
