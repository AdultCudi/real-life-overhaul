using System;
using System.Collections.Generic;
using Buildings;
using BusinessLayoutSets;
using Services;
using UnityEngine;
using Vehicles.VehicleTypes;
using LayoutItem = BusinessLayoutSets.Item;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A dealership's showroom floor made walkable, and its cars made whole, on the game's side (FloorMeasure, StockRules). The
    /// copy of Manhattan Luxury Cars stands in the game's own layout of it (MurrayHillCarDealershipLuxury, in the game's cache for
    /// the run), whose chairs before its desks and its lounge leave a person standing up from them nowhere to walk: a pair of chairs
    /// set at an angle before a desk, a chair before a footstool. At the load, before any interior is built from the layout, each
    /// seat that boxes a person in is moved the smallest step that frees it (the layout's own item records in memory, nothing
    /// saved), so the game's own copy of the showroom is mended the same. The cars the car mods put in the game's list for the
    /// showroom copied (their lists are kept by its business name, which the copy does not have) are added to the copy's own.
    /// Nothing here runs a frame: a measure at the load (a few milliseconds, a search of a few more when a seat is boxed in), and
    /// a list read when the player walks into the dealership.
    /// </summary>
    internal static partial class MadeGame
    {
        private const string ItemStart = "ba:itemname_";   // the game's item names begin so; the rules' do not

        private static readonly Dictionary<string, FloorVerdict> Floors = new Dictionary<string, FloorVerdict>(2);   // by the declaration's id: the verdict of its floor at this load

        /// <summary>
        /// The floor of a dealership's layout measured and mended, once a load, before any interior is built from it. Returns what
        /// the floor is for Player.log (null for a layout the rules do not cover). The layout's items are only moved, never added
        /// or taken away, so its valuation and its cars are as the game has them.
        /// </summary>
        private static string CompleteFloor(SpecialService copy, MadeSpec spec)
        {
            FloorVerdict verdict = FloorOf(copy, spec, true);
            if (verdict == null)
            {
                return null;
            }
            Floors[spec.Id] = verdict;
            return FloorMeasure.Words(verdict);
        }

        /// <summary>The verdict of a made building's floor at this load, or null for none (not a dealership, or the floor was not measured).</summary>
        public static FloorVerdict FloorVerdictOf(string id)
        {
            return id != null && Floors.TryGetValue(id, out FloorVerdict v) ? v : null;
        }

        /// <summary>
        /// The floor of the layout the copy stands in measured again now (a census), without moving anything; null for a place the rules
        /// do not cover.
        /// </summary>
        public static FloorVerdict FloorNow(SpecialService service, MadeSpec spec)
        {
            return FloorOf(service, spec, false);
        }

        private static FloorVerdict FloorOf(SpecialService service, MadeSpec spec, bool mend)
        {
            FloorRoom room = FloorRoom.For(spec.Size, spec.Version);
            BusinessLayoutSet layout = room != null ? LayoutOf(service, spec) : null;
            if (layout == null || layout.Items == null || !string.Equals(layout.LayoutName, FloorMeasure.Layout, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            var pieces = new List<FloorPiece>(128);
            var items = new List<LayoutItem>(128);
            for (int i = 0; i < layout.Items.Count; i++)
            {
                LayoutItem item = layout.Items[i];
                string name = item != null ? item.itemName : null;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                name = name.StartsWith(ItemStart, StringComparison.Ordinal) ? name.Substring(ItemStart.Length) : name;
                if (!FloorMeasure.Known(name))
                {
                    continue;
                }
                Quaternion turn = item.rotation;
                pieces.Add(new FloorPiece(name, item.position.x, item.position.z, turn.eulerAngles.y) { Fixed = !string.IsNullOrEmpty(item.parentId) });
                items.Add(item);
            }
            if (pieces.Count == 0)
            {
                return null;
            }
            var measure = new FloorMeasure(room);
            if (!mend)
            {
                return measure.Measure(pieces, null);
            }
            var before = new float[pieces.Count * 2];
            for (int i = 0; i < pieces.Count; i++)
            {
                before[i * 2] = pieces[i].X;
                before[i * 2 + 1] = pieces[i].Z;
            }
            FloorVerdict verdict = measure.Tidy(pieces);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].X != before[i * 2] || pieces[i].Z != before[i * 2 + 1])
                {
                    SerializableVector3 at = items[i].position;
                    at.x = pieces[i].X;
                    at.z = pieces[i].Z;
                    items[i].position = at;
                }
            }
            return verdict;
        }

        /// <summary>
        /// The cars the car mods have put in the game's list for the dealership copied (by its business name, "Manhattan Luxury Cars"),
        /// each once, those the game has, into <paramref name="into"/> (not cleared). How many were added.
        /// </summary>
        public static int ModdedCars(string copied, List<string> into)
        {
            if (string.IsNullOrEmpty(copied) || into == null || !ContractItemsForSaleService.TryGetVehiclesForContact(copied, out List<string> theirs))
            {
                return 0;
            }
            return StockRules.Distinct(theirs, name => VehicleTypeHelper.GetVehicleType(name) != null, into);
        }
    }
}
