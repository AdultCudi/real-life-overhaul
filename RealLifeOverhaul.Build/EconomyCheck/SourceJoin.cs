using System.Collections.Generic;
using System.IO;
using System.Text;

internal static partial class Program
{
    // Many files read into one text. Adding each file to a growing string copies the whole string every time, which
    // for the mod's whole source is hundreds of gigabytes of large strings the collector holds on to.
    private static string JoinedText(IEnumerable<string> files, string between)
    {
        var all = new StringBuilder();
        foreach (string file in files)
        {
            all.Append(File.ReadAllText(file)).Append(between);
        }
        return all.ToString();
    }
}
