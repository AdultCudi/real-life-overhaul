using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Shops;

namespace RealLifeOverhaul.Designs
{
    /// <summary>One piece of furniture of a design as the rules see it.</summary>
    public sealed class DesignItem
    {
        public string Id;
        public string Name;                  // the game's item name, with its "ba:itemname_"
        public int X, Y, Z;                  // centimetres, in the room's own place in the world
        public string Parent;                // the item it stands on, or empty
        public readonly List<string> Children = new List<string>(0);   // the items on it; reading them turns away a stack WriteCut could not write
    }

    /// <summary>A rectangle of floor, in centimetres; empty while nothing is in it.</summary>
    public struct FloorBox
    {
        public int MinX, MaxX, MinZ, MaxZ;
        public bool Any;                     // something was taken in, or the four sides were set

        public static FloorBox None => default;

        public bool Empty => !Any || MinX > MaxX || MinZ > MaxZ;
        public int Width => Empty ? 0 : MaxX - MinX;
        public int Depth => Empty ? 0 : MaxZ - MinZ;

        public bool Holds(int x, int z)
        {
            return !Empty && x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }

        public void Take(int x, int z)
        {
            if (!Any)
            {
                MinX = MaxX = x;
                MinZ = MaxZ = z;
                Any = true;
                return;
            }
            MinX = Math.Min(MinX, x);
            MaxX = Math.Max(MaxX, x);
            MinZ = Math.Min(MinZ, z);
            MaxZ = Math.Max(MaxZ, z);
        }
    }

    /// <summary>One design file of the game's that a business type's design may be made from.</summary>
    public sealed class DesignSource
    {
        public int Kind;                     // its place in DesignCatalogue.Kinds
        public int Donor;                    // its place in that kind's donors
        public DonorRoot Root;
        public string Size;                  // the game's folder of the building size and version ("C1")
        public string Name;                  // as the firm offers it: the file's name
        public string Path;                  // under StreamingAssets, with '/'
        public long Length;
        public long Ticks;                   // last written, UTC: only to see that it changed
    }

    /// <summary>What a Car Showroom, bank branch or new kind of shop keeps of one design, and what that comes to.</summary>
    public sealed class ShowroomCut
    {
        public readonly HashSet<string> Kept = new HashSet<string>(StringComparer.Ordinal);
        public bool[] Keep = new bool[0];    // by the item's place in the design; an id given twice is kept once, the first
        public int Furniture;                // kept pieces that are not ceiling lamps
        public int Tills;                    // kept points of sale, standing on kept counters
        public int Toilets;
        public int InLot;                    // pieces left out because they stood where the cars go
        public int Seats;                    // kept seats to wait on (DesignCatalogue.BranchSeats)
        public int Points;                   // kept points of sale a shop's customers pay at (DesignCatalogue.ShopTills)
        public int Leads;                    // kept furniture that shows a shop's primary goods (ShopKind.Leads)
        public int Dropped;                  // shelves left out because they show none of a shop's goods
        public string[] Restock = new string[0];   // pairs of item names, with "ba:itemname_": a kept shelf set to stock the first stocks the second

        public bool Valid => Furniture >= DesignCatalogue.ShowroomLeast && Toilets > 0;

        /// <summary>A bank branch's design: valid, and a till on a counter for its teller.</summary>
        public bool ValidBranch => Valid && Tills > 0;

        /// <summary>A new kind of shop's design: valid, a point of sale, and something that shows a primary good.</summary>
        public bool ValidShop => Valid && Points > 0 && Leads > 0;
    }

    /// <summary>
    /// The rules of the ready-made designs, game-free: the game's folder naming, which designs a type is offered under
    /// which name, and what a Car Showroom or bank branch keeps of one. Every answer depends only on its input, so the
    /// same game files make the same designs on every machine. Run on a worker thread when the files change.
    /// </summary>
    public static class DesignRules
    {
        private static readonly HashSet<string> Keeps = Build(DesignCatalogue.ShowroomKeeps);
        private static readonly HashSet<string> BranchKeeps = Build(DesignCatalogue.ShowroomKeeps, DesignCatalogue.BranchExtras);
        private static readonly HashSet<string> Seats = Build(DesignCatalogue.BranchSeats);
        private static readonly HashSet<string> ShopShelves = Build(DesignCatalogue.ShopShelves);
        private static readonly HashSet<string> ShopTills = Build(DesignCatalogue.ShopTills);

