using System;
using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul
{
    /// <summary>What the player's own gig music folder is read through, so the rules below run offline against a made-up disc.</summary>
    public interface IGigMusicFiles
    {
        bool FolderExists(string folder);

        /// <summary>The full paths of the files directly in a folder, in no order; empty when the folder cannot be read.</summary>
        string[] FilesIn(string folder);

        /// <summary>A file's size in bytes, or -1 when it cannot be read.</summary>
        long SizeOf(string path);

        bool FileExists(string path);

        bool CreateFolder(string folder);

        bool WriteText(string path, string text);
    }

    /// <summary>
    /// The kinds of gig sound the mod plays, one folder each under the mod's GigMusic folder: the busker's music, and the short
    /// effects a gig plays at a moment (the cart's customer calling out and its till, the taxi's car door).
    /// </summary>
    public enum GigSound
    {
        Busking = 0,
        CartCalls = 1,
        CartTill = 2,
        TaxiDoor = 3
    }

    /// <summary>The files taken from one folder, in the order they will play, and what was left out and why.</summary>
    public sealed class GigMusicSet
    {
        public string[] Paths = new string[0];
        public readonly List<string> Left = new List<string>();   // "name: reason", for the log

        public int Count => Paths.Length;
    }

    /// <summary>
    /// The player's own music for gigs, as rules with no game types, checked offline. The folder is read once as a gig starts:
    /// which files are taken (the same types the game's own custom radio reads, at most <see cref="MostFiles"/> of
    /// <see cref="MostMegabytes"/> MB each, checked from the file's size before anything is opened), in what order (by name, or
    /// shuffled by the save's seed so a save always hears the same mix), and whether the player's files or the game's own sound play.
    /// Nothing here runs per frame, and the time of a gig never depends on a file: the set's end cuts a file short with a fade.
    /// </summary>
    public static class GigMusicRules
    {
        public const string Root = "GigMusic";                 // under the mod's folder in the game's data folder
        public const string ReadmeName = "README.txt";
        public const int MostFiles = 24;                       // files taken from one folder: a set is minutes, and a folder of hundreds is a mistake
        public const int MostMegabytes = 40;                   // MB a file may be: about 40 minutes of mp3 at 128 kbps, 4 minutes of CD wav; streamed, so it is never held whole
        public const long MostBytes = MostMegabytes * 1024L * 1024L;
        public const int MostEffectFiles = 8;                  // files taken for a short effect: a few to pick among is plenty
        public const int MostEffectKilobytes = 1024;           // KB an effect file may be: a second or two of mp3, about 6 seconds of CD wav, held decoded for the gig
        public const long MostEffectBytes = MostEffectKilobytes * 1024L;
        public const float MostEffectSeconds = 8f;             // seconds an effect plays before it is cut: a file made longer is a mistake, and the gig never waits for it
        public const float FadeSeconds = 0.8f;                 // seconds a file is faded out at the set's end: short enough to be no wait, long enough for no click

        // The types the game's own custom radio reads (AudioFileFormatHelper.GetAudioTypeFromExtension).
        private static readonly string[] Types = { ".wav", ".ogg", ".aiff", ".aif", ".flac", ".mp3", ".mp2" };

        // The ones the game names as unsupported in its radio's list: said as such here too.
        private static readonly string[] NotRead = { ".m4a", ".m4b", ".aac", ".wma", ".opus", ".mid", ".midi" };

        /// <summary>The folder's name for a kind of gig sound: the gig's own name.</summary>
        public static string Name(GigSound kind)
        {
            switch (kind)
            {
                case GigSound.CartCalls: return "Cart/Calls";
                case GigSound.CartTill: return "Cart/Till";
                case GigSound.TaxiDoor: return "Taxi/Door";
                default: return "Busking";
            }
        }

        /// <summary>Whether a kind is a short effect played at a moment, not music played through a set.</summary>
        public static bool IsEffect(GigSound kind)
        {
            return kind != GigSound.Busking;
        }

        public static int MostFilesOf(GigSound kind)
        {
            return IsEffect(kind) ? MostEffectFiles : MostFiles;
        }

        public static long MostBytesOf(GigSound kind)
        {
            return IsEffect(kind) ? MostEffectBytes : MostBytes;
        }

        /// <summary>The size limit as the player is told it: MB for music, KB for an effect.</summary>
        public static string LimitText(GigSound kind)
        {
            return IsEffect(kind) ? MostEffectKilobytes + " KB" : MostMegabytes + " MB";
        }

        public static string FolderOf(string root, GigSound kind)
        {
            return Path.Combine(root, Name(kind));
        }

        /// <summary>Whether a file name is a type the game's own radio reads.</summary>
        public static bool IsMusic(string name)
        {
            string extension = Path.GetExtension(name ?? string.Empty).ToLowerInvariant();
            return Array.IndexOf(Types, extension) >= 0;
        }

        /// <summary>The file types taken, for the folder's note.</summary>
        public static string TypesText()
        {
            return string.Join(", ", Types);
        }

        /// <summary>
        /// The files taken from a folder, in play order: music types only, not hidden, not empty, not over the size limit; the first
        /// <see cref="MostFiles"/> by name; then shuffled by <paramref name="seed"/> if asked. Reads the folder's list and each size once.
        /// </summary>
        public static GigMusicSet Read(IGigMusicFiles files, string folder, bool shuffle, int seed)
        {
            return Read(files, folder, shuffle, seed, MostFiles, MostBytes);
        }

        /// <summary>The same for a kind of gig sound, with the limits of its kind: music or a short effect.</summary>
        public static GigMusicSet Read(IGigMusicFiles files, GigSound kind, string root, bool shuffle, int seed)
        {
            return Read(files, FolderOf(root, kind), shuffle, seed, MostFilesOf(kind), MostBytesOf(kind));
        }

        private static GigMusicSet Read(IGigMusicFiles files, string folder, bool shuffle, int seed, int mostFiles, long mostBytes)
        {
            var set = new GigMusicSet();
            if (files == null || !files.FolderExists(folder))
            {
                return set;
            }
            string[] all = files.FilesIn(folder) ?? new string[0];
            var kept = new List<string>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                string name = Path.GetFileName(all[i]);
                if (string.IsNullOrEmpty(name) || name[0] == '.' || name[0] == '~' || string.Equals(name, ReadmeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!IsMusic(name))
                {
                    if (Array.IndexOf(NotRead, Path.GetExtension(name).ToLowerInvariant()) >= 0)
                    {
                        set.Left.Add(name + ": the game does not read this type, as with its own radio");
                    }
                    continue;
                }
                long size = files.SizeOf(all[i]);
                if (size <= 0)
                {
                    set.Left.Add(name + ": empty or cannot be read");
                }
                else if (size > mostBytes)
                {
                    set.Left.Add(name + ": over " + (mostBytes >> 10 >= MostMegabytes * 1024L ? MostMegabytes + " MB" : (mostBytes >> 10) + " KB"));
                }
                else
                {
                    kept.Add(all[i]);
                }
            }
            kept.Sort(ByName);
            if (kept.Count > mostFiles)
            {
                for (int i = mostFiles; i < kept.Count; i++)
                {
                    set.Left.Add(Path.GetFileName(kept[i]) + ": past the " + mostFiles + " files taken");
                }
                kept.RemoveRange(mostFiles, kept.Count - mostFiles);
            }
            set.Paths = kept.ToArray();
            if (shuffle)
            {
                Shuffle(set.Paths, seed);
            }
            return set;
        }

        // By file name, upper and lower case alike; the full path settles two that differ only in case.
        private static int ByName(string a, string b)
        {
            int c = string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.CompareOrdinal(a, b);
        }

        /// <summary>
        /// A shuffle that is the same for the same seed on every machine and every run: a Fisher-Yates over a small generator of its
        /// own, never the game's random sequence.
        /// </summary>
        public static void Shuffle(string[] paths, int seed)
        {
            uint state = unchecked((uint)seed * 2654435761u + 0x9E3779B9u);
            if (state == 0u)
            {
                state = 1u;
            }
            for (int i = paths.Length - 1; i > 0; i--)
            {
                state ^= state << 13;   // xorshift32
                state ^= state >> 17;
                state ^= state << 5;
                int j = (int)(state % (uint)(i + 1));
                string held = paths[i];
                paths[i] = paths[j];
                paths[j] = held;
            }
        }

        /// <summary>The seed for a gig's shuffle: the save's own number and the kind of gig, so a save hears one mix of each folder.</summary>
        public static int SeedFor(int saveSeed, GigSound kind)
        {
            unchecked
            {
                return (int)(((uint)saveSeed * 73856093u) ^ ((uint)(kind + 1) * 19349663u)) & 0x7fffffff;
            }
        }

        /// <summary>Whether the player's files play: the option on and at least one file taken. Otherwise the game's own sound plays as before.</summary>
        public static bool UsesOwn(bool option, int taken)
        {
            return option && taken > 0;
        }

        /// <summary>Whether the player is told, once, that the option is on and the folder has nothing to play.</summary>
        public static bool SaysEmpty(bool option, int taken, bool said)
        {
            return option && taken <= 0 && !said;
        }

        /// <summary>The file an effect plays the <paramref name="n"/>th time (from 0): each in turn, round and round, in the order the read gave (by name or by the save's seed).</summary>
        public static int EffectIndex(int n, int count)
        {
            return count <= 0 ? -1 : (int)((uint)n % (uint)count);
        }

        /// <summary>The file after this one, wrapping round; -1 with nothing to play.</summary>
        public static int Following(int index, int count)
        {
            return count <= 0 ? -1 : (index + 1) % count;
        }

        /// <summary>A file that is the only one taken plays over again; with more, the next one plays.</summary>
        public static bool LoopsAlone(int count)
        {
            return count == 1;
        }

        /// <summary>The share of full volume a file has <paramref name="elapsed"/> seconds into the fade at the set's end.</summary>
        public static float FadeGain(float elapsed)
        {
            if (elapsed <= 0f)
            {
                return 1f;
            }
            return elapsed >= FadeSeconds ? 0f : 1f - elapsed / FadeSeconds;
        }

        /// <summary>
        /// Makes each gig's folder, and the note that says what goes where, if they are not there; never touches a file already there.
        /// True when the folders are in place.
        /// </summary>
        public static bool Prepare(IGigMusicFiles files, string root, string readme)
        {
            bool ready = files.CreateFolder(root);
            foreach (GigSound kind in Enum.GetValues(typeof(GigSound)))
            {
                ready &= files.CreateFolder(FolderOf(root, kind));
            }
            string note = Path.Combine(root, ReadmeName);
            if (ready && !files.FileExists(note))
            {
                files.WriteText(note, readme);
            }
            return ready;
        }
    }
}
