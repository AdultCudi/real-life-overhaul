using System;
using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// The Camera's photos on disk, done by <see cref="PhotoStoreRules"/>: a save's folder listed in the Gallery's order, a
    /// new photo's unique name, a photo deleted with its small copy, the wallpaper's line, and the look over the photos of
    /// deleted saves. Game-free (System.IO only): every call takes its folders as given, so the offline checks run it as it
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
        /// folder. The folder is made, and the save's id written beside the photos the first time (for the look over deleted saves).
        /// </summary>
        public static string NewPath(string folder, string saveId, DateTime taken, bool selfie, int day, int hour)
        {
            Directory.CreateDirectory(folder);
            string mark = Path.Combine(folder, PhotoStoreRules.SaveFile);
            if (!File.Exists(mark) && !string.IsNullOrEmpty(saveId))
            {
                File.WriteAllText(mark, saveId);
            }
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
                File.WriteAllText(file, name);
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

        // ---------------------------------------------------------------- deleted saves

        /// <summary>
        /// Every save's photos under the root looked over (PhotoStoreRules.SweepOf): a save gone is marked, a save back
        /// unmarked, a save gone for the grace days has its folder deleted (its name into <paramref name="deleted"/>).
        /// <paramref name="savePresent"/> answers whether the game still has a save of an id. The folder of the save being
        /// played (<paramref name="playing"/>) is never touched. Returns the number of folders looked at.
        /// </summary>
        public static int Sweep(string root, string playing, Func<string, bool> savePresent, DateTime today, List<string> deleted, List<string> marked)
        {
            int looked = 0;
            try
            {
                if (!Directory.Exists(root))
                {
                    return 0;
                }
                string[] folders = Directory.GetDirectories(root);
                for (int i = 0; i < folders.Length; i++)
                {
                    string folder = folders[i];
                    string name = Path.GetFileName(folder);
                    if (playing != null && string.Equals(name, PhotoStoreRules.FolderName(playing), StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string idFile = Path.Combine(folder, PhotoStoreRules.SaveFile);
                    if (!File.Exists(idFile))
                    {
                        continue;   // not a save's photos the Camera wrote (or not one it can tell): never touched
                    }
                    looked++;
                    string id = File.ReadAllText(idFile).Trim();
                    string goneFile = Path.Combine(folder, PhotoStoreRules.GoneFile);
                    bool isMarked = File.Exists(goneFile);
                    DateTime? since = isMarked ? PhotoStoreRules.MarkDay(File.ReadAllText(goneFile)) : null;
                    switch (PhotoStoreRules.SweepOf(id.Length > 0 && savePresent(id), isMarked, since, today))
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
                }
            }
            catch (IOException)
            {
                // (what could not be read or deleted is looked at again next time)
            }
            catch (UnauthorizedAccessException)
            {
            }
            return looked;
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
}
