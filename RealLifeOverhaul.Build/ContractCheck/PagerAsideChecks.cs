using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// When the phone's pager leaves a grid to another mod (Core/GameUi/PagerAsideRules.cs, PhoneGridPager.Aside.cs). A player's
// phone ran off the top of the screen with every app in two columns (#bug-reports, 2 Nov 2026, with Hotelier and
// Restaurateur). That mod puts its own button on the HUD phone and grows the phone to fit every button showing ("Phone grown
// by 3377 for 37 apps in 2 columns (4 rows shown before)"); it pages nothing. Its button joined this mod's pages, was paged
// off, and was shown again by its own mod; the pager took any button it had paged off shown again for another mod's pager,
// stepped aside for good and showed every button, and the other mod grew the phone for all 37. Now only a pager takes the
// grid: a known pager's script on it (BizSuite's PhoneAppsPager, AlunaPhonePages' PhoneSwipeHandler), or a hand that shows a
// button of the game's or this mod's on a page not showing. Another mod's button shown again joins the pages, unless its mod
// shows it on every frame; another mod's script that pages nothing, one that only sizes the phone, is no pager. Stood
// aside, the phone is kept within the screen.
//
// What is not here: the other mod itself, which is not on the build machine; its sizing is taken from its own log lines
// (two columns, rows of 225, four rows before it grows).
internal static partial class Program
{
    private const float PhoneHigh = 1230f, CanvasHigh = 2160f, PhoneRow = 225f;   // Smartphone/Container on the 3840 by 2160 canvas, anchored bottom right; a row of the grid
    private const int RowsShown = 4;                                              // the grid's rows before Hotelier and Restaurateur grows the phone

    // What Hotelier and Restaurateur adds to the phone for this many buttons showing, as its log lines give it (2 at four
    // rows, its own rounding: no row; fewer rows it never logged).
    private static float Grown(int showing)
    {
        int rows = (showing + 1) / 2;
        return rows >= RowsShown ? (rows - RowsShown) * PhoneRow + 2f : 0f;
    }

    private static void PagerAside(string scripts)
    {
        Section("the phone's pages: another mod's button, another mod's script and another mod's pager (Core/GameUi/PagerAsideRules)");
        PagerAsideJoin();
        PagerAsideResized();
        PagerAsidePagers();
        PagerAsideFit();
        PagerAsideSources(scripts);
    }

    // Another mod's button shown again by its own mod while paged off: it joins the pages, and the phone grows for no button.
    private static void PagerAsideJoin()
    {
        int once = PagerAsideRules.Run(30f, float.NaN, 0);
        Check(once == 1 && PagerAsideRules.Verdict(0, 1, once, false) == PagerAsideRules.Join,
            "another mod's button shown again by its own mod on a page not showing joins the pages; the pager does not step aside");
        // Shown again when the phone opens, or when the other mod counts again: never a fight however long the session.
        int run = 0, aside = 0;
        float last = float.NaN;
        for (int pass = 0; pass < 500; pass++)
        {
            float now = 12f + pass * 2.5f;
            run = PagerAsideRules.Run(now, last, run);
            last = now;
            aside += PagerAsideRules.Verdict(0, 1, run, false) == PagerAsideRules.Aside ? 1 : 0;
        }
        Check(aside == 0 && run == 1, "another mod showing its button again every 2.5 s, 500 times, is joined every time (" + aside + " step(s) aside)");
        // Several of its buttons shown again at once are one pass, not a run.
        Check(PagerAsideRules.Verdict(0, 4, PagerAsideRules.Run(40f, 10f, 1), false) == PagerAsideRules.Join, "four buttons of other mods shown again in one pass join the pages together");

        // The grid as the player had it: the game's 8, this mod's 28, Hotelier and Restaurateur's 1, paged 7 and the More apps
        // button a page (the button stands beside the grid). At every step of every page turn, hiding before showing, the
        // other mod counts at most a page of buttons showing and adds no row to the phone.
        const int apps = 37, tiles = PagerRules.PhoneTiles;
        int pages = PagerRules.Pages(apps, tiles), most = 0, page = 0;
        var shown = new bool[apps];
        for (int i = 0; i < apps; i++)
        {
            shown[i] = PagerRules.PageOf(i, apps, tiles) == page;
        }
        bool stays = true;
        for (int turn = 0; turn < pages * 3; turn++)
        {
            page = PagerRules.Next(page, pages);
            for (int hiding = 1; hiding >= 0; hiding--)
            {
                for (int i = 0; i < apps; i++)
                {
                    bool show = PagerRules.PageOf(i, apps, tiles) == page;
                    if (show == (hiding == 1) || shown[i] == show)
                    {
                        continue;
                    }
                    shown[i] = show;
                    int count = 0;
                    for (int j = 0; j < apps; j++)
                    {
                        count += shown[j] ? 1 : 0;
                    }
                    most = Math.Max(most, count);
                    stays &= Grown(count) < PhoneRow;
                }
            }
        }
        Check(pages == 6 && most == tiles - 1 && stays,
            "37 apps paged on the HUD phone: at every step of every page turn at most " + most + " showing (a page's apps; wanted " + (tiles - 1) + "), and the other mod adds no row to the phone");
        Check(Grown(37) == 3377f && Grown(36) == 3152f && Grown(9) == 227f && Grown(8) == 2f,
            "the other mod's growth as its log lines give it: 3377 for 37 apps, 3152 for 36, 227 for 9 (a row of 225 for each beyond its four, 2 at four)");
    }

