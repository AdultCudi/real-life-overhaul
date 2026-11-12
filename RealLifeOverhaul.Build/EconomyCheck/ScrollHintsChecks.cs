using System;
using RealLifeOverhaul.Ui;

// Three bugs from the owner's play of the wave 2 test build (3 Nov 2026), checked here.
// "I am unable to scroll in any interface": the engine hands the wheel to the first object under the pointer that takes it,
// and every target's face on a kit screen is a UiHit, which takes it. A face pointed at a target forgot the scroll region it
// stood in, so the wheel over any row or button of a list stopped there; only a gap between rows reached the list. Now a face
// is bound with its item's region and hands the wheel on (UiHit.Face, UiView.Slots). Wave 2 put most of the game's screens
// on the kit, so the dead wheel was met everywhere.
// "The phone is not closing": the kit's phone is raised inside the game's phone container to clear the HUD's bar, and the
// raise was measured from where the container stood now, so the game's fold raised it by as much as it took it down.
// Now the raise is measured where the container stands open (UiPhone.Raise), the same at every point of the fold.
// "The map, pause and menu are overlapping on the phone": the HUD's key chips stood at the bar's right end, where the phone
// stands. Now they keep their row and end a step left of the phone (UiHud.Chips, HudLive.PhoneBox).
internal static partial class Program
{
    private static void ScrollHints()
    {
        ScrollWheelChecks();
        PhoneFoldChecks();
        HintChipChecks();
        // What's new: in every language, in any row of it.
        Check(FixLineEverywhere("the mouse wheel scrolls the lists of the RLO interface again", "a roda do mouse volta a rolar as listas da interface do RLO",
                                "鼠标滚轮现在可以滚动 RLO 界面中的列表", "Das Mausrad scrollt die Listen der RLO-Oberfläche wieder",
                                "la molette de la souris fait de nouveau défiler les listes de l'interface RLO", "la rueda del ratón vuelve a desplazar las listas de la interfaz de RLO",
                                "колесо мыши снова прокручивает списки интерфейса RLO", "het scrollwiel scrolt de lijsten van de RLO-interface weer",
                                "fare tekerleği RLO arayüzündeki listeleri yeniden kaydırıyor"),
            "scroll: What's new describes the fix in each of the nine languages");
        Check(FixLineEverywhere("the phone closes again", "o celular volta a fechar", "手机现在可以正常关闭了", "Das Handy schließt sich wieder", "le téléphone se referme de nouveau",
                                "el teléfono vuelve a cerrarse", "телефон снова закрывается", "de telefoon sluit weer", "telefon yeniden kapanıyor"),
            "phone fold: What's new describes the fix in each of the nine languages");
        Check(FixLineEverywhere("the key hints over the bar", "as dicas de teclas sobre a barra", "条上方的按键提示", "Die Tastenhinweise über der Leiste", "les indications de touches au-dessus de la barre",
                                "las indicaciones de teclas sobre la barra", "подсказки клавиш над панелью", "de toetshints boven de balk", "çubuğun üzerindeki tuş ipuçları"),
            "hints: What's new describes the fix in each of the nine languages");
        Check(WhatsNewHas(LocaleFile("pl"), "Naprawiono: kółko myszy znów przewija listy interfejsu RLO") && WhatsNewHas(LocaleFile("pl"), "Naprawiono: telefon znów się zamyka")
              && WhatsNewHas(LocaleFile("pl"), "Naprawiono: podpowiedzi klawiszy nad paskiem") && WhatsNewHas(LocaleFile("ko"), "수정: 마우스 휠로 RLO 인터페이스의 목록을 다시 스크롤할 수 있습니다")
              && WhatsNewHas(LocaleFile("ko"), "수정: 휴대폰이 다시 닫힙니다") && WhatsNewHas(LocaleFile("ko"), "수정: 바 위의 키 안내"),
            "scroll and hints: What's new describes the three fixes in Polish and Korean");
    }

