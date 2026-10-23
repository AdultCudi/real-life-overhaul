using System;
using System.Collections.Generic;
using BigAmbitions.InteriorDesigner.InteriorElements;
using BigAmbitions.Items;
using Blueprints;
using BusinessLayoutSets;
using Controllers;
using Helpers;
using UnityEngine;
using LayoutItem = BusinessLayoutSets.Item;

namespace RealLifeOverhaul.Floors
{
    /// <summary>
    /// A floor's furniture of the game's own: a layout of a business type for the floor's structure, laid in the way
    /// BuildingManager.LoadBusinessLayoutSet lays one, a few pieces a frame while the screen is dark (FloorsDirector.Lay).
    /// </summary>
    internal static partial class FloorsGame
    {
        /// <summary>The game's layout for a structure, read once by the game's own helper (it keeps it); null when the game has none.</summary>
        public static BusinessLayoutSet LayoutSet(FloorLayout layout, string size, int version)
        {
            if (layout == null || string.IsNullOrEmpty(size))
            {
                return null;
            }
            return BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(layout.BusinessType, new BuildingSizeInfo(size, version), layout.Name, false);
        }

        /// <summary>The layout's pieces in the order they are laid: everything but the doors first, its doors (found by name) last.</summary>
        public static List<LayoutItem> LayOrder(BusinessLayoutSet set, string lastName)
        {
            List<LayoutItem> items = set.Items;
            var order = new List<LayoutItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemName != lastName)
                {
                    order.Add(items[i]);
                }
            }
            for (int i = 0; lastName != null && i < items.Count; i++)
            {
                if (items[i].itemName == lastName)
                {
                    order.Add(items[i]);
                }
            }
            return order;
        }

        /// <summary>The layout's walls and floors on the structure, by the game's own call.</summary>
        public static void ApplyLook(BusinessLayoutSet set)
        {
            var elements = new List<InteriorElement>(InteriorElementsHelper.InteriorElementsCache.Values);
            BuildingManager.ApplyInteriorDesign(set.interiorDesigns, elements.ToArray());
        }

        public static Transform Container => Manager != null ? Manager.IndoorItemContainer : null;

        /// <summary>One piece of a layout, the way BuildingManager.LoadBusinessLayoutSet makes it; null for one out of season or with no prefab.</summary>
        public static ItemController Place(LayoutItem item, Transform container)
        {
            BigAmbitions.Items.Item data = ItemsGetter.GetByName(item.itemName);
            if (data == null || data.isSeasonalForSale || (!data.IsAvailableInCurrentSeason() && !BuildingManager.ignoreSeasons))
            {
                return null;
            }
            ItemController c = PrefabHelper.CreatePrefabItem(item.itemName, container);
            if (c == null)
            {
                return null;
            }
            c.playerItemPurchaserSettings = new PlayerItemPurchaserSettings
            {
                enabled = item.playerItemPurchaserSettings != null && item.playerItemPurchaserSettings.enabled,
                itemName = item.playerItemPurchaserSettings != null ? item.playerItemPurchaserSettings.itemName : string.Empty,
                itemQuantity = item.playerItemPurchaserSettings != null ? item.playerItemPurchaserSettings.itemQuantity : 1
            };
            c.itemName = item.itemName;
            c.customPositions = item.customPositions;
            c.customColors = item.customColors;
            c.customValue = item.customValue;
            c.transform.position = item.position;
            c.transform.rotation = item.rotation;
            c.ItemInstance = ItemHelper.InitializeNewInstance(item.itemName);
            c.ItemInstance.id = item.id;
            c.ItemInstance.worldSpaceTextValue = item.worldSpaceTextValue;
            c.ItemInstance.linkedItemName = item.linkedItemName;
            c.gameObject.SetActive(true);
            return c;
        }

        /// <summary>Each piece made stood on the piece it stands on, as the game attaches a layout's stacked items.</summary>
        public static void Attach(Dictionary<string, KeyValuePair<ItemController, List<AttachableChild>>> made)
        {
            foreach (KeyValuePair<string, KeyValuePair<ItemController, List<AttachableChild>>> pair in made)
            {
                ItemController parent = pair.Value.Key;
                List<AttachableChild> children = pair.Value.Value;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    if (made.TryGetValue(children[i].childId, out KeyValuePair<ItemController, List<AttachableChild>> child) && parent.AttachmentPoints.Length > children[i].attachmentIndex)
                    {
                        child.Key.SetToParentPlaceableItem(parent, parent.AttachmentPoints[children[i].attachmentIndex]);
                    }
                }
            }
        }

        /// <summary>The game reads the pieces in the building again (its doors find their chairs, its stations their places).</summary>
        public static void Producers()
        {
            Manager?.ForceUpdateAvailableProducers();
        }

        /// <summary>Pieces laid by the floors taken away again while the player stays inside (a lift to a floor with other furniture).</summary>
        public static void Unlay(List<ItemController> laid)
        {
            for (int i = 0; laid != null && i < laid.Count; i++)
            {
                if (laid[i] != null)
                {
                    UnityEngine.Object.Destroy(laid[i].gameObject);
                }
            }
            laid?.Clear();
            Manager?.ScheduleUpdateAvailableProducers();   // a frame later: what is destroyed goes at the frame's end
        }
    }
}
