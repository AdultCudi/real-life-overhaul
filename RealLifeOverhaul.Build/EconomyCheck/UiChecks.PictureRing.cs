using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The owner, 11 Nov (beta 14): the game closed with a stack overflow as a street taxi was clicked, the city map's first opening of the
// session, in the second city loaded. The kit's picture finder was a chain of answers each holding the one before it, and the phone's
// (the camera's too) read the one before from a field it set again whenever another answer stood over its own: the second city's phone
// closed a ring, and the map's first picture went round it until the stack ran out. The pictures are now parts asked in turn
// (UiAnswers), each added once a session; and a question, a layout and a game screen's opening are each refused when they come back into
// themselves (UiReentry), said once a session with the stack. Run: --picture-ring; the lines read "picture ring: ..." and "re-entry: ...".
internal static partial class Program
{
    private const int RingCap = 64;   // a stand-in this deep has gone round: stopped there, so its check fails and never runs the stack out

    private static void UiPictureRingChecks()
    {
        Action<string> said = UiReentry.Said;
        try
        {
            RingOldChain();
            RingAnswers();
            RingSelfAsking();
            ReentryHostChecks();
            RingSources();
        }
        finally
        {
            UiReentry.Said = said;
            UiReentry.Forget();
        }
    }

    // ---------------------------------------------------------------- the session as it was

    private static Func<string, string> _oldFind, _oldPhoneBefore, _oldPhone, _oldWorld;
    private static int _oldDepth, _oldDeepest;

    // The owner's session on the old chain, as its code did it: city 1 adds the map's answer (once a session), then the phone's, then a world
    // window's over it; city 2 binds the phone again. The stand-in stops at RingCap, so this shows the old way rang and never reached the map.
    private static void RingOldChain()
    {
        _oldFind = null;
        _oldPhone = null;
        _oldWorld = null;
        Func<string, string> mapBefore = _oldFind;
        _oldFind = key => key.StartsWith("map:", StringComparison.Ordinal) ? "the map's line" : mapBefore?.Invoke(key);
        OldPhone();
        OldWorld();
        OldPhone();
        _oldDeepest = 0;
        string found = _oldFind("map:line:3");
        Check(_oldDeepest >= RingCap && found == null,
            "picture ring: the old chain went round " + _oldDeepest + " deep at the second city's first map picture and never reached the map's answer, as in the owner's crash");
    }

    // PhoneKit.HookPictures as it was: the answer before its own read from a field, set again whenever its own was not on top.
    private static void OldPhone()
    {
        if (_oldPhone != null && _oldFind == _oldPhone)
        {
            return;
        }
        _oldPhoneBefore = _oldFind;
        _oldPhone = key =>
        {
            if (key.StartsWith("phone:", StringComparison.Ordinal))
            {
                return "a mod's app icon";
            }
            _oldDepth++;
            _oldDeepest = Math.Max(_oldDeepest, _oldDepth);
            try
            {
                return _oldDepth >= RingCap ? null : _oldPhoneBefore?.Invoke(key);
            }
            finally
            {
                _oldDepth--;
            }
        };
        _oldFind = _oldPhone;
    }

    // WorldPictures.On as it was: wrapped again over whatever stood on top.
    private static void OldWorld()
    {
        if (_oldWorld != null && _oldFind == _oldWorld)
        {
            return;
        }
        Func<string, string> before = _oldFind;
        _oldWorld = key => key.StartsWith("world:", StringComparison.Ordinal) ? "a world sprite" : before?.Invoke(key);
        _oldFind = _oldWorld;
    }

    // ---------------------------------------------------------------- the same session on the parts

    private static int _ringAsked;

    private static string RingPhone(string key)
    {
        _ringAsked++;
        return key.StartsWith("phone:", StringComparison.Ordinal) ? "a mod's app icon" : null;
    }

    private static string RingWorld(string key)
    {
        _ringAsked++;
        return key.StartsWith("world:", StringComparison.Ordinal) ? "a world sprite" : null;
    }

