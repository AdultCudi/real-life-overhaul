using System;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.GameUi;

// A job's panel over a place out of the picture (Core/GameUi/PanelEdgeRules.cs, WorldPanel.KeepInView): the stocker's
// shelf was shown no prompt at all. The game's indoor camera (the owner's Player.log: 13 m from the player along
// (0, 0.86, -0.51), lens 49; PedestrianCam puts it there and turns it only round the player) sees the floor from about
// 5 m behind the player to 10 m ahead, the game hides its guider indoors, and the job's panel stood over the shelf
// itself, so a shelf further off had its panel drawn off the screen (and one 5 m ahead had most of its box above the
// top). The panel's only care was a place behind the camera, which that camera never has. Now such a panel waits at
// the screen's edge toward the place, its whole box on the screen.
internal static partial class Program
{
    private const float IndoorDistance = 13f;   // metres: the game's indoor camera from the player (Player.log, "indoor vanilla")
    private const float IndoorLens = 49f;       // degrees: its vertical lens
    private const float Wide = 16f / 9f;        // the picture's width over its height at 1080p

    private static void PanelEdges()
    {
        PanelEdgeIndoors();
        PanelEdgeBehind();
        PanelEdgeBounds();
        PanelEdgeTimed();
        PanelEdgeSources();
    }

    // The place in the indoor camera's space: x right, y up, z ahead, the camera looking at the player's feet.
    private static void IndoorView(float px, float py, float pz, out float x, out float y, out float z)
    {
        const float offY = 0.86f, offZ = -0.51f;
        float camY = offY * IndoorDistance, camZ = offZ * IndoorDistance;
        float gx = px, gy = py - camY, gz = pz - camZ;
        float n = (float)Math.Sqrt(offY * offY + offZ * offZ);
        float fy = -offY / n, fz = -offZ / n;   // ahead: toward the player
        float uy = -offZ / n, uz = offY / n;    // up across the picture, square to ahead
        x = gx;
        y = gy * uy + gz * uz;
        z = gy * fy + gz * fz;
    }

    private static bool OldInFront(float z) => z >= 0.5f;   // JobPanel.Fit until now: only a place nearer than half a metre ahead of the lens was moved

    private static float TanHalf => (float)Math.Tan(IndoorLens * 0.5 * Math.PI / 180.0);

    private static void PanelEdgeIndoors()
    {
        // A shelf 12 m ahead, 8 m behind and 14 m to the right of a player who just came in, its panel 1.6 m up.
        float[][] shelves = { new[] { 0f, 1.6f, 12f }, new[] { 0f, 1.6f, -8f }, new[] { 14f, 1.6f, 0f }, new[] { -14f, 1.6f, 0f } };
        string[] said = { "12 m ahead", "8 m behind", "14 m to the right", "14 m to the left" };
        for (int i = 0; i < shelves.Length; i++)
        {
            IndoorView(shelves[i][0], shelves[i][1], shelves[i][2], out float x, out float y, out float z);
            float across = 0.5f + 0.5f * x / (z * TanHalf * Wide), up = 0.5f + 0.5f * y / (z * TanHalf);
            bool off = across < 0f || across > 1f || up < 0f || up > 1f;
            Check(off && OldInFront(z), "panel edge (the fault): with the game's indoor camera a shelf " + said[i] + " is out of the picture (at " + across.ToString("0.00")
                                         + ", " + up.ToString("0.00") + "), and the old test took it for a place ahead, so its panel was drawn off the screen");
            bool moved = PanelEdgeRules.Edge(x, y, z, TanHalf, Wide, out float a, out float u);
            bool side = i == 0 ? OnLine(u, 1f - PanelEdgeRules.HighMargin) && Math.Abs(a - 0.5f) < 0.01f
                : i == 1 ? OnLine(u, PanelEdgeRules.LowMargin) && Math.Abs(a - 0.5f) < 0.01f
                : i == 2 ? OnLine(a, 1f - PanelEdgeRules.SideMargin)
                : OnLine(a, PanelEdgeRules.SideMargin) && u > 0.5f;
            Check(moved && side, "panel edge: a shelf " + said[i] + " has its panel at the picture's edge on that side (" + a.ToString("0.00") + ", " + u.ToString("0.00") + ")");
        }

        // A shelf only 5 m ahead is in the picture, but the box stands up from its point (pivot 0.5, 0) and most of it would be
        // above the top of the screen: it is brought down until the whole box is on the screen.
        IndoorView(0f, 1.6f, 5f, out float fx, out float fy, out float fz);
        float high = 0.5f + 0.5f * fy / (fz * TanHalf);
        Check(high > 1f - PanelEdgeRules.HighMargin && high < 1f && PanelEdgeRules.Edge(fx, fy, fz, TanHalf, Wide, out _, out float fu) && OnLine(fu, 1f - PanelEdgeRules.HighMargin),
            "panel edge: a shelf 5 m ahead, in the picture at " + high.ToString("0.00") + " of its height, has its box brought down whole onto the screen");

        // A shelf within reach stays where it is: the panel is over it.
        IndoorView(1f, 1.6f, 0.5f, out float nx, out float ny, out float nz);
        Check(!PanelEdgeRules.Edge(nx, ny, nz, TanHalf, Wide, out float na, out float nu) && na > 0.5f && nu > 0.4f && nu < 0.7f,
            "panel edge: a shelf beside the player is in the picture and its panel stands over it");
    }

    private static bool OnLine(float a, float b) => Math.Abs(a - b) < 0.001f;

