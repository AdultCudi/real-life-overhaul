using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;

// What a conversation leaves behind when it ends another way than by its own last button (Scripts/Core/GameUi/TextBoxRules.cs,
// TextBox.cs, ConversationWatch.cs). A player, issue 22: after closing a call by shutting the phone or pressing Esc,
// rather than with its own button, doors, keys and the mod's buttons no longer worked ("finish what you are doing first").
// The game closes its text box only with the box's own two buttons, so the box a call's composer asked for stayed up,
// with its field taking every key, after the call was gone; and DialogController.current, one static the dialogue panel
// and the phone's call share, was cleared by a call ending while the panel was open, so the panel could no longer be
// ended and its navigation blocker stayed. The model below does what the decompiled game does at each step (DialogUI,
// ContactsApp, FullMenu, CancelButtonHandler, TextInputUI); each path is run once as the game left it (nothing put
// right) and once with the mod's watch, which must put it right.
internal static partial class Program
{
    private static void TextBoxes()
    {
        TextBoxPhoneShut();
        TextBoxEsc();
        TextBoxHungUp();
        TextBoxPanelLeft();
        TextBoxDoor();
        TextBoxLoad();
        TextBoxGone();
        TextBoxOwnTime();
        TextBoxOptions();
        TextBoxNothing();
        TextBoxHandle();
        TextBoxCost();
        TextBoxSources();
        TextBoxHolds();
        TextBoxTexts();
    }

    // The game's pieces as the decompiled code has them.
    private sealed class TalkGame
    {
        public TalkHolder Holder;      // DialogController.current
        public bool PanelOpen;         // DialogUI.isPanelOpen
        public bool PanelBlocks;       // the navigation blocker DialogUI.ShowDialog set and StopDialog unsets (SpecialEmployeeDialog)
        public bool PhoneOpen;         // FullMenu.IsOpen
        public bool CallLive;          // ContactsApp.contact != null: a call is on
        public int Call;               // which call it is (the composer's Live: the game's call is still this one)
        public bool BoxShown;          // TextInputUI's object active
        public bool OptionsOpen;       // Options.IsVisible

        // DialogUI.ShowDialog: the handle is the panel's, and so is the blocker.
        public void PanelShow()
        {
            Holder = TalkHolder.Panel;
            PanelOpen = true;
            PanelBlocks = true;
        }

        // The mod's last button, Esc on the panel and the guard's way out: DialogController.current.CancelDialog().
        // With nobody holding the handle the mod's code finds no conversation (a fault the guard catches) and nothing ends.
        public void ModEnds()
        {
            if (Holder == TalkHolder.Panel)
            {
                Holder = TalkHolder.None;   // DialogUI.StopDialog
                PanelOpen = false;
                PanelBlocks = false;
            }
            else if (Holder == TalkHolder.Phone)
            {
                EndCall();
            }
        }

        // ContactsApp.StartCall (the phone open on the contact).
        public void StartCall()
        {
            PhoneOpen = true;
            Holder = TalkHolder.Phone;
            CallLive = true;
            Call++;
        }

        // ContactsApp.CancelDialog and FinishDialog: the handle cleared whatever holds it, while a call is on.
        public void EndCall()
        {
            if (CallLive)
            {
                CallLive = false;
                Holder = TalkHolder.None;
            }
        }

        // FullMenu hides the phone; the Contacts app's OnDisable cancels the call.
        public void ShutPhone()
        {
            PhoneOpen = false;
            EndCall();
        }

        // CancelButtonHandler.HandleEscapeClick: the phone first (HasClosedFullMenu), then the panel's OnCancel. The text
        // box is no case of it (only the game's staff schedule closes the box on Esc, its own).
        public void Esc()
        {
            if (PhoneOpen)
            {
                ShutPhone();
            }
            else if (PanelOpen)
            {
                ModEnds();
            }
        }

