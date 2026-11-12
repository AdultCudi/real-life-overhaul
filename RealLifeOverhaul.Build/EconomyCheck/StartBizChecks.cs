using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// BizMan's Start a business page on the kit: every kind of business reachable by the wheel, the keys and the pad, the chosen kind's
// detail carrying what the game says of it, no game sentence printed raw where a kit label belongs, the picture rule that keeps a
// game picture sharp, and each press going to the game's own button. The pieces that read the game are read from the sources.
internal static partial class Program
{
    private static void StartBizAll()
    {
        StartBizLayout();
        StartBizSources();
        StartBizPictures();
        StartBizTexts();
    }

    private static int StartBizRowsSeen(UiScene s, int rows)
    {
        int seen = 0;
        for (int i = 0; i < rows; i++)
        {
            seen += s.FindNode(BizManPages.StartKinds + UiDataTable.Row, i) >= 0 ? 1 : 0;
        }
        return seen;
    }

    private static void StartBizLayout()
    {
        UiWords words = UiWords("en");
        var s = new UiScene();
        // Three screen sizes: the layout is in px of a 1080 high screen at every resolution, so 720, 1080 and 1440 are the same frame
        // drawn at two thirds, one and one and a third; the aspects change what the frame holds.
        foreach (float aspect in new[] { 16f / 9f, 16f / 10f, 21f / 9f })
        {
            UiFrame frame = UiFrame.FullMenu(aspect, "BizMan");
            foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
            {
                string where = " at " + aspect.ToString("0.00") + (keys.Pad != UiPad.None ? " with a pad" : "");
                BizManModel m = UiSamples.BizMan(words, "start");
                int rows = m.StartKinds.RowCount;
                BizManPages.Lay(s, m, frame, true, keys);
                int firstSeen = StartBizRowsSeen(s, rows);
                Check(firstSeen > 0 && firstSeen < rows || firstSeen == rows, "start biz: the list lays its first rows" + where);
                // The wheel: each notch moves the list, and the offset it comes to shows the last kind.
                float last = -1f;
                for (int notch = 0; notch < 60 && m.StartKinds.Offset > last; notch++)
                {
                    last = m.StartKinds.Offset;
                    m.StartKinds.Offset += UiTokens.S16;
                    BizManPages.Lay(s, m, frame, true, keys);
                }
                Check(s.FindNode(BizManPages.StartKinds + UiDataTable.Row, rows - 1) >= 0, "start biz: the wheel brings the last kind of the list into view" + where);
                // The keys and the pad: stepping to the last kind asks the list to reveal it, wherever it was scrolled.
                m.StartKinds.Offset = 0f;
                m.StartKinds.Reveal = rows - 1;
                BizManPages.Lay(s, m, frame, true, keys);
                Check(s.FindNode(BizManPages.StartKinds + UiDataTable.Row, rows - 1) >= 0, "start biz: stepping to the last kind with the keys or the pad scrolls it into view" + where);
                m.StartKinds.Reveal = -1;
                m.StartKinds.Offset = 0f;
                BizManPages.Lay(s, m, frame, true, keys);
                Check(UiFocusRules.Unreached(s, out string missed) == 0, "start biz: the d-pad reaches every target of the page" + where + " (" + missed + ")");

                // The detail: each heading is a stop for the pad that brings it into view, and the wheel reaches the last row.
                int headings = 0;
                foreach (BizManStartRow r in m.StartDetail.Rows)
                {
                    headings += r.Kind == BizManStartKind.Heading ? 1 : 0;
                }
                bool every = headings > 1;
                for (int h = 0; h < headings; h++)
                {
                    m.StartOffset = 0f;
                    m.StartReveal = h;
                    BizManPages.Lay(s, m, frame, true, keys);
                    every &= s.FindNode(BizManPages.Heading, BizManPages.StartHeading + h) >= 0;
                }
                Check(every, "start biz: every heading of the detail is a stop the pad scrolls into view" + where);
                m.StartOffset = 100000f;
                BizManPages.Lay(s, m, frame, true, keys);
                UiClip clip = s.Clips[m.StartClip];
                Check(m.StartClip >= 0 && clip.Offset + clip.H >= clip.ContentHeight - 0.5f && clip.Offset > 0f, "start biz: the wheel takes the detail to its last row" + where);
                bool start = s.FindNode(BizManPages.Action, BizManPages.DoStartHere) >= 0;
                Check(start, "start biz: Start business stands at the detail's foot while it is scrolled" + where);
            }
        }
        // Nothing chosen: the detail asks for a choice and Start business is still offered, for the game to answer as it does.
        BizManModel none = UiSamples.BizMan(words, "start");
        none.StartDetail = null;
        BizManPages.Lay(s, none, UiFrame.FullMenu(16f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.Action, BizManPages.DoStartHere) >= 0, "start biz: with no kind chosen the page still offers Start business, which the game answers");
        // The sample's detail carries every kind of row the reader writes.
        BizManModel full = UiSamples.BizMan(words, "start");
        var kinds = new HashSet<BizManStartKind>();
        bool marks = false;
        foreach (BizManStartRow r in full.StartDetail.Rows)
        {
            kinds.Add(r.Kind);
            marks |= r.Mark != UiIcon.None;
        }
        Check(kinds.Count == 4 && marks, "start biz: the detail holds headings, facts, list lines with their marks and sentences");
    }

