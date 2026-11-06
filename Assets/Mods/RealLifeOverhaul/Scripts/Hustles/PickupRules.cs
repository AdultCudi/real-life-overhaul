using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>Why a pickup cannot be done at the player's feet right now, for the notice that says so.</summary>
    public enum PickupWhy
    {
        None = 0,           // nothing in the way
        GetOut = 1,         // the player is at the point in a vehicle
        NotStanding = 2     // the city has no building at the seller's address
    }

    /// <summary>
    /// A seller's door as the Marketplace keeps it: the address, the building once the city has one there, and when the city
    /// is asked again. The game parts hang what they measure on a class of their own that derives from it. Game-free.
    /// </summary>
    public class PickupEntry<T> where T : class
    {
        public string Street;
        public int Number;
        public T Building;        // null until the city's list has a building at the address
        public float NextLook;    // the time the city is asked again while there is none
        public int Looks;         // how many times it was asked

        /// <summary>True if a building is in hand; a miss is asked again after <see cref="PickupRules.LookEvery"/> seconds, or at once when <paramref name="force"/>.</summary>
        public bool Seek(float now, bool force, Func<string, int, T> find)
        {
            if (Building != null)
            {
                return true;
            }
            if (!force && now < NextLook)
            {
                return false;
            }
            NextLook = now + PickupRules.LookEvery;
            Looks++;
            Building = find(Street, Number);
            return Building != null;
        }
    }

    /// <summary>The doors in play, found by address; the oldest goes when the book is full. Game-free.</summary>
    public sealed class PickupBook<TEntry, T> where TEntry : PickupEntry<T>, new() where T : class
    {
        private readonly List<TEntry> _list;
        private readonly int _most;
        private readonly Action<TEntry> _evict;

        /// <param name="most">How many doors are kept.</param>
        /// <param name="evict">Called with a door as it leaves the book, to give back what hangs on it; may be null.</param>
        public PickupBook(int most, Action<TEntry> evict)
        {
            _list = new List<TEntry>(most);
            _most = most;
            _evict = evict;
        }

        public int Count => _list.Count;

        public TEntry this[int i] => _list[i];

        public TEntry Find(string street, int number)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].Number == number && string.Equals(_list[i].Street, street, StringComparison.Ordinal))
                {
                    return _list[i];
                }
            }
            return null;
        }

        public TEntry Add(string street, int number)
        {
            if (_list.Count >= _most)
            {
                _evict?.Invoke(_list[0]);
                _list.RemoveAt(0);
            }
            var made = new TEntry { Street = street, Number = number };
            _list.Add(made);
            return made;
        }

        public void Clear()
        {
            for (int i = 0; i < _list.Count && _evict != null; i++)
            {
                _evict(_list[i]);
            }
            _list.Clear();
        }
    }

    /// <summary>
    /// Where and until when a thing bought on the Marketplace is collected (docs/features/hustles.md, "Collecting"): the days a
    /// seller holds it, how near the player must be, how near the route must end, the repair of a save's waiting things and the
    /// refund of one that lapsed. Game-free and checked offline (EconomyCheck HustleChecks); costs nothing a frame.
    /// </summary>
    public static class PickupRules
    {
        public const int HoldDays = 3;              // days after the purchase a seller holds a thing; the last is the day it is bought plus this
        public const float Reach = 4.5f;            // metres from the pickup point that count as at it
        public const float Rise = 3f;               // ...and up or down
        public const float RouteEnd = 3f;           // metres the route's end may be from the pickup point
        public const float StageWithin = 80f;       // metres from the point the parcel is stood for the player to see
        public const float UnstageBeyond = 140f;    // ...and taken away again beyond this
        public const float MeasureWithin = 80f;     // metres from a building the player is to be before its pavement is measured
        public const float RefineWithin = 25f;      // a point found by a wider search is measured again once the player is this near
        public const float NudgeWithin = 30f;       // metres from the point at which a player in a vehicle is told to get out
        public const float MarkerLift = 1.5f;       // metres over the point the route's marker stands
        public const int LookEvery = 4;             // seconds between asking the city for a building it did not have
        public const int DoorsKept = 48;            // doors measured and remembered, while the city lasts

        /// <summary>The last day a seller holds a thing bought on this day.</summary>
        public static int UntilFor(int boughtDay)
        {
            return boughtDay + HoldDays;
        }

        /// <summary>True once the day is past the last day a thing is held; a thing with no day set is never lapsed (it is repaired on load).</summary>
        public static bool Lapsed(int until, int day)
        {
            return until > 0 && day > until;
        }

        /// <summary>Whole days left, the day counted: 1 on the last day. Never under 0.</summary>
        public static int DaysLeft(int until, int day)
        {
            return Math.Max(0, until - day + 1);
        }

        /// <summary>True if a player this far (flat) and this much higher or lower than the point is at it.</summary>
        public static bool Reaches(float flat, float rise)
        {
            return flat <= Reach && Math.Abs(rise) <= Rise;
        }

        /// <summary>True if the route's end is near enough the point that arriving at the one is arriving at the other.</summary>
        public static bool RouteArrives(float flatFromPoint)
        {
            return flatFromPoint <= RouteEnd;
        }

        /// <summary>What is in the way of a pickup at a place the player has come to.</summary>
        public static PickupWhy Why(bool inVehicle, bool buildingStands)
        {
            return !buildingStands ? PickupWhy.NotStanding : inVehicle ? PickupWhy.GetOut : PickupWhy.None;
        }

        /// <summary>What the player gets back for a thing that lapsed or could never be collected: what they paid, nothing for a test's.</summary>
        public static int Refund(Owned o)
        {
            return o == null || o.Test ? 0 : Math.Max(0, o.Paid + o.Spent);
        }

        /// <summary>
        /// A save's waiting things made right (on load): one with no last day, from a save before things lapsed, is held the full
        /// days from today; one with no address to go to can never be collected, so it leaves the list and is put in
        /// <paramref name="refunded"/>. Returns how many were changed.
        /// </summary>
        public static int Repair(HustleState s, int today, List<Owned> refunded)
        {
            int changed = 0;
            for (int i = s.Items.Count - 1; i >= 0; i--)
            {
                Owned o = s.Items[i];
                if (o.State != OwnedState.Waiting)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(o.Street) || o.Number <= 0)
                {
                    s.Items.RemoveAt(i);
                    refunded?.Add(o);
                    changed++;
                }
                else if (o.Until <= 0)
                {
                    o.Until = UntilFor(today);
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>
        /// Waiting things whose last day has gone leave the list, one event each (their refund in the amount), after the hour's
        /// other rules, which read the list in its order.
        /// </summary>
        public static void Lapse(HustleState s, int day, List<HustleEvent> events)
        {
            for (int i = s.Items.Count - 1; i >= 0; i--)
            {
                Owned o = s.Items[i];
                if (o.State != OwnedState.Waiting || !Lapsed(o.Until, day))
                {
                    continue;
                }
                s.Items.RemoveAt(i);
                events.Add(new HustleEvent { Kind = HustleEventKind.Expired, Id = o.Id, Amount = Refund(o), Who = o.Seller, Thing = o.Thing, Car = o.Car });
            }
        }
    }
}