        private static HashSet<string> Build(params string[][] lists)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int l = 0; l < lists.Length; l++)
            {
                for (int i = 0; i < lists[l].Length; i++)
                {
                    set.Add(DesignCatalogue.ItemPrefix + lists[l][i]);
                }
            }
            return set;
        }

        /// <summary>The game's folder name of an id ("ba:buildingtype_office" is "office"), as StringIdExtensions.GetIdWithoutType.</summary>
        public static string FolderOf(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.IndexOf('_') < 0)
            {
                return string.Empty;
            }
            return id.Split('_')[1];
        }

        /// <summary>True for a folder the game names a building size and version by ("C1", "K1", "T16").</summary>
        public static bool IsSize(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder.Length < 2 || folder[0] < 'A' || folder[0] > 'Z')
            {
                return false;
            }
            int digits = 0;
            for (int i = folder.Length - 1; i >= 1 && folder[i] >= '0' && folder[i] <= '9'; i--)
            {
                digits++;
            }
            for (int i = 1; i < folder.Length - digits; i++)
            {
                if (folder[i] < 'a' || folder[i] > 'z')
                {
                    return false;
                }
            }
            return digits > 0;
        }

        /// <summary>The game's size id and version of a size folder ("C1": "ba:buildingsize_c", 1), the reverse of BuildingSizeInfo.ToString.</summary>
        public static string SizeId(string folder, out int version)
        {
            version = 0;
            if (!IsSize(folder))
            {
                return null;
            }
            int cut = folder.Length;
            while (cut > 1 && folder[cut - 1] >= '0' && folder[cut - 1] <= '9')
            {
                cut--;
            }
            version = int.Parse(folder.Substring(cut), CultureInfo.InvariantCulture);
            return "ba:buildingsize_" + folder.Substring(0, cut).ToLowerInvariant();
        }

        /// <summary>An installer design's file name, or a layout's style (DesignCatalogue.DealerStyles); null for one not offered.</summary>
        public static string NameOf(DonorRoot root, string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(DesignCatalogue.Extension, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            string name = fileName.Substring(0, fileName.Length - DesignCatalogue.Extension.Length);
            if (root == DonorRoot.Installer)
            {
                return name.Length > 0 && name.IndexOf('|') < 0 ? name : null;
            }
            for (int i = 0; i < DesignCatalogue.DealerStyles.Length; i++)
            {
                string style = DesignCatalogue.DealerStyles[i];
                if (name.Length > style.Length && name.EndsWith(style, StringComparison.Ordinal))
                {
                    return style;
                }
            }
            return null;
        }

        /// <summary>Sorts the sources in place: by kind, size, donor, then path as the game lists them (case aside).</summary>
        public static void Order(List<DesignSource> sources)
        {
            sources.Sort((a, b) =>
            {
                int c = a.Kind.CompareTo(b.Kind);
                if (c == 0)
                {
                    c = string.CompareOrdinal(a.Size, b.Size);
                }
                if (c == 0)
                {
                    c = a.Donor.CompareTo(b.Donor);
                }
                if (c == 0)
                {
                    c = string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
                }
                return c != 0 ? c : string.CompareOrdinal(a.Path, b.Path);
            });
        }

        /// <summary>For DesignWay.Copy: of one kind and size's sources, in <see cref="Order"/>, the first of each name (case aside).</summary>
        public static void Copies(List<DesignSource> sources, List<DesignSource> into)
        {
            into.Clear();
            for (int i = 0; i < sources.Count; i++)
            {
                if (!Named(into, sources[i].Name))
                {
                    into.Add(sources[i]);
                }
            }
        }

        private static bool Named(List<DesignSource> chosen, string name)
        {
            for (int i = 0; i < chosen.Count; i++)
            {
                if (string.Equals(chosen[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The room a size's designs fill: everything they place, but strays at the world's origin.</summary>
        public static FloorBox Room(List<List<DesignItem>> designs)
        {
            FloorBox room = FloorBox.None;
            for (int d = 0; designs != null && d < designs.Count; d++)
            {
                List<DesignItem> items = designs[d];
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    DesignItem item = items[i];
                    if (!Stray(item))
                    {
                        room.Take(item.X, item.Z);
                    }
                }
            }
            return room;
        }

        /// <summary>An item a design left at the world's origin, outside every room.</summary>
        public static bool Stray(DesignItem item)
        {
            return Math.Abs(item.X) < DesignCatalogue.Origin && Math.Abs(item.Z) < DesignCatalogue.Origin;
        }

        /// <summary>
        /// The floor a Car Showroom's lot may take in this room: every car's place as DealerRules.Layout puts it, for a floor
        /// up to <see cref="DesignCatalogue.Slack"/> off the designs' reach either way, with <see cref="DesignCatalogue.Clear"/>
        /// round each.
        /// </summary>
        public static void Lot(FloorBox room, List<FloorBox> into)
        {
            into.Clear();
            if (room.Empty)
            {
                return;
            }
            var offsets = new List<int>(2 * DealerCatalogue.SpotsMost);
            int slack = DesignCatalogue.Slack, half = slack / 2;
            for (int dw = -slack; dw <= slack; dw += slack)
            {
                for (int dd = -slack; dd <= slack; dd += slack)
                {
                    int count = DealerRules.Layout(room.Width + dw, room.Depth + dd, offsets, out bool across);
                    int hx = (across ? DealerCatalogue.SpotDepth : DealerCatalogue.SpotWidth) / 2 + DesignCatalogue.Clear;
                    int hz = (across ? DealerCatalogue.SpotWidth : DealerCatalogue.SpotDepth) / 2 + DesignCatalogue.Clear;
                    for (int s = 0; s < count; s++)
                    {
                        int x = (room.MinX + room.MaxX) / 2 + offsets[2 * s], z = (room.MinZ + room.MaxZ) / 2 + offsets[2 * s + 1];
                        into.Add(new FloorBox { MinX = x - hx - half, MaxX = x + hx + half, MinZ = z - hz - half, MaxZ = z + hz + half, Any = true });
                    }
                }
            }
        }

        /// <summary>What a Car Showroom keeps: <see cref="DesignCatalogue.ShowroomKeeps"/> clear of the lot or above <see cref="DesignCatalogue.FloorTop"/>.</summary>
        public static ShowroomCut Cut(List<DesignItem> items, List<FloorBox> lot)
        {
            return Cut(items, lot, Keeps.Contains);
        }

        /// <summary>What a bank branch keeps: the showroom's furniture and <see cref="DesignCatalogue.BranchExtras"/>, with no lot.</summary>
        public static ShowroomCut BranchCut(List<DesignItem> items)
        {
            return Cut(items, NoLot, BranchKeeps.Contains);
        }

        /// <summary>
        /// What a new kind of shop keeps of a design of the game's shop it works as: everything but the shelves that show
        /// none of its goods (<see cref="DesignCatalogue.ShopShelves"/> not in its own), and what stood on them. A null kind keeps nothing.
        /// </summary>
        public static ShowroomCut StockCut(List<DesignItem> items, ShopKind shop)
        {
            if (shop == null)
            {
                return new ShowroomCut();
            }
            HashSet<string> own = Build(shop.Shelves), leads = Build(shop.Leads);
            ShowroomCut cut = Cut(items, NoLot, name => !ShopShelves.Contains(name) || own.Contains(name));
            for (int i = 0; i < items.Count; i++)
            {
                if (cut.Keep[i])
                {
                    cut.Points += ShopTills.Contains(items[i].Name) ? 1 : 0;
                    cut.Leads += leads.Contains(items[i].Name) ? 1 : 0;
                }
                else if (items[i].Name != null && ShopShelves.Contains(items[i].Name) && !own.Contains(items[i].Name))
                {
                    cut.Dropped++;
                }
            }
            cut.Restock = new string[shop.Restock.Length];
            for (int i = 0; i < shop.Restock.Length; i++)
            {
                cut.Restock[i] = DesignCatalogue.ItemPrefix + shop.Restock[i];
            }
            return cut;
        }

        /// <summary>What a shelf set to stock <paramref name="item"/> stocks in a cut design: its restocked good, or the same.</summary>
        public static string Restocked(ShowroomCut cut, string item)
        {
            for (int i = 0; cut != null && i + 1 < cut.Restock.Length; i += 2)
            {
                if (cut.Restock[i] == item)
                {
                    return cut.Restock[i + 1];
                }
            }
            return item;
        }

        private static readonly List<FloorBox> NoLot = new List<FloorBox>(0);

        private static ShowroomCut Cut(List<DesignItem> items, List<FloorBox> lot, Func<string, bool> keeps)
        {
            var cut = new ShowroomCut();
            var byId = new Dictionary<string, DesignItem>(items.Count, StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                if (!string.IsNullOrEmpty(items[i].Id) && !byId.ContainsKey(items[i].Id))
                {
                    byId.Add(items[i].Id, items[i]);
                }
            }
            for (int i = 0; i < items.Count; i++)
            {
                DesignItem item = items[i];
                if (string.IsNullOrEmpty(item.Id) || byId[item.Id] != item || string.IsNullOrEmpty(item.Name) || !keeps(item.Name) || Stray(item))
                {
                    continue;
                }
                if (item.Y < DesignCatalogue.FloorTop && InLot(lot, item.X, item.Z))
                {
                    cut.InLot++;
                    continue;
                }
                cut.Kept.Add(item.Id);
            }
            // Whatever stands on something left out goes with it, however deep the stack.
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < items.Count; i++)
                {
                    DesignItem item = items[i];
                    if (item.Id != null && !string.IsNullOrEmpty(item.Parent) && cut.Kept.Contains(item.Id) && !cut.Kept.Contains(item.Parent))
                    {
                        cut.Kept.Remove(item.Id);
                        changed = true;
                    }
                }
            }
            cut.Keep = new bool[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                DesignItem item = items[i];
                if (item.Id == null || !cut.Kept.Contains(item.Id) || byId[item.Id] != item)
                {
                    continue;
                }
                cut.Keep[i] = true;
                string bare = item.Name.StartsWith(DesignCatalogue.ItemPrefix, StringComparison.Ordinal) ? item.Name.Substring(DesignCatalogue.ItemPrefix.Length) : item.Name;
                cut.Furniture += bare.IndexOf(DesignCatalogue.LampMark, StringComparison.Ordinal) < 0 ? 1 : 0;
                cut.Tills += bare == DesignCatalogue.Till ? 1 : 0;
                cut.Toilets += bare == DesignCatalogue.Toilet ? 1 : 0;
                cut.Seats += Seats.Contains(item.Name) ? 1 : 0;
            }
            return cut;
        }

        private static bool InLot(List<FloorBox> lot, int x, int z)
        {
            for (int i = 0; i < lot.Count; i++)
            {
                if (lot[i].Holds(x, z))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A Car Showroom's designs for one size, as places in <paramref name="sources"/>: the game's dealerships first,
        /// then those with a till, then the rest; valid, one of each name, at most <see cref="DesignCatalogue.ShowroomMost"/>.
        /// </summary>
        public static void Showrooms(List<DesignSource> sources, List<ShowroomCut> cuts, List<int> into)
        {
            into.Clear();
            var named = new List<DesignSource>(DesignCatalogue.ShowroomMost);
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < sources.Count && into.Count < DesignCatalogue.ShowroomMost; i++)
                {
                    ShowroomCut cut = cuts[i];
                    bool dealer = sources[i].Root == DonorRoot.Layouts;
                    bool fits = pass == 0 ? dealer : !dealer && (pass == 1 ? cut != null && cut.Tills > 0 : cut != null && cut.Tills == 0);
                    if (!fits || cut == null || !cut.Valid || Named(named, sources[i].Name))
                    {
                        continue;
                    }
                    named.Add(sources[i]);
                    into.Add(i);
                }
            }
        }

        /// <summary>
        /// A bank branch's designs for one size, as places in <paramref name="sources"/>: those with seats first; each with a
        /// till, one of each name, at most <see cref="DesignCatalogue.BranchMost"/>.
        /// </summary>
        public static void Branches(List<DesignSource> sources, List<ShowroomCut> cuts, List<int> into)
        {
            into.Clear();
            var named = new List<DesignSource>(DesignCatalogue.BranchMost);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < sources.Count && into.Count < DesignCatalogue.BranchMost; i++)
                {
                    ShowroomCut cut = cuts[i];
                    if (cut == null || !cut.ValidBranch || (pass == 0) != (cut.Seats > 0) || Named(named, sources[i].Name))
                    {
                        continue;
                    }
                    named.Add(sources[i]);
                    into.Add(i);
                }
            }
        }

        /// <summary>
        /// A new kind of shop's designs for one size, as places in <paramref name="sources"/>: each with a point of sale and
        /// something showing a primary good, one of each name, at most <see cref="DesignCatalogue.ShopMost"/>, in the donors' order.
        /// </summary>
        public static void Stocks(List<DesignSource> sources, List<ShowroomCut> cuts, List<int> into)
        {
            into.Clear();
            var named = new List<DesignSource>(DesignCatalogue.ShopMost);
            for (int i = 0; i < sources.Count && into.Count < DesignCatalogue.ShopMost; i++)
            {
                ShowroomCut cut = cuts[i];
                if (cut == null || !cut.ValidShop || Named(named, sources[i].Name))
                {
                    continue;
                }
                named.Add(sources[i]);
                into.Add(i);
            }
        }

        /// <summary>FNV-1a over the rules' version and each source's path, length and time: the same stamp, the same designs.</summary>
        public static string Stamp(List<DesignSource> sources)
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, "rules=" + DesignCatalogue.RulesVersion.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < sources.Count; i++)
            {
                DesignSource s = sources[i];
                h = Mix(h, s.Path + "|" + s.Length.ToString(CultureInfo.InvariantCulture) + "|" + s.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static ulong Mix(ulong h, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                h ^= text[i];
                h *= 1099511628211UL;
            }
            h ^= '\n';
            h *= 1099511628211UL;
            return h;
        }

        /// <summary>The folder under the installers' folder a kind's design for a size goes in ("Office/C1/gamestudio").</summary>
        public static string Folder(string buildingFolder, string size, DesignKind kind)
        {
            return buildingFolder + "/" + size + "/" + FolderOf(kind.Type);
        }

        /// <summary>A path the mod may have written and so may delete: building/size/type/name.json of its own types, never climbing out.</summary>
        public static bool Ours(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf("..", StringComparison.Ordinal) >= 0 || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0
                || !path.EndsWith(DesignCatalogue.Extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string[] parts = path.Split('/');
            if (parts.Length != 4 || !IsSize(parts[1]) || parts[3].Length <= DesignCatalogue.Extension.Length)
            {
                return false;
            }
            for (int k = 0; k < DesignCatalogue.Kinds.Length; k++)
            {
                DesignKind kind = DesignCatalogue.Kinds[k];
                if (parts[2] == FolderOf(kind.Type) && string.Equals(parts[0], FolderOf(kind.Building), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// The list of the files the mod put in the installers' folder: "v=1", "stamp=...", then "path|length" a line, sorted.
    /// Only a file on it is ever replaced or deleted; any other is somebody else's. An unreadable list holds nothing.
    /// </summary>
    public sealed class DesignManifest
    {
        public const int Version = 1;

        public string Stamp = string.Empty;
        public readonly SortedDictionary<string, long> Files = new SortedDictionary<string, long>(StringComparer.Ordinal);

        public bool Holds(string path)
        {
            return path != null && Files.ContainsKey(path);
        }

        public string Write()
        {
            var text = new StringBuilder(64 + 64 * Files.Count);
            text.Append("v=").Append(Version.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("stamp=").Append(Stamp ?? string.Empty).Append('\n');
            foreach (KeyValuePair<string, long> file in Files)
            {
                text.Append(file.Key).Append('|').Append(file.Value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>Empty for no text, a damaged one or another version's; paths not the mod's are dropped.</summary>
        public static DesignManifest Read(string text)
        {
            var list = new DesignManifest();
            if (string.IsNullOrEmpty(text))
            {
                return list;
            }
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            if (lines.Length < 2 || lines[0] != "v=" + Version.ToString(CultureInfo.InvariantCulture) || !lines[1].StartsWith("stamp=", StringComparison.Ordinal))
            {
                return list;
            }
            list.Stamp = lines[1].Substring("stamp=".Length);
            for (int i = 2; i < lines.Length; i++)
            {
                int bar = lines[i].LastIndexOf('|');
                if (bar <= 0)
                {
                    continue;
                }
                string path = lines[i].Substring(0, bar);
                if (DesignRules.Ours(path) && long.TryParse(lines[i].Substring(bar + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long length) && length >= 0)
                {
                    list.Files[path] = length;
                }
            }
            return list;
        }
    }
}
