using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Utilities;

// A store's line it cannot read, or of a later version, is kept in the save exactly as it was read (Core/SaveLines),
// whatever the feature writes there afresh; a line it reads is the feature's to write.
internal static partial class Program
{
    private static void SaveLinesKept()
    {
        SaveLines.Told = null;
        string good = CityStore.Write(CityModel.Open(4242, 1));
        string later = good.Replace("v=" + CityStore.Version + "~", "v=" + (CityStore.Version + 1) + "~");
        var save = new Dictionary<string, string>
        {
            { CityStore.Key, "a line damaged beyond reading" },
            { UtilitiesStore.Key, "nonsense" },
        };
        CityState city = CityStore.Load(save, CityStore.Key, out int version);
        UtilitiesState utilities = UtilitiesStore.Load(save, out version);
        Check(city == null && utilities == null, "save lines: a damaged line gives no state (the feature starts afresh)");
        Check(SaveLines.IsKept(save, CityStore.Key) && SaveLines.IsKept(save, UtilitiesStore.Key), "save lines: the damaged lines are kept");

        // The features write their fresh state at the save; the kept lines go back as they were read.
        save[CityStore.Key] = good;
        save.Remove(UtilitiesStore.Key);
        SaveLines.PutBack(save);
        Check(save[CityStore.Key] == "a line damaged beyond reading" && save[UtilitiesStore.Key] == "nonsense", "save lines: a damaged line is never written over or removed");

        // Another save: a line of a later version is read as far as this version can, and kept as it is.
        var next = new Dictionary<string, string> { { CityStore.Key, later } };
        CityState read = CityStore.Load(next, CityStore.Key, out version);
        Check(read != null && version == CityStore.Version + 1 && SaveLines.IsKept(next, CityStore.Key), "save lines: a later version's line is read and kept");
        next[CityStore.Key] = CityStore.Write(read);
        SaveLines.PutBack(next);
        Check(next[CityStore.Key] == later, "save lines: a later version's line goes back as it was, not with less in it");
        Check(!SaveLines.IsKept(save, CityStore.Key), "save lines: a save read later has its own kept lines");

        // A line this version reads is the feature's.
        var plain = new Dictionary<string, string> { { CityStore.Key, good } };
        Check(CityStore.Load(plain, CityStore.Key, out version) != null && !SaveLines.IsKept(plain, CityStore.Key), "save lines: a good line is not kept");
        plain[CityStore.Key] = "changed";
        Check(SaveLines.PutBack(plain) == 0 && plain[CityStore.Key] == "changed", "save lines: a good line is written as the feature writes it");
        SaveLines.Forget();
    }
}