    private static void RingAnswers()
    {
        UiReentry.Forget();
        var said = new List<string>();
        UiReentry.Said = said.Add;
        var pictures = new UiAnswers<string>("stand-in pictures", 20);
        bool first = pictures.Add(key => key.StartsWith("map:", StringComparison.Ordinal) ? "the map's line" : null) & pictures.Add(RingPhone) & pictures.Add(RingWorld);
        bool again = pictures.Add(RingPhone) | pictures.Add(RingWorld) | pictures.Add(null);
        Check(first && !again && pictures.Count == 3, "picture ring: each part is in once, however often a city binds it again (" + pictures.Count + " parts)");
        _ringAsked = 0;
        string map = pictures.Ask("map:line:3");
        Check(map == "the map's line" && _ringAsked == 2,
            "picture ring: in the second city the map's first picture is answered, every part asked once on the way (" + _ringAsked + " asked before the map's)");
        Check(pictures.Ask("phone:2") == "a mod's app icon" && pictures.Ask("world:7") == "a world sprite" && pictures.Ask("dialog:face:1") == null && pictures.Ask(null) == null,
            "picture ring: each part answers its own keys, and a key no part has is no picture, not a question passed round");
        Check(said.Count == 0 && UiReentry.Refusals == 0, "picture ring: a plain session refuses nothing and says nothing");
    }

    // A part that asks the question again from inside its answer, the ring at its shortest: refused at once, and the parts under it still answer.
    private static void RingSelfAsking()
    {
        UiReentry.Forget();
        var said = new List<string>();
        UiReentry.Said = said.Add;
        var pictures = new UiAnswers<string>("stand-in pictures", 4);
        int depth = 0, deepest = 0;
        pictures.Add(key => key.StartsWith("map:", StringComparison.Ordinal) ? "the map's line" : null);
        pictures.Add(key =>
        {
            depth++;
            deepest = Math.Max(deepest, depth);
            try
            {
                return depth >= RingCap ? "gone round" : pictures.Ask(key);
            }
            finally
            {
                depth--;
            }
        });
        string first = pictures.Ask("map:line:3"), second = pictures.Ask("map:line:4");
        Check(deepest == 1 && first == "the map's line" && second == "the map's line",
            "picture ring: a part asking the question again from inside its answer is refused at once (" + deepest + " deep) and the part under it answers");
        Check(UiReentry.Refusals == 2 && said.Count == 1 && said[0].Contains("stand-in pictures") && said[0].Contains("map:line:3") && said[0].Contains("RingSelfAsking"),
            "picture ring: every refusal is counted and the first alone is said, with the stack that came back in (" + said.Count + " said, " + UiReentry.Refusals + " refused)");
    }

    // ---------------------------------------------------------------- the host's layout and a game screen's opening

    // A stand-in of UiHost.Layout and UiGameHost.Opened, guarded the way they are (RingSources holds the real ones to this shape). Its
    // screen lays itself out again from inside the layout, and the bringing switches the game's screen off and on, so its watcher opens it
    // again from inside the opening. Each stops at RingCap, so without the guard the checks fail and never run the stack out.
    private sealed class ReentryStandIn
    {
        private readonly UiReentry _laying = new UiReentry(), _opening = new UiReentry();
        private readonly bool _guarded;
        private bool _layAgain;
        public bool Stale, Fault;
        public int Depth, Deepest, Laid, Brought;

        public ReentryStandIn(bool guarded)
        {
            _guarded = guarded;
        }

        public void Layout()
        {
            if (_guarded && !_laying.Enter())
            {
                _layAgain = true;
                UiReentry.Refused("stand-in UiHost.Layout", "map");
                return;
            }
            try
            {
                Laid++;
                Deeper(Layout);
            }
            finally
            {
                if (_guarded)
                {
                    _laying.Leave();
                }
                if (_layAgain)
                {
                    _layAgain = false;
                    Stale = true;
                }
            }
        }

