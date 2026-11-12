using System;
using System.Collections.Generic;
using System.Reflection;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

/// <summary>
/// A second mod that exists only in the checks and the previews: what another author's mod does to the screens the kit
/// draws over, as data and as code that can throw, game-free. It adds a phone app with its own picture and one without,
/// three apps on the full menu's own row alone (copies of the row's template, as a mod with no phone button makes them), a
/// button hung on the phone outside its grid, a BizMan tab and a loose button on the business page, a counter on the HUD's
/// bar, a canvas of its own over a kit screen, and it has a button and a text that throw. EconomyCheck (UiForeignChecks) runs each case through the kit's rules; the
/// preview draws the phone and BizMan as they stand with its pieces on them. Its throwing code stands in the namespace
/// FakeOtherModCode, which the checks tell the kit is another author's (UiOrigin.Other).
/// </summary>
internal static class FakeOtherMod
{
    public const string Name = "Fake Other Mod";
    public const string AppWithPicture = "FOM_Notes";    // a phone app with an icon of its own
    public const string AppWithoutPicture = "FOM_Tools"; // a phone app with none: the kit makes its mark
    public const string TitleWithPicture = "Fake Notes";
    public const string TitleWithoutPicture = "Fake Tools";
    public const int PictureHandle = 2;                  // the sample picture of the first app (UiSamples.PictureBank is 1)
    public const int RowPictureHandle = 3;               // the sample picture of its first app on the full menu's row
    public const string Hung = "FOM_Pin";                // a button it hung on the phone itself, outside the grid
    public const string TitleHung = "Fake Pin";
    public const string PanelButton = "FOM_Enter";       // a button it put on the building's panel of the HUD

    /// <summary>Its apps on the full menu's own row, by their buttons' names (none of them on the phone's grid), and the words under them.</summary>
    public static readonly string[] RowApps = { "FOM_Desk_Btn", "FOM_Vault_Btn", "FOM_Yard_Btn" };
    public static readonly string[] RowTitles = { "Fake Desk", "Fake Vault", "Fake Yard" };
    public const string Tab = "FOM_Tab";                 // its tab in BizMan's tab bar
    public const string TabWords = "Notes";
    public const string Button = "FOM_Button";           // a loose button on the business page, outside the tab bar
    public const string HudChild = "FOM_Counter";        // a piece it hangs on the HUD's bar
    public const int CanvasOrder = 12;                   // the sort order of a canvas it opens, under the kit's

    /// <summary>The names of a business page's tab bar as the game made it, and with the fake mod's tab among them.</summary>
    public static string[] TabBar(bool withFake)
    {
        var names = new List<string>(BizManRules.GameTabs);
        names.Add("RealLife Interior");   // the overhaul's own tab, by its prefix
        if (withFake)
        {
            names.Insert(3, Tab);
        }
        return names.ToArray();
    }

    /// <summary>The business page's children outside the tab bar, as the game made them (its layout) and with the fake mod's button.</summary>
    public static string[] PageChildren(bool withFake) => withFake ? new[] { "Layout 30-70", Button } : new[] { "Layout 30-70" };

    /// <summary>The game's top bar children the kit hides by name, and with the fake mod's counter after them.</summary>
    public static string[] HudBar(bool withFake)
    {
        string[] game = { "Avatar", "AccessoriesPanel", "PlayerStats", "Splitter", "Money", "MoneyTransaction", "Splitter (1)", "Date", "Time", "TimeControls" };
        if (!withFake)
        {
            return game;
        }
        var all = new List<string>(game) { HudChild };
        return all.ToArray();
    }

    /// <summary>Adds its two phone apps as the game's grid would hand them over: the first with its picture, the second without any.</summary>
    public static void AddApps(PhoneItems items)
    {
        ref PhoneItem notes = ref items.Add();
        notes.Id = AppWithPicture;
        notes.Title = TitleWithPicture;
        notes.Kind = PhoneItemKind.Other;
        notes.Picture = PictureHandle;
        notes.Badge = 1;
        ref PhoneItem tools = ref items.Add();
        tools.Id = AppWithoutPicture;
        tools.Title = TitleWithoutPicture;
        tools.Kind = PhoneItemKind.Other;
        tools.Picture = UiForeignMark.Of(TitleWithoutPicture, AppWithoutPicture);   // what the game file gives an app with no picture
    }

    /// <summary>
    /// Adds what the game file finds outside the grid, after the grid's apps as it adds them: so many of its apps of the
    /// full menu's row (the first with a picture, the rest with the kit's mark) and, when asked, the button hung on the phone.
    /// </summary>
    public static void AddExtras(PhoneItems items, int onRow, bool hung)
    {
        for (int i = 0; i < onRow && i < RowApps.Length; i++)
        {
            ref PhoneItem app = ref items.Add();
            app.Id = RowApps[i];
            app.Title = RowTitles[i];
            app.Kind = PhoneItemKind.Other;
            app.From = PhoneItemFrom.Row;
            app.Picture = i == 0 ? RowPictureHandle : UiForeignMark.Of(RowTitles[i], RowApps[i]);
        }
        if (hung)
        {
            ref PhoneItem pin = ref items.Add();
            pin.Id = Hung;
            pin.Title = TitleHung;
            pin.Kind = PhoneItemKind.Other;
            pin.From = PhoneItemFrom.Frame;
            pin.Picture = UiForeignMark.Of(TitleHung, Hung);
        }
    }

}

namespace FakeOtherModCode
{
    /// <summary>Another author's code in a namespace of its own, so the checks can tell its frames from the kit's.</summary>
    internal static class Code
    {
        public static void Click() => throw new InvalidOperationException("The fake mod's button handler failed.");

        public static string Label() => throw new NullReferenceException("The fake mod's label was read before it was set.");

        public static void Nested() => Click();   // a handler that calls another of its own: still its frame innermost

        public static void Calls(System.Action kit) => kit();   // the mod calls into the kit, which throws: the kit's frame is innermost
    }
}

internal static partial class UiSamples
{
    /// <summary>The phone with the fake mod's two apps among the others', the home's last page where they land.</summary>
    public static PhoneScreen PhoneFake(UiWords t, int page = 1) => Phone(t, -1, page, false, false, true, false, true, WithFake(t));

    /// <summary>The phone's apps with the fake mod's two added (before End).</summary>
    public static PhoneItems WithFake(UiWords t)
    {
        var items = new PhoneItems();
        PhoneItems basis = PhoneApps(t, true);
        items.Begin();
        for (int i = 0; i < basis.Count; i++)
        {
            items.Add() = basis.Items[i];
        }
        FakeOtherMod.AddApps(items);
        items.End(true);
        return items;
    }

    /// <summary>The phone's apps with the fake mod's two of the grid, so many of its apps of the full menu's row and its button hung on the phone.</summary>
    public static PhoneItems WithFakeExtras(UiWords t, int onRow = 3, bool hung = true)
    {
        var items = new PhoneItems();
        PhoneItems basis = PhoneApps(t, true);
        items.Begin();
        for (int i = 0; i < basis.Count; i++)
        {
            items.Add() = basis.Items[i];
        }
        FakeOtherMod.AddApps(items);
        FakeOtherMod.AddExtras(items, onRow, hung);
        items.End(true);
        return items;
    }
}
