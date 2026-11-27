using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// The game is left or the save list opened only from the death card (Scripts/Ui/News/NewsLive.cs EndChoice). The kit's
// decision choice and the app window's tabs share the command number 1, so the News pages must take the choice only when
// the page is the death card: a tab press in the Newsstand, the Ledger or the Economy app quit the game (owner, 4 Nov).
internal static partial class Program
{
    private static void QuitGuards()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string news = File.ReadAllText(System.IO.Path.Combine(root, @"Ui\News\NewsLive.cs"));
        int choices = Regex.Matches(news, @"command == UiDecision\.ChoiceCommand").Count;
        Check(choices == 1 && news.Contains("_app == NewsApp.Ending && command == UiDecision.ChoiceCommand"),
            "quit guard: the News pages take the decision choice only on the death card, so a tab (the same number) never loads the saves or quits");
        var callers = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(f)) != "Permadeath")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"PermadeathGame\.(QuitDesktop|LoadSaves)\("))
            .Select(System.IO.Path.GetFileName).ToArray();
        Check(callers.Length == 1 && callers[0] == "NewsLive.cs" && Regex.Matches(news, @"PermadeathGame\.(QuitDesktop|LoadSaves)\(").Count == 2
              && Regex.Matches(news, @"\bEndChoice\(").Count == 2,
            "quit guard: leaving the game and the save list are called from the death card's choice alone (found in: " + string.Join(", ", callers) + ")");
        // The developer self-test (Diagnostics/SelfTest) quits the game when a plan says so, and only then: its runner exists only when a trigger file was read.
        string[] outside = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(@"Diagnostics\SelfTest\")).ToArray();
        int quits = outside.Sum(f => Regex.Matches(File.ReadAllText(f), @"Application\.Quit\(").Count);
        Check(quits == 1, "quit guard: the mod has one Application.Quit, the death card's (found " + quits + ")");
        string runner = File.ReadAllText(System.IO.Path.Combine(root, @"Diagnostics\SelfTest\SelfTestRunner.cs"));
        Check(Regex.Matches(runner, @"Application\.Quit\(").Count == 1 && runner.Contains("if (_plan.Quit)"), "quit guard: the self-test's one Application.Quit is the end of a plan that says quit");
    }
}
