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
        Check(MemberText(spots, "private static void Setup()").Contains("_crosswalkLayer = LayerMask.NameToLayer(\"Crosswalk\");")
              && clear.Contains("Setup();") && clear.Contains("Physics.AllLayers, QueryTriggerInteraction.Collide")
              && clear.Contains("hit.gameObject.layer == _crosswalkLayer || hit.CompareTag(\"Crosswalk\")"),
            "chess fix: crossings count by layer or tag on any layer, including triggers, without the traffic features");
        Check(spots.Contains("private static readonly Collider[] ChessOverlaps = new Collider[64];")
              && clear.Contains("bool full = count >= ChessOverlaps.Length;") && clear.Contains("crossing = true;")
              && clear.Contains("ChessOverlaps[i] = null;") && clear.Contains("return !crossing && !full;"),
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
              && clear.Contains("Chess check (crossing clearance): a crossing under the table, stools, approaches or onlookers, the place refused: PASS")
              && clear.Contains("Chess check (crossing clearance): more colliders than the query holds, the place refused in case one is a crossing: PASS")
              && clear.Contains("Chess check (crossing clearance): no crossing under the table, stools, approaches or onlookers: PASS"),
            "chess fix: diagnostics say, only when choosing a place, whether a crossing or a full query refused it");
        Check(MemberText(ModSource(@"Core\GameContract.cs"), "public static readonly string[] Layers =").Contains("\"Crosswalk\""),
            "chess fix: the Crosswalk layer is in the game contract, so the guard names it in Player.log if the game drops it");
        Check(FixLineEverywhere("street chess tables no longer block pedestrian crossings",
                                "mesas de xadrez na rua não bloqueiam mais as faixas de pedestres",
                                "街头棋桌不会再挡住人行横道", "Schachtische auf der Straße blockieren keine Fußgängerüberwege mehr",
                                "tables d'échecs dans la rue ne bloquent plus les passages piétons",
                                "mesas de ajedrez en la calle ya no bloquean los pasos de peatones",
                                "шахматные столы на улице больше не перекрывают пешеходные переходы",
                                "schaaktafels op straat blokkeren geen zebrapaden meer",
                                "sokaktaki satranç masaları artık yaya geçitlerini kapatmıyor"),
            "chess fix: What's new has the crossing line, with its fix mark, in some row of every language");
    }
}
