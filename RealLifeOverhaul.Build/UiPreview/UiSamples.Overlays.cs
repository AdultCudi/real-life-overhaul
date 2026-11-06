using System;
using RealLifeOverhaul.Ui;

/// <summary>
/// The cards over the world's items, as the kit draws them over the street or the shop's floor with the HUD's bar under them:
/// the small card over a shelf's stock (low, with its price, margin and next delivery), a priced item, a person at a till with
/// their face in the mood's ring, a recipe machine with its rate, a table with seats and a line, a till's requirement lines, a
/// street seat; the large card of a shelf's choice (closed and open on its second page), of a machine with its two choices and
/// the machines and ingredients it needs, of the fridge with the mod's Cook and Family buttons, of the radio, of a vehicle, of a
/// street cart, and of a workstation's name being typed; and a person's panel docked at the top left. The words are the mod's
/// keys, read in every language; the game's own words (an item's name, a station's name) are given as English.
/// </summary>
internal static partial class UiSamples
{
    public enum OverlayCase
    {
        StockLow, StockFull, Price, Person, PersonOff, Machine, Table, Till, Seat,
        Holds, HoldsOpen, Workstation, Fridge, Radio, Vehicle, Cart, Renaming, Empty
    }

    public static readonly OverlayCase[] OverlayHover = { OverlayCase.StockLow, OverlayCase.StockFull, OverlayCase.Price, OverlayCase.Person, OverlayCase.PersonOff, OverlayCase.Machine, OverlayCase.Table, OverlayCase.Till, OverlayCase.Seat };
    public static readonly OverlayCase[] OverlayClick = { OverlayCase.Holds, OverlayCase.HoldsOpen, OverlayCase.Workstation, OverlayCase.Fridge, OverlayCase.Radio, OverlayCase.Vehicle, OverlayCase.Cart, OverlayCase.Renaming, OverlayCase.Empty };