        // With the box up and its field selected, the game takes every key as typing (GameManager.ShouldBlockKeyboardShortcuts),
        // and the mod counts the player busy with a window (ThirdPersonCamera.IsUiOpen): doors by key and the mod's panels refuse.
        public bool Stuck => BoxShown;
    }

    // One box of the mod's, kept for what was open when it was asked, and the watch's look at it (TextBox.Sweep).
    private sealed class AskedBox
    {
        public TextBoxFor For;
        public Func<bool> Owner;
        public bool Moved;
        public TextBoxFate Last;

        public static AskedBox Ask(TalkGame g, Func<bool> owner)
        {
            g.BoxShown = true;
            return new AskedBox { For = TextBoxRules.For(owner != null, g.PhoneOpen, g.PanelOpen, g.OptionsOpen), Owner = owner };
        }

        public void Watch(TalkGame g, bool unloading)
        {
            if (For == TextBoxFor.Nothing)
            {
                return;
            }
            Last = TextBoxRules.Fate(For, g.BoxShown, For == TextBoxFor.Owner && Owner(), g.PhoneOpen, g.PanelOpen, g.OptionsOpen, Moved, unloading);
            if (Last == TextBoxFate.Keep)
            {
                return;
            }
            For = TextBoxFor.Nothing;
            if (TextBoxRules.Closes(Last))
            {
                g.BoxShown = false;   // TextInputUI.OnClose
            }
        }
    }

    // The watch's other look (ConversationWatch.HandBack).
    private static void HandBack(TalkGame g)
    {
        if (TextBoxRules.HandBack(g.PanelOpen, g.Holder))
        {
            g.Holder = TalkHolder.Panel;
        }
    }

    // A call's composer: the box is kept while the game's call is still this one.
    private static Func<bool> ThisCall(TalkGame g)
    {
        int call = g.Call;
        return () => g.CallLive && g.Holder == TalkHolder.Phone && g.Call == call;
    }

    // The issue's path: a call's typed answer, the phone shut instead of the call's own button.
    private static void TextBoxPhoneShut()
    {
        var old = new TalkGame();
        old.StartCall();
        old.BoxShown = true;
        old.ShutPhone();
        Check(old.Stuck, "text box: as the game leaves it, a call's box stays up after the phone is shut (the issue reproduced)");

        var g = new TalkGame();
        g.StartCall();
        AskedBox box = AskedBox.Ask(g, ThisCall(g));
        Check(box.For == TextBoxFor.Owner, "text box: a call's composer keeps its box for the call itself");
        box.Watch(g, false);
        Check(g.BoxShown && box.Last == TextBoxFate.Keep, "text box: while the call goes on its box stays");
        g.ShutPhone();
        box.Watch(g, false);
        Check(!g.BoxShown && box.Last == TextBoxFate.OwnerEnded && !g.Stuck, "text box: the phone shut in the middle of a call closes the call's box at the watch's next look");
    }

    // Esc in the middle of a call: the game shuts the phone first.
    private static void TextBoxEsc()
    {
        var g = new TalkGame();
        g.StartCall();
        AskedBox box = AskedBox.Ask(g, ThisCall(g));
        g.Esc();
        Check(!g.PhoneOpen && !g.CallLive, "text box: Esc in a call puts the phone away and the call with it, as the game does");
        box.Watch(g, false);
        Check(!g.BoxShown && box.Last == TextBoxFate.OwnerEnded, "text box: Esc in the middle of a call closes the call's box");
    }

    // Hung up with the phone still open, and a second call made at once: the first call's box goes, the second's is not touched.
    private static void TextBoxHungUp()
    {
        var g = new TalkGame();
        g.StartCall();
        AskedBox box = AskedBox.Ask(g, ThisCall(g));
        g.EndCall();
        g.StartCall();
        box.Watch(g, false);
        Check(!g.BoxShown && box.Last == TextBoxFate.OwnerEnded, "text box: a call hung up (and another begun) closes the first call's box though the phone is still open");
        AskedBox second = AskedBox.Ask(g, ThisCall(g));
        second.Watch(g, false);
        Check(g.BoxShown && second.Last == TextBoxFate.Keep, "text box: the new call's own box stays");
    }

