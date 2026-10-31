using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.GameUi;

// The mod's apps going on the phone (docs/standards/performance.md, RLO-PERF-024; Core/GameUi/InstallQueue.cs, PhoneNames.cs
// and PhoneApps.cs). The phone took them only while its menu was open, so some 35 went on in the few frames it was first opened,
// each searching both whole phones twice and laying the pages out four times: a frame of 77 ms in the beta 6 log
// (CalendarDirector.Update 26.9 ms, PhotosDirector.Tick 18.4 ms), longer in the frames after it the log did not time. Here
// the line is run on stand-in installs: one a frame, in the order they asked, one taken out of turn leaving the rest in
// theirs, the phones' names read once for every app, and over the sources the game's side using it.
//
// What is not here: how long an install takes in the game, which "Phone apps check (paced install): ..." in Player.log shows.
internal static partial class Program
{
    private const int PhoneAppsAsked = 35;     // the mod's apps on a full phone, as many as the beta 6 log put on
    private const double StandInMs = 0.4;      // ms a stand-in install takes here: well inside a frame

    private static void PhoneInstalls()
    {
        // One a frame, in the order they asked, every one put on once.
        var line = new InstallQueue<string>();
        var asked = new List<string>(PhoneAppsAsked);
        for (int i = 0; i < PhoneAppsAsked; i++)
        {
            asked.Add("app" + i);
            line.Add(asked[i]);
        }
        Check(!line.Add(asked[3]) && !line.Add(null) && line.Count == PhoneAppsAsked, "phone apps: an app already waiting, or none, is not put in the line again");
        var put = new List<string>(PhoneAppsAsked);
        var watch = new SpreadWatch();
        watch.Begin();
        int frames = 0, most = 0;
        while (line.Count > 0 && frames < PhoneAppsAsked * 2)
        {
            long began = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(InstallQueue<string>.SliceMs, 1);
            int inFrame = 0;
            while (line.Next(ref slice, out string app))
            {
                long one = Stopwatch.GetTimestamp();
                Spin(StandInMs);
                put.Add(app);
                watch.Item(put.Count - 1, one);
                inFrame++;
            }
            watch.Frame(began);
            most = Math.Max(most, inFrame);
            frames++;
        }
        bool inOrder = put.Count == asked.Count;
        for (int i = 0; inOrder && i < put.Count; i++)
        {
            inOrder = ReferenceEquals(put[i], asked[i]);
        }
        Check(most == 1 && frames == PhoneAppsAsked, "phone apps: " + PhoneAppsAsked + " apps waiting go on one a frame (" + frames + " frame(s), at most " + most + " in one)");
        Check(inOrder, "phone apps: they go on in the order they asked, each once");
        Check(watch.WorstSharedMs == 0.0 && watch.Verdict(true) == "PASS" && InstallQueue<string>.SliceMs <= SpreadWatch.WantedMs,
            "phone apps: no frame holds more than one install, within the 2 ms a frame of spread work (" + watch.Said((string)null) + ")");
        WorkSlice fresh = WorkSlice.Of(InstallQueue<string>.SliceMs, 1);
        Check(!line.Next(ref fresh, out string none) && none == null && !fresh.Over && line.Count == 0,
            "phone apps: an empty line puts nothing on and leaves the frame's slice untouched");

        // One taken out of turn: opened before its turn, or taken off. The rest keep their order.
        var five = new InstallQueue<string>();
        for (int i = 0; i < 5; i++)
        {
            five.Add(asked[i]);
        }
        bool took = five.Take(asked[2]), again = five.Take(asked[2]);
        var left = new List<string>();
        while (five.Count > 0)
        {
            WorkSlice s = WorkSlice.Of(InstallQueue<string>.SliceMs, 1);
            if (five.Next(ref s, out string app))
            {
                left.Add(app);
            }
        }
        Check(took && !again && left.Count == 4 && left[0] == asked[0] && left[1] == asked[1] && left[2] == asked[3] && left[3] == asked[4],
            "phone apps: one taken out of turn leaves the others in their order, and is taken once");

        // The phones' names: one search for a pair of phones, a look-up an app.
        var names = new PhoneNames();
        int searches = 0;
        int refused = 0;
        for (int i = 0; i < PhoneAppsAsked; i++)
        {
            if (!names.Read(11, 22))
            {
                names.Begin(11, 22);
                searches++;
                names.Learn("Persona");
                names.Learn("Contacts");
            }
            refused += names.Has(asked[i]) ? 1 : 0;
            names.Learn(asked[i]);
        }
        Check(searches == 1 && names.Searches == 1 && names.Asked == PhoneAppsAsked && refused == 0,
            "phone apps: both phones are searched once for " + PhoneAppsAsked + " apps, each asking its name once (" + names.Searches + " search(es), " + names.Asked + " asked)");
        Check(names.Has("Contacts") && names.Has(asked[7]), "phone apps: a name on the phone or of an app put on is known, so a second app of it is refused");
        names.Forget(asked[7]);
        Check(!names.Has(asked[7]) && !names.Has(null), "phone apps: an app taken off frees its name for the next one put on");
        Check(!names.Read(33, 22) && !names.Read(11, 44), "phone apps: a new phone or full menu (the next load) is searched anew");
        names.Begin(33, 22);
        Check(names.Searches == 2 && !names.Has(asked[0]) && names.Read(33, 22), "phone apps: the last load's names are forgotten with its phones");

        PhoneInstallSources();
    }

