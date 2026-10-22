using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The photos on disk, by <see cref="PhotoStoreRules"/>: a folder listed, a new photo's name, deletions, the wallpaper's
    /// and favourites' files, and the look over deleted saves' photos. Game-free and checked offline on folders of the
    /// checks' own. Runs on worker threads only, as a disk can be slow; a file that cannot be read or deleted is left as it
    /// is, and nothing here throws.
    /// </summary>
    public static class PhotoStore
    {
        /// <summary>A save's folder of photos under the root (persistentDataPath/RealLifeOverhaul/Photos).</summary>
        public static string Folder(string root, string saveId)
        {
            return Path.Combine(root, PhotoStoreRules.FolderName(saveId));
        }

        /// <summary>The small copy's file of a photo, where Flare reads it.</summary>
        public static string ThumbOf(string photo)
        {
            return PhotoThumbs.FileOf(photo);
        }

        // The failures caught here: the disk's and the rights'.
        private static bool Disk(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException;
        }

        /// <summary>A folder's photos in the Gallery's order, into a list emptied first; false if the folder cannot be read.</summary>
        public static bool List(string folder, List<PhotoEntry> into)
        {
            into.Clear();
            try
            {
                if (!Directory.Exists(folder))
                {
                    return true;   // no photo yet
                }
                string[] files = Directory.GetFiles(folder, "*" + PhotoStoreRules.Extension, SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    var entry = new PhotoEntry();
                    if (PhotoStoreRules.Parse(Path.GetFileName(files[i]), entry))
                    {
                        into.Add(entry);
                    }
                }
                PhotoStoreRules.Order(into);
                return true;
            }
            catch (Exception e) when (Disk(e))
            {
                return false;
            }
        }

        /// <summary>
        /// The path a new photo is written to, numbered past any of the same second. Makes the folder and writes the save's id
        /// beside the photos for the look over deleted saves.
        /// </summary>
        public static string NewPath(string folder, string saveId, DateTime taken, bool selfie, int day, int hour)
        {
            Directory.CreateDirectory(folder);
            MarkSave(folder, saveId);
            for (int number = 1; number < 1000; number++)
            {
                string path = Path.Combine(folder, PhotoStoreRules.NameFor(taken, selfie, day, hour, number));
                if (!File.Exists(path))
                {
                    return path;
                }
            }
            return Path.Combine(folder, PhotoStoreRules.NameFor(taken, selfie, day, hour, 1000 + Environment.TickCount % 1000));
        }

        /// <summary>
        /// Writes save.txt whole unless it already holds exactly this id, so a write cut short by a full disk is mended. False
        /// if it could not be written.
        /// </summary>
        public static bool MarkSave(string folder, string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                return false;
            }
            string mark = Path.Combine(folder, PhotoStoreRules.SaveFile);
            try
            {
                if (File.Exists(mark) && string.Equals(File.ReadAllText(mark).Trim(), saveId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception e) when (Disk(e))
            {
                // read as not there: written again below
            }
            byte[] bytes = Encoding.UTF8.GetBytes(saveId);
            return WriteWhole(mark, bytes, bytes.Length);
        }

        /// <summary>Writes under a name of its own, then moves into place, so the Gallery never lists half a file.</summary>
        public static bool WriteWhole(string path, byte[] bytes, int count)
        {
            string part = path + ".part";
            try
            {
                using (var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    file.Write(bytes, 0, count);
                }
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(part, path);
                return true;
            }
            catch (Exception e) when (Disk(e))
            {
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(part))
                    {
                        File.Delete(part);
                    }
                }
                catch (Exception)
                {
                    // a stray part file is never listed
                }
            }
        }

        /// <summary>Deletes a photo and its small copy; true if the photo is gone, whether or not it was there.</summary>
        public static bool Delete(string folder, string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }
            string photo = Path.Combine(folder, name);
            try
            {
                if (File.Exists(photo))
                {
                    File.Delete(photo);
                }
                string thumb = ThumbOf(photo);
                if (File.Exists(thumb))
                {
                    File.Delete(thumb);
                }
                return !File.Exists(photo);
            }
            catch (Exception e) when (Disk(e))
            {
                return false;
            }
        }

        /// <summary>The name of the photo set as the phone's wallpaper, or null.</summary>
        public static string Wallpaper(string folder)
        {
            try
            {
                string file = Path.Combine(folder, PhotoStoreRules.WallpaperFile);
                if (!File.Exists(file))
                {
                    return null;
                }
                string name = File.ReadAllText(file).Trim();
                return name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && File.Exists(Path.Combine(folder, name)) ? name : null;
            }
            catch (Exception e) when (Disk(e))
            {
                return null;
            }
        }

        /// <summary>Writes the wallpaper's photo, or takes the file away for null. False if it could not be.</summary>
        public static bool SetWallpaper(string folder, string name)
        {
            try
            {
                string file = Path.Combine(folder, PhotoStoreRules.WallpaperFile);
                if (string.IsNullOrEmpty(name))
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                    return true;
                }
                Directory.CreateDirectory(folder);
                byte[] bytes = Encoding.UTF8.GetBytes(name);
                return WriteWhole(file, bytes, bytes.Length);
            }
            catch (Exception e) when (Disk(e))
            {
                return false;
            }
        }

        /// <summary>The favourites read into a set emptied first; none if there is no file or it cannot be read.</summary>
        public static void Favourites(string folder, HashSet<string> into)
        {
            into.Clear();
            try
            {
                string file = Path.Combine(folder, GalleryRules.FavouritesFile);
                if (File.Exists(file))
                {
                    GalleryRules.ReadFavourites(File.ReadAllText(file), into);
                }
            }
            catch (Exception e) when (Disk(e))
            {
                into.Clear();
            }
        }

        /// <summary>Writes the favourites whole, or takes the file away for none. False if they could not be written.</summary>
        public static bool SetFavourites(string folder, ICollection<string> favourites)
        {
            try
            {
                string file = Path.Combine(folder, GalleryRules.FavouritesFile);
                if (favourites == null || favourites.Count == 0)
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                    return true;
                }
                Directory.CreateDirectory(folder);
                byte[] bytes = Encoding.UTF8.GetBytes(GalleryRules.WriteFavourites(favourites));
                return WriteWhole(file, bytes, bytes.Length);
            }
            catch (Exception e) when (Disk(e))
            {
                return false;
            }
        }

        /// <summary>
        /// Looks over every save's photos under the root: a save gone is marked, one back unmarked, one gone for the grace days
        /// deleted. The folder of the save being played is never touched. Returns the number of folders looked at.
        /// </summary>
        public static int Sweep(string root, string playing, Func<string, bool> savePresent, DateTime today, List<string> deleted, List<string> marked)
        {
            string[] folders;
            try
            {
                if (!Directory.Exists(root))
                {
                    return 0;
                }
                folders = Directory.GetDirectories(root);
            }
            catch (Exception e) when (Disk(e))
            {
                return 0;
            }
            int looked = 0;
            string played = playing != null ? PhotoStoreRules.FolderName(playing) : null;
            for (int i = 0; i < folders.Length; i++)
            {
                try
                {
                    looked += SweepOne(folders[i], played, savePresent, today, deleted, marked) ? 1 : 0;
                }
                catch (Exception e) when (Disk(e))
                {
                    // looked at again next time; the other folders now
                }
            }
            return looked;
        }

        // True if the folder is the Camera's and its save can be told. A folder whose save.txt is empty, or names an id of
        // another folder, is never judged gone.
        private static bool SweepOne(string folder, string played, Func<string, bool> savePresent, DateTime today, List<string> deleted, List<string> marked)
        {
            string name = Path.GetFileName(folder);
            if (played != null && string.Equals(name, played, StringComparison.Ordinal))
            {
                return false;
            }
            string idFile = Path.Combine(folder, PhotoStoreRules.SaveFile);
            if (!File.Exists(idFile))
            {
                return false;
            }
            string id = File.ReadAllText(idFile).Trim();
            if (id.Length == 0 || !string.Equals(PhotoStoreRules.FolderName(id), name, StringComparison.Ordinal))
            {
                return false;
            }
            string goneFile = Path.Combine(folder, PhotoStoreRules.GoneFile);
            bool isMarked = File.Exists(goneFile);
            DateTime? since = isMarked ? PhotoStoreRules.MarkDay(File.ReadAllText(goneFile)) : null;
            switch (PhotoStoreRules.SweepOf(savePresent(id), isMarked, since, today))
            {
                case PhotoStoreRules.Sweep.Mark:
                    File.WriteAllText(goneFile, PhotoStoreRules.MarkText(today));
                    marked?.Add(name);
                    break;
                case PhotoStoreRules.Sweep.Unmark:
                    File.Delete(goneFile);
                    break;
                case PhotoStoreRules.Sweep.Delete:
                    Directory.Delete(folder, true);
                    deleted?.Add(name);
                    break;
            }
            return true;
        }

        /// <summary>
        /// Moves the photos kept under the save's earlier folder name to its folder now, when that is not there yet and the
        /// earlier save.txt holds exactly this id, so another save's photos are never taken. True if moved.
        /// </summary>
        public static bool Adopt(string root, string saveId)
        {
            string earlier = PhotoStoreRules.EarlierFolderName(saveId);
            if (earlier == null)
            {
                return false;
            }
            try
            {
                string from = Path.Combine(root, earlier), to = Folder(root, saveId);
                string idFile = Path.Combine(from, PhotoStoreRules.SaveFile);
                if (Directory.Exists(to) || !Directory.Exists(from) || !File.Exists(idFile) || !string.Equals(File.ReadAllText(idFile).Trim(), saveId, StringComparison.Ordinal))
                {
                    return false;
                }
                Directory.Move(from, to);
                return true;
            }
            catch (Exception e) when (Disk(e))
            {
                return false;
            }
        }

        public const int PhotoMost = 32 * 1024 * 1024;   // bytes read at most; a photo of the Camera's is some 500 KB

        /// <summary>
        /// A photo's bytes for the engine to decode, or null if missing, unreadable, empty or over <see cref="PhotoMost"/>. By
        /// its path, never a file URL, so any letter in a folder's name reads.
        /// </summary>
        public static byte[] ReadPhoto(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return null;
                }
                long length = new FileInfo(path).Length;
                return length <= 0 || length > PhotoMost ? null : File.ReadAllBytes(path);
            }
            catch (Exception e) when (Disk(e))
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the game still has a save of an id: a folder of that name with a ".hsg" or ".json" file, in any of the
        /// version folders under SaveGames (the game moves a character's folder to each new version's). Only reads.
        /// </summary>
        public static bool SavePresent(string saveGames, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !Directory.Exists(saveGames))
            {
                return true;   // an id no folder can have, or no folder of saves at all: never judged gone
            }
            string[] versions = Directory.GetDirectories(saveGames);
            for (int i = 0; i < versions.Length; i++)
            {
                string folder = Path.Combine(versions[i], id);
                if (Directory.Exists(folder) && (Directory.GetFiles(folder, "*.hsg").Length > 0 || Directory.GetFiles(folder, "*.json").Length > 0))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Writes of one thing handed to workers in any order, the last asked for winning: a ticket is taken on the main thread,
    /// and a worker writes, under one lock, only while its ticket is the latest. Game-free; one for each save followed.
    /// </summary>
    public sealed class LastWins
    {
        private readonly object _gate = new object();
        private int _latest;

        /// <summary>A ticket for a write asked for now, later than every one before it.</summary>
        public int Next()
        {
            return Interlocked.Increment(ref _latest);
        }

        /// <summary>Runs the write, giving its result, if its ticket is still the latest; false if a later one was asked for.</summary>
        public bool Run(int ticket, Func<bool> write)
        {
            lock (_gate)
            {
                if (ticket != Volatile.Read(ref _latest))
                {
                    return false;
                }
                return write();
            }
        }
    }
}
