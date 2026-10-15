using System;
using System.Globalization;
using Helpers;
using Localizor;

namespace RealLifeOverhaul.Photos
{
    /// <summary>The Camera's and the Gallery's words: a key's text, and a text with its {names} filled in. Called on a click or when a page is written, never per frame.</summary>
    internal static class PhotosText
    {
        public static string T(string key)
        {
            return key.GetLocalization();
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public static string N(int value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        /// <summary>When a photo was taken, in words: the game's day and hour (the game's own clock words), and the real date and time as the computer's clock had it.</summary>
        public static string Taken(PhotoEntry photo)
        {
            DateTime at = PhotoStoreRules.Taken(photo);
            string real = at.Year > 1 ? at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "-";
            return photo.Day > 0
                ? Fill("reallife_gallery_taken", "day", photo.Day.ToString(CultureInfo.InvariantCulture), "hour", photo.Hour.GetFormattedTime(0f, true), "real", real)
                : real;
        }

        /// <summary>A number of photos in words: "No photos", "1 photo", "12 photos".</summary>
        public static string Count(int count)
        {
            return count <= 0 ? T("reallife_gallery_count_none") : count == 1 ? T("reallife_gallery_count_one") : Fill("reallife_gallery_count_many", "count", N(count));
        }

        /// <summary>A day's heading in the Gallery: "Today", "Yesterday", "Day 41" (the game's own days), against the game's day now.</summary>
        public static string Day(int day, int today)
        {
            switch (GalleryRules.WordOf(day, today))
            {
                case GalleryDayWord.Today: return T("reallife_gallery_day_today");
                case GalleryDayWord.Yesterday: return T("reallife_gallery_day_yesterday");
                case GalleryDayWord.Day: return Fill("reallife_gallery_day", "day", day.ToString(CultureInfo.InvariantCulture));
                default: return T("reallife_gallery_day_unknown");
            }
        }

        /// <summary>When a photo was taken, in the game's time, for the top of the photo shown whole: "Today, 14:00", "Day 41, 09:00".</summary>
        public static string When(PhotoEntry photo, int today)
        {
            string hour = photo.Hour.GetFormattedTime(0f, true);
            switch (GalleryRules.WordOf(photo.Day, today))
            {
                case GalleryDayWord.Today: return Fill("reallife_gallery_when_today", "hour", hour);
                case GalleryDayWord.Yesterday: return Fill("reallife_gallery_when_yesterday", "hour", hour);
                case GalleryDayWord.Day: return Fill("reallife_gallery_when_day", "day", photo.Day.ToString(CultureInfo.InvariantCulture), "hour", hour);
                default: return Real(photo);
            }
        }

        /// <summary>The real date and time a photo was taken, as the computer's clock had it ("2026-10-15 19:22"), or "-".</summary>
        public static string Real(PhotoEntry photo)
        {
            DateTime at = PhotoStoreRules.Taken(photo);
            return at.Year > 1 ? at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "-";
        }
    }
}
