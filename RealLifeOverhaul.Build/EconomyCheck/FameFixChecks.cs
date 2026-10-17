using System.IO;
using System.Text.RegularExpressions;

// Checks for Standing's scene window (Scripts/Fame/FamePanel): it is destroyed with the director, as every window the
// mod makes from the game's list is destroyed somewhere.
internal static partial class Program
{
    private static void FameFixes()
    {
        FameWindowGoes();
    }

    private static void FameWindowGoes()
    {
        string onDestroy = FlareMethod(Read(@"Fame\FameDirector.cs"), "private void OnDestroy()");
        string destroy = FlareMethod(Read(@"Fame\FamePanel.cs"), "public void Destroy()");
        Check(onDestroy.Contains("_panel?.Destroy();") && destroy.Contains("End();") && destroy.Contains("_window.Destroy("),
            "Standing: the scenes' window is ended and destroyed with the director");

        // Every DeskList the mod makes is destroyed: in its own file, or through its owner's field elsewhere.
        var made = new Regex(@"\b(\w+)\s*=\s*new DeskList\(");
        string all = string.Empty;
        string[] files = Directory.GetFiles(PhotoScripts, "*.cs", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            all += File.ReadAllText(file) + "\n";
        }
        int windows = 0;
        string kept = string.Empty;
        foreach (string file in files)
        {
            string own = File.ReadAllText(file);
            foreach (Match m in made.Matches(own))
            {
                windows++;
                if (!own.Contains(m.Groups[1].Value + ".Destroy(") && !all.Contains("." + m.Groups[1].Value + ".Destroy("))
                {
                    kept += " " + System.IO.Path.GetFileName(file) + " (" + m.Groups[1].Value + ")";
                }
            }
        }
        Check(windows >= 10 && kept.Length == 0, "every window made from the game's list (" + windows + ") is destroyed somewhere" + (kept.Length > 0 ? "; not:" + kept : string.Empty));
    }
}
