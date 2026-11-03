using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// Three bugs from the owner's play of the test build 85bb1e46a (3 Nov 2026), checked here.
// "After I open and close the map, the phone is hiding the settings, money notifications in the HUD": the HUD's canvas and
// the kit phone's stood at one sorting order, and two canvases of one order are drawn in the order they were last switched
// on. The HUD stood up after the phone, so it was drawn over it, until the map switched the game's phone canvas off and on
// alone (SmartphoneUI, onCityMapToggle) and the phone, folded down behind the bar, came out over the bar's bell, money and
// gear. Now the HUD keeps one order over the phone (UiHostRules.OverPhone, HudLive.Look), and nothing of the phone is drawn
// or pressed below the bar's top while the bar shows (UiPhone.Below, PhoneKit.Size).
// "The fast forward in the hud is disabled. It is not pressable": the square was a picture, dim unless it was on. Now it
// presses the game's own fast forward (the activity panel's switch) while the game offers one, and says when it does.
// Pressing the HUD's help threw "Collection was modified" inside the game's help: the Staff and roles lists were put back
// into the help's categories from its page event, inside its own walk of them (HelpSystem.OpenLink). Now a frame later.
internal static partial class Program
{
    private static void PhoneMap()
    {
        PhoneOrderChecks();
        PhoneClipChecks();
        FastForwardChecks();
        HelpPressChecks();
        Check(FixLineEverywhere("the phone no longer covers the bell, money and settings", "o celular não cobre mais o sino, o dinheiro e as configurações",
                                "手机不再遮挡HUD底栏上的铃铛、金钱和设置", "verdeckt das Handy nicht mehr die Glocke, das Geld und die Einstellungen",
                                "le téléphone ne recouvre plus la cloche", "el teléfono ya no tapa la campana", "телефон больше не закрывает колокольчик",
                                "bedekt de telefoon niet meer de bel", "telefon, HUD çubuğundaki zili"),
            "phone after the map: What's new describes the fix in each of the nine languages");
        Check(WhatsNewHas(LocaleFile("pl"), "Naprawiono: po mapie miasta telefon nie zakrywa już dzwonka") && WhatsNewHas(LocaleFile("ko"), "수정: 도시 지도 이후 휴대폰이"),
            "phone after the map: What's new describes the fix in Polish and Korean");
        int fast = 0, help = 0;
        foreach (string lang in ModLanguages.All)
        {
            fast += WhatsNewHas(LocaleFile(lang), FastWord(lang)) ? 1 : 0;
            UiWords t = UiWords(lang);
            help += t != null && !string.IsNullOrEmpty(t("reallife_hud_fast_why", null)) && t("reallife_hud_fast_why", null) != "reallife_hud_fast_why" ? 1 : 0;
        }
        Check(fast == ModLanguages.All.Length && help == ModLanguages.All.Length,
            "fast forward: What's new describes the fix (" + fast + ") and the square's notice is written (" + help + ") in each of the " + ModLanguages.All.Length + " languages");
    }

    // A word of each language's fast forward line in What's new.
    private static string FastWord(string lang)
    {
        switch (lang)
        {
            case "pt": return "Corrigido: o avanço rápido da barra do HUD funciona";
            case "zh-cn": return "修复：HUD底栏上的快进现在可以用了";
            case "de": return "Behoben: Der Schnellvorlauf in der Leiste des HUD funktioniert";
            case "pl": return "Naprawiono: przewijanie do przodu na pasku HUD działa";
            case "ko": return "수정: HUD 바의 빨리 감기가 작동합니다";
            case "en": return "Fixed: the fast forward on the HUD's bar works";
            default: return "HUD";
        }
    }