    // The wheel over a target in a list reaches the list.
    private static void ScrollWheelChecks()
    {
        string hit = ModSource(@"Ui\UiHit.cs"), slots = ModSource(@"Ui\UiView.Slots.cs");
        Check(WheelReachesList(hit, slots), "scroll: a target's face in a list is bound with the list's scroll region and hands the wheel on to it (UiHit.Face, UiView.Slots)");
        // The rule must be able to fail: the sources as they were before the fix, a face that forgets its region, and a wheel that waits on the target.
        string oldHit = hit.Replace("public void Face(UiView view, int node, int clip)", "public void Face(UiView view, int node)").Replace("_clip = view != null ? clip : -1;", "_clip = -1;");
        Check(!WheelReachesList(oldHit, slots.Replace("b.Hit.Face(face ? this : null, it.Node, it.Clip)", "b.Hit.Face(face ? this : null, it.Node)")),
            "scroll: the sources as they were (a face pointed at its target and at no region) are found");
        Check(!WheelReachesList(hit.Replace("_clip = view != null ? clip : -1;", "_clip = -1;"), slots), "scroll: a face that forgets its region is found");
        Check(!WheelReachesList(hit.Replace("_view == null || _clip < 0 || data == null", "_view == null || _clip < 0 || _node >= 0 || data == null"), slots),
            "scroll: a wheel that stops on a target's face is found");
        Check(!WheelReachesList(hit, slots.Replace("it.Node, it.Clip)", "it.Node, -1)")), "scroll: a face bound with no region is found");

        // Every registered screen: the rows and buttons inside a scroll region that holds more than it shows are faces bound
        // with that region, so the wheel over any of them turns it. Counted, so the case is known to be met.
        UiChrome.Status = () => UiSamples.Status;   // the masthead's money, day and time, as the interface kit's checks set them
        var s = new UiScene();
        UiWords en = UiWords("en");
        int screens = 0, faces = 0, wrong = 0;
        string first = "none";
        foreach (UiScreenCase c in UiCases())
        {
            c.Lay(s, UiFrame.FullMenu(16f / 9f, c.Title), true, UiDefaultKeys.Keyboard, en);
            bool counted = false;
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Node < 0 || it.Node >= s.NodeCount || s.Nodes[it.Node].Face != i || it.Clip < 0)
                {
                    continue;
                }
                if (it.Clip >= s.ClipCount)
                {
                    wrong++;
                    first = first == "none" ? c.Name : first;
                    continue;
                }
                if (s.Clips[it.Clip].ContentHeight > s.Clips[it.Clip].H + 0.5f)
                {
                    faces++;
                    if (!counted)
                    {
                        counted = true;
                        screens++;
                    }
                }
            }
        }
        Console.WriteLine("    scroll: " + screens + " screen cases with a list that holds more than it shows, " + faces + " targets in those lists, each handing the wheel on");
        Check(wrong == 0, "scroll: every target's face drawn in a scroll region names a region the screen has (" + first + ")");
        Check(screens >= 10 && faces >= 100, "scroll: the screens' lists that hold more than they show have targets in them, each handing the wheel on (" + screens + " screens, " + faces + " targets)");
    }

    // UiHit binds a face with the region its item stands in and tells the wheel to any region it holds; UiView.Slots passes the item's region.
    private static bool WheelReachesList(string hit, string slots)
    {
        string face = MemberText(hit, "public void Face(UiView view, int node, int clip)");
        string scroll = MemberText(hit, "public void OnScroll(PointerEventData data)");
        return face.Contains("_clip = view != null ? clip : -1;")
               && scroll.Contains("_view == null || _clip < 0 || data == null") && !scroll.Contains("_node")
               && scroll.Contains("_view.Wheeled?.Invoke(_clip, data.scrollDelta.y);")
               && slots.Contains("b.Hit.Face(face ? this : null, it.Node, it.Clip)");
    }

    // The kit's phone folds with the game's and stays clear of the HUD's bar while open.
    private static void PhoneFoldChecks()
    {
        const float Lift = 104f, Fold = 1145f / 2f;   // screen px: the bar's top and a step; the game's fold with the radio (1,145 units at 2 a px)
        // Open, from a foot of 40 px: raised by 64 px, and the raise holds once it is applied.
        float raise = UiPhone.Raise(Lift, 40f, 0f, 0f);
        Check(Math.Abs(raise - 64f) < 0.01f && Math.Abs(UiPhone.Raise(Lift, 40f + raise, raise, 0f) - raise) < 0.01f,
            "phone fold: open, the phone's foot is raised to the bar's top and stays there (" + raise + " px)");
        // Folding: every point of the game's fold keeps the same raise, so the phone goes down by all of it.
        bool same = true;
        for (float t = 0f; t <= Fold; t += Fold / 8f)
        {
            same &= Math.Abs(UiPhone.Raise(Lift, 40f + raise - t, raise, -t) - raise) < 0.01f;
        }
        Check(same, "phone fold: at every point of the game's fold the raise stays the same, so the phone folds down with the game's");
        // The rule as it was (measured where the container stands now): the fold raised it by as much as it took it down.
        Check(Math.Abs(OldRaise(Lift, 40f + raise - Fold, raise) - (raise + Fold)) < 0.01f && Math.Abs(UiPhone.Raise(Lift, 40f + raise - Fold, raise, -Fold) - (raise + Fold)) > 1f,
            "phone fold: the old measure undid the whole fold, and the new one does not");
        Check(UiPhone.Raise(0f, 40f, 0f, -Fold) == 0f && UiPhone.Raise(0f, -500f, 30f, 0f) == 0f, "phone fold: with no bar (the HUD off) the phone is never raised");
        Check(UiPhone.Raise(Lift, 200f, 0f, 0f) == 0f && UiPhone.Raise(Lift, 200f - Fold, 0f, -Fold) == 0f, "phone fold: a phone already clear of the bar is never raised, open or folded");
        // Another mod's container above its open place: measured where it stands, no more raise than the bar asks.
        Check(Math.Abs(UiPhone.Raise(Lift, 70f + 34f, 34f, 30f) - 34f) < 0.01f, "phone fold: a container another mod raised is measured where it stands");

        string kit = ModSource(@"Ui\Phone\PhoneKit.cs");
        string size = MemberText(kit, "private static void Size()");
        Check(size.Contains("UiPhone.Raise(Lift, LiftCorners[0].y, rect.anchoredPosition.y * scale, FoldTravel(scale))") && !size.Contains("Math.Max(0f, Lift - foot)")
              && MemberText(kit, "private static float FoldTravel(float scale)").Contains("_container.anchoredPosition.y - _window.unCollapsedPosition.y"),
            "phone fold: the kit's phone measures its raise where the game's container stands open (PhoneKit.Size)");
    }

    private static float OldRaise(float lift, float footNow, float raisedNow) => lift > 0f ? Math.Max(0f, lift - (footNow - raisedNow)) : 0f;

    // The HUD's key chips stand clear of the phone, open or folded, on their own row.
    private static void HintChipChecks()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        IUiKeys keys = UiDefaultKeys.Keyboard;
        var s = new UiScene();
        Func<UiHudModel, bool> lay = m =>
        {
            s.Begin("hud", frame.Width, frame.Height, true);
            s.AddPrompt(UiAction.Pick1, "Open map");
            s.AddPrompt(UiAction.Pick2, "Pause");
            s.AddPrompt(UiAction.Back, "Menu");
            return UiHud.Chips(s, m, frame, keys, out float _, out float _, out float _, out float _);
        };
        float lift = UiFrame.BandHeight + UiTokens.S2;   // the phone's foot over the bar, px of the layout (HudLive.Place)
        var none = new UiHudModel();
        lay(none);
        UiHud.Chips(s, none, frame, keys, out float x0, out float y0, out float w0, out float h0);
        Check(Math.Abs(x0 + w0 - (frame.Width - UiHud.Left)) < 0.01f, "hints: with no phone the chips end at the bar's right edge, as the mockup has them");

        var open = new UiHudModel { PhoneW = UiPhone.Width, PhoneH = UiPhone.HeightTall };
        open.PhoneX = frame.Width - UiTokens.S4 - open.PhoneW - 3f;   // a place off the grid, as a real screen gives it
        open.PhoneY = frame.Height - lift - open.PhoneH;
        var folded = new UiHudModel { PhoneW = open.PhoneW, PhoneX = open.PhoneX, PhoneH = lift + 38f };   // only the head shows over the bar
        folded.PhoneY = frame.Height - folded.PhoneH;
        Check(UiHudRules.Overlaps(x0, y0, w0, h0, open.PhoneX, open.PhoneY, open.PhoneW, open.PhoneH)
              && UiHudRules.Overlaps(x0, y0, w0, h0, folded.PhoneX, folded.PhoneY, folded.PhoneW, folded.PhoneH),
            "hints: the chips' own place lies under the phone, open and folded (the bug the owner saw)");
        foreach (UiHudModel m in new[] { open, folded })
        {
            string state = m == open ? "open" : "folded";
            bool shown = lay(m);
            UiHud.Chips(s, m, frame, keys, out float x, out float y, out float w, out float h);
            Check(shown && !UiHudRules.Overlaps(x, y, w, h, m.PhoneX, m.PhoneY, m.PhoneW, m.PhoneH) && x + w <= m.PhoneX - UiTokens.S2 + 0.01f,
                "hints: with the phone " + state + ", the chips end a step left of it and never cover it");
            Check(Math.Abs(y - y0) < 0.01f && Math.Abs(w - w0) < 0.01f && x >= UiHud.Left && Math.Abs(x / UiTokens.Grid - Math.Round(x / UiTokens.Grid)) < 0.01f,
                "hints: with the phone " + state + ", the chips keep their row over the bar, whole, on the grid");
        }
        var away = new UiHudModel { PhoneX = open.PhoneX, PhoneY = open.PhoneY, PhoneW = 0f, PhoneH = open.PhoneH };
        lay(away);
        UiHud.Chips(s, away, frame, keys, out float xa, out float _, out float _, out float _);
        Check(Math.Abs(xa - x0) < 0.01f, "hints: with the phone hidden, the chips are back at the bar's right edge");

        string live = ModSource(@"Ui\HudScreens\HudLive.cs");
        Check(live.Contains("sig = sig * 31 + PhoneBox(m);") && MemberText(live, "private static int PhoneBox(UiHudModel m)").Contains("PhoneKit.Rect")
              && MemberText(live, "private static int PhoneBox(UiHudModel m)").Contains("phone.GetWorldCorners(PhoneCorners)")
              && !MemberText(live, "private static int PhoneBox(UiHudModel m)").Contains("new "),
            "hints: the HUD reads where the phone stands with its other pieces, into a kept array, and lays out again when it moves");
    }
}