    // A talk in the street (the dialogue panel): left by Esc or by its own last button.
    private static void TextBoxPanelLeft()
    {
        var old = new TalkGame();
        old.PanelShow();
        old.BoxShown = true;
        old.Esc();
        Check(old.Stuck, "text box: as the game leaves it, a talk's box stays up after the talk is left with Esc");

        var g = new TalkGame();
        g.PanelShow();
        AskedBox box = AskedBox.Ask(g, null);
        Check(box.For == TextBoxFor.Panel, "text box: a box asked over the dialogue panel is kept for the panel");
        g.Esc();
        box.Watch(g, false);
        Check(!g.BoxShown && box.Last == TextBoxFate.PanelClosed && !g.Stuck, "text box: Esc on a talk closes its box with the talk");

        var h = new TalkGame();
        h.PanelShow();
        AskedBox left = AskedBox.Ask(h, null);
        h.ModEnds();
        left.Watch(h, false);
        Check(!h.BoxShown && left.Last == TextBoxFate.PanelClosed, "text box: a talk left by its own button closes its box");
    }

    // A door gone through, or another building: whatever the box was for is somewhere else now.
    private static void TextBoxDoor()
    {
        var g = new TalkGame { PhoneOpen = true };
        AskedBox box = AskedBox.Ask(g, null);
        Check(box.For == TextBoxFor.Phone, "text box: a box asked from a page of the phone is kept for the phone");
        box.Moved = true;
        box.Watch(g, false);
        Check(!g.BoxShown && box.Last == TextBoxFate.Moved, "text box: through a door or into another building, the box goes");

        var p = new TalkGame { PhoneOpen = true };
        AskedBox page = AskedBox.Ask(p, null);
        p.ShutPhone();
        page.Watch(p, false);
        Check(!p.BoxShown && page.Last == TextBoxFate.PhoneClosed, "text box: a phone page's box (a name, an amount, a note) goes when the phone is put away");
    }

    // A save loaded, the city unloading: every box of the mod's goes.
    private static void TextBoxLoad()
    {
        var g = new TalkGame();
        g.StartCall();
        AskedBox box = AskedBox.Ask(g, ThisCall(g));
        box.Watch(g, true);
        Check(!g.BoxShown && box.Last == TextBoxFate.Unloaded, "text box: a load or the city unloading closes the box, the call or not");
    }

    // The game's interface gone (the box's object destroyed or switched off by the game): nothing to close, only forgotten.
    private static void TextBoxGone()
    {
        var g = new TalkGame { PhoneOpen = true };
        AskedBox box = AskedBox.Ask(g, null);
        g.BoxShown = false;
        box.Watch(g, false);
        Check(box.Last == TextBoxFate.Closed && !TextBoxRules.Closes(box.Last) && box.For == TextBoxFor.Nothing,
            "text box: a box its own buttons closed, or whose interface went, is forgotten and not closed again");
        g.BoxShown = true;   // (the game's own ask of the box, later)
        box.Watch(g, false);
        Check(g.BoxShown, "text box: a box forgotten never closes the game's own later ask of the box");
    }

    // The owner's own time up (a talk in the street that times out): its own test says it is over.
    private static void TextBoxOwnTime()
    {
        var g = new TalkGame();
        g.PanelShow();
        float now = 0f, until = 30f;
        AskedBox box = AskedBox.Ask(g, () => now < until);
        box.Watch(g, false);
        Check(g.BoxShown, "text box: before its owner's time is up the box stays");
        now = 31f;
        box.Watch(g, false);
        Check(!g.BoxShown && box.Last == TextBoxFate.OwnerEnded, "text box: once its owner's own time is up the box goes, the panel open or not");
    }

