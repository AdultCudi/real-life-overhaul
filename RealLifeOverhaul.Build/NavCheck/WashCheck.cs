using System;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul;

// The game's car wash in a repair bay and the custom shop's ways out (Garage/WashRules.cs, Garage/GarageWash.cs,
// Garage/GarageBay.cs, Garage/GarageShop.cs, Garage/GarageDirector.cs). A player's report (20 Oct 2026): in the repair shop
// the car locked and only a restart helped; their log has the game's wash failing on another mod's car with no spray
// (NullReferenceException in CarController.WashCoroutine at washVfx.SetActive, after its hold on the car and the player),
// then the player clicking at the car's panel in vain until they left for the main menu.
//
// Checked on a model of the game's wash, frame by frame as CarController.WashCoroutine has it: the hold first, then the
// spray and the sound, then the dirt written each frame from the top down over 3 to 4 seconds of game time, then the hold
// let go. A wash that lives is never ended by the watch, at any frame rate, with a long frame, paused, or started again
// at once; one that stops (at its first line after the hold, mid-way, or with its car switched off) is ended once, soon
// after the longest wash; then, over the sources, that the stand-ins come before "Wash" can be pressed, that the end
// lets go everything the game's wash takes, and that every way out of the custom shop, the Garage's clean-up among them,
// gives the car and the view back.
internal static partial class Program
{
    private const float WashTick = 0.25f;   // s between the watch's looks (the Garage's quarter-second timer)

    // One wash in the model: the game's coroutine as the decompiled game has it, and the watch beside it.
    private sealed class WashModel
    {
        public float Now;
        public bool Washing;      // CarController.isWashing
        public bool Held;         // the car's hold and the player's blocker (SetFreeze, WashingVehicle)
        public float Dirt = 0.6f; // VehicleInstance.dirtiness
        public float Began, Lasts;
        public bool Dies;         // the coroutine stops at its first line after the hold
        public float DiesAt = -1f;  // or this many seconds in (the car's object switched off)
        public bool Living;
        public WashWatch Watch;
        public float NextLook;
        public int Ended;         // times the watch ended a wash
        public float EndedAt = -1f;

        public void Start(float lasts)
        {
            Washing = true;
            Held = true;
            Began = Now;
            Lasts = lasts;
            Living = !Dies;
        }

        // One frame of dt seconds of game time (0 while paused), then the watch's look if it is due (it runs on real time).
        public void Frame(float dt, float real)
        {
            Now += dt;
            if (Living && DiesAt >= 0f && Now - Began >= DiesAt)
            {
                Living = false;
            }
            if (Living)
            {
                float t = Now - Began;
                if (t < Lasts)
                {
                    Dirt = 1f - t / Lasts;
                }
                else
                {
                    Washing = false;
                    Held = false;
                    Living = false;
                }
            }
            NextLook -= real;
            if (NextLook <= 0f)
            {
                NextLook += WashTick;
                if (Watch.Step(7, Washing, Dirt, Now))
                {
                    Ended++;
                    EndedAt = Now - Began;
                    Washing = false;   // GarageWash.End: the game's own end of a wash
                    Held = false;
                    Dirt = 0f;
                }
            }
        }

        public void Run(float seconds, float dt, bool paused = false)
        {
            for (float t = 0f; t < seconds; t += dt)
            {
                Frame(paused ? 0f : dt, dt);
            }
        }
    }

    private static void Washes()
    {
        WashLiving();
        WashStopped();
        WashWatchParts();
        WashSpeed();
        WashSources();
    }

