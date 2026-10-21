using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Family
{
    /// <summary>HomeLife's part for the places in a home where the partner stands or sits, and the cot and the high chair.</summary>
    internal static partial class HomeLife
    {
        private static readonly string[] CookItems = { "stove", "oven", "microwave", "cooker", "grill", "hob" };
        private static readonly string[] KitchenItems = { "fridge", "coffeemachine", "kitchen", "sink", "counter", "dishwasher" };
        private static readonly string[] BedItems = { "bed" };
        private static readonly string[] SportItems = { "yoga", "treadmill", "weight", "dumbbell", "barbell", "gym", "exercise", "fitness", "punch", "rowing" };
        private static readonly string[] DeskItems = { "desk", "computer", "laptop" };
        private static readonly string[] PlantItems = { "plant", "flower" };
        private static readonly string[] LivingItems = { "sofa", "couch", "itemname_tv", "television", "armchair", "bookshel", "coffeetable" };
        private const int FloorAtLeast = 4;   // places in all, made up with clear floor
        private const int EachKindMost = 2;   // places of one kind (seats: MaxSeats)

        // The places they go to: beside what the game calls a cooker, a fridge or sink, the bed, a mat or weights, a desk, a
        // plant, a sofa or the television, and the seats themselves; failing those, clear floor. Close to the furniture (a small
        // flat's gaps are narrow), and reached from where the player came in.
        private static void Scan(BuildingManager manager, Vector3 player)
        {
            _spotCount = 0;
            _at = -1;
            _agentType = -1;
            Array.Clear(Seats, 0, Seats.Length);
            List<ItemController> items = manager.allItemControllers;
            var per = new int[10];
            var seen = Meta.Dev.Verbose ? new System.Text.StringBuilder(256) : null;
            for (int i = 0; items != null && i < items.Count && _spotCount < MaxSpots; i++)
            {
                ItemController item = items[i];
                if (item == null || !item.isActiveAndEnabled || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }
                int kind = KindOf(item);
                seen?.Append(seen.Length > 0 ? ", " : string.Empty).Append(item.itemName).Append(kind >= 0 ? "=" + kind : string.Empty);
                if (kind < 0 || per[kind] >= (kind == SeatKind ? MaxSeats : EachKindMost))
                {
                    continue;
                }
                Vector3 at = item.transform.position;
                if (!FamilyPlaces.Floor(at, player, 0.25f, 1.8f, out Vector3 spot, out int type) || !Usable(spot, at, type))
                {
                    continue;
                }
                Seats[_spotCount] = kind == SeatKind || kind == BedKind ? item : null;   // (the seat to sit on, the bed to lie in)
                Add(spot, at, kind, type);
                per[kind]++;
            }
            if (seen != null)
            {
                FamilyDirector.Logger?.Info("People check (seat): this home has " + per[SeatKind] + " place(s) to sit among " + _spotCount
                                            + " place(s) for the partner (the game's own seats, at most " + MaxSeats + "): " + (per[SeatKind] > 0 ? "PASS" : "none: they stand") + ".");
                FamilyDirector.Logger?.Info("Family check (home's furniture): " + seen + " (=kind: 0 kitchen, 1 living, 3 seat, 5 cooker, 6 sport, 7 bed, 8 desk, 9 plant).");
            }
            // Clear floor a few steps in from the door, if the furniture gave few places: enough to move between in a small home.
            for (int ring = 0; ring < 3 && _spotCount < FloorAtLeast; ring++)
            {
                float radius = 3f + ring * 1.6f;
                for (int i = 0; i < 8 && _spotCount < FloorAtLeast; i++)
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

        // What a piece of furniture is to them: something to sit on first (a chair, an armchair, a sofa), then by its name. -1: nothing.
        private static int KindOf(ItemController item)
        {
            string name = item.itemName;
            return Seating.IsSeat(item) ? SeatKind : Is(name, CookItems) ? CookKind : Is(name, KitchenItems) ? 0 : Is(name, BedItems) ? BedKind
                : Is(name, SportItems) ? SportKind : Is(name, DeskItems) ? DeskKind : Is(name, PlantItems) ? PlantKind : Is(name, LivingItems) ? 1 : -1;
        }

        private static bool Is(string name, string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (name.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
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
            if (CitySpots.Flat(spot - _entry) < 1.2f * 1.2f || CitySpots.Flat(spot - item) > 2.5f * 2.5f)
            {
                return false;
            }
            for (int i = 0; i < _spotCount; i++)
            {
                if (CitySpots.Flat(Spots[i] - spot) < 0.9f * 0.9f)
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

        // A place at the cot's side where rock_cot puts the hands on the rail, while a baby lies in it.
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
