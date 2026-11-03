using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The pause menu as a player meets it on day 2 of a run with a cafe: saved fourteen minutes ago with the autosave at its
/// default; the moment after the city loaded with nothing saved yet; the menu with a row another mod put on the game's
/// panel and the save's name held by a shared session; and in French, the longest of the game's words for these rows
/// (the game's own French; the mod's new words in English until the next translation batch).
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
        var rows = new List<UiMenuRowModel>
        {
            new UiMenuRowModel { Icon = UiIcon.FloppyDisk, Label = t("menu_save", "Save"), Key = UiAction.Alt },
            new UiMenuRowModel { Icon = UiIcon.Gear, Label = t("menu_options", "Options"), Key = UiAction.Option },
            new UiMenuRowModel { Icon = UiIcon.Layout, Label = t("common_blueprints", "Blueprints"), Key = UiAction.Count }
        };
        if (state == "other_mod")
        {
            rows.Add(new UiMenuRowModel { Icon = UiIcon.Cube, Label = "Session players", Key = UiAction.Count });
        }
        rows.Add(new UiMenuRowModel { Icon = UiIcon.Home, Tint = UiRole.Chart7, Label = t("menu_main_menu", "Main Menu"), Key = UiAction.Count, Split = true });
        rows.Add(new UiMenuRowModel { Icon = UiIcon.SignOut, Label = t("menu_save_and_exit_to_desktop", "Save and exit to desktop"), Key = UiAction.Count });
        rows.Add(new UiMenuRowModel { Icon = UiIcon.Power, Label = t("menu_exit_to_desktop", "Exit to Desktop"), Key = UiAction.Count, Danger = true });
        return new UiPauseModel
        {
            Title = t("reallife_ui_pause_title", "Paused"),
            When = t("reallife_ui_pause_when", "Day {day}, {time}").Replace("{day}", "2").Replace("{time}", "08:45"),
            NameLabel = t("reallife_ui_pause_name", "Save name"),
            Name = state == "other_mod" ? "Shared session, Speedy Bites" : "Speedy Bites, Day 2",
            NamePlaceholder = t("common_name", "Name"),
            NameDisabled = state == "other_mod",
            Saved = UiPauseMenu.SavedLine(t, state == "unsaved" ? -1 : 14, 10),
            Rows = rows.ToArray(),
            Back = t("reallife_ui_pause_back", "Back to the city")
        };
    }

    private static UiWords PauseFrench(UiWords t)
    {
        var fr = new Dictionary<string, string>
        {
            { "menu_save", "Sauvegarder" }, { "menu_options", "Options" }, { "common_blueprints", "Plans" }, { "menu_main_menu", "Menu principal" },
            { "menu_save_and_exit_to_desktop", "Enregistrer et quitter vers le Bureau" }, { "menu_exit_to_desktop", "Quitter vers le bureau" }, { "common_name", "Nom" }
        };
        return (key, english) => fr.TryGetValue(key, out string w) ? w : t(key, english);
    }
}