    private static void WashLiving()
    {
        float[] rates = { 1f / 144f, 1f / 60f, 1f / 30f, 1f / 12f };
        bool kept = true, done = true;
        foreach (float lasts in new[] { 3f, 3.5f, 4f })
        {
            foreach (float dt in rates)
            {
                var m = new WashModel();
                m.Start(lasts);
                m.Run(lasts + 12f, dt);
                kept &= m.Ended == 0;
                done &= !m.Washing && !m.Held;
            }
        }
        Check(kept && done, "wash: a wash of 3 to 4 seconds that runs is never ended by the watch, at 144 to 12 frames a second, and lets the car go by itself");

        var slow = new WashModel();
        slow.Start(4f);
        slow.Run(1f, 1f / 60f);
        slow.Frame(0.5f, 0.5f);   // one long frame mid-wash
        slow.Run(12f, 1f / 60f);
        Check(slow.Ended == 0 && !slow.Held, "wash: a long frame in the middle of a wash does not end it");

        var paused = new WashModel { Dies = true };
        paused.Start(4f);
        paused.Run(60f, 1f / 60f, true);
        Check(paused.Ended == 0 && paused.Held, "wash: with the game paused no time passes for the wash, and the watch ends nothing (a minute paused)");

        // A second wash at once, the first's dirt back at the top: timed from its own start, not the first's.
        var again = new WashModel();
        again.Start(4f);
        again.Run(4.1f, 1f / 60f);
        again.Start(4f);   // pressed again the moment the first let go
        again.Run(12f, 1f / 60f);
        Check(again.Ended == 0 && !again.Held, "wash: a second wash straight after the first is timed from its own start and runs to its end");

        // The same, pressed again inside the watch's quarter second, so the watch never sees the first one end.
        var quick = new WashModel();
        quick.Start(4f);
        quick.NextLook = 0.2f;
        quick.Run(4.02f, 1f / 60f);
        quick.Washing = true;   // (the second wash's first frame comes before the watch looks)
        quick.Start(4f);
        quick.Run(12f, 1f / 60f);
        Check(quick.Ended == 0 && !quick.Held, "wash: one pressed again before the watch saw the first end is known by its dirt starting at the top again, and runs to its end");
    }

    private static void WashStopped()
    {
        float most = WashRules.Longest + WashRules.Grace + WashTick + 0.05f;
        var first = new WashModel { Dies = true };
        first.Start(3.5f);
        first.Run(30f, 1f / 60f);
        Check(first.Ended == 1 && !first.Held && !first.Washing && first.Dirt == 0f && first.EndedAt > WashRules.Longest && first.EndedAt <= most,
            "wash: a wash that stops at its first line after the hold (the report's car with no spray) is ended once, "
            + first.EndedAt.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s after it began (the longest is " + WashRules.Longest + " s), the car let go and clean");

        var mid = new WashModel { DiesAt = 1.5f };
        mid.Start(4f);
        mid.Run(30f, 1f / 30f);
        Check(mid.Ended == 1 && !mid.Held && mid.EndedAt <= most, "wash: one stopped half way (its car's object switched off) is ended the same way");

        var late = new WashModel { DiesAt = 3.9f };
        late.Start(4f);
        late.Run(30f, 1f / 60f);
        Check(late.Ended == 1 && !late.Held && late.EndedAt <= most, "wash: one stopped just before its end is ended too, and only once");

        // The report's case with the watch's old absence: nothing ends it.
        var none = new WashModel { Dies = true };
        none.Start(3.5f);
        for (float t = 0f; t < 120f; t += 1f / 60f)
        {
            none.Now += 1f / 60f;   // the game's frames, no watch
        }
        Check(none.Held && none.Washing, "wash: without the watch the stopped wash holds the car and the player for good (the report)");
    }

    private static void WashWatchParts()
    {
        var w = new WashWatch();
        bool quiet = !w.Step(0, true, 0.5f, 0f) && !w.Step(5, false, 0.5f, 1f) && w.Car == 0;
        Check(quiet, "wash: no car, or a car not washing, is not watched");
        w.Step(5, true, 0.5f, 10f);
        bool other = !w.Step(6, true, 0.5f, 18.5f) && w.Car == 6 && w.Since == 18.5f;
        Check(other, "wash: another car seen washing is watched from when it is first seen");
        bool once = w.Step(6, true, 0.5f, 18.5f + WashRules.Longest + WashRules.Grace + 0.01f) && !w.Step(6, true, 0.5f, 40f) && w.Car == 6;
        Check(once, "wash: a stopped wash is ended once; seen again it is watched afresh, not ended again at once");
        var r = new WashWatch();
        r.Step(9, true, 0.2f, 0f);
        r.Step(9, true, 0.2f + WashRules.Restart * 0.5f, 5f);
        bool small = r.Since == 0f;
        r.Step(9, true, 1f, 6f);
        Check(small && r.Since == 6f, "wash: a small rise of the dirt (rain, a road) does not restart the clock; a wash starting at the top does");
        Check(!WashRules.Stopped(0f, WashRules.Longest + WashRules.Grace) && WashRules.Stopped(0f, WashRules.Longest + WashRules.Grace + 0.001f) && WashRules.Grace >= 2f,
            "wash: a wash is taken to have stopped only past the longest wash and a margin of " + WashRules.Grace + " s");
    }

    private static void WashSpeed()
    {
        var w = new WashWatch();
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        int ended = 0;
        for (int i = 0; i < 1000000; i++)
        {
            ended += w.Step(1 + (i / 1000) % 3, (i & 3) != 0, (i % 100) / 100f, i * 0.01f) ? 1 : 0;
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0 && watch.Elapsed.TotalMilliseconds < 50.0, "wash: a million looks in under 50 ms with nothing allocated ("
              + watch.Elapsed.TotalMilliseconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms; it looks four times a second; " + ended + ")");
    }

    private static void WashSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string file) => File.Exists(Path.Combine(scripts, file)) ? File.ReadAllText(Path.Combine(scripts, file)) : string.Empty;
        string wash = Read(@"Garage\GarageWash.cs"), bay = Read(@"Garage\GarageBay.cs"), shop = Read(@"Garage\GarageShop.cs"), director = Read(@"Garage\GarageDirector.cs"),
               contract = Read(@"Core\GameContract.cs"), features = Read(@"Meta\FeatureList.Life.cs");

