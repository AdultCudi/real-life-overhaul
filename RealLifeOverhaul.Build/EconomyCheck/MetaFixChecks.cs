using System;
using System.IO;
using IoPath = System.IO.Path;

// Faults found in Scripts/Meta: Import stopped with "import failed" when the backup of the settings could not be written,
// though Apply and ResetAll go on without it; and a report whose coroutine stopped with its object stayed "busy" for the
// session. Also two feature pages that described what the code does not do. All checked in the source and the pages.
internal static partial class Program
{
    private static void MetaFixes()
    {
        string presets = ModSource(@"Meta\Presets.cs");
        string import = MemberText(presets, "public static string Import()");
        int read = import.IndexOf("lines = File.ReadAllLines(path);", StringComparison.Ordinal);
        int failed = import.IndexOf("\"reallife_meta_import_failed\"", StringComparison.Ordinal);
        int backup = import.IndexOf("SaveBefore();", StringComparison.Ordinal);
        Check(read > 0 && failed > read && backup > failed && !import.Contains("Export(BeforePresetFile)"),
            "meta fix: Import fails only on the file it reads; the backup is made after, through SaveBefore, which logs a failure and goes on");
        Check(MemberText(presets, "private static string SaveBefore()").Contains("return null;"), "meta fix: SaveBefore answers null, never throws, when the backup cannot be written");

        string report = ModSource(@"Meta\BugReport.cs");
        Check(!report.Contains("private static bool _busy") && !report.Contains("private static bool _waiting")
              && report.Contains("private static bool Busy => _making != null;") && report.Contains("private static bool Waiting => _waiting != null;"),
            "meta fix: a report is busy only while the object its coroutine runs on is there, so a destroyed one never leaves it busy");
        string making = MemberText(report, "private static IEnumerator Making(string words)");
        Check(making.TrimEnd().EndsWith("_making = null;\r\n        }", StringComparison.Ordinal) || making.TrimEnd().EndsWith("_making = null;\n        }", StringComparison.Ordinal),
            "meta fix: a report that is made clears its busy state at its end");
        Check(MemberText(report, "private static void Fail(Exception e)").Contains("_making = null;")
              && MemberText(report, "private static IEnumerator WaitForMenus()").Contains("_waiting = null;"),
            "meta fix: a report that fails, and the wait for a menu, clear their state");
        Check(FixLineEverywhere("Load my choices from the file no longer fails", "Carregar minhas escolhas do arquivo não falha mais", "不再失败", "schlägt nicht mehr fehl",
                                "n'échoue plus", "ya no falla", "больше не завершается ошибкой",
                                "mislukt niet meer als de kopie van je huidige instellingen", "Seçimlerimi dosyadan yükle"),
            "meta fix: What's new has the import's line, with its fix mark, in some row of every language");
        Check(FixLineEverywhere("Report a bug no longer says it is busy", "Relatar um bug não diz mais", "不再在本次游戏的剩余时间里一直显示正忙", "Fehler melden sagt nicht mehr",
                                "Signaler un bug ne dit plus", "Informar de un error ya no dice", "больше не пишет, что занято",
                                "zegt niet meer dat het bezig is", "meşgul olduğunu söylemiyor"),
            "meta fix: What's new has the report's line, with its fix mark, in some row of every language");

        string docs = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features"));
        string meta = File.Exists(IoPath.Combine(docs, "meta.md")) ? File.ReadAllText(IoPath.Combine(docs, "meta.md")) : string.Empty;
        Check(meta.Length > 0 && !meta.Contains("SearchWords") && meta.Contains(".Also(words)") && !ModSource(@"Meta\OptionsLayout.Search.cs").Contains("SearchWords"),
            "docs fix: the Meta page gives the search's other words as OptionsTree's .Also, and names no SearchWords table");
        string careers = File.Exists(IoPath.Combine(docs, "careers.md")) ? File.ReadAllText(IoPath.Combine(docs, "careers.md")) : string.Empty;
        Check(careers.Length > 0 && !careers.Contains("choice keys") && careers.Contains("by that van's button on the game's panel over the desk"),
            "docs fix: the Careers page says the dispatcher's vans are the buttons of the game's panel, not choice keys");
    }
}
