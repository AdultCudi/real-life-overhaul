using System.Collections.Generic;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Entities;
using Helpers;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The cargo panel's Move to a warehouse (game side): a stack of a vehicle parked at one of the player's warehouses is put on that
    /// warehouse's storage by the game's own hand-over between holders (the shelves are cargo holders, as the vehicle is): a pack is
    /// first merged into a part box of the same goods, else set on a free pallet slot. What is left stays in the vehicle. Cost: a pass
    /// over the warehouse's items when the panel reads, and when the button is pressed; nothing else.
    /// </summary>
    internal static class CargoMove
    {
        private static readonly List<ItemInstance> Shelves = new List<ItemInstance>(32);

        /// <summary>The player's warehouse the holder's vehicle is parked at (its street data is the warehouse's address); null for any other holder.</summary>
        public static Warehouse Target(ICargoHolder holder)
        {
            var vehicle = holder as VehicleInstance;
            if (vehicle == null || string.IsNullOrEmpty(vehicle.streetName))
            {
                return null;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(vehicle.Address);
            return reg is Warehouse w && GoingPublic.Mine(reg) && w.HasEstablishedBusiness ? w : null;
        }

        /// <summary>True when the warehouse's storage has a free slot or a part box of these goods to fill.</summary>
        public static bool Room(Warehouse w, CargoInstance sample)
        {
            int free = 0, part = 0;
            Storage(w);
            for (int i = 0; i < Shelves.Count; i++)
            {
                ItemInstance shelf = Shelves[i];
                free += shelf.CanAddCargo() ? 1 : 0;
                for (int c = 0; sample != null && c < shelf.cargoInstances.Count; c++)
                {
                    CargoInstance have = shelf.cargoInstances[c];
                    part += have.itemName == sample.itemName && have.paid == sample.paid && !have.IsSealed && have.nestedCargoInstances.Count == 0
                            && have.amount < sample.GetMaxStockCapacity(shelf) ? 1 : 0;
                }
            }
            Shelves.Clear();
            return CargoMoveRules.Room(free, part);
        }

        /// <summary>
        /// Moves the stack's packs to the warehouse, tells the player what went and what stayed, and returns the packs moved.
        /// Each pack that is whole on the shelf leaves the vehicle; one the shelves only took part of keeps its rest in the vehicle.
        /// </summary>
        public static int Move(ICargoHolder holder, CargoItem item, Warehouse w)
        {
            if (holder == null || item == null || w == null || item.cargoInstances == null)
            {
                return 0;
            }
            Storage(w);
            var packs = new List<CargoInstance>(item.cargoInstances);
            int moved = 0;
            for (int p = packs.Count - 1; p >= 0; p--)
            {
                CargoInstance pack = packs[p];
                if (!pack.paid || pack.IsSealed || pack.nestedCargoInstances.Count > 0)
                {
                    continue;
                }
                for (int s = 0; s < Shelves.Count && pack.amount > 0; s++)
                {
                    Shelves[s].MergeIntoCargo(pack);
                }
                bool placed = pack.amount <= 0;   // merged away into part boxes
                for (int s = 0; s < Shelves.Count && !placed; s++)
                {
                    placed = Shelves[s].TryToAddToCargo(pack);   // true: the pack, or all that was left of it, is on the shelf
                }
                if (placed)
                {
                    holder.RemoveFromCargo(pack);
                    moved++;
                }
            }
            Shelves.Clear();
            Notifications.Show(moved > 0 ? NotificationType.Success : NotificationType.Warning, CargoMoveRules.NoticeKey(moved, packs.Count),
                new Dictionary<string, string> { { "count", moved.ToString() }, { "total", packs.Count.ToString() }, { "goods", ItemName(item) }, { "name", w.GetDisplayName() } }, 8f);
            GameEvent.Invoke("ba:gameevent_itemcargochanged");
            return moved;
        }

        // The warehouse's storage items: pallets and shelves the game counts its stock on (the iswarehousestorage tag).
        private static void Storage(Warehouse w)
        {
            Shelves.Clear();
            foreach (ItemInstance value in w.itemInstances.Values)
            {
                if (value.ItemCached.HasTag(TagRef.Itemtag.iswarehousestorage))
                {
                    Shelves.Add(value);
                }
            }
        }

        private static string ItemName(CargoItem item)
        {
            CargoInstance first = item.cargoInstances.Count > 0 ? item.cargoInstances[0] : null;
            return first != null && first.ItemCached != null ? UiText.T(first.ItemCached.itemName) : string.Empty;
        }
    }
}
