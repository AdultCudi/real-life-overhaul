using System;
using RealLifeOverhaul.Ui;

// The activity kit's samples (Scripts/Ui/Activity): the game's activity panel before it starts as the side card and while
// it runs as the strip above the status bar (a working day, a class at a desk, a desk session's time in the chair, cooking
// at home, a bus ride), the time skip's veil, the time of day tool and the funeral. Made-up places and people; the game's
// own words through its keys.
internal static partial class UiSamples
{
    /// <summary>The longest language's words for the activity's own lines (German, as the game writes them there).</summary>
    public static bool ActivityGerman;

    private static string Pick(string english, string german) => ActivityGerman ? german : english;

    private static UiSideButton GameButton(string label, UiButtonKind kind, UiGameKey key, bool left = false) =>
        new UiSideButton { Label = label, Kind = kind, Key = UiAction.Count, GameKey = key, Left = left };

    // ------------------------------------------------------------------------------------------------- a working day

    /// <summary>A working day at a studio: before it starts, the card with its facts and the hours to stay; while it runs, the strip.</summary>
    public static UiActivityModel ActivityWork(UiWords t, IUiKeys k, UiActivityState state)
    {
        var m = new UiActivityModel
        {
            State = state, Icon = UiIcon.Briefcase, Bar = true,
            Headline = state == UiActivityState.NotStarted ? Pick("Work as a junior game designer", "Als Junior-Spieledesigner arbeiten") : t("reallife_ui_activity_work_strip", "Working day"),
            Kicker = t("reallife_ui_activity_work_kicker", "Working day, {day}").Replace("{day}", Pick("Monday", "Montag")),
            Title = t("reallife_ui_activity_work_title", "Start your shift"),
            Body = t("reallife_ui_activity_work_body", "You are at your desk. The day passes in the world while you work, and you can skip it at any time. Choose how long to stay."),
            Place = "Northgate Studio", PlaceLines = new[] { Pick("14th floor, 7 Fifth Avenue", "14. Etage, 7 Fifth Avenue"), "Midtown" },
            Person = "Ines Calloway", PersonLine = Pick("Ines Calloway, studio lead", "Ines Calloway, Studioleitung"),
            Lines = new[]
            {
                new UiInfoRow { Label = t("reallife_ui_activity_pay", "Pay"), Value = t("reallife_ui_activity_pay_hour", "{money} an hour").Replace("{money}", "$38") },
                new UiInfoRow { Label = t("reallife_ui_activity_hours", "Hours"), Value = Pick("09:00 to 17:00, Monday to Friday", "09:00 bis 17:00, Montag bis Freitag") },
                new UiInfoRow
                {
                    Label = t("reallife_ui_activity_skill", "Skill"), Value = t("reallife_ui_activity_skill_day", "{skill} +{amount} this shift").Replace("{skill}", Pick("Game design", "Spieldesign")).Replace("{amount}", "6"),
                    Ink = UiRole.OkText,
                },
                new UiInfoRow { Label = t("reallife_ui_activity_standing", "Standing with the team"), Value = "+3", Ink = UiRole.OkText },
                new UiInfoRow { Label = t("reallife_ui_activity_stress", "Stress"), Value = "+9%", Ink = UiRole.WarnText },
            },
            HasDuration = true, DurationLabel = t("reallife_ui_activity_work_for", "Work for"), DurationValue = t("common_hours", "{hours} hours").Replace("{hours}", "6"), DurationShare = 5f / 7f,
            DurationFrom = t("common_hours_abbr", "{hours} h").Replace("{hours}", "1"), DurationTo = t("common_hours_abbr", "{hours} h").Replace("{hours}", "8"),
            Finish = t("reallife_ui_activity_finish", "You finish at {time}").Replace("{time}", "15:00"), Sum = t("reallife_ui_activity_before_tax", "{money} before tax").Replace("{money}", "$228"),
            Buttons = new[]
            {
                GameButton(t("common_back", "Back"), UiButtonKind.Secondary, GameKey(k, "Esc", UiIcon.PadB), true),
                GameButton(t("playeractivityui_timemachine", "Skip time"), UiButtonKind.Secondary, GameKey(k, "O", UiIcon.PadRb)),
                GameButton(t("workui_start", "Start working"), UiButtonKind.Primary, GameKey(k, "F", UiIcon.PadX)),
            },
            HasProgress = state == UiActivityState.Running || state == UiActivityState.Started,
            Progress = new UiProgressModel { Share = 0.55f }, MeterLeft = t("reallife_ui_activity_hour_of", "Hour {hour} of {hours}").Replace("{hour}", "4").Replace("{hours}", "6"),
            MeterRight = t("reallife_ui_activity_earned", "Earned {money}").Replace("{money}", "$152"),
            Clock = state == UiActivityState.NotStarted ? "08:45" : "12:45",
        };
        Running(m, t, k, state, t("reallife_ui_activity_skip_end", "Skip to the end"));
        return m;
    }