    // Canvases of one order drawn in the order they were last switched on: after each window the owner named, opened and
    // closed (the map, the game's menu, its options, a blueprint's preview), the HUD's bar is drawn over the kit phone.
    private static void PhoneOrderChecks()
    {
        string wrong = null;
        int runs = 0;
        foreach (int hudOwn in new[] { -1, 0, 1, 5 })   // the game's HUD's order against the game's phone's (0)
        {
            foreach (string window in new[] { "map", "menu", "options", "preview" })
            {
                runs++;
                if (!HudOverPhone(hudOwn, window, true) && wrong == null)
                {
                    wrong = window + " with the game's HUD at " + hudOwn;
                }
            }
        }
        Check(wrong == null && runs == 16, "phone after the map: after the map, the menu, the options and a preview the HUD's bar is drawn over the kit phone, whatever the canvases' orders ("
                                           + (wrong ?? runs + " runs") + ")");
        // The rule must be able to fail: the HUD at its own order, as it was, the phone over the bar after the map.
        Check(!HudOverPhone(0, "map", false) && HudOverPhone(0, "menu", false), "phone after the map: the HUD at its own order, as it was, is found under the phone after the map only");
        Check(UiHostRules.OverPhone(4, int.MinValue) == 4 && UiHostRules.OverPhone(4, 2) == 4 && UiHostRules.OverPhone(4, 4) == 5 && UiHostRules.OverPhone(4, 9) == 10,
            "phone after the map: the HUD keeps its own order over a lower phone, and stands one over a phone of its order or higher");
        string live = ModSource(@"Ui\HudScreens\HudLive.cs"), host = ModSource(@"Ui\Hud\HudHost.cs");
        Check(MemberText(live, "private static void Look(HudHost host, UIs uis)").Contains("host.KeepOver(PhoneKit.Order);")
              && MemberText(host, "public void KeepOver(int phone)").Contains("UiHostRules.OverPhone(_order, phone)"),
            "phone after the map: the HUD keeps over the phone's canvas at each look (HudLive.Look, HudHost.KeepOver)");
    }

    // One window opened and closed with the game's switching, then the canvases drawn: true when the HUD is over the phone.
    private static bool HudOverPhone(int hudOwn, string window, bool keepOver)
    {
        const int GamePhone = 0;
        int phone = GamePhone + 1;   // PhoneKit.Sort: one over the game's phone
        int seq = 0, phoneOn = ++seq, hudOn = ++seq;   // the phone is drawn at the city's start, the HUD stands up after it
        int hud = keepOver ? UiHostRules.OverPhone(UiHostRules.Above(hudOwn), phone) : UiHostRules.Above(hudOwn);   // HudLive.Look, four times a second
        switch (window)
        {
            case "map":   // SmartphoneUI: the game's phone canvas off with the map and on again, the top bar left as it is
                phoneOn = ++seq;
                break;
            default:   // UIs.HideUiMiniMenu, the options, BuildingPreview: the top bar and the phone off, both on again in one call
                phoneOn = ++seq;
                hudOn = ++seq;   // the HUD shows again at its next look, after the top bar does
                break;
        }
        return hud > phone || (hud == phone && hudOn > phoneOn);
    }