        int tick = bay.IndexOf("public static void Tick(GameManager gm)", StringComparison.Ordinal);
        int watchCall = tick >= 0 ? bay.IndexOf("GarageWash.Watch(gm);", tick, StringComparison.Ordinal) : -1;
        int listReturn = tick >= 0 ? bay.IndexOf("if (!ListOpen)", tick, StringComparison.Ordinal) : -1;
        Check(watchCall > tick && watchCall < listReturn, "wash: the bay's quarter-second look watches the wash before it looks at the workshop's list (it is no use only while the list is open)");
        int with = bay.IndexOf("internal static ButtonInfo[] With(ButtonInfo[] own)", StringComparison.Ordinal);
        int ready = with >= 0 ? bay.IndexOf("GarageWash.Ready(car);", with, StringComparison.Ordinal) : -1;
        int takes = with >= 0 ? bay.IndexOf("!Takes(car)", with, StringComparison.Ordinal) : -1;
        Check(ready > with && ready < takes, "wash: the stand-ins are given as the bay's panel lists its buttons, before \"Wash\" can be pressed, whatever car it is");
        Check(bay.Contains("GarageWash.Forget();"), "wash: nothing of the city's cars is remembered once the Garage's panel is taken off");
        Check(bay.Contains("return base.GetButtons();   // (the Garage switched off"),
            "wash: with the Garage switched off after its faults the bay's panel has the game's own buttons, not none (repair, wash and fuel stay)");

