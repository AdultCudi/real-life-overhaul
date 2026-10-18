using System.Text.RegularExpressions;

internal static partial class Program
{
    private static void LifeFixes()
    {
        string catalogue = ModSource(@"Life\LifeEventCatalogue.cs");
        Check(catalogue.Contains("HasHome = 1 << 30"), "life fix: HasHome uses the next free Need bit");
        string runner = MemberText(ModSource(@"Life\LifeEventRunner.cs"), "public static LifeContext ReadContext()");
        Check(runner.Contains("LifeBridges.HasHome != null && LifeBridges.HasHome()") && runner.Contains("flags |= Need.HasHome;"),
            "life fix: the context requires a wired home before setting HasHome");
        Check(ModSource(@"Life\LifeBridges.cs").Contains("public static Func<bool> HasHome;"),
            "life fix: Life asks for a home through an optional bridge");
        string links = ModSource(@"Wiring\LifeLinks.cs");
        string home = MemberText(links, "private static bool HasHome()");
        Check(MemberText(links, "internal static void Connect()").Contains("LifeBridges.HasHome = HasHome;")
              && MemberText(links, "internal static void Disconnect()").Contains("LifeBridges.HasHome = null;")
              && home.Contains("return HomeTidy.HasHome;") && home.Contains("catch (Exception e)")
              && home.Contains("Fault(\"LifeLinks.HasHome\", e);") && home.Contains("return false;"),
            "life fix: wiring asks Home's residential lookup, defaults to no home on a fault and clears on disconnect");

        string events = MemberText(ModSource(@"Life\LifeEventCatalogue.HomeAndWork.cs"), "private static LifeEvent[] Home()");
        string[] needs =
        {
            "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "None",
            "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "HasHome", "None", "None", "None",
            "HasHome", "HasHome | OnFoot", "None", "HasHome", "Indoors", "Indoors", "HasHome", "HasHome"
        };
        for (int i = 0; i < needs.Length; i++)
        {
            string id = "hm" + (i + 1).ToString("00");
            Match entry = Regex.Match(events, "[EC]\\(\"" + id + "\", ([^,]+),");
            Check(entry.Success && entry.Groups[1].Value == needs[i], "life fix: " + id + " needs " + needs[i]);
        }
        Check(FixLineEverywhere("news about your building, neighbours and home", "notícias sobre seu prédio, seus vizinhos e sua casa",
                                "关于你的住宅楼、邻居和住处的消息", "Nachrichten über dein Gebäude, deine Nachbarn und dein Zuhause",
                                "nouvelles de votre immeuble, de vos voisins et de votre logement", "noticias sobre tu edificio, tus vecinos y tu hogar",
                                "новости о вашем доме, соседях и жилье"),
            "life fix: What's new has the home events' line, with its fix mark, in every language");
    }
}
