using System;
using System.Linq;
using RealLifeOverhaul.Photos;
using RealLifeOverhaul.Ui;

// The interface parity fixes for what the first audits did not reach (13 Nov): the tutorial's arrow at a row of a list, the glyphs of the activity
// panel, the Gallery's storage line, the result popups of a delivery shift and a food delivery, and the ledger's words. Layouts are laid out from
// hand-made models; what reads the game is checked in its source.
internal static partial class Program
{
    private static void UiCoverFixChecks()
    {
        UiCoverTutorial();
        UiCoverIcons();
        UiCoverGallery();
        UiCoverJobSummary();
    }

    private static void UiCoverTutorial()
    {
        string aim = ModSource(@"Ui\Overlays\TutorialAimLive.cs"), scroll = ModSource(@"Ui\Overlays\TutorialAimScroll.cs");
        string tick = MemberText(aim, "public static void Tick(float now)");
        Check(tick.Contains("TutorialAimScroll.Reveal(data, target)") && tick.Contains("TutorialAimScroll.Lead(target, label)") && tick.Contains("TutorialAimScroll.Settled(target, label)")
              && MemberText(aim, "public static void Release()").Contains("TutorialAimScroll.Reset()"),
            "cover: an arrow at a hidden row has the game's list scrolled to it, the kit's list led to it once, and forgets the row when the arrow goes");
        Check(scroll.Contains("data.ShouldBeEnabled()") && scroll.Contains("view.Wheeled(clip, notches)") && scroll.Contains("MostSteps = 120") && scroll.Contains("StepsPerLook = 6")
              && scroll.Contains("if (_done || _spent >= MostSteps)"),
            "cover: the kit's list is scrolled by its own wheel, only while the game's own conditions would show the arrow, a few steps a look and never more than a hundred and twenty for one row");
        Check(MemberText(scroll, "private static void Show(ScrollRect scroll, RectTransform target)").Contains("verticalNormalizedPosition")
              && MemberText(scroll, "private static bool Inside(ScrollRect scroll, RectTransform target)").Contains("CalculateRelativeRectTransformBounds(view, target)")
              && !scroll.Contains("GetComponentInParent"),
            "cover: the game's list is scrolled as the game scrolls to a map filter, and the row's scroll view is found by walking the parents, not by a blind search");
        string find = MemberText(aim, "private static bool Find(string label, out Vector2 screen)");
        Check(find.Contains("scene.NodeBox(item.Node") && find.Contains("scene.Clips[node.Clip].H") && !find.Contains("node.Clip >= 0) {\n                        continue;"),
            "cover: a row of a scrolled list is found where it stands in view, and one scrolled out of its view is not");
    }

    private static void UiCoverIcons()
    {
        string read = ModSource(@"Ui\Activity\ActivityRead.cs");
        string icon = MemberText(read, "public static UiIcon Icon(IPlayerActivity a)");
        Check(icon.Contains("a is EntertainActivity") && icon.Contains("UiIcon.Waves") && icon.Contains("UiIcon.Golf") && icon.Contains("UiIcon.TennisBall") && icon.Contains("UiIcon.Ticket")
              && icon.Contains("Has(name, \"Hygiene\")") && !icon.Contains("Has(name, \"Paid\")"),
            "cover: entertaining, swimming, hygiene, golf, tennis and a paid attraction each have a glyph that says what they are, and a paid attraction is no longer a desk's briefcase");
        string entertain = MemberText(read, "private static UiIcon Entertain(IPlayerActivity a)");
        Check(entertain.Contains("UiIcon.Monitor") && entertain.Contains("UiIcon.MusicNotes") && entertain.Contains("UiIcon.Newspaper") && entertain.Contains("UiIcon.GameController")
              && entertain.Contains("GameContract.ActivityEntertainDevice"),
            "cover: entertaining is the games pad, the television, the decks or a book by what the device does");
        string script = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\UiAssets\scripts\icons.py")));
        string data = ModSource(@"Ui\UiIconPaths.Data.cs");
        bool set = true;
        foreach (string name in new[] { "Waves", "Ticket", "Golf", "TennisBall" })
        {
            UiIcon id = (UiIcon)Enum.Parse(typeof(UiIcon), name);
            set &= script.Contains("(\"" + name + "\", ") && script.Contains("\"" + name + "\": \"") && data.Contains("case UiIcon." + name + ": return filled ? " + name + "Fill : " + name + ";") && id != UiIcon.None && id < UiIcon.Count;
        }
        Check(set, "cover: each new glyph is pinned in the icon script with its fill weight and has its outlines in the generated data");
    }

