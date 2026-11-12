using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    private static bool _contactPictures;   // the firms' pictures answered among the kit's, added once
    private static readonly Dictionary<string, Sprite> ContactLogos = new Dictionary<string, Sprite>();

    // A firm's picture as the game's Contacts shows it ("contact:" and a shape of the game's logo maker, its sign's colour
    // after the colon): the game's own logo shape, white on the sign's colour, as its square signs are drawn.
    private static void ContactPicturesOn()
    {
        if (_contactPictures)
        {
            return;
        }
        _contactPictures = true;
        UiPictures.Answer(key => key != null && key.StartsWith("contact:", StringComparison.Ordinal) ? ContactLogo(key) : null);
    }

    private static Sprite ContactLogo(string key)
    {
        if (ContactLogos.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        string[] parts = key.Substring("contact:".Length).Split(':');
        string path = Path.Combine(BizGameShapes, parts[0] + ".png");
        Sprite sprite = null;
        if (File.Exists(path) && parts.Length > 1 && ColorUtility.TryParseHtmlString("#" + parts[1], out Color sign))
        {
            var shape = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            shape.LoadImage(File.ReadAllBytes(path));
            const int side = 128;
            var tex = new Texture2D(side, side, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[side * side];
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    float u = (x - side * 0.15f) / (side * 0.7f), v = (y - side * 0.15f) / (side * 0.7f);
                    float a = u >= 0f && u <= 1f && v >= 0f && v <= 1f ? shape.GetPixelBilinear(u, v).a : 0f;
                    pixels[y * side + x] = Color.Lerp(sign, Color.white, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f));
        }
        ContactLogos[key] = sprite;
        return sprite;
    }

    // The game's Contacts app on the kit in every state, over the day street the full menu leaves, with the frame's app row
    // under it; a call over a day street and the night street, as a call comes wherever the player is.
    static partial void AddContacts(List<Screen> screens)
    {
        ContactPicturesOn();
        foreach (string state in UiSamples.ContactsStates)
        {
            string st = state;
            bool call = st.StartsWith("call");
            var screen = new Screen
            {
                Name = "contacts_" + st, AppName = "Contacts", Icon = UiIcon.AddressBook, Backdrop = st == "call_replies" ? "11.jpg" : call ? "02.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, false), Lay = (s, f, d, k) =>
                {
                    ContactPicturesOn();
                    UiContacts.Lay(s, UiSamples.ContactsSample(English, st), f, d, k);
                }
            };
            if (call)
            {
                screen.HoverCommand = st == "call_start" ? UiCallCard.ButtonCommand : UiCallCard.ChoiceCommand;
                screen.HoverArg = st == "call_start" ? 0 : 1;
                screen.FocusCommand = st == "call_start" ? UiCallCard.ButtonCommand : UiCallCard.ChoiceCommand;
                screen.Walk = new[] { Down, Down, Up };
            }
            else if (st == "offer" || st == "offer_fr" || st == "wage")
            {
                screen.HoverCommand = UiConversation.ChoiceCommand;
                screen.HoverArg = 1;
                screen.FocusCommand = st == "wage" ? UiConversation.FormCommand : UiConversation.ChoiceCommand;
                screen.Walk = st == "wage" ? new[] { Down, Down, Down, Up, Right, Left, Left } : new[] { Down, Down, Up, Right, Left, Left };
            }
            else if (st == "empty")
            {
                screen.HoverCommand = UiAppWindow.NavCommand;
                screen.HoverArg = 2;
                screen.FocusCommand = UiMessageList.SearchCommand;
                screen.Walk = new[] { Left, Down, Down, Right };
            }
            else if (st == "all")
            {
                screen.HoverCommand = UiMessageList.RowCommand;
                screen.HoverArg = 1;
                screen.FocusCommand = UiMessageList.RowCommand;
                screen.FocusArg = UiSamples.ContactsChosen(st);
                screen.Walk = new[] { Up, Down, Down, Right, Up, Up, Up, Down, Left, Left };
            }
            else
            {
                // A group of one or two: the mouse rests on its own action, the keys go down the short list, across and back.
                screen.HoverCommand = UiContacts.ActionCommand;
                screen.HoverArg = 0;
                screen.FocusCommand = UiMessageList.RowCommand;
                screen.FocusArg = UiSamples.ContactsChosen(st);
                screen.Walk = new[] { Down, Right, Up, Down, Left, Left };
            }
            screens.Add(screen);
        }
    }
}
