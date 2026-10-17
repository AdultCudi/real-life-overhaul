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
        public string Name;          // "RLO Texts", "RLO Texts Medium", "RLO Texts Lite"
        public string Model;         // the .gguf, full path
        public string Exe;           // llama-server.exe, full path
        public int Rank;             // the better model the higher: the pack's tier (Lite 1, Medium 2, the full pack 3)
        public int Version;
        public int Data = TextIntents.DataV2;   // the training data its model learnt ("data=3"); a pack.txt without the line is v2's
        public long ModelBytes;
    }

    /// <summary>A device llama.cpp can run on, as its --list-devices says it.</summary>
    public struct TextDevice
    {
        public string Name;          // "Vulkan0"
        public string Label;         // "NVIDIA GeForce RTX 5070 Ti"
        public int TotalMiB, FreeMiB;
    }

    /// <summary>
    /// Finding a pack and choosing where it runs. A pack is a folder with a pack.txt (name, model, exe, rank, tier, version and
    /// data, each "key=value" on a line; paths relative to the folder). Data is the version of the training data its model
    /// learnt: 2 for the first packs (no data line), 3 for the retrained ones; the mod asks a pack only the intents of its
    /// data version or older (TextIntent.Data, TextsHub.Data), so a v2 pack is never asked what it never learnt. The mod
    /// looks, once a session:
    ///
    ///   the fixed folder   "RLO Texts" in the game's own data folder (LocalLow/Hovgaard Games/Big Ambitions), a pack
    ///                      in it or in any folder under it (a pack put there by hand)
    ///   the Workshop       only the folders of RLO's own pack items (<see cref="WorkshopItems"/>), switched on in the
    ///                      game's mod list or not; never any other item's
    ///   announced          the folders the pack's own small mod told the game's process when the game loaded it
    ///   ModsLocal          a pack placed as a local mod
    ///
    /// SAFETY. A pack's exe and model must lie inside its own folder (a path that leaves it is refused), its rank is one
    /// of the known packs' (see <see cref="TierMost"/>), and its runtime folder must hold exactly llama.cpp's files as
    /// pinned in <see cref="TextRuntime"/>, byte for byte (SHA-256), and no other .exe or .dll (<see cref="Verify"/>, off
    /// the game's thread, once a session, before anything of the pack is started). A pack that fails is not run.
    ///
    /// The best is the highest tier (<see cref="Tier"/>), then the newest version. Device: Auto runs on the processor unless a GPU has ample
    /// free memory (<see cref="Ample"/>), asked once of llama.cpp itself (--list-devices). Game-free (EconomyCheck,
    /// TextsChecks.cs). COST: a listing of a few folders once a session, off the game's thread.
    /// </summary>
    public static class TextPacks
    {
        public const string File = "pack.txt";
        public const string Fixed = "RLO Texts";       // the folder under the game's data folder
        public const string Announced = "RealLifeOverhaul.TextPacks";   // the process's data slot the pack's mod writes its folder into ('|' between several)
        public const int SpareMiB = 1024;               // a GPU is ample with twice the model and this much more free
        public const int RankMost = 2;                  // the "rank" line's most, as RLO before 0.29.1 reads it (Lite 1; Medium and the full pack 2): none may claim more
        public const int TierMost = 3;                  // the "tier" line's most (Lite 1, Medium 2, the full pack 3): none may claim more

        /// <summary>
        /// A pack's place among RLO's packs from its pack.txt: the "tier" line (1 Lite, 2 Medium, 3 the full pack) when
        /// there is one, else the "rank" line as the first packs wrote it (1 Lite, 2 the full pack, so 3); 0 when neither
        /// is one RLO knows (not a pack). The packs keep a "rank" of 1 or 2 beside their tier because RLO before 0.29.1
        /// refuses a rank above 2: it orders them by rank then version, so the full pack (rank 2, the newer version) still
        /// comes before Medium (rank 2) and Medium before Lite (rank 1).
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
            return !hasRank ? 0 : rank == RankMost ? TierMost : rank;
        }

        /// <summary>
        /// RLO's own pack items on the Workshop (RLO Texts, RLO Texts Lite, RLO Texts Medium), by their item ids. Only these
        /// Workshop folders are ever looked in; an empty id is skipped (<see cref="WorkshopPacks"/>).
        /// </summary>
        public static readonly string[] WorkshopItems =
        {
            "3820711613",   // RLO Texts
            "3820712020",   // RLO Texts Lite
            "3820776564"    // RLO Texts Medium
        };

        /// <summary>The folders of RLO's own pack items under the game's Workshop content folder (none while their ids are empty).</summary>
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

        /// <summary>True if a full path lies inside a folder (the folder and a separator start it).</summary>
        public static bool Inside(string folder, string path)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(path))
            {
                return false;
            }
            string root = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether a pack's runtime is exactly llama.cpp's pinned files: every one there with its SHA-256, and no other .exe or
        /// .dll in its folder. Null when it is; else why not. Reads about 82 MB: off the game's thread, once a session.
        /// </summary>
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
                }
            }
            pack.Rank = Tier(rank, tier, hasRank, hasTier);
            if (pack.Model == null || pack.Exe == null || !Inside(folder, pack.Model) || !Inside(folder, pack.Exe) || pack.Rank < 1
                || !System.IO.File.Exists(pack.Model) || !System.IO.File.Exists(pack.Exe))
            {
                return null;   // (a path out of its own folder, a rank no pack of RLO's has, or files missing: not a pack)
            }
            pack.Name = pack.Name ?? Path.GetFileName(folder);
            pack.ModelBytes = new FileInfo(pack.Model).Length;
            return pack;
        }

        /// <summary>Every pack in these places (each a pack folder, or a folder of pack folders), the best first.</summary>
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
                // (an unreadable folder is not a pack)
            }
        }

        private static int Better(TextPack a, TextPack b)
        {
            return a.Rank != b.Rank ? b.Rank.CompareTo(a.Rank) : b.Version.CompareTo(a.Version);
        }

        /// <summary>The folders a data slot of the process holds ('|' between several).</summary>
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

        // ---------------------------------------------------------------- the device

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
        /// True for graphics built into the processor, by the name llama.cpp gives it (its list does not say). Such a chip
        /// counts the PC's own memory as its free memory, so it can look roomier than a real graphics card: AMD's
        /// "Radeon(TM) Graphics", "Radeon 780M Graphics" and Vega, Intel's UHD, Iris and HD Graphics and its Arc chips
        /// named by a number with a letter after it ("Arc 140V"), not its cards ("Arc A770", "Arc B580").
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

        /// <summary>
        /// The device to run on: null for the processor. <paramref name="choice"/>: 0 Auto (a GPU only if ample), 1 the
        /// processor, 2 the GPU, ample or not. Of several, a graphics card before graphics built into the processor
        /// (<see cref="BuiltIn"/>), then the one with the most free memory.
        /// </summary>
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

        /// <summary>Asks llama.cpp for its devices (a few seconds at most, once a session, off the game's thread).</summary>
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
