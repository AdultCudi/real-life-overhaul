using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The pause menu as a player meets it on day 412 of a run with a cafe and two more shops: saved fourteen minutes ago with
/// the autosave at its default, the city's card and its four latest saves of nine; the moment after a new city loaded, with
/// nothing saved yet and no save on disk; the menu with a row another mod put on the game's panel and the save's name held
/// by a shared session (no save may be loaded there); and in French, the longest of the game's words for these rows (the
/// game's own French; the mod's new words in English until the next translation batch).
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] PauseStates = { "menu", "unsaved", "other_mod", "menu_fr" };

    /// <summary>The pause menu in a state.</summary>
    public static UiPauseModel Pause(UiWords t, string state)
    {
        if (state.EndsWith("_fr"))
        {
            t = PauseFrench(t);
        }
        bool fresh = state == "unsaved", shared = state == "other_mod";
        var rows = new List<UiMenuRowModel>
        {
            new UiMenuRowModel { Icon = UiIcon.FloppyDisk, Label = t("reallife_ui_pause_save_game", "Save game"), Key = UiAction.Alt, Primary = true },
            new UiMenuRowModel { Icon = UiIcon.Gear, Label = t("menu_options", "Options"), Key = UiAction.Option },
            new UiMenuRowModel { Icon = UiIcon.Layout, Label = t("common_blueprints", "Blueprints"), Key = UiAction.Count }
        };
        if (shared)
        {
            rows.Add(new UiMenuRowModel { Icon = UiIcon.Cube, Label = "Session players", Key = UiAction.Count });
        }
        rows.Add(new UiMenuRowModel { Icon = UiIcon.Home, Tint = UiRole.CatTech, Label = t("reallife_ui_pause_main_menu", "Main menu"), Key = UiAction.Count, Split = true });
        rows.Add(new UiMenuRowModel { Icon = UiIcon.SignOut, Tint = UiRole.WarnText, Label = t("menu_save_and_exit_to_desktop", "Save and exit to desktop"), Key = UiAction.Count });
        rows.Add(new UiMenuRowModel { Icon = UiIcon.Power, Label = t("reallife_ui_pause_quit", "Quit to desktop"), Key = UiAction.Count, Danger = true });
        int day = fresh ? 1 : 412;
        var m = new UiPauseModel
        {
            Title = t("reallife_ui_pause_title", "Paused"),
            When = t("reallife_ui_pause_when", "Day {day}, {time}").Replace("{day}", day.ToString()).Replace("{time}", fresh ? "08:00" : "14:32"),
            NameLabel = t("reallife_ui_pause_name", "Save name"),
            Name = shared ? "Shared session, Speedy Bites" : fresh ? "Speedy Bites" : "Speedy Bites, Day 412",
            NamePlaceholder = t("common_name", "Name"),
            NameDisabled = shared,
            Saved = UiPauseMenu.SavedLine(t, fresh ? -1 : 14, 10),
            Rows = rows.ToArray(),
            Back = t("reallife_ui_pause_back", "Back to the city"),
            CityTitle = t("reallife_ui_pause_city", "This city"),
            CityNote = t(fresh ? "common_monday" : "common_tuesday", fresh ? "Monday" : "Tuesday"),
            Facts = new[]
            {
                new UiPauseFact { Icon = UiIcon.Wallet, Label = t("reallife_ui_pause_money", "Money"), Value = fresh ? "$4,200" : "$48,210" },
                new UiPauseFact { Icon = UiIcon.ChartLineUp, Label = t("reallife_ui_pause_worth", "Net worth"), Value = fresh ? "$4,200" : "$212,400" },
                new UiPauseFact { Icon = UiIcon.Store, Label = t("reallife_ui_pause_businesses", "Businesses"), Value = fresh ? "0" : "3" },
                new UiPauseFact { Icon = UiIcon.People, Label = t("reallife_ui_pause_employees", "Employees"), Value = fresh ? "0" : "14" },
                new UiPauseFact { Icon = UiIcon.Calendar, Label = t("reallife_ui_pause_day", "Day"), Value = day.ToString() },
                new UiPauseFact { Icon = UiIcon.Clock, Label = t("reallife_ui_pause_played", "Played"), Value = UiPauseMenu.Played(t, fresh ? 40L : 31L * 3600L + 20L * 60L) }
            },
            SavesTitle = t("reallife_ui_pause_saves", "Recent saves"),
            SavesEmpty = t("reallife_ui_pause_saves_none", "This city has no save on disk yet. Save game keeps one under the name above."),
            Load = t("reallife_ui_pause_load", "Load"),
            Over = t("reallife_ui_pause_over", "Save over")
        };
        if (!fresh)
        {
            m.SavesCount = t("reallife_ui_pause_saves_count", "{shown} of {all}").Replace("{shown}", "4").Replace("{all}", "9");
            m.Saves = new[]
            {
                new UiPauseSave { Name = shared ? "Shared session, Speedy Bites" : "Speedy Bites, Day 412", Line = UiPauseMenu.SaveLine(t, 412, 14), Current = true, NoLoad = shared },
                new UiPauseSave { Name = "Recover #3", Line = UiPauseMenu.SaveLine(t, 412, 4), Auto = true, NoLoad = shared },
                new UiPauseSave { Name = "Before the bank loan", Line = UiPauseMenu.SaveLine(t, 398, 26 * 60), NoLoad = shared },
                new UiPauseSave { Name = "Speedy Bites, Day 371, the week the second shop opened on Broadway", Line = UiPauseMenu.SaveLine(t, 371, 6 * 24 * 60), NoLoad = shared }
            };
        }
        return m;
    }

    private static UiWords PauseFrench(UiWords t)
    {
        var fr = new Dictionary<string, string>
        {
            { "menu_save", "Sauvegarder" }, { "menu_options", "Options" }, { "common_blueprints", "Plans" }, { "menu_main_menu", "Menu principal" },
            { "menu_save_and_exit_to_desktop", "Enregistrer et quitter vers le Bureau" }, { "menu_exit_to_desktop", "Quitter vers le bureau" }, { "common_name", "Nom" },
            { "common_tuesday", "Mardi" }, { "common_monday", "Lundi" }
        };
        return (key, english) => fr.TryGetValue(key, out string w) ? w : t(key, english);
    }
}