    private static void UiCoverGallery()
    {
        var model = new UiAlbumModel();
        GalleryAlbumFill.Fill(model, CgPhotos(40), new GalleryQuery(), null, null, null, 40, 5, 4, 8, "photo:", UiSamples.SampleTexts((k, e) => e), true);
        var plain = new UiScene();
        plain.Begin("gallery", 1920f, 1080f, true);
        UiAlbum.Lay(plain, UiAt.Screen, model, 100f, 100f, 1200f, 700f, UiDefaultKeys.Keyboard);
        model.StorageLabel = "40 of 300 photos kept for this save, on this computer";
        var kept = new UiScene();
        kept.Begin("gallery", 1920f, 1080f, true);
        UiAlbum.Lay(kept, UiAt.Screen, model, 100f, 100f, 1200f, 700f, UiDefaultKeys.Keyboard);
        float footY = UiTokens.Chip, plainClip = plain.Clips[0].H, keptClip = kept.Clips[0].H;
        Check(Says(kept, model.StorageLabel, UiRole.Muted) && !Says(plain, model.StorageLabel, UiRole.Muted) && keptClip < plainClip && keptClip > plainClip - footY - UiTokens.S4 && TextBelowClip(kept, model.StorageLabel),
            "cover: the Gallery's storage line stands under the grid in the muted colour, and the grid gives it the room");
        model.StorageLabel = "The Gallery is full (300 photos). Delete some to take more.";
        model.StorageFull = true;
        var full = new UiScene();
        full.Begin("gallery", 1920f, 1080f, true);
        UiAlbum.Lay(full, UiAt.Screen, model, 100f, 100f, 1200f, 700f, UiDefaultKeys.Keyboard);
        Check(Says(full, model.StorageLabel, UiRole.Accent), "cover: a full Gallery's line is in the accent, as the old page's footer was");
        string screen = ModSource(@"Ui\Life\GalleryScreen.cs");
        string fill = MemberText(screen, "private void FillAlbum(PhotosDirector director)");
        Check(fill.Contains("PhotoStoreRules.Full(director.Count)") && fill.Contains("reallife_gallery_full_line") && fill.Contains("reallife_gallery_storage") && fill.Contains("PhotoStoreRules.Cap"),
            "cover: the Gallery reads the count and the cap from the photo store, as the old page did");
        string keys = ModSource(@"Ui\UiScreens.Life.cs");
        Check(keys.Contains("\"reallife_gallery_storage\"") && keys.Contains("\"reallife_gallery_full_line\""), "cover: the storage line's two texts are among the Gallery screen's keys");
    }

    private static bool Says(UiScene s, string words, UiRole ink)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && s.Items[i].Text == words && s.Items[i].Ink == ink)
            {
                return true;
            }
        }
        return false;
    }

    private static bool TextBelowClip(UiScene s, string words)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && s.Items[i].Text == words)
            {
                return s.Items[i].Y >= s.Clips[0].Y + s.Clips[0].H;
            }
        }
        return false;
    }

    private static void UiCoverJobSummary()
    {
        string job = ModSource(@"Ui\HudScreens\HudJobSummary.cs"), notes = ModSource(@"Ui\HudScreens\HudNotes.cs"), contract = ModSource(@"Core\GameContract.JobSummary.cs");
        Check(job.Contains("UiWidgets.Calling(job, \"OnClickClose\")") && job.Contains("UiWidgets.Press(_close)") && job.Contains("Held.Add(job.transform)") && job.Contains("GiveUpAfter = 3f")
              && job.Contains("food_delivery_summary") && job.Contains("delivery_job_summary") && job.Contains("row.alpha < Seen"),
            "cover: the result popup is a card made from the game's own rows as each fades in, closed by the popup's own button, and given back to the game if it cannot be read");
        Check(contract.Contains("Optional(Field(JobSummaryPart") && contract.Contains("_activeJobSummary") && contract.Contains("canvasGroups"),
            "cover: the popup's contract members are optional, so a game without them leaves the popup as it draws it");
        string read = MemberText(notes, "private static int Read(ref int count)");
        Check(read.Contains("HudJobSummary.Look(_daily, out int jobSig)") && notes.Contains("HudJobSummary.Begin()") && notes.Contains("HudJobSummary.End()") && notes.Contains("return HudJobSummary.Press();"),
            "cover: the HUD's notices take the popup's card in, begin and end it with the notices, and press its button");
        string ledger = ModSource(@"Ui\Sweep\SweepLedger.cs");
        Check(!ledger.Contains("nothing in the kit opens this one") && ledger.Contains("the game turns that button off in a modded save"),
            "cover: the ledger says the kit presses the game's report button and the game turns it off in a modded save");
        Check(UiScreens.All.Any(d => d.Id == "hud_notices"), "cover: the notices screen is still registered");
    }
}
