using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Blueprints;
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
    /// copy of Manhattan Luxury Cars stands in the game's own layout of it (MurrayHillCarDealershipLuxury), which the game's own
    /// Manhattan Luxury Cars shares and which is left exactly as the game has it. At the load the copy is given a layout of its own,
    /// a copy of that one filed under a name of the mod's (as the leased offices' are, Uptown's Borrow), before any interior is built
    /// from it, and only the copy's furniture is changed. The dealer's desks a car mod turns the layout's plain desks into (the game
    /// seats the player at one and then pauses on its dialog) have the loose chairs round them taken out: they left the player, standing
    /// up from the seat at the desk, with a chair at each side, a plant and the glass partitions beyond. The dealer's own seat and the
    /// seat set for the player on each desk stay, as the game's desk reads both. Each other seat that boxes a person in is moved the
    /// smallest step that frees it. The cars the car mods put in the game's list for the showroom copied (their lists are kept by its
    /// business name, which the copy does not have) are added to the copy's own. Nothing here runs a frame: a measure at the load (a
    /// few milliseconds, a search of a few more when a seat is boxed in), and a list read when the player walks into the dealership.
    /// </summary>
    internal static partial class MadeGame
    {
        private const string ItemStart = "ba:itemname_";   // the game's item names begin so; the rules' do not

        private static readonly Dictionary<string, FloorVerdict> Floors = new Dictionary<string, FloorVerdict>(2);   // by the declaration's id: the verdict of its floor at this load
        private static readonly Dictionary<string, ClearVerdict> Desks = new Dictionary<string, ClearVerdict>(2);    // and of its dealer's desks
        private static readonly Dictionary<string, string> FloorFailed = new Dictionary<string, string>(2);          // and why it could not be given a layout of its own, where it could not

        /// <summary>The name the dealership's own layout is filed under: the game has none of it, so no business of the game's names it.</summary>
        public static string FloorName(MadeSpec spec)
        {
            return "RealLife " + spec.Id + " " + FloorMeasure.Layout;
        }

        /// <summary>
        /// The dealership given a layout of its own, measured, its dealer's desks cleared and its seats mended, once a load, before any
        /// interior is built from it. Returns what the floor is for Player.log (null for a layout the rules do not cover). The game's own
        /// layout is never touched: the copy's pieces are taken out and moved, so the game's own Manhattan Luxury Cars is as it was.
        /// </summary>
        private static string CompleteFloor(SpecialService copy, MadeSpec spec)
        {
            Floors.Remove(spec.Id);
            Desks.Remove(spec.Id);
            FloorFailed.Remove(spec.Id);
            FloorRoom room = FloorRoom.For(spec.Size, spec.Version);
            string name = FloorName(spec);
            if (string.Equals(copy.layout, name, StringComparison.OrdinalIgnoreCase))
            {
                copy.layout = FloorMeasure.Layout;   // a copy made whole at an earlier load of this run: from the game's layout again
            }
            BusinessLayoutSet shipped = room != null ? LayoutOf(copy, spec) : null;
            if (shipped == null || shipped.Items == null || !string.Equals(shipped.LayoutName, FloorMeasure.Layout, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            BusinessLayoutSet own = Copy(shipped, name);
            var pieces = new List<FloorPiece>(128);
            var items = new List<LayoutItem>(128);
            Pieces(own, pieces, items);
            if (pieces.Count == 0)
            {
                return null;
            }
            var measure = new FloorMeasure(room);
            int removed = 0;
            List<int> loose = FloorMeasure.LooseAt(pieces);
            for (int n = loose.Count - 1; n >= 0; n--)
            {
                LayoutItem gone = items[loose[n]];
                if (gone.stackedItems == null || gone.stackedItems.Count == 0)   // a seat that carries something stays, and the desk's verdict says so
                {
                    own.Items.Remove(gone);
                    removed++;
                }
            }
            if (removed > 0)
            {
                Pieces(own, pieces, items);
            }
            var before = new float[pieces.Count * 2];
            for (int i = 0; i < pieces.Count; i++)
            {
                before[i * 2] = pieces[i].X;
                before[i * 2 + 1] = pieces[i].Z;
            }
            FloorVerdict floor = measure.Tidy(pieces);
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
            ClearVerdict desks = measure.Clear(pieces);
            desks.Removed = removed;
            if (!Filed(own, name, spec))
            {
                FloorFailed[spec.Id] = "the game files its layouts another way now (a game update?), so the dealership stands in the game's own";
                return null;
            }
            copy.layout = name;
            Floors[spec.Id] = floor;
            Desks[spec.Id] = desks;
            return FloorMeasure.Words(floor) + "; " + removed + " chair(s) taken out near the dealer's desk (" + FloorMeasure.Words(desks) + ")";
        }

        /// <summary>The verdict of a made building's floor at this load, or null for none (not a dealership, or the floor was not measured).</summary>
        public static FloorVerdict FloorVerdictOf(string id)
        {
            return id != null && Floors.TryGetValue(id, out FloorVerdict v) ? v : null;
        }

        /// <summary>The verdict of a made building's dealer's desks at this load, or null for none.</summary>
        public static ClearVerdict DeskVerdictOf(string id)
        {
            return id != null && Desks.TryGetValue(id, out ClearVerdict v) ? v : null;
        }

        /// <summary>Why the dealership's floor was not given a layout of its own at this load, or null.</summary>
        public static string FloorFailedOf(string id)
        {
            return id != null && FloorFailed.TryGetValue(id, out string why) ? why : null;
        }

        /// <summary>
        /// The floor and the dealer's desks of the layout the copy stands in measured again now (a census), without changing anything; null
        /// for a place the rules do not cover, or one that stands in the game's own layout.
        /// </summary>
        public static FloorVerdict FloorNow(SpecialService service, MadeSpec spec, out ClearVerdict desks)
        {
            desks = null;
            FloorRoom room = FloorRoom.For(spec.Size, spec.Version);
            BusinessLayoutSet layout = room != null ? LayoutOf(service, spec) : null;
            if (layout == null || layout.Items == null || !string.Equals(layout.LayoutName, FloorName(spec), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            var pieces = new List<FloorPiece>(128);
            Pieces(layout, pieces, new List<LayoutItem>(128));
            if (pieces.Count == 0)
            {
                return null;
            }
            var measure = new FloorMeasure(room);
            desks = measure.Clear(pieces);
            return measure.Measure(pieces, null);
        }

        // A layout set under a name of the mod's, a copy of the game's with every item its own (the game's is shared with its own Manhattan
        // Luxury Cars, and a car mod changes its desks in place), so taking an item out or moving one touches the copy alone.
        private static BusinessLayoutSet Copy(BusinessLayoutSet from, string name)
        {
            var items = new List<LayoutItem>(from.Items.Count);
            for (int i = 0; i < from.Items.Count; i++)
            {
                if (from.Items[i] != null)
                {
                    items.Add(Copy(from.Items[i]));
                }
            }
            return new BusinessLayoutSet
            {
                Items = items, interiorDesigns = from.interiorDesigns, BusinessType = from.BusinessType, BuildingSize = from.BuildingSize, BuildingVersion = from.BuildingVersion,
                LayoutName = name, buildNumber = from.buildNumber, requiredModIds = from.requiredModIds
            };
        }

        private static LayoutItem Copy(LayoutItem s)
        {
            PlayerItemPurchaserSettings sold = s.playerItemPurchaserSettings;
            var stacked = new List<AttachableChild>(s.stackedItems != null ? s.stackedItems.Count : 0);
            for (int i = 0; s.stackedItems != null && i < s.stackedItems.Count; i++)
            {
                stacked.Add(new AttachableChild { childId = s.stackedItems[i].childId, childItemName = s.stackedItems[i].childItemName, attachmentIndex = s.stackedItems[i].attachmentIndex });
            }
            return new LayoutItem
            {
                id = s.id, rotation = s.rotation, position = s.position, itemName = s.itemName,
                playerItemPurchaserSettings = sold == null ? null
                    : new PlayerItemPurchaserSettings { name = sold.name, enabled = sold.enabled, itemName = sold.itemName, itemQuantity = sold.itemQuantity },
                stackedItems = stacked, parentId = s.parentId, dirtSpotsThatAffects = s.dirtSpotsThatAffects != null ? new List<int>(s.dirtSpotsThatAffects) : new List<int>(),
                customPositions = s.customPositions != null ? new List<SerializableVector3>(s.customPositions) : null,
                customColors = s.customColors != null ? new List<CustomColor>(s.customColors) : null,
                customValue = s.customValue, worldSpaceTextValue = s.worldSpaceTextValue, linkedItemName = s.linkedItemName, dirtAffectedCells = s.dirtAffectedCells
            };
        }

        // The set in the game's own book of layouts under its name, and found there by the game's own lookup: false when the game files its layouts another way now.
        private static bool Filed(BusinessLayoutSet own, string name, MadeSpec spec)
        {
            if (BusinessLayoutSetHelper.loadingLayouts)
            {
                return false;
            }
            Dictionary<string, BusinessLayoutSet> all = BusinessLayoutSetHelper.GetAllBusinessLayoutSets();
            if (all == null)
            {
                return false;
            }
            string key = own.BusinessType + "|" + own.BuildingSize + "|" + own.BuildingVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + name.ToLowerInvariant();
            all[key] = own;
            if (BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(own.BusinessType, new BuildingSizeInfo(spec.Size, spec.Version), name, false) == own)
            {
                return true;
            }
            all.Remove(key);
            return false;
        }

        // The pieces of a layout the rules know a footprint of, with the items they stand for, in the layout's order: where each is, and what each is set on.
        private static void Pieces(BusinessLayoutSet layout, List<FloorPiece> pieces, List<LayoutItem> items)
        {
            pieces.Clear();
            items.Clear();
            var at = new Dictionary<string, int>(layout.Items.Count);
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
                if (!string.IsNullOrEmpty(item.id))
                {
                    at[item.id] = pieces.Count;
                }
                pieces.Add(new FloorPiece(name, item.position.x, item.position.z, turn.eulerAngles.y) { Fixed = !string.IsNullOrEmpty(item.parentId) });
                items.Add(item);
            }
            for (int i = 0; i < layout.Items.Count; i++)
            {
                LayoutItem holder = layout.Items[i];
                if (holder == null || holder.stackedItems == null)
                {
                    continue;
                }
                int parent = !string.IsNullOrEmpty(holder.id) && at.TryGetValue(holder.id, out int p) ? p : -2;   // -2: set on a piece the rules know no footprint of
                for (int k = 0; k < holder.stackedItems.Count; k++)
                {
                    AttachableChild child = holder.stackedItems[k];
                    if (child != null && !string.IsNullOrEmpty(child.childId) && at.TryGetValue(child.childId, out int c))
                    {
                        pieces[c].Parent = parent;
                        pieces[c].Slot = child.attachmentIndex;
                        pieces[c].Fixed = true;   // set on another piece: never moved
                    }
                }
            }
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
