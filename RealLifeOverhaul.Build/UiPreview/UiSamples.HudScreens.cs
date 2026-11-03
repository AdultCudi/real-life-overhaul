using System;
using RealLifeOverhaul.Ui;

/// <summary>
/// The HUD's live pieces over the running city (Scripts/Ui/HudScreens), each as the game's own would fill it on a Monday
/// morning in Midtown or a rainy autumn night: the side panels with the mod's shift card, the game's current job and the
/// building the player stands in; the objectives as the game's window lists them; the short notices as cards at the top;
/// the top bar's small buttons in their pill with the dances and the accessories over it; and the game's control hints as
/// key chips. The words a screen's entry names come through their keys; names, places and figures are the sample city's.
/// </summary>
internal static partial class UiSamples
{
    // The status bar's own words: its entry (hud) names them, so these scenes give the bar its English as it stands.
    private static string BarWords(string key, string english) => english;

    /// <summary>
    /// The bar the HUD's pieces stand over: a spring morning, or an autumn night in the rain; with the top bar's small
    /// buttons in their pill (the dances, the accessories, the help), <paramref name="open"/> the ones whose panel is up.
    /// </summary>
    public static UiStatusModel PiecesBar(bool night, params int[] open)
    {
        UiStatusModel m = HudBar(BarWords, false, false, night);
        m.Time = night ? "22:47" : "08:45";
        m.Day = night ? "Friday (Day 415)" : "Monday (Day 2)";
        m.Temperature = night ? "9°C" : "18°C";
        m.Season = night ? "Autumn, rain" : "Spring, sunny";
        m.Place = night ? "1250 Avenue of the Americas" : "7 5th Avenue";
        m.District = night ? "Lower Manhattan" : "Midtown";
        m.MoneyDay = BarWords("reallife_ui_hud_money_today", "{amount} today").Replace("{amount}", night ? "-$385" : "+$1,240");
        m.MoneyTrend = night ? -1 : 1;
        m.Tools = new[] { UiIcon.MusicNotes, UiIcon.Cap, UiIcon.Info };
        m.ToolsOn = new bool[3];
        m.ToolsOff = new bool[3];
        m.ToolCount = 3;
        foreach (int i in open)
        {
            m.ToolsOn[i] = true;
        }
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the side panels

    /// <summary>
    /// The side panels: at work in the morning, the shift card open with its meter, the job folded, the player's own florist
    /// open with its switch, its two buttons and the walls; at night, the job open after the day's shift and a café the
    /// player does not own, closed.
    /// </summary>
    public static UiSidePanelModel[] PiecesSides(UiWords t, bool night)
    {
        var job = new UiSidePanelModel
        {
            Title = t("reallife_ui_hud_job", "Current job"), Meta = "Sales assistant", Icon = UiIcon.Briefcase, Collapsed = !night,
            HeadName = "Jonas Foods", HeadSub = "4 7th Avenue",
            Rows = new[]
            {
                new UiInfoRow { Label = t("common_today", "Today"), Value = night ? "Finished" : "09:00 - 17:00", Ink = night ? UiRole.OkText : UiRole.None },
                new UiInfoRow { Label = t("common_tomorrow", "Tomorrow"), Value = "09:00 - 17:00" },
            },
            Actions = new[]
            {
                new UiActionModel { Label = t("common_setdestination", "Set destination"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Count },
                new UiActionModel { Label = t("playerhud_currentjob_quit_job", "Quit Job"), Kind = UiButtonKind.Destructive, Key = UiAction.Count },
            },
        };
        if (night)
        {
            var cafe = new UiSidePanelModel
            {
                Title = "1250 Avenue of the Americas", Icon = UiIcon.Building,
                HeadName = "Corner Perk", HeadSub = "Coffee shop, Lower Manhattan", HeadChip = t("common_closed", "Closed"), HeadTone = UiRole.Danger,
                Rows = new[] { new UiInfoRow { Label = t("bizman_schedule_opening_hours", "Opening hours"), Value = "07:00 - 19:00" } },
                Actions = new[] { new UiActionModel { Label = t("open_in_bizman", "Open in BizMan"), Kind = UiButtonKind.Primary, Key = UiAction.Count } },
                Tool = UiIcon.Grid,
            };
            return new[] { job, cafe };
        }
        var shift = new UiSidePanelModel
        {
            Title = t("reallife_ui_hud_shift", "Your shift"), Meta = "Jonas Foods", Icon = UiIcon.Briefcase,
            Rows = new[]
            {
                new UiInfoRow { Label = "On shift 2 h 45 min, about 5 h 15 min left" },
                new UiInfoRow { Label = "Customers served 14" },
                new UiInfoRow { Label = "Earned so far $105", Ink = UiRole.OkText },
            },
            HasProgress = true, Progress = new UiProgressModel { Share = 0.34f },
        };
        var florist = new UiSidePanelModel
        {
            Title = "7 5th Avenue", Meta = t("reallife_ui_hud_your_business", "Your business"), Icon = UiIcon.Store,
            HeadName = "Bloom & Co", HeadSub = "Florist, Midtown", HeadChip = t("common_open", "Open"), HeadTone = UiRole.Ok,
            Rows = new[]
            {
                new UiInfoRow { Label = t("bizman_schedule_opening_hours", "Opening hours"), Value = "08:00 - 20:00" },
                new UiInfoRow { Label = t("reallife_ui_hud_customers", "Customers inside"), Value = "6" },
            },
            SwitchLabel = t("bizman_temporarily_closed", "Temporarily closed"),
            Actions = new[]
            {
                new UiActionModel { Label = t("open_in_bizman", "Open in BizMan"), Kind = UiButtonKind.Primary, Key = UiAction.Count },
                new UiActionModel { Label = t("interior_designer", "Interior designer"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
            },
            Tool = UiIcon.Grid,
        };
        return new[] { shift, job, florist };
    }

    /// <summary>The HUD with its side panels at the top right: at work in the morning, or at night after the shift.</summary>
    public static void PiecesSideScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool night) =>
        Hud(s, f, dark, k, t, new UiHudModel { Side = PiecesSides(t, night) }, PiecesBar(night));

    // ------------------------------------------------------------------------------------------------- the objectives

    /// <summary>
    /// The objectives as the game's window lists them: the business alerts (one the game marks red), the personal goals now
    /// with their progress, Uncle Fred, a delivery mission with its time, Cancel and the way so far, the workout plan and a
    /// construction, each band with its count; or, tight, more than the screen holds, the last groups folded to their bands.
    /// </summary>
    public static UiObjectivesModel PiecesGoals(UiWords t, bool tight)
    {
        var m = new UiObjectivesModel
        {
            Title = t("tutorial_objectives", "objectives"), LocateLabel = t("reallife_ui_hud_locate", "Locate"),
            Meta = t("reallife_ui_hud_goals_open", "{count} open").Replace("{count}", tight ? "14" : "9"),
            EmptyTitle = t("reallife_ui_hud_goals_empty", "Nothing to do right now"), EmptyBody = t("reallife_ui_hud_goals_empty_body", "New tasks, alerts and quests show here as they come."),
        };
        string done = t("reallife_ui_hud_goals_done", "{done} of {count} done");
        var goals = new UiObjectiveGroup
        {
            Title = t("common_personal_goals", "Personal Goals"), Icon = UiIcon.Trophy, Note = done.Replace("{done}", "12").Replace("{count}", "96"), Lines = new[]
            {
                new UiObjectiveLine { Text = t("personalgoal_bank_balance", "Reach a bank balance of {amount}").Replace("{amount}", "$50,000"), Progress = "$31,400" },
                new UiObjectiveLine { Text = t("personalgoal_minemployees", "Employ at least {minimumEmployees} people").Replace("{minimumEmployees}", "2"), Progress = t("reallife_ui_hud_goal_of", "{current} of {amount}").Replace("{current}", "1").Replace("{amount}", "2") },
                new UiObjectiveLine { Text = t("personalgoal_realestate", "Own at least {amount} buildings").Replace("{amount}", "1"), Progress = t("reallife_ui_hud_goal_of", "{current} of {amount}").Replace("{current}", "0").Replace("{amount}", "1") },
            }
        };
        var alerts = new UiObjectiveGroup
        {
            Title = t("bizman_alerts", "Alerts"), Icon = UiIcon.ListChecks, Note = "3", Lines = new[]
            {
                new UiObjectiveLine { Text = "Bloom & Co is dirty and needs to be cleaned", Locate = true, Urgent = true },
                new UiObjectiveLine { Text = "Bloom & Co has no products to sell.", Locate = true },
                new UiObjectiveLine { Text = "Taxes to pay in 3 days" },
            }
        };
        var fred = new UiObjectiveGroup
        {
            Title = t("uncle_fred", "Uncle Fred"), Icon = UiIcon.Person, Note = done.Replace("{done}", "1").Replace("{count}", "2"), Lines = new[]
            {
                new UiObjectiveLine { Text = "Rent apartment at 12 Hudson Street", Locate = true }, new UiObjectiveLine { Text = "Buy a phone", Done = true },
            }
        };
        var delivery = new UiObjectiveGroup
        {
            Title = t("ba:skill_deliverydriver", "Delivery Driver"), Icon = UiIcon.Truck, Note = "3", Lines = new[]
            {
                new UiObjectiveLine { Text = "1 h 40 min", Close = t("common_cancel", "Cancel"), HasShare = true, Share = 0.34f },
                new UiObjectiveLine { Text = "88 Elm Street (1.2 km)", Sub = "24 x Lilies", Locate = true },
                new UiObjectiveLine { Text = "31 Murray Street (2.8 km)", Sub = "6 x Roses, 2 x Vases", Locate = true },
            }
        };
        var workout = new UiObjectiveGroup
        {
            Title = t("workout_plan", "Workout Plan"), Icon = UiIcon.Heartbeat, Collapsed = tight, Note = done.Replace("{done}", "1").Replace("{count}", "2"),
            Lines = new[] { new UiObjectiveLine { Text = "Do Cardio for 30 minutes", Done = true }, new UiObjectiveLine { Text = "Do Weights for 20 minutes" } }
        };
        var build = new UiObjectiveGroup
        {
            Title = t("reallife_bld_track_header", "Construction"), Icon = UiIcon.Hammer, Collapsed = tight, Note = "1",
            Lines = new[] { new UiObjectiveLine { Text = "Annex, 38th Street: frame the first floor", Locate = true } }
        };
        if (!tight)
        {
            m.Groups = new[] { alerts, goals, fred, delivery, workout, build };
            return m;
        }
        var food = new UiObjectiveGroup
        {
            Title = t("food_delivery_job_title", "Food Delivery"), Icon = UiIcon.ForkKnife, Collapsed = true,
            Lines = new[] { new UiObjectiveLine { Text = "Pick up the order at Corner Perk", Locate = true } }
        };
        alerts.Lines = new[]
        {
            alerts.Lines[0], alerts.Lines[1], alerts.Lines[2],
            new UiObjectiveLine { Text = "Corner Perk is out of Coffee", Locate = true }, new UiObjectiveLine { Text = "Business is open but there are no employees working", Locate = true },
        };
        alerts.Note = "5";
        m.Groups = new[] { alerts, goals, fred, delivery, workout, build, food };
        return m;
    }

    /// <summary>The HUD with its objectives at the left: every group, or more than the screen holds.</summary>
    public static void PiecesGoalsScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool tight) =>
        Hud(s, f, dark, k, t, new UiHudModel { Objectives = PiecesGoals(t, tight) }, PiecesBar(tight));

    // ------------------------------------------------------------------------------------------------- the notices

    /// <summary>
    /// The short notices as cards at the top: a personal goal reached with its reward and the next goal, the day's summary as
    /// the game's panel fills it with the day's best earner, and the hospital's word (where and when, the bill, the shift
    /// missed, the energy back) with its Continue; or at night the casino boat's welcome and the way over the bridge.
    /// </summary>
    public static UiHudNoticeModel[] PiecesNotes(UiWords t, bool night)
    {
        if (night)
        {
            return new[]
            {
                new UiHudNoticeModel { Kicker = t("common_casino", "Casino"), Icon = UiIcon.Coin, Body = "Welcome to international waters. Best of luck!" },
                new UiHudNoticeModel
                {
                    Kicker = t("reallife_ui_hud_bridge", "Bridge"), Icon = UiIcon.Car, Title = t("reallife_ui_hud_bridge_title", "You can skip the drive across"),
                    Action = t("playerhud_skip_bridge_button", "Skip over bridge"),
                },
            };
        }
        return new[]
        {
            new UiHudNoticeModel
            {
                Kicker = t("personalgoal_achieved", "Personal goal achieved"), Icon = UiIcon.Trophy, Figure = "+$5,000",
                Title = t("personalgoal_bank_balance", "Reach a bank balance of {amount}").Replace("{amount}", "$10,000"),
                Body = t("reallife_ui_hud_goal_next", "Next goal: {goal}").Replace("{goal}", t("personalgoal_bank_balance", "Reach a bank balance of {amount}").Replace("{amount}", "$50,000")),
            },
            new UiHudNoticeModel
            {
                Kicker = t("daily_summary", "Day {day} Summary").Replace("{day}", "1"), Icon = UiIcon.Chart,
                BigLabel = t("total_profit", "Total profit"), Big = "$2,115", BigTone = UiRole.OkText,
                Rows = new[] { new UiInfoRow { Label = t("daily_summary_businesses", "Businesses"), Value = "3" } }, RowCount = 1,
                Link = t("daily_summary_details", "Details (EconoView)"), LinkIcon = UiIcon.ChartLineUp,
                Footer = t("reallife_ui_hud_best_earner", "Best earner today: {name}").Replace("{name}", "Bloom & Co"),
            },
            new UiHudNoticeModel
            {
                Kicker = t("reallife_ui_hud_hospital", "Hospital"), Icon = UiIcon.Plus, IconTone = UiRole.Danger,
                Title = t("reallife_ui_hud_hospital_title", "You were taken to hospital"), Meta = "08:00",
                Body = t("reallife_ui_hud_hospital_where", "You collapsed near {place} at {time}.").Replace("{place}", "7 5th Avenue").Replace("{time}", "02:14"),
                Chips = new[]
                {
                    t("reallife_ui_hud_hospital_bill", "{amount} bill").Replace("{amount}", "-$2,000"),
                    t("reallife_ui_hud_hospital_missed", "Missed {time} shift").Replace("{time}", "06:00"),
                    t("reallife_ui_hud_hospital_energy", "Energy restored"),
                },
                ChipTones = new[] { UiRole.Danger, UiRole.Warn, UiRole.Ok }, ChipCount = 3,
                Action = t("menu_continue", "Continue"),
            },
        };
    }

    /// <summary>The HUD with its notice cards at the top centre.</summary>
    public static void PiecesNotesScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool night)
    {
        UiHudNoticeModel[] cards = PiecesNotes(t, night);
        Hud(s, f, dark, k, t, new UiHudModel { Notices = cards, NoticeCount = cards.Length }, PiecesBar(night));
    }

    // ------------------------------------------------------------------------------------------------- the bar's popups

    /// <summary>
    /// The top bar's two lists over the bar: the game's dances, the four known pressable, the one danced chosen, the rest to
    /// learn at the dance class, with Stop dancing; and the accessories the player wears, the hand's and the head's with
    /// their switches, how many show, with Take all off. Indoors, the accessories alone, with nothing worn.
    /// </summary>
    public static UiHudPopupModel[] PiecesPopups(UiWords t, UiStatusModel bar, float frameWidth, bool indoors)
    {
        UiPillPlan plan = UiDraw.PlanPills(bar, frameWidth);
        var accessories = new UiHudPopupModel
        {
            Title = t("reallife_ui_hud_accessories", "Accessories"), Empty = t("reallife_ui_hud_accessories_empty", "You are not wearing any accessories."),
            AnchorX = UiDraw.ToolMiddle(plan, 1),
        };
        if (indoors)
        {
            return new[] { accessories };
        }
        accessories.Rows = new[]
        {
            new UiHudPopupRow { Icon = UiIcon.Briefcase, Text = "Coffee cup", Sub = t("reallife_ui_hud_slot_hand", "Hand"), HasSwitch = true, On = true },
            new UiHudPopupRow { Icon = UiIcon.Cap, Text = "Grey flat cap", Sub = t("reallife_ui_hud_slot_head", "Head"), HasSwitch = true, On = false },
            new UiHudPopupRow { Icon = UiIcon.Headset, Text = "Earbuds", Sub = t("reallife_ui_hud_slot_phone", "Phone") },
        };
        accessories.RowCount = 3;
        accessories.Meta = t("reallife_ui_hud_accessories_shown", "{shown} of {count} shown").Replace("{shown}", "2").Replace("{count}", "3");
        accessories.Action = t("reallife_ui_hud_accessories_off", "Take all off");
        string dancing = t("reallife_ui_hud_dancing", "Dancing");
        string[] names = { "Boogie", "Sway", "Swing", "The Macarena", "Shy Dance", "Fist Pump", "Swim", "Twist", "Smooth Operator" };
        string known = t("reallife_ui_hud_dances_known", "{known} of {count} known").Replace("{known}", "4").Replace("{count}", "9");
        var dances = new UiHudPopupModel
        {
            Title = t("reallife_ui_hud_dances", "Dances"), Meta = known, Numbered = true, Rows = new UiHudPopupRow[names.Length], RowCount = names.Length,
            Empty = t("reallife_ui_hud_dances_empty", "No dances to choose from here."), Action = t("reallife_ui_hud_dance_stop", "Stop dancing"), AnchorX = UiDraw.ToolMiddle(plan, 0),
        };
        for (int i = 0; i < names.Length; i++)
        {
            bool locked = i >= 4;   // past the first four: learned at the school's dance class
            dances.Rows[i] = new UiHudPopupRow
            {
                Text = names[i], Press = !locked, Disabled = locked, Chip = i == 2 ? dancing : locked ? t("reallife_study_course_dance_class", "Dance class") : null,
                ChipTone = i == 2 ? UiRole.Accent : UiRole.None,
            };
        }
        return new[] { dances, accessories };
    }

    /// <summary>The HUD with the bar's popups open over their buttons: the dances and the accessories, or the accessories alone indoors.</summary>
    public static void PiecesPopupsScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool indoors)
    {
        UiStatusModel bar = indoors ? PiecesBar(false, 1) : PiecesBar(false, 0, 1);
        UiHudPopupModel[] popups = PiecesPopups(t, bar, f.Width, indoors);
        Hud(s, f, dark, k, t, new UiHudModel { Popups = popups, PopupCount = popups.Length }, bar);
    }

