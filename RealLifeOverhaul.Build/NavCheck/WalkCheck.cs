using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Walking;

// Walking on a key (Walking/WalkRules.cs): the toggle and what stops it, the speed put on and when it is given back, and
// the walk cycle it plays. Checked on a model of the game's own loop, frame by frame, as the decompiled game has it:
// PlayerController.Update picks a pace (ThirdPersonCharacter.ToggleRunning: run outdoors, jog indoors or while Toggle
// Running is held outdoors, jog with no energy) and writes that pace's speed into the agent only when the pace changes
// (SetWalkingSpeed); leaving a vehicle writes it again a frame later with the pace unchanged; a job of the game's and
// Going Public's time skip put speeds of their own on the agent. Then, over the sources, that walking reads one key, sends
// nothing, saves nothing and touches the game only through WalkGame, and that its texts are in the three languages.
internal static partial class Program
{
    // The game's figures on foot: GlobalReferences' own values (BigAmbitions/GlobalReferences.cs, walkingSpeed* and animationSpeed*).
    private static readonly WalkFigures GameFeet = new WalkFigures
    {
        Zombie = 1.5f, Walk = 1.2f, WalkFast = 1.8f, Jog = 4f, Run = 6f, Scooter = 12f,
        AnimWalk = 0.75f, AnimWalkFast = 1f, AnimJog = 1.3f, AnimRun = 0.85f,
    };

    private const float WalkFrame = 1f / 60f;   // s: one frame of the model

    // The player's body as the game drives it, and walking's director over it, one frame at a time.
    private sealed class WalkModel
    {
        public WalkFigures F = GameFeet;
        public WalkPace Pace = WalkPace.Run;
        public float Agent;
        public bool Indoors, ShiftHeld, NoEnergy, InVehicle, Busy;
        public float Now;
        public int Looks;          // how many times walking decided anything
        public int Writes;         // speeds walking wrote (a walk put on, or the game's given back)

        // The director's state (Walking/WalkDirector.cs).
        public bool Walking;
        public float Seen = -1f;
        public bool Again;
        public float LookAt;

        public WalkModel()
        {
            Agent = WalkRules.GameSpeed(Pace, F);
        }

        // ThirdPersonCharacter.ToggleRunning as PlayerController.Update calls it (runByDefaultIndoors off, the game's default).
        public void GameFrame()
        {
            bool running = Indoors ? ShiftHeld : !ShiftHeld;
            WalkPace want = NoEnergy || !running ? WalkPace.Jog : WalkPace.Run;
            if (Busy)
            {
                return;   // (an activity: ToggleRunning stands aside)
            }
            if (want != Pace)
            {
                Pace = want;
                Agent = WalkRules.GameSpeed(Pace, F);   // SetWalkingSpeed
            }
        }

        // SetWalkingSpeed with the pace unchanged: a vehicle left (a frame later), a job of the game's ended.
        public void GameRewrites()
        {
            Agent = WalkRules.GameSpeed(Pace, F);
        }

        public void Press()
        {
            if (WalkRules.CanToggle(true, true, false, InVehicle, Busy) != WalkRefusal.None)
            {
                return;
            }
            if (!WalkRules.Toggled(Walking))
            {
                if (WalkRules.Near(Agent, WalkRules.WalkSpeed(F)))
                {
                    GameRewrites();
                    Writes++;
                }
                Walking = false;
                return;
            }
            Walking = true;
            Look();
        }

        public void Load()
        {
            Walking = WalkRules.AfterLoad();
            Pace = WalkPace.Run;
            Agent = WalkRules.GameSpeed(Pace, F);
            Seen = -1f;
            Again = false;
        }

        // WalkDirector.Keep: look only when the speed differs from the last looked at, or a lasting reason may have ended.
        public void ModFrame()
        {
            if (Walking && (Agent != Seen || (Again && Now >= LookAt)))
            {
                Look();
            }
        }

