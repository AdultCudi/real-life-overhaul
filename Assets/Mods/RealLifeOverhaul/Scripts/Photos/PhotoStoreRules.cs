using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Photos
{
    /// <summary>One photo of the Gallery, as its file name says it: when it was taken (the real clock and the game's), and whether it is a selfie.</summary>
    public sealed class PhotoEntry
    {
        public string Name;     // the file's name, with ".jpg"
        public long Stamp;      // when it was taken, real time, as yyyyMMddHHmmss
        public int Number;      // 1, or the second, third... photo of the same second
        public int Day;         // the game's day it was taken on (0: not known)
        public int Hour;        // ...and its hour
        public bool Selfie;
    }

    /// <summary>
    /// The rules of the Camera's photos on disk, in plain numbers and names: where a save's photos are, what a photo is
    /// called, the Gallery's order and pages, the cap a save keeps and the oldest offered for deletion when it is full, and
    /// what becomes of the photos of a save the player has deleted. No Unity or game type and no file is touched here, so
    /// every rule is run as it is by the offline checks (EconomyCheck, PhotoChecks.cs); PhotoStore does the files with these.
    ///
    /// THE FOLDERS. persistentDataPath/RealLifeOverhaul/Photos/(the save's folder)/: each photo a JPG of about 1080 lines,
    /// its small copy (Core/PhotoThumbs' format, 256 pixels wide) in "Thumbs" beside it, where Flare reads it
    /// (PhotoThumbs.FileOf), and three small text files: "save.txt" (the save's own id, written with the first photo),
    /// "wallpaper.txt" (the photo set as the phone's wallpaper) and, only once the save is found deleted, "gone.txt" (the
    /// day it was first found so). A save is the game's character: every save file of one playthrough (its own saves, the
    /// game's autosaves, a "save as") shares its id (GameInstance.characterId) and so its photos. Nothing is in the save.
    ///
    /// A PHOTO'S NAME. "Photo_" or "Selfie_", the real date and time, the game's day and hour, and a number for a second
    /// photo in the same second: Photo_20261015_143005_D34_14.jpg, Selfie_20261015_143005_D34_14_2.jpg. The name is the
    /// Gallery's order (newest first), so copying the folder elsewhere keeps it.
    ///
    /// THE CAP. A save keeps <see cref="Cap"/> photos at most. Never deleted without the player: opening the camera with
    /// the Gallery full asks first (the game's question box) whether the oldest <see cref="OfferAtOnce"/> may go, never the
    /// one that is the wallpaper; a No keeps every photo and the camera stays shut.
    ///
    /// A DELETED SAVE. Once a game, behind the main menu or the first load, a worker looks at each save's photos: the
    /// save's folder of the game gone (no save file of that id in any of the game's version folders), its photos are
    /// marked with the day; still gone <see cref="GraceDays"/> days later, they are deleted with one line in Player.log;
    /// the save back meanwhile (restored from a backup or the cloud), the mark is taken off.
    /// </summary>
    public static class PhotoStoreRules
    {
        public const int Cap = 300;               // photos a save keeps at most: some 150 MB of 1080-line photos
        public const int OfferAtOnce = 10;        // photos offered for deletion when the Gallery is full: room for a session's shooting
        public const int Columns = 5;             // the Gallery's grid: five across...
        public const int Rows = 4;                // ...four down
        public const int PerPage = Columns * Rows;
        public const int GraceDays = 30;          // real days the photos of a deleted save are kept after it is first found gone
        public const string Extension = ".jpg";
        public const string PhotoPrefix = "Photo_";
        public const string SelfiePrefix = "Selfie_";
        public const string RootFolder = "Photos";          // under persistentDataPath/RealLifeOverhaul
        public const string SaveFile = "save.txt";
        public const string WallpaperFile = "wallpaper.txt";
        public const string GoneFile = "gone.txt";
        public const string Unsaved = "unsaved";             // the folder of a save with no id (a new game not saved yet)
        public const int NameMost = 80;                      // characters of a save's folder's name at most

        /// <summary>The Gallery's tabs: every photo, or the selfies alone.</summary>
        public const int TabAll = 0, TabSelfies = 1;

        // ---------------------------------------------------------------- the folders

        public const int HashLength = 9;                     // "_" and eight hex digits: the mark of a name that was changed

        /// <summary>
        /// A save's folder's name, one for each id and never one for two: an id of letters, digits, '-', '+' and '=' alone (the
        /// game's Base64 ids) is its folder's name as it is; any other (a space, a '/', an accent, a '_' of an older save named
        /// after its character, or one longer than <see cref="NameMost"/>) has those characters made '_', is cut to leave room,
        /// and ends in '_' and eight hex digits of the whole id (so "A B" and "A_B" are two folders, and no id kept as it is
        /// can end so, having no '_'). "unsaved" for none, and "unsaved_" and the digits for an id of nothing plain.
        /// </summary>
        public static string FolderName(string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                return Unsaved;
            }
            bool changed = saveId.Length > NameMost || string.Equals(saveId, Unsaved, StringComparison.OrdinalIgnoreCase);
            bool any = false;
            for (int i = 0; i < saveId.Length; i++)
            {
                bool plain = Plain(saveId[i]);
                changed |= !plain;
                any |= plain;
            }
            string hash = "_" + Hash(saveId).ToString("x8", CultureInfo.InvariantCulture);
            if (!any)
            {
                return Unsaved + hash;
            }
            if (!changed)
            {
                return saveId;
            }
            int length = Math.Min(saveId.Length, NameMost - HashLength);
            var name = new StringBuilder(length + HashLength);
            for (int i = 0; i < length; i++)
            {
                char c = saveId[i];
                name.Append(Plain(c) ? c : '_');
            }
            return name.Append(hash).ToString();
        }

        /// <summary>
        /// The name an earlier build of the Camera gave a save's folder (every character not plain made '_', cut to
        /// <see cref="NameMost"/>), or null where it is the name <see cref="FolderName"/> gives now. A folder of that name whose
        /// "save.txt" holds the very id is that save's photos, moved to the new name (PhotoStore.Adopt).
        /// </summary>
        public static string EarlierFolderName(string saveId)
        {
            if (string.IsNullOrEmpty(saveId))
            {
                return null;
            }
            int length = Math.Min(saveId.Length, NameMost);
            var name = new StringBuilder(length);
            bool any = false;
            for (int i = 0; i < length; i++)
            {
                bool plain = Plain(saveId[i]);
                name.Append(plain ? saveId[i] : '_');
                any |= plain;
            }
            string earlier = any ? name.ToString() : Unsaved + "_" + Hash(saveId).ToString("x8", CultureInfo.InvariantCulture);
            return string.Equals(earlier, FolderName(saveId), StringComparison.Ordinal) ? null : earlier;
        }

        private static bool Plain(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '+' || c == '=';
        }

        /// <summary>
        /// Whether two paths name one folder, whatever their separators: the engine's persistentDataPath has '/', and the
        /// game's Mono turns every '/' into '\' in what Path gives back (its CleanPath), which .NET on Windows does not. A
        /// trailing separator and the case of the letters (Windows names files without it) do not count.
        /// </summary>
        public static bool SameFolder(string a, string b)
        {
            if (a == null || b == null)
            {
                return false;
            }
            int la = TrimmedLength(a), lb = TrimmedLength(b);
            if (la != lb)
            {
                return false;
            }
            for (int i = 0; i < la; i++)
            {
                char x = a[i] == '/' ? '\\' : a[i], y = b[i] == '/' ? '\\' : b[i];
                if (x != y && char.ToUpperInvariant(x) != char.ToUpperInvariant(y))
                {
                    return false;
                }
            }
            return true;
        }

        private static int TrimmedLength(string path)
        {
            int length = path.Length;
            while (length > 1 && (path[length - 1] == '/' || path[length - 1] == '\\'))
            {
                length--;
            }
            return length;
        }

        private static uint Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619;
                }
                return h;
            }
        }

        // ---------------------------------------------------------------- a photo's name

        /// <summary>A photo's name: its kind, the real moment, the game's day and hour, and a number past 1 for another of the same second.</summary>
        public static string NameFor(DateTime taken, bool selfie, int day, int hour, int number)
        {
            var name = new StringBuilder(48);
            name.Append(selfie ? SelfiePrefix : PhotoPrefix)
                .Append(taken.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture))
                .Append("_D").Append(Math.Max(0, day).ToString(CultureInfo.InvariantCulture))
                .Append('_').Append(Math.Max(0, Math.Min(23, hour)).ToString("00", CultureInfo.InvariantCulture));
            if (number > 1)
            {
                name.Append('_').Append(number.ToString(CultureInfo.InvariantCulture));
            }
            return name.Append(Extension).ToString();
        }

        /// <summary>A photo's name read back; false for a file that is not one of the Camera's (it is left alone and not listed).</summary>
        public static bool Parse(string name, PhotoEntry into)
        {
            if (string.IsNullOrEmpty(name) || into == null || !name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            bool selfie;
            int from;
            if (name.StartsWith(SelfiePrefix, StringComparison.Ordinal))
            {
                selfie = true;
                from = SelfiePrefix.Length;
            }
            else if (name.StartsWith(PhotoPrefix, StringComparison.Ordinal))
            {
                selfie = false;
                from = PhotoPrefix.Length;
            }
            else
            {
                return false;
            }
            string[] parts = name.Substring(from, name.Length - from - Extension.Length).Split('_');
            if (parts.Length < 4 || parts.Length > 5 || parts[0].Length != 8 || parts[1].Length != 6 || parts[2].Length < 2 || parts[2][0] != 'D')
            {
                return false;
            }
            if (!long.TryParse(parts[0] + parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long stamp)
                || !DateTime.TryParseExact(parts[0] + parts[1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || !int.TryParse(parts[2].Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int day)
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int hour) || hour > 23)
            {
                return false;
            }
            int number = 1;
            if (parts.Length == 5 && (!int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 2))
            {
                return false;
            }
            into.Name = name;
            into.Stamp = stamp;
            into.Number = number;
            into.Day = day;
            into.Hour = hour;
            into.Selfie = selfie;
            return true;
        }

        /// <summary>The real moment a photo was taken, from its stamp.</summary>
        public static DateTime Taken(PhotoEntry photo)
        {
            DateTime.TryParseExact(photo.Stamp.ToString("00000000000000", CultureInfo.InvariantCulture), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime at);
            return at;
        }

        // ---------------------------------------------------------------- the order and the pages

        /// <summary>The Gallery's order: newest first; within one second the later number first; then by name, so that it is one order whatever the folder gave.</summary>
        public static int Newest(PhotoEntry a, PhotoEntry b)
        {
            if (a.Stamp != b.Stamp)
            {
                return a.Stamp > b.Stamp ? -1 : 1;
            }
            if (a.Number != b.Number)
            {
                return a.Number > b.Number ? -1 : 1;
            }
            return -string.CompareOrdinal(a.Name, b.Name);
        }

        private static readonly Comparison<PhotoEntry> NewestFirst = Newest;

        /// <summary>Puts a list in the Gallery's order (newest first).</summary>
        public static void Order(List<PhotoEntry> photos)
        {
            photos.Sort(NewestFirst);
        }

        /// <summary>Whether a photo is on a tab: every photo on the first, the selfies alone on the second.</summary>
        public static bool OnTab(PhotoEntry photo, int tab)
        {
            return tab != TabSelfies || photo.Selfie;
        }

        /// <summary>The photos of a tab, in order, into a list (emptied first).</summary>
        public static void Of(List<PhotoEntry> ordered, int tab, List<PhotoEntry> into)
        {
            into.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (OnTab(ordered[i], tab))
                {
                    into.Add(ordered[i]);
                }
            }
        }

        /// <summary>The pages a number of photos fills: at least one (an empty Gallery has one empty page).</summary>
        public static int Pages(int count)
        {
            return count <= 0 ? 1 : (count + PerPage - 1) / PerPage;
        }

        /// <summary>A page asked for, kept between the first and the last there is.</summary>
        public static int ClampPage(int page, int count)
        {
            return Math.Max(0, Math.Min(page, Pages(count) - 1));
        }

        /// <summary>The first photo of a page and how many it shows.</summary>
        public static int PageRange(int page, int count, out int shown)
        {
            page = ClampPage(page, count);
            int first = page * PerPage;
            shown = Math.Max(0, Math.Min(PerPage, count - first));
            return first;
        }

        /// <summary>The page a photo stands on, by its place in the list.</summary>
        public static int PageOf(int index)
        {
            return index <= 0 ? 0 : index / PerPage;
        }

        // ---------------------------------------------------------------- the cap

        /// <summary>True when a save has as many photos as it keeps: no photo more until some go.</summary>
        public static bool Full(int count)
        {
            return count >= Cap;
        }

        /// <summary>How many photos are offered for deletion when the camera is opened: none below the cap; at it, enough for <see cref="OfferAtOnce"/> more.</summary>
        public static int ToOffer(int count)
        {
            return count < Cap ? 0 : count - Cap + OfferAtOnce;
        }

        /// <summary>
        /// The oldest photos offered for deletion, oldest first, into a list (emptied first): <paramref name="count"/> of them
        /// from the end of a list in the Gallery's order, never the photo named <paramref name="keep"/> (the wallpaper).
        /// </summary>
        public static void Oldest(List<PhotoEntry> ordered, int count, string keep, List<PhotoEntry> into)
        {
            into.Clear();
            for (int i = ordered.Count - 1; i >= 0 && into.Count < count; i--)
            {
                if (keep == null || !string.Equals(ordered[i].Name, keep, StringComparison.OrdinalIgnoreCase))
                {
                    into.Add(ordered[i]);
                }
            }
        }

        // ---------------------------------------------------------------- a deleted save

        /// <summary>What becomes of a save's photos when the photos are looked over.</summary>
        public enum Sweep
        {
            Keep,      // the save is there (or is gone and its time is not up): nothing done
            Mark,      // the save is found gone for the first time: the day is written
            Unmark,    // the save is back: the mark is taken off
            Delete     // gone for GraceDays: its photos are deleted
        }

        /// <summary>
        /// A save's photos looked over: whether its save is there, whether they are marked and since when (null for a mark that
        /// cannot be read, which is written again as today), and today.
        /// </summary>
        public static Sweep SweepOf(bool savePresent, bool marked, DateTime? markedOn, DateTime today)
        {
            if (savePresent)
            {
                return marked ? Sweep.Unmark : Sweep.Keep;
            }
            if (!marked || !markedOn.HasValue || markedOn.Value.Date > today.Date)
            {
                return Sweep.Mark;   // (a mark in the future is a clock that was wrong: counted again from today)
            }
            return (today.Date - markedOn.Value.Date).TotalDays >= GraceDays ? Sweep.Delete : Sweep.Keep;
        }

        /// <summary>A mark's day as written ("yyyy-MM-dd"), or null if it cannot be read.</summary>
        public static DateTime? MarkDay(string text)
        {
            if (text != null && DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day))
            {
                return day;
            }
            return null;
        }

        /// <summary>A day as a mark writes it.</summary>
        public static string MarkText(DateTime day)
        {
            return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
