using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Photos
{
    /// <summary>The albums of the kit's Gallery: every photo, the favourites, the selfies, photos of a business, photos of a place, and those posted on Flare.</summary>
    public enum GalleryKit
    {
        All = 0,
        Favourites = 1,
        Selfies = 2,
        Businesses = 3,
        Places = 4,
        Flare = 5,
        Count = 6
    }

    /// <summary>How the shown photos are ordered. Newest and oldest go by the game's day and the time of the shot; the others by the name, then newest.</summary>
    public enum GallerySort
    {
        Newest = 0,
        Oldest = 1,
        Place = 2,
        Business = 3,
        Count = 4
    }

    /// <summary>What the Gallery shows: an album, a search, and a place, a business and a day to keep to. The Flare picker asks the same question.</summary>
    public sealed class GalleryQuery
    {
        public GalleryKit Kit;
        public string Search;     // words to find in a photo's place, district, business and day; null or empty for none
        public string Place;      // keep to photos of this place, or null
        public string Business;   // keep to photos of this business, or null
        public int Day;           // keep to the photos of this game day, or 0
        public GallerySort Sort;

        /// <summary>Whether anything narrows the album: a search, a place, a business or a day.</summary>
        public bool Narrowed => !string.IsNullOrEmpty(Search) || Place != null || Business != null || Day > 0;

        public void Clear()
        {
            Kit = GalleryKit.All;
            Search = null;
            Place = null;
            Business = null;
            Day = 0;
            Sort = GallerySort.Newest;
        }

        public void Narrow()
        {
            Search = null;
            Place = null;
            Business = null;
            Day = 0;
        }
    }

    /// <summary>
    /// The Gallery's game-free questions over the photos in memory: which are in an album, which match a search and a filter,
    /// their order, and the values a filter can take. One pass over the list a question, no allocation but the sort's; run when
    /// the player changes the album, types or taps, never on a frame. Checked offline with 500 photos.
    /// </summary>
    public static class GalleryFind
    {
        public const int ValuesMost = 64;   // distinct places, businesses or days a filter offers

        /// <summary>Whether a photo is in an album. <paramref name="posted"/> holds the names of the photos posted on Flare.</summary>
        public static bool InKit(PhotoEntry photo, GalleryKit kit, ICollection<string> favourites, ICollection<string> posted)
        {
            switch (kit)
            {
                case GalleryKit.Favourites: return favourites != null && favourites.Contains(photo.Name);
                case GalleryKit.Selfies: return photo.Selfie || (photo.HasSide && photo.Side.Kind == (int)CameraKind.Selfie);
                case GalleryKit.Businesses: return photo.HasSide && !string.IsNullOrEmpty(photo.Side.Business);
                case GalleryKit.Places: return photo.HasSide && !string.IsNullOrEmpty(photo.Side.Place);
                case GalleryKit.Flare: return posted != null && posted.Contains(photo.Name);
                default: return true;
            }
        }

        /// <summary>How many photos each album holds, into <paramref name="counts"/> (<see cref="GalleryKit.Count"/> long).</summary>
        public static void Counts(List<PhotoEntry> photos, ICollection<string> favourites, ICollection<string> posted, int[] counts)
        {
            for (int k = 0; k < counts.Length; k++)
            {
                counts[k] = 0;
            }
            for (int i = 0; i < photos.Count; i++)
            {
                for (int k = 0; k < (int)GalleryKit.Count && k < counts.Length; k++)
                {
                    counts[k] += InKit(photos[i], (GalleryKit)k, favourites, posted) ? 1 : 0;
                }
            }
        }

        /// <summary>Whether a photo is kept by the search and the filters of a query (its album is asked apart).</summary>
        public static bool Kept(PhotoEntry photo, GalleryQuery q)
        {
            if (q.Place != null && !string.Equals(photo.Side.Place, q.Place, StringComparison.Ordinal))
            {
                return false;
            }
            if (q.Business != null && !string.Equals(photo.Side.Business, q.Business, StringComparison.Ordinal))
            {
                return false;
            }
            if (q.Day > 0 && photo.Day != q.Day)
            {
                return false;
            }
            return string.IsNullOrEmpty(q.Search) || Matches(photo, q.Search);
        }

        /// <summary>Whether every word of a search is found in the photo's place, district, business, weather or day (case ignored).</summary>
        public static bool Matches(PhotoEntry photo, string search)
        {
            int at = 0;
            while (at < search.Length)
            {
                while (at < search.Length && search[at] == ' ')
                {
                    at++;
                }
                int end = at;
                while (end < search.Length && search[end] != ' ')
                {
                    end++;
                }
                if (end > at && !Has(photo, search, at, end - at))
                {
                    return false;
                }
                at = end;
            }
            return true;
        }

        private static bool Has(PhotoEntry photo, string text, int start, int length)
        {
            PhotoSidecar s = photo.Side;
            if (Contains(s.Place, text, start, length) || Contains(s.District, text, start, length) || Contains(s.Business, text, start, length)
                || Contains(s.Weather, text, start, length) || Contains(s.Tags, text, start, length))
            {
                return true;
            }
            // A number is a day of the game's.
            int day = 0;
            for (int i = 0; i < length; i++)
            {
                char c = text[start + i];
                if (c < '0' || c > '9' || day > 100000)
                {
                    return false;
                }
                day = day * 10 + (c - '0');
            }
            return length > 0 && day == photo.Day;
        }

        private static bool Contains(string hay, string text, int start, int length)
        {
            if (string.IsNullOrEmpty(hay) || length > hay.Length)
            {
                return false;
            }
            return hay.IndexOf(text.Substring(start, length), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The photos an album holds that a query keeps, in the query's order, into a list emptied first. With no search, no filter
        /// and the album All, that is every photo: the Flare picker offers the whole Gallery, never the latest few.
        /// </summary>
        public static void Select(List<PhotoEntry> photos, GalleryQuery q, ICollection<string> favourites, ICollection<string> posted, List<PhotoEntry> into)
        {
            into.Clear();
            for (int i = 0; i < photos.Count; i++)
            {
                if (InKit(photos[i], q.Kit, favourites, posted) && Kept(photos[i], q))
                {
                    into.Add(photos[i]);
                }
            }
            into.Sort(q.Sort == GallerySort.Oldest ? Old : q.Sort == GallerySort.Place ? ByPlace : q.Sort == GallerySort.Business ? ByBusiness : New);
        }

        private static readonly Comparison<PhotoEntry> New = GalleryRules.ByDay;
        private static readonly Comparison<PhotoEntry> Old = GalleryRules.Oldest;
        private static readonly Comparison<PhotoEntry> ByPlace = (a, b) => Then(Order(a.Side.Place, b.Side.Place), a, b);
        private static readonly Comparison<PhotoEntry> ByBusiness = (a, b) => Then(Order(a.Side.Business, b.Side.Business), a, b);

        // A photo with no value goes last.
        private static int Order(string a, string b)
        {
            bool none = string.IsNullOrEmpty(a), other = string.IsNullOrEmpty(b);
            return none || other ? (none == other ? 0 : none ? 1 : -1) : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // Equal values by the day order.
        private static int Then(int by, PhotoEntry a, PhotoEntry b)
        {
            return by != 0 ? by : GalleryRules.ByDay(a, b);
        }

        /// <summary>
        /// The distinct places, businesses or days of the photos an album holds, most photos first, into a list emptied first (at
        /// most <see cref="ValuesMost"/>): what a filter can take. A day is written as its number.
        /// </summary>
        public static void Values(List<PhotoEntry> photos, GalleryKit kit, ICollection<string> favourites, ICollection<string> posted, int field, List<string> into)
        {
            into.Clear();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < photos.Count; i++)
            {
                if (!InKit(photos[i], kit, favourites, posted))
                {
                    continue;
                }
                string v = field == 0 ? photos[i].Side.Place : field == 1 ? photos[i].Side.Business : photos[i].Day > 0 ? photos[i].Day.ToString() : null;
                if (string.IsNullOrEmpty(v))
                {
                    continue;
                }
                counts.TryGetValue(v, out int n);
                counts[v] = n + 1;
            }
            foreach (KeyValuePair<string, int> pair in counts)
            {
                into.Add(pair.Key);
            }
            into.Sort((a, b) =>
            {
                int c = counts[b].CompareTo(counts[a]);
                return c != 0 ? c : string.CompareOrdinal(a, b);
            });
            if (into.Count > ValuesMost)
            {
                into.RemoveRange(ValuesMost, into.Count - ValuesMost);
            }
        }

        /// <summary>The value after the one held in a list, none (null) after the last, the first after none: a filter's cycle.</summary>
        public static string Next(List<string> values, string current)
        {
            if (values.Count == 0)
            {
                return null;
            }
            if (current == null)
            {
                return values[0];
            }
            int at = values.IndexOf(current);
            return at < 0 || at + 1 >= values.Count ? null : values[at + 1];
        }

        /// <summary>The album after another, round the list (a key of the album chips).</summary>
        public static GalleryKit KitAt(int index)
        {
            return index >= 0 && index < (int)GalleryKit.Count ? (GalleryKit)index : GalleryKit.All;
        }

        public static GallerySort NextSort(GallerySort sort)
        {
            return (GallerySort)(((int)sort + 1) % (int)GallerySort.Count);
        }

        public static readonly string[] KitKeys =
        {
            "reallife_gallery_tab_all", "reallife_gallery_album_favourites", "reallife_gallery_tab_selfies", "reallife_gallery_album_businesses",
            "reallife_gallery_album_places", "reallife_gallery_album_flare"
        };

        public static readonly string[] SortKeys =
        {
            "reallife_ui_life_sort_new", "reallife_ui_life_sort_old", "reallife_gallery_sort_place", "reallife_gallery_sort_business"
        };
    }
}
