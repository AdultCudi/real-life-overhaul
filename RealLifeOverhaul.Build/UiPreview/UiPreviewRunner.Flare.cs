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
        Add("home", "13.jpg", FlareFace.LikeCommand, 0);
        Add("home_following", "02.jpg", FlareFace.TabCommand, 1);
        Add("home_businesses", "13.jpg", FlareFace.AuthorCommand, 0);
        Add("explore", "02.jpg", FlareFace.RowCommand, 1);
        Typed(Add("explore_search", "13.jpg", FlareFace.FollowCommand, 1), FlareFace.SearchField);
        Add("explore_tag", "11.jpg", FlareFace.LikeCommand, 0);
        Add("notices", "13.jpg", FlareFace.RowCommand, 0);
        Add("notices_empty", "02.jpg", FlareFace.NavCommand, 0);
        Typed(Add("messages", "02.jpg", FlareFace.QuickCommand, 1), FlareFace.ThreadField);
        Add("messages_offer", "13.jpg", FlareFace.QuickCommand, 0);
        Add("messages_none", "02.jpg", FlareFace.RowCommand, 1);
        Add("profile", "13.jpg", FlareFace.TabCommand, 1);
        Add("profile_other", "11.jpg", FlareFace.ProfileFollowCommand, 0);
        Typed(Add("thread", "11.jpg", FlareFace.LikeCommand, 0), FlareFace.ReplyField);
        Add("thread_mine", "13.jpg", FlareFace.LikeCommand, 0);
        Add("business", "02.jpg", FlareFace.ButtonCommand, 0);
        Add("business_turning", "13.jpg", FlareFace.PickCommand, 1);
        Add("business_none", "02.jpg", FlareFace.NavCommand, 6);
        Add("studio", "13.jpg", FlareFace.RowCommand, 1);
        Add("hire", "02.jpg", FlareFace.ButtonCommand, 0);
        Add("city", "11.jpg", FlareFace.ButtonCommand, 1);
        Add("city_empty", "13.jpg", FlareFace.NavCommand, 8);
        Typed(Add("compose", "13.jpg", FlareFace.SheetChipCommand, 0), FlareFace.ComposeField);
        Add("compose_promote", "02.jpg", FlareFace.SheetConfirmCommand, 0);
        Add("hire_sheet", "13.jpg", FlareFace.SheetConfirmCommand, 0);
        Add("reply_sheet", "02.jpg", FlareFace.OptionCommand, 1);
        Typed(Add("photo_picker", "13.jpg", FlareFace.PickerPhotoCommand, 2), FlareFace.PickerField);
        Add("notices_sales", "02.jpg", FlareFace.MarkAllCommand, 0);
        // Every pad shot is the d-pad's reach from the first target (UiPreviewRunner.TourOf): right first and then down, left and up, each stop joined to the one it was pressed from.
        Add("city_new", "13.jpg", FlareFace.NavCommand, 8);
        Add("compose_report", "11.jpg", FlareFace.SheetChipCommand, 2);
        Add("compose_moment", "02.jpg", FlareFace.SheetChipCommand, 1);
        Add("ad_sheet", "13.jpg", FlareFace.SheetConfirmCommand, 0);
        Typed(Add("edit_sheet", "02.jpg", FlareFace.SheetConfirmCommand, 0), FlareFace.BioField);
        // The same pages with the pad's focus on the nav entry the page stands on, as it is after a press on it: the walk then starts by right, which goes into the page.
        AddFromNav("home", "13.jpg", FlareFace.Home);
        AddFromNav("explore", "02.jpg", FlareFace.Explore);
        AddFromNav("hire", "02.jpg", FlareFace.Hire);
        AddFromNav("studio", "13.jpg", FlareFace.Studio);
        // The wide languages, where a label is longest: the hire table's header, the sheet's chips and the city page (the owner's bar: no word cut in any language).
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            AddIn(lang, "hire", "02.jpg", FlareFace.ButtonCommand, 0);
            AddIn(lang, "compose_report", "11.jpg", FlareFace.SheetChipCommand, 2);
            AddIn(lang, "city_new", "13.jpg", FlareFace.NavCommand, 8);
            AddIn(lang, "home", "13.jpg", FlareFace.LikeCommand, 0);
            AddIn(lang, "studio", "13.jpg", FlareFace.RowCommand, 1);
            AddIn(lang, "business", "02.jpg", FlareFace.ButtonCommand, 0);
            AddIn(lang, "profile", "13.jpg", FlareFace.TabCommand, 1);
            AddIn(lang, "notices_sales", "02.jpg", FlareFace.MarkAllCommand, 0);
            AddIn(lang, "hire_sheet", "13.jpg", FlareFace.SheetConfirmCommand, 0);
            AddIn(lang, "ad_sheet", "13.jpg", FlareFace.SheetConfirmCommand, 0);
            AddIn(lang, "compose_moment", "02.jpg", FlareFace.SheetChipCommand, 1);
        }

        Screen AddIn(string lang, string state, string backdrop, int hover, int arg)
        {
            UiWords words = DialogWords(lang);
            var made = new Screen
            {
                Name = "flare_" + state + Suffix(lang),
                AppName = "Flare",
                Icon = UiIcon.Fire,
                Backdrop = backdrop,
                HoverCommand = hover,
                HoverArg = arg,
                Tour = true,   // the pad shot is every target the d-pad reaches from the first, right then down (UiPreviewRunner.TourOf)
                Lay = (s, f, d, k) => UiSamples.Flare(s, f, d, k, words, state)
            };
            screens.Add(made);
            return made;
        }

        Screen AddFromNav(string state, string backdrop, int page)
        {
            Screen screen = Add(state, backdrop, FlareFace.NavCommand, page);
            screen.Name += "_fromnav";
            screen.FocusCommand = FlareFace.NavCommand;
            screen.FocusArg = page;
            return screen;
        }

        Screen Add(string state, string backdrop, int hover, int arg)
        {
            var screen = new Screen
            {
                Name = "flare_" + state,
                AppName = "Flare",
                Icon = UiIcon.Fire,
                Backdrop = backdrop,
                HoverCommand = hover,
                HoverArg = arg,
                Tour = true,   // the pad shot is every target the d-pad reaches from the first, right then down (UiPreviewRunner.TourOf)
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
