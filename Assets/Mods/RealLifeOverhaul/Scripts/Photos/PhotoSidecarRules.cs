using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the camera knew when a photo was taken: the place, the filter and the frame, plus the hour and the weather
    /// the title is made from. It lives in a small file beside the JPG, written once at the shot. Game-free.
    /// </summary>
    public struct PhotoSidecar
    {
        public string Place;
        public string District;
        public int Filter;
        public int Frame;
        public int Hour;
        public int Day;
        public int Celsius;
        public string Weather;   // a short token: sunny, clear, rain, mist, storm, snow, heat, cold, ice
    }

    /// <summary>
    /// The sidecar beside a photo. One field a line, so a later version can add a line and an older one keeps the lines
    /// it does not know. Nothing is read on a frame: the Gallery reads one file when a photo is opened. Game-free.
    /// </summary>
    public static class PhotoSidecarRules
    {
        public const string Suffix = ".rlo";
        public const int Version = 1;

        /// <summary>The sidecar's path, beside the JPG.</summary>
        public static string PathOf(string photoPath)
        {
            return string.IsNullOrEmpty(photoPath) ? null : photoPath + Suffix;
        }

        /// <summary>The file's text. Fields a later reader does not know are simply extra lines.</summary>
        public static string Format(PhotoSidecar s)
        {
            var text = new StringBuilder(160);
            text.Append("v=").Append(Version.ToString(CultureInfo.InvariantCulture)).Append('\n');
            Line(text, "place", s.Place);
            Line(text, "district", s.District);
            text.Append("filter=").Append(s.Filter.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("frame=").Append(s.Frame.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("hour=").Append(s.Hour.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("day=").Append(s.Day.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("c=").Append(s.Celsius.ToString(CultureInfo.InvariantCulture)).Append('\n');
            Line(text, "weather", s.Weather);
            return text.ToString();
        }

        /// <summary>Reads a sidecar. A line this version does not know is kept by being ignored. False for nothing.</summary>
        public static bool Parse(string text, out PhotoSidecar s)
        {
            s = default;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            bool any = false;
            int i = 0;
            while (i < text.Length)
            {
                int end = text.IndexOf('\n', i);
                if (end < 0)
                {
                    end = text.Length;
                }
                int eq = text.IndexOf('=', i, end - i);
                if (eq > i)
                {
                    string key = text.Substring(i, eq - i).Trim();
                    string value = text.Substring(eq + 1, end - eq - 1).Trim();
                    any |= Take(ref s, key, value);
                }
                i = end + 1;
            }
            return any;
        }

        /// <summary>True from 19:00 through to before 06:00.</summary>
        public static bool Night(int hour) => hour >= 19 || hour < 6;

        /// <summary>The photo's title from the place and the hour. A place with no name stays a plain photo.</summary>
        public static string Title(PhotoSidecar s, string atNight, string plain)
        {
            string place = string.IsNullOrEmpty(s.Place) ? null : s.Place;
            if (place == null)
            {
                return plain;
            }
            if (Night(s.Hour) && !string.IsNullOrEmpty(atNight))
            {
                return atNight.Replace("{place}", place);
            }
            return place;
        }

        /// <summary>Up to four tags: the place, the district, and night or the weather. Empty slots stay null.</summary>
        public static void Tags(PhotoSidecar s, string nightTag, string weatherTag, string[] into)
        {
            if (into == null)
            {
                return;
            }
            for (int i = 0; i < into.Length; i++)
            {
                into[i] = null;
            }
            int n = 0;
            n = Add(into, n, s.Place);
            n = Add(into, n, s.District);
            if (Night(s.Hour))
            {
                Add(into, n, nightTag);
            }
            else
            {
                Add(into, n, weatherTag);
            }
        }

        /// <summary>How many tags <see cref="Tags"/> wrote.</summary>
        public static int TagCount(string[] tags)
        {
            int n = 0;
            if (tags == null)
            {
                return 0;
            }
            for (int i = 0; i < tags.Length; i++)
            {
                n += string.IsNullOrEmpty(tags[i]) ? 0 : 1;
            }
            return n;
        }

        private static int Add(string[] into, int n, string value)
        {
            if (n >= into.Length || string.IsNullOrEmpty(value))
            {
                return n;
            }
            for (int i = 0; i < n; i++)
            {
                if (string.Equals(into[i], value, StringComparison.Ordinal))
                {
                    return n;
                }
            }
            into[n] = value;
            return n + 1;
        }

        private static void Line(StringBuilder text, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            text.Append(key).Append('=').Append(Clean(value)).Append('\n');
        }

        private static string Clean(string value)
        {
            var b = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length && b.Length < 80; i++)
            {
                char c = value[i];
                if (c != '\n' && c != '\r' && c != '=')
                {
                    b.Append(c);
                }
            }
            return b.ToString();
        }

        private static bool Take(ref PhotoSidecar s, string key, string value)
        {
            switch (key)
            {
                case "place": s.Place = value; return true;
                case "district": s.District = value; return true;
                case "filter": s.Filter = Int(value); return true;
                case "frame": s.Frame = Int(value); return true;
                case "hour": s.Hour = Int(value); return true;
                case "day": s.Day = Int(value); return true;
                case "c": s.Celsius = Int(value); return true;
                case "weather": s.Weather = value; return true;
                default: return false;
            }
        }

        private static int Int(string value)
        {
            int n = 0;
            bool neg = false;
            int i = 0;
            if (value != null && value.Length > 0 && value[0] == '-')
            {
                neg = true;
                i = 1;
            }
            for (; value != null && i < value.Length; i++)
            {
                char c = value[i];
                if (c < '0' || c > '9' || n > 1000000)
                {
                    break;
                }
                n = n * 10 + (c - '0');
            }
            return neg ? -n : n;
        }
    }
}