    // The phone's place and its cut, by the kit's own sums, open, folding, folded and hovered, the bar shown and hidden, and
    // a window that hid both showing the phone before the bar: what is drawn of it never comes below the bar's top.
    private static void PhoneClipChecks()
    {
        const float Height = 564f, Foot = 8f;   // px: the kit's device, and the game's container's open foot
        float lift = UiFrame.BandHeight + UiTokens.S2;
        string wrong = null;
        int cases = 0;
        foreach (float travel in new[] { 0f, -120f, -300f, -482f, -472f, 15f })   // open, folding, folded, hovered, a container above its place
        {
            cases++;
            float raise = 0f;
            for (int look = 0; look < 3; look++)
            {
                raise = UiPhone.Raise(lift, Foot + travel + raise, raise, travel);
            }
            float foot = Foot + travel + raise, cut = UiPhone.Below(lift, foot, foot + Height), drawn = foot + cut;
            bool right = drawn >= UiFrame.BandHeight - 0.01f && cut <= Height && (travel < 0f || cut == 0f) && Math.Abs(UiPhone.Below(0f, foot, foot + Height)) < 0.01f;
            if (!right && wrong == null)
            {
                wrong = "travel " + travel + ": foot " + foot + ", cut " + cut;
            }
        }
        Check(wrong == null, "phone after the map: open, folding, folded and hovered, nothing of the phone is drawn below the bar's top, and nothing is cut while it stands open or without a bar ("
                             + (wrong ?? cases + " places") + ")");
        Check(!(Foot - 300f + UiPhone.Raise(lift, Foot - 300f, 0f, -300f) >= UiFrame.BandHeight), "phone after the map: a folded phone without the cut is found over the bar");
        // The game's menu closed: the phone shows before the bar does, at no lift; the bar's lift places it at once.
        string kit = ModSource(@"Ui\Phone\PhoneKit.cs");
        string size = MemberText(kit, "private static void Size()");
        Check(size.Contains("UiPhone.Below(Lift, LiftCorners[0].y, LiftCorners[1].y) / scale") && size.Contains("_clip.padding = new Vector4(0f, below, 0f, 0f);")
              && kit.Contains("_clip = _root.AddComponent<RectMask2D>();") && MemberText(kit, "public static float Lift").Contains("Size();"),
            "phone after the map: the kit cuts the phone at the bar's top (a RectMask2D's padding, PhoneKit.Size) and a new lift places it at once (PhoneKit.Lift)");
    }

    // The three time squares on the HUD's bar: each a target; the pause and the play press the game's pause button's
    // method; the fast forward the game's own switch while the game offers it, else a notice of when it does.
    private static void FastForwardChecks()
    {
        string screen = ModSource(@"Ui\Hud\HudScreen.cs"), status = ModSource(@"Ui\Hud\HudStatusGame.cs");
        string fast = MemberText(screen, "private static bool Fast()"), offered = MemberText(status, "public static bool FastOffered(UIs uis)");
        Check(MemberText(screen, "public bool Press(int command, int arg)").Contains("return arg == UiDraw.FastSquare ? Fast() : Pause(arg == 0);")
              && fast.Contains("HudStatusGame.FastOffered(uis)") && fast.Contains("OnPlayerActionPressed(global::PlayerAction.SecondaryInteract)") && fast.Contains("\"reallife_hud_fast_why\""),
            "fast forward: its square presses the game's own switch as its key does (PlayerActivityUI.OnPlayerActionPressed), and says when the game offers it");
        Check(offered.Contains("PlayerActivity.PlayerActivityUI.IsPanelOpen") && offered.Contains("a.HasFastForward()") && offered.Contains("BuildingManager.IsInsideBuilding")
              && status.Contains("m.FastOffered = FastOffered(uis);") && status.Contains("(FastOffered(uis) ? 8 : 0)"),
            "fast forward: offered by the game's own rule (an activity that has it, inside a building), and the bar laid out again when that changes");
    }

    // The help's lists put back a frame after its page is said, never inside its walk of its categories.
    private static void HelpPressChecks()
    {
        string help = ModSource(@"Meta\StaffHelpGame.cs");
        string page = MemberText(help, "private static void OnPage(string slug)"), later = MemberText(help, "private static IEnumerator Later()");
        Check(!page.Contains("Inject();") && page.Contains("help.StartCoroutine(Guard.Routine(Later()));") && later.IndexOf("yield return null;", StringComparison.Ordinal) >= 0
              && later.IndexOf("yield return null;", StringComparison.Ordinal) < later.IndexOf("Inject();", StringComparison.Ordinal),
            "help: the Staff and roles lists go back into the game's help a frame after its page event, never inside HelpSystem.OpenLink's walk");
        Check(!MemberText(help.Replace("help.StartCoroutine(Guard.Routine(Later()));", "Inject();"), "private static void OnPage(string slug)").Contains("Guard.Routine(Later())"),
            "help: the page event as it was (the lists put back inside the walk) is found");
    }
}