    // The game's side uses the line, the names and the held pagers.
    private static void PhoneInstallSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string phone = Source(scripts, @"Core\GameUi\PhoneApps.cs");
        string pager = Source(scripts, @"Core\GameUi\PhoneGridPager.cs");
        string pump = Source(scripts, @"Core\GameUi\PhoneAppQueue.cs");
        if (phone.Length == 0 || pager.Length == 0 || pump.Length == 0)
        {
            Check(false, "phone apps: PhoneApps.cs, PhoneGridPager.cs and PhoneAppQueue.cs are at " + scripts);
            return;
        }
        string up = Body(phone, "public static bool PhoneUp");
        string built = Body(phone, "private static bool MenuBuilt(");
        Check(up.Contains("ReferenceEquals(menu, _upMenu)") && up.Contains("MenuBuilt(menu)") && !up.Contains("GetComponentInChildren")
              && built.Contains("activeSelf") && built.Contains("Enum.TryParse(child.name, out AppName _)"),
            "phone apps: the phone takes apps once the full menu has made its buttons, shown or not, then for that menu at one comparison");
        string install = Body(phone, "public static PhoneApp Install(");
        Check(install.Contains("OnPhone(phone, menu, def.Id)") && install.Contains("app.Queue(menu)") && !install.Contains("GetComponentsInChildren") && !install.Contains("Instantiate(")
              && !install.Contains("Put()") && !install.Contains("Named("),
            "phone apps: an install asks the names and joins the line; it makes no button and searches no phone");
        string onPhone = Body(phone, "private static bool OnPhone(");
        Check(onPhone.IndexOf("Names.Read(", StringComparison.Ordinal) >= 0 && onPhone.IndexOf("Names.Read(", StringComparison.Ordinal) < onPhone.IndexOf("GetComponentsInChildren", StringComparison.Ordinal)
              && onPhone.Contains("return Names.Has(id);") && Count(phone, "GetComponentsInChildren<SmartphoneAppButton>") == 1,
            "phone apps: the phones are searched for names only when a new pair is first seen");
        string pumped = Body(phone, "internal static bool Pump()");
        Check(pumped.Contains("WorkSlice.Of(InstallQueue<PhoneApp>.SliceMs, 1)") && pumped.Contains("Waiting.Next(ref slice, out PhoneApp app)") && pumped.Contains("Spread.Frame(began)")
              && !pumped.Contains("WorkSlice.Whole") && pumped.Contains("Finish();"),
            "phone apps: the pump puts on a slice of the line a frame, and after the last shows the buttons and lays the pages out");
        string putNow = Body(phone, "private void PutNow()");
        Check(putNow.Contains("PhoneGridPager.Holding++") && putNow.Contains("PhoneGridPager.Holding--") && putNow.IndexOf("Holding--", StringComparison.Ordinal) > putNow.IndexOf("finally", StringComparison.Ordinal),
            "phone apps: an app is put on with the pagers held, let go on every path");
        string finish = Body(phone, "private static void Finish()");
        Check(finish.Contains("Made[i].SetActive(true)") && finish.Contains("hud.Refresh()") && finish.Contains("strip.Refresh()") && Count(finish, ".Refresh()") == 2,
            "phone apps: after the line's last its buttons show together and each pager lays its grid out once");
        string putBody = Body(phone, "private void Put()");
        Check(putBody.Contains("Made.Add(hud.gameObject)") && putBody.Contains("Made.Add(inMenu.gameObject)") && !putBody.Contains("SetActive(true)"),
            "phone apps: an app's buttons stay hidden until the line's last is on");
        Check(Body(phone, "public void Open()").Contains("Place();") && Body(phone, "public bool Ready()").Contains("Place()") && Body(phone, "public void Remove()").Contains("Waiting.Take(this)"),
            "phone apps: an app opened before its turn is put on at once, and one taken off leaves the line");
        Check(Body(phone, "private static void Said()").Contains("\"Phone apps check (paced install): \""), "phone apps: the line writes its self-check line after its last");
        Check(Body(pager, "private void OnTransformChildrenChanged()").Contains("Holding == 0") && Body(pager, "internal void Changed()").Contains("Holding > 0")
              && Count(pager, "if (Holding == 0)") == 3,
            "phone apps: a held pager lays nothing out for a button's arrival, and lays out once when let go");
        Check(Body(pump, "private void OnUpdate()").Contains("if (!PhoneApp.Pump())") && Body(pump, "private void OnUpdate()").Contains("enabled = false;"),
            "phone apps: the pump switches itself off once the line is empty, costing nothing a frame after");
    }
}
