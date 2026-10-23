using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
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

        // ------------------------------------------------------------------ the show flats' designs

        /// <summary>
        /// Every show flat design the game's residential designs give (ShowFlatDesigns): each apartment structure the game lists,
        /// each of the firm's designs for it read by the firm's own helper, staged and cut plain, with its pieces at the game's
        /// market prices, the firm's fee and the game's interior score of its walls and floors. Main thread, on a press.
        /// </summary>
        public static void ShowDesigns(List<ShowDesign> into)
        {
            into.Clear();
            List<BuildingSizeInfo> structures = BuildingSizeHelper.GetBuildingVersionsByBuildingType(ShowFlatDesigns.Building);
            for (int s = 0; structures != null && s < structures.Count; s++)
            {
                BuildingSizeInfo info = structures[s];
                if (info == null || !ShowFlatDesigns.Flat(info.buildingSize))
                {
                    continue;
                }
                BuildingSizeData data = BuildingSizeHelper.GetData(info.buildingSize);
                int sqm = data != null ? Mathf.RoundToInt(data.squareMeters) : 0;
                string[] names = InteriorInstallationFirmHelper.GetInteriorDesignsNamesForBuilding(ShowFlatDesigns.Building, info);
                for (int n = 0; names != null && n < names.Length; n++)
                {
                    BusinessLayoutSet set = InteriorInstallationFirmHelper.GetInteriorDesignLayout(names[n], ShowFlatDesigns.Building, info);
                    if (set == null || set.Items == null || set.Items.Count == 0)
                    {
                        continue;
                    }
                    for (int f = 0; f < 2; f++)
                    {
                        ShowDesign d = Cut(set, info, sqm, names[n], (ShowFinish)f);
                        if (ShowFlatDesigns.Offered(d.Finish, d.Names.Count, set.Items.Count))
                        {
                            into.Add(d);
                        }
                    }
                }
            }
        }

        private static ShowDesign Cut(BusinessLayoutSet set, BuildingSizeInfo info, int sqm, string name, ShowFinish finish)
        {
            var d = new ShowDesign
            {
                Folder = info.ToString(), Size = info.buildingSize, Version = info.buildingVersion, Sqm = sqm, Design = name, Finish = finish,
                Fee = (long)Mathf.Round(InteriorInstallationFirmHelper.GetInstallationFee(info.buildingSize))
            };
            d.Id = ShowFlatDesigns.Id(d.Folder, name, finish);
            double furniture = 0.0;
            for (int i = 0; i < set.Items.Count; i++)
            {
                BusinessLayoutSets.Item item = set.Items[i];
                if (item == null || !ShowFlatDesigns.Keeps(finish, item.itemName))
                {
                    continue;
                }
                d.Names.Add(item.itemName);
                d.Kept.Add(item.id ?? string.Empty);
                furniture += ItemHelper.GetDefaultMarketPrice(item.itemName);
            }
            d.Furniture = (long)Math.Round(furniture);
            try
            {
                d.Interior = set.interiorDesigns != null && set.interiorDesigns.Count > 0
                    ? Mathf.Clamp(InteriorDesign.InteriorScoreCalculator.GetInteriorScorePercentage(set.interiorDesigns), 0, 100) : 0;
            }
            catch (Exception)
            {
                d.Interior = 0;   // a material the game no longer knows scores nothing, as the game's own calculator gives
            }
            return d;
        }
    }
}
