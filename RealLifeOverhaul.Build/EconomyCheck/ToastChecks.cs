using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;

// The game's toasts on the kit (4 Nov): the owner still saw the game's own toast although the state line said "notice cards (drawn over the
// game's screen)". The toast stack's object (NotificationsUI.container) is switched on by the game's Start and stays on, so the watcher the host
// puts on it ran its OnEnable inside AddComponent, before the host was set on it, and nothing ever told the host to show the cards. These checks
// hold the rule that shows the kit at the bind, which entries of the stack are toasts, that a card's press reaches the entry it was drawn for and
// no other kind of action does (a recording source, rule 13 of the wave 4 master), and the once-a-session line that proves a toast was drawn.
internal static partial class Program
{
    private static void ToastChecks()
    {
        ToastBindChecks();
        ToastStackChecks();
        ToastPressChecks();
        ToastSourceChecks();
    }

    // The host shows the kit at the bind over an object that is on all the time, and only for such a screen.
    private static void ToastBindChecks()
    {
        Check(UiHostRules.ShowsAtBind(true, true), "toasts bind: a screen whose object stands on all the time, bound while it is on, shows the kit at once");
        Check(!UiHostRules.ShowsAtBind(true, false) && !UiHostRules.ShowsAtBind(false, true) && !UiHostRules.ShowsAtBind(false, false),
            "toasts bind: a screen the game opens later, or a standing object that is off, waits for the game to switch it on");
        // The order the engine runs things in: AddComponent runs OnEnable at once, with no host on the watcher yet, so the first Opened is never heard.
        var heard = new List<string>();
        bool hostSet = false;
        Action onEnable = () => heard.Add(hostSet ? "opened" : "lost");
        onEnable();   // AddComponent on an object that is on
        hostSet = true;
        if (UiHostRules.ShowsAtBind(true, true))
        {
            onEnable();   // the bind's own Opened
        }
        Check(heard.Count == 2 && heard[0] == "lost" && heard[1] == "opened", "toasts bind: the OnEnable of a watcher added to an object that is on is lost, so the bind opens the host itself (" + string.Join(", ", heard) + ")");
    }

    // The game's stack as the screen reads it: a template and another mod's child are not toasts; the kit's verdict line.
    private static void ToastStackChecks()
    {
        // A stack as the game leaves it: its four templates off, two toasts, a child another mod added without the game's Text.
        (string name, bool on, bool text)[] stack =
        {
            ("SuccessTemplate", false, true), ("ErrorTemplate", false, true), ("InfoTemplate", false, true), ("WarningTemplate", false, true),
            ("Info(Clone)", true, true), ("Other mod's badge", true, false), ("Warning(Clone)", true, true)
        };
        var toasts = new List<string>();
        foreach ((string name, bool on, bool text) e in stack)
        {
            if (UiNoticeRules.IsToast(e.on, e.text))
            {
                toasts.Add(e.name);
            }
        }
        Check(toasts.Count == 2 && toasts[0] == "Info(Clone)" && toasts[1] == "Warning(Clone)", "toasts stack: the entries on with the game's Text are the toasts, oldest first; the four templates and another mod's child are not (" + string.Join(", ", toasts) + ")");
        Check(UiNoticeRules.Wrong(2, 2, 2, true) == null, "toasts line: every toast drawn and its own entry unseen is a pass");
        Check(UiNoticeRules.Wrong(2, 2, 2, false) != null && UiNoticeRules.Wrong(2, 2, 2, false).Contains("not showing"), "toasts line: cards bound but not showing is a failure that says so");
        Check(UiNoticeRules.Wrong(2, 1, 2, true) != null && UiNoticeRules.Wrong(2, 1, 2, true).Contains("1 of 2"), "toasts line: one entry of the game's still seen is a failure that counts it");
        Check(UiNoticeRules.Wrong(3, 3, 0, true) != null && UiNoticeRules.Wrong(0, 0, 0, true) != null, "toasts line: a toast with no card drawn is a failure");
    }

    // A recording source: it knows which kind of action each drawn number is and fails when one reaches the stack as another kind or for an entry the card was not drawn for.
    private sealed class ToastRecorder
    {
        private readonly Dictionary<int, string> _kinds = new Dictionary<int, string>();
        public readonly List<string> Wrong = new List<string>();
        public readonly List<int> Clicked = new List<int>();
        public int Pressed;

        public ToastRecorder(params (string name, int command)[] registry)
        {
            foreach ((string name, int command) in registry)
            {
                if (_kinds.TryGetValue(command, out string other))
                {
                    Wrong.Add("number " + command + " is both " + other + " and " + name);
                }
                else
                {
                    _kinds[command] = name;
                }
            }
        }

