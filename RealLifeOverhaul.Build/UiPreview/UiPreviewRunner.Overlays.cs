using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The cards over the world's items: each small card beside its point over the shop, the factory, the warehouse or the room it
    // stands in (a shelf's low stock and a full one, a priced item, a person on and off shift, a recipe machine, a table, a till, a
    // seat); each large card (a shelf's choice closed and open, a machine, the fridge with the mod's buttons, the radio, a vehicle,
    // a street cart, a workstation being renamed, an empty fridge); a person's panel docked, and followed. Then the longest of the
    // languages beside the English: German for the small and the large card, Portuguese and Chinese for the cards and the panel.
    static partial void AddOverlays(List<Screen> screens)
    {
        var hover = new[]
        {
            new[] { "stock_low", "07.jpg", "0.5", "0.55" }, new[] { "stock_full", "10.jpg", "0.44", "0.5" }, new[] { "price", "07.jpg", "0.62", "0.5" },
            new[] { "person", "00.jpg", "0.5", "0.5" }, new[] { "person_off", "09.jpg", "0.58", "0.52" }, new[] { "machine", "09.jpg", "0.46", "0.55" },
            new[] { "table", "00.jpg", "0.5", "0.6" }, new[] { "till", "07.jpg", "0.4", "0.5" }, new[] { "seat", "00.jpg", "0.56", "0.58" },
        };
        for (int i = 0; i < hover.Length; i++)
        {
            string[] h = hover[i];
            UiSamples.OverlayCase which = UiSamples.OverlayHover[i];
            float x = float.Parse(h[2], System.Globalization.CultureInfo.InvariantCulture), y = float.Parse(h[3], System.Globalization.CultureInfo.InvariantCulture);
            screens.Add(Overlay("overlays_" + h[0], h[1], ModEnglish, which, x, y));
        }
        var click = new[]
        {
            new[] { "holds", "07.jpg", "0.5", "0.5", UiOverlayCard.ChoiceCommand.ToString() }, new[] { "holds_open", "07.jpg", "0.5", "0.5", UiOverlayCard.OptionCommand.ToString() },
            new[] { "workstation", "09.jpg", "0.5", "0.55", UiOverlayCard.ChoiceCommand.ToString() }, new[] { "fridge", "00.jpg", "0.5", "0.55", UiOverlayCard.ActionCommand.ToString() },
            new[] { "radio", "00.jpg", "0.5", "0.5", UiOverlayCard.StepCommand.ToString() }, new[] { "vehicle", "10.jpg", "0.5", "0.55", UiOverlayCard.ActionCommand.ToString() },
            new[] { "cart", "02.jpg", "0.5", "0.6", UiOverlayCard.ActionCommand.ToString() }, new[] { "renaming", "00.jpg", "0.5", "0.55", UiOverlayCard.ActionCommand.ToString() },
            new[] { "empty", "00.jpg", "0.5", "0.55", UiOverlayCard.ActionCommand.ToString() },
        };
        for (int i = 0; i < click.Length; i++)
        {
            string[] c = click[i];
            UiSamples.OverlayCase which = UiSamples.OverlayClick[i];
            float x = float.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture), y = float.Parse(c[3], System.Globalization.CultureInfo.InvariantCulture);
            screens.Add(Overlay("overlays_" + c[0], c[1], ModEnglish, which, x, y, int.Parse(c[4], System.Globalization.CultureInfo.InvariantCulture), c[0] == "holds_open" ? 2 : 1));
        }
        screens.Add(Person("overlays_person_panel", "00.jpg", ModEnglish, false));
        screens.Add(Person("overlays_person_followed", "00.jpg", ModEnglish, true));
        screens.Add(Overlay("overlays_longest_stock", "07.jpg", BizGerman, UiSamples.OverlayCase.StockLow, 0.5f, 0.55f));
        screens.Add(Overlay("overlays_longest_person", "00.jpg", BizGerman, UiSamples.OverlayCase.Person, 0.5f, 0.5f));
        screens.Add(Overlay("overlays_longest_machine", "09.jpg", BizGerman, UiSamples.OverlayCase.Machine, 0.46f, 0.55f));
        screens.Add(Overlay("overlays_longest_workstation", "09.jpg", BizGerman, UiSamples.OverlayCase.Workstation, 0.5f, 0.55f, UiOverlayCard.ChoiceCommand, 1));
        screens.Add(Person("overlays_longest_person_panel", "00.jpg", BizGerman, false));
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords t = ModWords(lang);
            string suffix = lang == "pt" ? "_pt" : "_zh";
            screens.Add(Overlay("overlays_stock_low" + suffix, "07.jpg", t, UiSamples.OverlayCase.StockLow, 0.5f, 0.55f));
            screens.Add(Overlay("overlays_person" + suffix, "00.jpg", t, UiSamples.OverlayCase.Person, 0.5f, 0.5f));
            screens.Add(Overlay("overlays_machine" + suffix, "09.jpg", t, UiSamples.OverlayCase.Machine, 0.46f, 0.55f));
            screens.Add(Overlay("overlays_workstation" + suffix, "09.jpg", t, UiSamples.OverlayCase.Workstation, 0.5f, 0.55f, UiOverlayCard.ChoiceCommand, 1));
            screens.Add(Overlay("overlays_radio" + suffix, "00.jpg", t, UiSamples.OverlayCase.Radio, 0.5f, 0.5f, UiOverlayCard.StepCommand, 1));
            screens.Add(Person("overlays_person_panel" + suffix, "00.jpg", t, false));
        }
    }

    // A card over its backdrop beside its point, the HUD's bar under it; the mouse on one of its targets in the hover shot.
    private static Screen Overlay(string name, string backdrop, UiWords t, UiSamples.OverlayCase which, float x, float y, int hoverCommand = -1, int hoverArg = 0) =>
        new Screen
        {
            Name = name, AppName = "HUD", Backdrop = backdrop, HoverCommand = hoverCommand, HoverArg = hoverArg, Walk = Moves("R"),
            Lay = (s, f, d, k) => UiSamples.OverlayScene(s, f, d, k, t, UiSamples.OverlayModel(t, k, which), x, y)
        };

    // A person's panel docked at the HUD's top left over the shop's floor.
    private static Screen Person(string name, string backdrop, UiWords t, bool followed) =>
        new Screen
        {
            Name = name, AppName = "HUD", Backdrop = backdrop, HoverCommand = UiInfoPanel.FooterCommand, HoverArg = 2, Walk = Moves("DDDDDDDDDLDRR"),
            Lay = (s, f, d, k) => UiSamples.OverlayPersonScene(s, f, d, k, t, followed)
        };
}
