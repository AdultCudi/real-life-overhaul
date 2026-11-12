namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A mouse press on a kit target, kept by what the target is (its layer, command and argument) and not by the pooled
    /// object that showed it or its number in the scene: a layout between the pointer going down and coming up renumbers
    /// the targets and hands the pooled objects to other ones, which dropped the click or sent it to a neighbour. The
    /// press goes to the target the pointer went down on, once, when the pointer comes up still over it. Game-free, so the
    /// offline checks drive it. Cost: a few comparisons on a press and a release, nothing in a frame.
    /// </summary>
    public sealed class UiPress
    {
        private bool _armed;
        private bool _known;
        private bool _dragged;
        private int _command;
        private int _arg;
        private byte _layer;

        /// <summary>The pointer is down on a target and has not come up.</summary>
        public bool Armed => _armed;

        /// <summary>The pointer went down on a node of the scene (-1 or one outside it presses nothing).</summary>
        public void Down(UiScene scene, int node)
        {
            _dragged = false;
            _armed = scene != null && node >= 0 && node < scene.NodeCount;
            _known = _armed;
            if (_armed)
            {
                _command = scene.Nodes[node].Command;
                _arg = scene.Nodes[node].Arg;
                _layer = scene.Nodes[node].Layer;
            }
        }

        /// <summary>The press became a drag on a screen that takes drags: it is no click.</summary>
        public void Dragged()
        {
            _dragged = true;
        }

        /// <summary>Forgets the press, as a view hidden mid press does.</summary>
        public void Cancel()
        {
            _armed = false;
        }

        /// <summary>
        /// The number the pressed target has in this scene, or -1 when it is gone. It is still named after the pointer came up, until the
        /// next press: the engine tells the up before the end of a drag, and a drag that lost its target then was never let go of.
        /// </summary>
        public int Current(UiScene scene) => _known && scene != null ? Find(scene, _command, _arg, _layer) : -1;

        /// <summary>
        /// The pointer came up at a place of the layout (px). The node to press, or -1: none when it was not down on a target,
        /// became a drag, the target is gone from the scene or the pointer is no longer over it. Spent once told.
        /// </summary>
        public int Up(UiScene scene, float x, float y)
        {
            bool was = _armed;
            int node = Current(scene);
            bool dragged = _dragged;
            _armed = false;
            _dragged = false;
            Why = !was ? NoPress : dragged ? "it became a drag" : node < 0 ? "the target is gone from the layout" : !Over(scene, node, x, y) ? "the pointer left the target" : Pressed;
            return Why == Pressed ? node : -1;
        }

        public const string Pressed = "pressed";
        private const string NoPress = "no press was down";

        /// <summary>Why the last up pressed nothing (or Pressed), for the press trace.</summary>
        public string Why { get; private set; } = NoPress;

        /// <summary>
        /// The target under a place of the layout (px), read from the scene itself: the one on the topmost layer, and on it the one
        /// drawn last, or -1. The press is told from where the pointer is, never from the object the engine's last raycast named.
        /// </summary>
        public static int At(UiScene scene, float x, float y)
        {
            int best = -1;
            long rank = -1;
            for (int i = 0; scene != null && i < scene.NodeCount; i++)
            {
                if (!Over(scene, i, x, y))
                {
                    continue;
                }
                long r = scene.Nodes[i].Layer * 100000000L + (scene.Nodes[i].Face >= 0 ? scene.Nodes[i].Face : i);
                if (r >= rank)
                {
                    rank = r;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The node of a layer that runs a command with an argument, or -1.</summary>
        public static int Find(UiScene scene, int command, int arg, int layer)
        {
            for (int i = 0; i < scene.NodeCount; i++)
            {
                ref UiNode n = ref scene.Nodes[i];
                if (n.Command == command && n.Arg == arg && n.Layer == layer)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Whether a place of the layout is inside a node's box, and inside the scroll region it stands in.</summary>
        public static bool Over(UiScene scene, int node, float px, float py)
        {
            scene.NodeBox(node, out float x, out float y, out float w, out float h);
            if (px < x || px > x + w || py < y || py > y + h)
            {
                return false;
            }
            short clip = scene.Nodes[node].Clip;
            if (clip >= 0)
            {
                ref UiClip c = ref scene.Clips[clip];
                return px >= c.X && px <= c.X + c.W && py >= c.Y && py <= c.Y + c.H;
            }
            return true;
        }
    }
}
