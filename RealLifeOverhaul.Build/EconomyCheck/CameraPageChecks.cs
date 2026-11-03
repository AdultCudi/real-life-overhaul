using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Photos;
using RealLifeOverhaul.Ui;

// The Camera's page on the kit: its body never carries a {name} left unfilled and says what is true (the camera's refusal, a full
// gallery with its numbers, or the page's note when the camera is not refused); its Close button, its Back key and Esc all close the
// Camera app, and so does every page of the kit that draws a Back button; a press on the Camera app that is not refused leaves the
// kit's page (the camera's open waits a frame for the phone's menu, which the game opens just after it switches the app on); and a
// photo's time reads as the game's clock, "Day 2, 19:00", with no computer date in the kicker.
internal static partial class Program
{
    private static void CameraPageChecks()
    {
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(PhotoLocales, language + ".json"));
            string Say(string word) => words != null && words.TryGetValue(word, out string v) ? v : "<missing " + word + ">";
            var keys = new List<string> { null };
            foreach (CameraRefusal refusal in Enum.GetValues(typeof(CameraRefusal)))
            {
                keys.Add(CameraRules.RefusalKey(refusal));
            }
            string braces = "none", wrongBody = "none", missing = "none";
            foreach (int count in new[] { 0, 1, 12, PhotoStoreRules.Cap - 1, PhotoStoreRules.Cap })
            {
                foreach (string key in keys)
                {
                    var m = new UiDecisionModel();
                    CameraPage.Fill(m, count, key, count > 0 ? "Day 2, 19:00" : null, Say);
                    string where = language + ", " + count + " photos, " + (key ?? "not refused");
                    braces = m.Body.Contains("{") || m.Kicker.Contains("{") || m.Title.Contains("{") ? where : braces;
                    missing = m.Body.Contains("<missing") || m.Kicker.Contains("<missing") || m.Title.Contains("<missing") ? where : missing;
                    bool full = count >= PhotoStoreRules.Cap;
                    string want = full ? Say("reallife_ui_life_camera_full_body").Replace("{cap}", PhotoStoreRules.Cap.ToString()).Replace("{count}", count.ToString())
                        : Say(key ?? "reallife_phonecam_page_note");
                    wrongBody = m.Body != want ? where : wrongBody;
                }
            }
            Check(braces == "none", "camera page: the page's words never carry a {name} left unfilled, in " + language + " (" + braces + ")");
            Check(missing == "none", "camera page: every word the page uses is in " + language + " (" + missing + ")");
            Check(wrongBody == "none", "camera page: a full gallery says so with its numbers, a refused camera says why, and a camera not refused gets the page's note, never the full text, in "
                + language + " (" + wrongBody + ")");
        }

        // Every target the page draws that says Back or Close does it: the Esc key presses the Back-keyed target, the Close button is it.
        var page = new UiDecisionModel();
        CameraPage.Fill(page, 3, null, "Day 2, 19:00", word => word);
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Camera");
        LifePages.Refused(scene, page, frame, true, UiDefaultKeys.Keyboard);
        int backs = 0, trapped = 0;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            UiNode n = scene.Nodes[i];
            if (n.Key == UiAction.Back || n.Command == UiDraw.CloseCommand)
            {
                backs++;
                trapped += n.Command == UiDraw.CloseCommand || CameraPage.Answer(n.Command, n.Arg) == CameraAnswer.Close ? 0 : 1;
            }
        }
        Check(backs >= 1 && trapped == 0, "camera page: the Close button and the Back key are a target the page answers by closing the app (" + backs + " found, " + trapped + " answered by nothing)");
        Check(CameraPage.Answer(UiDecision.ButtonCommand, 0) == CameraAnswer.Close && CameraPage.Answer(UiDecision.ChoiceCommand, 0) == CameraAnswer.Gallery
              && CameraPage.Answer(UiDecision.ChoiceCommand, 1) == CameraAnswer.None && CameraPage.Key(UiAction.Alt) == CameraAnswer.Gallery && CameraPage.Key(UiAction.Back) == CameraAnswer.None,
            "camera page: the Close button closes, the choice opens the Gallery, nothing else answers");
        string screen = Read(@"Ui\Life\CameraScreen.cs");
        string app = Read(@"Photos\CameraApp.cs");
        Check(Regex.IsMatch(screen, @"public bool Back\(\) => Do\(CameraAnswer\.Close\);") && screen.Contains("Close?.Invoke();") && app.Contains("Close = () => RealLifeOverhaul.Ui.UiShell.CloseMenu()"),
            "camera page: Back closes the app through the full menu's own close, as the other apps' pages do");

        // A press on the Camera app that is not refused leaves the kit's page.
        string director = Read(@"Photos\PhotosDirector.cs");
        string put = Between(director, "private IEnumerator OpenWhenPutAway(", "CameraRefusal refusal = _mode");
        Check(put.IndexOf("yield return null;", StringComparison.Ordinal) >= 0 && put.IndexOf("yield return null;", StringComparison.Ordinal) < put.IndexOf("PhotosGame.CloseMenu();", StringComparison.Ordinal),
            "camera open: the phone is put away a frame after the open is asked, so the menu the game opens just after switching the app on is the one closed");
        string opened = Between(app, "private void Opened()", "private void Closed()");
        Check(opened.IndexOf("UiClone.Safe(Write)", StringComparison.Ordinal) >= 0 && opened.IndexOf("Ask(false)", StringComparison.Ordinal) > opened.IndexOf("UiClone.Safe(Write)", StringComparison.Ordinal)
              && opened.Contains("_refused == CameraRefusal.None"),
            "camera open: an app not refused asks for the camera, and a fault writing its old page cannot stop that");

        // The kit's pages that draw a Back button (a key of Back with no command of its own sends the template's button command) answer it:
        // the folder of each such page handles that command. Every runner UiScreens names is looked at.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        int looked = 0;
        string unanswered = "none";
        foreach (UiScreenDef def in UiScreens.All)
        {
            if (def.Runner == null)
            {
                continue;
            }
            foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (!Regex.IsMatch(File.ReadAllText(path), @"class " + def.Runner + @"\b"))
                {
                    continue;
                }
                looked++;
                string folder = System.IO.Path.GetDirectoryName(path);
                string all = string.Empty;
                foreach (string sibling in Directory.GetFiles(folder, "*.cs", folder == scripts ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories))
                {
                    all += File.ReadAllText(sibling);
                }
                bool draws = Regex.IsMatch(all, @"Key = UiAction\.Back\s*\}");   // a button of Back with no command of its own
                if (draws && !all.Contains("ButtonCommand"))
                {
                    unanswered = def.Id + " (" + def.Runner + ")";
                }
                break;
            }
        }
        Check(looked >= 30 && unanswered == "none", "kit pages: every page that draws a Back button answers it (" + looked + " runners looked at, " + unanswered + ")");
    }

    // The kicker: the game's day and its clock, no computer date.
    private static void CameraTakenWords()
    {
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(PhotoLocales, language + ".json"));
            string taken = words != null && words.TryGetValue("reallife_gallery_taken", out string v) ? v : string.Empty;
            Check(taken.Contains("{day}") && taken.Contains("{hour}") && !taken.Contains("{real}") && !taken.Contains("("),
                "photo time: a photo's time reads as the game's day and hour with no computer date, in " + language + " (" + taken + ")");
        }
        string text = Read(@"Photos\PhotosText.cs");
        Check(text.Contains("hour.GetFormattedTime(0f, false)") && !text.Contains("GetFormattedTime(0f, true)") && text.Contains("public static string TakenOn(")
              && Read(@"Ui\Life\GalleryScreen.cs").Contains("PhotosText.TakenOn(photo)"),
            "photo time: the hour is written as the game's clock writes it (19:00, or 07:00 PM), never a bare 19, and the Gallery's fact keeps the computer's date");
    }
}
