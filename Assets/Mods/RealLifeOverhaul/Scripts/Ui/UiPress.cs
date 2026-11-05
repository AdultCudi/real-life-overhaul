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

        /// <summary>The number the pressed target has in this scene, or -1 when it is gone.</summary>
        public int Current(UiScene scene) => _armed && scene != null ? Find(scene, _command, _arg, _layer) : -1;

        /// <summary>
        /// The pointer came up at a place of the layout (px). The node to press, or -1: none when it was not down on a target,
        /// became a drag, the target is gone from the scene or the pointer is no longer over it. Spent once told.
        /// </summary>
        public int Up(UiScene scene, float x, float y)
        {
            int node = Current(scene);
            bool dragged = _dragged;
            _armed = false;
            _dragged = false;
            return node >= 0 && !dragged && Over(scene, node, x, y) ? node : -1;
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
