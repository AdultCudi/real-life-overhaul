using System;
using System.IO;

// Checks, over the sources, that the mod's pedestrian zones never meet the game's dropped pools. The fault: at the quit of
// 2 Nov 2026 the game's PoolingManager had already disposed its pools (its OnDestroy), and Uptown's GroundDirector.OnDestroy
// then cleared its zones: StationaryAiSpawner.ClearPedestrians asked Pool.GetPoolHandler for 'CasualStationaryAiPool' and
// threw KeyNotFoundException. Now the zones are cleared on the mod's unload event, while the pools stand, and taking a zone
// away never throws when the pools are gone.
internal static partial class Program
{
    private static void Unzones()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string people = Source(scripts, @"NewGround\PeopleGame.cs");
        string mod = Source(scripts, @"Core\RealLifeOverhaulMod.cs");
        string director = Source(scripts, @"Uptown\GroundDirector.cs");
        if (people.Length == 0 || mod.Length == 0 || director.Length == 0)
        {
            Check(false, "unzone: the mod's sources are at " + scripts);
            return;
        }
        string unzone = Body(people, "public static void Unzone(ref StationaryAiSpawner zone)");
        int asked = unzone.IndexOf("PoolsStand(zone)", StringComparison.Ordinal);
        int cleared = unzone.IndexOf("zone.ClearPedestrians();", StringComparison.Ordinal);
        Check(asked >= 0 && cleared > asked && Count(unzone, "ClearPedestrians") == 1, "unzone: a zone is cleared only after the game's pools are asked for (PeopleGame.Unzone)");
        Check(Body(people, "private static bool PoolStands(").Contains("PoolingManager.GetPool(") && Body(people, "private static bool PoolStands(").Contains("catch (KeyNotFoundException)"),
            "unzone: a pool the game has dropped answers no and does not throw (PeopleGame.PoolStands)");
        Check(unzone.Contains("StationaryAiSpawner.AllPedestrianSpawners.Remove(zone)") && unzone.Contains("SceneryTrees.Destroy(zone.gameObject)"),
            "unzone: with the pools gone the zone is still taken off the game's list and destroyed");
        int city = mod.IndexOf("public class RealLifeOverhaulCity", StringComparison.Ordinal);
        string unload = city < 0 ? string.Empty : Body(mod.Substring(city), "public Task OnUnloadAsync()");   // the city part's, not the menu part's
        int zones = unload.IndexOf("Uptown.GroundDirector.Unloading(", StringComparison.Ordinal);
        int root = unload.IndexOf("UnityEngine.Object.Destroy(_root)", StringComparison.Ordinal);
        Check(zones >= 0 && root > zones, "unzone: Uptown's zones are cleared on the unload event, before the mod's root is destroyed");
        string unloading = Body(director, "public static void Unloading(");
        Check(unloading.Contains("GroundGame.Unpopulate()") && unloading.Contains("pedestrians' zone(s) cleared while the game's pools stood"),
            "unzone: the unload event unpopulates the zones and the stopped line notes how many were cleared");
        Check(Body(director, "private void OnDestroy()").Contains("PeopleGame.PoolsGone"), "unzone: GroundDirector.OnDestroy reports zones that met dropped pools");
        string others = null;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = Code(File.ReadAllText(path));
            if ((code.Contains(".ClearPedestrians(") || code.Contains("PoolingManager.")) && Path.GetFileName(path) != "PeopleGame.cs")
            {
                others = path;
                break;
            }
        }
        Check(others == null, "unzone: no other source of the mod calls the game's pools" + (others != null ? " (" + others + ")" : string.Empty));
    }
}
