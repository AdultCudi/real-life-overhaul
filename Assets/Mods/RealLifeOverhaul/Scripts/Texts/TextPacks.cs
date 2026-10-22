using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul.Texts
{
    /// <summary>A pack found on disk: its folder and what its pack.txt says.</summary>
    public sealed class TextPack
    {
        public string Folder;
        public string Name;          // "RLO Texts", "RLO Texts Medium", "RLO Texts Lite", "RLO Texts Large"
        public string Model;         // the .gguf, full path
        public string Exe;           // llama-server.exe, full path
        public int Rank;             // the pack's tier, the better model the higher (Lite 1, Medium 2, the full pack 3, Large 4)
        public int Version;
        public int Data = TextIntents.DataV2;   // the training data its model learnt ("data=3"); a pack.txt without the line is v2's
        public long ModelBytes;
        public string[] Languages = { TextLanguages.English };   // the languages it was trained and passed in; English alone without the line
    }

    /// <summary>A device llama.cpp can run on, as its --list-devices says it.</summary>
    public struct TextDevice
    {
        public string Name;          // "Vulkan0"
        public string Label;         // "NVIDIA GeForce RTX 5070 Ti"
        public int TotalMiB, FreeMiB;
    }

    /// <summary>
    /// Finding a pack (a folder with a pack.txt) and choosing where it runs. A pack is run only when its exe and model lie
    /// inside its folder and its runtime is exactly llama.cpp's pinned files (<see cref="Verify"/>). Game-free.
    /// COST: a listing of a few folders once a session, off the game's thread.
    /// </summary>
    public static class TextPacks
    {
        public const string File = "pack.txt";
        public const string Fixed = "RLO Texts";       // the folder under the game's data folder
        public const string Announced = "RealLifeOverhaul.TextPacks";   // the process's data slot the pack's mod writes its folder into, '|' between several
        public const int SpareMiB = 1024;               // a GPU is ample with twice the model and this much more free
        public const int RankMost = 2;                  // the "rank" line's most, which older RLO versions refuse past: none may claim more
        public const int TierMost = 4;                  // the "tier" line's most: none may claim more
        public const int LargeTier = 4;                 // RLO Texts Large: an RLO before data v6 (most tier 3) takes it for no pack

        /// <summary>
        /// A pack's tier: its "tier" line, else its "rank" line as the first packs wrote it; 0 when neither is known. Packs
        /// keep a rank of 1 or 2 for older RLO versions, which refuse more.
        /// </summary>
        public static int Tier(int rank, int tier, bool hasRank, bool hasTier)
        {
            if (hasRank && (rank < 1 || rank > RankMost))
            {
                return 0;
            }
            if (hasTier)
            {
                return tier >= 1 && tier <= TierMost ? tier : 0;
            }
            return !hasRank ? 0 : rank == RankMost ? FullTier : rank;   // a pack from before the tier line: the full pack at most, never Large
        }

        public const int FullTier = 3;                  // the full pack, RLO Texts: what a rank of 2 without a tier line is

        /// <summary>RLO's own pack items on the Workshop, the only Workshop folders ever looked in; an empty id is skipped.</summary>
        public static readonly string[] WorkshopItems =
        {
            "3820711613",   // RLO Texts
            "3820712020",   // RLO Texts Lite
            "3820776564",   // RLO Texts Medium
            "3824106911"    // RLO Texts Large
        };

        /// <summary>True once RLO Texts Large has its Workshop id: only then is it offered.</summary>
        public static bool LargePublished => !string.IsNullOrEmpty(WorkshopItems[3]) && IsDigits(WorkshopItems[3]);

        public static List<string> WorkshopPacks(string workshopContent, string[] items)
        {
            var list = new List<string>(2);
            for (int i = 0; workshopContent != null && i < items.Length; i++)
            {
                if (!string.IsNullOrEmpty(items[i]) && IsDigits(items[i]))
                {
                    list.Add(Path.Combine(workshopContent, items[i]));
                }
            }
            return list;
        }

        private static bool IsDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                {
                    return false;
                }
            }
            return true;
        }

        public static bool Inside(string folder, string path)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(path))
            {
                return false;
            }
            string root = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Null when a pack's runtime is exactly llama.cpp's pinned files, nothing else; else why not. Reads about 82 MB, off the game's thread.</summary>
        public static string Verify(TextPack pack, string[] pinned)
        {
            string runtime = Path.GetDirectoryName(pack.Exe);
            if (!Inside(pack.Folder, pack.Exe) || !string.Equals(Path.GetFileName(pack.Exe), pinned[0], StringComparison.OrdinalIgnoreCase))
            {
                return "the server is not where the pack keeps it";
            }
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SHA256 sha = SHA256.Create())
            {
                for (int i = 0; i + 1 < pinned.Length; i += 2)
                {
                    allowed.Add(pinned[i]);
                    string file = Path.Combine(runtime, pinned[i]);
                    if (!System.IO.File.Exists(file))
                    {
                        return pinned[i] + " is missing";
                    }
                    using (FileStream s = System.IO.File.OpenRead(file))
                    {
                        if (!string.Equals(Hex(sha.ComputeHash(s)), pinned[i + 1], StringComparison.Ordinal))
                        {
                            return pinned[i] + " is not llama.cpp's own";
                        }
                    }
                }
            }
            foreach (string file in Directory.GetFiles(runtime))
            {
                string ext = Path.GetExtension(file);
                if ((string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase))
                    && !allowed.Contains(Path.GetFileName(file)))
                {
                    return Path.GetFileName(file) + " is not one of llama.cpp's files";
                }
            }
            return null;
        }

        private static string Hex(byte[] bytes)
        {
            var c = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                c[i * 2] = "0123456789abcdef"[bytes[i] >> 4];
                c[i * 2 + 1] = "0123456789abcdef"[bytes[i] & 15];
            }
            return new string(c);
        }

        /// <summary>Reads a pack.txt; null if it is not a usable pack (no model or no exe on disk).</summary>
        public static TextPack Read(string folder)
        {
            string path = Path.Combine(folder, File);
            if (!System.IO.File.Exists(path))
            {
                return null;
            }
            var pack = new TextPack { Folder = folder };
            int rank = 0, tier = 0;
            bool hasRank = false, hasTier = false;
            foreach (string raw in System.IO.File.ReadAllLines(path))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.Length == 0 || line[0] == '#' || eq <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "name": pack.Name = value; break;
                    case "model": pack.Model = Path.GetFullPath(Path.Combine(folder, value)); break;
                    case "exe": pack.Exe = Path.GetFullPath(Path.Combine(folder, value)); break;
                    case "rank": hasRank = true; int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rank); break;
                    case "tier": hasTier = true; int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out tier); break;
                    case "version": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out pack.Version); break;
                    case "data": pack.Data = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int data) && data >= TextIntents.DataV2 ? data : TextIntents.DataV2; break;
                    case "languages": pack.Languages = TextLanguages.Parse(value); break;
                }
            }
            pack.Rank = Tier(rank, tier, hasRank, hasTier);
            if (pack.Model == null || pack.Exe == null || !Inside(folder, pack.Model) || !Inside(folder, pack.Exe) || pack.Rank < 1
                || !System.IO.File.Exists(pack.Model) || !System.IO.File.Exists(pack.Exe))
            {
                return null;   // a path out of its own folder, a rank no pack of RLO's has, or files missing
            }
            pack.Name = pack.Name ?? Path.GetFileName(folder);
            pack.ModelBytes = new FileInfo(pack.Model).Length;
            return pack;
        }

        /// <summary>Every pack in these places, each a pack folder or a folder of pack folders, the best first.</summary>
        public static List<TextPack> Find(IEnumerable<string> places)
        {
            var found = new List<TextPack>(4);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string place in places)
            {
                if (string.IsNullOrEmpty(place) || !Directory.Exists(place))
                {
                    continue;
                }
                Add(place, found, seen);
                string[] under;
                try
                {
                    under = Directory.GetDirectories(place);
                }
                catch (Exception)
                {
                    continue;
                }
                for (int i = 0; i < under.Length; i++)
                {
                    Add(under[i], found, seen);
                }
            }
            found.Sort(Better);
            return found;
        }

        private static void Add(string folder, List<TextPack> found, HashSet<string> seen)
        {
            string full;
            try
            {
                full = Path.GetFullPath(folder);
            }
            catch (Exception)
            {
                return;
            }
            if (!seen.Add(full))
            {
                return;
            }
            try
            {
                TextPack pack = Read(full);
                if (pack != null)
                {
                    found.Add(pack);
                }
            }
            catch (Exception)
            {
                // an unreadable folder is not a pack
            }
        }

        private static int Better(TextPack a, TextPack b)
        {
            return a.Rank != b.Rank ? b.Rank.CompareTo(a.Rank) : b.Version.CompareTo(a.Version);
        }

        public static string[] Split(string announced)
        {
            return string.IsNullOrEmpty(announced) ? Array.Empty<string>() : announced.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>The game's Workshop content folder from its data folder (…/steamapps/common/Big Ambitions/Big Ambitions_Data); null if not under Steam.</summary>
        public static string WorkshopOf(string dataPath, string appId)
        {
            try
            {
                DirectoryInfo game = Directory.GetParent(dataPath);              // …/common/Big Ambitions
                DirectoryInfo common = game != null ? game.Parent : null;          // …/common
                DirectoryInfo apps = common != null ? common.Parent : null;        // …/steamapps
                return apps != null ? Path.Combine(apps.FullName, "workshop", "content", appId) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The devices in llama.cpp's --list-devices output ("  Vulkan0: NAME (16000 MiB, 15000 MiB free)").</summary>
        public static List<TextDevice> Devices(string output)
        {
            var list = new List<TextDevice>(2);
            if (string.IsNullOrEmpty(output))
            {
                return list;
            }
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                int colon = line.IndexOf(':');
                int open = line.LastIndexOf('(');
                int close = line.LastIndexOf(')');
                if (colon <= 0 || open < colon || close < open || line.IndexOf(' ') < colon)
                {
                    continue;
                }
                string[] mib = line.Substring(open + 1, close - open - 1).Split(',');
                if (mib.Length != 2 || !Mib(mib[0], out int total) || !Mib(mib[1], out int free))
                {
                    continue;
                }
                list.Add(new TextDevice { Name = line.Substring(0, colon), Label = line.Substring(colon + 1, open - colon - 1).Trim(), TotalMiB = total, FreeMiB = free });
            }
            return list;
        }

        private static bool Mib(string part, out int value)
        {
            string s = part.Trim();
            int space = s.IndexOf(' ');
            return int.TryParse(space > 0 ? s.Substring(0, space) : s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>True if a GPU with this much free memory has ample room for a model of this size beside the game.</summary>
        public static bool Ample(int freeMiB, long modelBytes)
        {
            long modelMiB = modelBytes / (1024 * 1024);
            return freeMiB >= modelMiB * 2 + SpareMiB;
        }

        /// <summary>
        /// True for graphics built into the processor, by its name: such a chip counts the PC's memory as free, so it looks
        /// roomier than a card. Intel's Arc chips are named "Arc 140V", its cards "Arc A770".
        /// </summary>
        public static bool BuiltIn(string label)
        {
            string s = (label ?? string.Empty).ToLowerInvariant();
            if (s.Contains("intel"))
            {
                return !s.Contains("arc") || Regex.IsMatch(s, @"arc(\(tm\))?\s+\d{3}[a-z]\b");
            }
            if (s.Contains("radeon"))
            {
                return Regex.IsMatch(s, @"radeon(\(tm\))?\s+(graphics|vega\b|\d{3}m\b)");
            }
            return false;
        }

        /// <summary>The device to run on, null for the processor; <paramref name="choice"/> 0 Auto (a GPU only if ample), 1 the processor, 2 the GPU.</summary>
        public static string Choose(int choice, List<TextDevice> devices, long modelBytes)
        {
            if (choice == 1 || devices == null || devices.Count == 0)
            {
                return null;
            }
            TextDevice best = devices[0];
            for (int i = 1; i < devices.Count; i++)
            {
                bool card = !BuiltIn(devices[i].Label), bestCard = !BuiltIn(best.Label);
                if (card != bestCard ? card : devices[i].FreeMiB > best.FreeMiB)
                {
                    best = devices[i];
                }
            }
            return choice == 2 || Ample(best.FreeMiB, modelBytes) ? best.Name : null;
        }

        /// <summary>The graphics card of these devices with the most free memory (never graphics built into the processor); false with none.</summary>
        public static bool Card(List<TextDevice> devices, out TextDevice card)
        {
            card = default(TextDevice);
            bool found = false;
            for (int i = 0; devices != null && i < devices.Count; i++)
            {
                if (!BuiltIn(devices[i].Label) && (!found || devices[i].FreeMiB > card.FreeMiB))
                {
                    card = devices[i];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// The best pack to run, RLO Texts Large only on a card with the memory for it where it was not too slow before;
        /// <paramref name="why"/> says why Large was passed over. Null with no pack.
        /// </summary>
        public static TextPack Pick(List<TextPack> verified, List<TextDevice> devices, int choice, string largeSlow, out string why)
        {
            why = null;
            for (int i = 0; verified != null && i < verified.Count; i++)
            {
                TextPack p = verified[i];
                if (p.Rank != LargeTier)
                {
                    return p;
                }
                if (choice == 1)
                {
                    why = why ?? "RLO Texts Large is not run on the processor (the option says the processor)";
                    continue;
                }
                if (!Card(devices, out TextDevice card) || !TextSpeed.LargeFits(card, p.ModelBytes))
                {
                    why = why ?? "RLO Texts Large needs a graphics card with " + (p.ModelBytes / (1024 * 1024) * 2 + SpareMiB) + " MB free; "
                          + (Card(devices, out TextDevice some) ? some.Label + " has " + some.FreeMiB + " MB" : "there is none");
                    continue;
                }
                if (largeSlow != null && largeSlow == card.Label)
                {
                    why = why ?? "RLO Texts Large was too slow on " + card.Label + " before";
                    continue;
                }
                return p;
            }
            return null;
        }

        /// <summary>Asks llama.cpp for its devices: a few seconds at most, once a session, off the game's thread.</summary>
        public static string ListDevices(string exe, int timeoutMs)
        {
            try
            {
                var info = new ProcessStartInfo(exe, "--list-devices")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(exe)
                };
                TextServer.Offline(info);
                using (Process p = Process.Start(info))
                {
                    if (p == null)
                    {
                        return null;
                    }
                    var output = p.StandardOutput.ReadToEndAsync();
                    var err = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        p.Kill();
                        return null;
                    }
                    return output.Result + "\n" + err.Result;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
