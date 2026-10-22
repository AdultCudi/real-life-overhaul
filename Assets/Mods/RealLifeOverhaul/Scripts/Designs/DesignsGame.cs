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
    /// The only place the ready-made designs touch the game: where its StreamingAssets folder is, a design file read and
    /// written in the game's own format (the JSON its BusinessLayoutSetHelper reads, every field kept as it was), the game's
    /// own list of an address's designs (InteriorInstallationFirmHelper), its installers' orders and its notices. Each read is
    /// a small method of its own. Nothing here runs per frame: the files on a worker thread once, the rest on a press or a load.
    /// </summary>
    internal static class DesignsGame
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>The game's StreamingAssets folder. The engine answers this on the main thread only.</summary>
        public static string Root()
        {
            return Application.streamingAssetsPath;
        }

        /// <summary>A design file's items as the rules see them, in the file's order; null for a file that cannot be read.</summary>
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

        /// <summary>
        /// Writes a Car Showroom's design: the source as it is, every field kept, with only the items the cut keeps (and what
        /// stands on them pointing only at kept items), named as offered and marked as the showroom's. False if it could not.
        /// </summary>
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

        /// <summary>The names of the designs the game's own installation firm lists for a type at a size (the main thread only).</summary>
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

        /// <summary>The installers' orders waiting for a business of one of these types, of this save.</summary>
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

        /// <summary>The game's own installation of an order, now, as on its day (it pays, sells what is there, puts the design in, says so).</summary>
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
