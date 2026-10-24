using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Blueprints;
using Buildings;
using Entities;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Designs
{
    /// <summary>
    /// The only place the designs touch the game: StreamingAssets, design files in the JSON BusinessLayoutSetHelper reads
    /// (every field kept), the firm's own lists, its orders and notices. The files on a worker thread, the rest on a press or a load.
    /// </summary>
    internal static class DesignsGame
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Main thread only.</summary>
        public static string Root()
        {
            return Application.streamingAssetsPath;
        }

        /// <summary>A design file's items in the file's order; null for a file that cannot be read.</summary>
        public static List<DesignItem> Read(string path)
        {
            try
            {
                JArray items = Parse(path)?["Items"] as JArray;
                if (items == null)
                {
                    return null;
                }
                var list = new List<DesignItem>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    list.Add(Item(items[i] as JObject));
                }
                return list;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static JObject Parse(string path)
        {
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
        }

        private static DesignItem Item(JObject o)
        {
            var item = new DesignItem();
            if (o == null)
            {
                return item;
            }
            item.Id = (string)o["id"];
            item.Name = (string)o["itemName"];
            item.Parent = (string)o["parentId"] ?? string.Empty;
            JObject at = o["position"] as JObject;
            if (at != null)
            {
                item.X = Centimetres(at["x"]);
                item.Y = Centimetres(at["y"]);
                item.Z = Centimetres(at["z"]);
            }
            if (o["stackedItems"] is JArray stacked)
            {
                for (int i = 0; i < stacked.Count; i++)
                {
                    string child = (string)(stacked[i] as JObject)?["childId"];
                    if (!string.IsNullOrEmpty(child))
                    {
                        item.Children.Add(child);
                    }
                }
            }
            return item;
        }

        private static int Centimetres(JToken value)
        {
            return value != null && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                ? (int)Math.Round((double)value * 100.0) : 0;
        }

        /// <summary>Writes the source, every field kept, with only the cut's items and stacks, named and typed as offered; false if not.</summary>
        public static bool WriteCut(string source, ShowroomCut cut, string target, string type, string name)
        {
            JObject design = Parse(source);
            if (!(design?["Items"] is JArray items) || items.Count != cut.Keep.Length)
            {
                return false;
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (!cut.Keep[i])
                {
                    items.RemoveAt(i);
                    continue;
                }
                if (items[i] is JObject shelf && shelf["playerItemPurchaserSettings"] is JObject stock && stock["itemName"] is JValue good)
                {
                    string now = (string)good, then = DesignRules.Restocked(cut, now);
                    if (now != null && then != now)
                    {
                        stock["itemName"] = then;   // a tea bar's machines pour tea
                    }
                }
                if (items[i] is JObject item && item["stackedItems"] is JArray stacked)
                {
                    for (int c = stacked.Count - 1; c >= 0; c--)
                    {
                        string child = (string)(stacked[c] as JObject)?["childId"];
                        if (string.IsNullOrEmpty(child) || !cut.Kept.Contains(child))
                        {
                            stacked.RemoveAt(c);
                        }
                    }
                }
            }
            design["BusinessType"] = type;
            design["LayoutName"] = name;
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? string.Empty);
            File.WriteAllText(target, design.ToString(Formatting.Indented), Utf8);
            return true;
        }

        /// <summary>How many designs the game's firm lists for a type at a size. Main thread only.</summary>
        public static int Listed(string building, string size, string type)
        {
            string sizeId = DesignRules.SizeId(size, out int version);
            if (sizeId == null)
            {
                return 0;
            }
            string[] names = InteriorInstallationFirmHelper.GetInteriorDesignsNamesForBuilding(building, new BuildingSizeInfo(sizeId, version), type);
            return names != null ? names.Length : 0;
        }

        /// <summary>The firms' orders waiting for a business of one of these types.</summary>
        public static void Orders(HashSet<string> types, List<InteriorInstallationFirmContract> into)
        {
            into.Clear();
            List<InteriorInstallationFirmContract> all = SaveGameManager.Current?.interiorInstallationFirmContracts;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                InteriorInstallationFirmContract order = all[i];
                BuildingRegistration reg = order != null ? BuildingHelper.GetBuildingRegistration(order.addressToDoTheInstallation) : null;
                if (reg != null && !order.isBlueprint && types.Contains(order.businessTypeName ?? string.Empty))
                {
                    into.Add(order);
                }
            }
        }

        /// <summary>The game's own installation, now: it pays, sells what is there, puts the design in and says so.</summary>
        public static void Install(InteriorInstallationFirmContract order)
        {
            order.DoInstallation();
        }

        public static bool Loaded()
        {
            return SaveGameManager.Current != null && ModState.Loaded;
        }

        public static void Notice(NotificationType kind, string key, Dictionary<string, string> data)
        {
            Notifications.Show(kind, key, data, 8f);
        }
    }
}
