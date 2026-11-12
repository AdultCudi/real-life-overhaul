using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] CheckoutCommands = UiCheckout.Commands;
    private static readonly int[] VehicleBuyCommands = UiVehicleBuy.Commands;
    private static readonly int[] ItemPanelCommands = UiItemPanel.Commands;
    private static readonly int[] CargoCommands = UiCargo.Commands;
    private static readonly int[] WardrobeCommands = UiWardrobe.Commands;
    private static readonly int[] GarageStripCommands = UiGarageStrip.Commands;

    // Buying and what the player holds, every state each has: laid out in every language, frame and input as any screen.
    static partial void UiCasesBuy(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "checkout":
                foreach (string state in UiSamples.CheckoutStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "Checkout", Commands = CheckoutCommands,
                        Lay = (s, f, d, k, t) => UiCheckout.Lay(s, UiSamples.Checkout(t, st), f, d, k) });
                }
                break;
            case "vehicle_purchase":
                foreach (string state in UiSamples.VehicleBuyStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "Vehicle", Commands = VehicleBuyCommands,
                        Lay = (s, f, d, k, t) => UiVehicleBuy.Lay(s, UiSamples.VehicleBuy(t, st), f, d, k) });
                }
                break;
            case "item_panel":
                foreach (string state in UiSamples.ItemPanelStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "HUD", Commands = ItemPanelCommands, Overlay = true,
                        Lay = (s, f, d, k, t) => UiItemPanel.Lay(s, UiSamples.ItemPanel(t, st), f, d, k) });
                }
                break;
            case "manage_cargo":
                foreach (string state in UiSamples.CargoStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "Cargo", Commands = CargoCommands,
                        Lay = (s, f, d, k, t) => UiCargo.Lay(s, UiSamples.Cargo(t, st), f, d, k) });
                }
                break;
            case "wardrobe":
                foreach (string state in UiSamples.WardrobeStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "Clothes", Commands = WardrobeCommands,
                        Lay = (s, f, d, k, t) => UiWardrobe.Lay(s, UiSamples.Wardrobe(t, st), f, d, k) });
                }
                break;
            case "garage_shop":
                foreach (string state in UiSamples.GarageStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "Garage", Commands = GarageStripCommands,
                        Lay = (s, f, d, k, t) => UiGarageStrip.Lay(s, UiSamples.GarageStrip(t, st), f, d, k) });
                }
                break;
        }
    }
}