    private static void StartBizSources()
    {
        string read = ModSource(@"Ui\BizMan\BizManScreen.Start.cs");
        string page = ModSource(@"Ui\BizMan\BizManScreen.Page.cs");
        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs");
        string screen = ModSource(@"Ui\BizMan\BizManScreen.cs");
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs");
        // What the game says of a kind: its goods or its staff, where the goods come from, what it needs, the diploma, the building.
        Check(read.Contains("bt.GetPrimaryProducts()") && read.Contains("bt.employeePrimarySkills") && read.Contains("GameContract.KindSourcesBox") && read.Contains("bt.businessRequirements")
              && read.Contains("GetRequiredItemCount") && read.Contains("bt.courseRequired") && read.Contains("EducationHelper.HasCompletedDiploma") && read.Contains("\"TrafficIndex\"")
              && read.Contains("\"CustomerCapacity\"") && read.Contains("\"MarketValue\"") && read.Contains("GameContract.KindSourceable"),
            "start biz: the detail reads the game's goods or staff, sources, requirements by the building's size, diploma and the building's facts");
        Check(read.Contains("GameContract.KindRivalCount") && read.Contains("reallife_ui_bizman_competitors"), "start biz: the competitors are a figure under a label of the kit");
        // No game sentence raw where a kit label belongs: the game's own competitors label (its English, US spelling) is never read.
        Check(!read.Contains("GameContract.KindRivals,") && !read.Contains("Join2"), "start biz: the game's competitors sentence is not printed raw");
        // The presses are the game's own buttons.
        Check(press.Contains("case BizManPages.DoStartHere: return SetUpBusiness(b);") && read.Contains("Calling(start, \"SetUpBusiness\")")
              && read.Contains("GameContract.KindSelect") && press.Contains("Calling(Get<StartBusinessUI>(GameContract.ShowStart, pres), \"RandomizeName\")"),
            "start biz: choose, Randomise and Start press the game's own buttons, so its checks and other mods' patches run");
        Check(read.Contains("Calling(pres, \"PreviewBuilding\")") && read.Contains("Calling(pres, \"TerminateContract\")"), "start biz: the building's Preview and Terminate stay, as the game shows them");
        // The scroll: the page keeps the place it was scrolled to across the refresh a wheel notch asks for, and the wheel and the pad reach it.
        string clear = MemberText(page, "private void ClearPart()");
        Check(clear.Length > 0 && !clear.Contains("Catalogue") && !clear.Contains("StartKinds") && !read.Contains("_m.Catalogue") && !screen.Contains("_m.Catalogue") && !parts.Contains("_m.Catalogue"),
            "start biz: the page's refresh does not empty the list before the scroll it keeps is read");
        Check(read.Contains("UiDataTableModel old = again ? _m.StartKinds : null;") && read.Contains("Offset = old != null ? old.Offset : 0f"), "start biz: a refresh of the page shown keeps the list's scroll");
        Check(screen.Contains("if (StartWheel(clip, by))") && screen.Contains("if (StartReveal(list, index))") && press.Contains("BizManPages.StartKinds + UiDataTable.Row")
              && screen.Contains("KindTyped(text)"), "start biz: the wheel, the pad's reveal, the search and a row's press reach the page");
        // Start business is an action of the page, among the pages' actions: a number of the commands' block once put it past the Interior tab's buttons.
        Check(Array.IndexOf(BizManPages.Commands, BizManPages.StartKinds + UiDataTable.Row) >= 0 && BizManPages.StartKinds >= 9800
              && Array.IndexOf(BizManPages.Actions, BizManPages.DoStartHere) >= 0 && BizManPages.DoStartHere < BizManPages.StartKinds,
            "start biz: the page's commands stand in the job's block, and Start business among the pages' actions");
    }