    // ------------------------------------------------------------------------------------------------- the key chips

    /// <summary>
    /// The game's control hints while driving, as key chips over the bar: each hint's keys as the game names them, and the
    /// game's menu key last as the way back.
    /// </summary>
    public static void PiecesHintsScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t)
    {
        var m = new UiHudModel
        {
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Pick1, Label = t("input_key_throttle", "Throttle") }, new UiPrompt { Action = UiAction.Pick2, Label = t("input_key_brakes", "Brakes") },
                new UiPrompt { Action = UiAction.Pick3, Label = t("input_key_handbrake", "Handbrake") }, new UiPrompt { Action = UiAction.Back, Label = t("input_key_menu", "Menu") },
            },
            PromptCount = 4,
        };
        Hud(s, f, dark, new GameHintKeys(k, "Esc", "W", "S", "Space"), t, m, PiecesBar(true));
    }

    /// <summary>
    /// The kit's keys with the game's own key names on the number keys the HUD's chips carry and on the way back (as
    /// Ui/HudScreens/HudHints names them).
    /// </summary>
    private sealed class GameHintKeys : IUiKeys
    {
        private readonly IUiKeys _inner;
        private readonly string _back;
        private readonly string[] _names;

        public GameHintKeys(IUiKeys inner, string back, params string[] names)
        {
            _inner = inner;
            _back = back;
            _names = names;
        }

        public UiPad Pad => _inner != null ? _inner.Pad : UiPad.None;

        public string Name(UiAction action)
        {
            int at = (int)action - (int)UiAction.Pick1;
            if (action == UiAction.Back)
            {
                return _back;
            }
            return at >= 0 && at < _names.Length ? _names[at] : _inner != null ? _inner.Name(action) : null;
        }

        public UiIcon Glyph(UiAction action)
        {
            int at = (int)action - (int)UiAction.Pick1;
            return action == UiAction.Back || (at >= 0 && at < _names.Length) ? UiIcon.None : _inner != null ? _inner.Glyph(action) : UiIcon.None;
        }
    }
}
