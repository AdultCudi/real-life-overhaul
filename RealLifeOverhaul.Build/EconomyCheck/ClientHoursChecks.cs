using System;
using System.IO;
using FilePath = System.IO.Path;
using System.Text.Json;

/// <summary>Indoor clients must have time for the whole shift and must release the worker when their doors close.</summary>
internal static partial class Program
{
    private static void ClientHours()
    {
        string mod = FilePath.GetFullPath(FilePath.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul"));
        string jobs = FilePath.Combine(mod, "Scripts", "Jobs");
        string sites = File.ReadAllText(FilePath.Combine(jobs, "JobSites.cs"));
        string pick = MemberText(sites, "public static bool RandomClient(");
        Check(pick.Contains("i < ClientPicks") && pick.Contains("out DoorSpot found, only, 1")
              && pick.Contains("JobSpots.Flat(ctx.PlayerPosition, found.Front) * secondsPerMetre")
              && pick.Contains("Mathf.Max(seconds * speed + walk, walk + units * ShiftRates.Minutes(def)) + ClosingMargin")
              && pick.Contains("if (open < needed)") && sites.Contains("ClosingMargin = 15;"),
            "client hours: bounded picks reserve the walk and the longer of hand work or the clock, with a closing margin, at the game's speed");
        string hours = MemberText(sites, "private static float OpenMinutes(");
        Check(hours.Contains("registration.temporarilyClosed") && hours.Contains("registration.scheduleDays")
              && hours.Contains("TimeHelper.GetDayOfWeek(day)") && hours.Contains("!schedule.IsOpenAtHour(hour)")
              && hours.Contains("60f - TimeHelper.CurrentMinute") && hours.Contains("if (++hour == 24)")
              && hours.Contains("day++;") && hours.Contains("i < 168"),
            "client hours: consecutive game schedule hours stop at a gap and cover split slots, midnight, weekdays and the current partial hour");
        Check(pick.Contains("Meta.Dev.Verbose") && pick.Contains("client refused:") && pick.Contains("game minutes; the walk, work, clock and margin need"),
            "client hours: a refused pick says which client and how much open time was missing");
        string available = MemberText(sites, "private static bool IsClient(");
        Check(sites.Contains("public static bool Available(in DoorSpot spot) => IsClient(spot.Building);")
              && available.Contains("BuildingHelper.CanEnterBuilding(") && available.Contains("IsBuildingBlockedByAnyService("),
            "client hours: the running check reads the game's current door restrictions, including temporary closures");
        string enter = MemberText(sites, "public static bool Enter(");
        Check(enter.Contains("spot.Building.Interact();") && enter.Contains("return Inside(spot) || (premises != null && premises.enteringBuilding);")
              && enter.Contains("uis.gameSpeed.Paused"),
            "client hours: entry must actually start, since the game's interaction return value also accepts a refused door");

        foreach (string kind in new[] { "Cleaner", "Stocker" })
        {
            string source = File.ReadAllText(FilePath.Combine(jobs, "Kinds", kind + "Job.cs"));
            string begin = MemberText(source, "protected override bool OnBegin(");
            string tick = MemberText(source, "public override void Tick(");
            string act = MemberText(source, "private void Act(");
            string units = kind == "Cleaner" ? "_units" : "_boxes";
            string client = kind == "Cleaner" ? "AnyClient" : "ShopClient";
            Check(begin.Contains("JobSites.RandomClient(Ctx, Def, " + units + ", ClientMin, ClientMax, Seconds, SecondsPerMetre, JobSites." + client + ", out _client)")
                  && begin.Contains("canWork: ClientOpen") && source.Contains("private bool ClientOpen() => !ClientClosed(_client);"),
                "client hours: " + kind + " uses its whole offer and shares the closure check with its clock");
            int check = tick.IndexOf("if (!ClientOpen())", StringComparison.Ordinal);
            Check(check >= 0 && tick.IndexOf("_clock.Hold()", StringComparison.Ordinal) > check,
                "client hours: " + kind + " checks closure before clock offers, fast-forward, timeouts and hand work");
            Check(act.Contains("if (!ClientClosed(_client) && !JobSites.Enter(_client))") && act.Contains("ClientClosed(_client, true);"),
                "client hours: " + kind + " calls a refused door off too");
            string end = MemberText(source, "public override void End(");
            int read = end.IndexOf("ReadClean();", StringComparison.Ordinal);   // by position, never by line ending: a Windows checkout has CRLF
            bool settles = kind == "Cleaner" ? read >= 0 && end.IndexOf("Pay();", read, StringComparison.Ordinal) > read : end.Contains("Settle(false);");
            Check(settles && end.Contains("_clock?.Close();"),
                "client hours: " + kind + " keeps its usual earned hand pay and cleanup on interruption");
        }
        string job = File.ReadAllText(FilePath.Combine(jobs, "Job.cs"));
        string closed = MemberText(job, "protected bool ClientClosed(");
        Check(closed.Contains("if (Over)") && closed.Contains("JobSites.Available(client)") && closed.Contains("Clocked?.Stop();")
              && closed.Contains("Finish(JobEnd.Interrupted);") && !closed.Contains("JobEnd.Failed")
              && closed.Contains("\"reallife_job_client_closed\", \"place\", name") && closed.Contains("has closed or refused entry;"),
            "client hours: a closure settles the clock, says why and ends once as Interrupted");
        string clock = File.ReadAllText(FilePath.Combine(jobs, "ShiftClock.cs"));
        string worked = MemberText(clock, "void ISkippableShift.SkipWorked(");
        int counted = worked.IndexOf("_done = ClockRules.Units(minutes, false, _left, _each, _open);", StringComparison.Ordinal);
        Check(counted >= 0 && worked.IndexOf("_canWork?.Invoke();", StringComparison.Ordinal) > counted,
            "client hours: a time skip counts the work up to the client's closing, then asks the client, without waiting for a street tick");
        Check(MemberText(clock, "void ISkippableShift.SkipStarted(").Contains("_open = _openFor != null ? _openFor() : int.MaxValue;")
              && MemberText(clock, "void ISkippableShift.SkipEnded(").Contains("int count = ClockRules.Units(minutes, whole, _left, _each, _open);")
              && sites.Contains("public static int MinutesOpen(in DoorSpot spot)")
              && MemberText(sites, "public static int MinutesOpen(").Contains("OpenMinutes(spot.Building.buildingRegistration)"),
            "client hours: the clock learns the client's open minutes when it starts, and pays nothing done after the doors shut");
        foreach (string kind in new[] { "Cleaner", "Stocker" })
        {
            string source = File.ReadAllText(FilePath.Combine(jobs, "Kinds", kind + "Job.cs"));
            Check(MemberText(source, "protected override bool OnBegin(").Contains("openFor: ClientOpenFor")
                  && source.Contains("private int ClientOpenFor() => JobSites.MinutesOpen(_client);"),
                "client hours: " + kind + " tells its clock how long the client stays open");
        }
        // A run of 7 units of 30 minutes (210): open throughout, the count is as before; shut part of the way, nothing after it.
        Check(RealLifeOverhaul.ClockRules.Units(95, false, 7, 30, int.MaxValue) == 3 && RealLifeOverhaul.ClockRules.Units(210, true, 7, 30, int.MaxValue) == 7
              && RealLifeOverhaul.ClockRules.Units(210, true, 7, 30, 210) == 7 && RealLifeOverhaul.ClockRules.Units(500, false, 7, 30, 1000) == 7,
            "client hours: a client open for the whole run leaves the clock's count as it was");
        Check(RealLifeOverhaul.ClockRules.Units(210, true, 7, 30, 100) == 3 && RealLifeOverhaul.ClockRules.Units(210, false, 7, 30, 100) == 3
              && RealLifeOverhaul.ClockRules.Units(120, false, 7, 30, 89) == 2 && RealLifeOverhaul.ClockRules.Units(210, true, 7, 30, 0) == 0
              && RealLifeOverhaul.ClockRules.Units(210, true, 7, 30, -5) == 0 && RealLifeOverhaul.ClockRules.Units(60, false, 7, 30, 59) == 1,
            "client hours: a run reported after the client closed counts only the units done before it shut (the time machine reports once, at the end)");
        Check(RealLifeOverhaul.ClockRules.Units(50, false, 7, 0, int.MaxValue) == 7 && RealLifeOverhaul.ClockRules.Units(3, false, 7, 0, 2) == 2,
            "client hours: a unit of no length is counted as a minute, never divided by");
        string stop = MemberText(clock, "public void Stop(");
        Check(stop.Contains("if (!_closed && Working)") && stop.Contains("SkipEnded(_done * _each, false);")
              && stop.IndexOf("Close();", StringComparison.Ordinal) > stop.IndexOf("SkipEnded(", StringComparison.Ordinal),
            "client hours: stopping pays only completed clock units once, then cancels the activity and time machine");
        string ending = MemberText(File.ReadAllText(FilePath.Combine(jobs, "JobDirector.Ending.cs")), "private void EndJob(");
        Check(ending.Contains("bool failed = why == JobEnd.Failed || (why == JobEnd.Quit && job.QuitIsFailure);"),
            "client hours: Interrupted never counts as a strike");

        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string[] pieces = { "cleaning and shelf-stocking jobs only choose clients", "os trabalhos de limpeza e reposição só escolhem clientes",
            "清洁和补货工作只会选择", "Reinigungs- und Auffüllaufträge wählen nur Kunden", "les travaux de nettoyage et de mise en rayon choisissent",
            "los trabajos de limpieza y reposición solo eligen", "для уборки и пополнения полок выбираются", "schoonmaak- en vakkenvulklussen kiezen", "temizlik ve raf doldurma işleri yalnızca" };
        for (int i = 0; i < codes.Length; i++)
        {
            using JsonDocument words = JsonDocument.Parse(File.ReadAllText(FilePath.Combine(mod, "Locales", codes[i] + ".json")));
            bool message = words.RootElement.TryGetProperty("reallife_job_client_closed", out JsonElement line) && line.GetString().Contains("{place}");
            bool fix = false;
            foreach (JsonProperty row in words.RootElement.EnumerateObject())
            {
                fix |= row.Name.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.GetString().Contains(pieces[i]);
            }
            Check(message && fix, "client hours: " + codes[i] + " has the in-world closure message and its own fix sentence");
        }
    }
}