    // The options page: its box goes when the page is closed.
    private static void TextBoxOptions()
    {
        var g = new TalkGame { OptionsOpen = true };
        AskedBox box = AskedBox.Ask(g, null);
        Check(box.For == TextBoxFor.Options, "text box: a box asked from the options page is kept for the page");
        g.OptionsOpen = false;
        box.Watch(g, false);
        Check(!g.BoxShown && box.Last == TextBoxFate.OptionsClosed, "text box: the options page closed, its box goes");
    }

    // Nothing of the mod's or the game's open: the box is left to its own two buttons, as before.
    private static void TextBoxNothing()
    {
        var g = new TalkGame();
        AskedBox box = AskedBox.Ask(g, null);
        box.Watch(g, true);
        Check(box.For == TextBoxFor.Nothing && g.BoxShown && box.Last == TextBoxFate.Keep, "text box: a box asked with nothing open is never closed by the watch");
        Check(TextBoxRules.For(false, true, true, true) == TextBoxFor.Phone, "text box: asked with the phone over the panel, the box is the phone's");
    }

    // A talk in person and a call that took its handle: the talk could no longer be ended, and its blocker stayed.
    private static void TextBoxHandle()
    {
        var old = new TalkGame();
        old.PanelShow();
        old.StartCall();
        old.ShutPhone();
        old.Esc();
        old.ModEnds();
        Check(old.PanelOpen && old.PanelBlocks && old.Holder == TalkHolder.None,
            "conversation: as the game leaves it, a talk whose handle a call took and cleared cannot be ended, and its navigation blocker stays (the issue reproduced)");

        var g = new TalkGame();
        g.PanelShow();
        g.StartCall();
        HandBack(g);
        Check(g.Holder == TalkHolder.Phone, "conversation: while the call is on the handle stays the call's");
        g.ShutPhone();
        HandBack(g);
        Check(g.Holder == TalkHolder.Panel, "conversation: once the call has let go the panel has its handle back");
        g.Esc();
        Check(!g.PanelOpen && !g.PanelBlocks && g.Holder == TalkHolder.None, "conversation: and Esc ends the talk again, its blocker with it");

        var h = new TalkGame();
        h.PanelShow();
        h.StartCall();
        h.EndCall();
        HandBack(h);
        h.ModEnds();
        Check(!h.PanelOpen && !h.PanelBlocks, "conversation: a call hung up in the phone gives the talk its handle back, and its own last button ends it");
        Check(!TextBoxRules.HandBack(false, TalkHolder.None), "conversation: no panel open, nothing is handed");
    }

