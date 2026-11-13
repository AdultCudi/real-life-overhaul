using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The buying screens in every state their samples hold, each over what it would stand on: the checkout in the stores
    // (07 and 00), the tax office indoors (05) and the casino boat's ticket at the harbour (08); a car's purchase on the day
    // and the night street, the boat's at the harbour; the item panel on the day and the night street, indoors with a
    // mop and a basket, and once over the game's own item panel (06) to compare; manage cargo on the street; the clothes
    // shop in the store, the wardrobe in the villa, the hairdresser and the surgeon indoors; the garage shop on the street.
    // The French states are in French, the longest language for these screens' words, the game's words from its own file.
    // Pictures: the mockups' thumbnails of the game's goods (local only, like the backdrops), and a swatch's colour flat.
    static partial void AddBuy(List<Screen> screens)
    {
        UiPictures.Answer(BuyPicture);
        string[] checkout = { "07.jpg", "00.jpg", "05.jpg", "07.jpg", "08.jpg", "00.jpg", "07.jpg" };
        for (int i = 0; i < UiSamples.CheckoutStates.Length; i++)
        {
            string st = UiSamples.CheckoutStates[i];
            var screen = new Screen
            {
                Name = "buy_checkout_" + st, AppName = "Checkout", Icon = UiIcon.ShoppingCart, Backdrop = checkout[i],
                Lay = (s, f, d, k) => UiCheckout.Lay(s, UiSamples.Checkout(English, st), f, d, k), Bar = BuyBar(false, checkout[i])
            };
            CheckoutInput(screen, st);
            screens.Add(screen);
        }
        string[] vehicle = { "13.jpg", "11.jpg", "08.jpg", "13.jpg" };
        for (int i = 0; i < UiSamples.VehicleBuyStates.Length; i++)
        {
            string st = UiSamples.VehicleBuyStates[i];
            screens.Add(new Screen
            {
                Name = "buy_vehicle_" + st, AppName = "Vehicle", Icon = UiIcon.Car, Backdrop = vehicle[i],
                Lay = (s, f, d, k) => UiVehicleBuy.Lay(s, UiSamples.VehicleBuy(English, st), f, d, k), Bar = BuyBar(false, vehicle[i]),
                FocusCommand = UiVehicleBuy.SwatchCommand, FocusArg = 0, HoverCommand = UiVehicleBuy.BuyCommand, HoverArg = 0,
                Walk = Moves(st == "car_delivery" ? "RRRDDDR" : "RRRDR")
            });
        }
        string[] panel = { "13.jpg", "11.jpg", "13.jpg", "00.jpg", "07.jpg", "05.jpg", "02.jpg", "13.jpg", "13.jpg", "13.jpg" };
        for (int i = 0; i < UiSamples.ItemPanelStates.Length; i++)
        {
            string st = UiSamples.ItemPanelStates[i];
            screens.Add(ItemPanelShot("buy_item_panel_" + st, st, panel[Math.Min(i, panel.Length - 1)]));   // the later states stand on the last backdrop
        }
        screens.Add(ItemPanelShot("buy_item_panel_vehicle_night", "vehicle", "11.jpg"));
        screens.Add(ItemPanelShot("buy_item_panel_vehicle_before", "vehicle", "06.jpg"));
        string[] cargo = { "13.jpg", "11.jpg", "02.jpg", "13.jpg" };
        for (int i = 0; i < UiSamples.CargoStates.Length; i++)
        {
            string st = UiSamples.CargoStates[i];
            screens.Add(new Screen
            {
                Name = "buy_cargo_" + st, AppName = "Cargo", Icon = UiIcon.Package, Backdrop = cargo[i],
                Lay = (s, f, d, k) => UiCargo.Lay(s, UiSamples.Cargo(English, st), f, d, k), Bar = BuyBar(true, cargo[i]),
                FocusCommand = st == "empty" ? -1 : UiCargo.CatalogueCommand + UiCatalogue.Card, FocusArg = 0,
                HoverCommand = st == "empty" ? -1 : UiCargo.CatalogueCommand + UiCatalogue.Card, HoverArg = 2, Walk = Moves(st == "empty" ? "" : "RRDRR")
            });
        }
        string[] wardrobe = { "00.jpg", "12.jpg", "05.jpg", "00.jpg" };
        for (int i = 0; i < UiSamples.WardrobeStates.Length; i++)
        {
            string st = UiSamples.WardrobeStates[i];
            screens.Add(new Screen
            {
                Name = "buy_wardrobe_" + st, AppName = "Clothes", Icon = UiIcon.Person, Backdrop = wardrobe[i],
                Lay = (s, f, d, k) => UiWardrobe.Lay(s, UiSamples.Wardrobe(English, st), f, d, k), Bar = BuyBar(false, wardrobe[i]),
                FocusCommand = UiWardrobe.GridCommand + UiCatalogue.Card, FocusArg = 0,
                HoverCommand = UiWardrobe.GridCommand + UiCatalogue.Card, HoverArg = 2,
                Walk = Moves("RRDDRD")
            });
        }
        string[] garage = { "13.jpg", "11.jpg", "02.jpg" };
        for (int i = 0; i < UiSamples.GarageStates.Length; i++)
        {
            string st = UiSamples.GarageStates[i];
            screens.Add(new Screen
            {
                Name = "buy_garage_" + st, AppName = "Garage", Icon = UiIcon.Wrench, Backdrop = garage[i],
                Lay = (s, f, d, k) => UiGarageStrip.Lay(s, UiSamples.GarageStrip(English, st), f, d, k), Bar = BuyBar(false, garage[i]),
                FocusCommand = UiGarageStrip.CardCommand, FocusArg = 0, HoverCommand = UiGarageStrip.CardCommand, HoverArg = 1, Walk = Moves("DDDD")
            });
        }
    }

    // The bar every buying screen stands over: the HUD's own, the one the game shows while walking (UiSamples.HudBar), at night
    // over the night street (11), paused under the cargo; its pills pictures while the screen takes the keys.
    private static Func<UiStatusModel> BuyBar(bool paused, string backdrop) => () =>
    {
        UiStatusModel bar = UiSamples.HudBar(English, paused, false, backdrop == "11.jpg");
        bar.PillsPress = false;
        return bar;
    };

    // The item panel takes no key: the game reads its own, so its keys and pad shots show it as the keys leave it. It stands
    // over the HUD's bar, its pills live as they are on the street.
    private static Screen ItemPanelShot(string name, string state, string backdrop) => new Screen
    {
        Name = name, AppName = "HUD", Icon = UiIcon.Package, Backdrop = backdrop, NoKeys = true,
        Bar = () => UiSamples.HudBar(English, false, false, backdrop == "11.jpg"),
        Lay = (s, f, d, k) =>
        {
            UiItemPanel.Lay(s, UiSamples.ItemPanel(English, state), f, d, k);
            if (state == "paper_bag_minimap")
            {
                // A stand in for the mod's minimap card (Navigation draws it on the game's HUD, not the kit): its place only.
                UiDraw.Box(s, UiAt.Screen, UiSamples.MinimapX, UiSamples.MinimapY, UiSamples.MinimapW, UiSamples.MinimapH, UiRole.Field, UiRole.Line, UiTokens.RadiusPanel,
                    UiFlags.Decor);
                UiDraw.TextMiddle(s, UiAt.Screen, UiSamples.MinimapX + UiTokens.S4, UiSamples.MinimapY, UiTokens.S12, UiSamples.MinimapW - UiTokens.S8, UiStyle.SectionHeader,
                    UiRole.Text2, "MINIMAP, A STAND IN", UiFlags.Truncate);
            }
            UiDraw.StatusBar(s, f, k, false);
        },
        HoverCommand = UiItemPanel.ActionCommand, HoverArg = 1, Walk = Moves("")
    };

    // Where the mouse rests and the keys stand in each checkout state, and the pad's walk from there.
    private static void CheckoutInput(Screen screen, string state)
    {
        switch (state)
        {
            case "taxes":
                screen.FocusCommand = UiCheckout.TaxTypeCommand;
                screen.HoverCommand = UiCheckout.TaxPayCommand;
                screen.HoverArg = 1;
                screen.Walk = Moves("RDLDR");
                break;
            case "queue":
                screen.FocusCommand = UiCheckout.CancelCommand;
                screen.HoverCommand = UiCheckout.CancelCommand;
                screen.Walk = Moves("R");
                break;
            case "empty":
                screen.FocusCommand = UiCheckout.CancelCommand;
                screen.HoverCommand = UiCheckout.OrderCommand;
                screen.Walk = Moves("R");
                break;
            case "ticket":
                screen.FocusCommand = UiCheckout.OrderCommand;
                screen.HoverCommand = UiCheckout.CancelCommand;
                screen.Walk = Moves("L");
                break;
            default:
                screen.FocusCommand = UiCheckout.MoreCommand;
                screen.HoverCommand = UiCheckout.LessCommand;
                screen.HoverArg = 1;
                screen.Walk = Moves("DDDLDR");
                break;
        }
    }

    private static readonly Dictionary<string, Sprite> BuyPictures = new Dictionary<string, Sprite>(32);
    private static string _buyThumbs;

    // "buy:<name>": the mockups' thumbnail of that name; "swatch:RRGGBB": a square of that colour. Null for any other key.
    private static Sprite BuyPicture(string key)
    {
        if (key == null)
        {
            return null;
        }
        bool swatch = key.StartsWith(UiDraw.SwatchPicture, StringComparison.Ordinal);
        if (!swatch && !key.StartsWith("buy:", StringComparison.Ordinal))
        {
            return null;
        }
        if (BuyPictures.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        Sprite sprite = null;
        if (swatch)
        {
            int rgb = UiDraw.SwatchColour(key);
            if (rgb >= 0)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
                var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = c;
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
        }
        else
        {
            string path = Path.Combine(BuyThumbs(), key.Substring(4) + ".png");
            if (File.Exists(path))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(path));
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        BuyPictures[key] = sprite;
        return sprite;
    }

    // The mockups' thumbnails: not-in-repo beside the repository (or above a worktree of it), never committed.
    private static string BuyThumbs()
    {
        if (_buyThumbs != null)
        {
            return _buyThumbs;
        }
        _buyThumbs = string.Empty;
        for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
        {
            string thumbs = Path.Combine(dir.FullName, "not-in-repo", "previews", "ui", "mockups", "buy", "thumbs");
            if (Directory.Exists(thumbs))
            {
                _buyThumbs = thumbs;
                break;
            }
        }
        return _buyThumbs;
    }
}
