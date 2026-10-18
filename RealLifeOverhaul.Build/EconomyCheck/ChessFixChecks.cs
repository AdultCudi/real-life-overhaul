using System;

// Crossing clearance is a placement query, so these checks hold its shape, trigger handling and both callers as text.
internal static partial class Program
{
    private static void ChessFixes()
    {
        string spots = ModSource(@"CityLife\CitySpots.cs");
        string clear = MemberText(spots, "public static bool ChessClear(Vector3 table, Vector3 along)");
        Check(spots.Contains("private static readonly Vector3 ChessHalf = new Vector3(2f, 1.5f, 1.85f);")
              && clear.Contains("Physics.OverlapBoxNonAlloc(table + Vector3.up * 0.5f, ChessHalf, ChessOverlaps,")
              && clear.Contains("Quaternion.LookRotation(along)"),
            "chess fix: one oriented box covers the table, stools, approaches and either onlooker after pavement snapping");
        Check(spots.Contains("LayerMask.NameToLayer(\"Crosswalk\")")
              && clear.Contains("Physics.AllLayers, QueryTriggerInteraction.Collide")
              && clear.Contains("hit.gameObject.layer == CrosswalkLayer || hit.CompareTag(\"Crosswalk\")"),
            "chess fix: crossings count by layer or tag on any layer, including triggers, without the traffic features");
        Check(spots.Contains("private static readonly Collider[] ChessOverlaps = new Collider[64];")
              && clear.Contains("bool clear = count < ChessOverlaps.Length;") && clear.Contains("clear = false;")
              && clear.Contains("ChessOverlaps[i] = null;") && clear.Contains("return clear;"),
            "chess fix: the query reuses its buffer, releases collider references and refuses a full buffer");

        string leisure = ModSource(@"Leisure\Activities\ChessGame.cs");
        string put = MemberText(leisure, "public bool PutOut(Vector3 anchor, Vector3 player, out TestRefusal why)");
        int leisureCheck = put.IndexOf("|| !CitySpots.ChessClear(table, along))", StringComparison.Ordinal);
        int leisureProp = put.IndexOf("Transform prop = city.Props.Stand(", StringComparison.Ordinal);
        Check(leisureCheck > 0 && leisureProp > leisureCheck
              && put.Substring(leisureCheck, leisureProp - leisureCheck).Contains("return false;"),
            "chess fix: Leisure refuses an overlapping candidate before creating props or people");

        string city = ModSource(@"CityLife\Activities\StandingScenes.cs");
        string find = MemberText(city, "public override bool Find(CitySite site)");
        int cityCheck = find.IndexOf("|| !CitySpots.ChessClear(_table, along)", StringComparison.Ordinal);
        int accepted = find.IndexOf("Centre = _table;", StringComparison.Ordinal);
        Check(cityCheck > 0 && accepted > cityCheck
              && find.Substring(cityCheck, accepted - cityCheck).Contains("return false;"),
            "chess fix: ambient chess refuses an overlapping candidate before accepting the site or beginning the scene");

        Check(clear.Contains("if (Meta.Dev.Verbose)")
              && clear.Contains("Chess check (crossing clearance): the whole setup misses crossings: PASS.")
              && clear.Contains("candidate refused (crossing overlap or full query buffer): FAIL."),
            "chess fix: diagnostics report crossing clearance only when choosing a place");
        Check(FixLineEverywhere("street chess tables no longer block pedestrian crossings",
                                "mesas de xadrez na rua não bloqueiam mais as faixas de pedestres",
                                "街头棋桌不会再挡住人行横道", "Schachtische auf der Straße blockieren keine Fußgängerüberwege mehr",
                                "tables d'échecs dans la rue ne bloquent plus les passages piétons",
                                "mesas de ajedrez en la calle ya no bloquean los pasos de peatones",
                                "шахматные столы на улице больше не перекрывают пешеходные переходы"),
            "chess fix: What's new has the crossing line, with its fix mark, in some row of every language");
    }
}
