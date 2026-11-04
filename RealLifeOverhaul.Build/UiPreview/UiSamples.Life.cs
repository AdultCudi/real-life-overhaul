using System;
using RealLifeOverhaul.Ui;

/// <summary>
/// Samples for the Planner, the Calendar, the Camera's refusal, the Gallery and the viewfinder. The names are the sort a
/// player would see. Game-free.
/// </summary>
internal static partial class UiSamples
{
    private static void LifeBar(UiPlanModel m, UiWords t)
    {
        m.Move = t("reallife_ui_prompt_move", "Move");
        m.Select = t("reallife_ui_prompt_select", "Select");
        m.Back = t("reallife_ui_prompt_back", "Back");
        m.Tabs = t("reallife_ui_prompt_page", "Page");
        m.LocateWord = t("reallife_ui_life_locate", "Locate");
    }

    private static UiNavEntry[] PlanNav(UiWords t, int upcoming, int notes) => new[]
    {
        new UiNavEntry { Icon = UiIcon.Clock, Label = t("reallife_ui_life_tab_today", "Today") },
        new UiNavEntry { Icon = UiIcon.Calendar, Label = t("reallife_planner_tab_upcoming", "Upcoming"), Count = upcoming > 0 ? upcoming.ToString() : null },
        new UiNavEntry { Icon = UiIcon.Grid, Label = t("reallife_planner_tab_week", "Week") },
        new UiNavEntry { Icon = UiIcon.Info, Label = t("reallife_ui_life_tab_notes", "Notes"), Count = notes > 0 ? notes.ToString() : null },
    };

    private static string[] PlanChips(UiWords t) => new[]
    {
        t("reallife_planner_filter_all", "All"),
        t("reallife_ui_life_group_work", "Work"),
        t("reallife_ui_life_group_life", "Life"),
        t("reallife_planner_group_money", "Money"),
        t("reallife_planner_group_business", "Business"),
        t("reallife_ui_life_group_study", "Study"),
    };

    private static string[] DayHours()
    {
        var hours = new string[16];
        for (int i = 0; i < hours.Length; i++)
        {
            hours[i] = (7 + i).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":00";
        }
        return hours;
    }