    // A grid another mod resizes, or carries a script of its own that pages nothing: no step aside.
    private static void PagerAsideResized()
    {
        Check(PagerAsideRules.Verdict(0, 0, 0, PagerAsideRules.Pager("Restaurateur.PhoneGrid")) == PagerAsideRules.Keep
              && !PagerAsideRules.Pager("GoingPublic.Hub") && !PagerAsideRules.Pager(null) && !PagerAsideRules.Pager(string.Empty),
            "another mod's script on the grid that pages nothing (one that sizes the phone) is no pager: the pages stay");
        Check(PagerAsideRules.Verdict(0, 0, 0, false) == PagerAsideRules.Keep, "a grid resized by another mod, with no button shown on a page not showing, keeps its pages");
    }

    // A real pager: the pager steps aside as before.
    private static void PagerAsidePagers()
    {
        Check(PagerAsideRules.Pager("BizSuite.PhoneAppsPager") && PagerAsideRules.Pager("AlunaPhonePages.PhoneSwipeHandler")
              && PagerAsideRules.Verdict(0, 0, 0, true) == PagerAsideRules.Aside,
            "BizSuite's pager or AlunaPhonePages' swipe on the grid: the pager steps aside");
        Check(PagerAsideRules.Verdict(1, 0, 0, false) == PagerAsideRules.Aside && PagerAsideRules.Verdict(2, 3, 1, false) == PagerAsideRules.Aside,
            "a hand that shows a button of the game's or this mod's on a page not showing pages the grid: the pager steps aside");
        // A mod that shows its button on every frame would have a pass a frame: after a short run it is left the grid.
        int run = 0, frames = 0;
        float last = float.NaN;
        while (frames < 60)
        {
            float now = 5f + frames / 60f;
            run = PagerAsideRules.Run(now, last, run);
            last = now;
            frames++;
            if (PagerAsideRules.Verdict(0, 1, run, false) == PagerAsideRules.Aside)
            {
                break;
            }
        }
        Check(frames == PagerAsideRules.FightRun + 1, "another mod showing its button on every frame is left the grid after " + frames + " passes (wanted " + (PagerAsideRules.FightRun + 1) + "), never a pass a frame");
    }

    // Stood aside with every button showing: the phone kept within the screen.
    private static void PagerAsideFit()
    {
        float grown = PhoneHigh + Grown(37);   // 4607, its top 2447 above the screen's
        float fitted = PagerAsideRules.FitHeight(grown, PhoneHigh, 0f, grown - CanvasHigh, 0f);
        Check(Math.Abs(fitted - CanvasHigh) < 0.01f, "the phone grown for 37 apps (" + grown + " high, anchored at the bottom) is kept to the screen's height (" + fitted + ", wanted " + CanvasHigh + ")");
        float centred = PagerAsideRules.FitHeight(3000f, PhoneHigh, 0.5f, 200f, 200f);
        Check(Math.Abs(centred - 2600f) < 0.01f, "a part grown both ways (pivot in the middle) running 200 past either edge is cut by 400 (" + centred + ")");
        Check(PagerAsideRules.FitHeight(PhoneHigh, PhoneHigh, 0f, 300f, 0f) == PhoneHigh && PagerAsideRules.FitHeight(PhoneHigh + 227f, PhoneHigh, 0f, -100f, 0f) == PhoneHigh + 227f,
            "a phone not grown, or grown but on the screen, is left as it is");
        Check(PagerAsideRules.FitHeight(1500f, PhoneHigh, 0f, 900f, 0f) == PhoneHigh && PagerAsideRules.FitHeight(4000f, PhoneHigh, 1f, 900f, 0f) == 4000f,
            "only what another mod added is taken back, never below the game's own height, and nothing when a cut would not move the edge");
        Check(PagerAsideRules.FitHeight(4000f, PhoneHigh, float.NaN, 900f, 0f) == 4000f && PagerAsideRules.FitHeight(4000f, PhoneHigh, 0f, float.NaN, 0f) == 4000f,
            "anything unknown leaves the phone as it is");
    }

