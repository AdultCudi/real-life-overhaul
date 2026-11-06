using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

/// <summary>
/// The tooltip card as a player meets it: an item's tooltip as the game builds it (its name, its customers an hour and its products), an
/// employee's (their years, the business and shift, the hours a week and the mood bar, today's customers served, their skills and what they
/// ask for), the net worth card over the money pill on a day 288 run, and Build mode's hover tip. The same models the live card makes
/// from the game's builder, laid by the same card.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] TooltipStates = { "item", "employee", "worth", "build", "worth_early" };

    /// <summary>The card's words for a state.</summary>
    public static UiTooltipModel Tooltip(UiWords t, string state)
    {
        switch (state)
        {
            case "item": return TooltipItem(t);
            case "employee": return TooltipEmployee(t);
            case "worth": return TooltipWorth(t, true);
            case "worth_early": return TooltipWorth(t, false);
            case "build": return TooltipBuild(t);
        }
        return TooltipItem(t);
    }

    private static UiTooltipModel TooltipItem(UiWords t)
    {
        var m = new UiTooltipModel();
        m.Text("Espresso machine", UiRole.None, true, true);
        m.Text(t("bizman_customers_capacity", "Customer capacity") + ": 4", UiRole.None, false, true);
        m.Text(t("common_products", "Products") + ":", UiRole.None, false, true);
        m.Text("Espresso", UiRole.None, false, true);
        m.Text("Latte", UiRole.None, false, true);
        m.Text("Flat white", UiRole.None, false, true);
        return m;
    }

    private static UiTooltipModel TooltipEmployee(UiWords t)
    {
        var m = new UiTooltipModel();
        m.Header("Maya Okafor", t("common_years_amount", "{years} years").Replace("{years}", "34"),
            "Speedy Bites · " + t("reallife_ui_tip_shift_until", "on shift until {time}").Replace("{time}", "19:00"));
        m.Rule();
        m.Text(t("common_hours_per_week", "{weeklyHours} hours a week").Replace("{weeklyHours}", "32").ToUpperInvariant());
        m.Meter(t("reallife_ui_tip_mood", "Mood"), t("reallife_ui_tip_mood_good", "Good"), 72, UiRole.Ok);
        m.Category(t("reallife_ui_tip_today", "Today"));
        UiTipBlock today = m.Rows();
        today.Lines.Add(new UiTipLine { Left = t("reallife_ui_tip_served", "Customers served"), Right = "41" });
        m.Rule();
        UiTipBlock skills = m.Rows();
        skills.Lines.Add(new UiTipLine { Left = t("ba:skill_customerservice", "Customer service"), Right = "68%" });
        skills.Lines.Add(new UiTipLine { Left = t("ba:skill_cooking", "Cooking"), Right = "41%" });
        skills.Lines.Add(new UiTipLine { Left = t("ba:skill_cleaning", "Cleaning"), Right = "55%" });
        m.Rule();
        UiTipBlock asks = m.Rows(UiTipKind.Checks);
        asks.Lines.Add(new UiTipLine { Left = t("tip_demand_flexible", "Flexible hours"), Right = t("tip_priority_low", "Low"), Ink = UiRole.OkText, On = true });
        asks.Lines.Add(new UiTipLine { Left = t("tip_demand_weekends", "Weekends off"), Right = t("tip_priority_high", "High"), Ink = UiRole.DangerText, On = false });
        m.Rule();
        m.Text(t("tooltip_right_click_to_remove", "Right click to remove").ToUpperInvariant(), UiRole.AccentText, false, true);
        return m;
    }

    private static UiTooltipModel TooltipBuild(UiWords t)
    {
        var m = new UiTooltipModel();
        m.Header("Midtown, 5th Avenue");
        m.Rule();
        m.Text("Rent $4.20 a day per square metre", UiRole.Text2);
        m.Text("Traffic: busy", UiRole.Text2);
        m.Text("Free to build", UiRole.OkText);
        return m;
    }

    private static UiTooltipModel TooltipWorth(UiWords t, bool history)
    {
        const int days = 30;
        var line = new float[days];
        long now = 1901000000L, high = 1931000000L, low = 1713000000L;
        for (int i = 0; i < days; i++)
        {
            double wave = Math.Sin(i * 0.45) * 0.035 + Math.Sin(i * 0.13) * 0.02;
            line[i] = (float)(1790000000.0 + i * 3900000.0 + wave * 1790000000.0);
        }
        line[days - 1] = now;
        var s = new WorthSummary
        {
            Now = now, Has = history, High = high, Low = low, HighDay = 281, LowDay = 262, Change = now - (long)line[0], FromDay = 259, Points = days
        };
        return UiNetWorthTip.Model(t, s, 288, line, ShortMoney);
    }

    private static string Money(long amount) => (amount < 0 ? "-" : string.Empty) + "$" + Math.Abs(amount).ToString("N0");

    private static string ShortMoney(long amount) => amount >= 1000000000L ? "$" + (amount / 1e9).ToString("0.0#") + "B" : amount >= 1000000L ? "$" + (amount / 1e6).ToString("0.#") + "M" : "$" + amount.ToString("N0");

    /// <summary>The card over a street: the HUD's bar below it as the game stands it, the card where the pointer rests.</summary>
    public static void TooltipScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state, float pointerX, float pointerY)
    {
        Hud(s, f, dark, k, t, new UiHudModel(), HudBar(t, false, false, false));
        UiTooltipModel m = Tooltip(t, state);
        float w = UiTooltipCard.Width(m), h = UiTooltipCard.Height(m, w);
        float x, y;
        if (state.StartsWith("worth"))
        {
            // Over the money pill at the bar's right end, as the host stands it.
            UiTooltipRules.OverPill(f.Width - 320f, f.BandY, 140f, w, h, f.Width, UiTooltipRules.Gap, out x, out y);
        }
        else
        {
            UiTooltipRules.Place(f.Width * pointerX, f.Height * pointerY, w, h, f.Width, f.Height, UiTooltipRules.Gap, out x, out y);
        }
        x = UiDraw.SnapDown(x);   // the preview lays it on the grid; in the game it stands wherever the pointer is
        y = UiDraw.SnapDown(y);
        var on = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 };
        UiTooltipCard.Lay(s, on, m, x, y, w);
    }
}