    /// <summary>
    /// A shift of the drinks cart left to the clock, the card the gig jobs now have: the stress and the skill the hours give, the slider
    /// from one hour to the whole shift with the finish and the sum before tax; while it runs, the hour and what is earned.
    /// </summary>
    public static UiActivityModel ActivityCart(UiWords t, IUiKeys k, UiActivityState state)
    {
        bool before = state == UiActivityState.NotStarted;
        var m = new UiActivityModel
        {
            State = state, Icon = UiIcon.Coffee, Bar = true,
            Headline = before ? Pick("Work the drinks cart", "Am Getraenkewagen arbeiten") : t("reallife_ui_activity_work_strip", "Working day"),
            Place = Pick("Drinks cart", "Getraenkewagen"), PlaceLines = new[] { "Fifth Avenue", "Midtown" },
            Lines = new[]
            {
                new UiInfoRow { Label = Pick("Pay", "Lohn"), Value = Pick("12 customers, about $184 at 85%", "12 Kunden, etwa 184 $ bei 85 %") },
                new UiInfoRow
                {
                    Label = t("reallife_ui_activity_stress", "Stress"), Value = "+6%", Ink = UiRole.WarnText,
                },
                new UiInfoRow
                {
                    Label = t("reallife_ui_activity_skill", "Skill"), Value = t("reallife_ui_activity_skill_day", "{skill} +{amount} this shift").Replace("{skill}", Pick("Customer service", "Kundenservice")).Replace("{amount}", "0.24"),
                    Ink = UiRole.OkText,
                },
            },
            HasDuration = before, DurationLabel = t("reallife_ui_activity_how_long", "How long"), DurationValue = t("common_hours", "{hours} hours").Replace("{hours}", "2"), DurationShare = 1f / 3f,
            DurationFrom = t("common_hours_abbr", "{hours} h").Replace("{hours}", "1"), DurationTo = t("common_hours_abbr", "{hours} h").Replace("{hours}", "4"),
            Finish = t("reallife_ui_activity_finish", "You finish at {time}").Replace("{time}", "12:00"), Sum = t("reallife_ui_activity_before_tax", "{money} before tax").Replace("{money}", "$92"),
            Buttons = new[]
            {
                GameButton(t("common_cancel", "Cancel"), UiButtonKind.Secondary, GameKey(k, "Esc", UiIcon.PadB), true),
                GameButton(t("workui_start", "Start working"), UiButtonKind.Primary, GameKey(k, "F", UiIcon.PadX)),
            },
            HasProgress = !before, Progress = new UiProgressModel { Share = 0.4f },
            MeterLeft = t("reallife_ui_activity_hour_of", "Hour {hour} of {hours}").Replace("{hour}", "1").Replace("{hours}", "2"),
            MeterRight = t("reallife_ui_activity_earned", "Earned {money}").Replace("{money}", "$31"), Clock = before ? "10:00" : "10:50",
        };
        if (!before)
        {
            m.Kicker = t("reallife_ui_activity_work_strip", "Working day");
        }
        Running(m, t, k, state, t("reallife_ui_activity_skip_end", "Skip to the end"));
        return m;
    }

