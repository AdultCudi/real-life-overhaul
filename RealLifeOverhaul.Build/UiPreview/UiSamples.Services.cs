using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Services app's samples: the model each page writes, with real-looking names, long names, empty, full and refused states. Every word goes through the
// screen's text source so the checks measure it in all eleven languages. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] ServicesStates =
    {
        "cleaning", "cleaning_client", "cleaning_add", "cleaning_empty", "crew", "crew_person", "contracts", "contracts_offer", "contracts_running", "sell", "sell_listed", "sell_picker",
        "sell_rivals", "sell_refused", "sell_empty", "sell_short", "off"
    };

    private static readonly string[] SvTabNames = { "cleaning", "crew", "contracts", "sell" };

    /// <summary>The tab a state stands on.</summary>
    public static int ServicesTabOf(string state)
    {
        for (int i = 0; i < SvTabNames.Length; i++)
        {
            if (state == SvTabNames[i] || state.StartsWith(SvTabNames[i] + "_", StringComparison.Ordinal))
            {
                return i;
            }
        }
        return 0;
    }

    private static UiPrompt[] SvPrompts(UiWords t) => new[]
    {
        new UiPrompt { Action = UiAction.Move, Label = W(t, "reallife_ui_prompt_move", "Move") },
        new UiPrompt { Action = UiAction.Confirm, Label = W(t, "reallife_ui_prompt_select", "Select") },
        new UiPrompt { Action = UiAction.Back, Label = W(t, "reallife_ui_prompt_back", "Back") }
    };

    private static UiNavEntry[] SvNav(UiWords t, string[] counts)
    {
        UiIcon[] icons = { UiIcon.Spark, UiIcon.People, UiIcon.ClipboardText, UiIcon.Tag };
        string[] labels = { W(t, "reallife_svc_tab_cleaning", "Cleaning"), W(t, "reallife_svc_tab_crew", "Crew"), W(t, "reallife_svc_tab_contracts", "Contracts"), W(t, "reallife_svc_tab_sale", "Sell") };
        var nav = new UiNavEntry[4];
        for (int i = 0; i < nav.Length; i++)
        {
            nav[i] = new UiNavEntry { Icon = icons[i], Label = labels[i], Count = counts != null && i < counts.Length ? counts[i] : null };
        }
        return nav;
    }

    private static UiActionModel SvBtn(List<Func<bool>> runs, string label, UiButtonKind kind, UiAction key, bool disabled = false)
    {
        runs.Add(Yes);
        return new UiActionModel { Label = label, Kind = kind, Icon = UiIcon.None, Key = key, Command = ServicesFace.ActionCommand, Arg = disabled ? -1 : runs.Count - 1, Disabled = disabled };
    }

    private static UiActionModel SvBack(UiWords t) => new UiActionModel
    {
        Label = W(t, "reallife_co_back", "Back"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Command = ServicesFace.BackCommand, Arg = 0
    };

    private static UiFactLine SvStep(List<Func<bool>> runs, string label, string value, string hint, bool less = true, bool more = true)
    {
        runs.Add(Yes);
        runs.Add(Yes);
        return UiFactLine.Amount(label, value, hint, less, more, ServicesFace.StepLess, ServicesFace.StepMore, runs.Count - 2);
    }

    private static void SvChips(ServicesModel m, UiWords t, string[] labels, int[] counts, int at)
    {
        m.Chips = new UiActionModel[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            m.Chips[i] = ServicesRuns.Chip(Fx(W(t, "reallife_ui_svc_chip", "{name} {count}"), "name", labels[i], "count", counts[i].ToString()), ServicesFace.ChipCommand, i);
        }
        m.ChipAt = at;
    }

    private static UiDataRow SvRow(string[] cells, int[] values, UiRole[] tones, string[] subs, string name = null, UiRole ring = UiRole.None) => new UiDataRow
    {
        Cells = cells, Values = values, Tones = tones, Subs = subs, Hue = name != null ? UiColourHue(name) : 0f, Ring = ring
    };

    private static UiRole[] SvTones(int n, int at, UiRole tone)
    {
        var tones = new UiRole[n];
        tones[at] = tone;
        return tones;
    }

    private static string SvMoney(int amount) => Usd(amount);

    /// <summary>The model a state of the Services app writes.</summary>
    public static ServicesModel Services(UiWords t, string state)
    {
        var runs = new List<Func<bool>>();
        int tab = ServicesTabOf(state);
        var m = new ServicesModel
        {
            Title = W(t, "reallife_svc_app", "Services"), Icon = UiIcon.Handshake, Tab = tab, Prompts = SvPrompts(t),
            Nav = SvNav(t, new[] { "6", "4", "5", "4" })
        };
        if (state == "off")
        {
            m.Shape = ServicesShape.Off;
            m.OffTitle = W(t, "reallife_svc_tab_cleaning", "Cleaning");
            m.OffBody = W(t, "reallife_co_part_off", "Switched off in the options, or stopped after an error. Nothing of it runs and nothing is charged.");
            m.OffAction = W(t, "reallife_ui_svc_close", "Close");
            m.Runs = runs.ToArray();
            return m;
        }
        switch (tab)
        {
            case 0: SvCleaning(m, t, state, runs); break;
            case 1: SvCrew(m, t, state, runs); break;
            case 2: SvContracts(m, t, state, runs); break;
            default: SvSell(m, t, state, runs); break;
        }
        m.Runs = runs.ToArray();
        return m;
    }

    private static UiDataTableModel SvTable(UiDataColumn[] columns, List<UiDataRow> rows, int selected, string emptyTitle, string emptyBody, UiIcon icon) => new UiDataTableModel
    {
        Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, Selected = selected, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon
    };

    // ------------------------------------------------------------------------------------------------- Cleaning

    private static void SvCleaning(ServicesModel m, UiWords t, string state, List<Func<bool>> runs)
    {
        m.Shape = ServicesShape.Table;
        string[] names = { "Hudson Data Hall", "Pioneer Game Studio", "Bloom and Co", "Harbourside Freight Terminal and Depot", "Murray Hill Dental", "Garment District Weavers" };
        string[] where = { "Hell's Kitchen", "Midtown", "Garment District", "Hudson Yards", "Murray Hill", "Garment District" };
        int[] clean = { 92, 78, 64, 88, 96, 71 };
        var rows = new List<UiDataRow>();
        if (state != "cleaning_empty")
        {
            for (int i = 0; i < names.Length; i++)
            {
                bool contract = i == 3 || i == 5;
                bool nobody = i == 2;
                string next = nobody ? W(t, "reallife_svc_why_nobody", "nobody is free") : i % 3 == 0 ? "22:00, Rosa Kowalski" : i == 4 ? W(t, "reallife_svc_row_waiting", "when needed") : "18:00, Dan Mbeki";
                rows.Add(SvRow(new[] { names[i], i == 4 ? "" : (30 + i * 12) + " min", (i * 9) + " min", clean[i].ToString(), next,
                        W(t, nobody ? "reallife_ui_svc_state_nobody" : contract ? "reallife_svc_row_contract" : "reallife_svc_row_on", nobody ? "Not served" : contract ? "under contract" : "on the round") },
                    new[] { (int)(contract ? UiIcon.Building : UiIcon.Store), 0, 0, clean[i], 0, 0 }, SvTones(6, 5, nobody ? UiRole.Warn : contract ? UiRole.Notice : UiRole.Ok),
                    new[] { where[i], null, null, null, null, null }));
            }
            rows.Add(SvRow(new[] { "Corner Kitchen", "", "", "83", "", W(t, "reallife_ui_svc_state_not_on", "Not on the round") }, new[] { (int)UiIcon.Store, 0, 0, 83, 0, 0 },
                SvTones(6, 5, UiRole.Muted), new[] { "Midtown", null, null, null, null, null }));
        }
        var columns = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_co_col_business", "Business"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_needs", "Needs a day"), Kind = UiCellKind.Figure, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_got", "Got today"), Kind = UiCellKind.Figure, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_cleanliness", "Cleanliness"), Kind = UiCellKind.Meter, Width = 152f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_next", "Next visit"), Kind = UiCellKind.Text, Width = 232f },
            new UiDataColumn { Title = W(t, "reallife_co_col_state", "State"), Kind = UiCellKind.Chip, Width = 152f }
        };
        string[] keys = new string[rows.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = i == rows.Count - 1 ? "add:x" : "cli:" + i;
        }
        m.RowKeys = keys;
        int selected = state == "cleaning_client" ? 0 : state == "cleaning_add" ? rows.Count - 1 : -1;
        m.Table = SvTable(columns, rows, selected, W(t, "reallife_ui_svc_no_round", "Nothing on the round"), W(t, "reallife_svc_empty_cleaning", "Nothing of the kind chosen."), UiIcon.Spark);
        SvChips(m, t, new[] { W(t, "reallife_ui_svc_chip_all", "All"), W(t, "reallife_ui_svc_chip_own", "Your shops"), W(t, "reallife_ui_svc_chip_contracts", "Contracts"),
            W(t, "reallife_ui_svc_chip_not_on", "Not on the round") }, rows.Count == 0 ? new[] { 0, 0, 0, 0 } : new[] { 7, 4, 2, 1 }, 0);
        if (state == "cleaning_client")
        {
            var lines = new List<UiFactLine>
            {
                UiFactLine.Row(W(t, "reallife_svc_client_clean", "Cleanliness:"), "92, up 4 since the morning"),
                UiFactLine.Say(Fx(W(t, "reallife_svc_client_needs", "Needs about {need} a day. Got {today} today and {before} the day before."), "need", "36 minutes", "today", "0 minutes", "before", "38 minutes")),
                UiFactLine.Say(Fx(W(t, "reallife_svc_client_next", "Next: {name}, {from} to {to}."), "name", "Rosa Kowalski", "from", "22:00", "to", "22:45"), UiRole.OkText),
                UiFactLine.Section(W(t, "reallife_ibox_sec_how_often", "How often")),
                SvStep(runs, W(t, "reallife_ui_svc_row_how_often", "Visits"), W(t, "reallife_ui_svc_visits_n", "{count} a day").Replace("{count}", "2"), null),
                SvStep(runs, Fx(W(t, "reallife_ui_svc_row_visit_at", "Visit {number} at"), "number", "1"), "09:00", null),
                SvStep(runs, Fx(W(t, "reallife_ui_svc_row_visit_at", "Visit {number} at"), "number", "2"), "14:00", null)
            };
            m.Side = new UiFactCardModel
            {
                Title = names[0], Sub = where[0], Chip = W(t, "reallife_svc_row_on", "on the round"), Lines = lines.ToArray(),
                Actions = new[] { SvBack(t), SvBtn(runs, W(t, "reallife_svc_client_remove", "Take off the round"), UiButtonKind.Secondary, UiAction.Count) }
            };
        }
        else if (state == "cleaning_add")
        {
            m.Side = new UiFactCardModel
            {
                Title = "Corner Kitchen", Sub = "Midtown",
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_svc_client_clean", "Cleanliness:"), "83"),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_add_own_hours", "Its own rota has {hours} cleaning hours a week. On the round it needs none of them."), "hours", "14")),
                    UiFactLine.Say(W(t, "reallife_svc_add_says", "On the round, a cleaner of the crew comes when its floor needs it, or at hours you set."), UiRole.Muted)
                },
                Actions = new[] { SvBack(t), SvBtn(runs, W(t, "reallife_svc_add_do", "Add to the round"), UiButtonKind.Primary, UiAction.Alt) }
            };
        }
        else
        {
            m.Side = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_service", "The service"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Say(Fx(W(t, "reallife_svc_service_who", "Crew: {crew} cleaners. Round: {businesses} businesses, {contracts} under contract."), "crew", "6", "businesses", "6", "contracts", "2")),
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_service_week", "The service a week"), SvMoney(4310)),
                    UiFactLine.Say(Fx(W(t, "reallife_ui_svc_week_split", "Wages {wages}, van {van}"), "wages", SvMoney(3720), "van", SvMoney(590)), UiRole.Muted),
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_own_week", "One cleaner at each shop a week"), SvMoney(5200)),
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_fees_week", "Contract fees a week"), SvMoney(9400)),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_service_round_left", "Visits still to come today: {count}. Businesses left over: {left}."), "count", "5", "left", "1")),
                    UiFactLine.Toggle(W(t, "reallife_ui_svc_van", "A van for the crew"), true, ServicesFace.ActionCommand, SvRun(runs)),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_van_says_on", "By van a way takes less than half the time, at {cost} a kilometre, charged each morning."), "cost", "$4"), UiRole.Muted)
                }
            };
        }
    }

    private static int SvRun(List<Func<bool>> runs)
    {
        runs.Add(Yes);
        return runs.Count - 1;
    }

    // ------------------------------------------------------------------------------------------------- Crew

    private static void SvCrew(ServicesModel m, UiWords t, string state, List<Func<bool>> runs)
    {
        m.Shape = ServicesShape.Table;
        string[] names = { "Rosa Kowalski", "Dan Mbeki", "Yuki Chen", "Ibrahim Aziz", "Nora Olsen", "Carlos Vega-Hernandez" };
        int[] skill = { 88, 72, 64, 81, 58, 47 }, mood = { 82, 66, 74, 90, 45, 30 };
        var rows = new List<UiDataRow>();
        for (int i = 0; i < names.Length; i++)
        {
            bool crew = i != 5;
            rows.Add(SvRow(new[] { names[i], skill[i].ToString(), mood[i].ToString(), "$18.00", "32", i == 4 ? W(t, "reallife_svc_crew_sick", "off sick") : "18:00 to 22:00",
                    W(t, crew ? "reallife_svc_crew_on" : "reallife_svc_crew_off", crew ? "on the crew" : "not on the crew") },
                new[] { 0, skill[i], mood[i], 0, 0, 0, 0 }, SvTones(7, 6, crew ? UiRole.Ok : UiRole.Muted),
                new[] { i == 5 ? W(t, "reallife_svc_crew_no_base", "no building") : "Hudson Data Hall", null, null, null, null, null, null }, names[i],
                mood[i] >= 70 ? UiRole.Ok : mood[i] >= 40 ? UiRole.Warn : UiRole.Danger));
        }
        var columns = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_co_col_name", "Name"), Kind = UiCellKind.Person, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_skill", "Cleaning"), Kind = UiCellKind.Meter, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_mood", "Satisfaction"), Kind = UiCellKind.Meter, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_wage", "An hour"), Kind = UiCellKind.Figure, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_hours", "Hours a week"), Kind = UiCellKind.Figure, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_today", "Today"), Kind = UiCellKind.Text, Width = 168f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_crew", "Crew"), Kind = UiCellKind.Chip, Width = 152f }
        };
        var keys = new string[rows.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = "emp:" + i;
        }
        m.RowKeys = keys;
        m.Table = SvTable(columns, rows, state == "crew_person" ? 1 : -1, W(t, "reallife_ui_svc_no_cleaners", "No cleaners"), W(t, "reallife_svc_row_no_cleaner", "You employ no cleaner."), UiIcon.People);
        SvChips(m, t, new[] { W(t, "reallife_ui_svc_chip_all", "All"), W(t, "reallife_svc_kind_crew_on", "On the crew"), W(t, "reallife_svc_kind_crew_off", "Not on the crew") }, new[] { 6, 5, 1 }, 0);
        if (state == "crew_person")
        {
            m.Side = new UiFactCardModel
            {
                Title = names[1], Sub = "Hudson Data Hall", Chip = W(t, "reallife_svc_crew_on", "on the crew"),
                Lines = new[]
                {
                    UiFactLine.Bar(W(t, "reallife_ibox_skill", "Skill"), skill[1]), UiFactLine.Bar(W(t, "reallife_ui_svc_row_mood", "Satisfaction"), mood[1]),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_crew_skill", "Cleaning {skill}, satisfaction {mood}%. Takes {rate} points an hour off a small shop's floor."), "skill", "72", "mood", "66", "rate", "31")),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_crew_wage", "Paid {wage} an hour for {hours} hours a week."), "wage", "$18.00", "hours", "32")),
                    UiFactLine.Row(W(t, "reallife_svc_crew_base", "Assigned to:"), "Hudson Data Hall"),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_crew_visit", "{from} to {to}: {business} ({way} to get there)"), "from", "18:00", "to", "18:45", "business", "Pioneer Game Studio", "way", "850 m"))
                },
                Actions = new[] { SvBack(t), SvBtn(runs, W(t, "reallife_svc_crew_take_off", "Take off the crew"), UiButtonKind.Secondary, UiAction.Alt) }
            };
        }
        else
        {
            m.Side = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_crew", "The crew"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Say(W(t, "reallife_svc_crew_says_hire", "Hire cleaners through the game's recruitment and assign each to a building of yours.")),
                    UiFactLine.Say(W(t, "reallife_svc_crew_says_hours", "Their cleaning shifts in BizMan's schedule are their working hours on the round.")),
                    UiFactLine.Say(W(t, "reallife_svc_crew_says_pay", "The game pays them for those hours as always. While a cleaner is out, their own building goes without their work.")),
                    UiFactLine.Say(W(t, "reallife_svc_crew_says_pick", "Pick a cleaner to put them on the crew."), UiRole.Muted)
                }
            };
        }
    }

    // ------------------------------------------------------------------------------------------------- Contracts

    private static void SvContracts(ServicesModel m, UiWords t, string state, List<Func<bool>> runs)
    {
        m.Shape = ServicesShape.Table;
        string[] names = { "Uptown Savings Branch", "Broadway Box Office", "Harbourside Freight Terminal and Depot", "Garment District Weavers", "Pioneer Game Studio" };
        string[] where = { "Uptown", "Midtown", "Hudson Yards", "Garment District", "Midtown" };
        var rows = new List<UiDataRow>();
        for (int i = 0; i < names.Length; i++)
        {
            bool offer = i < 2, late = i == 3;
            rows.Add(SvRow(new[] { names[i], SvMoney(1200 + i * 330), (70 + i * 5).ToString(), offer ? "" : (84 - i * 6).ToString(), "day " + (412 + i),
                    W(t, offer ? "reallife_ui_svc_state_offer" : late ? "reallife_ui_svc_state_late" : "reallife_ui_svc_state_active", offer ? "Offer" : late ? "Late" : "Active") },
                new[] { (int)UiIcon.Building, 0, 0, offer ? -1 : 84 - i * 6, 0, 0 }, SvTones(6, 5, offer ? UiRole.Warn : late ? UiRole.Danger : UiRole.Ok),
                new[] { where[i], null, null, null, null, null }));
        }
        var columns = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_svc_col_client", "Client"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_svc_col_fee_week", "A week"), Kind = UiCellKind.Money, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_standard", "Asks"), Kind = UiCellKind.Figure, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_svc_col_floor", "Floor now"), Kind = UiCellKind.Meter, Width = 128f },
            new UiDataColumn { Title = W(t, "reallife_ui_svc_col_week_ends", "Week ends"), Kind = UiCellKind.Date, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_co_col_state", "State"), Kind = UiCellKind.Chip, Width = 128f }
        };
        var keys = new string[rows.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = (i < 2 ? "offer:" : "con:") + i;
        }
        m.RowKeys = keys;
        m.Table = SvTable(columns, rows, state == "contracts_offer" ? 0 : state == "contracts_running" ? 3 : -1, W(t, "reallife_ui_svc_no_contracts", "No contracts"),
            W(t, "reallife_svc_row_no_offer", "No business asks for a contract at the moment."), UiIcon.ClipboardText);
        SvChips(m, t, new[] { W(t, "reallife_ui_svc_chip_all", "All"), W(t, "reallife_svc_kind_offer", "Offers"), W(t, "reallife_ui_svc_chip_running", "Running") }, new[] { 5, 2, 3 }, 0);
        if (state == "contracts_offer")
        {
            m.Side = new UiFactCardModel
            {
                Title = names[0], Sub = where[0], Chip = W(t, "reallife_ui_svc_state_offer", "Offer"), ChipTone = UiRole.Warn,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_svc_contract_fee", "Fee:"), Fx(W(t, "reallife_svc_row_a_week", "{amount} a week"), "amount", SvMoney(1200))),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_contract_standard", "Wants its floor at {standard} or better. Mornings a week allowed under it: {days}"), "standard", "70", "days", "2")),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_contract_work", "{size} m2. About {minutes} of your crew's cleaning a day."), "size", "420", "minutes", "55 minutes")),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_contract_penalty", "A missed week pays no fee and costs {share}% of it. Missed weeks running that end it: {weeks}"), "share", "25", "weeks", "2"), UiRole.Muted),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_contract_until", "The offer stands until day {day}."), "day", "412"), UiRole.WarnText)
                },
                Actions = new[] { SvBack(t), SvBtn(runs, W(t, "reallife_svc_contract_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count),
                    SvBtn(runs, W(t, "reallife_svc_contract_take", "Take the contract"), UiButtonKind.Primary, UiAction.Alt) }
            };
        }
        else if (state == "contracts_running")
        {
            m.Side = new UiFactCardModel
            {
                Title = names[3], Sub = where[3], Chip = W(t, "reallife_ui_svc_state_late", "Late"), ChipTone = UiRole.Danger,
                Lines = new[]
                {
                    UiFactLine.Tick(Fx(W(t, "reallife_svc_contract_missed", "Weeks missed running: {count}. At {most} the contract ends."), "count", "1", "most", "2"), false),
                    UiFactLine.Row(W(t, "reallife_svc_contract_fee", "Fee:"), Fx(W(t, "reallife_svc_row_a_week", "{amount} a week"), "amount", SvMoney(2190))),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_contract_week", "Mornings under the standard this week: {under} of {most} allowed. Week ends on day {day}"), "under", "3", "most", "2", "day", "415")),
                    UiFactLine.Row(W(t, "reallife_svc_contract_earned", "Fees paid so far:"), SvMoney(8760)),
                    UiFactLine.Go(W(t, "reallife_svc_contract_visits", "Its visits"), ServicesFace.ActionCommand, SvRun(runs))
                },
                Actions = new[] { SvBack(t), SvBtn(runs, W(t, "reallife_svc_contract_end", "End the contract"), UiButtonKind.Secondary, UiAction.Count) }
            };
        }
        else
        {
            m.Side = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_contracts", "Contracts"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_svc_contracts_running", "Contracts running:"), "3"),
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_fees_week", "Contract fees a week"), SvMoney(9400)),
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_booked_week", "Booked in the last 7 days"), SvMoney(8200)),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_contracts_says", "A business asks every {days} days at most, up to {most} offers at once."), "days", "3", "most", "3"))
                }
            };
        }
    }

    // ------------------------------------------------------------------------------------------------- Sell

    private static UiDataTableModel SvOffers(UiWords t, string[][] cells, int[] standing, int selected, bool rivals)
    {
        var rows = new List<UiDataRow>();
        foreach (string[] c in cells)
        {
            rows.Add(rivals
                ? SvRow(new[] { c[0], c[1], c[2], c[3], c[4] }, new[] { (int)UiIcon.Store, 0, 0, 0, 0 }, new UiRole[5], new[] { c[5], c[6], null, null, null }, c[1])
                : SvRow(new[] { c[0], c[1], c[2], c[3], c[4] }, new[] { 0, 0, standing[rows.Count], 0, 0 }, new UiRole[5], new[] { c[5], null, null, null, null }, c[0]));
        }
        UiDataColumn[] columns = rivals
            ? new[]
            {
                new UiDataColumn { Title = W(t, "reallife_co_col_business", "Business"), Kind = UiCellKind.Tile, Width = 0f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_seller", "Seller"), Kind = UiCellKind.Person, Width = 248f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_asking", "Asking"), Kind = UiCellKind.Money, Width = 128f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_takings_week", "Takings a week"), Kind = UiCellKind.Money, Width = 152f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_expires", "Stands until"), Kind = UiCellKind.Date, Width = 104f }
            }
            : new[]
            {
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_buyer", "Buyer"), Kind = UiCellKind.Person, Width = 0f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_offer", "Offer"), Kind = UiCellKind.Money, Width = 128f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_against", "Against worth"), Kind = UiCellKind.Meter, Width = 192f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_terms", "Terms"), Kind = UiCellKind.Text, Width = 232f },
                new UiDataColumn { Title = W(t, "reallife_ui_svc_col_expires", "Stands until"), Kind = UiCellKind.Date, Width = 104f }
            };
        return SvTable(columns, rows, selected, W(t, "reallife_ui_svc_no_offers_listed", "No offers yet"), W(t, "reallife_ui_svc_no_offers_listed_body", "Offers come over the next days while it is on the market."), UiIcon.Tag);
    }

    private static void SvSell(ServicesModel m, UiWords t, string state, List<Func<bool>> runs)
    {
        m.Shape = ServicesShape.Sell;
        bool empty = state == "sell_empty", rivals = state == "sell_rivals", listed = state == "sell_listed" || state == "sell_short", refused = state == "sell_refused";
        var p = new ServicesSell { PickerTitle = W(t, "reallife_ui_svc_sell_pick_title", "Business to sell"), PickerOpen = state == "sell_picker" };
        m.Sell = p;
        p.Businesses = empty ? new string[0] : new[] { "Bloom and Co, Garment District", "Hudson Data Hall, Hell's Kitchen", "Harbourside Freight Terminal and Depot, Hudson Yards" };
        p.BusinessKeys = empty ? new string[0] : new[] { "a", "b", "c" };
        p.Chosen = empty ? -1 : 0;
        p.WorthLabel = W(t, "reallife_ui_svc_worth_today", "Worth today");
        p.Views = new[]
        {
            ServicesRuns.Chip(Fx(W(t, "reallife_ui_svc_view_offers", "Offers {count}"), "count", empty ? "0" : "4"), ServicesFace.ViewCommand, 0),
            ServicesRuns.Chip(Fx(W(t, "reallife_ui_svc_view_rivals", "Rivals selling {count}"), "count", "2"), ServicesFace.ViewCommand, 1)
        };
        p.View = rivals ? 1 : 0;
        if (empty)
        {
            p.Worth = W(t, "reallife_ui_svc_no_business", "No business to sell");
            p.WorthSub = W(t, "reallife_ui_svc_no_business_sub", "Open a business and it can be sold here.");
            p.Valued = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_valued", "How it is valued"), Heading = true,
                Lines = new[] { UiFactLine.Say(Fx(W(t, "reallife_svc_sale_says_value", "A business is worth {days} days of its recent profit."), "days", "14")) }
            };
        }
        else
        {
            p.Worth = SvMoney(1284000);
            p.WorthSub = Fx(W(t, "reallife_ui_svc_worth_sub", "{days} days of its books, by its kind, district and the economy"), "days", "14");
            var valued = new List<UiFactLine>
            {
                UiFactLine.Row(W(t, "reallife_svc_value_base", "Recent profit:"), Fx(W(t, "reallife_svc_value_base_value", "{amount} for {days} days"), "amount", SvMoney(1070000), "days", "14")),
                UiFactLine.Row(W(t, "reallife_ui_svc_goodwill", "Goodwill"), SvMoney(1096000)),
                UiFactLine.Say(Fx(W(t, "reallife_ui_svc_goodwill_by", "By its kind {type}, district {district}, the economy {economy}, days of books {books}."), "type", "100%", "district", "104%", "economy", "112%", "books", "100%"), UiRole.Muted),
                UiFactLine.Row(W(t, "reallife_svc_value_fixtures", "Stock and furniture:"), SvMoney(314800)),
                UiFactLine.Row(W(t, "reallife_svc_value_worth", "Worth:"), SvMoney(1284000)),
                UiFactLine.Row(W(t, "reallife_ui_svc_loans_to_repay", "Loans to repay"), "-" + SvMoney(126400)),
                UiFactLine.Row(W(t, "reallife_ui_svc_left_at_value", "Left at that worth"), SvMoney(1157600)),
                UiFactLine.Row(W(t, "reallife_ui_svc_deposit_back", "Deposit back from the landlord"), SvMoney(8400))
            };
            p.Valued = new UiFactCardModel { Title = W(t, "reallife_ui_svc_sec_valued", "How it is valued"), Heading = true, Lines = valued.ToArray() };
            p.Raise = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_raise", "What would raise it before you sell"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Tick(Fx(W(t, "reallife_svc_raise_interior", "Interior score {now}%, the district wants {want}%"), "now", "74", "want", "70") + ". " + W(t, "reallife_svc_raise_met", "Good already"), true),
                    UiFactLine.Tick(Fx(W(t, "reallife_svc_raise_rating", "Customer satisfaction {now}%"), "now", "62") + ". " + Fx(W(t, "reallife_svc_raise_to", "At {to}%, about {amount} more"), "to", "72", "amount", SvMoney(48000)), false),
                    UiFactLine.Tick(Fx(W(t, "reallife_svc_raise_books", "Books for {now} of {want} days"), "now", "9", "want", "14") + ". " + Fx(W(t, "reallife_svc_raise_days", "In {count} days, about {amount} more"), "count", "5", "amount", SvMoney(9000)), false)
                }
            };
            p.After = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_after", "After earlier sales"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Say(Fx(W(t, "reallife_svc_row_due", "{business}: parts still to come: {count}, each {amount}, the next on day {day}"), "business", "Corner Kitchen", "count", "3", "amount", SvMoney(21000), "day", "418"))
                }
            };
        }
        if (rivals)
        {
            p.Offers = SvOffers(t, new[]
            {
                new[] { "Burger Barn on Fifth", "Huang Guo", SvMoney(412000), SvMoney(9800), "day 416", W(t, "reallife_ui_svc_then_taken", "{buyer} takes it if you do not buy").Replace("{buyer}", "Rival Foods"), W(t, "reallife_ui_svc_rival_owner", "Rival") },
                new[] { "Greenfield Grocers", "Marta Reyes-Lindqvist", SvMoney(238000), SvMoney(5100), "day 414", W(t, "reallife_ui_svc_then_kept", "The seller keeps it if you do not buy"), W(t, "reallife_ui_svc_rival_owner", "Rival") }
            }, null, 0, true);
            p.OfferKeys = new[] { "rsale:1", "rsale:2" };
            p.OffersNote = W(t, "reallife_ui_svc_rival_note", "A rival's sale: buy it before another rival does");
            p.Accept = new UiFactCardModel
            {
                Title = Fx(W(t, "reallife_svc_rsale_title", "{business} for sale"), "business", "Burger Barn on Fifth"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_svc_bid_price", "Price:"), SvMoney(412000)), UiFactLine.Row(W(t, "reallife_ui_svc_col_takings_week", "Takings a week"), SvMoney(9800)),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_rsale_then", "You may buy it until day {day}. After that {buyer} takes it over."), "day", "416", "buyer", "Rival Foods"), UiRole.WarnText),
                    UiFactLine.Say(W(t, "reallife_svc_rsale_how", "You buy it as a takeover in BizMan, with the same diplomas. It becomes your business there."))
                },
                Actions = new[] { SvBtn(runs, W(t, "reallife_svc_rsale_buy", "Buy this business"), UiButtonKind.Primary, UiAction.Alt) }
            };
            p.OffersTitle = W(t, "reallife_ui_svc_view_rivals_title", "Rivals' businesses for sale");
        }
        else
        {
            string[][] cells = empty ? new string[0][] : new[]
            {
                new[] { "Huang Guo", SvMoney(1342000), "+4.5%", W(t, "reallife_ui_svc_term_cash", "Cash"), "day 415", W(t, "reallife_svc_who_rival", "A rival with {count} businesses.").Replace("{count}", "12") },
                new[] { "Murray Hill Holdings", SvMoney(1296000), "+0.9%", Fx(W(t, "reallife_ui_svc_term_keeps", "{pay}, keeps staff"), "pay", W(t, "reallife_ui_svc_term_cash", "Cash")), "day 417", W(t, "reallife_svc_who_investor", "An investor.") },
                new[] { "Anya Petrova and the staff", SvMoney(1180000), "-8.1%", Fx(W(t, "reallife_ui_svc_term_weeks", "Paid over {count} weeks"), "count", "3"), "day 418", W(t, "reallife_svc_who_staff_note", "Your staff, {count} of them, led by the most skilled.").Replace("{count}", "9") },
                new[] { "Harbourside Freight HSF", SvMoney(1120000), "-12.8%", W(t, "reallife_ui_svc_term_cash", "Cash"), "day 413", Fx(W(t, "reallife_svc_who_chain", "A listed company, {ticker} on the market."), "ticker", "HSF") }
            };
            bool noOffers = empty || state == "sell_listed";
            p.Offers = noOffers ? SvOffers(t, new string[0][], null, -1, false) : SvOffers(t, cells, new[] { 76, 66, 24, 20 }, 0, false);
            p.OfferKeys = noOffers ? new string[0] : new[] { "bid:1", "bid:2", "bid:3", "bid:4" };
            p.OffersNote = noOffers ? null : Fx(W(t, "reallife_ui_svc_offers_note", "Offers against what it is worth, {count} standing"), "count", "4");
            if (noOffers)
            {
                p.Accept = new UiFactCardModel
                {
                    Title = W(t, "reallife_ui_svc_sec_accept_none", "If you accept an offer"), Heading = true,
                    Lines = new[] { UiFactLine.Say(W(t, empty ? "reallife_svc_sale_says_after" : "reallife_ui_svc_accept_wait", "No offer is picked.")) }
                };
            }
            else
            {
                bool shortCash = state == "sell_short";
                var lines = new List<UiFactLine>
                {
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_offer", "Offer"), SvMoney(1342000)),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_bid_until", "The offer stands until day {day}. You may ask for {share}% more, once."), "day", "415", "share", "10"), UiRole.Muted),
                    UiFactLine.Row(W(t, "reallife_ui_svc_loans_repaid", "Loans repaid"), "-" + SvMoney(126400)),
                    UiFactLine.Row(W(t, "reallife_ui_svc_tax_gain", "Tax on the gain, about"), "-" + SvMoney(87300)),
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_receive", "You receive on the day"), shortCash ? "-" + SvMoney(5200) : SvMoney(1128300), shortCash ? UiRole.DangerText : UiRole.OkText),
                    UiFactLine.Space(),
                    UiFactLine.Tick(shortCash ? Fx(W(t, "reallife_ui_svc_check_short", "Your cash is {amount} short of paying out the investors and loans"), "amount", SvMoney(5200))
                        : W(t, "reallife_ui_svc_check_repaid", "Investors and loans can be paid out today"), !shortCash),
                    UiFactLine.Tick(W(t, "reallife_svc_bid_staff_not", "The staff are not taken on. They stay with you, unassigned."), false),
                    UiFactLine.Section(W(t, "reallife_ui_svc_sec_give_up", "What you give up")),
                    UiFactLine.Row(W(t, "reallife_ui_svc_row_profit_day", "Profit, average a day"), SvMoney(4100)),
                    UiFactLine.Say(Fx(W(t, "reallife_ui_svc_lease_ends", "The lease ends and the deposit of {amount} comes back to you."), "amount", SvMoney(8400))),
                    UiFactLine.Say(Fx(W(t, "reallife_svc_bid_clause", "No business of that kind in that district for {days} days. Breaking it costs {share}% of the price."), "days", "30", "share", "15"))
                };
                if (refused)
                {
                    lines.Add(UiFactLine.Tick(W(t, "reallife_svc_refuse_inside", "Leave the building first: a business cannot be sold while you stand in it."), false));
                }
                p.Accept = new UiFactCardModel
                {
                    Title = Fx(W(t, "reallife_ui_svc_sec_accept", "If you accept {buyer}'s offer"), "buyer", "Huang Guo"), Heading = true, Lines = lines.ToArray(),
                    Actions = new[] { SvBtn(runs, W(t, "reallife_svc_bid_refuse", "Refuse"), UiButtonKind.Secondary, UiAction.Count),
                        SvBtn(runs, W(t, "reallife_svc_bid_counter", "Ask for more"), UiButtonKind.Secondary, UiAction.Count),
                        SvBtn(runs, W(t, "reallife_svc_bid_accept", "Accept"), UiButtonKind.Primary, UiAction.Alt, refused || shortCash) }
                };
            }
        }
        if (empty)
        {
            p.Price = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_price", "Or list it at your price"), Heading = true, Lines = new[] { UiFactLine.Say(W(t, "reallife_svc_sale_says_market", "On the market, offers come over the next days.")) }
            };
            p.PriceFoot = new UiFactCardModel { Lines = new[] { UiFactLine.Say(Fx(W(t, "reallife_svc_sale_says_quick", "A quick sale pays {share}% of the value at once."), "share", "70")) } };
        }
        else
        {
            int step = 4;
            p.Price = new UiFactCardModel
            {
                Title = W(t, "reallife_ui_svc_sec_price", "Or list it at your price"), Heading = true,
                Lines = new[] { SvStep(runs, W(t, "reallife_ui_svc_row_asking", "Asking price"), SvMoney(1380000), Fx(W(t, "reallife_ui_svc_price_hint", "{share} of what it is worth"), "share", "120%")) }
            };
            int[] pct = { 80, 90, 100, 110, 120, 135, 150 };
            int[] most = { 5, 5, 4, 3, 2, 1, 1 };
            var points = new string[7];
            var reads = new string[7];
            var offers = new float[7];
            var tints = new UiRole[7];
            for (int i = 0; i < 7; i++)
            {
                points[i] = pct[i] + "%";
                reads[i] = SvMoney(1284000 * pct[i] / 100);
                offers[i] = most[i];
                tints[i] = i == step ? UiRole.Accent : UiRole.Muted;
            }
            p.PriceBars = new UiChartModel
            {
                Kind = UiChartKind.Bars, Title = W(t, "reallife_ui_svc_bars_title", "Offers each price draws"), Points = points, Reads = reads, BarTints = tints, Whole = true, Focus = step,
                Series = new[] { new UiSeries { Label = W(t, "reallife_ui_svc_bars_series", "Offers at most"), Values = offers } }, Format = v => ((int)v).ToString()
            };
            var foot = new List<UiFactLine>
            {
                UiFactLine.Say(Fx(W(t, "reallife_svc_asking_says", "At that price: days between offers {days}, offers at most {most}."), "days", "4", "most", "2"))
            };
            if (listed)
            {
                foot.Add(UiFactLine.Say(Fx(W(t, "reallife_svc_listed_since", "On the market since day {day}, asking {amount}. Offers standing: {count}."), "day", "405", "amount", SvMoney(1380000), "count", "4")));
            }
            if (refused)
            {
                foot.Add(UiFactLine.Tick(W(t, "reallife_svc_refuse_inside", "Leave the building first: a business cannot be sold while you stand in it."), false));
            }
            var actions = new List<UiActionModel> { SvBtn(runs, Fx(W(t, "reallife_ui_svc_quick_at", "Quick sale {amount} now"), "amount", SvMoney(898800)), UiButtonKind.Secondary, UiAction.Count, refused) };
            if (listed)
            {
                actions.Add(SvBtn(runs, W(t, "reallife_svc_list_withdraw", "Take off the market"), UiButtonKind.Secondary, UiAction.Count));
            }
            actions.Add(SvBtn(runs, Fx(W(t, listed ? "reallife_ui_svc_change_to" : "reallife_ui_svc_list_at", listed ? "Change the price to {amount}" : "List at {amount}"), "amount", SvMoney(1380000)),
                UiButtonKind.Secondary, UiAction.Option, refused));
            p.PriceFoot = new UiFactCardModel { Lines = foot.ToArray(), Actions = actions.ToArray() };
        }
    }
}
