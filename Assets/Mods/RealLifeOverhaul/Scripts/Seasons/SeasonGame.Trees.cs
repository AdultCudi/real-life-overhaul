using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The trees' leaves through the year. The game's trees (TreeController.AllTrees, no search) draw their leaves with a
    /// handful of shared materials of its wind shader, coloured by "_MainColor". Those materials are found by walking the
    /// trees a few hundred a second (a few milliseconds' work in all, spread out), and each is coloured as its own colour
    /// times the season's tint: in memory only, never saved, and put back exactly. A tint is written only when it changes,
    /// a few times a game day at most, so nothing runs a frame. Trunks, people and cars are on other materials.
    /// </summary>
    public static partial class SeasonGame
    {
        private const string LeafShader = "VegetationShaders/VegetationWindShader";
        private const int TreesASecond = 300;   // trees walked a second for their leaf materials, until all are known

        private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
        private static readonly List<Material> Leaves = new List<Material>(16);
        private static readonly List<Color> LeavesOwn = new List<Color>(16);
        private static readonly List<Renderer> TreeParts = new List<Renderer>(16);
        private static readonly List<Material> PartPaints = new List<Material>(4);
        private static readonly HashSet<Material> NotLeaves = new HashSet<Material>();   // trunks and the rest: a shader's name is read once each
        private static int _treesWalked;
        private static Tint _tint = new Tint(1f, 1f, 1f);
        private static bool _tinted;

        /// <summary>The leaf materials found so far, and the trees walked of the city's.</summary>
        public static int LeafMaterials => Leaves.Count;
        public static int TreesWalked => _treesWalked;
        public static int TreeCount => TreeController.AllTrees.Count;
        public static Tint TreeTint => _tinted ? _tint : new Tint(1f, 1f, 1f);

        /// <summary>
        /// Every leaf material the game's own colour times <paramref name="tint"/>, the game's own for a plain tint. Walks
        /// the next few hundred trees for leaf materials not met yet; a new one takes the tint at once.
        /// </summary>
        public static void Trees(Tint tint)
        {
            int found = Leaves.Count;
            WalkTrees();
            bool change = !_tinted || !tint.Same(_tint);
            if (tint.Plain)
            {
                if (_tinted)
                {
                    RestoreTrees();
                }
                return;
            }
            for (int i = change ? 0 : found; i < Leaves.Count; i++)
            {
                if (Leaves[i] != null)
                {
                    Color own = LeavesOwn[i];
                    Leaves[i].SetColor(MainColorId, new Color(own.r * tint.R, own.g * tint.G, own.b * tint.B, own.a));
                }
            }
            _tint = tint;
            _tinted = true;
        }

        /// <summary>Every leaf material's own colour back. Safe at any time; the materials found are kept for the city.</summary>
        public static void RestoreTrees()
        {
            if (!_tinted)
            {
                return;
            }
            for (int i = 0; i < Leaves.Count; i++)
            {
                if (Leaves[i] != null)
                {
                    Leaves[i].SetColor(MainColorId, LeavesOwn[i]);
                }
            }
            _tinted = false;
            _tint = new Tint(1f, 1f, 1f);
        }

        // The next trees' renderers' materials, without allocating: a leaf material of the game's wind shader is kept once.
        private static void WalkTrees()
        {
            List<TreeController> trees = TreeController.AllTrees;
            int until = Mathf.Min(trees.Count, _treesWalked + TreesASecond);
            for (; _treesWalked < until; _treesWalked++)
            {
                TreeController tree = trees[_treesWalked];
                if (tree == null)
                {
                    continue;
                }
                tree.GetComponentsInChildren(true, TreeParts);
                for (int r = 0; r < TreeParts.Count; r++)
                {
                    if (TreeParts[r] is ParticleSystemRenderer)
                    {
                        continue;   // the game's own falling leaves keep their colour
                    }
                    TreeParts[r].GetSharedMaterials(PartPaints);
                    for (int m = 0; m < PartPaints.Count; m++)
                    {
                        Material paint = PartPaints[m];
                        if (paint == null || NotLeaves.Contains(paint) || Leaves.Contains(paint))
                        {
                            continue;
                        }
                        if (paint.shader != null && paint.HasProperty(MainColorId) && paint.shader.name == LeafShader)
                        {
                            Leaves.Add(paint);
                            LeavesOwn.Add(paint.GetColor(MainColorId));
                        }
                        else
                        {
                            NotLeaves.Add(paint);
                        }
                    }
                }
            }
        }

        /// <summary>The city is unloading: the leaves put back and forgotten (the next city has its own).</summary>
        private static void ResetTrees()
        {
            RestoreTrees();
            Leaves.Clear();
            LeavesOwn.Clear();
            NotLeaves.Clear();
            _treesWalked = 0;
        }
    }
}