    // The strip's parts as the game's panel gives them while it runs: the time machine, fast forward, Stop on Escape.
    private static void Running(UiActivityModel m, UiWords t, IUiKeys k, UiActivityState state, string skip)
    {
        m.CanSkip = state != UiActivityState.Finished;
        m.SkipLabel = skip ?? t("playeractivityui_timemachine", "Skip time");
        m.SkipKey = GameKey(k, "O", UiIcon.PadRb);
        m.CanFast = state == UiActivityState.Running || state == UiActivityState.Started;
        m.Fast = state == UiActivityState.Running;
        m.FastLabel = t("reallife_ui_activity_fast_speed", "Fast forward x{speed}").Replace("{speed}", "6");
        m.FastKey = GameKey(k, "P", UiIcon.PadY);
        m.StripButtons = state == UiActivityState.Moving
            ? new[] { GameButton(t("common_cancel", "Cancel"), UiButtonKind.Secondary, GameKey(k, "Esc", UiIcon.PadB)) }
            : new[] { GameButton(t("workui_stop", "Stop"), UiButtonKind.Secondary, GameKey(k, "Esc", UiIcon.PadB)) };
        m.StateWords = state == UiActivityState.Moving ? t("reallife_ui_activity_moving", "On your way") : state == UiActivityState.Waiting ? t("reallife_ui_activity_waiting", "Waiting for it to start")
            : state == UiActivityState.Finished ? t("reallife_ui_activity_done", "Done") : null;
    }

    // ------------------------------------------------------------------------------------------------- a class at a desk

    /// <summary>A class at a college desk before it starts: its fee and what it teaches, the hours to sit.</summary>
    public static UiActivityModel ActivityClass(UiWords t, IUiKeys k)
    {
        return new UiActivityModel
        {
            State = UiActivityState.NotStarted, Icon = UiIcon.Cap, Bar = true, Kicker = "Hillcrest College, Monday", Title = "Marketing basics, lesson 3 of 8",
            Place = "Hillcrest College", PlaceLines = new[] { "Room 204, 18 Elm Street", "Garment District" },
            Lines = new[]
            {
                new UiInfoRow { Label = "Fee", Value = "$40" },
                new UiInfoRow { Label = t("reallife_ui_activity_skill", "Skill"), Value = "Marketing +4", Ink = UiRole.OkText },
                new UiInfoRow { Label = "Energy", Value = "-12", Ink = UiRole.WarnText },
            },
            HasDuration = true, DurationLabel = t("reallife_ui_activity_how_long", "How long"), DurationValue = t("common_hours", "{hours} hours").Replace("{hours}", "2"), DurationShare = 1f / 3f,
            DurationFrom = t("common_hours_abbr", "{hours} h").Replace("{hours}", "1"), DurationTo = t("common_hours_abbr", "{hours} h").Replace("{hours}", "4"),
            Finish = t("reallife_ui_activity_finish", "You finish at {time}").Replace("{time}", "12:00"),
            Buttons = new[]
            {
                GameButton(t("common_back", "Back"), UiButtonKind.Secondary, GameKey(k, "Esc", UiIcon.PadB), true),
                GameButton(t("playeractivityui_timemachine", "Skip time"), UiButtonKind.Secondary, GameKey(k, "O", UiIcon.PadRb)),
                GameButton("Start the class", UiButtonKind.Primary, GameKey(k, "F", UiIcon.PadX)),
            },
            Clock = "10:00",
        };
    }

    /// <summary>
    /// Rest on a bench with the slider at its most: the game writes its slider line as two sentences ("Rest for 24 hours, 0
    /// minutes. Will stop resting at 19:15"); the card puts the first beside the label and the second under the track.
    /// </summary>
    public static UiActivityModel ActivityRest(UiWords t, IUiKeys k)
    {
        return new UiActivityModel
        {
            State = UiActivityState.NotStarted, Icon = UiIcon.Sofa, Bar = true, Headline = Pick("Rest on bench", "Auf der Bank ausruhen"), Place = "7 4th Avenue", PlaceLines = new[] { "Garment District" },
            HasDuration = true, DurationLabel = t("reallife_ui_activity_how_long", "How long"), DurationShare = 1f,
            DurationValue = Pick("Rest for 24 hours, 0 minutes. Will stop resting at 19:15", "24 Stunden, 0 Minuten ausruhen. Die Ruhe endet um 19:15 Uhr"),
            Buttons = new[]
            {
                GameButton(t("common_cancel", "Cancel"), UiButtonKind.Secondary, GameKey(k, "Esc", UiIcon.PadB), true),
                GameButton(Pick("Start resting", "Ausruhen"), UiButtonKind.Primary, GameKey(k, "Enter", UiIcon.PadX)),
            },
            Clock = "19:15",
        };
    }

    // ------------------------------------------------------------------------------------------------- the strip's others