        // The screen's Press as ShellNotices wires it: the rule picks the entry, the entry's button is clicked.
        public void Press(int command, int arg, int entries, int wantEntry)
        {
            Pressed++;
            string kind = _kinds.TryGetValue(command, out string k) ? k : "?";
            int at = UiNoticeRules.EntryFor(command, arg, entries);
            if (kind == "card")
            {
                if (at != wantEntry)
                {
                    Wrong.Add("a card for entry " + wantEntry + " reaches entry " + at);
                }
                else if (at >= 0)
                {
                    Clicked.Add(at);
                }
            }
            else if (at >= 0)
            {
                Wrong.Add("a " + kind + " action (" + command + ", " + arg + ") reaches entry " + at + " as a card");
            }
        }
    }

    // Every action the cards draw is pressed at the recorder, and an action of the list reaches no entry.
    private static void ToastPressChecks()
    {
        var recorder = new ToastRecorder(("card", UiNotices.CardCommand), ("clear", UiNotices.ClearCommand));
        Check(recorder.Wrong.Count == 0, "toasts press: the card and the clear actions have numbers of their own (" + string.Join("; ", recorder.Wrong) + ")");
        UiWords english = (key, en) => en;
        int drawnCards = 0, states = 0;
        foreach (int count in new[] { 1, 2, 3, 5, 9 })
        {
            UiNoticeCardsModel model = UiSamples.NoticeCards(english, 0, count, false);
            var scene = new UiScene();
            UiNotices.LayCards(scene, model, UiFrame.FullMenu(16f / 9f, "Notices"), true, UiDefaultKeys.Keyboard);
            states++;
            int from = Math.Max(0, count - UiNotices.MostCards);
            var seen = new List<int>();
            for (int i = 0; i < scene.NodeCount; i++)
            {
                UiNode n = scene.Nodes[i];
                // the k-th card drawn is the toast at place from + k in the stack: its entry is that one
                recorder.Press(n.Command, n.Arg, count, from + seen.Count);
                seen.Add(n.Arg);
                drawnCards++;
            }
            Check(seen.Count == count - from && (seen.Count == 0 || (seen[0] == from && seen[seen.Count - 1] == count - 1)),
                "toasts press: with " + count + " toasts the newest " + (count - from) + " are drawn as cards numbered by their entry in the stack (" + string.Join(", ", seen) + ")");
            // the list's Clear all arrives at the stack as a card: it must reach no entry
            recorder.Press(UiNotices.ClearCommand, 0, count, -1);
            recorder.Press(UiNotices.CardCommand, count, count, -1);   // a card past the end: no entry
            recorder.Press(UiNotices.CardCommand, -1, count, -1);
        }
        Check(recorder.Wrong.Count == 0 && drawnCards >= 12 && recorder.Clicked.Count == drawnCards,
            "toasts press: every card drawn in " + states + " states clicks the entry it was drawn for, and clear, a card past the end and a negative one click none (" + drawnCards + " cards; " + string.Join("; ", recorder.Wrong) + ")");
    }

    // The wiring that cannot run without the game, read from the source: the bind shows the kit, the pieces go unseen as the toast arrives, one hold, one line.
    private static void ToastSourceChecks()
    {
        string root = FindRoot();
        string ui = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui");
        string host = File.ReadAllText(System.IO.Path.Combine(ui, "Host", "UiGameHost.cs"));
        string notices = File.ReadAllText(System.IO.Path.Combine(ui, "Shell", "ShellNotices.cs"));
        int install = host.IndexOf("public void Install()", StringComparison.Ordinal);
        int bind = host.IndexOf("UiHostRules.ShowsAtBind(_screen is IUiGameStands, found.gameObject.activeInHierarchy)", StringComparison.Ordinal);
        int watch = host.IndexOf("_watch.Host = this;", StringComparison.Ordinal);
        Check(install >= 0 && watch > install && bind > watch && host.IndexOf("Opened();", bind, StringComparison.Ordinal) > bind,
            "toasts source: the host opens the kit at the bind, after the watcher has its host, for a screen that stands on all the time");
        Check(notices.Contains("class CardsBox : IUiGameScreen, IUiGameStands, IUiGameOffers") && notices.Contains("Kind => UiGameKind.Overlay"),
            "toasts source: the toast cards are an overlay that stands on all the time and offers other mods' children itself");
        Check(notices.Contains("_live.Arrived(newest)") && notices.Contains("new NoticeCardsScreen(found.transform, _conceal)") && !notices.Contains("new UiConceal()"),
            "toasts source: a toast's pieces go unseen the moment the game makes it, in the host's own hold, so switching the cards off gives the game's toast back");
        Check(notices.Contains("Notice check (cards): ") && notices.Contains("the game's own entry hidden in ") && notices.Contains("_cardsSaid") && notices.Contains("UiNoticeRules.Wrong("),
            "toasts source: the first toast drawn as a card writes one line that counts the game's entries hidden");
        Check(!notices.Contains("FindObjects") && !notices.Contains("FindObjectsOfType") && notices.Contains("Every = 0.25f"),
            "toasts source: the cards search nothing over the scene and look at the stack four times a second while it holds a toast");
    }
}