    private static void PagerAsideSources(string scripts)
    {
        string pager = PagerSource(scripts);
        if (pager == null)
        {
            Check(false, "the pager's source is not where it was");
            return;
        }
        string someone = Body(pager, "private bool SomeoneElse()") ?? string.Empty;
        Check(someone.Contains("PagerAsideRules.Verdict(own, others, _shownRun, pager)") && someone.Contains("PagerAsideRules.Run(Time.unscaledTime, _lastShown, _shownRun)")
              && someone.Contains("_gameButtons.Contains(child) || RankOf(child) >= 0") && someone.Contains("child.GetComponent<PageMark>().Off = false;"),
            "the pager asks the rules: the game's and this mod's buttons shown on a page not showing step it aside, other mods' join the pages");
        Check(Body(pager, "private static bool Paging(Type type)")?.Contains("PagerAsideRules.Pager(type.FullName)") == true && pager.Contains("KnownScripts[type] = pager;")
              && !someone.Contains("Known("),
            "a script on the grid steps the pager aside only when it is a known pager, each kind looked at once");
        Check(!Regex.IsMatch(someone, @"\b(rect|sizeDelta|height|lossyScale)\b") && Body(pager, "internal void Changed()")?.Contains("FitLater();") == true
              && Body(pager, "internal void Changed()")?.Contains("StepAside") == false && Body(pager, "private void Fit()")?.Contains("StepAside") == false,
            "a change of the phone's size never steps the pager aside; stood aside, it only keeps the phone on the screen");
        string fit = Body(pager, "private void Fit()") ?? string.Empty;
        Check(fit.Contains("PagerAsideRules.FitHeight(height, _frameHeight[i], part.pivot.y, (Corners[1].y - top) / scale, (bottom - Corners[0].y) / scale)")
              && fit.Contains("SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, wanted)") && fit.Contains("if (fitted && !_fitTold)")
              && Body(pager, "public void Refresh()")?.Contains("Fit();") == true && Body(pager, "private void StepAside()")?.Contains("FitLater();") == true,
            "stood aside, every pass keeps the phone's parts within the screen through the rules, told once in Player.log");
        string read = Body(pager, "private void ReadFrame()") ?? string.Empty;
        Check(read.Contains("_frameHeight[i] = parts[i].rect.height;") && read.Contains("AddComponent<PhoneFrameWatch>()") && Body(pager, "public static PhoneGridPager On(")?.Contains("pager.ReadFrame();") == true
              && Body(pager, "private void FitLater()")?.Contains("Canvas.willRenderCanvases += _late;") == true,
            "the phone's heights as the game built them are read once when the HUD pager comes, and a change of its parts' size is one pass before the canvas is drawn");
        string apply = Body(pager, "private void Apply()") ?? string.Empty;
        string folders = Body(pager, "private void ShowFolders(bool hiding)") ?? string.Empty;
        string gaps = Body(pager, "private void ShowGaps(int count, bool hiding)") ?? string.Empty;
        Check(apply.Contains("for (int hiding = 1; hiding >= 0; hiding--)") && apply.Contains("if (show == (hiding == 1))")
              && apply.Contains("ShowFolders(hiding == 1);") && apply.Contains("ShowGaps(gaps, hiding == 1);")
              && folders.Contains("if (on != hiding)") && gaps.Contains("on != hiding"),
            "a page turn hides the last page's buttons, folder tiles and empty tiles before it shows the next page's, so no mod counting what shows sees more than a page");
        Check(Body(pager, "internal string VisitorCheck()")?.Contains("visitor.SetActive(true);   // its own mod shows it again") == true,
            "the test Check the phone's pages shows a stand-in for another mod's button again on a page not showing");
    }
}
