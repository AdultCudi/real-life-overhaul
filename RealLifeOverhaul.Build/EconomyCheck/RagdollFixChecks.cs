using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Permadeath;

// Faults found in Scripts/Ragdoll, Permadeath and Life: a car passed through one of the mod's people kneeling by hand,
// whose animator the pose had switched off, because the strike was refused before the pose was cleared (and a pose
// cleared for a throw that then failed was not put back); the memorial stored a character's id with its commas made
// spaces and looked up the raw id; and the hospital trip read the game's building manager with no test that it was there.
internal static partial class Program
{
    private static void RagdollLifeFixes()
    {
        RagdollKneeling();
        PermadeathMemorialIds();
        LifeBuildingManager();
        Check(FixLineEverywhere("no longer passes through a medic", "não atravessa mais um socorrista", "汽车不再穿过跪在街上的急救员", "fährt nicht mehr durch einen Sanitäter",
                                "ne traverse plus un secouriste", "ya no atraviesa a un sanitario", "больше не проезжает сквозь медика", "rijdt niet meer door een hulpverlener",
                                "diz çökmüş bir sağlık görevlisinin"),
            "ragdoll fix: What's new has the kneeling medic's line, with its fix mark, in some row of every language");
    }

    private static void RagdollKneeling()
    {
        string ragdoll = ModSource(@"Ragdoll\Ragdoll.cs");
        string system = ModSource(@"Ragdoll\RagdollSystem.cs");
        string incidents = ModSource(@"Ragdoll\PedestrianIncidents.cs");
        string traffic = ModSource(@"Ragdoll\PedestrianIncidents.Traffic.cs");
        string casualty = ModSource(@"Ragdoll\Casualty.cs");
        string actor = ModSource(@"People\StreetActor.cs");
        string city = ModSource(@"CityLife\CityPeople.cs");
        Check(ragdoll.Contains("public static bool CanTake(BaseHuman character, bool animatorHeldOff = false)")
              && ragdoll.Contains("(!character.animator.enabled && !animatorHeldOff)")
              && system.Contains("Ragdoll.CanTake(character, animatorHeldOff)"),
            "ragdoll fix: the test before a throw can be asked for someone whose animator is off only for a pose set by hand");
        Check(actor.Contains("public bool PosedByHand => Kneeling && Character != null && Character.visible;"),
            "ragdoll fix: kneeling by hand counts only for someone drawn; a hidden character's animator is off for being hidden, and stays refused");
        int asked = Regex.Matches(incidents + traffic, @"CanStrike\(actor\.Character, posedByHand: actor\.PosedByHand\)").Count;
        Check(asked == 2 && !Regex.IsMatch(incidents + traffic, @"CanStrike\(actor\.Character\)") && incidents.Contains("ragdolls.CanBegin(person, posedByHand)")
              && city.Contains("ragdolls.CanBegin(person.Character, person.Actor.PosedByHand)"),
            "ragdoll fix: the player's car, traffic and the living city all strike a kneeling medic as one standing (" + asked + " of 2 strike paths)");
        string begin = MemberText(casualty, "public bool BeginActor(StreetActor actor, Vector3 velocity, Outcome outcome, bool byPlayer, float now)");
        int pre = begin.IndexOf("ragdolls.CanBegin(actor.Character, byHand)", StringComparison.Ordinal);
        int clear = begin.IndexOf("actor.ClearPose();", StringComparison.Ordinal);
        int back = begin.IndexOf("actor.KneelByHand();", StringComparison.Ordinal);
        Check(pre > 0 && clear > pre && back > clear && !begin.Contains("SetVisible(true)"),
            "ragdoll fix: BeginActor asks first, then clears the pose, and kneels them again if the throw is refused after all; nobody hidden is shown for it");
    }

    private static void PermadeathMemorialIds()
    {
        string list = PermadeathRules.MemorialAdd(string.Empty, "save,7");
        list = PermadeathRules.MemorialAdd(list, "plain");
        string again = PermadeathRules.MemorialAdd(list, "save,7");
        Check(list == "save 7,plain" && again == list, "permadeath memorial: an id with a comma is stored once, its comma a space (" + list + ")");
        Check(PermadeathRules.MemorialHas(list, "save,7") && PermadeathRules.MemorialHas(list, "plain") && !PermadeathRules.MemorialHas(list, "save")
              && !PermadeathRules.MemorialHas(list, "7") && !PermadeathRules.MemorialHas(list, "plai") && !PermadeathRules.MemorialHas(null, "plain")
              && !PermadeathRules.MemorialHas(list, null) && !PermadeathRules.MemorialHas(",,", ""),
            "permadeath memorial: an id is found as it was stored, comma or not, and no part of one stands for another");
        string game = ModSource(@"Permadeath\PermadeathGame.cs");
        Check(Regex.Matches(game, @"PermadeathRules\.MemorialHas\(").Count == 2 && game.Contains("PermadeathRules.MemorialAdd(list, characterId)")
              && !game.Contains("characterId.Replace(") && !game.Contains("private static bool Has("),
            "permadeath memorial: keeping and finding a character use the one rule, so a comma in an id never hides a death");
    }

    private static void LifeBuildingManager()
    {
        string life = ModSource(@"Life\LifeEvents.cs");
        string prison = ModSource(@"Justice\PrisonStay.cs");
        Check(life.Contains("if (BuildingManager.IsInsideBuilding && buildings != null)") && life.Contains("(buildings != null && buildings.exitingBuilding)")
              && prison.Contains("if (BuildingManager.IsInsideBuilding && buildings != null)") && prison.Contains("|| buildings.exitingBuilding)"),
            "life fix: the hospital trip and the release from prison read the building manager once and test it, as the custody trip does");
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var bare = new System.Collections.Generic.List<string>();
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string[] lines = System.IO.File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                // Outside the building the test before || is false, and the manager is read whether it is there or not.
                if (Regex.IsMatch(lines[i], @"!?BuildingManager\.IsInsideBuilding\s*\|\|\s*InstanceBehavior<BuildingManager>\.Instance\."))
                {
                    bare.Add(System.IO.Path.GetFileName(path) + ":" + (i + 1));
                }
            }
        }
        Check(bare.Count == 0, "life fix: no wait on leaving a building reads the building manager untested once outside" + (bare.Count > 0 ? " (" + string.Join(", ", bare) + ")" : string.Empty));
    }
}
