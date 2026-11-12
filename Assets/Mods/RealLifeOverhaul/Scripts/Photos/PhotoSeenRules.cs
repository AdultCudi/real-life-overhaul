using System;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the camera saw when the shutter was pressed, as rules with no game types: which building of the frame is the shot's
    /// subject, how a frame's people are counted, the words (tokens) a shot is tagged with, and what an edit is called. The game
    /// file hands in each building and person as a point in the view; the pass runs once a shot, never on a frame. Checked offline.
    /// </summary>
    public static class PhotoSeenRules
    {
        public const float BuildingRange = 150f;   // metres: a building's front further than this is not the shot's subject
        public const float PeopleRange = 80f;      // metres: a person further than this is too small to be in the shot
        public const int PeopleMost = 99;          // people counted at most
        public const int CrowdFrom = 6;            // people in a frame that are a crowd
        public const string EditFilter = "filter", EditFrame = "frame", EditRotate = "rotate", EditCrop = "crop";

        /// <summary>The part of the view the photo keeps, as fractions of the screen: left, top, right, bottom.</summary>
        public struct View
        {
            public float Left, Top, Right, Bottom;

            public View(float left, float top, float right, float bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            /// <summary>The view of a crop of a screen, in whole pixels.</summary>
            public static View Of(int screenWidth, int screenHeight, int x, int y, int width, int height)
            {
                if (screenWidth <= 0 || screenHeight <= 0)
                {
                    return new View(0f, 0f, 1f, 1f);
                }
                return new View((float)x / screenWidth, (float)y / screenHeight, (float)(x + width) / screenWidth, (float)(y + height) / screenHeight);
            }
        }

        /// <summary>
        /// Whether a point is in the shot: in front of the lens, within <paramref name="range"/> metres, and inside the view the
        /// photo keeps. <paramref name="x"/> and <paramref name="y"/> are viewport fractions (0 to 1, y up as the engine has it).
        /// </summary>
        public static bool InFrame(float x, float y, float depth, float range, in View view)
        {
            return depth > 0.1f && depth <= range && x >= view.Left && x <= view.Right && (1f - y) >= view.Top && (1f - y) <= view.Bottom;
        }

        /// <summary>How far a point is from the middle of the photo, squared, as fractions of the view's own size: lower is more the subject.</summary>
        public static float Off(float x, float y, in View view)
        {
            float w = Math.Max(0.001f, view.Right - view.Left), h = Math.Max(0.001f, view.Bottom - view.Top);
            float dx = ((x - view.Left) / w) - 0.5f, dy = ((1f - y - view.Top) / h) - 0.5f;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Whether a building at this off-centre (squared) and depth beats the one held. A nearer one wins when two stand about as
        /// near the middle (within a hundredth), so the shop in front is the subject, not the tower behind it.
        /// </summary>
        public static bool Better(float off, float depth, float heldOff, float heldDepth)
        {
            if (float.IsNaN(heldOff))
            {
                return true;
            }
            if (Math.Abs(off - heldOff) < 0.01f)
            {
                return depth < heldDepth;
            }
            return off < heldOff;
        }

        /// <summary>People counted: never fewer than none, never more than <see cref="PeopleMost"/>.</summary>
        public static int People(int counted)
        {
            return Math.Max(0, Math.Min(PeopleMost, counted));
        }

        /// <summary>
        /// The words a shot is tagged with, comma between, as tokens (<see cref="TagKey"/> is its locale key): a selfie,
        /// a portrait or wide shot, night, the weather when it is not a fair one, a business in view, people or a crowd.
        /// </summary>
        public static string Tokens(PhotoSidecar s, bool selfie)
        {
            var text = new StringBuilder(48);
            Add(text, selfie || s.Kind == (int)CameraKind.Selfie ? "selfie" : null);
            Add(text, s.Kind == (int)CameraKind.Portrait ? "portrait" : null);
            Add(text, s.Kind == (int)CameraKind.Wide ? "wide" : null);
            Add(text, PhotoSidecarRules.Night(s.Hour) ? "night" : null);
            Add(text, Weathered(s.Weather));
            Add(text, !string.IsNullOrEmpty(s.Business) ? "business" : null);
            Add(text, s.HasPeople && s.People >= CrowdFrom ? "crowd" : s.HasPeople && s.People > 0 ? "people" : null);
            Add(text, s.Boost ? "boost" : null);
            return text.Length == 0 ? null : text.ToString();
        }

        // A weather worth a tag: not the fair ones.
        private static string Weathered(string weather)
        {
            switch (weather)
            {
                case "rain":
                case "storm":
                case "snow":
                case "mist":
                case "heat":
                case "cold":
                case "ice":
                    return weather;
                default:
                    return null;
            }
        }

        private static void Add(StringBuilder text, string token)
        {
            if (token == null)
            {
                return;
            }
            if (text.Length > 0)
            {
                text.Append(',');
            }
            text.Append(token);
        }

        /// <summary>A token's locale key.</summary>
        public static string TagKey(string token)
        {
            switch (token)
            {
                case "night": return "reallife_ui_life_tag_night";
                case "rain": return "reallife_ui_hud_weather_rain";   // the weather's own words
                case "storm": return "reallife_ui_hud_weather_storm";
                case "snow": return "reallife_ui_hud_weather_snow";
                case "mist": return "reallife_ui_hud_weather_mist";
                case "heat": return "reallife_ui_hud_weather_heat";
                case "cold": return "reallife_ui_hud_weather_cold";
                case "ice": return "reallife_ui_hud_weather_ice";
                case "selfie": return "reallife_gallery_tag_selfie";
                case "portrait": return "reallife_gallery_tag_portrait";
                case "wide": return "reallife_gallery_tag_wide";
                case "business": return "reallife_gallery_tag_business";
                case "crowd": return "reallife_gallery_tag_crowd";
                case "people": return "reallife_gallery_tag_people";
                case "boost": return "reallife_gallery_tag_boost";
                default: return null;
            }
        }

        /// <summary>The nth token of a comma list, or null past its end.</summary>
        public static string TokenAt(string tokens, int index)
        {
            if (string.IsNullOrEmpty(tokens) || index < 0)
            {
                return null;
            }
            int start = 0;
            for (int i = 0; ; i++)
            {
                int end = tokens.IndexOf(',', start);
                if (i == index)
                {
                    string token = (end < 0 ? tokens.Substring(start) : tokens.Substring(start, end - start)).Trim();
                    return token.Length == 0 ? null : token;
                }
                if (end < 0)
                {
                    return null;
                }
                start = end + 1;
            }
        }

        /// <summary>How many tokens a comma list has.</summary>
        public static int TokenCount(string tokens)
        {
            if (string.IsNullOrEmpty(tokens))
            {
                return 0;
            }
            int n = 1;
            for (int i = 0; i < tokens.Length; i++)
            {
                n += tokens[i] == ',' ? 1 : 0;
            }
            return n;
        }

        /// <summary>What an edit did, as tokens: only what changed against the photo it was made from.</summary>
        public static string Edits(bool filter, bool frame, int quarters, bool crop)
        {
            var text = new StringBuilder(32);
            Add(text, filter ? EditFilter : null);
            Add(text, frame ? EditFrame : null);
            Add(text, ((quarters % 4) + 4) % 4 != 0 ? EditRotate : null);
            Add(text, crop ? EditCrop : null);
            return text.Length == 0 ? null : text.ToString();
        }
    }

    /// <summary>The photo's clock as the game writes it, with the minute when the photo has one. Game-free.</summary>
    public static class PhotoTimeRules
    {
        /// <summary>
        /// "19:05", or "07:05 PM" on the game's 12 hour clock; with no minute (a photo taken before the camera stored it) the hour
        /// alone, "19h" through <paramref name="hourOnly"/> ("{hour}h"), or "07 PM". <paramref name="minute"/> below 0 is no minute.
        /// </summary>
        public static string Clock(int hour, int minute, bool twelve, string hourOnly)
        {
            hour = Math.Max(0, Math.Min(23, hour));
            bool known = minute >= 0;
            minute = Math.Min(59, minute);
            if (twelve)
            {
                int h = hour % 12 == 0 ? 12 : hour % 12;
                string tail = hour >= 12 ? "PM" : "AM";
                return known
                    ? h.ToString("00", CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture) + " " + tail
                    : h.ToString("00", CultureInfo.InvariantCulture) + " " + tail;
            }
            return known
                ? hour.ToString("00", CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture)
                : (string.IsNullOrEmpty(hourOnly) ? "{hour}h" : hourOnly).Replace("{hour}", hour.ToString("00", CultureInfo.InvariantCulture));
        }
    }
}
