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
            Check(end.Contains(kind == "Cleaner" ? "ReadClean();\n                Pay();" : "Settle(false);") && end.Contains("_clock?.Close();"),
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
        int canWork = worked.IndexOf("!_canWork()", StringComparison.Ordinal);
        Check(canWork >= 0 && worked.IndexOf("_done =", StringComparison.Ordinal) > canWork,
            "client hours: a time skip checks the client before counting more work, without waiting for a street tick");
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
