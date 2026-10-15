using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Camera's photos on disk, done by <see cref="PhotoStoreRules"/>: a save's folder listed newest first, a new
    /// photo's unique name, a photo deleted with its small copy, the wallpaper's line, the favourites' file, and the look
    /// over the photos of deleted saves. Game-free (System.IO only): every call takes its folders as given, so the offline checks run it as it
    /// is on a folder of their own (EconomyCheck, PhotoChecks.cs).
    ///
    /// Every call here runs on a worker thread (PhotosDirector hands it over), never the game's: a folder of 300 photos is
    /// listed in a few milliseconds, but a disk can be slow. A file that cannot be read or deleted is left as it is and
    /// counted; nothing here throws.
    /// </summary>
    public static class PhotoStore
    {
        /// <summary>A save's folder of photos under the root (persistentDataPath/RealLifeOverhaul/Photos).</summary>
        public static string Folder(string root, string saveId)
        {
            return Path.Combine(root, PhotoStoreRules.FolderName(saveId));
        }

        /// <summary>The small copy's file of a photo: where Flare reads it (Core/PhotoThumbs.FileOf).</summary>
        public static string ThumbOf(string photo)
        {
            return PhotoThumbs.FileOf(photo);
        }

        /// <summary>The Camera's photos of a folder, in the Gallery's order, into a list (emptied first). Other files are left out. False if the folder cannot be read.</summary>
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
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// The full path a new photo is written to: its name by the rules, numbered past any of the same second already in the
        /// folder. The folder is made, and the save's id written beside the photos (for the look over deleted saves): written
        /// whole, and written again whenever it is not there, is empty or names another id (a write cut short by a full disk).
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

        /// <summary>The save's id beside its photos ("save.txt"), written whole unless it already says exactly that id. False if it could not be.</summary>
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
            catch (IOException)
            {
                // (read as not there: written again below)
            }
            catch (UnauthorizedAccessException)
            {
            }
            byte[] bytes = Encoding.UTF8.GetBytes(saveId);
            return WriteWhole(mark, bytes, bytes.Length);
        }

        /// <summary>A file written whole: under a name of its own first, then moved into place, so that the Gallery never lists half of one.</summary>
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
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
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
                    // (a stray part file is not a photo: it is never listed)
                }
            }
        }

        /// <summary>A photo and its small copy deleted. True if the photo is gone (it was, or now is).</summary>
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
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- the wallpaper

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
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>The wallpaper's photo written down (null: none). False if it could not be.</summary>
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
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- the favourites

        /// <summary>The photos marked as favourites ("favourites.txt" beside them: GalleryRules.ReadFavourites), into a set (emptied first). None if there is no file or it cannot be read.</summary>
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
            catch (IOException)
            {
                into.Clear();
            }
            catch (UnauthorizedAccessException)
            {
                into.Clear();
            }
        }

        /// <summary>The favourites written down, whole (none: the file taken away). False if they could not be.</summary>
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
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- deleted saves

        /// <summary>
        /// Every save's photos under the root looked over (PhotoStoreRules.SweepOf): a save gone is marked, a save back
        /// unmarked, a save gone for the grace days has its folder deleted (its name into <paramref name="deleted"/>).
        /// <paramref name="savePresent"/> answers whether the game still has a save of an id. The folder of the save being
        /// played (<paramref name="playing"/>) is never touched. Returns the number of folders looked at.
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
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
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
                catch (IOException)
                {
                    // (a folder that could not be read or deleted is looked at again next time; the others are looked at now)
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            return looked;
        }

        // One save's folder looked over; true if it is the Camera's and its save could be told. A folder whose "save.txt" is
        // empty, or names an id whose folder is not this one, is never touched: its save cannot be told, so it is never judged gone.
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
                return false;   // not a save's photos the Camera wrote (or not one it can tell): never touched
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

        // ---------------------------------------------------------------- an earlier build's folder

        /// <summary>
        /// The photos an earlier build of the Camera kept under the save's earlier folder name (an id with a character that was
        /// made '_': PhotoStoreRules.EarlierFolderName) moved to the save's folder now, when that folder is not there yet and the
        /// earlier one's "save.txt" holds exactly this id (so another save's photos are never taken). True if moved.
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
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- a whole photo

        public const int PhotoMost = 32 * 1024 * 1024;   // bytes a photo file is read up to: a 1080-line JPG is some 500 KB; past this it is no photo of the Camera's

        /// <summary>
        /// A photo's file read whole, for the engine to decode (PhotosGame.Load): the bytes, or null if it is not there, cannot
        /// be read, or is empty or larger than <see cref="PhotoMost"/>. By its path on disk, never a file URL, so a folder with
        /// '+', '#', '%', a space, an accent or Chinese in its name reads as any other.
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
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the game still has a save of an id: a folder of that name, with a save file in it (".hsg" or ".json"), in
        /// any of the game's version folders under its "SaveGames" folder (the game moves a character's folder to each new
        /// version's). Read only: nothing of the game's is made or changed.
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
    /// Writes of one thing (the wallpaper's line) handed to workers in any order, of which the last asked for wins: each is
    /// given a ticket when it is asked for (on the main thread), and a worker writes only while its ticket is the latest, under
    /// one lock, so two quick changes never leave the earlier one written. Game-free; one is made for each save followed.
    /// </summary>
    public sealed class LastWins
    {
        private readonly object _gate = new object();
        private int _latest;

        /// <summary>A ticket for a write asked for now: later than every one before it.</summary>
        public int Next()
        {
            return Interlocked.Increment(ref _latest);
        }

        /// <summary>The write done if its ticket is still the latest (true), else nothing (false: a later one was asked for).</summary>
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
