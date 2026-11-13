using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// A player's bug thread, "Mobile interface issues" (13 Nov 2026), its first report, checked here.
//
// The phone. "If I put my phone away, I can't open it again because of the new interface": the game saves the phone's fold and
// has no key for it, a folded phone shows only its head at the foot of the screen, and the HUD's bar stands there. The kit's
// own phone lifts its head over the bar and answers the Phone key (1.0.9). But another mod that pages the phone's apps (Aluna's
// Phone Pages: the log says "the RLO phone stands down to the classic phone for this session") has the kit give the classic
// phone back, and the classic phone had no way: its head stood under the bar, and nothing read a key. Now the bar has a phone
// button in its pill of small buttons while the classic phone is in play, the Phone key folds and unfolds the classic phone,
// the first sight of a folded phone is named once in a notice with the key, and a press that does nothing says why in Player.log
// (Ui/Phone/PhoneReach.cs, PhoneReachRules.cs).
//
internal static partial class Program
{
    private static void PhoneReach()
    {
        PhoneReachRuleChecks();
        PhoneReachSourceChecks();
    }

    // ------------------------------------------------------------------------------------------------- the phone

    private static void PhoneReachRuleChecks()
    {
        Check(PhoneReachRules.Offer(true, false) && !PhoneReachRules.Offer(true, true) && !PhoneReachRules.Offer(false, false) && !PhoneReachRules.Offer(false, true),
            "phone reach: the bar's phone button shows while the game shows its classic phone, never over the kit's own phone, never for a phone the game has hidden");
        Check(PhoneReachRules.Mine(false) && !PhoneReachRules.Mine(true),
            "phone reach: the Phone key is the classic phone's only while the kit's phone is not showing (the kit's phone answers it itself)");
        Check(PhoneReachRules.Remind(false, true, true) && !PhoneReachRules.Remind(true, true, true) && !PhoneReachRules.Remind(false, false, true) && !PhoneReachRules.Remind(false, true, false),
            "phone reach: the hint is said once, only for a folded phone the game shows");
        Check(PhoneReachRules.Why(false, true, true, true) == PhoneWhy.NoCity && PhoneReachRules.Why(true, false, true, true) == PhoneWhy.NoPhone
              && PhoneReachRules.Why(true, true, false, true) == PhoneWhy.NoPhone && PhoneReachRules.Why(true, true, true, false) == PhoneWhy.Hidden
              && PhoneReachRules.Why(true, true, true, true) == PhoneWhy.None,
            "phone reach: the cause a press cannot move the phone is told in order: no city, no phone, hidden by the game, none");
        bool words = true;
        foreach (PhoneWhy why in new[] { PhoneWhy.NoCity, PhoneWhy.NoPhone, PhoneWhy.Hidden, PhoneWhy.Refused })
        {
            string line = PhoneReachRules.Words(why, "the full menu is open", "F11");
            words &= line != null && line.StartsWith("UI: the phone key (F11)", StringComparison.Ordinal) && line.EndsWith(".", StringComparison.Ordinal);
        }
        words &= PhoneReachRules.Words(PhoneWhy.None, null, "F11") == null && PhoneReachRules.Words(PhoneWhy.Hidden, "the full menu is open", "F11").Contains("the full menu is open");
        Check(words, "phone reach: each cause has its line for Player.log, naming the key, and the hidden one names what hid the phone");
        Check(PhoneReachRules.Held(true, false, false, "F11").Contains("being chosen") && PhoneReachRules.Held(false, true, false, "F11").Contains("holds the keyboard")
              && PhoneReachRules.Held(false, false, true, "F11").Contains("text field") && PhoneReachRules.Held(false, false, false, "F11") == null,
            "phone reach: a key the keys' own check refused says why (a key being chosen, a panel of the mod, a text field), and says nothing when it was only the modifier");
    }