        public void Opened()
        {
            if (_guarded && !_opening.Enter())
            {
                UiReentry.Refused("stand-in UiGameHost.Opened", "map");
                return;
            }
            try
            {
                Brought++;
                Deeper(Opened);
            }
            finally
            {
                if (_guarded)
                {
                    _opening.Leave();
                }
            }
        }

        private void Deeper(Action again)
        {
            Depth++;
            Deepest = Math.Max(Deepest, Depth);
            try
            {
                if (Fault)
                {
                    Fault = false;
                    throw new InvalidOperationException("a fault inside the call");
                }
                if (Depth < RingCap)
                {
                    again();
                }
            }
            finally
            {
                Depth--;
            }
        }
    }

    private static void ReentryHostChecks()
    {
        UiReentry.Forget();
        var said = new List<string>();
        UiReentry.Said = said.Add;
        var host = new ReentryStandIn(true);
        host.Layout();
        Check(host.Deepest == 1 && host.Laid == 1 && host.Stale,
            "re-entry: a layout asked for from inside the layout ends at once (" + host.Deepest + " deep) and the screen is marked to be laid out again next frame");
        host.Deepest = 0;
        host.Layout();
        Check(host.Laid == 2 && host.Deepest == 1, "re-entry: the next frame lays the screen out again, the guard left at the end of every layout");
        host.Deepest = 0;
        host.Opened();
        Check(host.Deepest == 1 && host.Brought == 1, "re-entry: an opening set off from inside the opening ends at once (" + host.Deepest + " deep), the kit brought once");
        host.Fault = true;
        try
        {
            host.Layout();
        }
        catch (InvalidOperationException)
        {
            // the fault goes on to the caller, as a layout's does to the host's frame guard
        }
        int laid = host.Laid;
        host.Layout();
        Check(host.Laid == laid + 1, "re-entry: a fault inside a layout leaves the guard on its way out, so the next layout runs");
        Check(said.Count == 2 && UiReentry.Refusals == 4 && said[0].Contains("stand-in UiHost.Layout") && said[0].Contains("ReentryStandIn") && said[1].Contains("stand-in UiGameHost.Opened"),
            "re-entry: each place says its first refusal once a session with the stack that came back in, never again (" + said.Count + " said, " + UiReentry.Refusals + " refused)");
        var bare = new ReentryStandIn(false);
        bare.Layout();
        bare.Opened();
        Check(bare.Laid >= RingCap && bare.Brought >= RingCap, "re-entry: the stand-in without the guard goes round " + bare.Laid + " deep, so the checks above fail without it");
    }

    // ---------------------------------------------------------------- the code as it ships

