using Newtonsoft.Json.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

public sealed class BuildingRegistration
{
    public string Place = "synthetic";
    public bool HasValidAddress = true;
    public string StreetName = "ba:street_test";
    public int StreetNumber = 1;
}

namespace RealLifeOverhaul.Economy
{
    internal static class CompanyGame
    {
        public static string Place(BuildingRegistration reg) => reg.Place;
    }

    // Substitute only the native director lifecycle; the linked ownership methods below ship unchanged.
    public sealed partial class CompanyDirector
    {
        private bool _running = true, _broken = false, _mgrBroken = false;
        private readonly ManagerState _mgr;
        private readonly CompanyState _state;
        private int Today => 12;
        private bool On(CompanyPart part) => _running && !_broken;
        public CompanyDirector(ManagerState managers, CompanyState company) { _mgr = managers; _state = company; }
        public void FixtureUnavailable() { _running = false; }
    }
}

internal static partial class Program
{
    private static void CompanionManagerChecks()
    {
        var managers = new ManagerState();
        var company = new CompanyState();
        var manager = new ManagerFile { Place = "synthetic", Id = "employee", Role = ManagerCatalogue.RolePlace };
        managers.Managers.Add(manager);
        var director = new CompanyDirector(managers, company);
        var reg = new BuildingRegistration();
        var target = new JObject { ["itemIds"] = new JArray("held") };
        manager.Tasks = 1 << (int)ManagerTask.Rota;
        string before = ManagerStore.Write(managers);
        Check(CompanionSnapshot.Json(director.CompanionOwnership("schedule", target, reg)).Contains("manager-controls-scheduling"),
            "ownership: active manager scheduling is conditional without handing it over");
        Check(CompanionSnapshot.Json(director.CompanionOwnership("hire", target, reg)).Contains("manager-controls-scheduling"),
            "ownership: moving or hiring at a manager-scheduled site remains guarded");
        Check(ManagerStore.Write(managers) == before, "ownership: scheduling query preserves manager state bytes");
        manager.Tasks = 1 << (int)ManagerTask.Hiring;
        Check(CompanionSnapshot.Json(director.CompanionOwnership("hire", target, reg)).Contains("manager-controls-hiring"),
            "ownership: hiring owner is conditional without disabling replacement hiring");
        manager.Tasks = 1 << (int)ManagerTask.Stock;
        Check(CompanionSnapshot.Json(director.CompanionOwnership("wholesale", target, reg)).Contains("stock-line-not-player-held"),
            "ownership: unheld stock line is refused");
        manager.Held.Add("held");
        before = ManagerStore.Write(managers);
        Check(director.CompanionOwnership("wholesale", target, reg) == null, "ownership: already held stock line allows continuation");
        ((JArray)target["itemIds"]).Add("unheld");
        Check(CompanionSnapshot.Json(director.CompanionOwnership("wholesale", target, reg)).Contains("stock-line-not-player-held"),
            "ownership: mixed held and unheld batch is refused");
        Check(ManagerStore.Write(managers) == before, "ownership: allowed and refused stock queries never create player holds");
        var week = new JArray(new JObject { ["isOpen"] = true, ["hours"] = new JArray(new JObject { ["from"] = 0, ["to"] = 8 }) });
        Check(CompanionSnapshot.Json(director.CompanionPermit(reg, week)).Contains("late-opening-permit-missing"),
            "ownership: late hours without an owner permit are refused");
        director.FixtureUnavailable();
        Check(CompanionSnapshot.Json(director.CompanionOwnership("hire", target, reg)).Contains("manager-state-unavailable"),
            "ownership: inactive owner never authorizes a write");
    }
}
