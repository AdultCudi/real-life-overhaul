using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // Flare in every page and sheet (UiSamples.FlareStates), over the day streets and Midtown by night; the fields that take
    // the keys get their typing shots. The posts' photos are the mockup's (not-in-repo/previews/ui/flare_mock), cut to each
    // box's shape as the game's side cuts the bank's.
    static partial void AddFlare(List<Screen> screens)
    {
        Func<string, Sprite> before = UiPictures.Find;
        UiPictures.Find = key => key != null && key.StartsWith(FlareFace.PicturePrefix, StringComparison.Ordinal) ? FlarePicture(key) : before?.Invoke(key);
        Add("home", "13.jpg", FlareFace.LikeCommand, 0, "DRD");
        Add("home_following", "02.jpg", FlareFace.TabCommand, 1, "D");
        Add("home_businesses", "13.jpg", FlareFace.AuthorCommand, 0, "DD");
        Add("explore", "02.jpg", FlareFace.RowCommand, 1, "DD");
        Typed(Add("explore_search", "13.jpg", FlareFace.FollowCommand, 1, "D"), FlareFace.SearchField);
        Add("explore_tag", "11.jpg", FlareFace.LikeCommand, 0, "D");
        Add("notices", "13.jpg", FlareFace.RowCommand, 0, "DD");
        Add("notices_empty", "02.jpg", FlareFace.NavCommand, 0, "D");
        Typed(Add("messages", "02.jpg", FlareFace.QuickCommand, 1, "D"), FlareFace.ThreadField);
        Add("messages_offer", "13.jpg", FlareFace.QuickCommand, 0, "D");
        Add("messages_none", "02.jpg", FlareFace.RowCommand, 1, "D");
        Add("profile", "13.jpg", FlareFace.TabCommand, 1, "DD");
        Add("profile_other", "11.jpg", FlareFace.ProfileFollowCommand, 0, "D");
        Typed(Add("thread", "11.jpg", FlareFace.LikeCommand, 0, "D"), FlareFace.ReplyField);
        Add("business", "02.jpg", FlareFace.ButtonCommand, 0, "DD");
        Add("business_turning", "13.jpg", FlareFace.PickCommand, 1, "D");
        Add("business_none", "02.jpg", FlareFace.NavCommand, 6, "D");
        Add("studio", "13.jpg", FlareFace.RowCommand, 1, "DD");
        Add("hire", "02.jpg", FlareFace.ButtonCommand, 0, "DD");
        Add("city", "11.jpg", FlareFace.ButtonCommand, 1, "DD");
        Add("city_empty", "13.jpg", FlareFace.NavCommand, 8, "D");
        Typed(Add("compose", "13.jpg", FlareFace.SheetChipCommand, 0, "D"), FlareFace.ComposeField);
        Add("compose_promote", "02.jpg", FlareFace.SheetConfirmCommand, 0, "D");
        Add("hire_sheet", "13.jpg", FlareFace.SheetConfirmCommand, 0, "D");
        Add("reply_sheet", "02.jpg", FlareFace.OptionCommand, 1, "D");
        Typed(Add("photo_picker", "13.jpg", FlareFace.PickerPhotoCommand, 2, "D"), FlareFace.PickerField);
        Add("notices_sales", "02.jpg", FlareFace.MarkAllCommand, 0, "DDDR");
        Add("city_new", "13.jpg", FlareFace.NavCommand, 8, "DDDDRD");
        Add("compose_report", "11.jpg", FlareFace.SheetChipCommand, 2, "DDDDDDDD");
        Add("compose_moment", "02.jpg", FlareFace.SheetChipCommand, 1, "DDDDDDRD");
        Add("ad_sheet", "13.jpg", FlareFace.SheetConfirmCommand, 0, "DDDDDDD");
        Typed(Add("edit_sheet", "02.jpg", FlareFace.SheetConfirmCommand, 0, "DDDD"), FlareFace.BioField);

        Screen Add(string state, string backdrop, int hover, int arg, string walk)
        {
            var screen = new Screen
            {
                Name = "flare_" + state,
                AppName = "Flare",
                Icon = UiIcon.Fire,
                Backdrop = backdrop,
                HoverCommand = hover,
                HoverArg = arg,
                Walk = Moves(walk),
                Lay = (s, f, d, k) => UiSamples.Flare(s, f, d, k, ModEnglish, state)
            };
            screens.Add(screen);
            return screen;
        }

        void Typed(Screen screen, int field) => Typing(screen, FlareFace.FieldCommand, field);
    }

    private static readonly Dictionary<string, Sprite> FlarePictures = new Dictionary<string, Sprite>();
    private static string _flareMock;

    // A sample's photo ("flare:2.40:sample:p1" is the mockup's p1.jpg) cut about its middle to the shape its box wants; once.
    private static Sprite FlarePicture(string key)
    {
        if (FlarePictures.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        Sprite sprite = null;
        if (FlareFace.PictureOf(key, out float aspect, out string inner) && inner.StartsWith("sample:", StringComparison.Ordinal))
        {
            string path = Path.Combine(FlareMock(), inner.Substring("sample:".Length) + ".jpg");
            if (File.Exists(path))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                tex.LoadImage(File.ReadAllBytes(path));
                float w = tex.width, h = tex.height;
                float cw = Mathf.Min(w, h * aspect), ch = Mathf.Min(h, w / aspect);
                sprite = Sprite.Create(tex, new Rect((w - cw) / 2f, (h - ch) / 2f, cw, ch), new Vector2(0.5f, 0.5f));
            }
        }
        FlarePictures[key] = sprite;
        return sprite;
    }

    // The mockup's folder: not-in-repo beside the repository (or above a worktree of it), never committed.
    private static string FlareMock()
    {
        if (_flareMock != null)
        {
            return _flareMock;
        }
        _flareMock = string.Empty;
        for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
        {
            string mock = Path.Combine(dir.FullName, "not-in-repo", "previews", "ui", "flare_mock");
            if (Directory.Exists(mock))
            {
                _flareMock = mock;
                break;
            }
        }
        return _flareMock;
    }
}