    private static void StartBizPictures()
    {
        // A picture of 512 pixels in a 32 px tile, at 720, 1080 and 1440: resampled to the size it shows at.
        foreach (float device in new[] { 2f / 3f, 1f, 4f / 3f })
        {
            float x = 0f, y = 0f, w = 32f, h = 32f;
            int how = UiSharpRules.Plan(512f, 512f, device, ref x, ref y, ref w, ref h, out int tw, out int th);
            Check(how == 2 && tw == (int)Math.Round(32f * device) && th == tw && w == 32f, "start biz pictures: a large picture in a small tile is resampled to its size at " + (int)(device * 1080f) + "p");
            x = y = 0f;
            w = h = 200f;
            how = UiSharpRules.Plan(64f, 64f, device, ref x, ref y, ref w, ref h, out _, out _);
            Check(how == 1 && Math.Abs(w * device - 64f) < 0.01f && Math.Abs(x - (200f - w) / 2f) < 0.01f, "start biz pictures: a small picture is never stretched past its pixels at " + (int)(device * 1080f) + "p");
            x = y = 0f;
            w = h = 64f;
            how = UiSharpRules.Plan(64f * device, 64f * device, device, ref x, ref y, ref w, ref h, out _, out _);
            Check(how == 0 && w == 64f, "start biz pictures: a picture drawn at its own pixels is left as it is at " + (int)(device * 1080f) + "p");
        }
        string slots = ModSource(@"Ui\UiView.Slots.cs");
        string sharp = ModSource(@"Ui\UiView.Sharp.cs");
        Check(slots.Contains("sprite = Crisp(sprite, ref px, ref py, ref pw, ref ph);") && slots.Contains("Place(slot.Rect, px, py, pw, ph);")
              && sharp.Contains("UiSharpRules.Plan(") && sharp.Contains("Graphics.Blit"),
            "start biz pictures: every game picture the kit draws goes through the one sharpening rule");
    }

    private static void StartBizTexts()
    {
        string[] keys =
        {
            "reallife_ui_bizman_competitors", "reallife_ui_bizman_competitors_here", "reallife_ui_bizman_find_kind", "reallife_ui_bizman_start_pick", "reallife_ui_bizman_start_pick_body",
            "reallife_ui_bizman_start_sources", "reallife_ui_bizman_start_source_missing", "reallife_ui_bizman_start_needs", "reallife_ui_bizman_start_diploma",
            "reallife_ui_bizman_start_diploma_missing"
        };
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            bool all = true;
            foreach (string key in keys)
            {
                all &= texts.ContainsKey(key) && !texts[key].Contains("!");
            }
            Check(all, "start biz texts: the page's own texts are in " + code);
        }
        foreach (string code in new[] { "en", "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" })
        {
            string next = WhatsNewText(LocaleFile(code), WhatsNewKey(LocaleFile("en"), "Start a business in BizMan is redrawn as a searchable list"));   // the row that holds it
            Check(next.Contains("BizMan"), "start biz texts: What's new names the start page's fix in " + code);
        }
    }
}