    /// <summary>One card of the cases above, with the words as the mod writes them.</summary>
    public static UiOverlayModel OverlayModel(UiWords t, IUiKeys k, OverlayCase which)
    {
        var m = new UiOverlayModel();
        string of(int a, int b) => t("reallife_ui_overlay_of", "{amount} of {room}").Replace("{amount}", a.ToString()).Replace("{room}", b.ToString());
        switch (which)
        {
            case OverlayCase.StockLow:
                m.Title = "Flour shelf";
                m.Icon = UiIcon.Package;
                m.Tag = t("reallife_ui_overlay_stock", "Stock");
                m.Figure = of(9, 40);
                m.Meter = 22;
                m.MeterTone = UiRole.Danger;
                m.AddRow(t("reallife_ui_overlay_item", "Item"), "Flour, 5 kg");
                m.AddRow(t("reallife_ui_overlay_price", "Price"), "$4.50");
                m.AddRow(t("reallife_ui_overlay_margin", "Margin"), t("reallife_ui_overlay_margin_of", "{margin}%, cost {cost}").Replace("{margin}", "69").Replace("{cost}", "$1.38"), UiRole.OkText);
                m.AddRow(t("reallife_ui_overlay_delivery", "Next delivery"), "Tuesday");
                m.AddAction(new UiOverlayAction { Label = "Click to manage", Hint = true });
                break;
            case OverlayCase.StockFull:
                m.Title = "Storage rack";
                m.Icon = UiIcon.Package;
                m.Tag = t("reallife_ui_overlay_stock", "Stock");
                m.Figure = of(31, 40);
                m.Meter = 77;
                m.MeterTone = UiRole.Ok;
                m.AddRow(t("reallife_ui_overlay_item", "Item"), "Espresso beans, 1 kg");
                m.AddRow(t("reallife_ui_overlay_delivery", "Next delivery"), t("reallife_ui_overlay_today", "Today"));
                m.AddAction(new UiOverlayAction { Label = "Click to add to inventory", Hint = true });
                break;
            case OverlayCase.Price:
                m.Title = "Cheeseburger";
                m.Icon = UiIcon.Tag;
                m.Tag = t("reallife_ui_overlay_price", "Price");
                m.Figure = "$4.50";
                m.FigureLabel = t("reallife_ui_overlay_price", "Price");
                m.AddRow(t("reallife_ui_overlay_customers", "Customers an hour"), "6");
                break;
            case OverlayCase.Person:
                m.Title = "Maria Rossi";
                m.Face = "Maria Rossi";
                m.Hue = 0.07f;
                m.Ring = UiRole.Ok;
                m.Tag = "Cash register";
                m.Figure = t("reallife_ui_overlay_until", "On shift until {time}").Replace("{time}", "15:00");
                m.Meter = 41;
                m.MeterTone = UiRole.Accent;
                m.AddRow(t("reallife_ui_overlay_mood", "Mood"), t("reallife_ui_overlay_mood_high", "Good") + ", 72%", UiRole.OkText);
                m.AddRow(t("reallife_ui_overlay_queue", "Queuing"), "2");
                break;
            case OverlayCase.PersonOff:
                m.Title = "Jordan Reyes";
                m.Face = "Jordan Reyes";
                m.Hue = 0.55f;
                m.Ring = UiRole.Danger;
                m.Tag = "Workbench";
                m.Figure = t("reallife_ui_overlay_off_shift", "Off shift");
                m.AddRow(t("reallife_ui_overlay_mood", "Mood"), t("reallife_ui_overlay_mood_low", "Unhappy") + ", 24%", UiRole.DangerText);
                break;
            case OverlayCase.Machine:
                m.Title = "Grill";
                m.Icon = UiIcon.Factory;
                m.Tag = t("reallife_ui_overlay_recipe", "Recipe");
                m.Figure = "Cheeseburger";
                m.FigureLabel = t("reallife_ui_overlay_per_hour", "{amount} an hour").Replace("{amount}", "14");
                m.AddRow(t("reallife_ui_overlay_needs", "Needs"), "Patty, bun, cheese");
                m.AddRow(t("reallife_ui_overlay_pallets", "On the pallets"), "3");
                m.AddAction(new UiOverlayAction { Label = "Click to manage", Hint = true });
                break;
            case OverlayCase.Table:
                m.Title = "Dining table";
                m.Icon = UiIcon.People;
                m.Tag = t("reallife_ui_overlay_seats", "Seats");
                m.Figure = of(3, 4);
                m.FigureLabel = t("reallife_ui_overlay_taken", "taken");
                m.Meter = 75;
                m.MeterTone = UiRole.Accent;
                break;
            case OverlayCase.Till:
                m.Title = "Till counter";
                m.Icon = UiIcon.ListChecks;
                m.Tag = t("reallife_ui_overlay_needs", "Needs");
                m.AddCheck("Cashier on shift", null, true);
                m.AddCheck("Card reader", null, true);
                m.AddCheck("Security camera", t("reallife_ui_overlay_short", "Short"), false);
                m.AddRow(t("reallife_ui_overlay_queue", "Queuing"), "2");
                break;
            case OverlayCase.Seat:
                m.Title = "Armchair";
                m.Icon = UiIcon.Home;
                m.Note = "A comfortable chair for a rest.";
                m.AddAction(new UiOverlayAction { Label = "Click to sit down", Hint = true });
                break;
            case OverlayCase.Holds:
            case OverlayCase.HoldsOpen:
                m.Detail = true;
                m.Title = "Display shelf";
                m.Icon = UiIcon.Package;
                m.Tag = t("reallife_ui_overlay_stock", "Stock");
                m.Choices[m.ChoiceCount++] = new UiOverlayChoice
                {
                    Label = t("reallife_ui_overlay_holds", "Holds"), Value = "Whole milk, 1 l", Options = Options(15), OptionCount = 15, Chosen = 7,
                    Open = which == OverlayCase.HoldsOpen, Page = 1,
                };
                m.AddRow(t("reallife_ui_overlay_stock", "Stock"), "18/24");
                m.AddAction(new UiOverlayAction { Label = "Remove content", Main = true });
                m.AddAction(new UiOverlayAction { Label = "Pick up one" });
                break;
            case OverlayCase.Workstation:
                m.Detail = true;
                m.Title = "Production line";
                m.Icon = UiIcon.Factory;
                m.Tag = t("reallife_ui_overlay_recipe", "Recipe");
                m.Choices[m.ChoiceCount++] = new UiOverlayChoice { Label = t("reallife_ui_overlay_workstation", "Workstation"), Value = "Burger assembly", Options = Options(3), OptionCount = 3, Chosen = 0 };
                m.Choices[m.ChoiceCount++] = new UiOverlayChoice { Label = t("reallife_ui_overlay_makes", "Makes"), Value = "Cheeseburger", Options = Options(4), OptionCount = 4, Chosen = 1 };
                m.AddCheck("Grill", null, true);
                m.AddCheck("Bun toaster", null, false);
                m.AddCheck("Patty", null, true);
                m.AddCheck("Cheese", t("reallife_ui_overlay_short", "Short"), false);
                break;
            case OverlayCase.Fridge:
                m.Detail = true;
                m.Title = "Fridge";
                m.Icon = UiIcon.ForkKnife;
                m.AddAction(new UiOverlayAction { Label = "Eat Leftover pasta (2)", Main = true });
                m.AddAction(new UiOverlayAction { Label = "Take Orange juice (1)" });
                m.AddAction(new UiOverlayAction { Label = "Cook a meal" });
                m.AddAction(new UiOverlayAction { Label = "Family" });
                m.AddAction(new UiOverlayAction { Label = "Empty the fridge" });
                break;
            case OverlayCase.Radio:
                m.Detail = true;
                m.Title = "Loudspeaker";
                m.Icon = UiIcon.MusicNotes;
                m.Tag = t("reallife_ui_overlay_radio", "Radio");
                m.AddRow(t("reallife_ui_overlay_station", "Station"), "Harbour FM");
                m.AddRow(t("reallife_ui_overlay_song", "Song"), "Night Shift");
                m.Level = 60;
                m.LevelLabel = t("reallife_ui_overlay_volume", "Volume");
                m.AddAction(new UiOverlayAction { Label = t("reallife_ui_overlay_next_station", "Next station"), Icon = UiIcon.SkipForward, Main = true });
                m.AddAction(new UiOverlayAction { Label = t("reallife_ui_overlay_mute", "Turn off"), Icon = UiIcon.Pause });
                break;
            case OverlayCase.Vehicle:
                m.Detail = true;
                m.Title = "Delivery van";
                m.Icon = UiIcon.Car;
                m.AddAction(new UiOverlayAction { Label = "Enter vehicle", Main = true });
                m.AddAction(new UiOverlayAction { Label = "Manage storage" });
                m.AddAction(new UiOverlayAction { Label = "Add hand truck items", Disabled = true });
                break;
            case OverlayCase.Cart:
                m.Detail = true;
                m.Title = "Hot dog cart";
                m.Icon = UiIcon.ShoppingCart;
                m.AddAction(new UiOverlayAction { Label = "Hot dog, $3.50", Main = true });
                m.AddAction(new UiOverlayAction { Label = "Pretzel, $2.75" });
                m.AddAction(new UiOverlayAction { Label = "Bottled water, $1.50" });
                break;
            case OverlayCase.Renaming:
                m.Detail = true;
                m.Title = "Cash register 2|";
                m.Icon = UiIcon.Person;
                m.Tag = t("reallife_ui_overlay_employee", "Employee");
                m.AddAction(new UiOverlayAction { Label = t("reallife_ui_overlay_rename", "Rename"), Icon = UiIcon.Pencil });
                m.AddAction(new UiOverlayAction { Label = "Work" });
                break;
            default:
                m.Detail = true;
                m.Title = "Fridge";
                m.Icon = UiIcon.ForkKnife;
                m.Note = "This fridge is empty.";
                m.AddAction(new UiOverlayAction { Label = "Add items", Main = true });
                break;
        }
        return m;
    }

