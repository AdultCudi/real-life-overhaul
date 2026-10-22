using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Photos
{
    /// <summary>One photo of the Gallery, as its file name says it.</summary>
    public sealed class PhotoEntry
    {
        public string Name;     // the file's name, with ".jpg"
        public long Stamp;      // when it was taken, real time, as yyyyMMddHHmmss
        public int Number;      // 1, or the second, third... photo of the same second
        public int Day;         // the game's day it was taken on (0: not known)
        public int Hour;        // the game's hour it was taken at
        public bool Selfie;
    }

    /// <summary>
    /// The game-free rules of the photos on disk, checked offline; <see cref="PhotoStore"/> does the files.
    ///
    /// A save's folder, persistentDataPath/RealLifeOverhaul/Photos/(folder name), holds each photo as a JPG of about 1080
    /// lines, its small copy in "Thumbs" (where Flare reads it), "save.txt" (the save's id), "wallpaper.txt" and, once the
    /// save is found deleted, "gone.txt" with the day. Every save file of one playthrough shares the game's character id,
    /// and so its photos; nothing is in the save itself.
    ///
    /// A photo's name sorts as the Gallery's order: Photo_20261015_143005_D34_14.jpg, Selfie_20261015_143005_D34_14_2.jpg
    /// (the real date and time, the game's day and hour, a number for another of the same second).
    ///
    /// Nothing is deleted without the player: with <see cref="Cap"/> photos, opening the camera asks whether the oldest
    /// <see cref="OfferAtOnce"/> may go, never the wallpaper. The photos of a save gone from disk are marked with the day,
    /// deleted after <see cref="GraceDays"/> days, and unmarked if the save comes back.
    /// </summary>
    public static class PhotoStoreRules
    {
        public const int Cap = 300;               // photos a save keeps at most: some 150 MB
        public const int OfferAtOnce = 10;        // photos offered for deletion when the Gallery is full
        public const int GraceDays = 30;          // real days a deleted save's photos are kept after it is first found gone
        public const string Extension = ".jpg";
        public const string PhotoPrefix = "Photo_";
        public const string SelfiePrefix = "Selfie_";
        public const string RootFolder = "Photos";          // under persistentDataPath/RealLifeOverhaul
        public const string SaveFile = "save.txt";
        public const string WallpaperFile = "wallpaper.txt";
        public const string GoneFile = "gone.txt";
        public const string Unsaved = "unsaved";             // the folder of a save with no id (a new game not saved yet)
        public const int NameMost = 80;                      // characters of a save's folder's name at most
        public const int HashLength = 9;                     // "_" and eight hex digits: the mark of a name that was changed

        /// <summary>
        /// A save's folder's name, one for each id and never one for two. A game id (letters, digits, '-', '+', '=') is used as
        /// it is; any other has its other characters made '_', is cut to fit, and ends in '_' and a hash of the whole id, which
        /// a plain id cannot (it has no '_'). "unsaved" for none, "unsaved_" and the hash for an id with nothing plain.
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
        /// The folder name an earlier build gave (other characters made '_' with no hash, cut at <see cref="NameMost"/>), or
        /// null where it equals <see cref="FolderName"/>. <see cref="PhotoStore.Adopt"/> moves such a folder whose save.txt
        /// holds the same id.
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
        /// Whether two paths name one folder, ignoring a trailing separator and case. The separators are compared as one: the
        /// engine's persistentDataPath has '/', and the game's Mono turns them into '\' in what Path gives back.
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

        /// <summary>A photo's name; a <paramref name="number"/> past 1 is for another photo of the same second.</summary>
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

        /// <summary>A photo's name read back; false for a file that is not the Camera's, which is left alone.</summary>
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

        /// <summary>Newest first, the later number first within a second, then by name, so one order whatever the folder gave.</summary>
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

        /// <summary>Newest first.</summary>
        public static void Order(List<PhotoEntry> photos)
        {
            photos.Sort(NewestFirst);
        }

        /// <summary>True when a save has as many photos as it keeps.</summary>
        public static bool Full(int count)
        {
            return count >= Cap;
        }

        /// <summary>How many photos opening the camera offers for deletion: none below the cap, else room for <see cref="OfferAtOnce"/>.</summary>
        public static int ToOffer(int count)
        {
            return count < Cap ? 0 : count - Cap + OfferAtOnce;
        }

        /// <summary>
        /// The oldest <paramref name="count"/> photos of a list in the Gallery's order, oldest first, into a list emptied first;
        /// never the wallpaper, <paramref name="keep"/>.
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

        /// <summary>What becomes of a save's photos when the photos are looked over.</summary>
        public enum Sweep
        {
            Keep,      // the save is there (or is gone and its time is not up): nothing done
            Mark,      // the save is found gone for the first time: the day is written
            Unmark,    // the save is back: the mark is taken off
            Delete     // gone for GraceDays: its photos are deleted
        }

        /// <summary>A save's photos looked over; a <paramref name="markedOn"/> of null, a mark not read, is written again as today.</summary>
        public static Sweep SweepOf(bool savePresent, bool marked, DateTime? markedOn, DateTime today)
        {
            if (savePresent)
            {
                return marked ? Sweep.Unmark : Sweep.Keep;
            }
            if (!marked || !markedOn.HasValue || markedOn.Value.Date > today.Date)
            {
                return Sweep.Mark;   // a mark in the future is a clock that was wrong: counted again from today
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