        private void Look()
        {
            Looks++;
            WalkStep step = WalkRules.Decide(Walking, !InVehicle, Busy, ShiftHeld, Pace, Agent, F, out bool again);
            Again = again;
            LookAt = Now + WalkRules.LookAgain;
            switch (step)
            {
                case WalkStep.Hold:
                    Agent = WalkRules.WalkSpeed(F);
                    Writes++;
                    break;
                case WalkStep.GiveBack:
                    GameRewrites();
                    Writes++;
                    break;
            }
            Seen = Agent;
        }

        public void Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                Now += WalkFrame;
                GameFrame();
                ModFrame();
            }
        }

        public bool Walks => WalkRules.Near(Agent, WalkRules.WalkSpeed(F));
        public bool AtGamePace => WalkRules.Near(Agent, WalkRules.GameSpeed(Pace, F));
    }

    private static void Walks()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        WalkToggleChecks();
        WalkSpeedChecks();
        WalkLoopChecks();
        WalkCycleChecks();
        WalkSourceChecks(scripts);
    }

    // ---------------------------------------------------------------- the toggle and what stops it

    private static void WalkToggleChecks()
    {
        Check(WalkRules.Toggled(false) && !WalkRules.Toggled(true), "walking: a press walks from the usual pace and gives the usual pace back from a walk");
        Check(WalkRules.Toggled(WalkRules.Toggled(false)) == false, "walking: two presses are where the player began");
        Check(!WalkRules.AfterLoad(), "walking: a loaded save starts at the game's own pace (the game's pace and auto-run are not saved either)");
        Check(WalkRules.CanToggle(true, true, false, false, false) == WalkRefusal.None, "walking: on foot with nothing open, a press switches");
        Check(WalkRules.CanToggle(false, true, false, false, false) == WalkRefusal.Off, "walking: with its option off the key does nothing");
        Check(WalkRules.CanToggle(true, false, false, false, false) == WalkRefusal.NotLoaded, "walking: outside a city the key does nothing");
        Check(WalkRules.CanToggle(true, true, true, false, false) == WalkRefusal.Window, "walking: typing in a box or a menu open, the key does nothing");
        Check(WalkRules.CanToggle(true, true, false, true, false) == WalkRefusal.Vehicle, "walking: in a vehicle the key does nothing");
        Check(WalkRules.CanToggle(true, true, false, false, true) == WalkRefusal.Busy, "walking: seated or held still the key does nothing");
        Check(WalkRules.CanToggle(false, false, true, true, true) == WalkRefusal.Off && WalkRules.CanToggle(true, false, true, true, true) == WalkRefusal.NotLoaded
              && WalkRules.CanToggle(true, true, true, true, true) == WalkRefusal.Window && WalkRules.CanToggle(true, true, false, true, true) == WalkRefusal.Vehicle,
              "walking: the first reason that holds is the one given");
        int none = 0;
        for (int bits = 0; bits < 32; bits++)
        {
            bool on = (bits & 1) != 0, loaded = (bits & 2) != 0, window = (bits & 4) != 0, vehicle = (bits & 8) != 0, busy = (bits & 16) != 0;
            if (WalkRules.CanToggle(on, loaded, window, vehicle, busy) == WalkRefusal.None)
            {
                none++;
                Check(on && loaded && !window && !vehicle && !busy, "walking: a press switches only with the option on, a city loaded and nothing in the way (case " + bits + ")");
            }
        }
        Check(none == 1, "walking: exactly one of the 32 cases lets a press switch: " + none);
    }

    // ---------------------------------------------------------------- the speed put on

    private static void WalkSpeedChecks()
    {
        WalkFigures f = GameFeet;
        float walk = WalkRules.WalkSpeed(f);
        Check(Near(walk, f.Walk, 0.0001f), "walking: the walk is the game's own walk (" + f.Walk + " m/s): " + walk);
        Check(walk < f.Jog && walk < f.Run && walk < f.WalkFast, "walking: the walk is slower than the game's jog, run and brisk walk");
        WalkFigures slow = f;
        slow.Walk = 0.3f;
        WalkFigures fast = f;
        fast.Walk = 3f;
        WalkFigures crowded = f;
        crowded.Walk = 1.2f;
        crowded.Jog = 1.0f;
        Check(Near(WalkRules.WalkSpeed(slow), WalkRules.SlowestWalk, 0.0001f), "walking: a game walk below " + WalkRules.SlowestWalk + " m/s is held at it");
        Check(Near(WalkRules.WalkSpeed(fast), WalkRules.FastestWalk, 0.0001f), "walking: a game walk above " + WalkRules.FastestWalk + " m/s is held at it");
        Check(WalkRules.WalkSpeed(crowded) == 0f, "walking: a game whose jog is no faster than a walk has no walk (walking does nothing)");
        Check(WalkRules.WalkSpeed(default) == 0f, "walking: no figures (outside a city) give no walk");

        // Each pace's speed is the game's own (ThirdPersonCharacter.GetSpeed).
        Check(WalkRules.GameSpeed(WalkPace.Run, f) == 6f && WalkRules.GameSpeed(WalkPace.Jog, f) == 4f && WalkRules.GameSpeed(WalkPace.Walk, f) == 1.2f
              && WalkRules.GameSpeed(WalkPace.Zombie, f) == 1.5f && WalkRules.GameSpeed(WalkPace.Scooter, f) == 12f, "walking: each pace's speed is the game's GetSpeed");

        // Decide, case by case.
        bool again;
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Run, 6f, f, out again) == WalkStep.Hold && !again, "walking: outdoors at the game's run, the walk goes on");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Jog, 4f, f, out again) == WalkStep.Hold && !again, "walking: indoors at the game's jog, the walk goes on");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Run, walk, f, out again) == WalkStep.Leave && !again, "walking: the walk already on is left as it is");
        Check(WalkRules.Decide(true, true, false, true, WalkPace.Jog, 4f, f, out again) == WalkStep.Leave && again, "walking: Toggle Running held, the game's own pace stands, looked at again");
        Check(WalkRules.Decide(true, true, false, true, WalkPace.Run, walk, f, out again) == WalkStep.GiveBack && again, "walking: Toggle Running held on a walk, the game's own pace is given back");
        Check(WalkRules.Decide(true, false, false, false, WalkPace.Run, walk, f, out again) == WalkStep.GiveBack && again, "walking: in a vehicle, the walk is given back and the walk waits");
        Check(WalkRules.Decide(true, true, true, false, WalkPace.Run, walk, f, out again) == WalkStep.GiveBack && again, "walking: seated or held still, the walk is given back and the walk waits");
        Check(WalkRules.Decide(true, true, true, false, WalkPace.Run, 3.3f, f, out again) == WalkStep.Leave && again, "walking: a job's own pace while busy is left as it is");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Zombie, 1.5f, f, out again) == WalkStep.Leave, "walking: the game's zombie walk is left as it is");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Scooter, 12f, f, out again) == WalkStep.Leave, "walking: a scooter's pace is left as it is");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Walk, 1.2f, f, out again) == WalkStep.Leave, "walking: the game's own walk pace is left as it is");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Run, 18f, f, out again) == WalkStep.Leave,
              "walking: another speed on the agent (Going Public's time skip, three times the run) is never written over");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Run, 3.3f, f, out again) == WalkStep.Leave, "walking: another speed on the agent (a job of the game's) is never written over");
        Check(WalkRules.Decide(false, true, false, false, WalkPace.Run, walk, f, out again) == WalkStep.GiveBack && !again, "walking: off, a walk on the agent is given back");
        Check(WalkRules.Decide(false, true, false, false, WalkPace.Run, 6f, f, out again) == WalkStep.Leave, "walking: off, the game's own speed is left as it is");
        Check(WalkRules.Decide(true, true, false, false, WalkPace.Run, 6f, crowded, out again) == WalkStep.Leave, "walking: with no walk slower than the jog nothing is written");

        // Over every pace, every speed the game writes and every reason: a walk goes on only over the game's own run or jog,
        // only on foot and free, and nothing faster than the game's pace is ever written.
        foreach (WalkPace pace in Enum.GetValues(typeof(WalkPace)))
        {
            foreach (float agent in new[] { 0f, walk, 1.5f, 3.3f, 4f, 6f, 12f, 18f })
            {
                for (int bits = 0; bits < 16; bits++)
                {
                    bool walking = (bits & 1) != 0, onFoot = (bits & 2) != 0, busy = (bits & 4) != 0, held = (bits & 8) != 0;
                    WalkStep step = WalkRules.Decide(walking, onFoot, busy, held, pace, agent, f, out again);
                    if (step == WalkStep.Hold)
                    {
                        Check(walking && onFoot && !busy && !held && (pace == WalkPace.Run || pace == WalkPace.Jog) && WalkRules.Near(agent, WalkRules.GameSpeed(pace, f)),
                              "walking: a walk goes on only over the game's own run or jog, on foot and free (" + pace + ", " + agent + " m/s, case " + bits + ")");
                        Check(walk < agent, "walking: a walk put on is slower than what it replaces (" + pace + ")");
                    }
                    if (step == WalkStep.GiveBack)
                    {
                        Check(WalkRules.Near(agent, walk), "walking: only a walk walking put there is given back (" + pace + ", " + agent + " m/s)");
                    }
                }
            }
        }

        // What a look costs: comparisons only.
        var clock = Stopwatch.StartNew();
        int holds = 0;
        for (int i = 0; i < 200000; i++)
        {
            if (WalkRules.Decide(true, true, false, (i & 7) == 0, WalkPace.Run, (i & 1) == 0 ? 6f : walk, f, out again) == WalkStep.Hold)
            {
                holds++;
            }
        }
        clock.Stop();
        Check(holds > 0 && clock.Elapsed.TotalMilliseconds < 100.0, "walking: 200,000 looks take well under a frame each (" + clock.Elapsed.TotalMilliseconds.ToString("F1") + " ms in all)");
    }

    // ---------------------------------------------------------------- frame by frame against the game's own loop

    private static void WalkLoopChecks()
    {
        float walk = WalkRules.WalkSpeed(GameFeet);

        // Outdoors: a press walks; the game's run keeps being chosen and walking is not asked again while nothing changes.
        var m = new WalkModel();
        m.Frames(10);
        Check(m.AtGamePace && m.Pace == WalkPace.Run && m.Looks == 0, "walking loop: before any press the game's run is on and walking looks at nothing");
        m.Press();
        m.Frames(1);
        Check(m.Walks, "walking loop: a press outdoors puts the walk on at once: " + m.Agent);
        int looks = m.Looks;
        m.Frames(6000);
        Check(m.Walks && m.Looks == looks, "walking loop: a hundred seconds of walking cost no look (the speed did not change): " + (m.Looks - looks));

        // Toggle Running held outdoors: the game's jog while held; let go, the walk again the next frame.
        m.ShiftHeld = true;
        m.Frames(2);
        Check(m.Pace == WalkPace.Jog && m.AtGamePace, "walking loop: Toggle Running held outdoors gives the game's own jog: " + m.Agent);
        m.Frames(120);
        Check(m.AtGamePace, "walking loop: the game's own pace holds for as long as the key is held");
        m.ShiftHeld = false;
        m.Frames(1);
        Check(m.Walks, "walking loop: Toggle Running let go, the walk is back the next frame: " + m.Agent);

        // Indoors (the game's jog), and Toggle Running held there (the game's run).
        m.Indoors = true;
        m.Frames(1);
        Check(m.Pace == WalkPace.Jog && m.Walks, "walking loop: through a door, indoors at the game's jog, still walking: " + m.Agent);
        m.ShiftHeld = true;
        m.Frames(1);
        Check(m.Pace == WalkPace.Run && m.AtGamePace, "walking loop: Toggle Running held indoors gives the game's own run: " + m.Agent);
        m.ShiftHeld = false;
        m.Frames(1);
        Check(m.Walks, "walking loop: let go indoors, walking again: " + m.Agent);
        m.Indoors = false;
        m.Frames(1);
        Check(m.Pace == WalkPace.Run && m.Walks, "walking loop: out of the door again, still walking");

        // A vehicle: the key does nothing in it; the game writes its pace again a frame after getting out, and the walk comes back.
        m.InVehicle = true;
        bool before = m.Walking;
        m.Press();
        Check(m.Walking == before, "walking loop: in a vehicle a press changes nothing");
        m.Frames(30);
        m.InVehicle = false;
        m.GameRewrites();
        m.Frames(1);
        Check(m.Walks, "walking loop: out of a vehicle the game writes its pace again and the walk is back: " + m.Agent);

        // A job of the game's puts its own pace on the agent while it is busy, then writes the game's back as it ends.
        m.Busy = true;
        m.Agent = 3.3f;
        m.Frames(60);
        Check(WalkRules.Near(m.Agent, 3.3f), "walking loop: a job's own pace is left as it is while it lasts: " + m.Agent);
        m.GameRewrites();
        m.Frames(3);
        Check(m.AtGamePace, "walking loop: the job ending gives the game's pace while still busy, left as it is");
        m.Busy = false;
        m.Frames((int)(WalkRules.LookAgain / WalkFrame) + 2);
        Check(m.Walks, "walking loop: once free, the walk is back within a quarter of a second, with the speed unchanged: " + m.Agent);

        // Going Public's time skip triples a working body's agent and gives the base back: never written over meanwhile.
        m.Agent = WalkRules.GameSpeed(m.Pace, m.F) * 3f;
        m.Frames(30);
        Check(WalkRules.Near(m.Agent, 18f), "walking loop: Going Public's paced speed is never written over: " + m.Agent);
        m.GameRewrites();
        m.Frames(1);
        Check(m.Walks, "walking loop: the skip's pace given back, the walk is back: " + m.Agent);

        // No energy: the game's jog whatever the key, and walking over it.
        m.NoEnergy = true;
        m.Frames(1);
        Check(m.Pace == WalkPace.Jog && m.Walks, "walking loop: out of energy the game jogs and walking walks: " + m.Agent);
        m.NoEnergy = false;
        m.Frames(1);
        Check(m.Pace == WalkPace.Run && m.Walks, "walking loop: energy back, the game runs and walking walks: " + m.Agent);

        // A press again: the game's own pace, exactly, and nothing more is looked at.
        m.Press();
        m.Frames(1);
        Check(!m.Walking && m.AtGamePace && WalkRules.Near(m.Agent, 6f), "walking loop: a press again gives back the game's own run: " + m.Agent);
        looks = m.Looks;
        m.ShiftHeld = true;
        m.Frames(30);
        m.ShiftHeld = false;
        m.Frames(30);
        Check(m.Looks == looks && m.AtGamePace, "walking loop: off, walking never looks and the game's pace is untouched");

        // A load ends walking: the new body is at the game's own pace.
        m.Press();
        m.Frames(1);
        Check(m.Walks, "walking loop: walking again before a load");
        m.Load();
        m.Frames(5);
        Check(!m.Walking && m.AtGamePace && m.Pace == WalkPace.Run, "walking loop: after a load the player runs at the game's own pace");

        // Seated: a press does nothing, and walking already on carries on after.
        m.Press();
        m.Frames(1);
        m.Busy = true;
        m.Press();
        Check(m.Walking, "walking loop: seated, a press does not end walking");
        m.Frames(600);
        m.Busy = false;
        m.Frames(20);
        Check(m.Walks, "walking loop: up again, still walking: " + m.Agent);
        Check(walk > 0f && m.Writes < 30, "walking loop: walking wrote the agent only on changes (" + m.Writes + " writes in the whole run)");
    }

    // ---------------------------------------------------------------- the walk cycle

    private static void WalkCycleChecks()
    {
        WalkFigures f = GameFeet;
        float walk = WalkRules.WalkSpeed(f);
        Check(Near(WalkRules.AnimRate(walk, f), f.AnimWalk, 0.0001f), "walking: the walk plays the game's walk cycle at its own rate (" + f.AnimWalk + "): " + WalkRules.AnimRate(walk, f));
        Check(WalkRules.IsWalk(walk, f), "walking: the walk is the walk end of the game's blend, under its brisk walk");
        Check(!WalkRules.IsWalk(f.Jog, f) && !WalkRules.IsWalk(f.Run, f) && !WalkRules.IsWalk(f.WalkFast, f), "walking: the game's jog, run and brisk walk are not a walk");
        // The game's own Move, at its own figures: the rates at each figure, and halfway between a walk and a brisk walk.
        Check(Near(WalkRules.AnimRate(1.8f, f), 1f, 0.0001f) && Near(WalkRules.AnimRate(4f, f), 1.3f, 0.0001f) && Near(WalkRules.AnimRate(6f, f), 0.85f, 0.0001f)
              && Near(WalkRules.AnimRate(1.5f, f), 0.875f, 0.0001f) && Near(WalkRules.AnimRate(9f, f), 0.85f, 0.0001f), "walking: the cycle's rates are the game's Move's at its figures");
        float last = WalkRules.AnimRate(0f, f);
        float jump = 0f;
        for (float v = 0.01f; v <= 7f; v += 0.01f)
        {
            float r = WalkRules.AnimRate(v, f);
            jump = Math.Max(jump, Math.Abs(r - last));
            last = r;
        }
        Check(jump < 0.02f, "walking: the cycle's rate has no step from a stand to a run (largest change in 1 cm/s: " + jump.ToString("F4") + ")");
        // Over the clamp: any game walk between the slowest and the fastest is played in the walk band.
        foreach (float gameWalk in new[] { 0.5f, 0.8f, 1.0f, 1.2f, 1.4f, 1.6f, 1.7f })
        {
            WalkFigures g = f;
            g.Walk = gameWalk;
            float w = WalkRules.WalkSpeed(g);
            Check(w >= WalkRules.SlowestWalk && w <= WalkRules.FastestWalk && WalkRules.IsWalk(w, g), "walking: a game walk of " + gameWalk + " m/s gives a walk the blend plays as a walk: " + w);
        }
    }

    // ---------------------------------------------------------------- the sources

    private static void WalkSourceChecks(string scripts)
    {
        string folder = Path.Combine(scripts, "Walking");
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 5, "walking sources: the folder holds its files: " + files.Length);
        string game = Code(File.ReadAllText(Path.Combine(folder, "WalkGame.cs")));
        string director = Code(File.ReadAllText(Path.Combine(folder, "WalkDirector.cs")));
        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            string code = Code(File.ReadAllText(file));
            if (name != "WalkGame.cs")
            {
                Check(!Regex.IsMatch(code, @"\bnavmeshAgent\b|\bSetWalkingSpeed\b|\bwalkingSpeed\b|\bPlayerAction\b|\bNotifications\b|\bInstanceBehavior\b|\bGlobalReferences\b"),
                      "walking sources: only WalkGame touches the game (" + name + ")");
            }
            Check(!Regex.IsMatch(code, @"\bInput\.|\bKeyCode\b|\bKeys\.Held\b"), "walking sources: no key is read but through Keys.Down (" + name + ")");
            Check(!Regex.IsMatch(code, @"\bGoingPublic\b|\bMPClient\b|\bMPServer\b|\bSend\(|\bmodData\b|\bSaveHook\b|\bPlayerPrefs\b"),
                  "walking sources: nothing is sent and nothing saved (" + name + ")");
        }
        Check(Regex.Matches(director, @"Keys\.Down\(KeyAction\.Walk\)").Count == 1, "walking sources: the director reads its one key once");
        Check(game.Contains("gm.playerController.Character"), "walking sources: the body is the local player's (the game's own player controller)");
        Check(game.Contains("body.SetWalkingSpeed(body.walkingSpeed)"), "walking sources: the game's own speed is given back by the game's own call");
        Check(game.Contains("trackOnSaveGame: false"), "walking sources: its notices stay out of the save");

        // The per-frame path allocates nothing: OnUpdate, Keep and Look build no object and join no text.
        foreach (string method in new[] { "OnUpdate", "Keep", "Look" })
        {
            Match body = Regex.Match(director, @"private void " + method + @"\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
            Check(body.Success, "walking sources: the director has " + method);
            if (body.Success)
            {
                string b = body.Groups["b"].Value;
                Check(!Regex.IsMatch(b, @"\bnew\b|""\s*\+|\+\s*""|\$""|ToString\("), "walking sources: " + method + " allocates nothing");
            }
        }

        // The key, the option and the page.
        string keys = Code(File.ReadAllText(Path.Combine(scripts, @"Core\Keys.cs")));
        string settings = Code(File.ReadAllText(Path.Combine(scripts, @"Core\Settings.cs")));
        string tree = Code(File.ReadAllText(Path.Combine(scripts, @"Meta\OptionsTree.cs")));
        Check(Regex.IsMatch(keys, @"\bWalk,") && keys.Contains("new Binding(\"walk\", \"reallife_keys_walk\", KeyCode."),
              "walking sources: the walk key is an action of Core/Keys, changeable on the options page");
        Check(settings.Contains("WalkToggleId = \"walk_toggle\"") && settings.Contains("WalkToggle = Bool(modId, WalkToggleId, WalkToggle)") && settings.Contains("WalkToggle = true"),
              "walking sources: its option is saved, read and on unless switched off");
        Check(tree.Contains("\"reallife_keys_walk\"") && tree.Contains("\"walk_toggle\""), "walking sources: the key's row and the option have their places on the options page");

        // Every text the folder names is in the three languages, and the notices are short.
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        const string Named = "\"(reallife_(?:walk|guide_walking|meta_title_walking|keys_walk|opt_[a-z]+_(?:walk_toggle|onfoot))[a-z_]*)\"";
        var named = files.SelectMany(file => Regex.Matches(File.ReadAllText(file), Named).Select(x => x.Groups[1].Value))
                         .Concat(new[] { "reallife_keys_walk", "reallife_opt_tip_walk_toggle", "reallife_opt_sec_onfoot" }).Distinct().ToList();
        Check(named.Count >= 12, "walking sources: its texts are found: " + named.Count);
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, code + ".json")));
            foreach (string key in named)
            {
                Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0, "walking texts: " + key + " is in " + code + ".json");
            }
            foreach (string notice in new[] { WalkText.On, WalkText.Off, WalkText.TestWalking, WalkText.TestHeld, WalkText.TestRunning })
            {
                Check(doc.RootElement.TryGetProperty(notice, out JsonElement value) && value.GetString().Length <= 240,
                      "walking texts: the notice " + notice + " is at most 240 characters in " + code);
            }
            string next = doc.RootElement.GetProperty("reallife_changes_next").GetString();
            Check(next.Contains("F3"), "walking texts: What's new's next row names the walk key in " + code);
        }
    }
}
