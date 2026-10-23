namespace RealLifeOverhaul.NewGround
{
    /// <summary>The locale keys of new ground, written out whole so the text checks find them.</summary>
    public static class NewGroundText
    {
        public const string Title = "reallife_meta_title_newground";                 // the feature's name
        public const string About = "reallife_guide_newground_about";                // one sentence: what it is
        public const string TrialRoad = "reallife_newground_trial_road";             // the developer option: a test road at the next load
        public const string TestUnder = "reallife_newground_test_under";             // "Walk: what is under me"
        public const string TestPatch = "reallife_newground_test_patch";             // "Walk: build a test patch"
        public const string TestGrow = "reallife_newground_test_grow";               // "Walk: grow the city's own mesh by one tile"
        public const string TestLink = "reallife_newground_test_link";               // "Walk: link the patch"
        public const string TestCensus = "reallife_newground_test_census";           // "Roads: census"
        public const string TestRoad = "reallife_newground_test_road";               // "Roads: add a test road at the next load"
        public const string TestParking = "reallife_newground_test_parking";         // "Parking: copy a lane"

        public const string Under = "reallife_newground_under";                      // {player}, {people}: of the 9 points
        public const string Patch = "reallife_newground_patch";                      // {ptiles}, {pms}, {ntiles}, {nms}, {path}
        public const string Grow = "reallife_newground_grow";                        // {player}, {people}: the paths onto the new tile
        public const string Link = "reallife_newground_link";                        // {player}, {people}: the paths over the link
        public const string Census = "reallife_newground_census";                    // {points}, {roads}, {junctions}, {row}, {column}, {rows}, {columns}
        public const string RoadOn = "reallife_newground_road_on";                   // the test road comes at the next load
        public const string RoadOff = "reallife_newground_road_off";                 // no test road at the next load
        public const string RoadAdded = "reallife_newground_road_added";             // {points}: the test road is in the traffic
        public const string RoadDrove = "reallife_newground_road_drove";             // {cars}: drove it in two game minutes
        public const string Parking = "reallife_newground_parking";                  // {metres}, {cars}, {spots}
        public const string Back = "reallife_newground_back";                        // the test ground is given back

        public const string NoMeshes = "reallife_newground_refused_meshes";          // the city's walking meshes were not found
        public const string NoRoom = "reallife_newground_refused_room";              // no clear open ground for a patch
        public const string NoTile = "reallife_newground_refused_tile";              // no empty tile beside the walking mesh
        public const string NoRoads = "reallife_newground_refused_roads";            // the city's road data was not found
        public const string Session = "reallife_newground_refused_session";          // not in a Going Public session
        public const string NoLane = "reallife_newground_refused_lane";              // no parking lane with room along it
        public const string Builder = "reallife_newground_refused_builder";          // the engine's builder did not do what the test needs
        public const string Busy = "reallife_newground_refused_busy";                // the last walking test is still being given back
    }
}
