using System.Collections.Generic;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The older shapes' own samples, every page of the gallery and every state of the parts' gallery. The desk session
    // stands indoors, where a desk is; the parts stand over the day street, as the style sheet does.
    static partial void AddKit(List<Screen> screens)
    {
        screens.Add(new Screen { Name = "dashboard", AppName = "Economy", Lay = (s, f, d, k) => UiDashboard.Lay(s, UiSamples.Dashboard(English), f, d, k),
            HoverCommand = UiDashboard.RowCommand, HoverArg = 2, Walk = new[] { Down, Down, Down, Right, Down, Left, Up, Up }, Backdrop = "02.jpg" });
        screens.Add(new Screen { Name = "session", AppName = "Desk", Lay = (s, f, d, k) => UiSessionSteps.Lay(s, UiSamples.Session(English), f, d, k),
            Walk = new[] { Down, Down, Down, Right, Left, Up }, Backdrop = "03.jpg" });
        screens.Add(new Screen { Name = "form", AppName = "CityAds", Lay = (s, f, d, k) => UiForm.Lay(s, UiSamples.Form(English), f, d, k),
            Walk = new[] { Down, Down, Down, Down, Right, Left }, Backdrop = "02.jpg" });
        for (int page = 0; page < UiGallery.PageCount; page++)
        {
            int p = page;
            screens.Add(new Screen { Name = "gallery_" + page, AppName = "Interface gallery", Lay = (s, f, d, k) => UiGallery.Lay(s, UiSamples.Gallery(English, p), f, d, k),
                Walk = new[] { Down, Down, Right, Down, Right, Down, Left }, Backdrop = "13.jpg" });
        }
        UiPictures.Find = Picture;
        for (int i = 0; i < UiSamples.PartStates.Length; i++)
        {
            string state = UiSamples.PartStates[i];
            var screen = new Screen { Name = "parts_" + (i + 1) + "_" + state, AppName = "Interface parts", Icon = UiIcon.Grid, Backdrop = "13.jpg",
                Lay = (s, f, d, k) => UiPartsGallery.Lay(s, UiSamples.Parts(English, state), f, d, k) };
            PartsInput(screen, state);
            if (state == "fields_typing")
            {
                Typing(screen, UiPartsGallery.Field, UiPartsGallery.Arg(true, 2, 0));   // the business's name takes the keys
            }
            else if (state == "amount_typing")
            {
                Typing(screen, UiPartsGallery.Amount, UiPartsGallery.Arg(false, 2, 2));   // the shelves' amount takes the keys
            }
            screens.Add(screen);
        }
    }

    private static Sprite _city;

    // The painting's picture offline: a view of Broadway at night from the game's own pictures, in the painting's 4:3, as
    // the game's sprite of it would stand there. Other keys have no picture, as a key the game lacks.
    private static Sprite Picture(string key)
    {
        if (key != UiPartsGalleryScreen.CityPicture || string.IsNullOrEmpty(_backdrops))
        {
            return null;
        }
        if (_city == null)
        {
            string path = System.IO.Path.Combine(_backdrops, "11.jpg");
            if (!System.IO.File.Exists(path))
            {
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(path));
            float h = tex.height * 0.7f, w = h * 4f / 3f;
            _city = Sprite.Create(tex, new Rect((tex.width - w) / 2f, (tex.height - h) / 2f, w, h), new Vector2(0.5f, 0.5f));
        }
        return _city;
    }

    // Where the mouse rests and the keys stand in each state, and the pad's walk from the page's first target.
    private static void PartsInput(Screen screen, string state)
    {
        switch (state)
        {
            case "controls":
                screen.HoverCommand = UiPartsGallery.Choice;
                screen.HoverArg = UiPartsGallery.Arg(false, 0, 1);
                screen.FocusCommand = UiPartsGallery.Slider;
                screen.FocusArg = UiPartsGallery.Arg(false, 1, 0);
                screen.Walk = Moves("UUURDDDLDDD");
                break;
            case "fields_typing":
                screen.HoverCommand = UiPartsGallery.Key;
                screen.HoverArg = UiPartsGallery.Arg(true, 2, 3);
                screen.FocusCommand = UiPartsGallery.Field;
                screen.FocusArg = UiPartsGallery.Arg(true, 2, 0);
                screen.Walk = Moves("DDDLUUU");
                break;
            case "amount_typing":
                screen.HoverCommand = UiPartsGallery.Choice;
                screen.HoverArg = UiPartsGallery.Arg(false, 0, 1);
                screen.FocusCommand = UiPartsGallery.Amount;
                screen.FocusArg = UiPartsGallery.Arg(false, 2, 2);
                screen.Walk = Moves("DDDLUUU");
                break;
            case "choice_open":
                screen.HoverCommand = UiPartsGallery.Option;
                screen.HoverArg = 3;
                screen.Walk = Moves("DDDUU");
                break;
            case "table_picked":
                screen.HoverCommand = UiDataTable.First + UiDataTable.Row;
                screen.HoverArg = 5;
                screen.FocusCommand = UiDataTable.First + UiDataTable.Row;
                screen.FocusArg = 3;   // on the last row picked, which the detail shows
                screen.Walk = Moves("UUUURRRDDDL");
                break;
            case "table":
            case "table_empty":
                screen.HoverCommand = UiDataTable.First + UiDataTable.Row;
                screen.HoverArg = 5;
                screen.FocusCommand = UiDataTable.First + UiDataTable.Row;
                screen.FocusArg = 0;   // the row the detail shows: focus drives it, so the ring and the cyan fifth stand on one row
                screen.Walk = Moves("RUURRRDDDLL");
                break;
            case "charts":
                screen.HoverCommand = UiChart.First + UiChart.Point;
                screen.HoverArg = 8;
                screen.FocusCommand = UiChart.First + UiChart.Point;
                screen.FocusArg = 8;
                screen.Walk = Moves("LLDRRRRUL");
                break;
            case "catalogue":
            case "catalogue_empty":
                screen.HoverCommand = UiCatalogue.First + UiCatalogue.Card;
                screen.HoverArg = 2;
                screen.FocusCommand = UiCatalogue.First + UiCatalogue.Card;
                screen.FocusArg = 6;
                screen.Walk = Moves("DLLURRRUL");
                break;
            case "statement":
            case "statement_new":
                screen.HoverCommand = UiStatement.First + UiStatement.Line;
                screen.HoverArg = 2;
                screen.Walk = Moves("RRDDDDDU");
                break;
            default:
                screen.HoverCommand = UiTrack.First + 1;
                screen.HoverArg = 2;
                screen.FocusCommand = UiTrack.First;
                screen.FocusArg = 1;
                screen.Walk = Moves("RRDRDLL");
                break;
        }
    }
}