    /// <summary>A desk session's time in the chair: the strip with the session's meter and Stop.</summary>
    public static UiActivityModel ActivityChair(UiWords t, IUiKeys k)
    {
        var m = new UiActivityModel
        {
            State = UiActivityState.Running, Icon = UiIcon.Presentation, Bar = true, Headline = "Sales meeting", Place = "Ironbridge Trading, your office",
            HasProgress = true, Progress = new UiProgressModel { Share = 0.4f },
            MeterLeft = t("reallife_ui_activity_hour_of", "Hour {hour} of {hours}").Replace("{hour}", "1").Replace("{hours}", "2"), MeterRight = "1 h 12 min", Clock = "15:48",
        };
        Running(m, t, k, m.State, null);
        return m;
    }

    /// <summary>Cooking at home, watched for a while before the time skip takes the rest.</summary>
    public static UiActivityModel ActivityCook(UiWords t, IUiKeys k)
    {
        var m = new UiActivityModel
        {
            State = UiActivityState.Started, Icon = UiIcon.ForkKnife, Bar = true, Headline = "Cooking dinner", Place = "Home, 40 Mercer Street",
            HasProgress = true, Progress = new UiProgressModel { Share = 0.2f, Label = "25 minutes remaining" }, Clock = "19:05",
        };
        Running(m, t, k, m.State, null);
        return m;
    }

    /// <summary>A bus ride across the city: the stops to go, the time skip to the last one.</summary>
    public static UiActivityModel ActivityBus(UiWords t, IUiKeys k)
    {
        var m = new UiActivityModel
        {
            State = UiActivityState.Running, Icon = UiIcon.Train, Bar = true, Headline = "Bus to Midtown", Place = "Line 4, Fifth Avenue",
            HasProgress = true, Progress = new UiProgressModel { Share = 0.6f }, MeterLeft = "3 stops to go", MeterRight = "12 min", Clock = "08:12",
        };
        Running(m, t, k, m.State, null);
        m.CanFast = false;
        m.StripButtons = new[] { GameButton("Get off", UiButtonKind.Secondary, GameKey(k, "Esc", UiIcon.PadB)) };
        return m;
    }

