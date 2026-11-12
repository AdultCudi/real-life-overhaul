using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using RealLifeOverhaul;

// Typing in a text field never triggers a shortcut (Core/TypingNow, Core/TypingRules). The owner, 3 Nov 2026: typing in the
// box for something of your own to say opened BizMan on B. The cause: the kit's hidden text field, the one that carries
// the keyboard for its screens, stood on the default layer, and the game holds its shortcuts only while the selected object
// stands on the UI layer (GameManager.HasInputSelected). Two rules now hold it: every field the mod makes is put on the
// game's layer, and every source of the mod that reads keys a player types with asks TypingNow.Now. This reads every source.
internal static partial class Program
{
    private static void TypingKeys()
    {
        string scripts = IOPath.GetFullPath(IOPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string unguarded = "none", offLayer = "none";
        int files = 0, readers = 0;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string relative = IOPath.GetRelativePath(scripts, path).Replace('\\', '/');
            string text = File.ReadAllText(path);
            files++;
            readers += TypingRules.ReadsKeys(text) ? 1 : 0;
            if (TypingRules.Unguarded(relative, text))
            {
                unguarded = relative;
            }
            if (TypingRules.FieldOffLayer(text))
            {
                offLayer = relative;
            }
        }
        Check(files > 500 && readers >= 8, "typing: the scan reads the mod's whole source and finds its readers of typed keys (" + files + " files, " + readers + " readers)");
        Check(unguarded == "none", "typing: every source that reads keys a player types with asks TypingNow first (" + unguarded + ")");
        Check(offLayer == "none", "typing: every text field the mod makes stands on the game's UI layer, so the game holds its shortcuts (" + offLayer + ")");

        // The scan must be able to fail: a reader that never asks, a field left on the default layer, and the real sources with the rule taken out.
        Check(TypingRules.Unguarded("X/Open.cs", "if (Input.GetKeyDown(KeyCode.B)) { Open(); }"), "typing: a source that reads the B key and never asks is found");
        Check(TypingRules.Unguarded("X/Walk.cs", "var move = PlayerAction.Move.Vector();"), "typing: a source that reads the game's Move action and never asks is found");
        Check(TypingRules.Unguarded("X/Pick.cs", "if (Keyboard.current[Key.Digit1].wasPressedThisFrame) { Pick(); }"), "typing: a source that reads the number keys through the keyboard and never asks is found");
        Check(!TypingRules.Unguarded("X/Open.cs", "if (!TypingNow.Now && Input.GetKeyDown(KeyCode.B)) { Open(); }"), "typing: the same source with the rule is passed");
        Check(!TypingRules.Unguarded("X/Esc.cs", "if (PlayerAction.Cancel.Pressed() || PlayerAction.Confirm.Pressed() || Input.GetKey(KeyCode.LeftShift)) { }")
              && !TypingRules.Unguarded("X/Note.cs", "// Input.GetKeyDown(KeyCode.B) is read nowhere"), "typing: Esc, Enter, a modifier and a comment are not shortcuts");
        Check(TypingRules.FieldOffLayer("var f = go.AddComponent<TMP_InputField>();")
              && !TypingRules.FieldOffLayer("var f = go.AddComponent<TMP_InputField>(); TypingNow.OnGameLayer(go);"), "typing: a field left off the game's layer is found");
        string worst = "none";
        foreach (string relative in new[] { "Core/Keys.cs", "Ui/UiGame.cs", "Ui/Phone/PhoneKit.cs", "Photos/GalleryApp.Viewer.cs", "Meals/MealsGame.cs", "Economy/Dealer/Floor/ShowroomView.cs", "Transport/BusRider.Doors.cs", "Transport/BusRidePanel.cs" })
        {
            string text = ModSource(relative.Replace('/', '\\'));
            if (text.Length == 0 || !TypingRules.ReadsKeys(text) || TypingRules.Unguarded(relative, text) || !TypingRules.Unguarded(relative, text.Replace("TypingNow.", "TypingGone.")))
            {
                worst = relative;
            }
        }
        Check(worst == "none", "typing: the readers named by the bug (shortcuts, the kit, the phone, the gallery, meals, the showroom, the bus) ask, and are found when they stop (" + worst + ")");
        string host = ModSource(@"Ui\UiHost.cs");
        Check(TypingRules.FieldOffLayer(host.Replace("TypingNow.OnGameLayer(", "Gone(")) && host.Contains("TypingNow.OnGameLayer(r.gameObject);"),
            "typing: the kit's hidden text field is put on the game's layer when it is made (the cause of the bug)");

        // The shared rule's shape: asked once a frame, the key tested first, the arrows of a panel and photo mode held too.
        string rule = ModSource(@"Core\TypingNow.cs");
        Check(rule.Contains("Time.frameCount") && rule.Contains("isFocused") && rule.Contains("TryGetComponent(out _tmp)") && rule.Contains("ReferenceEquals(on, _seen)")
              && !rule.Contains("GetComponent<") && !rule.Contains("new "), "typing: the rule reads once a frame, finds a field once for each object selected and allocates nothing");
        string keys = ModSource(@"Core\Keys.cs");
        Check(MemberText(keys, "public static bool Down(KeyAction action)").Contains("Input.GetKeyDown(binding.Key) && Modified(binding) && !TypingNow.Now")
              && MemberText(keys, "public static bool Held(KeyAction action)").Contains("Input.GetKey(binding.Key) && Modified(binding) && !TypingNow.Now")
              && MemberText(keys, "public static int PanelScroll()").Contains("TypingNow.Now")
              && MemberText(keys, "private static float Axis(").Contains("TypingNow.Now") && MemberText(keys, "private static bool Down(KeyCode key)").Contains("!TypingNow.Now"),
            "typing: every key action, the panel's arrows and photo mode's keys wait while a field has the keyboard, the key tested before the field");
        string game = ModSource(@"Ui\UiGame.cs");
        Check(game.Contains("bool typing = TypingNow.Now;") && game.Contains("state.Back = PlayerAction.Cancel.Pressed();") && game.Contains("state.Confirm = PlayerAction.Confirm.Pressed();")
              && game.Contains("state.TabNext = !typing &&") && game.Contains("state.Alt = !typing &&") && game.Contains("!typing && i < 9"),
            "typing: the kit's steps, tabs, choices and number keys wait while a field has the keyboard, and Esc and Enter still reach the screen");

        // What's new: in every language, in any row of it.
        Check(FixLineEverywhere("typing in a text field no longer triggers shortcuts", "digitar em um campo de texto não aciona mais atalhos",
                                "在文本框中打字不再触发快捷键", "Tippen in einem Textfeld löst keine Tastenkürzel mehr aus",
                                "écrire dans un champ de texte ne déclenche plus de raccourcis", "escribir en un campo de texto ya no activa atajos",
                                "набор текста в текстовом поле больше не вызывает горячие клавиши", "typen in een tekstvak activeert geen sneltoetsen meer",
                                "bir metin kutusuna yazmak artık kısayolları tetiklemiyor"),
            "typing: What's new describes the fix in each of the nine languages");
        Check(WhatsNewHas(LocaleFile("pl"), "Naprawiono: pisanie w polu tekstowym nie uruchamia już skrótów")
              && WhatsNewHas(LocaleFile("ko"), "수정: 텍스트 입력란에 글자를 입력해도 더 이상 단축키가 작동하지 않습니다"),
            "typing: What's new describes the fix in Polish and Korean");
    }
}
