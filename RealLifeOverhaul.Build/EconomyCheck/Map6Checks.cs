using System;
using RealLifeOverhaul.Ui;

// The faults of the second look at the city map's dock, checked game-free: a fresh open reads the counts and the groups again though nothing looked while it was shut, the
// district page follows the district pointed at, and the game's building box is hidden by alpha and raycast alone and given back. Part of `--map5`.
internal static partial class Program
{
    private static void Map6Checks()
    {
        // The dock as the screen drives it: a layout reads the city's count when the look says it is stale. Nothing looks while the dock is hidden, so a close is never seen.
        var look = new MapLook();
        int city = 3, shown = -1;
        void Lay(float now)
        {
            look.Look(true, now);
            if (look.CountsStale)
            {
                shown = city;
                look.CountsStale = false;
            }
        }
        Lay(10f);
        Lay(10.25f);
        Check(shown == 3, "map dock: the first open counts the places on each line");
        city = 5;
        Lay(10.5f);
        Check(shown == 3, "map dock: while the map stays open the counts are not read again (the game is paused, nothing changes)");
        Lay(60f);
        Check(shown == 5, "map dock: opened again after a close (no look in between), the new count shows, not the first open's");
        look.GroupsStale = false;
        look.Look(true, 61f);
        Check(!look.GroupsStale, "map dock: a look a moment after the last one leaves the groups as they are");
        look.Look(true, 90f);
        Check(look.GroupsStale && look.CountsStale, "map dock: a look long after the last one finds the groups and counts stale, as a fresh open does");
        look.GroupsStale = look.CountsStale = false;
        look.Look(false, 91f);
        look.Look(true, 91.2f);
        Check(look.GroupsStale && look.CountsStale, "map dock: a close seen by a look and an open after it is a fresh open too");

        // The district page: the stamp it is drawn from moves with the district pointed at, and only on that page.
        int pointedA = 10, pointedB = 11;
        Check(MapLook.DistrictStamp(true, pointedA) != MapLook.DistrictStamp(true, pointedB), "map district page: pointing at a second district changes what the dock is drawn from");
        Check(MapLook.DistrictStamp(false, pointedA) == MapLook.DistrictStamp(false, pointedB), "map dock: the other pages do not relayout for a district pointed at");

        string live = Map5Source("Ui/MapDock/MapLive.cs");
        Check(live.Contains("DistrictOwner.KitStamp") && live.Contains("MapLook.DistrictStamp(true, DistrictOwner.KitStamp)"),
            "map district page: the dock's signature holds the district view's stamp while the district page shows");
        int at = live.IndexOf("DistrictOwner.KitPoint(", StringComparison.Ordinal);
        int dirty = at < 0 ? -1 : live.IndexOf("_dirty = true", at, StringComparison.Ordinal);
        Check(at >= 0 && dirty > at && dirty - at < 260 && live.Contains("DistrictOwner.KitStamp != stamp"),
            "map district page: pointing at a row sets the page dirty when the district pointed at changed");
        int lay = live.IndexOf("public void Lay(", StringComparison.Ordinal);
        int looked = lay < 0 ? -1 : live.IndexOf("_look.Look(", lay, StringComparison.Ordinal);
        int build = lay < 0 ? -1 : live.IndexOf("Build();", lay, StringComparison.Ordinal);
        Check(looked > lay && looked < build, "map dock: a layout looks at the map before it builds, so a fresh open is counted afresh");

        // The game's building box: hidden through the host's conceal (a canvas group's alpha and raycast), never switched off, and not for the subway list.
        string game = Map5Source("Ui/MapDock/MapFiltersGame.cs");
        string host = Map5Source("Ui/Host/UiConceal.cs");
        Check(game.Contains("conceal.Add(CardPanel())") && game.Contains("if (!_subway)") && !game.Contains("SetActive("),
            "map dock: the game's building box is hidden with the filters' dock, by the conceal alone, and never switched off");
        Check(host.Contains("Group.alpha = 0f") && host.Contains("blocksRaycasts = l.KeepBlocking && l.Blocks") && host.Contains("l.Group.alpha = l.Alpha") && !host.Contains("SetActive("),
            "map dock: the conceal gives the box its alpha and raycast back exactly as found when the host hides, switches off or is removed");

        // The minimap's destination text is joined only when its parts change.
        string dest = Map5Source("Navigation/NavHud.Destination.cs");
        Check(dest.Contains("minutes != _minutesShown || distance != _distanceShown"), "minimap: the minutes and distance are joined into one text only when a part changed");
    }
}