    // Four times a second while a box is up: allocation-free and quick.
    private static void TextBoxCost()
    {
        TextBoxCostRun(1000, out _, out _);
        TextBoxCostRun(1000000, out double ms, out long bytes);
        Check(bytes == 0, "text box: the watch's rules allocate nothing (" + bytes + " bytes over a million looks)");
        Check(ms < 200.0, "text box: a million looks of the watch's rules in " + ms.ToString("0.0") + " ms (wanted: under 200 ms)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void TextBoxCostRun(int times, out double ms, out long bytes)
    {
        int closes = 0;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < times; i++)
        {
            TextBoxFor kept = TextBoxRules.For((i & 1) == 0, (i & 2) == 0, (i & 4) == 0, (i & 8) == 0);
            TextBoxFate fate = TextBoxRules.Fate(kept, (i & 16) == 0, (i & 32) == 0, (i & 2) == 0, (i & 4) == 0, (i & 8) == 0, (i & 64) == 0, (i & 128) == 0);
            closes += TextBoxRules.Closes(fate) ? 1 : 0;
            closes += TextBoxRules.HandBack((i & 4) == 0, (TalkHolder)(i % 3)) ? 1 : 0;
        }
        ms = watch.Elapsed.TotalMilliseconds;
        bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (closes < 0)
        {
            Console.WriteLine(closes);
        }
    }

    // The mod as built: every box through TextBox, the watch running, the call's composer owning its box, the taxi's card let go.
    private static void TextBoxSources()
    {
        string scripts = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts");
        string Read(string rel) => File.ReadAllText(System.IO.Path.Combine(scripts, rel));
        string box = Read(@"Core\GameUi\TextBox.cs");
        string watch = Read(@"Core\GameUi\ConversationWatch.cs");
        string start = Read(@"Core\RealLifeOverhaulMod.cs");
        string composer = Read(@"Social\PersonDialog.Composer.cs");
        string cab = Read(@"Transport\Taxi\TaxiCab.cs");
        var direct = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(@"Core\GameUi\TextBox.cs", StringComparison.Ordinal) && File.ReadAllText(file).Contains("textInputUi.Show("))
            {
                direct.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(direct.Count == 0, "text box: every box of the mod's is asked through TextBox, which keeps it for something (asked directly: " + string.Join(", ", direct) + ")");
        Check(box.Contains("TextBoxRules.For(") && box.Contains("TextBoxRules.Fate(") && box.Contains("box.OnClose()") && box.Contains("if (ask == _asked)")
              && box.Contains("Guard.Way<string>(") && box.Contains("Guard.Way(() =>") && box.Contains("catch (Exception e)"),
            "text box: TextBox keeps each ask for what was open, closes it with the game's own close, forgets only its own ask, and hands the game guarded callbacks");
        Check(watch.Contains("TextBox.Sweep(false)") && watch.Contains("TextBox.Sweep(true)") && watch.Contains("TextBoxRules.WatchEvery") && watch.Contains("Time.unscaledTime")
              && watch.Contains("HotSpots.Time(HotUpdate)") && watch.Contains("TextBoxRules.HandBack(") && watch.Contains("DialogController.current = panel"),
            "text box: the watch looks four times a second, timed, closes a box as a city starts and ends, and gives the panel its handle back");
        Check(start.Contains("Guard.Add<GameUi.ConversationWatch>(_root)") && start.Contains("GameUi.TextBox.Logger = context.Logger"), "text box: the watch starts with the city");
        Check(composer.Contains("_boxWanted ?? (_boxWanted = () => Live)"), "text box: a call's composer keeps its box for the call itself (the game's call is still this one)");
        int release = cab.IndexOf("public void Release(Leave how, string by)", StringComparison.Ordinal);
        int card = release >= 0 ? cab.IndexOf("OverlayGuard.Release(Controller);", release, StringComparison.Ordinal) : -1;
        int off = release >= 0 ? cab.IndexOf("TrafficTools.Remove(car, by);", release, StringComparison.Ordinal) : -1;
        int abandon = cab.IndexOf("private void Abandon()", StringComparison.Ordinal);
        Check(card > release && card < off && abandon > 0 && cab.IndexOf("OverlayGuard.Release(Controller);", abandon, StringComparison.Ordinal) > abandon,
            "taxi: a called taxi let go, on every way out, lets the game's card over it go first (the pool keeps the vehicle, so the card never sees it gone)");
    }

    // Each hold of the mod's on the player's walk has a value of its own: the game keeps them in one set, so two features on
    // one value lift each other's hold.
    private static void TextBoxHolds()
    {
        string scripts = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts");
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var twice = new List<string>();
        var value = new Regex(@"\(NavigationBlocker\)(\d+)");
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in value.Matches(File.ReadAllText(file)))
            {
                string name = System.IO.Path.GetFileName(file);
                if (seen.TryGetValue(m.Groups[1].Value, out string first) && first != name)
                {
                    twice.Add(m.Groups[1].Value + " (" + first + ", " + name + ")");
                }
                else
                {
                    seen[m.Groups[1].Value] = name;
                }
            }
        }
        Check(seen.Count >= 11 && twice.Count == 0, "holds: each of the mod's own navigation blockers is used by one feature only (shared: " + string.Join("; ", twice) + ")");
    }

    private static void TextBoxTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        string[] parts = { "typing box", "caixa de digitação", "输入框" };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            Check(AnyChangesRow(words[l], parts[l]), "text box: What's new says so in " + codes[l]);
        }
    }
}
