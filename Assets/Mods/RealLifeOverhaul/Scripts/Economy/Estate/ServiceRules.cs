using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A planned visit by a contractor: what is serviced, the day, who comes (a business of the city, by its address key) and the fee.</summary>
    public sealed class ServiceBooking
    {
        public int Kind;            // a call kind, 1 to EstateCatalogue.CallKinds: the fault it keeps down
        public int Day;
        public string Contractor = string.Empty;
        public int Fee;
    }

    /// <summary>
    /// Booked servicing: a planned visit that keeps a kind of fault down for a season (the matching call is not made on most days
    /// it would be). Game-free; the director pays the fee on the day and the contractor is a business of the city.
    /// </summary>
    public static class ServiceRules
    {
        private const int ForService = 72, ForContractor = 73;

        /// <summary>The fee of a visit for a kind of fault: a share of what a call for it would be quoted.</summary>
        public static int Fee(EstateHolding h, int kind, int index)
        {
            return Math.Max(1, EstateCatalogue.Share(EstateRules.CallCost(h, kind, index), EstateCatalogue.ServiceShare));
        }

        /// <summary>How long a service holds: a season of the game's year, never under a fortnight.</summary>
        public static int Season(int daysPerYear) => Math.Max(14, daysPerYear / 4);

        /// <summary>The earliest day a service can be booked for.</summary>
        public static int Earliest(int day) => day + EstateCatalogue.ServiceDaysAhead;

        /// <summary>The contractor for a visit: one of the city's businesses that do the trade, by a roll; empty when there are none.</summary>
        public static string Contractor(string[] firms, int seed, int key, int day)
        {
            return firms == null || firms.Length == 0 ? string.Empty : firms[EstateRules.Roll(seed, day, ForContractor, key) * firms.Length / 1000];
        }

        /// <summary>A visit is booked. False when the kind is none, the day is too soon, the building has the most booked or the kind is already booked.</summary>
        public static bool Book(EstateHolding h, int kind, int day, int bookDay, string contractor, int fee)
        {
            if (h == null || kind < 1 || kind > EstateCatalogue.CallKinds || bookDay < Earliest(day) || h.Services.Count >= EstateCatalogue.ServicesMost || fee < 1)
            {
                return false;
            }
            for (int i = 0; i < h.Services.Count; i++)
            {
                if (h.Services[i].Kind == kind)
                {
                    return false;
                }
            }
            h.Services.Add(new ServiceBooking { Kind = kind, Day = bookDay, Contractor = contractor ?? string.Empty, Fee = fee });
            h.Services.Sort((a, b) => a.Day.CompareTo(b.Day));
            return true;
        }

        public static bool Cancel(EstateHolding h, int index)
        {
            if (h == null || index < 0 || index >= h.Services.Count)
            {
                return false;
            }
            h.Services.RemoveAt(index);
            return true;
        }

        /// <summary>The first booked visit whose day has come, or null; nothing changed.</summary>
        public static ServiceBooking NextDue(EstateHolding h, int day)
        {
            for (int i = 0; h != null && i < h.Services.Count; i++)
            {
                if (h.Services[i].Day <= day)
                {
                    return h.Services[i];
                }
            }
            return null;
        }

        /// <summary>The visit is done and paid for: it is taken off the list and its kind held down for a season from the day.</summary>
        public static void Complete(EstateHolding h, ServiceBooking b, int day, int daysPerYear)
        {
            if (h == null || b == null || !h.Services.Remove(b))
            {
                return;
            }
            h.ServiceUntil[EstateCatalogue.Clamp(b.Kind, 0, EstateCatalogue.CallKinds)] = day + Season(daysPerYear);
        }

        /// <summary>True while a service holds the kind down.</summary>
        public static bool Holds(EstateHolding h, int kind, int day)
        {
            return h != null && kind >= 1 && kind <= EstateCatalogue.CallKinds && h.ServiceUntil[kind] >= day;
        }

        /// <summary>True when a call of this kind that would be made today is not: a service holds and the roll is under the cut.</summary>
        public static bool Averts(EstateHolding h, int kind, int seed, int day, int key)
        {
            return Holds(h, kind, day) && EstateRules.Roll(seed, day, ForService, key) < EstateCatalogue.ServiceCut;
        }
    }
}
