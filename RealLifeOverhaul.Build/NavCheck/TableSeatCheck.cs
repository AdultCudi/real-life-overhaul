using System;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Leisure;

// Somebody seated at the chess table faces the board's centre (CityLife/TableSeat.cs; the owner, 21 Oct 2026: "The chess npc
// is not facing the board he is facing somewhere"): from either stool, whichever way the table stands and whichever way its
// axis was flipped towards the player; a body turned off the board is caught and one facing it is not; and over the sources,
// every chess_sit played by a street character is kept at the table and looked at once a second.
internal static partial class Program
{
    private static void TableSeats()
    {
        const float tableX = 241f, tableZ = -346f;   // where the owner's table stood
        bool faces = true, opposite = true, onStool = true, caught = true, turnedCaught = true;
        for (int deg = 0; deg < 360; deg += 5)
        {
            double a = deg * Math.PI / 180.0;
            float alongX = (float)Math.Sin(a), alongZ = (float)Math.Cos(a);
            for (int flip = -1; flip <= 1; flip += 2)
            {
                float ax = alongX * flip, az = alongZ * flip;   // the axis turned towards the player, as ChessCorner.PutOut does
                TableSeat.Stool(tableX, tableZ, ax, az, LeisureReach.StoolOut, 1, out float mineX, out float mineZ);
                TableSeat.Stool(tableX, tableZ, ax, az, LeisureReach.StoolOut, -1, out float theirsX, out float theirsZ);
                onStool &= Near(Distance(mineX - tableX, mineZ - tableZ), LeisureReach.StoolOut, 0.0005f)
                           && Near(Distance(theirsX - tableX, theirsZ - tableZ), LeisureReach.StoolOut, 0.0005f);
                bool both = TableSeat.Way(mineX, mineZ, tableX, tableZ, out float mx, out float mz)
                            & TableSeat.Way(theirsX, theirsZ, tableX, tableZ, out float tx, out float tz);
                // Facing the way the rule gives, each faces the centre; the two face each other across the board.
                faces &= both && TableSeat.Off(tx, tz, theirsX, theirsZ, tableX, tableZ) < 0.1f && TableSeat.Off(mx, mz, mineX, mineZ, tableX, tableZ) < 0.1f;
                opposite &= Near(mx * tx + mz * tz, -1f, 0.0001f);
                // The table's own heading is the way from one stool only: from the other, facing it is facing away from the board.
                caught &= TableSeat.Faces(TableSeat.Off(ax, az, theirsX, theirsZ, tableX, tableZ)) && !TableSeat.Faces(TableSeat.Off(ax, az, mineX, mineZ, tableX, tableZ));
                // Turned a quarter or more, either way, is caught; a few degrees is not.
                for (int turn = -180; turn <= 180; turn += 15)
                {
                    double t = turn * Math.PI / 180.0;
                    float fx = (float)(tx * Math.Cos(t) + tz * Math.Sin(t)), fz = (float)(-tx * Math.Sin(t) + tz * Math.Cos(t));
                    bool off = !TableSeat.Faces(TableSeat.Off(fx, fz, theirsX, theirsZ, tableX, tableZ));
                    turnedCaught &= off == (Math.Abs(turn) > TableSeat.FacedWithin);
                }
            }
        }
        Check(onStool, "table seat: both stools are 0.62 m from the board's centre along the table, whichever way it stands");
        Check(faces, "table seat: the opponent and the player each face the board's centre from their stool, at every table heading and either way the axis was turned");
        Check(opposite, "table seat: the two stools face each other across the board");
        Check(caught, "table seat: the table's own heading faces the board from one stool only; from the other it is caught as facing away");
        Check(turnedCaught, "table seat: a body turned " + TableSeat.FacedWithin + " degrees or more off the board is caught at every heading, and one that faces it is not");

        // Edges: a body with no way to face, a seat on the centre, and a length that is not one.
        Check(TableSeat.Off(0f, 0f, 1f, 0f, 0f, 0f) == 0f && TableSeat.Off(1f, 0f, 0f, 0f, 0f, 0f) == 0f && !TableSeat.Way(0f, 0f, 0.01f, 0f, out _, out _),
            "table seat: no way to face (a zero forward, a seat on the centre) reads as facing, not as a fault");
        Check(Near(TableSeat.Off(0f, -5f, 0f, 0.62f, 0f, 0f), 0f, 0.01f) && Near(TableSeat.Off(0f, 5f, 0f, 0.62f, 0f, 0f), 180f, 0.01f)
              && Near(TableSeat.Off(3f, 0f, 0f, 0.62f, 0f, 0f), 90f, 0.01f), "table seat: the angle off the board does not depend on the forward's length");
        TableSeat.Stool(0f, 0f, 0f, 0f, 0.62f, 1, out float sx, out float sz);
        Check(sx == 0f && sz == 0f, "table seat: a table with no axis puts no stool off its centre");
        Check(TableSeat.FacedWithin > 0f && TableSeat.FacedWithin <= 10f, "table seat: a few degrees off reads as facing, a visible turn does not");

        // Over the sources: every street character playing chess_sit is kept at the table, and looked at once a second.
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string corner = Code(File.ReadAllText(Path.Combine(scripts, "Leisure", "Activities", "ChessGame.cs")));
        string scenes = Code(File.ReadAllText(Path.Combine(scripts, "CityLife", "Activities", "ChessActivity.cs")));
        string person = Code(File.ReadAllText(Path.Combine(scripts, "CityLife", "CityPerson.cs")));
        Check(corner.Contains("person.Work(\"chess_sit\", city.Props, 0f)") && corner.Contains("person.AtTable(table);") && corner.Contains("_opponent.KeepAtTable(")
              && corner.IndexOf("KeepFacing();", StringComparison.Ordinal) >= 0 && corner.IndexOf("KeepFacing();", StringComparison.Ordinal) < corner.IndexOf("if (busy)", StringComparison.Ordinal),
            "table seat source: the street chess opponent is kept at the table from the stool it was seated on, and looked at once a second, a game on or not");
        Check(scenes.Contains("Cast[0].AtTable(_table);") && scenes.Contains("Cast[1].AtTable(_table);") && scenes.Contains(".KeepAtTable(CitySpots.InView("),
            "table seat source: both players of the living city's chess table are kept at it and looked at once a second");
        Check(person.Contains("agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;") && person.Contains("agent.updateRotation = false;")
              && person.Contains("agent.obstacleAvoidanceType = _ownAvoidance;") && person.Contains("agent.updateRotation = _ownTurning;"),
            "table seat source: at a table the agent neither steers away from the table nor turns them, and both are given back off the stool");
        int leaves = 0;
        foreach (string way in new[] { "public bool WalkTo(", "internal void Reset(", "internal void Surrender(", "public bool Sit(", "public bool Lie(" })
        {
            int at = person.IndexOf(way, StringComparison.Ordinal);
            int body = at >= 0 ? person.IndexOf('{', at) : -1;
            int end = body >= 0 ? BraceEnd(person, body) : -1;
            if (end > body && person.Substring(body, end - body).Contains("LeaveTable();"))
            {
                leaves++;
            }
        }
        Check(leaves == 5, "table seat source: every way off the stool (walking, the pool, a car strike, a seat, a bed) gives the agent back (" + leaves + " of 5)");
    }

    private static float Distance(float x, float z) => (float)Math.Sqrt(x * x + z * z);

    // The index of the brace that closes the one at open.
    private static int BraceEnd(string code, int open)
    {
        int depth = 0;
        for (int i = open; i < code.Length; i++)
        {
            depth += code[i] == '{' ? 1 : code[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return i;
            }
        }
        return -1;
    }
}