        Check(wash.Contains("SprayField.GetValue(car) as GameObject != null") && wash.Contains("SoundField.GetValue(car) as AudioSource != null")
              && wash.Contains("if (spray && sound)") && wash.Contains("ReferenceEquals(car, _readied)"),
            "wash: a car is given stand-ins only for the spray and the sound it lacks, once a car; the game's own cars keep theirs");
        int end = wash.IndexOf("private static void End(CarController car, GameManager gm)", StringComparison.Ordinal);
        string ending = end >= 0 ? wash.Substring(end) : string.Empty;
        Check(ending.Contains("car.isWashing = false;") && ending.Contains("spray.SetActive(false);") && ending.Contains("sound.Stop();")
              && ending.Contains("car.SetFreeze(false);") && ending.Contains("UnsetVehicleNavigationBlocker(VehicleNavigationBlocker.WashingVehicle)") && ending.Contains("car.SetDirtiness(0f);"),
            "wash: a stopped wash is ended as the game's own end does: not washing, the spray off, the sound stopped, the car's hold and the player's blocker let go, the car clean");
        Check(ending.Contains("bool held = shop != null && shop.IsOpen && ReferenceEquals(shop.Car, car);") && ending.Contains("if (!held)"),
            "wash: the custom shop's own hold on the car is left to the shop while it is open");
        Check(wash.Contains("if (car == null || !car.isWashing)") && wash.Contains("_watch.Car = 0;"),
            "wash: with no car of the player's washing the watch is one compare and forgets what it saw");
        Check(contract.Contains("WashSpray = Optional(Field(CarWash, Game, \"CarController\", \"washVfx\", \"GameObject\"))")
              && contract.Contains("WashSound = Optional(Field(CarWash, Game, \"CarController\", \"washAudioSource\", \"AudioSource\"))")
              && wash.Contains("GameContract.WashSpray.Field") && wash.Contains("GameContract.WashSound.Field") && wash.Contains("if (SprayField == null || SoundField == null)"),
            "wash: the game's spray and sound are read through the contract, optional: without them the watch still ends a stopped wash");

        // Every way out of the custom shop gives the car and the view back (GarageShop.Close), the guard's switch-off among them.
        int close = shop.IndexOf("public void Close(bool keep)", StringComparison.Ordinal);
        string closing = close >= 0 ? shop.Substring(close, Math.Min(shop.Length - close, 4000)) : string.Empty;
        Check(closing.Contains("_held.input.autoSetInput = _autoInput;") && closing.Contains("car.SetFreeze(false);") && closing.Contains("_vcam.gameObject.SetActive(false);")
              && closing.Contains("UnityEngine.Object.Destroy(_dragArea);") && closing.Contains("GarageList.Close();") && closing.Contains("uis.customColorPicker.Close();"),
            "shop: closing it gives the pedals back, lets go of the car, gives the game's camera the view, takes the drag plate off, closes the list and the colour picker");
        Check(shop.Contains("\"the driver left the seat\"") && shop.Contains("\"the city is loading\"") && shop.Contains("\"another screen came up\"") && shop.Contains("\"the Garage was switched off\"")
              && shop.IndexOf("Close(false);", Math.Max(0, shop.IndexOf("_listClosing = why;", StringComparison.Ordinal)), StringComparison.Ordinal) > shop.IndexOf("_listClosing = why;", StringComparison.Ordinal),
            "shop: it closes when the driver leaves, the city loads, the pause menu, map or phone comes up (Esc), or the Garage is switched off");
        int listClosed = shop.IndexOf("private void ListClosed()", StringComparison.Ordinal);
        int picked = shop.IndexOf("private void Picked(int id)", StringComparison.Ordinal);
        int open = shop.IndexOf("public bool Open(VehicleController car)", StringComparison.Ordinal);
        Check(listClosed >= 0 && shop.IndexOf("Close(false);", listClosed, StringComparison.Ordinal) > listClosed
              && picked >= 0 && shop.IndexOf("Close(false);", picked, StringComparison.Ordinal) < listClosed
              && open >= 0 && shop.IndexOf("Close(false);", open, StringComparison.Ordinal) < close,
            "shop: its list cancelled (the yellow button or the game's cancel key), a fault in a choice, and a fault while opening each close the shop");
        int letGo = director.IndexOf("public static void LetGo()", StringComparison.Ordinal);
        Check(features.Contains(".Cleanup(GarageDirector.LetGo)") && letGo >= 0 && director.IndexOf("director._shop.Close(false);", letGo, StringComparison.Ordinal) > letGo
              && director.IndexOf("GarageBay.CloseList(", letGo, StringComparison.Ordinal) > letGo,
            "shop: when the Garage is switched off, by the options page or by the guard after its faults (its timer then never runs again), the shop and its lists are closed at once");
    }
}
