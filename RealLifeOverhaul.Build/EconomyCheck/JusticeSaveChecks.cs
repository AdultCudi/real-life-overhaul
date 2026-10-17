using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Justice's save line (Scripts/Justice/JusticeState.Save.cs): a name with characters that could break the line is written
// as %XXXX and read back as it was, an escape that ends a value too, and a '%' of the name itself is always escaped, so
// nothing written can be read as an escape it is not.
internal static partial class Program
{
    private static void JusticeSaveRoundTrip()
    {
        string[] names = { "Dupré", "50% off", "a,b|c;d~e", "%0041", "Ünal & Sons", string.Empty, "plain name", "end%", "é" };
        bool same = true, escaped = true;
        string failed = string.Empty;
        foreach (string name in names)
        {
            JusticeState.Clear();
            JusticeState.LawyerName = name;
            JusticeState.LawyerStreet = name + "ß";
            JusticeState.PrisonCar = "car " + name;
            JusticeState.PrisonSeen = name + ",%";
            string line = JusticeState.Write();
            JusticeState.Clear();
            JusticeState.Parse(line);
            bool back = JusticeState.LawyerName == name && JusticeState.LawyerStreet == name + "ß" && JusticeState.PrisonCar == "car " + name
                        && JusticeState.PrisonSeen == name + ",%";
            same &= back;
            failed += back ? string.Empty : " '" + name + "'";
            escaped &= !Regex.IsMatch(line, "%(?![0-9A-F]{4})");
        }
        JusticeState.Clear();
        Check(same, "justice: names in the save come back as written, an escape at the very end of one too" + failed);
        Check(escaped, "justice: every '%' the save line holds begins a four-digit escape, so none is read as one it is not");
    }
}