    /// <summary>A busy Monday, or a day with nothing on it yet.</summary>
    public static void PlannerToday(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool empty)
    {
        var m = new UiPlanModel
        {
            Title = t("reallife_planner_app", "Planner"), Tab = 0, Nav = PlanNav(t, empty ? 0 : 14, empty ? 0 : 3),
            Chips = PlanChips(t), Chip = 0, Week = t("reallife_ui_life_week", "Week {week}").Replace("{week}", "1"),
            EmptyTitle = t("reallife_ui_life_empty_today_title", "Nothing on today"),
            EmptyBody = t("reallife_ui_life_empty_today_body", "Shifts, classes, bills and notes land here as the day fills."),
        };
        LifeBar(m, t);
        var board = new UiBoardModel
        {
            FirstHour = 7, LastHour = 23, Hours = DayHours(), Now = empty ? -1 : 8 * 60 + 45,
            EmptyTitle = m.EmptyTitle, EmptyBody = m.EmptyBody,
        };
        if (!empty)
        {
            board.Lanes = new[]
            {
                new UiBoardLane { Name = t("reallife_ui_life_group_work", "Work"), Icon = UiIcon.Briefcase, Tint = UiRole.Chart1 },
                new UiBoardLane { Name = t("reallife_ui_life_group_study", "Study"), Icon = UiIcon.Cap, Tint = UiRole.Chart6 },
                new UiBoardLane { Name = t("reallife_ui_life_group_life", "Life"), Icon = UiIcon.Heart, Tint = UiRole.Chart5 },
                new UiBoardLane { Name = t("reallife_planner_group_money", "Money"), Icon = UiIcon.Coin, Tint = UiRole.Chart2 },
                new UiBoardLane { Name = t("reallife_planner_group_business", "Business"), Icon = UiIcon.Store, Tint = UiRole.Chart3 },
            };
            board.Shifts = new[]
            {
                new UiBoardShift { Lane = 0, Start = 7 * 60, End = 8 * 60, Name = "Open Speedy Bites", Span = "07:00 to 08:00" },
                new UiBoardShift { Lane = 4, Start = 9 * 60, End = 10 * 60 + 30, Name = "Coffee delivery", Span = "09:00 to 10:30" },
                new UiBoardShift { Lane = 0, Start = 11 * 60, End = 12 * 60, Name = "Weekly team meeting", Span = "11:00 to 12:00" },
                new UiBoardShift { Lane = 2, Start = 12 * 60 + 30, End = 13 * 60 + 30, Name = "Lunch with Helga", Span = "12:30 to 13:30" },
                new UiBoardShift { Lane = 0, Start = 14 * 60, End = 16 * 60, Name = "Interview two developers", Span = "14:00 to 16:00" },
                new UiBoardShift { Lane = 3, Start = 17 * 60, End = 17 * 60 + 30, Name = "Rent, 70 Fifth Avenue", Span = "17:00 to 17:30" },
                new UiBoardShift { Lane = 1, Start = 20 * 60, End = 22 * 60, Name = "Evening class, Marketing", Span = "20:00 to 22:00" },
            };
            board.Actions = new[]
            {
                new UiActionModel { Label = t("reallife_ui_life_jump", "Jump to now"), Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                new UiActionModel { Label = t("reallife_ui_life_add", "Add an entry"), Kind = UiButtonKind.Primary, Key = UiAction.Alt },
            };
        }
        m.Board = board;
        LifePages.Planner(s, m, f, dark, k);
    }

    /// <summary>The days after today.</summary>
    public static void PlannerUpcoming(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var m = new UiPlanModel
        {
            Title = t("reallife_planner_app", "Planner"), Tab = 1, Nav = PlanNav(t, 6, 3), Chips = PlanChips(t), Chip = 0,
            Week = t("reallife_ui_life_week", "Week {week}").Replace("{week}", "1"),
            Add = t("reallife_ui_life_add", "Add an entry"),
            Rows = new[]
            {
                new UiPlanRow { Icon = UiIcon.Truck, Title = "Coffee delivery, Bloom and Co", Meta = "Tue 09:00", Locate = true },
                new UiPlanRow { Icon = UiIcon.Cap, Title = "Marketing class, campus", Meta = "Tue 18:00", Locate = true },
                new UiPlanRow { Icon = UiIcon.Megaphone, Title = "Thrifty Threads post", Meta = "Thu 14:00", Locate = false },
                new UiPlanRow { Icon = UiIcon.Coin, Title = "Staff pay, 26 people", Meta = "Sun 23:00", Locate = false },
                new UiPlanRow { Icon = UiIcon.Receipt, Title = "Water bill", Meta = "Day 8", Locate = false },
                new UiPlanRow { Icon = UiIcon.Briefcase, Title = "Interview, Pioneer Game Studio", Meta = "Day 9 10:00", Locate = true },
            },
            RowCount = 6,
            EmptyTitle = t("reallife_ui_life_empty_upcoming_title", "Nothing coming up"),
            EmptyBody = t("reallife_ui_life_empty_upcoming_body", "Later shifts, bills and classes show here."),
        };
        LifeBar(m, t);
        LifePages.Planner(s, m, f, dark, k);
    }

    /// <summary>Seven days on the board.</summary>
    public static void PlannerWeek(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var m = new UiPlanModel
        {
            Title = t("reallife_planner_app", "Planner"), Tab = 2, Nav = PlanNav(t, 6, 3), Chips = PlanChips(t), Chip = 0,
            Week = t("reallife_ui_life_week", "Week {week}").Replace("{week}", "1"),
        };
        LifeBar(m, t);
        string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var lanes = new UiBoardLane[7];
        for (int i = 0; i < 7; i++)
        {
            lanes[i] = new UiBoardLane { Name = days[i], Icon = UiIcon.Calendar, Tint = UiRole.Chart1 };
        }
        m.Board = new UiBoardModel
        {
            FirstHour = 7, LastHour = 23, Hours = DayHours(), Now = -1, Lanes = lanes,
            Shifts = new[]
            {
                new UiBoardShift { Lane = 0, Start = 7 * 60, End = 8 * 60, Name = "Open the shop" },
                new UiBoardShift { Lane = 0, Start = 20 * 60, End = 22 * 60, Name = "Evening class" },
                new UiBoardShift { Lane = 1, Start = 9 * 60, End = 10 * 60, Name = "Delivery" },
                new UiBoardShift { Lane = 1, Start = 18 * 60, End = 20 * 60, Name = "Class" },
                new UiBoardShift { Lane = 3, Start = 14 * 60, End = 15 * 60, Name = "Sponsored post" },
                new UiBoardShift { Lane = 4, Start = 9 * 60, End = 17 * 60, Name = "Shift" },
                new UiBoardShift { Lane = 6, Start = 22 * 60, End = 23 * 60, Name = "Staff pay" },
            },
            Actions = new[] { new UiActionModel { Label = t("reallife_ui_life_add", "Add an entry"), Kind = UiButtonKind.Primary, Key = UiAction.Alt } },
        };
        LifePages.Planner(s, m, f, dark, k);
    }

    /// <summary>Notes, with one long enough to wrap a column.</summary>
    public static void PlannerNotes(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool empty)
    {
        var m = new UiPlanModel
        {
            Title = t("reallife_planner_app", "Planner"), Tab = 3, Nav = PlanNav(t, 6, empty ? 0 : 3), Chips = PlanChips(t), Chip = 0,
            Week = t("reallife_ui_life_week", "Week {week}").Replace("{week}", "1"),
            Add = t("reallife_ui_life_add", "Add an entry"),
            EmptyTitle = t("reallife_ui_life_empty_notes_title", "No notes yet"),
            EmptyBody = t("reallife_ui_life_empty_notes_body", "Add a note and it stays on the day you pick."),
        };
        LifeBar(m, t);
        if (!empty)
        {
            m.Rows = new[]
            {
                new UiPlanRow { Icon = UiIcon.Info, Title = "Ask Maria Rossi about Sunday shifts. Bring the loan papers to the bank.", Meta = "Today" },
                new UiPlanRow { Icon = UiIcon.Info, Title = "Call the wholesaler about the coffee order.", Meta = "Tue" },
                new UiPlanRow { Icon = UiIcon.Info, Title = "Pick up the shop keys from the office.", Meta = "Wed" },
            };
            m.RowCount = 3;
        }
        LifePages.Planner(s, m, f, dark, k);
    }

    private static string[] Weekdays(UiWords t) => new[]
    {
        t("common_monday", "Mon"), t("common_tuesday", "Tue"), t("common_wednesday", "Wed"), t("common_thursday", "Thu"),
        t("common_friday", "Fri"), t("common_saturday", "Sat"), t("common_sunday", "Sun"),
    };

    /// <summary>A full month, or the same month with nothing written on it.</summary>
    public static void CalendarMonth(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool empty)
    {
        string april = t("reallife_calendar_month_4", "April");
        var diary = new UiDiaryModel
        {
            Month = april + ", " + t("reallife_ui_life_week", "Week {week}").Replace("{week}", "1"),
            Weekdays = Weekdays(t), Lead = 0, Length = 30, Today = 2, Chosen = 2, PastBefore = 2,
            ChosenTitle = t("reallife_ui_life_day_line", "{count} entries, {done} done").Replace("{count}", empty ? "0" : "4").Replace("{done}", empty ? "0" : "1"),
            ChosenEmpty = t("reallife_ui_life_nothing_day", "Nothing is set for this day. What you plan shows here."),
            AheadTitle = t("reallife_ui_life_tomorrow", "Tomorrow"),
            More = t("reallife_ui_life_more", "+{0} more"),
        };
        if (!empty)
        {
            diary.Entries = new[]
            {
                new UiDiaryEntry { Day = 2, Icon = UiIcon.Store, Title = "Open Speedy Bites", Value = "07:00" },
                new UiDiaryEntry { Day = 2, Icon = UiIcon.Truck, Title = "Coffee delivery", Value = "09:00", Action = t("reallife_ui_life_locate", "Locate") },
                new UiDiaryEntry { Day = 2, Icon = UiIcon.People, Title = "Weekly team meeting", Value = "11:00" },
                new UiDiaryEntry { Day = 2, Icon = UiIcon.Cap, Title = "Evening class", Value = "20:00" },
                new UiDiaryEntry { Day = 3, Icon = UiIcon.Truck, Title = "Bloom and Co delivery", Value = "09:00" },
                new UiDiaryEntry { Day = 3, Icon = UiIcon.Cap, Title = "Marketing class", Value = "18:00" },
                new UiDiaryEntry { Day = 6, Icon = UiIcon.Coin, Title = "Staff pay" },
                new UiDiaryEntry { Day = 8, Icon = UiIcon.Receipt, Title = "Rent due" },
                new UiDiaryEntry { Day = 15, Icon = UiIcon.Coin, Title = "Loan payment" },
            };
            diary.ChosenFigures = new[] { new UiPairModel { Key = t("reallife_ui_life_fact_place", "Place"), Value = "7th Avenue" } };
        }
        var m = new UiCalModel
        {
            Title = t("reallife_calendar_app", "Calendar"), Diary = diary,
            Weather = t("reallife_ui_life_weather", "{sky}, {temp}").Replace("{sky}", t("reallife_ui_hud_weather_sunny", "Sunny")).Replace("{temp}", "18 C"),
            Add = t("reallife_ui_life_add", "Add an entry"), Open = t("reallife_ui_life_open_planner", "Open Planner"),
            Move = t("reallife_ui_prompt_move", "Move"), Select = t("reallife_ui_prompt_select", "Select"), Back = t("reallife_ui_prompt_back", "Back"),
        };
        LifePages.Calendar(s, m, f, dark, k);
    }

    /// <summary>The camera refused because the gallery is at its cap.</summary>
    public static void CameraRefused(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var m = new UiDecisionModel
        {
            Kicker = t("reallife_ui_life_newest", "Newest photo, {taken}").Replace("{taken}", "Mon 08:12"),
            Title = t("reallife_ui_life_camera_full_title", "The gallery is full"),
            Body = t("reallife_ui_life_camera_full_body", "There is no room for a new photo. The gallery holds {cap} and all {count} are used.")
                .Replace("{cap}", "300").Replace("{count}", "300"),
            Emblem = UiIcon.Camera, EmblemTint = UiRole.Accent,
            Choices = new[]
            {
                new UiDecisionChoice
                {
                    Key = UiAction.Alt, Name = t("reallife_ui_life_open_gallery", "Open Gallery"),
                    Lines = new[] { new UiConsequence { Text = t("reallife_ui_life_camera_stay", "The photos stay on this computer"), Tone = 0, Change = 0 } },
                },
            },
            Selected = 0,
            Buttons = new[]
            {
                new UiActionModel { Label = t("common_close", "Close"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
            },
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            },
            PromptCount = 3,
        };
        LifePages.Refused(s, m, f, dark, k);
    }

    private static readonly string[] PhotoSamplePlaces = { "Broadway", "7th Avenue", "Fulton Street", "Pier 17", "Hudson Yards" };
    private static readonly string[] PhotoSampleDistricts = { "Midtown", "Midtown", "Financial District", "Chelsea", "Hell's Kitchen" };
    private static readonly string[] PhotoSampleBusinesses = { null, "Bloom Cafe", null, "Speedy Bites", null, "Thrifty Threads" };

    /// <summary>A save's photos as the Gallery lists them: eight a day, newest first, each with what the camera knew of it.</summary>
    public static System.Collections.Generic.List<RealLifeOverhaul.Photos.PhotoEntry> SamplePhotos(int count, int today)
    {
        var list = new System.Collections.Generic.List<RealLifeOverhaul.Photos.PhotoEntry>(count);
        for (int i = 0; i < count; i++)
        {
            int day = Math.Max(1, today - i / 8);
            int hour = (23 - (i % 8) * 3 + 24) % 24;
            var photo = new RealLifeOverhaul.Photos.PhotoEntry
            {
                Name = "Photo_20261104_12" + (i / 60).ToString("00") + (i % 60).ToString("00") + "_D" + day + "_" + hour.ToString("00") + ".jpg", Stamp = 20261104120000L + (i / 60) * 100 + i % 60, Number = 1,
                Day = day, Hour = hour, Selfie = i % 9 == 4,
            };
            var side = new RealLifeOverhaul.Photos.PhotoSidecar
            {
                Place = PhotoSamplePlaces[i % PhotoSamplePlaces.Length], District = PhotoSampleDistricts[i % PhotoSampleDistricts.Length], Hour = hour, Day = day,
                Weather = i % 5 == 3 ? "rain" : "sunny", Filter = i % 7 == 1 ? 1 : 0, Aspect = 2, Kind = i % 9 == 4 ? 1 : i % 11 == 2 ? 2 : 0,
                Business = PhotoSampleBusinesses[i % PhotoSampleBusinesses.Length],
                HasPeople = true, People = i % 7,
            };
            if (i % 4 != 3)
            {
                side.HasMinute = true;
                side.Minute = (i * 7) % 60;
            }
            side.Tags = RealLifeOverhaul.Photos.PhotoSeenRules.Tokens(side, photo.Selfie);
            photo.Take(side);
            list.Add(photo);
        }
        return list;
    }

    /// <summary>The Gallery's words from the sample's own source, and a computer's time that is not the game's.</summary>
    public static RealLifeOverhaul.Ui.GalleryTexts SampleTexts(UiWords t)
    {
        return new RealLifeOverhaul.Ui.GalleryTexts { T = key => t(key, key), Real = p => "2026-11-04 12:" + (p.Number + 10), Size = p => "412 KB" };
    }

    private static UiNavEntry[] GalleryNav(UiWords t, int[] counts)
    {
        UiIcon[] icons = { UiIcon.Image, UiIcon.Heart, UiIcon.SmileyHappy, UiIcon.Store, UiIcon.MapPin, UiIcon.Megaphone };
        var nav = new UiNavEntry[6];
        for (int i = 0; i < nav.Length; i++)
        {
            nav[i] = new UiNavEntry { Icon = icons[i], Label = t(RealLifeOverhaul.Photos.GalleryFind.KitKeys[i], RealLifeOverhaul.Photos.GalleryFind.KitKeys[i]), Count = counts[i].ToString() };
        }
        return nav;
    }

    /// <summary>The album as the Gallery's own code fills it: <paramref name="state"/> is "grid", "select", "search", "flare" or "none".</summary>
    private static UiAlbumModel Album(UiWords t, int count, string state, out int[] counts)
    {
        var photos = SamplePhotos(count, 40);
        var query = new RealLifeOverhaul.Photos.GalleryQuery();
        var favourites = new System.Collections.Generic.HashSet<string>();
        var posted = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < photos.Count; i++)
        {
            if (i % 7 == 0)
            {
                favourites.Add(photos[i].Name);
            }
            if (i % 11 == 0)
            {
                posted.Add(photos[i].Name);
            }
        }
        if (state == "search")
        {
            query.Search = "broad";
            query.Business = "Bloom Cafe";
        }
        if (state == "flare")
        {
            query.Kit = RealLifeOverhaul.Photos.GalleryKit.Flare;
        }
        var shown = new System.Collections.Generic.List<RealLifeOverhaul.Photos.PhotoEntry>();
        RealLifeOverhaul.Photos.GalleryFind.Select(photos, query, favourites, posted, shown);
        counts = new int[(int)RealLifeOverhaul.Photos.GalleryKit.Count];
        RealLifeOverhaul.Photos.GalleryFind.Counts(photos, favourites, posted, counts);
        var places = new System.Collections.Generic.List<string>();
        var businesses = new System.Collections.Generic.List<string>();
        var days = new System.Collections.Generic.List<string>();
        RealLifeOverhaul.Photos.GalleryFind.Values(photos, query.Kit, favourites, posted, 0, places);
        RealLifeOverhaul.Photos.GalleryFind.Values(photos, query.Kit, favourites, posted, 1, businesses);
        RealLifeOverhaul.Photos.GalleryFind.Values(photos, query.Kit, favourites, posted, 2, days);
        var picked = new System.Collections.Generic.HashSet<string>();
        if (state == "select")
        {
            for (int i = 0; i < Math.Min(3, shown.Count); i++)
            {
                picked.Add(shown[i].Name);
            }
        }
        var model = new UiAlbumModel { Selecting = state == "select" };
        RealLifeOverhaul.Ui.GalleryAlbumFill.Fill(model, shown, query, favourites, posted, picked, 40, places.Count, businesses.Count, days.Count, "photo:", SampleTexts(t), true);
        model.Selecting = state == "select";
        return model;
    }

    /// <summary>The album: a few photos, a full gallery, an empty one, select mode, a search with a filter, or the Flare album.</summary>
    public static void GalleryGrid(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, int count, string state)
    {
        UiAlbumModel album = Album(t, count, state, out int[] counts);
        LifePages.Gallery(s, f, dark, k, t("reallife_gallery_app", "Gallery"), GalleryNav(t, counts), state == "flare" ? 5 : 0, false, album, null,
            t("reallife_ui_prompt_move", "Move"), t("reallife_ui_prompt_select", "Select"), t("reallife_ui_prompt_back", "Back"));
    }

    /// <summary>One photo open, with its facts built by the Gallery's own code, its tags and its neighbours; <paramref name="old"/> is a photo with no minute.</summary>
    public static void GalleryViewer(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool old)
    {
        var photos = SamplePhotos(9, 40);
        RealLifeOverhaul.Photos.PhotoEntry photo = photos[old ? 3 : 0];
        var texts = SampleTexts(t);
        var model = new UiPhotoModel
        {
            Picture = "photo:" + photo.Name,
            Title = photo.Side.Business ?? photo.Side.Place,
            Caption = RealLifeOverhaul.Photos.PhotoTimeRules.Clock(photo.Hour, photo.Minute, false, t("reallife_gallery_hour_only", "{hour}h")),
            Position = t("reallife_ui_life_of_day", "{at} of {count}, {day}").Replace("{at}", "4").Replace("{count}", "9").Replace("{day}", "Today"),
            FactsTitle = t("reallife_ui_life_facts", "Photo"), TagsTitle = t("reallife_ui_life_tags", "Tags"), WhereTitle = t("reallife_ui_life_where", "Where it went"),
            Neighbours = new[] { "photo:a", "photo:b", "photo:c", "photo:d", "photo:e", "photo:f", "photo:g" }, NeighbourAt = 3,
            Actions = new[]
            {
                new UiActionModel { Label = t("reallife_gallery_post_flare", "Post to Flare"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Icon = UiIcon.Send },
                new UiActionModel { Label = t("reallife_gallery_edit", "Edit"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Pencil, Key = UiAction.Count },
                new UiActionModel { Label = t("reallife_ui_life_wallpaper", "Set as wallpaper"), Kind = UiButtonKind.Secondary, Key = UiAction.Option, Icon = UiIcon.Monitor },
                new UiActionModel { Icon = UiIcon.Heart, Kind = UiButtonKind.Primary, Key = UiAction.Count },
                new UiActionModel { Icon = UiIcon.Trash, Kind = UiButtonKind.Destructive, Key = UiAction.Count },
            },
        };
        string story = t("reallife_ui_life_flare_line", "Posted on Flare by {name}: {likes} likes, {visits} visits, {money}.")
            .Replace("{name}", "Speedy Bites").Replace("{likes}", "1.2K").Replace("{visits}", "46").Replace("{money}", "+$1,180");
        RealLifeOverhaul.Ui.GalleryAlbumFill.Facts(model, photo, true, false, old ? null : story, old ? null : t("reallife_gallery_flare_posted", "Posted, {likes} likes").Replace("{likes}", "1.2K"), texts);
        LifePages.Gallery(s, f, dark, k, t("reallife_gallery_app", "Gallery"), GalleryNav(t, new[] { 9, 2, 1, 4, 9, 1 }), 0, true, null, model,
            t("reallife_ui_prompt_move", "Move"), t("reallife_ui_prompt_select", "Select"), t("reallife_ui_prompt_back", "Back"));
    }

    /// <summary>The editor over a photo: its filters, frames and shapes, the turn, Save as a new photo.</summary>
    public static void GalleryEdit(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var m = new UiPhotoEditModel
        {
            Picture = "photoedit:preview",
            FilterTitle = t("reallife_phonecam_filter_title", "Filter"), FrameTitle = t("reallife_phonecam_frame_title", "Frame"), ShapeTitle = t("reallife_gallery_fact_shape", "Shape"),
            Caption = t("reallife_gallery_edit_note", "The original stays. The edit is saved as a new photo."),
            TurnLabel = t("reallife_gallery_edit_turn", "Turn"), SaveLabel = t("reallife_gallery_edit_save", "Save as new photo"),
            CancelLabel = t("common_cancel", "Cancel"), ResetLabel = t("reallife_gallery_edit_reset", "Reset"),
            Filter = 1, Frame = 1, Shape = 2,
        };
        m.Filters = new string[RealLifeOverhaul.Photos.CameraRules.Filters.Length];
        for (int i = 0; i < m.Filters.Length; i++)
        {
            m.Filters[i] = t(RealLifeOverhaul.Photos.CameraRules.Filters[i].NameKey, "Filter");
        }
        m.Frames = new string[RealLifeOverhaul.Photos.CameraRules.FrameKeys.Length];
        for (int i = 0; i < m.Frames.Length; i++)
        {
            m.Frames[i] = t(RealLifeOverhaul.Photos.CameraRules.FrameKeys[i], "Frame");
        }
        m.Shapes = new[]
        {
            t("reallife_gallery_shape_original", "As taken"), t("reallife_phonecam_aspect_43", "4:3"), t("reallife_phonecam_aspect_169", "16:9"), t("reallife_phonecam_aspect_11", "1:1"),
            t("reallife_phonecam_aspect_916", "9:16"),
        };
        LifePages.Gallery(s, f, dark, k, t("reallife_gallery_app", "Gallery"), GalleryNav(t, new[] { 9, 2, 1, 4, 9, 1 }), 0, true, null, new UiPhotoModel(),
            t("reallife_ui_prompt_move", "Move"), t("reallife_ui_prompt_select", "Select"), t("reallife_ui_prompt_back", "Back"), m);
    }

    /// <summary>The viewfinder over a street: a state is photo, saved, selfie, portrait, wide, timer, zoom, night or clean.</summary>
    public static void Viewfinder(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state)
    {
        bool selfie = state == "selfie", portrait = state == "portrait", wide = state == "wide", night = state == "night";
        bool zoomed = state == "zoom";
        string[] filters =
        {
            t("reallife_phonecam_filter_none", "Natural"), t("reallife_phonecam_filter_warm", "Warm"), t("reallife_phonecam_filter_cool", "Cool"),
            t("reallife_phonecam_filter_mono", "Black and white"), t("reallife_phonecam_filter_vivid", "Vivid"), t("reallife_phonecam_filter_fade", "Fade"),
            t("reallife_phonecam_filter_sepia", "Sepia"), t("reallife_phonecam_filter_golden", "Golden"), t("reallife_phonecam_filter_matte", "Matte"),
            t("reallife_phonecam_filter_chrome", "Chrome"), t("reallife_phonecam_filter_dream", "Dream"),
        };
        var tools = new System.Collections.Generic.List<UiFinderTool>
        {
            new UiFinderTool { Id = 0, Icon = UiIcon.Timer, On = state == "timer", Label = state == "timer" ? t("reallife_phonecam_timer_s", "{seconds} s").Replace("{seconds}", "3") : t("reallife_phonecam_tool_timer", "Timer") },
            new UiFinderTool { Id = 1, Icon = UiIcon.Grid, On = true, Label = t("reallife_phonecam_guide_thirds", "Thirds") },
        };
        if (!selfie)
        {
            tools.Add(new UiFinderTool { Id = 2, Icon = UiIcon.Level, On = state == "photo" || zoomed, Label = t("reallife_phonecam_tool_level", "Level") });
        }
        tools.Add(new UiFinderTool { Id = 3, Icon = UiIcon.Crop, On = state == "wide", Label = t(state == "wide" ? "reallife_phonecam_aspect_43" : "reallife_phonecam_aspect_169", state == "wide" ? "4:3" : "16:9") });
        if (night)
        {
            tools.Add(new UiFinderTool { Id = 4, Icon = UiIcon.Moon, On = true, Label = t("reallife_phonecam_tool_boost", "Night boost") });
        }
        if (!selfie)
        {
            tools.Add(new UiFinderTool { Id = 5, Icon = UiIcon.EyeSlash, On = false, Label = t("reallife_phonecam_tool_body", "Hide me") });
        }
        else
        {
            tools.Add(new UiFinderTool { Id = 7, Icon = UiIcon.Person, On = false, Label = t("reallife_phonecam_pose", "Pose: {pose}").Replace("{pose}", "Wave") });
        }
        tools.Add(new UiFinderTool { Id = 6, Icon = UiIcon.FrameCorners, On = false, Label = t("reallife_phonecam_tool_clean", "Clean view") });
        string[] lenses = wide ? new[] { "0.4x", "0.6x", "1.0x" } : portrait ? new[] { "1.0x", "1.5x", "2.5x" } : new[] { "1.0x", "2.0x", "5.0x" };
        var m = new UiFinderModel
        {
            Title = t("reallife_phonecam_app", "Camera"),
            Modes = new[] { t("reallife_ui_life_mode_photo", "Photo"), t("reallife_ui_life_mode_selfie", "Selfie"), t("reallife_ui_life_mode_portrait", "Portrait"), t("reallife_ui_life_mode_wide", "Wide") },
            Mode = selfie ? 1 : portrait ? 2 : wide ? 3 : 0,
            Place = "7th Avenue, Midtown", When = (night ? "21:12, " + t("reallife_ui_hud_weather_clear", "Clear") : "08:45, " + t("reallife_ui_hud_weather_sunny", "Sunny")),
            Count = t("reallife_ui_life_photos_today", "{count} photos today").Replace("{count}", "9"),
            Filters = filters, Filter = zoomed ? 1 : 0,
            Frames = new[] { t("reallife_phonecam_frame_none", "No frame"), t("reallife_phonecam_frame_polaroid", "Polaroid"), t("reallife_phonecam_frame_film", "Film"), t("reallife_phonecam_frame_border", "Border") },
            Frame = 0,
            FilterTitle = t("reallife_phonecam_filter_title", "Filter"), FrameTitle = t("reallife_phonecam_frame_title", "Frame"),
            Toast = state == "saved" ? t("reallife_ui_life_saved", "Saved to the gallery") : null,
            Open = state == "saved" ? t("reallife_ui_life_open", "Open") : null,
            Latest = "viewfinder:latest",
            Newest = t("reallife_ui_life_newest_photo", "Newest photo"), OpenGallery = t("reallife_ui_life_open_gallery", "Open Gallery"), Shoot = t("reallife_ui_life_take", "Take a photo"),
            Zoom = selfie ? null : zoomed ? "2.4x" : wide ? "0.4x" : "1.0x",
            Lenses = selfie ? new string[0] : lenses, Lens = selfie ? -1 : zoomed ? -1 : 0,
            ZoomOut = !(wide || state == "photo"), ZoomIn = true,
            Exposure = night ? "+0.5" : "0.0", ExposureDown = true, ExposureUp = true,
            Tools = tools.ToArray(),
            Countdown = state == "timer" ? "3" : null,
            Clean = state == "clean", CleanBack = t("reallife_phonecam_clean_back", "Show controls"),
            Move = t("reallife_ui_prompt_move", "Move"), Select = t("reallife_ui_prompt_select", "Select"), Back = t("reallife_ui_prompt_back", "Back"),
        };
        LifePages.Finder(s, m, f, dark, k);
    }
}