    private static void PhoneReachSourceChecks()
    {
        string reach = ModSource(@"Ui\Phone\PhoneReach.cs"), kit = ModSource(@"Ui\Phone\PhoneKit.cs"), director = ModSource(@"Ui\UiDirector.cs"), top = ModSource(@"Ui\HudScreens\HudTopGame.cs");
        string frame = MemberText(reach, "internal static void Frame()"), press = MemberText(reach, "public static bool Press()"), fold = MemberText(reach, "private static void Fold(");
        Check(frame.Contains("PhoneReachRules.Mine(PhoneKit.Showing)") && frame.Contains("Keys.Down(KeyAction.Phone)") && frame.Contains("Held();"),
            "phone reach: the key reads the Phone binding as the kit's phone does, leaves it to the kit's phone while that shows, and says why when the keys refused it");
        Check(fold.Contains("UiWidgets.Hand(collapse)") && fold.Contains("window.ChangeState(!window.IsCollapsed)") && press.Contains("window.IsCollapsed != folded") && press.Contains("PhoneWhy.Refused"),
            "phone reach: the game's own fold button is pressed (its listener keeps the fold in the save), the window's own change stands in, and a fold that did not move says so");
        Check(reach.Contains("class PhoneReachWatch : MonoBehaviour") && reach.Split(new[] { "void Update()" }, StringSplitOptions.None).Length == 2 && !reach.Contains("Find(") && !reach.Contains("FindObject"),
            "phone reach: one watch on the director's object, one key test a frame, nothing searched");
        string slow = MemberText(director, "private void Slow()");
        Check(slow.Contains("Quietly(PhoneReach.Ensure);") && slow.IndexOf("Quietly(PhoneReach.Ensure);", StringComparison.Ordinal) < slow.IndexOf("InstallOne(kit);", StringComparison.Ordinal)
              && slow.Contains("PhoneReach.Stop();") && !MemberText(director, "private static void RemoveOne(int kit, string why)").Contains("PhoneReach"),
            "phone reach: the key stands while the interface is on and is let go only when it goes off, never with the phone kit, so a fault of that kit is when it works");
        Check(top.Contains("Phone = 4") && top.Contains("new UiIcon[5]") && top.Contains("new int[5]") && top.Contains("PhoneReach.Look(out bool phoneOpen)") && top.Contains("Tool(Phone, UiIcon.Phone, phoneOpen, false);")
              && top.Contains("case Phone:") && top.Contains("return PhoneReach.Press();") && top.Contains("return PhoneReach.Words();"),
            "phone reach: the bar's pill has the phone button, pressing the reach's fold, with the key in its hover words");
        string standDown = MemberText(kit, "private static void StandDown(string why)");
        Check(standDown.Contains("Keys.Label(KeyAction.Phone)") && standDown.Contains("phone button in the HUD's bar"),
            "phone reach: the line that says the kit's phone stood down names the key and the button that fold the classic phone meanwhile");
        Check(kit.Contains("PhoneReach.Refused();") && MemberText(kit, "public void Collapse()").Contains("_window.ChangeState(!_window.IsCollapsed)"),
            "phone reach: the kit's own phone says in Player.log when the key moved nothing, and its head always has an answer");
        Dictionary<string, string> en = LocaleFile("en");
        en.TryGetValue("reallife_phone_tool_note", out string note);
        en.TryGetValue("reallife_phone_put_away", out string away);
        Check(note != null && note.Contains("{key}") && away != null && away.StartsWith("Your phone is put away. Press {key} or the phone button", StringComparison.Ordinal) && away.Length <= 240
              && !note.Contains("!") && !away.Contains("!") && !away.Contains("—") && !away.Contains("–"),
            "phone reach: the hover words and the notice are written, name the key as bound now, and the notice is under 240 characters");
        string screens = ModSource(@"Ui\UiScreens.Phone.cs");
        Check(screens.Contains("\"reallife_phone_tool_note\"") && screens.Contains("\"reallife_phone_put_away\""), "phone reach: both texts are in the phone screen's key list");
        Check(ModSource(@"Core\Keys.cs").Contains("public static bool Capturing => _capturing;"), "phone reach: the keys say when one is being chosen, for the line that tells why a key did nothing");
    }
}