    private static void RingSources()
    {
        string host = Read(@"Ui\UiHost.cs"), gameHost = Read(@"Ui\Host\UiGameHost.cs");
        Check(LayoutGuarded(host) && !LayoutGuarded(host.Replace("if (!_laying.Enter())", "if (false)")) && !LayoutGuarded(host.Replace("_laidSize = Vector2.zero;   // laid out again next frame, with", "//")),
            "re-entry: UiHost.Layout refuses a layout from inside its own, marks the screen to be laid next frame and leaves the guard on every way out (and the rule fails on a copy without it)");
        Check(OpenedGuarded(gameHost) && !OpenedGuarded(gameHost.Replace("_opening.Leave();", string.Empty)),
            "re-entry: UiGameHost.Opened refuses an opening from inside its own and leaves the guard on every way out (and the rule fails on a copy without it)");
        Check(Read(@"Ui\UiDirector.cs").Contains("UiReentry.Said = line => Logger?.Warn(line);"), "re-entry: a refusal is said through the mod's logger");

        // No part holds the finder or another part's answer, and only the renderer asks it, so no answer can lead back into another.
        string scripts = PhotoScripts, preview = System.IO.Path.Combine(RepoRoot(), "RealLifeOverhaul.Build", "UiPreview");
        var held = new List<string>();
        var askers = new List<string>();
        var loose = new List<string>();
        int parts = 0;
        foreach (string root in new[] { scripts, preview })
        {
            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path), name = System.IO.Path.GetFileName(path);
                if (Regex.IsMatch(text, @"UiPictures\.Find\s*(?:[=!]=?|;|\))") || Regex.IsMatch(text, @"Func<string,\s*Sprite>\s+(?:before|_before|_next|_picturesBefore|_pictures|_mine)\b"))
                {
                    held.Add(name);
                }
                if (Regex.IsMatch(text, @"UiPictures\.Find\(") && name != "UiView.Slots.cs")
                {
                    askers.Add(name);
                }
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains("UiPictures.Answer(") || lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    parts++;
                    bool once = false;
                    for (int j = i - 1; j >= Math.Max(0, i - 3) && !once; j--)
                    {
                        once = Regex.IsMatch(lines[j], @"^\s*_\w+ = true;");
                    }
                    if (!once && !path.StartsWith(preview, StringComparison.OrdinalIgnoreCase))
                    {
                        loose.Add(name + ":" + (i + 1));
                    }
                }
            }
        }
        Check(held.Count == 0, "picture ring: no file sets, compares or keeps the picture finder, or keeps the answer before its own (" + string.Join(", ", held) + ")");
        Check(askers.Count == 0, "picture ring: only the renderer asks for a picture, never an answer, so no answer leads into another (" + string.Join(", ", askers) + ")");
        Check(parts >= 14 && loose.Count == 0, "picture ring: every part of the mod (" + parts + " in the mod and the preview) is added behind its once a session flag, never again a frame or a city (" + string.Join(", ", loose) + ")");
        string pictures = Read(@"Ui\UiPictures.cs");
        Check(pictures.Contains("public static Sprite Find(string key) => Answers.Ask(key);") && pictures.Contains("public static void Answer(Func<string, Sprite> part)") && !pictures.Contains("public static Func<string, Sprite> Find"),
            "picture ring: the finder is a question the parts answer in turn, no longer a hook each part wraps");
    }

    // UiHost.Layout: the guard entered first, a refused layout marked to be laid next frame, the guard left and the mark taken in a finally, and
    // the layout's work (the screen laid) only behind it.
    private static bool LayoutGuarded(string host)
    {
        string guard = Between(host, "private void Layout()", "private void LayAndDraw()"), work = Between(host, "private void LayAndDraw()", "private int FindRow(");
        int enter = guard.IndexOf("if (!_laying.Enter())", StringComparison.Ordinal), lay = guard.IndexOf("LayAndDraw();", StringComparison.Ordinal);
        int leave = guard.IndexOf("_laying.Leave();", StringComparison.Ordinal), fin = guard.IndexOf("finally", StringComparison.Ordinal);
        return enter >= 0 && lay > enter && fin > lay && leave > fin && guard.Contains("_layAgain = true;") && guard.Contains("UiReentry.Refused(\"UiHost.Layout\"")
               && guard.IndexOf("_laidSize = Vector2.zero;", StringComparison.Ordinal) > leave && work.Contains("_screen.Lay(") && Regex.Matches(host, @"\bLayAndDraw\(\);").Count == 1;
    }

    // UiGameHost.Opened: the guard entered before the kit is brought, and left in a finally.
    private static bool OpenedGuarded(string gameHost)
    {
        string opened = Between(gameHost, "internal void Opened()", "internal void Closed()");
        int enter = opened.IndexOf("if (!_opening.Enter())", StringComparison.Ordinal), bring = opened.IndexOf("Bring();", StringComparison.Ordinal);
        int fin = opened.LastIndexOf("finally", StringComparison.Ordinal), leave = opened.IndexOf("_opening.Leave();", StringComparison.Ordinal);
        return enter >= 0 && bring > enter && fin > bring && leave > fin && opened.Contains("UiReentry.Refused(\"UiGameHost.Opened\"");
    }
}
