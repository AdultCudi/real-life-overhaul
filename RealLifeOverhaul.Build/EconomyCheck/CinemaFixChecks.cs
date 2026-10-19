using System;

// A cinema outing's time skip had no recovery, where the game's own cinema seat rests the player.
internal static partial class Program
{
    private static void CinemaFixes()
    {
        string catalogue = ModSource(@"Social\SocialCatalogue.cs");
        int cinema = catalogue.IndexOf("Kind = PlanKind.Cinema,", StringComparison.Ordinal);
        int rests = catalogue.IndexOf("SeatedRest = true,", StringComparison.Ordinal);
        Check(catalogue.Contains("public bool SeatedRest;") && cinema > 0 && rests > cinema
              && rests < catalogue.IndexOf("Invite =", cinema, StringComparison.Ordinal)
              && catalogue.IndexOf("SeatedRest = true,", rests + 1, StringComparison.Ordinal) < 0,
            "cinema fix: the cinema and theatre outing rests while seated, and only it");

        string effects = ModSource(@"Social\OutingScene.Rest.cs");
        string begin = MemberText(effects, "private void BeginRestEffects()");
        string end = MemberText(effects, "internal void EndRestEffects()");
        Check(begin.Contains("if (!_def.SeatedRest)") && begin.Contains("EnergyHelper.RemoveEnergySpender(\"move\");")
              && begin.Contains("EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Bench);")
              && begin.Contains("EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;")
              && end.Contains("EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);"),
            "cinema fix: the seated rest is the game's bench recovery at the default multiplier, and is given back");

        string scene = ModSource(@"Social\OutingScene.cs");
        string goInside = MemberText(scene, "private void GoInside(float now)");
        int rest = goInside.IndexOf("BeginRestEffects();", StringComparison.Ordinal);
        Check(rest > 0 && rest < goInside.IndexOf("StartTimeMachine(", StringComparison.Ordinal)
              && MemberText(scene, "private void Inside(float now)").Contains("EndRestEffects();")
              && MemberText(scene, "public override void End(SceneEnd why)").Contains("EndRestEffects();")
              && ModSource(@"Social\SocialDirector.cs").Contains("(_scene as OutingScene)?.EndRestEffects();"),
            "cinema fix: the rest begins with the skip and ends when it ends, when the outing ends and when the feature faults");
        Check(FixLineEverywhere("a film at the cinema or the theatre with a friend now rests you", "um filme no cinema ou no teatro com um amigo agora faz você descansar",
                                "和朋友去电影院或剧院看电影时，现在会像游戏自带的座位一样让你休息", "Ein Film mit Freunden im Kino oder Theater lässt dich jetzt ausruhen",
                                "un film au cinéma ou au théâtre avec un ami vous repose maintenant", "una película en el cine o el teatro con un amigo ahora te hace descansar",
                                "фильм в кино или театре с другом теперь даёт отдых", "een film in de bioscoop of het theater met een vriend laat je nu uitrusten",
                                "bir arkadaşla sinemada veya tiyatroda izlenen bir film artık sizi"),
            "cinema fix: What's new has the cinema line, with its fix mark, in some row of every language");
    }
}
