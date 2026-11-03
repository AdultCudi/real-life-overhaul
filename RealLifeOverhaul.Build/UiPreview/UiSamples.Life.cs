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

    private static UiNavEntry[] GalleryNav(UiWords t, int all, int selfies) => new[]
    {
        new UiNavEntry { Icon = UiIcon.Image, Label = t("reallife_gallery_tab_all", "All photos"), Count = all.ToString() },
        new UiNavEntry { Icon = UiIcon.Camera, Label = t("reallife_gallery_tab_selfies", "Selfies"), Count = selfies.ToString() },
    };

    private static UiAlbumModel Album(UiWords t, int count, bool oldest, bool fav, bool selecting)
    {
        var photos = new UiAlbumPhoto[count];
        int per = count >= 8 ? 8 : Math.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            int day = 40 - i / per;
            int onDay = Math.Min(per, count - (i / per) * per);
            photos[i] = new UiAlbumPhoto
            {
                Day = day,
                DayLabel = t("reallife_ui_life_day_band", "Day {day}").Replace("{day}", day.ToString()),
                DayMeta = t("reallife_ui_life_photos_day", "{count} photos").Replace("{count}", onDay.ToString()),
                Favourite = i % 7 == 0,
                Picked = selecting && i < 3,
            };
        }
        return new UiAlbumModel
        {
            Photos = photos, Count = count,
            SortLabel = t(oldest ? "reallife_ui_life_sort_old" : "reallife_ui_life_sort_new", oldest ? "Oldest first" : "Newest first"),
            FavLabel = t("reallife_ui_life_favourites", "Favourites"), FavOn = fav,
            SelectLabel = t("reallife_ui_life_select", "Select"), Selecting = selecting,
            DeleteLabel = t("reallife_ui_life_delete_selected", "Delete selected"), DeleteCount = selecting ? 3 : 0,
            CountLabel = t("reallife_ui_life_album_count", "{count} photos").Replace("{count}", count.ToString()),
            EmptyTitle = t("reallife_ui_life_empty_gallery_title", "No photos yet"),
            EmptyBody = t("reallife_ui_life_empty_gallery_body", "Open the camera and take one. It shows here, newest first."),
        };
    }

    /// <summary>The album: a few photos, a full gallery, an empty one, or select mode.</summary>
    public static void GalleryGrid(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, int count, bool selecting)
    {
        int selfies = count == 0 ? 0 : Math.Max(1, count / 8);
        LifePages.Gallery(s, f, dark, k, t("reallife_gallery_app", "Gallery"), GalleryNav(t, count, selfies), 0, false,
            Album(t, count, false, false, selecting), null,
            t("reallife_ui_prompt_move", "Move"), t("reallife_ui_prompt_select", "Select"), t("reallife_ui_prompt_back", "Back"));
    }

    /// <summary>One photo open, with its facts, its tags and its neighbours.</summary>
    public static void GalleryViewer(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var photo = new UiPhotoModel
        {
            Title = t("reallife_ui_life_at_night", "{place} at night").Replace("{place}", "Broadway"),
            Caption = "Mon, day 2, 21:00",
            Position = t("reallife_ui_life_of_today", "{at} of {count} today").Replace("{at}", "4").Replace("{count}", "9"),
            FactsTitle = t("reallife_ui_life_facts", "Photo"),
            Facts = new[]
            {
                new UiPhotoFact { Label = t("reallife_ui_life_fact_taken", "Taken"), Value = "Mon, day 2, 21:00" },
                new UiPhotoFact { Label = t("reallife_ui_life_fact_place", "Place"), Value = "Broadway, Midtown", Link = true },
                new UiPhotoFact { Label = t("reallife_ui_life_fact_camera", "Camera"), Value = t("reallife_ui_life_camera_rear", "Rear camera") },
                new UiPhotoFact { Label = t("reallife_ui_life_fact_filter", "Filter"), Value = t("reallife_phonecam_filter_warm", "Warm") },
                new UiPhotoFact { Label = t("reallife_ui_life_fact_frame", "Frame"), Value = t("reallife_phonecam_frame_none", "No frame") },
                new UiPhotoFact { Label = t("reallife_ui_life_fact_size", "Size"), Value = "1.2 MB" },
            },
            TagsTitle = t("reallife_ui_life_tags", "Tags"),
            Tags = new[] { "Broadway", "Midtown", t("reallife_ui_life_tag_night", "Night lights") },
            WhereTitle = t("reallife_ui_life_where", "Where it went"),
            Where = t("reallife_ui_life_flare_line", "Posted on Flare by {name}: {likes} likes, {visits} visits, {money}.")
                .Replace("{name}", "Speedy Bites").Replace("{likes}", "1.2K").Replace("{visits}", "46").Replace("{money}", "+$1,180"),
            Neighbours = new[] { "a", "b", "c", "d", "e", "f", "g" }, NeighbourAt = 3,
            Actions = new[]
            {
                new UiActionModel { Label = t("reallife_ui_life_share", "Share"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Icon = UiIcon.Send },
                new UiActionModel { Label = t("reallife_ui_life_wallpaper", "Set as wallpaper"), Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                new UiActionModel { Icon = UiIcon.Heart, Kind = UiButtonKind.Secondary },
                new UiActionModel { Icon = UiIcon.Trash, Kind = UiButtonKind.Destructive },
            },
        };
        LifePages.Gallery(s, f, dark, k, t("reallife_gallery_app", "Gallery"), GalleryNav(t, 9, 2), 0, true, null, photo,
            t("reallife_ui_prompt_move", "Move"), t("reallife_ui_prompt_select", "Select"), t("reallife_ui_prompt_back", "Back"));
    }

    /// <summary>The viewfinder over a street, with a saved toast.</summary>
    public static void Viewfinder(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool toast)
    {
        var m = new UiFinderModel
        {
            Modes = new[] { t("reallife_ui_life_mode_photo", "Photo"), t("reallife_ui_life_mode_selfie", "Selfie") }, Mode = 0,
            Place = "7th Avenue, Midtown", When = "08:45, " + t("reallife_ui_hud_weather_sunny", "Sunny"),
            Count = t("reallife_ui_life_photos_today", "{count} photos today").Replace("{count}", "9"),
            Filters = new[]
            {
                t("reallife_phonecam_filter_none", "Natural"), t("reallife_phonecam_filter_warm", "Warm"), t("reallife_phonecam_filter_cool", "Cool"),
                t("reallife_phonecam_filter_mono", "Black and white"), t("reallife_phonecam_filter_vivid", "Vivid"), t("reallife_phonecam_filter_fade", "Fade"),
            },
            Filter = 0,
            Frames = new[] { t("reallife_phonecam_frame_none", "No frame"), t("reallife_phonecam_frame_border", "Border"), t("reallife_phonecam_frame_film", "Film") },
            Frame = 0, Grid = true,
            Toast = toast ? t("reallife_ui_life_saved", "Saved to the gallery") : null,
            Open = toast ? t("reallife_ui_life_open", "Open") : null,
            Move = t("reallife_ui_prompt_move", "Move"), Select = t("reallife_ui_prompt_select", "Select"), Back = t("reallife_ui_prompt_back", "Back"),
        };
        LifePages.Finder(s, m, f, dark, k);
    }
}