    private static string[] Options(int count)
    {
        string[] names = { "Flour, 5 kg", "Sugar, 2 kg", "Whole milk, 1 l", "Butter, 250 g", "Eggs, dozen", "Olive oil, 1 l", "Rice, 5 kg", "Pasta, 500 g", "Tomatoes, 1 kg", "Onions, 1 kg",
            "Apples, 1 kg", "Bananas, 1 kg", "Orange juice, 1 l", "Coffee beans, 1 kg", "Tea bags, 100" };
        var list = new string[count];
        for (int i = 0; i < count; i++)
        {
            list[i] = names[i % names.Length];
        }
        return list;
    }

    /// <summary>A person's panel as the kit reads it from the game.</summary>
    public static UiInfoModel OverlayPerson(UiWords t, bool followed = false)
    {
        string name = "Maria Rossi";
        return new UiInfoModel
        {
            Title = t("reallife_ui_overlay_employee", "Employee"), Icon = UiIcon.Person, Name = name, Hue = 0.07f, Ring = UiRole.Ok,
            Status = t("reallife_ui_overlay_on_shift", "On shift"), StatusIcon = UiIcon.Check, StatusTone = UiRole.Ok,
            Facts = "Cash register, Speedy Bites, 7 5th Avenue",
            Sections = new[]
            {
                new UiInfoSection
                {
                    Label = t("reallife_ui_overlay_today_shift", "Today's shift"), Rows = new[]
                    {
                        new UiInfoRow { Label = t("reallife_ui_overlay_hours", "Hours"), Value = t("reallife_ui_overlay_range", "{from} to {to}").Replace("{from}", "07:00").Replace("{to}", "15:00") },
                        new UiInfoRow { Label = t("reallife_ui_overlay_worked", "Worked so far"), Value = t("reallife_ui_overlay_h_min", "{h} h {min} min").Replace("{h}", "3").Replace("{min}", "15"), Meter = true, Fill = 0.41f },
                    }
                },
                new UiInfoSection
                {
                    Label = t("reallife_ui_overlay_how", "How they are"), Rows = new[]
                    {
                        new UiInfoRow { Label = t("reallife_ui_overlay_mood", "Mood"), Value = t("reallife_ui_overlay_mood_high", "Good") + ", 72%", Ink = UiRole.OkText, Meter = true, Fill = 0.72f },
                        new UiInfoRow { Label = "Customer service", Value = "58%", Meter = true, Fill = 0.58f },
                    }
                },
                new UiInfoSection
                {
                    Label = t("reallife_ui_overlay_pay", "Pay"), Rows = new[]
                    {
                        new UiInfoRow { Label = t("reallife_ui_overlay_wage", "Wage"), Value = t("reallife_ui_overlay_per_hour_money", "{amount} an hour").Replace("{amount}", "$18.00") },
                        new UiInfoRow { Label = t("reallife_ui_overlay_earned", "Earned this week"), Value = "$612.00" },
                        new UiInfoRow { Label = t("reallife_ui_overlay_page", "Their page"), Value = t("reallife_ui_overlay_open_page", "Open {name}'s page").Replace("{name}", "Maria R."), Link = true },
                    }
                },
                new UiInfoSection
                {
                    Label = t("reallife_ui_overlay_wants", "Wants"), Rows = new[]
                    {
                        new UiInfoRow { Label = "Weekly hours", Value = t("reallife_ui_overlay_met", "Met"), Ink = UiRole.OkText },
                        new UiInfoRow { Label = "Health insurance", Value = t("reallife_ui_overlay_unmet", "Not met"), Ink = UiRole.WarnText },
                    }
                },
            },
            FooterLabel = t("reallife_ui_overlay_open", "Open page"),
            Footer = new[] { UiIcon.Locate, UiIcon.Navigation, followed ? UiIcon.EyeSlash : UiIcon.Eye, UiIcon.Calendar },
            FooterTints = new UiRole[4],
        };
    }

