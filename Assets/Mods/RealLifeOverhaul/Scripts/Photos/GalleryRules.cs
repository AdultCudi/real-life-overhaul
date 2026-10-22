using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Photos
{
    /// <summary>The Gallery's albums: every photo, the selfies, and the photos the player marked as favourites.</summary>
    public enum GalleryAlbum
    {
        All = 0,
        Selfies = 1,
        Favourites = 2
    }

    /// <summary>What a row of the Gallery's grid shows: a day's heading, a strip of photos, or the count at the foot.</summary>
    public enum GalleryRowKind
    {
        Day,
        Strip,
        Foot
    }

    /// <summary>How a day's heading reads: today, yesterday, a day of the game's by its number, or a day not known.</summary>
    public enum GalleryDayWord
    {
        Today,
        Yesterday,
        Day,
        Unknown
    }

    /// <summary>One row of the Gallery's grid: what it shows, where it stands and how tall it is (canvas units).</summary>
    public struct GalleryRow
    {
        public GalleryRowKind Kind;
        public int Day;          // the game's day of the photos under it (0: not known)
        public int First;        // a strip's first photo, by its place in the album; a day's first photo
        public int Count;        // a strip's photos; a day's photos
        public float Top;
        public float Height;
    }

    /// <summary>
    /// Where the Gallery stands: the grid or a photo shown whole, the album, the photo and the grid's scroll. Game-free and
    /// checked offline; a few numbers set on a click, never per frame.
    /// </summary>
    public sealed class GalleryNav
    {
        public GalleryAlbum Album = GalleryAlbum.All;
        public bool Viewing;
        public int Index = -1;           // the photo shown whole, by its place in the album
        public float Scroll;             // canvas units the grid is scrolled down

        /// <summary>Shows a photo of the grid whole at once; false for a place that is not a photo of the album.</summary>
        public bool Tap(int index, int count)
        {
            if (index < 0 || index >= count)
            {
                return false;
            }
            Viewing = true;
            Index = index;
            return true;
        }

        /// <summary>Back to the grid, scrolled where it was.</summary>
        public void Back()
        {
            Viewing = false;
        }

        /// <summary>The photo before (-1) or after (+1) the one shown; false at either end.</summary>
        public bool Step(int delta, int count)
        {
            if (!Viewing)
            {
                return false;
            }
            int to = GalleryRules.Step(Index, delta, count);
            if (to == Index || to < 0)
            {
                return false;
            }
            Index = to;
            return true;
        }

        /// <summary>Another album: the grid, from its top.</summary>
        public bool Switch(GalleryAlbum album)
        {
            if (album == Album)
            {
                return false;
            }
            Album = album;
            Viewing = false;
            Index = -1;
            Scroll = 0f;
            return true;
        }

        /// <summary>After the album is read again: the photo found by name, else the one in its place; with none left, the grid.</summary>
        public void Reread(int foundAt, int count)
        {
            if (!Viewing)
            {
                Index = -1;
                return;
            }
            Index = foundAt >= 0 ? foundAt : GalleryRules.AfterRemoval(Index, count);
            if (Index < 0)
            {
                Viewing = false;
            }
        }
    }

    /// <summary>
    /// The Gallery's game-free rules: the albums' order, the grid of square photos under day headings, the rows in view, the
    /// photo before and after, the favourites' file and the dark look. Checked offline; run on a click or a scroll, never
    /// per frame.
    /// </summary>
    public static class GalleryRules
    {
        public const int ColumnsNarrow = 3;          // photos across a narrow grid
        public const int ColumnsWide = 4;            // photos across a wide grid
        public const float WideFrom = 1400f;         // canvas units of the grid's width from which it is four across
        public const float GridMost = 2000f;         // canvas units the grid is at most: a phone's column in the game's wide frame
        public const float CellGap = 6f;             // canvas units between two photos
        public const float DayHeight = 104f;         // canvas units of a day's heading
        public const float FootHeight = 170f;        // canvas units of the count at the grid's foot
        public const float Over = 500f;              // canvas units beyond the view whose rows are built, either side
        public const int FilmSide = 4;               // neighbours either side of the photo shown whole in its strip
        public const int KeptMost = 160;             // small copies kept as textures while the Gallery is open (some 110 KB each)
        public const int ReadAtOnce = 16;            // small copies a worker reads in one go
        public const double UploadBudgetMs = 1.0;    // milliseconds of the main thread a frame for making small copies textures
        public const int DarkFrom = 19, DarkUntil = 7;   // the game's hours of the dark look (as the phone's and Flare's)
        public const string FavouritesFile = "favourites.txt";
        public const float LatestWait = 2f;          // real seconds the viewfinder's ask for the newest photo waits for the app to open

        public static bool InAlbum(PhotoEntry photo, GalleryAlbum album, ICollection<string> favourites)
        {
            switch (album)
            {
                case GalleryAlbum.Selfies:
                    return photo.Selfie;
                case GalleryAlbum.Favourites:
                    return favourites != null && favourites.Contains(photo.Name);
                default:
                    return true;
            }
        }

        /// <summary>The Gallery's order: the game's day, latest first and a day not known last, then the newest first.</summary>
        public static int ByDay(PhotoEntry a, PhotoEntry b)
        {
            int da = a.Day > 0 ? a.Day : -1, db = b.Day > 0 ? b.Day : -1;
            if (da != db)
            {
                return da > db ? -1 : 1;
            }
            return PhotoStoreRules.Newest(a, b);
        }

        private static readonly Comparison<PhotoEntry> DayFirst = ByDay;

        /// <summary>An album's photos in the Gallery's order; <paramref name="into"/> is emptied first.</summary>
        public static void Album(List<PhotoEntry> photos, GalleryAlbum album, ICollection<string> favourites, List<PhotoEntry> into)
        {
            into.Clear();
            for (int i = 0; i < photos.Count; i++)
            {
                if (InAlbum(photos[i], album, favourites))
                {
                    into.Add(photos[i]);
                }
            }
            into.Sort(DayFirst);
        }

        public static int IndexOf(List<PhotoEntry> photos, string name)
        {
            if (name == null)
            {
                return -1;
            }
            for (int i = 0; i < photos.Count; i++)
            {
                if (string.Equals(photos[i].Name, name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>How a day's heading reads, against the game's day now.</summary>
        public static GalleryDayWord WordOf(int day, int today)
        {
            if (day <= 0)
            {
                return GalleryDayWord.Unknown;
            }
            if (day == today)
            {
                return GalleryDayWord.Today;
            }
            return day == today - 1 ? GalleryDayWord.Yesterday : GalleryDayWord.Day;
        }

        public static int Columns(float width)
        {
            return width >= WideFrom ? ColumnsWide : ColumnsNarrow;
        }

        /// <summary>A square photo's side, the gaps between the photos taken out.</summary>
        public static float Cell(float width, int columns)
        {
            return columns <= 0 ? 0f : Math.Max(0f, (width - CellGap * (columns - 1)) / columns);
        }

        /// <summary>The grid's rows, emptied first: each day's heading and strips, then the count at the foot; returns the height.</summary>
        public static float Layout(List<PhotoEntry> shown, int columns, float cell, List<GalleryRow> rows)
        {
            rows.Clear();
            if (shown.Count == 0 || columns <= 0)
            {
                return 0f;
            }
            float y = 0f;
            int i = 0;
            while (i < shown.Count)
            {
                int day = shown[i].Day > 0 ? shown[i].Day : 0;
                int end = i;
                while (end < shown.Count && (shown[end].Day > 0 ? shown[end].Day : 0) == day)
                {
                    end++;
                }
                rows.Add(new GalleryRow { Kind = GalleryRowKind.Day, Day = day, First = i, Count = end - i, Top = y, Height = DayHeight });
                y += DayHeight;
                for (int at = i; at < end; at += columns)
                {
                    rows.Add(new GalleryRow { Kind = GalleryRowKind.Strip, Day = day, First = at, Count = Math.Min(columns, end - at), Top = y, Height = cell });
                    y += cell + CellGap;
                }
                i = end;
            }
            rows.Add(new GalleryRow { Kind = GalleryRowKind.Foot, First = shown.Count, Top = y, Height = FootHeight });
            return y + FootHeight;
        }

        /// <summary>The first row whose foot is at or below a height, by a binary search; 0 for none.</summary>
        public static int RowAt(List<GalleryRow> rows, float y)
        {
            int lo = 0, hi = rows.Count - 1;
            if (hi < 0)
            {
                return 0;
            }
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (rows[mid].Top + rows[mid].Height < y)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo;
        }

        /// <summary>The rows to build: those in view and <see cref="Over"/> either side; false, both -1, with no row.</summary>
        public static bool InView(List<GalleryRow> rows, float scrolled, float viewHeight, out int first, out int last)
        {
            if (rows.Count == 0)
            {
                first = last = -1;
                return false;
            }
            first = RowAt(rows, scrolled - Over);
            last = RowAt(rows, scrolled + viewHeight + Over);
            return true;
        }

        /// <summary>The strip a photo of the album stands in, by its place, or -1.</summary>
        public static int RowOf(List<GalleryRow> rows, int index)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                GalleryRow r = rows[i];
                if (r.Kind == GalleryRowKind.Strip && index >= r.First && index < r.First + r.Count)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The scroll that brings a row into view: unchanged if it is, else just enough.</summary>
        public static float ScrollFor(GalleryRow row, float scrolled, float viewHeight, float total)
        {
            float most = Math.Max(0f, total - viewHeight);
            float want = scrolled;
            if (row.Top < scrolled)
            {
                want = row.Top - DayHeight;
            }
            else if (row.Top + row.Height > scrolled + viewHeight)
            {
                want = row.Top + row.Height - viewHeight + CellGap;
            }
            return Math.Max(0f, Math.Min(most, want));
        }

        /// <summary>
        /// Whether the newest photo asked for at <paramref name="askedAt"/> (real seconds; -1 none) is still wanted: only within
        /// <see cref="LatestWait"/>, so an opening the game refused never makes a later one start on the newest photo.
        /// </summary>
        public static bool LatestWanted(float askedAt, float now)
        {
            return askedAt >= 0f && now >= askedAt && now - askedAt <= LatestWait;
        }

        /// <summary>The photo before (-1) or after (+1), the same place at either end; -1 for an empty album.</summary>
        public static int Step(int index, int delta, int count)
        {
            if (count <= 0)
            {
                return -1;
            }
            index = Math.Max(0, Math.Min(index, count - 1));
            return Math.Max(0, Math.Min(index + delta, count - 1));
        }

        /// <summary>The photo shown after a deletion: the next, now at its place, or the last; -1 for none.</summary>
        public static int AfterRemoval(int index, int count)
        {
            return count <= 0 ? -1 : Math.Max(0, Math.Min(index, count - 1));
        }

        /// <summary>The strip under the photo shown: up to <see cref="FilmSide"/> either side, more on one side at an end.</summary>
        public static int Film(int index, int count, out int last)
        {
            if (count <= 0 || index < 0)
            {
                last = -1;
                return 0;
            }
            int span = FilmSide * 2 + 1;
            int first = Math.Max(0, Math.Min(index - FilmSide, count - span));
            last = Math.Min(count - 1, first + span - 1);
            return first;
        }

        /// <summary>Reads favourites.txt, a name a line, into the set emptied first, leaving out repeats and other lines; returns the count.</summary>
        public static int ReadFavourites(string text, HashSet<string> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            var probe = new PhotoEntry();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string name = lines[i].Trim();
                if (name.Length > 0 && PhotoStoreRules.Parse(name, probe))
                {
                    into.Add(name);
                }
            }
            return into.Count;
        }

        /// <summary>favourites.txt's text: the names sorted, so one set always writes the same file.</summary>
        public static string WriteFavourites(ICollection<string> favourites)
        {
            var names = new List<string>(favourites);
            names.Sort(StringComparer.Ordinal);
            var text = new StringBuilder(names.Count * 40);
            for (int i = 0; i < names.Count; i++)
            {
                text.Append(names[i]).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>Marks or unmarks a favourite; true if it is one now.</summary>
        public static bool Toggle(HashSet<string> favourites, string name)
        {
            if (favourites.Remove(name))
            {
                return false;
            }
            favourites.Add(name);
            return true;
        }

        /// <summary><see cref="Prune"/> only for a folder read whole: a failed listing shows no photo, and every mark would go.</summary>
        public static int PruneListed(HashSet<string> favourites, List<PhotoEntry> photos, bool listed)
        {
            return listed ? Prune(favourites, photos) : 0;
        }

        /// <summary>Takes out the favourites whose photo is gone; returns how many.</summary>
        public static int Prune(HashSet<string> favourites, List<PhotoEntry> photos)
        {
            if (favourites.Count == 0)
            {
                return 0;
            }
            var kept = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < photos.Count; i++)
            {
                if (favourites.Contains(photos[i].Name))
                {
                    kept.Add(photos[i].Name);
                }
            }
            int gone = favourites.Count - kept.Count;
            if (gone > 0)
            {
                favourites.IntersectWith(kept);
            }
            return gone;
        }

        /// <summary>Whether the Gallery wears its dark look at a game hour, as the phone does.</summary>
        public static bool Dark(int hour)
        {
            return hour >= DarkFrom || hour < DarkUntil;
        }
    }
}