    /// <summary>A card over the dimmed world, the bar under it as the game's HUD keeps it.</summary>
    public static void ActivityCardScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiActivityModel a, bool night = false)
    {
        UiStatusModel bar = HudBar(t, true, false, night);
        bar.Time = a.Clock;
        WithBar(Over(bar), () => UiActivity.LayCard(s, a, f, dark, k));
    }

    /// <summary>The strip above the HUD's bar.</summary>
    public static void ActivityStripScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiActivityModel a, bool night = false)
    {
        UiStatusModel bar = HudBar(t, false, a.Fast, night);
        bar.Time = a.Clock;
        Hud(s, f, dark, k, t, new UiHudModel { Activity = a, MoneyChange = false }, bar);
    }

    // ------------------------------------------------------------------------------------------------- the time skip

    /// <summary>The time machine skipping to the end of a working day: the ring two thirds round, the goal, what is left.</summary>
    public static UiTimeSkipModel ActivitySkip(UiWords t, bool info)
    {
        return new UiTimeSkipModel
        {
            Title = t("playeractivityui_timemachine", "Skip time"), Clock = "14:20", Day = DayOf(t, 0, 2),
            Goal = info ? "You're being taken to the closest medical center" : t("reallife_ui_skip_to_end", "Skipping to the end at"), GoalTime = info ? null : "17:00",
            Pace = info ? t("reallife_ui_skip_pace_until", "Fast forward x{speed}. Until {time}, {left} to go").Replace("{speed}", "60").Replace("{time}", "16:00").Replace("{left}", t("reallife_ui_skip_hours_minutes", "{hours} h {minutes} min").Replace("{hours}", "1").Replace("{minutes}", "40"))
                : t("reallife_ui_skip_pace", "Fast forward x{speed}, {left} to go").Replace("{speed}", "60").Replace("{left}", t("reallife_ui_skip_hours_minutes", "{hours} h {minutes} min").Replace("{hours}", "2").Replace("{minutes}", "40")),
            Share = 0.67f, CanCancel = !info, CancelLabel = t("common_cancel", "Cancel"),
            Prompts = info ? new UiPrompt[0] : new[] { new UiPrompt { Action = UiAction.Back, Label = t("common_cancel", "Cancel") } }, PromptCount = info ? 0 : 1,
        };
    }

    public static void ActivitySkipScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool info = false)
    {
        UiStatusModel bar = HudBar(t, false, true, false);
        bar.Time = "14:20";
        bar.Day = DayOf(t, 0, 2);
        WithBar(Over(bar), () => UiTimeSkip.Lay(s, ActivitySkip(t, info), f, dark, k));
    }

    // ------------------------------------------------------------------------------------------------- the time of day tool

    public static UiTimeOfDayModel ActivityDayTool(UiWords t) => new UiTimeOfDayModel
    {
        Title = t("reallife_ui_time_of_day", "Time of day"), Clock = "19:40", Share = (1180f - 300f) / (1440f - 300f), From = "05:00", To = "24:00",
        Earlier = t("reallife_ui_time_of_day_earlier", "10 min earlier"), Later = t("reallife_ui_time_of_day_later", "10 min later"), Bar = true,
    };

    public static void ActivityDayToolScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        UiStatusModel bar = HudBar(t, false, false, true);
        WithBar(Over(bar), () => UiTimeOfDay.Lay(s, ActivityDayTool(t), f, dark, k));
    }

    // ------------------------------------------------------------------------------------------------- the funeral

    /// <summary>The funeral's card: the late character, the words, the game's ways on; the backup's row only with a backup.</summary>
    public static UiSideCardModel ActivityFuneral(UiWords t, IUiKeys k, bool backup)
    {
        const string name = "Theo Marlow";
        var choices = new System.Collections.Generic.List<UiSideChoice>(3);
        if (backup)
        {
            choices.Add(new UiSideChoice
            {
                Name = t("reallife_ui_funeral_backup", "Load the backup"), Line = t("reallife_ui_funeral_backup_when", "Return to day {day}, saved {when}").Replace("{day}", "211").Replace("{when}", "3 Nov, 06:10"),
            });
        }
        choices.Add(new UiSideChoice
        {
            Name = t("reallife_ui_funeral_menu", "Back to the main menu"), Line = t("reallife_ui_funeral_menu_line", "Pick another save or start a new city"),
        });
        choices.Add(new UiSideChoice { Name = t("reallife_ui_funeral_quit", "Quit to the desktop"), Line = t("reallife_ui_funeral_quit_line", "Close the game") });
        var buttons = new System.Collections.Generic.List<UiSideButton>
        {
            GameButton(t("reallife_ui_funeral_menu_short", "Main menu"), backup ? UiButtonKind.Secondary : UiButtonKind.Primary, GameKey(k, "P", UiIcon.PadY)),
        };
        if (backup)
        {
            buttons.Add(GameButton(t("reallife_ui_funeral_backup", "Load the backup"), UiButtonKind.Primary, GameKey(k, "F", UiIcon.PadX)));
        }
        return new UiSideCardModel
        {
            Portrait = name, SideName = name,
            SideLines = new[] { t("reallife_ui_funeral_age", "Aged {age}").Replace("{age}", "41"), t("reallife_ui_funeral_days", "Day {day} in the city").Replace("{day}", "214") },
            Kicker = t("reallife_ui_funeral_kicker", "The end of a life, {place}").Replace("{place}", "Midtown"), Title = t("reallife_ui_funeral_title", "The funeral"),
            // The life as the obituary words it, the strongest rival's flowers last; names are the sample's own.
            Body = t("reallife_pd_obit_lived", "{name} had lived in the city for {years}.").Replace("{name}", name).Replace("{years}", "3 years")
                   + " " + t("reallife_ui_funeral_founded_many", "They founded {founded} businesses.").Replace("{founded}", "4")
                   + " " + t("reallife_ui_funeral_staff_many", "{employees} people worked for them.").Replace("{employees}", "37")
                   + " " + t("reallife_ui_funeral_rival", "{rival} sent flowers but did not stay.").Replace("{rival}", "Bald Luthor"),
            Choices = choices.ToArray(), Selected = 0, Note = backup ? t("reallife_ui_funeral_note", "Press 1, 2 or 3") : t("reallife_ui_funeral_note_two", "Press 1 or 2"),
            Buttons = buttons.ToArray(),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_funeral_quit_short", "Quit") },
            },
            PromptCount = 3,
        };
    }

    public static void ActivityFuneralScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool backup = true)
    {
        UiStatusModel bar = HudBar(t, true, false, true);
        WithBar(Over(bar), () => UiSideCard.Lay(s, ActivityFuneral(t, k, backup), f, dark, k, "funeral"));
    }
}