    /// <summary>A card over the street or the floor beside its point, the HUD's bar under it; the small card with its leader.</summary>
    public static void OverlayScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, UiOverlayModel card, float pointX, float pointY, bool night = false)
    {
        var m = new UiHudModel();
        Walking(m, t);
        WithBar(HudBar(t, false, false, night), () =>
        {
            UiHud.Lay(s, m, f, dark, k);
            float w = UiOverlayCard.Width(card), h = UiOverlayCard.Height(card);
            float px = f.Width * pointX, py = f.Height * pointY;
            UiHudRules.Beside(px, py, true, w, h, f.Width, f.BandY, 0f, 0f, 0f, 0f, out float x, out float y);
            x = UiDraw.Snap(x);
            y = UiDraw.Snap(y);
            UiOverlayCard.Lay(s, UiAt.Screen, card, x, y);
            if (!card.Detail && OverlayRules.LeaderShows(px, py, x, y, w, h, UiHudRules.Lift))
            {
                float line = UiTokens.S1 / 2f, reach = UiHudRules.Lift - UiOverlayCard.LeaderWidth / 2f;
                UiDraw.Box(s, UiAt.Screen, px - line / 2f, py - UiHudRules.Lift, line, reach, UiRole.Accent, UiRole.None, 0f, UiFlags.Decor);
                UiDraw.Circle(s, UiAt.Screen, px - UiOverlayCard.LeaderWidth / 2f, py - UiOverlayCard.LeaderWidth / 2f, UiOverlayCard.LeaderWidth, UiOverlayCard.LeaderWidth, UiRole.Accent, UiFlags.Decor);
            }
        });
    }

    /// <summary>A person's panel docked at the top left of the HUD's own column, over the shop floor.</summary>
    public static void OverlayPersonScene(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool followed = false)
    {
        var m = new UiHudModel { Info = OverlayPerson(t, followed) };
        Walking(m, t);
        WithBar(HudBar(t, false, false, false), () => UiHud.Lay(s, m, f, dark, k));
    }
}
