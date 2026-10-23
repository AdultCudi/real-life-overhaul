using RealLifeOverhaul;
using RealLifeOverhaul.Utilities;

namespace Helpers
{
    public static class BusinessTypeHelper
    {
        public static readonly string[] BusinessTypeNames = { "ba:businesstype_giftshop" };
    }
}

namespace RealLifeOverhaul.Utilities
{
    internal static class UtilitiesGame
    {
        public static string Place(BuildingRegistration reg) => reg.Place;
        public static PlaceKind KindOf(string type) => PlaceKind.Retail;
    }

    // Substitute native lifecycle fields only; the linked capture method and account projection ship unchanged.
    public sealed partial class UtilitiesDirector
    {
        private bool _loaded, _failed, _gaveUp;
        private UtilitiesState _state;
        private GameInstance _save;
        public UtilitiesDirector(GameInstance save, UtilitiesState state, bool phoneFailed, bool failed = false)
        {
            _save = save;
            _state = state;
            _loaded = true;
            _gaveUp = phoneFailed;
            _failed = failed;
        }
        public bool FixturePhoneFailed => _gaveUp;
    }
}

internal static partial class Program
{
    private static void CompanionUtilitiesLifecycleChecks()
    {
        var save = new GameInstance();
        save.BuildingRegistrations.Add(new BuildingRegistration());
        var state = new UtilitiesState { LastDay = 12, MonthStart = 10 };
        state.Accounts.Add(new RealLifeOverhaul.Utilities.Account { Place = "synthetic", Service = Service.Power,
            State = SupplyState.Active, Accrued = 1234, Owed = 87 });
        SaveGameManager.Current = save;
        string before = UtilitiesStore.Write(state);
        var director = new UtilitiesDirector(save, state, true);
        CompanionModule captured = director.CompanionUtilities();
        string json = CompanionSnapshot.Json(captured.Data);
        Check(captured.Status == "ready" && json.Contains("\"accruedCents\":1234") && json.Contains("\"owedCents\":8700"),
            "companion: phone installation failure preserves live utility accounts and balances");
        Check(director.FixturePhoneFailed && UtilitiesStore.Write(state) == before && save.BuildingRegistrations.Count == 1,
            "companion: utility capture after phone failure preserves state and its failure flag");
        Check(new UtilitiesDirector(save, state, true, true).CompanionUtilities().Status == "failed",
            "companion: actual utility director failure still withholds inactive books");
        SaveGameManager.Current = new GameInstance();
        Check(director.CompanionUtilities().Status == "unavailable",
            "companion: utility books from a different save remain unavailable");
        SaveGameManager.Current = null;
    }
}
