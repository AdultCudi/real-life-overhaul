using System;
using System.Text.RegularExpressions;

// Found in Scripts/Diagnostics and Civic: the logs page said the session lines could say nothing of the graphics card,
// though SessionWatch writes the engine's own figures for it where the build offers them; and the civic props' count of
// those out, whose one reader went with the faults' clean-up, was kept up for nobody.
internal static partial class Program
{
    private static void DiagnosticsFixes()
    {
        string page = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\reference\logs-and-debugging.md")));
        string lines = ModSource(@"Diagnostics\SessionWatch.Lines.cs");
        Check(!page.Contains("and anything about the graphics card.") && page.Contains("the graphics card's longest frame of the minute")
              && page.Contains("the engine's own frame timing: not offered by this build") && lines.Contains("\"; the engine's own frame timing: not offered by this build\"")
              && lines.Contains("ms at most in a frame (the engine's own figures)"),
            "logs page: it says what the session lines give of the graphics card, and in the words the lines use");
        string props = ModSource(@"Civic\CivicProps.cs");
        Check(props.Length > 0 && !Regex.IsMatch(props, @"\bOut\b"), "civic props: no count of the props out that nothing reads");
    }
}
