using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Family
{
    /// <summary>HomeLife's part for the places in a home where the partner stands or sits, and the cot and the high chair.</summary>
    internal static partial class HomeLife
    {
        private static readonly string[] KitchenItems = { "stove", "fridge", "coffeemachine", "microwave", "kitchen", "sink", "oven" };
        private static readonly string[] LivingItems = { "sofa", "couch", "itemname_tv", "television", "armchair", "bookshel", "coffeetable" };

        // The places they stand: beside what the game calls a cooker, a fridge, a sofa, a television; failing all, clear floor.
        private static void Scan(BuildingManager manager, Vector3 player)
        {
            _spotCount = 0;
            _at = -1;
            _agentType = -1;
            Array.Clear(Seats, 0, Seats.Length);
            List<ItemController> items = manager.allItemControllers;
            int kitchens = 0, livings = 0, seats = 0;
            for (int i = 0; items != null && i < items.Count && _spotCount < MaxSpots; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.isActiveAndEnabled || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }
                // Something to sit on (whatever the game calls it: a chair, an armchair, a sofa) is a place to sit; the rest are places to stand.
                int kind = Seating.IsSeat(item) ? SeatKind : Is(item.itemName, KitchenItems) ? 0 : Is(item.itemName, LivingItems) ? 1 : -1;
                if (kind < 0 || (kind == 0 && kitchens >= 2) || (kind == 1 && livings >= 2) || (kind == SeatKind && seats >= MaxSeats))
                {
                    continue;
                }
                Vector3 at = item.transform.position;
                if (!FamilyPlaces.Floor(at, player, 0.4f, 1.6f, out Vector3 spot, out int type) || !Usable(spot, at, type))
                {
                    continue;
                }
                Seats[_spotCount] = kind == SeatKind ? item : null;
                Add(spot, at, kind, type);
                if (kind == 0)
                {
                    kitchens++;
                }
                else if (kind == 1)
                {
                    livings++;
                }
                else
                {
                    seats++;
                }
            }
            if (Meta.Dev.Verbose)
            {
                FamilyDirector.Logger?.Info("People check (seat): this home has " + seats + " place(s) to sit among " + _spotCount
                                            + " place(s) for the partner (the game's own seats, at most " + MaxSeats + "): " + (seats > 0 ? "PASS" : "none: they stand") + ".");
            }
            // Clear floor a few steps in from the door, if the furniture gave nothing (or one place only).
            for (int ring = 0; ring < 2 && _spotCount < 2; ring++)
            {
                float radius = 3f + ring * 1.6f;
                for (int i = 0; i < 8 && _spotCount < 2; i++)
                {
                    float angle = (i + ring * 0.5f) * Mathf.PI / 4f;
                    Vector3 near = player + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (FamilyPlaces.Floor(near, player, 0.55f, 0.5f, out Vector3 spot, out int type) && Usable(spot, spot, type))
                    {
                        Add(spot, player, 2, type);
                    }
                }
            }
            if (_spotCount == 0 && !_placeFailedLogged)
            {
                _placeFailedLogged = true;
                FamilyDirector.Logger?.Info("Family: no clear floor on a navigation mesh was found in this home; the partner is not shown here (they still live here).");
            }
        }

        private static bool Is(string name, string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (name.IndexOf(parts[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Not in the doorway, not on top of another place, on the same mesh as the rest, and within reach of what it is beside.
        private static bool Usable(Vector3 spot, Vector3 item, int type)
        {
            if (_agentType >= 0 && type != (_agentType == 0 ? NavMeshHelperIds.Npc : NavMeshHelperIds.Player))
            {
                return false;
            }
            if (CitySpots.Flat(spot - _entry) < 1.6f * 1.6f || CitySpots.Flat(spot - item) > 2.0f * 2.0f)
            {
                return false;
            }
            for (int i = 0; i < _spotCount; i++)
            {
                if (CitySpots.Flat(Spots[i] - spot) < 1.5f * 1.5f)
                {
                    return false;
                }
            }
            return true;
        }

        private static void Add(Vector3 spot, Vector3 face, int kind, int type)
        {
            _agentType = type == NavMeshHelperIds.Npc ? 0 : 1;
            Spots[_spotCount] = spot;
            Faces[_spotCount] = face;
            Kinds[_spotCount] = kind;
            _spotCount++;
        }

        private static int AgentType => _agentType == 0 ? NavMeshHelperIds.Npc : NavMeshHelperIds.Player;

        // The cot and the high chair, each on a clear piece of floor that people then walk round.
        private static void Nursery(Vector3 player)
        {
            FamilySave s = FamilyState.S;
            FamilyDirector director = FamilyDirector.Instance;
            bool cot = s.Cot && FamilyState.AnyUnder(FamilyBalance.CotUntil);
            bool chair = false;
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                chair |= !c.Grown && c.Age >= FamilyBalance.HighChairFrom && c.Age < FamilyBalance.HighChairUntil;
            }
            if (!cot && !chair)
            {
                return;
            }
            int placed = 0;
            for (int ring = 0; ring < 3 && (cot || chair); ring++)
            {
                float radius = 2.6f + ring * 1.3f;
                for (int i = 0; i < 10 && (cot || chair); i++)
                {
                    float angle = (i * 0.63f + ring * 0.3f + placed) * Mathf.PI * 0.4f;
                    Vector3 near = player + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (!FamilyPlaces.Floor(near, player, 0.85f, 0.5f, out Vector3 spot, out _) || CitySpots.Flat(spot - _entry) < 2.2f * 2.2f || Taken(spot))
                    {
                        continue;
                    }
                    if (cot)
                    {
                        _cot = director.Props.Stand(FamilyProps.Cot, spot, player - spot, true);
                        FamilyProps.Tuck(_cot, FamilyState.AnyUnder(FamilyBalance.BabyUntil));   // a baby in arms is in it; a toddler's cot stands empty
                        cot = false;
                    }
                    else
                    {
                        _chair = director.Props.Stand(FamilyProps.HighChair, spot, player - spot, true);
                        chair = false;
                    }
                    placed++;
                }
            }
        }

        // A place at the cot's long side for rocking it while a baby lies in it: within a hand's breadth of where rock_cot
        // has the feet, so that the rail comes to the hands.
        private static void CotPlace()
        {
            if (_cot == null || _spotCount == 0 || _spotCount > MaxSpots || !FamilyState.AnyUnder(FamilyBalance.BabyUntil) || !ClipLibrary.Has(RockClip))
            {
                return;
            }
            var filter = new NavMeshQueryFilter { agentTypeID = AgentType, areaMask = NavMesh.AllAreas };
            for (int side = 1; side >= -1; side -= 2)
            {
                if (NavMesh.SamplePosition(_cot.position + _cot.right * (side * CotBeside), out NavMeshHit hit, 0.08f, filter))
                {
                    Spots[_spotCount] = hit.position;
                    Faces[_spotCount] = _cot.position;
                    Kinds[_spotCount] = CotKind;
                    Seats[_spotCount] = null;
                    _spotCount++;
                    return;
                }
            }
        }

        private static bool Taken(Vector3 spot)
        {
            for (int i = 0; i < _spotCount; i++)
            {
                if (CitySpots.Flat(Spots[i] - spot) < 1.6f * 1.6f)
                {
                    return true;
                }
            }
            return (_cot != null && CitySpots.Flat(_cot.position - spot) < 1.8f * 1.8f) || (_chair != null && CitySpots.Flat(_chair.position - spot) < 1.8f * 1.8f);
        }
    }
}