    private static void PanelEdgeBehind()
    {
        // Behind the lens the game's projection mirrors a place: the edge is on the side it truly is.
        Check(PanelEdgeRules.Edge(5f, 0f, -3f, TanHalf, Wide, out float a, out float u) && OnLine(a, 1f - PanelEdgeRules.SideMargin),
            "panel edge: a place behind the lens to the right is at the right edge, not mirrored to the left");
        Check(PanelEdgeRules.Edge(-5f, 1f, -3f, TanHalf, Wide, out a, out u) && OnLine(a, PanelEdgeRules.SideMargin),
            "panel edge: a place behind the lens to the left is at the left edge");
        Check(PanelEdgeRules.Edge(0f, 0f, -3f, TanHalf, Wide, out a, out u) && OnLine(a, 0.5f) && OnLine(u, PanelEdgeRules.LowMargin),
            "panel edge: a place straight behind the lens is at the bottom's middle, the way the player turns to it");
        Check(PanelEdgeRules.Edge(0f, 0f, 0f, TanHalf, Wide, out a, out u) && OnLine(u, PanelEdgeRules.LowMargin),
            "panel edge: a place at the lens itself has a point on the edge");
        Check(PanelEdgeRules.Edge(3f, 0f, 10f, 0f, Wide, out a, out u) && a >= 0f && a <= 1f && u >= 0f && u <= 1f,
            "panel edge: a lens with no angle gives a point in the picture, never a division by zero");
    }

    // Wherever the place is, the panel's point is on the screen with room for the box.
    private static void PanelEdgeBounds()
    {
        int moved = 0, kept = 0, outside = 0, unsteady = 0;
        for (int ix = -20; ix <= 20; ix++)
        {
            for (int iy = -10; iy <= 10; iy++)
            {
                for (int iz = -10; iz <= 30; iz += 2)
                {
                    float x = ix * 1.7f, y = iy * 1.3f, z = iz * 1.1f;
                    bool edge = PanelEdgeRules.Edge(x, y, z, TanHalf, Wide, out float a, out float u);
                    moved += edge ? 1 : 0;
                    kept += edge ? 0 : 1;
                    if (a < PanelEdgeRules.SideMargin - 0.0001f || a > 1f - PanelEdgeRules.SideMargin + 0.0001f || u < PanelEdgeRules.LowMargin - 0.0001f
                        || u > 1f - PanelEdgeRules.HighMargin + 0.0001f)
                    {
                        outside++;
                    }
                    PanelEdgeRules.Edge(x, y, z, TanHalf, Wide, out float a2, out float u2);
                    unsteady += a2 == a && u2 == u ? 0 : 1;
                }
            }
        }
        Check(outside == 0 && moved > 0 && kept > 0 && unsteady == 0, "panel edge: over " + (moved + kept) + " places round the camera every point is on the screen with room for the box ("
                                                                     + moved + " moved to the edge, " + kept + " over their place; " + outside + " outside, " + unsteady + " not the same twice)");
    }

    private static void PanelEdgeTimed()
    {
        Edges(1000, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long ticks = Edges(1000000, out int moved);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        double each = ticks * 1000000000.0 / System.Diagnostics.Stopwatch.Frequency / 1000000.0;
        Check(bytes == 0 && moved > 0 && each < 200.0, "panel edge: the edge asked every frame costs " + each.ToString("0") + " ns and allocates " + bytes + " bytes (wanted under 200 ns, none)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long Edges(int count, out int moved)
    {
        moved = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            moved += PanelEdgeRules.Edge((i % 61) - 30f, (i % 23) - 11f, (i % 41) - 8f, 0.4557f, 1.7778f, out _, out _) ? 1 : 0;
        }
        watch.Stop();
        return watch.ElapsedTicks;
    }

    // Over the sources: every job's panel is kept in view, the game's own camera asked as its box asks it, nothing hidden by distance.
    private static void PanelEdgeSources()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string relative) => File.Exists(Path.Combine(root, relative)) ? Code(File.ReadAllText(Path.Combine(root, relative))) : string.Empty;

        string job = Read(@"Jobs\JobPanel.cs");
        Check(job.Contains("_panel.KeepInView = true;") && !job.Contains("Fit(") && !job.Contains("Vector3.up * 2.1f") && !job.Contains("PlayerController player"),
            "panel edge sources: every job's panel is kept in the picture, and none is moved over the player's head by the old behind-the-camera test");
        string panel = Read(@"Core\GameUi\WorldPanel.cs"), view = Read(@"Core\GameUi\WorldPanel.View.cs");
        Check(panel.Contains("return KeepInView && InView(at, out Vector3 shown) ? shown : at;") && panel.Contains("Home() + _shift : Home();"),
            "panel edge sources: the point the game's box is given every frame is the edge point for a place out of the picture");
        Check(view.Contains("GameManager.GetMainCamera()") && view.Contains("PanelEdgeRules.Edge(") && view.Contains("camera.ViewportToWorldPoint(")
              && !view.Contains("FindObject") && !view.Contains("new Vector3[") && !view.Contains("GetComponent"),
            "panel edge sources: the edge is worked out on the camera OverlayUI.Update projects with, with nothing searched or allocated");
        string spills = Read(@"Jobs\Kinds\CleanerJob.Spills.cs");
        Check(!spills.Contains("PatchSeen") && spills.Contains("_panel.Offer(_panelFar, _panelLeft, left, _panel.White, ButtonMop, false, null, false);"),
            "panel edge sources: the cleaner's nearest spill has its panel however far it is, as the stocker's shelf has");
        string stocker = Read(@"Jobs\Kinds\StockerJob.cs");
        Check(stocker.Contains("\"Jobs check (stocker shelf): the shelf to stock is \"") && stocker.Contains("_panel.AtEdge") && stocker.Contains("\"Jobs check (stocker shelf): no shelf of \""),
            "panel edge sources: the stocker writes one self-check line a shelf, with the panel up, at the edge or refused, and one when no shelf is found");
    }
}
