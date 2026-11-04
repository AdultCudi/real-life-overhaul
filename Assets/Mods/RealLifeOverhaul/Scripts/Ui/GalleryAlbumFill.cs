using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Photos;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Gallery's words need from the game: its texts, its clock, and a photo's size. The screen fills it; the checks fill it with their own.</summary>
    public sealed class GalleryTexts
    {
        public Func<string, string> T = key => key;
        public bool TwelveHour;
        public Func<PhotoEntry, string> Real = photo => "-";   // the computer's date and time of the shot, or "-"
        public Func<PhotoEntry, string> Size = photo => string.Empty;
    }

    /// <summary>
    /// The Gallery's model built from the photos, game-free: the album's cells under a heading a day, its search and filter chips, and
    /// one photo's facts and tags. Checked offline with 500 photos. Cost: one pass over the photos a change of the album, the
    /// search or the picks, never on a frame.
    /// </summary>
    public static class GalleryAlbumFill
    {
        /// <summary>The cells, the day headings and the chips of an album; <paramref name="shown"/> is already selected and ordered.</summary>
        public static void Fill(UiAlbumModel m, List<PhotoEntry> shown, GalleryQuery q, ICollection<string> favourites, ICollection<string> posted, ICollection<string> picked,
            int today, int places, int businesses, int days, string pictureKeys, GalleryTexts x, bool canFavourite)
        {
            int n = shown.Count;
            if (m.Photos == null || m.Photos.Length < n)
            {
                m.Photos = new UiAlbumPhoto[Math.Max(16, n + n / 4)];
            }
            m.Count = n;
            m.Search = q.Search;
            m.SearchPlaceholder = x.T("reallife_gallery_search");
            m.SortLabel = x.T(GalleryFind.SortKeys[(int)q.Sort]);
            m.SelectLabel = x.T("reallife_ui_life_select");
            m.DoneLabel = x.T("reallife_gallery_select_done");
            m.FavLabel = x.T("reallife_ui_life_favourite_picked");
            m.ShareLabel = x.T("reallife_ui_life_share");
            m.DeleteLabel = x.T("reallife_ui_life_delete_selected");
            m.CameraLabel = x.T("reallife_phonecam_app");
            m.EmptyTitle = x.T(q.Narrowed ? "reallife_gallery_none_match_title" : n == 0 && q.Kit != GalleryKit.All ? "reallife_gallery_none_album_title" : "reallife_ui_life_empty_gallery_title");
            m.EmptyBody = x.T(q.Narrowed ? "reallife_gallery_none_match_body" : n == 0 && q.Kit != GalleryKit.All ? "reallife_gallery_none_album_body" : "reallife_ui_life_empty_gallery_body");
            m.CountLabel = n == 1 ? x.T("reallife_gallery_count_one") : x.T("reallife_gallery_count_many").Replace("{count}", n.ToString("N0", CultureInfo.CurrentCulture));
            int picks = 0;
            bool everyFavourite = canFavourite && picked != null && picked.Count > 0;
            int i = 0;
            while (i < n)
            {
                int day = shown[i].Day;
                int count = 0;
                while (i + count < n && shown[i + count].Day == day)
                {
                    count++;
                }
                string label = DayWords(day, today, x);
                string meta = count == 1 ? x.T("reallife_gallery_count_one") : x.T("reallife_ui_life_photos_day").Replace("{count}", count.ToString(CultureInfo.InvariantCulture));
                for (int k = 0; k < count; k++)
                {
                    PhotoEntry photo = shown[i + k];
                    bool pick = picked != null && picked.Contains(photo.Name);
                    bool fav = favourites != null && favourites.Contains(photo.Name);
                    picks += pick ? 1 : 0;
                    everyFavourite &= !pick || fav;
                    m.Photos[i + k] = new UiAlbumPhoto
                    {
                        Picture = pictureKeys + photo.Name, Day = day, DayLabel = label, DayMeta = meta,
                        Favourite = fav, Posted = posted != null && posted.Contains(photo.Name), Picked = pick,
                    };
                }
                i += count;
            }
            m.PickedCount = picks;
            m.FavAll = everyFavourite && picks > 0;
            m.PickedLabel = picks == 0 ? x.T("reallife_gallery_pick_none") : x.T("reallife_gallery_picked").Replace("{count}", picks.ToString(CultureInfo.InvariantCulture));
            var chips = new List<UiAlbumChip>(4);
            if (places > 0 || q.Place != null)
            {
                chips.Add(new UiAlbumChip { Icon = UiIcon.MapPin, Command = UiAlbum.FilterPlace, On = q.Place != null, Label = q.Place ?? x.T("reallife_gallery_filter_place") });
            }
            if (businesses > 0 || q.Business != null)
            {
                chips.Add(new UiAlbumChip { Icon = UiIcon.Store, Command = UiAlbum.FilterBusiness, On = q.Business != null, Label = q.Business ?? x.T("reallife_gallery_filter_business") });
            }
            if (days > 0 || q.Day > 0)
            {
                chips.Add(new UiAlbumChip
                {
                    Icon = UiIcon.Calendar, Command = UiAlbum.FilterDay, On = q.Day > 0,
                    Label = q.Day > 0 ? x.T("reallife_gallery_day").Replace("{day}", q.Day.ToString(CultureInfo.InvariantCulture)) : x.T("reallife_gallery_filter_day"),
                });
            }
            if (q.Narrowed)
            {
                chips.Add(new UiAlbumChip { Icon = UiIcon.Close, Command = UiAlbum.FilterClear, Label = x.T("reallife_gallery_filter_clear") });
            }
            m.Chips = chips.ToArray();
        }

        /// <summary>A day's heading: "Today", "Yesterday", "Day 41", or the unknown day's.</summary>
        public static string DayWords(int day, int today, GalleryTexts x)
        {
            switch (GalleryRules.WordOf(day, today))
            {
                case GalleryDayWord.Today: return x.T("reallife_gallery_day_today");
                case GalleryDayWord.Yesterday: return x.T("reallife_gallery_day_yesterday");
                case GalleryDayWord.Day: return x.T("reallife_gallery_day").Replace("{day}", day.ToString(CultureInfo.InvariantCulture));
                default: return x.T("reallife_gallery_day_unknown");
            }
        }

        /// <summary>A photo's time as the game writes it: "Day 41, 19:05", the hour alone ("19h") for a photo with no minute; the computer's time after it in brackets.</summary>
        public static string Taken(PhotoEntry photo, GalleryTexts x)
        {
            string clock = PhotoTimeRules.Clock(photo.Hour, photo.Minute, x.TwelveHour, x.T("reallife_gallery_hour_only"));
            string real = x.Real(photo);
            string game = photo.Day > 0
                ? x.T("reallife_gallery_taken").Replace("{day}", photo.Day.ToString(CultureInfo.InvariantCulture)).Replace("{hour}", clock)
                : real;
            return photo.Day > 0 && real != "-" ? game + " (" + real + ")" : game;
        }

        public const int FactsMost = 12, TagsMost = 8;

        /// <summary>One photo's facts and tags for the viewer's side panel; <paramref name="story"/> is what Flare says it did, or null.</summary>
        public static void Facts(UiPhotoModel m, PhotoEntry photo, bool favourite, bool wallpaper, string story, string posted, GalleryTexts x)
        {
            PhotoSidecar side = photo.HasSide ? photo.Side : default;
            var facts = new List<UiPhotoFact>(FactsMost);
            facts.Add(new UiPhotoFact { Label = x.T("reallife_ui_life_fact_taken"), Value = Taken(photo, x) });
            if (!string.IsNullOrEmpty(side.Place))
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_ui_life_fact_place"), Value = side.Place, Link = true });
            }
            if (!string.IsNullOrEmpty(side.District) && side.District != side.Place)
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_gallery_fact_district"), Value = side.District });
            }
            if (!string.IsNullOrEmpty(side.Business))
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_gallery_fact_business"), Value = side.Business, Link = true });
            }
            if (!string.IsNullOrEmpty(side.Weather))
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_gallery_fact_weather"), Value = x.T(WeatherKey(side.Weather)) });
            }
            if (side.HasPeople)
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_gallery_fact_people"), Value = PeopleWords(side.People, x) });
            }
            facts.Add(new UiPhotoFact { Label = x.T("reallife_ui_life_fact_camera"), Value = x.T(KindKey(photo.Selfie ? (int)CameraKind.Selfie : side.Kind)) });
            facts.Add(new UiPhotoFact { Label = x.T("reallife_ui_life_fact_filter"), Value = x.T(CameraRules.Filters[CameraRules.ClampFilter(side.Filter)].NameKey) });
            facts.Add(new UiPhotoFact { Label = x.T("reallife_ui_life_fact_frame"), Value = x.T(CameraRules.FrameKeys[(int)CameraRules.ClampFrame(side.Frame)]) });
            if (side.Aspect > 0)
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_gallery_fact_shape"), Value = x.T(CameraRules.AspectKeys[(int)CameraRules.ClampAspect(side.Aspect)]) });
            }
            string size = x.Size(photo);
            if (!string.IsNullOrEmpty(size))
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_ui_life_fact_size"), Value = size });
            }
            if (!string.IsNullOrEmpty(side.Original))
            {
                facts.Add(new UiPhotoFact { Label = x.T("reallife_gallery_fact_edited"), Value = EditWords(side.Edits, x) });
            }
            facts.Add(new UiPhotoFact { Label = x.T("reallife_gallery_fact_flare"), Value = posted ?? x.T("reallife_gallery_flare_not_posted") });
            if (facts.Count > FactsMost)
            {
                facts.RemoveRange(FactsMost, facts.Count - FactsMost);
            }
            m.Facts = facts.ToArray();
            var tags = new List<string>(TagsMost);
            Tag(tags, side.Place);
            Tag(tags, side.District);
            Tag(tags, side.Business);
            for (int i = 0; i < PhotoSeenRules.TokenCount(side.Tags) && tags.Count < TagsMost; i++)
            {
                string token = PhotoSeenRules.TokenAt(side.Tags, i);
                string key = token == null ? null : PhotoSeenRules.TagKey(token);
                Tag(tags, key == null ? null : x.T(key));
            }
            if (side.Tags == null && PhotoSidecarRules.Night(side.Hour) && photo.HasSide)
            {
                Tag(tags, x.T("reallife_ui_life_tag_night"));   // a photo of the first camera: its night, from its hour
            }
            m.Tags = tags.ToArray();
            m.Where = story;
        }

        private static void Tag(List<string> tags, string tag)
        {
            if (string.IsNullOrEmpty(tag) || tags.Count >= TagsMost || tags.Contains(tag))
            {
                return;
            }
            tags.Add(tag);
        }

        private static string WeatherKey(string weather)
        {
            switch (weather)
            {
                case "rain": return "reallife_ui_hud_weather_rain";
                case "storm": return "reallife_ui_hud_weather_storm";
                case "snow": return "reallife_ui_hud_weather_snow";
                case "mist": return "reallife_ui_hud_weather_mist";
                case "heat": return "reallife_ui_hud_weather_heat";
                case "cold": return "reallife_ui_hud_weather_cold";
                case "ice": return "reallife_ui_hud_weather_ice";
                case "clear": return "reallife_ui_hud_weather_clear";
                default: return "reallife_ui_hud_weather_sunny";
            }
        }

        private static string WeatherWord(string weather)
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
                case "clear": return "clear";
                default: return "sunny";
            }
        }

        private static string KindKey(int kind)
        {
            switch (kind)
            {
                case (int)CameraKind.Selfie: return "reallife_ui_life_camera_selfie";
                case (int)CameraKind.Portrait: return "reallife_gallery_camera_portrait";
                case (int)CameraKind.Wide: return "reallife_gallery_camera_wide";
                default: return "reallife_ui_life_camera_rear";
            }
        }

        public static string PeopleWords(int people, GalleryTexts x)
        {
            return people <= 0 ? x.T("reallife_gallery_people_none") : people == 1 ? x.T("reallife_gallery_people_one") : x.T("reallife_gallery_people_many").Replace("{count}", people.ToString(CultureInfo.InvariantCulture));
        }

        private static string EditKey(string token)
        {
            switch (token)
            {
                case PhotoSeenRules.EditFilter: return "reallife_gallery_edit_filter";
                case PhotoSeenRules.EditFrame: return "reallife_gallery_edit_frame";
                case PhotoSeenRules.EditRotate: return "reallife_gallery_edit_rotate";
                case PhotoSeenRules.EditCrop: return "reallife_gallery_edit_crop";
                default: return "reallife_gallery_edit_copy";
            }
        }

        private static string EditWords(string edits, GalleryTexts x)
        {
            string text = string.Empty;
            for (int i = 0; i < PhotoSeenRules.TokenCount(edits); i++)
            {
                string token = PhotoSeenRules.TokenAt(edits, i);
                text += (text.Length > 0 ? ", " : string.Empty) + x.T(EditKey(token));
            }
            return text.Length > 0 ? text : x.T("reallife_gallery_edit_copy");
        }
    }
}
