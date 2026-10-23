using System.Collections.Generic;
using BigAmbitions.Items;

namespace RealLifeOverhaul.Interiors
{
    internal static partial class InteriorGame
    {
        /// <summary>Uses the gameplay classifier without reading ItemCached or inserting the Sorted cache.</summary>
        internal static object CompanionItems()
        {
            var rows = new List<object>();
            foreach (Item item in ItemsGetter.AllItems)
            {
                if (item == null || string.IsNullOrEmpty(item.itemName))
                {
                    continue;
                }
                int sort = Classify(item);
                rows.Add(CompanionSnapshot.Row("itemId", item.itemName, "category", sort & 0xff,
                    "seat", (sort & (1 << Seat)) != 0, "table", (sort & (1 << Table)) != 0,
                    "kitchen", (sort & (1 << Kitchen)) != 0));
            }
            return rows;
        }
    }
}
